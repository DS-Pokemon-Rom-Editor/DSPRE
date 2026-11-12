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
        // False when the row or data can't be read.
        private bool LoadFile(int id)
        {
            ItemNarcTableEntry entry;
            ItemData data;
            string loadError;
            try
            {
                entry = ReadTableEntry(id);
                (data, loadError) = ReadItemData((int)entry.ItemData);
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Item editor: item {id} couldn't be read: {ex}");
                _ = DialogHelper.ShowErrorAsync($"Item {id} couldn't be read.\n{ex.Message}", "Item editor");
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

                _history.Reset(Snapshot());
                _lastCaptureUtc = DateTime.MinValue;
                RaiseUndoState();
            }
            finally { _isLoading = false; }
            return true;
        }

        private const int ItemDataSize = 36;

        private static (ItemData data, string loadError) ReadItemData(int dataId)
        {
            if (!HgEngineProject.IsActive) return (ReadBuiltItemData(dataId), null);
            // The built copy only fills fields missing from the entry. Save reports them.
            ItemData data = ArchiveMembers.Has(DirNames.ItemData, dataId) ? ReadBuiltItemData(dataId) : new ItemData(new MemoryStream(new byte[ItemDataSize]), dataId);
            HgEngineItemSource.TryLoad(dataId, data, out string loadError);
            return (data, loadError);
        }

        private void ApplyItemData(ItemData data, string loadError)
        {
            _currentData = data;
            SourceLoadError = loadError;
            PopulateFromCurrentData();
        }

        private static ItemData ReadBuiltItemData(int dataId) =>
            new ItemData(new MemoryStream(ArchiveMembers.Read(DirNames.ItemData, dataId)), dataId);

        private void PopulateFromCurrentData()
        {
            // Resynced before the index is set, since shrinking the list can push -1 through the binding.
            ResetAndCover(HoldEffectNames, HoldEffectKey, (int)_currentData.HoldEffect);
            _holdEffectIndex = (int)_currentData.HoldEffect;
            _holdEffectParam = _currentData.HoldEffectParam;
            OnPropertyChanged(nameof(HoldEffectIndex));
            OnPropertyChanged(nameof(HoldEffectParam));

            _fieldPocketIndex = (int)_currentData.FieldPocket;
            EnsureCovers(FieldPocketNames, "item_field_pockets", _fieldPocketIndex);
            OnPropertyChanged(nameof(FieldPocketIndex));

            BattlePocket bp = _currentData.BattlePocket;
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

            _preventToss = _currentData.PreventToss;
            _selectable  = _currentData.Selectable;
            _partyUse    = _currentData.PartyUse == 1;
            OnPropertyChanged(nameof(PreventToss));
            OnPropertyChanged(nameof(Selectable));
            OnPropertyChanged(nameof(PartyUse));
            OnPropertyChanged(nameof(PartyParamsEnabled));

            _price = _currentData.FullPrice;
            OnPropertyChanged(nameof(Price));

            _naturalGiftTypeIndex = (int)_currentData.NaturalGiftType;
            EnsureCovers(NaturalGiftTypeNames, "item_natural_gift", _naturalGiftTypeIndex);
            _naturalGiftPower    = _currentData.NaturalGiftPower;
            _flingEffect         = _currentData.FlingEffect;
            _flingPower          = _currentData.FlingPower;
            _pluckEffect         = _currentData.PluckEffect;
            CoverEffects(FlingEffectNames, ItemLabels.FlingEffects, _flingEffect);
            CoverEffects(PluckEffectNames, ItemLabels.PluckEffects, _pluckEffect);
            OnPropertyChanged(nameof(NaturalGiftTypeIndex));
            OnPropertyChanged(nameof(NaturalGiftPower));
            OnPropertyChanged(nameof(FlingEffect));
            OnPropertyChanged(nameof(FlingPower));
            OnPropertyChanged(nameof(PluckEffect));

            ResetAndCover(FieldUseFuncNames, FieldUseKey, (int)_currentData.FieldUseFunc);
            _fieldUseFuncIndex = (int)_currentData.FieldUseFunc;
            OnPropertyChanged(nameof(FieldUseFuncIndex));

            _battleUseFuncIndex = (int)_currentData.BattleUseFunc;
            EnsureCovers(BattleUseFuncNames, "item_battle_use", _battleUseFuncIndex);
            OnPropertyChanged(nameof(BattleUseFuncIndex));

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

            foreach (string name in PartyPropNames) OnPropertyChanged(name);
        }

        private static readonly string[] PartyPropNames =
        {
            nameof(SlpHeal),   nameof(PsnHeal),   nameof(BrnHeal),   nameof(FrzHeal),
            nameof(PrzHeal),   nameof(CfsHeal),   nameof(InfHeal),   nameof(GuardSpec),
            nameof(Revive),    nameof(ReviveAll),  nameof(LevelUp),   nameof(Evolve),
            nameof(AtkStages), nameof(DefStages),  nameof(SpAtkStages),  nameof(SpDefStages),
            nameof(SpeedStages), nameof(AccuracyStages), nameof(CritRateStages),
            nameof(HpRestore), nameof(HpRestoreParam), nameof(PpRestore), nameof(PpRestoreParam),
            nameof(HpAmountMode), nameof(HpAmountIsExact), nameof(PpAmountMode), nameof(PpAmountIsExact),
            nameof(PpUps),     nameof(PpMax),      nameof(PpRestoreAll),
            nameof(EVHp),      nameof(EVAtk),      nameof(EVDef),     nameof(EVSpeed),
            nameof(EVSpAtk),   nameof(EVSpDef),
            nameof(EVHpValue), nameof(EVAtkValue), nameof(EVDefValue), nameof(EVSpeedValue),
            nameof(EVSpAtkValue), nameof(EVSpDefValue),
            nameof(FriendshipLow),  nameof(FriendshipMid),  nameof(FriendshipHigh),
            nameof(FriendshipLowValue), nameof(FriendshipMidValue), nameof(FriendshipHighValue)
        };
    }
}
