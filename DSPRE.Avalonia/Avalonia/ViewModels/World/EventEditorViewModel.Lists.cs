using DSPRE.Avalonia;
using DSPRE.Avalonia.Gl;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using global::Avalonia.Controls;
using global::Avalonia.Platform.Storage;
using LibNDSFormats.NSBMD;
using LibNDSFormats.NSBTX;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static DSPRE.RomInfo;
using DSPRE.Avalonia.Data;
using System.Text;

namespace DSPRE.Avalonia.ViewModels.World
{
    public partial class EventEditorViewModel
    {
        // The selected event's cell, else a header cell: (0,0) is usually another header's map.
        private (int x, int y) NewEventCell()
        {
            if (_current != null) return (_current.XMatrixPosition, _current.YMatrixPosition);
            HashSet<(int x, int y)> cells = HeaderCells();
            if (cells != null)
                foreach ((int x, int y) c in cells) return c;
            return (0, 0);
        }

        public void AddSpawnable() { if (_file == null) return; (int cx, int cy) = NewEventCell(); _file.Spawnables.Add(new Spawnable(cx, cy)); RefreshLists(); Dirty(); SelectedSpawnableIndex = _file.Spawnables.Count - 1; }
        public void RemoveSpawnable() { if (_file == null || _selSpawn < 0 || _selSpawn >= _file.Spawnables.Count) return; _file.Spawnables.RemoveAt(_selSpawn); RefreshLists(); Dirty(); SelectedSpawnableIndex = -1; RefreshMarkers(); }
        public void AddOverworld()
        {
            if (_file == null) return;
            (int cx, int cy) = NewEventCell();
            _file.Overworlds.Add(new Overworld(FreeOverworldId(), cx, cy));
            RefreshLists(); Dirty(); SelectedOverworldIndex = _file.Overworlds.Count - 1;
        }
        public void RemoveOverworld() { if (_file == null || _selOw < 0 || _selOw >= _file.Overworlds.Count) return; _file.Overworlds.RemoveAt(_selOw); RefreshLists(); Dirty(); SelectedOverworldIndex = -1; RefreshMarkers(); }
        public void AddWarp() { if (_file == null) return; (int cx, int cy) = NewEventCell(); _file.Warps.Add(new Warp(cx, cy)); RefreshLists(); Dirty(); SelectedWarpIndex = _file.Warps.Count - 1; }
        public void RemoveWarp() { if (_file == null || _selWarp < 0 || _selWarp >= _file.Warps.Count) return; _file.Warps.RemoveAt(_selWarp); RefreshLists(); Dirty(); SelectedWarpIndex = -1; RefreshMarkers(); }
        public void AddTrigger() { if (_file == null) return; (int cx, int cy) = NewEventCell(); _file.Triggers.Add(new Trigger(cx, cy)); RefreshLists(); Dirty(); SelectedTriggerIndex = _file.Triggers.Count - 1; }
        public void RemoveTrigger() { if (_file == null || _selTrig < 0 || _selTrig >= _file.Triggers.Count) return; _file.Triggers.RemoveAt(_selTrig); RefreshLists(); Dirty(); SelectedTriggerIndex = -1; RefreshMarkers(); }

        public void DuplicateSpawnable() { if (_file == null || _spawn == null) return; _file.Spawnables.Add(new Spawnable(_spawn)); RefreshLists(); Dirty(); SelectedSpawnableIndex = _file.Spawnables.Count - 1; }
        public void DuplicateOverworld() { if (_file == null || _ow == null) return; _file.Overworlds.Add(new Overworld(_ow) { OwID = (ushort)FreeOverworldId() }); RefreshLists(); Dirty(); SelectedOverworldIndex = _file.Overworlds.Count - 1; }

        private int FreeOverworldId()
        {
            int id = 0;
            while (_file.Overworlds.Any(o => o.OwID == id)) id++;
            return id;
        }
        public void DuplicateWarp() { if (_file == null || _warp == null) return; _file.Warps.Add(new Warp(_warp)); RefreshLists(); Dirty(); SelectedWarpIndex = _file.Warps.Count - 1; }
        public void DuplicateTrigger() { if (_file == null || _trig == null) return; _file.Triggers.Add(new Trigger(_trig)); RefreshLists(); Dirty(); SelectedTriggerIndex = _file.Triggers.Count - 1; }

        /// <summary>Follows the selected warp to its destination header's event file.</summary>
        public void TestWarp()
        {
            if (_warp == null) return;
            try
            {
                MapHeader h = MapHeader.GetMapHeader(_warp.Header);
                if (h == null) { StatusText = $"Destination header {_warp.Header} not found."; return; }
                int dest = h.EventFileID;
                if (dest < 0 || dest >= EventNames.Count) { StatusText = $"Header {_warp.Header} at event file {dest} is out of range."; return; }
                SelectedEventIndex = dest;
                StatusText = $"Warp leads to header {_warp.Header} at event file {dest}.";
            }
            catch (Exception ex) when (AppLogger.Noted("EventEditorViewModel.TestWarp", ex)) { StatusText = "Test warp failed: " + ex.Message; }
        }

        public void SortOverworldsAsc() { if (_file == null) return; _file.Overworlds.Sort((a, b) => a.OwID.CompareTo(b.OwID)); RefreshLists(); Dirty(); }
        public void SortOverworldsDesc() { if (_file == null) return; _file.Overworlds.Sort((a, b) => b.OwID.CompareTo(a.OwID)); RefreshLists(); Dirty(); }

        public void AddEventFile()
        {
            try
            {
                int newId = _archive.Add(new EventFile().ToByteArray());
                EventNames.Add("Event file " + newId);
                SelectedEventIndex = newId;
                OnPropertyChanged(nameof(HasUnsavedChanges));
                StatusText = $"Added event file {newId}.";
            }
            catch (Exception ex) when (AppLogger.Noted("EventEditorViewModel.AddEventFile", ex)) { _ = DialogHelper.ShowErrorAsync($"Couldn't add event file:\n{ex.Message}", "Event editor"); }
        }

        public async Task RemoveLastEventFileAsync()
        {
            int count = EventNames.Count;
            if (count == 0) return;
            int last = count - 1;
            // A header still pointing at the file would load garbage, so it stays while one does.
            List<(ushort Header, string Name)> uses = HeaderLinks.HeadersLinking(HeaderLinks.Kind.Event, last);
            if (uses.Count > 0)
            {
                await DialogHelper.ShowInfoAsync($"Event file {last} stays: these headers still use it." + Environment.NewLine + HeaderLinks.Describe(uses)
                    + Environment.NewLine + "Point them at another event file first.", "Event editor");
                return;
            }
            if (!await DialogHelper.AskYesNoAsync($"Delete the last event file ({last})?", "Delete event file?")) return;
            try
            {
                // The edits belong to the file being deleted.
                if (_selectedIndex == last) { SetClean(); SelectedEventIndex = last - 1; }
                _archive.RemoveLast();
                EventNames.RemoveAt(last);
                OnPropertyChanged(nameof(HasUnsavedChanges));
                StatusText = $"Removed event file {last}.";
            }
            catch (Exception ex) when (AppLogger.Noted("EventEditorViewModel.RemoveLastEventFileAsync", ex)) { _ = DialogHelper.ShowErrorAsync($"Couldn't remove event file:\n{ex.Message}", "Event editor"); }
        }
    }
}
