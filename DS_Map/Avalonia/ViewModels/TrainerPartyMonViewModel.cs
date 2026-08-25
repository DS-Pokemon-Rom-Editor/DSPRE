using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using global::Avalonia.Media.Imaging;
using DSPRE.Avalonia;
using DSPRE.HgEngine;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>Six raw stat bytes, reused for both a party mon's explicit `.setIvs`/`.setEvs`
    /// (hg-engine's `TrainerPokemonEVIV`), gated as a whole by the trainer-level `IV_EV_SET` flag.</summary>
    public class StatBlockViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler Changed;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set(ref decimal f, decimal v, [CallerMemberName] string n = null)
        { if (f == v) return false; f = v; OnPropertyChanged(n); Changed?.Invoke(this, EventArgs.Empty); return true; }

        private decimal _hp; public decimal Hp { get => _hp; set => Set(ref _hp, value); }
        private decimal _attack; public decimal Attack { get => _attack; set => Set(ref _attack, value); }
        private decimal _defense; public decimal Defense { get => _defense; set => Set(ref _defense, value); }
        private decimal _speed; public decimal Speed { get => _speed; set => Set(ref _speed, value); }
        private decimal _spAttack; public decimal SpAttack { get => _spAttack; set => Set(ref _spAttack, value); }
        private decimal _spDefense; public decimal SpDefense { get => _spDefense; set => Set(ref _spDefense, value); }

        public void Load(int hp, int attack, int defense, int speed, int spAttack, int spDefense)
        {
            _hp = hp; _attack = attack; _defense = defense; _speed = speed; _spAttack = spAttack; _spDefense = spDefense;
            OnPropertyChanged(nameof(Hp)); OnPropertyChanged(nameof(Attack)); OnPropertyChanged(nameof(Defense));
            OnPropertyChanged(nameof(Speed)); OnPropertyChanged(nameof(SpAttack)); OnPropertyChanged(nameof(SpDefense));
        }
    }

    /// <summary>hg-engine-only extended fields for one party slot: every field hg-engine's real
    /// `TrainerPokemonData` struct has beyond the base vanilla-compatible set (species/level/ivs/
    /// abilitySlot/item/moves/ballSeal), each only meaningful (and only ever written to source) when its
    /// gating <see cref="TrainerEditorViewModel"/>-level trainer-data-type flag is checked.</summary>
    public class TrainerPartyMonHgeExtras : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler Changed;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); Changed?.Invoke(this, EventArgs.Empty); return true; }

        // Explicit ability (TRAINER_DATA_TYPE_ABILITY), distinct from the always-present abilitySlot
        // selector (TrainerPartyMonViewModel.AbilityIndex): this lets the mon carry ANY ability, not just
        // one of its species' normal two.
        private int _abilityId = -1; public int AbilityId { get => _abilityId; set { if (Set(ref _abilityId, value)) { } } }

        // Held/catch ball (TRAINER_DATA_TYPE_BALL): an ITEM_* id, same list as held items.
        private int _ballId = -1; public int BallId { get => _ballId; set => Set(ref _ballId, value); }

        // Explicit IVs/EVs (TRAINER_DATA_TYPE_IV_EV_SET): both fields are ALWAYS present together in
        // hg-engine's struct once this flag is set; there's no per-stat sub-gate.
        public StatBlockViewModel SetIvs { get; } = new StatBlockViewModel();
        public StatBlockViewModel SetEvs { get; } = new StatBlockViewModel();

        public System.Collections.Generic.IReadOnlyList<string> NatureNames => DVCalculator.Natures;
        private int _natureIndex; public int NatureIndex { get => _natureIndex; set => Set(ref _natureIndex, value); }

        // Shiny lock (TRAINER_DATA_TYPE_SHINY_LOCK): forces/forbids a shiny encounter for this mon.
        private bool _shinyLocked; public bool ShinyLocked { get => _shinyLocked; set => Set(ref _shinyLocked, value); }

        // Additional flags (TRAINER_DATA_TYPE_ADDITIONAL_FLAGS): each sub-flag independently gates its
        // own value field (TRAINER_DATA_EXTRA_TYPE_*), unlike IV_EV_SET's all-or-nothing pair above.
        private bool _extraStatusEnabled; public bool ExtraStatusEnabled { get => _extraStatusEnabled; set => Set(ref _extraStatusEnabled, value); }
        private int _extraStatus; public int ExtraStatus { get => _extraStatus; set => Set(ref _extraStatus, value); }

        private bool _extraHpEnabled; public bool ExtraHpEnabled { get => _extraHpEnabled; set => Set(ref _extraHpEnabled, value); }
        private decimal _extraHp; public decimal ExtraHp { get => _extraHp; set => Set(ref _extraHp, value); }

        private bool _extraAttackEnabled; public bool ExtraAttackEnabled { get => _extraAttackEnabled; set => Set(ref _extraAttackEnabled, value); }
        private decimal _extraAttack; public decimal ExtraAttack { get => _extraAttack; set => Set(ref _extraAttack, value); }

        private bool _extraDefenseEnabled; public bool ExtraDefenseEnabled { get => _extraDefenseEnabled; set => Set(ref _extraDefenseEnabled, value); }
        private decimal _extraDefense; public decimal ExtraDefense { get => _extraDefense; set => Set(ref _extraDefense, value); }

        private bool _extraSpeedEnabled; public bool ExtraSpeedEnabled { get => _extraSpeedEnabled; set => Set(ref _extraSpeedEnabled, value); }
        private decimal _extraSpeed; public decimal ExtraSpeed { get => _extraSpeed; set => Set(ref _extraSpeed, value); }

        private bool _extraSpAtkEnabled; public bool ExtraSpAtkEnabled { get => _extraSpAtkEnabled; set => Set(ref _extraSpAtkEnabled, value); }
        private decimal _extraSpAtk; public decimal ExtraSpAtk { get => _extraSpAtk; set => Set(ref _extraSpAtk, value); }

        private bool _extraSpDefEnabled; public bool ExtraSpDefEnabled { get => _extraSpDefEnabled; set => Set(ref _extraSpDefEnabled, value); }
        private decimal _extraSpDef; public decimal ExtraSpDef { get => _extraSpDef; set => Set(ref _extraSpDef, value); }

        private bool _extraPpCountsEnabled; public bool ExtraPpCountsEnabled { get => _extraPpCountsEnabled; set => Set(ref _extraPpCountsEnabled, value); }
        private decimal _extraPp1; public decimal ExtraPp1 { get => _extraPp1; set => Set(ref _extraPp1, value); }
        private decimal _extraPp2; public decimal ExtraPp2 { get => _extraPp2; set => Set(ref _extraPp2, value); }
        private decimal _extraPp3; public decimal ExtraPp3 { get => _extraPp3; set => Set(ref _extraPp3, value); }
        private decimal _extraPp4; public decimal ExtraPp4 { get => _extraPp4; set => Set(ref _extraPp4, value); }

        private bool _extraNicknameEnabled; public bool ExtraNicknameEnabled { get => _extraNicknameEnabled; set => Set(ref _extraNicknameEnabled, value); }
        private string _extraNickname = ""; public string ExtraNickname { get => _extraNickname; set => Set(ref _extraNickname, value ?? ""); }

        public TrainerPartyMonHgeExtras()
        {
            SetIvs.Changed += (s, e) => Changed?.Invoke(this, EventArgs.Empty);
            SetEvs.Changed += (s, e) => Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// One slot of a trainer's party (up to 6). Holds species/form/level/moves/item/
    /// gender/ability/IV/ball-seal (vanilla-compatible base set), plus <see cref="HgeExtras"/> for
    /// hg-engine's extended per-mon fields. The ability list is rebuilt per selected species
    /// (Default + the species' two abilities, mirroring the WinForms editor). Raises
    /// <see cref="Changed"/> on any edit so the parent can mark itself dirty.
    /// </summary>
    public class TrainerPartyMonViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler Changed;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        public int Slot { get; }
        private bool _suppress;

        // Shared (owned by the parent VM)
        public ObservableCollection<string> PokemonNames { get; }
        public ObservableCollection<string> MoveNames { get; }
        public ObservableCollection<string> ItemNames { get; }
        public ObservableCollection<string> GenderItems { get; } = new ObservableCollection<string> { "Default Gender", "Male", "Female" };
        public ObservableCollection<string> AbilityItems { get; } = new ObservableCollection<string>();

        /// <summary>Full ability list (not species-restricted), for hg-engine's optional explicit
        /// `.ability` field, unlike <see cref="AbilityItems"/>, which is always just the species' own
        /// two abilities for the always-present abilitySlot selector.</summary>
        public ObservableCollection<string> AllAbilityNames { get; } = new ObservableCollection<string>();

        /// <summary>Every hg-engine-only field beyond the vanilla-compatible base set above.</summary>
        public TrainerPartyMonHgeExtras HgeExtras { get; } = new TrainerPartyMonHgeExtras();

        private readonly string[] _abilityNames;
        private readonly (int abi1, int abi2)[] _abilities;
        private readonly bool _abilityEditable;

        public TrainerPartyMonViewModel(int slot,
            ObservableCollection<string> pokeNames, ObservableCollection<string> moveNames, ObservableCollection<string> itemNames,
            string[] abilityNames, (int abi1, int abi2)[] abilities, bool abilityEditable, bool genderVisible, bool formVisible, bool ballEnabled)
        {
            Slot = slot;
            PokemonNames = pokeNames; MoveNames = moveNames; ItemNames = itemNames;
            _abilityNames = abilityNames; _abilities = abilities; _abilityEditable = abilityEditable;
            foreach (var n in abilityNames) AllAbilityNames.Add(n);
            GenderVisible = genderVisible; FormVisible = formVisible; BallEnabled = ballEnabled;
            AbilityEnabled = abilityEditable;
            HgeExtras.Changed += (s, e) => Touch();
        }

        // ── Visibility / enablement ─────────────────────────────────────────────────
        private bool _isVisible = true;
        public bool IsVisible { get => _isVisible; set => Set(ref _isVisible, value); }
        public bool GenderVisible { get; }
        public bool FormVisible { get; }
        private bool _ballEnabled;
        public bool BallEnabled { get => _ballEnabled; set => Set(ref _ballEnabled, value); }
        private bool _movesEnabled;
        public bool MovesEnabled { get => _movesEnabled; set => Set(ref _movesEnabled, value); }
        private bool _itemEnabled;
        public bool ItemEnabled { get => _itemEnabled; set => Set(ref _itemEnabled, value); }
        private bool _abilityEnabled;
        public bool AbilityEnabled { get => _abilityEnabled; set => Set(ref _abilityEnabled, value); }

        // hg-engine-only advanced-field visibility, driven by the trainer-level TRAINER_DATA_TYPE_*
        // checkboxes (TrainerEditorViewModel). false for every vanilla (non-hg-engine) trainer.
        private bool _hgeAdvancedVisible;
        public bool HgeAdvancedVisible { get => _hgeAdvancedVisible; set => Set(ref _hgeAdvancedVisible, value); }
        private bool _hgeExplicitAbilityEnabled;
        public bool HgeExplicitAbilityEnabled { get => _hgeExplicitAbilityEnabled; set => Set(ref _hgeExplicitAbilityEnabled, value); }
        private bool _hgeBallEnabled;
        public bool HgeBallEnabled { get => _hgeBallEnabled; set => Set(ref _hgeBallEnabled, value); }
        private bool _hgeIvEvEnabled;
        public bool HgeIvEvEnabled { get => _hgeIvEvEnabled; set => Set(ref _hgeIvEvEnabled, value); }
        private bool _hgeNatureEnabled;
        public bool HgeNatureEnabled { get => _hgeNatureEnabled; set => Set(ref _hgeNatureEnabled, value); }
        private bool _hgeShinyLockEnabled;
        public bool HgeShinyLockEnabled { get => _hgeShinyLockEnabled; set => Set(ref _hgeShinyLockEnabled, value); }
        private bool _hgeAdditionalFlagsEnabled;
        public bool HgeAdditionalFlagsEnabled { get => _hgeAdditionalFlagsEnabled; set => Set(ref _hgeAdditionalFlagsEnabled, value); }

        // ── Fields ──────────────────────────────────────────────────────────────────
        private int _speciesIndex = -1;
        public int SpeciesIndex
        {
            get => _speciesIndex;
            set { if (Set(ref _speciesIndex, value)) { RebuildAbilities(); UpdateIcon(); Touch(); } }
        }

        private decimal _formId;
        public decimal FormId { get => _formId; set { if (Set(ref _formId, value)) Touch(); } }

        private decimal _level = 1;
        public decimal Level { get => _level; set { if (Set(ref _level, value)) Touch(); } }

        private int _move1 = -1; public int Move1 { get => _move1; set { if (Set(ref _move1, value)) Touch(); } }
        private int _move2 = -1; public int Move2 { get => _move2; set { if (Set(ref _move2, value)) Touch(); } }
        private int _move3 = -1; public int Move3 { get => _move3; set { if (Set(ref _move3, value)) Touch(); } }
        private int _move4 = -1; public int Move4 { get => _move4; set { if (Set(ref _move4, value)) Touch(); } }

        private int _itemIndex = -1; public int ItemIndex { get => _itemIndex; set { if (Set(ref _itemIndex, value)) Touch(); } }
        private int _genderIndex; public int GenderIndex { get => _genderIndex; set { if (Set(ref _genderIndex, value)) Touch(); } }
        private int _abilityIndex; public int AbilityIndex { get => _abilityIndex; set { if (Set(ref _abilityIndex, value)) Touch(); } }
        private decimal _difficulty; public decimal Difficulty { get => _difficulty; set { if (Set(ref _difficulty, value)) Touch(); } }
        private decimal _ballSeals; public decimal BallSeals { get => _ballSeals; set { if (Set(ref _ballSeals, value)) Touch(); } }

        private Bitmap _pokemonIcon;
        public Bitmap PokemonIcon { get => _pokemonIcon; set => Set(ref _pokemonIcon, value); }

        private void Touch() { if (!_suppress) Changed?.Invoke(this, EventArgs.Empty); }

        // ── Load from model (suppress change events) ────────────────────────────────
        public void Load(int species, int form, int level, int[] moves, int item, int genderIndex, int abilityIndex, int difficulty, int ballSeals)
        {
            _suppress = true;
            SpeciesIndex = species;
            FormId = form;
            Level = level;
            if (moves != null && moves.Length >= 4) { Move1 = moves[0]; Move2 = moves[1]; Move3 = moves[2]; Move4 = moves[3]; }
            else { Move1 = Move2 = Move3 = Move4 = 0; }
            ItemIndex = item;
            GenderIndex = genderIndex;
            AbilityIndex = abilityIndex;
            Difficulty = difficulty;
            BallSeals = ballSeals;
            HgeExtras.AbilityId = -1;
            HgeExtras.BallId = -1;
            HgeExtras.SetIvs.Load(0, 0, 0, 0, 0, 0);
            HgeExtras.SetEvs.Load(0, 0, 0, 0, 0, 0);
            HgeExtras.NatureIndex = 0;
            HgeExtras.ShinyLocked = false;
            HgeExtras.ExtraStatusEnabled = HgeExtras.ExtraHpEnabled = HgeExtras.ExtraAttackEnabled = false;
            HgeExtras.ExtraDefenseEnabled = HgeExtras.ExtraSpeedEnabled = HgeExtras.ExtraSpAtkEnabled = false;
            HgeExtras.ExtraSpDefEnabled = HgeExtras.ExtraPpCountsEnabled = HgeExtras.ExtraNicknameEnabled = false;
            HgeExtras.ExtraNickname = "";
            _suppress = false;
        }

        private void RebuildAbilities()
        {
            AbilityItems.Clear();
            if (_speciesIndex < 0 || _speciesIndex >= _abilities.Length) return;
            var ab = _abilities[_speciesIndex];
            string a1 = ab.abi1 >= 0 && ab.abi1 < _abilityNames.Length ? _abilityNames[ab.abi1] : "?";
            string a2 = ab.abi2 >= 0 && ab.abi2 < _abilityNames.Length ? _abilityNames[ab.abi2] : "?";

            if (!_abilityEditable)
            {
                // DPPt: ability not editable, show ability 1 three times (matches WinForms padding).
                AbilityItems.Add(a1); AbilityItems.Add(a1); AbilityItems.Add(a1);
            }
            else
            {
                AbilityItems.Add("Default Ability");
                AbilityItems.Add(a1);
                AbilityItems.Add(a2);
            }
        }

        // Mirrors PokemonEditorViewModel.LoadMon's icon handling: hg-engine doesn't keep icons in
        // personal.narc at all (each species' icon is a source PNG, data/graphics/sprites/<name>/
        // icon.png), so it's loaded directly rather than through the vanilla NCGR/NCLR/ARM9-palette-table
        // pipeline, which relies on a hardcoded byte offset that's meaningless against hg-engine's
        // recompiled ARM9 (see HgEnginePokemonIcons).
        private void UpdateIcon()
        {
            try
            {
                if (_speciesIndex <= 0) { PokemonIcon = null; return; }

                if (HgEngineProject.IsActive)
                {
                    PokemonIcon = HgEnginePokemonIcons.TryGetIconPath(_speciesIndex, out string iconPath)
                        ? ImageConverter.LoadHgeIconFirstFrame(iconPath) : null;
                    return;
                }

                var gdi = DSUtils.GetPokePicRaw(_speciesIndex, 56, 56);
                PokemonIcon = ImageConverter.ToAvaloniaBitmap(gdi);
            }
            catch { PokemonIcon = null; }
        }
    }
}
