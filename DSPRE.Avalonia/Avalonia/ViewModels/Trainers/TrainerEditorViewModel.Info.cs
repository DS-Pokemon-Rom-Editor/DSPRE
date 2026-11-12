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
    public partial class TrainerEditorViewModel
    {
        /// <summary>One use of the trainer. Places lists what loads a script or event file.</summary>
        public sealed record TrainerUse(string Kind, string Location, string Places, int SourceId);
        public ObservableCollection<TrainerUse> Uses { get; } = new ObservableCollection<TrainerUse>();
        private string _usesStatus = "";
        public string UsesStatus { get => _usesStatus; private set => Set(ref _usesStatus, value); }

        public async Task FindUsesAsync()
        {
            int id = _loadedTrainerId;
            if (id < 0) return;
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

        private readonly Dictionary<int, int?> _prizeMul = new Dictionary<int, int?>();

        private int? PrizeMultiplier(int trainerClass)
        {
            if (trainerClass < 0) return null;
            if (!_prizeMul.TryGetValue(trainerClass, out int? mul))
                _prizeMul[trainerClass] = mul = TrainerPrize.TryGetMultiplier(trainerClass, out int m) ? m : null;
            return mul;
        }

        private TrainerPartyMonViewModel LastPartyMon => Party.Take((int)PartyCount).LastOrDefault();

        /// <summary>What beating this trainer pays, or null with no readable class multiplier.</summary>
        public string PrizeText => LastPartyMon is TrainerPartyMonViewModel last && PrizeMultiplier(_trainerClassIndex) is int mul
            ? TrainerPrize.Amount((int)last.Level, mul, DoubleBattle).ToString("N0") : null;
        public bool HasPrize => PrizeText != null;
        /// <summary>The Pokédollar sign from the ROM's font. The character map writes it as $.</summary>
        public Bitmap PrizeSign => DSPRE.Avalonia.Data.GameGlyph.Mask('$');
        public bool HasPrizeSign => PrizeSign != null;
        public string PrizeTip => PrizeMultiplier(_trainerClassIndex) is int mul
            ? $"Last Pokémon's level × 4 × the class multiplier ({mul}){(DoubleBattle ? " × 2 for a double battle" : "")}. Amulet Coin doubles it."
            : null;
        private void RaisePrize() { OnPropertyChanged(nameof(PrizeText)); OnPropertyChanged(nameof(HasPrize)); OnPropertyChanged(nameof(PrizeTip)); }

        private bool _derivedQueued;   // edits come in bursts, so the recount runs once after them
        private void QueueDerived()
        {
            if (_derivedQueued) return;
            _derivedQueued = true;
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => { _derivedQueued = false; RefreshDerived(); },
                global::Avalonia.Threading.DispatcherPriority.Background);
        }

        /// <summary>Every DV, IV and nature this slot can get, given the slots before it.</summary>
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
                if (!_learnsetsUnpacked) { DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.Learnsets }); _learnsetsUnpacked = true; }   // learnset files are missing until unpacked
                return new LearnsetData(mon.SpeciesIndex).GetLearnsetAtLevel((int)mon.Level);
            }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is IndexOutOfRangeException) { return null; }
        }

        public void MoveMon(TrainerPartyMonViewModel mon, int delta)
        {
            int from = Party.IndexOf(mon), to = from + delta, count = (int)_partyCount;
            if (from < 0 || to < 0 || to >= count) return;
            List<int> order = Enumerable.Range(0, count).ToList();
            (order[from], order[to]) = (order[to], order[from]);
            ReorderParty(order);
        }

        public ObservableCollection<TrainerItemSlotViewModel> TrainerItems { get; } = new ObservableCollection<TrainerItemSlotViewModel>();

        // hg-engine only: flags and battle types come from the checkout's include/trainer_data.h.
        public bool IsHgeActive => HgEngineProject.IsActive;
        public ObservableCollection<AiFlagViewModel> TrainerDataTypeFlags { get; } = new ObservableCollection<AiFlagViewModel>();
        public ObservableCollection<string> BattleTypeOptions { get; } = new ObservableCollection<string>();

        // The battle type is OR'd into the battle's flags. Vanilla uses these two; others are kept.
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

        // The ComboBox can drop its selection while its rows fill: blank it one frame, restore it later.
        private bool _battleTypeRepushPending;   // one at a time: a second would save the -1 and restore that
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

        private bool _hgeTrainerTypeKnown, _hgeAiFlagsKnown;   // false when Trainers.c held what the editor can't read; saving then leaves it alone
        private readonly string[] _hgeItemRaw = new string[TrainerProperties.TrainerItemCount];   // an unreadable item's token, written back as read

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
            "Basic", "Evaluate attack", "Expert", "Setup", "Risky",
            "Prioritize extremes", "Baton Pass", "Tag strategy", "Check HP", "Weather", "Harassment"
        };

        public const string AiNotesUrl = "https://gist.github.com/lhearachel/ff61af1f58c84c96592b0b8184dba096";   // each flag's intent, from Lhea's notes on the Gen 4 trainer AI
        private static readonly string[] AiFlagTips =
        {
            "Avoids moves that waste a turn or help the opponent right away.",
            "Puts raw damage ahead of everything else.",
            "Favours or avoids some move effects in certain situations.",
            "Favours setup moves on the battle's first turn, not each Pokémon's first turn.",
            "Favours risky attacking or status moves.",
            "Favours moves with variable power, fixed damage, high base power or no power.",
            "Favours setting up at high HP and passing stat boosts on to the party.",
            "Favours moves that help its partner and avoids moves that harm it.",
            "Avoids some move effects when the attacker or the target is at certain HP.",
            "Sets up weather on the first turn of battle if there is none.",
            "Favours moves that harass or disrupt the opponent.",
        };
        private static string AiFlagTip(int i) => i >= 0 && i < AiFlagTips.Length ? AiFlagTips[i] : null;

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        private int _loadingDepth;   // counted so a nested load can't clear the flag early
        public bool IsLoading => _loadingDepth > 0;
        private void PushLoading() { _loadingDepth++; OnPropertyChanged(nameof(IsLoading)); }
        private void PopLoading() { _loadingDepth = Math.Max(0, _loadingDepth - 1); OnPropertyChanged(nameof(IsLoading)); }

        /// <summary>Trainer id to select once the list loads. Set before SetupAsync.</summary>
        public int InitialIndex { get; set; }
    }
}
