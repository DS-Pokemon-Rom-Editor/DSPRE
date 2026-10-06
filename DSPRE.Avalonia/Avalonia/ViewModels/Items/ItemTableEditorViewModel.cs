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
    public class CommonPickupRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPC([CallerMemberName] string n = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private readonly List<ushort> _ids;
        private readonly int _b; // bracket index
        private readonly Action _dirty;
        private readonly Action<int> _adjacentRefresh; // pass absolute id index

        // Exposed for AXAML ComboBox binding inside DataGrid template
        public ObservableCollection<string> ItemNamesList { get; }

        public string LevelRange { get; }

        public CommonPickupRow(int bracket, List<ushort> ids,
                               Action dirty, Action<int> adjacentRefresh,
                               ObservableCollection<string> itemNamesList)
        {
            _b = bracket; _ids = ids;
            _dirty = dirty; _adjacentRefresh = adjacentRefresh;
            ItemNamesList = itemNamesList;
            LevelRange = $"Lv {bracket * 10 + 1}-{(bracket + 1) * 10}";
        }

        private int Get(int slot) => _ids[_b + slot];

        // The list's index is the item id. -1 is the box being cleared, which keeps the item.
        private void Set(int slot, int id)
        {
            if (id < 0 || id > ushort.MaxValue || _ids[_b + slot] == id) return;
            _ids[_b + slot] = (ushort)id;
            _dirty();
            _adjacentRefresh(_b + slot);
        }

        public void RefreshSlots(int absoluteId)
        {
            // If this row references absoluteId, refresh that slot
            int rel = absoluteId - _b;
            if (rel >= 0 && rel <= 8) OnPC($"Item{rel}");
        }

        public int Item0 { get => Get(0); set => Set(0, value); }
        public int Item1 { get => Get(1); set => Set(1, value); }
        public int Item2 { get => Get(2); set => Set(2, value); }
        public int Item3 { get => Get(3); set => Set(3, value); }
        public int Item4 { get => Get(4); set => Set(4, value); }
        public int Item5 { get => Get(5); set => Set(5, value); }
        public int Item6 { get => Get(6); set => Set(6, value); }
        public int Item7 { get => Get(7); set => Set(7, value); }
        public int Item8 { get => Get(8); set => Set(8, value); }
    }

    public class RarePickupRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPC([CallerMemberName] string n = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private readonly List<ushort> _ids;
        private readonly int _b;
        private readonly Action _dirty;
        private readonly Action<int> _adjacentRefresh;

        public ObservableCollection<string> ItemNamesList { get; }
        public string LevelRange { get; }

        public RarePickupRow(int bracket, List<ushort> ids,
                             Action dirty, Action<int> adjacentRefresh,
                             ObservableCollection<string> itemNamesList)
        {
            _b = bracket; _ids = ids;
            _dirty = dirty; _adjacentRefresh = adjacentRefresh;
            ItemNamesList = itemNamesList;
            LevelRange = $"Lv {bracket * 10 + 1}-{(bracket + 1) * 10}";
        }

        private int Get(int slot) => _ids[_b + slot];

        // The list's index is the item id. -1 is the box being cleared, which keeps the item.
        private void Set(int slot, int id)
        {
            if (id < 0 || id > ushort.MaxValue || _ids[_b + slot] == id) return;
            _ids[_b + slot] = (ushort)id;
            _dirty();
            _adjacentRefresh(_b + slot);
        }

        public void RefreshSlots(int absoluteId)
        {
            int rel = absoluteId - _b;
            if (rel >= 0 && rel <= 1) OnPC($"Item{rel}");
        }

        public int Item0 { get => Get(0); set => Set(0, value); }
        public int Item1 { get => Get(1); set => Set(1, value); }
    }

    public class ActivationRowVM : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPC([CallerMemberName] string n = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public string Label { get; }

        private int _value;
        public int Value
        {
            get => _value;
            set { if (_value != value) { _value = value; OnPC(); } }
        }

        private string _probability = "";
        public string Probability { get => _probability; set { _probability = value; OnPC(); } }

        private string _description = "";
        public string Description { get => _description; set { _description = value; OnPC(); } }

        // Activation rows are a READ-ONLY view (the DataGrid is IsReadOnly): the divisor + slot thresholds are
        // computed by RecalcActivation; only the divisor (ActivationDivisorEdit) is user-editable and saved.
        public ActivationRowVM(string label, int value)
        {
            Label = label; _value = value;
        }
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

        public void Refresh() { OnPC(nameof(Display)); }
    }

    /// <summary>One Rock Smash NARC entry (data/a/2/5/3): a header's item-drop odds and which of the
    /// three hardcoded tables it rolls from. <see cref="Missing"/> flags headers whose file didn't
    /// exist on disk (a modified ROM, or a header added after the NARC was last touched). Save
    /// materializes it with whatever's currently shown (defaults to Odds=0/Default if left alone).</summary>
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
            HeaderLabel = $"{data.ID:D4}: {headerName}";
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
    /// (English offsets only, see <see cref="RomInfo.IsRockSmashItemTableAvailable"/>).</summary>
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

        private const int COMMON_COUNT = 18;
        private const int RARE_COUNT   = 11;
        private const int WEIGHT_SIZE  = 9;

        // ── Item names for combo boxes ────────────────────────────────────────
        public ObservableCollection<string> ItemNames { get; } = new();
        private string[] _rawItemNames = Array.Empty<string>();
        private string[] _headerNames = Array.Empty<string>();

        // ── Pickup Table ──────────────────────────────────────────────────────
        public bool ShowPickupTab { get; }
        private List<ushort> _commonIDs = new();
        private List<ushort> _rareIDs   = new();
        private int    _activationDivisor = 10;
        private byte[] _weightTable = new byte[WEIGHT_SIZE];

        public ObservableCollection<CommonPickupRow> CommonRows { get; } = new();
        public ObservableCollection<RarePickupRow>   RareRows   { get; } = new();
        public ObservableCollection<ActivationRowVM> ActivationRows { get; } = new();

        private int _activDivisorEdit = 10;
        public int ActivationDivisorEdit
        {
            get => _activDivisorEdit;
            set
            {
                if (value < 1 || value > 255) return;
                _activDivisorEdit = value;
                _activationDivisor = value;
                OnPropertyChanged();
                RecalcActivation();
                SetPickupDirty();
            }
        }

        // ── Hidden Items ──────────────────────────────────────────────────────
        public bool ShowHiddenItemsTab { get; }
        // Hidden-item bg events run script 8000 + the entry's index (_std_hidden_item).
        private const int HIDDEN_SCRIPT_BASE = 8000;
        private int _hiddenMaxCapacity = 256;
        // False when a count site isn't the expected cmp, so nothing is patched.
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
            }
        }

        public bool HiddenItemSelected => _selectedHiddenItem != null;

        /// <summary>Opens the common script a hidden item runs, 8000 plus its script ID.</summary>
        public void GoToHiddenItemScript()
        {
            if (_selectedHiddenItem == null) return;
            string message = AvaloniaEditorLauncher.GoToScript(8000 + _selectedHiddenItem.ScriptID, null, out bool opened);
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
                _selectedHiddenItem.Refresh();
                OnPropertyChanged(nameof(SelectedScriptLabel));
                SetHiddenDirty();
                OnPropertyChanged();
            }
        }

        public string SelectedScriptLabel =>
            _selectedHiddenItem != null ? $"Use in spawnable: 8{_selectedHiddenItem.ScriptID:D3}" : "";

        // hg-engine's table is a plain C array with no fixed capacity, unlike vanilla's ARM9-reserved buffer.
        public string HiddenEntryCount => HgEngineProject.IsActive
            ? $"Entries: {HiddenItems.Count}"
            : $"Entries: {HiddenItems.Count} / {_hiddenMaxCapacity}";

        // ── Rock Smash (HGSS) ────────────────────────────────────────────────────
        // Per-header odds/table (data/a/2/5/3): available for any HGSS ROM, any language.
        // The 3 hardcoded item-slot tables are RockSmashItemSlots, at English-only-confirmed offsets.

        public bool ShowRockSmashTab { get; }
        public bool ShowRockSmashItemTables { get; private set; }
        public ObservableCollection<RockSmashHeaderRow> RockSmashRows { get; } = new();
        public RockSmashItemSlotsRow RockSmashDefaultTable { get; private set; }
        public RockSmashItemSlotsRow RockSmashRuinsOfAlphTable { get; private set; }
        public RockSmashItemSlotsRow RockSmashCliffCaveTable { get; private set; }

        public string RockSmashCount => $"Headers: {RockSmashRows.Count}";

        // ── Dirty ─────────────────────────────────────────────────────────────
        private bool _pickupDirty;
        private bool _hiddenDirty;
        private bool _rockSmashDirty;
        public bool HasUnsavedChanges => _pickupDirty || _hiddenDirty || _rockSmashDirty;
        public string UnsavedChangesDescription =>
            string.Join(", ", new[]
            {
                _pickupDirty ? "Pickup Table" : null,
                _hiddenDirty ? "Hidden Items" : null,
                _rockSmashDirty ? "Rock Smash" : null
            }.Where(s => s != null).DefaultIfEmpty("Item Table Editor"));

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

        private PickupState CurrentPickup() => new(_commonIDs.ToArray(), _rareIDs.ToArray(), _activationDivisor, (byte[])_weightTable.Clone());

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
                for (int i = 0; i < s.Pickup.Common.Length && i < _commonIDs.Count; i++) _commonIDs[i] = s.Pickup.Common[i];
                for (int i = 0; i < s.Pickup.Rare.Length && i < _rareIDs.Count; i++) _rareIDs[i] = s.Pickup.Rare[i];
                for (int i = 0; i < _commonIDs.Count; i++) RefreshCommonAdjacent(i);
                for (int i = 0; i < _rareIDs.Count; i++) RefreshRareAdjacent(i);
                _activationDivisor = _activDivisorEdit = s.Pickup.Divisor;
                Array.Copy(s.Pickup.Weights, _weightTable, Math.Min(WEIGHT_SIZE, s.Pickup.Weights.Length));
                OnPropertyChanged(nameof(ActivationDivisorEdit));
                if (ActivationRows.Count > 0) RecalcActivation();

                int keep = _selectedHiddenItem != null ? HiddenItems.IndexOf(_selectedHiddenItem) : -1;
                HiddenItems.Clear();
                foreach (int[] h in s.Hidden)
                    HiddenItems.Add(new HiddenItemRowVM((ushort)h[0], (ushort)h[1], (ushort)h[2], _rawItemNames) { Range = (byte)h[3], Padding = (ushort)h[4] });
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
            for (int i = 0; i < 30; i++) ItemNames.Add($"{i}: {_rawItemNames[i]}");

            for (int i = 0; i < COMMON_COUNT; i++) _commonIDs.Add((ushort)(i % 10));
            for (int i = 0; i < RARE_COUNT;   i++) _rareIDs.Add((ushort)(i % 10));
            for (int i = 0; i < WEIGHT_SIZE;  i++) _weightTable[i] = (byte)((i + 1) * 10);

            BuildPickupRows();
            BuildActivationRows();
            BuildHiddenDummyRows();
            BuildRockSmashDummyRows();
        }

        // ── Runtime constructor ───────────────────────────────────────────────
        public ItemTableEditorViewModel(string[] itemNames, IEnumerable<string> headerNames = null)
        {
            _rawItemNames = itemNames;
            for (int i = 0; i < itemNames.Length; i++) ItemNames.Add($"{i}: {itemNames[i]}");
            _headerNames = headerNames?.ToArray() ?? Array.Empty<string>();

            ShowPickupTab           = RomInfo.pickupTableOverlayNumber != -1;
            ShowHiddenItemsTab      = RomInfo.IsHiddenItemsEditorAvailable() || HgEngineProject.IsActive;
            ShowRockSmashTab        = RomInfo.IsRockSmashEditorAvailable();
            ShowRockSmashItemTables = RomInfo.IsRockSmashItemTableAvailable();

            if (ShowPickupTab)
            {
                if (OverlayUtils.IsCompressed(RomInfo.pickupTableOverlayNumber))
                    OverlayUtils.Decompress(RomInfo.pickupTableOverlayNumber);
                LoadPickupTable();
            }
            if (ShowHiddenItemsTab)
            {
                if (!HgEngineProject.IsActive) ARM9.DecompressIfMarked();
                LoadHiddenItems();
            }
            if (ShowRockSmashTab)
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.rockSmash });
                LoadRockSmash();
            }
            ResetUndo();
        }

        // ── Pickup load ───────────────────────────────────────────────────────
        private void LoadPickupTable()
        {
            PickupTable table = PickupTable.Read();
            _commonIDs.Clear();
            _commonIDs.AddRange(table.Common);
            _rareIDs.Clear();
            _rareIDs.AddRange(table.Rare);
            _activationDivisor = _activDivisorEdit = table.Divisor > 0 ? table.Divisor : 10;
            Array.Copy(table.Weights, _weightTable, WEIGHT_SIZE);

            BuildPickupRows();
            BuildActivationRows();
        }

        private void BuildPickupRows()
        {
            CommonRows.Clear();
            for (int b = 0; b < 10; b++)
                CommonRows.Add(new CommonPickupRow(b, _commonIDs,
                    SetPickupDirty, RefreshCommonAdjacent, ItemNames));

            RareRows.Clear();
            for (int b = 0; b < 10; b++)
                RareRows.Add(new RarePickupRow(b, _rareIDs,
                    SetPickupDirty, RefreshRareAdjacent, ItemNames));
        }

        private void RefreshCommonAdjacent(int absIdx)
        {
            foreach (CommonPickupRow row in CommonRows) row.RefreshSlots(absIdx);
        }

        private void RefreshRareAdjacent(int absIdx)
        {
            foreach (RarePickupRow row in RareRows) row.RefreshSlots(absIdx);
        }

        private void BuildActivationRows()
        {
            ActivationRows.Clear();
            RecalcActivation();
        }

        private void RecalcActivation()
        {
            double chance = 100.0 / _activationDivisor;

            if (ActivationRows.Count == 0)
            {
                // Build initial rows
                ActivationRowVM divisorRow = new ActivationRowVM("Activation %", _activationDivisor);
                divisorRow.Probability  = $"{chance:F2}%";
                divisorRow.Description  = "1/divisor × 100 (modulo-based)";
                ActivationRows.Add(divisorRow);

                int prev = 0;
                for (int i = 0; i < WEIGHT_SIZE; i++)
                {
                    int thresh = _weightTable[i];
                    int range  = thresh - prev;
                    double prob = chance / 100.0 * range;
                    ActivationRowVM row = new ActivationRowVM($"Slot {i + 1}", thresh);
                    row.Probability  = $"{prob:F2}%";
                    row.Description  = $"{prev}, {thresh - 1} ({range} values)";
                    ActivationRows.Add(row);
                    prev = thresh;
                }

                // Rare
                double rareProb = chance / 100.0 * 2;
                ActivationRowVM rareRow = new ActivationRowVM("Rare (98-99)", 0);
                rareRow.Probability = $"{rareProb:F2}%";
                rareRow.Description = "98 to 99 (2 values)";
                ActivationRows.Add(rareRow);
            }
            else
            {
                // Update existing rows
                ActivationRows[0].Probability = $"{chance:F2}%";
                int prev = 0;
                for (int i = 0; i < WEIGHT_SIZE; i++)
                {
                    int thresh = _weightTable[i];
                    int range  = thresh - prev;
                    double prob = chance / 100.0 * range;
                    ActivationRowVM row = ActivationRows[i + 1];
                    row.Value       = thresh;
                    row.Probability = $"{prob:F2}%";
                    row.Description = $"{prev}, {thresh - 1} ({range} values)";
                    prev = thresh;
                }
            }
        }

        public bool UpdateWeightThreshold(int slotIndex, int newValue)
        {
            // slotIndex 0-8, newValue 0-100
            int prev = slotIndex > 0 ? _weightTable[slotIndex - 1] : 0;
            int next = slotIndex < WEIGHT_SIZE - 1 ? _weightTable[slotIndex + 1] : 100;
            if (newValue <= prev || newValue >= next) return false;
            _weightTable[slotIndex] = (byte)newValue;
            RecalcActivation();
            SetPickupDirty();
            return true;
        }

        // ── Hidden items load ─────────────────────────────────────────────────
        // Hidden Items isn't one of DSPRE's owned domains for the packed ROM, so the vanilla ARM9-offset
        // read below would show a stale packed-ROM snapshot rather than the checkout's real
        // data/HiddenItems.c when hg-engine is linked.
        private void LoadHiddenItems()
        {
            HiddenItems.Clear();

            if (HgEngineProject.IsActive)
            {
                if (HgEngineHiddenItems.TryLoad(out List<HgEngineHiddenItems.Entry> entries, out string err))
                {
                    foreach (HgEngineHiddenItems.Entry e in entries)
                        HiddenItems.Add(new HiddenItemRowVM((ushort)e.ItemId, (ushort)e.Quantity, (ushort)e.Index, _rawItemNames));
                }
                else
                {
                    AppLogger.Error($"hg-engine hidden items read failed: {err}");
                }
                if (HiddenItems.Count > 0) SelectedHiddenItem = HiddenItems[0];
                OnPropertyChanged(nameof(HiddenEntryCount));
                return;
            }

            _hiddenMaxCapacity = RomInfo.hiddenItemTableCapacity;
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

            if (HiddenItems.Count > 0) SelectedHiddenItem = HiddenItems[0];
            OnPropertyChanged(nameof(HiddenEntryCount));
        }

        private static int ReadHiddenItemCount() => HiddenItemTable.Count();

        private static List<int> HiddenItemUsers(int index)
        {
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.eventFiles });
            List<int> files = new List<int>();
            int script = HIDDEN_SCRIPT_BASE + index;
            int fileCount = Filesystem.GetEventFileCount();
            for (int i = 0; i < fileCount; i++)
                if (new EventFile(i).spawnables.Any(sp => sp.scriptNumber == script)) files.Add(i);
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
            ushort sid = 95;
            while (used.Contains(sid) && sid < 256) sid++;
            HiddenItemRowVM entry = new HiddenItemRowVM(0, 1, sid, _rawItemNames);
            HiddenItems.Add(entry);
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
                await DialogHelper.ShowError(
                    $"Event file {string.Join(", ", users.Take(5))}{(users.Count > 5 ? " and more" : "")} still uses this entry. Change or delete those events first.",
                    "Hidden Items");
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
            OnPropertyChanged(nameof(RockSmashCount));

            if (ShowRockSmashItemTables)
            {
                try { LoadRockSmashItemTables(); }
                // An hg-engine source DSPRE can't read hides the tables rather than showing wrong ones.
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
                if (error != null) await DialogHelper.ShowError($"Hidden items were not saved.\n{error}", "Item Tables");
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
            new PickupTable
            {
                Common = _commonIDs.ToArray(), Rare = _rareIDs.ToArray(),
                Divisor = (byte)_activationDivisor, Weights = (byte[])_weightTable.Clone(),
            }.Write();

            _pickupDirty = false;
            SavedTab();
            SaveNotice.Saved(UnsavedChangesDescription);
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void SaveHiddenItems()
        {
            if (HgEngineProject.IsActive)
            {
                _ = SaveHiddenItemsToSourceAsync();
                return;
            }

            HiddenItemRowVM empty = HiddenItems.FirstOrDefault(h => h.ItemID == 0);
            string refusal = !_hiddenSitesValid ? "The ARM9 count checks aren't where expected."
                : HiddenItems.Count > _hiddenMaxCapacity ? $"There is room for {_hiddenMaxCapacity} entries."
                : empty != null ? $"Script {empty.ScriptID} has no item. Pick one or remove the entry."
                : null;
            if (refusal != null)
            {
                _ = DialogHelper.ShowError($"Hidden items were not saved.\n{refusal}", "Item Tables");
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
                    _ = DialogHelper.ShowError("The Rock Smash items were not saved:\n" + e.Message, "Item Tables");
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
