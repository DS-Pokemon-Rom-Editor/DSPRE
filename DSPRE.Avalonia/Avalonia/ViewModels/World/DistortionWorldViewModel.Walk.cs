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
        private TornWorldRuntime _runtime = new TornWorldRuntime();
        private uint _activeHeader;
        private TornWorldRuntime.ElevatorRide _ride;
        private int _rideAnchor;
        private (int x, int y, int z) _rideAnchorHome;
        private (int x, int z) _rideTile;
        private (float x, float y, float z) _rideFootRaw;
        private MoveFacing _rideFacing;
        private int _lastTickFrame = -1;

        private bool RideActive => _ride != null && !_ride.Done;

        /// <summary>A ride or jump runs as a field task and holds the controls until it ends.</summary>
        public bool PlayerHeld => RideActive || CrossingActive;

        private bool CrossingActive
        {
            get
            {
                if (_crossing == null || CurrentFrame == null) return false;
                float through = _crossing.Through(CurrentFrame());
                return through >= 0f && through < 1f;
            }
        }

        /// <summary>Asks the view to show another floor mid-ride, when only one floor is shown.</summary>
        public event Action RideChangedFloor;

        /// <summary>Starts a walk like a warp in: default flags, this floor's default groups.</summary>
        public void StartWalk()
        {
            _ride = null;
            _rideAnchor = 0;
            _crossing = null;
            _playerRoll = 0f;
            _lastCamera = null;
            _lastTickFrame = -1;

            FloorRow floor = CurrentFloor;
            _activeHeader = floor != null ? (uint)floor.Floor.HeaderId : 0;
            _runtime = new TornWorldRuntime(_activeHeader);
            _runtime.EnterFloor(floor?.Data);
        }

        private void EnterFloor(uint header)
        {
            _activeHeader = header;
            _runtime.EnterFloor(Floors.FirstOrDefault(f => (uint)f.Floor.HeaderId == header)?.Data);
        }

        private bool GhostVisible(uint header, long group)
        {
            if (header == _activeHeader) return _runtime.IsGroupVisible(group);
            TornWorldFile file = Floors.FirstOrDefault(f => (uint)f.Floor.HeaderId == header)?.Data;
            return file != null && group >= 0 && group < TornWorldRuntime.GhostGroupCount
                && (file.DefaultVisibleGroups & (1L << (int)group)) != 0;
        }

        /// <summary>How see-through a placed prop is in the walk preview. Null draws it as is.</summary>
        public float? OpacityOf(int modelId)
        {
            if (!_placed.TryGetValue(modelId, out PlacedProp placed)) return null;

            if (placed.Carried) return RideActive && modelId == _rideAnchor ? (float?)null : 0f;
            if (placed.GhostGroup >= 0) return GhostVisible(placed.Header, placed.GhostGroup) ? (float?)null : 0f;
            if (placed.Platform == null) return null;

            if (RideActive)
            {
                if (modelId == _rideAnchor) return null;
                if (_ride.PlatformHeader == placed.Header && _ride.Platform == placed.Platform) return 0f;   // the ridden platform becomes the template waiting where the ride ends
            }
            return _runtime.IsPresent(placed.Platform) ? (float?)null : 0f;
        }

        // Floors and props are half a tile per Y step (HeightOf), so the ride uses that too.
        private (float x, float y, float z) RideShift(int modelId)
        {
            if (!RideActive || modelId != _rideAnchor) return (0f, 0f, 0f);
            return ((_ride.X - _rideAnchorHome.x) * 16f,
                    (_ride.Y - _rideAnchorHome.y) * 8f,
                    (_ride.Z - _rideAnchorHome.z) * 16f);
        }

        /// <summary>Runs the game's per-frame tasks up to a preview frame.</summary>
        public void Tick(int frame)
        {
            if (_lastTickFrame < 0 || frame < _lastTickFrame) { _lastTickFrame = frame; return; }
            int elapsed = frame - _lastTickFrame;
            _lastTickFrame = frame;

            for (int i = 0; i < elapsed && RideActive; i++)
            {
                TornWorldRuntime.RideEvent happened = _ride.Tick();
                if (happened == TornWorldRuntime.RideEvent.ChangedFloor)
                {
                    EnterFloor(_ride.Header);
                    if (!_wholeWorld) { RideChangedFloor?.Invoke(); return; }
                }
                else if (happened == TornWorldRuntime.RideEvent.Arrived)
                {
                    FinishRide();
                    return;
                }
            }
        }

        /// <summary>Shows the floor the ride is now on, when one floor is shown at a time.</summary>
        public bool ShowRideFloor()
        {
            if (!RideActive) return false;
            int index = -1;
            for (int i = 0; i < Floors.Count; i++)
                if ((uint)Floors[i].Floor.HeaderId == _ride.Header) { index = i; break; }
            if (index < 0) return false;

            SelectedFloorIndex = index;
            return true;
        }

        /// <summary>Where the rider stands on the floor now shown, in walk tiles.</summary>
        public (int x, int z)? RideWalkTile()
            => RideActive ? WalkTileFor(_ride.Header, _ride.PathStartX, _ride.PathStartY, _ride.PathStartZ) : null;

        public MoveFacing RideFacing => _rideFacing;

        /// <summary>After a floor reload: where the rider now is and what frame it is.</summary>
        public void RideContinuesFrom(int tileX, int tileZ, int frame)
        {
            _rideTile = (tileX, tileZ);
            _lastTickFrame = frame;
        }

        private void FinishRide()
        {
            TornWorldRuntime.ElevatorRide ride = _ride;
            _ride = null;
            _rideAnchor = 0;
            if (ride == null) return;

            (int x, int z)? tile = WalkTileFor(ride.Header, ride.EndX, ride.EndY, ride.EndZ);
            if (tile != null) PutPlayerOn?.Invoke(tile.Value.x, tile.Value.z, _rideFacing);
        }

        private (int x, int z)? WalkTileFor(uint header, int worldX, int worldY, int worldZ)
        {
            int floorIndex = -1;
            foreach ((int at, FloorRow floor) in ShownFloors())
                if ((uint)floor.Floor.HeaderId == header) { floorIndex = at; break; }
            if (floorIndex < 0) return null;

            foreach (TornWorldSurfaces.Surface surface in _surfaces.Values)
            {
                if (surface.FloorIndex != floorIndex || surface.IsGround) continue;
                if (surface.TryTileFor(worldX, worldY, worldZ, out int col, out int row)) return surface.WalkTile(col, row);
            }

            foreach (TornWorldSurfaces.Surface surface in _surfaces.Values)   // ground height comes from the land data, so only X and Z pick the tile
            {
                if (surface.FloorIndex != floorIndex || !surface.IsGround) continue;
                int col = worldX - surface.GroundX, row = worldZ - surface.GroundZ;
                if (col >= 0 && row >= 0 && col < MapFile.MapSize && row < MapFile.MapSize) return surface.WalkTile(col, row);
            }
            return null;
        }

        public void SomebodyStoodOn(int tileX, int tileZ, int frame, MoveFacing facing)
        {
            LastGravityChange = null;
            if (RideActive) return;
            if (!TrySurface(tileX, tileZ, out TornWorldSurfaces.Surface surface, out int col, out int row)) return;

            FloorRow shown = ShownFloorAt(surface.FloorIndex);
            if (shown == null) return;

            uint header = (uint)shown.Floor.HeaderId;
            if (header != _activeHeader) EnterFloor(header);

            (int worldX, int worldY, int worldZ) = surface.EventAt(col, row);
            int looking = Direction(facing);

            _runtime.StepOn(shown.Data, worldX, worldY, worldZ, looking);   // HandleGhostPropTriggerAt only runs after a real step

            if (TryStartRide(tileX, tileZ, frame, facing)) return;

            TakeGravityPoint(tileX, tileZ, surface, shown, worldX, worldY, worldZ, looking);
        }

        private bool TryStartRide(int tileX, int tileZ, int frame, MoveFacing facing)
        {
            if (!_elevatorTiles.TryGetValue((tileX, tileZ), out PlacedProp placed)) return false;
            if (!_runtime.IsPresent(placed.Platform)) return false;

            TornWorldCodeTables.Tables tables = TornWorldCodeTables.Read(out _);
            if (tables == null) return false;

            (float x, float y, float z)? foot = RawFoot(tileX, tileZ);
            if (foot == null) return false;

            _ride = new TornWorldRuntime.ElevatorRide(_runtime, tables, placed.Header, placed.Platform);
            _rideAnchor = placed.ModelId;
            _rideAnchorHome = (placed.HomeX, placed.HomeY, placed.HomeZ);
            _rideTile = (tileX, tileZ);
            _rideFootRaw = foot.Value;
            _rideFacing = facing;
            _lastTickFrame = frame;
            return true;
        }

        public float RollAt(int tileX, int tileZ)
        {
            if (!TrySurface(tileX, tileZ, out TornWorldSurfaces.Surface surface, out _, out _)) return 0f;

            if (_crossing != null && CurrentFrame != null
                && tileX == _crossing.ToTileX && tileZ == _crossing.ToTileZ)
            {
                int step = CurrentFrame() - _crossing.StartFrame;
                if (step >= 0 && step < _crossing.Frames)
                    return Drawn(_crossing.FromRoll + TornWorldRuntime.TurnAfter(_crossing.Point, step));
            }

            return Drawn(_playerRoll != 0f ? _playerRoll : SurfaceRoll(surface));
        }

        private static float SurfaceRoll(TornWorldSurfaces.Surface surface)
        {
            if (surface.IsGround || surface.Kind == TornWorldFile.PlatformKind.Floor) return 0f;
            switch (surface.Kind)
            {
                case TornWorldFile.PlatformKind.WestWall: return 90f;
                case TornWorldFile.PlatformKind.EastWall: return 270f;
                default: return 180f;
            }
        }

        /// <summary>The hop of a jump between surfaces, in tiles, for the player's sprite only.</summary>
        public (float x, float y, float z) PlayerSpriteShift()
        {
            if (_crossing == null || CurrentFrame == null) return (0f, 0f, 0f);
            int step = CurrentFrame() - _crossing.StartFrame;
            (float x, float y, float z) = TornWorldRuntime.HopOffset(_crossing.Point, step);
            return (x / 16f, y / 16f, z / 16f);
        }

        private static float Drawn(float turn) => -turn;

        private float _playerRoll;

        private (float pitch, float yaw, float roll, int steps)? _lastCamera;

        private static readonly TornWorldFile.CameraRegion BaseCamera = new TornWorldFile.CameraRegion();

        private sealed class Crossing
        {
            public int FromTileX, FromTileZ, ToTileX, ToTileZ;
            public float FromRoll;
            public int StartFrame, Frames;
            public TornWorldFile.JumpPoint Point;

            public float Through(int frame) => (frame - StartFrame) / (float)Frames;
        }

        private Crossing _crossing;

        public Action<int, int, MoveFacing> PutPlayerOn;

        public string LastGravityChange { get; private set; }

        private void TakeGravityPoint(int tileX, int tileZ, TornWorldSurfaces.Surface surface, FloorRow shown,
            int worldX, int worldY, int worldZ, int looking)
        {
            if (PutPlayerOn == null) return;

            TornWorldFile.JumpPoint point = TornWorldRuntime.JumpPointAt(shown.Data, worldX, worldY, worldZ, looking);
            if (point == null) return;

            int toX = worldX + point.DisplacementX;
            int toY = worldY + point.DisplacementY;
            int toZ = worldZ + point.DisplacementZ;

            bool toGround = point.TargetKind == (int)TornWorldFile.PlatformKind.Invalid;

            foreach (TornWorldSurfaces.Surface landing in _surfaces.Values)
            {
                if (landing.FloorIndex != surface.FloorIndex) continue;
                if (landing == surface) continue;
                if (landing.IsGround != toGround) continue;

                int toCol, toRow;
                if (toGround)
                {
                    if (!GroundTile(landing, toX, toZ, out toCol, out toRow)) continue;   // ground height comes from the land data, so toY is not used
                }
                else
                {
                    if ((int)landing.Kind != point.TargetKind) continue;
                    if (point.TargetPlatformIndex != 0xFFFF && landing.PlatformIndex != point.TargetPlatformIndex) continue;
                    if (!landing.TryTileFor(toX, toY, toZ, out toCol, out toRow)) continue;
                }
                if (!landing.CanWalk(toCol, toRow)) continue;

                (int walkX, int walkZ) = landing.WalkTile(toCol, toRow);

                // A walk that starts on a wall has no roll yet, so it leaves by the wall's turn.
                float wasRoll = _playerRoll != 0f ? _playerRoll : SurfaceRoll(surface);
                _playerRoll = ((wasRoll + point.SpriteRotationAngle) % 360f + 360f) % 360f;

                _crossing = new Crossing
                {
                    FromTileX = tileX,
                    FromTileZ = tileZ,
                    ToTileX = walkX,
                    ToTileZ = walkZ,
                    FromRoll = wasRoll,
                    StartFrame = CurrentFrame?.Invoke() ?? 0,
                    Frames = Math.Max(1, (int)point.MovementSteps),
                    Point = point,
                };

                PutPlayerOn(walkX, walkZ, Facing(point.FinalFacingDirection));
                LastGravityChange = $"Gravity: {Name(landing.Kind)}";
                return;
            }
        }

        private static bool GroundTile(TornWorldSurfaces.Surface ground, int worldX, int worldZ, out int col, out int row)
        {
            col = worldX - ground.GroundX;
            row = worldZ - ground.GroundZ;
            return col >= 0 && row >= 0 && col < MapFile.MapSize && row < MapFile.MapSize;
        }

        private static string Name(TornWorldFile.PlatformKind kind)
        {
            switch (kind)
            {
                case TornWorldFile.PlatformKind.WestWall: return "west wall";
                case TornWorldFile.PlatformKind.EastWall: return "east wall";
                case TornWorldFile.PlatformKind.Ceiling: return "ceiling";
                default: return "floor";
            }
        }

        private static int Direction(MoveFacing facing)
        {
            switch (facing)
            {
                case MoveFacing.Up: return 0;
                case MoveFacing.Down: return 1;
                case MoveFacing.Left: return 2;
                default: return 3;
            }
        }

        private static MoveFacing Facing(int direction)
        {
            switch (direction)
            {
                case 0: return MoveFacing.Up;
                case 1: return MoveFacing.Down;
                case 2: return MoveFacing.Left;
                case 3: return MoveFacing.Right;
                default: return MoveFacing.Down;
            }
        }
    }
}
