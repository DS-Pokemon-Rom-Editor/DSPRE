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
        private void BuildScene(IReadOnlyList<FloorRow> floors)
        {
            Model3D = null;
            _cellAltitude.Clear();
            Area = null;
            Events = null;
            StandingCount = 0;
            _travelling = 0;
            _placed.Clear();
            _elevatorTiles.Clear();
            _rideAnchor = 0;
            BuildingAnimationSet.ForgetRegistered();

            try
            {
                List<(int gridX, int gridY, MapFile map, byte areaId, float altitude, float originX, float originZ)> cells = FloorCells(floors);
                _selectedMapCell = MapCells.Count > 0 ? 0 : -1;
                Raise(nameof(SelectedMapCell));
                OpenShape();

                if (cells.Count == 0) return;
                PlaceCells(floors, cells);
            }
            catch (Exception ex)
            {
                AppLogger.Error("DistortionWorld.BuildScene: " + ex.Message);
            }

            RebuildOverlay();
            SceneShown?.Invoke();

            if (!HasUnsavedChanges)
                StatusText = StandingCount > 0
                    ? $"{Floors.Count} floors, {StandingCount} platforms and props, {WalkableTiles} walkable tiles"
                    : $"{Floors.Count} floors.";
        }

        private List<(int gridX, int gridY, MapFile map, byte areaId, float altitude, float originX, float originZ)> FloorCells(IReadOnlyList<FloorRow> floors)
        {
            List<(int gridX, int gridY, MapFile map, byte areaId, float altitude, float originX, float originZ)> cells = new List<(int gridX, int gridY, MapFile map, byte areaId, float altitude,
                                  float originX, float originZ)>();
            MapCells.Clear();

            for (int index = 0; index < floors.Count; index++)
            {
                TornWorldMapTable.Floor floor = floors[index].Floor;
                MapHeader header;
                try { header = MapHeader.GetMapHeader((ushort)floor.HeaderId); } catch (Exception ex) { AppLogger.WarnOnce("DistortionWorldViewModel.BuildScene", ex); header = null; }
                if (header == null) continue;

                if (Area == null)
                {
                    CameraId = header.CameraAngleID;
                    MusicDayId = header.MusicDayID;
                    MusicNightId = header.MusicNightID;
                    try { Area = new AreaData(header.AreaDataID); } catch (Exception ex) { AppLogger.WarnOnce("DistortionWorldViewModel.BuildScene", ex); Area = null; }
                    try { Events = new EventFile(header.EventFileID); } catch (Exception ex) { AppLogger.WarnOnce("DistortionWorldViewModel.BuildScene", ex); Events = null; }
                }

                GameMatrix matrix = new GameMatrix(header.MatrixID);
                for (int y = 0; y < matrix.Height; y++)
                    for (int x = 0; x < matrix.Width; x++)
                    {
                        if (matrix.Maps[y, x] == GameMatrix.EMPTY) continue;
                        if (matrix.HasHeadersSection && matrix.Headers[y, x] != floor.HeaderId) continue;

                        float altitude = ((matrix.HasHeightsSection ? matrix.Altitudes[y, x] : 0) + floor.OffsetAltitude)
                                       * (NsbmdGeometry.TileSize / 2f);

                        MapCells.Add(new MapCellRow
                        {
                            MapId = matrix.Maps[y, x],
                            AreaId = header.AreaDataID,
                            Label = $"Map {matrix.Maps[y, x]}  ·  {floor.HeaderId} at {x},{y}",
                        });

                        cells.Add((index * GridBand + x, y,
                                   new MapFile(matrix.Maps[y, x], GameFamily, discardMoveperms: false),
                                   header.AreaDataID, altitude,
                                   (floor.OffsetX + x * MapFile.MapSize) * NsbmdGeometry.TileSize,
                                   (floor.OffsetZ + y * MapFile.MapSize) * NsbmdGeometry.TileSize));
                    }
            }
            return cells;
        }

        private void PlaceCells(IReadOnlyList<FloorRow> floors, List<(int gridX, int gridY, MapFile map, byte areaId, float altitude, float originX, float originZ)> cells)
        {
            foreach ((int gridX, int gridY, MapFile map, byte areaId, float altitude, float originX, float originZ) c in cells) _cellAltitude[(c.gridX, c.gridY)] = c.altitude;
            List<(int cellX, int cellY, PlacedBuilding placed)> standing = StandingModels(floors);
            BuildWalkable(floors, cells);

            int leastX = cells.Min(c => c.gridX), leastY = cells.Min(c => c.gridY);
            List<(int gridX, int gridY, MapFile map, byte areaId, float altitude, float, float)> placed = cells.Select(c => (c.gridX, c.gridY, c.map, c.areaId, c.altitude,
                    c.originX - (c.gridX - leastX) * NsbmdGeometry.MapStride,
                    c.originZ - (c.gridY - leastY) * NsbmdGeometry.MapStride))
                .ToList();

            Model3D = MatrixSceneBuilder.BuildFromPlaced(GameFamily, placed,
                NsbmdGeometry.MatrixStitchMode.Grid, standing);
        }

        private const int GridBand = 8;

        private readonly Dictionary<(int, int), float> _cellAltitude = new Dictionary<(int, int), float>();

        public int StandingCount { get; private set; }

        private bool TryTile(int floorIndex, TornWorldMapTable.Floor floor, int tileX, int tileZ,
            out float originX, out float originZ, out int col, out int row, out float altitude)
        {
            originX = originZ = 0f; col = row = 0; altitude = 0f;

            int localX = tileX - floor.OffsetX, localZ = tileZ - floor.OffsetZ;
            if (localX < 0 || localZ < 0) return false;

            int across = localX / MapFile.MapSize, down = localZ / MapFile.MapSize;
            if (!_cellAltitude.TryGetValue((floorIndex * GridBand + across, down), out altitude)) return false;

            col = localX % MapFile.MapSize;
            row = localZ % MapFile.MapSize;
            originX = (floor.OffsetX + across * MapFile.MapSize) * NsbmdGeometry.TileSize;
            originZ = (floor.OffsetZ + down * MapFile.MapSize) * NsbmdGeometry.TileSize;
            return true;
        }

        private static float HeightOf(int tileY) => tileY * (NsbmdGeometry.TileSize / 2f);
        public MapCollisionGrid Collision { get; private set; } = new MapCollisionGrid();

        private readonly Dictionary<(int x, int y), TornWorldSurfaces.Surface> _surfaces
            = new Dictionary<(int, int), TornWorldSurfaces.Surface>();

        public int WalkableTiles { get; private set; }

        public int MapTiles { get; private set; }

        public int TurnedSurfaces { get; private set; }

        private void BuildWalkable(IReadOnlyList<FloorRow> floors,
            IReadOnlyList<(int gridX, int gridY, MapFile map, byte areaId, float altitude, float originX, float originZ)> maps)
        {
            MapCollisionGrid walkable = new MapCollisionGrid();
            _surfaces.Clear();
            SurfaceRows.Clear();
            WalkableTiles = 0;
            MapTiles = 0;
            TurnedSurfaces = 0;

            for (int index = 0; index < floors.Count; index++)
            {
                TornWorldMapTable.Floor floor = floors[index].Floor;

                MapFile MapAt(int across, int down)
                {
                    foreach ((int gridX, int gridY, MapFile map, byte areaId, float altitude, float originX, float originZ) cell in maps)
                        if (cell.gridX == index * GridBand + across && cell.gridY == down) return cell.map;
                    return null;
                }

                int wide = 1, deep = 1;
                foreach ((int gridX, int gridY, MapFile map, byte areaId, float altitude, float originX, float originZ) cell in maps)
                    if (cell.gridX >= index * GridBand && cell.gridX < (index + 1) * GridBand)
                    {
                        wide = Math.Max(wide, cell.gridX - index * GridBand + 1);
                        deep = Math.Max(deep, cell.gridY + 1);
                    }

                foreach (TornWorldSurfaces.Surface surface in TornWorldSurfaces.ForFloor(floor, floors[index].Data, MapAt, Grid,
                                                                  wide, deep, index, GridBand))
                {
                    _surfaces[(surface.PatchX, surface.PatchY)] = surface;

                    if (index == IndexOfShown(CurrentFloor))
                        SurfaceRows.Add(new SurfaceRow
                        {
                            Surface = surface,
                            Label = surface.IsGround
                                ? $"Map  {surface.GroundX},{surface.GroundZ}"
                                : $"{surface.Kind}  {floors[index].Data.Platforms[surface.PlatformIndex].AttributeId}",
                        });
                    walkable.Add(surface.PatchX, surface.PatchY, surface.Collisions);
                    walkable.AddTypes(surface.PatchX, surface.PatchY, surface.Types);

                    if (!surface.IsGround && surface.Kind != TornWorldFile.PlatformKind.Floor) TurnedSurfaces++;

                    for (int row = 0; row < MapFile.MapSize; row++)
                        for (int col = 0; col < MapFile.MapSize; col++)
                        {
                            if (!surface.CanWalk(col, row)) continue;
                            WalkableTiles++;
                            if (surface.IsGround) MapTiles++;
                        }
                }
            }

            Collision = walkable;
        }

        private bool TrySurface(float tileX, float tileZ, out TornWorldSurfaces.Surface surface,
            out int col, out int row)
        {
            int patchX = (int)Math.Floor(tileX / MapFile.MapSize);
            int patchY = (int)Math.Floor(tileZ / MapFile.MapSize);
            col = (int)Math.Floor(tileX) - patchX * MapFile.MapSize;
            row = (int)Math.Floor(tileZ) - patchY * MapFile.MapSize;
            return _surfaces.TryGetValue((patchX, patchY), out surface);
        }

        public (int x, int z)? WhereToStand()
        {
            List<(int x, int z)> standing = new List<(int x, int z)>();

            TornWorldSurfaces.Surface chosen = _selectedPlatformIndex >= 0 && _selectedPlatformIndex < SurfaceRows.Count
                ? SurfaceRows[_selectedPlatformIndex].Surface : null;
            if (chosen != null && !chosen.IsGround)
                for (int row = 0; row < MapFile.MapSize; row++)
                    for (int col = 0; col < MapFile.MapSize; col++)
                        if (chosen.CanWalk(col, row)) standing.Add(chosen.WalkTile(col, row));

            if (standing.Count == 0)
            foreach (TornWorldSurfaces.Surface surface in _surfaces.Values)
            {
                if (!surface.IsGround) continue;
                for (int row = 0; row < MapFile.MapSize; row++)
                    for (int col = 0; col < MapFile.MapSize; col++)
                        if (surface.CanWalk(col, row)) standing.Add(surface.WalkTile(col, row));
            }
            if (standing.Count == 0)
                foreach (TornWorldSurfaces.Surface surface in _surfaces.Values)
                    for (int row = 0; row < MapFile.MapSize; row++)
                        for (int col = 0; col < MapFile.MapSize; col++)
                            if (surface.CanWalk(col, row)) standing.Add(surface.WalkTile(col, row));

            if (standing.Count == 0) return null;

            double midX = standing.Average(t => t.x), midZ = standing.Average(t => t.z);
            (int x, int z) best = standing[0];
            double nearest = double.MaxValue;
            foreach ((int x, int z) tile in standing)
            {
                double dx = tile.x - midX, dz = tile.z - midZ;
                double away = dx * dx + dz * dz;
                if (away < nearest) { nearest = away; best = tile; }
            }
            return best;
        }

        public (float pitch, float yaw, float roll, int steps)? CameraAt(int tileX, int tileZ, MoveFacing facing)
        {
            if (!TrySurface(tileX, tileZ, out TornWorldSurfaces.Surface surface, out int col, out int row)) return null;

            FloorRow shown = ShownFloorAt(surface.FloorIndex);
            if (shown == null) return null;

            (int worldX, int worldY, int worldZ) = surface.EventAt(col, row);

            int looking = Direction(facing);

            TornWorldFile.CameraRegion found = null;
            foreach (TornWorldFile.CameraRegion region in shown.Data.CameraRegions)
                if (region.PlayerDirection == looking && region.Bounds.Contains(worldX, worldY, worldZ))
                    found = region;

            if (found != null)
            {
                _lastCamera = (found.PitchDegrees, found.YawDegrees, found.RollDegrees, Math.Max(1, found.TransitionSteps));
                return _lastCamera;
            }

            return _lastCamera ?? (BaseCamera.PitchDegrees, BaseCamera.YawDegrees, BaseCamera.RollDegrees, 16);
        }

        private FloorRow ShownFloorAt(int floorIndex)
        {
            foreach ((int at, FloorRow floor) in ShownFloors()) if (at == floorIndex) return floor;
            return null;
        }
    }
}
