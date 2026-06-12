using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using DSPRE.Avalonia;
using DSPRE.Editors;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>
    /// Avalonia port of the WinForms <c>HiddenItemsEditor</c>. Edits the HG/SS hidden-item table
    /// in the ARM9 (item id + amount + script id per entry). Offsets are the HeartGold (US) layout,
    /// matching the WinForms editor. Each entry is reachable from a spawnable via script <c>8NNN</c>.
    /// </summary>
    public class HiddenItemsEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private const int TABLE_LENGTH_OFFSET_1 = 0x405E4; // current entry count (1 byte)
        private const int TABLE_LENGTH_OFFSET_2 = 0x405E8; // max capacity (1 byte)
        private const int TABLE_OFFSET = 0xFA558;
        private const int ENTRY_SIZE = 8;
        private const int MAX_ENTRIES = 256;

        private sealed class Entry { public ushort ItemID, Amount, ScriptID; }
        private readonly List<Entry> _items = new List<Entry>();
        private string[] _itemNames = Array.Empty<string>();
        private int _maxCapacity = MAX_ENTRIES;

        public ObservableCollection<string> ItemNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Entries { get; } = new ObservableCollection<string>();

        private bool _suppress;

        private int _selIndex = -1;
        public int SelectedIndex { get => _selIndex; set { if (Set(ref _selIndex, value)) LoadEntry(value); } }
        public bool HasEntry => _selIndex >= 0 && _selIndex < _items.Count;

        private int _itemIndex = -1;
        public int ItemIndex { get => _itemIndex; set { if (Set(ref _itemIndex, value) && !_suppress && HasEntry && value >= 0) { _items[_selIndex].ItemID = (ushort)value; UpdateEntry(_selIndex); Dirty(); } } }

        private decimal _amount, _scriptId;
        public decimal Amount { get => _amount; set { if (Set(ref _amount, value) && !_suppress && HasEntry) { _items[_selIndex].Amount = (ushort)value; UpdateEntry(_selIndex); Dirty(); } } }
        public decimal ScriptId { get => _scriptId; set { if (Set(ref _scriptId, value) && !_suppress && HasEntry) { _items[_selIndex].ScriptID = (ushort)value; UpdateEntry(_selIndex); Dirty(); OnPropertyChanged(nameof(ScriptCall)); } } }

        public string ScriptCall => HasEntry ? $"Use in spawnable: 8{_items[_selIndex].ScriptID:D3}" : "";

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }
        public string CountText => $"Entries: {_items.Count} / {_maxCapacity}";

        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => "Hidden Items";
        public void SaveChanges() => Save();
        public void DiscardChanges() { _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); Load(); }
        private void Dirty() { if (_dirty) return; _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetClean() { if (!_dirty) return; _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        public HiddenItemsEditorViewModel() { }
        public HiddenItemsEditorViewModel(bool _) { }

        public async Task SetupAsync(Window owner)
        {
            try
            {
                if (gameFamily != GameFamilies.HGSS)
                {
                    StatusText = "Hidden Items editor supports HeartGold/SoulSilver only.";
                    return;
                }
                _itemNames = GetItemNames();
                foreach (var n in _itemNames) ItemNames.Add(n);
                if (ARM9.CheckCompressionMark()) ARM9.Decompress(arm9Path);
                Load();
            }
            catch (Exception ex)
            {
                StatusText = "Error: " + ex.Message;
                await DialogHelper.ShowError($"Failed to set up Hidden Items Editor:\n{ex.Message}", "Hidden Items");
            }
        }

        private void Load()
        {
            _items.Clear();
            int tableLength = ARM9.ReadBytes(TABLE_LENGTH_OFFSET_1, 1)[0];
            _maxCapacity = ARM9.ReadBytes(TABLE_LENGTH_OFFSET_2, 1)[0];
            if (tableLength < 0 || tableLength > _maxCapacity) tableLength = Math.Min(MAX_ENTRIES, _maxCapacity);

            byte[] table = ARM9.ReadBytes(TABLE_OFFSET, tableLength * ENTRY_SIZE);
            for (int i = 0; i < tableLength; i++)
            {
                int o = i * ENTRY_SIZE;
                ushort itemID = BitConverter.ToUInt16(table, o);
                if (itemID == 0) break;
                _items.Add(new Entry { ItemID = itemID, Amount = table[o + 2], ScriptID = BitConverter.ToUInt16(table, o + 6) });
            }
            RefreshList();
            SetClean();
            StatusText = $"Loaded {_items.Count} hidden item(s).";
        }

        private void RefreshList()
        {
            _suppress = true;
            Entries.Clear();
            foreach (var it in _items) Entries.Add(Display(it));
            _suppress = false;
            SelectedIndex = _items.Count > 0 ? 0 : -1;
            OnPropertyChanged(nameof(CountText));
        }

        private string Display(Entry it) => $"8{it.ScriptID:D3}: {ItemName(it.ItemID)} x{it.Amount}";
        private string ItemName(ushort id) => id < _itemNames.Length ? _itemNames[id] : "???";

        private void UpdateEntry(int index)
        {
            if (index < 0 || index >= _items.Count) return;
            _suppress = true;
            Entries[index] = Display(_items[index]);
            _suppress = false;
        }

        private void LoadEntry(int index)
        {
            OnPropertyChanged(nameof(HasEntry));
            OnPropertyChanged(nameof(ScriptCall));
            if (index < 0 || index >= _items.Count) return;
            var it = _items[index];
            _suppress = true;
            ItemIndex = it.ItemID < ItemNames.Count ? it.ItemID : 0;
            Amount = it.Amount; ScriptId = it.ScriptID;
            _suppress = false;
        }

        public void AddEntry()
        {
            if (_items.Count >= _maxCapacity) { _ = DialogHelper.ShowError($"Maximum entries ({_maxCapacity}) reached.", "Cannot Add"); return; }
            ushort newScriptID = 95;
            var used = new HashSet<ushort>(_items.Select(h => h.ScriptID));
            while (used.Contains(newScriptID) && newScriptID < 256) newScriptID++;
            _items.Add(new Entry { ItemID = 0, Amount = 1, ScriptID = newScriptID });
            RefreshList();
            Dirty();
            SelectedIndex = _items.Count - 1;
        }

        public void RemoveEntry()
        {
            if (!HasEntry) return;
            _items.RemoveAt(_selIndex);
            RefreshList();
            Dirty();
        }

        public void Save()
        {
            try
            {
                byte[] table = new byte[_maxCapacity * ENTRY_SIZE];
                for (int i = 0; i < _items.Count; i++)
                {
                    var it = _items[i];
                    int o = i * ENTRY_SIZE;
                    BitConverter.GetBytes(it.ItemID).CopyTo(table, o);
                    table[o + 2] = (byte)it.Amount;
                    BitConverter.GetBytes(it.ScriptID).CopyTo(table, o + 6);
                }
                ARM9.WriteBytes(table, TABLE_OFFSET);
                ARM9.WriteBytes(new byte[] { (byte)_items.Count }, TABLE_LENGTH_OFFSET_1);
                SetClean();
                StatusText = $"Saved {_items.Count} hidden item(s).";
            }
            catch (Exception ex) { _ = DialogHelper.ShowError($"Save failed:\n{ex.Message}", "Hidden Items"); }
        }
    }
}
