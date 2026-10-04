using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using DSPRE.Avalonia.Models;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Trainers
{
    /// <summary>
    /// Avalonia port of the WinForms <c>TrainerFlagBulkEditor</c>: bulk-edit trainer AI flags and the
    /// double-battle flag, either by selecting trainers and toggling flags for all of them at once
    /// (By Trainer), or by picking one flag and checking/unchecking it per trainer (By Flag).
    /// Choose Items/Choose Moves aren't included: they control the trainerParty file's binary layout,
    /// not just a flag, so editing them here without touching that file corrupts the party data.
    /// </summary>
    public class TrainerFlagBulkEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        public static readonly string[] FlagNames =
        {
            "AI: Basic", "AI: Evaluate Attack", "AI: Expert", "AI: Setup", "AI: Risky",
            "AI: Prioritize Extremes", "AI: Baton Pass", "AI: Tag Strategy", "AI: Check HP",
            "AI: Weather", "AI: Harassment",
            "Double Battle"
        };
        private const int AI_FLAG_COUNT = TrainerProperties.AI_COUNT;

        private readonly string[] _trainerNames;
        private readonly string[] _trainerClassNames;
        private readonly int _trainerCount;
        private readonly Dictionary<int, TrainerProperties> _trainerData = new();
        private readonly HashSet<int> _selectedTrainerIds = new();
        private bool _suppressTreeEvents;
        private bool _suppressChecklistApply;
        private bool _isDirty;

        public ObservableCollection<TrainerFlagGroupNode> Tree { get; } = new();
        public ObservableCollection<FlagChecklistItem> FlagChecklist { get; } = new();
        public string[] FlagNamesList => FlagNames;

        public void SetMode(bool byFlag) => IsByFlagMode = byFlag;

        public bool IsByTrainerMode => !IsByFlagMode;
        private bool _isByFlagMode;
        public bool IsByFlagMode
        {
            get => _isByFlagMode;
            set
            {
                if (!Set(ref _isByFlagMode, value)) return;
                OnPropertyChanged(nameof(IsByTrainerMode));
                OnPropertyChanged(nameof(SelectAllLabel));
                OnPropertyChanged(nameof(SelectNoneLabel));
                RebuildTree();
                if (!value) RefreshFlagChecklistFromSelection();
                UpdateStatus();
            }
        }

        public string SelectAllLabel => IsByFlagMode ? "Enable All" : "Select All";
        public string SelectNoneLabel => IsByFlagMode ? "Disable All" : "Select None";

        private int _currentFlagIndex;
        public int CurrentFlagIndex
        {
            get => _currentFlagIndex;
            set
            {
                if (!Set(ref _currentFlagIndex, value)) return;
                if (IsByFlagMode) { RebuildTree(); UpdateStatus(); }
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
        public string UnsavedChangesDescription => "Trainer Flag Bulk Editor";
        public void SaveChanges() => SaveAllChanges();
        public void DiscardChanges()
        {
            if (_isDirty)
            {
                LoadAllTrainerData();
                RebuildTree();
                RefreshFlagChecklistFromSelection();
            }
            _isDirty = false;
            ResetUndo();
            OnPropertyChanged(nameof(HasUnsavedChanges));
            UpdateStatus();
        }

        // ── Undo / redo: every trainer's flags, one bit each ──
        private DSPRE.Avalonia.ByteStateUndo _undo;
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();
        private void RaiseUndo() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private byte[] TakeState()
        {
            var state = new byte[_trainerCount * 2];
            for (int i = 0; i < _trainerCount; i++)
            {
                int bits = 0;
                for (int f = 0; f < FlagNames.Length; f++) if (GetFlag(_trainerData[i], f)) bits |= 1 << f;
                state[i * 2] = (byte)bits;
                state[i * 2 + 1] = (byte)(bits >> 8);
            }
            return state;
        }

        private void ApplyState(byte[] state)
        {
            for (int i = 0; i < _trainerCount; i++)
            {
                int bits = state[i * 2] | state[i * 2 + 1] << 8;
                for (int f = 0; f < FlagNames.Length; f++) SetFlag(_trainerData[i], f, (bits & (1 << f)) != 0);
            }
            RebuildTree();
            RefreshFlagChecklistFromSelection();
            RecountDirty();
        }

        private void ResetUndo() { _undo = new DSPRE.Avalonia.ByteStateUndo(TakeState, ApplyState, RaiseUndo); RaiseUndo(); }

        // Unsaved while any trainer's flags differ from what was last read or saved.
        private void RecountDirty()
        {
            _isDirty = _trainerData.Any(kv => !SnapshotFlags(kv.Value).SequenceEqual(_loadedFlags[kv.Key]));
            OnPropertyChanged(nameof(HasUnsavedChanges));
            UpdateStatus();
        }

        private void Edited()
        {
            _undo?.Record();
            RecountDirty();
        }

        public TrainerFlagBulkEditorViewModel()
        {
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.trainerProperties });

            _trainerNames = DSPRE.TrainerNames.GetAll();
            _trainerClassNames = GetTrainerClassNames();
            _trainerCount = Filesystem.GetTrainerPropertiesCount();

            LoadAllTrainerData();

            foreach (var name in FlagNames)
                FlagChecklist.Add(new FlagChecklistItem { Index = FlagChecklist.Count, Name = name });

            RebuildTree();
            RefreshFlagChecklistFromSelection();
            ResetUndo();
            UpdateStatus();
        }

        private void LoadAllTrainerData()
        {
            string dir = gameDirs[DirNames.trainerProperties].unpackedDir;
            var source = FromSource ? HgEngine.HgEngineTrainerSource.LoadAll() : null;
            _sourceAi.Clear();
            _sourceBattle.Clear();
            for (int i = 0; i < _trainerCount; i++)
            {
                using var fs = new FileStream(Path.Combine(dir, i.ToString("D4")), FileMode.Open);
                _trainerData[i] = new TrainerProperties((ushort)i, fs);
                if (source != null) ReadSource(i, i < source.Count ? source[i] : (HgEngine.HgEngineSourceBlock?)null);
                _loadedFlags[i] = SnapshotFlags(_trainerData[i]);
            }
        }

        // ── hg-engine: data/Trainers.c ──────────────────────────────────────
        private static bool FromSource => HgEngine.HgEngineProject.IsActive;
        private const string TrainerDataHeader = "include/trainer_data.h";
        private static readonly HgEngine.FieldPathSegment[] AiPath = { HgEngine.FieldPathSegment.Field("data"), HgEngine.FieldPathSegment.Field("aiFlags") };
        private static readonly HgEngine.FieldPathSegment[] BattlePath = { HgEngine.FieldPathSegment.Field("data"), HgEngine.FieldPathSegment.Field("battleType") };
        // The source's own values; a trainer missing here can't be saved.
        private readonly Dictionary<int, int> _sourceAi = new(), _sourceBattle = new();

        // The checkout's F_ bits 0-10 are the ones these boxes show; higher ones (roaming, Safari) are kept as they are.
        private void ReadSource(int id, HgEngine.HgEngineSourceBlock? entry)
        {
            if (entry is not { } block || !block.TryGetFlagsValue(AiPath, TrainerDataHeader, out int ai)) return;
            int battle = block.TryGetSymbol(BattlePath, TrainerDataHeader, out int b) ? b : block.TryGetRaw(BattlePath, out _) ? -1 : 0;
            if (battle < 0) return;
            _sourceAi[id] = ai;
            _sourceBattle[id] = battle;
            var tp = _trainerData[id];
            for (int f = 0; f < AI_FLAG_COUNT; f++) tp.AI[f] = (ai & (1 << f)) != 0;
            tp.doubleBattle = battle != 0;
        }

        private async System.Threading.Tasks.Task SaveSourceAsync()
        {
            var writes = new List<(int Id, List<HgEngine.HgEngineFieldWrite> Fields, bool[] Now)>();
            var symbols = HgEngine.HgEngineSymbolTable.Load(TrainerDataHeader);
            foreach (var (id, tp) in _trainerData)
            {
                bool[] loaded = _loadedFlags[id], now = SnapshotFlags(tp);
                if (loaded.SequenceEqual(now)) continue;
                if (!_sourceAi.TryGetValue(id, out int ai))
                {
                    await DialogHelper.ShowError($"Trainer {id}'s flags couldn't be read from Trainers.c, so nothing was saved.", "Trainer Flag Bulk Editor");
                    return;
                }
                for (int f = 0; f < AI_FLAG_COUNT; f++)
                    if (now[f] != loaded[f]) ai = now[f] ? ai | (1 << f) : ai & ~(1 << f);
                var fields = new List<HgEngine.HgEngineFieldWrite>
                {
                    new(AiPath, symbols?.TryGetFlagsExpression(ai, "F_", out string expr) == true ? expr : ai.ToString()),
                };
                if (now[AI_FLAG_COUNT] != loaded[AI_FLAG_COUNT])
                    fields.Add(new(BattlePath, now[AI_FLAG_COUNT] ? "DOUBLE_BATTLE" : "SINGLE_BATTLE"));
                writes.Add((id, fields, now));
            }

            var (saved, error) = await HgEngineSave.RunAsync(() =>
            {
                foreach (var (id, fields, _) in writes)
                {
                    if (!HgEngine.HgEngineWriter.TryWriteFields(HgEngine.HgEngineDomain.Trainers, id, fields, out var unresolved, out string e, allOrNothing: true))
                        return $"Trainer {id}: {e}";
                    if (unresolved.Count > 0) return $"Trainer {id}: Trainers.c has no {string.Join(", ", unresolved)}.";
                }
                return null;
            });
            if (!saved) { if (error != null) await DialogHelper.ShowError("Not everything was saved:\n" + error, "Trainer Flag Bulk Editor"); return; }

            var source = HgEngine.HgEngineTrainerSource.LoadAll();
            foreach (var (id, _, now) in writes)
            {
                if (id < source.Count) ReadSource(id, source[id]);
                _loadedFlags[id] = SnapshotFlags(_trainerData[id]);
            }
            _isDirty = false;
            _undo?.MarkSaved();
            SaveNotice.Saved(UnsavedChangesDescription);
            OnPropertyChanged(nameof(HasUnsavedChanges));
            UpdateStatus($"Saved {writes.Count} trainers into Trainers.c.");
        }

        // Flags as last read or saved, so a save only touches the bits the user changed.
        private readonly Dictionary<int, bool[]> _loadedFlags = new();

        private bool[] SnapshotFlags(TrainerProperties tp)
        {
            var flags = new bool[FlagNames.Length];
            for (int f = 0; f < flags.Length; f++) flags[f] = GetFlag(tp, f);
            return flags;
        }

        private bool GetFlag(TrainerProperties tp, int flagIndex) =>
            flagIndex < AI_FLAG_COUNT ? tp.AI[flagIndex] : tp.doubleBattle;

        private void SetFlag(TrainerProperties tp, int flagIndex, bool value)
        {
            if (flagIndex < AI_FLAG_COUNT) tp.AI[flagIndex] = value;
            else tp.doubleBattle = value;
        }

        private string TrainerLabel(int id) =>
            id >= 0 && id < _trainerNames.Length ? _trainerNames[id] : $"[{id:D2}] ???";

        private string ClassLabel(byte classId) =>
            classId < _trainerClassNames.Length ? _trainerClassNames[classId] : $"Class {classId}";

        // ── Tree building ───────────────────────────────────────────────────
        private void RebuildTree()
        {
            _suppressTreeEvents = true;
            Tree.Clear();

            string filter = FilterText?.Trim();
            bool hasFilter = !string.IsNullOrEmpty(filter);

            var byClass = new SortedDictionary<byte, List<int>>();
            for (int i = 0; i < _trainerCount; i++)
            {
                byte classId = _trainerData[i].trainerClass;
                if (!byClass.TryGetValue(classId, out var list)) byClass[classId] = list = new List<int>();
                list.Add(i);
            }

            foreach (var (classId, memberIds) in byClass)
            {
                var matching = hasFilter
                    ? memberIds.Where(id => SearchMatch.Contains(TrainerLabel(id), filter)).ToList()
                    : memberIds;
                if (matching.Count == 0) continue;

                var group = new TrainerFlagGroupNode { ClassId = classId, OnCheckedChanged = OnGroupChecked };
                foreach (var id in matching)
                {
                    var leaf = new TrainerFlagLeafNode
                    {
                        TrainerId = id,
                        DisplayName = TrainerLabel(id),
                        OnCheckedChanged = OnLeafChecked,
                    };
                    leaf.SetCheckedSilent(IsByFlagMode ? GetFlag(_trainerData[id], CurrentFlagIndex) : _selectedTrainerIds.Contains(id));
                    group.Children.Add(leaf);
                }
                UpdateGroupDisplay(group);
                Tree.Add(group);
            }

            _suppressTreeEvents = false;
        }

        private void UpdateGroupDisplay(TrainerFlagGroupNode group)
        {
            int total = group.Children.Count;
            int checkedCount = group.Children.Count(c => c.IsChecked);
            group.DisplayName = $"{ClassLabel(group.ClassId)} [{checkedCount}/{total}]";
            group.SetCheckedSilent(total > 0 && checkedCount == total);
        }

        private void OnLeafChecked(TrainerFlagLeafNode leaf)
        {
            if (_suppressTreeEvents) return;

            ApplyLeafCheckSideEffect(leaf.TrainerId, leaf.IsChecked);

            var group = Tree.FirstOrDefault(g => g.Children.Contains(leaf));
            if (group != null) UpdateGroupDisplay(group);

            if (IsByTrainerMode) RefreshFlagChecklistFromSelection();
            if (IsByFlagMode) Edited(); else UpdateStatus();
        }

        private void OnGroupChecked(TrainerFlagGroupNode group)
        {
            if (_suppressTreeEvents) return;

            _suppressTreeEvents = true;
            foreach (var child in group.Children)
            {
                child.SetCheckedSilent(group.IsChecked);
                ApplyLeafCheckSideEffect(child.TrainerId, group.IsChecked);
            }
            _suppressTreeEvents = false;
            UpdateGroupDisplay(group);

            if (IsByTrainerMode) RefreshFlagChecklistFromSelection();
            if (IsByFlagMode) Edited(); else UpdateStatus();
        }

        private void ApplyLeafCheckSideEffect(int trainerId, bool isChecked)
        {
            if (IsByTrainerMode)
            {
                if (isChecked) _selectedTrainerIds.Add(trainerId);
                else _selectedTrainerIds.Remove(trainerId);
            }
            else
            {
                SetFlagForTrainer(trainerId, CurrentFlagIndex, isChecked);
            }
        }

        private void SetFlagForTrainer(int trainerId, int flagIndex, bool enabled)
        {
            var tp = _trainerData[trainerId];
            if (GetFlag(tp, flagIndex) == enabled) return;
            SetFlag(tp, flagIndex, enabled);
            _isDirty = true;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        public void SetAllVisibleLeavesChecked(bool value)
        {
            _suppressTreeEvents = true;
            foreach (var group in Tree)
            {
                foreach (var leaf in group.Children)
                {
                    leaf.SetCheckedSilent(value);
                    ApplyLeafCheckSideEffect(leaf.TrainerId, value);
                }
                UpdateGroupDisplay(group);
            }
            _suppressTreeEvents = false;

            if (IsByTrainerMode) RefreshFlagChecklistFromSelection();
            if (IsByFlagMode) Edited(); else UpdateStatus();
        }

        // ── Right-hand flag checklist (By Trainer mode) ────────────────────
        private void RefreshFlagChecklistFromSelection()
        {
            for (int f = 0; f < FlagChecklist.Count; f++)
            {
                bool? state;
                if (_selectedTrainerIds.Count == 0)
                {
                    state = false;
                }
                else
                {
                    int haveCount = _selectedTrainerIds.Count(id => GetFlag(_trainerData[id], f));
                    state = haveCount == 0 ? false : haveCount == _selectedTrainerIds.Count ? true : (bool?)null;
                }
                FlagChecklist[f].SetChecked(state);
            }
        }

        public void ToggleFlagForSelection(int flagIndex)
        {
            if (_selectedTrainerIds.Count == 0)
            {
                AppMessages.Info("Select at least one trainer on the left first.", "No Selection");
                return;
            }

            bool enable = FlagChecklist[flagIndex].IsChecked != true;
            foreach (var id in _selectedTrainerIds)
                SetFlagForTrainer(id, flagIndex, enable);

            RefreshFlagChecklistFromSelection();
            Edited();
        }

        // ── Save ─────────────────────────────────────────────────────────
        public void SaveAllChanges()
        {
            if (FromSource) { _ = SaveSourceAsync(); return; }
            string dir = gameDirs[DirNames.trainerProperties].unpackedDir;
            foreach (var (id, tp) in _trainerData.ToList())
            {
                bool[] loaded = _loadedFlags[id];
                bool[] current = SnapshotFlags(tp);
                if (loaded.SequenceEqual(current)) continue;

                // Other editors may have saved this trainer since it was read here.
                string path = Path.Combine(dir, id.ToString("D4"));
                TrainerProperties onDisk;
                using (var fs = new FileStream(path, FileMode.Open))
                    onDisk = new TrainerProperties((ushort)id, fs);
                for (int f = 0; f < current.Length; f++)
                    if (current[f] != loaded[f]) SetFlag(onDisk, f, current[f]);
                TrainerRecords.SaveProperties(id, onDisk);

                _trainerData[id] = onDisk;
                _loadedFlags[id] = SnapshotFlags(onDisk);
            }

            _isDirty = false;
            _undo?.MarkSaved();
            SaveNotice.Saved(UnsavedChangesDescription);
            OnPropertyChanged(nameof(HasUnsavedChanges));
            UpdateStatus("All trainer flag changes have been saved.");
        }

        private void UpdateStatus(string message = null)
        {
            if (message != null) { StatusText = message; return; }

            if (IsByTrainerMode)
            {
                StatusText = $"{_trainerCount} trainers in {Tree.Count} classes. {_selectedTrainerIds.Count} selected." +
                    (_isDirty ? " [Unsaved Changes]" : "");
            }
            else
            {
                string flagLabel = CurrentFlagIndex >= 0 && CurrentFlagIndex < FlagNames.Length ? FlagNames[CurrentFlagIndex] : "?";
                int enabledCount = _trainerData.Count(kvp => GetFlag(kvp.Value, CurrentFlagIndex));
                StatusText = $"{flagLabel}: {enabledCount} of {_trainerCount} trainers have it enabled." +
                    (_isDirty ? " [Unsaved Changes]" : "");
            }
        }
    }
}
