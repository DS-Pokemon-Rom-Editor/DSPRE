using Avalonia.Controls;
using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Items
{
    /// <summary>One row of the Pickup grid: the Chance row, or a level range with its slots.</summary>
    public class PickupRangeRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        internal void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private readonly ItemTableEditorViewModel _owner;
        private readonly int _range;

        public PickupRangeRow(ItemTableEditorViewModel owner, int range) { _owner = owner; _range = range; }

        public bool IsChanceRow => _range < 0;
        public bool IsItemRow => _range >= 0;
        public string LevelRange => IsChanceRow ? "Chance" : ItemTableEditorViewModel.RangeName(_range);
        public ObservableCollection<string> ItemNamesList => _owner.ItemNames;

        private int Item(int slot) => IsItemRow ? _owner.CommonAt(_range, slot) : 0;
        private void SetItem(int slot, int id) { if (IsItemRow) _owner.SetCommon(_range, slot, id); }
        private int Rare(int slot) => IsItemRow ? _owner.RareAt(_range, slot) : 0;
        private void SetRare(int slot, int id) { if (IsItemRow) _owner.SetRare(_range, slot, id); }

        private void SetChance(int slot, decimal value)
        {
            if (!IsChanceRow || _owner.SetSlotChance(slot, (int)value)) return;
            // A refused value: show the stored one again once the box has taken the typed one.
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => Raise($"Chance{slot}"));
        }

        public int Item0 { get => Item(0); set => SetItem(0, value); }
        public int Item1 { get => Item(1); set => SetItem(1, value); }
        public int Item2 { get => Item(2); set => SetItem(2, value); }
        public int Item3 { get => Item(3); set => SetItem(3, value); }
        public int Item4 { get => Item(4); set => SetItem(4, value); }
        public int Item5 { get => Item(5); set => SetItem(5, value); }
        public int Item6 { get => Item(6); set => SetItem(6, value); }
        public int Item7 { get => Item(7); set => SetItem(7, value); }
        public int Item8 { get => Item(8); set => SetItem(8, value); }
        public int Rare0 { get => Rare(0); set => SetRare(0, value); }
        public int Rare1 { get => Rare(1); set => SetRare(1, value); }
        public string Name0 => _owner.ItemName(Item(0));
        public string Name1 => _owner.ItemName(Item(1));
        public string Name2 => _owner.ItemName(Item(2));
        public string Name3 => _owner.ItemName(Item(3));
        public string Name4 => _owner.ItemName(Item(4));
        public string Name5 => _owner.ItemName(Item(5));
        public string Name6 => _owner.ItemName(Item(6));
        public string Name7 => _owner.ItemName(Item(7));
        public string Name8 => _owner.ItemName(Item(8));
        public string RareName0 => _owner.ItemName(Rare(0));
        public string RareName1 => _owner.ItemName(Rare(1));
        public bool Open0 => _owner.ExpandedPickupColumn == 0;
        public bool Open1 => _owner.ExpandedPickupColumn == 1;
        public bool Open2 => _owner.ExpandedPickupColumn == 2;
        public bool Open3 => _owner.ExpandedPickupColumn == 3;
        public bool Open4 => _owner.ExpandedPickupColumn == 4;
        public bool Open5 => _owner.ExpandedPickupColumn == 5;
        public bool Open6 => _owner.ExpandedPickupColumn == 6;
        public bool Open7 => _owner.ExpandedPickupColumn == 7;
        public bool Open8 => _owner.ExpandedPickupColumn == 8;
        public bool Open9 => _owner.ExpandedPickupColumn == 9;
        public bool Open10 => _owner.ExpandedPickupColumn == 10;
        public decimal Chance0 { get => _owner.SlotChance(0); set => SetChance(0, value); }
        public decimal Chance1 { get => _owner.SlotChance(1); set => SetChance(1, value); }
        public decimal Chance2 { get => _owner.SlotChance(2); set => SetChance(2, value); }
        public decimal Chance3 { get => _owner.SlotChance(3); set => SetChance(3, value); }
        public decimal Chance4 { get => _owner.SlotChance(4); set => SetChance(4, value); }
        public decimal Chance5 { get => _owner.SlotChance(5); set => SetChance(5, value); }
        public decimal Chance6 { get => _owner.SlotChance(6); set => SetChance(6, value); }
        public decimal Chance7 { get => _owner.SlotChance(7); set => SetChance(7, value); }
        public decimal Chance8 { get => _owner.SlotChance(8); set => SetChance(8, value); }
        public string ChanceText0 => $"{_owner.SlotChance(0)}%";
        public string ChanceText1 => $"{_owner.SlotChance(1)}%";
        public string ChanceText2 => $"{_owner.SlotChance(2)}%";
        public string ChanceText3 => $"{_owner.SlotChance(3)}%";
        public string ChanceText4 => $"{_owner.SlotChance(4)}%";
        public string ChanceText5 => $"{_owner.SlotChance(5)}%";
        public string ChanceText6 => $"{_owner.SlotChance(6)}%";
        public string ChanceText7 => $"{_owner.SlotChance(7)}%";
        public string ChanceText8 => $"{_owner.SlotChance(8)}%";
    }

    public sealed class PickupStairCell
    {
        public string Text { get; init; } = "";
        public bool Used { get; init; }
    }

    public class PickupEntryRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        internal void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private readonly ItemTableEditorViewModel _owner;
        private readonly bool _rare;
        private readonly int _index;

        public PickupEntryRow(ItemTableEditorViewModel owner, bool rare, int index) { _owner = owner; _rare = rare; _index = index; }

        public ObservableCollection<string> ItemNamesList => _owner.ItemNames;
        public int Item { get => _owner.EntryAt(_rare, _index); set => _owner.SetEntry(_rare, _index, value); }
        public IReadOnlyList<PickupStairCell> Cells => _owner.StairCells(_rare, _index);
    }

    public class HiddenItemRowVM : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPC([CallerMemberName] string n = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public ushort ItemID   { get; set; }
        public ushort Amount   { get; set; }
        public ushort ScriptID { get; set; }

        /// <summary>The record's search range and padding, kept to write back.</summary>
        public byte Range { get; set; }
        public ushort Padding { get; set; }

        private string[] _names;
        public HiddenItemRowVM(ushort item, ushort amount, ushort script, string[] names)
        { ItemID = item; Amount = amount; ScriptID = script; _names = names; }

        public string Display =>
            $"Script {ScriptID} (8{ScriptID:D3}): {(ItemID < _names.Length ? _names[ItemID] : "???")} x{Amount}";

        public string Where { get; set; } = "";
        public bool Daily { get; set; }
        public string Detail => Daily ? $"{Where} (resets daily)" : Where;

        public void Refresh() { OnPC(nameof(ItemID)); OnPC(nameof(Display)); OnPC(nameof(Detail)); }
    }

    /// <summary>One Rock Smash entry (data/a/2/5/3). Missing means no file yet.</summary>
    public class RockSmashHeaderRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPC([CallerMemberName] string n = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public RockSmashData Data { get; }
        private readonly Action _dirty;

        public string HeaderLabel { get; }
        public bool Missing => !Data.Existed;
        public string MissingLabel => Missing ? "Will be created on save" : "";

        public RockSmashHeaderRow(RockSmashData data, string headerName, Action dirty)
        {
            Data = data;
            _dirty = dirty;
            HeaderLabel = headerName;
        }

        public int Odds
        {
            get => Data.Odds;
            set
            {
                int clamped = Math.Max(0, Math.Min(100, value));
                if (Data.Odds == clamped) return;
                Data.Odds = (ushort)clamped;
                OnPC();
                _dirty();
            }
        }

        // 0 = Default, 1 = Ruins of Alph, 2 = Cliff Cave, matches RockSmashData.TableType.
        public int TypeIndex
        {
            get => (int)Data.Type;
            set
            {
                RockSmashData.TableType t = (RockSmashData.TableType)value;
                if (Data.Type == t) return;
                Data.Type = t;
                OnPC();
                _dirty();
            }
        }

        public void RefreshMissing() { OnPC(nameof(Missing)); OnPC(nameof(MissingLabel)); }
    }

    /// <summary>One of Rock Smash's three fixed 8-slot item tables.</summary>
    public class RockSmashItemSlotsRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPC([CallerMemberName] string n = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public string Label { get; }
        public readonly ushort[] ItemIDs;
        private readonly Action _dirty;

        public ObservableCollection<string> ItemNames { get; }

        public RockSmashItemSlotsRow(string label, ushort[] itemIDs, ObservableCollection<string> itemNames, Action dirty)
        {
            Label = label; ItemIDs = itemIDs; ItemNames = itemNames; _dirty = dirty;
        }

        private int Get(int slot) => ItemIDs[slot];

        // The list's index is the item id. -1 is the box being cleared, which keeps the item.
        private void Set(int slot, int id)
        {
            if (id < 0 || id > ushort.MaxValue || ItemIDs[slot] == id) return;
            ItemIDs[slot] = (ushort)id;
            _dirty();
        }

        public int Item0 { get => Get(0); set => Set(0, value); }
        public int Item1 { get => Get(1); set => Set(1, value); }
        public int Item2 { get => Get(2); set => Set(2, value); }
        public int Item3 { get => Get(3); set => Set(3, value); }
        public int Item4 { get => Get(4); set => Set(4, value); }
        public int Item5 { get => Get(5); set => Set(5, value); }
        public int Item6 { get => Get(6); set => Set(6, value); }
        public int Item7 { get => Get(7); set => Set(7, value); }

        public void Refresh() { for (int i = 0; i < 8; i++) OnPC($"Item{i}"); }
    }
}
