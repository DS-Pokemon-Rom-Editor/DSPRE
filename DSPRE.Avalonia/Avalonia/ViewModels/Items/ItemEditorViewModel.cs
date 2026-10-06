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
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static DSPRE.ROMFiles.ItemData;
using static DSPRE.RomInfo;
using AvaBitmap = Avalonia.Media.Imaging.Bitmap;

namespace DSPRE.Avalonia.ViewModels.Items
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

        // Set when the item's itemdata.c entry couldn't be read; saving would write the shown values over it.
        private string _sourceLoadError;
        public string SourceLoadError { get => _sourceLoadError; private set { if (Set(ref _sourceLoadError, value)) OnPropertyChanged(nameof(HasSourceLoadError)); } }
        public bool HasSourceLoadError => _sourceLoadError != null;

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
            foreach (string n in itemNames) ItemNames.Add(n);
            PopulateEnumCollections();
            MaxItemIndex  = itemNames.Length - 1;
            MaxItemDataId = GetItemDataFileCount() - 1;
            PopulateIconPaletteDropdowns();

            if (!RomInfo.isHGE)
            {
                int bad = ItemTable.Limits().FirstBadRow;
                if (bad >= 0) TableLimitNote = $"Items from {bad} on have no valid table row.";
            }

            _selectedItemIndex = 1;
            OnPropertyChanged(nameof(SelectedItemIndex));
            if (!LoadFile(1)) _selectedItemIndex = -1;
        }

        // Set when the ARM9 table has a row pointing past the item archives before the game's own limit.
        public string TableLimitNote { get; }
        public bool HasTableLimitNote => TableLimitNote != null;

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
                if (RecordSwitchGuard.IsSnappingBack) return;
                if (_selectedItemIndex == value || _syncingList) return;
                if (_isLoading || value < 0 || value >= ItemNames.Count)
                {
                    _selectedItemIndex = value;
                    OnPropertyChanged();
                    return;
                }
                if (_pendingItem != null) { _ = ConfirmDropPendingAsync(value); return; }
                if (_dataDirty || _entryDirty)
                {
                    // Snap the picker back to the item still loaded until the user has answered.
                    RecordSwitchGuard.SnapBack(() => _selectedItemIndex, v => _selectedItemIndex = v, () => OnPropertyChanged(nameof(SelectedItemIndex)));
                    _ = SwitchItemAsync(value);
                    return;
                }
                SwitchTo(value);
            }
        }

        private async Task SwitchItemAsync(int requested)
        {
            if (!await RecordSwitchGuard.ConfirmLeaveAsync(this, null, "item")) return;
            if (requested < ItemNames.Count) SwitchTo(requested);
        }

        /// <summary>Loads an item, leaving the current one selected and loaded when it can't be read.</summary>
        private void SwitchTo(int index)
        {
            if (LoadFile(index)) _selectedItemIndex = index;
            OnPropertyChanged(nameof(SelectedItemIndex));
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
                if (RecordSwitchGuard.IsSnappingBack) return;
                // A new item's data is its own template until it is saved, so the box can't point it elsewhere.
                if (_pendingItem != null && !_isLoading && value != _itemDataId) { OnPropertyChanged(); return; }
                // Pointing at other data replaces the loaded data, so its unsaved edits are settled first.
                if (_dataDirty && !_isLoading && value != _itemDataId && value >= 0 && value <= MaxItemDataId)
                {
                    RecordSwitchGuard.SnapBack(() => _itemDataId, v => _itemDataId = v, () => OnPropertyChanged(nameof(ItemDataId)));
                    _ = SwitchItemDataAsync(value);
                    return;
                }
                if (!Set(ref _itemDataId, value)) return;
                if (_isLoading || value < 0 || value > MaxItemDataId) return;
                PointAtItemData(value);
            }
        }

        private async Task SwitchItemDataAsync(int requested)
        {
            if (!await RecordSwitchGuard.ConfirmLeaveAsync(this, null, "item data")) return;
            if (_dataDirty || !Set(ref _itemDataId, requested, nameof(ItemDataId))) return;
            PointAtItemData(requested);
        }

        private void PointAtItemData(int dataId)
        {
            ItemData data;
            string loadError;
            try { (data, loadError) = ReadItemData(dataId); }
            catch (Exception ex)
            {
                AppLogger.Error($"Item Editor: item data {dataId} could not be read: {ex}");
                _itemDataId = (int)_currentEntry.itemData;
                OnPropertyChanged(nameof(ItemDataId));
                return;
            }
            _currentEntry.itemData = (uint)dataId;
            ApplyItemData(data, loadError);
            UpdateSharedDataNote(_selectedItemIndex);
            SetEntryDirty();
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
            set { if (Set(ref _price, value) && !_isLoading && _currentData != null) { _currentData.FullPrice = value; SetDataDirty(); } }
        }
        // hg-engine's item record adds price_high, so prices there run to 20 bits.
        public int PriceMaximum => RomInfo.isHGE ? ItemData.MaxHgEnginePrice : ushort.MaxValue;

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

        // ── Party Params: Status Heals ──────────────────────────────────────
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

        // ── Party Params: Stat Stages ───────────────────────────────────────
        private int _atkStages;      public int AtkStages      { get => _atkStages;      set { if (Set(ref _atkStages,      value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.AtkStages      = value; SetDataDirty(); } } }
        private int _defStages;      public int DefStages      { get => _defStages;      set { if (Set(ref _defStages,      value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.DefStages      = value; SetDataDirty(); } } }
        private int _spAtkStages;    public int SpAtkStages    { get => _spAtkStages;    set { if (Set(ref _spAtkStages,    value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.SpAtkStages    = value; SetDataDirty(); } } }
        private int _spDefStages;    public int SpDefStages    { get => _spDefStages;    set { if (Set(ref _spDefStages,    value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.SpDefStages    = value; SetDataDirty(); } } }
        private int _speedStages;    public int SpeedStages    { get => _speedStages;    set { if (Set(ref _speedStages,    value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.SpeedStages    = value; SetDataDirty(); } } }
        private int _accuracyStages; public int AccuracyStages { get => _accuracyStages; set { if (Set(ref _accuracyStages, value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.AccuracyStages = value; SetDataDirty(); } } }
        private int _critRateStages; public int CritRateStages { get => _critRateStages; set { if (Set(ref _critRateStages, value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.CritRateStages = value; SetDataDirty(); } } }

        // ── Party Params: Restore ───────────────────────────────────────────
        private bool _hpRestore;    public bool HpRestore    { get => _hpRestore;    set { if (Set(ref _hpRestore,    value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.HPRestore    = value; SetDataDirty(); } } }
        private int  _hpRestoreParam; public int HpRestoreParam { get => _hpRestoreParam; set { if (Set(ref _hpRestoreParam, value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.HPRestoreParam = (byte)value; SetDataDirty(); } } }
        private bool _ppRestore;    public bool PpRestore    { get => _ppRestore;    set { if (Set(ref _ppRestore,    value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.PPRestore    = value; SetDataDirty(); } } }
        private int  _ppRestoreParam; public int PpRestoreParam { get => _ppRestoreParam; set { if (Set(ref _ppRestoreParam, value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.PPRestoreParam = (byte)value; SetDataDirty(); } } }
        private bool _ppUps;        public bool PpUps        { get => _ppUps;        set { if (Set(ref _ppUps,        value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.PPUps        = value; SetDataDirty(); } } }
        private bool _ppMax;        public bool PpMax        { get => _ppMax;        set { if (Set(ref _ppMax,        value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.PPMax        = value; SetDataDirty(); } } }
        private bool _ppRestoreAll; public bool PpRestoreAll { get => _ppRestoreAll; set { if (Set(ref _ppRestoreAll, value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.PPRestoreAll = value; SetDataDirty(); } } }

        // ── Party Params: EVs ───────────────────────────────────────────────
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

        // ── Party Params: Friendship ────────────────────────────────────────
        private bool _friendshipLow;  public bool FriendshipLow  { get => _friendshipLow;  set { if (Set(ref _friendshipLow,  value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.FriendshipLow  = value; SetDataDirty(); } } }
        private bool _friendshipMid;  public bool FriendshipMid  { get => _friendshipMid;  set { if (Set(ref _friendshipMid,  value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.FriendshipMid  = value; SetDataDirty(); } } }
        private bool _friendshipHigh; public bool FriendshipHigh { get => _friendshipHigh; set { if (Set(ref _friendshipHigh, value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.FriendshipHigh = value; SetDataDirty(); } } }

        private int _friendshipLowValue;  public int FriendshipLowValue  { get => _friendshipLowValue;  set { if (Set(ref _friendshipLowValue,  value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.FriendshipLowValue  = (sbyte)value; SetDataDirty(); } } }
        private int _friendshipMidValue;  public int FriendshipMidValue  { get => _friendshipMidValue;  set { if (Set(ref _friendshipMidValue,  value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.FriendshipMidValue  = (sbyte)value; SetDataDirty(); } } }
        private int _friendshipHighValue; public int FriendshipHighValue { get => _friendshipHighValue; set { if (Set(ref _friendshipHighValue, value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.FriendshipHighValue = (sbyte)value; SetDataDirty(); } } }

        // ── IEditorWithUnsavedChanges ────────────────────────────────────────
        private bool _dataDirty;
        private bool _entryDirty;
        public bool HasUnsavedChanges       => _dataDirty || _entryDirty || _pendingItem != null;
        public string UnsavedChangesDescription =>
            _pendingItem != null ? $"New item {_pendingItem.DisplayName}" : $"Item Editor (item {_selectedItemIndex})";

        // A new item exists only here until Save; Discard drops it.
        private HgEngineItemExpansion.PendingItem _pendingItem;
        private int _returnIndex;
        // Set while the list itself changes, so the selector's own index updates aren't taken as picks.
        private bool _syncingList;

        public void SaveChanges() => _ = SaveAsync();

        async System.Threading.Tasks.Task<bool> IEditorWithUnsavedChanges.SaveChangesAsync()
        {
            await SaveAsync();
            return !HasUnsavedChanges;
        }

        public async System.Threading.Tasks.Task SaveAsync()
        {
            if (_pendingItem != null)
            {
                await SavePendingItemAsync();
                RaiseUndoState();
                return;
            }
            if (_entryDirty) SaveTableEntry();
            if (_dataDirty && !await SaveItemDataAsync()) { RaiseUndoState(); return; }
            _history.MarkSaved();
            RaiseUndoState();
        }

        public void DiscardChanges()
        {
            _dataDirty = _entryDirty = false;
            if (_pendingItem != null)
            {
                int back = _returnIndex;
                DropPendingItem();
                _selectedItemIndex = back;
                OnPropertyChanged(nameof(SelectedItemIndex));
                LoadFile(back);
            }
            else if (_selectedItemIndex >= 0 && _selectedItemIndex < ItemNames.Count)
                LoadFile(_selectedItemIndex);
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        /// <summary>The ROM's item names, with an unsaved new item shown at its id.</summary>
        private void SyncNames()
        {
            List<string> names = RomInfo.GetItemNames().ToList();
            if (_pendingItem != null)
            {
                while (names.Count <= _pendingItem.Id) names.Add("");
                names[_pendingItem.Id] = _pendingItem.DisplayName + " (not saved)";
            }
            // In-place update (ListSync), not Clear+Add: Clear briefly empties the collection, which
            // resets the FusionAutoCompleteBox's bound SelectedIndex.
            _syncingList = true;
            try { DSPRE.Avalonia.Data.ListSync.Apply(ItemNames, names); }
            finally { _syncingList = false; }
            MaxItemIndex = ItemNames.Count - 1;
            OnPropertyChanged(nameof(MaxItemIndex));
            OnPropertyChanged(nameof(SelectedItemIndex));
        }

        private void DropPendingItem()
        {
            if (_pendingItem == null) return;
            _pendingItem = null;
            SyncNames();
        }

        private async Task ConfirmDropPendingAsync(int newIndex)
        {
            if (!await DialogHelper.AskYesNo("The new item is not saved. Discard it and proceed?", "Unsaved Changes"))
            {
                RecordSwitchGuard.SnapBack(() => _selectedItemIndex, v => _selectedItemIndex = v, () => OnPropertyChanged(nameof(SelectedItemIndex)));
                return;
            }
            _dataDirty = _entryDirty = false;
            DropPendingItem();
            newIndex = Math.Min(newIndex, ItemNames.Count - 1);
            _selectedItemIndex = newIndex;
            OnPropertyChanged(nameof(SelectedItemIndex));
            LoadFile(newIndex);
        }

        /// <summary>Writes the new item's define, entry, fields and text in one save, then shows it as saved.</summary>
        private async Task<bool> SavePendingItemAsync()
        {
            HgEngineItemExpansion.PendingItem pending = _pendingItem;
            ItemData data = _currentData;
            (bool saved, string error) = await HgEngineSave.RunAsync(() => HgEngineItemExpansion.TryCommitItem(pending, data, out string commitError) ? null : commitError);
            if (!saved)
            {
                if (error != null) await DialogHelper.ShowError($"{pending.DisplayName} was not saved.\n{error}", "Item Editor");
                return false;
            }

            HgEngineItemExpansion.CompleteAdd(pending);
            _pendingItem = null;
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.itemData });
            data.SaveToFileDefaultDir(pending.Id, false);
            MaxItemDataId = GetItemDataFileCount() - 1;
            OnPropertyChanged(nameof(MaxItemDataId));
            SyncNames();
            AppEvents.RaiseNamesChanged();
            _selectedItemIndex = pending.Id;
            OnPropertyChanged(nameof(SelectedItemIndex));
            LoadFile(pending.Id);   // reads back through the new define
            SaveNotice.Saved(UnsavedChangesDescription);
            return true;
        }

        /// <summary>Export the current item's data to a file (WinForms "Save to file").</summary>
        public void ExportToFile()
        {
            _currentData?.SaveToFileExplorePath($"itemdata_{_itemDataId:D4}", showSuccessMessage: true);
        }

        /// <summary>hg-engine-only: shapes a brand new item and opens it for editing. Nothing is written
        /// until Save, and Discard drops it.</summary>
        public async Task AddNewItemAsync(Window owner)
        {
            if (!HgEngineProject.IsActive) return;
            if (_pendingItem != null)
            {
                await DialogHelper.ShowError("Save or discard the new item first.", "Add New Item", owner);
                return;
            }
            if ((_dataDirty || _entryDirty) && !await DialogHelper.AskYesNo("There are unsaved changes to the current item. Discard and proceed?", "Unsaved Changes", owner))
                return;

            string name = await DialogHelper.PromptText("New item's display name:", "Add New Item", owner: owner);
            if (name == null) return;

            ItemData data = new ItemData(new MemoryStream(new byte[ItemDataSize]), 0);
            if (!HgEngineItemExpansion.TryPrepareItem(name, out HgEngineItemExpansion.PendingItem pending, out string error)
                || !HgEngineItemExpansion.TryReadTemplate(pending, data, out error))
            {
                await DialogHelper.ShowError($"Could not add the item:\n{error}", "Add New Item", owner);
                return;
            }
            data.ID = pending.Id;

            _returnIndex = _selectedItemIndex;
            _pendingItem = pending;
            SyncNames();

            _isLoading = true;
            try
            {
                _selectedItemIndex = pending.Id;
                OnPropertyChanged(nameof(SelectedItemIndex));
                // hg-engine's table entry follows from the id, so the add sets none of its fields.
                _currentEntry = ReadTableEntry(pending.Id);
                RefreshEntryBoundProps();
                _currentData = data;
                SourceLoadError = null;
                PopulateFromCurrentData();
                UpdateIcon();
                _dataDirty = _entryDirty = false;
                _history.Reset(Snapshot());
                _lastCaptureUtc = DateTime.MinValue;
            }
            finally { _isLoading = false; }
            OnPropertyChanged(nameof(HasUnsavedChanges));
            RaiseUndoState();
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
            UpdateSharedDataNote(_selectedItemIndex);
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

        private string _sharedDataNote = "";
        /// <summary>Set when other items read the same item data, which PlatPatches' expanded items often do.</summary>
        public string SharedDataNote { get => _sharedDataNote; private set { _sharedDataNote = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSharedDataNote)); } }
        public bool HasSharedDataNote => _sharedDataNote.Length > 0;

        // Follows the data id being edited, not the saved one, so pointing an item elsewhere updates it at once.
        private void UpdateSharedDataNote(int id)
        {
            if (RomInfo.isHGE || PlatPatches.Items() == null) { SharedDataNote = ""; return; }
            List<int> others = ItemTable.SharingData(id, (int)_currentEntry.itemData, ItemNames.Count);
            if (others.Count == 0) { SharedDataNote = ""; return; }
            string names = string.Join(", ", others.Take(4).Select(i => i < ItemNames.Count ? ItemNames[i] : $"Item {i}"));
            SharedDataNote = $"Shares its item data with {names}{(others.Count > 4 ? $" and {others.Count - 4} more" : "")}; editing it changes those too.";
        }

        // ── Load ──────────────────────────────────────────────────────────────
        /// <summary>False, with nothing changed, when the item's row or data can't be read.</summary>
        private bool LoadFile(int id)
        {
            ItemNarcTableEntry entry;
            ItemData data;
            string loadError;
            try
            {
                entry = ReadTableEntry(id);
                (data, loadError) = ReadItemData((int)entry.itemData);
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Item Editor: item {id} could not be read: {ex}");
                _ = DialogHelper.ShowError($"Item {id} could not be read.\n{ex.Message}", "Item Editor");
                return false;
            }

            _isLoading = true;
            try
            {
                _currentEntry = entry;
                RefreshEntryBoundProps();
                UpdateSharedDataNote(id);

                ApplyItemData(data, loadError);
                UpdateIcon();

                _dataDirty = _entryDirty = false;
                OnPropertyChanged(nameof(HasUnsavedChanges));

                _history.Reset(Snapshot());   // loaded state is the clean undo baseline for this item
                _lastCaptureUtc = DateTime.MinValue;
                RaiseUndoState();
            }
            finally { _isLoading = false; }
            return true;
        }

        private const int ItemDataSize = 36;

        private static (ItemData data, string loadError) ReadItemData(int dataId)
        {
            string path = Path.Combine(RomInfo.gameDirs[DirNames.itemData].unpackedDir, dataId.ToString("D4"));
            if (!HgEngineProject.IsActive) return (ReadBuiltItemData(path, dataId), null);
            // The built copy only fills a field the entry lacks, which the save then reports.
            ItemData data = File.Exists(path) ? ReadBuiltItemData(path, dataId) : new ItemData(new MemoryStream(new byte[ItemDataSize]), dataId);
            HgEngineItemSource.TryLoad(dataId, data, out string loadError);
            return (data, loadError);
        }

        private void ApplyItemData(ItemData data, string loadError)
        {
            _currentData = data;
            SourceLoadError = loadError;
            PopulateFromCurrentData();
        }

        private static ItemData ReadBuiltItemData(string path, int dataId)
        {
            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            return new ItemData(stream, dataId);
        }

        private void PopulateFromCurrentData()
        {
            // Hold effect: combo bound by index == byte value; extend the list if the value is beyond the
            // known labels (a hacked/undefined effect) so it still has a slot.
            // Resynced before the index is set, since shrinking the list can push -1 through the binding.
            ResetAndCover(HoldEffectNames, HoldEffectKey, (int)_currentData.holdEffect);
            _holdEffectIndex = (int)_currentData.holdEffect;
            _holdEffectParam = _currentData.HoldEffectParam;
            OnPropertyChanged(nameof(HoldEffectIndex));
            OnPropertyChanged(nameof(HoldEffectParam));

            // Field pocket
            _fieldPocketIndex = (int)_currentData.fieldPocket;
            EnsureCovers(FieldPocketNames, "item_field_pockets", _fieldPocketIndex);
            OnPropertyChanged(nameof(FieldPocketIndex));

            // Battle pocket flags
            BattlePocket bp = _currentData.battlePocket;
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
            _price = _currentData.FullPrice;
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

            // Functions: combos bound by index == byte value; extend lists for unknown/raw values.
            ResetAndCover(FieldUseFuncNames, FieldUseKey, (int)_currentData.fieldUseFunc);
            _fieldUseFuncIndex = (int)_currentData.fieldUseFunc;
            OnPropertyChanged(nameof(FieldUseFuncIndex));

            _battleUseFuncIndex = (int)_currentData.battleUseFunc;
            EnsureCovers(BattleUseFuncNames, "item_battle_use", _battleUseFuncIndex);
            OnPropertyChanged(nameof(BattleUseFuncIndex));

            // Party params
            ItemPartyUseParam p = _currentData.PartyUseParam;
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

            foreach (string name in _partyPropNames) OnPropertyChanged(name);
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
        // hg-engine rows follow from the id; a newly added item with no compiled icon yet shows "n/a" (see UpdateIcon).
        private static ItemNarcTableEntry ReadTableEntry(int index) => DSPRE.ROMFiles.ItemTable.Read(index);

        private void SaveTableEntry()
        {
            if (RomInfo.isHGE)
            {
                // hg-engine has no item table to write (see ItemTable.Read). Item data itself still saves
                // normally (SaveItemData, keyed directly by item id).
                _entryDirty = false;
                OnPropertyChanged(nameof(HasUnsavedChanges));
                return;
            }

            DSPRE.ROMFiles.ItemTable.Write(_selectedItemIndex, _currentEntry);
            _entryDirty = false;
            SaveNotice.Saved(UnsavedChangesDescription);
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        /// <summary>False when hg-engine's source couldn't take the edit; the item then stays unsaved.</summary>
        private async System.Threading.Tasks.Task<bool> SaveItemDataAsync()
        {
            if (_currentData == null) return true;
            int item = (int)_currentEntry.itemData;
            ItemData data = _currentData;
            if (HgEngineProject.IsActive)
            {
                if (SourceLoadError != null)
                {
                    await DialogHelper.ShowError($"Item {item} was not saved.\n{SourceLoadError}", "Item Editor");
                    return false;
                }
                // The source is what the next sync rebuilds from, so a save that can't reach it is no save.
                (bool saved, string error) = await HgEngineSave.RunAsync(() => HgEngineItemSource.TryWrite(item, data, out string writeError) ? null : writeError);
                if (!saved)
                {
                    if (error != null) await DialogHelper.ShowError($"Item {item} was not saved.\n{error}", "Item Editor");
                    return false;
                }
            }
            data.SaveToFileDefaultDir(item, false);
            _dataDirty = false;
            SaveNotice.Saved(UnsavedChangesDescription);
            OnPropertyChanged(nameof(HasUnsavedChanges));
            return true;
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
                NCLR palette = new NCLR(Path.Combine(dir, palFile), (int)_currentEntry.itemPalette, palFile);
                NCGR image   = new NCGR(Path.Combine(dir, imgFile), (int)_currentEntry.itemIcon,    imgFile);
                NCER sprite  = new NCER(Path.Combine(dir, "0001"),  2, "0001");
                RawImage raw     = sprite.Get_RawImage(image, palette, 0, image.Width, image.Height, trans: true, currOAM: -1, draw_index: null);
                ItemIcon = ImageConverter.ToAvaloniaBitmap(raw);
            }
            catch (Exception ex) { AppLogger.Error("UpdateIcon: " + ex); ItemIcon = null; }
        }

        private void PopulateIconPaletteDropdowns()
        {
            string dir = RomInfo.gameDirs[DirNames.itemIcons].unpackedDir;
            string[] files  = Directory.GetFiles(dir, "*", SearchOption.TopDirectoryOnly);
            uint idx   = 0;
            foreach (string file in files)
            {
                using FileStream stream = File.OpenRead(file);
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
            DSPRE.Avalonia.Data.LabelStore.Sync(HoldEffectNames,      HoldEffectKey);
            DSPRE.Avalonia.Data.LabelStore.Sync(FieldPocketNames,     "item_field_pockets");
            DSPRE.Avalonia.Data.LabelStore.Sync(FieldUseFuncNames,    FieldUseKey);
            DSPRE.Avalonia.Data.LabelStore.Sync(BattleUseFuncNames,   "item_battle_use");
            DSPRE.Avalonia.Data.LabelStore.Sync(NaturalGiftTypeNames, "item_natural_gift");
            AppEvents.LabelsChanged -= OnLabelsChanged; AppEvents.LabelsChanged += OnLabelsChanged;
        }

        // DP has no GiratinaBoost and a shorter field-use table than Pt, which is shorter than HGSS's.
        private static string HoldEffectKey => RomInfo.gameFamily == GameFamilies.DP ? "item_hold_effects_dp" : "item_hold_effects";
        private static string FieldUseKey => RomInfo.gameFamily switch
        {
            GameFamilies.DP => "item_field_use_dp",
            GameFamilies.Plat => "item_field_use_pt",
            _ => "item_field_use",
        };

        /// <summary>Back to the game's own labels, plus a slot for a value read beyond them.</summary>
        private static void ResetAndCover(System.Collections.ObjectModel.ObservableCollection<string> coll, string key, int index)
        {
            if (coll.Count > DSPRE.Avalonia.Data.LabelStore.Count(key)) DSPRE.Avalonia.Data.LabelStore.Sync(coll, key);
            EnsureCovers(coll, key, index);
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
                EnsureCovers(HoldEffectNames,      HoldEffectKey,        (int)_currentData.holdEffect);
                EnsureCovers(FieldPocketNames,     "item_field_pockets", (int)_currentData.fieldPocket);
                EnsureCovers(FieldUseFuncNames,    FieldUseKey,          (int)_currentData.fieldUseFunc);
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
