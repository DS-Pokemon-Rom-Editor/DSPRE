using System;
using System.Collections.ObjectModel;
using System.Linq;
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
        /// <summary>Vanilla stock tier, or on hg-engine the badges needed (Kanto's count too).</summary>
        public int MaxTier { get; }

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
                if (_getTier == null || value < 0 || value > MaxTier || value == _getTier()) return;
                _setTier((ushort)value);
                Notify();
                _changed();
            }
        }

        internal MartItemRowVM(int slot, string[] itemNames, Func<ushort> getItem,
            Action<ushort> setItem, Action changed, Func<ushort> getTier = null,
            Action<ushort> setTier = null, int maxTier = 6)
        {
            MaxTier = maxTier;
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

    public sealed class MartEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        private MartData _data;
        private readonly string[] _itemNames;
        private byte[] _saved;
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

        public bool HasUnsavedChanges => _data != null && _saved != null && !TakeState().AsSpan().SequenceEqual(_saved);
        public string UnsavedChangesDescription => "Mart inventories";
        public bool CanResize => _data?.ExpansionAvailable == true;
        public bool CanAddItem => CanResize && SelectedShop != null
            && (!SelectedShop.IsCommon || SelectedShop.Items.Count < _data.CommonItemLimit);
        public bool CanAddShop => _data?.CanAddShops == true;
        public bool CanRemoveItem => CanResize && SelectedShop?.Items.Count > 1;
        public bool CanRemoveCustomShop => CanResize && SelectedShop?.SpecialtySource?.IsCustom == true
            && SelectedShop.SpecialtySource.Id == _data.SpecialtyShops.Count - 1;
        public string ResizeStatus => _data?.FromSource == true
            ? $"Edited in hg-engine's {DSPRE.HgEngine.HgEngineMarts.SourceRelPath}. Lists can be any length; the marts themselves are the game's slots."
            : CanResize
            ? "ARM9 expansion detected. Inventory resizing and custom marts are available."
            : "Apply the ARM9 expansion patch to add or remove inventory slots or custom marts.";
        public string NewShopDisplayGuide => _data?.FromSource == true ? ""
            : SelectedShop?.SpecialtySource?.IsCustom == true
            ? $"To display this mart, call SpMartScreen {SelectedShop.SpecialtySource.Id} from your script. DSPRE does not modify scripts automatically."
            : "Custom marts receive a new SpMartScreen ID. You remain responsible for calling that ID from an event script.";

        public MartEditorViewModel()
        {
            if (!Design.IsDesignMode) return;
            MartShopVM sample = new MartShopVM("Common Mart", "Stock unlocked as the story advances");
            Shops.Add(sample);
            SelectedShop = sample;
        }

        public MartEditorViewModel(MartData data, string[] itemNames)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _itemNames = itemNames ?? Array.Empty<string>();
            PopulateShops();
            StartUndo();
        }

        private ByteStateUndo _undo;
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();

        private void StartUndo()
        {
            _saved = TakeState();
            _undo = new ByteStateUndo(TakeState, ApplyState, () => { Notify(nameof(CanUndo)); Notify(nameof(CanRedo)); });
            Notify(nameof(CanUndo)); Notify(nameof(CanRedo)); Notify(nameof(HasUnsavedChanges));
        }

        private byte[] TakeState() => ByteStateUndo.Pack(w =>
        {
            w.Write(_data.CommonItems.Count);
            foreach (MartData.CommonEntry e in _data.CommonItems) { w.Write(e.ItemId); w.Write(e.RequiredTier); }
            w.Write(_data.SpecialtyShops.Count);
            foreach (MartData.SpecialtyShop shop in _data.SpecialtyShops) { w.Write(shop.Items.Count); foreach (ushort item in shop.Items) w.Write(item); }
        });

        // Shows the mart the step changed.
        private void ApplyState(byte[] state)
        {
            byte[] before = TakeState();
            int changed = -1;
            ByteStateUndo.Unpack(state, r =>
            {
                System.Collections.Generic.List<MartData.CommonEntry> common = new System.Collections.Generic.List<MartData.CommonEntry>();
                for (int n = r.ReadInt32(), i = 0; i < n; i++) common.Add(new MartData.CommonEntry { ItemId = r.ReadUInt16(), RequiredTier = r.ReadUInt16() });
                if (common.Count != _data.CommonItems.Count || common.Where((e, i) => e.ItemId != _data.CommonItems[i].ItemId || e.RequiredTier != _data.CommonItems[i].RequiredTier).Any())
                    changed = 0;
                _data.CommonItems.Clear();
                _data.CommonItems.AddRange(common);

                int shops = r.ReadInt32();
                while (_data.SpecialtyShops.Count > shops) _data.RemoveLastSpecialtyShop();
                while (_data.SpecialtyShops.Count < shops) _data.AddSpecialtyShop();
                for (int s = 0; s < shops; s++)
                {
                    System.Collections.Generic.List<ushort> items = new System.Collections.Generic.List<ushort>();
                    for (int n = r.ReadInt32(), i = 0; i < n; i++) items.Add(r.ReadUInt16());
                    System.Collections.Generic.List<ushort> list = _data.SpecialtyShops[s].Items;
                    if (changed < 0 && !list.SequenceEqual(items)) changed = s + 1;
                    list.Clear();
                    list.AddRange(items);
                }
            });
            PopulateShops(changed >= 0 ? changed : Math.Max(0, Shops.IndexOf(SelectedShop)));
            foreach (string n in new[] { nameof(SelectedShopDescription), nameof(CanAddItem), nameof(CanRemoveItem), nameof(CanRemoveCustomShop), nameof(HasUnsavedChanges) })
                Notify(n);
        }

        private void PopulateShops(int selectedIndex = 0)
        {
            Shops.Clear();
            MartShopVM common = new MartShopVM("Common Mart", "Stock unlocked as the story advances");
            for (int i = 0; i < _data.CommonItems.Count; i++)
            {
                MartData.CommonEntry entry = _data.CommonItems[i];
                common.Items.Add(new MartItemRowVM(i + 1, _itemNames,
                    () => entry.ItemId,
                    value => entry.ItemId = value,
                    SetDirty,
                    () => entry.RequiredTier,
                    value => entry.RequiredTier = value, _data.MaxTier));
            }
            Shops.Add(common);

            foreach (MartData.SpecialtyShop source in _data.SpecialtyShops)
            {
                MartShopVM shop = new MartShopVM(source.Name, $"Specialty Mart ID {source.Id}", source);
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
            if (!CanAddShop) return;
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
            if (_data == null || !HasUnsavedChanges) return;
            if (!_data.SaveCurrent()) return;
            _saved = TakeState();
            Notify(nameof(HasUnsavedChanges));
            SaveNotice.Saved(UnsavedChangesDescription);
        }

        /// <summary>Shown while the marts can't grow yet this ROM could take the ARM9 expansion.</summary>
        public bool CanOfferExpansion => !CanResize && PatchToolboxLogic.Arm9ExpansionWhyNot() == null;

        /// <summary>Applies the ARM9 expansion and reloads the marts so they can grow.</summary>
        public async System.Threading.Tasks.Task OfferExpansionAsync()
        {
            if (HasUnsavedChanges)
            {
                await DialogHelper.ShowInfo("Save or discard the mart changes first; the marts reload after the expansion.", "Mart Editor");
                return;
            }
            if (!await Arm9ExpansionOffer.EnsureAsync("Adding mart slots or custom marts", "Mart Editor")) return;
            _data = MartData.LoadCurrent();
            PopulateShops();
            StartUndo();
            foreach (string n in new[] { nameof(CanResize), nameof(CanAddShop), nameof(CanAddItem), nameof(CanRemoveItem), nameof(CanRemoveCustomShop),
                                      nameof(ResizeStatus), nameof(CanOfferExpansion) })
                Notify(n);
        }

        public void DiscardChanges()
        {
            if (_data != null)
            {
                int selected = Math.Max(0, Shops.IndexOf(SelectedShop));
                _data = MartData.LoadCurrent();
                PopulateShops(selected);
                foreach (string n in new[] { nameof(SelectedShopDescription), nameof(CanAddItem), nameof(CanRemoveItem), nameof(CanRemoveCustomShop) })
                    Notify(n);
                StartUndo();
            }
        }

        private void SetDirty()
        {
            Notify(nameof(HasUnsavedChanges));
            _undo?.Record();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void Notify([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
