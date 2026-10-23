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
    // ─── Row VMs ──────────────────────────────────────────────────────────────
    /// <summary>
    /// One row of the Pickup grid: the Chance row above the level ranges, or one level range with its nine slots and two
    /// rare slots. Cells go through the editor, which knows how the table is laid out.
    /// </summary>
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

    /// <summary>One level range's cell in an entry's row: the chance there, or nothing where the range doesn't read it.</summary>
    public sealed class PickupStairCell
    {
        public string Text { get; init; } = "";
        public bool Used { get; init; }
    }

    /// <summary>One entry of the game's common or rare pickup list, with its chance at each level range.</summary>
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

        /// <summary>The record's search range and padding, which the editor doesn't show but must write back.</summary>
        public byte Range { get; set; }
        public ushort Padding { get; set; }

        private string[] _names;
        public HiddenItemRowVM(ushort item, ushort amount, ushort script, string[] names)
        { ItemID = item; Amount = amount; ScriptID = script; _names = names; }

        public string Display =>
            $"Script {ScriptID} (8{ScriptID:D3}): {(ItemID < _names.Length ? _names[ItemID] : "???")} x{Amount}";

        /// <summary>Where the item is placed, filled in by the editor from the event files.</summary>
        public string Where { get; set; } = "";
        public bool Daily { get; set; }
        public string Detail => Daily ? $"{Where} (resets daily)" : Where;

        public void Refresh() { OnPC(nameof(ItemID)); OnPC(nameof(Display)); OnPC(nameof(Detail)); }
    }

    /// <summary>One Rock Smash entry (data/a/2/5/3): a header's item odds and which of the three tables it
    /// rolls from. <see cref="Missing"/> marks a header with no file yet; Save creates it from what is shown.</summary>
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

    /// <summary>One of Rock Smash's three fixed 8-slot item tables, hardcoded in HGSS's ov001.bin
    /// (see <see cref="RomInfo.IsRockSmashItemTableAvailable"/>).</summary>
    public class RockSmashItemSlotsRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPC([CallerMemberName] string n = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public string Label { get; }
        public readonly ushort[] ItemIDs;
        private readonly Action _dirty;

        /// <summary>The choices for every slot, shared with the editor.</summary>
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

    // ─── Main ViewModel ───────────────────────────────────────────────────────
    public class ItemTableEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        // ── Item names for combo boxes ────────────────────────────────────────
        public ObservableCollection<string> ItemNames { get; } = new();
        private string[] _rawItemNames = Array.Empty<string>();
        private string[] _headerNames = Array.Empty<string>();

        // ── Pickup ────────────────────────────────────────────────────────────
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

        // The list's index is the item id. -1 is the box being cleared, which keeps the item.
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

        // Without the independent slots patch each range reads one step further along the same list, so other cells can
        // be this same entry; they are refreshed and named.
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

        // ── Pickup as the game's two lists ───────────────────────────────────
        public ObservableCollection<PickupEntryRow> PickupCommonEntries { get; } = new();
        public ObservableCollection<PickupEntryRow> PickupRareEntries { get; } = new();

        private bool _pickupListView;
        /// <summary>Shows the 18 common and 11 rare entries as the game stores them, each with its chance per level range.</summary>
        public bool PickupListView
        {
            get => _pickupListView;
            set { if (_pickupListView == value) return; _pickupListView = value; PickupLinkNote = ""; OnPropertyChanged(); OnPropertyChanged(nameof(PickupViewLabel)); }
        }
        public string PickupViewLabel => _pickupListView ? "Show by slot" : "Show by item";
        // With the patch every cell is its own entry, so the lists would only repeat the grid.
        public bool ShowPickupViewToggle => !_pickup.Independent;

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

        // Rare 1 is roll 99 and Rare 2 roll 98; a slot total that reaches a roll takes it from the rare item.
        private int RareChance(int slot)
        {
            int total = _pickup.Weights[PickupTable.WeightSize - 1];
            return slot == 0 ? (total <= 99 ? 1 : 0) : (total <= 98 ? 1 : 0);
        }

        /// <summary>An entry's chance at each level range. Each range reads the list one step further on, so the
        /// used cells run down the table as a staircase.</summary>
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

        /// <summary>Changes one slot's chance and keeps the others', refusing a total past 98 (98 and 99 are the rare rolls).</summary>
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
                int total = _pickup.Weights[PickupTable.WeightSize - 1];
                if (total < 98) return $"Rolls {total} to 97 give nothing.";
                if (total == 99) return "Rare 2 never drops.";
                if (total > 99) return "The rare items never drop.";
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

        // ── Hidden Items ──────────────────────────────────────────────────────
        public bool ShowHiddenItemsTab { get; }
        private int _hiddenMaxCapacity = 256;
        private Dictionary<int, List<HiddenItemTable.Placement>> _hiddenPlacements = new();
        // False when a count site isn't the expected instruction or literal, so nothing is patched.
        private bool _hiddenSitesValid = true;

        public ObservableCollection<HiddenItemRowVM> HiddenItems { get; } = new();

        private HiddenItemRowVM _selectedHiddenItem;
        public HiddenItemRowVM SelectedHiddenItem
        {
            get => _selectedHiddenItem;
            set
            {
                _selectedHiddenItem = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedItemID));
                OnPropertyChanged(nameof(SelectedAmount));
                OnPropertyChanged(nameof(SelectedScriptID));
                OnPropertyChanged(nameof(SelectedScriptLabel));
                OnPropertyChanged(nameof(HiddenItemSelected));
                OnPropertyChanged(nameof(CanGoToHiddenEvent));
            }
        }

        public bool HiddenItemSelected => _selectedHiddenItem != null;

        /// <summary>Opens the common script a hidden item runs, 8000 plus its script ID.</summary>
        public void GoToHiddenItemScript()
        {
            if (_selectedHiddenItem == null) return;
            string message = AvaloniaEditorLauncher.GoToScript(HiddenItemTable.ScriptBase + _selectedHiddenItem.ScriptID, null, out bool opened);
            if (!opened) AppMessages.Info(message, "Hidden item script");
        }

        public int SelectedItemID
        {
            get => _selectedHiddenItem?.ItemID ?? 0;
            set
            {
                if (_selectedHiddenItem == null || value < 0) return;
                _selectedHiddenItem.ItemID = (ushort)value;
                _selectedHiddenItem.Refresh();
                SetHiddenDirty();
                OnPropertyChanged();
            }
        }

        public decimal SelectedAmount
        {
            get => _selectedHiddenItem?.Amount ?? 1;
            set
            {
                if (_selectedHiddenItem == null) return;
                _selectedHiddenItem.Amount = (ushort)value;
                _selectedHiddenItem.Refresh();
                SetHiddenDirty();
                OnPropertyChanged();
            }
        }

        public decimal SelectedScriptID
        {
            get => _selectedHiddenItem?.ScriptID ?? 0;
            set
            {
                if (_selectedHiddenItem == null) return;
                _selectedHiddenItem.ScriptID = (ushort)value;
                PlaceHiddenRow(_selectedHiddenItem);
                OnPropertyChanged(nameof(SelectedScriptLabel));
                OnPropertyChanged(nameof(CanGoToHiddenEvent));
                SetHiddenDirty();
                OnPropertyChanged();
            }
        }

        public string SelectedScriptLabel =>
            _selectedHiddenItem != null ? $"Use in spawnable: 8{_selectedHiddenItem.ScriptID:D3}" : "";

        public int HiddenScriptMax => HgEngineProject.IsActive ? 255 : RomInfo.HiddenItemScriptMax;

        private List<HiddenItemTable.Placement> SelectedPlacements =>
            _selectedHiddenItem != null && _hiddenPlacements.TryGetValue(_selectedHiddenItem.ScriptID, out List<HiddenItemTable.Placement> p)
                ? p : new List<HiddenItemTable.Placement>();

        public bool CanGoToHiddenEvent => SelectedPlacements.Count > 0;

        public void SelectHiddenScript(int script)
        {
            HiddenItemRowVM row = HiddenItems.FirstOrDefault(h => h.ScriptID == script);
            if (row != null) SelectedHiddenItem = row;
        }

        public void GoToHiddenEvent()
        {
            List<HiddenItemTable.Placement> placed = SelectedPlacements;
            if (placed.Count > 0) AvaloniaEditorLauncher.OpenEventEditorWithSpawnable(placed[0].EventFile, placed[0].Spawnable);
        }

        // DP and Pt fill their tables, so Add reuses a removed entry's script, whose found flag a save may have set.
        public bool ShowHiddenReuseNote => !HgEngineProject.IsActive && RomInfo.GameFamily is GameFamilies.DP or GameFamilies.Plat;
        public string HiddenAddHelp => ShowHiddenReuseNote ? "Reuses a removed entry's script and its found flag" : null;

        private void PlaceHiddenRows()
        {
            HashSet<int> daily = new HashSet<int>(RomInfo.HiddenItemDailyScripts);
            foreach (HiddenItemRowVM row in HiddenItems) PlaceHiddenRow(row, daily);
        }

        private void PlaceHiddenRow(HiddenItemRowVM row, HashSet<int> daily = null)
        {
            daily ??= new HashSet<int>(RomInfo.HiddenItemDailyScripts);
            row.Daily = daily.Contains(row.ScriptID);
            row.Where = !_hiddenPlacements.TryGetValue(row.ScriptID, out List<HiddenItemTable.Placement> placed) ? "Not placed"
                : PlacementText(placed[0]) + (placed.Count > 1 ? $" and {placed.Count - 1} more" : "");
            row.Refresh();
        }

        private static string PlacementText(HiddenItemTable.Placement p)
        {
            string place = p.Header >= 0 ? HeaderLabels.LocationNameOf(MapHeader.GetMapHeader((ushort)p.Header)) : "";
            return $"{(string.IsNullOrEmpty(place) ? $"Event file {p.EventFile}" : place)} ({p.X}, {p.Y})";
        }

        // hg-engine's table is a plain C array with no fixed capacity, unlike vanilla's ARM9-reserved buffer.
        public string HiddenEntryCount => HgEngineProject.IsActive
            ? $"Entries: {HiddenItems.Count}"
            : $"Entries: {HiddenItems.Count} / {_hiddenMaxCapacity}";

        // ── Rock Smash (HGSS) ────────────────────────────────────────────────────
        // Per-header odds/table (data/a/2/5/3): available for any HGSS ROM, any language.
        // The three fixed item-slot tables are RockSmashItemSlots, at RomInfo.rockSmashItemTablesOffset.

        public bool ShowRockSmashTab { get; }
        public bool ShowRockSmashItemTables { get; private set; }
        public ObservableCollection<RockSmashHeaderRow> RockSmashRows { get; } = new();
        public RockSmashItemSlotsRow RockSmashDefaultTable { get; private set; }
        public RockSmashItemSlotsRow RockSmashRuinsOfAlphTable { get; private set; }
        public RockSmashItemSlotsRow RockSmashCliffCaveTable { get; private set; }

        public string RockSmashCount => $"{RockSmashShown.Count} of {RockSmashRows.Count} headers";

        /// <summary>The headers the list shows; RockSmashRows keeps every header for saving and undo.</summary>
        public ObservableCollection<RockSmashHeaderRow> RockSmashShown { get; } = new();

        private string _rockSmashSearch = "";
        public string RockSmashSearch { get => _rockSmashSearch; set { if (_rockSmashSearch == (value ?? "")) return; _rockSmashSearch = value ?? ""; OnPropertyChanged(); FilterRockSmash(); } }

        private bool _rockSmashOnlyWithOdds = true;
        public bool RockSmashOnlyWithOdds { get => _rockSmashOnlyWithOdds; set { if (_rockSmashOnlyWithOdds == value) return; _rockSmashOnlyWithOdds = value; OnPropertyChanged(); FilterRockSmash(); } }

        // Filtered only when the search or the toggle changes, so a row being edited to 0 stays in view.
        private void FilterRockSmash()
        {
            RockSmashShown.Clear();
            string search = _rockSmashSearch.Trim();
            foreach (RockSmashHeaderRow r in RockSmashRows)
            {
                if (_rockSmashOnlyWithOdds && r.Odds == 0) continue;
                if (search.Length > 0 && !SearchMatch.Contains(r.HeaderLabel, search)) continue;
                RockSmashShown.Add(r);
            }
            OnPropertyChanged(nameof(RockSmashCount));
        }

        // ── Dirty ─────────────────────────────────────────────────────────────
        private bool _pickupDirty;
        private bool _hiddenDirty;
        private bool _rockSmashDirty;
        public bool HasUnsavedChanges => _pickupDirty || _hiddenDirty || _rockSmashDirty;
        public string UnsavedChangesDescription =>
            string.Join(", ", new[]
            {
                _pickupDirty ? "Pickup table" : null,
                _hiddenDirty ? "Hidden items" : null,
                _rockSmashDirty ? "Rock Smash items" : null
            }.Where(s => s != null).DefaultIfEmpty("Item tables"));

        private void SetPickupDirty() { _pickupDirty = true; Edited(); }
        private void SetHiddenDirty() { _hiddenDirty = true; Edited(); }
        private void SetRockSmashDirty() { _rockSmashDirty = true; Edited(); }

        // ── Undo / redo: every tab's tables ───────────────────────────────────
        private sealed record PickupState(ushort[] Common, ushort[] Rare, int Divisor, byte[] Weights);
        private sealed record TablesState(PickupState Pickup, int[][] Hidden, int[][] RockSmash, ushort[][] Slots);
        private DSPRE.Avalonia.ByteStateUndo _undo;
        private TablesState _saved;
        private bool _applyingUndo;
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();
        private void RaiseUndo() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private PickupState CurrentPickup() => new((ushort[])_pickup.Common.Clone(), (ushort[])_pickup.Rare.Clone(), _pickup.Divisor, (byte[])_pickup.Weights.Clone());

        private int[][] CurrentHidden() => HiddenItems.Select(h => new int[] { h.ItemID, h.Amount, h.ScriptID, h.Range, h.Padding }).ToArray();

        private int[][] CurrentRockSmash() => RockSmashRows.Select(r => new int[] { r.Odds, r.TypeIndex }).ToArray();

        private ushort[][] CurrentSlots() => ShowRockSmashItemTables && RockSmashDefaultTable != null
            ? new[] { (ushort[])RockSmashDefaultTable.ItemIDs.Clone(), (ushort[])RockSmashRuinsOfAlphTable.ItemIDs.Clone(), (ushort[])RockSmashCliffCaveTable.ItemIDs.Clone() }
            : Array.Empty<ushort[]>();

        private TablesState Current() => new(CurrentPickup(), CurrentHidden(), CurrentRockSmash(), CurrentSlots());

        private static bool Same<T>(T a, T b) => DSPRE.Avalonia.UndoJson.Take(a).AsSpan().SequenceEqual(DSPRE.Avalonia.UndoJson.Take(b));

        private void ResetUndo()
        {
            _saved = Current();
            _undo = new DSPRE.Avalonia.ByteStateUndo(() => DSPRE.Avalonia.UndoJson.Take(Current()), ApplyState, RaiseUndo);
            RaiseUndo();
        }

        // Each tab counts as unsaved while it differs from what was last read or saved, so undoing back clears it.
        private void RecountDirty()
        {
            if (_saved == null) { OnPropertyChanged(nameof(HasUnsavedChanges)); return; }
            _pickupDirty = !Same(CurrentPickup(), _saved.Pickup);
            _hiddenDirty = !Same(CurrentHidden(), _saved.Hidden);
            _rockSmashDirty = !Same(CurrentRockSmash(), _saved.RockSmash) || !Same(CurrentSlots(), _saved.Slots);
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void Edited()
        {
            if (_applyingUndo) return;
            _undo?.Record();
            RecountDirty();
        }

        private void ApplyState(byte[] state)
        {
            TablesState s = DSPRE.Avalonia.UndoJson.Read<TablesState>(state);
            _applyingUndo = true;
            try
            {
                Array.Copy(s.Pickup.Common, _pickup.Common, Math.Min(s.Pickup.Common.Length, _pickup.Common.Length));
                Array.Copy(s.Pickup.Rare, _pickup.Rare, Math.Min(s.Pickup.Rare.Length, _pickup.Rare.Length));
                Array.Copy(s.Pickup.Weights, _pickup.Weights, Math.Min(s.Pickup.Weights.Length, _pickup.Weights.Length));
                _pickup.Divisor = (byte)s.Pickup.Divisor;
                RaisePickupCells();
                PickupLinkNote = "";
                OnPropertyChanged(nameof(ActivationDivisorEdit));
                OnPropertyChanged(nameof(ActivationText));

                int keep = _selectedHiddenItem != null ? HiddenItems.IndexOf(_selectedHiddenItem) : -1;
                HiddenItems.Clear();
                foreach (int[] h in s.Hidden)
                    HiddenItems.Add(new HiddenItemRowVM((ushort)h[0], (ushort)h[1], (ushort)h[2], _rawItemNames) { Range = (byte)h[3], Padding = (ushort)h[4] });
                PlaceHiddenRows();
                SelectedHiddenItem = HiddenItems.Count > 0 ? HiddenItems[Math.Clamp(keep, 0, HiddenItems.Count - 1)] : null;
                OnPropertyChanged(nameof(HiddenEntryCount));

                for (int i = 0; i < s.RockSmash.Length && i < RockSmashRows.Count; i++)
                {
                    RockSmashRows[i].Odds = s.RockSmash[i][0];
                    RockSmashRows[i].TypeIndex = s.RockSmash[i][1];
                }
                RockSmashItemSlotsRow[] tables = new[] { RockSmashDefaultTable, RockSmashRuinsOfAlphTable, RockSmashCliffCaveTable };
                for (int t = 0; t < s.Slots.Length && t < tables.Length; t++)
                {
                    if (tables[t] == null) continue;
                    Array.Copy(s.Slots[t], tables[t].ItemIDs, Math.Min(s.Slots[t].Length, tables[t].ItemIDs.Length));
                    tables[t].Refresh();
                }
            }
            finally { _applyingUndo = false; }
            RecountDirty();
        }

        // ── Design-time constructor ───────────────────────────────────────────
        public ItemTableEditorViewModel()
        {
            if (!Design.IsDesignMode) return;

            ShowPickupTab           = true;
            ShowHiddenItemsTab      = true;
            ShowRockSmashTab        = true;
            ShowRockSmashItemTables = true;

            _rawItemNames = Enumerable.Range(0, 30).Select(i => $"Item {i}").ToArray();
            for (int i = 0; i < 30; i++) ItemNames.Add(_rawItemNames[i]);

            _pickup = new PickupTable
            {
                Common = Enumerable.Range(0, PickupTable.CommonCount).Select(i => (ushort)(i % 10)).ToArray(),
                Rare = Enumerable.Range(0, PickupTable.RareCount).Select(i => (ushort)(i % 10)).ToArray(),
                Weights = new byte[] { 30, 40, 50, 60, 70, 80, 90, 94, 98 },
                Divisor = 10,
            };
            BuildPickupRows();
            BuildHiddenDummyRows();
            BuildRockSmashDummyRows();
        }

        // ── Runtime constructor ───────────────────────────────────────────────
        public ItemTableEditorViewModel(string[] itemNames, IEnumerable<string> headerNames = null)
        {
            _rawItemNames = itemNames;
            for (int i = 0; i < itemNames.Length; i++) ItemNames.Add(itemNames[i]);
            _headerNames = headerNames?.ToArray() ?? Array.Empty<string>();

            ShowPickupTab           = RomInfo.PickupTableOverlayNumber != -1;
            ShowHiddenItemsTab      = RomInfo.IsHiddenItemsEditorAvailable() || HgEngineProject.IsActive;
            ShowRockSmashTab        = RomInfo.IsRockSmashEditorAvailable();
            ShowRockSmashItemTables = RomInfo.IsRockSmashItemTableAvailable();

            if (ShowPickupTab)
            {
                if (OverlayUtils.IsCompressed(RomInfo.PickupTableOverlayNumber))
                    OverlayUtils.Decompress(RomInfo.PickupTableOverlayNumber);
                LoadPickupTable();
            }
            if (ShowHiddenItemsTab)
            {
                if (!HgEngineProject.IsActive) ARM9.DecompressIfMarked();
                LoadHiddenItems();
            }
            if (ShowRockSmashTab)
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.RockSmash });
                LoadRockSmash();
            }
            ResetUndo();
        }

        // ── Pickup load ───────────────────────────────────────────────────────
        private void LoadPickupTable()
        {
            _pickup = PickupTable.Read();
            if (_pickup.Divisor == 0) _pickup.Divisor = 10;
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
            OnPropertyChanged(nameof(PickupChanceNote));
        }

        // ── Hidden items load ─────────────────────────────────────────────────
        // With hg-engine the packed ROM is stale, so read the checkout's data/HiddenItems.c instead.
        private void LoadHiddenItems()
        {
            HiddenItems.Clear();
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.EventFiles });
            _hiddenPlacements = HiddenItemTable.Placements();

            if (HgEngineProject.IsActive)
            {
                if (HgEngineHiddenItems.TryLoad(out List<HgEngineHiddenItems.Entry> entries, out string err))
                {
                    foreach (HgEngineHiddenItems.Entry e in entries)
                        HiddenItems.Add(new HiddenItemRowVM((ushort)e.ItemId, (ushort)e.Quantity, (ushort)e.Index, _rawItemNames));
                    PlaceHiddenRows();
                }
                else
                {
                    AppLogger.Error($"hg-engine hidden items read failed: {err}");
                }
                if (HiddenItems.Count > 0) SelectedHiddenItem = HiddenItems[0];
                OnPropertyChanged(nameof(HiddenEntryCount));
                return;
            }

            _hiddenMaxCapacity = RomInfo.HiddenItemTableCapacity;
            int tableLen = ReadHiddenItemCount();
            if (tableLen < 0)
            {
                _hiddenSitesValid = false;
                AppLogger.Error("Hidden items: the ARM9 count checks aren't where expected; the table is left alone.");
                OnPropertyChanged(nameof(HiddenEntryCount));
                return;
            }
            tableLen = Math.Min(tableLen, _hiddenMaxCapacity);

            foreach (HiddenItemTable.Entry e in HiddenItemTable.Read(tableLen))
                HiddenItems.Add(new HiddenItemRowVM(e.Item, e.Quantity, e.Script, _rawItemNames) { Range = e.Range, Padding = e.Padding });
            PlaceHiddenRows();

            if (HiddenItems.Count > 0) SelectedHiddenItem = HiddenItems[0];
            OnPropertyChanged(nameof(HiddenEntryCount));
        }

        private static int ReadHiddenItemCount() => HiddenItemTable.Count();

        private static List<int> HiddenItemUsers(int index)
        {
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.EventFiles });
            List<int> files = new List<int>();
            int script = HiddenItemTable.ScriptBase + index;
            int fileCount = Filesystem.GetEventFileCount();
            for (int i = 0; i < fileCount; i++)
                if (new EventFile(i).Spawnables.Any(sp => sp.ScriptNumber == script)) files.Add(i);
            return files;
        }

        private void BuildHiddenDummyRows()
        {
            for (int i = 0; i < 5; i++)
                HiddenItems.Add(new HiddenItemRowVM(1, 1, (ushort)(95 + i), _rawItemNames));
            if (HiddenItems.Count > 0) SelectedHiddenItem = HiddenItems[0];
        }

        public void AddHiddenItem()
        {
            if (!HgEngineProject.IsActive && HiddenItems.Count >= _hiddenMaxCapacity) return;
            HashSet<ushort> used = new HashSet<ushort>(HiddenItems.Select(h => h.ScriptID));
            // DP and Pt take the lowest free script, whose flag is one the game already sets aside.
            ushort sid = ShowHiddenReuseNote ? (ushort)0 : (ushort)95;
            while (used.Contains(sid) && sid < HiddenScriptMax) sid++;
            HiddenItemRowVM entry = new HiddenItemRowVM(0, 1, sid, _rawItemNames);
            HiddenItems.Add(entry);
            PlaceHiddenRow(entry);
            SelectedHiddenItem = entry;
            SetHiddenDirty();
            OnPropertyChanged(nameof(HiddenEntryCount));
        }

        public async System.Threading.Tasks.Task RemoveSelectedHiddenItemAsync()
        {
            if (_selectedHiddenItem == null) return;
            List<int> users = HiddenItemUsers(_selectedHiddenItem.ScriptID);
            if (users.Count > 0)
            {
                await DialogHelper.ShowErrorAsync(
                    $"Event file {string.Join(", ", users.Take(5))}{(users.Count > 5 ? " and more" : "")} still uses this entry. Change or delete those events first.",
                    "Hidden items");
                return;
            }
            int idx = HiddenItems.IndexOf(_selectedHiddenItem);
            HiddenItems.Remove(_selectedHiddenItem);
            SelectedHiddenItem = HiddenItems.Count > 0
                ? HiddenItems[Math.Min(idx, HiddenItems.Count - 1)]
                : null;
            SetHiddenDirty();
            OnPropertyChanged(nameof(HiddenEntryCount));
        }

        // ── Rock Smash load ──────────────────────────────────────────────────────
        private void LoadRockSmash()
        {
            RockSmashRows.Clear();
            int headerCount = RomInfo.GetHeaderCount();
            for (int i = 0; i < headerCount; i++)
            {
                RockSmashData data = new RockSmashData((ushort)i);
                string name = i < _headerNames.Length ? _headerNames[i] : $"Header {i}";
                RockSmashRows.Add(new RockSmashHeaderRow(data, name, SetRockSmashDirty));
            }
            FilterRockSmash();

            if (ShowRockSmashItemTables)
            {
                try { LoadRockSmashItemTables(); }
                // When DSPRE can't read the hg-engine source, the tables are hidden so no wrong ones show.
                catch (InvalidOperationException e)
                {
                    AppLogger.Error("Item Tables, Rock Smash items: " + e.Message);
                    ShowRockSmashItemTables = false;
                    OnPropertyChanged(nameof(ShowRockSmashItemTables));
                }
            }
        }

        private void LoadRockSmashItemTables()
        {
            RockSmashDefaultTable = new RockSmashItemSlotsRow("Default",
                RockSmashItemSlots.Read(RockSmashItemSlots.DefaultOffset), ItemNames, SetRockSmashDirty);
            RockSmashRuinsOfAlphTable = new RockSmashItemSlotsRow("Ruins of Alph",
                RockSmashItemSlots.Read(RockSmashItemSlots.RuinsOfAlphOffset), ItemNames, SetRockSmashDirty);
            RockSmashCliffCaveTable = new RockSmashItemSlotsRow("Cliff Cave",
                RockSmashItemSlots.Read(RockSmashItemSlots.CliffCaveOffset), ItemNames, SetRockSmashDirty);

            OnPropertyChanged(nameof(RockSmashDefaultTable));
            OnPropertyChanged(nameof(RockSmashRuinsOfAlphTable));
            OnPropertyChanged(nameof(RockSmashCliffCaveTable));
        }

        private void BuildRockSmashDummyRows()
        {
            for (int i = 0; i < 6; i++)
                RockSmashRows.Add(new RockSmashHeaderRow(new RockSmashData((ushort)i, ""), $"Route {i}", SetRockSmashDirty));

            ushort[] Dummy() => new ushort[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            RockSmashDefaultTable      = new RockSmashItemSlotsRow("Default", Dummy(), ItemNames, SetRockSmashDirty);
            RockSmashRuinsOfAlphTable  = new RockSmashItemSlotsRow("Ruins of Alph", Dummy(), ItemNames, SetRockSmashDirty);
            RockSmashCliffCaveTable    = new RockSmashItemSlotsRow("Cliff Cave", Dummy(), ItemNames, SetRockSmashDirty);
        }

        // ── Save ──────────────────────────────────────────────────────────────
        public void SaveChanges()
        {
            if (_pickupDirty && ShowPickupTab) SavePickupTable();
            if (_hiddenDirty && ShowHiddenItemsTab) SaveHiddenItems();
            if (_rockSmashDirty && ShowRockSmashTab) SaveRockSmash();
        }

        async System.Threading.Tasks.Task<bool> IEditorWithUnsavedChanges.SaveChangesAsync()
        {
            if (_pickupDirty && ShowPickupTab) SavePickupTable();
            if (_hiddenDirty && ShowHiddenItemsTab)
            {
                if (HgEngineProject.IsActive) await SaveHiddenItemsToSourceAsync();
                else SaveHiddenItems();
            }
            if (_rockSmashDirty && ShowRockSmashTab) SaveRockSmash();
            return !HasUnsavedChanges;
        }

        private async System.Threading.Tasks.Task SaveHiddenItemsToSourceAsync()
        {
            List<HgEngineHiddenItems.Entry> entries = new List<HgEngineHiddenItems.Entry>(HiddenItems.Count);
            foreach (HiddenItemRowVM e in HiddenItems)
                entries.Add(new HgEngineHiddenItems.Entry { ItemId = e.ItemID, Quantity = e.Amount, Index = e.ScriptID });
            (bool saved, string error) = await DSPRE.Avalonia.HgEngineSave.RunAsync(() => HgEngineHiddenItems.TrySave(entries, out string err) ? null : err);
            if (!saved)
            {
                if (error != null) await DialogHelper.ShowErrorAsync($"Hidden items weren't saved.\n{error}", "Item tables");
                return;
            }

            _hiddenDirty = false;
            SavedTab();
            SaveNotice.Saved(UnsavedChangesDescription);
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        // A tab just written becomes the new saved state for that tab; undo's saved mark follows once nothing is left.
        private void SavedTab()
        {
            if (_saved == null) return;
            _saved = new TablesState(
                _pickupDirty ? _saved.Pickup : CurrentPickup(),
                _hiddenDirty ? _saved.Hidden : CurrentHidden(),
                _rockSmashDirty ? _saved.RockSmash : CurrentRockSmash(),
                _rockSmashDirty ? _saved.Slots : CurrentSlots());
            if (!HasUnsavedChanges) _undo?.MarkSaved();
        }

        private void SavePickupTable()
        {
            _pickup.Write();

            _pickupDirty = false;
            PickupLinkNote = "";
            SavedTab();
            SaveNotice.Saved(UnsavedChangesDescription);
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void SaveHiddenItems()
        {
            if (HgEngineProject.IsActive)
            {
                DialogHelper.Watch(SaveHiddenItemsToSourceAsync());
                return;
            }

            HiddenItemRowVM empty = HiddenItems.FirstOrDefault(h => h.ItemID == 0);
            string refusal = !_hiddenSitesValid ? "The ARM9 count checks aren't where expected."
                : HiddenItems.Count > _hiddenMaxCapacity ? $"There is room for {_hiddenMaxCapacity} entries."
                : empty != null ? $"Script {empty.ScriptID} has no item. Pick one or remove the entry."
                : null;
            if (refusal != null)
            {
                _ = DialogHelper.ShowErrorAsync($"Hidden items weren't saved.\n{refusal}", "Item tables");
                return;
            }

            HiddenItemTable.Write(HiddenItems.Select(e => new HiddenItemTable.Entry
            {
                Item = e.ItemID, Quantity = (byte)e.Amount, Range = e.Range, Padding = e.Padding, Script = e.ScriptID,
            }).ToList(), _hiddenMaxCapacity);

            _hiddenDirty = false;
            SavedTab();
            SaveNotice.Saved(UnsavedChangesDescription);
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void SaveRockSmash()
        {
            // Whole-file overwrite per header (matches Headbutt's per-header-NARC convention);
            // File.WriteAllBytes creates the file if it didn't exist, so headers that were "missing"
            // from a/2/5/3 (modified ROM, or a header added since) get materialized here.
            foreach (RockSmashHeaderRow row in RockSmashRows)
            {
                row.Data.SaveToFile();
                row.RefreshMissing();
            }

            if (ShowRockSmashItemTables)
            {
                try
                {
                    RockSmashItemSlots.Write(RockSmashItemSlots.DefaultOffset, RockSmashDefaultTable.ItemIDs);
                    RockSmashItemSlots.Write(RockSmashItemSlots.RuinsOfAlphOffset, RockSmashRuinsOfAlphTable.ItemIDs);
                    RockSmashItemSlots.Write(RockSmashItemSlots.CliffCaveOffset, RockSmashCliffCaveTable.ItemIDs);
                }
                catch (Exception e) when (e is InvalidOperationException || e is System.IO.IOException || e is UnauthorizedAccessException)
                {
                    AppLogger.Error("Item Tables, Rock Smash items: " + e.Message);
                    _ = DialogHelper.ShowErrorAsync("The Rock Smash items weren't saved:\n" + e.Message, "Item tables");
                    return;
                }
            }

            _rockSmashDirty = false;
            SavedTab();
            SaveNotice.Saved(UnsavedChangesDescription);
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        // Re-reads each edited table from the project; the flags clear even if a read fails so closing never gets stuck.
        public void DiscardChanges()
        {
            try
            {
                if (_pickupDirty && ShowPickupTab) { LoadPickupTable(); OnPropertyChanged(nameof(ActivationDivisorEdit)); }
                if (_hiddenDirty && ShowHiddenItemsTab) { SelectedHiddenItem = null; LoadHiddenItems(); }
                if (_rockSmashDirty && ShowRockSmashTab) LoadRockSmash();
            }
            finally
            {
                _pickupDirty = false;
                _hiddenDirty = false;
                _rockSmashDirty = false;
                ResetUndo();
                OnPropertyChanged(nameof(HasUnsavedChanges));
            }
        }
    }
}
