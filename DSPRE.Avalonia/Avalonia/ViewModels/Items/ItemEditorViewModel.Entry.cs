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
        public bool TableEntryEditable => !RomInfo.IsHGE;
        public string TableEntryNote => RomInfo.IsHGE ? "hg-engine finds an item's data and icon by its id." : null;

        private string _selectedIconImage;
        public string SelectedIconImage
        {
            get => _selectedIconImage;
            set
            {
                if (!Set(ref _selectedIconImage, value)) return;
                if (_isLoading || value == null) return;
                _currentEntry.ItemIcon = uint.Parse(value);
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
                _currentEntry.ItemPalette = uint.Parse(value);
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
                // A new item can't use other data until it's saved.
                if (_pendingItem != null && !_isLoading && value != _itemDataId) { OnPropertyChanged(); return; }
                // Switching data drops unsaved edits, so ask first.
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
                AppLogger.Error($"Item editor: item data {dataId} couldn't be read: {ex}");
                _itemDataId = (int)_currentEntry.ItemData;
                OnPropertyChanged(nameof(ItemDataId));
                return;
            }
            _currentEntry.ItemData = (uint)dataId;
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
        // A new hg-engine item has no built icon yet.
        public bool IconUnavailable => ItemIcon == null && RomInfo.IsHGE;
    }
}
