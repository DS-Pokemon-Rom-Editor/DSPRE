using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DSPRE.ROMFiles;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>What the Wild Pokémon views and the map workspace use, for either game family.</summary>
    public interface IWildEditor : IEditorWithUnsavedChanges, IDetachable, INotifyPropertyChanged
    {
        int SelectedEncounterIndex { get; set; }
        Task SaveCommandAsync();
        void AddEncounterFile();
        Task RemoveLastEncounterFileAsync();
        Task RepairAllAsync();
        void RefreshSlotLabels();
        void ImportEncounterFile(string path);
        void ExportEncounterFile(string path);
    }

    /// <summary>
    /// The Wild Pokémon editor's file list, rates, water rows, undo and file management. Each family adds its
    /// walking and special rows.
    /// </summary>
    public abstract class WildEditorViewModel<TFile> : IWildEditor, ISupportsUndo
        where TFile : EncounterFile, new()
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        protected bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        protected const string EditorTitle = "Wild Pokémon editor";

        // ── Family hooks ──────────────────────────────────────────────────
        /// <summary>The file at this id, with <paramref name="error"/> naming a part that couldn't be read.</summary>
        protected abstract TFile LoadRecord(byte[] bytes, int id, out string error);
        protected abstract TFile Parse(Stream stream);
        /// <summary>The header's encounter file, or -1 when it has none; <paramref name="locationName"/> gets its location name index.</summary>
        protected abstract int EncounterFileOf(MapHeader header, out int locationName);
        protected abstract IEnumerable<ObservableCollection<WildEncounterRow>> EditableRows { get; }
        protected abstract bool SpeciesFeedsNotes(ObservableCollection<WildEncounterRow> rows);
        protected abstract void PopulateFamilyRows();
        protected abstract void WriteFamilyRowsToFile();
        protected abstract void SetNotes();
        protected abstract void CollectProblems(List<string> problems);
        /// <summary>Whether tab 2, Special, has anything the game can roll.</summary>
        protected abstract bool SpecialTabHasEncounters { get; }
        protected virtual bool ShowForms => false;
        /// <summary>The slot odds name for a rod, as Encounter slot odds lists it.</summary>
        protected virtual string RodOdds(string rod) => rod;
        protected virtual void RecordLoaded(string error) { }
        /// <summary>False stops the save before anything is written.</summary>
        protected virtual Task<bool> CanSaveAsync() => Task.FromResult(true);
        /// <summary>Writes anything the archive copy depends on; false stops the save.</summary>
        protected virtual Task<bool> WriteSourceAsync() => Task.FromResult(true);

        // ── IEditorWithUnsavedChanges ──────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty || _archive.IsDirty;
        public string UnsavedChangesDescription
            => Data.StagedArchive.Join($"Encounter file {SelectedEncounterIndex}", _dirty, _archive.Describe("encounter file", "encounter files"));
        void IEditorWithUnsavedChanges.SaveChanges() => DialogHelper.Watch(SaveCommandAsync());
        async Task<bool> IEditorWithUnsavedChanges.SaveChangesAsync()
        {
            await SaveCommandAsync();
            return !HasUnsavedChanges;
        }
        public void DiscardChanges()
        {
            // Added and removed files go with the open file's edits.
            _archive.Discard();
            BuildEncounterNameList();
            if (_selectedEncounterIndex >= EncounterNames.Count)
            {
                _selectedEncounterIndex = EncounterNames.Count - 1;
                OnPropertyChanged(nameof(SelectedEncounterIndex));
            }
            if (Current == null) { SetClean(); return; }
            try { LoadFile(_selectedEncounterIndex); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { SetClean(); }
        }

        // ── Name lists ────────────────────────────────────────────────────
        public ObservableCollection<string> PokemonNames { get; } = new();
        public ObservableCollection<string> EncounterNames { get; } = new();

        // ── Encounter selector ────────────────────────────────────────────
        private int _selectedEncounterIndex;
        public int SelectedEncounterIndex
        {
            get => _selectedEncounterIndex;
            set
            {
                if (RecordSwitchGuard.IsSnappingBack) return;
                if (value == _selectedEncounterIndex || value < 0 || value >= EncounterNames.Count) return;
                if (_dirty) { _ = ConfirmDiscardAsync(value); return; }
                _selectedEncounterIndex = value;
                OnPropertyChanged();
                LoadFile(value);
            }
        }

        // ── Encounter rates ───────────────────────────────────────────────
        private int _walkingRate;  public int WalkingRate  { get => _walkingRate;  set { if (Set(ref _walkingRate,  value) && !Loading) { Current.WalkingRate  = (byte)value; RateEdited(); } } }
        private int _surfRate;     public int SurfRate     { get => _surfRate;     set { if (Set(ref _surfRate,     value) && !Loading) { Current.SurfRate     = (byte)value; RateEdited(); } } }
        private int _oldRodRate;   public int OldRodRate   { get => _oldRodRate;   set { if (Set(ref _oldRodRate,   value) && !Loading) { Current.OldRodRate   = (byte)value; RateEdited(); } } }
        private int _goodRodRate;  public int GoodRodRate  { get => _goodRodRate;  set { if (Set(ref _goodRodRate,  value) && !Loading) { Current.GoodRodRate  = (byte)value; RateEdited(); } } }
        private int _superRodRate; public int SuperRodRate { get => _superRodRate; set { if (Set(ref _superRodRate, value) && !Loading) { Current.SuperRodRate = (byte)value; RateEdited(); } } }

        /// <summary>Rates decide which empty slots are warned about.</summary>
        protected void RateEdited() { SetDirty(); SetNotes(); }

        // ── Water encounters ──────────────────────────────────────────────
        public ObservableCollection<WildEncounterRow> SurfRows     { get; } = new();
        public ObservableCollection<WildEncounterRow> OldRodRows   { get; } = new();
        public ObservableCollection<WildEncounterRow> GoodRodRows  { get; } = new();
        public ObservableCollection<WildEncounterRow> SuperRodRows { get; } = new();

        // ── Title ─────────────────────────────────────────────────────────
        private string _title = EditorTitle;
        public string Title { get => _title; protected set => Set(ref _title, value); }

        // ── State ─────────────────────────────────────────────────────────
        protected TFile Current { get; private set; }
        // Added, removed and saved encounter files wait here until Save.
        private readonly Data.StagedArchive _archive = new(DirNames.Encounters);
        private readonly int _totalHeaders;
        /// <summary>True while rows are filled from the file, so setters don't count it as an edit.</summary>
        protected bool Loading { get; private set; }
        private bool _rowsHooked;
        private readonly PokemonIconCache _pokemonIcons = new();

        // ── Undo / redo: the encounter file's bytes ───────────────────────────
        // Grid edits live in the row view models until save, so a snapshot writes them into the file first.
        private ByteStateUndo _undo;
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();
        private void RaiseUndoState() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private byte[] Snapshot()
        {
            WriteRowsToFile();
            return Current.ToByteArray();
        }

        private void ApplyState(byte[] bytes)
        {
            Current = Parse(new MemoryStream(bytes));
            PopulateRows();
            _dirty = _undo.IsDirty;
            StagedChanged();
        }

        // Grid rows don't reach the dirty and undo path otherwise, so each row is subscribed once.
        private void HookRowsOnce()
        {
            if (_rowsHooked) return;
            _rowsHooked = true;
            foreach (ObservableCollection<WildEncounterRow> rows in EditableRows)
            {
                bool feedsNotes = SpeciesFeedsNotes(rows);
                foreach (WildEncounterRow row in rows)
                    row.PropertyChanged += (_, e) =>
                    {
                        if (Loading) return;
                        if (feedsNotes && e.PropertyName == nameof(WildEncounterRow.PokemonIndex)) SetNotes();
                        if (e.PropertyName != nameof(WildEncounterRow.PokemonIcon) && e.PropertyName != nameof(WildEncounterRow.Label)
                            && e.PropertyName != nameof(WildEncounterRow.Note) && e.PropertyName != nameof(WildEncounterRow.NoteTip)
                            && e.PropertyName != nameof(WildEncounterRow.Replacements) && e.PropertyName != nameof(WildEncounterRow.Warning)) SetDirty();
                    };
            }
        }

        // ── Constructors ──────────────────────────────────────────────────
        protected WildEditorViewModel(string[] pokemonNames, int totalHeaders)
        {
            _totalHeaders = totalHeaders;
            foreach (string n in pokemonNames) PokemonNames.Add(n);
        }

        /// <summary>Design preview only.</summary>
        protected WildEditorViewModel() { }

        /// <summary>The second half of each editor's <c>Open</c>: lists the files and loads the first one to show.</summary>
        protected void LoadFirst(int encToOpen)
        {
            SetMonIconsPalTableAddress();
            BuildEncounterNameList();
            int count = EncounterNames.Count;
            if (encToOpen < 0) encToOpen = FirstWithEncounters(count);
            if (encToOpen >= count) encToOpen = 0;
            _selectedEncounterIndex = encToOpen;
            LoadFile(encToOpen);
            // Only a loaded editor listens, so a failed open leaves nothing subscribed.
            AppEvents.NamesChanged += OnNamesChanged;
            EncounterSlotOdds.Saved += OnOddsSaved;
        }

        /// <summary>Changes made inside are not edits. The previous state comes back on dispose.</summary>
        protected IDisposable Quietly()
        {
            bool was = Loading;
            Loading = true;
            return new OnDispose(() => Loading = was);
        }

        /// <summary>Design preview only: sample values are never edits, and designer bindings can write back later.</summary>
        protected void StayQuiet() => Loading = true;

        private int _selectedTab;
        /// <summary>Kept when switching files, unless the new file has nothing on it.</summary>
        public int SelectedTab { get => _selectedTab; set => Set(ref _selectedTab, value); }

        // Tabs 0 to 2 are Walking, Water and Special; later tabs always count as filled.
        private bool TabHasEncounters(int tab) => tab switch
        {
            0 => Current.WalkingRate > 0,
            1 => Current.SurfRate > 0 || Current.OldRodRate > 0 || Current.GoodRodRate > 0 || Current.SuperRodRate > 0,
            2 => SpecialTabHasEncounters,
            _ => true,
        };

        private void PickTab()
        {
            if (Current == null || TabHasEncounters(SelectedTab)) return;
            for (int tab = 0; tab < 3; tab++)
                if (TabHasEncounters(tab)) { SelectedTab = tab; return; }
        }

        // Opened without a file in mind: start on the first file with encounters, since file 0 is empty.
        private int FirstWithEncounters(int count)
        {
            for (int id = 0; id < count; id++)
            {
                try { if (LoadRecord(_archive.Read(id), id, out _).HasEncounters) return id; }
                catch (IOException) { }   // a file that can't be read is skipped
            }
            return 0;
        }

        // ── Save ──────────────────────────────────────────────────────────
        public async Task SaveCommandAsync()
        {
            if (Current == null || !await CanSaveAsync()) return;
            List<string> problems = new List<string>();
            CollectProblems(problems);
            if (problems.Count > 0 && !await DialogHelper.AskYesNoAsync("These slots can be rolled but look wrong:\n" + WildEncounterChecks.Describe(problems)
                    + "\n\nSave anyway?", EditorTitle)) return;
            WriteRowsToFile();
            if (!await WriteSourceAsync()) return;
            try { _archive.Save(_selectedEncounterIndex, Current.ToByteArray()); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                CrashReporter.LogHandled(ex);
                await DialogHelper.ShowErrorAsync($"Encounter file {_selectedEncounterIndex} wasn't saved.\n{ex.Message}", EditorTitle);
                StagedChanged();
                return;
            }
            SetClean();
            SaveNotice.Saved(UnsavedChangesDescription);
            _undo?.MarkSaved();
            RaiseUndoState();
        }

        // ── Events ────────────────────────────────────────────────────────
        private void OnNamesChanged(object sender, EventArgs e)
        {
            Data.ListSync.Apply(PokemonNames, GetPokemonNames());
            RefreshNotes();
        }

        private void OnOddsSaved() => global::Avalonia.Threading.Dispatcher.UIThread.Post(RefreshSlotLabels);

        // The "Replaced by" tips carry species names; rebuilding them is not an edit.
        private void RefreshNotes()
        {
            if (Current == null) return;
            using (Quietly()) SetNotes();
        }

        public void Detach()
        {
            AppEvents.NamesChanged -= OnNamesChanged;
            EncounterSlotOdds.Saved -= OnOddsSaved;
            _pokemonIcons.Dispose();
        }

        // ── Dirty state ───────────────────────────────────────────────────
        protected void SetDirty()
        {
            if (Loading) return;
            _undo?.Record();
            _dirty = _undo == null || _undo.IsDirty;
            StagedChanged();
        }

        private void SetClean() { _dirty = false; StagedChanged(); }

        // Added, removed and repaired files keep the editor unsaved after the open file's edits are clean.
        private void StagedChanged()
        {
            Title = (HasUnsavedChanges ? "● " : "") + EditorTitle;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private async Task ConfirmDiscardAsync(int newId)
        {
            if (!await RecordSwitchGuard.ConfirmLeaveAsync(this, null, "encounter file")) { RecordSwitchGuard.SnapBack(() => _selectedEncounterIndex, v => _selectedEncounterIndex = v, () => OnPropertyChanged(nameof(SelectedEncounterIndex))); return; }
            _dirty = false;
            // Discarding can drop the file being switched to when it was added and not saved.
            if (newId >= EncounterNames.Count) newId = EncounterNames.Count - 1;
            _selectedEncounterIndex = newId;
            OnPropertyChanged(nameof(SelectedEncounterIndex));
            LoadFile(newId);
        }

        // ── File list ─────────────────────────────────────────────────────
        private void BuildEncounterNameList()
        {
            EncounterNames.Clear();
            Dictionary<int, List<string>> locationMap = new Dictionary<int, List<string>>();
            List<string> locationNames = GetLocationNames();
            for (ushort i = 0; i < _totalHeaders; i++)
            {
                int file = EncounterFileOf(MapHeader.GetMapHeader(i), out int locIdx);
                if (file < 0) continue;
                if (!locationMap.ContainsKey(file)) locationMap[file] = new List<string>();
                // Names come from the header's own location; the encounter file id is an unrelated number.
                locationMap[file].Add(locIdx < locationNames.Count ? locationNames[locIdx] : "Unknown");
            }
            for (int i = 0; i < _archive.Count; i++)
            {
                string label = locationMap.ContainsKey(i) ? string.Join(" + ", locationMap[i]) : "Unused";
                EncounterNames.Add($"[{i}] {label}");
            }
        }

        /// <summary>Headers whose wild Pokémon come from this encounter file, read fresh.</summary>
        private List<ushort> HeadersUsing(int file)
        {
            List<ushort> users = new List<ushort>();
            for (ushort i = 0; i < _totalHeaders; i++)
            {
                MapHeader h = MapHeader.GetMapHeader(i);
                if (h != null && EncounterFileOf(h, out _) == file) users.Add(i);
            }
            return users;
        }

        private void LoadFile(int id)
        {
            Current = LoadRecord(_archive.Read(id), id, out string loadError);
            RecordLoaded(loadError);
            PopulateRows();
            PickTab();
            SetClean();

            // The loaded state is this file's clean undo baseline.
            _undo = new ByteStateUndo(Snapshot, ApplyState, RaiseUndoState);
            RaiseUndoState();
        }

        // ── Rows ──────────────────────────────────────────────────────────
        /// <summary>Re-reads the slot chances into the row labels, for after Encounter slot odds saved. Keeps unsaved edits.</summary>
        public virtual void RefreshSlotLabels()
        {
            for (int i = 0; i < SurfRows.Count; i++) SurfRows[i].Label = EncounterSlotOdds.SlotLabel("Surfing", i);
            for (int i = 0; i < OldRodRows.Count; i++) OldRodRows[i].Label = EncounterSlotOdds.SlotLabel(RodOdds("Old Rod"), i);
            for (int i = 0; i < GoodRodRows.Count; i++) GoodRodRows[i].Label = EncounterSlotOdds.SlotLabel(RodOdds("Good Rod"), i);
            for (int i = 0; i < SuperRodRows.Count; i++) SuperRodRows[i].Label = EncounterSlotOdds.SlotLabel(RodOdds("Super Rod"), i);
        }

        private void PopulateRows()
        {
            Loading = true;
            WalkingRate  = Current.WalkingRate;
            SurfRate     = Current.SurfRate;
            OldRodRate   = Current.OldRodRate;
            GoodRodRate  = Current.GoodRodRate;
            SuperRodRate = Current.SuperRodRate;
            PopulateFamilyRows();
            SyncRows(SurfRows,     5, i => EncounterSlotOdds.SlotLabel("Surfing", i),            i => Current.SurfPokemon[i],     _ => 0, i => Current.SurfMinLevels[i],     i => Current.SurfMaxLevels[i],     true);
            SyncRows(OldRodRows,   5, i => EncounterSlotOdds.SlotLabel(RodOdds("Old Rod"), i),   i => Current.OldRodPokemon[i],   _ => 0, i => Current.OldRodMinLevels[i],   i => Current.OldRodMaxLevels[i],   true);
            SyncRows(GoodRodRows,  5, i => EncounterSlotOdds.SlotLabel(RodOdds("Good Rod"), i),  i => Current.GoodRodPokemon[i],  _ => 0, i => Current.GoodRodMinLevels[i],  i => Current.GoodRodMaxLevels[i],  true);
            SyncRows(SuperRodRows, 5, i => EncounterSlotOdds.SlotLabel(RodOdds("Super Rod"), i), i => Current.SuperRodPokemon[i], _ => 0, i => Current.SuperRodMinLevels[i], i => Current.SuperRodMaxLevels[i], true);
            SetNotes();
            Loading = false;
            HookRowsOnce();
        }

        protected void SyncRows(
            ObservableCollection<WildEncounterRow> rows, int count,
            Func<int, string> labelFn, Func<int, int> pokeFn, Func<int, int> lvlFn,
            Func<int, int> minFn, Func<int, int> maxFn, bool hasMinMax)
        {
            while (rows.Count > count) rows.RemoveAt(rows.Count - 1);
            while (rows.Count < count) rows.Add(new WildEncounterRow(PokemonNames, _pokemonIcons.Get) { ShowForm = ShowForms });
            for (int i = 0; i < count; i++)
            {
                rows[i].Label         = labelFn(i);
                rows[i].PackedSpecies = pokeFn(i);
                rows[i].Level         = hasMinMax ? 0 : lvlFn(i);
                if (hasMinMax && minFn != null) { rows[i].MinLevel = minFn(i); rows[i].MaxLevel = maxFn(i); }
            }
        }

        private void WriteRowsToFile()
        {
            WriteFamilyRowsToFile();
            WriteWaterGroup(Current.SurfPokemon,     Current.SurfMinLevels,     Current.SurfMaxLevels,     SurfRows);
            WriteWaterGroup(Current.OldRodPokemon,   Current.OldRodMinLevels,   Current.OldRodMaxLevels,   OldRodRows);
            WriteWaterGroup(Current.GoodRodPokemon,  Current.GoodRodMinLevels,  Current.GoodRodMaxLevels,  GoodRodRows);
            WriteWaterGroup(Current.SuperRodPokemon, Current.SuperRodMinLevels, Current.SuperRodMaxLevels, SuperRodRows);
        }

        protected static void WriteWaterGroup(ushort[] poke, byte[] min, byte[] max, ObservableCollection<WildEncounterRow> rows)
        {
            for (int i = 0; i < rows.Count && i < poke.Length; i++)
            {
                poke[i] = (ushort)rows[i].PackedSpecies;
                min[i]  = (byte)rows[i].MinLevel;
                max[i]  = (byte)rows[i].MaxLevel;
            }
        }

        protected WildEncounterRow.Replacement Rep(ObservableCollection<WildEncounterRow> rows, int i, string what)
        {
            if (i < 0 || i >= rows.Count || rows[i].PokemonIndex <= 0) return null;
            int species = rows[i].PokemonIndex;
            return new WildEncounterRow.Replacement { Icon = _pokemonIcons.Get(species), Tip = $"{what}: {(species < PokemonNames.Count ? PokemonNames[species] : species.ToString())}" };
        }

        protected static IReadOnlyList<WildEncounterRow.Replacement> Reps(params WildEncounterRow.Replacement[] reps) => reps.Where(r => r != null).ToList();

        // ── Encounter-file management ─────────────────────────────────────
        public void AddEncounterFile()
        {
            int count = EncounterNames.Count;
            _archive.Add(new TFile().ToByteArray());
            StagedChanged();
            EncounterNames.Add($"[{count}] (new)");
            SelectedEncounterIndex = count;
        }

        public async Task RemoveLastEncounterFileAsync()
        {
            int count = EncounterNames.Count;
            if (count <= 1) return;
            int last = count - 1;
            List<ushort> users = HeadersUsing(last);
            if (users.Count > 0)
            {
                await DialogHelper.ShowErrorAsync(
                    $"Encounter file {last} is used by header{(users.Count > 1 ? "s" : "")} {string.Join(", ", users)}. Point {(users.Count > 1 ? "them" : "it")} elsewhere first.",
                    "Encounter file in use");
                return;
            }
            bool leaving = _selectedEncounterIndex == last;
            string unsaved = leaving && _dirty ? " Its unsaved changes go with it." : "";
            if (!await DialogHelper.AskYesNoAsync($"Delete the last encounter file ({last})?{unsaved}", "Delete encounter file?")) return;
            _archive.RemoveLast();
            StagedChanged();
            if (leaving)
            {
                _selectedEncounterIndex = last - 1;
                OnPropertyChanged(nameof(SelectedEncounterIndex));
                LoadFile(last - 1);
            }
            EncounterNames.RemoveAt(last);
        }

        public void ImportEncounterFile(string path)
        {
            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            Current = Parse(stream);
            PopulateRows();
            SetDirty();
        }

        public void ExportEncounterFile(string path) => PlainFiles.WriteBytes(path, Current.ToByteArray());

        public async Task RepairAllAsync()
        {
            if (!await DialogHelper.AskYesNoAsync("Open every encounter file and reset corrupted fields to their defaults?", "Repair all encounter files?")) return;
            int n = _archive.Count;
            for (int i = 0; i < n; i++)
            {
                // The shown file is repaired from its unsaved edits, so they are kept.
                byte[] source = i == _selectedEncounterIndex && Current != null ? Current.ToByteArray() : _archive.Read(i);
                _archive.Put(i, Parse(new MemoryStream(source)).ToByteArray());
            }
            LoadFile(_selectedEncounterIndex);
            StagedChanged();
        }
    }
}
