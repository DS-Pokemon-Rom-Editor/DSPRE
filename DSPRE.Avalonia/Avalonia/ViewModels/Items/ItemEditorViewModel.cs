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
    public partial class ItemEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo, IDetachable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (EqualityComparer<T>.Default.Equals(f, v)) return false;
            f = v; OnPropertyChanged(n); return true;
        }

        public string HgEngineBanner => DSPRE.HgEngine.HgEngineProject.BannerText;
        public bool ShowHgEngineBanner => HgEngineBanner != null;

        // Save refuses while this is set (itemdata.c entry couldn't be read).
        private string _sourceLoadError;
        public string SourceLoadError { get => _sourceLoadError; private set { if (Set(ref _sourceLoadError, value)) OnPropertyChanged(nameof(HasSourceLoadError)); } }
        public bool HasSourceLoadError => _sourceLoadError != null;

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
            _partyUse            = true;
            _hpRestore           = true;
            _hpRestoreParam      = 50;
            _slpHeal             = true;
            _psnHeal             = true;
        }

        public ItemEditorViewModel(string[] itemNames)
        {
            foreach (string n in itemNames) ItemNames.Add(n);
            PopulateEnumCollections();
            MaxItemIndex  = itemNames.Length - 1;
            MaxItemDataId = GetItemDataFileCount() - 1;
            PopulateIconPaletteDropdowns();

            if (!RomInfo.IsHGE)
            {
                int bad = ItemTable.Limits().FirstBadRow;
                if (bad >= 0) TableLimitNote = $"Items from {bad} on have no valid table row.";
            }

            _selectedItemIndex = 1;
            OnPropertyChanged(nameof(SelectedItemIndex));
            if (!LoadFile(1)) _selectedItemIndex = -1;
        }

        // Set when an ARM9 table row points past the item archives.
        public string TableLimitNote { get; }
        public bool HasTableLimitNote => TableLimitNote != null;

        public ObservableCollection<string> ItemNames          { get; } = new();
        public ObservableCollection<string> IconImages         { get; } = new();
        public ObservableCollection<string> IconPalettes       { get; } = new();
        public ObservableCollection<string> HoldEffectNames    { get; } = new();
        public ObservableCollection<string> FieldPocketNames   { get; } = new();
        public ObservableCollection<string> FieldUseFuncNames  { get; } = new();
        public ObservableCollection<string> BattleUseFuncNames { get; } = new();
        public ObservableCollection<string> NaturalGiftTypeNames { get; } = new();

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
                    // Snap back until the user answers.
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

        private void SwitchTo(int index)
        {
            if (LoadFile(index)) _selectedItemIndex = index;
            OnPropertyChanged(nameof(SelectedItemIndex));
        }

        private bool _isLoading;
        private ItemNarcTableEntry _currentEntry;
        private ItemData _currentData;
    }
}
