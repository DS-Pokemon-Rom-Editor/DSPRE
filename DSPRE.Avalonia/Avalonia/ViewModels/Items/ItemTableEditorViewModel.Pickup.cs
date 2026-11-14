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
    public partial class ItemTableEditorViewModel
    {
        public bool ShowPickupTab { get; }
        private PickupTable _pickup = new PickupTable();
        public ObservableCollection<PickupRangeRow> PickupRows { get; } = new();

        public static string RangeName(int range) => $"Lv {range * 10 + 1}-{(range + 1) * 10}";
        private static string ColumnName(int slot, bool rare) => rare ? $"Rare {slot + 1}" : $"Slot {slot + 1}";

        internal int CommonAt(int range, int slot) => _pickup.Common[_pickup.CommonIndex(range, slot)];
        internal int RareAt(int range, int slot) => _pickup.Rare[_pickup.RareIndex(range, slot)];
        internal string ItemName(int id) => id >= 0 && id < _rawItemNames.Length ? _rawItemNames[id] : $"Item {id}";

        private int _expandedPickupColumn;
        /// <summary>The one unfolded column: slots 0-8, then the two rare slots as 9 and 10.</summary>
        public int ExpandedPickupColumn
        {
            get => _expandedPickupColumn;
            set
            {
                if (value < 0 || value > 10 || value == _expandedPickupColumn) return;
                _expandedPickupColumn = value;
                foreach (PickupRangeRow row in PickupRows)
                    for (int c = 0; c <= 10; c++) row.Raise($"Open{c}");
            }
        }

        private void RaisePickupCells()
        {
            foreach (PickupEntryRow entry in PickupCommonEntries.Concat(PickupRareEntries)) entry.Raise(nameof(PickupEntryRow.Item));
            foreach (PickupRangeRow row in PickupRows)
            {
                for (int j = 0; j < PickupTable.CommonSlots; j++) { row.Raise($"Item{j}"); row.Raise($"Name{j}"); }
                for (int k = 0; k < PickupTable.RareSlots; k++) { row.Raise($"Rare{k}"); row.Raise($"RareName{k}"); }
            }
            RaiseChances();
        }

        internal void SetCommon(int range, int slot, int id)
        {
            int at = _pickup.CommonIndex(range, slot);
            if (id < 0 || id > ushort.MaxValue || _pickup.Common[at] == id) return;
            _pickup.Common[at] = (ushort)id;
            AfterCellEdit(range, slot, false);
        }

        internal void SetRare(int range, int slot, int id)
        {
            int at = _pickup.RareIndex(range, slot);
            if (id < 0 || id > ushort.MaxValue || _pickup.Rare[at] == id) return;
            _pickup.Rare[at] = (ushort)id;
            AfterCellEdit(range, slot, true);
        }

        // Without the independent slots patch other cells can be this same entry, so they refresh too.
        private void AfterCellEdit(int range, int slot, bool rare)
        {
            int at = rare ? _pickup.RareIndex(range, slot) : _pickup.CommonIndex(range, slot);
            int slotCount = rare ? PickupTable.RareSlots : PickupTable.CommonSlots;
            List<string> others = new List<string>();
            for (int r = 0; r < PickupTable.Ranges; r++)
                for (int j = 0; j < slotCount; j++)
                {
                    if ((rare ? _pickup.RareIndex(r, j) : _pickup.CommonIndex(r, j)) != at) continue;
                    PickupRows[r + 1].Raise(rare ? $"Rare{j}" : $"Item{j}");
                    PickupRows[r + 1].Raise(rare ? $"RareName{j}" : $"Name{j}");
                    if (r != range || j != slot) others.Add($"{RangeName(r)} {ColumnName(j, rare)}");
                }
            PickupLinkNote = others.Count == 0 ? ""
                : $"Also changed: {string.Join(", ", others)}.";
            SetPickupDirty();
        }

        public ObservableCollection<PickupEntryRow> PickupCommonEntries { get; } = new();
        public ObservableCollection<PickupEntryRow> PickupRareEntries { get; } = new();

        private bool _pickupListView;
        public bool PickupListView
        {
            get => _pickupListView;
            set { if (_pickupListView == value) return; _pickupListView = value; PickupLinkNote = ""; OnPropertyChanged(); OnPropertyChanged(nameof(PickupViewLabel)); }
        }
        public string PickupViewLabel => _pickupListView ? "Show by slot" : "Show by item";
        // With the patch every cell is its own entry, so the lists would only repeat the grid.
        public bool ShowPickupViewToggle => !_pickup.Independent;
        private bool _pickupPatchSupported;
        public bool OffersPickupPatch => _pickupPatchSupported && !_pickup.Independent;

        internal int EntryAt(bool rare, int index) => rare ? _pickup.Rare[index] : _pickup.Common[index];

        internal void SetEntry(bool rare, int index, int id)
        {
            ushort[] list = rare ? _pickup.Rare : _pickup.Common;
            if (id < 0 || id > ushort.MaxValue || list[index] == id) return;
            list[index] = (ushort)id;
            PickupLinkNote = "";
            RaisePickupCells();
            SetPickupDirty();
        }

        private void BuildPickupEntries()
        {
            PickupCommonEntries.Clear();
            PickupRareEntries.Clear();
            if (_pickup.Independent) { PickupListView = false; return; }
            for (int i = 0; i < _pickup.Common.Length; i++) PickupCommonEntries.Add(new PickupEntryRow(this, false, i));
            for (int i = 0; i < _pickup.Rare.Length; i++) PickupRareEntries.Add(new PickupEntryRow(this, true, i));
        }

        public IReadOnlyList<string> PickupRangeNames { get; } = Enumerable.Range(0, PickupTable.Ranges).Select(RangeName).ToList();

        // Rare 1 is roll 99 and Rare 2 roll 98. Only the first slot's cut-off can take them.
        private int RareChance(int slot)
        {
            int first = _pickup.Weights[0];
            return slot == 0 ? (first <= 99 ? 1 : 0) : (first <= 98 ? 1 : 0);
        }

        /// <summary>An entry's chance per level range, each reading the list one step further.</summary>
        internal IReadOnlyList<PickupStairCell> StairCells(bool rare, int index)
        {
            int slotCount = rare ? PickupTable.RareSlots : PickupTable.CommonSlots;
            List<PickupStairCell> cells = new List<PickupStairCell>();
            for (int r = 0; r < PickupTable.Ranges; r++)
            {
                int slot = Enumerable.Range(0, slotCount).FirstOrDefault(j => (rare ? _pickup.RareIndex(r, j) : _pickup.CommonIndex(r, j)) == index, -1);
                cells.Add(slot < 0 ? new PickupStairCell()
                                   : new PickupStairCell { Used = true, Text = $"{(rare ? RareChance(slot) : SlotChance(slot))}%" });
            }
            return cells;
        }

        private string _pickupLinkNote = "";
        public string PickupLinkNote { get => _pickupLinkNote; private set { _pickupLinkNote = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowPickupLinkNote)); } }
        public bool ShowPickupLinkNote => !string.IsNullOrEmpty(_pickupLinkNote);

        // The weights are cut-offs on a 0-99 roll; a slot's chance is its cut-off minus the one before.
        internal int SlotChance(int slot) => _pickup.Weights[slot] - (slot > 0 ? _pickup.Weights[slot - 1] : 0);

        /// <summary>Changes one slot's chance, keeping the others. A total past 98 is refused.</summary>
        internal bool SetSlotChance(int slot, int percent)
        {
            int delta = percent - SlotChance(slot);
            if (delta == 0) return true;
            if (percent < 0 || _pickup.Weights[PickupTable.WeightSize - 1] + delta > 98) return false;
            for (int j = slot; j < PickupTable.WeightSize; j++) _pickup.Weights[j] = (byte)(_pickup.Weights[j] + delta);
            PickupLinkNote = "";
            RaiseChances();
            SetPickupDirty();
            return true;
        }

        private void RaiseChances()
        {
            if (PickupRows.Count > 0)
                for (int j = 0; j < PickupTable.WeightSize; j++) { PickupRows[0].Raise($"Chance{j}"); PickupRows[0].Raise($"ChanceText{j}"); }
            foreach (PickupEntryRow entry in PickupCommonEntries.Concat(PickupRareEntries)) entry.Raise(nameof(PickupEntryRow.Cells));
            OnPropertyChanged(nameof(PickupChanceNote));
        }

        public string PickupChanceNote
        {
            get
            {
                int total = _pickup.Weights[PickupTable.WeightSize - 1], first = _pickup.Weights[0];
                if (first > 99) return "The rare items never drop.";
                if (first == 99) return "Rare 2 never drops.";
                if (total < 98) return $"Rolls {total} to 97 give nothing.";
                return "";
            }
        }

        public decimal ActivationDivisorEdit
        {
            get => _pickup.Divisor;
            set
            {
                if (value < 1 || value > 255 || (byte)value == _pickup.Divisor) return;
                _pickup.Divisor = (byte)value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ActivationText));
                SetPickupDirty();
            }
        }

        public string ActivationText => $"battles ({100.0 / Math.Max((byte)1, _pickup.Divisor):0.#}%)";

        private void LoadPickupTable()
        {
            _pickup = PickupTable.Read();
            if (_pickup.Divisor == 0) _pickup.Divisor = 10;
            _pickupPatchSupported = !HgEngineProject.IsActive && PickupSlotsPatch.SupportsCurrentRom;
            BuildPickupRows();
        }

        private void BuildPickupRows()
        {
            PickupRows.Clear();
            PickupRows.Add(new PickupRangeRow(this, -1));
            for (int r = 0; r < PickupTable.Ranges; r++) PickupRows.Add(new PickupRangeRow(this, r));
            PickupLinkNote = "";
            BuildPickupEntries();
            OnPropertyChanged(nameof(ShowPickupViewToggle));
            OnPropertyChanged(nameof(OffersPickupPatch));
            OnPropertyChanged(nameof(PickupChanceNote));
        }
    }
}
