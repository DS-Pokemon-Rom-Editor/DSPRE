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
    /// <summary>Platinum's Distortion World: gravity boxes, transitions and props per floor.</summary>
    public partial class DistortionWorldViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public class FloorRow
        {
            public TornWorldMapTable.Floor Floor { get; set; }
            public TornWorldFile Data { get; set; }
            public string Label { get; set; }
        }

        private readonly ScriptNarc _archive = new ScriptNarc(DirNames.TornWorld);
        private readonly ScriptNarc _attributes = new ScriptNarc(DirNames.TornWorldAttributes);
        private TornWorldMapTable _table;
        private readonly HashSet<int> _editedMembers = new HashSet<int>();
        private readonly HashSet<int> _editedGrids = new HashSet<int>();

        private readonly Dictionary<int, ushort[]> _gridCache = new Dictionary<int, ushort[]>();
        private readonly Dictionary<int, ushort[]> _shownGrids = new Dictionary<int, ushort[]>();

        private readonly Dictionary<int, MapFile> _shapeEdited = new Dictionary<int, MapFile>();

        /// <summary>Archives read here, unpacked first by the launcher behind the busy overlay.</summary>
        public static void Unpack() => DSUtils.TryUnpackNarcs(new List<DirNames> {
            DirNames.Maps, DirNames.ExteriorBuildingModels, DirNames.BuildingTextures,
            DirNames.MapTextures, DirNames.Matrices, DirNames.AreaData, DirNames.EventFiles,
            DirNames.FieldEffectModels });

        public DistortionWorldViewModel()
        {
            try { Unpack(); }
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
                MapModel.Open(null, 0, GameFamily, null);
                return;
            }

            MapCellRow row = MapCells[_selectedMapCell];
            if (!_shapeEdited.TryGetValue(row.MapId, out MapFile map))
            {
                try { map = new MapFile(row.MapId, GameFamily, discardMoveperms: false, showMessages: false); }
                catch (Exception ex) { AppLogger.Error("DistortionWorld.OpenShape: " + ex.Message); return; }
                _shapeEdited[row.MapId] = map;
            }

            MapModel.Open(map, row.AreaId, GameFamily, $"Map {row.MapId}");
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

            string name = TilePermissions.BehaviourLabel(behaviour, GameFamily);

            HoverNote = past
                ? $"x {worldX}, y {worldY}, z {worldZ}  ·  outside"
                : $"x {worldX}, y {worldY}, z {worldZ}  ·  {(blocked ? "blocked" : "walkable")}  ·  {name}";
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
            foreach (TileBehaviour b in TilePermissions.BehavioursFor(GameFamily))
                BehaviourPainters.Add(new Painter(b.Value, b.Label));
            _behaviourPainterIndex = 0;
            Raise(nameof(BehaviourPainterIndex));
        }

        public byte BehaviourPaint =>
            _behaviourPainterIndex >= 0 && _behaviourPainterIndex < BehaviourPainters.Count
                ? BehaviourPainters[_behaviourPainterIndex].Value : (byte)0;

        public void PickCollision(byte value) => CollisionPainterIndex = TilePermissions.IsBlocked(value) ? 1 : 0;

        // A behaviour with no painter is left alone.
        public void PickBehaviour(byte value)
        {
            for (int i = 0; i < BehaviourPainters.Count; i++)
                if (BehaviourPainters[i].Value == value) { BehaviourPainterIndex = i; return; }
        }

        private string _floorNote = "";
        public string FloorNote { get => _floorNote; private set => Set(ref _floorNote, value); }

        private string _status = "";
        public string StatusText { get => _status; private set => Set(ref _status, value); }

        public bool Available => _table != null && _table.Floors.Count > 0;

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
