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
        private int _holdEffectIndex;
        public int HoldEffectIndex
        {
            get => _holdEffectIndex;
            set
            {
                if (!Set(ref _holdEffectIndex, value)) return;
                if (_isLoading || _currentData == null || value < 0) return;
                _currentData.HoldEffect = (HoldEffect)value;
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

        private int _fieldPocketIndex;
        public int FieldPocketIndex
        {
            get => _fieldPocketIndex;
            set
            {
                if (!Set(ref _fieldPocketIndex, value)) return;
                if (_isLoading || _currentData == null || value < 0) return;
                _currentData.FieldPocket = (FieldPocket)value;
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
            _currentData.BattlePocket = bp;
        }

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

        private int _price;
        public int Price
        {
            get => _price;
            set { if (Set(ref _price, value) && !_isLoading && _currentData != null) { _currentData.FullPrice = value; SetDataDirty(); } }
        }
        // hg-engine's item record adds price_high, so prices there run to 20 bits.
        public int PriceMaximum => RomInfo.IsHGE ? ItemData.MaxHgEnginePrice : ushort.MaxValue;

        private int _naturalGiftTypeIndex;
        public int NaturalGiftTypeIndex
        {
            get => _naturalGiftTypeIndex;
            set
            {
                if (!Set(ref _naturalGiftTypeIndex, value)) return;
                if (_isLoading || _currentData == null || value < 0) return;
                _currentData.NaturalGiftType = (NaturalGiftType)value;
                SetDataDirty();
            }
        }

        private int _naturalGiftPower;
        public int NaturalGiftPower { get => _naturalGiftPower; set { if (Set(ref _naturalGiftPower, value) && !_isLoading && _currentData != null) { _currentData.NaturalGiftPower = (byte)value; SetDataDirty(); } } }

        public ObservableCollection<string> FlingEffectNames { get; } = new();
        public ObservableCollection<string> PluckEffectNames { get; } = new();

        private static void CoverEffects(ObservableCollection<string> names, string[] known, int value)
        {
            int count = Math.Max(known.Length, value + 1);
            while (names.Count > count) names.RemoveAt(names.Count - 1);
            for (int i = names.Count; i < count; i++) names.Add($"{i}: {(i < known.Length ? known[i] : "does nothing")}");
        }

        private int _flingEffect;
        public int FlingEffect      { get => _flingEffect;      set { if (value >= 0 && Set(ref _flingEffect, value) && !_isLoading && _currentData != null) { _currentData.FlingEffect = (byte)value; SetDataDirty(); } } }

        private int _flingPower;
        public int FlingPower       { get => _flingPower;       set { if (Set(ref _flingPower,       value) && !_isLoading && _currentData != null) { _currentData.FlingPower       = (byte)value; SetDataDirty(); } } }

        private int _pluckEffect;
        public int PluckEffect      { get => _pluckEffect;      set { if (value >= 0 && Set(ref _pluckEffect, value) && !_isLoading && _currentData != null) { _currentData.PluckEffect = (byte)value; SetDataDirty(); } } }

        private int _fieldUseFuncIndex;
        public int FieldUseFuncIndex
        {
            get => _fieldUseFuncIndex;
            set
            {
                if (!Set(ref _fieldUseFuncIndex, value)) return;
                if (_isLoading || _currentData == null || value < 0) return;
                _currentData.FieldUseFunc = (FieldUseFunc)value;
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
                _currentData.BattleUseFunc = (BattleUseFunc)value;
                SetDataDirty();
            }
        }
    }
}
