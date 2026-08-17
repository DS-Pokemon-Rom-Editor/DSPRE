using Avalonia.Controls;
using DSPRE;
using DSPRE.Avalonia;
using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using Images;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static DSPRE.ROMFiles.ItemData;
using static DSPRE.RomInfo;
using AvaBitmap = Avalonia.Media.Imaging.Bitmap;

namespace DSPRE.Avalonia.ViewModels
{
    public class ItemEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (EqualityComparer<T>.Default.Equals(f, v)) return false;
            f = v; OnPropertyChanged(n); return true;
        }

        // ─── hg-engine source banner ──────────────────────────────────────────────
        public string HgEngineBanner => DSPRE.HgEngine.HgEngineProject.BannerText;
        public bool ShowHgEngineBanner => HgEngineBanner != null;

        // ── Design-time constructor ──────────────────────────────────────────
        public ItemEditorViewModel()
        {
            if (!Design.IsDesignMode) return;

            for (int i = 0; i < 20; i++) ItemNames.Add($"Item {i:D3}");
            PopulateEnumCollections();
            for (int i = 0; i < 6; i++) { IconImages.Add((i * 2 + 1).ToString("D4")); IconPalettes.Add((i * 2 + 2).ToString("D4")); }

            MaxItemIndex  = 19;
            MaxItemDataId = 9;

            _selectedItemIndex   = 0;
            _selectedIconImage   = IconImages[0];
            _selectedIconPalette = IconPalettes[0];
            _itemDataId          = 1;
            _holdEffectIndex      = 0;
            _fieldPocketIndex     = 0;
            _fieldUseFuncIndex    = 0;
            _battleUseFuncIndex   = 0;
            _naturalGiftTypeIndex = 0;
            _price               = 500;
            _naturalGiftPower    = 80;
            _flingPower          = 60;
            _partyUse            = true;  // show party params as enabled in preview
            _hpRestore           = true;
            _hpRestoreParam      = 50;
            _slpHeal             = true;
            _psnHeal             = true;
        }

        // ── Runtime constructor ──────────────────────────────────────────────
        public ItemEditorViewModel(string[] itemNames)
        {
            foreach (var n in itemNames) ItemNames.Add(n);
            PopulateEnumCollections();
            MaxItemIndex  = itemNames.Length - 1;
            MaxItemDataId = GetItemDataFileCount() - 1;
            PopulateIconPaletteDropdowns();

            // (GiratinaBoost is no longer hidden for DP — the combos bind by index == byte value, so the
            //  list must stay value-aligned. It's just a harmless unused entry there; relabel it if desired.)

            // Load first item
            _selectedItemIndex = 1;
            OnPropertyChanged(nameof(SelectedItemIndex));
            LoadFile(1);
        }

        // ── Collections ─────────────────────────────────────────────────────
        public ObservableCollection<string> ItemNames          { get; } = new();
        public ObservableCollection<string> IconImages         { get; } = new();
        public ObservableCollection<string> IconPalettes       { get; } = new();
        public ObservableCollection<string> HoldEffectNames    { get; } = new();
        public ObservableCollection<string> FieldPocketNames   { get; } = new();
        public ObservableCollection<string> FieldUseFuncNames  { get; } = new();
        public ObservableCollection<string> BattleUseFuncNames { get; } = new();
        public ObservableCollection<string> NaturalGiftTypeNames { get; } = new();

        // ── Selector ─────────────────────────────────────────────────────────
        public int MaxItemIndex  { get; private set; }
        public int MaxItemDataId { get; private set; }

        private int _selectedItemIndex = -1;
        public int SelectedItemIndex
        {
            get => _selectedItemIndex;
            set
            {
                if (_selectedItemIndex == value) return;
                _selectedItemIndex = value;
                OnPropertyChanged();
                if (!_isLoading && value >= 0 && value < ItemNames.Count)
                    LoadFile(value);
            }
        }

        // ── Item Table Entry ─────────────────────────────────────────────────
        private string _selectedIconImage;
        public string SelectedIconImage
        {
            get => _selectedIconImage;
            set
            {
                if (!Set(ref _selectedIconImage, value)) return;
                if (_isLoading || value == null) return;
                _currentEntry.itemIcon = uint.Parse(value);
                UpdateIcon();
                SetEntryDirty();
            }
        }

        private string _selectedIconPalette;
        public string SelectedIconPalette
        {
            get => _selectedIconPalette;
            set
            {
                if (!Set(ref _selectedIconPalette, value)) return;
                if (_isLoading || value == null) return;
                _currentEntry.itemPalette = uint.Parse(value);
                UpdateIcon();
                SetEntryDirty();
            }
        }

        private int _itemDataId;
        public int ItemDataId
        {
            get => _itemDataId;
            set
            {
                if (!Set(ref _itemDataId, value)) return;
                if (_isLoading || value < 0 || value > MaxItemDataId) return;
                _currentEntry.itemData = (uint)value;
                LoadItemData(value);
                SetEntryDirty();
            }
        }

        private AvaBitmap _itemIcon;
        public AvaBitmap ItemIcon
        {
            get => _itemIcon;
            private set { if (Set(ref _itemIcon, value)) OnPropertyChanged(nameof(IconUnavailable)); }
        }
        public bool IconUnavailable => ItemIcon == null && RomInfo.isHGE;

        // ── Hold Effect ──────────────────────────────────────────────────────
        private int _holdEffectIndex;
        public int HoldEffectIndex
        {
            get => _holdEffectIndex;
            set
            {
                if (!Set(ref _holdEffectIndex, value)) return;
                if (_isLoading || _currentData == null || value < 0) return;
                _currentData.holdEffect = (HoldEffect)value;
                SetDataDirty();
            }
        }

        private int _holdEffectParam;
        public int HoldEffectParam
        {
            get => _holdEffectParam;
            set
            {
                if (!Set(ref _holdEffectParam, value)) return;
                if (_isLoading || _currentData == null) return;
                _currentData.HoldEffectParam = (byte)value;
                SetDataDirty();
            }
        }

        // ── Pocket ───────────────────────────────────────────────────────────
        private int _fieldPocketIndex;
        public int FieldPocketIndex
        {
            get => _fieldPocketIndex;
            set
            {
                if (!Set(ref _fieldPocketIndex, value)) return;
                if (_isLoading || _currentData == null || value < 0) return;
                _currentData.fieldPocket = (FieldPocket)value;
                SetDataDirty();
            }
        }

        private bool _pokeBallsBattlePocket;
        public bool PokeBallsBattlePocket     { get => _pokeBallsBattlePocket;     set { if (Set(ref _pokeBallsBattlePocket,     value) && !_isLoading && _currentData != null) { UpdateBattlePocket(); SetDataDirty(); } } }
        private bool _battleItemsBattlePocket;
        public bool BattleItemsBattlePocket   { get => _battleItemsBattlePocket;   set { if (Set(ref _battleItemsBattlePocket,   value) && !_isLoading && _currentData != null) { UpdateBattlePocket(); SetDataDirty(); } } }
        private bool _hpRestoreBattlePocket;
        public bool HpRestoreBattlePocket     { get => _hpRestoreBattlePocket;     set { if (Set(ref _hpRestoreBattlePocket,     value) && !_isLoading && _currentData != null) { UpdateBattlePocket(); SetDataDirty(); } } }
        private bool _statusHealersBattlePocket;
        public bool StatusHealersBattlePocket { get => _statusHealersBattlePocket; set { if (Set(ref _statusHealersBattlePocket, value) && !_isLoading && _currentData != null) { UpdateBattlePocket(); SetDataDirty(); } } }
        private bool _ppRestoreBattlePocket;
        public bool PpRestoreBattlePocket     { get => _ppRestoreBattlePocket;     set { if (Set(ref _ppRestoreBattlePocket,     value) && !_isLoading && _currentData != null) { UpdateBattlePocket(); SetDataDirty(); } } }

        private void UpdateBattlePocket()
        {
            if (_currentData == null) return;
            BattlePocket bp = BattlePocket.None;
            if (_pokeBallsBattlePocket)       bp |= BattlePocket.PokeBalls;
            if (_battleItemsBattlePocket)     bp |= BattlePocket.BattleItems;
            if (_hpRestoreBattlePocket)       bp |= BattlePocket.HpRestore;
            if (_statusHealersBattlePocket)   bp |= BattlePocket.StatusHealers;
            if (_ppRestoreBattlePocket)       bp |= BattlePocket.PpRestore;
            _currentData.battlePocket = bp;
        }

        // ── Checks ───────────────────────────────────────────────────────────
        private bool _preventToss;
        public bool PreventToss
        {
            get => _preventToss;
            set { if (Set(ref _preventToss, value) && !_isLoading && _currentData != null) { _currentData.PreventToss = value; SetDataDirty(); } }
        }

        private bool _selectable;
        public bool Selectable
        {
            get => _selectable;
            set { if (Set(ref _selectable, value) && !_isLoading && _currentData != null) { _currentData.Selectable = value; SetDataDirty(); } }
        }

        private bool _partyUse;
        public bool PartyUse
        {
            get => _partyUse;
            set
            {
                if (!Set(ref _partyUse, value)) return;
                if (!_isLoading && _currentData != null) { _currentData.PartyUse = (byte)(value ? 1 : 0); SetDataDirty(); }
                OnPropertyChanged(nameof(PartyParamsEnabled));
            }
        }
        public bool PartyParamsEnabled => _partyUse;

        // ── Price ────────────────────────────────────────────────────────────
        private int _price;
        public int Price
        {
            get => _price;
            set { if (Set(ref _price, value) && !_isLoading && _currentData != null) { _currentData.price = (ushort)value; SetDataDirty(); } }
        }

        // ── Move Related ─────────────────────────────────────────────────────
        private int _naturalGiftTypeIndex;
        public int NaturalGiftTypeIndex
        {
            get => _naturalGiftTypeIndex;
            set
            {
                if (!Set(ref _naturalGiftTypeIndex, value)) return;
                if (_isLoading || _currentData == null || value < 0) return;
                _currentData.naturalGiftType = (NaturalGiftType)value;
                SetDataDirty();
            }
        }

        private int _naturalGiftPower;
        public int NaturalGiftPower { get => _naturalGiftPower; set { if (Set(ref _naturalGiftPower, value) && !_isLoading && _currentData != null) { _currentData.NaturalGiftPower = (byte)value; SetDataDirty(); } } }

        private int _flingEffect;
        public int FlingEffect      { get => _flingEffect;      set { if (Set(ref _flingEffect,      value) && !_isLoading && _currentData != null) { _currentData.FlingEffect      = (byte)value; SetDataDirty(); } } }

        private int _flingPower;
        public int FlingPower       { get => _flingPower;       set { if (Set(ref _flingPower,       value) && !_isLoading && _currentData != null) { _currentData.FlingPower       = (byte)value; SetDataDirty(); } } }

        private int _pluckEffect;
        public int PluckEffect      { get => _pluckEffect;      set { if (Set(ref _pluckEffect,      value) && !_isLoading && _currentData != null) { _currentData.PluckEffect      = (byte)value; SetDataDirty(); } } }

        // ── Functions ────────────────────────────────────────────────────────
        private int _fieldUseFuncIndex;
        public int FieldUseFuncIndex
        {
            get => _fieldUseFuncIndex;
            set
            {
                if (!Set(ref _fieldUseFuncIndex, value)) return;
                if (_isLoading || _currentData == null || value < 0) return;
                _currentData.fieldUseFunc = (FieldUseFunc)value;
                SetDataDirty();
            }
        }

        private int _battleUseFuncIndex;
        public int BattleUseFuncIndex
        {
            get => _battleUseFuncIndex;
            set
            {
                if (!Set(ref _battleUseFuncIndex, value)) return;
                if (_isLoading || _currentData == null || value < 0) return;
                _currentData.battleUseFunc = (BattleUseFunc)value;
                SetDataDirty();
            }
        }

        // ── Party Params — Status Heals ──────────────────────────────────────
        private bool _slpHeal;   public bool SlpHeal   { get => _slpHeal;   set { if (Set(ref _slpHeal,   value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.SlpHeal   = value; SetDataDirty(); } } }
        private bool _psnHeal;   public bool PsnHeal   { get => _psnHeal;   set { if (Set(ref _psnHeal,   value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.PsnHeal   = value; SetDataDirty(); } } }
        private bool _brnHeal;   public bool BrnHeal   { get => _brnHeal;   set { if (Set(ref _brnHeal,   value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.BrnHeal   = value; SetDataDirty(); } } }
        private bool _frzHeal;   public bool FrzHeal   { get => _frzHeal;   set { if (Set(ref _frzHeal,   value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.FrzHeal   = value; SetDataDirty(); } } }
        private bool _przHeal;   public bool PrzHeal   { get => _przHeal;   set { if (Set(ref _przHeal,   value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.PrzHeal   = value; SetDataDirty(); } } }
        private bool _cfsHeal;   public bool CfsHeal   { get => _cfsHeal;   set { if (Set(ref _cfsHeal,   value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.CfsHeal   = value; SetDataDirty(); } } }
        private bool _infHeal;   public bool InfHeal   { get => _infHeal;   set { if (Set(ref _infHeal,   value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.InfHeal   = value; SetDataDirty(); } } }
        private bool _guardSpec; public bool GuardSpec { get => _guardSpec; set { if (Set(ref _guardSpec, value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.GuardSpec = value; SetDataDirty(); } } }
        private bool _revive;    public bool Revive    { get => _revive;    set { if (Set(ref _revive,    value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.Revive    = value; SetDataDirty(); } } }
        private bool _reviveAll; public bool ReviveAll { get => _reviveAll; set { if (Set(ref _reviveAll, value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.ReviveAll = value; SetDataDirty(); } } }
        private bool _levelUp;   public bool LevelUp   { get => _levelUp;   set { if (Set(ref _levelUp,   value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.LevelUp   = value; SetDataDirty(); } } }
        private bool _evolve;    public bool Evolve    { get => _evolve;    set { if (Set(ref _evolve,    value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.Evolve    = value; SetDataDirty(); } } }

        // ── Party Params — Stat Stages ───────────────────────────────────────
        private int _atkStages;      public int AtkStages      { get => _atkStages;      set { if (Set(ref _atkStages,      value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.AtkStages      = value; SetDataDirty(); } } }
        private int _defStages;      public int DefStages      { get => _defStages;      set { if (Set(ref _defStages,      value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.DefStages      = value; SetDataDirty(); } } }
        private int _spAtkStages;    public int SpAtkStages    { get => _spAtkStages;    set { if (Set(ref _spAtkStages,    value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.SpAtkStages    = value; SetDataDirty(); } } }
        private int _spDefStages;    public int SpDefStages    { get => _spDefStages;    set { if (Set(ref _spDefStages,    value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.SpDefStages    = value; SetDataDirty(); } } }
        private int _speedStages;    public int SpeedStages    { get => _speedStages;    set { if (Set(ref _speedStages,    value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.SpeedStages    = value; SetDataDirty(); } } }
        private int _accuracyStages; public int AccuracyStages { get => _accuracyStages; set { if (Set(ref _accuracyStages, value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.AccuracyStages = value; SetDataDirty(); } } }
        private int _critRateStages; public int CritRateStages { get => _critRateStages; set { if (Set(ref _critRateStages, value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.CritRateStages = value; SetDataDirty(); } } }

        // ── Party Params — Restore ───────────────────────────────────────────
        private bool _hpRestore;    public bool HpRestore    { get => _hpRestore;    set { if (Set(ref _hpRestore,    value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.HPRestore    = value; SetDataDirty(); } } }
        private int  _hpRestoreParam; public int HpRestoreParam { get => _hpRestoreParam; set { if (Set(ref _hpRestoreParam, value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.HPRestoreParam = (byte)value; SetDataDirty(); } } }
        private bool _ppRestore;    public bool PpRestore    { get => _ppRestore;    set { if (Set(ref _ppRestore,    value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.PPRestore    = value; SetDataDirty(); } } }
        private int  _ppRestoreParam; public int PpRestoreParam { get => _ppRestoreParam; set { if (Set(ref _ppRestoreParam, value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.PPRestoreParam = (byte)value; SetDataDirty(); } } }
        private bool _ppUps;        public bool PpUps        { get => _ppUps;        set { if (Set(ref _ppUps,        value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.PPUps        = value; SetDataDirty(); } } }
        private bool _ppMax;        public bool PpMax        { get => _ppMax;        set { if (Set(ref _ppMax,        value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.PPMax        = value; SetDataDirty(); } } }
        private bool _ppRestoreAll; public bool PpRestoreAll { get => _ppRestoreAll; set { if (Set(ref _ppRestoreAll, value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.PPRestoreAll = value; SetDataDirty(); } } }

        // ── Party Params — EVs ───────────────────────────────────────────────
        private bool _evHp;    public bool EVHp    { get => _evHp;    set { if (Set(ref _evHp,    value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.EVHp    = value; SetDataDirty(); } } }
        private bool _evAtk;   public bool EVAtk   { get => _evAtk;   set { if (Set(ref _evAtk,   value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.EVAtk   = value; SetDataDirty(); } } }
        private bool _evDef;   public bool EVDef   { get => _evDef;   set { if (Set(ref _evDef,   value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.EVDef   = value; SetDataDirty(); } } }
        private bool _evSpeed; public bool EVSpeed { get => _evSpeed; set { if (Set(ref _evSpeed, value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.EVSpeed = value; SetDataDirty(); } } }
        private bool _evSpAtk; public bool EVSpAtk { get => _evSpAtk; set { if (Set(ref _evSpAtk, value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.EVSpAtk = value; SetDataDirty(); } } }
        private bool _evSpDef; public bool EVSpDef { get => _evSpDef; set { if (Set(ref _evSpDef, value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.EVSpDef = value; SetDataDirty(); } } }

        private int _evHpValue;    public int EVHpValue    { get => _evHpValue;    set { if (Set(ref _evHpValue,    value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.EVHpValue    = (sbyte)value; SetDataDirty(); } } }
        private int _evAtkValue;   public int EVAtkValue   { get => _evAtkValue;   set { if (Set(ref _evAtkValue,   value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.EVAtkValue   = (sbyte)value; SetDataDirty(); } } }
        private int _evDefValue;   public int EVDefValue   { get => _evDefValue;   set { if (Set(ref _evDefValue,   value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.EVDefValue   = (sbyte)value; SetDataDirty(); } } }
        private int _evSpeedValue; public int EVSpeedValue { get => _evSpeedValue; set { if (Set(ref _evSpeedValue, value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.EVSpeedValue = (sbyte)value; SetDataDirty(); } } }
        private int _evSpAtkValue; public int EVSpAtkValue { get => _evSpAtkValue; set { if (Set(ref _evSpAtkValue, value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.EVSpAtkValue = (sbyte)value; SetDataDirty(); } } }
        private int _evSpDefValue; public int EVSpDefValue { get => _evSpDefValue; set { if (Set(ref _evSpDefValue, value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.EVSpDefValue = (sbyte)value; SetDataDirty(); } } }

        // ── Party Params — Friendship ────────────────────────────────────────
        private bool _friendshipLow;  public bool FriendshipLow  { get => _friendshipLow;  set { if (Set(ref _friendshipLow,  value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.FriendshipLow  = value; SetDataDirty(); } } }
        private bool _friendshipMid;  public bool FriendshipMid  { get => _friendshipMid;  set { if (Set(ref _friendshipMid,  value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.FriendshipMid  = value; SetDataDirty(); } } }
        private bool _friendshipHigh; public bool FriendshipHigh { get => _friendshipHigh; set { if (Set(ref _friendshipHigh, value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.FriendshipHigh = value; SetDataDirty(); } } }

        private int _friendshipLowValue;  public int FriendshipLowValue  { get => _friendshipLowValue;  set { if (Set(ref _friendshipLowValue,  value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.FriendshipLowValue  = (sbyte)value; SetDataDirty(); } } }
        private int _friendshipMidValue;  public int FriendshipMidValue  { get => _friendshipMidValue;  set { if (Set(ref _friendshipMidValue,  value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.FriendshipMidValue  = (sbyte)value; SetDataDirty(); } } }
        private int _friendshipHighValue; public int FriendshipHighValue { get => _friendshipHighValue; set { if (Set(ref _friendshipHighValue, value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.FriendshipHighValue = (sbyte)value; SetDataDirty(); } } }

        // ── IEditorWithUnsavedChanges ────────────────────────────────────────
        private bool _dataDirty;
        private bool _entryDirty;
        public bool HasUnsavedChanges       => _dataDirty || _entryDirty;
        public string UnsavedChangesDescription => $"Item Editor (item {_selectedItemIndex})";

        public void SaveChanges()
        {
            if (_entryDirty) SaveTableEntry();
            if (_dataDirty)  SaveItemData();
            _history.MarkSaved();
            RaiseUndoState();
        }

        public void DiscardChanges()
        {
            _dataDirty = _entryDirty = false;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        /// <summary>Export the current item's data to a file (WinForms "Save to file").</summary>
        public void ExportToFile()
        {
            _currentData?.SaveToFileExplorePath($"itemdata_{_itemDataId:D4}", showSuccessMessage: true);
        }

        /// <summary>hg-engine-only: mints a brand new item (define + itemdata.c entry + name) and jumps
        /// straight to editing it.</summary>
        public async Task AddNewItemAsync(Window owner)
        {
            if (!HgEngineProject.IsActive) return;
            string name = await DialogHelper.PromptText("New item's display name:", "Add New Item", owner: owner);
            if (name == null) return;

            if (!HgEngineItemExpansion.TryAddItem(name, out int newItemId, out string error))
            {
                await DialogHelper.ShowError($"Could not add the item:\n{error}", "Add New Item", owner);
                return;
            }

            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.itemData });

            // In-place update (ListSync), not Clear+Add — Clear briefly empties the collection, which
            // resets the FusionAutoCompleteBox's bound SelectedIndex out from under the assignment below.
            DSPRE.Avalonia.Data.ListSync.Apply(ItemNames, RomInfo.GetItemNames());
            MaxItemIndex = ItemNames.Count - 1;
            OnPropertyChanged(nameof(MaxItemIndex));
            AppEvents.RaiseNamesChanged();

            SelectedItemIndex = newItemId;
        }

        // RecordUndoSnapshot runs BEFORE the dirty-flag short-circuit, so EVERY edit is captured (not just the first).
        private void SetDataDirty()  { RecordUndoSnapshot(); if (_dataDirty)  return; _dataDirty  = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetEntryDirty() { RecordUndoSnapshot(); if (_entryDirty) return; _entryDirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        // ── Internal state ────────────────────────────────────────────────────
        private bool _isLoading;
        private ItemNarcTableEntry _currentEntry;
        private ItemData _currentData;

        // ── Undo / redo (ISupportsUndo) ────────────────────────────────────────
        // Composite snapshot: the item-data file bytes + the 4 editable table-entry fields (icon / palette /
        // itemData mapping / AGB). Edit bursts within CoalesceMs collapse into one step.
        private sealed class ItemSnapshot { public byte[] Data; public uint Icon, Palette, ItemDataId, Agb; }
        private readonly UndoHistory<ItemSnapshot> _history = new();
        private DateTime _lastCaptureUtc = DateTime.MinValue;
        private const int CoalesceMs = 500;

        public bool CanUndo => _history.CanUndo;
        public bool CanRedo => _history.CanRedo;
        public void Undo() { if (_history.CanUndo) ApplyState(_history.Undo()); }
        public void Redo() { if (_history.CanRedo) ApplyState(_history.Redo()); }
        private void RaiseUndoState() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private ItemSnapshot Snapshot() => new ItemSnapshot
        {
            Data       = _currentData?.ToByteArray(),
            Icon       = _currentEntry.itemIcon,
            Palette    = _currentEntry.itemPalette,
            ItemDataId = _currentEntry.itemData,
            Agb        = _currentEntry.itemAGB,
        };

        private void ApplyState(ItemSnapshot snap)
        {
            if (snap == null || _currentData == null) return;
            _isLoading = true;
            _currentEntry.itemIcon    = snap.Icon;
            _currentEntry.itemPalette = snap.Palette;
            _currentEntry.itemData    = snap.ItemDataId;
            _currentEntry.itemAGB     = snap.Agb;
            if (snap.Data != null) _currentData = new ItemData(new MemoryStream(snap.Data), (int)snap.ItemDataId);
            RefreshEntryBoundProps();
            PopulateFromCurrentData();
            UpdateIcon();
            _isLoading = false;

            _dataDirty = _entryDirty = _history.IsDirty;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            RaiseUndoState();
        }

        private void RecordUndoSnapshot()
        {
            if (_isLoading || _currentData == null) return;
            bool coalesce = (DateTime.UtcNow - _lastCaptureUtc).TotalMilliseconds < CoalesceMs;
            _history.Capture(Snapshot(), coalesce);
            _lastCaptureUtc = DateTime.UtcNow;
            RaiseUndoState();
        }

        /// <summary>Refreshes the icon/palette/itemData bound props from <see cref="_currentEntry"/>.</summary>
        private void RefreshEntryBoundProps()
        {
            string iconID = _currentEntry.itemIcon.ToString("D4");
            string palID  = _currentEntry.itemPalette.ToString("D4");
            _selectedIconImage   = IconImages.Contains(iconID) ? iconID : null;
            _selectedIconPalette = IconPalettes.Contains(palID) ? palID  : null;
            _itemDataId          = (int)_currentEntry.itemData;
            OnPropertyChanged(nameof(SelectedIconImage));
            OnPropertyChanged(nameof(SelectedIconPalette));
            OnPropertyChanged(nameof(ItemDataId));
        }

        // ── Load ──────────────────────────────────────────────────────────────
        private void LoadFile(int id)
        {
            _isLoading = true;
            try
            {
                _currentEntry = ReadTableEntry(id);
                RefreshEntryBoundProps();

                LoadItemData((int)_currentEntry.itemData);
                UpdateIcon();

                _dataDirty = _entryDirty = false;
                OnPropertyChanged(nameof(HasUnsavedChanges));

                _history.Reset(Snapshot());   // loaded state is the clean undo baseline for this item
                _lastCaptureUtc = DateTime.MinValue;
                RaiseUndoState();
            }
            finally { _isLoading = false; }
        }

        private void LoadItemData(int dataId)
        {
            string path = Path.Combine(RomInfo.gameDirs[DirNames.itemData].unpackedDir, dataId.ToString("D4"));
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            _currentData = new ItemData(stream, dataId);
            PopulateFromCurrentData();
        }

        private void PopulateFromCurrentData()
        {
            // Hold effect — combo bound by index == byte value; extend the list if the value is beyond the
            // known labels (a hacked/undefined effect) so it still has a slot.
            _holdEffectIndex = (int)_currentData.holdEffect;
            EnsureCovers(HoldEffectNames, "item_hold_effects", _holdEffectIndex);
            _holdEffectParam = _currentData.HoldEffectParam;
            OnPropertyChanged(nameof(HoldEffectIndex));
            OnPropertyChanged(nameof(HoldEffectParam));

            // Field pocket
            _fieldPocketIndex = (int)_currentData.fieldPocket;
            EnsureCovers(FieldPocketNames, "item_field_pockets", _fieldPocketIndex);
            OnPropertyChanged(nameof(FieldPocketIndex));

            // Battle pocket flags
            var bp = _currentData.battlePocket;
            _pokeBallsBattlePocket     = (bp & BattlePocket.PokeBalls)     != 0;
            _battleItemsBattlePocket   = (bp & BattlePocket.BattleItems)   != 0;
            _hpRestoreBattlePocket     = (bp & BattlePocket.HpRestore)     != 0;
            _statusHealersBattlePocket = (bp & BattlePocket.StatusHealers) != 0;
            _ppRestoreBattlePocket     = (bp & BattlePocket.PpRestore)     != 0;
            OnPropertyChanged(nameof(PokeBallsBattlePocket));
            OnPropertyChanged(nameof(BattleItemsBattlePocket));
            OnPropertyChanged(nameof(HpRestoreBattlePocket));
            OnPropertyChanged(nameof(StatusHealersBattlePocket));
            OnPropertyChanged(nameof(PpRestoreBattlePocket));

            // Checks
            _preventToss = _currentData.PreventToss;
            _selectable  = _currentData.Selectable;
            _partyUse    = _currentData.PartyUse == 1;
            OnPropertyChanged(nameof(PreventToss));
            OnPropertyChanged(nameof(Selectable));
            OnPropertyChanged(nameof(PartyUse));
            OnPropertyChanged(nameof(PartyParamsEnabled));

            // Price
            _price = _currentData.price;
            OnPropertyChanged(nameof(Price));

            // Move related
            _naturalGiftTypeIndex = (int)_currentData.naturalGiftType;
            EnsureCovers(NaturalGiftTypeNames, "item_natural_gift", _naturalGiftTypeIndex);
            _naturalGiftPower    = _currentData.NaturalGiftPower;
            _flingEffect         = _currentData.FlingEffect;
            _flingPower          = _currentData.FlingPower;
            _pluckEffect         = _currentData.PluckEffect;
            OnPropertyChanged(nameof(NaturalGiftTypeIndex));
            OnPropertyChanged(nameof(NaturalGiftPower));
            OnPropertyChanged(nameof(FlingEffect));
            OnPropertyChanged(nameof(FlingPower));
            OnPropertyChanged(nameof(PluckEffect));

            // Functions — combos bound by index == byte value; extend lists for unknown/raw values.
            _fieldUseFuncIndex = (int)_currentData.fieldUseFunc;
            EnsureCovers(FieldUseFuncNames, "item_field_use", _fieldUseFuncIndex);
            OnPropertyChanged(nameof(FieldUseFuncIndex));

            _battleUseFuncIndex = (int)_currentData.battleUseFunc;
            EnsureCovers(BattleUseFuncNames, "item_battle_use", _battleUseFuncIndex);
            OnPropertyChanged(nameof(BattleUseFuncIndex));

            // Party params
            var p = _currentData.PartyUseParam;
            _slpHeal = p.SlpHeal; _psnHeal = p.PsnHeal; _brnHeal = p.BrnHeal; _frzHeal = p.FrzHeal;
            _przHeal = p.PrzHeal; _cfsHeal = p.CfsHeal; _infHeal = p.InfHeal; _guardSpec = p.GuardSpec;
            _revive  = p.Revive;  _reviveAll = p.ReviveAll; _levelUp = p.LevelUp; _evolve = p.Evolve;
            _atkStages = p.AtkStages; _defStages = p.DefStages; _spAtkStages = p.SpAtkStages;
            _spDefStages = p.SpDefStages; _speedStages = p.SpeedStages; _accuracyStages = p.AccuracyStages;
            _critRateStages = p.CritRateStages;
            _hpRestore = p.HPRestore; _hpRestoreParam = p.HPRestoreParam;
            _ppRestore = p.PPRestore; _ppRestoreParam = p.PPRestoreParam;
            _ppUps = p.PPUps; _ppMax = p.PPMax; _ppRestoreAll = p.PPRestoreAll;
            _evHp = p.EVHp; _evAtk = p.EVAtk; _evDef = p.EVDef; _evSpeed = p.EVSpeed;
            _evSpAtk = p.EVSpAtk; _evSpDef = p.EVSpDef;
            _evHpValue = p.EVHpValue; _evAtkValue = p.EVAtkValue; _evDefValue = p.EVDefValue;
            _evSpeedValue = p.EVSpeedValue; _evSpAtkValue = p.EVSpAtkValue; _evSpDefValue = p.EVSpDefValue;
            _friendshipLow = p.FriendshipLow; _friendshipMid = p.FriendshipMid; _friendshipHigh = p.FriendshipHigh;
            _friendshipLowValue = p.FriendshipLowValue; _friendshipMidValue = p.FriendshipMidValue;
            _friendshipHighValue = p.FriendshipHighValue;

            foreach (var name in _partyPropNames) OnPropertyChanged(name);
        }

        private static readonly string[] _partyPropNames =
        {
            nameof(SlpHeal),   nameof(PsnHeal),   nameof(BrnHeal),   nameof(FrzHeal),
            nameof(PrzHeal),   nameof(CfsHeal),   nameof(InfHeal),   nameof(GuardSpec),
            nameof(Revive),    nameof(ReviveAll),  nameof(LevelUp),   nameof(Evolve),
            nameof(AtkStages), nameof(DefStages),  nameof(SpAtkStages),  nameof(SpDefStages),
            nameof(SpeedStages), nameof(AccuracyStages), nameof(CritRateStages),
            nameof(HpRestore), nameof(HpRestoreParam), nameof(PpRestore), nameof(PpRestoreParam),
            nameof(PpUps),     nameof(PpMax),      nameof(PpRestoreAll),
            nameof(EVHp),      nameof(EVAtk),      nameof(EVDef),     nameof(EVSpeed),
            nameof(EVSpAtk),   nameof(EVSpDef),
            nameof(EVHpValue), nameof(EVAtkValue), nameof(EVDefValue), nameof(EVSpeedValue),
            nameof(EVSpAtkValue), nameof(EVSpDefValue),
            nameof(FriendshipLow),  nameof(FriendshipMid),  nameof(FriendshipHigh),
            nameof(FriendshipLowValue), nameof(FriendshipMidValue), nameof(FriendshipHighValue)
        };

        // ── Helpers ───────────────────────────────────────────────────────────
        private static ItemNarcTableEntry ReadTableEntry(int index)
        {
            // RomInfo.itemTableOffset is a hardcoded per-vanilla-ROM-version ARM9 RAM address for the
            // item -> (itemData, icon, palette, agb) indirection table — meaningless on hg-engine's
            // recompiled ARM9. hg-engine's own data/itemdata/itemdata.c is a flat array indexed directly
            // by item id with no such indirection, so item data itself is read directly by id here.
            // Icon/palette DO have a real, deterministic mapping too, but NOT "same slot for both": hg-
            // engine's data/graphics/itemgra.mk emits one NCGR+NCLR pair per item into $(BUILD)/a018,
            // named "<id+2>-00.NCGR"/"<id+2>-01.NCLR" (a leftover of the vanilla dump script's own
            // numbering) — but the final a018.narc is packed by naturally-sorted filename order, headed
            // by two fixed files (0000.NANR, 0001.NCER). So each item contributes two CONSECUTIVE narc
            // members, image then palette: image slot = 2*id + 2, palette slot = 2*id + 3. Confirmed
            // against the real checkout end to end (id 0 "none": slots 2/3; id 1 ITEM_MASTER_BALL:
            // slots 4/5 — verified by magic bytes, RGCN then RLCN — through the real item range). A
            // brand-new item added but not yet compiled into a ROM simply won't have slots yet and falls
            // back to "n/a" like before (see UpdateIcon).
            if (RomInfo.isHGE)
            {
                uint imageSlot = (uint)(2 * index + 2);
                uint paletteSlot = imageSlot + 1;
                return new ItemNarcTableEntry { itemData = (uint)index, itemIcon = imageSlot, itemPalette = paletteSlot, itemAGB = 0 };
            }

            uint offset = RomInfo.itemTableOffset;
            return new ItemNarcTableEntry
            {
                itemData    = ARM9.ReadWordLE((uint)(offset + index * 8)),
                itemIcon    = ARM9.ReadWordLE((uint)(offset + index * 8 + 2)),
                itemPalette = ARM9.ReadWordLE((uint)(offset + index * 8 + 4)),
                itemAGB     = ARM9.ReadWordLE((uint)(offset + index * 8 + 6))
            };
        }

        private void SaveTableEntry()
        {
            if (RomInfo.isHGE)
            {
                // Nothing reliable to write back to: see ReadTableEntry. Item data itself still saves
                // normally (SaveItemData, keyed directly by item id).
                _entryDirty = false;
                OnPropertyChanged(nameof(HasUnsavedChanges));
                return;
            }

            uint offset = (uint)(_selectedItemIndex * 8);
            uint base_  = RomInfo.itemTableOffset;
            ARM9.WriteBytes(BitConverter.GetBytes((ushort)_currentEntry.itemData),    base_ + offset);
            ARM9.WriteBytes(BitConverter.GetBytes((ushort)_currentEntry.itemIcon),    base_ + offset + 2);
            ARM9.WriteBytes(BitConverter.GetBytes((ushort)_currentEntry.itemPalette), base_ + offset + 4);
            ARM9.WriteBytes(BitConverter.GetBytes((ushort)_currentEntry.itemAGB),     base_ + offset + 6);
            _entryDirty = false;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void SaveItemData()
        {
            _currentData?.SaveToFileDefaultDir((int)_currentEntry.itemData, false);
            WriteHgEngineSource();
            _dataDirty = false;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        // Curated v1 scope: every top-level ITEMDATA field this editor already exposes, except price
        // (hg-engine's source sets it via an ITEM_PRICE(n) macro call, not a plain ".price = n" — the
        // anchored patcher can't locate a field spelled as a macro argument, so it's reported unresolved
        // and left untouched rather than guessed) and the nested .partyUseParam sub-fields (30+ fields
        // whose hg-engine names don't line up 1:1 with this editor's ItemData.PartyUseParam property
        // names — left for a later pass rather than risking a wrong mapping).
        private void WriteHgEngineSource()
        {
            if (!HgEngineProject.IsActive || _currentData == null) return;

            string TypeSymbol(int type) =>
                HgEngineSymbolTable.Load("include/constants/pokemon.h")?.TryGetNameWithPrefix(type, "TYPE_", out string n) == true ? n : type.ToString();
            // item.h packs ITEM_*/POCKET_*/BATTLE_POCKET_* into one flat namespace, so a plain by-value
            // lookup can return a same-valued name from the wrong family — filter by prefix (see
            // TryGetNameWithPrefix's doc comment for the live bug this fixes).
            string PocketSymbol(int pocket) =>
                HgEngineSymbolTable.Load("include/constants/item.h")?.TryGetNameWithPrefix(pocket, "POCKET_", out string n) == true ? n : pocket.ToString();
            // battlePocket is itself a bit-OR of up to 5 checkboxes (Poké Balls/Battle Items/HP Restore/
            // Status Healers/PP Restore) — TryGetFlagsExpression handles both the common single-pocket
            // case and any combination, falling back to a raw number only if some bit isn't covered.
            string BattlePocketSymbol(int pocket) =>
                HgEngineSymbolTable.Load("include/constants/item.h")?.TryGetFlagsExpression(pocket, "BATTLE_POCKET_", out string n) == true ? n : pocket.ToString();
            string Bool(bool b) => b ? "TRUE" : "FALSE";

            var fields = new List<HgEngineFieldWrite>
            {
                new(new[] { FieldPathSegment.Field("holdEffect") }, ((int)_currentData.holdEffect).ToString()),
                new(new[] { FieldPathSegment.Field("holdEffectParam") }, _currentData.HoldEffectParam.ToString()),
                new(new[] { FieldPathSegment.Field("pluckEffect") }, _currentData.PluckEffect.ToString()),
                new(new[] { FieldPathSegment.Field("flingEffect") }, _currentData.FlingEffect.ToString()),
                new(new[] { FieldPathSegment.Field("flingPower") }, _currentData.FlingPower.ToString()),
                new(new[] { FieldPathSegment.Field("naturalGiftPower") }, _currentData.NaturalGiftPower.ToString()),
                new(new[] { FieldPathSegment.Field("naturalGiftType") }, TypeSymbol((int)_currentData.naturalGiftType)),
                new(new[] { FieldPathSegment.Field("prevent_toss") }, Bool(_currentData.PreventToss)),
                new(new[] { FieldPathSegment.Field("selectable") }, Bool(_currentData.Selectable)),
                new(new[] { FieldPathSegment.Field("fieldPocket") }, PocketSymbol((int)_currentData.fieldPocket)),
                new(new[] { FieldPathSegment.Field("battlePocket") }, BattlePocketSymbol((int)_currentData.battlePocket)),
                new(new[] { FieldPathSegment.Field("fieldUseFunc") }, ((int)_currentData.fieldUseFunc).ToString()),
                new(new[] { FieldPathSegment.Field("battleUseFunc") }, ((int)_currentData.battleUseFunc).ToString()),
                new(new[] { FieldPathSegment.Field("partyUse") }, _currentData.PartyUse.ToString()),
            };

            int itemId = (int)_currentEntry.itemData;
            if (!HgEngineWriter.TryWriteFields(HgEngineDomain.Items, itemId, fields, out var unresolved, out string error))
            { AppLogger.Error($"hg-engine write failed for item {itemId}: {error}"); return; }

            if (unresolved.Count > 0)
                AppLogger.Info($"hg-engine write for item {itemId}: source doesn't declare {string.Join(", ", unresolved)}, left unchanged.");
        }

        private void UpdateIcon()
        {
            if (Design.IsDesignMode) return;
            if (_currentEntry.itemIcon == uint.MaxValue || _currentEntry.itemPalette == uint.MaxValue)
            {
                ItemIcon = null;   // hg-engine: no reliable icon/palette index for this item, see ReadTableEntry
                return;
            }
            try
            {
                string dir     = RomInfo.gameDirs[DirNames.itemIcons].unpackedDir;
                string palFile = _currentEntry.itemPalette.ToString("D4");
                string imgFile = _currentEntry.itemIcon.ToString("D4");
                var palette = new NCLR(Path.Combine(dir, palFile), (int)_currentEntry.itemPalette, palFile);
                var image   = new NCGR(Path.Combine(dir, imgFile), (int)_currentEntry.itemIcon,    imgFile);
                var sprite  = new NCER(Path.Combine(dir, "0001"),  2, "0001");
                var raw     = sprite.Get_RawImage(image, palette, 0, image.Width, image.Height, trans: true, currOAM: -1, draw_index: null);
                ItemIcon = ImageConverter.ToAvaloniaBitmap(raw);
            }
            catch (Exception ex) { AppLogger.Error("UpdateIcon: " + ex); ItemIcon = null; }
        }

        private void PopulateIconPaletteDropdowns()
        {
            string dir = RomInfo.gameDirs[DirNames.itemIcons].unpackedDir;
            var files  = Directory.GetFiles(dir, "*", SearchOption.TopDirectoryOnly);
            uint idx   = 0;
            foreach (var file in files)
            {
                using var stream = File.OpenRead(file);
                byte[] header = new byte[4];
                stream.Read(header, 0, 4);
                string magic = Encoding.ASCII.GetString(header);
                if      (magic == "RGCN") IconImages.Add(idx.ToString("D4"));
                else if (magic == "RLCN") IconPalettes.Add(idx.ToString("D4"));
                idx++;
            }
        }

        // Item enum dropdowns now bind by SelectedIndex == raw byte value and pull their labels from the
        // customisable LabelStore (Tools ▸ Edit Dropdown Labels). Synced in place to keep the selection.
        private void PopulateEnumCollections()
        {
            DSPRE.Avalonia.Data.LabelStore.Sync(HoldEffectNames,      "item_hold_effects");
            DSPRE.Avalonia.Data.LabelStore.Sync(FieldPocketNames,     "item_field_pockets");
            DSPRE.Avalonia.Data.LabelStore.Sync(FieldUseFuncNames,    "item_field_use");
            DSPRE.Avalonia.Data.LabelStore.Sync(BattleUseFuncNames,   "item_battle_use");
            DSPRE.Avalonia.Data.LabelStore.Sync(NaturalGiftTypeNames, "item_natural_gift");
            AppEvents.LabelsChanged -= OnLabelsChanged; AppEvents.LabelsChanged += OnLabelsChanged;
        }

        /// <summary>Extends a combo list so it has a slot at <paramref name="index"/> (for a raw byte value
        /// beyond the known labels), labelling new slots from the LabelStore (overridable / "Singular N").</summary>
        private static void EnsureCovers(System.Collections.ObjectModel.ObservableCollection<string> coll, string key, int index)
        {
            while (coll.Count <= index && coll.Count < 256)
                coll.Add(DSPRE.Avalonia.Data.LabelStore.GetLabel(key, coll.Count));
        }

        private void OnLabelsChanged(object sender, EventArgs e)
        {
            PopulateEnumCollections();   // re-sync labels
            if (_currentData != null)    // re-extend for the current item's (possibly raw) values
            {
                EnsureCovers(HoldEffectNames,      "item_hold_effects",  (int)_currentData.holdEffect);
                EnsureCovers(FieldPocketNames,     "item_field_pockets", (int)_currentData.fieldPocket);
                EnsureCovers(FieldUseFuncNames,    "item_field_use",     (int)_currentData.fieldUseFunc);
                EnsureCovers(BattleUseFuncNames,   "item_battle_use",    (int)_currentData.battleUseFunc);
                EnsureCovers(NaturalGiftTypeNames, "item_natural_gift",  (int)_currentData.naturalGiftType);
            }
            Repoke(_holdEffectIndex,      nameof(HoldEffectIndex),      v => _holdEffectIndex = v);
            Repoke(_fieldPocketIndex,     nameof(FieldPocketIndex),     v => _fieldPocketIndex = v);
            Repoke(_fieldUseFuncIndex,    nameof(FieldUseFuncIndex),    v => _fieldUseFuncIndex = v);
            Repoke(_battleUseFuncIndex,   nameof(BattleUseFuncIndex),   v => _battleUseFuncIndex = v);
            Repoke(_naturalGiftTypeIndex, nameof(NaturalGiftTypeIndex), v => _naturalGiftTypeIndex = v);
        }

        private void Repoke(int current, string name, Action<int> set)
        {
            if (current < 0) return;
            set(-1); OnPropertyChanged(name);
            global::Avalonia.Threading.Dispatcher.UIThread.Post(
                () => { set(current); OnPropertyChanged(name); },
                global::Avalonia.Threading.DispatcherPriority.Background);
        }

        /// <summary>Unsubscribes from app-wide events; called when the editor window closes.</summary>
        public void Detach() => AppEvents.LabelsChanged -= OnLabelsChanged;

        private static int GetItemDataFileCount() =>
            Directory.GetFiles(RomInfo.gameDirs[DirNames.itemData].unpackedDir, "*", SearchOption.TopDirectoryOnly).Length;
    }
}
