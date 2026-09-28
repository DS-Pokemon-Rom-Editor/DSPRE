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

namespace DSPRE.Avalonia.ViewModels.World
{
    /// <summary>Distortion World data (Platinum): gravity boxes, surface transitions and props per floor.</summary>
    public class DistortionWorldViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
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

            var row = MapCells[_selectedMapCell];
            if (!_shapeEdited.TryGetValue(row.MapId, out var map))
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

            var platform = _selectedPlatformIndex >= 0 && _selectedPlatformIndex < Platforms.Count
                ? Platforms[_selectedPlatformIndex] : null;
            if (platform == null) { HoverNote = ""; return; }

            var bounds = platform.Bounds;
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
            foreach (var b in TilePermissions.BehavioursFor(gameFamily))
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
                var labels = HeaderNames();

                foreach (var floor in _table.Floors)
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

            var row = Floors[_selectedFloorIndex];
            foreach (var platform in row.Data.Platforms) Platforms.Add(platform);
            foreach (var jump in row.Data.JumpPoints) JumpPoints.Add(jump);
            foreach (var region in row.Data.CameraRegions) CameraRegions.Add(region);
            foreach (var prop in row.Data.GhostProps) GhostProps.Add(prop);
            foreach (var trigger in row.Data.GhostTriggers) GhostTriggers.Add(trigger);

            _shownGrids.Clear();
            foreach (var platform in row.Data.Platforms)
            {
                var grid = Grid(platform.AttributeId);
                if (grid != null) _shownGrids[platform.AttributeId] = grid;
            }

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
            var floor = CurrentFloor?.Floor;
            if (Model3D == null || floor == null) return null;

            int index = 0;
            foreach (var (i, shown) in ShownFloors()) if (shown == CurrentFloor) { index = i; break; }
            if (!TryTile(index, floor, floor.OffsetX, floor.OffsetZ, out float originX, out float originZ, out _, out _, out _))
                return null;

            return Model3D.ToNormalized(originX + NsbmdGeometry.MapStride / 2f,
                                        floor.OffsetAltitude * (NsbmdGeometry.TileSize / 2f),
                                        originZ + NsbmdGeometry.MapStride / 2f);
        }

        private int IndexOfShown(FloorRow floor)
        {
            foreach (var (index, shown) in ShownFloors()) if (shown == floor) return index;
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
            _animatedKinds.Clear();
            _travelling = 0;
            _elevators.Clear();
            _elevatorTiles.Clear();
            _crossing = null;
            _playerRoll = 0f;
            _lastCamera = null;
            BuildingAnimationSet.ForgetRegistered();

            try
            {
                var cells = new List<(int gridX, int gridY, MapFile map, byte areaId, float altitude,
                                      float originX, float originZ)>();
                MapCells.Clear();

                for (int index = 0; index < floors.Count; index++)
                {
                    var floor = floors[index].Floor;
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

                    var matrix = new GameMatrix(header.matrixID);
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

                foreach (var c in cells) _cellAltitude[(c.gridX, c.gridY)] = c.altitude;
                var standing = StandingModels(floors);
                BuildWalkable(floors, cells);

                int leastX = cells.Min(c => c.gridX), leastY = cells.Min(c => c.gridY);
                var placed = cells.Select(c => (c.gridX, c.gridY, c.map, c.areaId, c.altitude,
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
            var walkable = new MapCollisionGrid();
            _surfaces.Clear();
            SurfaceRows.Clear();
            WalkableTiles = 0;
            MapTiles = 0;
            TurnedSurfaces = 0;

            for (int index = 0; index < floors.Count; index++)
            {
                var floor = floors[index].Floor;

                MapFile MapAt(int across, int down)
                {
                    foreach (var cell in maps)
                        if (cell.gridX == index * GridBand + across && cell.gridY == down) return cell.map;
                    return null;
                }

                int wide = 1, deep = 1;
                foreach (var cell in maps)
                    if (cell.gridX >= index * GridBand && cell.gridX < (index + 1) * GridBand)
                    {
                        wide = Math.Max(wide, cell.gridX - index * GridBand + 1);
                        deep = Math.Max(deep, cell.gridY + 1);
                    }

                foreach (var surface in TornWorldSurfaces.ForFloor(floor, floors[index].Data, MapAt, Grid,
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
            var standing = new List<(int x, int z)>();

            var chosen = _selectedPlatformIndex >= 0 && _selectedPlatformIndex < SurfaceRows.Count
                ? SurfaceRows[_selectedPlatformIndex].Surface : null;
            if (chosen != null && !chosen.IsGround)
                for (int row = 0; row < MapFile.mapSize; row++)
                    for (int col = 0; col < MapFile.mapSize; col++)
                        if (chosen.CanWalk(col, row)) standing.Add(chosen.WalkTile(col, row));

            if (standing.Count == 0)
            foreach (var surface in _surfaces.Values)
            {
                if (!surface.IsGround) continue;
                for (int row = 0; row < MapFile.mapSize; row++)
                    for (int col = 0; col < MapFile.mapSize; col++)
                        if (surface.CanWalk(col, row)) standing.Add(surface.WalkTile(col, row));
            }
            if (standing.Count == 0)
                foreach (var surface in _surfaces.Values)
                    for (int row = 0; row < MapFile.mapSize; row++)
                        for (int col = 0; col < MapFile.mapSize; col++)
                            if (surface.CanWalk(col, row)) standing.Add(surface.WalkTile(col, row));

            if (standing.Count == 0) return null;

            double midX = standing.Average(t => t.x), midZ = standing.Average(t => t.z);
            (int x, int z) best = standing[0];
            double nearest = double.MaxValue;
            foreach (var tile in standing)
            {
                double dx = tile.x - midX, dz = tile.z - midZ;
                double away = dx * dx + dz * dz;
                if (away < nearest) { nearest = away; best = tile; }
            }
            return best;
        }

        public (float pitch, float yaw, int steps)? CameraAt(int tileX, int tileZ, MoveFacing facing)
        {
            if (!TrySurface(tileX, tileZ, out var surface, out int col, out int row)) return null;

            FloorRow shown = null;
            foreach (var (at, floor) in ShownFloors()) if (at == surface.FloorIndex) { shown = floor; break; }
            if (shown == null) return null;

            var (worldX, worldY, worldZ) = surface.WorldAt(col, row);

            int looking = Direction(facing);

            TornWorldFile.CameraRegion found = null;
            foreach (var region in shown.Data.CameraRegions)
                if (region.PlayerDirection == looking && region.Bounds.Contains(worldX, worldY, worldZ))
                    found = region;

            if (found != null)
            {
                _lastCamera = (found.PitchDegrees, found.YawDegrees, Math.Max(1, found.TransitionSteps));
                return _lastCamera;
            }

            return _lastCamera ?? (BaseCamera.PitchDegrees, BaseCamera.YawDegrees, 16);
        }

        public void SomebodyStoodOn(int tileX, int tileZ, int frame)
        {
            if (_elevatorTiles.TryGetValue((tileX, tileZ), out int modelId)
                && _elevators.TryGetValue(modelId, out var travel)) travel.Start(frame);

            TakeGravityPoint(tileX, tileZ);
        }

        public float RollAt(int tileX, int tileZ)
        {
            if (!TrySurface(tileX, tileZ, out var surface, out _, out _)) return 0f;

            if (_crossing != null && CurrentFrame != null
                && tileX == _crossing.ToTileX && tileZ == _crossing.ToTileZ)
            {
                float through = _crossing.Through(CurrentFrame());
                if (through >= 0f && through < 1f)
                    return Drawn(_crossing.FromRoll + (_crossing.ToRoll - _crossing.FromRoll) * through);
            }

            if (surface.IsGround || surface.Kind == TornWorldFile.PlatformKind.Floor) return 0f;

            if (_playerRoll != 0f) return Drawn(_playerRoll);

            switch (surface.Kind)
            {
                case TornWorldFile.PlatformKind.WestWall: return Drawn(90f);
                case TornWorldFile.PlatformKind.EastWall: return Drawn(270f);
                default: return Drawn(180f);
            }
        }

        private static float Drawn(float turn) => -turn;

        private float _playerRoll;

        private (float pitch, float yaw, int steps)? _lastCamera;

        private static readonly TornWorldFile.CameraRegion BaseCamera = new TornWorldFile.CameraRegion();

        private sealed class Crossing
        {
            public int FromTileX, FromTileZ, ToTileX, ToTileZ;
            public float FromRoll, ToRoll;
            public int StartFrame, Frames;

            public float Through(int frame) => (frame - StartFrame) / (float)Frames;
        }

        private Crossing _crossing;

        public Action<int, int, MoveFacing> PutPlayerOn;

        public string LastGravityChange { get; private set; }

        private void TakeGravityPoint(int tileX, int tileZ)
        {
            LastGravityChange = null;
            if (PutPlayerOn == null) return;
            if (!TrySurface(tileX, tileZ, out var surface, out int col, out int row)) return;

            FloorRow shown = null;
            foreach (var (at, floor) in ShownFloors()) if (at == surface.FloorIndex) { shown = floor; break; }
            if (shown == null) return;

            var (worldX, worldY, worldZ) = surface.WorldAt(col, row);

            foreach (var point in shown.Data.JumpPoints)
            {
                if (!point.Bounds.Contains(worldX, worldY, worldZ)) continue;

                int toX = worldX + point.DisplacementX;
                int toY = worldY + point.DisplacementY;
                int toZ = worldZ + point.DisplacementZ;

                foreach (var landing in _surfaces.Values)
                {
                    if (landing.FloorIndex != surface.FloorIndex) continue;
                    if (landing == surface) continue;
                    if ((int)landing.Kind != point.TargetKind) continue;
                    if (point.TargetPlatformIndex >= 0 && !landing.IsGround
                        && landing.PlatformIndex != point.TargetPlatformIndex) continue;
                    if (!landing.TryTileFor(toX, toY, toZ, out int toCol, out int toRow)) continue;
                    if (!landing.CanWalk(toCol, toRow)) continue;

                    var (walkX, walkZ) = landing.WalkTile(toCol, toRow);

                    float wasRoll = _playerRoll;
                    _playerRoll = landing.IsGround || landing.Kind == TornWorldFile.PlatformKind.Floor
                        ? 0f
                        : (_playerRoll + point.SpriteRotationAngle) % 360f;

                    _crossing = new Crossing
                    {
                        FromTileX = tileX,
                        FromTileZ = tileZ,
                        ToTileX = walkX,
                        ToTileZ = walkZ,
                        FromRoll = wasRoll,
                        ToRoll = _playerRoll,
                        StartFrame = CurrentFrame?.Invoke() ?? 0,
                        Frames = Math.Max(1, (int)point.MovementSteps),
                    };

                    PutPlayerOn(walkX, walkZ, Facing(point.FinalFacingDirection));
                    LastGravityChange = $"Gravity: {Name(landing.Kind)}";
                    return;
                }
            }
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
            var scene = Model3D;
            if (scene == null || !TrySurface(tileX, tileZ, out var surface, out int col, out int row))
                return (0f, 0f, 0f);

            float acrossCol = tileX - (float)Math.Floor(tileX);
            float acrossRow = tileZ - (float)Math.Floor(tileZ);

            var (x0, y0, z0) = surface.WorldAt(col, row);
            var (x1, y1, z1) = surface.WorldAt(col + 1, row);
            var (x2, y2, z2) = surface.WorldAt(col, row + 1);

            float worldX = x0 + (x1 - x0) * acrossCol + (x2 - x0) * acrossRow;
            float worldY = y0 + (y1 - y0) * acrossCol + (y2 - y0) * acrossRow;
            float worldZ = z0 + (z1 - z0) * acrossCol + (z2 - z0) * acrossRow;

            if (_crossing != null && CurrentFrame != null
                && (int)Math.Floor(tileX) == _crossing.ToTileX && (int)Math.Floor(tileZ) == _crossing.ToTileZ)
            {
                float through = _crossing.Through(CurrentFrame());
                if (through >= 1f) _crossing = null;
                else if (through >= 0f && TrySurface(_crossing.FromTileX, _crossing.FromTileZ, out var was,
                                                    out int wasCol, out int wasRow))
                {
                    var (fx, fy, fz) = was.WorldAt(wasCol, wasRow);
                    worldX = fx + (worldX - fx) * through;
                    worldY = fy + (worldY - fy) * through;
                    worldZ = fz + (worldZ - fz) * through;
                }
            }

            float unit = NsbmdGeometry.TileSize / 16f;
            float rideX = 0f, rideY = 0f, rideZ = 0f;
            if (_elevatorTiles.TryGetValue(((int)Math.Floor(tileX), (int)Math.Floor(tileZ)), out int riding)
                && _elevators.TryGetValue(riding, out var carriage) && carriage.Running && CurrentFrame != null)
            {
                var (mx, my, mz) = carriage.At(CurrentFrame());
                rideX = mx * unit; rideY = my * unit; rideZ = mz * unit;
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

            return scene.ToNormalized(rawX + rideX, rawY + rideY, rawZ + rideZ);
        }

        private List<(int cellX, int cellY, PlacedBuilding placed)> StandingModels(IReadOnlyList<FloorRow> floors)
        {
            var standing = new List<(int, int, PlacedBuilding)>();

            var tables = TornWorldCodeTables.Read(out string why);
            if (tables == null)
            {
                if (why != null) AppLogger.Error("DistortionWorld.StandingModels: " + why);
                return standing;
            }

            for (int index = 0; index < floors.Count; index++)
            {
                uint header = (uint)floors[index].Floor.HeaderId;
                foreach (var platform in tables.PlatformsOn(header))
                    Place(standing, tables, floors[index].Floor, index, platform.PropKind,
                          platform.TileX, platform.TileY, platform.TileZ,
                          platform.IsElevator ? tables.PathAt(platform.ElevatorPathIndex) : null);
                foreach (var prop in tables.PropsOn(header))
                    Place(standing, tables, floors[index].Floor, index, prop.PropKind,
                          prop.TileX, prop.TileY, prop.TileZ, null);
            }

            StandingCount = standing.Count;
            return standing;
        }

        private void Place(List<(int, int, PlacedBuilding)> standing, TornWorldCodeTables.Tables tables,
            TornWorldMapTable.Floor floor, int floorIndex, int propKind, int tileX, int tileY, int tileZ,
            TornWorldCodeTables.ElevatorPath path)
        {
            var model = PropModel(tables.ModelFor(propKind));
            if (model == null) return;
            if (!TryTile(floorIndex, floor, tileX, tileZ, out _, out _, out int col, out int row, out float altitude)) return;

            int modelId = path != null && path.Frames > 0 ? TravellingId() : -1 - propKind;
            RegisterMotion(tables, propKind, modelId, path);

            var (offX, offY, offZ) = tables.OffsetFor(propKind);
            float unit = NsbmdGeometry.TileSize / 16f;

            float x = (col + 0.5f) * NsbmdGeometry.TileSize - NsbmdGeometry.MapStride / 2f + offX * unit;
            float z = (row + 0.5f) * NsbmdGeometry.TileSize - NsbmdGeometry.MapStride / 2f + offZ * unit;
            float y = HeightOf(tileY) - altitude + offY * unit;

            float scale = (model.modelScale == 0 ? 1f : model.modelScale) / 64f;
            var (kindScale, _, _) = tables.ScaleFor(propKind);
            if (kindScale > 0f) scale *= kindScale;
            var transform = Mat4.Multiply(Mat4.Scale(scale, scale, scale),
                                          Mat4.Translate(x / scale, y / scale, z / scale));

            int gridX = floorIndex * GridBand + (tileX - floor.OffsetX) / MapFile.mapSize;
            int gridY = (tileZ - floor.OffsetZ) / MapFile.mapSize;

            if (path != null && path.Frames > 0)
                _elevatorTiles[(gridX * MapFile.mapSize + col, gridY * MapFile.mapSize + row)] = modelId;

            standing.Add((gridX, gridY, new PlacedBuilding
            {
                Model = model,
                Transform = transform,
                ModelId = modelId,
                TileX = gridX * MapFile.mapSize + col,
                TileZ = gridY * MapFile.mapSize + row,
            }));
        }

        private int _travelling;
        private int TravellingId() => -1000 - _travelling++;

        private readonly Dictionary<int, BuildingAnimationSet.TravelAnimation> _elevators
            = new Dictionary<int, BuildingAnimationSet.TravelAnimation>();
        private readonly Dictionary<(int x, int z), int> _elevatorTiles = new Dictionary<(int, int), int>();

        public Func<int> CurrentFrame;

        private readonly HashSet<int> _animatedKinds = new HashSet<int>();

        private void RegisterMotion(TornWorldCodeTables.Tables tables, int propKind, int modelId,
            TornWorldCodeTables.ElevatorPath path)
        {
            bool perInstance = modelId <= -1000;
            if (!perInstance && !_animatedKinds.Add(propKind)) return;

            int member = tables.AnimationFor(propKind);

            if (member < 0 && tables.HoverOffsets.Length > 0)
            {
                var hover = new BuildingAnimationSet.HoverAnimation(tables.HoverOffsets, tables.HoverStep);
                BuildingAnimationSet.WholeModelMotion motion = hover;

                if (path != null && path.Frames > 0)
                {
                    var travel = new BuildingAnimationSet.TravelAnimation(
                        path.FinalX * 16f, path.FinalY * 16f, path.FinalZ * 16f, path.Frames);
                    _elevators[modelId] = travel;
                    motion = new BuildingAnimationSet.CombinedMotion(hover, travel);
                }

                BuildingAnimationSet.Register(modelId, motion: motion);
            }

            if (member < 0) return;

            byte[] raw = FieldEffectMember(member);
            if (raw == null || raw.Length < 4) return;

            string magic = System.Text.Encoding.ASCII.GetString(raw, 0, 4);
            try
            {
                if (magic == "BTA0") BuildingAnimationSet.Register(modelId, scrolling: TextureSrtAnimation.Load(raw));
                else if (magic == "BCA0") BuildingAnimationSet.Register(modelId, joint: JointAnimation.Load(raw));
            }
            catch (Exception ex)
            {
                AppLogger.Error($"DistortionWorld.RegisterMotion({propKind}): " + ex.Message);
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
            if (_propModels.TryGetValue(member, out var cached)) return cached;

            NSBMDModel model = null;
            try
            {
                if (gameDirs.ContainsKey(DirNames.fieldEffectModels))
                {
                    string path = System.IO.Path.Combine(gameDirs[DirNames.fieldEffectModels].unpackedDir, member.ToString("D4"));
                    if (System.IO.File.Exists(path))
                    {
                        using var file = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read);
                        var nsbmd = NSBMDLoader.LoadNSBMD(file);
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
            var mesh = new List<float>();
            if (Model3D != null && _showGravity)
                foreach (var (floorIndex, floor) in ShownFloors())
                {
                    var chosen = _selectedPlatformIndex >= 0 && _selectedPlatformIndex < SurfaceRows.Count
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
            var grid = Grid(platform.AttributeId);
            if (grid == null) return;

            int kind = (int)platform.Kind;
            var (r, g, b) = kind >= 0 && kind < KindColours.Length ? KindColours[kind] : (0.6f, 0.6f, 0.6f);
            if (selected) { r = Math.Min(1f, r * 1.6f); g = Math.Min(1f, g * 1.6f); b = Math.Min(1f, b * 1.6f); }

            var bounds = platform.Bounds;
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
                        var special = (tile & 0xFF) != 0;
                        AddQuad(mesh, x0 + insetX, x0 + sizeX - insetX, z0 + insetZ, z0 + sizeZ - insetZ, height,
                            special ? 0.89f : r, special ? 0.76f : g, special ? 0.33f : b);
                    }
        }

        private void AddQuad(List<float> v, float x0, float x1, float z0, float z1, float y, float r, float g, float b)
        {
            void Vertex(float x, float z)
            {
                var (nx, ny, nz) = Model3D.ToNormalized(x, y, z);
                v.Add(nx); v.Add(ny); v.Add(nz); v.Add(0); v.Add(0); v.Add(r); v.Add(g); v.Add(b);
            }
            Vertex(x0, z0); Vertex(x1, z0); Vertex(x1, z1);
            Vertex(x0, z0); Vertex(x1, z1); Vertex(x0, z1);
        }
        private ushort[] Grid(int attributeId)
        {
            if (_gridCache.TryGetValue(attributeId, out var cached)) return cached;
            if (!_attributes.Available) return null;

            try
            {
                byte[] raw = _attributes.Get(attributeId);
                if (raw == null) return null;

                var grid = new ushort[raw.Length / 2];
                for (int i = 0; i < grid.Length; i++) grid[i] = (ushort)(raw[i * 2] | (raw[i * 2 + 1] << 8));
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

            var chosen = _selectedPlatformIndex >= 0 && _selectedPlatformIndex < SurfaceRows.Count
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
                var platform = chosen.Bounds;
                bool upright = chosen.Kind == TornWorldFile.PlatformKind.WestWall
                            || chosen.Kind == TornWorldFile.PlatformKind.EastWall;
                UsedRows = Math.Min(PaintedGridSize, (upright ? platform.SizeY : platform.SizeX) + 1);
                UsedColumns = Math.Min(PaintedGridSize, platform.SizeZ + 1);
                PlatformNote = $"{UsedRows} down ({(upright ? "height" : "x")}) x {UsedColumns} across (z)";
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
                    return "West wall: rows run up, columns along Z.";
                case TornWorldFile.PlatformKind.EastWall:
                    return "East wall: rows run down, columns along Z.";
                case TornWorldFile.PlatformKind.Ceiling:
                    return "Ceiling: rows run along -X, columns along Z.";
                default:
                    return "Floor: rows run along X, columns along Z.";
            }
        }

        public void ApplyPaintedGrid()
        {
            var chosen = _selectedPlatformIndex >= 0 && _selectedPlatformIndex < SurfaceRows.Count
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

                var platform = Platforms.Count > chosen.PlatformIndex ? Platforms[chosen.PlatformIndex] : null;
                if (platform != null && _shownGrids.TryGetValue(platform.AttributeId, out var grid))
                {
                    int rows = Math.Min(PaintedGridSize, platform.TilesVertical);
                    int cols = Math.Min(PaintedGridSize, platform.TilesHorizontal);
                    for (int row = 0; row < rows; row++)
                        for (int col = 0; col < cols; col++)
                        {
                            int index = col + row * platform.TilesVertical;
                            if (index >= grid.Length) continue;
                            grid[index] = (ushort)((CollisionCells[row, col] != 0 ? 0x8000 : 0) | BehaviourCells[row, col]);
                        }

                    _editedGrids.Add(platform.AttributeId);
                    Raise(nameof(HasUnsavedChanges));
                }
            }

            Raise(nameof(PaintedGroundWarning));
            SayWhatIsUnsaved();
            BuildCollisionFromSurfaces();
            RebuildOverlay();
            SceneShown?.Invoke();
        }

        public string PaintedGroundWarning { get; private set; }

        private void BuildCollisionFromSurfaces()
        {
            var walkable = new MapCollisionGrid();
            foreach (var surface in _surfaces.Values)
            {
                walkable.Add(surface.PatchX, surface.PatchY, surface.Collisions);
                walkable.AddTypes(surface.PatchX, surface.PatchY, surface.Types);
            }
            Collision = walkable;
        }

        public void MarkEdited()
        {
            if (_selectedFloorIndex < 0 || _selectedFloorIndex >= Floors.Count) return;
            _editedMembers.Add(Floors[_selectedFloorIndex].Floor.DataMember);
            Raise(nameof(HasUnsavedChanges));
            SayWhatIsUnsaved();
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
                foreach (var row in Floors.Where(f => _editedMembers.Contains(f.Floor.DataMember)))
                {
                    _archive.Put(row.Floor.DataMember, row.Data.ToByteArray());
                }

                foreach (int id in _editedGrids)
                {
                    if (!_gridCache.TryGetValue(id, out var grid)) continue;
                    var raw = new byte[grid.Length * 2];
                    for (int i = 0; i < grid.Length; i++)
                    {
                        raw[i * 2] = (byte)(grid[i] & 0xFF);
                        raw[i * 2 + 1] = (byte)(grid[i] >> 8);
                    }
                    _attributes.Put(id, raw);
                }

                foreach (var kv in _shapeEdited)
                {
                    if (!_shapeChanged.Contains(kv.Key)) continue;
                    kv.Value.SaveToFileDefaultDir(kv.Key, showSuccessMessage: false);
                    AppEvents.RaiseMapSaved(this, kv.Key);
                }

                _editedMembers.Clear();
                _editedGrids.Clear();
                _shapeChanged.Clear();
                Raise(nameof(HasUnsavedChanges));
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

            var data = Floors[_shownFloorIndex].Data;
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
