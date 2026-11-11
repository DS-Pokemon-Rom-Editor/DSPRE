using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    public class TradeEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo, IDetachable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (Equals(f, v)) return false;
            f = v;
            OnPropertyChanged(n);
            return true;
        }

        // ----------------------------------------------------------------
        // IEditorWithUnsavedChanges
        // ----------------------------------------------------------------

        private bool _tradeDirty;
        private bool _textDirty;
        public bool HasUnsavedChanges => _tradeDirty || _textDirty;
        public string UnsavedChangesDescription => "In-game trades";
        public void SaveChanges()
        {
            if (!HasUnsavedChanges) return;
            if (_tradeDirty) SaveTradeCore();
            if (_textDirty) SaveTextCore();
            if (!HasUnsavedChanges) SaveNotice.Saved(UnsavedChangesDescription);
        }

        public void DiscardChanges()
        {
            if (!HasUnsavedChanges) return;
            if (_cur != null) LoadFromFile(_cur.Id);
            else { _tradeDirty = _textDirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        }

        // ── Undo / redo (ISupportsUndo) ────────────────────────────────────────
        // Trade edits live in the VM fields (only synced to _cur at save), and the nickname/OT names live in a
        // text archive, so the snapshot is composite: the synced trade-data bytes + the two text strings.
        private sealed class TradeSnapshot { public byte[] Data; public string OtName; public string Nickname; }
        private readonly UndoHistory<TradeSnapshot> _history = new();
        private System.DateTime _lastCaptureUtc = System.DateTime.MinValue;
        private const int CoalesceMs = 500;

        public bool CanUndo => _history.CanUndo;
        public bool CanRedo => _history.CanRedo;
        public void Undo() { if (_history.CanUndo) ApplyState(_history.Undo()); }
        public void Redo() { if (_history.CanRedo) ApplyState(_history.Redo()); }
        private void RaiseUndoState() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private TradeSnapshot Snapshot()
        {
            if (_cur == null) return null;
            SyncTradeFieldsToCur();   // pull the live VM fields into _cur so its bytes reflect the edits
            return new TradeSnapshot { Data = _cur.ToByteArray(), OtName = OtName, Nickname = Nickname };
        }

        private void ApplyState(TradeSnapshot snap)
        {
            if (snap == null || _cur == null) return;
            _loading = true;
            _cur = new TradeData(_cur.Id, new MemoryStream(snap.Data));
            PopulateFromCur();
            OtName = snap.OtName;
            Nickname = snap.Nickname;
            _loading = false;
            RefreshPersonality();

            _tradeDirty = _textDirty = _history.IsDirty;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            RaiseUndoState();
        }

        private void RecordUndoSnapshot()
        {
            if (_loading || _cur == null) return;
            bool coalesce = (System.DateTime.UtcNow - _lastCaptureUtc).TotalMilliseconds < CoalesceMs;
            _history.Capture(Snapshot(), coalesce);
            _lastCaptureUtc = System.DateTime.UtcNow;
            RaiseUndoState();
        }

        private void MarkSavedIfClean()
        {
            if (!_tradeDirty && !_textDirty) { _history.MarkSaved(); RaiseUndoState(); }
        }

        /// <summary>Copies the live trade VM fields into <see cref="_cur"/> (shared by Save and snapshotting).</summary>
        private void SyncTradeFieldsToCur()
        {
            if (_cur == null) return;
            _cur.Species          = SelectedSpecies;
            _cur.HpIV             = HpIV;
            _cur.AtkIV            = AtkIV;
            _cur.DefIV            = DefIV;
            _cur.SpeedIV          = SpeIV;
            _cur.SpAtkIV          = SpaIV;
            _cur.SpDefIV          = SpdIV;
            _cur.Ability          = SelectedAbility;
            _cur.OtID             = OtID;
            _cur.Cool             = Cool;
            _cur.Beauty           = Beauty;
            _cur.Cute             = Cute;
            _cur.Smart            = Smart;
            _cur.Tough            = Tough;
            _cur.Pid              = Pid;
            _cur.HeldItem         = SelectedHeldItem;
            _cur.OtGender         = SelectedOTGender;
            _cur.Sheen            = Sheen;
            _cur.Language         = SelectedLanguage;
            _cur.RequestedSpecies = SelectedRequested;
            _cur.Unknown          = IsHGSS ? Unknown : 0;
        }

        /// <summary>Pushes <see cref="_cur"/> into the bound trade fields. Caller guards with _loading.</summary>
        private void PopulateFromCur()
        {
            SelectedSpecies  = _cur.Species;
            HpIV  = _cur.HpIV;
            AtkIV = _cur.AtkIV;
            DefIV = _cur.DefIV;
            SpeIV = _cur.SpeedIV;
            SpaIV = _cur.SpAtkIV;
            SpdIV = _cur.SpDefIV;
            SelectedAbility  = _cur.Ability;
            OtID             = _cur.OtID;
            Cool   = _cur.Cool;
            Beauty = _cur.Beauty;
            Cute   = _cur.Cute;
            Smart  = _cur.Smart;
            Tough  = _cur.Tough;
            Pid    = _cur.Pid;
            SelectedHeldItem = _cur.HeldItem;
            SelectedOTGender = _cur.OtGender;
            Sheen            = _cur.Sheen;
            SelectedLanguage = _cur.Language;
            SelectedRequested = _cur.RequestedSpecies;
            Unknown          = _cur.Unknown;
        }

        // ----------------------------------------------------------------
        // Lists (ComboBox sources)
        // ----------------------------------------------------------------

        public ObservableCollection<string> PokemonNames  { get; } = new();
        public ObservableCollection<string> AbilityNames  { get; } = new();
        public ObservableCollection<string> ItemNames     { get; } = new();
        public ObservableCollection<string> OTGenderNames { get; } = new() { "Male", "Female" };
        public ObservableCollection<string> LanguageNames { get; } = new();

        // ----------------------------------------------------------------
        // Current trade state
        // ----------------------------------------------------------------

        private TradeData _cur;
        private TextArchive _tradeArchive;

        private string _title = "Trade editor";
        public string Title { get => _title; private set => Set(ref _title, value); }

        // ---- Trade ID spinner ----
        private int _tradeID;
        public int TradeID
        {
            get => _tradeID;
            set
            {
                if (!Set(ref _tradeID, value)) return;
                OnPropertyChanged(nameof(TradeIDMin));
                OnPropertyChanged(nameof(TradeIDMax));
            }
        }
        public int TradeIDMin => 0;
        public int TradeIDMax => TradeData.GetTradeCount() - 1;

        // ---- Species / requested ----
        private int _selectedSpecies;
        public int SelectedSpecies
        {
            get => _selectedSpecies;
            set { if (Set(ref _selectedSpecies, value)) { RefreshPersonality(); MarkTradeDirty(); } }
        }
        private int _selectedRequested;
        public int SelectedRequested
        {
            get => _selectedRequested;
            set { if (Set(ref _selectedRequested, value)) MarkTradeDirty(); }
        }

        // ---- Ability / held item / OT gender / language ----
        private int _selectedAbility;
        public int SelectedAbility
        {
            get => _selectedAbility;
            set { if (Set(ref _selectedAbility, value)) MarkTradeDirty(); }
        }
        private int _selectedHeldItem;
        public int SelectedHeldItem
        {
            get => _selectedHeldItem;
            set { if (Set(ref _selectedHeldItem, value)) MarkTradeDirty(); }
        }
        private int _selectedOTGender;
        public int SelectedOTGender
        {
            get => _selectedOTGender;
            set { if (Set(ref _selectedOTGender, value)) MarkTradeDirty(); }
        }
        private int _selectedLanguage;
        public int SelectedLanguage
        {
            get => _selectedLanguage;
            set { if (Set(ref _selectedLanguage, value)) MarkTradeDirty(); }
        }

        // ---- IVs ----
        private int _hpIV, _atkIV, _defIV, _speIV, _spaIV, _spdIV;
        public int HpIV  { get => _hpIV;  set { if (Set(ref _hpIV,  value)) MarkTradeDirty(); } }
        public int AtkIV { get => _atkIV; set { if (Set(ref _atkIV, value)) MarkTradeDirty(); } }
        public int DefIV { get => _defIV; set { if (Set(ref _defIV, value)) MarkTradeDirty(); } }
        public int SpeIV { get => _speIV; set { if (Set(ref _speIV, value)) MarkTradeDirty(); } }
        public int SpaIV { get => _spaIV; set { if (Set(ref _spaIV, value)) MarkTradeDirty(); } }
        public int SpdIV { get => _spdIV; set { if (Set(ref _spdIV, value)) MarkTradeDirty(); } }

        // ---- Contest stats ----
        private int _cool, _beauty, _cute, _smart, _tough, _sheen;
        public int Cool   { get => _cool;   set { if (Set(ref _cool,   value)) MarkTradeDirty(); } }
        public int Beauty { get => _beauty; set { if (Set(ref _beauty, value)) MarkTradeDirty(); } }
        public int Cute   { get => _cute;   set { if (Set(ref _cute,   value)) MarkTradeDirty(); } }
        public int Smart  { get => _smart;  set { if (Set(ref _smart,  value)) MarkTradeDirty(); } }
        public int Tough  { get => _tough;  set { if (Set(ref _tough,  value)) MarkTradeDirty(); } }
        public int Sheen  { get => _sheen;  set { if (Set(ref _sheen,  value)) MarkTradeDirty(); } }

        // ---- Numerics ----
        private int _otID, _pid, _unknown;
        public int OtID   { get => _otID;    set { if (Set(ref _otID,    value)) { RefreshPersonality(); MarkTradeDirty(); } } }
        public int Pid    { get => _pid;     set { if (Set(ref _pid,     value)) { RefreshPersonality(); MarkTradeDirty(); } } }
        public int Unknown { get => _unknown; set { if (Set(ref _unknown, value)) MarkTradeDirty(); } }

        // ---- Nature / ability / gender / shiny ----
        public IReadOnlyList<string> Natures => DVCalculator.Natures;
        public ObservableCollection<string> AbilityChoices { get; } = new();
        public ObservableCollection<string> GenderChoices  { get; } = new();

        private int _natureIndex, _abilityIndex, _genderIndex;
        public int NatureIndex
        {
            get => _natureIndex;
            set => ChoosePersonality(value, _abilityIndex, _genderIndex, _isShiny);
        }
        public int AbilityIndex
        {
            get => _abilityIndex;
            set => ChoosePersonality(_natureIndex, value, _genderIndex, _isShiny);
        }
        public int GenderIndex
        {
            get => _genderIndex;
            set => ChoosePersonality(_natureIndex, _abilityIndex, value, _isShiny);
        }

        public bool CanChooseAbility => AbilityChoices.Count > 1;
        public bool CanChooseGender  => GenderChoices.Count > 1;
        public string AbilityTip => CanChooseAbility ? "Changes the PID" : "This Pokémon has one ability";
        public string GenderTip  => CanChooseGender ? "Changes the PID" : "This Pokémon's gender is fixed";

        private bool _isShiny;
        public bool IsShiny
        {
            get => _isShiny;
            set => ChoosePersonality(_natureIndex, _abilityIndex, _genderIndex, value);
        }

        // Null when a patch changed the shiny check in a way DSPRE can't read.
        private readonly int? _shinyValues;
        public bool CanChooseShiny => _shinyValues > 0;
        public string ShinyTip => _shinyValues switch
        {
            null => "Shiny check changed by a patch DSPRE can't read",
            0 => "This ROM has no shiny Pokémon",
            _ => "Changes the PID",
        };

        // ---- Text fields ----
        private string _otName = string.Empty;
        public string OtName
        {
            get => _otName;
            set { if (Set(ref _otName, value)) MarkTextDirty(); }
        }
        private string _nickname = string.Empty;
        public string Nickname
        {
            get => _nickname;
            set { if (Set(ref _nickname, value)) MarkTextDirty(); }
        }

        // ---- HGSS-only visibility ----
        public bool IsHGSS => RomInfo.GameFamily == RomInfo.GameFamilies.HGSS;

        // ----------------------------------------------------------------
        // Loading flag (prevents handlers from firing during load)
        // ----------------------------------------------------------------

        private bool _loading;
        private bool _refreshingPersonality;

        // ----------------------------------------------------------------
        // Constructor
        // ----------------------------------------------------------------

        public TradeEditorViewModel()
        {
            _loading = true;

            if (Design.IsDesignMode)
            {
                for (int i = 0; i < 10; i++) PokemonNames.Add($"Pokémon {i}");
                for (int i = 0; i < 5;  i++) AbilityNames.Add($"Ability {i}");
                for (int i = 0; i < 10; i++) ItemNames.Add($"Item {i}");
                foreach (string n in System.Enum.GetNames(typeof(TradeOriginLang))) LanguageNames.Add(n);
                SelectedSpecies   = 1;
                SelectedRequested = 2;
                SelectedAbility   = 0;
                SelectedHeldItem  = 0;
                HpIV = AtkIV = DefIV = SpeIV = SpaIV = SpdIV = 31;
                OtName   = "Trainer";
                Nickname = "Ditto";
                TradeID  = 0;
                _loading = false;
                return;
            }

            _shinyValues = ShinyCheck.Values();
            foreach (string n in RomInfo.GetPokemonNames())  PokemonNames.Add(n);
            foreach (string n in RomInfo.GetAbilityNames())  AbilityNames.Add(n);
            foreach (string n in RomInfo.GetItemNames())     ItemNames.Add(n);
            ReloadLanguages();

            _tradeArchive = new TextArchive(GetTextBankIndex());

            if (TradeData.GetTradeCount() > 0)
                LoadFromFile(0);

            _loading = false;
        }

        private void ReloadLanguages()
        {
            LabelStore.Sync(LanguageNames, "trade_languages");
            AppEvents.LabelsChanged -= OnLabelsChanged; AppEvents.LabelsChanged += OnLabelsChanged;
            AppEvents.NamesChanged  -= OnNamesChanged;  AppEvents.NamesChanged  += OnNamesChanged;
            AppEvents.PersonalDataSaved    -= OnPersonalDataSaved;    AppEvents.PersonalDataSaved    += OnPersonalDataSaved;
        }
        private void OnLabelsChanged(object sender, System.EventArgs e)
        {
            ReloadLanguages();
            Repoke(_selectedLanguage, nameof(SelectedLanguage), v => _selectedLanguage = v);
        }
        private void OnNamesChanged(object sender, System.EventArgs e)
        {
            // Pokémon / ability / item names live in ROM text archives; refresh when the Text editor saves.
            ListSync.Apply(PokemonNames, RomInfo.GetPokemonNames());
            ListSync.Apply(AbilityNames, RomInfo.GetAbilityNames());
            ListSync.Apply(ItemNames,    RomInfo.GetItemNames());
            // Re-resolve the species / requested / ability / held-item combos' displayed text.
            Repoke(_selectedSpecies,   nameof(SelectedSpecies),   v => _selectedSpecies = v);
            Repoke(_selectedRequested, nameof(SelectedRequested), v => _selectedRequested = v);
            Repoke(_selectedAbility,   nameof(SelectedAbility),   v => _selectedAbility = v);
            Repoke(_selectedHeldItem,  nameof(SelectedHeldItem),  v => _selectedHeldItem = v);
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ReloadSpeciesPersonality, global::Avalonia.Threading.DispatcherPriority.Background);
        }
        private void OnPersonalDataSaved(object sender, int species)
        {
            if (species == _selectedSpecies) ReloadSpeciesPersonality();
        }
        private void Repoke(int current, string name, System.Action<int> set)
        {
            if (current < 0) return;
            set(-1); OnPropertyChanged(name);
            global::Avalonia.Threading.Dispatcher.UIThread.Post(
                () => { set(current); OnPropertyChanged(name); },
                global::Avalonia.Threading.DispatcherPriority.Background);
        }
        /// <summary>Unsubscribes from app-wide events; call when the editor window closes.</summary>
        public void Detach()
        {
            AppEvents.LabelsChanged -= OnLabelsChanged;
            AppEvents.NamesChanged -= OnNamesChanged;
            AppEvents.PersonalDataSaved -= OnPersonalDataSaved;
        }

        // ----------------------------------------------------------------
        // Commands
        // ----------------------------------------------------------------

        /// <summary>Called when TradeID spinner value changes (after user confirms).</summary>
        public async Task ChangeTradeIDAsync(int newID)
        {
            if (_loading) return;
            // A cancel puts the spinner back on the shown trade, which lands here again.
            if (_cur != null && newID == _cur.Id) return;
            if (_tradeDirty || _textDirty)
            {
                DialogHelper.MsgResult result = await DialogHelper.AskYesNoCancelAsync(
                    "Save your changes before switching trades?",
                    "Unsaved changes");

                if (result == DialogHelper.MsgResult.Yes)
                    SaveChanges();
                else if (result == DialogHelper.MsgResult.Cancel)
                {
                    // revert spinner: caller must handle
                    TradeID = _cur?.Id ?? 0;
                    return;
                }
            }

            if (newID < 0 || newID >= TradeData.GetTradeCount()) return;
            LoadFromFile(newID);
        }



        // ----------------------------------------------------------------
        // Private helpers
        // ----------------------------------------------------------------

        private int _personalSpecies = -1;
        private byte _genderRatio;
        private ushort _secondAbility;
        private readonly List<int> _genderValues = new();

        private void ReloadSpeciesPersonality()
        {
            _personalSpecies = -1;
            RefreshPersonality();
        }

        private void RefreshPersonality()
        {
            if (Design.IsDesignMode || _loading) return;
            _refreshingPersonality = true;
            try
            {
                if (_personalSpecies != _selectedSpecies)
                {
                    LoadSpeciesChoices();
                    // Refilling a list clears its selection, and the binding skips a value that didn't change.
                    _abilityIndex = _genderIndex = -1;
                    OnPropertyChanged(nameof(AbilityIndex));
                    OnPropertyChanged(nameof(GenderIndex));
                }

                uint pid = (uint)_pid;
                _natureIndex = DVCalculator.GetNatureFromPID(pid);
                _abilityIndex = TradePersonality.AbilitySlot(pid, _secondAbility);
                _genderIndex = _genderValues.IndexOf(TradePersonality.Gender(pid, _genderRatio));
                _isShiny = _shinyValues is int shinyValues && TradePersonality.IsShiny((uint)_otID, pid, shinyValues);

                OnPropertyChanged(nameof(NatureIndex));
                OnPropertyChanged(nameof(AbilityIndex));
                OnPropertyChanged(nameof(GenderIndex));
                OnPropertyChanged(nameof(IsShiny));
            }
            finally { _refreshingPersonality = false; }
        }

        private void LoadSpeciesChoices()
        {
            PokemonPersonalData personal = null;
            try
            {
                if (_selectedSpecies >= 0 && _selectedSpecies < RomInfo.GetPersonalFilesCount()) personal = new PokemonPersonalData(_selectedSpecies);
            }
            catch (System.Exception ex) when (ex is IOException or System.UnauthorizedAccessException)
            {
                AppLogger.Warn($"TradeEditor: Couldn't read species {_selectedSpecies}: {ex.Message}");
                return;
            }
            _personalSpecies = _selectedSpecies;
            _genderRatio = personal?.GenderVec ?? 0;
            _secondAbility = personal?.SecondAbility ?? 0;

            AbilityChoices.Clear();
            AbilityChoices.Add(AbilityName(personal?.FirstAbility ?? 0));
            if (_secondAbility != 0) AbilityChoices.Add(AbilityName(_secondAbility));

            _genderValues.Clear();
            _genderValues.AddRange(TradePersonality.Genders(_genderRatio));
            GenderChoices.Clear();
            foreach (int gender in _genderValues) GenderChoices.Add(TradePersonality.GenderNames[gender]);

            OnPropertyChanged(nameof(CanChooseAbility));
            OnPropertyChanged(nameof(CanChooseGender));
            OnPropertyChanged(nameof(AbilityTip));
            OnPropertyChanged(nameof(GenderTip));
        }

        private string AbilityName(int ability) => ability < AbilityNames.Count ? AbilityNames[ability] : ability.ToString();

        private void ChoosePersonality(int nature, int abilitySlot, int genderIndex, bool shiny)
        {
            // Refilling a list for a new species briefly selects nothing.
            if (_refreshingPersonality || _loading || _cur == null || nature < 0 || abilitySlot < 0 || genderIndex < 0) return;
            if (_personalSpecies != _selectedSpecies) return;
            uint pid = TradePersonality.Find((uint)_pid, (uint)_otID, nature, abilitySlot, _genderValues[genderIndex], shiny, _shinyValues ?? 0, _genderRatio, _secondAbility);
            if (pid != (uint)_pid) { Pid = (int)pid; return; }

            // No PID gives that mix. The controls hold the rejected pick, and the binding skips a value it already has.
            _refreshingPersonality = true;
            _natureIndex = _abilityIndex = _genderIndex = -1;
            _isShiny = shiny;
            OnPropertyChanged(nameof(NatureIndex));
            OnPropertyChanged(nameof(AbilityIndex));
            OnPropertyChanged(nameof(GenderIndex));
            OnPropertyChanged(nameof(IsShiny));
            _refreshingPersonality = false;
            global::Avalonia.Threading.Dispatcher.UIThread.Post(RefreshPersonality, global::Avalonia.Threading.DispatcherPriority.Background);
        }

        private void LoadFromFile(int tradeID)
        {
            _loading = true;
            _cur = new TradeData(tradeID);
            _tradeArchive = new TextArchive(GetTextBankIndex());

            TradeID          = tradeID;
            PopulateFromCur();

            OtName   = GetOTName(tradeID);
            Nickname = GetMonNickname(tradeID);

            _tradeDirty = false;
            _textDirty  = false;
            OnPropertyChanged(nameof(HasUnsavedChanges));

            _loading = false;
            RefreshPersonality();

            _history.Reset(Snapshot());   // loaded state is the clean undo baseline for this trade
            _lastCaptureUtc = System.DateTime.MinValue;
            RaiseUndoState();
        }

        private void SaveTradeCore()
        {
            if (_cur == null) return;
            SyncTradeFieldsToCur();

            // TradeID may already hold the next trade: the spinner updates it before the save prompt.
            _cur.SaveToFileDefaultDir(_cur.Id, false);
            _tradeDirty = false;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            AppLogger.Debug($"TradeEditor: Saved trade data for ID {_cur.Id}.");
            MarkSavedIfClean();
        }

        private void SaveTextCore()
        {
            if (_tradeArchive == null || _cur == null) return;
            int id = _cur.Id;
            int count = TradeData.GetTradeCount();
            if (id < 0 || id + count >= _tradeArchive.Messages.Count)
            {
                AppLogger.Error("TradeEditor: Can't save to text bank. Index is out of range.");
                return;
            }
            _tradeArchive.Messages[id]         = Nickname;
            _tradeArchive.Messages[id + count] = OtName;
            _tradeArchive.SaveToExpandedDir(GetTextBankIndex(), false);
            _textDirty = false;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            AppLogger.Debug($"TradeEditor: Saved trade text data to message bank {GetTextBankIndex()}");
            MarkSavedIfClean();
        }

        private void MarkTradeDirty()
        {
            if (_loading) return;
            RecordUndoSnapshot();
            _tradeDirty = true;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void MarkTextDirty()
        {
            if (_loading) return;
            RecordUndoSnapshot();
            _textDirty = true;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private int GetTextBankIndex()
        {
            if (TradeData.TextBank >= 0) return TradeData.TextBank;
            AppLogger.Error("TradeEditor: Invalid game family for text bank index retrieval.");
            return 0;
        }

        private string GetMonNickname(int tradeID)
        {
            const int maxLen = 10;
            string msg = _tradeArchive.Messages[tradeID];
            return msg.Length > maxLen ? msg.Substring(0, maxLen) : msg;
        }

        private string GetOTName(int tradeID)
        {
            const int maxLen = 7;
            int index = tradeID + TradeData.GetTradeCount();
            string msg = _tradeArchive.Messages[index];
            return msg.Length > maxLen ? msg.Substring(0, maxLen) : msg;
        }
    }
}
