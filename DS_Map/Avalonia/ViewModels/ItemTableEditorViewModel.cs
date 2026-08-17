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

namespace DSPRE.Avalonia.ViewModels
{
    // ─── Row VMs ──────────────────────────────────────────────────────────────
    public class CommonPickupRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPC([CallerMemberName] string n = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private readonly List<ushort> _ids;
        private readonly string[] _names;
        private readonly int _b; // bracket index
        private readonly Action _dirty;
        private readonly Action<int> _adjacentRefresh; // pass absolute id index

        // Exposed for AXAML ComboBox binding inside DataGrid template
        public ObservableCollection<string> ItemNamesList { get; }

        public string LevelRange { get; }

        public CommonPickupRow(int bracket, List<ushort> ids, string[] names,
                               Action dirty, Action<int> adjacentRefresh,
                               ObservableCollection<string> itemNamesList)
        {
            _b = bracket; _ids = ids; _names = names;
            _dirty = dirty; _adjacentRefresh = adjacentRefresh;
            ItemNamesList = itemNamesList;
            LevelRange = $"Lv {bracket * 10 + 1}-{(bracket + 1) * 10}";
        }

        private string Get(int slot)
        {
            var id = _ids[_b + slot];
            return id < _names.Length ? $"{id}: {_names[id]}" : $"{id}: ???";
        }

        private void Set(int slot, string val)
        {
            if (val == null) return;
            int colon = val.IndexOf(':');
            if (colon > 0 && ushort.TryParse(val.Substring(0, colon).Trim(), out ushort id))
            {
                _ids[_b + slot] = id;
                _dirty();
                _adjacentRefresh(_b + slot);
            }
        }

        public void RefreshSlots(int absoluteId)
        {
            // If this row references absoluteId, refresh that slot
            int rel = absoluteId - _b;
            if (rel >= 0 && rel <= 8) OnPC($"Item{rel}");
        }

        public string Item0 { get => Get(0); set => Set(0, value); }
        public string Item1 { get => Get(1); set => Set(1, value); }
        public string Item2 { get => Get(2); set => Set(2, value); }
        public string Item3 { get => Get(3); set => Set(3, value); }
        public string Item4 { get => Get(4); set => Set(4, value); }
        public string Item5 { get => Get(5); set => Set(5, value); }
        public string Item6 { get => Get(6); set => Set(6, value); }
        public string Item7 { get => Get(7); set => Set(7, value); }
        public string Item8 { get => Get(8); set => Set(8, value); }
    }

    public class RarePickupRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPC([CallerMemberName] string n = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private readonly List<ushort> _ids;
        private readonly string[] _names;
        private readonly int _b;
        private readonly Action _dirty;
        private readonly Action<int> _adjacentRefresh;

        public ObservableCollection<string> ItemNamesList { get; }
        public string LevelRange { get; }

        public RarePickupRow(int bracket, List<ushort> ids, string[] names,
                             Action dirty, Action<int> adjacentRefresh,
                             ObservableCollection<string> itemNamesList)
        {
            _b = bracket; _ids = ids; _names = names;
            _dirty = dirty; _adjacentRefresh = adjacentRefresh;
            ItemNamesList = itemNamesList;
            LevelRange = $"Lv {bracket * 10 + 1}-{(bracket + 1) * 10}";
        }

        private string Get(int slot)
        {
            var id = _ids[_b + slot];
            return id < _names.Length ? $"{id}: {_names[id]}" : $"{id}: ???";
        }

        private void Set(int slot, string val)
        {
            if (val == null) return;
            int colon = val.IndexOf(':');
            if (colon > 0 && ushort.TryParse(val.Substring(0, colon).Trim(), out ushort id))
            {
                _ids[_b + slot] = id;
                _dirty();
                _adjacentRefresh(_b + slot);
            }
        }

        public void RefreshSlots(int absoluteId)
        {
            int rel = absoluteId - _b;
            if (rel >= 0 && rel <= 1) OnPC($"Item{rel}");
        }

        public string Item0 { get => Get(0); set => Set(0, value); }
        public string Item1 { get => Get(1); set => Set(1, value); }
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

        private string[] _names;
        public HiddenItemRowVM(ushort item, ushort amount, ushort script, string[] names)
        { ItemID = item; Amount = amount; ScriptID = script; _names = names; }

        public string Display =>
            $"Script {ScriptID} (8{ScriptID:D3}): {(ItemID < _names.Length ? _names[ItemID] : "???")} x{Amount}";

        public void Refresh() { OnPC(nameof(Display)); }
    }

    /// <summary>One Rock Smash NARC entry (data/a/2/5/3): a header's item-drop odds and which of the
    /// three hardcoded tables it rolls from. <see cref="Missing"/> flags headers whose file didn't
    /// exist on disk (a modified ROM, or a header added after the NARC was last touched) — Save
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

        // 0 = Default, 1 = Ruins of Alph, 2 = Cliff Cave — matches RockSmashData.TableType.
        public int TypeIndex
        {
            get => (int)Data.Type;
            set
            {
                var t = (RockSmashData.TableType)value;
                if (Data.Type == t) return;
                Data.Type = t;
                OnPC();
                _dirty();
            }
        }

        public void RefreshMissing() { OnPC(nameof(Missing)); OnPC(nameof(MissingLabel)); }
    }

    /// <summary>One of Rock Smash's three fixed 8-slot item tables, hardcoded in HGSS's ov001.bin
    /// (English offsets only — see <see cref="RomInfo.IsRockSmashItemTableAvailable"/>).</summary>
    public class RockSmashItemSlotsRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPC([CallerMemberName] string n = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public string Label { get; }
        public readonly ushort[] ItemIDs;
        private readonly string[] _names;
        private readonly Action _dirty;

        public RockSmashItemSlotsRow(string label, ushort[] itemIDs, string[] names, Action dirty)
        {
            Label = label; ItemIDs = itemIDs; _names = names; _dirty = dirty;
        }

        private string Get(int slot) =>
            ItemIDs[slot] < _names.Length ? $"{ItemIDs[slot]}: {_names[ItemIDs[slot]]}" : $"{ItemIDs[slot]}: ???";

        private void Set(int slot, string val)
        {
            if (val == null) return;
            int colon = val.IndexOf(':');
            if (colon > 0 && ushort.TryParse(val.Substring(0, colon).Trim(), out ushort id))
            {
                ItemIDs[slot] = id;
                _dirty();
            }
        }

        public string Item0 { get => Get(0); set => Set(0, value); }
        public string Item1 { get => Get(1); set => Set(1, value); }
        public string Item2 { get => Get(2); set => Set(2, value); }
        public string Item3 { get => Get(3); set => Set(3, value); }
        public string Item4 { get => Get(4); set => Set(4, value); }
        public string Item5 { get => Get(5); set => Set(5, value); }
        public string Item6 { get => Get(6); set => Set(6, value); }
        public string Item7 { get => Get(7); set => Set(7, value); }
    }

    // ─── Main ViewModel ───────────────────────────────────────────────────────
    public class ItemTableEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
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
        private const int HIDDEN_ENTRY_SIZE = 8;
        private const int HIDDEN_TABLE_OFFSET = 0xFA558;
        private const int HIDDEN_LENGTH_OFFSET1 = 0x405A8;
        private const int HIDDEN_LENGTH_OFFSET2 = 0x40610;
        private const int HIDDEN_TABLE_LEN_OFFSET = 0x405E4;
        private const int HIDDEN_MAX_OFFSET  = 0x405E8;
        private int _hiddenMaxCapacity = 256;

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
        // The 3 hardcoded item-slot tables live in ov001.bin at English-only-confirmed offsets.
        private const int ROCKSMASH_OVERLAY = 1;
        private const uint ROCKSMASH_RUINS_OF_ALPH_OFFSET = 0x23D04;
        private const uint ROCKSMASH_DEFAULT_OFFSET = 0x23D14;
        private const uint ROCKSMASH_CLIFF_CAVE_OFFSET = 0x23D24;
        private const int ROCKSMASH_SLOTS = 8;

        public bool ShowRockSmashTab { get; }
        public bool ShowRockSmashItemTables { get; }
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

        private void SetPickupDirty() { _pickupDirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetHiddenDirty() { _hiddenDirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetRockSmashDirty() { _rockSmashDirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }

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
                if (!HgEngineProject.IsActive && ARM9.CheckCompressionMark()) ARM9.Decompress(RomInfo.arm9Path);
                LoadHiddenItems();
            }
            if (ShowRockSmashTab)
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.rockSmash });
                LoadRockSmash();
            }
        }

        // ── Pickup load ───────────────────────────────────────────────────────
        private void LoadPickupTable()
        {
            string path = OverlayUtils.GetPath(RomInfo.pickupTableOverlayNumber);

            _commonIDs.Clear();
            var common = DSUtils.ReadFromFile(path, RomInfo.pickupCommonItemsOffset, COMMON_COUNT * 2);
            for (int i = 0; i < COMMON_COUNT; i++)
                _commonIDs.Add(BitConverter.ToUInt16(common, i * 2));

            _rareIDs.Clear();
            var rare = DSUtils.ReadFromFile(path, RomInfo.pickupRareItemsOffset, RARE_COUNT * 2);
            for (int i = 0; i < RARE_COUNT; i++)
                _rareIDs.Add(BitConverter.ToUInt16(rare, i * 2));

            var divisorByte = DSUtils.ReadFromFile(path, RomInfo.pickupActivationDivisorOffset, 1);
            _activationDivisor = _activDivisorEdit = divisorByte[0] > 0 ? divisorByte[0] : 10;

            var weights = DSUtils.ReadFromFile(path, RomInfo.pickupWeightTableOffset, WEIGHT_SIZE);
            Array.Copy(weights, _weightTable, WEIGHT_SIZE);

            BuildPickupRows();
            BuildActivationRows();
        }

        private void BuildPickupRows()
        {
            CommonRows.Clear();
            for (int b = 0; b < 10; b++)
                CommonRows.Add(new CommonPickupRow(b, _commonIDs, _rawItemNames,
                    SetPickupDirty, RefreshCommonAdjacent, ItemNames));

            RareRows.Clear();
            for (int b = 0; b < 10; b++)
                RareRows.Add(new RarePickupRow(b, _rareIDs, _rawItemNames,
                    SetPickupDirty, RefreshRareAdjacent, ItemNames));
        }

        private void RefreshCommonAdjacent(int absIdx)
        {
            foreach (var row in CommonRows) row.RefreshSlots(absIdx);
        }

        private void RefreshRareAdjacent(int absIdx)
        {
            foreach (var row in RareRows) row.RefreshSlots(absIdx);
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
                var divisorRow = new ActivationRowVM("Activation %", _activationDivisor);
                divisorRow.Probability  = $"{chance:F2}%";
                divisorRow.Description  = "1/divisor × 100 (modulo-based)";
                ActivationRows.Add(divisorRow);

                int prev = 0;
                for (int i = 0; i < WEIGHT_SIZE; i++)
                {
                    int thresh = _weightTable[i];
                    int range  = thresh - prev;
                    double prob = chance / 100.0 * range;
                    var row = new ActivationRowVM($"Slot {i + 1}", thresh);
                    row.Probability  = $"{prob:F2}%";
                    row.Description  = $"{prev}–{thresh - 1} ({range} values)";
                    ActivationRows.Add(row);
                    prev = thresh;
                }

                // Rare
                double rareProb = chance / 100.0 * 2;
                var rareRow = new ActivationRowVM("Rare (98-99)", 0);
                rareRow.Probability = $"{rareProb:F2}%";
                rareRow.Description = "98–99 (2 values)";
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
                    var row = ActivationRows[i + 1];
                    row.Value       = thresh;
                    row.Probability = $"{prob:F2}%";
                    row.Description = $"{prev}–{thresh - 1} ({range} values)";
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
                if (HgEngineHiddenItems.TryLoad(out var entries, out string err))
                {
                    foreach (var e in entries)
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

            byte[] lenData = ARM9.ReadBytes(HIDDEN_TABLE_LEN_OFFSET, 1);
            int tableLen = lenData[0];

            byte[] maxData = ARM9.ReadBytes(HIDDEN_MAX_OFFSET, 1);
            _hiddenMaxCapacity = maxData[0];

            if (tableLen < 0 || tableLen > _hiddenMaxCapacity)
                tableLen = Math.Min(256, _hiddenMaxCapacity);

            byte[] table = ARM9.ReadBytes(HIDDEN_TABLE_OFFSET, tableLen * HIDDEN_ENTRY_SIZE);

            for (int i = 0; i < tableLen; i++)
            {
                int off = i * HIDDEN_ENTRY_SIZE;
                ushort itemID   = BitConverter.ToUInt16(table, off);
                if (itemID == 0) break;
                ushort amount   = table[off + 2];
                ushort scriptID = BitConverter.ToUInt16(table, off + 6);
                HiddenItems.Add(new HiddenItemRowVM(itemID, amount, scriptID, _rawItemNames));
            }

            if (HiddenItems.Count > 0) SelectedHiddenItem = HiddenItems[0];
            OnPropertyChanged(nameof(HiddenEntryCount));
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
            var used = new HashSet<ushort>(HiddenItems.Select(h => h.ScriptID));
            ushort sid = 95;
            while (used.Contains(sid) && sid < 256) sid++;
            var entry = new HiddenItemRowVM(0, 1, sid, _rawItemNames);
            HiddenItems.Add(entry);
            SelectedHiddenItem = entry;
            SetHiddenDirty();
            OnPropertyChanged(nameof(HiddenEntryCount));
        }

        public void RemoveSelectedHiddenItem()
        {
            if (_selectedHiddenItem == null) return;
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
                var data = new RockSmashData((ushort)i);
                string name = i < _headerNames.Length ? _headerNames[i] : $"Header {i}";
                RockSmashRows.Add(new RockSmashHeaderRow(data, name, SetRockSmashDirty));
            }
            OnPropertyChanged(nameof(RockSmashCount));

            if (ShowRockSmashItemTables)
            {
                if (OverlayUtils.IsCompressed(ROCKSMASH_OVERLAY)) OverlayUtils.Decompress(ROCKSMASH_OVERLAY);
                string path = OverlayUtils.GetPath(ROCKSMASH_OVERLAY);

                RockSmashDefaultTable = new RockSmashItemSlotsRow("Default",
                    ReadSlots(path, ROCKSMASH_DEFAULT_OFFSET), _rawItemNames, SetRockSmashDirty);
                RockSmashRuinsOfAlphTable = new RockSmashItemSlotsRow("Ruins of Alph",
                    ReadSlots(path, ROCKSMASH_RUINS_OF_ALPH_OFFSET), _rawItemNames, SetRockSmashDirty);
                RockSmashCliffCaveTable = new RockSmashItemSlotsRow("Cliff Cave",
                    ReadSlots(path, ROCKSMASH_CLIFF_CAVE_OFFSET), _rawItemNames, SetRockSmashDirty);

                OnPropertyChanged(nameof(RockSmashDefaultTable));
                OnPropertyChanged(nameof(RockSmashRuinsOfAlphTable));
                OnPropertyChanged(nameof(RockSmashCliffCaveTable));
            }
        }

        private static ushort[] ReadSlots(string overlayPath, uint offset)
        {
            byte[] raw = DSUtils.ReadFromFile(overlayPath, offset, ROCKSMASH_SLOTS * 2);
            var slots = new ushort[ROCKSMASH_SLOTS];
            for (int i = 0; i < ROCKSMASH_SLOTS; i++) slots[i] = BitConverter.ToUInt16(raw, i * 2);
            return slots;
        }

        private void BuildRockSmashDummyRows()
        {
            for (int i = 0; i < 6; i++)
                RockSmashRows.Add(new RockSmashHeaderRow(new RockSmashData((ushort)i, ""), $"Route {i}", SetRockSmashDirty));

            ushort[] Dummy() => new ushort[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            RockSmashDefaultTable      = new RockSmashItemSlotsRow("Default", Dummy(), _rawItemNames, SetRockSmashDirty);
            RockSmashRuinsOfAlphTable  = new RockSmashItemSlotsRow("Ruins of Alph", Dummy(), _rawItemNames, SetRockSmashDirty);
            RockSmashCliffCaveTable    = new RockSmashItemSlotsRow("Cliff Cave", Dummy(), _rawItemNames, SetRockSmashDirty);
        }

        // ── Save ──────────────────────────────────────────────────────────────
        public void SaveChanges()
        {
            if (_pickupDirty && ShowPickupTab) SavePickupTable();
            if (_hiddenDirty && ShowHiddenItemsTab) SaveHiddenItems();
            if (_rockSmashDirty && ShowRockSmashTab) SaveRockSmash();
        }

        private void SavePickupTable()
        {
            string path = OverlayUtils.GetPath(RomInfo.pickupTableOverlayNumber);

            var common = new byte[COMMON_COUNT * 2];
            for (int i = 0; i < _commonIDs.Count; i++)
                BitConverter.GetBytes(_commonIDs[i]).CopyTo(common, i * 2);
            DSUtils.WriteToFile(path, common, RomInfo.pickupCommonItemsOffset);

            var rare = new byte[RARE_COUNT * 2];
            for (int i = 0; i < _rareIDs.Count; i++)
                BitConverter.GetBytes(_rareIDs[i]).CopyTo(rare, i * 2);
            DSUtils.WriteToFile(path, rare, RomInfo.pickupRareItemsOffset);

            DSUtils.WriteToFile(path, new byte[] { (byte)_activationDivisor }, RomInfo.pickupActivationDivisorOffset);
            DSUtils.WriteToFile(path, _weightTable, RomInfo.pickupWeightTableOffset);

            _pickupDirty = false;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void SaveHiddenItems()
        {
            if (HgEngineProject.IsActive)
            {
                var entries = new List<HgEngineHiddenItems.Entry>(HiddenItems.Count);
                foreach (var e in HiddenItems)
                    entries.Add(new HgEngineHiddenItems.Entry { ItemId = e.ItemID, Quantity = e.Amount, Index = e.ScriptID });
                if (!HgEngineHiddenItems.TrySave(entries, out string err))
                    AppLogger.Error($"hg-engine hidden items write failed: {err}");

                _hiddenDirty = false;
                OnPropertyChanged(nameof(HasUnsavedChanges));
                return;
            }

            int tableLen = HiddenItems.Count;
            byte[] table = new byte[_hiddenMaxCapacity * HIDDEN_ENTRY_SIZE];

            for (int i = 0; i < tableLen; i++)
            {
                int off = i * HIDDEN_ENTRY_SIZE;
                var e = HiddenItems[i];
                BitConverter.GetBytes(e.ItemID).CopyTo(table, off);
                table[off + 2] = (byte)e.Amount;
                BitConverter.GetBytes(e.ScriptID).CopyTo(table, off + 6);
            }

            ARM9.WriteBytes(table, HIDDEN_TABLE_OFFSET);
            ARM9.WriteBytes(new byte[] { (byte)tableLen }, HIDDEN_TABLE_LEN_OFFSET);

            _hiddenDirty = false;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void SaveRockSmash()
        {
            // Whole-file overwrite per header (matches Headbutt's per-header-NARC convention);
            // File.WriteAllBytes creates the file if it didn't exist, so headers that were "missing"
            // from a/2/5/3 (modified ROM, or a header added since) get materialized here.
            foreach (var row in RockSmashRows)
            {
                row.Data.SaveToFile();
                row.RefreshMissing();
            }

            if (ShowRockSmashItemTables)
            {
                string path = OverlayUtils.GetPath(ROCKSMASH_OVERLAY);
                WriteSlots(path, ROCKSMASH_DEFAULT_OFFSET, RockSmashDefaultTable.ItemIDs);
                WriteSlots(path, ROCKSMASH_RUINS_OF_ALPH_OFFSET, RockSmashRuinsOfAlphTable.ItemIDs);
                WriteSlots(path, ROCKSMASH_CLIFF_CAVE_OFFSET, RockSmashCliffCaveTable.ItemIDs);
            }

            _rockSmashDirty = false;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private static void WriteSlots(string overlayPath, uint offset, ushort[] slots)
        {
            byte[] raw = new byte[ROCKSMASH_SLOTS * 2];
            for (int i = 0; i < ROCKSMASH_SLOTS; i++) BitConverter.GetBytes(slots[i]).CopyTo(raw, i * 2);
            DSUtils.WriteToFile(overlayPath, raw, offset);
        }

        public void DiscardChanges()
        {
            _pickupDirty = false;
            _hiddenDirty = false;
            _rockSmashDirty = false;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }
}
