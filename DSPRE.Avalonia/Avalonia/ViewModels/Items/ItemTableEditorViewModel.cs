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
    public partial class ItemTableEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo, IDetachable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public ObservableCollection<string> ItemNames { get; } = new();
        private string[] _rawItemNames = Array.Empty<string>();
        private string[] _headerNames = Array.Empty<string>();

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

        private sealed record PickupState(ushort[] Common, ushort[] Rare, int Divisor, byte[] Weights);
        private sealed record TablesState(PickupState Pickup, int[][] Hidden, int[][] RockSmash, ushort[][] Slots);
        private ByteStateUndo _undo;
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
            _undo = new ByteStateUndo(() => DSPRE.Avalonia.UndoJson.Take(Current()), ApplyState, RaiseUndo);
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
                AppEvents.RomPatchStateChanged += OnRomPatchStateChanged;
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

        private void OnRomPatchStateChanged(object sender, EventArgs e) => OnPatchStateChanged();
        public void Detach() => AppEvents.RomPatchStateChanged -= OnRomPatchStateChanged;

        /// <summary>Rereads Pickup once the toolbox gives each range its own items, unless edits here are still unsaved.</summary>
        public void OnPatchStateChanged()
        {
            if (_pickup == null || _pickup.Independent || HasUnsavedChanges || !PickupSlotsPatch.IsApplied) return;
            LoadPickupTable();
            ResetUndo();
        }
    }
}
