using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Platform.Storage;
using DSPRE.Avalonia;
using DSPRE.Avalonia.Gl;
using DSPRE.Editors;
using DSPRE.Resources;
using DSPRE.Models;
using DSPRE.ROMFiles;
using LibNDSFormats.NSBMD;
using LibNDSFormats.NSBTX;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.World
{
    public partial class MapEditorViewModel
    {
        // A step holds every loaded map's parts, the area and the event moves waiting for Save.
        private sealed class MapPart
        {
            public MapFile Map;
            public byte[] Permissions, Buildings, Model, Terrain, Sound;
            public NSBMD Parsed;
            public bool CellDirty;
        }

        private sealed class MapStep
        {
            public MapPart[] Maps;
            public byte[] Area;
            public bool AreaDirty;
            public (int File, EventMove[] Moves)[] Moves;

            public bool SameAs(MapStep o) =>
                Maps.Length == o.Maps.Length
                && Maps.Zip(o.Maps).All(p => ReferenceEquals(p.First.Map, p.Second.Map)
                    && p.First.Permissions.AsSpan().SequenceEqual(p.Second.Permissions)
                    && p.First.Buildings.AsSpan().SequenceEqual(p.Second.Buildings)
                    && ReferenceEquals(p.First.Model, p.Second.Model) && ReferenceEquals(p.First.Terrain, p.Second.Terrain)
                    && ReferenceEquals(p.First.Sound, p.Second.Sound))
                && (Area ?? Array.Empty<byte>()).AsSpan().SequenceEqual(o.Area ?? Array.Empty<byte>())
                && Moves.Sum(m => m.Moves.Length) == o.Moves.Sum(m => m.Moves.Length);
        }

        private readonly UndoHistory<MapStep> _history = new UndoHistory<MapStep>(100);
        private MapStep _lastStep;
        private DateTime _lastStepAt = DateTime.MinValue;
        private bool _applyingStep;

        public bool CanUndo => _history.CanUndo;
        public bool CanRedo => _history.CanRedo;
        public void Undo() { if (!CanUndo) return; ApplyStep(_history.Undo()); }
        public void Redo() { if (!CanRedo) return; ApplyStep(_history.Redo()); }
        private void RaiseUndo() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private MapStep TakeStep()
        {
            List<(MapFile Map, bool Dirty)> parts = IsHeaderView
                ? _headerCells.Select(c => (c.Map, c.Dirty)).ToList()
                : new List<(MapFile Map, bool Dirty)> { (_map, false) };
            return new MapStep
            {
                Maps = parts.Where(p => p.Map != null).Select(p => new MapPart
                {
                    Map = p.Map, Permissions = p.Map.CollisionsToByteArray(), Buildings = p.Map.BuildingsToByteArray(),
                    Model = p.Map.MapModelData, Parsed = p.Map.MapModel, Terrain = p.Map.Bdhc, Sound = p.Map.Bgs, CellDirty = p.Dirty,
                }).ToArray(),
                Area = _area?.ToByteArray(),
                AreaDirty = _areaDirty,
                Moves = _eventMoves.Select(kv => (kv.Key, kv.Value.ToArray())).ToArray(),
            };
        }

        private void ResetUndo()
        {
            _lastStep = TakeStep();
            _history.Reset(_lastStep);
            _lastStepAt = DateTime.MinValue;
            RaiseUndo();
        }

        private void RecordUndo()
        {
            if (_applyingStep || _lastStep == null) return;
            MapStep now = TakeStep();
            if (now.SameAs(_lastStep)) return;
            // A different set of loaded maps is a new history, not a step.
            if (now.Maps.Length != _lastStep.Maps.Length || now.Maps.Zip(_lastStep.Maps).Any(p => !ReferenceEquals(p.First.Map, p.Second.Map)))
            {
                ResetUndo();
                return;
            }
            bool burst = (DateTime.UtcNow - _lastStepAt).TotalMilliseconds < 500;
            _history.Capture(now, burst);
            _lastStep = now;
            _lastStepAt = DateTime.UtcNow;
            RaiseUndo();
        }

        private void ApplyStep(MapStep step)
        {
            _applyingStep = true;
            try
            {
                foreach (MapPart part in step.Maps)
                {
                    MapFile map = part.Map;
                    map.ImportPermissions(part.Permissions);
                    map.ImportBuildings(part.Buildings);
                    // The model and terrain windows edit these too; only what this history recorded goes back.
                    MapPart known = _lastStep?.Maps.FirstOrDefault(p => ReferenceEquals(p.Map, map));
                    if (known == null || ReferenceEquals(map.MapModelData, known.Model)) { map.MapModelData = part.Model; map.MapModel = part.Parsed; }
                    if (known == null || ReferenceEquals(map.Bdhc, known.Terrain)) map.Bdhc = part.Terrain;
                    if (known == null || ReferenceEquals(map.Bgs, known.Sound)) map.Bgs = part.Sound;
                    HeaderMapCell cell = _headerCells.FirstOrDefault(c => ReferenceEquals(c.Map, map));
                    // A map this step changes must be written again, even when undo goes back past a save.
                    bool changed = known == null
                        || !known.Permissions.AsSpan().SequenceEqual(part.Permissions) || !known.Buildings.AsSpan().SequenceEqual(part.Buildings)
                        || !ReferenceEquals(known.Model, part.Model) || !ReferenceEquals(known.Terrain, part.Terrain) || !ReferenceEquals(known.Sound, part.Sound);
                    if (cell != null) cell.Dirty = part.CellDirty || changed;
                }
                if (_area != null && step.Area != null)
                {
                    using MemoryStream ms = new MemoryStream(step.Area);
                    _area = new AreaData(ms);
                    SyncTilesetsToArea();
                }
                _areaDirty = step.AreaDirty
                    || (_lastStep?.Area != null && step.Area != null && !_lastStep.Area.AsSpan().SequenceEqual(step.Area));

                foreach ((int file, List<EventMove> moves) in _eventMoves)
                    if (_eventsToSave.TryGetValue(file, out EventFile events))
                        for (int i = moves.Count - 1; i >= 0; i--)
                            if (EventAt(events, moves[i].Kind, moves[i].Index) is Event e) { e.XMapPosition = moves[i].FromX; e.YMapPosition = moves[i].FromY; }
                _eventMoves.Clear();
                foreach ((int file, EventMove[] moves) in step.Moves)
                {
                    if (!_eventsToSave.TryGetValue(file, out EventFile events)) continue;
                    _eventMoves[file] = moves.ToList();
                    foreach (EventMove m in moves)
                        if (EventAt(events, m.Kind, m.Index) is Event e) { e.XMapPosition = m.ToX; e.YMapPosition = m.ToY; }
                }

                _lastStep = step;
                _lastStepAt = DateTime.MinValue;
                if (_dirty != _history.IsDirty) { _dirty = _history.IsDirty; OnPropertyChanged(nameof(HasUnsavedChanges)); }
                int keepBuilding = _selectedBuildingIndex;
                RefreshBuildings();
                // Rebuilding the list drops its selection; stay on the building being edited.
                if (keepBuilding >= 0 && keepBuilding < Buildings.Count) { SelectedBuildingIndex = -1; SelectedBuildingIndex = keepBuilding; }
                OnPropertyChanged(nameof(Collisions));
                OnPropertyChanged(nameof(Types));
                RaiseArea();
                RebuildPreview();
            }
            finally { _applyingStep = false; }
            RaiseUndo();
        }
    }
}
