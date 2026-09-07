using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using DSPRE.Editors;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.ViewModels.Items
{
    public sealed class MartItemRowVM : INotifyPropertyChanged
    {
        private readonly Func<ushort> _getItem;
        private readonly Action<ushort> _setItem;
        private readonly Func<ushort> _getTier;
        private readonly Action<ushort> _setTier;
        private readonly Action _changed;

        public int Slot { get; }
        public string[] ItemNames { get; }
        public bool HasTier => _getTier != null;

        public int ItemId
        {
            get => _getItem();
            set
            {
                if (value < 0 || value >= ItemNames.Length || value > ushort.MaxValue || value == _getItem()) return;
                _setItem((ushort)value);
                Notify();
                _changed();
            }
        }

        public int RequiredTier
        {
            get => _getTier?.Invoke() ?? 1;
            set
            {
                if (_getTier == null || value < 1 || value > 6 || value == _getTier()) return;
                _setTier((ushort)value);
                Notify();
                _changed();
            }
        }

        internal MartItemRowVM(int slot, string[] itemNames, Func<ushort> getItem,
            Action<ushort> setItem, Action changed, Func<ushort> getTier = null,
            Action<ushort> setTier = null)
        {
            Slot = slot;
            ItemNames = itemNames;
            _getItem = getItem;
            _setItem = setItem;
            _getTier = getTier;
            _setTier = setTier;
            _changed = changed;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void Notify([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed class MartShopVM
    {
        public string Name { get; }
        public string Kind { get; }
        public ObservableCollection<MartItemRowVM> Items { get; } = new();
        public string CountLabel => $"{Items.Count} items";
        internal MartData.SpecialtyShop SpecialtySource { get; }
        internal bool IsCommon => SpecialtySource == null;

        internal MartShopVM(string name, string kind, MartData.SpecialtyShop specialtySource = null)
        {
            Name = name;
            Kind = kind;
            SpecialtySource = specialtySource;
        }

    }

    public sealed class MartEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        private readonly MartData _data;
        private readonly string[] _itemNames;
        private bool _dirty;
        private MartShopVM _selectedShop;

        public ObservableCollection<MartShopVM> Shops { get; } = new();

        public MartShopVM SelectedShop
        {
            get => _selectedShop;
            set
            {
                if (_selectedShop == value) return;
                _selectedShop = value;
                Notify();
                Notify(nameof(SelectedShopDescription));
                Notify(nameof(NewShopDisplayGuide));
                Notify(nameof(CanRemoveCustomShop));
                Notify(nameof(CanAddItem));
                Notify(nameof(CanRemoveItem));
            }
        }

        public string SelectedShopDescription => SelectedShop == null
            ? ""
            : SelectedShop.Kind + ", " + SelectedShop.CountLabel;

        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => "Mart inventories";
        public bool CanResize => _data?.ExpansionAvailable == true;
        public bool CanAddItem => CanResize && SelectedShop != null
            && (!SelectedShop.IsCommon || SelectedShop.Items.Count < 63);
        public bool CanRemoveItem => CanResize && SelectedShop?.Items.Count > 1;
        public bool CanRemoveCustomShop => CanResize && SelectedShop?.SpecialtySource?.IsCustom == true
            && SelectedShop.SpecialtySource.Id == _data.SpecialtyShops.Count - 1;
        public string ResizeStatus => CanResize
            ? "ARM9 expansion detected. Inventory resizing and custom marts are available."
            : "Apply the ARM9 expansion patch to add or remove inventory slots or custom marts.";
        public string NewShopDisplayGuide => SelectedShop?.SpecialtySource?.IsCustom == true
            ? $"To display this mart, call SpMartScreen {SelectedShop.SpecialtySource.Id} from your script. DSPRE does not modify scripts automatically."
            : "Custom marts receive a new SpMartScreen ID. You remain responsible for calling that ID from an event script.";

        public MartEditorViewModel()
        {
            if (!Design.IsDesignMode) return;
            var sample = new MartShopVM("Common Mart", "Stock unlocked as the story advances");
            Shops.Add(sample);
            SelectedShop = sample;
        }

        public MartEditorViewModel(MartData data, string[] itemNames)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _itemNames = itemNames ?? Array.Empty<string>();
            PopulateShops();
        }

        private void PopulateShops(int selectedIndex = 0)
        {
            Shops.Clear();
            var common = new MartShopVM("Common Mart", "Stock unlocked as the story advances");
            for (int i = 0; i < _data.CommonItems.Count; i++)
            {
                MartData.CommonEntry entry = _data.CommonItems[i];
                common.Items.Add(new MartItemRowVM(i + 1, _itemNames,
                    () => entry.ItemId,
                    value => entry.ItemId = value,
                    SetDirty,
                    () => entry.RequiredTier,
                    value => entry.RequiredTier = value));
            }
            Shops.Add(common);

            foreach (MartData.SpecialtyShop source in _data.SpecialtyShops)
            {
                var shop = new MartShopVM(source.Name, $"Specialty Mart ID {source.Id}", source);
                for (int i = 0; i < source.Items.Count; i++)
                {
                    int index = i;
                    shop.Items.Add(new MartItemRowVM(i + 1, _itemNames,
                        () => source.Items[index],
                        value => source.Items[index] = value,
                        SetDirty));
                }
                Shops.Add(shop);
            }

            SelectedShop = Shops.Count > 0 ? Shops[Math.Clamp(selectedIndex, 0, Shops.Count - 1)] : null;
        }

        public void AddItem()
        {
            if (!CanAddItem) return;
            int selected = Shops.IndexOf(SelectedShop);
            if (SelectedShop.IsCommon)
                _data.CommonItems.Add(new MartData.CommonEntry { ItemId = 1, RequiredTier = 1 });
            else
                SelectedShop.SpecialtySource.Items.Add(1);
            PopulateShops(selected);
            SetDirty();
        }

        public void RemoveLastItem()
        {
            if (!CanRemoveItem) return;
            int selected = Shops.IndexOf(SelectedShop);
            if (SelectedShop.IsCommon)
                _data.CommonItems.RemoveAt(_data.CommonItems.Count - 1);
            else
                SelectedShop.SpecialtySource.Items.RemoveAt(SelectedShop.SpecialtySource.Items.Count - 1);
            PopulateShops(selected);
            SetDirty();
        }

        public void AddShop()
        {
            if (!CanResize) return;
            MartData.SpecialtyShop added = _data.AddSpecialtyShop();
            PopulateShops(added.Id + 1);
            SetDirty();
        }

        public void RemoveCustomShop()
        {
            if (!CanRemoveCustomShop) return;
            int nextSelection = Math.Max(0, Shops.Count - 2);
            _data.RemoveLastSpecialtyShop();
            PopulateShops(nextSelection);
            SetDirty();
        }

        public void SaveChanges()
        {
            if (!_dirty || _data == null) return;
            if (!_data.SaveCurrent()) return;
            SetClean();
            SaveNotice.Saved(UnsavedChangesDescription);
        }

        public void DiscardChanges()
        {
            _dirty = false;
            Notify(nameof(HasUnsavedChanges));
        }

        private void SetDirty()
        {
            if (_dirty) return;
            _dirty = true;
            Notify(nameof(HasUnsavedChanges));
        }

        private void SetClean()
        {
            if (!_dirty) return;
            _dirty = false;
            Notify(nameof(HasUnsavedChanges));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void Notify([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
