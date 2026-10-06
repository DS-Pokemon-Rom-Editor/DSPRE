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
    /// <summary>Distortion World data (Platinum): gravity boxes, surface transitions and props per floor.</summary>
    public class DistortionWorldViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public class FloorRow
        {
            public TornWorldMapTable.Floor Floor { get; set; }
            public TornWorldFile Data { get; set; }
            public string Label { get; set; }
        }

        private readonly ScriptNarc _archive = new ScriptNarc(DirNames.tornWorld);
        private readonly ScriptNarc _attributes = new ScriptNarc(DirNames.tornWorldAttributes);
        private TornWorldMapTable _table;
        private readonly HashSet<int> _editedMembers = new HashSet<int>();
        private readonly HashSet<int> _editedGrids = new HashSet<int>();

        private readonly Dictionary<int, ushort[]> _gridCache = new Dictionary<int, ushort[]>();
        private readonly Dictionary<int, ushort[]> _shownGrids = new Dictionary<int, ushort[]>();

        private readonly Dictionary<int, MapFile> _shapeEdited = new Dictionary<int, MapFile>();

        public DistortionWorldViewModel()
        {
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> {
                    DirNames.maps, DirNames.exteriorBuildingModels, DirNames.buildingTextures,
                    DirNames.mapTextures, DirNames.matrices, DirNames.areaData, DirNames.eventFiles,
                    DirNames.fieldEffectModels });
            }
            catch (Exception ex) { AppLogger.Error("DistortionWorld.Unpack: " + ex.Message); }

            MapModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(MapModelEditorViewModel.Changed) || !MapModel.Changed) return;
                if (_selectedMapCell < 0 || _selectedMapCell >= MapCells.Count) return;
                _shapeChanged.Add(MapCells[_selectedMapCell].MapId);
                Raise(nameof(HasUnsavedChanges));
            };

            FillPainters();
            Load();
        }

        public ObservableCollection<FloorRow> Floors { get; } = new ObservableCollection<FloorRow>();

        public sealed class MapCellRow
        {
            public int MapId { get; set; }
            public byte AreaId { get; set; }
            public string Label { get; set; }
            public override string ToString() => Label;
        }

        public ObservableCollection<MapCellRow> MapCells { get; } = new ObservableCollection<MapCellRow>();

        public MapModelEditorViewModel MapModel { get; } = new MapModelEditorViewModel();

        private int _selectedMapCell = -1;
        public int SelectedMapCell
        {
            get => _selectedMapCell;
            set { if (Set(ref _selectedMapCell, value)) OpenShape(); }
        }

        private void OpenShape()
        {
            if (_selectedMapCell < 0 || _selectedMapCell >= MapCells.Count)
            {
                MapModel.Open(null, 0, gameFamily, null);
                return;
            }

            MapCellRow row = MapCells[_selectedMapCell];
            if (!_shapeEdited.TryGetValue(row.MapId, out MapFile map))
            {
                try { map = new MapFile(row.MapId, gameFamily, discardMoveperms: false, showMessages: false); }
                catch (Exception ex) { AppLogger.Error("DistortionWorld.OpenShape: " + ex.Message); return; }
                _shapeEdited[row.MapId] = map;
            }

            MapModel.Open(map, row.AreaId, gameFamily, $"Map {row.MapId}");
        }

        private IEnumerable<KeyValuePair<int, MapFile>> ChangedShapes =>
            _shapeEdited.Where(kv => _shapeChanged.Contains(kv.Key));

        private readonly HashSet<int> _shapeChanged = new HashSet<int>();
        public ObservableCollection<TornWorldFile.FloatingPlatform> Platforms { get; } = new ObservableCollection<TornWorldFile.FloatingPlatform>();

        public sealed class SurfaceRow
        {
            public TornWorldSurfaces.Surface Surface { get; set; }
            public string Label { get; set; }
            public override string ToString() => Label;
        }

        public ObservableCollection<SurfaceRow> SurfaceRows { get; } = new ObservableCollection<SurfaceRow>();
        public ObservableCollection<TornWorldFile.JumpPoint> JumpPoints { get; } = new ObservableCollection<TornWorldFile.JumpPoint>();
        public ObservableCollection<TornWorldFile.CameraRegion> CameraRegions { get; } = new ObservableCollection<TornWorldFile.CameraRegion>();
        public ObservableCollection<TornWorldFile.GhostProp> GhostProps { get; } = new ObservableCollection<TornWorldFile.GhostProp>();
        public ObservableCollection<TornWorldFile.GhostTrigger> GhostTriggers { get; } = new ObservableCollection<TornWorldFile.GhostTrigger>();

        private int _selectedFloorIndex = -1;
        public int SelectedFloorIndex
        {
            get => _selectedFloorIndex;
            set { if (Set(ref _selectedFloorIndex, value)) ShowFloor(); }
        }

        public IReadOnlyDictionary<int, ushort[]> ShownGrids => _shownGrids;

        public NsbmdRenderModel Model3D { get; private set; }

        public float[] OverlayMesh { get; private set; } = Array.Empty<float>();
        public int OverlayVertexCount { get; private set; }

        private bool _showGravity = true;
        public bool ShowGravity { get => _showGravity; set { if (Set(ref _showGravity, value)) { RebuildOverlay(); SceneShown?.Invoke(); } } }

        private bool _flat2D;
        public bool Flat2D { get => _flat2D; set => Set(ref _flat2D, value); }

        private double _overlayHeight;
        public double OverlayHeight { get => _overlayHeight; set { if (Set(ref _overlayHeight, value)) { RebuildOverlay(); SceneShown?.Invoke(); } } }

        public event Action SceneShown;

        public AreaData Area { get; private set; }
        public EventFile Events { get; private set; }
        public int CameraId { get; private set; }
        public int MusicDayId { get; private set; }
        public int MusicNightId { get; private set; }

        public event Action FloorShown;

        public event Action PlatformShown;

        private int _selectedPlatformIndex = -1;
        public int SelectedPlatformIndex
        {
            get => _selectedPlatformIndex;
            set { if (Set(ref _selectedPlatformIndex, value)) ShowPlatform(); }
        }

        public byte[,] CollisionCells { get; } = new byte[PaintedGridSize, PaintedGridSize];
        public byte[,] BehaviourCells { get; } = new byte[PaintedGridSize, PaintedGridSize];

        public int UsedRows { get; private set; } = PaintedGridSize;
        public int UsedColumns { get; private set; } = PaintedGridSize;

        private static int Down(TornWorldFile.FloatingPlatform platform)
            => platform.Kind == TornWorldFile.PlatformKind.WestWall || platform.Kind == TornWorldFile.PlatformKind.EastWall
                ? platform.Bounds.SizeY
                : platform.Bounds.SizeX;

        private const int PaintedGridSize = 32;

        private string _platformNote = "";
        public string PlatformNote { get => _platformNote; private set => Set(ref _platformNote, value); }

        private string _hoverNote = "";

        public string HoverNote { get => _hoverNote; private set => Set(ref _hoverNote, value); }

        public void HoverGrid(int? col, int? row)
        {
            if (col == null || row == null)
            {
                HoverNote = "";
                return;
            }

            TornWorldFile.FloatingPlatform platform = _selectedPlatformIndex >= 0 && _selectedPlatformIndex < Platforms.Count
                ? Platforms[_selectedPlatformIndex] : null;
            if (platform == null) { HoverNote = ""; return; }

            TornWorldFile.Bounds bounds = platform.Bounds;
            int c = col.Value, r = row.Value;

            int worldX, worldY, worldZ = bounds.StartZ + r;
            switch (platform.Kind)
            {
                case TornWorldFile.PlatformKind.WestWall:
                    worldX = bounds.StartX; worldY = bounds.StartY + bounds.SizeY - c; break;
                case TornWorldFile.PlatformKind.EastWall:
                    worldX = bounds.StartX; worldY = bounds.StartY + c; break;
                case TornWorldFile.PlatformKind.Ceiling:
                    worldX = bounds.StartX + bounds.SizeX - c; worldY = bounds.StartY; break;
                default:
                    worldX = bounds.StartX + c; worldY = bounds.StartY; break;
            }

            bool past = c >= UsedColumns || r >= UsedRows;
            bool blocked = CollisionCells[r, c] != 0;
            byte behaviour = BehaviourCells[r, c];

            string name = TilePermissions.BehaviourLabel(behaviour, gameFamily);

            HoverNote = past
                ? $"x {worldX}, y {worldY}, z {worldZ}  ·  outside"
                : $"x {worldX}, y {worldY}, z {worldZ}  ·  {(blocked ? "blocked" : "walk")}  ·  {name}";
        }

        private string _gridNote = "";

        public string GridNote { get => _gridNote; private set => Set(ref _gridNote, value); }

        private int _collisionPainterIndex;
        public int CollisionPainterIndex
        {
            get => _collisionPainterIndex;
            set { if (Set(ref _collisionPainterIndex, value)) Raise(nameof(CollisionPaintValue)); }
        }

        public byte CollisionPaintValue => _collisionPainterIndex == 1 ? (byte)0x80 : (byte)0x00;

        public sealed class Painter
        {
            public Painter(byte value, string name) { Value = value; Name = name; }
            public byte Value { get; }
            public string Name { get; }
            public override string ToString() => Name;
        }

        public ObservableCollection<Painter> BehaviourPainters { get; } = new ObservableCollection<Painter>();

        private int _behaviourPainterIndex;
        public int BehaviourPainterIndex
        {
            get => _behaviourPainterIndex;
            set { if (Set(ref _behaviourPainterIndex, value)) Raise(nameof(BehaviourPaint)); }
        }

        private void FillPainters()
        {
            BehaviourPainters.Clear();
            foreach (TileBehaviour b in TilePermissions.BehavioursFor(gameFamily))
                BehaviourPainters.Add(new Painter(b.Value, b.Label));
            _behaviourPainterIndex = 0;
            Raise(nameof(BehaviourPainterIndex));
        }

        public byte BehaviourPaint =>
            _behaviourPainterIndex >= 0 && _behaviourPainterIndex < BehaviourPainters.Count
                ? BehaviourPainters[_behaviourPainterIndex].Value : (byte)0;

        private string _floorNote = "";
        public string FloorNote { get => _floorNote; private set => Set(ref _floorNote, value); }

        private string _status = "";
        public string Status { get => _status; private set => Set(ref _status, value); }

        public bool Available => _table != null && _table.Floors.Count > 0;

        private void Load()
        {
            try
            {
                byte[] table = _archive.Available ? _archive.Get(0) : null;
                if (table == null) { Status = "This ROM has no Distortion World data."; return; }

                _table = new TornWorldMapTable(table);
                IReadOnlyList<string> labels = HeaderNames();

                foreach (TornWorldMapTable.Floor floor in _table.Floors)
                {
                    string name = labels != null && floor.HeaderId < labels.Count ? labels[(int)floor.HeaderId] : null;
                    Floors.Add(new FloorRow
                    {
                        Floor = floor,
                        Data = new TornWorldFile(_archive.Get(floor.DataMember)),
                        Label = string.IsNullOrWhiteSpace(name) ? $"Header {floor.HeaderId}" : name,
                    });
                }

                Status = $"{Floors.Count} floors.";
                if (Floors.Count > 0) SelectedFloorIndex = 0;
                ResetSteps();
            }
            catch (Exception ex)
            {
                AppLogger.Error("DistortionWorld.Load: " + ex.Message);
                Status = "Could not read Distortion World data: " + ex.Message;
            }
        }

        private static IReadOnlyList<string> HeaderNames()
        {
            try { return HeaderLabels.Friendly(); } catch { return null; }
        }

        private int _shownFloorIndex = -1;

        public void ShowFloorAgain() => ShowFloor();

        private void ShowFloor()
        {
            CommitShownFloor();

            Platforms.Clear(); JumpPoints.Clear(); CameraRegions.Clear();
            GhostProps.Clear(); GhostTriggers.Clear();
            _shownFloorIndex = _selectedFloorIndex;
            if (_selectedFloorIndex < 0 || _selectedFloorIndex >= Floors.Count) { FloorNote = ""; return; }

            FloorRow row = Floors[_selectedFloorIndex];
            foreach (TornWorldFile.FloatingPlatform platform in row.Data.Platforms) Platforms.Add(platform);
            foreach (TornWorldFile.JumpPoint jump in row.Data.JumpPoints) JumpPoints.Add(jump);
            foreach (TornWorldFile.CameraRegion region in row.Data.CameraRegions) CameraRegions.Add(region);
            foreach (TornWorldFile.GhostProp prop in row.Data.GhostProps) GhostProps.Add(prop);
            foreach (TornWorldFile.GhostTrigger trigger in row.Data.GhostTriggers) GhostTriggers.Add(trigger);

            _shownGrids.Clear();
            foreach (TornWorldFile.FloatingPlatform platform in row.Data.Platforms)
            {
                ushort[] grid = Grid(platform.AttributeId);
                if (grid != null) _shownGrids[platform.AttributeId] = grid;
            }
            RememberGridShapes();

            FloorNote = $"File {row.Floor.DataMember}  ·  x {row.Floor.OffsetX}, y {row.Floor.OffsetAltitude}, z {row.Floor.OffsetZ}";

            BuildScene(ShownFloors().Select(f => f.floor).ToList());

            FloorShown?.Invoke();
            _selectedPlatformIndex = SurfaceRows.Count > 0 ? 0 : -1;
            Raise(nameof(SelectedPlatformIndex));
            ShowPlatform();
        }

        private bool _wholeWorld;

        public bool WholeWorld
        {
            get => _wholeWorld;
            set
            {
                if (!Set(ref _wholeWorld, value)) return;
                Raise(nameof(OneFloorOnly));
                BuildScene(ShownFloors().Select(f => f.floor).ToList());
                FloorShown?.Invoke();
            }
        }

        public bool OneFloorOnly
        {
            get => !_wholeWorld;
            set { if (value == _wholeWorld) WholeWorld = !value; }
        }

        public (float x, float y, float z)? SelectedFloorCentre()
        {
            TornWorldMapTable.Floor floor = CurrentFloor?.Floor;
            if (Model3D == null || floor == null) return null;

            int index = 0;
            foreach ((int i, FloorRow shown) in ShownFloors()) if (shown == CurrentFloor) { index = i; break; }
            if (!TryTile(index, floor, floor.OffsetX, floor.OffsetZ, out float originX, out float originZ, out _, out _, out _))
                return null;

            return Model3D.ToNormalized(originX + NsbmdGeometry.MapStride / 2f,
                                        floor.OffsetAltitude * (NsbmdGeometry.TileSize / 2f),
                                        originZ + NsbmdGeometry.MapStride / 2f);
        }

        private int IndexOfShown(FloorRow floor)
        {
            foreach ((int index, FloorRow shown) in ShownFloors()) if (shown == floor) return index;
            return 0;
        }

        private FloorRow CurrentFloor =>
            _selectedFloorIndex >= 0 && _selectedFloorIndex < Floors.Count ? Floors[_selectedFloorIndex] : null;

        private IEnumerable<(int index, FloorRow floor)> ShownFloors()
        {
            if (_wholeWorld)
            {
                for (int i = 0; i < Floors.Count; i++) yield return (i, Floors[i]);
            }
            else if (CurrentFloor != null)
            {
                yield return (0, CurrentFloor);
            }
        }
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
                List<(int gridX, int gridY, MapFile map, byte areaId, float altitude, float originX, float originZ)> cells = new List<(int gridX, int gridY, MapFile map, byte areaId, float altitude,
                                      float originX, float originZ)>();
                MapCells.Clear();

                for (int index = 0; index < floors.Count; index++)
                {
                    TornWorldMapTable.Floor floor = floors[index].Floor;
                    MapHeader header;
                    try { header = MapHeader.GetMapHeader((ushort)floor.HeaderId); } catch { header = null; }
                    if (header == null) continue;

                    if (Area == null)
                    {
                        CameraId = header.cameraAngleID;
                        MusicDayId = header.musicDayID;
                        MusicNightId = header.musicNightID;
                        try { Area = new AreaData(header.areaDataID); } catch { Area = null; }
                        try { Events = new EventFile(header.eventFileID); } catch { Events = null; }
                    }

                    GameMatrix matrix = new GameMatrix(header.matrixID);
                    for (int y = 0; y < matrix.height; y++)
                        for (int x = 0; x < matrix.width; x++)
                        {
                            if (matrix.maps[y, x] == GameMatrix.EMPTY) continue;
                            if (matrix.hasHeadersSection && matrix.headers[y, x] != floor.HeaderId) continue;

                            float altitude = ((matrix.hasHeightsSection ? matrix.altitudes[y, x] : 0) + floor.OffsetAltitude)
                                           * (NsbmdGeometry.TileSize / 2f);

                            MapCells.Add(new MapCellRow
                            {
                                MapId = matrix.maps[y, x],
                                AreaId = header.areaDataID,
                                Label = $"Map {matrix.maps[y, x]}  ·  {floor.HeaderId} at {x},{y}",
                            });

                            cells.Add((index * GridBand + x, y,
                                       new MapFile(matrix.maps[y, x], gameFamily, discardMoveperms: false),
                                       header.areaDataID, altitude,
                                       (floor.OffsetX + x * MapFile.mapSize) * NsbmdGeometry.TileSize,
                                       (floor.OffsetZ + y * MapFile.mapSize) * NsbmdGeometry.TileSize));
                        }
                }

                _selectedMapCell = MapCells.Count > 0 ? 0 : -1;
                Raise(nameof(SelectedMapCell));
                OpenShape();

                if (cells.Count == 0) return;

                foreach ((int gridX, int gridY, MapFile map, byte areaId, float altitude, float originX, float originZ) c in cells) _cellAltitude[(c.gridX, c.gridY)] = c.altitude;
                List<(int cellX, int cellY, PlacedBuilding placed)> standing = StandingModels(floors);
                BuildWalkable(floors, cells);

                int leastX = cells.Min(c => c.gridX), leastY = cells.Min(c => c.gridY);
                List<(int gridX, int gridY, MapFile map, byte areaId, float altitude, float, float)> placed = cells.Select(c => (c.gridX, c.gridY, c.map, c.areaId, c.altitude,
                        c.originX - (c.gridX - leastX) * NsbmdGeometry.MapStride,
                        c.originZ - (c.gridY - leastY) * NsbmdGeometry.MapStride))
                    .ToList();

                Model3D = MatrixSceneBuilder.BuildFromPlaced(gameFamily, placed,
                    NsbmdGeometry.MatrixStitchMode.Grid, standing);
            }
            catch (Exception ex)
            {
                AppLogger.Error("DistortionWorld.BuildScene: " + ex.Message);
            }

            RebuildOverlay();
            SceneShown?.Invoke();

            if (!HasUnsavedChanges)
                Status = StandingCount > 0
                    ? $"{Floors.Count} floors, {StandingCount} platforms and props, {WalkableTiles} walkable tiles"
                    : $"{Floors.Count} floors.";
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

            int across = localX / MapFile.mapSize, down = localZ / MapFile.mapSize;
            if (!_cellAltitude.TryGetValue((floorIndex * GridBand + across, down), out altitude)) return false;

            col = localX % MapFile.mapSize;
            row = localZ % MapFile.mapSize;
            originX = (floor.OffsetX + across * MapFile.mapSize) * NsbmdGeometry.TileSize;
            originZ = (floor.OffsetZ + down * MapFile.mapSize) * NsbmdGeometry.TileSize;
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

                    for (int row = 0; row < MapFile.mapSize; row++)
                        for (int col = 0; col < MapFile.mapSize; col++)
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
            int patchX = (int)Math.Floor(tileX / MapFile.mapSize);
            int patchY = (int)Math.Floor(tileZ / MapFile.mapSize);
            col = (int)Math.Floor(tileX) - patchX * MapFile.mapSize;
            row = (int)Math.Floor(tileZ) - patchY * MapFile.mapSize;
            return _surfaces.TryGetValue((patchX, patchY), out surface);
        }

        public (int x, int z)? WhereToStand()
        {
            List<(int x, int z)> standing = new List<(int x, int z)>();

            TornWorldSurfaces.Surface chosen = _selectedPlatformIndex >= 0 && _selectedPlatformIndex < SurfaceRows.Count
                ? SurfaceRows[_selectedPlatformIndex].Surface : null;
            if (chosen != null && !chosen.IsGround)
                for (int row = 0; row < MapFile.mapSize; row++)
                    for (int col = 0; col < MapFile.mapSize; col++)
                        if (chosen.CanWalk(col, row)) standing.Add(chosen.WalkTile(col, row));

            if (standing.Count == 0)
            foreach (TornWorldSurfaces.Surface surface in _surfaces.Values)
            {
                if (!surface.IsGround) continue;
                for (int row = 0; row < MapFile.mapSize; row++)
                    for (int col = 0; col < MapFile.mapSize; col++)
                        if (surface.CanWalk(col, row)) standing.Add(surface.WalkTile(col, row));
            }
            if (standing.Count == 0)
                foreach (TornWorldSurfaces.Surface surface in _surfaces.Values)
                    for (int row = 0; row < MapFile.mapSize; row++)
                        for (int col = 0; col < MapFile.mapSize; col++)
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

        // ── Walk preview state ───────────────────────────────────────────────────────

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

        /// <summary>The game runs these as field tasks, which take the controls until they finish.</summary>
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

        /// <summary>Starts a walk the way a warp in does: default flags, this floor's default prop groups.</summary>
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

        /// <summary>How see-through a placed prop is in the walk preview, or null to draw it as it is.</summary>
        public float? OpacityOf(int modelId)
        {
            if (!_placed.TryGetValue(modelId, out PlacedProp placed)) return null;

            if (placed.Carried) return RideActive && modelId == _rideAnchor ? (float?)null : 0f;
            if (placed.GhostGroup >= 0) return GhostVisible(placed.Header, placed.GhostGroup) ? (float?)null : 0f;
            if (placed.Platform == null) return null;

            if (RideActive)
            {
                if (modelId == _rideAnchor) return null;
                // Where the ride ends there is already a template, which the ridden platform becomes.
                if (_ride.PlatformHeader == placed.Header && _ride.Platform == placed.Platform) return 0f;
            }
            return _runtime.IsPresent(placed.Platform) ? (float?)null : 0f;
        }

        // Floors and props stand half a tile per Y step (HeightOf), so the ride is measured the same way.
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

        /// <summary>Shows the floor the ride is now on, when the preview shows one floor at a time.</summary>
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

        /// <summary>After the preview reloaded a floor: where the rider now is and what frame it is.</summary>
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

            // Ground height comes from the land data, so only X and Z pick the tile.
            foreach (TornWorldSurfaces.Surface surface in _surfaces.Values)
            {
                if (surface.FloorIndex != floorIndex || !surface.IsGround) continue;
                int col = worldX - surface.GroundX, row = worldZ - surface.GroundZ;
                if (col >= 0 && row >= 0 && col < MapFile.mapSize && row < MapFile.mapSize) return surface.WalkTile(col, row);
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

            // HandleGhostPropTriggerAt only runs after a real step, which is the only way here.
            _runtime.StepOn(shown.Data, worldX, worldY, worldZ, looking);

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
                    // Ground height comes from the land data, so only X and Z pick the tile.
                    if (!GroundTile(landing, toX, toZ, out toCol, out toRow)) continue;
                }
                else
                {
                    if ((int)landing.Kind != point.TargetKind) continue;
                    if (point.TargetPlatformIndex != 0xFFFF && landing.PlatformIndex != point.TargetPlatformIndex) continue;
                    if (!landing.TryTileFor(toX, toY, toZ, out toCol, out toRow)) continue;
                }
                if (!landing.CanWalk(toCol, toRow)) continue;

                (int walkX, int walkZ) = landing.WalkTile(toCol, toRow);

                // A walk that starts on a wall has no roll of its own yet; the wall's turn is the one being left.
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
            return col >= 0 && row >= 0 && col < MapFile.mapSize && row < MapFile.mapSize;
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

        public (float x, float y, float z) TileFoot(float tileX, float tileZ)
        {
            NsbmdRenderModel scene = Model3D;
            if (scene == null) return (0f, 0f, 0f);

            if (RideActive && (int)Math.Floor(tileX) == _rideTile.x && (int)Math.Floor(tileZ) == _rideTile.z)
            {
                return scene.ToNormalized(
                    _rideFootRaw.x + (_ride.X - _ride.StartX) * NsbmdGeometry.TileSize,
                    _rideFootRaw.y + (_ride.Y - _ride.StartY) * (NsbmdGeometry.TileSize / 2f),
                    _rideFootRaw.z + (_ride.Z - _ride.StartZ) * NsbmdGeometry.TileSize);
            }

            (float x, float y, float z)? raw = RawFoot(tileX, tileZ);
            return raw == null ? (0f, 0f, 0f) : scene.ToNormalized(raw.Value.x, raw.Value.y, raw.Value.z);
        }

        private (float x, float y, float z)? RawFoot(float tileX, float tileZ)
        {
            NsbmdRenderModel scene = Model3D;
            if (scene == null || !TrySurface(tileX, tileZ, out TornWorldSurfaces.Surface surface, out int col, out int row)) return null;

            float acrossCol = tileX - (float)Math.Floor(tileX);
            float acrossRow = tileZ - (float)Math.Floor(tileZ);

            (int x0, int y0, int z0) = surface.WorldAt(col, row);
            (int x1, int y1, int z1) = surface.WorldAt(col + 1, row);
            (int x2, int y2, int z2) = surface.WorldAt(col, row + 1);

            float worldX = x0 + (x1 - x0) * acrossCol + (x2 - x0) * acrossRow;
            float worldY = y0 + (y1 - y0) * acrossCol + (y2 - y0) * acrossRow;
            float worldZ = z0 + (z1 - z0) * acrossCol + (z2 - z0) * acrossRow;

            if (_crossing != null && CurrentFrame != null
                && (int)Math.Floor(tileX) == _crossing.ToTileX && (int)Math.Floor(tileZ) == _crossing.ToTileZ)
            {
                float through = _crossing.Through(CurrentFrame());
                if (through >= 1f) _crossing = null;
                else if (through >= 0f && TrySurface(_crossing.FromTileX, _crossing.FromTileZ, out TornWorldSurfaces.Surface was,
                                                    out int wasCol, out int wasRow))
                {
                    (int fx, int fy, int fz) = was.WorldAt(wasCol, wasRow);
                    worldX = fx + (worldX - fx) * through;
                    worldY = fy + (worldY - fy) * through;
                    worldZ = fz + (worldZ - fz) * through;
                }
            }

            float rawX = (worldX + 0.5f) * NsbmdGeometry.TileSize;
            float rawZ = (worldZ + 0.5f) * NsbmdGeometry.TileSize;
            float rawY = worldY * (NsbmdGeometry.TileSize / 2f);

            if (surface.IsGround)
            {
                int patchX = (int)Math.Floor(tileX / MapFile.mapSize);
                int patchY = (int)Math.Floor(tileZ / MapFile.mapSize);
                if (scene.TryBdhcSurfaceY(patchX, patchY, rawX, rawZ, rawY, out float surfaceY)) rawY = surfaceY;
            }

            return (rawX, rawY, rawZ);
        }

        private sealed class PlacedProp
        {
            public int ModelId;
            public uint Header;
            public TornWorldCodeTables.MovingPlatform Platform;
            public long GhostGroup = -1;
            public bool Carried;
            public int HomeX, HomeY, HomeZ;
        }

        private readonly Dictionary<int, PlacedProp> _placed = new Dictionary<int, PlacedProp>();

        private List<(int cellX, int cellY, PlacedBuilding placed)> StandingModels(IReadOnlyList<FloorRow> floors)
        {
            List<(int, int, PlacedBuilding)> standing = new List<(int, int, PlacedBuilding)>();

            TornWorldCodeTables.Tables tables = TornWorldCodeTables.Read(out string why);
            if (tables == null)
            {
                if (why != null) AppLogger.Error("DistortionWorld.StandingModels: " + why);
                return standing;
            }

            for (int index = 0; index < floors.Count; index++)
            {
                uint header = (uint)floors[index].Floor.HeaderId;
                foreach (TornWorldCodeTables.MovingPlatform platform in tables.PlatformsOn(header))
                    Place(standing, tables, floors[index].Floor, index, platform.PropKind,
                          platform.TileX, platform.TileY, platform.TileZ,
                          new PlacedProp { Header = header, Platform = platform });
                foreach (TornWorldCodeTables.Prop prop in tables.PropsOn(header))
                    Place(standing, tables, floors[index].Floor, index, prop.PropKind,
                          prop.TileX, prop.TileY, prop.TileZ, new PlacedProp { Header = header });
                foreach (TornWorldFile.GhostProp ghost in floors[index].Data.GhostProps)
                    Place(standing, tables, floors[index].Floor, index, ghost.PropKind,
                          ghost.TileX, ghost.TileY, ghost.TileZ,
                          new PlacedProp { Header = header, GhostGroup = ghost.GroupId });

                // A floor loaded mid-ride gets the platform being ridden, where its current path set off from.
                if (RideActive && _ride.Header == header)
                {
                    int id = Place(standing, tables, floors[index].Floor, index, _ride.Platform.PropKind,
                                   _ride.PathStartX, _ride.PathStartY, _ride.PathStartZ,
                                   new PlacedProp { Header = header, Carried = true });
                    if (id != 0)
                    {
                        _rideAnchor = id;
                        _rideAnchorHome = (_ride.PathStartX, _ride.PathStartY, _ride.PathStartZ);
                    }
                }
            }

            StandingCount = standing.Count;
            return standing;
        }

        private int Place(List<(int, int, PlacedBuilding)> standing, TornWorldCodeTables.Tables tables,
            TornWorldMapTable.Floor floor, int floorIndex, int propKind, int tileX, int tileY, int tileZ,
            PlacedProp info)
        {
            NSBMDModel model = PropModel(tables.ModelFor(propKind));
            if (model == null) return 0;
            if (!TryTile(floorIndex, floor, tileX, tileZ, out _, out _, out int col, out int row, out float altitude)) return 0;

            int modelId = TravellingId();
            info.ModelId = modelId;
            info.HomeX = tileX; info.HomeY = tileY; info.HomeZ = tileZ;
            _placed[modelId] = info;
            RegisterMotion(tables, propKind, modelId, info.Platform != null || info.Carried);

            (float offX, float offY, float offZ) = tables.OffsetFor(propKind);
            float unit = NsbmdGeometry.TileSize / 16f;

            float x = (col + 0.5f) * NsbmdGeometry.TileSize - NsbmdGeometry.MapStride / 2f + offX * unit;
            float z = (row + 0.5f) * NsbmdGeometry.TileSize - NsbmdGeometry.MapStride / 2f + offZ * unit;
            float y = HeightOf(tileY) - altitude + offY * unit;

            // sPropScaleByKind only sizes the culling box (IsPropInView); props are drawn unscaled.
            float scale = (model.modelScale == 0 ? 1f : model.modelScale) / 64f;
            float[] transform = Mat4.Multiply(Mat4.Scale(scale, scale, scale),
                                          Mat4.Translate(x / scale, y / scale, z / scale));

            int gridX = floorIndex * GridBand + (tileX - floor.OffsetX) / MapFile.mapSize;
            int gridY = (tileZ - floor.OffsetZ) / MapFile.mapSize;

            if (info.Platform != null && info.Platform.IsElevator)
                _elevatorTiles[(gridX * MapFile.mapSize + col, gridY * MapFile.mapSize + row)] = info;

            standing.Add((gridX, gridY, new PlacedBuilding
            {
                Model = model,
                Transform = transform,
                ModelId = modelId,
                TileX = gridX * MapFile.mapSize + col,
                TileZ = gridY * MapFile.mapSize + row,
            }));
            return modelId;
        }

        private int _travelling;
        private int TravellingId() => -1000 - _travelling++;

        private readonly Dictionary<(int x, int z), PlacedProp> _elevatorTiles = new Dictionary<(int, int), PlacedProp>();

        public Func<int> CurrentFrame;

        private readonly Dictionary<int, (TextureSrtAnimation scrolling, JointAnimation joint)> _kindAnimations
            = new Dictionary<int, (TextureSrtAnimation, JointAnimation)>();

        private void RegisterMotion(TornWorldCodeTables.Tables tables, int propKind, int modelId, bool rides)
        {
            int member = tables.AnimationFor(propKind);

            if (member < 0)
            {
                BuildingAnimationSet.HoverAnimation hover = tables.HoverOffsets.Length > 0
                    ? new BuildingAnimationSet.HoverAnimation(tables.HoverOffsets, tables.HoverStep) : null;
                if (rides) BuildingAnimationSet.Register(modelId, motion: new PlatformMotion(this, modelId, hover));
                else if (hover != null) BuildingAnimationSet.Register(modelId, motion: hover);
                return;
            }

            if (!_kindAnimations.TryGetValue(propKind, out (TextureSrtAnimation scrolling, JointAnimation joint) loaded))
            {
                loaded = (null, null);
                byte[] raw = FieldEffectMember(member);
                if (raw != null && raw.Length >= 4)
                {
                    string magic = System.Text.Encoding.ASCII.GetString(raw, 0, 4);
                    try
                    {
                        if (magic == "BTA0") loaded.scrolling = TextureSrtAnimation.Load(raw);
                        else if (magic == "BCA0") loaded.joint = JointAnimation.Load(raw);
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error($"DistortionWorld.RegisterMotion({propKind}): " + ex.Message);
                    }
                }
                _kindAnimations[propKind] = loaded;
            }

            BuildingAnimationSet.Register(modelId, scrolling: loaded.scrolling, joint: loaded.joint,
                motion: rides ? new PlatformMotion(this, modelId, null) : null);
        }

        /// <summary>A moving platform's bob, plus the ride when it is the one being ridden.</summary>
        private sealed class PlatformMotion : BuildingAnimationSet.WholeModelMotion
        {
            private readonly DistortionWorldViewModel _owner;
            private readonly int _modelId;
            private readonly BuildingAnimationSet.HoverAnimation _hover;

            public PlatformMotion(DistortionWorldViewModel owner, int modelId, BuildingAnimationSet.HoverAnimation hover)
            {
                _owner = owner; _modelId = modelId; _hover = hover;
            }

            public override (float x, float y, float z) At(int frame)
            {
                (float hx, float hy, float hz) = _hover?.At(frame) ?? (0f, 0f, 0f);
                (float rx, float ry, float rz) = _owner.RideShift(_modelId);
                return (hx + rx, hy + ry, hz + rz);
            }
        }

        private static byte[] FieldEffectMember(int member)
        {
            try
            {
                if (!gameDirs.ContainsKey(DirNames.fieldEffectModels)) return null;
                string path = System.IO.Path.Combine(gameDirs[DirNames.fieldEffectModels].unpackedDir, member.ToString("D4"));
                return System.IO.File.Exists(path) ? System.IO.File.ReadAllBytes(path) : null;
            }
            catch { return null; }
        }

        private readonly Dictionary<int, NSBMDModel> _propModels = new Dictionary<int, NSBMDModel>();

        private NSBMDModel PropModel(int member)
        {
            if (member < 0) return null;
            if (_propModels.TryGetValue(member, out NSBMDModel cached)) return cached;

            NSBMDModel model = null;
            try
            {
                if (gameDirs.ContainsKey(DirNames.fieldEffectModels))
                {
                    string path = System.IO.Path.Combine(gameDirs[DirNames.fieldEffectModels].unpackedDir, member.ToString("D4"));
                    if (System.IO.File.Exists(path))
                    {
                        using FileStream file = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read);
                        NSBMD nsbmd = NSBMDLoader.LoadNSBMD(file);
                        if (nsbmd?.models != null && nsbmd.models.Length > 0)
                        {
                            try { nsbmd.MatchTextures(); } catch { /* it draws untextured */ }
                            model = nsbmd.models[0];
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"DistortionWorld.PropModel({member}): " + ex.Message);
            }

            _propModels[member] = model;
            return model;
        }

        private void RebuildOverlay()
        {
            List<float> mesh = new List<float>();
            if (Model3D != null && _showGravity)
                foreach ((int floorIndex, FloorRow floor) in ShownFloors())
                {
                    TornWorldSurfaces.Surface chosen = _selectedPlatformIndex >= 0 && _selectedPlatformIndex < SurfaceRows.Count
                        ? SurfaceRows[_selectedPlatformIndex].Surface : null;
                    bool selected = floor == CurrentFloor;

                    for (int i = 0; i < floor.Data.Platforms.Count; i++)
                        AppendPlatform(mesh, floor.Data.Platforms[i], floor.Floor, floorIndex,
                                       selected && chosen != null && !chosen.IsGround && chosen.PlatformIndex == i);
                }

            OverlayMesh = mesh.ToArray();
            OverlayVertexCount = OverlayMesh.Length / 8;
            Raise(nameof(OverlayMesh));
        }

        private const float TileInset = 0.16f;
        private const float Lift = 0.35f;

        private static readonly (float r, float g, float b)[] KindColours =
        {
            (0.30f, 0.60f, 0.38f), (0.32f, 0.49f, 0.77f), (0.59f, 0.42f, 0.77f), (0.76f, 0.55f, 0.27f),
        };

        private void AppendPlatform(List<float> mesh, TornWorldFile.FloatingPlatform platform,
            TornWorldMapTable.Floor floor, int floorIndex, bool selected)
        {
            ushort[] grid = Grid(platform.AttributeId);
            if (grid == null) return;

            int kind = (int)platform.Kind;
            (float r, float g, float b) = kind >= 0 && kind < KindColours.Length ? KindColours[kind] : (0.6f, 0.6f, 0.6f);
            if (selected) { r = Math.Min(1f, r * 1.6f); g = Math.Min(1f, g * 1.6f); b = Math.Min(1f, b * 1.6f); }

            TornWorldFile.Bounds bounds = platform.Bounds;
            for (int z = 0; z <= bounds.SizeZ; z++)
                for (int x = 0; x <= bounds.SizeX; x++)
                    for (int y = 0; y <= bounds.SizeY; y++)
                    {
                        int tileX = bounds.StartX + x, tileY = bounds.StartY + y, tileZ = bounds.StartZ + z;
                        ushort tile = TornWorldFile.AttributeAt(platform, grid, tileX, tileY, tileZ) ?? 0x8000;
                        if ((tile & 0x8000) != 0) continue;
                        if (!TryTile(floorIndex, floor, tileX, tileZ, out float originX, out float originZ,
                                     out int col, out int row, out _)) continue;

                        float sizeX = NsbmdGeometry.TileSize, sizeZ = NsbmdGeometry.TileSize;
                        float x0 = originX + col * sizeX, z0 = originZ + row * sizeZ;
                        float height = HeightOf(tileY) + Lift + (float)_overlayHeight;

                        float insetX = sizeX * TileInset, insetZ = sizeZ * TileInset;
                        bool special = (tile & 0xFF) != 0;
                        AddQuad(mesh, x0 + insetX, x0 + sizeX - insetX, z0 + insetZ, z0 + sizeZ - insetZ, height,
                            special ? 0.89f : r, special ? 0.76f : g, special ? 0.33f : b);
                    }
        }

        private void AddQuad(List<float> v, float x0, float x1, float z0, float z1, float y, float r, float g, float b)
        {
            void Vertex(float x, float z)
            {
                (float nx, float ny, float nz) = Model3D.ToNormalized(x, y, z);
                v.Add(nx); v.Add(ny); v.Add(nz); v.Add(0); v.Add(0); v.Add(r); v.Add(g); v.Add(b);
            }
            Vertex(x0, z0); Vertex(x1, z0); Vertex(x1, z1);
            Vertex(x0, z0); Vertex(x1, z1); Vertex(x0, z1);
        }
        private ushort[] Grid(int attributeId)
        {
            if (_gridCache.TryGetValue(attributeId, out ushort[] cached)) return cached;
            if (!_attributes.Available) return null;

            try
            {
                byte[] raw = _attributes.Get(attributeId);
                if (raw == null) return null;

                ushort[] grid = TornWorldSurfaces.GridFromBytes(raw);
                _gridCache[attributeId] = grid;
                return grid;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"DistortionWorld.Grid({attributeId}): " + ex.Message);
                return null;
            }
        }

        private void ShowPlatform()
        {
            for (int row = 0; row < PaintedGridSize; row++)
                for (int col = 0; col < PaintedGridSize; col++)
                {
                    CollisionCells[row, col] = MapCollisionGrid.BlockedBit;
                    BehaviourCells[row, col] = 0;
                }

            TornWorldSurfaces.Surface chosen = _selectedPlatformIndex >= 0 && _selectedPlatformIndex < SurfaceRows.Count
                ? SurfaceRows[_selectedPlatformIndex].Surface : null;

            if (chosen == null)
            {
                UsedRows = UsedColumns = PaintedGridSize;
                PlatformNote = "";
                GridNote = "";
                PlatformShown?.Invoke();
                return;
            }

            for (int row = 0; row < PaintedGridSize; row++)
                for (int col = 0; col < PaintedGridSize; col++)
                {
                    CollisionCells[row, col] = chosen.Collisions[row, col];
                    BehaviourCells[row, col] = chosen.Types[row, col];
                }

            if (chosen.IsGround)
            {
                UsedRows = UsedColumns = PaintedGridSize;
                PlatformNote = $"Map  ·  x {chosen.GroundX} to {chosen.GroundX + 31}, "
                             + $"z {chosen.GroundZ} to {chosen.GroundZ + 31}";
            }
            else
            {
                bool upright = chosen.Kind == TornWorldFile.PlatformKind.WestWall
                            || chosen.Kind == TornWorldFile.PlatformKind.EastWall;
                TornWorldFile.FloatingPlatform platform = chosen.PlatformIndex < Platforms.Count ? Platforms[chosen.PlatformIndex] : null;
                (int columns, int rows) = platform != null ? TornWorldSurfaces.PaintedExtent(platform) : (0, 0);
                UsedColumns = columns;
                UsedRows = rows;
                PlatformNote = $"{columns} across ({(upright ? "height" : "x")}) x {rows} down (z)";
            }

            GridNote = chosen.IsGround
                ? "Map permissions"
                : Explain(chosen.Kind);

            RebuildOverlay();
            PlatformShown?.Invoke();
            SceneShown?.Invoke();
        }

        private static string Explain(TornWorldFile.PlatformKind kind)
        {
            switch (kind)
            {
                case TornWorldFile.PlatformKind.WestWall:
                    return "West wall: columns run down Y, rows along Z.";
                case TornWorldFile.PlatformKind.EastWall:
                    return "East wall: columns run up Y, rows along Z.";
                case TornWorldFile.PlatformKind.Ceiling:
                    return "Ceiling: columns run along -X, rows along Z.";
                default:
                    return "Floor: columns run along X, rows along Z.";
            }
        }

        public void ApplyPaintedGrid()
        {
            TornWorldSurfaces.Surface chosen = _selectedPlatformIndex >= 0 && _selectedPlatformIndex < SurfaceRows.Count
                ? SurfaceRows[_selectedPlatformIndex].Surface : null;
            if (chosen == null) return;

            for (int row = 0; row < PaintedGridSize; row++)
                for (int col = 0; col < PaintedGridSize; col++)
                {
                    chosen.Collisions[row, col] = CollisionCells[row, col];
                    chosen.Types[row, col] = BehaviourCells[row, col];
                }

            if (chosen.IsGround)
            {
                PaintedGroundWarning = "Map permissions are saved from the Map Editor.";
            }
            else
            {
                PaintedGroundWarning = null;

                TornWorldFile.FloatingPlatform platform = Platforms.Count > chosen.PlatformIndex ? Platforms[chosen.PlatformIndex] : null;
                if (platform != null && _shownGrids.TryGetValue(platform.AttributeId, out ushort[] grid))
                {
                    TornWorldSurfaces.WriteBack(platform, grid, CollisionCells, BehaviourCells);
                    // Cells past the grid stay blocked, as the game treats them.
                    TornWorldSurfaces.Fill(chosen, platform, grid);

                    _editedGrids.Add(platform.AttributeId);
                    Raise(nameof(HasUnsavedChanges));
                }
            }

            Raise(nameof(PaintedGroundWarning));
            SayWhatIsUnsaved();
            BuildCollisionFromSurfaces();
            RebuildOverlay();
            SceneShown?.Invoke();
            Recorded();
        }

        public string PaintedGroundWarning { get; private set; }

        private void BuildCollisionFromSurfaces()
        {
            MapCollisionGrid walkable = new MapCollisionGrid();
            foreach (TornWorldSurfaces.Surface surface in _surfaces.Values)
            {
                walkable.Add(surface.PatchX, surface.PatchY, surface.Collisions);
                walkable.AddTypes(surface.PatchX, surface.PatchY, surface.Types);
            }
            Collision = walkable;
        }

        private readonly Dictionary<TornWorldFile.FloatingPlatform, (int attribute, int vertical, int horizontal)> _gridShapes
            = new Dictionary<TornWorldFile.FloatingPlatform, (int, int, int)>();

        private const int MostGridTiles = 256;

        private void RememberGridShapes()
        {
            _gridShapes.Clear();
            foreach (TornWorldFile.FloatingPlatform platform in Platforms)
                _gridShapes[platform] = (platform.AttributeId, platform.TilesVertical, platform.TilesHorizontal);
        }

        /// <summary>Records an edit. False when a value was refused and put back, so the rows need redrawing.</summary>
        public bool MarkEdited()
        {
            if (_selectedFloorIndex < 0 || _selectedFloorIndex >= Floors.Count) return true;

            string refused = null;
            bool gridsChanged = false;
            foreach (TornWorldFile.FloatingPlatform platform in Platforms)
            {
                if (!_gridShapes.TryGetValue(platform, out (int attribute, int vertical, int horizontal) was))
                {
                    _gridShapes[platform] = (platform.AttributeId, platform.TilesVertical, platform.TilesHorizontal);
                    continue;
                }

                if (platform.AttributeId != was.attribute)
                {
                    int grids = _attributes.Available ? _attributes.Count : 0;
                    if (platform.AttributeId < 0 || platform.AttributeId >= grids)
                    {
                        refused = $"There is no collision grid {platform.AttributeId}.";
                        platform.AttributeId = was.attribute;
                    }
                    else gridsChanged = true;
                }

                if (platform.TilesVertical != was.vertical || platform.TilesHorizontal != was.horizontal)
                {
                    if (platform.TilesVertical < 1 || platform.TilesHorizontal < 1
                        || platform.TilesVertical > MostGridTiles || platform.TilesHorizontal > MostGridTiles)
                    {
                        refused = $"Tile counts go from 1 to {MostGridTiles}.";
                        platform.TilesVertical = was.vertical;
                        platform.TilesHorizontal = was.horizontal;
                    }
                    else
                    {
                        ushort[] grid = Grid(platform.AttributeId);
                        if (grid != null)
                        {
                            _gridCache[platform.AttributeId] = TornWorldSurfaces.ResizeGrid(grid,
                                was.vertical, was.horizontal, platform.TilesVertical, platform.TilesHorizontal);
                            _editedGrids.Add(platform.AttributeId);
                        }
                        gridsChanged = true;
                    }
                }

                _gridShapes[platform] = (platform.AttributeId, platform.TilesVertical, platform.TilesHorizontal);
            }

            // One cell ends each edit, so a refused edit changed nothing and leaves the floor as it was.
            if (refused != null)
            {
                if (gridsChanged) RefreshSurfaces();
                Status = refused;
                return false;
            }

            _editedMembers.Add(Floors[_selectedFloorIndex].Floor.DataMember);
            Raise(nameof(HasUnsavedChanges));
            SayWhatIsUnsaved();
            if (gridsChanged) RefreshSurfaces();
            Recorded();
            return true;
        }

        private void RefreshSurfaces()
        {
            _shownGrids.Clear();
            foreach (TornWorldFile.FloatingPlatform platform in Platforms)
            {
                ushort[] grid = Grid(platform.AttributeId);
                if (grid != null) _shownGrids[platform.AttributeId] = grid;
            }

            int keep = _selectedPlatformIndex;
            CommitShownFloor();
            BuildScene(ShownFloors().Select(f => f.floor).ToList());
            FloorShown?.Invoke();
            _selectedPlatformIndex = keep >= 0 && keep < SurfaceRows.Count ? keep : (SurfaceRows.Count > 0 ? 0 : -1);
            Raise(nameof(SelectedPlatformIndex));
            ShowPlatform();
        }

        private void SayWhatIsUnsaved()
        {
            int floors = _editedMembers.Count, grids = _editedGrids.Count;
            if (floors + grids == 0) { Status = $"{Floors.Count} floors."; return; }
            Status = grids == 0 ? $"{floors} changed, not saved."
                   : floors == 0 ? $"{grids} grid(s) changed, not saved."
                   : $"{floors} floor(s) and {grids} grid(s) changed, not saved.";
        }

        public bool HasUnsavedChanges =>
            _editedMembers.Count > 0 || _editedGrids.Count > 0 || _shapeChanged.Count > 0;

        // ── Undo for floors and collision grids; map models keep their own undo in the model editor. ──
        private sealed class WorldState
        {
            public byte[][] Floors;
            public Dictionary<int, ushort[]> Grids;
            public int[] EditedMembers, EditedGrids;
        }

        private readonly Stack<(WorldState Before, WorldState After)> _undoSteps = new(), _redoSteps = new();
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
            _undoSteps.Clear(); _redoSteps.Clear();
            _lastState = Floors.Count > 0 ? TakeState() : null;
            RaiseSteps();
        }

        private void RaiseSteps() { Raise(nameof(CanUndo)); Raise(nameof(CanRedo)); }

        private void Recorded()
        {
            if (_lastState == null) return;
            WorldState now = TakeState();
            if (SameState(now, _lastState)) return;
            _undoSteps.Push((_lastState, now));
            _redoSteps.Clear();
            _lastState = now;
            RaiseSteps();
        }

        public bool CanUndo => _undoSteps.Count > 0;
        public bool CanRedo => _redoSteps.Count > 0;

        public void Undo()
        {
            if (_undoSteps.Count == 0) return;
            (WorldState Before, WorldState After) step = _undoSteps.Pop();
            _redoSteps.Push(step);
            Restore(step.Before, step.After);
        }

        public void Redo()
        {
            if (_redoSteps.Count == 0) return;
            (WorldState Before, WorldState After) step = _redoSteps.Pop();
            _undoSteps.Push(step);
            Restore(step.After, step.Before);
        }

        // Shows the floor the step changed.
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

        public void SaveChanges()
        {
            if (!HasUnsavedChanges) return;

            try
            {
                CommitShownFloor();
                foreach (FloorRow row in Floors.Where(f => _editedMembers.Contains(f.Floor.DataMember)))
                {
                    _archive.Put(row.Floor.DataMember, row.Data.ToByteArray());
                }

                foreach (int id in _editedGrids)
                {
                    if (!_gridCache.TryGetValue(id, out ushort[] grid)) continue;
                    _attributes.Put(id, TornWorldSurfaces.GridToBytes(grid));
                }

                foreach (KeyValuePair<int, MapFile> kv in _shapeEdited)
                {
                    if (!_shapeChanged.Contains(kv.Key)) continue;
                    kv.Value.SaveToFileDefaultDir(kv.Key, showSuccessMessage: false);
                    AppEvents.RaiseMapSaved(this, kv.Key);
                }

                _editedMembers.Clear();
                _editedGrids.Clear();
                _shapeChanged.Clear();
                Raise(nameof(HasUnsavedChanges));
                ResetSteps();
                Status = "Saved.";
            }
            catch (Exception ex)
            {
                AppLogger.Error("DistortionWorld.SaveChanges: " + ex.Message);
                AppMessages.Error("That could not be saved. " + ex.Message, "Distortion World");
            }
        }

        private void CommitShownFloor()
        {
            if (_shownFloorIndex < 0 || _shownFloorIndex >= Floors.Count) return;

            TornWorldFile data = Floors[_shownFloorIndex].Data;
            data.Platforms.Clear(); data.Platforms.AddRange(Platforms);
            data.JumpPoints.Clear(); data.JumpPoints.AddRange(JumpPoints);
            data.CameraRegions.Clear(); data.CameraRegions.AddRange(CameraRegions);
            data.GhostProps.Clear(); data.GhostProps.AddRange(GhostProps);
            data.GhostTriggers.Clear(); data.GhostTriggers.AddRange(GhostTriggers);
        }

        public Task<bool> SaveChangesAsync()
        {
            SaveChanges();
            return Task.FromResult(!HasUnsavedChanges);
        }

        public void DiscardChanges()
        {
            if (!HasUnsavedChanges) return;
            _editedMembers.Clear();
            _editedGrids.Clear();

            _shapeChanged.Clear();
            _shapeEdited.Clear();
            MapModel.Open(null, 0, gameFamily, null);
            _gridCache.Clear();
            _shownGrids.Clear();
            _shownFloorIndex = -1;
            Floors.Clear();
            Platforms.Clear(); JumpPoints.Clear(); CameraRegions.Clear();
            GhostProps.Clear(); GhostTriggers.Clear();
            _selectedFloorIndex = -1;
            Load();
            Raise(nameof(HasUnsavedChanges));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Raise([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            Raise(name);
            return true;
        }
    }
}
