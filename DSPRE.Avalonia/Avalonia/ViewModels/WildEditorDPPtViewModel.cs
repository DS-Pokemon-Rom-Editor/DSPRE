using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using global::Avalonia.Media;
using global::Avalonia.Media.Imaging;
using DSPRE.ROMFiles;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
{
    internal sealed class PokemonIconCache : IDisposable
    {
        private const int IconSize = 32;
        private readonly Dictionary<int, Bitmap> _icons = new();

        public Bitmap Get(int species)
        {
            if (species <= 0) return null;
            if (_icons.TryGetValue(species, out Bitmap icon)) return icon;

            try
            {
                var raw = DSUtils.GetPokePicRaw(species, IconSize, IconSize);
                icon = ImageConverter.ToAvaloniaBitmap(raw);
            }
            catch
            {
                icon = null;
            }

            _icons[species] = icon;
            return icon;
        }

        public void Dispose()
        {
            foreach (Bitmap icon in _icons.Values)
                icon?.Dispose();
            _icons.Clear();
        }
    }

    public class WildEncounterRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private readonly Func<int, IImage> _iconLoader;

        public WildEncounterRow() : this(Array.Empty<string>(), null) { }
        public WildEncounterRow(IEnumerable<string> pokemonNames, Func<int, IImage> iconLoader)
        {
            PokemonNames = pokemonNames ?? Array.Empty<string>();
            _iconLoader = iconLoader;
        }

        public IEnumerable<string> PokemonNames { get; }

        public string Label { get; set; }
        public string RateLabel { get; set; }

        private int _pokemonIndex;
        public int PokemonIndex
        {
            get => _pokemonIndex;
            set
            {
                if (_pokemonIndex == value) return;
                _pokemonIndex = value;
                OnPropertyChanged();
                UpdateIcon();
            }
        }

        private IImage _pokemonIcon;
        public IImage PokemonIcon => _pokemonIcon;

        private void UpdateIcon()
        {
            IImage icon = _iconLoader?.Invoke(_pokemonIndex);
            if (ReferenceEquals(_pokemonIcon, icon)) return;
            _pokemonIcon = icon;
            OnPropertyChanged(nameof(PokemonIcon));
        }

        private int _level;
        public int Level
        {
            get => _level;
            set { if (_level != value) { _level = value; OnPropertyChanged(); } }
        }

        private int _minLevel;
        public int MinLevel
        {
            get => _minLevel;
            set { if (_minLevel != value) { _minLevel = value; OnPropertyChanged(); } }
        }

        private int _maxLevel;
        public int MaxLevel
        {
            get => _maxLevel;
            set { if (_maxLevel != value) { _maxLevel = value; OnPropertyChanged(); } }
        }
    }

    public class WildEditorDPPtViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        // ── IEditorWithUnsavedChanges ──────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => $"Wild Encounters (DPPt #{SelectedEncounterIndex})";
        void IEditorWithUnsavedChanges.SaveChanges() => _ = SaveCommand();
        async Task<bool> IEditorWithUnsavedChanges.SaveChangesAsync()
        {
            await SaveCommand();
            return !HasUnsavedChanges;
        }
        public void DiscardChanges() => SetClean();

        // ── Pokemon name list ─────────────────────────────────────────────
        public ObservableCollection<string> PokemonNames { get; } = new();
        public ObservableCollection<string> EncounterNames { get; } = new();

        // ── Encounter selector ────────────────────────────────────────────
        private int _selectedEncounterIndex;
        public int SelectedEncounterIndex
        {
            get => _selectedEncounterIndex;
            set
            {
                if (value == _selectedEncounterIndex || value < 0 || value >= EncounterNames.Count) return;
                if (_dirty) { _ = ConfirmDiscardAsync(value); return; }
                _selectedEncounterIndex = value;
                OnPropertyChanged();
                LoadFile(value);
            }
        }

        // ── Encounter rates ───────────────────────────────────────────────
        private int _walkingRate;  public int WalkingRate  { get => _walkingRate;  set { if (Set(ref _walkingRate,  value) && !_loading) { _current.walkingRate  = (byte)value; SetDirty(); } } }
        private int _surfRate;     public int SurfRate     { get => _surfRate;     set { if (Set(ref _surfRate,     value) && !_loading) { _current.surfRate     = (byte)value; SetDirty(); } } }
        private int _oldRodRate;   public int OldRodRate   { get => _oldRodRate;   set { if (Set(ref _oldRodRate,   value) && !_loading) { _current.oldRodRate   = (byte)value; SetDirty(); } } }
        private int _goodRodRate;  public int GoodRodRate  { get => _goodRodRate;  set { if (Set(ref _goodRodRate,  value) && !_loading) { _current.goodRodRate  = (byte)value; SetDirty(); } } }
        private int _superRodRate; public int SuperRodRate { get => _superRodRate; set { if (Set(ref _superRodRate, value) && !_loading) { _current.superRodRate = (byte)value; SetDirty(); } } }

        // ── Walking encounters (12 slots) ─────────────────────────────────
        public ObservableCollection<WildEncounterRow> WalkingRows { get; } = new();

        // ── Time-specific (day/night, 2 each) ────────────────────────────
        public ObservableCollection<WildEncounterRow> DayRows    { get; } = new();
        public ObservableCollection<WildEncounterRow> NightRows  { get; } = new();
        public ObservableCollection<WildEncounterRow> SwarmRows  { get; } = new();

        // ── Dual slot / Radar ─────────────────────────────────────────────
        public ObservableCollection<WildEncounterRow> RadarRows      { get; } = new();
        public ObservableCollection<WildEncounterRow> RubyRows       { get; } = new();
        public ObservableCollection<WildEncounterRow> SapphireRows   { get; } = new();
        public ObservableCollection<WildEncounterRow> EmeraldRows    { get; } = new();
        public ObservableCollection<WildEncounterRow> FireRedRows    { get; } = new();
        public ObservableCollection<WildEncounterRow> LeafGreenRows  { get; } = new();

        // ── Water encounters ──────────────────────────────────────────────
        public ObservableCollection<WildEncounterRow> SurfRows     { get; } = new();
        public ObservableCollection<WildEncounterRow> OldRodRows   { get; } = new();
        public ObservableCollection<WildEncounterRow> GoodRodRows  { get; } = new();
        public ObservableCollection<WildEncounterRow> SuperRodRows { get; } = new();

        // ── Form data ─────────────────────────────────────────────────────
        public ObservableCollection<string> ShellosFormNames { get; } = new() { "West Sea", "East Sea" };
        public ObservableCollection<string> UnownTableNames  { get; } = new()
        {
            "Most Forms", "Only F", "Only R", "Only I", "Only N",
            "Only E", "Only D", "! and ?"
        };

        private int _shellosFormIndex;
        public int ShellosFormIndex
        {
            get => _shellosFormIndex;
            set { if (Set(ref _shellosFormIndex, value) && !_loading) { _current.regionalForms[0] = (uint)value; SetDirty(); } }
        }

        private int _gastrodonFormIndex;
        public int GastrodonFormIndex
        {
            get => _gastrodonFormIndex;
            set { if (Set(ref _gastrodonFormIndex, value) && !_loading) { _current.regionalForms[1] = (uint)value; SetDirty(); } }
        }

        private int _unownTableIndex;
        public int UnownTableIndex
        {
            get => _unownTableIndex;
            set { if (Set(ref _unownTableIndex, value) && !_loading) { _current.unknownTable = (uint)(value + 1); SetDirty(); } }
        }

        // ── Title ────────────────────────────────────────────────────────
        private string _title = "Wild Pokémon Editor (DPPt)";
        public string Title { get => _title; private set => Set(ref _title, value); }

        // ── Private state ─────────────────────────────────────────────────
        private EncounterFileDPPt _current;
        private string _dirPath;
        private bool _loading;
        private bool _rowsHooked;

        // ── Undo / redo (ISupportsUndo) ────────────────────────────────────────
        // Snapshot = the encounter file's bytes. Grid edits live in the row VMs (only synced to _current at
        // save), so Snapshot() syncs rows → _current first (sync-then-snapshot), like the Trade editor.
        private readonly DSPRE.Avalonia.UndoHistory<byte[]> _history = new();
        private readonly PokemonIconCache _pokemonIcons = new();
        private DateTime _lastCaptureUtc = DateTime.MinValue;
        private const int CoalesceMs = 500;

        public bool CanUndo => _history.CanUndo;
        public bool CanRedo => _history.CanRedo;
        public void Undo() { if (_history.CanUndo) ApplyState(_history.Undo()); }
        public void Redo() { if (_history.CanRedo) ApplyState(_history.Redo()); }
        private void RaiseUndoState() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private byte[] Snapshot()
        {
            if (_current == null) return null;
            WriteWalkingRowsToFile();   // pull the live grid rows + form fields into _current
            WriteWaterRowsToFile();
            return _current.ToByteArray();
        }

        private void ApplyState(byte[] bytes)
        {
            if (bytes == null) return;
            _current = new EncounterFileDPPt(new MemoryStream(bytes));
            PopulateRows();   // manages _loading itself; refreshes all rate/form fields + grid rows
            _dirty = _history.IsDirty;
            Title = _dirty ? "● Wild Pokémon Editor (DPPt)" : "Wild Pokémon Editor (DPPt)";
            OnPropertyChanged(nameof(HasUnsavedChanges));
            RaiseUndoState();
        }

        private void RecordUndoSnapshot()
        {
            if (_loading || _current == null) return;
            bool coalesce = (DateTime.UtcNow - _lastCaptureUtc).TotalMilliseconds < CoalesceMs;
            _history.Capture(Snapshot(), coalesce);
            _lastCaptureUtc = DateTime.UtcNow;
            RaiseUndoState();
        }

        // Grid-row edits don't otherwise reach the dirty/undo pipeline; subscribe each row once so a species
        // or level change marks the editor dirty (also fixes a latent "edited rows close without prompting").
        private void HookRowsOnce()
        {
            if (_rowsHooked) return;
            _rowsHooked = true;
            foreach (var coll in new[] { WalkingRows, DayRows, NightRows, SwarmRows, RadarRows, RubyRows,
                                         SapphireRows, EmeraldRows, FireRedRows, LeafGreenRows,
                                         SurfRows, OldRodRows, GoodRodRows, SuperRodRows })
                foreach (var row in coll)
                    row.PropertyChanged += (_, e) => { if (!_loading && e.PropertyName != nameof(WildEncounterRow.PokemonIcon)) SetDirty(); };
        }

        // ── Constructor ───────────────────────────────────────────────────
        public WildEditorDPPtViewModel(string dirPath, string[] pokemonNames, int encToOpen, int totalHeaders)
        {
            _dirPath = dirPath;
            SetMonIconsPalTableAddress();

            foreach (var n in pokemonNames) PokemonNames.Add(n);
            BuildEncounterNameList(totalHeaders);
            AppEvents.NamesChanged += OnNamesChanged;   // live-refresh species names from the Text editor

            int count = EncounterNames.Count;
            if (encToOpen >= count) encToOpen = 0;

            _selectedEncounterIndex = encToOpen;
            LoadFile(encToOpen);
        }

        public WildEditorDPPtViewModel()
        {
            if (!Design.IsDesignMode) return;

            Title = "Wild Pokémon Editor DPPt (Preview)";
            for (int i = 0; i < 30; i++) PokemonNames.Add($"Pokémon {i:000}");
            EncounterNames.Add("[0] Route 201"); EncounterNames.Add("[1] Route 202"); EncounterNames.Add("[2] Unused");
            _selectedEncounterIndex = 0;

            WalkingRate = 25; SurfRate = 10; OldRodRate = 25; GoodRodRate = 50; SuperRodRate = 75;

            string[] walkLabels = { "20%", "20%", "10%", "10%", "10%", "10%", "5%", "5%", "4%", "4%", "1%", "1%" };
            for (int i = 0; i < 12; i++)
                WalkingRows.Add(new WildEncounterRow(PokemonNames, null) { Label = walkLabels[i], PokemonIndex = i % PokemonNames.Count, Level = 5 });
            for (int i = 0; i < 2; i++)
            {
                DayRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"Day {i + 1}",    PokemonIndex = i % PokemonNames.Count, Level = 10 });
                NightRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"Night {i + 1}",  PokemonIndex = i % PokemonNames.Count, Level = 10 });
                SwarmRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"Swarm {i + 1}",  PokemonIndex = i % PokemonNames.Count, Level = 15 });
                RadarRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"Radar {i + 1}",  PokemonIndex = i % PokemonNames.Count, Level = 20 });
                RubyRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"Ruby {i + 1}",   PokemonIndex = i % PokemonNames.Count, Level = 20 });
                SapphireRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"Sapphire {i + 1}", PokemonIndex = i % PokemonNames.Count, Level = 20 });
                EmeraldRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"Emerald {i + 1}",  PokemonIndex = i % PokemonNames.Count, Level = 20 });
                FireRedRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"FR {i + 1}",        PokemonIndex = i % PokemonNames.Count, Level = 20 });
                LeafGreenRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"LG {i + 1}",       PokemonIndex = i % PokemonNames.Count, Level = 20 });
            }
            for (int i = 0; i < 5; i++)
            {
                SurfRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"Surf {i + 1}",     PokemonIndex = i % PokemonNames.Count, MinLevel = 20, MaxLevel = 30 });
                OldRodRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"Old Rod {i + 1}",  PokemonIndex = i % PokemonNames.Count, MinLevel = 5,  MaxLevel = 10 });
                GoodRodRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"Good Rod {i + 1}", PokemonIndex = i % PokemonNames.Count, MinLevel = 10, MaxLevel = 20 });
                SuperRodRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"Super Rod {i + 1}",PokemonIndex = i % PokemonNames.Count, MinLevel = 30, MaxLevel = 40 });
            }
        }

        // ── Commands ──────────────────────────────────────────────────────
        public async Task SaveCommand()
        {
            if (_current == null) return;
            WriteWalkingRowsToFile();
            WriteWaterRowsToFile();
            _current.SaveToFileDefaultDir(_selectedEncounterIndex, showSuccessMessage: true);
            SetClean();
            _history.MarkSaved();
            RaiseUndoState();
        }



        // ── Private helpers ───────────────────────────────────────────────
        private void OnNamesChanged(object sender, System.EventArgs e)
            => DSPRE.Avalonia.Data.ListSync.Apply(PokemonNames, DSPRE.RomInfo.GetPokemonNames());
        public void Detach()
        {
            AppEvents.NamesChanged -= OnNamesChanged;
            _pokemonIcons.Dispose();
        }

        private void SetDirty() { if (_loading) return; RecordUndoSnapshot(); _dirty = true;  Title = "● Wild Pokémon Editor (DPPt)"; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetClean() { _dirty = false; Title = "Wild Pokémon Editor (DPPt)";  OnPropertyChanged(nameof(HasUnsavedChanges)); }

        private async Task ConfirmDiscardAsync(int newId)
        {
            bool discard = await DialogHelper.AskYesNo(
                "There are unsaved changes. Discard and proceed?", "Unsaved Changes");
            if (!discard) { OnPropertyChanged(nameof(SelectedEncounterIndex)); return; }
            _dirty = false;
            _selectedEncounterIndex = newId;
            OnPropertyChanged(nameof(SelectedEncounterIndex));
            LoadFile(newId);
        }

        private void BuildEncounterNameList(int totalHeaders)
        {
            EncounterNames.Clear();
            string[] files = Directory.GetFiles(_dirPath);
            var locationMap = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<string>>();
            var locationNames = GetLocationNames();
            for (ushort i = 0; i < totalHeaders; i++)
            {
                MapHeader h;
                if (RomPatchState.flag_DynamicHeadersPatchApplied ||
                    PatchToolboxLogic.CheckFilesDynamicHeadersPatchApplied())
                    h = MapHeader.LoadFromFile(Path.Combine(gameDirs[DirNames.dynamicHeaders].unpackedDir, i.ToString("D4")), i, 0);
                else
                    h = MapHeader.LoadFromARM9(i);

                if (gameFamily == GameFamilies.DP || gameFamily == GameFamilies.Plat)
                {
                    if (h.wildPokemon != 0xFFFF)
                    {
                        if (!locationMap.ContainsKey(h.wildPokemon)) locationMap[h.wildPokemon] = new System.Collections.Generic.List<string>();
                        locationMap[h.wildPokemon].Add(locationNames[h.wildPokemon < locationNames.Count ? h.wildPokemon : 0]);
                    }
                }
            }
            for (int i = 0; i < files.Length; i++)
            {
                string label = locationMap.ContainsKey(i) ? string.Join(" + ", locationMap[i]) : "Unused";
                EncounterNames.Add($"[{i}] {label}");
            }
        }

        private void LoadFile(int id)
        {
            string path = Path.Combine(_dirPath, id.ToString("D4"));
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            _current = new EncounterFileDPPt(stream);
            PopulateRows();
            SetClean();

            _history.Reset(Snapshot());   // loaded state is the clean undo baseline for this file
            _lastCaptureUtc = DateTime.MinValue;
            RaiseUndoState();
        }

        private void PopulateRows()
        {
            _loading = true;
            WalkingRate  = _current.walkingRate;
            SurfRate     = _current.surfRate;
            OldRodRate   = _current.oldRodRate;
            GoodRodRate  = _current.goodRodRate;
            SuperRodRate = _current.superRodRate;

            // Form data: regionalForms[0]=Shellos, [1]=Gastrodon (0=West, 1=East)
            // unknownTable: 0 or 1-based index into UnownTable enum; 0 maps to first entry
            _shellosFormIndex  = (int)(_current.regionalForms[0] == 0 ? 0 : 1);
            OnPropertyChanged(nameof(ShellosFormIndex));
            _gastrodonFormIndex = (int)(_current.regionalForms[1] == 0 ? 0 : 1);
            OnPropertyChanged(nameof(GastrodonFormIndex));
            _unownTableIndex = _current.unknownTable == 0 ? 0 : (int)_current.unknownTable - 1;
            if (_unownTableIndex < 0) _unownTableIndex = 0;
            if (_unownTableIndex >= UnownTableNames.Count) _unownTableIndex = 0;
            OnPropertyChanged(nameof(UnownTableIndex));

            string[] walkLabels = { "20%", "20%", "10%", "10%", "10%", "10%", "5%", "5%", "4%", "4%", "1%", "1%" };
            SyncRows(WalkingRows,  12, i => walkLabels[i], i => (int)_current.walkingPokemon[i], i => _current.walkingLevels[i], null, null, false);
            SyncRows(DayRows,       2, i => $"Day {i+1}",   i => (int)_current.dayPokemon[i],     _ => 0,                         null, null, false);
            SyncRows(NightRows,     2, i => $"Night {i+1}", i => (int)_current.nightPokemon[i],   _ => 0,                         null, null, false);
            SyncRows(SwarmRows,     2, i => $"Swarm {i+1}", i => (int)_current.swarmPokemon[i],   _ => 0,                         null, null, false);
            SyncRows(RadarRows,     4, i => $"Radar {i+1}", i => (int)_current.radarPokemon[i],   _ => 0,                         null, null, false);
            SyncRows(RubyRows,      2, i => $"Ruby {i+1}",      i => (int)_current.rubyPokemon[i],      _ => 0, null, null, false);
            SyncRows(SapphireRows,  2, i => $"Sapphire {i+1}",  i => (int)_current.sapphirePokemon[i],  _ => 0, null, null, false);
            SyncRows(EmeraldRows,   2, i => $"Emerald {i+1}",   i => (int)_current.emeraldPokemon[i],   _ => 0, null, null, false);
            SyncRows(FireRedRows,   2, i => $"FireRed {i+1}",   i => (int)_current.fireRedPokemon[i],   _ => 0, null, null, false);
            SyncRows(LeafGreenRows, 2, i => $"LeafGreen {i+1}", i => (int)_current.leafGreenPokemon[i], _ => 0, null, null, false);
            SyncRows(SurfRows,     5, i => $"Surf {i+1}",     i => _current.surfPokemon[i],     _ => 0, i => _current.surfMinLevels[i],     i => _current.surfMaxLevels[i],     true);
            SyncRows(OldRodRows,   5, i => $"Old Rod {i+1}",  i => _current.oldRodPokemon[i],   _ => 0, i => _current.oldRodMinLevels[i],   i => _current.oldRodMaxLevels[i],   true);
            SyncRows(GoodRodRows,  5, i => $"Good Rod {i+1}", i => _current.goodRodPokemon[i],  _ => 0, i => _current.goodRodMinLevels[i],  i => _current.goodRodMaxLevels[i],  true);
            SyncRows(SuperRodRows, 5, i => $"Super Rod {i+1}",i => _current.superRodPokemon[i], _ => 0, i => _current.superRodMinLevels[i], i => _current.superRodMaxLevels[i], true);

            _loading = false;
            HookRowsOnce();   // subscribe row edits → SetDirty (idempotent)
        }

        // ── Encounter-file management ────────────────────────────────────────────────────
        public void AddEncounterFile()
        {
            int count = EncounterNames.Count;
            using (var w = new BinaryWriter(new FileStream(Path.Combine(_dirPath, count.ToString("D4")), FileMode.Create)))
                w.Write(new EncounterFileDPPt().ToByteArray());
            EncounterNames.Add($"[{count}] (new)");
            SelectedEncounterIndex = count;
        }

        public async Task RemoveLastEncounterFileAsync()
        {
            int count = EncounterNames.Count;
            if (count == 0) return;
            int last = count - 1;
            if (!await DialogHelper.AskYesNo($"Delete the last encounter file ({last})?", "Confirm deletion")) return;
            File.Delete(Path.Combine(_dirPath, last.ToString("D4")));
            if (_selectedEncounterIndex == last) SelectedEncounterIndex = last - 1;
            EncounterNames.RemoveAt(last);
        }

        public void ImportEncounterFile(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            _current = new EncounterFileDPPt(stream);
            PopulateRows();
            SetDirty();
        }

        public void ExportEncounterFile(string path) => File.WriteAllBytes(path, _current.ToByteArray());

        public async Task RepairAllAsync()
        {
            if (!await DialogHelper.AskYesNo("Open every encounter file and reset corrupted fields to their defaults?", "Repair all encounter files?")) return;
            int n = Directory.GetFiles(_dirPath).Length;
            for (int i = 0; i < n; i++)
            {
                using var s = new FileStream(Path.Combine(_dirPath, i.ToString("D4")), FileMode.Open, FileAccess.Read);
                new EncounterFileDPPt(s).SaveToFileDefaultDir(i, showSuccessMessage: false);
            }
            LoadFile(_selectedEncounterIndex);
        }

        private void SyncRows(
            ObservableCollection<WildEncounterRow> rows, int count,
            Func<int, string> labelFn, Func<int, int> pokeFn, Func<int, int> lvlFn,
            Func<int, int> minFn, Func<int, int> maxFn, bool hasMinMax)
        {
            while (rows.Count > count) rows.RemoveAt(rows.Count - 1);
            while (rows.Count < count) rows.Add(new WildEncounterRow(PokemonNames, _pokemonIcons.Get));
            for (int i = 0; i < count; i++)
            {
                rows[i].Label        = labelFn(i);
                rows[i].PokemonIndex = pokeFn(i);
                rows[i].Level        = hasMinMax ? 0 : lvlFn(i);
                if (hasMinMax && minFn != null) { rows[i].MinLevel = minFn(i); rows[i].MaxLevel = maxFn(i); }
            }
        }

        private void WriteWalkingRowsToFile()
        {
            for (int i = 0; i < WalkingRows.Count && i < 12; i++)
            {
                _current.walkingPokemon[i] = (uint)WalkingRows[i].PokemonIndex;
                _current.walkingLevels[i]  = (byte)WalkingRows[i].Level;
            }
            for (int i = 0; i < DayRows.Count   && i < 2; i++) _current.dayPokemon[i]   = (uint)DayRows[i].PokemonIndex;
            for (int i = 0; i < NightRows.Count  && i < 2; i++) _current.nightPokemon[i] = (uint)NightRows[i].PokemonIndex;
            for (int i = 0; i < SwarmRows.Count  && i < 2; i++) _current.swarmPokemon[i] = (ushort)SwarmRows[i].PokemonIndex;
            for (int i = 0; i < RadarRows.Count  && i < 4; i++) _current.radarPokemon[i] = (uint)RadarRows[i].PokemonIndex;
            for (int i = 0; i < RubyRows.Count   && i < 2; i++) _current.rubyPokemon[i]      = (uint)RubyRows[i].PokemonIndex;
            for (int i = 0; i < SapphireRows.Count && i < 2; i++) _current.sapphirePokemon[i] = (uint)SapphireRows[i].PokemonIndex;
            for (int i = 0; i < EmeraldRows.Count  && i < 2; i++) _current.emeraldPokemon[i]  = (uint)EmeraldRows[i].PokemonIndex;
            for (int i = 0; i < FireRedRows.Count   && i < 2; i++) _current.fireRedPokemon[i]  = (uint)FireRedRows[i].PokemonIndex;
            for (int i = 0; i < LeafGreenRows.Count && i < 2; i++) _current.leafGreenPokemon[i] = (uint)LeafGreenRows[i].PokemonIndex;

            // Form data
            _current.regionalForms[0] = (uint)_shellosFormIndex;
            _current.regionalForms[1] = (uint)_gastrodonFormIndex;
            _current.unknownTable     = (uint)(_unownTableIndex + 1);
        }

        private void WriteWaterRowsToFile()
        {
            WriteWaterGroup(_current.surfPokemon,     _current.surfMinLevels,     _current.surfMaxLevels,     SurfRows);
            WriteWaterGroup(_current.oldRodPokemon,   _current.oldRodMinLevels,   _current.oldRodMaxLevels,   OldRodRows);
            WriteWaterGroup(_current.goodRodPokemon,  _current.goodRodMinLevels,  _current.goodRodMaxLevels,  GoodRodRows);
            WriteWaterGroup(_current.superRodPokemon, _current.superRodMinLevels, _current.superRodMaxLevels, SuperRodRows);
        }

        private static void WriteWaterGroup(ushort[] poke, byte[] min, byte[] max, ObservableCollection<WildEncounterRow> rows)
        {
            for (int i = 0; i < rows.Count && i < poke.Length; i++)
            {
                poke[i] = (ushort)rows[i].PokemonIndex;
                min[i]  = (byte)rows[i].MinLevel;
                max[i]  = (byte)rows[i].MaxLevel;
            }
        }
    }
}
