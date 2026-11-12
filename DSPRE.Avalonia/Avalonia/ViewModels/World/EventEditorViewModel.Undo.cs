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
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty || _archive.IsDirty;
        public string UnsavedChangesDescription => StagedArchive.Join($"Event file {_selectedIndex}", _dirty, _archive.Describe("event file", "event files"));
        public void SaveChanges() => Save();
        public void DiscardChanges()
        {
            // Added and removed files go with the open file's edits.
            _dirty = false;
            _archive.Discard();
            ListEventFiles();
            if (_selectedIndex >= EventNames.Count) { _selectedIndex = EventNames.Count - 1; OnPropertyChanged(nameof(SelectedEventIndex)); }
            OnPropertyChanged(nameof(HasUnsavedChanges));
            if (_selectedIndex >= 0) LoadFile(_selectedIndex);
        }

        // Added, removed and saved event files wait here until Save.
        private readonly StagedArchive _archive = new(DirNames.EventFiles);

        private void ListEventFiles()
        {
            int count = _archive.Count;
            EventNames.Clear();
            for (int i = 0; i < count; i++) EventNames.Add("Event file " + i);
        }
        private void Dirty() { _undo?.Record(); if (_dirty) return; _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        private ByteStateUndo _undo;
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() { _undo?.Undo(); SyncDirtyWithUndo(); }
        public void Redo() { _undo?.Redo(); SyncDirtyWithUndo(); }
        private void SyncDirtyWithUndo()
        {
            if (_undo == null || _undo.IsDirty == _dirty) return;
            _dirty = _undo.IsDirty; OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void StartUndo()
        {
            _undo = _file == null ? null : new ByteStateUndo(() => _file.ToByteArray(), RestoreState,
                () => { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); });
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        }

        private void RestoreState(byte[] state)
        {
            int spawn = _selSpawn, ow = _selOw, warp = _selWarp, trig = _selTrig;
            Event active = _current;
            int id = _file.ID;
            using (MemoryStream ms = new MemoryStream(state)) _file = new EventFile(ms) { ID = id };
            _current = null; _spawn = null; _ow = null; _warp = null; _trig = null;
            _selSpawn = _selOw = _selWarp = _selTrig = -1;
            RefreshLists();
            int Keep(int i, int count) => i < count ? i : count - 1;
            List<Action> order = new List<Action>
            {
                () => SelectedSpawnableIndex = Keep(spawn, _file.Spawnables.Count),
                () => SelectedOverworldIndex = Keep(ow, _file.Overworlds.Count),
                () => SelectedWarpIndex = Keep(warp, _file.Warps.Count),
                () => SelectedTriggerIndex = Keep(trig, _file.Triggers.Count),
            };
            int last = active is Overworld ? 1 : active is Warp ? 2 : active is Trigger ? 3 : 0;
            for (int i = 0; i < order.Count; i++) if (i != last) order[i]();
            // The kind being edited is selected last, so the shared boxes end up on it.
            order[last]();
            foreach (string n in new[] { nameof(HasSelectedEvent), nameof(HasSpawn), nameof(HasOw), nameof(HasWarp), nameof(HasTrig) })
                OnPropertyChanged(n);
            Dirty();
            RefreshMarkers();
        }
        private void SetClean() { _undo?.MarkSaved(); if (!_dirty) return; _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        private int _selectedIndex = -1;
        public int SelectedEventIndex
        {
            get => _selectedIndex;
            set
            {
                if (RecordSwitchGuard.IsSnappingBack) return;
                if (_dirty && !_suppress && value >= 0 && _selectedIndex >= 0 && value != _selectedIndex)
                {
                    int requested = value;
                    RecordSwitchGuard.SnapBack(() => _selectedIndex, v => _selectedIndex = v, () => OnPropertyChanged(nameof(SelectedEventIndex)));
                    _ = ConfirmEventFileSwitchAsync(requested);
                    return;
                }
                if (Set(ref _selectedIndex, value) && !_suppress && value >= 0) LoadFile(value);
            }
        }

        private async Task ConfirmEventFileSwitchAsync(int requested)
        {
            if (!await RecordSwitchGuard.ConfirmLeaveAsync(this, _owner, "event file")) return;
            SetClean();
            // Discarding can drop the file being switched to when it was added and not saved.
            if (requested >= EventNames.Count) requested = EventNames.Count - 1;
            if (Set(ref _selectedIndex, requested, nameof(SelectedEventIndex))) LoadFile(requested);
        }

        public int InitialIndex { get; set; }

        public int InitialOverworldIndex { get; set; } = -1;
        public int InitialSpawnableIndex { get; set; } = -1;
    }
}
