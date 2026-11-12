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
        private void Reload()
        {
            if (_scene == null) return;
            int keepFrame = _frame;
            Load(_scene, _terrain, _events, _indoor, _collision, _seed, _tileToWorld, _walkerFor,
                 _walkerStartId, _scriptHome);
            PlaceNpcs(_footFinder);
            Frame = keepFrame;
            Rebuild();
        }

        private bool _animateTerrain = true;
        public bool AnimateTerrain { get => _animateTerrain; set { if (Set(ref _animateTerrain, value)) Rebuild(); } }

        private string _statusText = "Nothing to preview";
        public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }

        public void Load(NsbmdRenderModel scene, TextureSrtAnimation terrain, EventFile events, int seed = 0)
            => Load(scene, terrain, events, false, null, seed);

        public void Load(NsbmdRenderModel scene, TextureSrtAnimation terrain, EventFile events,
                         bool indoor, MapCollisionGrid collision, int seed,
                         Func<float, float, (float x, float y, float z)> tileToWorld = null,
                         Func<int, ScriptWalker> walkerFor = null,
                         Func<int, int> walkerStartId = null,
                         Func<int, string> scriptHome = null,
                         Func<int, IReadOnlyList<ScriptAction>> actionsFor = null)
        {
            _scene = scene; _terrain = terrain; _events = events; _seed = seed;
            _frame = 0;
            _npcs.Clear();
            _animatedMaterials.Clear();
            _buildings = null;
            _movingBuildings = 0;
            _jointBuildings = 0;
            _colourBuildings = 0;
            _doorBuildings = 0;
            _timeBuildings = 0;
            _playingOnce.Clear();
            _cameraTrail.Clear();
            _talkTarget = null;
            _cameraObject = null;
            _indoor = indoor;
            _collision = collision;
            _tileToWorld = tileToWorld;
            _walkerFor = walkerFor;
            _walkerStartId = walkerStartId;
            _scriptHome = scriptHome;
            _actionsFor = actionsFor;

            if (scene == null) { StatusText = "Nothing to preview"; return; }

            // People are placed in the scene's normalised coordinates, so a tile is measured in those too.
            _tileX = scene.CellStrideX / MapFile.MapSize * scene.Scale;
            _tileZ = scene.CellStrideZ / MapFile.MapSize * scene.Scale;

            // The animation drives materials by name (river, sea_on), as the game does.
            if (_terrain != null)
                foreach (KeyValuePair<int, string> kv in scene.MaterialNameByKey)
                {
                    int m = _terrain.IndexOf(kv.Value);
                    if (m >= 0 && !_terrain.IsStatic(m)) _animatedMaterials[kv.Key] = (_terrain, m);
                }

            // Indoor areas take building animations from a separate list.
            _buildings = new SceneBuildingAnimator(scene, indoor, _timeOfDay, _animatedMaterials.Keys);
            _movingBuildings = _buildings.MovingBuildings;
            _jointBuildings = _buildings.JointBuildings;
            _colourBuildings = _buildings.ColourBuildings;
            _doorBuildings = _buildings.DoorBuildings;
            _timeBuildings = _buildings.TimeBuildings;

            if (events?.Overworlds != null)
            {
                int n = 0;
                foreach (Overworld ow in events.Overworlds)
                {
                    OverworldMovement move = OverworldMovements.Find(RomInfo.GameFamily, ow.Movement);
                    MoveFacing facing = (MoveFacing)Math.Min(Math.Max((int)ow.Orientation, 0), 3);
                    // 0: param1 on looking trainers counts steps walked, not idle frames.
                    const int interval = 0;
                    // The engine refuses a step into a closed-off tile, so the preview checks the same grid.
                    int homeX = ow.XMatrixPosition * MapFile.MapSize + ow.XMapPosition;
                    int homeZ = ow.YMatrixPosition * MapFile.MapSize + ow.YMapPosition;
                    Npc npc = new Npc { Event = ow };
                    Func<int, int, bool> blocked = (dx, dz) =>
                        (collision != null && !collision.IsEmpty && collision.IsBlocked(homeX + dx, homeZ + dz))
                        || SomebodyOn(homeX + dx, homeZ + dz, npc, true);

                    npc.Motion = new OverworldAnimator(move, facing, ow.XRange, ow.YRange, interval,
                                                       seed + n++, blocked);
                    _npcs.Add(npc);
                }
            }

            BuildFlagList(events);
            OnPropertyChanged(nameof(HasEventFlags));
            OnPropertyChanged(nameof(HiddenSummary));
            _startTile = null;
            _startFacing = MoveFacing.Down;
            Player = MakePlayer();
            BuildStartBesideList();
            OnPropertyChanged(nameof(CanStepInto));

            StatusText = Describe();
            Rebuild();
        }

        private string Describe()
        {
            int people = _npcs.Count(n => IsOnMap(n));
            string water;
            int surfaces = _animatedMaterials.Count + (_buildings?.ScrolledSurfaces ?? 0);
            if (surfaces > 0)
                water = $"{surfaces} moving surface{(surfaces == 1 ? "" : "s")}";
            else if (_terrain == null) water = "no terrain animation here";
            else water = "terrain animation touches nothing on this map";

            string buildings = _movingBuildings > 0
                ? $", {_movingBuildings} moving building{(_movingBuildings == 1 ? "" : "s")}" : "";
            string joints = _jointBuildings > 0
                ? $", {_jointBuildings} with moving parts" : "";
            string colours = _colourBuildings > 0
                ? $", {_colourBuildings} fading in and out" : "";
            string doors = _doorBuildings > 0
                ? $", {_doorBuildings} door{(_doorBuildings == 1 ? "" : "s")} that only open when used" : "";
            string times = _timeBuildings > 0
                ? $", {_timeBuildings} showing their {FieldTimeOfDay.Name(_timeOfDay).ToLowerInvariant()} animation" : "";
            int away = HiddenCount;
            string hidden = away > 0 ? $", {away} hidden by flags" : "";
            return $"{water}{buildings}{joints}{colours}{doors}{times}, "
                 + $"{people} {(people == 1 ? "person" : "people")}{hidden}";
        }

        public void PlaceNpcs(Func<Overworld, (float x, float y, float z)> footFinder)
        {
            if (footFinder == null) return;
            _footFinder = footFinder;
            foreach (Npc npc in _npcs)
            {
                (float x, float y, float z) = footFinder(npc.Event);
                npc.FootX = x; npc.FootY = y; npc.FootZ = z;
            }
            Rebuild();
        }
    }
}
