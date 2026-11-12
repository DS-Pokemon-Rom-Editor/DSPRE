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
        private void UpdateIcon()
        {
            if (Design.IsDesignMode) return;
            try
            {
                byte[] drawing = ArchiveMembers.Read(DirNames.ItemIcons, (int)_currentEntry.ItemIcon);
                (int width, int height) = NitroBgCodec.ReadDrawingHeader(drawing) is NitroBgCodec.DrawingHeader head ? NitroBgCodec.DrawingSize(head) : (32, 32);
                RawImage raw = CellRenderer.Draw(NcerFile.Read(ArchiveMembers.Read(DirNames.ItemIcons, 1)), 0, drawing,
                    NitroBgCodec.ReadColours(ArchiveMembers.Read(DirNames.ItemIcons, (int)_currentEntry.ItemPalette)), width, height);
                ItemIcon = ImageConverter.ToAvaloniaBitmap(raw);
            }
            catch (Exception ex) { AppLogger.Error("UpdateIcon: " + ex); ItemIcon = null; }
        }

        private void PopulateIconPaletteDropdowns()
        {
            foreach (int id in ArchiveMembers.Ids(DirNames.ItemIcons))
            {
                string magic = ArchiveMembers.Magic(DirNames.ItemIcons, id);
                if      (magic == "RGCN") IconImages.Add(id.ToString("D4"));
                else if (magic == "RLCN") IconPalettes.Add(id.ToString("D4"));
            }
        }

        // Enum boxes bind by index as the raw byte.
        private void PopulateEnumCollections()
        {
            LabelStore.Sync(HoldEffectNames,      HoldEffectKey);
            LabelStore.Sync(FieldPocketNames,     "item_field_pockets");
            LabelStore.Sync(FieldUseFuncNames,    FieldUseKey);
            LabelStore.Sync(BattleUseFuncNames,   "item_battle_use");
            LabelStore.Sync(NaturalGiftTypeNames, "item_natural_gift");
            AppEvents.LabelsChanged -= OnLabelsChanged; AppEvents.LabelsChanged += OnLabelsChanged;
            _iconSaved ??= AppEvents.WhenSaved(UpdateIcon, DirNames.ItemIcons);
        }

        private IDisposable _iconSaved;

        // DP has no GiratinaBoost, so it gets its own hold effect list.
        private static string HoldEffectKey => RomInfo.GameFamily == GameFamilies.DP ? "item_hold_effects_dp" : "item_hold_effects";
        private static string FieldUseKey => RomInfo.GameFamily switch
        {
            GameFamilies.DP => "item_field_use_dp",
            GameFamilies.Plat => "item_field_use_pt",
            _ => "item_field_use",
        };

        private static void ResetAndCover(System.Collections.ObjectModel.ObservableCollection<string> coll, string key, int index)
        {
            if (coll.Count > LabelStore.Count(key)) LabelStore.Sync(coll, key);
            EnsureCovers(coll, key, index);
        }

        private static void EnsureCovers(System.Collections.ObjectModel.ObservableCollection<string> coll, string key, int index)
        {
            while (coll.Count <= index && coll.Count < 256)
                coll.Add(LabelStore.GetLabel(key, coll.Count));
        }

        private void OnLabelsChanged(object sender, EventArgs e)
        {
            PopulateEnumCollections();
            if (_currentData != null)
            {
                EnsureCovers(HoldEffectNames,      HoldEffectKey,        (int)_currentData.HoldEffect);
                EnsureCovers(FieldPocketNames,     "item_field_pockets", (int)_currentData.FieldPocket);
                EnsureCovers(FieldUseFuncNames,    FieldUseKey,          (int)_currentData.FieldUseFunc);
                EnsureCovers(BattleUseFuncNames,   "item_battle_use",    (int)_currentData.BattleUseFunc);
                EnsureCovers(NaturalGiftTypeNames, "item_natural_gift",  (int)_currentData.NaturalGiftType);
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

        public void Detach() { AppEvents.LabelsChanged -= OnLabelsChanged; _iconSaved?.Dispose(); _iconSaved = null; }

        private static int GetItemDataFileCount() =>
            NarcAPI.Narc.MemberCount(RomInfo.GameDirs[DirNames.ItemData].unpackedDir);
    }
}
