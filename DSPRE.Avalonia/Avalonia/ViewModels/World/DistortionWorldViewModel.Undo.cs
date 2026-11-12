using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.Gl;
using LibNDSFormats.NSBMD;
using DSPRE.Editors;
using DSPRE.Resources;
using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static DSPRE.RomInfo;
using System.IO;

namespace DSPRE.Avalonia.ViewModels.World
{
    public partial class DistortionWorldViewModel
    {
        // Floors and collision grids only. Map models keep their undo in the model editor.
        private sealed class WorldState
        {
            public byte[][] Floors;
            public Dictionary<int, ushort[]> Grids;
            public int[] EditedMembers, EditedGrids;
        }

        private readonly UndoHistory<WorldState> _history = new UndoHistory<WorldState>();
        private WorldState _lastState;

        private WorldState TakeState()
        {
            CommitShownFloor();
            return new WorldState
            {
                Floors = Floors.Select(f => f.Data.ToByteArray()).ToArray(),
                Grids = _gridCache.ToDictionary(kv => kv.Key, kv => (ushort[])kv.Value.Clone()),
                EditedMembers = _editedMembers.ToArray(),
                EditedGrids = _editedGrids.ToArray(),
            };
        }

        private static bool SameState(WorldState a, WorldState b) =>
            a.Floors.Length == b.Floors.Length && a.Floors.Zip(b.Floors).All(z => z.First.AsSpan().SequenceEqual(z.Second))
            && a.Grids.Count == b.Grids.Count && a.Grids.All(kv => b.Grids.TryGetValue(kv.Key, out ushort[] g) && kv.Value.AsSpan().SequenceEqual(g));

        private void ResetSteps()
        {
            _lastState = Floors.Count > 0 ? TakeState() : null;
            _history.Reset(_lastState);
            RaiseSteps();
        }

        private void RaiseSteps() { Raise(nameof(CanUndo)); Raise(nameof(CanRedo)); }

        private void Recorded()
        {
            if (_lastState == null) return;
            WorldState now = TakeState();
            if (SameState(now, _lastState)) return;
            _history.Capture(now);
            _lastState = now;
            RaiseSteps();
        }

        public bool CanUndo => _history.CanUndo;
        public bool CanRedo => _history.CanRedo;

        public void Undo() { if (_history.CanUndo) Restore(_history.Undo(), _lastState); }
        public void Redo() { if (_history.CanRedo) Restore(_history.Redo(), _lastState); }

        private void Restore(WorldState state, WorldState from)
        {
            CommitShownFloor();
            int changed = -1;
            for (int i = 0; i < Math.Min(Floors.Count, state.Floors.Length); i++)
            {
                if (Floors[i].Data.ToByteArray().AsSpan().SequenceEqual(state.Floors[i])) continue;
                Floors[i].Data = new TornWorldFile(state.Floors[i]);
                if (changed < 0) changed = i;
            }
            foreach (KeyValuePair<int, ushort[]> kv in state.Grids) _gridCache[kv.Key] = (ushort[])kv.Value.Clone();
            foreach (int id in from.Grids.Keys.Where(id => !state.Grids.ContainsKey(id)).ToList()) _gridCache.Remove(id);
            _editedMembers.Clear(); _editedMembers.UnionWith(state.EditedMembers);
            _editedGrids.Clear(); _editedGrids.UnionWith(state.EditedGrids);
            _lastState = state;

            // The working lists hold the replaced floor's objects, so they must not be written back.
            _shownFloorIndex = -1;
            if (changed >= 0 && changed != _selectedFloorIndex) SelectedFloorIndex = changed;
            else ShowFloor();
            Raise(nameof(HasUnsavedChanges));
            SayWhatIsUnsaved();
            RaiseSteps();
        }

        public string UnsavedChangesDescription
        {
            get
            {
                if (!HasUnsavedChanges) return null;
                int data = _editedMembers.Count + _editedGrids.Count;
                if (data > 0 && _shapeChanged.Count > 0)
                    return $"{data} data files and {_shapeChanged.Count} map models";
                return data > 0 ? $"{data} data files" : $"{_shapeChanged.Count} map models";
            }
        }
    }
}
