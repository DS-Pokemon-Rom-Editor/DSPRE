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
using Avalonia.Threading;

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
    public class TrainerEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo, IDetachable
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
        // What the list row shows, so a save that changes either refreshes it.
        private string _savedName;
        private byte _savedClass;

        private string[] _abilityNames = Array.Empty<string>();
        private (int abi1, int abi2)[] _abilities = Array.Empty<(int, int)>();
        private bool _abilityEditable;
        private bool _genderEditable;
        private bool _shinyPatch;
        private bool _customPokeballsPatch;
        private bool _ballEnabled;
        private bool _formVisible;

        public ObservableCollection<string> TrainerNames { get; } = new ObservableCollection<string>();

        /// <summary>The trainer list as shown, filtered by its search box.</summary>
        public Data.FilteredNames TrainerList => _trainerList ??= new Data.FilteredNames(TrainerNames,
            () => SelectedTrainerIndex, v => SelectedTrainerIndex = v, this, nameof(SelectedTrainerIndex));
        private Data.FilteredNames _trainerList;
        public ObservableCollection<string> TrainerClassItems { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> PokemonNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> MoveNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> ItemNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<TrainerPartyMonViewModel> Party { get; } = new ObservableCollection<TrainerPartyMonViewModel>();
        public ObservableCollection<AiFlagViewModel> AiFlags { get; } = new ObservableCollection<AiFlagViewModel>();

        /// <summary>The trainer's battle messages, read-only here; the Battle Message editor changes them.</summary>
        public sealed record BattleLine(string When, string Text);
        public ObservableCollection<BattleLine> BattleLines { get; } = new ObservableCollection<BattleLine>();
        public bool HasBattleLines => BattleLines.Count > 0;

        private void LoadBattleLines(int trainer)
        {
            BattleLines.Clear();
            if (IsHgeActive) { OnPropertyChanged(nameof(HasBattleLines)); return; }
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.TrainerTextTable, DirNames.TrainerTextOffset });
                TextArchive text = new TextArchive(TrainerMessageTextNumber);
                foreach (TrainerMessageTable.Entry e in TrainerMessageTable.Read().Where(e => e.TrainerId == trainer).OrderBy(e => e.TriggerId))
                    BattleLines.Add(new BattleLine(BattleMessageEditorViewModel.TriggerDescription(e.TriggerId),
                        e.MessageId >= 0 && e.MessageId < text.Messages.Count ? Shown(text.Messages[e.MessageId]) : ""));
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is ArgumentException)
            {
                AppLogger.Warn($"Battle messages of trainer {trainer}: {ex.Message}");
            }
            OnPropertyChanged(nameof(HasBattleLines));
        }

        // Line and page breaks are stored as escapes; here the text reads on as one paragraph.
        private static string Shown(string message) => string.Join(" ", message.Split(new[] { "\\n", "\\r", "\\f", "\n" }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()));

        // ── Where the trainer is used ───────────────────────────────────────────────
        /// <summary>One use of the trainer, and for scripts and events the places whose headers load that file.</summary>
        public sealed record TrainerUse(string Kind, string Location, string Places, int SourceId);
        public ObservableCollection<TrainerUse> Uses { get; } = new ObservableCollection<TrainerUse>();
        private string _usesStatus = "";
        public string UsesStatus { get => _usesStatus; private set => Set(ref _usesStatus, value); }

        public async Task FindUsesAsync()
        {
            int id = _loadedTrainerId;
            if (id < 0 || IsHgeActive) return;
            Uses.Clear();
            UsesStatus = "Looking…";
            (List<TrainerReference> refs, string error) = await Task.Run<(List<TrainerReference>, string)>(() =>
                TrainerReferenceScanner.TryFindCurrentProjectReferences(id, out List<TrainerReference> found, out string err) ? (found, null) : (null, err));
            if (id != _loadedTrainerId) return;
            if (refs == null) { UsesStatus = error ?? "The scan failed."; return; }
            IReadOnlyList<string> labels = HeaderLabels.Friendly();
            string Places(HeaderLinks.Kind kind, int file) => file < 0 ? "" :
                string.Join(", ", HeaderLinks.HeadersLinking(kind, file).Select(h => h.Header < labels.Count ? labels[h.Header] : h.Header.ToString()));
            foreach (TrainerReference r in refs)
            {
                string places = r.Kind == "Script" ? Places(HeaderLinks.Kind.Script, r.SourceId)
                    : r.Kind == "Event" ? Places(HeaderLinks.Kind.Event, r.SourceId) : "";
                Uses.Add(new TrainerUse(r.Kind, r.Location, places, r.SourceId));
            }
            UsesStatus = Uses.Count == 0 ? "Not used anywhere." : "";
        }

        /// <summary>Opens the script or event file a use is in.</summary>
        public void OpenUse(TrainerUse use)
        {
            if (use == null || use.SourceId < 0) return;
            if (use.Kind == "Script") AvaloniaEditorLauncher.OpenScriptEditor(use.SourceId);
            else if (use.Kind == "Event") AvaloniaEditorLauncher.OpenEventEditor(use.SourceId);
        }

        private void OnTextSaved(object sender, int archive)
        {
            if (archive == TrainerMessageTextNumber && _loadedTrainerId >= 0) LoadBattleLines(_loadedTrainerId);
        }

        // ── Prize money ─────────────────────────────────────────────────────────────
        private readonly Dictionary<int, int?> _prizeMul = new Dictionary<int, int?>();

        private int? PrizeMultiplier(int trainerClass)
        {
            if (trainerClass < 0) return null;
            if (!_prizeMul.TryGetValue(trainerClass, out int? mul))
                _prizeMul[trainerClass] = mul = TrainerPrize.TryGetMultiplier(trainerClass, out int m) ? m : null;
            return mul;
        }

        private TrainerPartyMonViewModel LastPartyMon => Party.Take((int)PartyCount).LastOrDefault();

        /// <summary>What beating this trainer pays, or null when the class multiplier can't be read.</summary>
        public string PrizeText => LastPartyMon is TrainerPartyMonViewModel last && PrizeMultiplier(_trainerClassIndex) is int mul
            ? TrainerPrize.Amount((int)last.Level, mul, DoubleBattle).ToString("N0") : null;
        public bool HasPrize => PrizeText != null;
        /// <summary>The Pokédollar sign from the ROM's font, which the character map writes as $.</summary>
        public Bitmap PrizeSign => DSPRE.Avalonia.Data.GameGlyph.Mask('$');
        public bool HasPrizeSign => PrizeSign != null;
        public string PrizeTip => PrizeMultiplier(_trainerClassIndex) is int mul
            ? $"Last Pokémon's level × 4 × the class multiplier ({mul}){(DoubleBattle ? " × 2 for a double battle" : "")}. Amulet Coin doubles it."
            : null;
        private void RaisePrize() { OnPropertyChanged(nameof(PrizeText)); OnPropertyChanged(nameof(HasPrize)); OnPropertyChanged(nameof(PrizeTip)); }

        // ── What each party member's personality gives ──────────────────────────────
        // Edits come in bursts (a load sets every field), so the recount runs once after them.
        private bool _derivedQueued;
        private void QueueDerived()
        {
            if (_derivedQueued) return;
            _derivedQueued = true;
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => { _derivedQueued = false; RefreshDerived(); },
                global::Avalonia.Threading.DispatcherPriority.Background);
        }

        /// <summary>Every DV, IV and nature this slot can get with the slots before it as they are now.</summary>
        public List<DVIVNatureTriplet> AllNatureOptions(TrainerPartyMonViewModel mon)
        {
            int slot = Party.IndexOf(mon);
            (ushort tid, byte cls, List<(int pokeId, int level, int gender, int ability, int dv)> input) = GetDVCalcInput();
            List<DVCalculator.PartyMon> mons = input.Select(p => new DVCalculator.PartyMon(p.pokeId, p.level, p.gender, p.ability, p.dv)).ToList();
            if (slot < 0 || slot >= mons.Count || mons[slot].Species <= 0) return new List<DVIVNatureTriplet>();
            bool male;
            try { male = DVCalculator.TrainerClassGender.GetTrainerClassGender(cls); }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is IndexOutOfRangeException) { male = true; }
            return DVCalculator.SlotOptions(tid, cls, male, mons, slot, highestOnly: false);
        }

        private void RefreshDerived()
        {
            if (_trainer == null && !IsHgeActive) return;
            (ushort tid, byte cls, List<(int pokeId, int level, int gender, int ability, int dv)> input) = GetDVCalcInput();
            List<DVCalculator.PartyMon> mons = input.Select(p => new DVCalculator.PartyMon(p.pokeId, p.level, p.gender, p.ability, p.dv)).ToList();
            if (mons.Count == 0 || mons.Any(m => m.Species <= 0)) return;
            bool male;
            try { male = DVCalculator.TrainerClassGender.GetTrainerClassGender(cls); }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is IndexOutOfRangeException) { male = true; }
            try
            {
                uint[] pids = DVCalculator.PartyPids(tid, cls, male, mons);
                for (int i = 0; i < mons.Count && i < Party.Count; i++)
                {
                    // The default flags' result for this slot, the slots before it as they are.
                    List<DVCalculator.PartyMon> plain = new List<DVCalculator.PartyMon>(mons);
                    plain[i] = plain[i] with { Gender = 0, Ability = 0 };
                    uint defaultPid = DVCalculator.PartyPids(tid, cls, male, plain)[i];
                    List<TrainerPartyMonViewModel.NatureChoice> choices = DVCalculator.SlotOptions(tid, cls, male, mons, i, highestOnly: true)
                        .OrderBy(t => t.Nature).Select(t => new TrainerPartyMonViewModel.NatureChoice(t.Nature, t.DV)).ToList();
                    Party[i].ShowPersonality(pids[i], defaultPid, choices, _chooseMoves ? null : DefaultMoveNames(Party[i]));
                }
            }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is IndexOutOfRangeException)
            {
                AppLogger.Warn("Trainer party natures: " + e.Message);
            }
        }

        private List<TrainerPartyMonViewModel.DefaultMove> DefaultMoveNames(TrainerPartyMonViewModel mon)
        {
            ushort[] moves = LevelUpMoves(mon);
            return moves == null ? null : moves.Where(m => m > 0)
                .Select(m => new TrainerPartyMonViewModel.DefaultMove(m, m < MoveNames.Count ? MoveNames[m] : m.ToString())).ToList();
        }

        private bool _learnsetsUnpacked;

        private ushort[] LevelUpMoves(TrainerPartyMonViewModel mon)
        {
            try
            {
                if (IsHgeActive) return HgeLevelUpMoves(mon.DataSpeciesId, (int)mon.Level);
                // Read before anything else unpacked them, the files are missing and the defaults would stay empty.
                if (!_learnsetsUnpacked) { DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.Learnsets }); _learnsetsUnpacked = true; }
                return new LearnsetData(mon.SpeciesIndex).GetLearnsetAtLevel((int)mon.Level);
            }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is IndexOutOfRangeException) { return null; }
        }

        /// <summary>Swaps a party member with the one above or below it.</summary>
        public void MoveMon(TrainerPartyMonViewModel mon, int delta)
        {
            int from = Party.IndexOf(mon), to = from + delta, count = (int)_partyCount;
            if (from < 0 || to < 0 || to >= count) return;
            List<int> order = Enumerable.Range(0, count).ToList();
            (order[from], order[to]) = (order[to], order[from]);
            ReorderParty(order);
        }

        // Held items (4 combos)
        public ObservableCollection<TrainerItemSlotViewModel> TrainerItems { get; } = new ObservableCollection<TrainerItemSlotViewModel>();

        // ── hg-engine-only: real trainer-data-type flags + battle type, dynamically resolved from the
        // linked checkout's include/trainer_data.h (never a hardcoded vanilla-derived list). Empty/unused
        // for vanilla ROMs, where the existing ChooseMoves/ChooseItems/DoubleBattle checkboxes below
        // still apply.
        public bool IsHgeActive => HgEngineProject.IsActive;
        public ObservableCollection<AiFlagViewModel> TrainerDataTypeFlags { get; } = new ObservableCollection<AiFlagViewModel>();
        public ObservableCollection<string> BattleTypeOptions { get; } = new ObservableCollection<string>();

        // The trainer's battle type is OR'd into the battle's flags (pokeplatinum trainer_data.c); vanilla trainers
        // use only these two. Any other value shows as its number and is kept.
        public IReadOnlyList<string> BattleTypeNames { get; } = new[] { "Single", "Double" };
        public IList<int> BattleTypeKeys { get; } = new[] { 0, 2 };
        private int[] _battleTypeValues = Array.Empty<int>();
        private int _battleTypeIndex;
        public int BattleTypeIndex
        {
            get => _battleTypeIndex;
            set
            {
                if (value < 0 && !_suppress)
                {
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(BattleTypeIndex)));
                    return;
                }
                if (Set(ref _battleTypeIndex, value) && !_suppress) SetDirty();
            }
        }

        // The ComboBox can drop its selection while its rows fill, and the binding ignores an unchanged value,
        // so blank it on one frame and restore it on a later one.
        // One pending repush at a time: a second one queued behind it would save the -1 and restore that.
        private bool _battleTypeRepushPending;
        private void RepushBattleTypeIndex()
        {
            if (_battleTypeRepushPending) return;
            _battleTypeRepushPending = true;
            Dispatcher dispatcher = global::Avalonia.Threading.Dispatcher.UIThread;
            dispatcher.Post(() =>
            {
                int keep = _battleTypeIndex;
                bool wasSuppressed = _suppress;
                _suppress = true;
                _battleTypeIndex = -1; OnPropertyChanged(nameof(BattleTypeIndex));
                _suppress = wasSuppressed;
                dispatcher.Post(() => { _battleTypeIndex = keep; _battleTypeRepushPending = false; OnPropertyChanged(nameof(BattleTypeIndex)); },
                    global::Avalonia.Threading.DispatcherPriority.Background);
            }, global::Avalonia.Threading.DispatcherPriority.Background);
        }

        // hg-engine trainers may have no party at all; a vanilla one keeps an empty party only as read.
        public decimal PartyCountMinimum => IsHgeActive || _emptyPartyAsRead ? 0 : 1;
        private bool _emptyPartyAsRead;
        private void SetEmptyPartyAsRead(bool value)
        {
            if (_emptyPartyAsRead == value) return;
            _emptyPartyAsRead = value;
            OnPropertyChanged(nameof(PartyCountMinimum));
        }

        // What Trainers.c held that the editor could not read; saving leaves those values alone.
        private bool _hgeTrainerTypeKnown, _hgeAiFlagsKnown;
        private readonly string[] _hgeItemRaw = new string[TrainerProperties.TrainerItemCount];

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
            foreach (TrainerPartyMonViewModel mon in Party)
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
            "Basic", "Evaluate Attack", "Expert", "Setup", "Risky",
            "Prioritize Extremes", "Baton Pass", "Tag Strategy", "Check HP", "Weather", "Harassment"
        };

        // Each flag's intent, from Lhea's notes on the Gen 4 trainer AI.
        public const string AiNotesUrl = "https://gist.github.com/lhearachel/ff61af1f58c84c96592b0b8184dba096";
        private static readonly string[] AiFlagTips =
        {
            "Discourage moves which would result in a (literally or functionally) wasted turn or which would immediately benefit the opponent.",
            "Prioritize raw damage output over all other outcomes.",
            "Provide additional logic to encourage/discourage certain move effects in particular circumstances.",
            "On the first turn of the battle (for the entire battle, not the battler), prioritize setup moves.",
            "Prioritize \"Risky\" attacking or status moves.",
            "Prioritize moves with variable power, flat damage, high base power, or zero power.",
            "Prioritize setting up when at higher HP thresholds and passing stat stage bonuses to party members.",
            "Encourage moves which would benefit our partner and discourage moves which would harm them.",
            "Discourage certain move effects at particular HP thresholds for both the attacker and target.",
            "Set up weather effects on the first turn of battle if they are not already present.",
            "Incentivize usage of moves which harass or disrupt the opponent's strategy.",
        };
        private static string AiFlagTip(int i) => i >= 0 && i < AiFlagTips.Length ? AiFlagTips[i] : null;

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
            set
            {
                if (RecordSwitchGuard.IsSnappingBack) return;
                if (value == _selectedTrainerIndex) return;
                if (_dirty && !_suppress && value >= 0 && _selectedTrainerIndex >= 0)
                {
                    // Snap the list back to the trainer still loaded until the user has answered.
                    int requested = value;
                    RecordSwitchGuard.SnapBack(() => _selectedTrainerIndex, v => _selectedTrainerIndex = v, () => OnPropertyChanged(nameof(SelectedTrainerIndex)));
                    _ = SwitchTrainerAsync(requested);
                    return;
                }
                if (Set(ref _selectedTrainerIndex, value) && !_suppress && value >= 0) _ = LoadTrainerWithBusyIndicatorAsync(value);
            }
        }

        private async Task SwitchTrainerAsync(int requested)
        {
            if (!await RecordSwitchGuard.ConfirmLeaveAsync(new TrainerSwitchTarget(this), _owner, "trainer")) return;
            SetClean();
            if (Set(ref _selectedTrainerIndex, requested, nameof(SelectedTrainerIndex))) await LoadTrainerWithBusyIndicatorAsync(requested);
        }

        // Only the trainer's edits belong to the selection: Save keeps both tabs, Discard keeps the class edits.
        private sealed class TrainerSwitchTarget : IEditorWithUnsavedChanges
        {
            private readonly TrainerEditorViewModel _vm;
            public TrainerSwitchTarget(TrainerEditorViewModel vm) => _vm = vm;
            public bool HasUnsavedChanges => _vm._dirty;
            public string UnsavedChangesDescription => _vm.UnsavedChangesDescription;
            public void SaveChanges() => _vm.SaveChanges();
            public async Task<bool> SaveChangesAsync() => await ((IEditorWithUnsavedChanges)_vm).SaveChangesAsync() && !_vm._dirty;
            public void DiscardChanges() => _vm.DiscardTrainer();
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
        public int TrainerNameMaxLength { get; } = SafeNameMax();
        private static int SafeNameMax() { try { return TrainerNameMaxChars; } catch (Exception ex) { AppLogger.WarnOnce("TrainerEditorViewModel.SafeNameMax", ex); return TrainerFile.CompressedNameLen(TrainerFile.DefaultNameLen + 1); } }

        private int _trainerClassIndex = -1;
        public int TrainerClassIndex
        {
            get => _trainerClassIndex;
            set
            {
                // The ComboBox reports -1 while its list refills; only a load may clear the class.
                if (value < 0 && !_suppress)
                {
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(TrainerClassIndex)));
                    return;
                }
                if (Set(ref _trainerClassIndex, value)) { if (!_suppress) SetDirty(); UpdateTrainerSprite(); OnPropertyChanged(nameof(ClassGender)); }
            }
        }

        /// <summary>The class's gender, which goes into every party Pokémon's personality.</summary>
        public string ClassGender
        {
            get
            {
                if (_trainerClassIndex < 0 || IsHgeActive) return "";
                try { return DVCalculator.TrainerClassGender.GetTrainerClassGender(_trainerClassIndex) ? "Male" : "Female"; }
                catch (Exception e) when (e is IOException || e is ArgumentException || e is IndexOutOfRangeException) { return ""; }
            }
        }

        private uint _battleType;
        public bool DoubleBattle
        {
            get => (_battleType & 2) != 0;
            set => SetBattleType(value ? _battleType | 2u : _battleType & ~2u);
        }
        public string BattleTypeRaw
        {
            get => _battleType.ToString();
            set { if (uint.TryParse(value?.Trim(), out uint v)) SetBattleType(v); }
        }

        /// <summary>The battle type flags as a number, for the number box every editor uses.</summary>
        public decimal BattleTypeValue
        {
            get => _battleType;
            set => SetBattleType((uint)Math.Max(0, value));
        }
        private void SetBattleType(uint value)
        {
            if (value == _battleType) return;
            _battleType = value;
            OnPropertyChanged(nameof(DoubleBattle));
            OnPropertyChanged(nameof(BattleTypeRaw));
            OnPropertyChanged(nameof(BattleTypeValue));
            if (!_suppress) SetDirty();
        }
        private bool _chooseMoves;
        public bool ChooseMoves
        {
            get => _chooseMoves;
            set
            {
                if (!Set(ref _chooseMoves, value)) return;
                QueueDerived();
                if (value && !_suppress) FillLevelUpMoves();
                ApplyMovesEnabled();
                if (!_suppress) SetDirty();
            }
        }
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
        public bool HasUnsavedChanges => _dirty || _classes?.HasUnsavedChanges == true;
        public string UnsavedChangesDescription =>
            _dirty && _classes?.HasUnsavedChanges == true ? $"{TrainerLabel}, {_classes.UnsavedChangesDescription}"
            : _dirty || _classes == null ? TrainerLabel
            : _classes.UnsavedChangesDescription;

        private string TrainerLabel => _loadedTrainerId >= 0 && _loadedTrainerId < TrainerNames.Count ? TrainerNames[_loadedTrainerId] : $"Trainer {_loadedTrainerId}";
        public void SaveChanges()
        {
            if (_dirty) Save();
            if (_classes?.HasUnsavedChanges == true) _classes.SaveChanges();
        }
        public void DiscardChanges()
        {
            _dirty = false;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            if (_selectedTrainerIndex >= 0) LoadTrainer(_selectedTrainerIndex);
            _classes?.DiscardChanges();
        }

        /// <summary>Reloads the shown trainer from disk; the Classes tab keeps its own edits.</summary>
        public void DiscardTrainer()
        {
            if (_selectedTrainerIndex >= 0) LoadTrainer(_selectedTrainerIndex);
        }

        /// <summary>The Classes tab shares this window, so its unsaved edits are this editor's too.</summary>
        public TrainerClassesViewModel Classes
        {
            get => _classes;
            set
            {
                if (_classes != null) _classes.PropertyChanged -= OnClassesChanged;
                _classes = value;
                if (_classes != null) _classes.PropertyChanged += OnClassesChanged;
                OnPropertyChanged(nameof(HasUnsavedChanges));
            }
        }
        private TrainerClassesViewModel _classes;
        private void OnClassesChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(HasUnsavedChanges)) OnPropertyChanged(nameof(HasUnsavedChanges));
        }
        // RecordUndoSnapshot runs BEFORE the _dirty short-circuit so EVERY edit is captured (not just the first).
        private void SetDirty() { RaisePrize(); QueueDerived(); if (_suppress) return; RecordUndoSnapshot(); if (_dirty) return; _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
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
                Trp   = _trainer.Trp.ToByteArray(),
                // Every slot, including those past the party count, so undo brings hidden ones back.
                Party = _trainer.Party.ToByteArray(TrainerFile.PokeInParty),
                Name  = _trainerName,
            };
        }

        private void ApplyState(TrainerSnapshot snap)
        {
            if (snap == null || _trainer == null || _loadedTrainerId < 0) return;
            _suppress = true;
            TrainerProperties trp = new TrainerProperties((ushort)_loadedTrainerId, new MemoryStream(snap.Trp));
            byte partyCount = trp.PartyCount;
            trp.PartyCount = TrainerFile.PokeInParty;
            _trainer = new TrainerFile(trp, new MemoryStream(snap.Party), snap.Name);
            trp.PartyCount = partyCount;
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
            for (int i = 0; i < AiFlagLabels.Length; i++) AiFlags.Add(new AiFlagViewModel(AiFlagLabels[i]) { Tip = AiFlagTip(i) });
        }

        public TrainerEditorViewModel(bool _) { }

        private void OnNamesChanged(object sender, System.EventArgs e)
        {
            DSPRE.Avalonia.Data.ListSync.Apply(PokemonNames, GetPokemonNames());
            DSPRE.Avalonia.Data.ListSync.Apply(MoveNames,    GetAttackNames());
            DSPRE.Avalonia.Data.ListSync.Apply(ItemNames,    GetItemNames());
            if (_customPokeballsPatch) ShowCapsuleNames();
            string[] classNames = GetTrainerClassNames();
            // Replacing the selected row can clear the list's selection, as in RefreshTrainerListEntry.
            int keep = _selectedTrainerIndex;
            bool wasSuppressed = _suppress;
            _suppress = true;
            try
            {
                DSPRE.Avalonia.Data.ListSync.Apply(TrainerNames, TrainerListEntries(classNames));
                if (keep >= 0 && keep < TrainerNames.Count)
                {
                    _selectedTrainerIndex = keep;
                    OnPropertyChanged(nameof(SelectedTrainerIndex));
                }
            }
            finally { _suppress = wasSuppressed; }

            List<string> formatted = new System.Collections.Generic.List<string>(classNames.Length);
            for (int i = 0; i < classNames.Length; i++) formatted.Add($"[{i:D3}] {classNames[i]}");
            DSPRE.Avalonia.Data.ListSync.Apply(TrainerClassItems, formatted);
        }
        /// <summary>Unsubscribes from app-wide events; call when the editor window closes.</summary>
        public void Detach()
        {
            AppEvents.NamesChanged -= OnNamesChanged;
            AppEvents.RomPatchStateChanged -= OnRomPatchStateChanged;
            AppEvents.ClassIntrosSaved -= OnClassIntrosSaved;
            _spriteSaved?.Dispose();
            Data.TrainerCapsuleCatalog.Changed -= ShowCapsuleNames;
            TextArchive.Saved -= OnTextSaved;
            StopPlaying();
        }

        private void OnClassIntrosSaved(object sender, EventArgs e) => Classes?.RefreshIntroSummary();

        // Reconfigure controls without reloading party data or discarding unsaved edits.
        private void OnRomPatchStateChanged(object sender, EventArgs e)
        {
            bool shiny = GameFamily == GameFamilies.HGSS && TrainerShinyPatch.DetectCurrentProject();
            if (shiny != _shinyPatch)
            {
                _shinyPatch = shiny;
                foreach (TrainerPartyMonViewModel mon in Party) mon.ShinyEnabled = shiny;
            }
            Classes?.RefreshPatchState();
            bool customPokeballs = TrainerCustomPokeballsPatch.DetectCurrentProject();
            if (customPokeballs != _customPokeballsPatch)
            {
                _customPokeballsPatch = customPokeballs;
                if (customPokeballs) DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.ItemIcons });
                ShowCapsuleNames();
            }
        }

        // A new list instance after a save makes every picker refresh its rows.
        private void ShowCapsuleNames()
        {
            IList<string> names = _customPokeballsPatch ? Array.Empty<string>() : Data.TrainerCapsuleCatalog.Names();
            foreach (TrainerPartyMonViewModel mon in Party) mon.ConfigureCapsulePicker(_customPokeballsPatch, names);
        }

        // ── Setup ─────────────────────────────────────────────────────────────────────
        public async Task SetupAsync(Window owner)
        {
            _owner = owner;
            StatusText = "Loading trainers…";
            PushLoading();
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> {
                    DirNames.TrainerProperties, DirNames.TrainerParty, DirNames.TrainerGraphics,
                    DirNames.PersonalPokeData, DirNames.MonIcons, DirNames.TextArchives });
                SetMonIconsPalTableAddress();

                _genderEditable = GameFamily == GameFamilies.HGSS || AIBackportEnabled;
                _shinyPatch = GameFamily == GameFamilies.HGSS && TrainerShinyPatch.DetectCurrentProject();
                _customPokeballsPatch = TrainerCustomPokeballsPatch.DetectCurrentProject();
                if (_customPokeballsPatch) DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.ItemIcons });
                _abilityEditable = _genderEditable;
                _ballEnabled = GameFamily != GameFamilies.DP;
                _formVisible = GameFamily != GameFamilies.DP;

                foreach (string n in GetPokemonNames()) PokemonNames.Add(n);
                foreach (string n in GetAttackNames()) MoveNames.Add(n);
                foreach (string n in GetItemNames()) ItemNames.Add(n);
                _abilityNames = GetAbilityNames();
                LoadAbilities();

                string[] classNames = GetTrainerClassNames();
                foreach (string n in TrainerListEntries(classNames)) TrainerNames.Add(n);
                AppEvents.NamesChanged += OnNamesChanged;   // live-refresh names from the Text editor
                TextArchive.Saved += OnTextSaved;
                AppEvents.RomPatchStateChanged += OnRomPatchStateChanged;
                AppEvents.ClassIntrosSaved += OnClassIntrosSaved;
                _spriteSaved = AppEvents.WhenSaved(UpdateTrainerSprite, DirNames.TrainerGraphics);

                for (int i = 0; i < classNames.Length; i++) TrainerClassItems.Add($"[{i:D3}] {classNames[i]}");

                if (IsHgeActive)
                {
                    foreach (HgEngineTrainerFieldSchema.NamedFlag flag in HgEngineTrainerFieldSchema.GetAiFlags())
                    {
                        AiFlagViewModel f = new AiFlagViewModel(flag.Name, flag.Bit);
                        f.Changed += (s, e) => { if (!_suppress) _hgeAiFlagsKnown = true; SetDirty(); };
                        AiFlags.Add(f);
                    }
                    foreach (HgEngineTrainerFieldSchema.NamedFlag flag in HgEngineTrainerFieldSchema.GetTrainerDataTypeFlags())
                    {
                        AiFlagViewModel f = new AiFlagViewModel(flag.Name, flag.Bit);
                        f.Changed += (s, e) =>
                        {
                            if (!_suppress) _hgeTrainerTypeKnown = true;
                            if (f.Checked && !_suppress) FillHgeDefaults(f.Label);
                            ApplyHgeGatingToParty();
                            RaiseHgeFlagCheckedChanged();
                            SetDirty();
                        };
                        TrainerDataTypeFlags.Add(f);
                    }
                    IReadOnlyList<HgEngineTrainerFieldSchema.NamedFlag> battleTypes = HgEngineTrainerFieldSchema.GetBattleTypes();
                    _battleTypeValues = battleTypes.Select(b => b.Bit).ToArray();
                    foreach (HgEngineTrainerFieldSchema.NamedFlag bt in battleTypes) BattleTypeOptions.Add(bt.Name);
                }
                else
                {
                    for (int i = 0; i < AiFlagLabels.Length; i++)
                    {
                        AiFlagViewModel f = new AiFlagViewModel(AiFlagLabels[i]) { Tip = AiFlagTip(i) };
                        f.Changed += (s, e) => SetDirty();
                        AiFlags.Add(f);
                    }
                }

                for (int i = 0; i < TrainerProperties.TrainerItemCount; i++)
                {
                    TrainerItemSlotViewModel slot = new TrainerItemSlotViewModel(ItemNames);
                    slot.Changed += (s, e) => SetDirty();
                    TrainerItems.Add(slot);
                }

                // hg-engine's real TrainerPokemonData struct has no gender field, nowhere to write a
                // forced gender to, so the selector is hidden rather than shown-but-silently-ignored.
                bool genderVisibleForMode = _genderEditable && !IsHgeActive;
                for (int i = 0; i < TrainerFile.PokeInParty; i++)
                {
                    TrainerPartyMonViewModel mon = new TrainerPartyMonViewModel(i, PokemonNames, MoveNames, ItemNames,
                        _abilityNames, _abilities, _abilityEditable, genderVisibleForMode, _formVisible, _ballEnabled);
                    mon.Changed += (s, e) => SetDirty();
                    Party.Add(mon);
                }
                ApplyHgeGatingToParty();
                if (_ballEnabled && (_customPokeballsPatch || Data.TrainerCapsuleCatalog.Available))
                {
                    ShowCapsuleNames();
                    Data.TrainerCapsuleCatalog.Changed += ShowCapsuleNames;
                }

                StatusText = $"{TrainerNames.Count} trainers.";
                if (TrainerNames.Count > 0)
                    SelectedTrainerIndex = Math.Min(Math.Max(0, InitialIndex), TrainerNames.Count - 1);
            }
            catch (Exception ex)
            {
                StatusText = $"Error loading trainers: {ex.Message}";
                await DialogHelper.ShowErrorAsync($"Failed to load trainers:\n{ex.Message}", "Trainer editor error");
            }
            finally { PopLoading(); }
        }

        private void LoadAbilities()
        {
            // Form species sit past the name list.
            int count = Math.Max(PokemonNames.Count, GetPersonalFilesCount());
            _abilities = new (int, int)[count];
            string dir = GameDirs[DirNames.PersonalPokeData].unpackedDir;
            for (int i = 0; i < count; i++)
            {
                try
                {
                    string path = Path.Combine(dir, i.ToString("D4"));
                    if (!File.Exists(path)) { _abilities[i] = (0, 0); continue; }
                    // hg-engine widens both abilities to u16 at other offsets.
                    PokemonPersonalData personal = new PokemonPersonalData(new FileStream(path, FileMode.Open, FileAccess.Read));
                    _abilities[i] = (personal.FirstAbility, personal.SecondAbility);
                }
                catch (Exception ex) { AppLogger.WarnOnce("TrainerEditorViewModel.LoadAbilities", ex); _abilities[i] = (0, 0); }
            }

            // The built copies trail Species.c until the next make.
            if (!IsHgeActive) return;
            if (!HgEngineSpeciesPersonalFields.TryLoadAllAbilities(out Dictionary<int, (int first, int second)> fromSource, out string error))
            {
                AppLogger.Error("Trainer Editor abilities could not be read from Species.c: " + error);
                return;
            }
            foreach (KeyValuePair<int, (int first, int second)> pair in fromSource)
            {
                if (pair.Key < 0 || pair.Key >= count) continue;
                (int first, int second) = pair.Value;
                _abilities[pair.Key] = (first >= 0 ? first : _abilities[pair.Key].abi1, second >= 0 ? second : _abilities[pair.Key].abi2);
            }
        }

        // ── Load a trainer ─────────────────────────────────────────────────────────────
        private void LoadTrainer(int index)
        {
            if (IsHgeActive) { LoadTrainerFromSource(index); return; }
            try
            {
                string[] trNames = GetSimpleTrainerNames();
                bool error = index >= trNames.Length;
                _trainer = TrainerRecords.Load(index, error ? TrainerFile.NAME_NOT_FOUND : trNames[index]);
                _loadedTrainerId = index;
                _savedName = _trainer.Name;
                _savedClass = _trainer.Trp.TrainerClass;

                _suppress = true;
                try { PopulateFromTrainer(); }
                finally { _suppress = false; }

                SetClean();
                _history.Reset(Snapshot());   // loaded state is the clean undo baseline for this trainer
                _lastCaptureUtc = DateTime.MinValue;
                RaiseUndoState();
                StatusText = _statusAfterLoad ?? $"Trainer {index} loaded.";
                _statusAfterLoad = null;
                OnPropertyChanged(nameof(UnsavedChangesDescription));
                LoadBattleLines(index);
                RaisePrize();
                QueueDerived();
                Uses.Clear();
                UsesStatus = "";
            }
            catch (Exception ex)
            {
                _ = DialogHelper.ShowErrorAsync($"Failed to load trainer {index}:\n{ex.Message}", "Trainer editor error");
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
        private const string PokemonConstantsHeader = "include/constants/pokemon.h";
        private const string BattleConstantsHeader = "include/constants/battle_constants.h";

        private void LoadTrainerFromSource(int index)
        {
            if (!HgEngineTrainerSource.TryLoad(index, out HgEngineSourceBlock block, out string error))
            {
                _ = DialogHelper.ShowErrorAsync($"Failed to load trainer {index} from hg-engine source:\n{error}", "Trainer editor error");
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

            // An unresolved class stays blank and unwritten rather than inheriting the previous trainer's.
            TrainerClassIndex = block.TryGetSymbol(new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("trainerClass") }, TrainerClassHeader, out int classId)
                ? classId : -1;

            bool gotTrainerType = block.TryGetFlagsValue(new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("trainerType") }, TrainerDataHeader, out int trainerTypeValue);
            _hgeTrainerTypeKnown = gotTrainerType;
            foreach (AiFlagViewModel f in TrainerDataTypeFlags) f.Checked = gotTrainerType && (trainerTypeValue & f.Value) != 0;
            RaiseHgeFlagCheckedChanged();
            ChooseMoves = HgeMovesFlagChecked;
            ChooseItems = HgeItemsFlagChecked;

            bool gotAiFlags = block.TryGetFlagsValue(new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("aiFlags") }, TrainerDataHeader, out int aiFlagsValue);
            _hgeAiFlagsKnown = gotAiFlags;
            foreach (AiFlagViewModel f in AiFlags) f.Checked = gotAiFlags && (aiFlagsValue & f.Value) != 0;

            // A battle type with no row stays -1 so saving leaves it alone.
            FieldPathSegment[] battleTypePath = new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("battleType") };
            BattleTypeIndex = block.TryGetSymbol(battleTypePath, TrainerDataHeader, out int battleTypeValue)
                ? Array.IndexOf(_battleTypeValues, battleTypeValue)
                : block.TryGetRaw(battleTypePath, out _) ? -1 : 0;   // absent means 0, SINGLE_BATTLE
            DoubleBattle = BattleTypeIndex > 0;   // kept loosely in sync for display only; save uses BattleTypeIndex
            RepushBattleTypeIndex();

            IReadOnlyList<HgEngineSourceBlock> itemsRaw = block.GetArrayElements(new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("items") });
            for (int i = 0; i < TrainerItems.Count; i++)
            {
                _hgeItemRaw[i] = i < itemsRaw.Count ? itemsRaw[i].Raw.Trim() : null;
                TrainerItems[i].Load(_hgeItemRaw[i] != null && HgEngineSourceBlock.TryResolveToken(_hgeItemRaw[i], ItemHeader, out int itemVal) ? itemVal : -1);
            }

            IReadOnlyList<HgEngineSourceBlock> partyRaw = block.GetArrayElements(new[] { FieldPathSegment.Field("party") });
            PartyCount = partyRaw.Count;
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
            // Unreadable species stays -1 so saving leaves the source value alone.
            int species = -1, form = 0;
            if (monBlock.TryGetRaw(new[] { FieldPathSegment.Field("species") }, out string speciesRaw)
                && HgEngineTrainerSource.TryParseSpecies(speciesRaw, SpeciesHeader, out int parsedSpecies, out int parsedForm))
            { species = parsedSpecies; form = parsedForm; }
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

            // A slot value with no row stays -1 so saving leaves it alone.
            int abilityIndex = 0;
            if (monBlock.TryGetSymbol(new[] { FieldPathSegment.Field("abilitySlot") }, TrainerDataHeader, out int slotValue))
            {
                HgEngineTrainerFieldSchema.NamedFlag slot = HgEngineTrainerFieldSchema.GetAbilitySlots().FirstOrDefault(s => s.Bit == slotValue && s.Name != null);
                abilityIndex = slot.Name == null ? -1 : Array.IndexOf(TrainerPartyMonViewModel.HgeAbilitySlotNames, slot.Name);
            }

            // hg-engine's TrainerPokemonData has no gender field at all, nowhere to read a forced
            // gender from, so it's always "Default" here (the Gender selector is hidden in the UI too).
            mon.Load(species, form, Math.Max(1, level), moves, itemVal, 0, abilityIndex, ivs, ballSeal);

            TrainerPartyMonHgeExtras extras = mon.HgeExtras;
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

            extras.NatureIndex = HgeNatureFlagChecked && monBlock.TryGetSymbol(new[] { FieldPathSegment.Field("nature") }, PokemonConstantsHeader, out int nature) ? nature : 0;
            extras.ShinyLocked = HgeShinyLockFlagChecked && monBlock.TryGetRaw(new[] { FieldPathSegment.Field("shinyLock") }, out string shinyRaw)
                && HgEngineTrainerSource.TryParseBool(shinyRaw, out bool shiny) && shiny;

            int extraFlagsValue = 0;
            bool gotExtraFlags = HgeAdditionalFlagsFlagChecked &&
                monBlock.TryGetFlagsValue(new[] { FieldPathSegment.Field("additionalFlags") }, TrainerDataHeader, out extraFlagsValue);
            IReadOnlyList<HgEngineTrainerFieldSchema.NamedFlag> extraFlagBits = gotExtraFlags ? HgEngineTrainerFieldSchema.GetExtraFlags() : System.Array.Empty<HgEngineTrainerFieldSchema.NamedFlag>();
            int ExtraBit(string suffix) { foreach (HgEngineTrainerFieldSchema.NamedFlag f in extraFlagBits) if (f.Name.EndsWith(suffix)) return f.Bit; return 0; }

            extras.ExtraStatusEnabled = gotExtraFlags && (extraFlagsValue & ExtraBit("STATUS")) != 0;
            extras.ExtraStatus = extras.ExtraStatusEnabled && monBlock.TryGetFlagsValue(new[] { FieldPathSegment.Field("status") }, BattleConstantsHeader, out int status) ? status : 0;

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
                IReadOnlyList<HgEngineSourceBlock> pp = monBlock.GetArrayElements(new[] { FieldPathSegment.Field("ppCounts") });
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
            FieldPathSegment[] path = nestedField != null
                ? new[] { FieldPathSegment.Field(nestedField), FieldPathSegment.Field(statField) }
                : new[] { FieldPathSegment.Field(statField) };
            return block.TryGetInt(path, out int v) ? v : 0;
        }

        /// <summary>Pushes <see cref="_trainer"/> into the bound fields + party mons. Caller guards with _suppress.</summary>
        private void PopulateFromTrainer()
        {
            TrainerProperties trp = _trainer.Trp;
            TrainerName = _trainer.Name;
            TrainerClassIndex = trp.TrainerClass;
            SetBattleType(trp.BattleType);
            ChooseMoves = trp.ChooseMoves;
            ChooseItems = trp.ChooseItems;
            // Lower the minimum before a 0 arrives and raise it after it leaves, so the spinner never clamps it.
            if (trp.PartyCount == 0) SetEmptyPartyAsRead(true);
            PartyCount = trp.PartyCount;
            if (trp.PartyCount > 0) SetEmptyPartyAsRead(false);

            for (int i = 0; i < TrainerItems.Count && i < trp.TrainerItems.Length; i++)
                TrainerItems[i].Load(trp.TrainerItems[i]);

            for (int i = 0; i < AiFlags.Count; i++)
                AiFlags[i].Checked = trp.AI != null && i < trp.AI.Count && trp.AI[i];

            for (int i = 0; i < Party.Count; i++)
                LoadMon(Party[i], _trainer.Party[i]);

            ApplyMovesEnabled();
            ApplyItemsEnabled();
            ApplyPartyVisibility();
        }

        /// <summary>Pushes the live VM fields into <see cref="_trainer"/> (shared by Save and snapshotting).</summary>
        private void SyncToTrainer()
        {
            if (_trainer == null) return;
            TrainerProperties trp = _trainer.Trp;
            trp.PartyCount = (byte)_partyCount;
            trp.ChooseMoves = _chooseMoves;
            trp.ChooseItems = _chooseItems;
            trp.BattleType = _battleType;
            trp.TrainerClass = (byte)Math.Max(0, _trainerClassIndex);

            for (int i = 0; i < trp.TrainerItems.Length && i < TrainerItems.Count; i++)
                trp.TrainerItems[i] = (ushort)Math.Max(0, TrainerItems[i].ItemIndex);

            for (int i = 0; i < AiFlags.Count && trp.AI != null && i < trp.AI.Count; i++)
                trp.AI[i] = AiFlags[i].Checked;

            for (int i = 0; i < TrainerFile.PokeInParty; i++)
                _trainer.Party[i].Moves = _chooseMoves ? new ushort[4] : null;

            // Slots past the party count are synced too, so undo keeps them; the party file stops at the count.
            for (int i = 0; i < Party.Count && i < TrainerFile.PokeInParty; i++)
            {
                TrainerPartyMonViewModel mon = Party[i];
                PartyPokemon p = _trainer.Party[i];
                p.PokeID = (ushort)Math.Max(0, mon.SpeciesIndex);
                p.FormID = (ushort)mon.FormId;
                p.Level = (ushort)mon.Level;

                if (_chooseMoves)
                    p.Moves = new ushort[] { (ushort)Math.Max(0, mon.Move1), (ushort)Math.Max(0, mon.Move2), (ushort)Math.Max(0, mon.Move3), (ushort)Math.Max(0, mon.Move4) };

                // The party file only has room for an item when the trainer says it does.
                p.HeldItem = _chooseItems ? (ushort)Math.Max(0, mon.ItemIndex) : null;

                p.Difficulty = (byte)mon.Difficulty;

                // In every other game this byte is the high half of the u16 difficulty, so it is kept as read.
                if (_genderEditable)
                {
                    // Bits the editor has no field for stay as they were read.
                    const PartyPokemon.GenderAndAbilityFlags Edited = PartyPokemon.GenderAndAbilityFlags.FORCE_MALE | PartyPokemon.GenderAndAbilityFlags.FORCE_FEMALE
                        | PartyPokemon.GenderAndAbilityFlags.ABILITY_SLOT1 | PartyPokemon.GenderAndAbilityFlags.ABILITY_SLOT2;
                    PartyPokemon.GenderAndAbilityFlags flags = p.GenderAndAbility & ~Edited;
                    if (mon.GenderIndex == 1) flags |= PartyPokemon.GenderAndAbilityFlags.FORCE_MALE;
                    else if (mon.GenderIndex == 2) flags |= PartyPokemon.GenderAndAbilityFlags.FORCE_FEMALE;
                    if (mon.AbilityIndex == 1) flags |= PartyPokemon.GenderAndAbilityFlags.ABILITY_SLOT1;
                    else if (mon.AbilityIndex == 2) flags |= PartyPokemon.GenderAndAbilityFlags.ABILITY_SLOT2;
                    p.GenderAndAbility = flags;
                    if (_shinyPatch) p.ForceShiny = mon.ForceShiny;
                }

                p.BallSeals = (ushort)mon.BallSeals;
            }
        }

        private void LoadMon(TrainerPartyMonViewModel mon, PartyPokemon p)
        {
            int gender = 0;
            if (p.GenderAndAbility.HasFlag(PartyPokemon.GenderAndAbilityFlags.FORCE_MALE)) gender = 1;
            else if (p.GenderAndAbility.HasFlag(PartyPokemon.GenderAndAbilityFlags.FORCE_FEMALE)) gender = 2;

            int ability = 0;
            if (p.GenderAndAbility.HasFlag(PartyPokemon.GenderAndAbilityFlags.ABILITY_SLOT1)) ability = 1;
            else if (p.GenderAndAbility.HasFlag(PartyPokemon.GenderAndAbilityFlags.ABILITY_SLOT2)) ability = 2;

            int[] moves = null;
            if (p.Moves != null)
            {
                moves = new int[4];
                for (int j = 0; j < 4 && j < p.Moves.Length; j++) moves[j] = p.Moves[j];
            }

            mon.Load(p.PokeID ?? 0, p.FormID, Math.Max(1, (int)p.Level), moves, p.HeldItem ?? 0, gender, ability, p.Difficulty, p.BallSeals,
                     _genderEditable && p.ForceShiny);
            mon.ShinyVisible = _genderEditable && GameFamily == GameFamilies.HGSS && !HgEngineProject.IsActive;
            mon.ShinyEnabled = _shinyPatch;
        }

        private void ApplyMovesEnabled() { foreach (TrainerPartyMonViewModel m in Party) m.MovesEnabled = _chooseMoves; }
        private void ApplyItemsEnabled() { foreach (TrainerPartyMonViewModel m in Party) m.ItemEnabled = _chooseItems; }
        private void ApplyPartyVisibility()
        {
            int count = (int)_partyCount;
            for (int i = 0; i < Party.Count; i++) Party[i].IsVisible = i < count;
        }

        // ── Sprite ──────────────────────────────────────────────────────────────────
        private IDisposable _spriteSaved;

        private void UpdateTrainerSprite()
        {
            StopPlaying();
            if (_trainerClassIndex < 0) { ClassImage = null; return; }
            int maxFrame = _sprite.Load(_trainerClassIndex);
            ClassFrameMax = maxFrame;
            OnPropertyChanged(nameof(HasClassSprite));
            OnPropertyChanged(nameof(CanPlay));
            _classFrame = _sprite.DefaultFrame;
            OnPropertyChanged(nameof(ClassFrame));
            RenderSprite();
        }
        private void RenderSprite() => ClassImage = _sprite.Render((int)_classFrame, 96, 96);

        // Plays the class sprite's frames at their own lengths, as the Classes tab does.
        private global::Avalonia.Threading.DispatcherTimer _animTimer;
        private int _playCountdown;
        private bool _isPlaying;
        public bool IsPlaying { get => _isPlaying; private set { if (Set(ref _isPlaying, value)) OnPropertyChanged(nameof(PlayIcon)); } }
        public string PlayIcon => _isPlaying ? "stop" : "play";
        public bool CanPlay => _classFrameMax > 0;

        public void TogglePlay()
        {
            if (_isPlaying) { StopPlaying(); return; }
            if (!CanPlay) return;
            _animTimer ??= new global::Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000.0 / 60) };
            _animTimer.Tick -= AnimTick;
            _animTimer.Tick += AnimTick;
            _classFrame = 0; OnPropertyChanged(nameof(ClassFrame)); RenderSprite();
            _playCountdown = _sprite.GetFrameDuration(0);
            IsPlaying = true;
            _animTimer.Start();
        }

        private void StopPlaying()
        {
            _animTimer?.Stop();
            if (!_isPlaying) return;
            IsPlaying = false;
            _classFrame = _sprite.DefaultFrame; OnPropertyChanged(nameof(ClassFrame)); RenderSprite();
        }

        private void AnimTick(object sender, EventArgs e)
        {
            if (--_playCountdown > 0) return;
            _classFrame = ((int)_classFrame + 1) % Math.Max(1, (int)_classFrameMax + 1);
            OnPropertyChanged(nameof(ClassFrame));
            RenderSprite();
            _playCountdown = _sprite.GetFrameDuration((int)_classFrame);
        }

        // ── Save ──────────────────────────────────────────────────────────────────────
        public void Save()
        {
            if (_selectedTrainerIndex < 0) return;

            if (IsHgeActive)
            {
                _ = SaveHgEngineAsync();
                return;
            }

            if (_trainer == null) return;

            // The game reads every record up to the party count, so a blank one would battle as species 0.
            for (int i = 0; i < (int)_partyCount && i < Party.Count; i++)
            {
                if (Party[i].SpeciesIndex > 0) continue;
                string error = $"Party slot {i + 1} has no Pokémon.";
                StatusText = $"Trainer {_selectedTrainerIndex} was not saved: {error}";
                _ = DialogHelper.ShowErrorAsync($"Trainer {_selectedTrainerIndex} was not saved:\n{error}", "Trainer editor error");
                return;
            }

            SyncToTrainer();
            bool listChanged = _savedName != _trainerName || _savedClass != _trainer.Trp.TrainerClass;

            TrainerRecords.Save(_selectedTrainerIndex, _trainer.Trp, _trainer.Party);

            UpdateTrainerName(_trainerName);

            _savedName = _trainerName;
            _savedClass = _trainer.Trp.TrainerClass;
            if (listChanged)
            {
                RefreshTrainerListEntry(_selectedTrainerIndex);
                AppEvents.RaiseNamesChanged();
            }

            SetClean();
            SaveNotice.Saved(UnsavedChangesDescription);
            _history.MarkSaved();
            RaiseUndoState();
            StatusText = $"Trainer {_selectedTrainerIndex} saved.";
        }

        async Task<bool> IEditorWithUnsavedChanges.SaveChangesAsync()
        {
            if (_dirty)
            {
                if (IsHgeActive) await SaveHgEngineAsync();
                else Save();
            }
            if (_classes?.HasUnsavedChanges == true) _classes.Save();
            return !HasUnsavedChanges;
        }

        private async Task SaveHgEngineAsync()
        {
            int trainer = _selectedTrainerIndex;
            if (trainer < 0) return;
            (bool saved, string error) = await HgEngineSave.RunAsync(WriteHgEngineSource);
            if (!saved)
            {
                if (error == null) return;
                StatusText = $"Trainer {trainer} was not saved: {error}";
                await DialogHelper.ShowErrorAsync($"Trainer {trainer} was not saved:\n{error}", "Trainer editor error");
                return;
            }

            RefreshTrainerListEntry(trainer);
            AppEvents.RaiseNamesChanged();   // other editors list trainer names from Trainers.c too
            if (trainer != _selectedTrainerIndex) return;
            SetClean();
            SaveNotice.Saved(UnsavedChangesDescription);
            _history.MarkSaved();
            RaiseUndoState();
            StatusText = $"Trainer {trainer} saved to hg-engine source.";
        }

        // partySize/partyMonCount/textCount are never written (trainerdatagen derives them).
        /// <summary>Writes the loaded trainer into Trainers.c. Returns why it could not, or null.</summary>
        private string WriteHgEngineSource()
        {
            if (!HgEngineProject.IsActive) return "No hg-engine checkout is linked.";

            // trainerdatagen stops the build at a gap in the party.
            for (int i = 0; i < (int)_partyCount && i < Party.Count; i++)
                if (Party[i].SpeciesIndex == 0) return $"Party slot {i + 1} has no Pokémon.";

            if (HgeAbilityFlagChecked) FillHgeDefaults("TRAINER_DATA_TYPE_ABILITY");
            if (HgeBallFlagChecked) FillHgeDefaults("TRAINER_DATA_TYPE_BALL");
            if (HgeMovesFlagChecked)
            {
                FillLevelUpMoves();
                // The game sets exactly these moves, so four empty ones leave the Pokémon with none.
                for (int i = 0; i < (int)_partyCount && i < Party.Count; i++)
                    if (Party[i].Move1 <= 0 && Party[i].Move2 <= 0 && Party[i].Move3 <= 0 && Party[i].Move4 <= 0)
                        return $"Party slot {i + 1} has no moves.";
            }

            string ClassSymbol(int classIndex) =>
                HgEngineSymbolTable.Load(TrainerClassHeader)?.TryGetNameWithPrefix(classIndex, "TRAINERCLASS_", out string n) == true ? n : classIndex.ToString();
            string SpeciesSymbol(int speciesId) =>
                HgEngineSymbolTable.Load(SpeciesHeader)?.TryGetNameWithPrefix(speciesId, "SPECIES_", out string n) == true ? n : speciesId.ToString();
            // -1 as a u16 is 0xFFFF, which the game takes as a real id.
            string ItemSymbol(int itemId) => itemId <= 0 ? "ITEM_NONE" :
                HgEngineSymbolTable.Load(ItemHeader)?.TryGetNameWithPrefix(itemId, "ITEM_", out string n) == true ? n : itemId.ToString();
            string AbilitySymbol(int abilityId) => abilityId <= 0 ? "ABILITY_NONE" :
                HgEngineSymbolTable.Load(AbilityHeader)?.TryGetNameWithPrefix(abilityId, "ABILITY_", out string n) == true ? n : abilityId.ToString();
            string MoveSymbol(int moveId) => moveId <= 0 ? "MOVE_NONE" :
                HgEngineSymbolTable.Load(MovesHeader)?.TryGetNameWithPrefix(moveId, "MOVE_", out string n) == true ? n : moveId.ToString();
            string NatureSymbol(int nature) =>
                HgEngineSymbolTable.Load(PokemonConstantsHeader)?.TryGetNameWithPrefix(nature, "NATURE_", out string n) == true ? n : nature.ToString();
            string FlagsExpr(string header, string prefix, int value) =>
                HgEngineSymbolTable.Load(header)?.TryGetFlagsExpression(value, prefix, out string expr) == true ? expr : value.ToString();

            int trainerTypeValue = TrainerDataTypeFlags.Where(f => f.Checked).Aggregate(0, (acc, f) => acc | f.Value);
            int aiFlagsValue = AiFlags.Where(f => f.Checked).Aggregate(0, (acc, f) => acc | f.Value);
            string battleTypeLiteral = BattleTypeIndex >= 0 && BattleTypeIndex < BattleTypeOptions.Count
                ? BattleTypeOptions[BattleTypeIndex] : "SINGLE_BATTLE";

            List<HgEngineFieldWrite> fields = new List<HgEngineFieldWrite>
            {
                new(new[] { FieldPathSegment.Field("name") }, HgEngineTrainerSource.ToCStringLiteral(_trainerName ?? "")),
            };
            if (_hgeAiFlagsKnown)
                fields.Add(new(new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("aiFlags") }, FlagsExpr(TrainerDataHeader, "F_", aiFlagsValue)));
            if (_hgeTrainerTypeKnown)
                fields.Add(new(new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("trainerType") }, FlagsExpr(TrainerDataHeader, "TRAINER_DATA_TYPE_", trainerTypeValue)));
            if (BattleTypeIndex >= 0 && BattleTypeIndex < BattleTypeOptions.Count)
                fields.Add(new(new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("battleType") }, battleTypeLiteral));

            if (_trainerClassIndex >= 0)
                fields.Add(new(new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("trainerClass") }, ClassSymbol(_trainerClassIndex)));

            fields.Add(new(new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("items") },
                "{ " + string.Join(", ", TrainerItems.Select((t, i) => t.ItemIndex >= 0 ? ItemSymbol(t.ItemIndex) : _hgeItemRaw[i] ?? "ITEM_NONE")) + " }"));

            IReadOnlyList<HgEngineTrainerFieldSchema.NamedFlag> abilitySlots = HgEngineTrainerFieldSchema.GetAbilitySlots();

            for (int i = 0; i < (int)_partyCount && i < Party.Count; i++)
            {
                TrainerPartyMonViewModel mon = Party[i];
                TrainerPartyMonHgeExtras extras = mon.HgeExtras;
                FieldPathSegment p = FieldPathSegment.Field("party");
                FieldPathSegment[] Path(params FieldPathSegment[] rest)
                {
                    FieldPathSegment[] full = new FieldPathSegment[rest.Length + 2];
                    full[0] = p; full[1] = FieldPathSegment.At(i);
                    System.Array.Copy(rest, 0, full, 2, rest.Length);
                    return full;
                }

                if (mon.SpeciesIndex >= 0)
                    fields.Add(new(Path(FieldPathSegment.Field("species")), HgEngineTrainerSource.FormatSpecies(SpeciesSymbol(mon.SpeciesIndex), Math.Min(31, (int)mon.FormId))));
                fields.Add(new(Path(FieldPathSegment.Field("level")), mon.Level.ToString()));
                fields.Add(new(Path(FieldPathSegment.Field("ivs")), mon.Difficulty.ToString()));
                fields.Add(new(Path(FieldPathSegment.Field("ballSeal")), mon.BallSeals.ToString()));

                string slotName = mon.AbilityIndex >= 0 && mon.AbilityIndex < TrainerPartyMonViewModel.HgeAbilitySlotNames.Length
                    ? TrainerPartyMonViewModel.HgeAbilitySlotNames[mon.AbilityIndex] : null;
                if (slotName != null && abilitySlots.Any(s => s.Name == slotName))
                    fields.Add(new(Path(FieldPathSegment.Field("abilitySlot")), slotName));

                // A field whose flag is off is removed, so values left from a slot's previous Pokémon
                // don't come back when the flag is turned on again. Unreadable flags leave every field alone.
                void Put(bool on, string field, Func<string> literal)
                {
                    if (!_hgeTrainerTypeKnown) return;
                    fields.Add(on ? new(Path(FieldPathSegment.Field(field)), literal()) : HgEngineFieldWrite.Remove(Path(FieldPathSegment.Field(field))));
                }

                Put(HgeItemsFlagChecked, "item", () => ItemSymbol(mon.ItemIndex));
                Put(HgeMovesFlagChecked, "moves", () => "{ " + string.Join(", ", MoveSymbol(mon.Move1), MoveSymbol(mon.Move2), MoveSymbol(mon.Move3), MoveSymbol(mon.Move4)) + " }");
                Put(HgeAbilityFlagChecked, "ability", () => AbilitySymbol(extras.AbilityId));
                Put(HgeBallFlagChecked, "ball", () => ItemSymbol(extras.BallId));

                string StatBlock(StatBlockViewModel s) =>
                    $"{{ .hp = {s.Hp}, .attack = {s.Attack}, .defense = {s.Defense}, .speed = {s.Speed}, .spAttack = {s.SpAttack}, .spDefense = {s.SpDefense} }}";
                Put(HgeIvEvFlagChecked, "setIvs", () => StatBlock(extras.SetIvs));
                Put(HgeIvEvFlagChecked, "setEvs", () => StatBlock(extras.SetEvs));
                Put(HgeNatureFlagChecked, "nature", () => NatureSymbol(extras.NatureIndex));
                Put(HgeShinyLockFlagChecked, "shinyLock", () => extras.ShinyLocked ? "1" : "0");

                bool extra = HgeAdditionalFlagsFlagChecked;
                Put(extra && extras.ExtraStatusEnabled, "status", () => extras.ExtraStatus.ToString());
                Put(extra && extras.ExtraHpEnabled, "hp", () => extras.ExtraHp.ToString());
                Put(extra && extras.ExtraAttackEnabled, "attack", () => extras.ExtraAttack.ToString());
                Put(extra && extras.ExtraDefenseEnabled, "defense", () => extras.ExtraDefense.ToString());
                Put(extra && extras.ExtraSpeedEnabled, "speed", () => extras.ExtraSpeed.ToString());
                Put(extra && extras.ExtraSpAtkEnabled, "spAttack", () => extras.ExtraSpAtk.ToString());
                Put(extra && extras.ExtraSpDefEnabled, "spDefense", () => extras.ExtraSpDef.ToString());
                Put(extra && extras.ExtraPpCountsEnabled, "ppCounts", () => "{ " + string.Join(", ", extras.ExtraPp1, extras.ExtraPp2, extras.ExtraPp3, extras.ExtraPp4) + " }");
                Put(extra && extras.ExtraNicknameEnabled, "nicknameStr", () => HgEngineTrainerSource.ToCStringLiteral(extras.ExtraNickname ?? ""));

                if (_hgeTrainerTypeKnown && !extra)
                    fields.Add(HgEngineFieldWrite.Remove(Path(FieldPathSegment.Field("additionalFlags"))));
                else if (_hgeTrainerTypeKnown)
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
                }
            }

            FieldPathSegment[] partyPath = new[] { FieldPathSegment.Field("party") };
            List<HgEngineArrayCount> arrayCounts = new List<HgEngineArrayCount>();
            if ((int)_partyCount == 0) fields.Add(HgEngineFieldWrite.Remove(partyPath));
            else arrayCounts.Add(new HgEngineArrayCount(partyPath, (int)_partyCount, NewPartyEntry));
            if (!HgEngineWriter.TryWriteFields(HgEngineDomain.Trainers, _selectedTrainerIndex, fields, out List<string> unresolved, out string error,
                    allowInsert: true, arrayCounts: arrayCounts, allOrNothing: true))
            {
                AppLogger.Error($"hg-engine write failed for trainer {_selectedTrainerIndex}: {error}");
                return error;
            }
            if (unresolved.Count > 0)
            {
                string missing = string.Join(", ", unresolved);
                AppLogger.Error($"hg-engine write for trainer {_selectedTrainerIndex} could not place {missing}");
                return $"Trainers.c has no place for {missing}.";
            }
            return null;

            static string NewPartyEntry(int index, string indent) =>
                "{\n" +
                indent + "    .ivs = 0,\n" +
                indent + "    .abilitySlot = TRAINER_POKEMON_ABILITY_1,\n" +
                indent + "    .level = 1,\n" +
                indent + "    .species = SPECIES_NONE,\n" +
                indent + "    .ballSeal = 0,\n" +
                indent + "}";

            static int ExtraBit(string suffix)
            {
                foreach (HgEngineTrainerFieldSchema.NamedFlag f in HgEngineTrainerFieldSchema.GetExtraFlags()) if (f.Name.EndsWith(suffix)) return f.Bit;
                return 0;
            }
        }

        // ── Add / Export / Import ─────────────────────────────────────────────────────
        /// <summary>Atomically appends a trainer and expands every verified dependent resource.</summary>
        // The add and remove report the roster's room; the trainer they select loads afterwards and would replace it.
        private string _statusAfterLoad;

        public void AddTrainer()
        {
            // Adding a brand-new trainer entry to Trainers.c isn't built yet. Writing a blank trainer
            // straight to the local a055/a056 binaries here would be silently discarded on the next
            // hg-engine source sync and never reach a real build, so this is refused rather than doing
            // something that looks like it worked but doesn't.
            if (IsHgeActive)
            {
                _ = DialogHelper.ShowErrorAsync("Adding new trainers isn't supported yet for hg-engine source-backed ROMs.", "Trainer editor error");
                return;
            }
            if (HasUnsavedChanges)
            {
                _ = DialogHelper.ShowErrorAsync("Save or discard the current trainer's changes before adding another trainer.", "Trainer editor error");
                return;
            }
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.Scripts });
                if (!TrainerRosterService.TryAddTrainer("Trainer", out int newIndex,
                    out string error))
                {
                    StatusText = error;
                    _ = DialogHelper.ShowErrorAsync(error, "Add trainer");
                    return;
                }

                AppEvents.RaiseNamesChanged();
                TrainerRosterAnalysis after = TrainerRosterService.AnalyzeCurrentProject();
                StatusText = _statusAfterLoad = after.CanAdd
                    ? $"Added trainer {newIndex}. {after.RemainingAdditions} more can be added safely."
                    : $"Added trainer {newIndex}. {after.RefusalReason}";
                SelectedTrainerIndex = newIndex;
            }
            catch (Exception ex) { _ = DialogHelper.ShowErrorAsync($"Couldn't add trainer:\n{ex.Message}", "Trainer editor error"); }
        }

        /// <summary>Removes only the final appended trainer after every known reference source is clear.</summary>
        public async Task RemoveLastAddedTrainerAsync()
        {
            if (HgEngineProject.IsActive)
            {
                await DialogHelper.ShowErrorAsync(
                    "Removing trainers isn't supported for hg-engine source-backed ROMs.",
                    "Trainer editor error");
                return;
            }
            if (HasUnsavedChanges)
            {
                await DialogHelper.ShowErrorAsync(
                    "Save or discard the current trainer's changes before removing a trainer.",
                    "Trainer editor error");
                return;
            }
            if (TrainerNames.Count == 0) return;

            int candidate = TrainerNames.Count - 1;
            bool confirmed = await DialogHelper.AskYesNoAsync(
                $"Remove trainer {candidate}? Only the final trainer can be removed. " +
                "DSPRE will cancel if an event, script, rematch table, battle message, or phonebook entry still uses it.",
                "Delete trainer?");
            if (!confirmed) return;

            StatusText = $"Checking references to trainer {candidate}…";
            PushLoading();
            try
            {
                bool removed = false;
                int removedId = -1;
                string error = null;
                await Task.Run(() =>
                {
                    DSUtils.TryUnpackNarcs(new List<DirNames>
                    {
                        DirNames.Scripts, DirNames.EventFiles, DirNames.TrainerTextTable,
                        DirNames.TrainerTextOffset
                    });
                    removed = TrainerRosterService.TryRemoveLastAddedTrainer(out removedId,
                        out error);
                });

                if (!removed)
                {
                    StatusText = error;
                    await DialogHelper.ShowErrorAsync(error, "Trainer editor error");
                    return;
                }

                AppEvents.RaiseNamesChanged();
                TrainerRosterAnalysis after = TrainerRosterService.AnalyzeCurrentProject();
                StatusText = _statusAfterLoad = $"Removed trainer {removedId}. " +
                    $"{after.RemainingAdditions} trainer slots are available.";
                SelectedTrainerIndex = Math.Max(0, removedId - 1);
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowErrorAsync($"Couldn't remove trainer:\n{ex.Message}",
                    "Trainer editor error");
            }
            finally { PopLoading(); }
        }

        public async Task ExportTrainerAsync()
        {
            if (_trainer == null) return;
            SyncToTrainer();
            FilePickerFileType filter = new FilePickerFileType("Gen IV Trainer File") { Patterns = new[] { "*.trf" } };
            string path = await DialogHelper.SaveFileAsync(_owner, "Export trainer", new[] { filter }, $"trainer_{_selectedTrainerIndex:D4}.trf");
            if (path == null) return;
            try
            {
                PlainFiles.WriteBytes(path, _trainer.ToByteArray());
                SaveNotice.Exported(path);
            }
            catch (Exception ex) { await DialogHelper.ShowErrorAsync($"Export failed:\n{ex.Message}", "Trainer editor error"); }
        }

        /// <summary>Loads a combined .trf (name + properties + party) into the CURRENTLY SELECTED
        /// trainer slot's in-memory state. Like Replace Properties/Import Party below, this stages the
        /// change (marks dirty) rather than writing to disk immediately, so Undo and the Save button work.</summary>
        public async Task ImportTrainerAsync()
        {
            if (_trainer == null || _selectedTrainerIndex < 0) return;
            FilePickerFileType filter = new FilePickerFileType("Gen IV Trainer File") { Patterns = new[] { "*.trf" } };
            string path = await DialogHelper.OpenFileAsync(_owner, "Import trainer", new[] { filter });
            if (path == null) return;
            try
            {
                using BinaryReader reader = new BinaryReader(File.OpenRead(path));
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
                SaveNotice.Imported($"trainer {_selectedTrainerIndex}", path);
            }
            catch (Exception ex) { await DialogHelper.ShowErrorAsync($"Import failed:\n{ex.Message}", "Trainer editor error"); }
        }

        public async Task ExportPropertiesAsync()
        {
            if (_trainer == null) return;
            SyncToTrainer();
            FilePickerFileType filter = new FilePickerFileType("Gen IV Trainer Properties") { Patterns = new[] { "*.trp" } };
            string path = await DialogHelper.SaveFileAsync(_owner, "Export trainer properties", new[] { filter }, $"trainer_{_selectedTrainerIndex:D4}.trp");
            if (path == null) return;
            try
            {
                PlainFiles.WriteBytes(path, _trainer.Trp.ToByteArray());
                SaveNotice.Exported(path);
            }
            catch (Exception ex) { await DialogHelper.ShowErrorAsync($"Export failed:\n{ex.Message}", "Trainer editor error"); }
        }

        public async Task ReplacePropertiesAsync()
        {
            if (_trainer == null || _selectedTrainerIndex < 0) return;
            FilePickerFileType filter = new FilePickerFileType("Gen IV Trainer Properties") { Patterns = new[] { "*.trp" } };
            string path = await DialogHelper.OpenFileAsync(_owner, "Import trainer properties", new[] { filter });
            if (path == null) return;
            try
            {
                using FileStream fs = File.OpenRead(path);
                _trainer.Trp = new TrainerProperties((ushort)_selectedTrainerIndex, fs);
                _suppress = true;
                try { PopulateFromTrainer(); } finally { _suppress = false; }
                SetDirty();
                SaveNotice.Imported($"the properties of trainer {_selectedTrainerIndex}", path);
            }
            catch (Exception ex) { await DialogHelper.ShowErrorAsync($"Import failed:\n{ex.Message}", "Trainer editor error"); }
        }

        public async Task ExportPartyAsync()
        {
            if (_trainer == null) return;
            SyncToTrainer();
            FilePickerFileType filter = new FilePickerFileType("Gen IV Party Data") { Patterns = new[] { "*.pdat" } };
            string path = await DialogHelper.SaveFileAsync(_owner, "Export trainer party", new[] { filter }, $"party_{_selectedTrainerIndex:D4}.pdat");
            if (path == null) return;
            try
            {
                _trainer.Party.ExportCondensedData = true;
                PlainFiles.WriteBytes(path, _trainer.Party.ToByteArray());
                _trainer.Party.ExportCondensedData = false;
                SaveNotice.Exported(path);
            }
            catch (Exception ex) { await DialogHelper.ShowErrorAsync($"Export failed:\n{ex.Message}", "Trainer editor error"); }
        }

        public async Task ImportPartyAsync()
        {
            if (_trainer == null) return;
            FilePickerFileType filter = new FilePickerFileType("Gen IV Party Data") { Patterns = new[] { "*.pdat" } };
            string path = await DialogHelper.OpenFileAsync(_owner, "Import trainer party", new[] { filter });
            if (path == null) return;
            try
            {
                using FileStream fs = File.OpenRead(path);
                _trainer.Party = new Party(readFirstByte: true, TrainerFile.PokeInParty, fs, _trainer.Trp);
                _suppress = true;
                try { PopulateFromTrainer(); } finally { _suppress = false; }
                SetDirty();
                SaveNotice.Imported($"the party of trainer {_selectedTrainerIndex}", path);
            }
            catch (Exception ex) { await DialogHelper.ShowErrorAsync($"Import failed:\n{ex.Message}", "Trainer editor error"); }
        }

        // ── Party order ───────────────────────────────────────────────────────────────
        public void ReorderParty(List<int> newOrder)
        {
            int count = newOrder.Count;
            if (count == 0) return;
            List<TrainerPartyMonViewModel.State> snap = new List<TrainerPartyMonViewModel.State>();
            for (int i = 0; i < count && i < Party.Count; i++) snap.Add(Party[i].Capture());
            for (int i = 0; i < count && i < Party.Count; i++)
            {
                int from = newOrder[i];
                if (from < 0 || from >= snap.Count) continue;
                Party[i].Restore(snap[from]);
            }
            SetDirty();
        }

        public void GoToTrainer(int index)
        {
            if (index >= 0 && index < TrainerNames.Count) SelectedTrainerIndex = index;
        }

        // ── Personality inputs ────────────────────────────────────────────────────────
        public (ushort trainerId, byte trainerClass, List<(int pokeId, int level, int gender, int ability, int dv)> party) GetDVCalcInput()
        {
            List<(int, int, int, int, int)> list = new List<(int, int, int, int, int)>();
            int count = (int)_partyCount;
            for (int i = 0; i < count && i < Party.Count; i++)
            {
                TrainerPartyMonViewModel m = Party[i];
                // The calculator speaks vanilla flags. hg-engine passes abilitySlot to the same personality
                // routine, where its hidden value 0x02 is the force-female bit.
                int ability = m.AbilityIndex, gender = m.GenderIndex;
                if (IsHgeActive)
                {
                    ability = m.AbilityIndex == 1 ? 2 : 0;
                    gender = m.AbilityIndex == 2 ? 2 : 0;
                }
                list.Add((m.SpeciesIndex, (int)m.Level, gender, ability, (int)m.Difficulty));
            }
            ushort tid = _trainer?.Trp.TrainerID ?? (ushort)Math.Max(0, _selectedTrainerIndex);
            return (tid, (byte)Math.Max(0, _trainerClassIndex), list);
        }

        // ── Defaults for newly enabled fields ─────────────────────────────────────────
        // Ticking custom moves otherwise saves four empty moves, which the game gives the Pokémon.
        private void FillLevelUpMoves()
        {
            if (!IsHgeActive)
            {
                try { DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.Learnsets }); }
                catch (Exception ex) { AppLogger.Error("Learnsets could not be unpacked: " + ex.Message); return; }
            }
            for (int i = 0; i < (int)_partyCount && i < Party.Count; i++)
            {
                TrainerPartyMonViewModel mon = Party[i];
                // Vanilla learnset files are per species only, so a vanilla form is left for the user to fill.
                if (mon.SpeciesIndex <= 0 || (mon.FormId > 0 && !IsHgeActive) || mon.Move1 > 0 || mon.Move2 > 0 || mon.Move3 > 0 || mon.Move4 > 0) continue;
                try
                {
                    ushort[] moves = LevelUpMoves(mon);
                    if (moves == null) continue;
                    mon.Move1 = moves[0]; mon.Move2 = moves[1]; mon.Move3 = moves[2]; mon.Move4 = moves[3];
                }
                catch (Exception ex) { AppLogger.Error($"Level-up moves for species {mon.SpeciesIndex} could not be read: {ex.Message}"); }
            }
        }

        // The same pick as a learnset file, taken from learnsets.json rather than the last build.
        private static ushort[] HgeLevelUpMoves(int speciesId, int level)
        {
            if (!HgEngineLearnsets.TryGetLevelMoves(speciesId, out List<(int level, int move)> list, out string error))
            {
                AppLogger.Error($"Level-up moves for species {speciesId} could not be read: {error}");
                return null;
            }
            MemoryStream stream = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                foreach ((int moveLevel, int move) in list) writer.Write((uint)((moveLevel << 16) | move));
                writer.Write(0x0000FFFFu);
            }
            stream.Position = 0;
            return new LearnsetData(stream, wide: true).GetLearnsetAtLevel(level);
        }

        // An unset ability or ball would be written as 0xFFFF.
        private void FillHgeDefaults(string flagName)
        {
            if (flagName == "TRAINER_DATA_TYPE_MOVES") { FillLevelUpMoves(); return; }
            int pokeBall = HgEngineSymbolTable.Load(ItemHeader)?.TryGetValue("ITEM_POKE_BALL", out int ball) == true ? ball : 4;
            for (int i = 0; i < (int)_partyCount && i < Party.Count; i++)
            {
                TrainerPartyMonHgeExtras extras = Party[i].HgeExtras;
                if (flagName == "TRAINER_DATA_TYPE_ABILITY" && extras.AbilityId <= 0) extras.AbilityId = Party[i].SlotAbilityId();
                if (flagName == "TRAINER_DATA_TYPE_BALL" && extras.BallId <= 0) extras.BallId = pokeBall;
            }
        }

        // ── Trainer list ──────────────────────────────────────────────────────────────
        private static List<string> TrainerListEntries(string[] classNames) => new List<string>(DSPRE.TrainerNames.GetAll());

        private void RefreshTrainerListEntry(int id)
        {
            if (id < 0 || id >= TrainerNames.Count) return;
            string entry;
            if (IsHgeActive)
            {
                if (!HgEngineTrainerSource.TryLoad(id, out HgEngineSourceBlock block, out _)) return;
                entry = DSPRE.TrainerNames.HgEngineEntry(id, block, GetTrainerClassNames());
            }
            else
            {
                string[] classNames = GetTrainerClassNames();
                string className = _savedClass < classNames.Length ? classNames[_savedClass] : $"Class {_savedClass}";
                entry = "[" + id.ToString("D2") + "] " + className + " " + _savedName;
            }
            if (TrainerNames[id] == entry) return;
            // Replacing the selected row can clear the list's selection.
            bool wasSuppressed = _suppress;
            _suppress = true;
            try
            {
                TrainerNames[id] = entry;
                _selectedTrainerIndex = id;
                OnPropertyChanged(nameof(SelectedTrainerIndex));
            }
            finally { _suppress = wasSuppressed; }
        }

        private void UpdateTrainerName(string newName)
        {
            if (_trainer == null) return;
            try
            {
                _trainer.Name = newName;
                TextArchive ta = new TextArchive(TrainerNamesMessageNumber);
                ta.SetSimpleTrainerName(_trainer.Trp.TrainerID, newName);
                ta.SaveToExpandedDir(TrainerNamesMessageNumber, showSuccessMessage: false);
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
        /// <summary>What the flag makes the AI favour, or null for flags with no note.</summary>
        public string Tip { get; init; }
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
            set
            {
                // A ComboBox reports -1 while its list refills; only a load may clear the item.
                if (value < 0)
                {
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ItemIndex))));
                    return;
                }
                if (_itemIndex != value) { _itemIndex = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ItemIndex))); Changed?.Invoke(this, EventArgs.Empty); }
            }
        }

        /// <summary>Sets the item as read, where -1 means one with no row.</summary>
        public void Load(int index)
        {
            if (_itemIndex == index) return;
            _itemIndex = index;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ItemIndex)));
        }

        public TrainerItemSlotViewModel(ObservableCollection<string> itemNames) { ItemNames = itemNames; }
    }
}
