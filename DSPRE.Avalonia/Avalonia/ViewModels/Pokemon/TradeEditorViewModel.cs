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
    public enum TradeOriginLang
    {
        NONE = 0, JAPANESE = 1, ENGLISH = 2, FRENCH = 3,
        ITALIAN = 4, GERMAN = 5, UNUSED = 6, SPANISH = 7, KOREAN = 8
    }
    public class TradeEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo
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
        public string UnsavedChangesDescription => "Trade Editor";
        void IEditorWithUnsavedChanges.SaveChanges() { SaveTradeCore(); SaveTextCore(); }
        public void DiscardChanges() { _tradeDirty = false; _textDirty = false; }

        // ── Undo / redo (ISupportsUndo) ────────────────────────────────────────
        // Trade edits live in the VM fields (only synced to _cur at save), and the nickname/OT names live in a
        // text archive, so the snapshot is composite: the synced trade-data bytes + the two text strings.
        private sealed class TradeSnapshot { public byte[] Data; public string OtName; public string Nickname; }
        private readonly DSPRE.Avalonia.UndoHistory<TradeSnapshot> _history = new();
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
            _cur = new TradeData(TradeID, new MemoryStream(snap.Data));
            PopulateFromCur();
            OtName = snap.OtName;
            Nickname = snap.Nickname;
            _loading = false;

            _tradeDirty = _textDirty = _history.IsDirty;
            Title = _history.IsDirty ? "● Trade Editor" : "Trade Editor";
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
            _cur.species          = SelectedSpecies;
            _cur.hpIV             = HpIV;
            _cur.atkIV            = AtkIV;
            _cur.defIV            = DefIV;
            _cur.speedIV          = SpeIV;
            _cur.spAtkIV          = SpaIV;
            _cur.spDefIV          = SpdIV;
            _cur.ability          = SelectedAbility;
            _cur.otID             = OtID;
            _cur.cool             = Cool;
            _cur.beauty           = Beauty;
            _cur.cute             = Cute;
            _cur.smart            = Smart;
            _cur.tough            = Tough;
            _cur.pid              = Pid;
            _cur.heldItem         = SelectedHeldItem;
            _cur.otGender         = SelectedOTGender;
            _cur.sheen            = Sheen;
            _cur.language         = SelectedLanguage;
            _cur.requestedSpecies = SelectedRequested;
            _cur.unknown          = IsHGSS ? Unknown : 0;
        }

        /// <summary>Pushes <see cref="_cur"/> into the bound trade fields. Caller guards with _loading.</summary>
        private void PopulateFromCur()
        {
            SelectedSpecies  = _cur.species;
            HpIV  = _cur.hpIV;
            AtkIV = _cur.atkIV;
            DefIV = _cur.defIV;
            SpeIV = _cur.speedIV;
            SpaIV = _cur.spAtkIV;
            SpdIV = _cur.spDefIV;
            SelectedAbility  = _cur.ability;
            OtID             = _cur.otID;
            Cool   = _cur.cool;
            Beauty = _cur.beauty;
            Cute   = _cur.cute;
            Smart  = _cur.smart;
            Tough  = _cur.tough;
            Pid    = _cur.pid;
            SelectedHeldItem = _cur.heldItem;
            SelectedOTGender = _cur.otGender;
            Sheen            = _cur.sheen;
            SelectedLanguage = _cur.language;
            SelectedRequested = _cur.requestedSpecies;
            Unknown          = _cur.unknown;
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

        private string _title = "Trade Editor";
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
            set { if (Set(ref _selectedSpecies, value)) MarkTradeDirty(); }
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
        public int OtID   { get => _otID;    set { if (Set(ref _otID,    value)) MarkTradeDirty(); } }
        public int Pid    { get => _pid;     set { if (Set(ref _pid,     value)) MarkTradeDirty(); } }
        public int Unknown { get => _unknown; set { if (Set(ref _unknown, value)) MarkTradeDirty(); } }

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
        public bool IsHGSS => RomInfo.gameFamily == RomInfo.GameFamilies.HGSS;

        // ----------------------------------------------------------------
        // Loading flag (prevents handlers from firing during load)
        // ----------------------------------------------------------------

        private bool _loading;

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
                foreach (var n in System.Enum.GetNames(typeof(TradeOriginLang))) LanguageNames.Add(n);
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

            foreach (var n in RomInfo.GetPokemonNames())  PokemonNames.Add(n);
            foreach (var n in RomInfo.GetAbilityNames())  AbilityNames.Add(n);
            foreach (var n in RomInfo.GetItemNames())     ItemNames.Add(n);
            ReloadLanguages();

            _tradeArchive = new TextArchive(GetTextBankIndex());

            if (TradeData.GetTradeCount() > 0)
                LoadFromFile(0);

            _loading = false;
        }

        private void ReloadLanguages()
        {
            DSPRE.Avalonia.Data.LabelStore.Sync(LanguageNames, "trade_languages");
            AppEvents.LabelsChanged -= OnLabelsChanged; AppEvents.LabelsChanged += OnLabelsChanged;
            AppEvents.NamesChanged  -= OnNamesChanged;  AppEvents.NamesChanged  += OnNamesChanged;
        }
        private void OnLabelsChanged(object sender, System.EventArgs e)
        {
            ReloadLanguages();
            Repoke(_selectedLanguage, nameof(SelectedLanguage), v => _selectedLanguage = v);
        }
        private void OnNamesChanged(object sender, System.EventArgs e)
        {
            // Pokémon / ability / item names live in ROM text archives; refresh when the Text editor saves.
            DSPRE.Avalonia.Data.ListSync.Apply(PokemonNames, RomInfo.GetPokemonNames());
            DSPRE.Avalonia.Data.ListSync.Apply(AbilityNames, RomInfo.GetAbilityNames());
            DSPRE.Avalonia.Data.ListSync.Apply(ItemNames,    RomInfo.GetItemNames());
            // Re-resolve the species / requested / ability / held-item combos' displayed text.
            Repoke(_selectedSpecies,   nameof(SelectedSpecies),   v => _selectedSpecies = v);
            Repoke(_selectedRequested, nameof(SelectedRequested), v => _selectedRequested = v);
            Repoke(_selectedAbility,   nameof(SelectedAbility),   v => _selectedAbility = v);
            Repoke(_selectedHeldItem,  nameof(SelectedHeldItem),  v => _selectedHeldItem = v);
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
        public void Detach() { AppEvents.LabelsChanged -= OnLabelsChanged; AppEvents.NamesChanged -= OnNamesChanged; }

        // ----------------------------------------------------------------
        // Commands
        // ----------------------------------------------------------------

        public void SaveTradeCommand()  => SaveTradeCore();
        public void SaveTextCommand()   => SaveTextCore();
        public void SaveAllCommand()    { SaveTradeCore(); SaveTextCore(); }

        /// <summary>Called when TradeID spinner value changes (after user confirms).</summary>
        public async Task ChangeTradeIDAsync(int newID)
        {
            if (_loading) return;
            if (_tradeDirty || _textDirty)
            {
                var result = await DialogHelper.AskYesNoCancel(
                    "You have unsaved changes. Do you want to save before changing the Trade ID?",
                    "Unsaved Changes");

                if (result == DialogHelper.MsgResult.Yes)
                {
                    SaveTradeCore();
                    SaveTextCore();
                }
                else if (result == DialogHelper.MsgResult.Cancel)
                {
                    // revert spinner: caller must handle
                    TradeID = _cur?.id ?? 0;
                    return;
                }
            }

            if (newID < 0 || newID >= TradeData.GetTradeCount()) return;
            LoadFromFile(newID);
        }



        // ----------------------------------------------------------------
        // Private helpers
        // ----------------------------------------------------------------

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
            Title = "Trade Editor";

            _loading = false;

            _history.Reset(Snapshot());   // loaded state is the clean undo baseline for this trade
            _lastCaptureUtc = System.DateTime.MinValue;
            RaiseUndoState();
        }

        private void SaveTradeCore()
        {
            if (_cur == null) return;
            SyncTradeFieldsToCur();

            _cur.SaveToFileDefaultDir(TradeID, false);
            _tradeDirty = false;
            if (!_textDirty) Title = "Trade Editor";
            AppLogger.Debug($"TradeEditor: Saved trade data for ID {_cur.id}.");
            MarkSavedIfClean();
        }

        private void SaveTextCore()
        {
            if (_tradeArchive == null) return;
            int count = TradeData.GetTradeCount();
            if (TradeID < 0 || TradeID + count > _tradeArchive.messages.Count)
            {
                AppLogger.Error("TradeEditor: Can't save to text bank. Index is out of range.");
                return;
            }
            _tradeArchive.messages[TradeID]         = Nickname;
            _tradeArchive.messages[TradeID + count] = OtName;
            _tradeArchive.SaveToExpandedDir(GetTextBankIndex(), false);
            _textDirty = false;
            if (!_tradeDirty) Title = "Trade Editor";
            AppLogger.Debug($"TradeEditor: Saved trade text data to message bank {GetTextBankIndex()}");
            MarkSavedIfClean();
        }

        private void MarkTradeDirty()
        {
            if (_loading) return;
            RecordUndoSnapshot();
            _tradeDirty = true;
            Title = "● Trade Editor";
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void MarkTextDirty()
        {
            if (_loading) return;
            RecordUndoSnapshot();
            _textDirty = true;
            Title = "● Trade Editor";
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private int GetTextBankIndex()
        {
            switch (RomInfo.gameFamily)
            {
                case RomInfo.GameFamilies.DP:
                    return RomInfo.gameLanguage == RomInfo.GameLanguages.Japanese ? 324 : 326;
                case RomInfo.GameFamilies.Plat:
                    return RomInfo.gameLanguage == RomInfo.GameLanguages.Japanese ? 369 : 370;
                case RomInfo.GameFamilies.HGSS:
                    return RomInfo.gameLanguage == RomInfo.GameLanguages.Japanese ? 198 : 200;
                default:
                    AppLogger.Error("TradeEditor: Invalid game family for text bank index retrieval.");
                    return 0;
            }
        }

        private string GetMonNickname(int tradeID)
        {
            const int maxLen = 10;
            string msg = _tradeArchive.messages[tradeID];
            return msg.Length > maxLen ? msg.Substring(0, maxLen) : msg;
        }

        private string GetOTName(int tradeID)
        {
            const int maxLen = 7;
            int index = tradeID + TradeData.GetTradeCount();
            string msg = _tradeArchive.messages[index];
            return msg.Length > maxLen ? msg.Substring(0, maxLen) : msg;
        }
    }
}
