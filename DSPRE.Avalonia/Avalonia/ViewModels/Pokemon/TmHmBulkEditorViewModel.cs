using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using DSPRE.Avalonia.Models;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    public sealed class SpeciesFamily
    {
        public List<int> MemberIds;
    }

    /// <summary>
    /// Avalonia port of the WinForms <c>TmHmBulkEditor</c>: bulk-edit TM/HM compatibility across many
    /// Pokémon at once, either by selecting species and toggling machines for all of them (By Pokémon),
    /// or by picking one machine and checking/unchecking it per species (By TM/HM). Also carries the
    /// evolution-family "Sync" helper (union/intersection) and "Copy Compatibility To…".
    /// </summary>
    public class TmHmBulkEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private readonly string[] _pokemonNames;
        private readonly int _speciesCount;
        private readonly Dictionary<int, PokemonPersonalData> _personalData = new();
        private readonly List<SpeciesFamily> _families;
        private readonly HashSet<int> _selectedSpeciesIds = new();
        private bool _suppressTreeEvents;
        private bool _isDirty;

        public ObservableCollection<SpeciesFamilyTreeNode> Tree { get; } = new();
        public ObservableCollection<FlagChecklistItem> MachineChecklist { get; } = new();
        public string[] MachineNamesList { get; }

        public bool IsByPokemonMode => !IsByMachineMode;
        private bool _isByMachineMode;
        public bool IsByMachineMode
        {
            get => _isByMachineMode;
            set
            {
                if (!Set(ref _isByMachineMode, value)) return;
                OnPropertyChanged(nameof(IsByPokemonMode));
                OnPropertyChanged(nameof(SelectAllLabel));
                OnPropertyChanged(nameof(SelectNoneLabel));
                RebuildTree();
                if (!value) RefreshMachineChecklistFromSelection();
                UpdateStatus();
            }
        }

        public string SelectAllLabel => IsByMachineMode ? "Enable All" : "Select All";
        public string SelectNoneLabel => IsByMachineMode ? "Disable All" : "Select None";

        private readonly int[] _order = TMEditor.DisplayOrder();
        /// <summary>The machine the "By TM/HM" box shows; <see cref="CurrentMachineIndex"/> is its place in the list.</summary>
        private int CurrentMachine => _currentMachineIndex >= 0 && _currentMachineIndex < _order.Length ? _order[_currentMachineIndex] : -1;

        private int _currentMachineIndex;
        public int CurrentMachineIndex
        {
            get => _currentMachineIndex;
            set
            {
                if (!Set(ref _currentMachineIndex, value)) return;
                if (IsByMachineMode) { RebuildTree(); UpdateStatus(); }
            }
        }

        private string _filterText = "";
        public string FilterText
        {
            get => _filterText;
            set { if (Set(ref _filterText, value)) RebuildTree(); }
        }

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        // ── IEditorWithUnsavedChanges ──
        public bool HasUnsavedChanges => _isDirty;
        public string UnsavedChangesDescription => "TM/HM Bulk Editor";
        public void SaveChanges() => SaveAllChanges();
        public void DiscardChanges()
        {
            _carriedDirty = false;
            LoadAll();
            RebuildTree();
            if (IsByPokemonMode) RefreshMachineChecklistFromSelection();
            ResetUndo();
            UpdateStatus();
        }

        // ── Undo / redo: every species' machine set ──
        private DSPRE.Avalonia.ByteStateUndo _undo;
        private bool _pendingEdit;
        // Unsaved edits that predate the history, kept when another editor's save restarts it.
        private bool _carriedDirty;
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();
        private void RaiseUndo() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private byte[] TakeState() => DSPRE.Avalonia.UndoJson.Take(_personalData.OrderBy(kv => kv.Key).Select(kv => kv.Value.machines.ToArray()).ToArray());

        private void ApplyState(byte[] state)
        {
            var sets = DSPRE.Avalonia.UndoJson.Read<byte[][]>(state);
            foreach (var (id, data) in _personalData)
                if (id < sets.Length) data.machines = new SortedSet<byte>(sets[id]);
            RebuildTree();
            if (IsByPokemonMode) RefreshMachineChecklistFromSelection();
            RecountDirty();
            UpdateStatus();
        }

        private void ResetUndo()
        {
            _pendingEdit = false;
            _undo = new DSPRE.Avalonia.ByteStateUndo(TakeState, ApplyState, RaiseUndo);
            RecountDirty();
            RaiseUndo();
        }

        private void RecountDirty()
        {
            _isDirty = _carriedDirty || _undo?.IsDirty == true;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        // One history step per user action, however many species it touched.
        private void FlushEdit()
        {
            if (!_pendingEdit) return;
            _pendingEdit = false;
            _undo?.Record();
            RecountDirty();
        }

        public TmHmBulkEditorViewModel(string[] pokemonNames)
        {
            _pokemonNames = pokemonNames;
            MachineNamesList = BuildMachineLabels();

            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.personalPokeData, DirNames.evolutions });

            // DP's personalPokeData NARC has fewer files than the species-name text archive, which still
            // lists Platinum-introduced forms (501-507) DP never got data files for.
            _speciesCount = Math.Min(pokemonNames.Length, GetPersonalFilesCount());
            LoadAll();
            AppEvents.PersonalDataSaved += OnPersonalDataSaved;

            _families = BuildFamilies();

            for (int p = 0; p < MachineNamesList.Length; p++)
                MachineChecklist.Add(new FlagChecklistItem { Index = _order[p], Name = MachineNamesList[p] });

            RebuildTree();
            RefreshMachineChecklistFromSelection();
            ResetUndo();
            UpdateStatus();
        }

        private void LoadAll()
        {
            for (int i = 0; i < _speciesCount; i++)
            {
                _personalData[i] = new PokemonPersonalData(i);
                _savedFileMachines[i] = FileMachines(_personalData[i].machines);
            }
            LoadExtraMaskTms();
        }

        private static string[] BuildMachineLabels()
        {
            // Per-machine move names (which move TM/HM slot i actually teaches), not the raw move-name
            // list indexed by slot; those are unrelated (slot i's move ID is rarely i itself).
            string[] machineMoveNames = TMEditor.ReadMachineMoveNames();
            return TMEditor.DisplayOrder().Where(i => i < machineMoveNames.Length)
                .Select(i => $"{TMEditor.MachineLabelFromIndex(i)} - {machineMoveNames[i]}").ToArray();
        }

        // Species with no evolution link of their own become singleton families.
        private List<SpeciesFamily> BuildFamilies()
        {
            int evoCount = Math.Min(GetEvolutionFilesList().Length, _speciesCount);

            var parent = new int[_speciesCount];
            for (int i = 0; i < _speciesCount; i++) parent[i] = i;

            int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
            void Union(int a, int b) { int ra = Find(a), rb = Find(b); if (ra != rb) parent[ra] = rb; }

            for (int i = 0; i < evoCount; i++)
            {
                EvolutionFile evo;
                try { evo = new EvolutionFile(i); } catch { continue; }

                foreach (var entry in evo.data)
                    if (entry.method != EvolutionMethod.None && entry.target > 0 && entry.target < _speciesCount)
                        Union(i, entry.target);
            }

            var groups = new Dictionary<int, List<int>>();
            for (int i = 0; i < _speciesCount; i++)
            {
                int root = Find(i);
                if (!groups.TryGetValue(root, out var list)) groups[root] = list = new List<int>();
                list.Add(i);
            }

            return groups.Values
                .Select(members => { members.Sort(); return new SpeciesFamily { MemberIds = members }; })
                .OrderBy(f => f.MemberIds[0])
                .ToList();
        }

        private string SpeciesLabel(int id) =>
            id >= 0 && id < _pokemonNames.Length ? $"{id:0000} - {_pokemonNames[id]}" : $"{id:0000} - ???";

        // ── Tree building ───────────────────────────────────────────────────
        private void RebuildTree()
        {
            _suppressTreeEvents = true;
            Tree.Clear();

            string filter = FilterText?.Trim();
            bool hasFilter = !string.IsNullOrEmpty(filter);

            foreach (var fam in _families)
            {
                var matching = hasFilter
                    ? fam.MemberIds.Where(id => SearchMatch.Contains(SpeciesLabel(id), filter)).ToList()
                    : fam.MemberIds;
                if (matching.Count == 0) continue;

                if (fam.MemberIds.Count == 1)
                {
                    Tree.Add(MakeLeafNode(fam.MemberIds[0]));
                }
                else
                {
                    var group = new SpeciesGroupNode { FamilyRootId = fam.MemberIds[0], OnCheckedChanged = OnGroupChecked };
                    foreach (var id in matching) group.Children.Add(MakeLeafNode(id));
                    UpdateGroupDisplay(group);
                    Tree.Add(group);
                }
            }

            _suppressTreeEvents = false;
        }

        private SpeciesLeafNode MakeLeafNode(int id)
        {
            var leaf = new SpeciesLeafNode { SpeciesId = id, DisplayName = SpeciesLabel(id), OnCheckedChanged = OnLeafChecked };
            leaf.SetCheckedSilent(IsByMachineMode ? _personalData[id].machines.Contains((byte)CurrentMachine) : _selectedSpeciesIds.Contains(id));
            return leaf;
        }

        private void UpdateGroupDisplay(SpeciesGroupNode group)
        {
            int total = group.Children.Count;
            int checkedCount = group.Children.Count(c => c.IsChecked);
            group.DisplayName = $"{SpeciesLabel(group.FamilyRootId)} family [{checkedCount}/{total}]";
            group.SetCheckedSilent(total > 0 && checkedCount == total);
        }

        private void OnLeafChecked(SpeciesLeafNode leaf)
        {
            if (_suppressTreeEvents) return;

            ApplyLeafCheckSideEffect(leaf.SpeciesId, leaf.IsChecked);

            var group = Tree.OfType<SpeciesGroupNode>().FirstOrDefault(g => g.Children.Contains(leaf));
            if (group != null) UpdateGroupDisplay(group);

            if (IsByPokemonMode) RefreshMachineChecklistFromSelection();
            FlushEdit();
            UpdateStatus();
        }

        private void OnGroupChecked(SpeciesGroupNode group)
        {
            if (_suppressTreeEvents) return;

            _suppressTreeEvents = true;
            foreach (var child in group.Children)
            {
                child.SetCheckedSilent(group.IsChecked);
                ApplyLeafCheckSideEffect(child.SpeciesId, group.IsChecked);
            }
            _suppressTreeEvents = false;
            UpdateGroupDisplay(group);

            if (IsByPokemonMode) RefreshMachineChecklistFromSelection();
            FlushEdit();
            UpdateStatus();
        }

        private void ApplyLeafCheckSideEffect(int speciesId, bool isChecked)
        {
            if (IsByPokemonMode)
            {
                if (isChecked) _selectedSpeciesIds.Add(speciesId);
                else _selectedSpeciesIds.Remove(speciesId);
            }
            else
            {
                SetMachineCompat(speciesId, isChecked);
            }
        }

        private void SetMachineCompat(int speciesId, bool enabled)
        {
            var data = _personalData[speciesId];
            bool changed = enabled ? data.machines.Add((byte)CurrentMachine) : data.machines.Remove((byte)CurrentMachine);
            if (changed) _pendingEdit = true;
        }

        public void SetAllVisibleLeavesChecked(bool value)
        {
            _suppressTreeEvents = true;
            foreach (var node in Tree)
            {
                if (node is SpeciesGroupNode group)
                {
                    foreach (var leaf in group.Children)
                    {
                        leaf.SetCheckedSilent(value);
                        ApplyLeafCheckSideEffect(leaf.SpeciesId, value);
                    }
                    UpdateGroupDisplay(group);
                }
                else if (node is SpeciesLeafNode leaf)
                {
                    leaf.SetCheckedSilent(value);
                    ApplyLeafCheckSideEffect(leaf.SpeciesId, value);
                }
            }
            _suppressTreeEvents = false;

            if (IsByPokemonMode) RefreshMachineChecklistFromSelection();
            FlushEdit();
            UpdateStatus();
        }

        // ── Right-hand machine checklist (By Pokémon mode) ──────────────────
        private void RefreshMachineChecklistFromSelection()
        {
            foreach (var item in MachineChecklist)
            {
                int m = item.Index;
                bool? state;
                if (_selectedSpeciesIds.Count == 0)
                {
                    state = false;
                }
                else
                {
                    int haveCount = _selectedSpeciesIds.Count(id => _personalData[id].machines.Contains((byte)m));
                    state = haveCount == 0 ? false : haveCount == _selectedSpeciesIds.Count ? true : (bool?)null;
                }
                item.SetChecked(state);
            }
        }

        public void ToggleMachineForSelection(int machineIndex)
        {
            if (_selectedSpeciesIds.Count == 0)
            {
                AppMessages.Info("Select at least one Pokémon on the left first.", "No Selection");
                return;
            }

            bool enable = MachineChecklist.FirstOrDefault(c => c.Index == machineIndex)?.IsChecked != true;
            foreach (var id in _selectedSpeciesIds)
            {
                var data = _personalData[id];
                bool changed = enable ? data.machines.Add((byte)machineIndex) : data.machines.Remove((byte)machineIndex);
                if (changed) _pendingEdit = true;
            }
            FlushEdit();

            RefreshMachineChecklistFromSelection();
            UpdateStatus();
        }

        // ── Sync Family / Copy Compatibility ────────────────────────────────
        public IReadOnlyList<List<int>> FamilyGroups => _families.Select(f => f.MemberIds).ToList();
        public int SingleSelectedSpeciesId => _selectedSpeciesIds.Count == 1 ? _selectedSpeciesIds.First() : -1;
        public string GetSpeciesLabel(int id) => SpeciesLabel(id);

        public void SyncFamilies(bool union)
        {
            var touched = _families.Where(f => f.MemberIds.Count > 1 && f.MemberIds.Any(_selectedSpeciesIds.Contains)).ToList();
            if (touched.Count == 0)
            {
                AppMessages.Info("Select at least one Pokémon from a multi-member evolution family (in By Pokémon view) first.", "Sync Family");
                return;
            }

            foreach (var fam in touched)
            {
                if (union)
                {
                    var u = new SortedSet<byte>();
                    foreach (var id in fam.MemberIds) u.UnionWith(_personalData[id].machines);
                    foreach (var id in fam.MemberIds) _personalData[id].machines = new SortedSet<byte>(u);
                }
                else
                {
                    var inter = new SortedSet<byte>(_personalData[fam.MemberIds[0]].machines);
                    foreach (var id in fam.MemberIds.Skip(1)) inter.IntersectWith(_personalData[id].machines);
                    foreach (var id in fam.MemberIds) _personalData[id].machines = new SortedSet<byte>(inter);
                }
            }
            AfterBulkFamilyChange($"Synced {touched.Count} famil{(touched.Count == 1 ? "y" : "ies")} ({(union ? "Union" : "Intersection")}).");
        }

        public void CopyMachinesTo(int sourceId, IEnumerable<int> targetIds)
        {
            var sourceSet = new SortedSet<byte>(_personalData[sourceId].machines);
            var targets = targetIds.Where(id => id != sourceId).ToList();
            foreach (var id in targets) _personalData[id].machines = new SortedSet<byte>(sourceSet);
            AfterBulkFamilyChange($"Copied TM/HM compatibility from {SpeciesLabel(sourceId)} to {targets.Count} Pokémon.");
        }

        private void AfterBulkFamilyChange(string message)
        {
            _pendingEdit = true;
            FlushEdit();
            RebuildTree();
            if (IsByPokemonMode) RefreshMachineChecklistFromSelection();
            UpdateStatus(message);
        }

        // ── Save ─────────────────────────────────────────────────────────
        // PlatPatches' TM121+ live in a synthetic-overlay mask; they share the machine sets at indices 128+, which the
        // personal file never writes, and go to the mask on save.
        private HashSet<(int Row, int PersonalId)> _maskLoaded = new();

        private void LoadExtraMaskTms()
        {
            var t = PlatPatches.Tms();
            if (t == null) return;
            _maskLoaded = PlatPatches.Compatibility(t, _personalData.Keys, PlatPatches.PersonalMaskRows);
            foreach (var (row, id) in _maskLoaded) _personalData[id].machines.Add((byte)(TMEditor.VanillaMachineCount + row));
        }

        /// <summary>Writes only the TM121+ bits changed here, so edits saved elsewhere since opening survive.
        /// Returns the species it wrote.</summary>
        private HashSet<int> SaveExtraMaskTms()
        {
            var t = PlatPatches.Tms();
            if (t == null) return new HashSet<int>();
            var now = new HashSet<(int Row, int PersonalId)>();
            foreach (var (id, data) in _personalData)
                foreach (byte m in data.machines)
                    if (m >= FirstMaskMachine) now.Add((m - TMEditor.VanillaMachineCount, id));
            var changes = now.Except(_maskLoaded).Select(c => (c.Item1, c.Item2, true))
                .Concat(_maskLoaded.Except(now).Select(c => (c.Row, c.PersonalId, false))).ToList();
            if (changes.Count > 0) PlatPatches.SetCanLearn(t, changes);
            _maskLoaded = now;
            return changes.Select(c => c.Item2).ToHashSet();
        }

        // Machines below this are bits in the personal file; the rest live in the TM121+ mask.
        private static int FirstMaskMachine => TMEditor.VanillaMachineCount + PlatPatches.PersonalMaskRows;

        // The personal-file machine bits as last read or written, so a save applies only what changed here.
        private readonly Dictionary<int, SortedSet<byte>> _savedFileMachines = new();

        private static SortedSet<byte> FileMachines(IEnumerable<byte> machines) =>
            new SortedSet<byte>(machines.Where(m => m < FirstMaskMachine));

        private static void ApplyDelta(SortedSet<byte> target, SortedSet<byte> from, SortedSet<byte> to)
        {
            foreach (byte m in from) if (!to.Contains(m)) target.Remove(m);
            foreach (byte m in to) if (!from.Contains(m)) target.Add(m);
        }

        /// <summary>Another editor saved a species: take its machine changes while keeping the ones made here.</summary>
        private void OnPersonalDataSaved(object sender, int id)
        {
            if (ReferenceEquals(sender, this) || !_personalData.TryGetValue(id, out var data)) return;
            if (!global::Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            {
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPersonalDataSaved(sender, id));
                return;
            }
            try
            {
                var onDisk = FileMachines(new PokemonPersonalData(id).machines);
                ApplyDelta(data.machines, _savedFileMachines[id], onDisk);
                _savedFileMachines[id] = onDisk;

                var t = PlatPatches.Tms();
                if (t != null)
                {
                    var maskOnDisk = PlatPatches.Compatibility(t, new[] { id }, PlatPatches.PersonalMaskRows);
                    var maskWas = _maskLoaded.Where(c => c.PersonalId == id).ToHashSet();
                    foreach (var (row, _) in maskWas.Except(maskOnDisk)) data.machines.Remove((byte)(TMEditor.VanillaMachineCount + row));
                    foreach (var (row, _) in maskOnDisk.Except(maskWas)) data.machines.Add((byte)(TMEditor.VanillaMachineCount + row));
                    _maskLoaded.ExceptWith(maskWas);
                    _maskLoaded.UnionWith(maskOnDisk);
                }
            }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException)
            {
                AppLogger.Warn($"TM/HM Bulk Editor: could not re-read species {id}: {e.Message}");
                return;
            }
            _carriedDirty = _isDirty;
            RebuildTree();
            if (IsByPokemonMode) RefreshMachineChecklistFromSelection();
            ResetUndo();
            UpdateStatus();
        }

        /// <summary>Unsubscribes from app-wide events; call when the editor window closes.</summary>
        public void Detach() => AppEvents.PersonalDataSaved -= OnPersonalDataSaved;

        public void SaveAllChanges()
        {
            var written = new HashSet<int>();
            try
            {
                written.UnionWith(SaveExtraMaskTms());

                // Each file is re-read just before writing and only the machine bits changed here are applied,
                // so edits other editors saved since this one opened survive.
                foreach (var (id, data) in _personalData)
                {
                    var now = FileMachines(data.machines);
                    if (now.SetEquals(_savedFileMachines[id])) continue;
                    var fresh = new PokemonPersonalData(id);
                    ApplyDelta(fresh.machines, _savedFileMachines[id], now);
                    fresh.SaveToFileDefaultDir(id, false);
                    _savedFileMachines[id] = FileMachines(fresh.machines);
                    written.Add(id);
                }
            }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException || e is InvalidOperationException || e is ArgumentException)
            {
                foreach (int id in written) AppEvents.RaisePersonalDataSaved(this, id);
                _ = DialogHelper.ShowError("Not everything was saved: the TM/HM compatibility couldn't be written.\n" + e.Message, "TM/HM Bulk Editor");
                return;
            }
            foreach (int id in written) AppEvents.RaisePersonalDataSaved(this, id);

            _carriedDirty = false;
            _undo?.MarkSaved();
            RecountDirty();
            SaveNotice.Saved(UnsavedChangesDescription);
            OnPropertyChanged(nameof(HasUnsavedChanges));
            UpdateStatus("All TM/HM compatibility changes have been saved.");
        }

        private void UpdateStatus(string message = null)
        {
            if (message != null) { StatusText = message; return; }

            if (IsByPokemonMode)
            {
                StatusText = $"{_speciesCount} Pokémon in {_families.Count} evolution families. {_selectedSpeciesIds.Count} selected." +
                    (_isDirty ? " [Unsaved Changes]" : "");
            }
            else
            {
                string machineLabel = CurrentMachineIndex >= 0 && CurrentMachineIndex < MachineNamesList.Length
                    ? MachineNamesList[CurrentMachineIndex] : "?";
                int compatCount = _personalData.Count(kvp => kvp.Value.machines.Contains((byte)CurrentMachine));
                StatusText = $"{machineLabel}: {compatCount} of {_speciesCount} Pokémon compatible." +
                    (_isDirty ? " [Unsaved Changes]" : "");
            }
        }
    }
}
