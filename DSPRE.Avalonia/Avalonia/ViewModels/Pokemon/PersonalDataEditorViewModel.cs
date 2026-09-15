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
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    public class PersonalDataEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo
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
        async Task<bool> IEditorWithUnsavedChanges.SaveChangesAsync()
        {
            await SaveCommand();
            return !HasUnsavedChanges;
        }
        public void DiscardChanges()
        {
            // Staged side-table values and pending follower operations exist only here, so put back the saved state.
            if (_dirty && _savedSnapshot != null && _current != null)
            {
                _history.Reset(_savedSnapshot);
                ApplyState(_savedSnapshot);
            }
            SetClean();
        }

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
        private int _ability1Index; public int Ability1Index { get => _ability1Index; set { if (Set(ref _ability1Index, value) && !_loading && _current != null && value >= 0) { _current.firstAbility  = (ushort)value; SetDirty(); } } }
        private int _ability2Index; public int Ability2Index { get => _ability2Index; set { if (Set(ref _ability2Index, value) && !_loading && _current != null && value >= 0) { _current.secondAbility = (ushort)value; SetDirty(); } } }

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

        // ── hg-engine standalone data fields ────────────────────────────────────
        // Each lives in its own data/*.c file outside Species.c. Edits are staged with the rest of the tab and
        // SaveCommand writes only the ones that differ from what was loaded.
        public bool ShowHgEngineExtras => DSPRE.HgEngine.HgEngineProject.IsActive;
        public ObservableCollection<string> IconPaletteOptions { get; } = new() { "Palette 0", "Palette 1", "Palette 2" };
        private const int IconPaletteCount = 3;
        public ObservableCollection<string> FollowerBounceOptions { get; } = new();
        private List<(string Name, int Value)> _followerBounceValues = new();

        /// <summary>The hg-engine side-table values and pending follower operations, as staged or as loaded.</summary>
        internal sealed record HgStaged(int HiddenAbility, int BaseExp, int BabyMon, int RegionalDex, int IconPalette,
            int FollowerSize, int FollowerBounceIndex, string OwFemaleForm, int OwSizeClassIndex,
            bool CreateOwEntry, string OwSpritePath);

        internal delegate bool ExpressionValidator(string expression, out string error);

        private HgStaged _hgLoaded;           // null without a linked checkout
        private int _hgLoadedBounceValue;     // kept when the file's bounce isn't one of the named options

        private int _hgHiddenAbilityIndex;
        public int HgHiddenAbilityIndex
        {
            get => _hgHiddenAbilityIndex;
            set { if (!RejectCleared(value, nameof(HgHiddenAbilityIndex)) && Set(ref _hgHiddenAbilityIndex, value)) StageHg(); }
        }

        private int _hgBaseExp;
        public int HgBaseExp { get => _hgBaseExp; set { if (Set(ref _hgBaseExp, value)) StageHg(); } }

        private int _hgBabyMonIndex;
        public int HgBabyMonIndex
        {
            get => _hgBabyMonIndex;
            set { if (!RejectCleared(value, nameof(HgBabyMonIndex)) && Set(ref _hgBabyMonIndex, value)) StageHg(); }
        }

        private int _hgRegionalDexNumber;
        public int HgRegionalDexNumber { get => _hgRegionalDexNumber; set { if (Set(ref _hgRegionalDexNumber, value)) StageHg(); } }

        private int _hgIconPaletteIndex;
        public int HgIconPaletteIndex
        {
            get => _hgIconPaletteIndex;
            set
            {
                // The ComboBox reports -1 while it refills.
                if (value < 0 || value >= IconPaletteOptions.Count)
                {
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(HgIconPaletteIndex)));
                    return;
                }
                if (Set(ref _hgIconPaletteIndex, value)) StageHg();
            }
        }

        private int _hgFollowerSize;
        public int HgFollowerSize { get => _hgFollowerSize; set { if (Set(ref _hgFollowerSize, value)) StageHg(); } }

        private int _hgFollowerBounceIndex;
        public int HgFollowerBounceIndex
        {
            get => _hgFollowerBounceIndex;
            set { if (!RejectCleared(value, nameof(HgFollowerBounceIndex)) && Set(ref _hgFollowerBounceIndex, value)) StageHg(); }
        }

        // A ComboBox reports -1 while its list refills; staging that would record a change nobody made.
        private bool RejectCleared(int value, string name)
        {
            if (value >= 0 || _loading) return false;
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPropertyChanged(name));
            return true;
        }

        private void StageHg() { if (!_loading && _current != null) SetDirty(); }

        private string _hgOwFemaleFormExpression = "FALSE";
        public string HgOwFemaleFormExpression
        {
            get => _hgOwFemaleFormExpression;
            set
            {
                if (!Set(ref _hgOwFemaleFormExpression, value)) return;
                // The reason shows under the box while typing; Save refuses the value until it is fixed.
                HgOwFemaleFormError = OwFemaleFormProblem(value);
                StageHg();
            }
        }

        private string OwFemaleFormProblem(string expression) =>
            _hgLoaded == null || expression == _hgLoaded.OwFemaleForm
            || HgEngineSpeciesOwFormFemale.TryValidateRawExpression(expression, out string error) ? "" : error;

        private string _hgOwFemaleFormError = "";
        public string HgOwFemaleFormError
        {
            get => _hgOwFemaleFormError;
            private set { if (Set(ref _hgOwFemaleFormError, value)) OnPropertyChanged(nameof(HasHgOwFemaleFormError)); }
        }
        public bool HasHgOwFemaleFormError => !string.IsNullOrEmpty(_hgOwFemaleFormError);

        private void LoadHgEngineExtras()
        {
            _hgLoaded = null;
            _owPendingCreate = false;
            _owPendingSpritePath = null;
            if (!DSPRE.HgEngine.HgEngineProject.IsActive) return;

            _hgHiddenAbilityIndex = HgEngineHiddenAbility.TryGetAbilityId(_currentId, out int ha) ? ha : 0;
            OnPropertyChanged(nameof(HgHiddenAbilityIndex));

            _hgBaseExp = HgEngineBaseExperience.TryGetBaseExp(_currentId, out int be) ? be : 0;
            OnPropertyChanged(nameof(HgBaseExp));

            _hgBabyMonIndex = HgEngineBabyMon.TryGetBabySpecies(_currentId, out int baby) ? baby : 0;
            OnPropertyChanged(nameof(HgBabyMonIndex));

            _hgRegionalDexNumber = HgEngineRegionalDex.TryGetDexNumber(_currentId, out int dex) ? dex : 0;
            OnPropertyChanged(nameof(HgRegionalDexNumber));

            _hgIconPaletteIndex = HgEngineIconPalette.TryGetPaletteId(_currentId, out int pal) ? pal : 0;
            OnPropertyChanged(nameof(HgIconPaletteIndex));

            FollowerBounceOptions.Clear();
            _followerBounceValues = HgEngineFollowerProperties.GetBounceOptions();
            foreach (var opt in _followerBounceValues) FollowerBounceOptions.Add(opt.Name);
            HgEngineFollowerProperties.TryGet(_currentId, out int size, out int bounce, out _, out _);
            _hgLoadedBounceValue = bounce;
            _hgFollowerSize = size; OnPropertyChanged(nameof(HgFollowerSize));
            _hgFollowerBounceIndex = _followerBounceValues.FindIndex(o => o.Value == bounce);
            if (_hgFollowerBounceIndex < 0) _hgFollowerBounceIndex = 0;
            OnPropertyChanged(nameof(HgFollowerBounceIndex));

            _hgOwFemaleFormExpression = HgEngineSpeciesOwFormFemale.TryGetRawExpression(_currentId, out string expr) ? expr : "FALSE";
            OnPropertyChanged(nameof(HgOwFemaleFormExpression));
            HgOwFemaleFormError = "";

            LoadOwFollower();
            _hgLoaded = CaptureHg();
        }

        private HgStaged CaptureHg() => ShowHgEngineExtras
            ? new HgStaged(_hgHiddenAbilityIndex, _hgBaseExp, _hgBabyMonIndex, _hgRegionalDexNumber, _hgIconPaletteIndex,
                _hgFollowerSize, _hgFollowerBounceIndex, _hgOwFemaleFormExpression, _owSizeClassIndex, _owPendingCreate, _owPendingSpritePath)
            : null;

        private void ApplyHg(HgStaged s)
        {
            if (s == null) return;
            _hgHiddenAbilityIndex = s.HiddenAbility;      OnPropertyChanged(nameof(HgHiddenAbilityIndex));
            _hgBaseExp = s.BaseExp;                       OnPropertyChanged(nameof(HgBaseExp));
            _hgBabyMonIndex = s.BabyMon;                  OnPropertyChanged(nameof(HgBabyMonIndex));
            _hgRegionalDexNumber = s.RegionalDex;         OnPropertyChanged(nameof(HgRegionalDexNumber));
            _hgIconPaletteIndex = s.IconPalette;          OnPropertyChanged(nameof(HgIconPaletteIndex));
            _hgFollowerSize = s.FollowerSize;             OnPropertyChanged(nameof(HgFollowerSize));
            _hgFollowerBounceIndex = s.FollowerBounceIndex; OnPropertyChanged(nameof(HgFollowerBounceIndex));
            _hgOwFemaleFormExpression = s.OwFemaleForm;   OnPropertyChanged(nameof(HgOwFemaleFormExpression));
            _owSizeClassIndex = s.OwSizeClassIndex;       OnPropertyChanged(nameof(OwSizeClassIndex));
            HgOwFemaleFormError = OwFemaleFormProblem(s.OwFemaleForm);
            _owPendingCreate = s.CreateOwEntry;
            _owPendingSpritePath = s.OwSpritePath;
            RefreshOwPending();
        }

        /// <summary>Why the staged values can't be written, or null. Only values that differ from the loaded
        /// ones are checked, since only those are written.</summary>
        internal static string ValidateHgStaged(HgStaged staged, HgStaged loaded, int bounceOptionCount, int sizeClassOptionCount, ExpressionValidator validateOwFemaleForm)
        {
            if (staged == null || loaded == null) return null;
            if (staged.OwFemaleForm != loaded.OwFemaleForm && !validateOwFemaleForm(staged.OwFemaleForm, out string owError))
                return $"OW Female Form: {owError}";
            if (staged.HiddenAbility != loaded.HiddenAbility && staged.HiddenAbility < 0) return "Pick a hidden ability.";
            if (staged.BabyMon != loaded.BabyMon && staged.BabyMon < 0) return "Pick a baby Pokémon.";
            if (staged.BaseExp != loaded.BaseExp && (staged.BaseExp < 0 || staged.BaseExp > ushort.MaxValue))
                return $"Base Exp must be 0 to {ushort.MaxValue}.";
            if (staged.RegionalDex != loaded.RegionalDex && (staged.RegionalDex < 0 || staged.RegionalDex > ushort.MaxValue))
                return $"Regional Dex # must be 0 to {ushort.MaxValue}.";
            if (staged.IconPalette != loaded.IconPalette && (staged.IconPalette < 0 || staged.IconPalette >= IconPaletteCount))
                return "Pick an icon palette.";
            if (staged.FollowerSize != loaded.FollowerSize && staged.FollowerSize is < 0 or > 1) return "Follower size must be 0 or 1.";
            if (staged.FollowerBounceIndex != loaded.FollowerBounceIndex && (staged.FollowerBounceIndex < 0 || staged.FollowerBounceIndex >= bounceOptionCount))
                return "Pick a bounce speed.";
            if (staged.OwSizeClassIndex != loaded.OwSizeClassIndex && (staged.OwSizeClassIndex < 0 || staged.OwSizeClassIndex >= sizeClassOptionCount))
                return "Pick a size class.";
            return null;
        }

        // ── Overworld follower sprite ─────────────────────────────────────────
        private int _owGfxIndex = -1;
        private bool _owHasSpriteFiles;
        private List<string> _owSizeClassValues = new();

        public ObservableCollection<string> OwSizeClassOptions { get; } = new();

        private bool _owHasEntry;
        public bool OwHasEntry { get => _owHasEntry; private set => Set(ref _owHasEntry, value); }

        private string _owStatusText = "";
        public string OwStatusText { get => _owStatusText; private set => Set(ref _owStatusText, value); }

        private Bitmap[] _owFrames = Array.Empty<Bitmap>();
        public Bitmap OwFollowerPreview => _owFrameIndex >= 0 && _owFrameIndex < _owFrames.Length ? _owFrames[_owFrameIndex] : null;

        public int OwFrameCount => _owFrames.Length;
        public int OwMaxFrameIndex => Math.Max(0, _owFrames.Length - 1);

        private int _owFrameIndex;
        public int OwFrameIndex
        {
            get => _owFrameIndex;
            set
            {
                if (_owFrames.Length == 0) return;
                int clamped = Math.Clamp(value, 0, _owFrames.Length - 1);
                if (!Set(ref _owFrameIndex, clamped)) return;
                OnPropertyChanged(nameof(OwFollowerPreview));
            }
        }

        private bool _owAnimating;
        public bool OwAnimating
        {
            get => _owAnimating;
            set { if (Set(ref _owAnimating, value)) OnPropertyChanged(nameof(OwAnimateButtonText)); }
        }
        public string OwAnimateButtonText => OwAnimating ? "Stop" : "Animate";
        public bool OwCanAnimate => _owFrames.Length > 1;

        private readonly global::Avalonia.Threading.DispatcherTimer _owFrameTimer =
            new() { Interval = TimeSpan.FromMilliseconds(150) };

        private void WireOwFrameTimer()
        {
            _owFrameTimer.Tick += (_, _) =>
            {
                if (OwAnimating) OwFrameIndex = (OwFrameIndex + 1) % Math.Max(1, _owFrames.Length);
            };
            _owFrameTimer.Start();
        }

        // Only shown/needed when this species' sprite files don't exist yet: cloning metadata from an
        // existing entry is the only supported way to create them (see HgEngineOverworldFollowerSprite).
        public bool OwNeedsTemplate => _owHasEntry && !_owHasSpriteFiles;

        private int _owTemplateSpeciesIndex = -1;
        public int OwTemplateSpeciesIndex { get => _owTemplateSpeciesIndex; set => Set(ref _owTemplateSpeciesIndex, value); }

        private int _owSizeClassIndex = -1;
        public int OwSizeClassIndex
        {
            get => _owSizeClassIndex;
            set { if (!RejectCleared(value, nameof(OwSizeClassIndex)) && Set(ref _owSizeClassIndex, value) && _owHasEntry) StageHg(); }
        }

        // Performed by SaveCommand; dropped by Discard or a species switch.
        private bool _owPendingCreate;
        private string _owPendingSpritePath;
        private string _owDiskPngPath;
        private string _owPreviewPath;

        private string _owPendingText = "";
        public string OwPendingText
        {
            get => _owPendingText;
            private set { if (Set(ref _owPendingText, value)) OnPropertyChanged(nameof(HasOwPending)); }
        }
        public bool HasOwPending => _owPendingText.Length > 0;
        public bool CanCreateOwEntry => !_owHasEntry && !_owPendingCreate;

        private void RefreshOwPending()
        {
            var parts = new List<string>();
            if (_owPendingCreate) parts.Add("The follower entry is created on save.");
            if (_owPendingSpritePath != null) parts.Add($"{Path.GetFileName(_owPendingSpritePath)} is imported on save.");
            OwPendingText = string.Join(" ", parts);
            OnPropertyChanged(nameof(CanCreateOwEntry));

            string preview = _owPendingSpritePath ?? _owDiskPngPath;
            if (preview == _owPreviewPath) return;
            _owPreviewPath = preview;
            Bitmap[] frames = Array.Empty<Bitmap>();
            if (preview != null)
            {
                try { frames = DSPRE.Avalonia.ImageConverter.LoadHgeOverworldFrames(preview); }
                catch { frames = Array.Empty<Bitmap>(); }
            }
            SetOwFrames(frames);
        }

        private void SetOwFrames(Bitmap[] frames)
        {
            _owFrames = frames;
            _owFrameIndex = 0;
            OwAnimating = false;
            OnPropertyChanged(nameof(OwFollowerPreview));
            OnPropertyChanged(nameof(OwFrameIndex));
            OnPropertyChanged(nameof(OwFrameCount));
            OnPropertyChanged(nameof(OwMaxFrameIndex));
            OnPropertyChanged(nameof(OwCanAnimate));
        }

        private void LoadOwFollower()
        {
            SetOwFrames(Array.Empty<Bitmap>());
            _owHasSpriteFiles = false;
            _owDiskPngPath = null;
            _owPreviewPath = null;

            if (!DSPRE.HgEngine.HgEngineProject.IsActive)
            {
                OwHasEntry = false;
                OwStatusText = "";
            }
            else if (!HgEngineOverworldFollowerSprite.TryGetAssignment(_currentId, out _owGfxIndex, out string sizeClass, out _))
            {
                OwHasEntry = false;
                OwStatusText = "No overworld follower entry for this species yet.";
            }
            else
            {
                OwHasEntry = true;
                _owSizeClassValues = HgEngineOverworldFollowerSprite.GetSizeClassOptions();
                OwSizeClassOptions.Clear();
                foreach (var s in _owSizeClassValues) OwSizeClassOptions.Add(s);
                _owSizeClassIndex = _owSizeClassValues.IndexOf(sizeClass);
                OnPropertyChanged(nameof(OwSizeClassIndex));

                // The species' own data/graphics/sprites/<name>/overworld.png (same convention as icon.png)
                // is the real, populated walk sprite; the newer per-form gfx-index table is a fallback for
                // species that only have art registered there.
                _owDiskPngPath = HgEngineOverworldSprite.TryGetSpritePngPath(_currentId, out string ownPath) ? ownPath
                    : HgEngineOverworldFollowerSprite.TryGetSpritePngPath(_owGfxIndex);
                _owHasSpriteFiles = _owDiskPngPath != null;
                OwStatusText = $"Overworld gfx #{_owGfxIndex}" + (_owHasSpriteFiles ? "" : " (no sprite art yet, import one below)");
            }
            OnPropertyChanged(nameof(OwNeedsTemplate));
            RefreshOwPending();
        }

        public void CreateOwFollowerEntry()
        {
            if (_current == null || !CanCreateOwEntry) return;
            _owPendingCreate = true;
            RefreshOwPending();
            SetDirty();
        }

        /// <summary>Stages the sheet and previews it; the template species is read when saving.</summary>
        public void ImportOwFollowerSprite(string pngSourcePath)
        {
            if (_current == null || !_owHasEntry || _owGfxIndex < 0) return;
            _owPendingSpritePath = pngSourcePath;
            RefreshOwPending();
            SetDirty();
        }

        // ── Private state ─────────────────────────────────────────────────────
        private PokemonPersonalData _current;
        private int _currentId;
        private bool _loading;

        // ── Undo / redo (ISupportsUndo) ────────────────────────────────────────
        // Composite snapshot: the personal-data file bytes + the hatch-result (which lives in a separate
        // table, not in the file) + the staged hg-engine side-table values. Edit bursts within CoalesceMs
        // collapse into one undo step.
        private sealed class PersonalSnapshot { public byte[] Data; public int Hatch; public HgStaged Hg; }
        private readonly DSPRE.Avalonia.UndoHistory<PersonalSnapshot> _history = new();
        private PersonalSnapshot _savedSnapshot;   // UndoHistory doesn't expose it, and Discard restores it
        private DateTime _lastCaptureUtc = DateTime.MinValue;
        private const int CoalesceMs = 500;

        public bool CanUndo => _history.CanUndo;
        public bool CanRedo => _history.CanRedo;
        public void Undo() { if (_history.CanUndo) ApplyState(_history.Undo()); }
        public void Redo() { if (_history.CanRedo) ApplyState(_history.Redo()); }
        private void RaiseUndoState() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private PersonalSnapshot Snapshot() =>
            new PersonalSnapshot { Data = _current.ToByteArray(), Hatch = _hatchResultIndex, Hg = CaptureHg() };

        private void ApplyState(PersonalSnapshot snap)
        {
            if (snap == null || _current == null) return;
            _loading = true;
            _current = new PokemonPersonalData(new MemoryStream(snap.Data));
            PopulateFromCurrent();
            _hatchResultIndex = snap.Hatch; OnPropertyChanged(nameof(HatchResultIndex));
            ApplyHg(snap.Hg);
            _loading = false;

            _dirty = _history.IsDirty;
            Title = _dirty ? "● Personal Data Editor" : "Personal Data Editor";
            OnPropertyChanged(nameof(HasUnsavedChanges));
            RaiseUndoState();
        }

        private void RecordUndoSnapshot()
        {
            if (_current == null) return;
            bool coalesce = (DateTime.UtcNow - _lastCaptureUtc).TotalMilliseconds < CoalesceMs;
            _history.Capture(Snapshot(), coalesce);
            _lastCaptureUtc = DateTime.UtcNow;
            RaiseUndoState();
        }
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

            WireOwFrameTimer();
            _typeNamesArr    = GetTypeNames();
            _abilityNamesArr = GetAbilityNames();
            _itemNamesArr    = GetItemNames();
            _machineMoveNames = TMEditor.ReadMachineMoveNames().ToArray();

            // Full pokemon name list (base + alt forms this ROM actually has data for + extra)
            _allFileNames = GetPokemonNamesWithForms(GetPersonalFilesCount());

            foreach (var n in _allFileNames)    PokemonNames.Add(n);
            foreach (var n in _typeNamesArr)    TypeNames.Add(n);
            foreach (var n in _abilityNamesArr) AbilityNames.Add(n);
            foreach (var n in _itemNamesArr)    ItemNames.Add(n);
            LoadEnumLabels();

            // LoadMon is called by parent PokemonEditorViewModel
        }

        /// <summary>
        /// Runtime constructor called from <see cref="PokemonEditorViewModel"/> when names are
        /// already available, to avoid repeating expensive ROM reads.
        /// </summary>
        internal PersonalDataEditorViewModel(string[] pokemonNames)
        {
            WireOwFrameTimer();
            _typeNamesArr     = GetTypeNames();
            _abilityNamesArr  = GetAbilityNames();
            _itemNamesArr     = GetItemNames();
            _machineMoveNames = TMEditor.ReadMachineMoveNames().ToArray();
            _allFileNames     = pokemonNames;

            foreach (var n in _allFileNames)    PokemonNames.Add(n);
            foreach (var n in _typeNamesArr)    TypeNames.Add(n);
            foreach (var n in _abilityNamesArr) AbilityNames.Add(n);
            foreach (var n in _itemNamesArr)    ItemNames.Add(n);
            LoadEnumLabels();
            // LoadMon is called by parent PokemonEditorViewModel after all child VMs are ready
        }

        /// <summary>Growth-curve / egg-group / dex-color dropdowns come from the customisable LabelStore
        /// (Tools ▸ Edit Dropdown Labels), so a ROM hack can rename/add them. Indices stay stable.</summary>
        private void LoadEnumLabels()
        {
            DSPRE.Avalonia.Data.LabelStore.Sync(GrowthCurveNames, "pokemon_growth_curves");
            DSPRE.Avalonia.Data.LabelStore.Sync(DexColorNames,    "pokemon_dex_colors");
            DSPRE.Avalonia.Data.LabelStore.Sync(EggGroupNames,    "pokemon_egg_groups");
            AppEvents.LabelsChanged -= OnLabelsChanged;   // idempotent (re)subscribe so live edits refresh
            AppEvents.LabelsChanged += OnLabelsChanged;
        }

        private void OnLabelsChanged(object sender, EventArgs e)
        {
            LoadEnumLabels();
            // Re-resolve each combo's displayed text (Avalonia keeps a stale SelectedItem when the selected
            // entry is replaced in place). Toggle via the backing field only, no data write.
            Repoke(_growthCurveIndex, nameof(GrowthCurveIndex), v => _growthCurveIndex = v);
            Repoke(_dexColorIndex,    nameof(DexColorIndex),   v => _dexColorIndex = v);
            Repoke(_eggGroup1Index,   nameof(EggGroup1Index),  v => _eggGroup1Index = v);
            Repoke(_eggGroup2Index,   nameof(EggGroup2Index),  v => _eggGroup2Index = v);
        }

        // Blank the combo this frame, restore on a LATER frame so the ComboBox re-resolves its displayed
        // item (a synchronous -1→v toggle gets coalesced into one update and the stale text stays).
        private void Repoke(int current, string name, Action<int> set)
        {
            if (current < 0) return;
            set(-1); OnPropertyChanged(name);
            global::Avalonia.Threading.Dispatcher.UIThread.Post(
                () => { set(current); OnPropertyChanged(name); },
                global::Avalonia.Threading.DispatcherPriority.Background);
        }

        /// <summary>Unsubscribes from app-wide events; called when the host window closes.</summary>
        public void Detach() { AppEvents.LabelsChanged -= OnLabelsChanged; _owFrameTimer.Stop(); }

        // ── Commands ──────────────────────────────────────────────────────────

        public async Task SaveCommand()
        {
            if (_current == null) return;
            string sourceError = null;
            bool partlySaved = false;
            if (HgEngineProject.IsActive)
            {
                var pending = (_hgLoaded, _hgLoadedBounceValue, _owPendingCreate, _owPendingSpritePath);
                var (committed, commitError) = await DSPRE.Avalonia.HgEngineSave.RunAsync(() =>
                {
                    sourceError = SaveHgEngineSource(out partlySaved);
                    // Steps that did succeed are kept when a later one fails.
                    return partlySaved ? null : sourceError;
                });
                if (!committed)
                {
                    // Nothing reached disk, so the save's own bookkeeping goes back to what is still pending.
                    (_hgLoaded, _hgLoadedBounceValue, _owPendingCreate, _owPendingSpritePath) = pending;
                    bool wasLoading = _loading;
                    _loading = true;
                    LoadOwFollower();
                    _loading = wasLoading;
                    partlySaved = false;
                    sourceError = commitError ?? sourceError;
                    if (sourceError == null) return;
                }
            }
            if (sourceError != null)
            {
                // Operations that did finish are no longer pending.
                if (partlySaved) _history.Capture(Snapshot(), coalesce: true);
                string lead = partlySaved ? $"Species {_currentId} was only partly saved:" : $"Species {_currentId} was not saved:";
                await DSPRE.Avalonia.DialogHelper.ShowError($"{lead}\n{sourceError}", "Personal Data");
                return;
            }
            _current.SaveToFileDefaultDir(_currentId, showSuccessMessage: true);
            // hg-engine rebuilds pms.narc from data/BabyMons.c, which the Baby Pokémon picker edits.
            if (!HgEngineProject.IsActive) WriteHatchResult(_currentId, HatchResultIndex);
            var saved = Snapshot();
            _history.Capture(saved, coalesce: true);
            _history.MarkSaved();
            _savedSnapshot = saved;
            SetClean();
            SaveNotice.Saved(UnsavedChangesDescription);
            RaiseUndoState();
        }

        /// <summary>Writes Species.c, then each side-table value that differs from what was loaded, then the
        /// pending follower operations. Returns why it stopped, or null. Nothing is written while the entry
        /// didn't load cleanly or a staged value is invalid. The TM list is not here: hg-engine keeps machine
        /// moves in its learnset source.</summary>
        private string SaveHgEngineSource(out bool partlySaved)
        {
            partlySaved = false;
            if (!HgEngineProject.IsActive) return null;
            if (_hgLoadError != null) return _hgLoadError;

            var s = CaptureHg();
            var loaded = _hgLoaded ?? s;
            string invalid = ValidateHgStaged(s, loaded, _followerBounceValues.Count, _owSizeClassValues.Count, HgEngineSpeciesOwFormFemale.TryValidateRawExpression);
            if (invalid != null) return invalid;

            int? templateGfx = null;
            if (s.OwSpritePath != null)
            {
                if (!File.Exists(s.OwSpritePath)) return $"{Path.GetFileName(s.OwSpritePath)} was not found.";
                if (OwNeedsTemplate)
                {
                    if (_owTemplateSpeciesIndex < 0 || _owTemplateSpeciesIndex >= PokemonNames.Count)
                        return "Pick a template species for the overworld sprite.";
                    if (!HgEngineOverworldFollowerSprite.TryGetAssignment(_owTemplateSpeciesIndex, out int gfx, out _, out string templateError))
                        return $"Template species: {templateError}";
                    templateGfx = gfx;
                }
            }

            if (!HgEngineSpeciesPersonalFields.TryWriteSource(_currentId, _current, out string error))
            {
                AppLogger.Error($"hg-engine write failed for species {_currentId}: {error}");
                return error;
            }
            partlySaved = true;

            int id = _currentId;
            var failures = new List<string>();
            bool Step(bool changed, string what, Func<string> write)
            {
                if (!changed) return false;
                string stepError = write();
                if (stepError == null) return true;
                AppLogger.Error($"hg-engine {what} write failed for species {id}: {stepError}");
                failures.Add($"{what}: {stepError}");
                return false;
            }

            if (Step(s.HiddenAbility != loaded.HiddenAbility, "Hidden Ability",
                    () => HgEngineHiddenAbility.TrySetAbilityId(id, s.HiddenAbility, out string e) ? null : e))
                loaded = loaded with { HiddenAbility = s.HiddenAbility };
            if (Step(s.BaseExp != loaded.BaseExp, "Base Exp",
                    () => HgEngineBaseExperience.TrySetBaseExp(id, s.BaseExp, out string e) ? null : e))
                loaded = loaded with { BaseExp = s.BaseExp };
            if (Step(s.BabyMon != loaded.BabyMon, "Baby Pokémon",
                    () => HgEngineBabyMon.TrySetBabySpecies(id, s.BabyMon, out string e) ? null : e))
                loaded = loaded with { BabyMon = s.BabyMon };
            if (Step(s.RegionalDex != loaded.RegionalDex, "Regional Dex #",
                    () => HgEngineRegionalDex.TrySetDexNumber(id, s.RegionalDex, out string e) ? null : e))
                loaded = loaded with { RegionalDex = s.RegionalDex };
            // The Battle Display tab writes this table too, and also only when its own value changed.
            if (Step(s.IconPalette != loaded.IconPalette, "Icon Palette",
                    () => HgEngineIconPalette.TrySetPaletteId(id, s.IconPalette, out string e) ? null : e))
                loaded = loaded with { IconPalette = s.IconPalette };

            // A bounce the named options don't cover is written back as it was.
            int bounceValue = s.FollowerBounceIndex == loaded.FollowerBounceIndex ? _hgLoadedBounceValue : _followerBounceValues[s.FollowerBounceIndex].Value;
            if (Step(s.FollowerSize != loaded.FollowerSize || s.FollowerBounceIndex != loaded.FollowerBounceIndex, "Follower",
                    () => HgEngineFollowerProperties.TrySet(id, s.FollowerSize, bounceValue, out string e) ? null : e))
            {
                loaded = loaded with { FollowerSize = s.FollowerSize, FollowerBounceIndex = s.FollowerBounceIndex };
                _hgLoadedBounceValue = bounceValue;
            }
            if (Step(s.OwFemaleForm != loaded.OwFemaleForm, "OW Female Form",
                    () => HgEngineSpeciesOwFormFemale.TrySetRawExpression(id, s.OwFemaleForm, out string e) ? null : e))
                loaded = loaded with { OwFemaleForm = s.OwFemaleForm };
            if (Step(_owHasEntry && s.OwSizeClassIndex != loaded.OwSizeClassIndex, "Size Class",
                    () => HgEngineOverworldFollowerSprite.TrySetSizeClass(id, _owSizeClassValues[s.OwSizeClassIndex], out string e) ? null : e))
                loaded = loaded with { OwSizeClassIndex = s.OwSizeClassIndex };

            bool followerFilesChanged = false;
            if (Step(s.CreateOwEntry, "Overworld follower entry",
                    () => HgEngineOverworldFollowerSprite.TryEnsureEntry(id, out _, out string e) ? null : e))
            { _owPendingCreate = false; followerFilesChanged = true; }
            string label = id < PokemonNames.Count ? PokemonNames[id] : id.ToString();
            if (Step(s.OwSpritePath != null, "Overworld sprite",
                    () => HgEngineOverworldFollowerSprite.TryImportSprite(_owGfxIndex, s.OwSpritePath, templateGfx, label, out string e) ? null : e))
            { _owPendingSpritePath = null; followerFilesChanged = true; }

            if (followerFilesChanged)
            {
                bool wasLoading = _loading;
                _loading = true;
                LoadOwFollower();
                _loading = wasLoading;
                loaded = loaded with { OwSizeClassIndex = _owSizeClassIndex };
            }
            _hgLoaded = loaded with { CreateOwEntry = false, OwSpritePath = null };
            HgOwFemaleFormError = OwFemaleFormProblem(_hgOwFemaleFormExpression);

            return failures.Count == 0 ? null : string.Join("\n", failures);
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
                // The unpacked copy is the last build; Species.c is what the checkout holds now.
                bool fromSource = HgEngineProject.IsActive;
                var unread = new List<int>();
                for (int i = 0; i < GetPersonalFilesCount(); i++)
                {
                    var d = new PokemonPersonalData(i);
                    if (fromSource && !HgEngineSpeciesPersonalFields.TryLoadInto(i, d, out _)) { unread.Add(i); continue; }
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
                string note = unread.Count == 0 ? ""
                    : $"\n\n{unread.Count} Pokémon could not be read from Species.c and were left out: {string.Join(", ", unread.Take(10))}{(unread.Count > 10 ? ", ..." : "")}";
                await DialogHelper.ShowInfo($"Exported to:\n{path}{note}", "Export Complete");
            }
            catch (Exception ex) { await DialogHelper.ShowError($"Error: {ex.Message}", "Export Error"); }
        }

        public async Task ImportCommand(Window owner)
        {
            // The reload after importing would drop this Pokémon's staged edits.
            if (HasUnsavedChanges) { await DialogHelper.ShowError("Save or discard this Pokémon's changes first.", "Import Error"); return; }
            string path = await DialogHelper.OpenFile(owner, "Import Personal Data from CSV",
                new[] { DialogHelper.CsvFilter, DialogHelper.AllFilter });
            if (path == null) return;
            try
            {
                var lines = File.ReadAllLines(path);
                if (lines.Length < 2) { await DialogHelper.ShowError("File is empty or has no data rows.", "Import Error"); return; }
                int imported = 0, skipped = 0;
                // The next build replaces the unpacked copy from Species.c, so on hg-engine that is where rows go.
                bool toSource = HgEngineProject.IsActive;
                var failed = new List<string>();
                for (int i = 1; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i])) continue;
                    var p = lines[i].Split(',');
                    if (p.Length < 31) { skipped++; continue; }
                    if (!int.TryParse(p[0].Trim(), out int id) || id < 0 || id >= GetPersonalFilesCount()) { skipped++; continue; }
                    var d = new PokemonPersonalData(id);
                    if (toSource && !HgEngineSpeciesPersonalFields.TryLoadInto(id, d, out string readError)) { failed.Add($"{id}: {readError}"); continue; }
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
                    if (toSource && !HgEngineSpeciesPersonalFields.TryWriteSource(id, d, out string writeError)) { failed.Add($"{id}: {writeError}"); continue; }
                    d.SaveToFileDefaultDir(id, showSuccessMessage: false);
                    imported++;
                }
                if (_currentId >= 0) LoadMon(_currentId);
                string note = failed.Count == 0 ? ""
                    : $"\n\nNot imported:\n{string.Join("\n", failed.Take(10))}{(failed.Count > 10 ? "\n..." : "")}";
                await DialogHelper.ShowInfo($"Imported {imported} entries. Skipped {skipped}.{note}", "Import Complete");
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



        // ── Private helpers ───────────────────────────────────────────────────

        private void SetDirty()  { if (_loading) return; RecordUndoSnapshot(); _dirty = true;  Title = "● Personal Data Editor"; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetClean()  { _dirty = false; Title = "Personal Data Editor";  OnPropertyChanged(nameof(HasUnsavedChanges)); }

        private async Task ConfirmDiscardAsync(int newId)
        {
            bool discard = await DialogHelper.AskYesNo(
                "There are unsaved changes. Discard and proceed?", "Unsaved Changes");
            if (!discard) return;
            _dirty = false;
            LoadMon(newId);
        }

        // Set when Species.c couldn't be read for this Pokémon; Save refuses rather than write guessed values.
        private string _hgLoadError;
        public string HgLoadError
        {
            get => _hgLoadError;
            private set { if (Set(ref _hgLoadError, value)) OnPropertyChanged(nameof(HasHgLoadError)); }
        }
        public bool HasHgLoadError => _hgLoadError != null;

        internal void LoadMon(int id)
        {
            if (id < 0 || id >= GetPersonalFilesCount())
            {
                MonIconBitmap = null;
                return;
            }

            _loading = true;
            _currentId = id;
            _current   = new PokemonPersonalData(id);

            // The unpacked copy only refreshes after a build, so Species.c is what edits must start from.
            string loadError = null;
            if (HgEngineProject.IsActive && !HgEngineSpeciesPersonalFields.TryLoadInto(id, _current, out loadError))
                AppLogger.Error($"hg-engine Species.c read failed for species {id}: {loadError}");
            HgLoadError = loadError == null ? null : $"Species.c could not be read, so this Pokémon can't be saved: {loadError}";

            PopulateFromCurrent();
            LoadHgEngineExtras();
            _hatchResultIndex = GetHatchResult(id); OnPropertyChanged(nameof(HatchResultIndex));

            // Load sprite icon
            try
            {
                var drawingImg = DSUtils.GetPokePicRaw(DSUtils.ResolveIconId(id), 64, 64);
                MonIconBitmap = ImageConverter.ToAvaloniaBitmap(drawingImg);
            }
            catch { MonIconBitmap = null; }

            SetClean();
            _loading = false;

            _savedSnapshot = Snapshot();
            _history.Reset(_savedSnapshot);   // loaded state is the clean undo baseline for this mon
            _lastCaptureUtc = DateTime.MinValue;
            RaiseUndoState();
        }

        /// <summary>Pushes <see cref="_current"/> into the bound fields + machine lists. Caller guards with _loading.</summary>
        private void PopulateFromCurrent()
        {
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

            RebuildMachineLists();
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
