using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using DSPRE.Resources;
using DSPRE.Avalonia.Views.Shell;
using DSPRE.Csv;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    public partial class PersonalDataEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo, IDetachable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
            if (n != nameof(HasUnsavedChanges)) return;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImportNote)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasImportNote)));
        }

        public string ImportNote
        {
            get
            {
                int n = _pending.Count + (_heldRecord && _dirty ? 1 : 0);
                return n == 0 ? "" : $"{n} imported Pokémon not saved yet";
            }
        }
        public bool HasImportNote => ImportNote.Length > 0;
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private bool _dirty;
        public bool HasUnsavedChanges => _dirty || _pending.Count > 0;
        public string UnsavedChangesDescription =>
            _pending.Count > 0 ? $"Personal data of {_pending.Count + (_dirty ? 1 : 0)} Pokémon"
            : _current != null ? $"Personal data of {(_currentId < PokemonNames.Count ? PokemonNames[_currentId] : $"Pokémon {_currentId}")}" : "Personal data";
        void IEditorWithUnsavedChanges.SaveChanges() => DialogHelper.Watch(SaveCommandAsync());
        async Task<bool> IEditorWithUnsavedChanges.SaveChangesAsync()
        {
            await SaveCommandAsync();
            return !HasUnsavedChanges;
        }
        public void DiscardChanges()
        {
            _pending.Clear();
            DiscardRecordEdits();
        }

        /// <summary>Puts back the shown Pokémon's saved state and keeps the other imported ones.</summary>
        public void DiscardRecordEdits()
        {
            _heldRecord = false;
            // Staged side-table values and follower operations exist only here, so restore the saved state.
            if (_dirty && _savedSnapshot != null && _current != null)
            {
                _history.Reset(_savedSnapshot);
                ApplyState(_savedSnapshot);
            }
            SetClean();
        }

        // Imported Pokémon waiting for Save. The shown one returns here on a switch, with no prompt.
        private readonly Dictionary<int, byte[]> _pending = new Dictionary<int, byte[]>();
        private bool _heldRecord;

        /// <summary>Edits a switch would lose. Imports can be held, other edits can't.</summary>
        public bool HasRecordEdits => _dirty && !(_heldRecord && OnlyDataChanged());

        /// <summary>Before another Pokémon loads: keeps the shown one's imported edits.</summary>
        public void HoldForSwitch()
        {
            if (!_heldRecord || !OnlyDataChanged()) return;
            if (_dirty) _pending[_currentId] = _current.ToByteArray();
            _heldRecord = false;
            SetClean();
        }

        private bool OnlyDataChanged()
        {
            if (_savedSnapshot == null || _current == null) return false;
            PersonalSnapshot now = Snapshot(), was = _savedSnapshot;
            return now.Hatch == was.Hatch && Equals(now.Hg, was.Hg) && Same(now.Followers, was.Followers)
                && Same(now.ExtraTms, was.ExtraTms) && Same(now.SourceMachineMoves, was.SourceMachineMoves)
                && (now.Athlon == null ? was.Athlon == null : was.Athlon != null && now.Athlon.Length == was.Athlon.Length
                    && now.Athlon.Zip(was.Athlon).All(r => r.First.AsSpan().SequenceEqual(r.Second)));
        }

        private static bool Same<T>(T[] a, T[] b) => a == null ? b == null : b != null && a.SequenceEqual(b);

        private void TakeImported(byte[] data)
        {
            _loading = true;
            _current = new PokemonPersonalData(new MemoryStream(data));
            PopulateFromCurrent();
            _loading = false;
            _lastCaptureUtc = DateTime.MinValue;
            RecordUndoSnapshot();
            _dirty = _history.IsDirty;
            Title = _dirty ? "● Personal data editor" : "Personal data editor";
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private (PokemonPersonalData Data, string Error) HeldData(int id)
        {
            if (id == _currentId && _current != null)
                return (new PokemonPersonalData(new MemoryStream(_current.ToByteArray())), _hgLoadError);
            if (_pending.TryGetValue(id, out byte[] staged)) return (new PokemonPersonalData(new MemoryStream(staged)), null);
            try
            {
                PokemonPersonalData d = new PokemonPersonalData(id);
                // The unpacked copy is the last build; Species.c is what the checkout holds now.
                if (HgEngineProject.IsActive && !HgEngineSpeciesPersonalFields.TryLoadInto(id, d, out string error))
                    return (null, "Species.c couldn't be read: " + error);
                return (d, null);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { return (null, e.Message); }
        }

        // Writes the imported Pokémon not shown into Species.c, inside the save's hg-engine session.
        private string WritePendingSource()
        {
            foreach ((int id, byte[] bytes) in _pending)
                if (!HgEngineSpeciesPersonalFields.TryWriteSource(id, new PokemonPersonalData(new MemoryStream(bytes)), out string error))
                    return $"Species {id}: {error}";
            return null;
        }

        private void FinishPending()
        {
            foreach ((int id, byte[] bytes) in _pending)
            {
                // Readers of the unpacked copy see the edit before the next build replaces it.
                new PokemonPersonalData(new MemoryStream(bytes)).SaveToFileDefaultDir(id, showSuccessMessage: false);
                AppEvents.RaisePersonalDataSaved(this, id);
            }
            _pending.Clear();
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private PokemonPersonalData _current;
        private int _currentId;
        private bool _loading;

        public PersonalDataEditorViewModel()
        {
            if (Design.IsDesignMode)
            {
                Title = "Personal data editor (Preview)";
                for (int i = 0; i < 10; i++) PokemonNames.Add($"Pokémon {i}");
                TypeNames.Add("Normal"); TypeNames.Add("Fire"); TypeNames.Add("Water");
                AbilityNames.Add("Overgrow"); AbilityNames.Add("Blaze"); AbilityNames.Add("Torrent");
                ItemNames.Add("----"); ItemNames.Add("Oran Berry"); ItemNames.Add("Sitrus Berry");
                foreach (string n in Enum.GetNames(typeof(PokemonGrowthCurve))) GrowthCurveNames.Add(n);
                foreach (string n in Enum.GetNames(typeof(PokemonDexColor)))    DexColorNames.Add(n);
                foreach (string n in Enum.GetNames(typeof(PokemonEggGroup)))    EggGroupNames.Add(n);
                AddedMachines.Add(new MachineRow { Label = "TM01 - Focus Punch" }); AddedMachines.Add(new MachineRow { Index = 1, Label = "TM02 - Dragon Claw" });
                AddableMachines.Add(new MachineRow { Index = 2, Label = "TM03 - Water Pulse" }); AddableMachines.Add(new MachineRow { Index = 3, Label = "TM04 - Calm Mind" });
                BaseHP = 45; BaseAtk = 49; BaseDef = 49; BaseSpe = 45; BaseSpA = 65; BaseSpD = 65;
                EvSpA = 1; CatchRate = 45; BaseExp = 64; GenderVec = 31; EggSteps = 20;
                BaseFriendship = 70; EscapeRate = 0;
                GenderLabel = GetGenderText(31);
                _monIconBitmap = null;
                return;
            }

            WireOwFrameTimer();
            _typeNamesArr    = GetTypeNames();
            _abilityNamesArr = GetAbilityNames();
            _itemNamesArr    = GetItemNames();
            _machineMoveNames = TMEditor.ReadMachineMoveNames().ToArray();
            _machineMoves = TMEditor.ReadMachineMoves();

            _allFileNames = GetPokemonNamesWithForms(GetPersonalFilesCount());

            foreach (string n in _allFileNames)    PokemonNames.Add(n);
            foreach (string n in _typeNamesArr)    TypeNames.Add(n);
            foreach (string n in _abilityNamesArr) AbilityNames.Add(n);
            foreach (string n in _itemNamesArr)    ItemNames.Add(n);
            LoadEnumLabels();
            LoadHeldItemOdds();

        }

        /// <summary>Takes the names from PokemonEditorViewModel so the ROM isn't read again.</summary>
        internal PersonalDataEditorViewModel(string[] pokemonNames)
        {
            WireOwFrameTimer();
            _typeNamesArr     = GetTypeNames();
            _abilityNamesArr  = GetAbilityNames();
            _itemNamesArr     = GetItemNames();
            _machineMoveNames = TMEditor.ReadMachineMoveNames().ToArray();
            _machineMoves = TMEditor.ReadMachineMoves();
            _allFileNames     = pokemonNames;

            foreach (string n in _allFileNames)    PokemonNames.Add(n);
            foreach (string n in _typeNamesArr)    TypeNames.Add(n);
            foreach (string n in _abilityNamesArr) AbilityNames.Add(n);
            foreach (string n in _itemNamesArr)    ItemNames.Add(n);
            LoadEnumLabels();
            LoadHeldItemOdds();
        }

        public void RefreshNames(string[] pokemonNames)
        {
            _typeNamesArr     = GetTypeNames();
            _abilityNamesArr  = GetAbilityNames();
            _itemNamesArr     = GetItemNames();
            _machineMoveNames = TMEditor.ReadMachineMoveNames().ToArray();
            _machineMoves = TMEditor.ReadMachineMoves();
            _allFileNames     = pokemonNames;
            ListSync.Apply(PokemonNames, _allFileNames);
            ListSync.Apply(TypeNames, _typeNamesArr);
            ListSync.Apply(AbilityNames, _abilityNamesArr);
            ListSync.Apply(ItemNames, _itemNamesArr);
            RebuildMachineLists();
        }

        // Labels come from the LabelStore, so a hack can rename them.
        private void LoadEnumLabels()
        {
            LabelStore.Sync(GrowthCurveNames, "pokemon_growth_curves");
            LabelStore.Sync(DexColorNames,    "pokemon_dex_colors");
            LabelStore.Sync(EggGroupNames,    "pokemon_egg_groups");
            AppEvents.LabelsChanged -= OnLabelsChanged;
            AppEvents.LabelsChanged += OnLabelsChanged;
            AppEvents.PersonalDataSaved -= OnPersonalDataSaved;
            AppEvents.PersonalDataSaved += OnPersonalDataSaved;
            _iconSaved = AppEvents.WhenSaved(() => { if (_current != null) ShowMonIcon(); }, DirNames.MonIcons);
            DSPRE.ROMFiles.WildHeldItemOdds.Saved -= OnHeldItemOddsSaved;
            DSPRE.ROMFiles.WildHeldItemOdds.Saved += OnHeldItemOddsSaved;
        }

        // Another editor wrote this species: reload when clean, else take only its machine changes.
        private void OnPersonalDataSaved(object sender, int id)
        {
            if (ReferenceEquals(sender, this) || _current == null || id != _currentId) return;
            if (!global::Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            {
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPersonalDataSaved(sender, id));
                return;
            }
            if (!_dirty) { LoadMon(id); return; }

            PokemonPersonalData onDisk;
            try { onDisk = new PokemonPersonalData(id); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                AppLogger.Warn($"Personal Data: couldn't re-read species {id}: {e.Message}");
                return;
            }

            PokemonPersonalData saved = new PokemonPersonalData(new MemoryStream(_savedSnapshot.Data));
            SortedSet<byte> was = new SortedSet<byte>(saved.Machines.Where(m => m < FirstMaskMachine));
            SortedSet<byte> now = new SortedSet<byte>(onDisk.Machines.Where(m => m < FirstMaskMachine));
            foreach (byte m in was) if (!now.Contains(m)) _current.Machines.Remove(m);
            foreach (byte m in now) if (!was.Contains(m)) _current.Machines.Add(m);
            saved.Machines = new SortedSet<byte>(now.Concat(saved.Machines.Where(m => m >= FirstMaskMachine)));

            PlatPatches.ExtraTms t = PlatPatches.Tms();
            if (t != null)
            {
                HashSet<int> maskNow = PlatPatches.Compatibility(t, new[] { id }, PlatPatches.PersonalMaskRows)
                    .Select(c => TMEditor.VanillaMachineCount + c.Row).ToHashSet();
                foreach (int m in _extraMaskTmsSaved.Except(maskNow)) _extraMaskTms.Remove(m);
                foreach (int m in maskNow.Except(_extraMaskTmsSaved)) _extraMaskTms.Add(m);
                _extraMaskTmsSaved = maskNow;
            }

            _savedSnapshot.Data = saved.ToByteArray();
            _savedSnapshot.ExtraTms = _extraMaskTmsSaved.OrderBy(i => i).ToArray();
            RebuildMachineLists();
            _history.Capture(Snapshot(), coalesce: true);
            RaiseUndoState();
        }

        private void OnLabelsChanged(object sender, EventArgs e)
        {
            LoadEnumLabels();
            // Avalonia keeps a stale SelectedItem when the entry is replaced, so each combo's field is re-set.
            Repoke(_growthCurveIndex, nameof(GrowthCurveIndex), v => _growthCurveIndex = v);
            Repoke(_dexColorIndex,    nameof(DexColorIndex),   v => _dexColorIndex = v);
            Repoke(_eggGroup1Index,   nameof(EggGroup1Index),  v => _eggGroup1Index = v);
            Repoke(_eggGroup2Index,   nameof(EggGroup2Index),  v => _eggGroup2Index = v);
        }

        // Blank now, restore on a later frame: a same-frame -1 then v toggle is merged and keeps the text.
        private void Repoke(int current, string name, Action<int> set)
        {
            if (current < 0) return;
            set(-1); OnPropertyChanged(name);
            global::Avalonia.Threading.Dispatcher.UIThread.Post(
                () => { set(current); OnPropertyChanged(name); },
                global::Avalonia.Threading.DispatcherPriority.Background);
        }

        private IDisposable _iconSaved;
        public void Detach() { _iconSaved?.Dispose(); AppEvents.LabelsChanged -= OnLabelsChanged; AppEvents.PersonalDataSaved -= OnPersonalDataSaved; DSPRE.ROMFiles.WildHeldItemOdds.Saved -= OnHeldItemOddsSaved; _owFrameTimer.Stop(); }

        // Wild held items saves on the UI thread, so the chances can be shown again straight away.
        private void OnHeldItemOddsSaved()
        {
            LoadHeldItemOdds();
            OnPropertyChanged(nameof(HasHeldItemOdds));
            OnPropertyChanged(nameof(CanOpenWildHeldItems));
            RaiseHeldItemOdds();
        }
    }
}
