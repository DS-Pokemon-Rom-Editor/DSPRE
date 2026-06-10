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
using DSPRE.ROMFiles;
using DSPRE.Resources;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
{
    public class PersonalDataEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        // ── IEditorWithUnsavedChanges ──────────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription =>
            _current != null ? $"Personal Data (Mon {_currentId} - {(_currentId < PokemonNames.Count ? PokemonNames[_currentId] : "")})" : "Personal Data Editor";
        void IEditorWithUnsavedChanges.SaveChanges() => _ = SaveCommand();
        public void DiscardChanges() => SetClean();

        // ── Name lists (ComboBox sources) ─────────────────────────────────────
        public ObservableCollection<string> PokemonNames  { get; } = new();
        public ObservableCollection<string> TypeNames     { get; } = new();
        public ObservableCollection<string> AbilityNames  { get; } = new();
        public ObservableCollection<string> ItemNames     { get; } = new();
        public ObservableCollection<string> GrowthCurveNames { get; } = new();
        public ObservableCollection<string> DexColorNames { get; } = new();
        public ObservableCollection<string> EggGroupNames { get; } = new();

        // ── TM machine list boxes ─────────────────────────────────────────────
        public ObservableCollection<string> AddedMachines    { get; } = new();
        public ObservableCollection<string> AddableMachines  { get; } = new();

        private int _selectedAddedMachineIndex = -1;
        public int SelectedAddedMachineIndex
        {
            get => _selectedAddedMachineIndex;
            set => Set(ref _selectedAddedMachineIndex, value);
        }

        private int _selectedAddableMachineIndex = -1;
        public int SelectedAddableMachineIndex
        {
            get => _selectedAddableMachineIndex;
            set => Set(ref _selectedAddableMachineIndex, value);
        }

        // ── Base stats
        private int _baseHP;   public int BaseHP   { get => _baseHP;   set { if (Set(ref _baseHP,   value) && !_loading) { _current.baseHP    = (byte)value; SetDirty(); } } }
        private int _baseAtk;  public int BaseAtk  { get => _baseAtk;  set { if (Set(ref _baseAtk,  value) && !_loading) { _current.baseAtk   = (byte)value; SetDirty(); } } }
        private int _baseDef;  public int BaseDef  { get => _baseDef;  set { if (Set(ref _baseDef,  value) && !_loading) { _current.baseDef   = (byte)value; SetDirty(); } } }
        private int _baseSpe;  public int BaseSpe  { get => _baseSpe;  set { if (Set(ref _baseSpe,  value) && !_loading) { _current.baseSpeed  = (byte)value; SetDirty(); } } }
        private int _baseSpA;  public int BaseSpA  { get => _baseSpA;  set { if (Set(ref _baseSpA,  value) && !_loading) { _current.baseSpAtk = (byte)value; SetDirty(); } } }
        private int _baseSpD;  public int BaseSpD  { get => _baseSpD;  set { if (Set(ref _baseSpD,  value) && !_loading) { _current.baseSpDef = (byte)value; SetDirty(); } } }

        // ── EV yields ─────────────────────────────────────────────────────────
        private int _evHP;  public int EvHP  { get => _evHP;  set { if (Set(ref _evHP,  value) && !_loading) { _current.evHP    = (byte)value; SetDirty(); } } }
        private int _evAtk; public int EvAtk { get => _evAtk; set { if (Set(ref _evAtk, value) && !_loading) { _current.evAtk   = (byte)value; SetDirty(); } } }
        private int _evDef; public int EvDef { get => _evDef; set { if (Set(ref _evDef, value) && !_loading) { _current.evDef   = (byte)value; SetDirty(); } } }
        private int _evSpe; public int EvSpe { get => _evSpe; set { if (Set(ref _evSpe, value) && !_loading) { _current.evSpeed = (byte)value; SetDirty(); } } }
        private int _evSpA; public int EvSpA { get => _evSpA; set { if (Set(ref _evSpA, value) && !_loading) { _current.evSpAtk = (byte)value; SetDirty(); } } }
        private int _evSpD; public int EvSpD { get => _evSpD; set { if (Set(ref _evSpD, value) && !_loading) { _current.evSpDef = (byte)value; SetDirty(); } } }

        // ── Types ─────────────────────────────────────────────────────────────
        private int _type1Index; public int Type1Index { get => _type1Index; set { if (Set(ref _type1Index, value) && !_loading && _current != null) { _current.type1 = (PokemonType)value; SetDirty(); } } }
        private int _type2Index; public int Type2Index { get => _type2Index; set { if (Set(ref _type2Index, value) && !_loading && _current != null) { _current.type2 = (PokemonType)value; SetDirty(); } } }

        // ── Abilities ─────────────────────────────────────────────────────────
        private int _ability1Index; public int Ability1Index { get => _ability1Index; set { if (Set(ref _ability1Index, value) && !_loading && _current != null) { _current.firstAbility  = (byte)value; SetDirty(); } } }
        private int _ability2Index; public int Ability2Index { get => _ability2Index; set { if (Set(ref _ability2Index, value) && !_loading && _current != null) { _current.secondAbility = (byte)value; SetDirty(); } } }

        // ── Held items ────────────────────────────────────────────────────────
        private int _item1Index; public int Item1Index { get => _item1Index; set { if (Set(ref _item1Index, value) && !_loading && _current != null) { _current.item1 = (ushort)value; SetDirty(); } } }
        private int _item2Index; public int Item2Index { get => _item2Index; set { if (Set(ref _item2Index, value) && !_loading && _current != null) { _current.item2 = (ushort)value; SetDirty(); } } }

        // ── Misc numeric ──────────────────────────────────────────────────────
        private int _catchRate;       public int CatchRate       { get => _catchRate;       set { if (Set(ref _catchRate,       value) && !_loading) { _current.catchRate       = (byte)value; SetDirty(); } } }
        private int _baseExp;         public int BaseExp         { get => _baseExp;         set { if (Set(ref _baseExp,         value) && !_loading) { _current.givenExp        = (byte)value; SetDirty(); } } }
        private int _genderVec;       public int GenderVec       { get => _genderVec;       set { if (Set(ref _genderVec,       value) && !_loading) { _current.genderVec       = (byte)value; GenderLabel = GetGenderText(value); SetDirty(); } } }
        private int _eggSteps;        public int EggSteps        { get => _eggSteps;        set { if (Set(ref _eggSteps,        value) && !_loading) { _current.eggSteps        = (byte)value; SetDirty(); } } }
        private int _baseFriendship;  public int BaseFriendship  { get => _baseFriendship;  set { if (Set(ref _baseFriendship,  value) && !_loading) { _current.baseFriendship  = (byte)value; SetDirty(); } } }
        private int _escapeRate;      public int EscapeRate      { get => _escapeRate;      set { if (Set(ref _escapeRate,      value) && !_loading) { _current.escapeRate      = (byte)value; SetDirty(); } } }

        // ── Combo selectors ───────────────────────────────────────────────────
        private int _growthCurveIndex; public int GrowthCurveIndex { get => _growthCurveIndex; set { if (Set(ref _growthCurveIndex, value) && !_loading && _current != null) { _current.growthCurve = (PokemonGrowthCurve)value; SetDirty(); } } }
        private int _dexColorIndex;    public int DexColorIndex    { get => _dexColorIndex;    set { if (Set(ref _dexColorIndex,    value) && !_loading && _current != null) { _current.color        = (PokemonDexColor)value;    SetDirty(); } } }
        private int _eggGroup1Index;   public int EggGroup1Index   { get => _eggGroup1Index;   set { if (Set(ref _eggGroup1Index,   value) && !_loading && _current != null) { _current.eggGroup1   = (byte)value; SetDirty(); } } }
        private int _eggGroup2Index;   public int EggGroup2Index   { get => _eggGroup2Index;   set { if (Set(ref _eggGroup2Index,   value) && !_loading && _current != null) { _current.eggGroup2   = (byte)value; SetDirty(); } } }
        private int _hatchResultIndex; public int HatchResultIndex { get => _hatchResultIndex; set { if (Set(ref _hatchResultIndex, value) && !_loading)                     SetDirty(); } }

        // ── Bool ──────────────────────────────────────────────────────────────
        private bool _flipFlag;
        public bool FlipFlag
        {
            get => _flipFlag;
            set { if (Set(ref _flipFlag, value) && !_loading && _current != null) { _current.flip = value; SetDirty(); } }
        }

        // ── Labels ────────────────────────────────────────────────────────────
        private string _genderLabel = string.Empty;
        public string GenderLabel { get => _genderLabel; private set => Set(ref _genderLabel, value); }

        private string _title = "Personal Data Editor";
        public string Title { get => _title; private set => Set(ref _title, value); }

        private Bitmap _monIconBitmap;
        public Bitmap MonIconBitmap { get => _monIconBitmap; private set => Set(ref _monIconBitmap, value); }

        // ── Private state ─────────────────────────────────────────────────────
        private PokemonPersonalData _current;
        private int _currentId;
        private bool _loading;
        private string[] _allFileNames;
        private string[] _machineMoveNames;
        private string[] _typeNamesArr;
        private string[] _abilityNamesArr;
        private string[] _itemNamesArr;

        // ── Constructor ───────────────────────────────────────────────────────
        public PersonalDataEditorViewModel()
        {
            if (Design.IsDesignMode)
            {
                Title = "Personal Data Editor (Preview)";
                for (int i = 0; i < 10; i++) PokemonNames.Add($"Pokémon {i}");
                TypeNames.Add("Normal"); TypeNames.Add("Fire"); TypeNames.Add("Water");
                AbilityNames.Add("Overgrow"); AbilityNames.Add("Blaze"); AbilityNames.Add("Torrent");
                ItemNames.Add("----"); ItemNames.Add("Oran Berry"); ItemNames.Add("Sitrus Berry");
                foreach (var n in Enum.GetNames(typeof(PokemonGrowthCurve))) GrowthCurveNames.Add(n);
                foreach (var n in Enum.GetNames(typeof(PokemonDexColor)))    DexColorNames.Add(n);
                foreach (var n in Enum.GetNames(typeof(PokemonEggGroup)))    EggGroupNames.Add(n);
                AddedMachines.Add("TM01 - Focus Punch"); AddedMachines.Add("TM02 - Dragon Claw");
                AddableMachines.Add("TM03 - Water Pulse"); AddableMachines.Add("TM04 - Calm Mind");
                BaseHP = 45; BaseAtk = 49; BaseDef = 49; BaseSpe = 45; BaseSpA = 65; BaseSpD = 65;
                EvSpA = 1; CatchRate = 45; BaseExp = 64; GenderVec = 31; EggSteps = 20;
                BaseFriendship = 70; EscapeRate = 0;
                GenderLabel = GetGenderText(31);
                _monIconBitmap = null;
                return;
            }

            _typeNamesArr    = GetTypeNames();
            _abilityNamesArr = GetAbilityNames();
            _itemNamesArr    = GetItemNames();
            _machineMoveNames = TMEditor.ReadMachineMoveNames().ToArray();

            // Build full pokemon name list (base + alt forms + extra)
            int count = GetPersonalFilesCount();
            string[] pokeNames = GetPokemonNames();
            var fullList = new List<string>(pokeNames);
            for (int i = 0; i < PokeDatabase.PersonalData.personalExtraFiles.Length; i++)
            {
                var e = PokeDatabase.PersonalData.personalExtraFiles[i];
                fullList.Add(fullList[e.monId] + " - " + e.description);
            }
            int extraCount = fullList.Count;
            for (int i = 0; i < count - extraCount; i++) fullList.Add($"Extra entry {fullList.Count}");
            _allFileNames = fullList.ToArray();

            foreach (var n in _allFileNames)    PokemonNames.Add(n);
            foreach (var n in _typeNamesArr)    TypeNames.Add(n);
            foreach (var n in _abilityNamesArr) AbilityNames.Add(n);
            foreach (var n in _itemNamesArr)    ItemNames.Add(n);
            foreach (var n in Enum.GetNames(typeof(PokemonGrowthCurve))) GrowthCurveNames.Add(n);
            foreach (var n in Enum.GetNames(typeof(PokemonDexColor)))    DexColorNames.Add(n);
            foreach (var n in Enum.GetNames(typeof(PokemonEggGroup)))    EggGroupNames.Add(n);

            // LoadMon is called by parent PokemonEditorViewModel
        }

        /// <summary>
        /// Runtime constructor called from <see cref="PokemonEditorViewModel"/> when names are
        /// already available, to avoid repeating expensive ROM reads.
        /// </summary>
        internal PersonalDataEditorViewModel(string[] pokemonNames)
        {
            _typeNamesArr     = GetTypeNames();
            _abilityNamesArr  = GetAbilityNames();
            _itemNamesArr     = GetItemNames();
            _machineMoveNames = TMEditor.ReadMachineMoveNames().ToArray();
            _allFileNames     = pokemonNames;

            foreach (var n in _allFileNames)    PokemonNames.Add(n);
            foreach (var n in _typeNamesArr)    TypeNames.Add(n);
            foreach (var n in _abilityNamesArr) AbilityNames.Add(n);
            foreach (var n in _itemNamesArr)    ItemNames.Add(n);
            foreach (var n in Enum.GetNames(typeof(PokemonGrowthCurve))) GrowthCurveNames.Add(n);
            foreach (var n in Enum.GetNames(typeof(PokemonDexColor)))    DexColorNames.Add(n);
            foreach (var n in Enum.GetNames(typeof(PokemonEggGroup)))    EggGroupNames.Add(n);
            // LoadMon is called by parent PokemonEditorViewModel after all child VMs are ready
        }

        // ── Commands ──────────────────────────────────────────────────────────

        public async Task SaveCommand()
        {
            if (_current == null) return;
            _current.SaveToFileDefaultDir(_currentId, showSuccessMessage: true);
            WriteHatchResult(_currentId, HatchResultIndex);
            SetClean();
        }

        public async Task ExportCommand(Window owner)
        {
            string path = await DialogHelper.SaveFile(owner, "Export Personal Data to CSV",
                new[] { DialogHelper.CsvFilter, DialogHelper.AllFilter }, "PersonalData.csv");
            if (path == null) return;
            try
            {
                using var writer = new StreamWriter(path);
                writer.WriteLine("Pokemon ID,Pokemon Name,Type 1,Type 2,Base HP,Base Atk,Base Def,Base SpAtk,Base SpDef,Base Speed," +
                    "EV HP,EV Atk,EV Def,EV SpAtk,EV SpDef,EV Speed," +
                    "Ability 1,Ability 2,Item 1,Item 2," +
                    "Catch Rate,Base Exp,Gender Ratio,Egg Steps,Base Friendship,Growth Curve," +
                    "Egg Group 1,Egg Group 2,Escape Rate,Dex Color,Flip");
                for (int i = 0; i < GetPersonalFilesCount(); i++)
                {
                    var d = new PokemonPersonalData(i);
                    string pn = i < _allFileNames.Length ? _allFileNames[i] : $"Pokemon_{i}";
                    string t1 = (int)d.type1 < _typeNamesArr.Length ? _typeNamesArr[(int)d.type1] : d.type1.ToString();
                    string t2 = (int)d.type2 < _typeNamesArr.Length ? _typeNamesArr[(int)d.type2] : d.type2.ToString();
                    string a1 = d.firstAbility  < _abilityNamesArr.Length ? _abilityNamesArr[d.firstAbility]  : $"Ability_{d.firstAbility}";
                    string a2 = d.secondAbility < _abilityNamesArr.Length ? _abilityNamesArr[d.secondAbility] : $"Ability_{d.secondAbility}";
                    string i1 = d.item1 < _itemNamesArr.Length ? _itemNamesArr[d.item1] : $"Item_{d.item1}";
                    string i2 = d.item2 < _itemNamesArr.Length ? _itemNamesArr[d.item2] : $"Item_{d.item2}";
                    writer.WriteLine($"{i},{pn},{t1},{t2},{d.baseHP},{d.baseAtk},{d.baseDef},{d.baseSpAtk},{d.baseSpDef},{d.baseSpeed}," +
                        $"{d.evHP},{d.evAtk},{d.evDef},{d.evSpAtk},{d.evSpDef},{d.evSpeed}," +
                        $"{a1},{a2},{i1},{i2}," +
                        $"{d.catchRate},{d.givenExp},{d.genderVec},{d.eggSteps},{d.baseFriendship},{d.growthCurve}," +
                        $"{(PokemonEggGroup)d.eggGroup1},{(PokemonEggGroup)d.eggGroup2},{d.escapeRate},{d.color},{d.flip}");
                }
                await DialogHelper.ShowInfo($"Exported to:\n{path}", "Export Complete");
            }
            catch (Exception ex) { await DialogHelper.ShowError($"Error: {ex.Message}", "Export Error"); }
        }

        public async Task ImportCommand(Window owner)
        {
            string path = await DialogHelper.OpenFile(owner, "Import Personal Data from CSV",
                new[] { DialogHelper.CsvFilter, DialogHelper.AllFilter });
            if (path == null) return;
            try
            {
                var lines = File.ReadAllLines(path);
                if (lines.Length < 2) { await DialogHelper.ShowError("File is empty or has no data rows.", "Import Error"); return; }
                int imported = 0, skipped = 0;
                for (int i = 1; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i])) continue;
                    var p = lines[i].Split(',');
                    if (p.Length < 31) { skipped++; continue; }
                    if (!int.TryParse(p[0].Trim(), out int id) || id < 0 || id >= GetPersonalFilesCount()) { skipped++; continue; }
                    var d = new PokemonPersonalData(id);
                    if (byte.TryParse(p[4].Trim(), out byte v)) d.baseHP    = v;
                    if (byte.TryParse(p[5].Trim(), out v))  d.baseAtk   = v;
                    if (byte.TryParse(p[6].Trim(), out v))  d.baseDef   = v;
                    if (byte.TryParse(p[7].Trim(), out v))  d.baseSpAtk = v;
                    if (byte.TryParse(p[8].Trim(), out v))  d.baseSpDef = v;
                    if (byte.TryParse(p[9].Trim(), out v))  d.baseSpeed = v;
                    if (byte.TryParse(p[10].Trim(), out v)) d.evHP    = v;
                    if (byte.TryParse(p[11].Trim(), out v)) d.evAtk   = v;
                    if (byte.TryParse(p[12].Trim(), out v)) d.evDef   = v;
                    if (byte.TryParse(p[13].Trim(), out v)) d.evSpAtk = v;
                    if (byte.TryParse(p[14].Trim(), out v)) d.evSpDef = v;
                    if (byte.TryParse(p[15].Trim(), out v)) d.evSpeed = v;
                    if (byte.TryParse(p[21].Trim(), out v)) d.catchRate     = v;
                    if (byte.TryParse(p[22].Trim(), out v)) d.givenExp      = v;
                    if (byte.TryParse(p[23].Trim(), out v)) d.genderVec     = v;
                    if (byte.TryParse(p[24].Trim(), out v)) d.eggSteps      = v;
                    if (byte.TryParse(p[25].Trim(), out v)) d.baseFriendship= v;
                    if (byte.TryParse(p[28].Trim(), out v)) d.escapeRate    = v;
                    if (Enum.TryParse(p[2].Trim(),  out PokemonType t1)) d.type1 = t1;
                    if (Enum.TryParse(p[3].Trim(),  out PokemonType t2)) d.type2 = t2;
                    d.SaveToFileDefaultDir(id, showSuccessMessage: false);
                    imported++;
                }
                if (_currentId >= 0) LoadMon(_currentId);
                await DialogHelper.ShowInfo($"Imported {imported} entries. Skipped {skipped}.", "Import Complete");
            }
            catch (Exception ex) { await DialogHelper.ShowError($"Error: {ex.Message}", "Import Error"); }
        }

        public void AddMachineCommand()
        {
            if (_current == null || _selectedAddableMachineIndex < 0 || _selectedAddableMachineIndex >= AddableMachines.Count) return;
            int idx = ZeroBasedIndexFromMachineName(AddableMachines[_selectedAddableMachineIndex]);
            _current.machines.Add((byte)idx);
            RebuildMachineLists();
            SetDirty();
        }

        public void RemoveMachineCommand()
        {
            if (_current == null || _selectedAddedMachineIndex < 0 || _selectedAddedMachineIndex >= AddedMachines.Count) return;
            int idx = ZeroBasedIndexFromMachineName(AddedMachines[_selectedAddedMachineIndex]);
            _current.machines.Remove((byte)idx);
            RebuildMachineLists();
            SetDirty();
        }

        public void AddAllMachinesCommand()
        {
            if (_current == null) return;
            byte tot = (byte)(PokemonPersonalData.tmsCount + PokemonPersonalData.hmsCount);
            _current.machines = new SortedSet<byte>();
            for (byte i = 0; i < tot; i++) _current.machines.Add(i);
            RebuildMachineLists();
            SetDirty();
        }

        public void RemoveAllMachinesCommand()
        {
            if (_current == null) return;
            _current.machines.Clear();
            RebuildMachineLists();
            SetDirty();
        }

        public async Task<bool> ConfirmCloseAsync()
        {
            if (!_dirty) return true;
            var r = await DialogHelper.AskYesNoCancel(
                "You have unsaved changes. Save before closing?", "Unsaved Changes");
            if (r == DialogHelper.MsgResult.Yes) { await SaveCommand(); return true; }
            return r == DialogHelper.MsgResult.No;
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private void SetDirty()  { if (_loading) return; _dirty = true;  Title = "Personal Data Editor*"; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetClean()  { _dirty = false; Title = "Personal Data Editor";  OnPropertyChanged(nameof(HasUnsavedChanges)); }

        private async Task ConfirmDiscardAsync(int newId)
        {
            bool discard = await DialogHelper.AskYesNo(
                "There are unsaved changes. Discard and proceed?", "Unsaved Changes");
            if (!discard) return;
            _dirty = false;
            LoadMon(newId);
        }

        internal void LoadMon(int id)
        {
            _loading = true;
            _currentId = id;
            _current   = new PokemonPersonalData(id);

            BaseHP  = _current.baseHP;  BaseAtk = _current.baseAtk; BaseDef = _current.baseDef;
            BaseSpe = _current.baseSpeed; BaseSpA = _current.baseSpAtk; BaseSpD = _current.baseSpDef;
            EvHP  = _current.evHP;  EvAtk = _current.evAtk; EvDef = _current.evDef;
            EvSpe = _current.evSpeed; EvSpA = _current.evSpAtk; EvSpD = _current.evSpDef;

            _type1Index = (int)_current.type1; OnPropertyChanged(nameof(Type1Index));
            _type2Index = (int)_current.type2; OnPropertyChanged(nameof(Type2Index));
            _ability1Index = _current.firstAbility;  OnPropertyChanged(nameof(Ability1Index));
            _ability2Index = _current.secondAbility; OnPropertyChanged(nameof(Ability2Index));
            _item1Index = _current.item1; OnPropertyChanged(nameof(Item1Index));
            _item2Index = _current.item2; OnPropertyChanged(nameof(Item2Index));

            CatchRate = _current.catchRate; BaseExp = _current.givenExp;
            GenderVec = _current.genderVec; EggSteps = _current.eggSteps;
            BaseFriendship = _current.baseFriendship; EscapeRate = _current.escapeRate;

            _growthCurveIndex = (int)_current.growthCurve; OnPropertyChanged(nameof(GrowthCurveIndex));
            _dexColorIndex    = (int)_current.color;        OnPropertyChanged(nameof(DexColorIndex));
            _eggGroup1Index   = _current.eggGroup1;         OnPropertyChanged(nameof(EggGroup1Index));
            _eggGroup2Index   = _current.eggGroup2;         OnPropertyChanged(nameof(EggGroup2Index));

            _flipFlag = _current.flip; OnPropertyChanged(nameof(FlipFlag));
            GenderLabel = GetGenderText(_current.genderVec);
            _hatchResultIndex = GetHatchResult(id); OnPropertyChanged(nameof(HatchResultIndex));

            RebuildMachineLists();

            // Load sprite icon
            int iconId = id;
            int excess = iconId - GetPokemonNames().Length;
            if (excess >= 0 && excess < PokeDatabase.PersonalData.personalExtraFiles.Length)
                iconId = PokeDatabase.PersonalData.personalExtraFiles[excess].iconId;
            try
            {
                var drawingImg = DSUtils.GetPokePic(iconId, 64, 64);
                MonIconBitmap = ImageConverter.ToAvaloniaBitmap(drawingImg);
            }
            catch { MonIconBitmap = null; }

            SetClean();
            _loading = false;
        }

        private void RebuildMachineLists()
        {
            AddedMachines.Clear();
            AddableMachines.Clear();
            if (_current == null || _machineMoveNames == null) return;
            byte tot = (byte)(PokemonPersonalData.tmsCount + PokemonPersonalData.hmsCount);
            for (byte i = 0; i < tot; i++)
            {
                string label = TMEditor.MachineLabelFromIndex(i);
                string move  = _machineMoveNames.Length > i ? _machineMoveNames[i] : $"UNK_{i}";
                string entry = $"{label} - {move}";
                if (_current.machines.Contains(i)) AddedMachines.Add(entry);
                else                               AddableMachines.Add(entry);
            }
        }

        private static int ZeroBasedIndexFromMachineName(string name)
        {
            var label = name.Split('-')[0].Trim();
            if (label.StartsWith("TM")) return int.Parse(label.Substring(2)) - 1;
            if (label.StartsWith("HM")) return int.Parse(label.Substring(2)) + PokemonPersonalData.tmsCount - 1;
            return -1;
        }

        private static string GetGenderText(int vec)
        {
            switch (vec)
            {
                case (byte)PokemonGender.Male:    return "100% Male";
                case (byte)PokemonGender.Female:  return "100% Female";
                case (byte)PokemonGender.Unknown: return "Gender Unknown";
                default:
                    float femalePct = 100 * ((vec + 1) / 256f);
                    return $"{100 - femalePct:F1}% Male / {femalePct:F1}% Female";
            }
        }

        private static int GetHatchResult(int monId)
        {
            string path = Path.Combine(dataPath, @"poketool/personal/pms.narc");
            if (!File.Exists(path)) return 0;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            int offset = monId * 2;
            if (offset + 1 > stream.Length) return 0;
            stream.Seek(offset, SeekOrigin.Begin);
            using var reader = new BinaryReader(stream);
            return reader.ReadUInt16();
        }

        private static void WriteHatchResult(int monId, int value)
        {
            string path = Path.Combine(dataPath, @"poketool/personal/pms.narc");
            if (!File.Exists(path)) return;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Write);
            int offset = monId * 2;
            if (offset + 1 > stream.Length) return;
            stream.Seek(offset, SeekOrigin.Begin);
            using var writer = new BinaryWriter(stream);
            writer.Write((ushort)value);
        }
    }
}
