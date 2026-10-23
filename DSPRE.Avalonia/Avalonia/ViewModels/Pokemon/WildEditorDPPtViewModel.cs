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
using System.Linq;
using DSPRE.ROMFiles;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Pokemon
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
                RawImage raw = DSUtils.GetPokePicRaw(species, IconSize, IconSize);
                icon = ImageConverter.ToAvaloniaBitmap(raw);
            }
            catch (Exception ex)
            {
                AppLogger.WarnOnce("PokemonIconCache.Get", ex);
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

        private string _label;
        public string Label { get => _label; set { if (_label == value) return; _label = value; OnPropertyChanged(); } }
        public string RateLabel { get; set; }

        private string _note, _noteTip;
        public string Note { get => _note; set { if (_note == value) return; _note = value; OnPropertyChanged(); } }
        public string NoteTip { get => _noteTip; set { if (_noteTip == value) return; _noteTip = value; OnPropertyChanged(); } }

        public sealed class Replacement
        {
            public IImage Icon { get; init; }
            public string Tip { get; init; }
        }

        private string _warning;
        /// <summary>Why this slot gives a glitch Pokémon when the game uses it, or null.</summary>
        public string Warning { get => _warning; set { if (_warning == value) return; _warning = value; OnPropertyChanged(); } }

        private IReadOnlyList<Replacement> _replacements = Array.Empty<Replacement>();
        public IReadOnlyList<Replacement> Replacements { get => _replacements; set { _replacements = value ?? Array.Empty<Replacement>(); OnPropertyChanged(); } }

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

        /// <summary>True on an hg-engine folder, whose species words also carry a form.</summary>
        public bool ShowForm { get; set; }

        private int _form;
        public int Form
        {
            get => _form;
            set { if (_form != value) { _form = value; OnPropertyChanged(); } }
        }

        /// <summary>The slot's stored value: the species, with hg-engine's form above it when the project has forms.</summary>
        public int PackedSpecies
        {
            get => ShowForm ? _pokemonIndex | (_form << DSPRE.HgEngine.HgEngineTrainerSource.FormShift) : _pokemonIndex;
            set
            {
                PokemonIndex = ShowForm ? value & DSPRE.HgEngine.HgEngineTrainerSource.SpeciesMask : value;
                Form = ShowForm ? value >> DSPRE.HgEngine.HgEngineTrainerSource.FormShift : 0;
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

    public class WildEditorDPPtViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo, IDetachable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        // ── IEditorWithUnsavedChanges ──────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty || _archive.IsDirty;
        public string UnsavedChangesDescription
            => DSPRE.Avalonia.Data.StagedArchive.Join($"Encounter file {SelectedEncounterIndex}", _dirty, _archive.Describe("encounter file", "encounter files"));
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
            BuildEncounterNameList(_totalHeaders);
            if (_selectedEncounterIndex >= EncounterNames.Count)
            {
                _selectedEncounterIndex = EncounterNames.Count - 1;
                OnPropertyChanged(nameof(SelectedEncounterIndex));
            }
            if (_current == null) { SetClean(); return; }
            try { LoadFile(_selectedEncounterIndex); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { SetClean(); }
        }

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
                if (RecordSwitchGuard.IsSnappingBack) return;
                if (value == _selectedEncounterIndex || value < 0 || value >= EncounterNames.Count) return;
                if (_dirty) { _ = ConfirmDiscardAsync(value); return; }
                _selectedEncounterIndex = value;
                OnPropertyChanged();
                LoadFile(value);
            }
        }

        // ── Encounter rates ───────────────────────────────────────────────
        private int _walkingRate;  public int WalkingRate  { get => _walkingRate;  set { if (Set(ref _walkingRate,  value) && !_loading) { _current.WalkingRate  = (byte)value; SetDirty(); SetNotes(); } } }
        private int _surfRate;     public int SurfRate     { get => _surfRate;     set { if (Set(ref _surfRate,     value) && !_loading) { _current.SurfRate     = (byte)value; SetDirty(); } } }
        private int _oldRodRate;   public int OldRodRate   { get => _oldRodRate;   set { if (Set(ref _oldRodRate,   value) && !_loading) { _current.OldRodRate   = (byte)value; SetDirty(); } } }
        private int _goodRodRate;  public int GoodRodRate  { get => _goodRodRate;  set { if (Set(ref _goodRodRate,  value) && !_loading) { _current.GoodRodRate  = (byte)value; SetDirty(); } } }
        private int _superRodRate; public int SuperRodRate { get => _superRodRate; set { if (Set(ref _superRodRate, value) && !_loading) { _current.SuperRodRate = (byte)value; SetDirty(); } } }

        // ── Walking encounters (12 slots) ─────────────────────────────────
        public ObservableCollection<WildEncounterRow> WalkingRows { get; } = new();
        public ObservableCollection<WildTimeRow> WalkingSlots { get; } = new();

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
        /// <summary>Which Unown letters appear here. </summary>
        public ObservableCollection<string> UnownTableNames  { get; } = new()
        {
            "Most forms (0)",
            "Most forms", "Only F", "Only R", "Only I", "Only N",
            "Only E", "Only D", "! and ?"
        };

        // Stored as a percentage the game only tests for zero, so the original number is kept unless the form changes.
        private uint _shellosRaw, _gastrodonRaw;

        private int _shellosFormIndex;
        public int ShellosFormIndex
        {
            get => _shellosFormIndex;
            set { if (Set(ref _shellosFormIndex, value) && !_loading) { _current.FormRates[0] = EastWest(value, _shellosRaw); SetDirty(); } }
        }

        private int _gastrodonFormIndex;
        public int GastrodonFormIndex
        {
            get => _gastrodonFormIndex;
            set { if (Set(ref _gastrodonFormIndex, value) && !_loading) { _current.FormRates[1] = EastWest(value, _gastrodonRaw); SetDirty(); } }
        }

        /// <summary>West sea is zero; east sea is anything else, so a number already there is kept.</summary>
        private static uint EastWest(int index, uint had) =>
            index == 0 ? 0u : (had != 0 ? had : 1u);

        private int _unownTableIndex;
        public int UnownTableIndex
        {
            get => _unownTableIndex;
            set { if (Set(ref _unownTableIndex, value) && !_loading && value >= 0 && value < KnownUnownTables) { _current.UnownTable = (uint)value; SetDirty(); } }
        }

        // Past these, an extra row names the stored table.
        private const int KnownUnownTables = 9;

        // ── Title ────────────────────────────────────────────────────────
        private string _title = "Wild Pokémon editor";
        public string Title { get => _title; private set => Set(ref _title, value); }

        // ── Private state ─────────────────────────────────────────────────
        private EncounterFileDPPt _current;
        private string _dirPath;
        // Added, removed and saved encounter files wait here until Save.
        private readonly DSPRE.Avalonia.Data.StagedArchive _archive = new(DirNames.Encounters);
        private int _totalHeaders;
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
            Title = _dirty ? "● Wild Pokémon editor" : "Wild Pokémon editor";
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

        // Grid rows don't reach the dirty and undo path otherwise, so each row is subscribed once.
        private void HookRowsOnce()
        {
            if (_rowsHooked) return;
            _rowsHooked = true;
            foreach (ObservableCollection<WildEncounterRow> coll in new[] { WalkingRows, DayRows, NightRows, SwarmRows, RadarRows, RubyRows,
                                         SapphireRows, EmeraldRows, FireRedRows, LeafGreenRows,
                                         SurfRows, OldRodRows, GoodRodRows, SuperRodRows })
                foreach (WildEncounterRow row in coll)
                    row.PropertyChanged += (_, e) =>
                    {
                        if (_loading) return;
                        if (e.PropertyName == nameof(WildEncounterRow.PokemonIndex) && !WalkingRows.Contains((WildEncounterRow)_)) SetNotes();
                        if (e.PropertyName != nameof(WildEncounterRow.PokemonIcon) && e.PropertyName != nameof(WildEncounterRow.Label)
                            && e.PropertyName != nameof(WildEncounterRow.Note) && e.PropertyName != nameof(WildEncounterRow.NoteTip)
                            && e.PropertyName != nameof(WildEncounterRow.Replacements)) SetDirty();
                    };
        }

        // ── Constructor ───────────────────────────────────────────────────
        public WildEditorDPPtViewModel(string dirPath, string[] pokemonNames, int encToOpen, int totalHeaders)
        {
            _dirPath = dirPath;
            _totalHeaders = totalHeaders;
            SetMonIconsPalTableAddress();

            foreach (string n in pokemonNames) PokemonNames.Add(n);
            BuildEncounterNameList(totalHeaders);
            AppEvents.NamesChanged += OnNamesChanged;   // live-refresh species names from the Text editor
            EncounterSlotOdds.Saved += OnOddsSaved;

            int count = EncounterNames.Count;
            if (encToOpen < 0) encToOpen = FirstWithEncounters(count);
            if (encToOpen >= count) encToOpen = 0;

            _selectedEncounterIndex = encToOpen;
            LoadFile(encToOpen);
            SelectedTab = FirstTabWithEncounters();
        }

        /// <summary>The tab shown. Set once on open to the first one with encounters, then left to the user.</summary>
        public int SelectedTab { get; set; }

        private int FirstTabWithEncounters()
        {
            if (_current == null || _current.WalkingRate > 0) return 0;
            if (_current.SurfRate > 0 || _current.OldRodRate > 0 || _current.GoodRodRate > 0 || _current.SuperRodRate > 0) return 1;
            return _current.HasEncounters ? 2 : 0;
        }

        // Opened without a file in mind: start on the first file with encounters, since file 0 is empty.
        private int FirstWithEncounters(int count)
        {
            for (int id = 0; id < count; id++)
            {
                try
                {
                    using FileStream stream = new FileStream(Path.Combine(_dirPath, id.ToString("D4")), FileMode.Open, FileAccess.Read);
                    if (new EncounterFileDPPt(stream).HasEncounters) return id;
                }
                catch (IOException) { }   // a file that can't be read is skipped
            }
            return 0;
        }

        public WildEditorDPPtViewModel()
        {
            if (!Design.IsDesignMode) return;

            Title = "Wild Pokémon editor DPPt (Preview)";
            for (int i = 0; i < 30; i++) PokemonNames.Add($"Pokémon {i:000}");
            EncounterNames.Add("[0] Route 201"); EncounterNames.Add("[1] Route 202"); EncounterNames.Add("[2] Unused");
            _selectedEncounterIndex = 0;

            WalkingRate = 25; SurfRate = 10; OldRodRate = 25; GoodRodRate = 50; SuperRodRate = 75;

            string[] walkLabels = Enumerable.Range(0, 12).Select(i => EncounterSlotOdds.SlotLabel("Walking", i)).ToArray();
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
        public async Task SaveCommandAsync()
        {
            if (_current == null) return;
            List<string> problems = new List<string>();
            WildEncounterChecks.Check(problems, "Walking", WalkingRate, WalkingRows, false);
            foreach (ObservableCollection<WildEncounterRow> rows in new[] { WalkingRows, SwarmRows, RadarRows, RubyRows, SapphireRows, EmeraldRows, FireRedRows, LeafGreenRows })
                foreach (WildEncounterRow r in rows) if (r.Warning != null) problems.Add(r.Warning);
            WildEncounterChecks.Check(problems, "Surf", SurfRate, SurfRows, true);
            WildEncounterChecks.Check(problems, "Old Rod", OldRodRate, OldRodRows, true);
            WildEncounterChecks.Check(problems, "Good Rod", GoodRodRate, GoodRodRows, true);
            WildEncounterChecks.Check(problems, "Super Rod", SuperRodRate, SuperRodRows, true);
            if (problems.Count > 0 && !await DialogHelper.AskYesNoAsync("These slots can be rolled but look wrong:\n" + WildEncounterChecks.Describe(problems)
                    + "\n\nSave anyway?", "Wild Pokémon editor")) return;
            WriteWalkingRowsToFile();
            WriteWaterRowsToFile();
            try { _archive.Save(_selectedEncounterIndex, _current.ToByteArray()); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                await DialogHelper.ShowErrorAsync("The encounter files couldn't be saved:" + Environment.NewLine + ex.Message, "Wild Pokémon editor");
                StagedChanged();
                return;
            }
            SetClean();
            SaveNotice.Saved(UnsavedChangesDescription);
            _history.MarkSaved();
            RaiseUndoState();
        }



        // ── Private helpers ───────────────────────────────────────────────
        private void OnNamesChanged(object sender, System.EventArgs e)
        {
            DSPRE.Avalonia.Data.ListSync.Apply(PokemonNames, DSPRE.RomInfo.GetPokemonNames());
            RefreshNotes();
        }

        private void OnOddsSaved() => global::Avalonia.Threading.Dispatcher.UIThread.Post(RefreshSlotLabels);

        // The "Replaced by" tips carry species names; rebuilding them is not an edit.
        private void RefreshNotes()
        {
            if (_current == null) return;
            bool was = _loading;
            _loading = true;
            try { SetNotes(); }
            finally { _loading = was; }
        }

        public void Detach()
        {
            AppEvents.NamesChanged -= OnNamesChanged;
            EncounterSlotOdds.Saved -= OnOddsSaved;
            _pokemonIcons.Dispose();
        }

        private void SetDirty() { if (_loading) return; RecordUndoSnapshot(); _dirty = true;  Title = "● Wild Pokémon editor"; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetClean() { _dirty = false; StagedChanged(); }

        // Added, removed and repaired files keep the editor unsaved after the open file's edits are clean.
        private void StagedChanged()
        {
            Title = (HasUnsavedChanges ? "● " : "") + "Wild Pokémon editor";
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

        private void BuildEncounterNameList(int totalHeaders)
        {
            EncounterNames.Clear();
            int fileCount = _archive.Count;
            Dictionary<int, List<string>> locationMap = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<string>>();
            List<string> locationNames = GetLocationNames();
            for (ushort i = 0; i < totalHeaders; i++)
            {
                MapHeader h = ReadHeader(i);

                if (GameFamily == GameFamilies.DP || GameFamily == GameFamilies.Plat)
                {
                    if (h.WildPokemon != 0xFFFF)
                    {
                        if (!locationMap.ContainsKey(h.WildPokemon)) locationMap[h.WildPokemon] = new System.Collections.Generic.List<string>();
                        // Names come from the header's own location; the encounter file id is an unrelated
                        // number and using it labels every entry with a different area.
                        int locIdx = GameFamily == GameFamilies.DP ? ((HeaderDP)h).LocationName : ((HeaderPt)h).LocationName;
                        locationMap[h.WildPokemon].Add(locIdx < locationNames.Count ? locationNames[locIdx] : "Unknown");
                    }
                }
            }
            for (int i = 0; i < fileCount; i++)
            {
                string label = locationMap.ContainsKey(i) ? string.Join(" + ", locationMap[i]) : "Unused";
                EncounterNames.Add($"[{i}] {label}");
            }
        }

        private static MapHeader ReadHeader(ushort id) => MapHeader.GetMapHeader(id);

        /// <summary>Headers whose wild Pokémon come from this encounter file, read fresh.</summary>
        private List<ushort> HeadersUsing(int file)
        {
            List<ushort> users = new List<ushort>();
            for (ushort i = 0; i < _totalHeaders; i++)
            {
                MapHeader h = ReadHeader(i);
                if (h != null && h.WildPokemon != NullEncounterID && h.WildPokemon == file) users.Add(i);
            }
            return users;
        }

        private void LoadFile(int id)
        {
            byte[] bytes = _archive.Read(id) ?? throw new FileNotFoundException($"Encounter file {id} is missing.");
            _current = new EncounterFileDPPt(new MemoryStream(bytes));
            PopulateRows();
            SetClean();

            _history.Reset(Snapshot());   // loaded state is the clean undo baseline for this file
            _lastCaptureUtc = DateTime.MinValue;
            RaiseUndoState();
        }

        /// <summary>Re-reads the slot chances into the row labels, for after Encounter slot odds saved. Keeps unsaved edits.</summary>
        public void RefreshSlotLabels()
        {
            for (int i = 0; i < WalkingRows.Count; i++) WalkingRows[i].Label = EncounterSlotOdds.SlotLabel("Walking", i);
            for (int i = 0; i < SurfRows.Count; i++) SurfRows[i].Label = EncounterSlotOdds.SlotLabel("Surfing", i);
            for (int i = 0; i < OldRodRows.Count; i++) OldRodRows[i].Label = EncounterSlotOdds.SlotLabel("Old Rod", i);
            for (int i = 0; i < GoodRodRows.Count; i++) GoodRodRows[i].Label = EncounterSlotOdds.SlotLabel("Good Rod", i);
            for (int i = 0; i < SuperRodRows.Count; i++) SuperRodRows[i].Label = EncounterSlotOdds.SlotLabel("Super Rod", i);
        }

        private void PopulateRows()
        {
            _loading = true;
            WalkingRate  = _current.WalkingRate;
            SurfRate     = _current.SurfRate;
            OldRodRate   = _current.OldRodRate;
            GoodRodRate  = _current.GoodRodRate;
            SuperRodRate = _current.SuperRodRate;

            // Form data: formRates[0]=Shellos, [1]=Gastrodon (0=West, anything else=East)
            // unownTable: 1..8 are the letter tables and 0 reads as 1, so the list index is the value.
            _shellosRaw = _current.FormRates[0];
            _gastrodonRaw = _current.FormRates[1];
            _shellosFormIndex  = (int)(_current.FormRates[0] == 0 ? 0 : 1);
            OnPropertyChanged(nameof(ShellosFormIndex));
            _gastrodonFormIndex = (int)(_current.FormRates[1] == 0 ? 0 : 1);
            OnPropertyChanged(nameof(GastrodonFormIndex));
            while (UnownTableNames.Count > KnownUnownTables) UnownTableNames.RemoveAt(KnownUnownTables);
            if (_current.UnownTable >= KnownUnownTables) UnownTableNames.Add($"Table {_current.UnownTable}");
            _unownTableIndex = (int)Math.Min(_current.UnownTable, KnownUnownTables);
            OnPropertyChanged(nameof(UnownTableIndex));

            string[] walkLabels = Enumerable.Range(0, 12).Select(i => EncounterSlotOdds.SlotLabel("Walking", i)).ToArray();
            SyncRows(WalkingRows,  12, i => walkLabels[i], i => (int)_current.WalkingPokemon[i], i => _current.WalkingLevels[i], null, null, false);
            SyncRows(DayRows,       2, i => $"Day {i+1}",   i => (int)_current.DayPokemon[i],     _ => 0,                         null, null, false);
            SyncRows(NightRows,     2, i => $"Night {i+1}", i => (int)_current.NightPokemon[i],   _ => 0,                         null, null, false);
            SyncRows(SwarmRows,     2, i => $"Swarm {i+1}", i => (int)_current.SwarmPokemon[i],   _ => 0,                         null, null, false);
            SyncRows(RadarRows,     4, i => $"Radar {i+1}", i => (int)_current.RadarPokemon[i],   _ => 0,                         null, null, false);
            SyncRows(RubyRows,      2, i => $"Ruby {i+1}",      i => (int)_current.RubyPokemon[i],      _ => 0, null, null, false);
            SyncRows(SapphireRows,  2, i => $"Sapphire {i+1}",  i => (int)_current.SapphirePokemon[i],  _ => 0, null, null, false);
            SyncRows(EmeraldRows,   2, i => $"Emerald {i+1}",   i => (int)_current.EmeraldPokemon[i],   _ => 0, null, null, false);
            SyncRows(FireRedRows,   2, i => $"FireRed {i+1}",   i => (int)_current.FireRedPokemon[i],   _ => 0, null, null, false);
            SyncRows(LeafGreenRows, 2, i => $"LeafGreen {i+1}", i => (int)_current.LeafGreenPokemon[i], _ => 0, null, null, false);
            SyncRows(SurfRows,     5, i => EncounterSlotOdds.SlotLabel("Surfing", i),   i => _current.SurfPokemon[i],     _ => 0, i => _current.SurfMinLevels[i],     i => _current.SurfMaxLevels[i],     true);
            SyncRows(OldRodRows,   5, i => EncounterSlotOdds.SlotLabel("Old Rod", i),   i => _current.OldRodPokemon[i],   _ => 0, i => _current.OldRodMinLevels[i],   i => _current.OldRodMaxLevels[i],   true);
            SyncRows(GoodRodRows,  5, i => EncounterSlotOdds.SlotLabel("Good Rod", i),  i => _current.GoodRodPokemon[i],  _ => 0, i => _current.GoodRodMinLevels[i],  i => _current.GoodRodMaxLevels[i],  true);
            SyncRows(SuperRodRows, 5, i => EncounterSlotOdds.SlotLabel("Super Rod", i), i => _current.SuperRodPokemon[i], _ => 0, i => _current.SuperRodMinLevels[i], i => _current.SuperRodMaxLevels[i], true);
            if (WalkingSlots.Count == 0)
                for (int i = 0; i < 12; i++)
                    WalkingSlots.Add(new WildTimeRow(WalkingRows[i], i is 2 or 3 ? DayRows[i - 2] : null, i is 2 or 3 ? NightRows[i - 2] : null));
            SetNotes();

            _loading = false;
            HookRowsOnce();   // subscribe row edits → SetDirty (idempotent)
        }

        // Walking slots each method replaces: day/night 2-3, swarm 0-1, Trophy Garden 6-7, Dual Slot 8-9, Radar 4-5 and 10-11.
        private void SetNotes()
        {
            for (int i = 0; i < WalkingRows.Count; i++)
                (WalkingRows[i].Note, WalkingRows[i].NoteTip) = i switch
                {
                    0 or 1 => ("Swarm", "During a swarm here the swarm Pokémon replaces this slot."),
                    4 or 5 or 10 or 11 => ("Poké Radar", "A shaking patch found with the Poké Radar can give the radar Pokémon here."),
                    6 or 7 => ("Trophy Garden", "In the Trophy Garden the day's Pokémon replaces this slot."),
                    8 or 9 => ("Dual Slot", "A GBA game in the second slot puts its Pokémon here, once the National Pokédex is obtained."),
                    _ => (null, null),
                };
            (string Game, ObservableCollection<WildEncounterRow> Rows)[] gba =
                { ("Ruby", RubyRows), ("Sapphire", SapphireRows), ("Emerald", EmeraldRows), ("FireRed", FireRedRows), ("LeafGreen", LeafGreenRows) };
            for (int i = 0; i < WalkingRows.Count; i++)
                WalkingRows[i].Replacements = i switch
                {
                    0 or 1 => Reps(Rep(SwarmRows, i, "Swarm")),
                    4 or 5 => Reps(Rep(RadarRows, i - 4, "Poké Radar")),
                    10 or 11 => Reps(Rep(RadarRows, i - 8, "Poké Radar")),
                    8 or 9 => Reps(gba.Select(g => Rep(g.Rows, i - 8, "Dual Slot " + g.Game)).ToArray()),
                    _ => null,
                };
            // The game copies these in without checking for an empty slot, so an empty one makes a glitch Pokémon.
            bool walks = WalkingRate > 0;
            foreach (WildEncounterRow r in WalkingRows) r.Warning = null;
            for (int i = 0; i < SwarmRows.Count; i++)
                SwarmRows[i].Warning = walks && SwarmRows[i].PokemonIndex <= 0 ? $"Empty: if a swarm comes here, walking slot {i + 1} gives a glitch Pokémon." : null;
            for (int i = 0; i < DayRows.Count && i + 2 < WalkingRows.Count; i++)
            {
                List<string> times = new List<string>();
                if (walks && DayRows[i].PokemonIndex <= 0) times.Add("day");
                if (walks && NightRows.Count > i && NightRows[i].PokemonIndex <= 0) times.Add("night");
                WalkingRows[i + 2].Warning = times.Count > 0 ? $"The {string.Join(" and ", times)} Pokémon {(times.Count == 1 ? "is" : "are")} empty, so at {(times.Count == 1 ? "that time" : "those times")} this slot gives a glitch Pokémon." : null;
            }
            int[] radarSlots = { 5, 6, 11, 12 };
            for (int i = 0; i < RadarRows.Count && i < radarSlots.Length; i++)
                RadarRows[i].Warning = walks && RadarRows[i].PokemonIndex <= 0 ? $"Empty: in a Poké Radar patch, walking slot {radarSlots[i]} gives a glitch Pokémon." : null;
            foreach ((string game, ObservableCollection<WildEncounterRow> rows) in gba)
                for (int i = 0; i < rows.Count; i++)
                    rows[i].Warning = walks && rows[i].PokemonIndex <= 0 ? $"Empty: with {game} in the second slot, walking slot {i + 9} gives a glitch Pokémon." : null;
            for (int i = 0; i < SwarmRows.Count; i++) (SwarmRows[i].Note, SwarmRows[i].NoteTip) = ($"Walking {i + 1}", "During a swarm here.");
            int[] radar = { 5, 6, 11, 12 };
            for (int i = 0; i < RadarRows.Count && i < radar.Length; i++) (RadarRows[i].Note, RadarRows[i].NoteTip) = ($"Walking {radar[i]}", "In a shaking patch found with the Poké Radar.");
            foreach (ObservableCollection<WildEncounterRow> game in new[] { RubyRows, SapphireRows, EmeraldRows, FireRedRows, LeafGreenRows })
                for (int i = 0; i < game.Count; i++) (game[i].Note, game[i].NoteTip) = ($"Walking {i + 9}", "With that game in the second slot, once the National Pokédex is obtained.");
        }

        private WildEncounterRow.Replacement Rep(ObservableCollection<WildEncounterRow> rows, int i, string what)
        {
            if (i < 0 || i >= rows.Count || rows[i].PokemonIndex <= 0) return null;
            int species = rows[i].PokemonIndex;
            return new WildEncounterRow.Replacement { Icon = _pokemonIcons.Get(species), Tip = $"{what}: {(species < PokemonNames.Count ? PokemonNames[species] : species.ToString())}" };
        }

        private static IReadOnlyList<WildEncounterRow.Replacement> Reps(params WildEncounterRow.Replacement[] reps) => reps.Where(r => r != null).ToList();

        // ── Encounter-file management ────────────────────────────────────────────────────
        public void AddEncounterFile()
        {
            int count = EncounterNames.Count;
            _archive.Add(new EncounterFileDPPt().ToByteArray());
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
            _current = new EncounterFileDPPt(stream);
            PopulateRows();
            SetDirty();
        }

        public void ExportEncounterFile(string path) => PlainFiles.WriteBytes(path, _current.ToByteArray());

        public async Task RepairAllAsync()
        {
            if (!await DialogHelper.AskYesNoAsync("Open every encounter file and reset corrupted fields to their defaults?", "Repair all encounter files?")) return;
            int n = _archive.Count;
            for (int i = 0; i < n; i++)
            {
                // The shown file is repaired from its unsaved edits, so they are kept.
                byte[] source = i == _selectedEncounterIndex && _current != null ? _current.ToByteArray() : _archive.Read(i);
                _archive.Put(i, new EncounterFileDPPt(new MemoryStream(source)).ToByteArray());
            }
            LoadFile(_selectedEncounterIndex);
            StagedChanged();
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
                _current.WalkingPokemon[i] = (uint)WalkingRows[i].PokemonIndex;
                _current.WalkingLevels[i]  = (byte)WalkingRows[i].Level;
            }
            for (int i = 0; i < DayRows.Count   && i < 2; i++) _current.DayPokemon[i]   = (uint)DayRows[i].PokemonIndex;
            for (int i = 0; i < NightRows.Count  && i < 2; i++) _current.NightPokemon[i] = (uint)NightRows[i].PokemonIndex;
            for (int i = 0; i < SwarmRows.Count  && i < 2; i++) _current.SwarmPokemon[i] = (ushort)SwarmRows[i].PokemonIndex;
            for (int i = 0; i < RadarRows.Count  && i < 4; i++) _current.RadarPokemon[i] = (uint)RadarRows[i].PokemonIndex;
            for (int i = 0; i < RubyRows.Count   && i < 2; i++) _current.RubyPokemon[i]      = (uint)RubyRows[i].PokemonIndex;
            for (int i = 0; i < SapphireRows.Count && i < 2; i++) _current.SapphirePokemon[i] = (uint)SapphireRows[i].PokemonIndex;
            for (int i = 0; i < EmeraldRows.Count  && i < 2; i++) _current.EmeraldPokemon[i]  = (uint)EmeraldRows[i].PokemonIndex;
            for (int i = 0; i < FireRedRows.Count   && i < 2; i++) _current.FireRedPokemon[i]  = (uint)FireRedRows[i].PokemonIndex;
            for (int i = 0; i < LeafGreenRows.Count && i < 2; i++) _current.LeafGreenPokemon[i] = (uint)LeafGreenRows[i].PokemonIndex;

            // Form data
            _current.FormRates[0] = EastWest(_shellosFormIndex, _shellosRaw);
            _current.FormRates[1] = EastWest(_gastrodonFormIndex, _gastrodonRaw);
            // The extra row keeps the stored table.
            if (_unownTableIndex < KnownUnownTables) _current.UnownTable = (uint)_unownTableIndex;
        }

        private void WriteWaterRowsToFile()
        {
            WriteWaterGroup(_current.SurfPokemon,     _current.SurfMinLevels,     _current.SurfMaxLevels,     SurfRows);
            WriteWaterGroup(_current.OldRodPokemon,   _current.OldRodMinLevels,   _current.OldRodMaxLevels,   OldRodRows);
            WriteWaterGroup(_current.GoodRodPokemon,  _current.GoodRodMinLevels,  _current.GoodRodMaxLevels,  GoodRodRows);
            WriteWaterGroup(_current.SuperRodPokemon, _current.SuperRodMinLevels, _current.SuperRodMaxLevels, SuperRodRows);
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
