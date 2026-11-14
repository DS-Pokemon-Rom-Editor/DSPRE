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

        // DP and Pt tables are full, so Add reuses a removed entry's script, whose flag a save may hold.
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

        // hg-engine's table is a plain C array with no fixed capacity, unlike the ARM9 buffer.
        public string HiddenEntryCount => HgEngineProject.IsActive
            ? $"Entries: {HiddenItems.Count}"
            : $"Entries: {HiddenItems.Count} / {_hiddenMaxCapacity}";

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
                AppLogger.Error("Hidden items: the ARM9 count checks aren't where expected. The table is left alone.");
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

        public async System.Threading.Tasks.Task AddHiddenItemAsync()
        {
            if (!HgEngineProject.IsActive && HiddenItems.Count >= _hiddenMaxCapacity)
            {
                await DialogHelper.ShowInfoAsync("The table is full. Remove an entry first.", "Hidden items");
                return;
            }
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
                string files = string.Join(", ", users.Take(5)) + (users.Count > 5 ? " and more" : "");
                await DialogHelper.ShowErrorAsync(users.Count == 1
                    ? $"Event file {files} still uses this entry. Change or delete its events first."
                    : $"Event files {files} still use this entry. Change or delete those events first.", "Hidden items");
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
    }
}
