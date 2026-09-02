using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Platform.Storage;
using DSPRE.Avalonia;
using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Trainers
{
    /// <summary>
    /// Avalonia port of the WinForms <c>TrainerEditor</c>. Core scope: trainer
    /// selection + properties (name, class, AI flags, held items, battle flags) and
    /// the full 6-Pokémon party (species/form/level/moves/item/gender/ability/IV/ball
    /// seals), plus the trainer-class sprite preview (shared
    /// <see cref="TrainerClassSpriteRenderer"/>).
    ///
    /// Deferred (sub-forms): Battle Message editor, DV Calculator, Mon Reorder,
    /// Trainer Search. Simplification: the per-species "more than one gender" gate is
    /// not applied: the gender selector is editable whenever the game supports it
    /// (HGSS / AI-backport).
    /// </summary>
    public class TrainerEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        // ─── hg-engine source banner ──────────────────────────────────────────────
        public string HgEngineBanner => DSPRE.HgEngine.HgEngineProject.BannerText;
        public bool ShowHgEngineBanner => HgEngineBanner != null;

        private Window _owner;
        private bool _suppress;
        private TrainerFile _trainer;
        private int _loadedTrainerId = -1;

        private string[] _abilityNames = Array.Empty<string>();
        private (int abi1, int abi2)[] _abilities = Array.Empty<(int, int)>();
        private bool _abilityEditable;
        private bool _genderEditable;
        private bool _ballEnabled;
        private bool _formVisible;

        public ObservableCollection<string> TrainerNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> TrainerClassItems { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> PokemonNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> MoveNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> ItemNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<TrainerPartyMonViewModel> Party { get; } = new ObservableCollection<TrainerPartyMonViewModel>();
        public ObservableCollection<AiFlagViewModel> AiFlags { get; } = new ObservableCollection<AiFlagViewModel>();

        // Held items (4 combos)
        public ObservableCollection<TrainerItemSlotViewModel> TrainerItems { get; } = new ObservableCollection<TrainerItemSlotViewModel>();

        // ── hg-engine-only: real trainer-data-type flags + battle type, dynamically resolved from the
        // linked checkout's include/trainer_data.h (never a hardcoded vanilla-derived list). Empty/unused
        // for vanilla ROMs, where the existing ChooseMoves/ChooseItems/DoubleBattle checkboxes below
        // still apply.
        public bool IsHgeActive => HgEngineProject.IsActive;
        public ObservableCollection<AiFlagViewModel> TrainerDataTypeFlags { get; } = new ObservableCollection<AiFlagViewModel>();
        public ObservableCollection<string> BattleTypeOptions { get; } = new ObservableCollection<string>();
        private int[] _battleTypeValues = Array.Empty<int>();
        private int _battleTypeIndex;
        public int BattleTypeIndex
        {
            get => _battleTypeIndex;
            set { if (Set(ref _battleTypeIndex, value) && !_suppress) SetDirty(); }
        }

        private bool TrainerDataTypeChecked(string flagName) =>
            TrainerDataTypeFlags.FirstOrDefault(f => f.Label == flagName)?.Checked == true;
        public bool HgeMovesFlagChecked => TrainerDataTypeChecked("TRAINER_DATA_TYPE_MOVES");
        public bool HgeItemsFlagChecked => TrainerDataTypeChecked("TRAINER_DATA_TYPE_ITEMS");
        public bool HgeAbilityFlagChecked => TrainerDataTypeChecked("TRAINER_DATA_TYPE_ABILITY");
        public bool HgeBallFlagChecked => TrainerDataTypeChecked("TRAINER_DATA_TYPE_BALL");
        public bool HgeIvEvFlagChecked => TrainerDataTypeChecked("TRAINER_DATA_TYPE_IV_EV_SET");
        public bool HgeNatureFlagChecked => TrainerDataTypeChecked("TRAINER_DATA_TYPE_NATURE_SET");
        public bool HgeShinyLockFlagChecked => TrainerDataTypeChecked("TRAINER_DATA_TYPE_SHINY_LOCK");
        public bool HgeAdditionalFlagsFlagChecked => TrainerDataTypeChecked("TRAINER_DATA_TYPE_ADDITIONAL_FLAGS");

        private void ApplyHgeGatingToParty()
        {
            foreach (var mon in Party)
            {
                mon.HgeAdvancedVisible = IsHgeActive;
                mon.HgeExplicitAbilityEnabled = HgeAbilityFlagChecked;
                mon.HgeBallEnabled = HgeBallFlagChecked;
                mon.HgeIvEvEnabled = HgeIvEvFlagChecked;
                mon.HgeNatureEnabled = HgeNatureFlagChecked;
                mon.HgeShinyLockEnabled = HgeShinyLockFlagChecked;
                mon.HgeAdditionalFlagsEnabled = HgeAdditionalFlagsFlagChecked;

                if (IsHgeActive)
                {
                    mon.MovesEnabled = HgeMovesFlagChecked;
                    mon.ItemEnabled = HgeItemsFlagChecked;
                }
            }
        }

        private void RaiseHgeFlagCheckedChanged()
        {
            OnPropertyChanged(nameof(HgeMovesFlagChecked));
            OnPropertyChanged(nameof(HgeItemsFlagChecked));
            OnPropertyChanged(nameof(HgeAbilityFlagChecked));
            OnPropertyChanged(nameof(HgeBallFlagChecked));
            OnPropertyChanged(nameof(HgeIvEvFlagChecked));
            OnPropertyChanged(nameof(HgeNatureFlagChecked));
            OnPropertyChanged(nameof(HgeShinyLockFlagChecked));
            OnPropertyChanged(nameof(HgeAdditionalFlagsFlagChecked));
        }

        private static readonly string[] AiFlagLabels =
        {
            "AI 0", "Basic", "Evaluate Attack", "Expert", "Setup", "Risky",
            "Prioritize Extremes", "Baton Pass", "Tag Strategy", "Check HP", "Weather", "Harassment"
        };

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        // Reference-counted so an inner load (e.g. the initial SelectedTrainerIndex set inside
        // SetupAsync) can't have its outer scope clear the flag out from under it.
        private int _loadingDepth;
        public bool IsLoading => _loadingDepth > 0;
        private void PushLoading() { _loadingDepth++; OnPropertyChanged(nameof(IsLoading)); }
        private void PopLoading() { _loadingDepth = Math.Max(0, _loadingDepth - 1); OnPropertyChanged(nameof(IsLoading)); }

        /// <summary>Trainer id to select once the list loads (set before SetupAsync; e.g. from a "Go to Trainer #N" jump).</summary>
        public int InitialIndex { get; set; }

        // ── Trainer selection / properties ──────────────────────────────────────────
        private int _selectedTrainerIndex = -1;
        public int SelectedTrainerIndex
        {
            get => _selectedTrainerIndex;
            set { if (Set(ref _selectedTrainerIndex, value) && !_suppress && value >= 0) _ = LoadTrainerWithBusyIndicatorAsync(value); }
        }

        private async Task LoadTrainerWithBusyIndicatorAsync(int index)
        {
            PushLoading();
            await Task.Yield();
            try { LoadTrainer(index); }
            finally { PopLoading(); }
        }

        private string _trainerName = "";
        public string TrainerName { get => _trainerName; set { if (Set(ref _trainerName, value) && !_suppress) SetDirty(); } }

        private int _trainerClassIndex = -1;
        public int TrainerClassIndex
        {
            get => _trainerClassIndex;
            set { if (Set(ref _trainerClassIndex, value)) { if (!_suppress) SetDirty(); UpdateTrainerSprite(); } }
        }

        private bool _doubleBattle; public bool DoubleBattle { get => _doubleBattle; set { if (Set(ref _doubleBattle, value) && !_suppress) SetDirty(); } }
        private bool _chooseMoves; public bool ChooseMoves { get => _chooseMoves; set { if (Set(ref _chooseMoves, value)) { ApplyMovesEnabled(); if (!_suppress) SetDirty(); } } }
        private bool _chooseItems; public bool ChooseItems { get => _chooseItems; set { if (Set(ref _chooseItems, value)) { ApplyItemsEnabled(); if (!_suppress) SetDirty(); } } }

        private decimal _partyCount = 1;
        public decimal PartyCount
        {
            get => _partyCount;
            set { if (Set(ref _partyCount, value)) { ApplyPartyVisibility(); if (!_suppress) SetDirty(); } }
        }

        // ── Trainer-class sprite ─────────────────────────────────────────────────────
        private readonly TrainerClassSpriteRenderer _sprite = new TrainerClassSpriteRenderer();
        private Bitmap _classImage; public Bitmap ClassImage { get => _classImage; private set => Set(ref _classImage, value); }
        public bool HasClassSprite => _sprite.HasSprite;
        private decimal _classFrame; public decimal ClassFrame { get => _classFrame; set { if (Set(ref _classFrame, value)) RenderSprite(); } }
        private decimal _classFrameMax; public decimal ClassFrameMax { get => _classFrameMax; private set => Set(ref _classFrameMax, value); }

        // ── Dirty tracking ───────────────────────────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => $"Trainer Editor (Trainer {_loadedTrainerId})";
        public void SaveChanges() => Save();
        public void DiscardChanges() { _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); if (_selectedTrainerIndex >= 0) LoadTrainer(_selectedTrainerIndex); }
        // RecordUndoSnapshot runs BEFORE the _dirty short-circuit so EVERY edit is captured (not just the first).
        private void SetDirty() { if (_suppress) return; RecordUndoSnapshot(); if (_dirty) return; _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetClean() { if (!_dirty) return; _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        // ── Undo / redo (ISupportsUndo) ────────────────────────────────────────
        // Composite snapshot via the SAME serialization the proven Save/Load round-trip uses: trp bytes +
        // party bytes (synced from the VM first) + the name. Edit bursts within CoalesceMs collapse into one.
        private sealed class TrainerSnapshot { public byte[] Trp; public byte[] Party; public string Name; }
        private readonly DSPRE.Avalonia.UndoHistory<TrainerSnapshot> _history = new();
        private DateTime _lastCaptureUtc = DateTime.MinValue;
        private const int CoalesceMs = 500;

        public bool CanUndo => _history.CanUndo;
        public bool CanRedo => _history.CanRedo;
        public void Undo() { if (_history.CanUndo) ApplyState(_history.Undo()); }
        public void Redo() { if (_history.CanRedo) ApplyState(_history.Redo()); }
        private void RaiseUndoState() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private TrainerSnapshot Snapshot()
        {
            if (_trainer == null) return null;
            SyncToTrainer();
            return new TrainerSnapshot
            {
                Trp   = _trainer.trp.ToByteArray(),
                Party = _trainer.party.ToByteArray(),
                Name  = _trainerName,
            };
        }

        private void ApplyState(TrainerSnapshot snap)
        {
            if (snap == null || _trainer == null || _loadedTrainerId < 0) return;
            _suppress = true;
            _trainer = new TrainerFile(
                new TrainerProperties((ushort)_loadedTrainerId, new MemoryStream(snap.Trp)),
                new MemoryStream(snap.Party),
                snap.Name);
            PopulateFromTrainer();
            _suppress = false;

            _dirty = _history.IsDirty;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            RaiseUndoState();
        }

        private void RecordUndoSnapshot()
        {
            if (_suppress || _trainer == null) return;
            bool coalesce = (DateTime.UtcNow - _lastCaptureUtc).TotalMilliseconds < CoalesceMs;
            _history.Capture(Snapshot(), coalesce);
            _lastCaptureUtc = DateTime.UtcNow;
            RaiseUndoState();
        }

        // ── Constructors ────────────────────────────────────────────────────────────
        public TrainerEditorViewModel()
        {
            if (!Design.IsDesignMode) return;
            TrainerNames.Add("DESIGN TRAINER");
            for (int i = 0; i < AiFlagLabels.Length; i++) AiFlags.Add(new AiFlagViewModel(AiFlagLabels[i]));
        }

        public TrainerEditorViewModel(bool _) { }

        private void OnNamesChanged(object sender, System.EventArgs e)
        {
            DSPRE.Avalonia.Data.ListSync.Apply(PokemonNames, GetPokemonNames());
            DSPRE.Avalonia.Data.ListSync.Apply(MoveNames,    GetAttackNames());
            DSPRE.Avalonia.Data.ListSync.Apply(ItemNames,    GetItemNames());
            DSPRE.Avalonia.Data.ListSync.Apply(TrainerNames, new System.Collections.Generic.List<string>(DSPRE.TrainerNames.GetAll()));

            string[] classNames = GetTrainerClassNames();
            var formatted = new System.Collections.Generic.List<string>(classNames.Length);
            for (int i = 0; i < classNames.Length; i++) formatted.Add($"[{i:D3}] {classNames[i]}");
            DSPRE.Avalonia.Data.ListSync.Apply(TrainerClassItems, formatted);
        }
        /// <summary>Unsubscribes from app-wide events; call when the editor window closes.</summary>
        public void Detach() => AppEvents.NamesChanged -= OnNamesChanged;

        // ── Setup ─────────────────────────────────────────────────────────────────────
        public async Task SetupAsync(Window owner)
        {
            _owner = owner;
            StatusText = "Loading trainers…";
            PushLoading();
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> {
                    DirNames.trainerProperties, DirNames.trainerParty, DirNames.trainerGraphics,
                    DirNames.personalPokeData, DirNames.monIcons, DirNames.textArchives });
                SetMonIconsPalTableAddress();

                _genderEditable = gameFamily == GameFamilies.HGSS || AIBackportEnabled;
                _abilityEditable = _genderEditable;
                _ballEnabled = gameFamily != GameFamilies.DP;
                _formVisible = gameFamily != GameFamilies.DP;

                foreach (var n in GetPokemonNames()) PokemonNames.Add(n);
                foreach (var n in GetAttackNames()) MoveNames.Add(n);
                foreach (var n in GetItemNames()) ItemNames.Add(n);
                _abilityNames = GetAbilityNames();
                LoadAbilities();

                foreach (var n in DSPRE.TrainerNames.GetAll()) TrainerNames.Add(n);
                AppEvents.NamesChanged += OnNamesChanged;   // live-refresh names from the Text editor

                string[] classNames = GetTrainerClassNames();
                for (int i = 0; i < classNames.Length; i++) TrainerClassItems.Add($"[{i:D3}] {classNames[i]}");

                if (IsHgeActive)
                {
                    foreach (var flag in HgEngineTrainerFieldSchema.GetAiFlags())
                    {
                        var f = new AiFlagViewModel(flag.Name, flag.Bit);
                        f.Changed += (s, e) => SetDirty();
                        AiFlags.Add(f);
                    }
                    foreach (var flag in HgEngineTrainerFieldSchema.GetTrainerDataTypeFlags())
                    {
                        var f = new AiFlagViewModel(flag.Name, flag.Bit);
                        f.Changed += (s, e) => { ApplyHgeGatingToParty(); RaiseHgeFlagCheckedChanged(); SetDirty(); };
                        TrainerDataTypeFlags.Add(f);
                    }
                    var battleTypes = HgEngineTrainerFieldSchema.GetBattleTypes();
                    _battleTypeValues = battleTypes.Select(b => b.Bit).ToArray();
                    foreach (var bt in battleTypes) BattleTypeOptions.Add(bt.Name);
                }
                else
                {
                    for (int i = 0; i < AiFlagLabels.Length; i++)
                    {
                        var f = new AiFlagViewModel(AiFlagLabels[i]);
                        f.Changed += (s, e) => SetDirty();
                        AiFlags.Add(f);
                    }
                }

                for (int i = 0; i < TrainerProperties.TRAINER_ITEMS; i++)
                {
                    var slot = new TrainerItemSlotViewModel(ItemNames);
                    slot.Changed += (s, e) => SetDirty();
                    TrainerItems.Add(slot);
                }

                // hg-engine's real TrainerPokemonData struct has no gender field, nowhere to write a
                // forced gender to, so the selector is hidden rather than shown-but-silently-ignored.
                bool genderVisibleForMode = _genderEditable && !IsHgeActive;
                for (int i = 0; i < TrainerFile.POKE_IN_PARTY; i++)
                {
                    var mon = new TrainerPartyMonViewModel(i, PokemonNames, MoveNames, ItemNames,
                        _abilityNames, _abilities, _abilityEditable, genderVisibleForMode, _formVisible, _ballEnabled);
                    mon.Changed += (s, e) => SetDirty();
                    Party.Add(mon);
                }
                ApplyHgeGatingToParty();

                StatusText = $"Loaded {TrainerNames.Count} trainers ({gameFamily}).";
                if (TrainerNames.Count > 0)
                    SelectedTrainerIndex = Math.Min(Math.Max(0, InitialIndex), TrainerNames.Count - 1);
            }
            catch (Exception ex)
            {
                StatusText = $"Error loading trainers: {ex.Message}";
                await DialogHelper.ShowError($"Failed to load trainers:\n{ex.Message}", "Trainer Editor Error");
            }
            finally { PopLoading(); }
        }

        private void LoadAbilities()
        {
            int count = PokemonNames.Count;
            _abilities = new (int, int)[count];
            string dir = gameDirs[DirNames.personalPokeData].unpackedDir;
            for (int i = 0; i < count; i++)
            {
                try
                {
                    string path = Path.Combine(dir, i.ToString("D4"));
                    if (!File.Exists(path)) { _abilities[i] = (0, 0); continue; }
                    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                    var sp = new SpeciesFile(fs);
                    _abilities[i] = (sp.Ability1, sp.Ability2);
                }
                catch { _abilities[i] = (0, 0); }
            }
        }

        // ── Load a trainer ─────────────────────────────────────────────────────────────
        private void LoadTrainer(int index)
        {
            if (IsHgeActive) { LoadTrainerFromSource(index); return; }
            try
            {
                string suffix = Path.DirectorySeparatorChar + index.ToString("D4");
                string[] trNames = GetSimpleTrainerNames();
                bool error = index >= trNames.Length;

                using (var propStream = new FileStream(gameDirs[DirNames.trainerProperties].unpackedDir + suffix, FileMode.Open, FileAccess.Read))
                using (var partyStream = new FileStream(gameDirs[DirNames.trainerParty].unpackedDir + suffix, FileMode.Open, FileAccess.Read))
                {
                    _trainer = new TrainerFile(
                        new TrainerProperties((ushort)index, propStream),
                        partyStream,
                        error ? TrainerFile.NAME_NOT_FOUND : trNames[index]);
                }
                _loadedTrainerId = index;

                _suppress = true;
                try { PopulateFromTrainer(); }
                finally { _suppress = false; }

                SetClean();
                _history.Reset(Snapshot());   // loaded state is the clean undo baseline for this trainer
                _lastCaptureUtc = DateTime.MinValue;
                RaiseUndoState();
                StatusText = $"Trainer {index} loaded.";
                OnPropertyChanged(nameof(UnsavedChangesDescription));
            }
            catch (Exception ex)
            {
                _ = DialogHelper.ShowError($"Failed to load trainer {index}:\n{ex.Message}", "Trainer Editor Error");
            }
        }

        // ── hg-engine source-backed load path ──────────────────────────────────────────
        // Reads Trainers.c source TEXT directly (via HgEngineTrainerSource/HgEngineSourceBlock), never
        // the compiled a055/a056 narcs, and never a hardcoded byte layout.
        private const string TrainerClassHeader = "include/constants/trainerclass.h";
        private const string SpeciesHeader = "include/constants/species.h";
        private const string ItemHeader = "include/constants/item.h";
        private const string AbilityHeader = "include/constants/ability.h";
        private const string MovesHeader = "include/constants/moves.h";
        private const string TrainerDataHeader = "include/trainer_data.h";

        private void LoadTrainerFromSource(int index)
        {
            if (!HgEngineTrainerSource.TryLoad(index, out var block, out string error))
            {
                _ = DialogHelper.ShowError($"Failed to load trainer {index} from hg-engine source:\n{error}", "Trainer Editor Error");
                return;
            }
            _trainer = null;   // no binary model for hg-engine trainers, see TryLoad's doc comment
            _loadedTrainerId = index;

            _suppress = true;
            try { PopulateFromSourceBlock(block); }
            finally { _suppress = false; }

            SetClean();
            _history.Reset(null);   // undo relies on _trainer's byte round-trip; not available for hg-engine trainers
            _lastCaptureUtc = DateTime.MinValue;
            RaiseUndoState();
            StatusText = $"Trainer {index} loaded from hg-engine source.";
            OnPropertyChanged(nameof(UnsavedChangesDescription));
        }

        /// <summary>Pushes one trainer's source block into the bound fields + party mons. Caller guards
        /// with _suppress.</summary>
        private void PopulateFromSourceBlock(HgEngineSourceBlock block)
        {
            TrainerName = block.TryGetString(new[] { FieldPathSegment.Field("name") }, out string name) ? name : "";

            if (block.TryGetSymbol(new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("trainerClass") }, TrainerClassHeader, out int classId))
                TrainerClassIndex = classId;

            bool gotTrainerType = block.TryGetFlagsValue(new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("trainerType") }, TrainerDataHeader, out int trainerTypeValue);
            foreach (var f in TrainerDataTypeFlags) f.Checked = gotTrainerType && (trainerTypeValue & f.Value) != 0;
            RaiseHgeFlagCheckedChanged();
            ChooseMoves = HgeMovesFlagChecked;
            ChooseItems = HgeItemsFlagChecked;

            bool gotAiFlags = block.TryGetFlagsValue(new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("aiFlags") }, TrainerDataHeader, out int aiFlagsValue);
            foreach (var f in AiFlags) f.Checked = gotAiFlags && (aiFlagsValue & f.Value) != 0;

            BattleTypeIndex = 0;
            if (block.TryGetSymbol(new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("battleType") }, TrainerDataHeader, out int battleTypeValue))
            {
                int idx = Array.IndexOf(_battleTypeValues, battleTypeValue);
                if (idx >= 0) BattleTypeIndex = idx;
            }
            DoubleBattle = BattleTypeIndex > 0;   // kept loosely in sync for display only; save uses BattleTypeIndex

            var itemsRaw = block.GetArrayElements(new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("items") });
            for (int i = 0; i < TrainerItems.Count; i++)
                TrainerItems[i].ItemIndex = i < itemsRaw.Count && HgEngineSourceBlock.TryResolveToken(itemsRaw[i].Raw, ItemHeader, out int itemVal) ? itemVal : -1;

            var partyRaw = block.GetArrayElements(new[] { FieldPathSegment.Field("party") });
            PartyCount = Math.Max(1, partyRaw.Count);
            for (int i = 0; i < Party.Count; i++)
            {
                if (i < partyRaw.Count) LoadMonFromSource(Party[i], partyRaw[i]);
                else Party[i].Load(0, 0, 1, null, 0, 0, 0, 0, 0);
            }

            ApplyMovesEnabled();
            ApplyItemsEnabled();
            ApplyPartyVisibility();
            ApplyHgeGatingToParty();
        }

        private void LoadMonFromSource(TrainerPartyMonViewModel mon, HgEngineSourceBlock monBlock)
        {
            monBlock.TryGetInt(new[] { FieldPathSegment.Field("ivs") }, out int ivs);
            monBlock.TryGetSymbol(new[] { FieldPathSegment.Field("species") }, SpeciesHeader, out int species);
            monBlock.TryGetInt(new[] { FieldPathSegment.Field("level") }, out int level);
            monBlock.TryGetInt(new[] { FieldPathSegment.Field("ballSeal") }, out int ballSeal);

            int itemVal = 0;
            if (HgeItemsFlagChecked) monBlock.TryGetSymbol(new[] { FieldPathSegment.Field("item") }, ItemHeader, out itemVal);

            int[] moves = null;
            if (HgeMovesFlagChecked)
            {
                moves = new int[4];
                for (int j = 0; j < 4; j++)
                    monBlock.TryGetSymbol(new[] { FieldPathSegment.Field("moves"), FieldPathSegment.At(j) }, MovesHeader, out moves[j]);
            }

            int abilityIndex = 0;
            if (monBlock.TryGetSymbol(new[] { FieldPathSegment.Field("abilitySlot") }, TrainerDataHeader, out int slotValue))
            {
                var slots = HgEngineTrainerFieldSchema.GetAbilitySlots();
                for (int i = 0; i < slots.Count; i++) if (slots[i].Bit == slotValue) { abilityIndex = i; break; }
            }

            // hg-engine's TrainerPokemonData has no gender field at all, nowhere to read a forced
            // gender from, so it's always "Default" here (the Gender selector is hidden in the UI too).
            mon.Load(species, 0, Math.Max(1, level), moves, itemVal, 0, abilityIndex, ivs, ballSeal);

            var extras = mon.HgeExtras;
            extras.AbilityId = HgeAbilityFlagChecked && monBlock.TryGetSymbol(new[] { FieldPathSegment.Field("ability") }, AbilityHeader, out int abilityId) ? abilityId : -1;
            extras.BallId = HgeBallFlagChecked && monBlock.TryGetSymbol(new[] { FieldPathSegment.Field("ball") }, ItemHeader, out int ballId) ? ballId : -1;

            if (HgeIvEvFlagChecked)
            {
                extras.SetIvs.Load(
                    GetInt(monBlock, "setIvs", "hp"), GetInt(monBlock, "setIvs", "attack"), GetInt(monBlock, "setIvs", "defense"),
                    GetInt(monBlock, "setIvs", "speed"), GetInt(monBlock, "setIvs", "spAttack"), GetInt(monBlock, "setIvs", "spDefense"));
                extras.SetEvs.Load(
                    GetInt(monBlock, "setEvs", "hp"), GetInt(monBlock, "setEvs", "attack"), GetInt(monBlock, "setEvs", "defense"),
                    GetInt(monBlock, "setEvs", "speed"), GetInt(monBlock, "setEvs", "spAttack"), GetInt(monBlock, "setEvs", "spDefense"));
            }
            else
            {
                extras.SetIvs.Load(0, 0, 0, 0, 0, 0);
                extras.SetEvs.Load(0, 0, 0, 0, 0, 0);
            }

            extras.NatureIndex = HgeNatureFlagChecked && monBlock.TryGetInt(new[] { FieldPathSegment.Field("nature") }, out int nature) ? nature : 0;
            extras.ShinyLocked = HgeShinyLockFlagChecked && monBlock.TryGetInt(new[] { FieldPathSegment.Field("shinyLock") }, out int shiny) && shiny != 0;

            int extraFlagsValue = 0;
            bool gotExtraFlags = HgeAdditionalFlagsFlagChecked &&
                monBlock.TryGetFlagsValue(new[] { FieldPathSegment.Field("additionalFlags") }, TrainerDataHeader, out extraFlagsValue);
            var extraFlagBits = gotExtraFlags ? HgEngineTrainerFieldSchema.GetExtraFlags() : System.Array.Empty<HgEngineTrainerFieldSchema.NamedFlag>();
            int ExtraBit(string suffix) { foreach (var f in extraFlagBits) if (f.Name.EndsWith(suffix)) return f.Bit; return 0; }

            extras.ExtraStatusEnabled = gotExtraFlags && (extraFlagsValue & ExtraBit("STATUS")) != 0;
            extras.ExtraStatus = extras.ExtraStatusEnabled && monBlock.TryGetInt(new[] { FieldPathSegment.Field("status") }, out int status) ? status : 0;

            extras.ExtraHpEnabled = gotExtraFlags && (extraFlagsValue & ExtraBit("HP")) != 0;
            extras.ExtraHp = extras.ExtraHpEnabled ? GetInt(monBlock, null, "hp") : 0;
            extras.ExtraAttackEnabled = gotExtraFlags && (extraFlagsValue & ExtraBit("ATK")) != 0;
            extras.ExtraAttack = extras.ExtraAttackEnabled ? GetInt(monBlock, null, "attack") : 0;
            extras.ExtraDefenseEnabled = gotExtraFlags && (extraFlagsValue & ExtraBit("DEF")) != 0;
            extras.ExtraDefense = extras.ExtraDefenseEnabled ? GetInt(monBlock, null, "defense") : 0;
            extras.ExtraSpeedEnabled = gotExtraFlags && (extraFlagsValue & ExtraBit("SPEED")) != 0;
            extras.ExtraSpeed = extras.ExtraSpeedEnabled ? GetInt(monBlock, null, "speed") : 0;
            extras.ExtraSpAtkEnabled = gotExtraFlags && (extraFlagsValue & ExtraBit("SP_ATK")) != 0;
            extras.ExtraSpAtk = extras.ExtraSpAtkEnabled ? GetInt(monBlock, null, "spAttack") : 0;
            extras.ExtraSpDefEnabled = gotExtraFlags && (extraFlagsValue & ExtraBit("SP_DEF")) != 0;
            extras.ExtraSpDef = extras.ExtraSpDefEnabled ? GetInt(monBlock, null, "spDefense") : 0;

            extras.ExtraPpCountsEnabled = gotExtraFlags && (extraFlagsValue & ExtraBit("PP_COUNTS")) != 0;
            if (extras.ExtraPpCountsEnabled)
            {
                var pp = monBlock.GetArrayElements(new[] { FieldPathSegment.Field("ppCounts") });
                extras.ExtraPp1 = pp.Count > 0 && HgEngineSourceBlock.TryResolveToken(pp[0].Raw, null, out int p1) ? p1 : 0;
                extras.ExtraPp2 = pp.Count > 1 && HgEngineSourceBlock.TryResolveToken(pp[1].Raw, null, out int p2) ? p2 : 0;
                extras.ExtraPp3 = pp.Count > 2 && HgEngineSourceBlock.TryResolveToken(pp[2].Raw, null, out int p3) ? p3 : 0;
                extras.ExtraPp4 = pp.Count > 3 && HgEngineSourceBlock.TryResolveToken(pp[3].Raw, null, out int p4) ? p4 : 0;
            }
            else { extras.ExtraPp1 = extras.ExtraPp2 = extras.ExtraPp3 = extras.ExtraPp4 = 0; }

            extras.ExtraNicknameEnabled = gotExtraFlags && (extraFlagsValue & ExtraBit("NICKNAME")) != 0;
            extras.ExtraNickname = extras.ExtraNicknameEnabled && monBlock.TryGetString(new[] { FieldPathSegment.Field("nicknameStr") }, out string nick) ? nick : "";
        }

        private static int GetInt(HgEngineSourceBlock block, string nestedField, string statField)
        {
            var path = nestedField != null
                ? new[] { FieldPathSegment.Field(nestedField), FieldPathSegment.Field(statField) }
                : new[] { FieldPathSegment.Field(statField) };
            return block.TryGetInt(path, out int v) ? v : 0;
        }

        /// <summary>Pushes <see cref="_trainer"/> into the bound fields + party mons. Caller guards with _suppress.</summary>
        private void PopulateFromTrainer()
        {
            var trp = _trainer.trp;
            TrainerName = _trainer.name;
            TrainerClassIndex = trp.trainerClass;
            DoubleBattle = trp.doubleBattle;
            ChooseMoves = trp.chooseMoves;
            ChooseItems = trp.chooseItems;
            PartyCount = Math.Max(1, (int)trp.partyCount);

            for (int i = 0; i < TrainerItems.Count && i < trp.trainerItems.Length; i++)
                TrainerItems[i].ItemIndex = trp.trainerItems[i];

            for (int i = 0; i < AiFlags.Count; i++)
                AiFlags[i].Checked = trp.AI != null && i < trp.AI.Count && trp.AI[i];

            for (int i = 0; i < Party.Count; i++)
                LoadMon(Party[i], _trainer.party[i]);

            ApplyMovesEnabled();
            ApplyItemsEnabled();
            ApplyPartyVisibility();
        }

        /// <summary>Pushes the live VM fields into <see cref="_trainer"/> (shared by Save and snapshotting).</summary>
        private void SyncToTrainer()
        {
            if (_trainer == null) return;
            var trp = _trainer.trp;
            trp.partyCount = (byte)_partyCount;
            trp.chooseMoves = _chooseMoves;
            trp.chooseItems = _chooseItems;
            trp.doubleBattle = _doubleBattle;
            trp.trainerClass = (byte)Math.Max(0, _trainerClassIndex);

            for (int i = 0; i < trp.trainerItems.Length && i < TrainerItems.Count; i++)
                trp.trainerItems[i] = (ushort)Math.Max(0, TrainerItems[i].ItemIndex);

            for (int i = 0; i < AiFlags.Count && trp.AI != null && i < trp.AI.Count; i++)
                trp.AI[i] = AiFlags[i].Checked;

            for (int i = 0; i < TrainerFile.POKE_IN_PARTY; i++)
                _trainer.party[i].moves = _chooseMoves ? new ushort[4] : null;

            for (int i = 0; i < (int)_partyCount; i++)
            {
                var mon = Party[i];
                var p = _trainer.party[i];
                p.pokeID = (ushort)Math.Max(0, mon.SpeciesIndex);
                p.formID = (ushort)mon.FormId;
                p.level = (ushort)mon.Level;

                if (_chooseMoves)
                    p.moves = new ushort[] { (ushort)Math.Max(0, mon.Move1), (ushort)Math.Max(0, mon.Move2), (ushort)Math.Max(0, mon.Move3), (ushort)Math.Max(0, mon.Move4) };

                if (_chooseItems)
                    p.heldItem = (ushort)Math.Max(0, mon.ItemIndex);

                p.difficulty = (byte)mon.Difficulty;

                var flags = PartyPokemon.GenderAndAbilityFlags.NO_FLAGS;
                if (_genderEditable)
                {
                    if (mon.GenderIndex == 1) flags = PartyPokemon.GenderAndAbilityFlags.FORCE_MALE;
                    else if (mon.GenderIndex == 2) flags = PartyPokemon.GenderAndAbilityFlags.FORCE_FEMALE;
                }
                if (mon.AbilityIndex == 1) flags |= PartyPokemon.GenderAndAbilityFlags.ABILITY_SLOT1;
                else if (mon.AbilityIndex == 2) flags |= PartyPokemon.GenderAndAbilityFlags.ABILITY_SLOT2;
                p.genderAndAbilityFlags = flags;

                p.ballSeals = (ushort)mon.BallSeals;
            }
        }

        private void LoadMon(TrainerPartyMonViewModel mon, PartyPokemon p)
        {
            int gender = 0;
            if (p.genderAndAbilityFlags.HasFlag(PartyPokemon.GenderAndAbilityFlags.FORCE_MALE)) gender = 1;
            else if (p.genderAndAbilityFlags.HasFlag(PartyPokemon.GenderAndAbilityFlags.FORCE_FEMALE)) gender = 2;

            int ability = 0;
            if (p.genderAndAbilityFlags.HasFlag(PartyPokemon.GenderAndAbilityFlags.ABILITY_SLOT1)) ability = 1;
            else if (p.genderAndAbilityFlags.HasFlag(PartyPokemon.GenderAndAbilityFlags.ABILITY_SLOT2)) ability = 2;

            int[] moves = null;
            if (p.moves != null)
            {
                moves = new int[4];
                for (int j = 0; j < 4 && j < p.moves.Length; j++) moves[j] = p.moves[j];
            }

            mon.Load(p.pokeID ?? 0, p.formID, Math.Max(1, (int)p.level), moves, p.heldItem ?? 0, gender, ability, p.difficulty, p.ballSeals);
        }

        private void ApplyMovesEnabled() { foreach (var m in Party) m.MovesEnabled = _chooseMoves; }
        private void ApplyItemsEnabled() { foreach (var m in Party) m.ItemEnabled = _chooseItems; }
        private void ApplyPartyVisibility()
        {
            int count = (int)_partyCount;
            for (int i = 0; i < Party.Count; i++) Party[i].IsVisible = i < count;
        }

        // ── Sprite ──────────────────────────────────────────────────────────────────
        private void UpdateTrainerSprite()
        {
            if (_trainerClassIndex < 0) { ClassImage = null; return; }
            int maxFrame = _sprite.Load(_trainerClassIndex);
            ClassFrameMax = maxFrame;
            OnPropertyChanged(nameof(HasClassSprite));
            _classFrame = _sprite.DefaultFrame;
            OnPropertyChanged(nameof(ClassFrame));
            RenderSprite();
        }
        private void RenderSprite() => ClassImage = _sprite.Render((int)_classFrame, 96, 96);

        // ── Save ──────────────────────────────────────────────────────────────────────
        public void Save()
        {
            if (_selectedTrainerIndex < 0) return;

            if (IsHgeActive)
            {
                UpdateTrainerName(_trainerName);   // keeps the in-app trainer-list dropdown in sync; the real name lives in source (below)
                WriteHgEngineSource();
                SetClean();
                _history.MarkSaved();
                RaiseUndoState();
                StatusText = $"Trainer {_selectedTrainerIndex} saved to hg-engine source.";
                return;
            }

            if (_trainer == null) return;
            SyncToTrainer();

            string indexStr = Path.DirectorySeparatorChar + _selectedTrainerIndex.ToString("D4");
            File.WriteAllBytes(gameDirs[DirNames.trainerProperties].unpackedDir + indexStr, _trainer.trp.ToByteArray());
            File.WriteAllBytes(gameDirs[DirNames.trainerParty].unpackedDir + indexStr, _trainer.party.ToByteArray());

            UpdateTrainerName(_trainerName);

            SetClean();
            _history.MarkSaved();
            RaiseUndoState();
            StatusText = $"Trainer {_selectedTrainerIndex} saved.";
        }

        // Optional per-mon fields are only written when their gating trainer-data-type flag is checked;
        // partySize/partyMonCount/textCount are never written (trainerdatagen derives them).
        private void WriteHgEngineSource()
        {
            if (!HgEngineProject.IsActive) return;

            string ClassSymbol(int classIndex) =>
                HgEngineSymbolTable.Load(TrainerClassHeader)?.TryGetNameWithPrefix(classIndex, "TRAINERCLASS_", out string n) == true ? n : classIndex.ToString();
            string SpeciesSymbol(int speciesId) =>
                HgEngineSymbolTable.Load(SpeciesHeader)?.TryGetNameWithPrefix(speciesId, "SPECIES_", out string n) == true ? n : speciesId.ToString();
            string ItemSymbol(int itemId) =>
                HgEngineSymbolTable.Load(ItemHeader)?.TryGetNameWithPrefix(itemId, "ITEM_", out string n) == true ? n : itemId.ToString();
            string AbilitySymbol(int abilityId) =>
                HgEngineSymbolTable.Load(AbilityHeader)?.TryGetNameWithPrefix(abilityId, "ABILITY_", out string n) == true ? n : abilityId.ToString();
            string MoveSymbol(int moveId) =>
                HgEngineSymbolTable.Load(MovesHeader)?.TryGetNameWithPrefix(moveId, "MOVE_", out string n) == true ? n : moveId.ToString();
            string FlagsExpr(string header, string prefix, int value) =>
                HgEngineSymbolTable.Load(header)?.TryGetFlagsExpression(value, prefix, out string expr) == true ? expr : value.ToString();

            int trainerTypeValue = TrainerDataTypeFlags.Where(f => f.Checked).Aggregate(0, (acc, f) => acc | f.Value);
            int aiFlagsValue = AiFlags.Where(f => f.Checked).Aggregate(0, (acc, f) => acc | f.Value);
            string battleTypeLiteral = BattleTypeIndex >= 0 && BattleTypeIndex < BattleTypeOptions.Count
                ? BattleTypeOptions[BattleTypeIndex] : "SINGLE_BATTLE";

            var fields = new List<HgEngineFieldWrite>
            {
                new(new[] { FieldPathSegment.Field("name") }, HgEngineTrainerSource.ToCStringLiteral(_trainerName ?? "")),
                new(new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("trainerClass") }, ClassSymbol(_trainerClassIndex)),
                new(new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("aiFlags") }, FlagsExpr(TrainerDataHeader, "F_", aiFlagsValue)),
                new(new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("trainerType") }, FlagsExpr(TrainerDataHeader, "TRAINER_DATA_TYPE_", trainerTypeValue)),
                new(new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("battleType") }, battleTypeLiteral),
            };

            fields.Add(new(new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("items") },
                "{ " + string.Join(", ", TrainerItems.Select(t => ItemSymbol(t.ItemIndex))) + " }"));

            var abilitySlots = HgEngineTrainerFieldSchema.GetAbilitySlots();

            for (int i = 0; i < (int)_partyCount && i < Party.Count; i++)
            {
                var mon = Party[i];
                var extras = mon.HgeExtras;
                var p = FieldPathSegment.Field("party");
                FieldPathSegment[] Path(params FieldPathSegment[] rest)
                {
                    var full = new FieldPathSegment[rest.Length + 2];
                    full[0] = p; full[1] = FieldPathSegment.At(i);
                    System.Array.Copy(rest, 0, full, 2, rest.Length);
                    return full;
                }

                fields.Add(new(Path(FieldPathSegment.Field("species")), SpeciesSymbol(mon.SpeciesIndex)));
                fields.Add(new(Path(FieldPathSegment.Field("level")), mon.Level.ToString()));
                fields.Add(new(Path(FieldPathSegment.Field("ivs")), mon.Difficulty.ToString()));
                fields.Add(new(Path(FieldPathSegment.Field("ballSeal")), mon.BallSeals.ToString()));

                string abilitySlotName = mon.AbilityIndex >= 0 && mon.AbilityIndex < abilitySlots.Count
                    ? abilitySlots[mon.AbilityIndex].Name : abilitySlots.Count > 0 ? abilitySlots[0].Name : "0";
                fields.Add(new(Path(FieldPathSegment.Field("abilitySlot")), abilitySlotName));

                if (HgeItemsFlagChecked)
                    fields.Add(new(Path(FieldPathSegment.Field("item")), ItemSymbol(mon.ItemIndex)));

                if (HgeMovesFlagChecked)
                {
                    string movesLiteral = "{ " + string.Join(", ", MoveSymbol(mon.Move1), MoveSymbol(mon.Move2), MoveSymbol(mon.Move3), MoveSymbol(mon.Move4)) + " }";
                    fields.Add(new(Path(FieldPathSegment.Field("moves")), movesLiteral));
                }

                if (HgeAbilityFlagChecked)
                    fields.Add(new(Path(FieldPathSegment.Field("ability")), AbilitySymbol(extras.AbilityId)));

                if (HgeBallFlagChecked)
                    fields.Add(new(Path(FieldPathSegment.Field("ball")), ItemSymbol(extras.BallId)));

                if (HgeIvEvFlagChecked)
                {
                    string StatBlock(StatBlockViewModel s) =>
                        $"{{ .hp = {s.Hp}, .attack = {s.Attack}, .defense = {s.Defense}, .speed = {s.Speed}, .spAttack = {s.SpAttack}, .spDefense = {s.SpDefense} }}";
                    fields.Add(new(Path(FieldPathSegment.Field("setIvs")), StatBlock(extras.SetIvs)));
                    fields.Add(new(Path(FieldPathSegment.Field("setEvs")), StatBlock(extras.SetEvs)));
                }

                if (HgeNatureFlagChecked)
                    fields.Add(new(Path(FieldPathSegment.Field("nature")), extras.NatureIndex.ToString()));

                if (HgeShinyLockFlagChecked)
                    fields.Add(new(Path(FieldPathSegment.Field("shinyLock")), extras.ShinyLocked ? "1" : "0"));

                if (HgeAdditionalFlagsFlagChecked)
                {
                    int extraFlagsValue = 0;
                    if (extras.ExtraStatusEnabled) extraFlagsValue |= ExtraBit("STATUS");
                    if (extras.ExtraHpEnabled) extraFlagsValue |= ExtraBit("HP");
                    if (extras.ExtraAttackEnabled) extraFlagsValue |= ExtraBit("ATK");
                    if (extras.ExtraDefenseEnabled) extraFlagsValue |= ExtraBit("DEF");
                    if (extras.ExtraSpeedEnabled) extraFlagsValue |= ExtraBit("SPEED");
                    if (extras.ExtraSpAtkEnabled) extraFlagsValue |= ExtraBit("SP_ATK");
                    if (extras.ExtraSpDefEnabled) extraFlagsValue |= ExtraBit("SP_DEF");
                    if (extras.ExtraPpCountsEnabled) extraFlagsValue |= ExtraBit("PP_COUNTS");
                    if (extras.ExtraNicknameEnabled) extraFlagsValue |= ExtraBit("NICKNAME");

                    fields.Add(new(Path(FieldPathSegment.Field("additionalFlags")), FlagsExpr(TrainerDataHeader, "TRAINER_DATA_EXTRA_TYPE_", extraFlagsValue)));

                    if (extras.ExtraStatusEnabled) fields.Add(new(Path(FieldPathSegment.Field("status")), extras.ExtraStatus.ToString()));
                    if (extras.ExtraHpEnabled) fields.Add(new(Path(FieldPathSegment.Field("hp")), extras.ExtraHp.ToString()));
                    if (extras.ExtraAttackEnabled) fields.Add(new(Path(FieldPathSegment.Field("attack")), extras.ExtraAttack.ToString()));
                    if (extras.ExtraDefenseEnabled) fields.Add(new(Path(FieldPathSegment.Field("defense")), extras.ExtraDefense.ToString()));
                    if (extras.ExtraSpeedEnabled) fields.Add(new(Path(FieldPathSegment.Field("speed")), extras.ExtraSpeed.ToString()));
                    if (extras.ExtraSpAtkEnabled) fields.Add(new(Path(FieldPathSegment.Field("spAttack")), extras.ExtraSpAtk.ToString()));
                    if (extras.ExtraSpDefEnabled) fields.Add(new(Path(FieldPathSegment.Field("spDefense")), extras.ExtraSpDef.ToString()));
                    if (extras.ExtraPpCountsEnabled)
                    {
                        string ppLiteral = "{ " + string.Join(", ", extras.ExtraPp1, extras.ExtraPp2, extras.ExtraPp3, extras.ExtraPp4) + " }";
                        fields.Add(new(Path(FieldPathSegment.Field("ppCounts")), ppLiteral));
                    }
                    if (extras.ExtraNicknameEnabled)
                        fields.Add(new(Path(FieldPathSegment.Field("nicknameStr")), HgEngineTrainerSource.ToCStringLiteral(extras.ExtraNickname ?? "")));
                }
            }

            if (!HgEngineWriter.TryWriteFields(HgEngineDomain.Trainers, _selectedTrainerIndex, fields, out var unresolved, out string error, allowInsert: true))
            { AppLogger.Error($"hg-engine write failed for trainer {_selectedTrainerIndex}: {error}"); return; }

            if (unresolved.Count > 0)
                AppLogger.Info($"hg-engine write for trainer {_selectedTrainerIndex}: source doesn't declare {string.Join(", ", unresolved)}, left unchanged.");

            static int ExtraBit(string suffix)
            {
                foreach (var f in HgEngineTrainerFieldSchema.GetExtraFlags()) if (f.Name.EndsWith(suffix)) return f.Bit;
                return 0;
            }
        }

        // ── Add / Export / Import ─────────────────────────────────────────────────────
        /// <summary>Appends a brand-new trainer (blank properties + one empty party slot) after the
        /// last existing one and selects it, mirroring the WinForms "Add Trainer" button.</summary>
        public void AddTrainer()
        {
            // Adding a brand-new trainer entry to Trainers.c isn't built yet. Writing a blank trainer
            // straight to the local a055/a056 binaries here would be silently discarded on the next
            // hg-engine source sync and never reach a real build, so this is refused rather than doing
            // something that looks like it worked but doesn't.
            if (IsHgeActive)
            {
                _ = DialogHelper.ShowError("Adding new trainers isn't supported yet for hg-engine source-backed ROMs.", "Trainer Editor");
                return;
            }
            try
            {
                int newIndex = TrainerNames.Count;
                string suffix = Path.DirectorySeparatorChar + newIndex.ToString("D4");
                File.WriteAllBytes(gameDirs[DirNames.trainerProperties].unpackedDir + suffix,
                    new TrainerProperties((ushort)newIndex).ToByteArray());
                File.WriteAllBytes(gameDirs[DirNames.trainerParty].unpackedDir + suffix,
                    new PartyPokemon().ToByteArray());

                var ta = new TextArchive(trainerNamesMessageNumber);
                ta.SetSimpleTrainerName(newIndex, "New Trainer");
                ta.SaveToExpandedDir(trainerNamesMessageNumber, showSuccessMessage: false);

                string[] classNames = GetTrainerClassNames();
                string className = classNames.Length > 0 ? classNames[0] : "";
                TrainerNames.Add($"[{newIndex:D2}] {className} New Trainer");
                SelectedTrainerIndex = newIndex;
                StatusText = $"Added trainer {newIndex}.";
            }
            catch (Exception ex) { _ = DialogHelper.ShowError($"Couldn't add trainer:\n{ex.Message}", "Trainer Editor"); }
        }

        public async Task ExportTrainerAsync()
        {
            if (_trainer == null) return;
            SyncToTrainer();
            var filter = new FilePickerFileType("Gen IV Trainer File") { Patterns = new[] { "*.trf" } };
            string path = await DialogHelper.SaveFile(_owner, "Export trainer", new[] { filter }, $"trainer_{_selectedTrainerIndex:D4}.trf");
            if (path == null) return;
            try
            {
                File.WriteAllBytes(path, _trainer.ToByteArray());
                StatusText = "Trainer exported.";
            }
            catch (Exception ex) { await DialogHelper.ShowError($"Export failed:\n{ex.Message}", "Export Error"); }
        }

        /// <summary>Loads a combined .trf (name + properties + party) into the CURRENTLY SELECTED
        /// trainer slot's in-memory state. Like Replace Properties/Import Party below, this stages the
        /// change (marks dirty) rather than writing to disk immediately, so Undo and the Save button work.</summary>
        public async Task ImportTrainerAsync()
        {
            if (_trainer == null || _selectedTrainerIndex < 0) return;
            var filter = new FilePickerFileType("Gen IV Trainer File") { Patterns = new[] { "*.trf" } };
            string path = await DialogHelper.OpenFile(_owner, "Import trainer", new[] { filter });
            if (path == null) return;
            try
            {
                using var reader = new BinaryReader(File.OpenRead(path));
                string trName = reader.ReadString();
                byte datSize = reader.ReadByte();
                byte[] trDat = reader.ReadBytes(datSize);
                byte partySize = reader.ReadByte();
                byte[] pDat = reader.ReadBytes(partySize);

                _trainer = new TrainerFile(
                    new TrainerProperties((ushort)_selectedTrainerIndex, new MemoryStream(trDat)),
                    new MemoryStream(pDat), trName);

                _suppress = true;
                try { PopulateFromTrainer(); } finally { _suppress = false; }
                SetDirty();
                StatusText = "Trainer imported. Remember to save.";
                await DialogHelper.ShowInfo("Trainer imported successfully!\nRemember to save the current trainer.", "Import");
            }
            catch (Exception ex) { await DialogHelper.ShowError($"Import failed:\n{ex.Message}", "Import Error"); }
        }

        public async Task ExportPropertiesAsync()
        {
            if (_trainer == null) return;
            SyncToTrainer();
            var filter = new FilePickerFileType("Gen IV Trainer Properties") { Patterns = new[] { "*.trp" } };
            string path = await DialogHelper.SaveFile(_owner, "Export trainer properties", new[] { filter }, $"trainer_{_selectedTrainerIndex:D4}.trp");
            if (path == null) return;
            try
            {
                File.WriteAllBytes(path, _trainer.trp.ToByteArray());
                StatusText = "Properties exported.";
            }
            catch (Exception ex) { await DialogHelper.ShowError($"Export failed:\n{ex.Message}", "Export Error"); }
        }

        public async Task ReplacePropertiesAsync()
        {
            if (_trainer == null || _selectedTrainerIndex < 0) return;
            var filter = new FilePickerFileType("Gen IV Trainer Properties") { Patterns = new[] { "*.trp" } };
            string path = await DialogHelper.OpenFile(_owner, "Import trainer properties", new[] { filter });
            if (path == null) return;
            try
            {
                using var fs = File.OpenRead(path);
                _trainer.trp = new TrainerProperties((ushort)_selectedTrainerIndex, fs);
                _suppress = true;
                try { PopulateFromTrainer(); } finally { _suppress = false; }
                SetDirty();
                StatusText = "Properties imported. Remember to save.";
                await DialogHelper.ShowInfo("Trainer properties imported successfully!\nRemember to save the current trainer.", "Import");
            }
            catch (Exception ex) { await DialogHelper.ShowError($"Import failed:\n{ex.Message}", "Import Error"); }
        }

        public async Task ExportPartyAsync()
        {
            if (_trainer == null) return;
            SyncToTrainer();
            var filter = new FilePickerFileType("Gen IV Party Data") { Patterns = new[] { "*.pdat" } };
            string path = await DialogHelper.SaveFile(_owner, "Export trainer party", new[] { filter }, $"party_{_selectedTrainerIndex:D4}.pdat");
            if (path == null) return;
            try
            {
                _trainer.party.exportCondensedData = true;
                File.WriteAllBytes(path, _trainer.party.ToByteArray());
                _trainer.party.exportCondensedData = false;
                StatusText = "Party exported.";
            }
            catch (Exception ex) { await DialogHelper.ShowError($"Export failed:\n{ex.Message}", "Export Error"); }
        }

        public async Task ImportPartyAsync()
        {
            if (_trainer == null) return;
            var filter = new FilePickerFileType("Gen IV Party Data") { Patterns = new[] { "*.pdat" } };
            string path = await DialogHelper.OpenFile(_owner, "Import trainer party", new[] { filter });
            if (path == null) return;
            try
            {
                using var fs = File.OpenRead(path);
                _trainer.party = new Party(readFirstByte: true, TrainerFile.POKE_IN_PARTY, fs, _trainer.trp);
                _suppress = true;
                try { PopulateFromTrainer(); } finally { _suppress = false; }
                SetDirty();
                StatusText = "Party imported. Remember to save.";
                await DialogHelper.ShowInfo("Trainer party imported successfully!\nRemember to save the current trainer.", "Import");
            }
            catch (Exception ex) { await DialogHelper.ShowError($"Import failed:\n{ex.Message}", "Import Error"); }
        }

        // ── Mon reorder support ──────────────────────────────────────────────────────
        public IEnumerable<(int index, string display)> GetPartyForReorder()
        {
            int count = (int)_partyCount;
            for (int i = 0; i < count && i < Party.Count; i++)
            {
                var m = Party[i];
                string name = m.SpeciesIndex >= 0 && m.SpeciesIndex < PokemonNames.Count ? PokemonNames[m.SpeciesIndex] : "?";
                yield return (i, $"[{i}] {name} Lv. {(int)m.Level}");
            }
        }

        public void ReorderParty(List<int> newOrder)
        {
            int count = newOrder.Count;
            if (count == 0) return;
            var snap = new List<(int sp, int form, int lvl, int[] moves, int item, int gen, int ab, int diff, int ball)>();
            for (int i = 0; i < count && i < Party.Count; i++)
            {
                var m = Party[i];
                snap.Add((m.SpeciesIndex, (int)m.FormId, (int)m.Level,
                    new[] { m.Move1, m.Move2, m.Move3, m.Move4 }, m.ItemIndex,
                    m.GenderIndex, m.AbilityIndex, (int)m.Difficulty, (int)m.BallSeals));
            }
            for (int i = 0; i < count && i < Party.Count; i++)
            {
                int from = newOrder[i];
                if (from < 0 || from >= snap.Count) continue;
                var s = snap[from];
                Party[i].Load(s.sp, s.form, s.lvl, s.moves, s.item, s.gen, s.ab, s.diff, s.ball);
            }
            SetDirty();
        }

        public void GoToTrainer(int index)
        {
            if (index >= 0 && index < TrainerNames.Count) SelectedTrainerIndex = index;
        }

        // ── DV Calculator support ─────────────────────────────────────────────────────
        public (ushort trainerId, byte trainerClass, List<(int pokeId, int level, int gender, int ability, int dv)> party) GetDVCalcInput()
        {
            var list = new List<(int, int, int, int, int)>();
            int count = (int)_partyCount;
            for (int i = 0; i < count && i < Party.Count; i++)
            {
                var m = Party[i];
                list.Add((m.SpeciesIndex, (int)m.Level, m.GenderIndex, m.AbilityIndex, (int)m.Difficulty));
            }
            ushort tid = _trainer?.trp.trainerID ?? (ushort)Math.Max(0, _selectedTrainerIndex);
            return (tid, (byte)Math.Max(0, _trainerClassIndex), list);
        }

        public void ApplyDVCalc(IReadOnlyList<(int dv, int gender, int ability)> results)
        {
            for (int i = 0; i < results.Count && i < Party.Count; i++)
            {
                var m = Party[i];
                m.Difficulty = results[i].dv;
                m.GenderIndex = results[i].gender;
                m.AbilityIndex = results[i].ability;
            }
            SetDirty();
        }

        private void UpdateTrainerName(string newName)
        {
            try
            {
                _trainer.name = newName;
                var ta = new TextArchive(trainerNamesMessageNumber);
                ta.SetSimpleTrainerName(_trainer.trp.trainerID, newName);
                ta.SaveToExpandedDir(trainerNamesMessageNumber, showSuccessMessage: false);
            }
            catch (Exception ex) { AppLogger.Error("Trainer name save failed: " + ex.Message); }
        }
    }

    // ── Small sub-VMs ──────────────────────────────────────────────────────────────
    public class AiFlagViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler Changed;
        public string Label { get; }
        /// <summary>The real bit value this flag represents when dynamically resolved from an hg-engine
        /// header (see <see cref="DSPRE.HgEngine.HgEngineTrainerFieldSchema"/>); -1 for the static,
        /// vanilla-derived flag lists that don't carry a resolvable value.</summary>
        public int Value { get; }
        private bool _checked;
        public bool Checked
        {
            get => _checked;
            set { if (_checked != value) { _checked = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Checked))); Changed?.Invoke(this, EventArgs.Empty); } }
        }
        public AiFlagViewModel(string label, int value = -1) { Label = label; Value = value; }
    }

    public class TrainerItemSlotViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler Changed;
        public ObservableCollection<string> ItemNames { get; }
        private int _itemIndex = -1;
        public int ItemIndex
        {
            get => _itemIndex;
            set { if (_itemIndex != value) { _itemIndex = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ItemIndex))); Changed?.Invoke(this, EventArgs.Empty); } }
        }
        public TrainerItemSlotViewModel(ObservableCollection<string> itemNames) { ItemNames = itemNames; }
    }
}
