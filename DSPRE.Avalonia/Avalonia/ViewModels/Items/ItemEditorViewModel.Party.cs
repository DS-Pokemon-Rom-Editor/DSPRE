using Avalonia.Controls;
using Avalonia.Platform.Storage;
using DSPRE;
using DSPRE.Avalonia;
using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
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
    public partial class ItemEditorViewModel
    {
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

        private int _atkStages;      public int AtkStages      { get => _atkStages;      set { if (Set(ref _atkStages,      value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.AtkStages      = value; SetDataDirty(); } } }
        private int _defStages;      public int DefStages      { get => _defStages;      set { if (Set(ref _defStages,      value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.DefStages      = value; SetDataDirty(); } } }
        private int _spAtkStages;    public int SpAtkStages    { get => _spAtkStages;    set { if (Set(ref _spAtkStages,    value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.SpAtkStages    = value; SetDataDirty(); } } }
        private int _spDefStages;    public int SpDefStages    { get => _spDefStages;    set { if (Set(ref _spDefStages,    value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.SpDefStages    = value; SetDataDirty(); } } }
        private int _speedStages;    public int SpeedStages    { get => _speedStages;    set { if (Set(ref _speedStages,    value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.SpeedStages    = value; SetDataDirty(); } } }
        private int _accuracyStages; public int AccuracyStages { get => _accuracyStages; set { if (Set(ref _accuracyStages, value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.AccuracyStages = value; SetDataDirty(); } } }
        private int _critRateStages; public int CritRateStages { get => _critRateStages; set { if (Set(ref _critRateStages, value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.CritRateStages = value; SetDataDirty(); } } }

        private bool _hpRestore;    public bool HpRestore    { get => _hpRestore;    set { if (Set(ref _hpRestore,    value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.HPRestore    = value; SetDataDirty(); } } }
        private int  _hpRestoreParam;
        public int HpRestoreParam
        {
            get => _hpRestoreParam;
            set
            {
                if (!Set(ref _hpRestoreParam, value)) return;
                if (!_isLoading && _currentData != null) { _currentData.PartyUseParam.HPRestoreParam = (byte)value; SetDataDirty(); }
                OnPropertyChanged(nameof(HpAmountMode)); OnPropertyChanged(nameof(HpAmountIsExact));
            }
        }

        // HP 255, 254 and 253 mean full, half and a quarter; PP 127 means full (item_use_pokemon.c).
        private const int FullHp = 255, HalfHp = 254, QuarterHp = 253, FullPp = 127;
        public string[] HpAmountModes { get; } = { "Full", "Half", "Quarter", "Exact" };
        public string[] PpAmountModes { get; } = { "Full", "Exact" };

        public int HpAmountMode
        {
            get => _hpRestoreParam switch { FullHp => 0, HalfHp => 1, QuarterHp => 2, _ => 3 };
            set
            {
                if (value < 0 || value == HpAmountMode) return;
                HpRestoreParam = value switch { 0 => FullHp, 1 => HalfHp, 2 => QuarterHp, _ => 20 };
            }
        }
        public bool HpAmountIsExact => HpAmountMode == 3;

        public int PpAmountMode
        {
            get => _ppRestoreParam == FullPp ? 0 : 1;
            set
            {
                if (value < 0 || value == PpAmountMode) return;
                PpRestoreParam = value == 0 ? FullPp : 10;
            }
        }
        public bool PpAmountIsExact => PpAmountMode == 1;
        private bool _ppRestore;    public bool PpRestore    { get => _ppRestore;    set { if (Set(ref _ppRestore,    value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.PPRestore    = value; SetDataDirty(); } } }
        private int  _ppRestoreParam;
        public int PpRestoreParam
        {
            get => _ppRestoreParam;
            set
            {
                if (!Set(ref _ppRestoreParam, value)) return;
                if (!_isLoading && _currentData != null) { _currentData.PartyUseParam.PPRestoreParam = (byte)value; SetDataDirty(); }
                OnPropertyChanged(nameof(PpAmountMode)); OnPropertyChanged(nameof(PpAmountIsExact));
            }
        }
        private bool _ppUps;        public bool PpUps        { get => _ppUps;        set { if (Set(ref _ppUps,        value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.PPUps        = value; SetDataDirty(); } } }
        private bool _ppMax;        public bool PpMax        { get => _ppMax;        set { if (Set(ref _ppMax,        value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.PPMax        = value; SetDataDirty(); } } }
        private bool _ppRestoreAll; public bool PpRestoreAll { get => _ppRestoreAll; set { if (Set(ref _ppRestoreAll, value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.PPRestoreAll = value; SetDataDirty(); } } }

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

        private bool _friendshipLow;  public bool FriendshipLow  { get => _friendshipLow;  set { if (Set(ref _friendshipLow,  value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.FriendshipLow  = value; SetDataDirty(); } } }
        private bool _friendshipMid;  public bool FriendshipMid  { get => _friendshipMid;  set { if (Set(ref _friendshipMid,  value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.FriendshipMid  = value; SetDataDirty(); } } }
        private bool _friendshipHigh; public bool FriendshipHigh { get => _friendshipHigh; set { if (Set(ref _friendshipHigh, value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.FriendshipHigh = value; SetDataDirty(); } } }

        private int _friendshipLowValue;  public int FriendshipLowValue  { get => _friendshipLowValue;  set { if (Set(ref _friendshipLowValue,  value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.FriendshipLowValue  = (sbyte)value; SetDataDirty(); } } }
        private int _friendshipMidValue;  public int FriendshipMidValue  { get => _friendshipMidValue;  set { if (Set(ref _friendshipMidValue,  value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.FriendshipMidValue  = (sbyte)value; SetDataDirty(); } } }
        private int _friendshipHighValue; public int FriendshipHighValue { get => _friendshipHighValue; set { if (Set(ref _friendshipHighValue, value) && !_isLoading && _currentData != null) { _currentData.PartyUseParam.FriendshipHighValue = (sbyte)value; SetDataDirty(); } } }
    }
}
