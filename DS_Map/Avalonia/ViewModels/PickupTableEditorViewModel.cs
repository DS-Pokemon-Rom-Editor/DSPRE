using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using DSPRE.Avalonia;
using DSPRE.Editors;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>One editable pickup slot: a label plus the item it yields (index into the item list).</summary>
    public sealed class PickupSlot : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public string Name { get; }
        public ObservableCollection<string> Items { get; }
        private int _itemIndex;
        private readonly Action _changed;
        public PickupSlot(string name, int itemIndex, ObservableCollection<string> items, Action changed) { Name = name; _itemIndex = itemIndex; Items = items; _changed = changed; }
        public int ItemIndex
        {
            get => _itemIndex;
            set { if (_itemIndex == value) return; _itemIndex = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ItemIndex))); _changed?.Invoke(); }
        }
    }

    /// <summary>One editable activation-weight threshold (slot 1-9), 0-255.</summary>
    public sealed class PickupWeight : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public string Name { get; }
        private decimal _value;
        private readonly Action _changed;
        public PickupWeight(string name, byte value, Action changed) { Name = name; _value = value; _changed = changed; }
        public decimal Value
        {
            get => _value;
            set { if (_value == value) return; _value = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value))); _changed?.Invoke(); }
        }
    }

    /// <summary>
    /// Avalonia port of the WinForms <c>PickupTableEditor</c>. Edits the Pickup ability loot table
    /// stored in an overlay: 18 common item slots, 11 rare item slots, the activation divisor
    /// (<c>random % divisor</c>) and the 9 cumulative weight thresholds. The elaborate probability
    /// read-out from WinForms is dropped — the raw, editable values are kept.
    /// </summary>
    public class PickupTableEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private const int COMMON_COUNT = 18, RARE_COUNT = 11, WEIGHT_SIZE = 9;

        public ObservableCollection<string> ItemNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<PickupSlot> CommonSlots { get; } = new ObservableCollection<PickupSlot>();
        public ObservableCollection<PickupSlot> RareSlots { get; } = new ObservableCollection<PickupSlot>();
        public ObservableCollection<PickupWeight> Weights { get; } = new ObservableCollection<PickupWeight>();

        private decimal _divisor = 10;
        public decimal ActivationDivisor { get => _divisor; set { if (Set(ref _divisor, value)) { OnPropertyChanged(nameof(ActivationChanceText)); Dirty(); } } }
        public string ActivationChanceText => _divisor > 0 ? $"≈ {100.0 / (double)_divisor:F2}% chance to trigger" : "";

        private bool _available;
        public bool IsAvailable { get => _available; private set => Set(ref _available, value); }

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => "Pickup Table";
        public void SaveChanges() => Save();
        public void DiscardChanges() { _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); Load(); }
        private void Dirty() { if (_dirty) return; _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetClean() { if (!_dirty) return; _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        public PickupTableEditorViewModel() { }
        public PickupTableEditorViewModel(bool _) { }

        public async Task SetupAsync(Window owner)
        {
            try
            {
                if (pickupTableOverlayNumber == -1)
                {
                    StatusText = "Pickup Table is not available for this ROM version/language.";
                    IsAvailable = false;
                    return;
                }
                IsAvailable = true;
                foreach (var n in GetItemNames()) ItemNames.Add(n);
                if (OverlayUtils.IsCompressed(pickupTableOverlayNumber)) OverlayUtils.Decompress(pickupTableOverlayNumber);
                Load();
            }
            catch (Exception ex)
            {
                StatusText = "Error: " + ex.Message;
                await DialogHelper.ShowError($"Failed to set up Pickup Table Editor:\n{ex.Message}", "Pickup Table");
            }
        }

        private void Load()
        {
            string path = OverlayUtils.GetPath(pickupTableOverlayNumber);

            CommonSlots.Clear();
            byte[] common = DSUtils.ReadFromFile(path, pickupCommonItemsOffset, COMMON_COUNT * 2);
            for (int i = 0; i < COMMON_COUNT; i++)
                CommonSlots.Add(new PickupSlot($"Common {i + 1}", BitConverter.ToUInt16(common, i * 2), ItemNames, Dirty));

            RareSlots.Clear();
            byte[] rare = DSUtils.ReadFromFile(path, pickupRareItemsOffset, RARE_COUNT * 2);
            for (int i = 0; i < RARE_COUNT; i++)
                RareSlots.Add(new PickupSlot($"Rare {i + 1}", BitConverter.ToUInt16(rare, i * 2), ItemNames, Dirty));

            _divisor = DSUtils.ReadFromFile(path, pickupActivationDivisorOffset, 1)[0];
            OnPropertyChanged(nameof(ActivationDivisor));
            OnPropertyChanged(nameof(ActivationChanceText));

            Weights.Clear();
            byte[] weights = DSUtils.ReadFromFile(path, pickupWeightTableOffset, WEIGHT_SIZE);
            for (int i = 0; i < WEIGHT_SIZE; i++)
                Weights.Add(new PickupWeight($"Slot {i + 1} threshold", weights[i], Dirty));

            SetClean();
            StatusText = "Loaded pickup table.";
        }

        public void Save()
        {
            if (!_available) return;
            try
            {
                string path = OverlayUtils.GetPath(pickupTableOverlayNumber);

                byte[] common = new byte[COMMON_COUNT * 2];
                for (int i = 0; i < COMMON_COUNT; i++) BitConverter.GetBytes((ushort)CommonSlots[i].ItemIndex).CopyTo(common, i * 2);
                DSUtils.WriteToFile(path, common, pickupCommonItemsOffset);

                byte[] rare = new byte[RARE_COUNT * 2];
                for (int i = 0; i < RARE_COUNT; i++) BitConverter.GetBytes((ushort)RareSlots[i].ItemIndex).CopyTo(rare, i * 2);
                DSUtils.WriteToFile(path, rare, pickupRareItemsOffset);

                DSUtils.WriteToFile(path, new byte[] { (byte)_divisor }, pickupActivationDivisorOffset);

                byte[] weights = new byte[WEIGHT_SIZE];
                for (int i = 0; i < WEIGHT_SIZE; i++) weights[i] = (byte)Weights[i].Value;
                DSUtils.WriteToFile(path, weights, pickupWeightTableOffset);

                SetClean();
                StatusText = "Saved pickup table.";
            }
            catch (Exception ex) { _ = DialogHelper.ShowError($"Save failed:\n{ex.Message}", "Pickup Table"); }
        }
    }
}
