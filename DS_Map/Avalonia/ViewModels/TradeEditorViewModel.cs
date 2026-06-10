using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.ViewModels
{
    public enum TradeOriginLang
    {
        NONE = 0, JAPANESE = 1, ENGLISH = 2, FRENCH = 3,
        ITALIAN = 4, GERMAN = 5, UNUSED = 6, SPANISH = 7, KOREAN = 8
    }
    public class TradeEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
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
            foreach (var n in System.Enum.GetNames(typeof(TradeOriginLang))) LanguageNames.Add(n);

            _tradeArchive = new TextArchive(GetTextBankIndex());

            if (TradeData.GetTradeCount() > 0)
                LoadFromFile(0);

            _loading = false;
        }

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
                    // revert spinner — caller must handle
                    TradeID = _cur?.id ?? 0;
                    return;
                }
            }

            if (newID < 0 || newID >= TradeData.GetTradeCount()) return;
            LoadFromFile(newID);
        }

        public async Task<bool> ConfirmCloseAsync()
        {
            if (!HasUnsavedChanges) return true;

            var result = await DialogHelper.AskYesNoCancel(
                "You have unsaved changes. Do you want to save before closing?",
                "Unsaved Changes");

            if (result == DialogHelper.MsgResult.Yes)
            {
                SaveTradeCore();
                SaveTextCore();
                return true;
            }
            return result == DialogHelper.MsgResult.No;
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

            OtName   = GetOTName(tradeID);
            Nickname = GetMonNickname(tradeID);

            _tradeDirty = false;
            _textDirty  = false;
            Title = "Trade Editor";

            _loading = false;
        }

        private void SaveTradeCore()
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

            _cur.SaveToFileDefaultDir(TradeID, false);
            _tradeDirty = false;
            if (!_textDirty) Title = "Trade Editor";
            AppLogger.Debug($"TradeEditor: Saved trade data for ID {_cur.id}.");
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
        }

        private void MarkTradeDirty()
        {
            if (_loading) return;
            _tradeDirty = true;
            Title = "Trade Editor*";
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void MarkTextDirty()
        {
            if (_loading) return;
            _textDirty = true;
            Title = "Trade Editor*";
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
