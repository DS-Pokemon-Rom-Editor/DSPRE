using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using DSPRE.Avalonia.Gl;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.ViewModels.Battle
{
    public sealed partial class AnimatedPreviewViewModel
    {
        /// <summary>Needs a start tile, or overworlds to start among.</summary>
        public bool CanStepInto => Player != null || StartTile != null;

        private (int x, int z)? _startTile;

        /// <summary>Where to stand when stepping in, in whole-matrix tiles.</summary>
        public (int x, int z)? StartTile
        {
            get => _startTile;
            set
            {
                if (Nullable.Equals(_startTile, value)) return;
                _startTile = value;
                OnPropertyChanged(nameof(StartTile));
                OnPropertyChanged(nameof(StartTileText));
                Player = MakePlayer();
                OnPropertyChanged(nameof(Player));
                OnPropertyChanged(nameof(CanStepInto));
                Rebuild();
            }
        }

        public string StartTileText => _startTile == null
            ? "Starts in the middle of the map"
            : $"Starts at tile {_startTile.Value.x}, {_startTile.Value.z}";

        public void ClearStartTile() { _startFacing = MoveFacing.Down; StartTile = null; }

        private MoveFacing _startFacing = MoveFacing.Down;

        public ObservableCollection<string> StartBesideNames { get; } = new ObservableCollection<string>();

        private int _startBesideIndex = -1;

        private bool _placingStart;

        public bool PlacingStart
        {
            get => _placingStart;
            set { if (Set(ref _placingStart, value)) OnPropertyChanged(nameof(PlaceHint)); }
        }

        public string PlaceHint => _placingStart
            ? "Drag on the map to set where the walk starts."
            : "";

        private readonly List<(int x, int z)?> _startPlaces = new List<(int x, int z)?>();

        public int StartBesideIndex
        {
            get => _startBesideIndex;
            set
            {
                if (!Set(ref _startBesideIndex, value)) return;
                if (value <= 0 || value >= _startPlaces.Count) { ClearStartTile(); return; }

                (int x, int z)? place = _startPlaces[value];
                if (place == null) { ClearStartTile(); return; }
                StandBeside(place.Value.x, place.Value.z);
            }
        }

        private bool CanStand(int x, int z) =>
            (_collision == null || _collision.IsEmpty
             || (!_collision.IsBlocked(x, z) && !FieldTileBehaviors.IsWater(_collision.TypeAt(x, z), _family)))
            && !SomebodyOn(x, z, null, false);

        public void StandBeside(int ox, int oz)
        {
            List<(int dx, int dz, MoveFacing look)> tries = new List<(int dx, int dz, MoveFacing look)>
            {
                (0, 1, MoveFacing.Up),        // below it, looking up
                (0, -1, MoveFacing.Down),
                (1, 0, MoveFacing.Left),
                (-1, 0, MoveFacing.Right),
            };

            // Somebody there is usually spoken to from the side they face, so that side goes first.
            Npc there = _npcs.FirstOrDefault(n => FieldInteraction.TileX(n.Event) == ox && FieldInteraction.TileZ(n.Event) == oz);
            if (there != null)
            {
                (int fx, int fz) = FieldPlayer.Step(there.Motion.Facing);
                int front = tries.FindIndex(t => t.dx == fx && t.dz == fz);
                if (front > 0) { (int dx, int dz, MoveFacing look) pick = tries[front]; tries.RemoveAt(front); tries.Insert(0, pick); }
            }

            foreach ((int dx, int dz, MoveFacing look) in tries)
            {
                int x = ox + dx, z = oz + dz;
                if (!CanStand(x, z)) continue;
                _startFacing = look;
                StartTile = (x, z);
                return;
            }

            // Nowhere free beside it, so stand below it anyway and the map still opens somewhere useful.
            _startFacing = MoveFacing.Down;
            StartTile = (ox, oz + 1);
        }

        public void StandOn(int x, int z, MoveFacing facing = MoveFacing.Down)
        {
            (int x, int z)? free = NearestFreeTile(x, z);
            if (free == null) return;                  // nowhere near it will do; leave the marker be

            _startFacing = facing;
            StartTile = free;
            // The pick no longer matches, so back to "Wherever the marker is".
            if (_startBesideIndex != 0) { _startBesideIndex = 0; OnPropertyChanged(nameof(StartBesideIndex)); }
            if (_stepInto) { Player = MakePlayer(); Rebuild(); }
        }

        private (int x, int z)? NearestFreeTile(int x, int z, int reach = 3)
        {
            bool Free(int tx, int tz) => CanStand(tx, tz);

            if (Free(x, z)) return (x, z);

            for (int r = 1; r <= reach; r++)
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != r) continue;
                        if (Free(x + dx, z + dz)) return (x + dx, z + dz);
                    }
            return null;
        }

        private void BuildStartBesideList()
        {
            StartBesideNames.Clear();
            _startPlaces.Clear();

            StartBesideNames.Add("Wherever the marker is");
            _startPlaces.Add(null);

            foreach (Npc npc in _npcs)
            {
                StartBesideNames.Add($"Beside overworld {npc.Event.OwID}");
                _startPlaces.Add((FieldInteraction.TileX(npc.Event), FieldInteraction.TileZ(npc.Event)));
            }

            if (_events != null)
            {
                for (int i = 0; i < _events.Triggers.Count; i++)
                {
                    Trigger t = _events.Triggers[i];
                    StartBesideNames.Add(t.ScriptNumber == EventFile.NoScript
                        ? $"Beside trigger {i} with no script"
                        : $"Beside trigger {i} running script {t.ScriptNumber}");
                    _startPlaces.Add((FieldInteraction.TileX(t), FieldInteraction.TileZ(t)));
                }
                for (int i = 0; i < _events.Warps.Count; i++)
                {
                    Warp w = _events.Warps[i];
                    StartBesideNames.Add($"Beside warp {i}");
                    _startPlaces.Add((FieldInteraction.TileX(w), FieldInteraction.TileZ(w)));
                }
                for (int i = 0; i < _events.Spawnables.Count; i++)
                {
                    Spawnable sp = _events.Spawnables[i];
                    StartBesideNames.Add($"Beside spawnable {i}");
                    _startPlaces.Add((FieldInteraction.TileX(sp), FieldInteraction.TileZ(sp)));
                }
            }

            _startBesideIndex = 0;
            OnPropertyChanged(nameof(StartBesideIndex));
        }

        public (int x, int z)? TileAtScreen(double px, double py,
                                            Func<float, float, float, (float sx, float sy)?> project,
                                            double withinPixels = 80)
        {
            if (_collision == null || _collision.IsEmpty || _tileToWorld == null || project == null) return null;

            (int x, int z)? best = null;
            double bestD = withinPixels * withinPixels;
            foreach ((int x, int z) in _collision.Tiles)
            {
                (float x, float y, float z) foot = _tileToWorld(x, z);
                (float sx, float sy)? at = project(foot.x, foot.y, foot.z);
                if (at == null) continue;
                double dx = px - at.Value.sx, dy = py - at.Value.sy;
                double d = dx * dx + dy * dy;
                if (d < bestD) { bestD = d; best = (x, z); }
            }
            return best;
        }
    }
}
