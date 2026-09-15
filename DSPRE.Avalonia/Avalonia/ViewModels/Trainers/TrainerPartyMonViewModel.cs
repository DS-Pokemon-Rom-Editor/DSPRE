using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using global::Avalonia.Media.Imaging;
using DSPRE.Avalonia;
using DSPRE.HgEngine;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Trainers
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
        private string _extraNickname = "";
        public string ExtraNickname
        {
            get => _extraNickname;
            set
            {
                string kept = HgEngineTrainerSource.ToEncodableNickname(value);
                Set(ref _extraNickname, kept);
                if (kept != (value ?? "")) global::Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(ExtraNickname)));
            }
        }

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
        // A ComboBox reports -1 while its list refills; only a load may clear a choice.
        private bool _rebuildingAbilities;
        private bool RejectCleared(int value, string name)
        {
            if (value >= 0 || (_suppress && !_rebuildingAbilities)) return false;
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPropertyChanged(name));
            return true;
        }

        private int _speciesIndex = -1;
        public int SpeciesIndex
        {
            get => _speciesIndex;
            set { if (RejectCleared(value, nameof(SpeciesIndex))) return; if (Set(ref _speciesIndex, value)) { RebuildAbilities(); UpdateIcon(); Touch(); } }
        }

        private decimal _formId;
        public decimal FormId { get => _formId; set { if (Set(ref _formId, value)) { if (HgEngineProject.IsActive) RebuildAbilities(); Touch(); } } }

        /// <summary>The species id this slot's form reads its abilities and learnset from.</summary>
        public int DataSpeciesId => HgEngineProject.IsActive ? HgEngineFormRegistry.ResolveFormSpecies(_speciesIndex, (int)_formId) : _speciesIndex;

        // hg-engine keeps the form in the top five bits of the species word; vanilla allows six.
        public decimal FormMaximum => HgEngineProject.IsActive ? 31 : 63;

        private decimal _level = 1;
        public decimal Level { get => _level; set { if (Set(ref _level, value)) Touch(); } }

        private int _move1 = -1; public int Move1 { get => _move1; set { if (RejectCleared(value, nameof(Move1))) return; if (Set(ref _move1, value)) Touch(); } }
        private int _move2 = -1; public int Move2 { get => _move2; set { if (RejectCleared(value, nameof(Move2))) return; if (Set(ref _move2, value)) Touch(); } }
        private int _move3 = -1; public int Move3 { get => _move3; set { if (RejectCleared(value, nameof(Move3))) return; if (Set(ref _move3, value)) Touch(); } }
        private int _move4 = -1; public int Move4 { get => _move4; set { if (RejectCleared(value, nameof(Move4))) return; if (Set(ref _move4, value)) Touch(); } }

        private int _itemIndex = -1; public int ItemIndex { get => _itemIndex; set { if (RejectCleared(value, nameof(ItemIndex))) return; if (Set(ref _itemIndex, value)) Touch(); } }
        private int _genderIndex; public int GenderIndex { get => _genderIndex; set { if (RejectCleared(value, nameof(GenderIndex))) return; if (Set(ref _genderIndex, value)) Touch(); } }
        private int _abilityIndex; public int AbilityIndex { get => _abilityIndex; set { if (RejectCleared(value, nameof(AbilityIndex))) return; if (Set(ref _abilityIndex, value)) Touch(); } }
        private decimal _difficulty; public decimal Difficulty { get => _difficulty; set { if (Set(ref _difficulty, value)) Touch(); } }
        private decimal _ballSeals;
        public decimal BallSeals
        {
            get => _ballSeals;
            set
            {
                if (!Set(ref _ballSeals, value)) return;
                OnPropertyChanged(nameof(CapsuleIndex));
                ShowCapsule();
                Touch();
            }
        }

        // Picker row N is capsule N; row 0 is none.
        private IList<string> _capsuleNames = Array.Empty<string>();
        public IList<string> CapsuleNames
        {
            get => _capsuleNames;
            set { _capsuleNames = value ?? Array.Empty<string>(); OnPropertyChanged(); OnPropertyChanged(nameof(CapsuleIndex)); ShowCapsule(); }
        }

        public int CapsuleIndex
        {
            get => _ballSeals >= 0 && _ballSeals < _capsuleNames.Count ? (int)_ballSeals : -1;
            set { if (value >= 0 && value < _capsuleNames.Count) BallSeals = value; }
        }

        /// <summary>The chosen capsule's seals, placed on a 64 by 64 picture of the ball.</summary>
        public ObservableCollection<Data.CapsuleSticker> CapsuleStickers { get; } = new();

        public void ShowCapsule()
        {
            CapsuleStickers.Clear();
            foreach (var s in Data.TrainerCapsuleCatalog.Stickers((int)_ballSeals)) CapsuleStickers.Add(s);
        }

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
            _rebuildingAbilities = true;
            try
            {
                AbilityItems.Clear();
                int species = DataSpeciesId;
                if (species < 0 || species >= _abilities.Length) return;
                var ab = _abilities[species];
                string a1 = AbilityName(ab.abi1);
                string a2 = AbilityName(ab.abi2);

                if (HgEngineProject.IsActive)
                {
                    // Rows follow HgeAbilitySlotNames.
                    AbilityItems.Add(a1);
                    AbilityItems.Add(a2);
                    AbilityItems.Add(HgEngineHiddenAbility.TryGetAbilityId(species, out int hidden) && hidden > 0
                        ? $"{AbilityName(hidden)} (hidden)" : "Hidden ability");
                }
                else if (!_abilityEditable)
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
            finally { _rebuildingAbilities = false; }
            RepushAbilityIndex();
        }

        // The ComboBox clears its selection when its rows refill, and re-raising the same value is ignored
        // by the binding, so blank it on one frame and restore it on a later one.
        // One pending repush at a time: a second one queued behind it would save the -1 and restore that.
        private bool _abilityRepushPending;
        private void RepushAbilityIndex()
        {
            if (_abilityRepushPending) return;
            _abilityRepushPending = true;
            var dispatcher = global::Avalonia.Threading.Dispatcher.UIThread;
            dispatcher.Post(() =>
            {
                int keep = _abilityIndex;
                _abilityIndex = -1; OnPropertyChanged(nameof(AbilityIndex));
                dispatcher.Post(() => { _abilityIndex = keep; _abilityRepushPending = false; OnPropertyChanged(nameof(AbilityIndex)); },
                    global::Avalonia.Threading.DispatcherPriority.Background);
            }, global::Avalonia.Threading.DispatcherPriority.Background);
        }

        private string AbilityName(int id) => id >= 0 && id < _abilityNames.Length ? _abilityNames[id] : "?";

        /// <summary>hg-engine abilitySlot names, in the order of the ability rows.</summary>
        public static readonly string[] HgeAbilitySlotNames = { "TRAINER_POKEMON_ABILITY_1", "TRAINER_POKEMON_ABILITY_2", "TRAINER_POKEMON_ABILITY_HIDDEN" };

        /// <summary>The ability the chosen hg-engine slot gives this species.</summary>
        public int SlotAbilityId()
        {
            int species = DataSpeciesId;
            if (species < 0 || species >= _abilities.Length) return 0;
            var ab = _abilities[species];
            if (_abilityIndex == 1 && ab.abi2 > 0) return ab.abi2;
            if (_abilityIndex == 2 && HgEngineHiddenAbility.TryGetAbilityId(species, out int hidden) && hidden > 0) return hidden;
            return ab.abi1;
        }

        /// <summary>Everything a party slot holds, so a reorder can move it whole.</summary>
        public sealed class State
        {
            internal int Species, Form, Level, Item, Gender, Ability, Difficulty, BallSeals;
            internal int[] Moves;
            internal int AbilityId, BallId, Nature, Status;
            internal int[] Ivs, Evs;
            internal bool Shiny, StatusOn, HpOn, AtkOn, DefOn, SpeOn, SpAOn, SpDOn, PpOn, NickOn;
            internal decimal Hp, Atk, Def, Spe, SpA, SpD, Pp1, Pp2, Pp3, Pp4;
            internal string Nick;
        }

        public State Capture()
        {
            var x = HgeExtras;
            static int[] Stats(StatBlockViewModel s) => new[] { (int)s.Hp, (int)s.Attack, (int)s.Defense, (int)s.Speed, (int)s.SpAttack, (int)s.SpDefense };
            return new State
            {
                Species = _speciesIndex, Form = (int)_formId, Level = (int)_level, Item = _itemIndex, Gender = _genderIndex,
                Ability = _abilityIndex, Difficulty = (int)_difficulty, BallSeals = (int)_ballSeals,
                Moves = new[] { _move1, _move2, _move3, _move4 },
                AbilityId = x.AbilityId, BallId = x.BallId, Nature = x.NatureIndex, Status = x.ExtraStatus,
                Ivs = Stats(x.SetIvs), Evs = Stats(x.SetEvs), Shiny = x.ShinyLocked,
                StatusOn = x.ExtraStatusEnabled, HpOn = x.ExtraHpEnabled, AtkOn = x.ExtraAttackEnabled, DefOn = x.ExtraDefenseEnabled,
                SpeOn = x.ExtraSpeedEnabled, SpAOn = x.ExtraSpAtkEnabled, SpDOn = x.ExtraSpDefEnabled, PpOn = x.ExtraPpCountsEnabled, NickOn = x.ExtraNicknameEnabled,
                Hp = x.ExtraHp, Atk = x.ExtraAttack, Def = x.ExtraDefense, Spe = x.ExtraSpeed, SpA = x.ExtraSpAtk, SpD = x.ExtraSpDef,
                Pp1 = x.ExtraPp1, Pp2 = x.ExtraPp2, Pp3 = x.ExtraPp3, Pp4 = x.ExtraPp4, Nick = x.ExtraNickname,
            };
        }

        public void Restore(State s)
        {
            Load(s.Species, s.Form, s.Level, s.Moves, s.Item, s.Gender, s.Ability, s.Difficulty, s.BallSeals);
            _suppress = true;
            var x = HgeExtras;
            x.AbilityId = s.AbilityId; x.BallId = s.BallId; x.NatureIndex = s.Nature; x.ShinyLocked = s.Shiny;
            x.SetIvs.Load(s.Ivs[0], s.Ivs[1], s.Ivs[2], s.Ivs[3], s.Ivs[4], s.Ivs[5]);
            x.SetEvs.Load(s.Evs[0], s.Evs[1], s.Evs[2], s.Evs[3], s.Evs[4], s.Evs[5]);
            x.ExtraStatusEnabled = s.StatusOn; x.ExtraStatus = s.Status;
            x.ExtraHpEnabled = s.HpOn; x.ExtraHp = s.Hp;
            x.ExtraAttackEnabled = s.AtkOn; x.ExtraAttack = s.Atk;
            x.ExtraDefenseEnabled = s.DefOn; x.ExtraDefense = s.Def;
            x.ExtraSpeedEnabled = s.SpeOn; x.ExtraSpeed = s.Spe;
            x.ExtraSpAtkEnabled = s.SpAOn; x.ExtraSpAtk = s.SpA;
            x.ExtraSpDefEnabled = s.SpDOn; x.ExtraSpDef = s.SpD;
            x.ExtraPpCountsEnabled = s.PpOn; x.ExtraPp1 = s.Pp1; x.ExtraPp2 = s.Pp2; x.ExtraPp3 = s.Pp3; x.ExtraPp4 = s.Pp4;
            x.ExtraNicknameEnabled = s.NickOn; x.ExtraNickname = s.Nick;
            _suppress = false;
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
