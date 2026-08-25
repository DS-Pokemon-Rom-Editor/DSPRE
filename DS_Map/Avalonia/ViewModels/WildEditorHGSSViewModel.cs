using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
{
    public class WildEditorHGSSViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        // ─── hg-engine source banner ──────────────────────────────────────────────
        public string HgEngineBanner => DSPRE.HgEngine.HgEngineProject.BannerText;
        public bool ShowHgEngineBanner => HgEngineBanner != null;

        // ── IEditorWithUnsavedChanges ──────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => $"Wild Encounters (HGSS #{SelectedEncounterIndex})";
        void IEditorWithUnsavedChanges.SaveChanges() => _ = SaveCommand();
        async Task<bool> IEditorWithUnsavedChanges.SaveChangesAsync()
        {
            await SaveCommand();
            return !HasUnsavedChanges;
        }
        public void DiscardChanges() => SetClean();

        // ── Name lists ────────────────────────────────────────────────────
        public ObservableCollection<string> PokemonNames   { get; } = new();
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
        private int _walkingRate;     public int WalkingRate     { get => _walkingRate;     set { if (Set(ref _walkingRate,     value) && !_loading) { _current.walkingRate     = (byte)value; SetDirty(); } } }
        private int _surfRate;        public int SurfRate        { get => _surfRate;        set { if (Set(ref _surfRate,        value) && !_loading) { _current.surfRate        = (byte)value; SetDirty(); } } }
        private int _rockSmashRate;   public int RockSmashRate   { get => _rockSmashRate;   set { if (Set(ref _rockSmashRate,   value) && !_loading) { _current.rockSmashRate   = (byte)value; SetDirty(); } } }
        private int _oldRodRate;      public int OldRodRate      { get => _oldRodRate;      set { if (Set(ref _oldRodRate,      value) && !_loading) { _current.oldRodRate      = (byte)value; SetDirty(); } } }
        private int _goodRodRate;     public int GoodRodRate     { get => _goodRodRate;     set { if (Set(ref _goodRodRate,     value) && !_loading) { _current.goodRodRate     = (byte)value; SetDirty(); } } }
        private int _superRodRate;    public int SuperRodRate    { get => _superRodRate;    set { if (Set(ref _superRodRate,    value) && !_loading) { _current.superRodRate    = (byte)value; SetDirty(); } } }

        // ── Walking encounter rows (time-based, 12 slots each) ────────────
        public ObservableCollection<WildEncounterRow> MorningRows { get; } = new();
        public ObservableCollection<WildEncounterRow> DayRows     { get; } = new();
        public ObservableCollection<WildEncounterRow> NightRows   { get; } = new();

        // ── Swarm / Rock Smash / Radio ─────────────────────────────────────
        public ObservableCollection<WildEncounterRow> SwarmRows        { get; } = new();
        public ObservableCollection<WildEncounterRow> RockSmashRows    { get; } = new();
        public ObservableCollection<WildEncounterRow> HoennRadioRows   { get; } = new();
        public ObservableCollection<WildEncounterRow> SinnohRadioRows  { get; } = new();

        // ── Water encounters ──────────────────────────────────────────────
        public ObservableCollection<WildEncounterRow> SurfRows     { get; } = new();
        public ObservableCollection<WildEncounterRow> OldRodRows   { get; } = new();
        public ObservableCollection<WildEncounterRow> GoodRodRows  { get; } = new();
        public ObservableCollection<WildEncounterRow> SuperRodRows { get; } = new();

        // ── Title ─────────────────────────────────────────────────────────
        private string _title = "Wild Pokémon Editor (HGSS)";
        public string Title { get => _title; private set => Set(ref _title, value); }

        // ── Private state ─────────────────────────────────────────────────
        private EncounterFileHGSS _current;
        private string _dirPath;
        private bool _loading;
        private bool _rowsHooked;
        private readonly PokemonIconCache _pokemonIcons = new();

        // ── Undo / redo (ISupportsUndo) ────────────────────────────────────────
        // Sync-then-snapshot: grid rows live in the row VMs (synced to _current only at save), so Snapshot()
        // syncs rows → _current first, then serializes. Snapshot = the encounter file's bytes.
        private readonly DSPRE.Avalonia.UndoHistory<byte[]> _history = new();
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
            WriteWalkingRowsToFile();
            WriteWaterRowsToFile();
            return _current.ToByteArray();
        }

        private void ApplyState(byte[] bytes)
        {
            if (bytes == null) return;
            _current = new EncounterFileHGSS(new MemoryStream(bytes));
            PopulateRows();
            _dirty = _history.IsDirty;
            Title = _dirty ? "● Wild Pokémon Editor (HGSS)" : "Wild Pokémon Editor (HGSS)";
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

        // Subscribe grid-row edits → SetDirty once (also fixes "edited rows close without prompting").
        private void HookRowsOnce()
        {
            if (_rowsHooked) return;
            _rowsHooked = true;
            foreach (var coll in new[] { MorningRows, DayRows, NightRows, SwarmRows, RockSmashRows,
                                         HoennRadioRows, SinnohRadioRows, SurfRows, OldRodRows, GoodRodRows, SuperRodRows })
                foreach (var row in coll)
                    row.PropertyChanged += (_, e) => { if (!_loading && e.PropertyName != nameof(WildEncounterRow.PokemonIcon)) SetDirty(); };
        }

        // ── Constructor (runtime) ─────────────────────────────────────────
        public WildEditorHGSSViewModel(string dirPath, string[] pokemonNames, int encToOpen, int totalHeaders)
        {
            _dirPath = dirPath;
            SetMonIconsPalTableAddress();
            foreach (var n in pokemonNames) PokemonNames.Add(n);
            BuildEncounterNameList(totalHeaders);
            // Live-refresh the species list when names are edited in the Text editor (grid cells re-render
            // in place, so no per-combo re-poke is needed here).
            AppEvents.NamesChanged += OnNamesChanged;

            int count = EncounterNames.Count;
            if (encToOpen >= count) encToOpen = 0;
            _selectedEncounterIndex = encToOpen;
            LoadFile(encToOpen);
        }

        // ── Constructor (design-time) ─────────────────────────────────────
        public WildEditorHGSSViewModel()
        {
            if (!Design.IsDesignMode) return;

            Title = "Wild Pokémon Editor HGSS (Preview)";
            for (int i = 0; i < 30; i++) PokemonNames.Add($"Pokémon {i:000}");
            EncounterNames.Add("[0] Route 29"); EncounterNames.Add("[1] Route 30"); EncounterNames.Add("[2] Unused");
            _selectedEncounterIndex = 0;

            WalkingRate = 25; SurfRate = 10; RockSmashRate = 5;
            OldRodRate = 25; GoodRodRate = 50; SuperRodRate = 75;

            string[] walkLabels = { "20%", "20%", "10%", "10%", "10%", "10%", "5%", "5%", "4%", "4%", "1%", "1%" };
            for (int i = 0; i < 12; i++)
            {
                MorningRows.Add(new WildEncounterRow(PokemonNames, null) { Label = walkLabels[i], PokemonIndex = i % PokemonNames.Count, Level = 5 });
                DayRows.Add(new WildEncounterRow(PokemonNames, null) { Label = walkLabels[i], PokemonIndex = i % PokemonNames.Count, Level = 6 });
                NightRows.Add(new WildEncounterRow(PokemonNames, null) { Label = walkLabels[i], PokemonIndex = i % PokemonNames.Count, Level = 7 });
            }
            string[] dtSwarmLabels = { "Grass", "Surf", "Night Fish", "Rod" };
            for (int i = 0; i < 4; i++)
                SwarmRows.Add(new WildEncounterRow(PokemonNames, null) { Label = dtSwarmLabels[i], PokemonIndex = i % PokemonNames.Count, Level = 15 });
            for (int i = 0; i < 2; i++)
            {
                RockSmashRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"Rock Smash {i + 1}", PokemonIndex = i % PokemonNames.Count, MinLevel = 10, MaxLevel = 20 });
                HoennRadioRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"Slot {i + 1}",  PokemonIndex = i % PokemonNames.Count, Level = 15 });
                SinnohRadioRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"Slot {i + 1}", PokemonIndex = i % PokemonNames.Count, Level = 15 });
            }
            for (int i = 0; i < 5; i++)
            {
                SurfRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"Surf {i + 1}",      PokemonIndex = i % PokemonNames.Count, MinLevel = 20, MaxLevel = 30 });
                OldRodRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"Old Rod {i + 1}",   PokemonIndex = i % PokemonNames.Count, MinLevel = 5,  MaxLevel = 10 });
                GoodRodRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"Good Rod {i + 1}",  PokemonIndex = i % PokemonNames.Count, MinLevel = 10, MaxLevel = 20 });
                SuperRodRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"Super Rod {i + 1}", PokemonIndex = i % PokemonNames.Count, MinLevel = 30, MaxLevel = 40 });
            }
        }

        // ── Commands ──────────────────────────────────────────────────────
        public async Task SaveCommand()
        {
            if (_current == null) return;
            WriteWalkingRowsToFile();
            WriteWaterRowsToFile();
            _current.SaveToFileDefaultDir(_selectedEncounterIndex, showSuccessMessage: true);
            WriteHgEngineSource();
            SetClean();
            _history.MarkSaved();
            RaiseUndoState();
        }

        // Curated v1 scope: every EncounterFileHGSS field this editor already exposes. Water/rod slots
        // are undesignated "{ min, max, SPECIES_X }" structs in hg-engine's source (no field names at
        // all); At(0)/At(1)/At(2) locate them positionally, the same mechanism used for named fields.
        private void WriteHgEngineSource()
        {
            if (!HgEngineProject.IsActive || _current == null) return;

            var species = HgEngineSymbolTable.Load("include/constants/species.h");
            string SpeciesSymbol(int id) => species?.TryGetNameWithPrefix(id, "SPECIES_", out string n) == true ? n : id.ToString();

            var fields = new List<HgEngineFieldWrite>
            {
                new(new[] { FieldPathSegment.Field("rateWalk") }, _current.walkingRate.ToString()),
                new(new[] { FieldPathSegment.Field("rateSurf") }, _current.surfRate.ToString()),
                new(new[] { FieldPathSegment.Field("rateRockSmash") }, _current.rockSmashRate.ToString()),
                new(new[] { FieldPathSegment.Field("rateOldRod") }, _current.oldRodRate.ToString()),
                new(new[] { FieldPathSegment.Field("rateGoodRod") }, _current.goodRodRate.ToString()),
                new(new[] { FieldPathSegment.Field("rateSuperRod") }, _current.superRodRate.ToString()),
                new(new[] { FieldPathSegment.Field("landSwarm") }, SpeciesSymbol(_current.swarmPokemon[0])),
                new(new[] { FieldPathSegment.Field("surfSwarm") }, SpeciesSymbol(_current.swarmPokemon[1])),
                new(new[] { FieldPathSegment.Field("nightFish") }, SpeciesSymbol(_current.swarmPokemon[2])),
                new(new[] { FieldPathSegment.Field("fishSwarm") }, SpeciesSymbol(_current.swarmPokemon[3])),
            };

            for (int i = 0; i < 12; i++)
            {
                fields.Add(new(new[] { FieldPathSegment.Field("landSlots"), FieldPathSegment.Field("levels"), FieldPathSegment.At(i) }, _current.walkingLevels[i].ToString()));
                fields.Add(new(new[] { FieldPathSegment.Field("landSlots"), FieldPathSegment.Field("speciesMorning"), FieldPathSegment.At(i) }, SpeciesSymbol(_current.morningPokemon[i])));
                fields.Add(new(new[] { FieldPathSegment.Field("landSlots"), FieldPathSegment.Field("speciesDay"), FieldPathSegment.At(i) }, SpeciesSymbol(_current.dayPokemon[i])));
                fields.Add(new(new[] { FieldPathSegment.Field("landSlots"), FieldPathSegment.Field("speciesNight"), FieldPathSegment.At(i) }, SpeciesSymbol(_current.nightPokemon[i])));
            }
            for (int i = 0; i < 2; i++)
            {
                fields.Add(new(new[] { FieldPathSegment.Field("hoennSoundSpecies"), FieldPathSegment.At(i) }, SpeciesSymbol(_current.hoennMusicPokemon[i])));
                fields.Add(new(new[] { FieldPathSegment.Field("sinnohSoundSpecies"), FieldPathSegment.At(i) }, SpeciesSymbol(_current.sinnohMusicPokemon[i])));
                AddSlot("rockSmashSlots", i, _current.rockSmashMinLevels[i], _current.rockSmashMaxLevels[i], _current.rockSmashPokemon[i]);
            }
            for (int i = 0; i < 5; i++)
            {
                AddSlot("surfSlots", i, _current.surfMinLevels[i], _current.surfMaxLevels[i], _current.surfPokemon[i]);
                AddSlot("oldRodSlots", i, _current.oldRodMinLevels[i], _current.oldRodMaxLevels[i], _current.oldRodPokemon[i]);
                AddSlot("goodRodSlots", i, _current.goodRodMinLevels[i], _current.goodRodMaxLevels[i], _current.goodRodPokemon[i]);
                AddSlot("superRodSlots", i, _current.superRodMinLevels[i], _current.superRodMaxLevels[i], _current.superRodPokemon[i]);
            }

            void AddSlot(string arrayName, int i, byte min, byte max, ushort speciesId)
            {
                fields.Add(new(new[] { FieldPathSegment.Field(arrayName), FieldPathSegment.At(i), FieldPathSegment.At(0) }, min.ToString()));
                fields.Add(new(new[] { FieldPathSegment.Field(arrayName), FieldPathSegment.At(i), FieldPathSegment.At(1) }, max.ToString()));
                fields.Add(new(new[] { FieldPathSegment.Field(arrayName), FieldPathSegment.At(i), FieldPathSegment.At(2) }, SpeciesSymbol(speciesId)));
            }

            if (!HgEngineWriter.TryWriteFields(HgEngineDomain.Encounters, _selectedEncounterIndex, fields, out var unresolved, out string error))
            { AppLogger.Error($"hg-engine write failed for encounter table {_selectedEncounterIndex}: {error}"); return; }

            if (unresolved.Count > 0)
                AppLogger.Info($"hg-engine write for encounter table {_selectedEncounterIndex}: source doesn't declare {string.Join(", ", unresolved)}, left unchanged.");
        }



        // ── Private helpers ───────────────────────────────────────────────
        private void OnNamesChanged(object sender, System.EventArgs e)
            => DSPRE.Avalonia.Data.ListSync.Apply(PokemonNames, DSPRE.RomInfo.GetPokemonNames());
        public void Detach()
        {
            AppEvents.NamesChanged -= OnNamesChanged;
            _pokemonIcons.Dispose();
        }

        private void SetDirty() { if (_loading) return; RecordUndoSnapshot(); _dirty = true;  Title = "● Wild Pokémon Editor (HGSS)"; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetClean() { _dirty = false; Title = "Wild Pokémon Editor (HGSS)";  OnPropertyChanged(nameof(HasUnsavedChanges)); }

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

                if (h.wildPokemon != MapHeader.HGSS_NULL_ENCOUNTER_FILE_ID)
                {
                    if (!locationMap.ContainsKey(h.wildPokemon)) locationMap[h.wildPokemon] = new System.Collections.Generic.List<string>();
                    int locIdx = h.wildPokemon < locationNames.Count ? h.wildPokemon : 0;
                    locationMap[h.wildPokemon].Add(locationNames[((HeaderHGSS)h).locationName]);
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
            _current = new EncounterFileHGSS(stream);
            PopulateRows();
            SetClean();

            _history.Reset(Snapshot());   // loaded state is the clean undo baseline for this file
            _lastCaptureUtc = DateTime.MinValue;
            RaiseUndoState();
        }

        private void PopulateRows()
        {
            _loading = true;
            WalkingRate   = _current.walkingRate;
            SurfRate      = _current.surfRate;
            RockSmashRate = _current.rockSmashRate;
            OldRodRate    = _current.oldRodRate;
            GoodRodRate   = _current.goodRodRate;
            SuperRodRate  = _current.superRodRate;

            string[] walkLabels = { "20%", "20%", "10%", "10%", "10%", "10%", "5%", "5%", "4%", "4%", "1%", "1%" };
            SyncRows(MorningRows, 12, i => walkLabels[i], i => _current.morningPokemon[i], i => _current.walkingLevels[i], null, null, false);
            SyncRows(DayRows,     12, i => walkLabels[i], i => _current.dayPokemon[i],     i => _current.walkingLevels[i], null, null, false);
            SyncRows(NightRows,   12, i => walkLabels[i], i => _current.nightPokemon[i],   i => _current.walkingLevels[i], null, null, false);

            string[] swarmLabels = { "Grass", "Surf", "Night Fish", "Rod" };
            SyncRows(SwarmRows,       4, i => swarmLabels[i],       i => _current.swarmPokemon[i],       _ => 0,  null, null, false);
            string[] radioLabels = { "Slot 1", "Slot 2" };
            SyncRows(HoennRadioRows,  2, i => radioLabels[i],       i => _current.hoennMusicPokemon[i],  _ => 0,  null, null, false);
            SyncRows(SinnohRadioRows, 2, i => radioLabels[i],       i => _current.sinnohMusicPokemon[i], _ => 0,  null, null, false);
            SyncRows(RockSmashRows,   2, i => $"Rock Smash {i+1}", i => _current.rockSmashPokemon[i],   _ => 0,
                i => _current.rockSmashMinLevels[i], i => _current.rockSmashMaxLevels[i], true);

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
                w.Write(new EncounterFileHGSS().ToByteArray());
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
            _current = new EncounterFileHGSS(stream);
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
                new EncounterFileHGSS(s).SaveToFileDefaultDir(i, showSuccessMessage: false);
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
            for (int i = 0; i < MorningRows.Count && i < 12; i++) _current.morningPokemon[i] = (ushort)MorningRows[i].PokemonIndex;
            for (int i = 0; i < DayRows.Count     && i < 12; i++) _current.dayPokemon[i]     = (ushort)DayRows[i].PokemonIndex;
            for (int i = 0; i < NightRows.Count   && i < 12; i++) _current.nightPokemon[i]   = (ushort)NightRows[i].PokemonIndex;
            for (int i = 0; i < 12; i++) _current.walkingLevels[i] = (byte)(MorningRows.Count > i ? MorningRows[i].Level : 0);
            for (int i = 0; i < SwarmRows.Count   && i < 4; i++) _current.swarmPokemon[i]       = (ushort)SwarmRows[i].PokemonIndex;
            for (int i = 0; i < HoennRadioRows.Count  && i < 2; i++) _current.hoennMusicPokemon[i]  = (ushort)HoennRadioRows[i].PokemonIndex;
            for (int i = 0; i < SinnohRadioRows.Count && i < 2; i++) _current.sinnohMusicPokemon[i] = (ushort)SinnohRadioRows[i].PokemonIndex;
            for (int i = 0; i < RockSmashRows.Count && i < 2; i++)
            {
                _current.rockSmashPokemon[i]   = (ushort)RockSmashRows[i].PokemonIndex;
                _current.rockSmashMinLevels[i] = (byte)RockSmashRows[i].MinLevel;
                _current.rockSmashMaxLevels[i] = (byte)RockSmashRows[i].MaxLevel;
            }
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
