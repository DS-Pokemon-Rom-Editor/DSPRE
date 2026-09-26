using DSPRE.Avalonia.Gl;
using DSPRE.Models;
using DSPRE.ROMFiles;
using DSPRE.Resources;
using LibNDSFormats.NSBTX;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using static DSPRE.RomInfo;
using AvBitmap = global::Avalonia.Media.Imaging.Bitmap;

namespace DSPRE.Avalonia.ViewModels.World
{
    /// <summary>Tile painting for a map model, PDSMS style.</summary>
    public class MapTilesViewModel : INotifyPropertyChanged
    {
        public sealed class TileRow : INotifyPropertyChanged
        {
            public int Index { get; set; }
            public string Name { get; set; }
            public string Pictures { get; set; }
            public string Size { get; set; }
            public global::Avalonia.Media.IBrush Swatch { get; set; }

            public AvBitmap Picture { get; set; }

            // Drawn only when the tooltip first asks for it.
            internal Func<AvBitmap> MakeBig;
            private AvBitmap _big;
            public AvBitmap Big => _big ??= MakeBig?.Invoke();

            private int _uses;
            public int Uses
            {
                get => _uses;
                set { if (_uses == value) return; _uses = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Uses))); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UsesText))); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Fade))); }
            }
            public double Fade => _uses == 0 ? 0.55 : 1;
            public string UsesText => _uses == 0 ? "not on this map" : _uses == 1 ? "placed once" : $"placed {_uses} times";

            public string Label => $"{Name}  ·  {Pictures}{Size}";
            public override string ToString() => Label;
            public event PropertyChangedEventHandler PropertyChanged;
        }

        public sealed class ToolRow
        {
            public TilePainter.Tool Tool { get; set; }
            public string Glyph { get; set; }
            public string Tip { get; set; }
        }

        public sealed class LayerRow : INotifyPropertyChanged
        {
            public int Index { get; set; }
            public string Name => $"Layer {Index + 1}";

            private bool _visible = true;
            public bool Visible
            {
                get => _visible;
                set { if (_visible == value) return; _visible = value; Changed(); VisibilityChanged?.Invoke(this, EventArgs.Empty); }
            }

            private int _painted;
            public int Painted { get => _painted; set { if (_painted == value) return; _painted = value; Changed(); Changed(nameof(Said)); } }
            public string Said => _painted == 0 ? "" : _painted.ToString();

            public event EventHandler VisibilityChanged;
            public event PropertyChangedEventHandler PropertyChanged;
            private void Changed([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        }

        public sealed class HeightRow
        {
            public int Value { get; set; }
            public string Said => Value > 0 ? "+" + Value : Value.ToString();
            public global::Avalonia.Media.IBrush Swatch { get; set; }
        }

        public sealed class SmartRow
        {
            public int Index { get; set; }
            public AvBitmap Picture { get; set; }
        }

        public sealed class SlotRow
        {
            public int Slot { get; set; }
            public AvBitmap Picture { get; set; }
            public string Tip { get; set; }
        }

        private MapFile _map;
        private byte _areaId;
        private GameFamilies _family;
        private MapTileset _set;
        private readonly TilePainter _painter = new TilePainter();
        private readonly List<byte[]> _before = new List<byte[]>();

        private Dictionary<string, (byte[] rgba, int w, int h)> _pictures =
            new Dictionary<string, (byte[] rgba, int w, int h)>(StringComparer.Ordinal);
        private readonly Dictionary<(int tile, int turn), TileGridControl.Look?> _fromAbove = new Dictionary<(int, int), TileGridControl.Look?>();
        private readonly Dictionary<int, AvBitmap> _thumbs = new Dictionary<int, AvBitmap>();

        public NsbmdRenderModel Model3D { get; private set; }

        public NsbmdRenderModel Tile3D { get; private set; }

        public ObservableCollection<TileRow> Tiles { get; } = new ObservableCollection<TileRow>();

        /// <summary>The tile list as shown: tiles this map uses first, narrowed by the search.</summary>
        public ObservableCollection<TileRow> ShownTiles { get; } = new ObservableCollection<TileRow>();

        private string _tileSearch = "";
        public string TileSearch
        {
            get => _tileSearch;
            set { if (_tileSearch == (value ?? "")) return; _tileSearch = value ?? ""; Raise(); ShowTileList(); }
        }

        public TileRow ShownTile
        {
            get => Brush >= 0 && Brush < Tiles.Count ? Tiles[Brush] : null;
            set { if (value != null) Brush = value.Index; }
        }
        public ObservableCollection<LayerRow> Layers { get; } = new ObservableCollection<LayerRow>();
        public ObservableCollection<HeightRow> HeightPalette { get; } = new ObservableCollection<HeightRow>();
        public ObservableCollection<SmartRow> SmartDrawings { get; } = new ObservableCollection<SmartRow>();
        public ObservableCollection<SlotRow> Slots { get; } = new ObservableCollection<SlotRow>();

        public IReadOnlyList<ToolRow> Tools { get; } = new[]
        {
            new ToolRow { Tool = TilePainter.Tool.Paint, Glyph = "✎", Tip = "Paint (right: pick, middle: fill) (B)" },
            new ToolRow { Tool = TilePainter.Tool.Clear, Glyph = "⌫", Tip = "Erase (middle: erase area) (E)" },
            new ToolRow { Tool = TilePainter.Tool.Smart, Glyph = "▦", Tip = "Smart fill (S)" },
            new ToolRow { Tool = TilePainter.Tool.SmartInverted, Glyph = "▣", Tip = "Inverted smart fill (Shift+S)" },
            new ToolRow { Tool = TilePainter.Tool.Bucket, Glyph = "◧", Tip = "Fill (G)" },
            new ToolRow { Tool = TilePainter.Tool.Picker, Glyph = "⌖", Tip = "Picker (I)" },
            new ToolRow { Tool = TilePainter.Tool.Line, Glyph = "╱", Tip = "Line (L)" },
            new ToolRow { Tool = TilePainter.Tool.Rectangle, Glyph = "▭", Tip = "Rectangle (U)" },
            new ToolRow { Tool = TilePainter.Tool.Ellipse, Glyph = "◯", Tip = "Ellipse (O)" },
            new ToolRow { Tool = TilePainter.Tool.Select, Glyph = "⬚", Tip = "Select (M)" },
            new ToolRow { Tool = TilePainter.Tool.Lasso, Glyph = "➰", Tip = "Lasso (Q)" },
            new ToolRow { Tool = TilePainter.Tool.Wand, Glyph = "✦", Tip = "Magic wand (W)" },
        };

        public MapTilesViewModel()
        {
            for (int i = 0; i < TileGrid.Layers; i++)
            {
                var row = new LayerRow { Index = i };
                row.VisibilityChanged += (_, _) => GridChanged?.Invoke(this, EventArgs.Empty);
                Layers.Add(row);
            }
            for (int h = TileGrid.HighestStep; h >= TileGrid.LowestStep; h--)
                HeightPalette.Add(new HeightRow
                {
                    Value = h,
                    Swatch = new global::Avalonia.Media.SolidColorBrush(TileGridControl.HeightColour(h)),
                });
        }

        public int Brush
        {
            get => _painter.Brush;
            set
            {
                if (_painter.Brush == value) return;
                _painter.Brush = value;
                Raise();
                Raise(nameof(ShownTile));
                Describe();
                ShowTile();
                ShowChosen();
            }
        }

        public int Turn
        {
            get => _painter.Turn;
            set
            {
                byte turn = (byte)(((value % 4) + 4) % 4);
                if (_painter.Turn == turn) return;
                _painter.Turn = turn;
                Raise();
                Describe();
                ShowTile();
            }
        }

        public int ToolIndex
        {
            get => Array.FindIndex(Tools.ToArray(), t => t.Tool == _painter.Current);
            set
            {
                if (value < 0 || value >= Tools.Count || Tools[value].Tool == _painter.Current) return;
                _painter.Current = Tools[value].Tool;
                Raise();
            }
        }

        public int ActiveLayer
        {
            get => _painter.Layer;
            set
            {
                if (value < 0 || value >= TileGrid.Layers || value == _painter.Layer) return;
                _painter.Layer = value;
                Raise();
                GridChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool HeightsAllLayers
        {
            get => _painter.HeightsAllLayers;
            set { if (_painter.HeightsAllLayers == value) return; _painter.HeightsAllLayers = value; Raise(); }
        }

        private bool _showBuildings = true;
        public bool ShowBuildings
        {
            get => _showBuildings;
            set { if (Set(ref _showBuildings, value)) GridChanged?.Invoke(this, EventArgs.Empty); }
        }

        private bool _dimOtherLayers;
        public bool DimOtherLayers
        {
            get => _dimOtherLayers;
            set { if (Set(ref _dimOtherLayers, value)) GridChanged?.Invoke(this, EventArgs.Empty); }
        }

        public bool ShowHeights
        {
            get => _painter.Heights;
            set { if (_painter.Heights == value) return; _painter.Heights = value; Raise(); GridChanged?.Invoke(this, EventArgs.Empty); }
        }

        /// <summary>0 paints the chosen height, 1 raises and 2 lowers by one step.</summary>
        public int HeightWayIndex
        {
            get => (int)_painter.HeightChange;
            set { if ((int)_painter.HeightChange == value || value < 0) return; _painter.HeightChange = (TilePainter.HeightWay)value; Raise(); Raise(nameof(PaintsHeight)); }
        }

        public bool PaintsHeight => _painter.HeightChange == TilePainter.HeightWay.Set;

        public int HeightBrush
        {
            get => _painter.HeightBrush;
            set
            {
                value = Math.Clamp(value, TileGrid.LowestStep, TileGrid.HighestStep);
                if (_painter.HeightBrush == value) return;
                _painter.HeightBrush = value;
                Raise();
                Raise(nameof(HeightIndex));
            }
        }

        public int HeightIndex
        {
            get => TileGrid.HighestStep - HeightBrush;
            set { if (value >= 0) HeightBrush = TileGrid.HighestStep - value; }
        }

        public bool SmartTools
        {
            get => _painter.SmartTools;
            set { if (_painter.SmartTools == value) return; _painter.SmartTools = value; Raise(); }
        }

        public int SmartIndex
        {
            get => _set == null || _set.SmartDrawings.Count == 0 ? -1 : _painter.SmartIndex;
            set
            {
                if (value < 0 || _set == null || value >= _set.SmartDrawings.Count || value == _painter.SmartIndex) return;
                _painter.SmartIndex = value;
                Raise();
                FillSlots();
            }
        }

        public bool HasSmartDrawing => _set != null && _set.SmartDrawings.Count > 0;

        private string _note = "";
        public string Note { get => _note; private set => Set(ref _note, value); }

        private string _under = "";
        public string Under { get => _under; private set => Set(ref _under, value); }

        private string _warning;
        public string Warning { get => _warning; private set => Set(ref _warning, value); }

        private bool _dirty;
        public bool Dirty { get => _dirty; private set => Set(ref _dirty, value); }

        private bool _alsoTerrain = true;
        public bool AlsoTerrain { get => _alsoTerrain; set => Set(ref _alsoTerrain, value); }

        // Ramps marked for the next Apply, each a run of squares with its own kind and fill.
        private readonly List<TileRamps.Run> _rampRuns = new List<TileRamps.Run>();
        // Every marked square, for the code that only needs to know a square is a ramp.
        private readonly HashSet<(int x, int z)> _ramps = new HashSet<(int, int)>();
        public int RampCount => _ramps.Count;

        public IReadOnlyList<string> RampKinds { get; } = new[] { "Slope", "Stairs" };
        private int _rampKindIndex;
        public int RampKindIndex { get => _rampKindIndex; set => Set(ref _rampKindIndex, Math.Max(0, value)); }

        // Ramp fills: the ground's own picture, the brush tile's picture, the brush tile fitted over the ramp, or a tileset picture.
        private const int FillGround = 0, FillBrushPicture = 1, FillBrushTile = 2, FirstPicture = 3;
        public ObservableCollection<string> RampFills { get; } = new ObservableCollection<string>
            { "Ground's own picture", "Selected tile's picture", "Selected tile, fitted as the ramp" };
        private int _rampFillIndex;
        public int RampFillIndex { get => _rampFillIndex; set => Set(ref _rampFillIndex, Math.Max(0, value)); }

        private void FillRampFills()
        {
            while (RampFills.Count > FirstPicture) RampFills.RemoveAt(RampFills.Count - 1);
            if (_set != null)
                foreach (string p in _set.Tiles.SelectMany(t => t.Faces).Where(f => f.Look != null).Select(f => f.Picture)
                                               .Where(p => !string.IsNullOrEmpty(p)).Distinct().OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
                    RampFills.Add(p);
            if (_rampFillIndex >= RampFills.Count) RampFillIndex = FillGround;
        }

        private MapTileset.Tile RampTemplate()
            => _rampFillIndex == FillBrushTile && _set != null && Brush >= 0 && Brush < _set.Tiles.Count ? _set.Tiles[Brush] : null;

        private MapTileset.Face RampFill()
        {
            if (_set == null || _rampFillIndex == FillGround || _rampFillIndex == FillBrushTile) return null;
            if (_rampFillIndex == FillBrushPicture)
            {
                if (Brush < 0 || Brush >= _set.Tiles.Count) return null;
                var tile = _set.Tiles[Brush];
                return tile.Faces.FirstOrDefault(f => f.Look != null && f.Picture == tile.MainPicture) ?? tile.Faces.FirstOrDefault(f => f.Look != null);
            }
            string name = RampFills[_rampFillIndex];
            return _set.Tiles.SelectMany(t => t.Faces).FirstOrDefault(f => f.Look != null && f.Picture == name);
        }

        private List<(int x, int z)> SelectedSquares()
        {
            var mask = _painter.Selection;
            var picked = new List<(int, int)>();
            if (mask == null) return picked;
            for (int z = 0; z < TileGrid.Across; z++)
                for (int x = 0; x < TileGrid.Across; x++)
                    if (mask[x, z]) picked.Add((x, z));
            return picked;
        }

        private void RampsChanged()
        {
            _rampRuns.RemoveAll(r => r.Squares.Count == 0);
            _ramps.Clear();
            foreach (var run in _rampRuns) _ramps.UnionWith(run.Squares);
            Note = _rampRuns.Count == 0 ? "No ramps."
                 : $"{_rampRuns.Count} ramp{(_rampRuns.Count > 1 ? "s" : "")} ({_ramps.Count} squares), built on Apply.";
            Raise(nameof(RampCount));
            GridChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Marks the selection as one ramp run, replacing any ramp already on those squares.</summary>
        public void MarkRamp()
        {
            var picked = SelectedSquares();
            if (picked.Count == 0) return;
            foreach (var run in _rampRuns) run.Squares.ExceptWith(picked);
            var template = RampTemplate();
            var fill = RampFill();
            if (_rampFillIndex == FillBrushTile && template == null) { Warning = "Select the slope tile to fit first."; return; }
            if (_rampFillIndex != FillGround && _rampFillIndex != FillBrushTile && fill == null) { Warning = "That picture has no look to copy; pick another fill."; return; }
            _rampRuns.Add(new TileRamps.Run
            {
                Squares = new HashSet<(int x, int z)>(picked),
                Kind = _rampKindIndex == 1 ? TileRamps.Kind.Stairs : TileRamps.Kind.Slope,
                Fill = fill,
                Template = template,
            });
            RampsChanged();
        }

        public void UnmarkRamp()
        {
            var picked = SelectedSquares();
            foreach (var run in _rampRuns) run.Squares.ExceptWith(picked);
            RampsChanged();
        }

        public IReadOnlyCollection<(int x, int z)> RampSquares => _ramps;

        // The layer each square was walked on when the map was taken apart; maps can hide surfaces under the ground.
        private int[,] _walkedLayer;

        // How far above its tile's base each square was walked, kept while the square keeps that tile.
        private float[,] _walkedAbove;
        private int[,] _walkedTile;

        private float SurfaceOf(TileGrid grid, int x, int z, int l)
        {
            var sq = grid.At(x, z, l);
            if (sq.Tile < 0 || sq.Tile >= _set.Tiles.Count) return sq.Lift;
            if (_walkedLayer != null && x >= 0 && z >= 0 && x < TileGrid.Across && z < TileGrid.Across
                && _walkedLayer[x, z] == l && _walkedTile[x, z] == sq.Tile)
                return sq.Lift + _walkedAbove[x, z];
            return sq.Lift + _set.Tiles[sq.Tile].SurfaceY;
        }

        private int WalkedLayer(TileGrid grid, int x, int z)
        {
            if (_walkedLayer == null || x < 0 || z < 0 || x >= TileGrid.Across || z >= TileGrid.Across) return -1;
            int l = _walkedLayer[x, z];
            return l >= 0 && grid.At(x, z, l).Tile >= 0 && grid.At(x, z, l).Tile < _set.Tiles.Count ? l : -1;
        }

        private void FindWalkedLayers(TileGrid grid)
        {
            _walkedLayer = null;
            _walkedAbove = null;
            _walkedTile = null;
            if (_map?.bdhc == null || !BdhcFile.TryParse(_map.bdhc, out var terrain)) return;
            float toUnits = 16f / MapTileset.TileWidth;
            _walkedLayer = new int[TileGrid.Across, TileGrid.Across];
            _walkedAbove = new float[TileGrid.Across, TileGrid.Across];
            _walkedTile = new int[TileGrid.Across, TileGrid.Across];
            for (int z = 0; z < TileGrid.Across; z++)
                for (int x = 0; x < TileGrid.Across; x++)
                {
                    _walkedLayer[x, z] = -1;
                    _walkedTile[x, z] = -1;
                    if (!terrain.TryGetHeight((x + 0.5f) * 0.25f, (z + 0.5f) * 0.25f, 0f, out float walkY)) continue;
                    float best = float.MaxValue;
                    for (int l = 0; l < TileGrid.Layers; l++)
                    {
                        var sq = grid.At(x, z, l);
                        if (sq.Tile < 0) continue;
                        var tile = _set.Tiles[sq.Tile];
                        float bottom = grid.BaseLift + sq.Lift, top = bottom + tile.Corners.DefaultIfEmpty().Max(c => c?.Y ?? 0f);
                        float walk = walkY * 64f / toUnits;
                        // The walked height has to lie within the tile; among those, the tile whose ground is nearest wins.
                        float off = walk < bottom - 1e-3f || walk > top + 1e-3f ? float.MaxValue / 2 : Math.Abs(bottom + tile.SurfaceY - walk);
                        if (off < best)
                        {
                            best = off; _walkedLayer[x, z] = l; _walkedTile[x, z] = sq.Tile;
                            _walkedAbove[x, z] = walk - bottom;
                        }
                    }
                    if (best >= float.MaxValue / 2) { _walkedLayer[x, z] = -1; _walkedTile[x, z] = -1; }
                }
        }

        private int GroundLayer(TileGrid grid, int x, int z)
        {
            int walked = WalkedLayer(grid, x, z);
            if (walked >= 0) return walked;
            for (int l = 0; l < TileGrid.Layers; l++)
            {
                int t = grid.At(x, z, l).Tile;
                if (t >= 0 && t < _set.Tiles.Count) return l;
            }
            return -1;
        }

        private bool _alsoWalls = true;
        public bool AlsoWalls { get => _alsoWalls; set => Set(ref _alsoWalls, value); }

        private TileGrid _atLoad;

        private bool HeightChanged(int x, int z)
        {
            if (_atLoad == null) return true;
            int ground = Math.Max(0, GroundLayer(_painter.Grid, x, z));
            return _painter.Grid.At(x, z, ground).Lift != _atLoad.At(x, z, ground).Lift;
        }

        private bool _alsoMovement = true;
        public bool AlsoMovement { get => _alsoMovement; set => Set(ref _alsoMovement, value); }

        public bool SetHasMovement => _set != null && _set.Tiles.Any(t => t.CollisionDefaults.Count > 0);

        public bool Ready => _set != null && _set.Tiles.Count > 0;
        public bool CanUndo => _before.Count > 0;
        public bool CanUndoPaint => _painter.CanUndo;
        public bool CanRedoPaint => _painter.CanRedo;
        public bool HasCopy => _painter.HasCopy;

        public event EventHandler Changed;
        public event EventHandler GridChanged;

        public event EventHandler PendingChanged;

        public void Open(MapFile map, byte areaId, GameFamilies family)
        {
            _map = map; _areaId = areaId; _family = family;
            _knownModel = map?.mapModelData;
            _set = null;
            _painter.Load(new TileGrid(), null);
            _before.Clear();
            _fromAbove.Clear();
            _thumbs.Clear();
            _pictures = PicturesOfTheArea();
            Tiles.Clear();
            ShownTiles.Clear();
            _tileOrder.Clear();
            SmartDrawings.Clear();
            Slots.Clear();
            Brush = -1; Turn = 0; HeightBrush = 0; ActiveLayer = 0;
            Dirty = false; Warning = null;

            Note = map?.mapModelData == null
                ? "This map has no model."
                : "";

            Rebuild();
            RaiseAll();
            Changed?.Invoke(this, EventArgs.Empty);
            GridChanged?.Invoke(this, EventArgs.Empty);
        }

        private void RaiseAll()
        {
            foreach (string n in new[] { nameof(Ready), nameof(CanUndo), nameof(CanUndoPaint), nameof(CanRedoPaint),
                                         nameof(HasCopy), nameof(SmartIndex), nameof(HasSmartDrawing), nameof(ToolIndex),
                                         nameof(ActiveLayer), nameof(ShowHeights), nameof(HeightBrush), nameof(HeightIndex),
                                         nameof(Brush), nameof(Turn), nameof(SmartTools), nameof(HasOwnPictures), nameof(SetHasMovement) })
                Raise(n);
        }

        private byte[] _knownModel;

        public void Revisit()
        {
            if (_map == null || ReferenceEquals(_map.mapModelData, _knownModel)) return;
            Rebuild();
            Changed?.Invoke(this, EventArgs.Empty);
            if (_set != null)
                Warning = "The model changed in the Shape tab. Applying this grid replaces those changes.";
            _knownModel = _map.mapModelData;
        }

        public void TakeApart()
        {
            if (_map?.mapModelData == null) return;
            _knownModel = _map.mapModelData;

            var mesh = MapMesh.Read(_map.mapModelData, out string whynot);
            if (mesh == null) { Warning = whynot; return; }
            // Squares are measured at scale 64; a few maps declare another, which would misplace every tile.
            mesh.ScaleTo(TileScale);

            Func<int, string> pictureOf = material =>
            {
                int named = mesh.PictureFor(material);
                return named >= 0 ? mesh.NameOfPicture(named) : $"material{material}";
            };
            Func<int, string> paletteOf = material =>
            {
                int named = mesh.ColoursFor(material);
                return named >= 0 ? mesh.NameOfColours(named) : "";
            };

            var set = MapTileset.FromMap(mesh, pictureOf, "this map", paletteOf);
            var left = new List<MapMesh.Face>();
            var grid = TileGrid.Of(mesh, set, pictureOf, out int unmatched, paletteOf, left);
            int learned = TileCollisions.Learn(grid, set, _map.types, _map.collisions);
            Load(set, grid);
            _ripped = grid.Clone();
            FindWalkedLayers(grid);
            _painter.GroundSurface = (x, z, l) => SurfaceOf(_painter.Grid, x, z, l);
            var cut = new List<MapMesh.Face>();
            var split = new Dictionary<MapMesh.Face, List<MapMesh.Face>>();
            MapTileset.PiecesOf(mesh, cut, split);
            _wholeFaces = TileBake.WholeFaces(mesh, cut, pictureOf, paletteOf, split);
            // Pieces with no free layer stay until painted over; cut faces come back whole, so only the map's own faces count.
            var own = new HashSet<MapMesh.Face>(mesh.Faces);
            _leftFaces = TileBake.WholeFaces(mesh, left.Where(own.Contains), pictureOf, paletteOf);

            Note = $"{_set.Tiles.Count} tiles, {grid.Painted} cells"
                 + (learned > 0 ? $", {learned} with default permissions" : "")
                 + (unmatched > 0 ? $", {left.Count} faces kept as they are (no free layer)" : "");
            Warning = null;
        }

        private TileGrid _ripped;

        // Ground faces From map cut into squares; each goes back in whole while none of its squares change.
        private List<TileBake.WholeFace> _wholeFaces;

        private List<TileBake.WholeFace> _leftFaces;

        private int PutBackUntouched(TileBake.Result baked, ICollection<(int x, int z)> ramps)
        {
            if (_wholeFaces == null || _ripped == null) return 0;
            bool Untouched(TileBake.WholeFace w) => w.Squares.All(q => !ChangedSinceRipped(q.x, q.z) && !ramps.Contains(q));
            return TileBake.PutBackWhole(baked, _wholeFaces.Where(Untouched))
                 + TileBake.PutBackAsTheyWere(baked, (_leftFaces ?? new List<TileBake.WholeFace>()).Where(Untouched));
        }

        private bool ChangedSinceRipped(int x, int z)
        {
            for (int layer = 0; layer < TileGrid.Layers; layer++)
            {
                var now = _painter.Grid.At(x, z, layer);
                var was = _ripped.At(x, z, layer);
                if (now.Tile != was.Tile || now.Turn != was.Turn || now.FromX != was.FromX || now.FromZ != was.FromZ
                    || _painter.Grid.HeightAt(x, z, layer) != _ripped.HeightAt(x, z, layer)) return true;
            }
            return false;
        }

        private void Load(MapTileset set, TileGrid grid)
        {
            _ownTiles.Clear();
            if (set != null && set.PictureFiles.Count > 0)
                for (int i = 0; i < set.Tiles.Count; i++) _ownTiles.Add(i);
            _ripped = null;
            _wholeFaces = null;
            _leftFaces = null;
            _walkedLayer = null;
            // A search typed for the last tileset would hide the new one's tiles.
            _tileSearch = "";
            Raise(nameof(TileSearch));
            _ramps.Clear();
            _rampRuns.Clear();
            _tileSource.Clear();
            CollisionUnsaved = false;
            _atLoad = grid?.Clone();
            _set = set;
            _painter.Load(grid, set);
            _fromAbove.Clear();
            _thumbs.Clear();
            _pictures = PicturesOfTheArea();
            foreach (var kv in PicturesOfTheSet(set)) _pictures.TryAdd(kv.Key, kv.Value);

            FillList();
            FillSmart();
            ShowTile();
            // The Tile tab describes the chosen tile; with a new tileset that is a different tile, even at the same index.
            CollisionCells.Clear();
            ShowChosen();
            CountLayers();
            RaiseAll();
            Changed?.Invoke(this, EventArgs.Empty);
            GridChanged?.Invoke(this, EventArgs.Empty);
        }

        public void BringIn(string path)
        {
            if (Path.GetExtension(path).Equals(".pdsmap", StringComparison.OrdinalIgnoreCase))
            {
                BringInProject(path);
                return;
            }

            bool theirs = Path.GetExtension(path).Equals(".pdsts", StringComparison.OrdinalIgnoreCase);
            var set = theirs
                ? PdstsFile.Read(path, out string whynot)
                : MapTileset.FromObj(path, out whynot);
            if (set == null) { Warning = whynot; return; }
            if (theirs) TileCollisions.ReadMeta(path, set);

            _project = null;
            ProjectMaps.Clear();
            _saved.Clear();
            Raise(nameof(HasProject));
            Raise(nameof(HasSaved));
            Raise(nameof(SavedNote));

            SizePlaces(set);
            Load(set, new TileGrid());
            if (theirs) CameFrom(path, set);
            Warning = MissingPictures(set);

            int spreading = set.Tiles.Count(t => t.Spreads);
            Note = $"{set.Tiles.Count} tiles, {set.SmartDrawings.Count} smart drawings";
        }

        private void SizePlaces(MapTileset set)
        {
            var own = PicturesOfTheSet(set);
            var area = PicturesOfTheArea();
            set.TurnPlacesIntoDots(name =>
                name == null ? (0, 0)
                : own.TryGetValue(name, out var mine) ? (mine.w, mine.h)
                : area.TryGetValue(name, out var found) ? (found.w, found.h) : (0, 0));
        }

        private string MissingPictures(MapTileset set)
        {
            // Only placed tiles count. A picture and its palette must both be in the pack under 16-character names, or it draws pink.
            var (pictures, palettes) = WhatTheAreaCarries();
            var placed = new HashSet<int>(_painter.Grid?.Placed().Select(p => p.square.Tile) ?? Enumerable.Empty<int>());
            var faces = placed.Where(t => t >= 0 && t < set.Tiles.Count).SelectMany(t => set.Tiles[t].Faces).ToList();
            var missing = faces.Select(f => f.Picture)
                               .Where(p => !string.IsNullOrEmpty(p) && !pictures.Contains(NitroDictionary.Fit(p)))
                               .Concat(faces.Select(f => f.Palette)
                                            .Where(p => !string.IsNullOrEmpty(p) && !palettes.Contains(NitroDictionary.Fit(p)))
                                            .Select(p => p + " (palette)"))
                               .Distinct().ToList();
            return missing.Count == 0 ? null
                : $"Missing textures: {string.Join(", ", missing.Take(6))}"
                + (missing.Count > 6 ? $" and {missing.Count - 6} more" : "")
                + (set.PictureFiles.Count > 0
                    ? ". Use Add textures to ROM."
                    : "");
        }

        private PdsmapFile.Project _project;
        private string _projectPath;

        private readonly Dictionary<string, string> _saved = new Dictionary<string, string>();

        public bool HasSaved => _saved.Count > 0;

        private bool _alsoSaved = true;
        public bool AlsoSaved { get => _alsoSaved; set => Set(ref _alsoSaved, value); }

        public string SavedNote => _saved.Count == 0 ? "" : "Use PDSMS files ("
            + string.Join(", ", _saved.Keys.Select(k => k switch
            {
                "per" => "permissions", "bld" => "buildings", "bgs" => "BGS", "bdhc" => "BDHC", _ => k,
            })) + ")";

        public ObservableCollection<string> ProjectMaps { get; } = new ObservableCollection<string>();
        public bool HasProject => ProjectMaps.Count > 1;

        private int _projectMap = -1;
        public int ProjectMap
        {
            get => _projectMap;
            set
            {
                if (_project == null || value < 0 || value >= _project.Maps.Count || value == _projectMap) return;
                _projectMap = value;
                Raise();
                PaintProjectMap();
            }
        }

        private void BringInProject(string path)
        {
            var project = PdsmapFile.Read(path, out string whynot);
            if (project == null) { Warning = whynot; return; }
            if (string.IsNullOrEmpty(project.TilesetPath) || !File.Exists(project.TilesetPath))
            {
                Warning = $"Tileset not found: {Path.GetFileName(project.TilesetPath ?? "")}";
                return;
            }

            var set = PdstsFile.Read(project.TilesetPath, out whynot);
            if (set == null) { Warning = whynot; return; }
            TileCollisions.ReadMeta(project.TilesetPath, set);
            SizePlaces(set);

            _project = project;
            _projectPath = path;
            _projectFamily = FamilyOf(project.Game);
            _projectMadeFor = _projectFamily is GameFamilies made && made != _family ? GameName(project.Game) : null;
            ProjectMaps.Clear();
            foreach (var m in project.Maps) ProjectMaps.Add($"Map {m.X},{m.Y}  ·  area {m.Area}");
            Raise(nameof(HasProject));

            Load(set, new TileGrid());
            CameFrom(project.TilesetPath, set);
            _projectMap = -1;
            ProjectMap = 0;
        }

        private string _projectMadeFor;
        private GameFamilies? _projectFamily;

        // Building ids differ per game and DP terrain has its own format; permissions are shared by all games.
        private bool LeavesOut(string kind) => kind switch
        {
            "bld" => _projectMadeFor != null,
            "bdhc" => _projectMadeFor != null && (_family == GameFamilies.DP) != (_projectFamily == GameFamilies.DP),
            _ => false,
        };

        private static GameFamilies? FamilyOf(int pdsmsGame) => pdsmsGame switch
        {
            0 or 1 => GameFamilies.DP,
            2 => GameFamilies.Plat,
            3 or 4 => GameFamilies.HGSS,
            _ => null,
        };

        private static string GameName(int pdsmsGame) => pdsmsGame switch
        {
            0 => "Diamond", 1 => "Pearl", 2 => "Platinum", 3 => "HeartGold", 4 => "SoulSilver", _ => "another game",
        };

        private void PaintProjectMap()
        {
            var map = _project.Maps[_projectMap];

            _saved.Clear();
            string stem = Path.Combine(Path.GetDirectoryName(_projectPath) ?? ".",
                                       $"{Path.GetFileNameWithoutExtension(_projectPath)}_{map.X:D2}_{map.Y:D2}");
            foreach (string kind in new[] { "per", "bld", "bgs", "bdhc" })
            {
                if (kind == "bgs" && _family != GameFamilies.HGSS) continue;
                if (File.Exists(stem + "." + kind)) _saved[kind] = stem + "." + kind;
            }
            Raise(nameof(HasSaved));
            Raise(nameof(SavedNote));
            var grid = PdsmapFile.ToGrid(map, _set, out int dropped);
            int smart = _painter.SmartIndex;
            _painter.Load(grid, _set);
            _atLoad = grid.Clone();
            _painter.SmartIndex = Math.Min(smart, Math.Max(0, _set.SmartDrawings.Count - 1));
            Edited();
            SortTileList();

            string missing = MissingPictures(_set);
            Warning = dropped > 0
                ? $"{dropped} tiles skipped."
                  + (missing != null ? " " + missing : "")
                : missing;
            if (_saved.Keys.Any(LeavesOut))
                Warning = $"Made for {_projectMadeFor}: its "
                        + string.Join(" and ", _saved.Keys.Where(LeavesOut).Select(k => k == "bld" ? "buildings" : "terrain"))
                        + " are left out, as they differ in this game." + (Warning != null ? " " + Warning : "");
            Note = $"Map {map.X},{map.Y}: {grid.Painted} cells on {Enumerable.Range(0, TileGrid.Layers).Count(grid.HasAnything)} layers";
        }

        // Tiles read from a PDSMS tileset, with the file and position their collision is saved to.
        private readonly Dictionary<MapTileset.Tile, (string path, int listed)> _tileSource =
            new Dictionary<MapTileset.Tile, (string, int)>(ReferenceEqualityComparer.Instance);

        private void CameFrom(string path, MapTileset set)
        {
            foreach (var (tile, listed) in TileCollisions.ListedPlaces(set))
                _tileSource[tile] = (path, listed);
            Raise(nameof(ChosenFromTileset));
        }

        private bool _collisionUnsaved;
        public bool CollisionUnsaved { get => _collisionUnsaved; private set => Set(ref _collisionUnsaved, value); }

        public bool ChosenFromTileset => Chosen != null && _tileSource.ContainsKey(Chosen);

        /// <summary>Writes collision defaults of PDSMS tiles into each tileset's .pdsts.meta.</summary>
        public void SaveCollisionToTileset()
        {
            int written = 0;
            foreach (var file in _tileSource.GroupBy(kv => kv.Value.path, StringComparer.OrdinalIgnoreCase))
            {
                int n = TileCollisions.WriteMeta(file.Key, file.Select(kv => (kv.Key, kv.Value.listed)), out string whynot);
                if (n < 0) { Warning = $"Could not write {Path.GetFileName(file.Key)}.meta: {whynot}"; return; }
                written += n;
            }
            CollisionUnsaved = false;
            Note = $"Collision of {written} tiles saved to the tileset.";
        }

        public string SaveTo(string path)
        {
            if (_set == null || _set.Tiles.Count == 0) return "No tiles to export.";

            string why = _set.SaveObj(path);
            if (why != null) { Warning = why; return why; }

            Warning = null;
            Note = $"Exported {_set.Tiles.Count} tiles.";
            return null;
        }

        /// <summary>Area data ids of every header showing the map; the map editor fills this in.</summary>
        public Func<List<byte>> AreasOfMap { get; set; }

        public bool HasOwnPictures => _set != null && _set.PictureFiles.Count > 0;

        /// <summary>One texture the painted tiles need, and what adding it will do.</summary>
        public sealed class PictureChoice
        {
            public string Name { get; set; }
            public string Said { get; set; }
            public bool Clash { get; set; }
            /// <summary>For a clash: 0 keeps the area's picture, 1 adds this one under a new name, 2 replaces the area's.</summary>
            public int Choice { get; set; } = 1;
            public AvBitmap Ours { get; set; }
            public AvBitmap Theirs { get; set; }
            public bool HasTheirs => Theirs != null;
            internal DsTexture Texture;
        }

        /// <summary>Everything adding the textures would do, for the user to look over first.</summary>
        public sealed class TexturePlan
        {
            public List<PictureChoice> Pictures { get; } = new List<PictureChoice>();
            /// <summary>The ones that need a look: clashes first, then new textures.</summary>
            public List<PictureChoice> Shown => Pictures.Where(p => p.Said != Same).OrderByDescending(p => p.Clash).ThenBy(p => p.Name).ToList();
            public string AlreadyThere
            {
                get
                {
                    var same = Pictures.Where(p => p.Said == Same).Select(p => p.Name).ToList();
                    return same.Count == 0 ? null : $"Already in the pack, nothing to add: {string.Join(", ", same.Take(12))}{(same.Count > 12 ? $" and {same.Count - 12} more" : "")}.";
                }
            }
            public bool HasAlreadyThere => AlreadyThere != null;
            public string Where { get; set; }
            public string Memory { get; set; }
            public bool TooBig { get; set; }
            public int Adding => Pictures.Count(p => p.Said != Same);
            internal List<(int pack, List<byte> areas)> Packs = new List<(int, List<byte>)>();
            internal List<string> Unreadable = new List<string>();
        }

        private const string Same = "Already there";

        // The field gives all 3D textures on screen these, map, buildings and people together (VRAM banks A+B and E).
        private const int FieldTextureBytes = 256 * 1024, FieldPaletteBytes = 64 * 1024;

        // Tiles drawn with the tileset's own pictures rather than the area's.
        private readonly HashSet<int> _ownTiles = new HashSet<int>();

        public TexturePlan PlanPictures()
        {
            if (_set == null || _set.PictureFiles.Count == 0) { Warning = "This tileset has no texture files."; return null; }
            var used = new HashSet<string>(PicturesInUse(), StringComparer.OrdinalIgnoreCase);
            var wanted = _set.PictureFiles.Where(kv => used.Contains(kv.Key)).ToList();
            if (wanted.Count == 0) { Warning = "No painted tile uses the tileset's textures."; return null; }
            if (wanted.Count > NsbtxWriter.MostPictures) { Warning = $"{wanted.Count} textures needed, a texture pack holds {NsbtxWriter.MostPictures}."; return null; }

            var plan = new TexturePlan();
            var ours = new Dictionary<string, (byte[] rgba, int w, int h)>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in wanted)
            {
                try
                {
                    if (!AnyPng.TryReadRgba(File.ReadAllBytes(kv.Value), out byte[] rgba, out int w, out int h, out _))
                    { plan.Unreadable.Add(Path.GetFileName(kv.Value)); continue; }
                    var texture = DsTexture.From(rgba, w, h, kv.Key);
                    texture.PaletteNames = OwnFaces().Where(f => f.Picture == kv.Key && !string.IsNullOrEmpty(f.Palette))
                        .Select(f => f.Palette).Prepend(kv.Key).Distinct().ToList();
                    ours[kv.Key] = AsTheGameHasIt(texture) ?? (rgba, w, h);
                    plan.Pictures.Add(new PictureChoice { Name = kv.Key, Texture = texture, Ours = ImageConverter.FromRgba(rgba, w, h) });
                }
                catch { plan.Unreadable.Add(Path.GetFileName(kv.Value)); }
            }
            if (plan.Pictures.Count == 0) { Warning = "No texture could be read."; return null; }

            // Every area whose headers show this map; areas sharing a pack share the new one.
            var areas = AreasOfMap?.Invoke() ?? new List<byte>();
            if (!areas.Contains(_areaId)) areas.Insert(0, _areaId);
            string folder = gameDirs[DirNames.mapTextures].unpackedDir;
            foreach (var g in areas.GroupBy(a => (int)new AreaData(a).mapTileset)) plan.Packs.Add((g.Key, g.ToList()));

            var theirs = new Dictionary<int, (NsbtxWriter.Contents contents, Dictionary<string, (byte[] rgba, int w, int h)> pictures)>();
            foreach (var (pack, _) in plan.Packs)
            {
                string path = Path.Combine(folder, pack.ToString("D4"));
                var bytes = File.Exists(path) ? File.ReadAllBytes(path) : null;
                theirs[pack] = (bytes == null ? null : NsbtxWriter.Read(bytes),
                                bytes == null ? new Dictionary<string, (byte[] rgba, int w, int h)>() : PicturesInPack(bytes, PalettesOf));
            }

            foreach (var choice in plan.Pictures)
            {
                bool clash = false, missing = false;
                foreach (var (pack, _) in plan.Packs)
                {
                    var (contents, pictures) = theirs[pack];
                    string had = contents?.Textures.FirstOrDefault(n => string.Equals(n, NitroDictionary.Fit(choice.Name), StringComparison.OrdinalIgnoreCase));
                    if (had == null) { missing = true; continue; }
                    if (pictures.TryGetValue(had, out var their) && !LooksTheSame(ours[choice.Name], their))
                    {
                        clash = true;
                        choice.Theirs ??= ImageConverter.FromRgba(their.rgba, their.w, their.h);
                    }
                }
                choice.Clash = clash;
                choice.Said = clash ? "Different picture, same name" : missing ? "New" : Same;
            }

            // Video memory the field needs for this area afterwards: its map pack plus its buildings.
            int worstTex = 0, worstPal = 0;
            foreach (var (pack, packAreas) in plan.Packs)
            {
                var contents = theirs[pack].contents;
                int tex = contents?.TextureBytes ?? 0, pal = contents?.PaletteBytes ?? 0;
                foreach (var c in plan.Pictures.Where(c => c.Said != Same))
                {
                    tex += c.Texture.Pixels?.Length ?? 0;
                    pal += c.Texture.Colours.Length * 2;
                }
                int buildings = 0, buildingPalettes = 0;
                try
                {
                    string bld = Path.Combine(gameDirs[DirNames.buildingTextures].unpackedDir, new AreaData(packAreas[0]).buildingsTileset.ToString("D4"));
                    var b = File.Exists(bld) ? NsbtxWriter.Read(File.ReadAllBytes(bld)) : null;
                    buildings = b?.TextureBytes ?? 0; buildingPalettes = b?.PaletteBytes ?? 0;
                }
                catch { }
                worstTex = Math.Max(worstTex, tex + buildings);
                worstPal = Math.Max(worstPal, pal + buildingPalettes);
            }
            plan.TooBig = worstTex > FieldTextureBytes || worstPal > FieldPaletteBytes;
            plan.Memory = $"Map and building textures would take {worstTex / 1024} of the field's {FieldTextureBytes / 1024} KB, "
                        + $"palettes {Math.Max(1, worstPal / 1024)} of {FieldPaletteBytes / 1024} KB. People and objects on screen use the rest.";
            if (plan.Adding == 0) { Warning = null; Note = "Every texture these tiles use is already there."; return null; }
            plan.Where = plan.Packs.Count == 1
                ? $"Area {string.Join(", ", plan.Packs[0].areas)} gets a new pack: its own textures plus these."
                : $"This map shows under areas {string.Join(", ", plan.Packs.SelectMany(p => p.areas))}; each gets a new pack of its own textures plus these.";
            return plan;
        }

        // Two pictures match when nearly every texel has the same 15-bit colour and the same see-through-ness.
        private static bool LooksTheSame((byte[] rgba, int w, int h) a, (byte[] rgba, int w, int h) b)
        {
            if (a.w != b.w || a.h != b.h || a.rgba == null || b.rgba == null) return false;
            int n = a.w * a.h, differ = 0;
            for (int i = 0; i < n; i++)
            {
                int k = i * 4;
                bool clearA = a.rgba[k + 3] < 128, clearB = b.rgba[k + 3] < 128;
                if (clearA && clearB) continue;
                if (clearA != clearB || (a.rgba[k] >> 3) != (b.rgba[k] >> 3) || (a.rgba[k + 1] >> 3) != (b.rgba[k + 1] >> 3)
                    || (a.rgba[k + 2] >> 3) != (b.rgba[k + 2] >> 3)) differ++;
            }
            return differ <= n / 50;
        }

        // Palettes the set's faces draw a picture with.
        private IEnumerable<string> PalettesOf(string picture) =>
            _set.Tiles.SelectMany(t => t.Faces).Where(f => string.Equals(f.Picture, picture, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(f.Palette))
                .Select(f => f.Palette).Distinct();

        private IEnumerable<MapTileset.Face> OwnFaces() =>
            _ownTiles.Where(t => t >= 0 && t < _set.Tiles.Count).SelectMany(t => _set.Tiles[t].Faces);

        public string PutPicturesIntoTheRom(TexturePlan plan)
        {
            if (plan == null || _set == null) return "Nothing to add.";
            string folder = gameDirs[DirNames.mapTextures].unpackedDir;
            var packBytes = plan.Packs.ToDictionary(p => p.pack, p =>
            {
                string path = Path.Combine(folder, p.pack.ToString("D4"));
                return File.Exists(path) ? File.ReadAllBytes(path) : null;
            });
            var contents = packBytes.ToDictionary(kv => kv.Key, kv => kv.Value == null ? null : NsbtxWriter.Read(kv.Value));
            var taken = new HashSet<string>(contents.Values.Where(c => c != null).SelectMany(c => c.Textures.Concat(c.Palettes.Keys)), StringComparer.OrdinalIgnoreCase);
            taken.UnionWith(_set.Tiles.SelectMany(t => t.Faces).SelectMany(f => new[] { f.Picture, f.Palette }).Where(n => !string.IsNullOrEmpty(n)));
            string Fresh(string name)
            {
                // Names in a pack are at most 16 characters.
                for (int k = 2; ; k++)
                {
                    string tail = "_" + k, called = (name.Length + tail.Length > 16 ? name.Substring(0, 16 - tail.Length) : name) + tail;
                    if (taken.Add(called)) return called;
                }
            }
            void Rename(string picture, Func<MapTileset.Face, bool> which, Action<MapTileset.Face> change)
            {
                foreach (var f in OwnFaces().Where(f => f.Picture == picture && which(f)).ToList()) change(f);
            }

            var adding = new List<DsTexture>();
            var replace = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var renamed = new List<string>();
            foreach (var c in plan.Pictures)
            {
                if (c.Said == Same) continue;
                var t = c.Texture;
                if (c.Clash && c.Choice == 1)
                {
                    string old = t.Name, now = Fresh(old);
                    var palettes = new Dictionary<string, string>();
                    foreach (string pal in t.PaletteNames) palettes[pal] = Fresh(pal);
                    Rename(old, f => true, f => { if (f.Palette != null && palettes.TryGetValue(f.Palette, out var p2)) f.Palette = p2; f.Picture = now; });
                    if (_set.PictureFiles.TryGetValue(old, out var file)) _set.PictureFiles[now] = file;
                    t.Name = now;
                    t.PaletteNames = t.PaletteNames.Select(pal => palettes[pal]).ToList();
                    renamed.Add($"{old} as {now}");
                }
                else if (c.Clash && c.Choice == 2) replace.Add(t.Name);

                // A palette sharing a name with a different one already in a pack gets a name of its own.
                if (!replace.Contains(t.Name))
                {
                    byte[] mine = t.Colours.SelectMany(v => new[] { (byte)v, (byte)(v >> 8) }).ToArray();
                    for (int i = 0; i < t.PaletteNames.Count; i++)
                    {
                        string pal = t.PaletteNames[i];
                        bool differs = contents.Values.Any(cn => cn != null && cn.Palettes.TryGetValue(NitroDictionary.Fit(pal), out var theirs)
                                                                 && !theirs.Take(mine.Length).SequenceEqual(mine.Take(theirs.Length)));
                        if (!differs) continue;
                        string now = Fresh(pal);
                        Rename(t.Name, f => f.Palette == pal, f => f.Palette = now);
                        t.PaletteNames[i] = now;
                    }
                }
                adding.Add(t);
            }
            if (adding.Count == 0) { Warning = null; Note = "Nothing added."; return null; }

            var wrote = new List<string>();
            try
            {
                foreach (var (pack, packAreas) in plan.Packs)
                {
                    var bytes = packBytes[pack];
                    var made = bytes != null ? NsbtxWriter.Extend(bytes, adding, replace) : null;
                    if (made == null || made.Whynot != null) made = NsbtxWriter.Build(adding);
                    if (made.Whynot != null) { Warning = made.Whynot; return made.Whynot; }
                    int newPack = Directory.GetFiles(folder).Length;
                    File.WriteAllBytes(Path.Combine(folder, newPack.ToString("D4")), made.Bytes);
                    foreach (byte a in packAreas)
                    {
                        var area = new AreaData(a);
                        area.mapTileset = (ushort)newPack;
                        area.SaveToFileDefaultDir(a, showSuccessMessage: false);
                    }
                    wrote.Add($"pack {newPack} for area{(packAreas.Count > 1 ? "s" : "")} {string.Join(", ", packAreas)}");
                }

                _pictures = PicturesOfTheArea();
                foreach (var kv in PicturesOfTheSet(_set)) _pictures.TryAdd(kv.Key, kv.Value);
                _fromAbove.Clear();
                _thumbs.Clear();
                FillList();
                FillSmart();
                ShowTile();
                GridChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                AppLogger.Error("MapTiles.PutPictures: " + ex.Message);
                Warning = ex.Message;
                return ex.Message;
            }

            Dirty = true;
            string said = $"Added {adding.Count} textures: {string.Join("; ", wrote)}."
                        + (renamed.Count > 0 ? $" Renamed {string.Join(", ", renamed)}." : "")
                        + (replace.Count > 0 ? $" Replaced the area's {string.Join(", ", replace)}." : "");
            if (plan.Unreadable.Count > 0) said += $" Unreadable: {string.Join(", ", plan.Unreadable.Take(5))}.";

            // The game copies its own frames over any texture with an animated name.
            string[] Formats = { "none", "A3I5", "4-colour", "16-colour", "256-colour", "compressed", "A5I3", "direct" };
            var anims = MatrixSceneBuilder.FieldAnimations();
            var clashes = new List<string>();
            foreach (var t in adding)
            {
                var entry = anims?.For(t.Name);
                if (entry?.FramePack == null) continue;
                try
                {
                    NSBTXLoader.LoadNsbtx(new MemoryStream(entry.FramePack), out var frames, out _);
                    var first = frames?.FirstOrDefault();
                    if (first != null && (first.width != t.Width || first.height != t.Height || first.format != (int)t.Format))
                        clashes.Add($"{t.Name} ({t.Width}x{t.Height} {Formats[(int)t.Format & 7]} here, frames {first.width}x{first.height} {Formats[first.format & 7]})");
                }
                catch { }
            }
            Warning = clashes.Count == 0 ? null
                : "The game animates these by name with frames that don't fit: " + string.Join(", ", clashes)
                  + ". Rename the texture or give it new frames in the Tile tab.";
            Note = said;
            Changed?.Invoke(this, EventArgs.Empty);
            return null;
        }

        // Pictures the painted tiles need that the tileset itself supplies.
        private IEnumerable<string> PicturesInUse()
        {
            var tiles = new HashSet<int>();
            foreach (var p in _painter.Grid.Placed())
                if (_ownTiles.Contains(p.square.Tile)) tiles.Add(p.square.Tile);
            return tiles.Where(t => t >= 0 && t < _set.Tiles.Count)
                        .SelectMany(t => _set.Tiles[t].Pictures)
                        .Where(p => !string.IsNullOrEmpty(p)).Distinct();
        }

        private static Dictionary<string, (byte[] rgba, int w, int h)> PicturesInPack(string path)
            => File.Exists(path) ? PicturesInPack(File.ReadAllBytes(path)) : new Dictionary<string, (byte[] rgba, int w, int h)>(StringComparer.OrdinalIgnoreCase);

        // How a picture looks once it is in the game's format, so it compares like for like with a pack's.
        private static (byte[] rgba, int w, int h)? AsTheGameHasIt(DsTexture t)
        {
            var made = NsbtxWriter.Build(new[] { t });
            if (made.Whynot != null) return null;
            return PicturesInPack(made.Bytes).TryGetValue(t.Name, out var got) ? got : null;
        }

        /// <param name="palettesFor">Palette names a texture is drawn with, best first; a pack alone does not say which goes with which.</param>
        private static Dictionary<string, (byte[] rgba, int w, int h)> PicturesInPack(byte[] bytes, Func<string, IEnumerable<string>> palettesFor = null)
        {
            var known = new Dictionary<string, (byte[], int, int)>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var container = new global::LibNDSFormats.NSBMD.NSBMD();
                container.materials = NSBTXLoader.LoadNsbtx(new MemoryStream(bytes), out container.Textures, out container.Palettes);
                foreach (var mat in container.materials ?? new List<global::LibNDSFormats.NSBMD.NSBMDMaterial>())
                {
                    if (string.IsNullOrEmpty(mat.texname) || known.ContainsKey(mat.texname)) continue;
                    var wanted = (palettesFor?.Invoke(mat.texname) ?? Enumerable.Empty<string>()).Append(mat.texname).Append(mat.texname + "_pl");
                    var pal = wanted.Select(n => container.Palettes?.FirstOrDefault(q => q.palname == n)).FirstOrDefault(q => q != null);
                    if (pal != null) { mat.palname = pal.palname; mat.paldata = pal.paldata; }
                    var picture = NsbmdTextureDecoder.Decode(mat);
                    if (picture?.Rgba != null) known[mat.texname] = (picture.Rgba, picture.Width, picture.Height);
                }
            }
            catch (Exception ex) { AppLogger.Error("MapTiles.PicturesInPack: " + ex.Message); }
            return known;
        }

        private (HashSet<string> pictures, HashSet<string> palettes) WhatTheAreaCarries()
        {
            var pictures = new HashSet<string>(StringComparer.Ordinal);
            var palettes = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                var area = new AreaData(_areaId);
                string path = Path.Combine(gameDirs[DirNames.mapTextures].unpackedDir,
                                           area.mapTileset.ToString("D4"));
                if (!File.Exists(path)) return (pictures, palettes);

                NSBTXLoader.LoadNsbtx(new MemoryStream(File.ReadAllBytes(path)), out var textures, out var colours);
                foreach (var t in textures ?? new List<global::LibNDSFormats.NSBMD.NSBMDTexture>())
                    if (!string.IsNullOrEmpty(t.texname)) pictures.Add(t.texname.TrimEnd('\0'));
                foreach (var p in colours ?? new List<global::LibNDSFormats.NSBMD.NSBMDPalette>())
                    if (!string.IsNullOrEmpty(p.palname)) palettes.Add(p.palname.TrimEnd('\0'));
            }
            catch (Exception ex) { AppLogger.Error("MapTiles.AreaPictures: " + ex.Message); }
            return (pictures, palettes);
        }

        private void FillList()
        {
            FillRampFills();
            int keep = Brush;
            Tiles.Clear();
            if (_set == null) { Brush = -1; _tileOrder.Clear(); ShowTileList(); return; }

            var uses = TileUses();
            for (int i = 0; i < _set.Tiles.Count; i++)
            {
                var tile = _set.Tiles[i];
                int n = i;
                Tiles.Add(new TileRow
                {
                    Index = i,
                    Swatch = TileGridControl.Colour(i),
                    Picture = Thumbnail(i),
                    MakeBig = () => BigThumbnail(n),
                    Name = tile.Name,
                    Pictures = string.Join(", ", tile.Pictures.Where(p => !string.IsNullOrEmpty(p))),
                    Size = tile.Spreads && !(tile.Name ?? "").Contains($"{tile.Wide}x{tile.Deep}") ? $"  ·  {tile.Wide}x{tile.Deep} squares" : "",
                    Uses = uses[i],
                });
            }

            Brush = keep >= 0 && keep < Tiles.Count ? keep : Tiles.Count > 0 ? 0 : -1;
            SortTileList();
        }

        // Tiles on the map first; done when a whole map arrives, not while painting, so the list holds still.
        private void SortTileList()
        {
            CountUses();
            _tileOrder = Tiles.OrderByDescending(r => r.Uses > 0).ThenBy(r => r.Index).Select(r => r.Index).ToList();
            ShowTileList();
        }

        // Fixed when the list is filled, so the list does not reshuffle under the pointer while painting.
        private List<int> _tileOrder = new List<int>();

        private void ShowTileList()
        {
            ShownTiles.Clear();
            string[] words = _tileSearch.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (int i in _tileOrder)
            {
                if (i >= Tiles.Count) continue;
                var row = Tiles[i];
                if (words.All(w => row.Name.Contains(w, StringComparison.OrdinalIgnoreCase)
                                || row.Pictures.Contains(w, StringComparison.OrdinalIgnoreCase)))
                    ShownTiles.Add(row);
            }
            Raise(nameof(ShownTile));
        }

        /// <summary>How many times each tile is placed on the grid, every layer counted.</summary>
        private int[] TileUses()
        {
            var uses = new int[_set?.Tiles.Count ?? 0];
            var grid = _painter.Grid;
            if (grid == null) return uses;
            for (int l = 0; l < TileGrid.Layers; l++)
                for (int z = 0; z < TileGrid.Across; z++)
                    for (int x = 0; x < TileGrid.Across; x++)
                    {
                        var sq = grid.At(x, z, l);
                        if (sq.Tile >= 0 && sq.Tile < uses.Length && sq.FromX == x && sq.FromZ == z) uses[sq.Tile]++;
                    }
            return uses;
        }

        private void CountUses()
        {
            var uses = TileUses();
            for (int i = 0; i < Tiles.Count && i < uses.Length; i++) Tiles[i].Uses = uses[i];
        }

        private void FillSmart()
        {
            SmartDrawings.Clear();
            if (_set != null)
                for (int i = 0; i < _set.SmartDrawings.Count; i++)
                    SmartDrawings.Add(new SmartRow { Index = i, Picture = SmartPicture(_set.SmartDrawings[i]) });

            if (_painter.SmartIndex >= SmartDrawings.Count) _painter.SmartIndex = Math.Max(0, SmartDrawings.Count - 1);
            FillSlots();
            Raise(nameof(SmartIndex));
            Raise(nameof(HasSmartDrawing));
        }

        private void FillSlots()
        {
            Slots.Clear();
            var drawing = _painter.Drawing;
            if (drawing == null) return;
            for (int slot = 0; slot < SmartDrawing.Slots; slot++)
            {
                int tile = drawing[slot];
                Slots.Add(new SlotRow
                {
                    Slot = slot,
                    Picture = tile >= 0 ? Thumbnail(tile) : null,
                    Tip = tile >= 0 && tile < (_set?.Tiles.Count ?? 0) ? _set.Tiles[tile].Name : "empty",
                });
            }
        }

        public void SetSlot(int slot, bool empty)
        {
            var drawing = _painter.Drawing;
            if (drawing == null || slot < 0 || slot >= SmartDrawing.Slots) return;
            drawing[slot] = empty ? -1 : Brush;
            FillSlots();
            if (SmartIndex >= 0 && SmartIndex < SmartDrawings.Count)
                SmartDrawings[SmartIndex] = new SmartRow { Index = SmartIndex, Picture = SmartPicture(drawing) };
        }

        public void AddSmartDrawing()
        {
            if (_set == null) return;
            _set.SmartDrawings.Add(new SmartDrawing());
            FillSmart();
            SmartIndex = _set.SmartDrawings.Count - 1;
        }

        public void RemoveSmartDrawing()
        {
            if (_set == null || SmartIndex < 0 || SmartIndex >= _set.SmartDrawings.Count) return;
            _set.SmartDrawings.RemoveAt(SmartIndex);
            FillSmart();
        }

        private Dictionary<string, (byte[] rgba, int w, int h)> PicturesOfTheArea()
        {
            var known = new Dictionary<string, (byte[], int, int)>(StringComparer.Ordinal);
            try
            {
                var area = new AreaData(_areaId);
                string path = Path.Combine(gameDirs[DirNames.mapTextures].unpackedDir,
                                           area.mapTileset.ToString("D4"));
                if (!File.Exists(path)) return known;

                var container = new global::LibNDSFormats.NSBMD.NSBMD();
                container.materials = NSBTXLoader.LoadNsbtx(new MemoryStream(File.ReadAllBytes(path)),
                                                            out container.Textures, out container.Palettes);
                if (container.materials == null) return known;
                _areaTextures = container.Textures ?? new List<global::LibNDSFormats.NSBMD.NSBMDTexture>();
                _areaPalettes = container.Palettes ?? new List<global::LibNDSFormats.NSBMD.NSBMDPalette>();
                _paired.Clear();

                // The map's own materials say which palette each texture is drawn with.
                var drawnWith = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var m in _map?.mapModel?.models?.FirstOrDefault()?.Materials ?? new List<global::LibNDSFormats.NSBMD.NSBMDMaterial>())
                    if (!string.IsNullOrEmpty(m.texname) && !string.IsNullOrEmpty(m.palname)) drawnWith.TryAdd(m.texname, m.palname);
                foreach (var mat in container.materials)
                {
                    if (string.IsNullOrEmpty(mat.texname) || known.ContainsKey(mat.texname)) continue;
                    if (drawnWith.TryGetValue(mat.texname, out var palName) && container.Palettes?.FirstOrDefault(q => q.palname == palName) is var pal && pal != null)
                    { mat.palname = pal.palname; mat.paldata = pal.paldata; }
                    var picture = NsbmdTextureDecoder.Decode(mat);
                    if (picture?.Rgba != null)
                        known[mat.texname] = (picture.Rgba, picture.Width, picture.Height);
                }
            }
            catch (Exception ex) { AppLogger.Error("MapTiles.Pictures: " + ex.Message); }
            return known;
        }

        private static Dictionary<string, (byte[] rgba, int w, int h)> PicturesOfTheSet(MapTileset set)
        {
            var known = new Dictionary<string, (byte[], int, int)>(StringComparer.Ordinal);
            if (set == null) return known;
            foreach (var kv in set.PictureFiles)
            {
                try
                {
                    if (AnyPng.TryReadRgba(File.ReadAllBytes(kv.Value), out byte[] rgba, out int w, out int h, out _))
                        known[kv.Key] = (rgba, w, h);
                }
                catch (Exception ex) { AppLogger.Error("MapTiles.SetPicture: " + ex.Message); }
            }
            return known;
        }

        private bool Picture(string name, string colours, out byte[] rgba, out int width, out int height)
        {
            if (name != null && !string.IsNullOrEmpty(colours))
            {
                string key = name + "|" + colours;
                if (!_paired.TryGetValue(key, out var pair))
                    _paired[key] = pair = Decode(name, colours);
                if (pair.rgba != null) { rgba = pair.rgba; width = pair.w; height = pair.h; return true; }
            }
            if (name != null && _pictures.TryGetValue(name, out var found))
            { rgba = found.rgba; width = found.w; height = found.h; return true; }
            rgba = null; width = height = 0; return false;
        }

        private readonly Dictionary<string, (byte[] rgba, int w, int h)> _paired = new Dictionary<string, (byte[] rgba, int w, int h)>();
        private List<global::LibNDSFormats.NSBMD.NSBMDTexture> _areaTextures = new List<global::LibNDSFormats.NSBMD.NSBMDTexture>();
        private List<global::LibNDSFormats.NSBMD.NSBMDPalette> _areaPalettes = new List<global::LibNDSFormats.NSBMD.NSBMDPalette>();

        private (byte[] rgba, int w, int h) Decode(string picture, string colours)
        {
            try
            {
                var tex = _areaTextures.FirstOrDefault(t => t.texname == picture);
                var pal = _areaPalettes.FirstOrDefault(p => p.palname == colours);
                if (tex == null || pal == null) return (null, 0, 0);
                var stand_in = new global::LibNDSFormats.NSBMD.NSBMDMaterial
                {
                    texdata = tex.texdata, spdata = tex.spdata, texname = tex.texname,
                    texoffset = tex.texoffset, texsize = tex.texsize,
                    width = tex.width, height = tex.height, format = tex.format, color0 = tex.color0,
                    paldata = pal.paldata, palname = pal.palname, paloffset = pal.paloffset, palsize = pal.palsize,
                };
                var data = NsbmdTextureDecoder.Decode(stand_in);
                return data?.Rgba == null ? (null, 0, 0) : (data.Rgba, data.Width, data.Height);
            }
            catch (Exception ex) { AppLogger.Error("MapTiles.Decode: " + ex.Message); return (null, 0, 0); }
        }

        private AvBitmap BigThumbnail(int tile)
        {
            const int Size = 160;
            try
            {
                if (_set != null && tile >= 0 && tile < _set.Tiles.Count)
                    return ToBitmap(TileThumbnail.Draw(_set.Tiles[tile], Size, Picture), Size, Size);
            }
            catch (Exception ex) { AppLogger.Error("MapTiles.BigThumbnail: " + ex.Message); }
            return null;
        }

        private AvBitmap Thumbnail(int tile)
        {
            if (_thumbs.TryGetValue(tile, out var had)) return had;
            const int Size = 44;
            AvBitmap made = null;
            try
            {
                if (_set != null && tile >= 0 && tile < _set.Tiles.Count)
                    made = ToBitmap(TileThumbnail.Draw(_set.Tiles[tile], Size, Picture), Size, Size);
            }
            catch (Exception ex) { AppLogger.Error("MapTiles.Thumbnail: " + ex.Message); }
            _thumbs[tile] = made;
            return made;
        }

        public TileGridControl.Look? FromAbove(int tile, int turn)
        {
            if (_fromAbove.TryGetValue((tile, turn), out var had)) return had;
            TileGridControl.Look? made = null;
            try
            {
                if (_set != null && tile >= 0 && tile < _set.Tiles.Count)
                {
                    var drawn = TileThumbnail.DrawWholeFromAbove(_set.Tiles[tile], turn, 16, Picture);
                    if (drawn.wide > 0 && drawn.tall > 0)
                        made = new TileGridControl.Look(ToBitmap(drawn.rgba, drawn.wide, drawn.tall),
                                                        drawn.fromX, drawn.fromZ, drawn.across, drawn.down);
                }
            }
            catch (Exception ex) { AppLogger.Error("MapTiles.FromAbove: " + ex.Message); }
            _fromAbove[(tile, turn)] = made;
            return made;
        }

        private AvBitmap SmartPicture(SmartDrawing drawing)
        {
            const int Cell = 20;
            var rgba = new byte[SmartDrawing.Wide * Cell * SmartDrawing.Tall * Cell * 4];
            int stride = SmartDrawing.Wide * Cell;
            for (int slot = 0; slot < SmartDrawing.Slots; slot++)
            {
                int tile = drawing[slot];
                if (_set == null || tile < 0 || tile >= _set.Tiles.Count) continue;
                var (dots, w, h) = TileThumbnail.DrawFromAbove(_set.Tiles[tile], 0, Cell, Picture);
                int ox = (slot % SmartDrawing.Wide) * Cell, oy = (slot / SmartDrawing.Wide) * Cell;
                for (int y = 0; y < Math.Min(h, Cell); y++)
                    Array.Copy(dots, y * w * 4, rgba, ((oy + y) * stride + ox) * 4, Math.Min(w, Cell) * 4);
            }
            return ToBitmap(rgba, stride, SmartDrawing.Tall * Cell);
        }

        private static AvBitmap ToBitmap(byte[] rgba, int w, int h)
        {
            var bitmap = new global::Avalonia.Media.Imaging.WriteableBitmap(
                new global::Avalonia.PixelSize(w, h), new global::Avalonia.Vector(96, 96),
                global::Avalonia.Platform.PixelFormat.Rgba8888, global::Avalonia.Platform.AlphaFormat.Unpremul);
            using (var locked = bitmap.Lock())
                for (int y = 0; y < h; y++)
                    System.Runtime.InteropServices.Marshal.Copy(rgba, y * w * 4, locked.Address + y * locked.RowBytes, w * 4);
            return bitmap;
        }

        public IReadOnlyList<TileGridControl.Placed> PlacedTiles()
        {
            var hidden = new List<(int layer, int x, int z)>();
            if (_set != null) TileBake.Laid(_painter.Grid, _set, hidden);
            var skip = new HashSet<(int, int, int)>(hidden);
            return _painter.Grid.Placed()
                .Where(p => !skip.Contains((p.square.Layer, p.x, p.z)))
                // Drawn from the lowest top to the highest, so a tree is never hidden under a flat decal.
                .OrderBy(p => p.square.Lift + (p.square.Tile >= 0 && p.square.Tile < _set.Tiles.Count && _set.Tiles[p.square.Tile].Corners.Count > 0
                                                ? _set.Tiles[p.square.Tile].Corners.Max(c => c.Y) : 0f))
                .ThenBy(p => p.square.Layer)
                .ThenByDescending(p => FromAbove(p.square.Tile, p.square.Turn) is TileGridControl.Look l ? l.Across * l.Down : 1)
                .Select(p => new TileGridControl.Placed(p.x, p.z - p.square.PastNorth, p.square.Tile, p.square.Turn,
                                                         p.square.FullWide, p.square.FullDeep, p.square.Layer))
                .ToList();
        }

        public bool[] VisibleLayers() => Layers.Select(l => l.Visible).ToArray();

        // A building model's extent across and down in map units, by model id.
        private static readonly Dictionary<(string dir, uint id), (float x0, float x1, float z0, float z1)?> _modelExtent =
            new Dictionary<(string, uint), (float, float, float, float)?>();

        // Indoor HGSS maps take their building models from the interior archive.
        private string BuildingModelsDir()
        {
            bool interior = false;
            if (_family == GameFamilies.HGSS && gameDirs.ContainsKey(DirNames.interiorBuildingModels))
            {
                try { interior = new AreaData(_areaId).areaType == AreaData.TYPE_INDOOR; } catch { }
                if (interior) DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.interiorBuildingModels });
            }
            return gameDirs[interior ? DirNames.interiorBuildingModels : DirNames.exteriorBuildingModels].unpackedDir;
        }

        /// <summary>Buildings whose texture or palette is missing from the area's building pack, so they draw pink; null when none.</summary>
        private string BuildingPicturesMissing()
        {
            if (_map?.buildings == null || _map.buildings.Count == 0) return null;
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.buildingTextures });
                var area = new AreaData(_areaId);
                string packPath = Path.Combine(gameDirs[DirNames.buildingTextures].unpackedDir, area.buildingsTileset.ToString("D4"));
                if (!File.Exists(packPath)) return null;
                NSBTXLoader.LoadNsbtx(new MemoryStream(File.ReadAllBytes(packPath)), out var textures, out var colours);
                var have = new HashSet<string>((textures ?? new List<global::LibNDSFormats.NSBMD.NSBMDTexture>()).Select(t => t.texname.TrimEnd('\0'))
                    .Concat((colours ?? new List<global::LibNDSFormats.NSBMD.NSBMDPalette>()).Select(c => c.palname.TrimEnd('\0'))), StringComparer.Ordinal);
                string dir = BuildingModelsDir();
                var bad = new SortedDictionary<uint, List<string>>();
                foreach (uint id in _map.buildings.Select(b => b.modelID).Distinct())
                {
                    string path = Path.Combine(dir, id.ToString("D4"));
                    var mesh = File.Exists(path) ? MapMesh.Read(File.ReadAllBytes(path), out _) : null;
                    if (mesh == null) { bad[id] = new List<string> { "no such model" }; continue; }
                    var lacking = mesh.Faces.Select(f => f.Material).Distinct()
                        .SelectMany(m => new[] { mesh.PictureFor(m) >= 0 ? mesh.NameOfPicture(mesh.PictureFor(m)) : null,
                                                  mesh.ColoursFor(m) >= 0 ? mesh.NameOfColours(mesh.ColoursFor(m)) : null })
                        .Where(n => !string.IsNullOrEmpty(n) && !have.Contains(NitroDictionary.Fit(n))).Distinct().ToList();
                    if (lacking.Count > 0) bad[id] = lacking;
                }
                if (bad.Count == 0) return null;
                return $"Buildings drawn pink, their textures are not in area {_areaId}'s building pack {area.buildingsTileset}: "
                     + string.Join("; ", bad.Take(4).Select(kv => $"{kv.Key} ({string.Join(", ", kv.Value.Take(3))})"))
                     + (bad.Count > 4 ? $" and {bad.Count - 4} more" : "") + ".";
            }
            catch (Exception ex) { AppLogger.Error("MapTiles.BuildingPictures: " + ex.Message); return null; }
        }

        /// <summary>Where the map's buildings stand, in squares, so painting under a house is not a surprise.</summary>
        public List<(int index, double x0, double z0, double x1, double z1)> BuildingSquares()
        {
            var found = new List<(int, double, double, double, double)>();
            if (_map?.buildings == null) return found;
            int number = -1;
            string dir;
            try { dir = BuildingModelsDir(); } catch { return found; }
            foreach (var b in _map.buildings)
            {
                number++;
                if (!_modelExtent.TryGetValue((dir, b.modelID), out var extent))
                {
                    extent = null;
                    try
                    {
                        string path = Path.Combine(dir, b.modelID.ToString("D4"));
                        var mesh = File.Exists(path) ? MapMesh.Read(File.ReadAllBytes(path), out _) : null;
                        if (mesh != null && mesh.Vertices.Count > 0)
                        {
                            float s = mesh.ModelScale == 0f ? 1f : mesh.ModelScale;
                            extent = (mesh.Vertices.Min(v => v.X) * s, mesh.Vertices.Max(v => v.X) * s,
                                      mesh.Vertices.Min(v => v.Z) * s, mesh.Vertices.Max(v => v.Z) * s);
                        }
                    }
                    catch { }
                    _modelExtent[(dir, b.modelID)] = extent;
                }
                if (extent is not var (ex0, ex1, ez0, ez1)) continue;
                // Sizes are scale factors where 16 is one; a quarter turn about y swaps across and down.
                double sx = Math.Max(1u, b.width) / 16.0, sz = Math.Max(1u, b.length) / 16.0;
                double a0 = ex0 * sx / 16, a1 = ex1 * sx / 16, c0 = ez0 * sz / 16, c1 = ez1 * sz / 16;
                int quarter = (int)Math.Round(Building.U16ToDeg(b.yRotation) / 90.0) & 3;
                if (quarter == 1 || quarter == 3) (a0, a1, c0, c1) = (c0, c1, -a1, -a0);
                if (quarter == 2) (a0, a1, c0, c1) = (-a1, -a0, -c1, -c0);
                double cx = TileGrid.Across / 2 + b.xPosition + b.xFraction / 65536.0;
                double cz = TileGrid.Across / 2 + b.zPosition + b.zFraction / 65536.0;
                found.Add((number, cx + a0, cz + c0, cx + a1, cz + c1));
            }
            return found;
        }

        private int _selectedBuilding = -1;
        /// <summary>The building picked on the grid, by its index in the map; -1 for none.</summary>
        public int SelectedBuilding
        {
            get => _selectedBuilding;
            set { if (Set(ref _selectedBuilding, value)) GridChanged?.Invoke(this, EventArgs.Empty); }
        }

        /// <summary>Moves warps standing at or in front of a moved building; returns how many moved.</summary>
        public Func<(double x0, double z0, double x1, double z1), int, int, int> WarpsFollow { get; set; }

        private (short x, short z, (double x0, double z0, double x1, double z1) was)? _buildingAtPress;

        public void PressBuilding(int index)
        {
            if (_map?.buildings == null || index < 0 || index >= _map.buildings.Count) return;
            SelectedBuilding = index;
            var b = _map.buildings[index];
            var rect = BuildingSquares().FirstOrDefault(r => r.index == index);
            _buildingAtPress = (b.xPosition, b.zPosition, (rect.x0, rect.z0, rect.x1, rect.z1));
            Note = $"Building {index:D2}, model {b.modelID}, at {b.xPosition},{b.zPosition}. Drag to move it.";
        }

        public void DragBuilding(int index, int dx, int dz)
        {
            if (_buildingAtPress is not (short x, short z, _) || index != _selectedBuilding) return;
            var b = _map.buildings[index];
            b.xPosition = (short)(x + dx);
            b.zPosition = (short)(z + dz);
            GridChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ReleaseBuilding(int index, int dx, int dz)
        {
            if (_buildingAtPress is not (_, _, var was) || index != _selectedBuilding) return;
            _buildingAtPress = null;
            if (dx == 0 && dz == 0) return;
            DragBuilding(index, dx, dz);
            Dirty = true;
            int moved = MovePermissions(was, dx, dz);
            int warps = WarpsFollow?.Invoke(was, dx, dz) ?? 0;
            var b = _map.buildings[index];
            Note = $"Moved building {index:D2} to {b.xPosition},{b.zPosition}"
                 + (moved > 0 ? $", its {moved} blocked squares" : "")
                 + (warps > 0 ? $" and {warps} warp{(warps > 1 ? "s" : "")}." : ".")
                 + " Save from the map editor.";
        }

        /// <summary>Moves blocked and typed squares under a building's old outline with it, leaving plain ground behind.</summary>
        private int MovePermissions((double x0, double z0, double x1, double z1) was, int dx, int dz)
        {
            if (_map?.collisions == null || _map.types == null) return 0;
            int n = TileGrid.Across;
            // Outlines and blocked squares rarely line up, so touching blocked squares join, up to a square and a bit out.
            bool Within(int x, int z, double reach)
                => x + 0.5 >= was.x0 - reach && x + 0.5 <= was.x1 + reach && z + 0.5 >= was.z0 - reach && z + 0.5 <= was.z1 + reach;
            bool Used(int x, int z) => _map.collisions[z, x] != 0 || _map.types[z, x] != 0;
            var taken = new HashSet<(int x, int z)>();
            var queue = new Queue<(int x, int z)>();
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                    if (Within(x, z, 0.6) && Used(x, z) && taken.Add((x, z))) queue.Enqueue((x, z));
            while (queue.Count > 0)
            {
                var (qx, qz) = queue.Dequeue();
                foreach (var (ox, oz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int x = qx + ox, z = qz + oz;
                    if (x < 0 || z < 0 || x >= n || z >= n || !Within(x, z, 1.2) || !Used(x, z) || !taken.Add((x, z))) continue;
                    queue.Enqueue((x, z));
                }
            }
            var carried = taken.Select(q => (q.x, q.z, walk: _map.collisions[q.z, q.x], type: _map.types[q.z, q.x])).ToList();
            foreach (var (x, z, _, _) in carried) { _map.collisions[z, x] = 0; _map.types[z, x] = 0; }
            int moved = 0;
            foreach (var (x, z, walk, type) in carried)
            {
                int tx = x + dx, tz = z + dz;
                if (tx < 0 || tz < 0 || tx >= n || tz >= n) continue;
                _map.collisions[tz, tx] = walk;
                _map.types[tz, tx] = type;
                moved++;
            }
            return moved;
        }

        public void OverBuilding(int? index)
        {
            if (index is not int i || _map?.buildings == null || i >= _map.buildings.Count) return;
            var b = _map.buildings[i];
            Under = $"Building {i:D2}  ·  model {b.modelID}  ·  at {b.xPosition},{b.zPosition}  ·  drag the outline to move it";
        }

        public float[,] ActiveHeights()
        {
            var h = new float[TileGrid.Across, TileGrid.Across];
            for (int z = 0; z < TileGrid.Across; z++)
                for (int x = 0; x < TileGrid.Across; x++)
                {
                    // Only where this layer has a tile, at the top you walk on.
                    var sq = _painter.Grid.At(x, z, _painter.Layer);
                    h[z, x] = sq.Tile < 0 ? float.NaN : SurfaceOf(_painter.Grid, x, z, _painter.Layer) / TileGrid.Step;
                }
            return h;
        }

        public IReadOnlyList<(int x, int z)> Pending
            => _painter.Pasting && _hover is (int hx, int hz) ? _painter.PasteCells(hx, hz) : _painter.Pending;

        public bool[,] Selection => _painter.Selection;
        private (int x, int z)? _hover;

        public bool HasSelection => _painter.Selection != null;
        public bool HasClipboard => _painter.HasClipboard;

        public void CopySelection() { _painter.CopySelection(); Raise(nameof(HasClipboard)); }
        public void CutSelection() { if (_painter.CutSelection()) Edited(); Raise(nameof(HasClipboard)); }
        public void DeleteSelection() { if (_painter.DeleteSelection()) Edited(); }
        public void FillSelection() { if (_painter.FillSelection()) Edited(); }
        public void RotateSelection() { if (_painter.TransformSelection(TilePainter.Transform.Rotate)) { Edited(); SelectionShown(); } }
        public void FlipSelectionAcross() { if (_painter.TransformSelection(TilePainter.Transform.FlipAcross)) { Edited(); SelectionShown(); } }
        public void FlipSelectionDown() { if (_painter.TransformSelection(TilePainter.Transform.FlipDown)) { Edited(); SelectionShown(); } }
        public void SelectAll() { _painter.SelectAll(); SelectionShown(); }
        public void SelectNone() { _painter.SelectNone(); _painter.CancelPaste(); SelectionShown(); }

        public void Paste()
        {
            if (!_painter.HasClipboard) return;
            _painter.StartPaste();
            Note = "Click to paste, right-click to cancel.";
            PendingChanged?.Invoke(this, EventArgs.Empty);
        }

        private void SelectionShown()
        {
            Raise(nameof(HasSelection));
            PendingChanged?.Invoke(this, EventArgs.Empty);
        }

        public (int x, int z, int across, int down)? Ghost { get; private set; }

        private void CountLayers()
        {
            for (int l = 0; l < TileGrid.Layers; l++) Layers[l].Painted = _painter.Grid.PaintedOn(l);
        }

        public void Press(int x, int z, TilePainter.Button button, bool shift = false, bool ctrl = false)
        {
            if (_set == null) return;
            int brush = Brush, turn = Turn, height = HeightBrush;
            bool changed = _painter.Press(x, z, button, shift, ctrl);
            Warning = _painter.Warning;
            if (Brush != brush || Turn != turn) { Raise(nameof(Brush)); Raise(nameof(Turn)); Describe(); ShowTile(); }
            if (HeightBrush != height) { Raise(nameof(HeightBrush)); Raise(nameof(HeightIndex)); }
            if (changed) GridChanged?.Invoke(this, EventArgs.Empty);
            Raise(nameof(HasSelection));
            PendingChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Drag(int x, int z)
        {
            bool changed = _painter.Drag(x, z);
            if (changed) GridChanged?.Invoke(this, EventArgs.Empty);
            PendingChanged?.Invoke(this, EventArgs.Empty);
            Over(x, z);
        }

        public void Release(int x, int z)
        {
            bool changed = _painter.Release(x, z);
            Warning = _painter.Warning ?? Warning;
            Raise(nameof(HasSelection));
            PendingChanged?.Invoke(this, EventArgs.Empty);
            if (changed) Edited();
        }

        private void Edited()
        {
            Dirty = true;
            CountLayers();
            CountUses();
            if (_set != null && (Warning == null || Warning.StartsWith("Missing textures", StringComparison.Ordinal)))
                Warning = MissingPictures(_set);
            Raise(nameof(CanUndoPaint));
            Raise(nameof(CanRedoPaint));
            Raise(nameof(HasCopy));
            GridChanged?.Invoke(this, EventArgs.Empty);
            Preview();
        }

        public void UndoPaint() { if (_painter.Undo()) Edited(); }
        public void RedoPaint() { if (_painter.Redo()) Edited(); }

        public void ShiftLayer(int dx, int dz) { if (_painter.ShiftLayer(dx, dz)) Edited(); }
        public void RaiseLayer(int steps) { if (_painter.RaiseLayer(steps)) Edited(); }
        public void ClearLayer() { if (_painter.ClearLayer()) Edited(); }
        public void CopyLayer() { _painter.CopyLayer(); Raise(nameof(HasCopy)); Note = $"Copied layer {ActiveLayer + 1}"; }
        public void PasteLayer() { if (_painter.PasteLayer()) Edited(); }

        public void Over(int? x, int? z)
        {
            var ghost = (ValueTuple<int, int, int, int>?)null;
            _hover = x != null && z != null ? (x.Value, z.Value) : null;
            if (_painter.Pasting) PendingChanged?.Invoke(this, EventArgs.Empty);
            if (_set == null || x == null || z == null) { Under = ""; SetGhost(null); return; }

            if (!ShowHeights && _painter.Current == TilePainter.Tool.Paint && Brush >= 0 && Brush < _set.Tiles.Count)
            {
                var tile = _set.Tiles[Brush];
                var (across, down) = TileGrid.Footprint(tile.Wide, tile.Deep, (byte)Turn);
                var (ax, az) = _painter.AnchorFor(x.Value, z.Value, Brush);
                ghost = (ax, az, across, down);
            }
            SetGhost(ghost);

            // Everything on the square, layer by layer, so it is clear what an edit will touch.
            string Said(int layer)
            {
                var q = _painter.Grid.At(x.Value, z.Value, layer);
                if (q.Tile < 0 || q.Tile >= _set.Tiles.Count) return null;
                var t = _set.Tiles[q.Tile];
                string what = string.IsNullOrEmpty(t.Name) ? t.Picture : t.Name;
                string size = t.Spreads && !what.Contains($"{t.Wide}x{t.Deep}") ? $" {t.Wide}x{t.Deep}" : "";
                return $"{layer + 1}: {what}{size}{(q.Turn > 0 ? $" {q.Turn * 90}°" : "")}"
                     + $" at {(int)Math.Round(SurfaceOf(_painter.Grid, x.Value, z.Value, layer) / TileGrid.Step)}";
            }
            var here = Enumerable.Range(0, TileGrid.Layers).Select(Said).Where(t => t != null).ToList();
            string active = Said(ActiveLayer) ?? $"{ActiveLayer + 1}: empty";
            var others = here.Where(t => !t.StartsWith($"{ActiveLayer + 1}:")).ToList();
            Under = $"{x},{z}  ·  layer {active}" + (others.Count > 0 ? $"  ·  also {string.Join(", ", others)}" : "")
                  + (here.Count == 0 ? "  ·  nothing here, a hole" : "");
        }

        private void SetGhost((int, int, int, int)? ghost)
        {
            if (Ghost == ghost) return;
            Ghost = ghost;
            PendingChanged?.Invoke(this, EventArgs.Empty);
        }

        private MapTileset.Tile Chosen => _set != null && Brush >= 0 && Brush < _set.Tiles.Count ? _set.Tiles[Brush] : null;

        private int _animationStep = 8;
        public int AnimationStep { get => _animationStep; set => Set(ref _animationStep, Math.Clamp(value, 1, 255)); }

        public bool CanAnimate => FieldTextureAnimations.Available && !string.IsNullOrEmpty(_lookPicture);
        public bool IsAnimated => CanAnimate && MatrixSceneBuilder.FieldAnimations()?.For(_lookPicture) != null;

        public string AnimationNote
        {
            get
            {
                if (!CanAnimate) return "";
                var entry = MatrixSceneBuilder.FieldAnimations()?.For(_lookPicture);
                return entry == null ? $"{_lookPicture} is not animated."
                    : $"{_lookPicture}: {entry.Frames.Count} frames, {string.Join(" ", entry.Frames.Select(f => f.Duration))} steps.";
            }
        }

        /// <summary>Makes the chosen picture animate in game with these frames, drawn in its own format and palette.</summary>
        public string AnimateChosen(IReadOnlyList<string> framePaths)
        {
            if (!CanAnimate) return "No picture chosen.";
            string name = _lookPicture;
            try
            {
                string pack = Path.Combine(gameDirs[DirNames.mapTextures].unpackedDir, new AreaData(_areaId).mapTileset.ToString("D4"));
                NSBTXLoader.LoadNsbtx(new MemoryStream(File.ReadAllBytes(pack)), out var textures, out var palettes);
                var target = textures?.FirstOrDefault(t => t.texname == name);
                if (target == null) return Fail($"{name} is not in this area's textures. Add textures to ROM first.");

                var face = _set.Tiles.SelectMany(t => t.Faces).FirstOrDefault(f => f.Picture == name && !string.IsNullOrEmpty(f.Palette));
                var palette = palettes?.FirstOrDefault(pl => pl.palname == (face?.Palette ?? name))
                              ?? palettes?.FirstOrDefault(pl => pl.palname == name + "_pl") ?? palettes?.FirstOrDefault(pl => pl.palname == name);
                var colours = palette?.paldata?.Select(c => (ushort)((c.R >> 3) | ((c.G >> 3) << 5) | ((c.B >> 3) << 10))).ToArray()
                              ?? Array.Empty<ushort>();

                var frames = new List<DsTexture>();
                for (int i = 0; i < framePaths.Count; i++)
                {
                    if (!AnyPng.TryReadRgba(File.ReadAllBytes(framePaths[i]), out byte[] rgba, out int w, out int h, out _))
                        return Fail($"{Path.GetFileName(framePaths[i])} could not be read.");
                    if (w != target.width || h != target.height)
                        return Fail($"{Path.GetFileName(framePaths[i])} is {w}x{h}; {name} is {target.width}x{target.height}.");
                    byte[] texels = DsTexture.TexelsLike(rgba, w, h, target.format, colours, target.color0 != 0, out string whynot);
                    if (texels == null) return Fail(whynot);
                    frames.Add(new DsTexture { Name = $"{name}.{i + 1}", Width = w, Height = h, Format = (DsTexture.Kind)target.format,
                                               Pixels = texels, Colours = colours, FirstColourIsClear = target.color0 != 0 });
                }
                if (frames.Count == 0 || frames.Count > FieldTextureAnimations.MostFrames)
                    return Fail($"Give 1 to {FieldTextureAnimations.MostFrames} frames.");

                var built = NsbtxWriter.Build(frames);
                if (built.Whynot != null) return Fail(built.Whynot);

                var list = MatrixSceneBuilder.FieldAnimations(reload: true);
                var entry = list.For(name);
                if (entry == null) { entry = new FieldTextureAnimations.Entry { Name = name }; list.Entries.Add(entry); }
                entry.FramePack = built.Bytes;
                entry.Frames = Enumerable.Range(0, frames.Count).Select(i => ((byte)i, (byte)AnimationStep)).ToList();
                list.Save();
                MatrixSceneBuilder.FieldAnimations(reload: true);

                int inPack = textures.Count(t => list.For(t.texname) != null);
                Warning = inPack > 16 ? $"{inPack} of this area's textures are animated; the game animates the first 16." : null;
                Note = $"{name} animates with {frames.Count} frames. Save the ROM to write it.";
            }
            catch (Exception ex) { return Fail(ex.Message); }

            Rebuild();
            Raise(nameof(AnimationNote)); Raise(nameof(IsAnimated));
            return null;
        }

        public void StopAnimatingChosen()
        {
            var list = MatrixSceneBuilder.FieldAnimations(reload: true);
            var entry = list?.For(_lookPicture);
            if (entry == null) return;
            list.Entries.Remove(entry);
            list.Save();
            MatrixSceneBuilder.FieldAnimations(reload: true);
            Note = $"{_lookPicture} no longer animates.";
            Rebuild();
            Raise(nameof(AnimationNote)); Raise(nameof(IsAnimated));
        }

        private string Fail(string why) { Warning = why; return why; }

        public bool HasChosen => Chosen != null;

        public string TileName
        {
            get => Chosen?.Name ?? "";
            set
            {
                if (Chosen == null || string.IsNullOrWhiteSpace(value) || Chosen.Name == value) return;
                Chosen.Name = value.Trim();
                SetChanged(redraw: false);
            }
        }

        public int TileWide
        {
            get => Chosen?.Wide ?? 1;
            set => Resize(Math.Clamp(value, 1, MapTileset.MostSquares), Chosen?.Deep ?? 1);
        }

        public int TileDeep
        {
            get => Chosen?.Deep ?? 1;
            set => Resize(Chosen?.Wide ?? 1, Math.Clamp(value, 1, MapTileset.MostSquares));
        }

        private void Resize(int wide, int deep)
        {
            var tile = Chosen;
            if (tile == null || (tile.Wide == wide && tile.Deep == deep)) return;
            tile.Wide = wide; tile.Deep = deep;
            int dropped = 0;
            foreach (var g in _painter.AllGrids) dropped += g.Resized(Brush, wide, deep);
            if (dropped > 0) Warning = "";
            SetChanged(redraw: true);
        }

        public void NudgeTile(int dx, int dy, int dz)
        {
            if (Chosen == null) return;
            float step = MapTileset.TileWidth / 8f;
            MapTileset.Nudge(Chosen, dx * step, dy * step, dz * step);
            SetChanged(redraw: true);
        }

        public void TurnTileShape()
        {
            if (Chosen == null) return;
            MapTileset.TurnShape(Chosen);
            foreach (var g in _painter.AllGrids) g.Resized(Brush, Chosen.Wide, Chosen.Deep);
            SetChanged(redraw: true);
        }

        public void MirrorTile()
        {
            if (Chosen == null) return;
            MapTileset.Mirror(Chosen);
            SetChanged(redraw: true);
        }

        public void DuplicateTile()
        {
            if (_set == null) return;
            int made = _set.DuplicateTile(Brush);
            if (made < 0) return;
            SetChanged(redraw: false);
            Brush = made;
        }

        public void RemoveTile()
        {
            if (_set == null || Chosen == null) return;
            int gone = Brush;
            _set.RemoveTile(gone, _painter.AllGrids.ToArray());
            _fromAbove.Clear();
            _thumbs.Clear();
            SetChanged(redraw: true);
            Brush = Math.Min(gone, _set.Tiles.Count - 1);
        }

        public void MoveTile(int by)
        {
            if (_set == null || Chosen == null) return;
            int to = Brush + by;
            if (to < 0 || to >= _set.Tiles.Count) return;
            _set.SwapTiles(Brush, to, _painter.AllGrids.ToArray());
            _fromAbove.Clear();
            _thumbs.Clear();
            int now = to;
            SetChanged(redraw: true);
            Brush = now;
        }

        public void AddTilesFrom(string path)
        {
            if (_set == null) return;
            bool theirs = Path.GetExtension(path).Equals(".pdsts", StringComparison.OrdinalIgnoreCase);
            var other = theirs ? PdstsFile.Read(path, out string whynot) : MapTileset.FromObj(path, out whynot);
            if (other == null) { Warning = whynot; return; }
            if (theirs) TileCollisions.ReadMeta(path, other);
            SizePlaces(other);
            var places = theirs ? TileCollisions.ListedPlaces(other) : null;
            int first = _set.Append(other);
            if (theirs)
                for (int i = first; i < _set.Tiles.Count; i++)
                    if (places.TryGetValue(other.Tiles[i - first], out int listed)) _tileSource[_set.Tiles[i]] = (path, listed);
            if (other.PictureFiles.Count > 0)
                for (int i = first; i < _set.Tiles.Count; i++) _ownTiles.Add(i);
            foreach (var kv in PicturesOfTheSet(other)) _pictures.TryAdd(kv.Key, kv.Value);
            SetChanged(redraw: false);
            FillSmart();
            Brush = first;
            Note = $"Appended {other.Tiles.Count} tiles.";
            Warning = MissingPictures(_set);
            Raise(nameof(HasOwnPictures));
            Raise(nameof(SetHasMovement));
        }

        public bool TileJoinsAcross { get => Chosen?.AcrossTileable ?? false; set => Flag(t => t.AcrossTileable = value); }
        public bool TileJoinsDown { get => Chosen?.DownTileable ?? false; set => Flag(t => t.DownTileable = value); }
        public bool TileRepeatsAcross { get => Chosen?.PictureRepeatsAcross ?? false; set => Flag(t => t.PictureRepeatsAcross = value); }
        public bool TileRepeatsDown { get => Chosen?.PictureRepeatsDown ?? false; set => Flag(t => t.PictureRepeatsDown = value); }
        public bool TileAcrossTheMap { get => Chosen?.PictureAcrossTheMap ?? false; set => Flag(t => t.PictureAcrossTheMap = value); }

        private void Flag(Action<MapTileset.Tile> change)
        {
            if (Chosen == null) return;
            change(Chosen);
            SetChanged(redraw: false);
        }

        public string TileGroundKind { get => Hex(Chosen?.WholeCollision(TileCollisions.TypeLayer)); set => SetMovement(TileCollisions.TypeLayer, value); }
        public string TileWalk { get => Hex(Chosen?.WholeCollision(TileCollisions.CollisionLayer)); set => SetMovement(TileCollisions.CollisionLayer, value); }

        /// <summary>One square of the chosen tile's footprint in the collision grid.</summary>
        public sealed class CollisionCell : INotifyPropertyChanged
        {
            public int Index { get; set; }
            public int X, Y;
            private string _text;
            public string Text { get => _text; set { _text = value; Changed(); } }
            private global::Avalonia.Media.IBrush _fill;
            public global::Avalonia.Media.IBrush Fill { get => _fill; set { _fill = value; Changed(); } }
            private global::Avalonia.Media.IBrush _edge;
            public global::Avalonia.Media.IBrush Edge { get => _edge; set { _edge = value; Changed(); } }
            private string _tip;
            public string Tip { get => _tip; set { _tip = value; Changed(); } }
            public event PropertyChangedEventHandler PropertyChanged;
            private void Changed([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        }

        public ObservableCollection<CollisionCell> CollisionCells { get; } = new ObservableCollection<CollisionCell>();
        public int CollisionColumns => Chosen?.FootprintWide ?? 1;
        public int CollisionRows => Chosen?.FootprintDeep ?? 1;

        // Choices use the names the Map Editor paints with; "No default" leaves the map's own value alone.
        public ObservableCollection<string> WalkChoices { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> TypeChoices { get; } = new ObservableCollection<string>();
        private readonly List<int> _walkValues = new List<int>(), _typeValues = new List<int>();

        private static void Choices(ObservableCollection<string> names, List<int> values, IDictionary<byte, string> table, int also)
        {
            if (names.Count == 0)
            {
                names.Add("No default"); values.Add(-1);
                foreach (var kv in table.OrderBy(kv => kv.Key)) { names.Add(kv.Value); values.Add(kv.Key); }
            }
            if (also >= 0 && !values.Contains(also)) { names.Add($"[{also:X2}]"); values.Add(also); }
        }

        private int _chosenCell;
        public int ChosenCell
        {
            get => _chosenCell;
            set
            {
                if (value < 0 || value >= CollisionCells.Count) return;
                _chosenCell = value;
                FillCollisionCells();
            }
        }

        private int CellValue(int layer)
        {
            if (Chosen == null || _chosenCell >= CollisionCells.Count) return -1;
            var grid = Chosen.CollisionGrid(layer);
            var cell = CollisionCells[_chosenCell];
            return cell.X < grid.GetLength(0) && cell.Y < grid.GetLength(1) ? grid[cell.X, cell.Y] : -1;
        }

        private void SetCellValue(int layer, int value)
        {
            if (Chosen == null || _chosenCell >= CollisionCells.Count) return;
            var grid = Chosen.CollisionGrid(layer);
            var cell = CollisionCells[_chosenCell];
            if (grid[cell.X, cell.Y] == value) return;
            grid[cell.X, cell.Y] = value;
            Dirty = true;
            if (_tileSource.ContainsKey(Chosen)) CollisionUnsaved = true;
            Raise(nameof(SetHasMovement));
            FillCollisionCells();
        }

        public int CellWalkIndex
        {
            get => _walkValues.IndexOf(CellValue(TileCollisions.CollisionLayer));
            set { if (value >= 0 && value < _walkValues.Count) SetCellValue(TileCollisions.CollisionLayer, _walkValues[value]); }
        }

        public int CellTypeIndex
        {
            get => _typeValues.IndexOf(CellValue(TileCollisions.TypeLayer));
            set { if (value >= 0 && value < _typeValues.Count) SetCellValue(TileCollisions.TypeLayer, _typeValues[value]); }
        }

        private static readonly global::Avalonia.Media.IBrush NoDefault = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.FromRgb(0x2A, 0x2D, 0x35));
        private static readonly global::Avalonia.Media.IBrush CellEdge = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.FromRgb(0x12, 0x14, 0x1A));

        private void FillCollisionCells()
        {
            int wide = CollisionColumns, deep = CollisionRows;
            if (Chosen == null || CollisionCells.Count != wide * deep)
            {
                CollisionCells.Clear();
                if (Chosen != null)
                    for (int y = 0; y < deep; y++)
                        for (int x = 0; x < wide; x++) CollisionCells.Add(new CollisionCell { Index = CollisionCells.Count, X = x, Y = y });
                _chosenCell = Math.Clamp(_chosenCell, 0, Math.Max(0, CollisionCells.Count - 1));
            }
            if (Chosen != null)
            {
                var walk = Chosen.CollisionGrid(TileCollisions.CollisionLayer);
                var type = Chosen.CollisionGrid(TileCollisions.TypeLayer);
                string Name(IDictionary<byte, string> table, int v) => v < 0 ? "no default" : table.TryGetValue((byte)v, out var n) ? n : $"[{v:X2}]";
                foreach (var cell in CollisionCells)
                {
                    int w = walk[cell.X, cell.Y], t = type[cell.X, cell.Y];
                    Choices(WalkChoices, _walkValues, PokeDatabase.System.MapCollisionPainters, w);
                    Choices(TypeChoices, _typeValues, PokeDatabase.System.MapCollisionTypePainters, t);
                    cell.Text = (w switch { -1 => "-", 0x00 => "Walk", 0x80 => "Block", _ => w.ToString("X2") })
                              + "\n" + (t < 0 ? "-" : t.ToString("X2"));
                    cell.Fill = w < 0 ? NoDefault : PermissionColors.Brush((byte)w, true);
                    cell.Edge = cell.Index == _chosenCell ? global::Avalonia.Media.Brushes.White : CellEdge;
                    cell.Tip = $"{Name(PokeDatabase.System.MapCollisionPainters, w)} / {Name(PokeDatabase.System.MapCollisionTypePainters, t)}";
                }
            }
            Raise(nameof(CollisionColumns));
            Raise(nameof(CollisionRows));
            Raise(nameof(ChosenFromTileset));
            Raise(nameof(CellWalkIndex));
            Raise(nameof(CellTypeIndex));
        }

        private static string Hex(int? value) => value switch { null or -1 => "", -2 => "mixed", int v => v.ToString("X2") };

        private void SetMovement(int layer, string said)
        {
            if (Chosen == null) return;
            said = (said ?? "").Trim();
            if (said == "mixed") return;
            if (said.Length == 0) Chosen.SetWholeCollision(layer, -1);
            else if (int.TryParse(said, System.Globalization.NumberStyles.HexNumber, null, out int v) && v >= 0 && v <= 0xff)
                Chosen.SetWholeCollision(layer, v);
            else { Warning = $"Not a hex byte: {said}"; return; }
            Dirty = true;
            Raise(nameof(SetHasMovement));
        }

        public ObservableCollection<string> TilePictures { get; } = new ObservableCollection<string>();

        private string _lookPicture;
        public string LookPicture
        {
            get => _lookPicture;
            set { if (_lookPicture == value) return; _lookPicture = value; Raise(); RaiseLook(); }
        }

        private MaterialLook CurrentLook
            => Chosen?.Faces.FirstOrDefault(f => f.Picture == _lookPicture)?.Look ?? MaterialLook.Plain;

        public int LookAlpha { get => CurrentLook.Alpha; set => ChangeLook(l => l.With(alpha: value)); }
        public bool LookBothSides { get => CurrentLook.BothSides; set => ChangeLook(l => l.With(bothSides: value)); }
        public bool LookFog { get => CurrentLook.Fog; set => ChangeLook(l => l.With(fog: value)); }
        public bool LookLight0 { get => (CurrentLook.Lights & 1) != 0; set => Light(0, value); }
        public bool LookLight1 { get => (CurrentLook.Lights & 2) != 0; set => Light(1, value); }
        public bool LookLight2 { get => (CurrentLook.Lights & 4) != 0; set => Light(2, value); }
        public bool LookLight3 { get => (CurrentLook.Lights & 8) != 0; set => Light(3, value); }

        private void Light(int which, bool on)
        {
            int lights = CurrentLook.Lights;
            lights = on ? lights | (1 << which) : lights & ~(1 << which);
            ChangeLook(l => l.With(lights: lights));
        }

        private void ChangeLook(Func<MaterialLook, MaterialLook> change)
        {
            if (_set == null || string.IsNullOrEmpty(_lookPicture)) return;
            _set.ChangeLook(_lookPicture, change);
            RaiseLook();
            SetChanged(redraw: false);
        }

        private void RaiseLook()
        {
            foreach (string n in new[] { nameof(LookAlpha), nameof(LookBothSides), nameof(LookFog),
                                         nameof(LookLight0), nameof(LookLight1), nameof(LookLight2), nameof(LookLight3) })
                Raise(n);
        }

        private void ShowChosen()
        {
            TilePictures.Clear();
            if (Chosen != null) foreach (string p in Chosen.Pictures) TilePictures.Add(p);
            _lookPicture = TilePictures.FirstOrDefault();
            foreach (string n in new[] { nameof(HasChosen), nameof(TileName), nameof(TileWide), nameof(TileDeep), nameof(LookPicture),
                                         nameof(TileJoinsAcross), nameof(TileJoinsDown), nameof(TileRepeatsAcross), nameof(TileRepeatsDown),
                                         nameof(TileAcrossTheMap), nameof(TileGroundKind), nameof(TileWalk), nameof(SetHasMovement),
                                         nameof(AnimationNote), nameof(IsAnimated), nameof(CanAnimate) })
                Raise(n);
            RaiseLook();
            FillCollisionCells();
        }

        private void SetChanged(bool redraw)
        {
            Dirty = true;
            if (redraw)
            {
                _fromAbove.Clear();
                _thumbs.Clear();
            }
            else if (Brush >= 0)
            {
                _thumbs.Remove(Brush);
                foreach (var key in _fromAbove.Keys.Where(k => k.tile == Brush).ToList()) _fromAbove.Remove(key);
            }
            FillList();
            FillSlots();
            ShowChosen();
            ShowTile();
            CountLayers();
            GridChanged?.Invoke(this, EventArgs.Empty);
            Preview();
        }

        private void Preview()
        {
            if (_set == null || _map == null) return;

            var baked = TileBake.Of(_painter.Grid, _set);
            if (baked.Whynot != null) { Warning = baked.Whynot; Model3D = null; Raise(nameof(Model3D)); Changed?.Invoke(this, EventArgs.Empty); return; }
            PutBackUntouched(baked, _ramps);
            TileBake.JoinFlat(baked);

            byte[] model = TileBake.ToModel(baked, out string whynot, TileScale);
            if (model == null) { Warning = whynot; return; }

            try
            {
                var shown = new MapFile(new MemoryStream(_map.ToByteArray()), _family,
                                        discardMoveperms: false, showMessages: false);
                shown.LoadMapModel(model, showMessages: false);
                shown.mapModelData = model;

                Model3D = MatrixSceneBuilder.BuildFromPlaced(_family,
                    new[] { (0, 0, shown, _areaId, 0f, 0f, 0f) },
                    NsbmdGeometry.MatrixStitchMode.Grid);
            }
            catch (Exception ex) { AppLogger.Error("MapTiles.Preview: " + ex.Message); }

            Note = $"{_painter.Grid.Painted} cells  ·  {baked.Faces.Count} faces  ·  "
                 + $"{baked.Triangles} triangles  ·  {model.Length:n0} bytes";
            Raise(nameof(Model3D));
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private void Rebuild()
        {
            Model3D = null;
            if (_map == null) return;
            try
            {
                Model3D = MatrixSceneBuilder.BuildFromPlaced(_family,
                    new[] { (0, 0, _map, _areaId, 0f, 0f, 0f) },
                    NsbmdGeometry.MatrixStitchMode.Grid);
            }
            catch (Exception ex) { AppLogger.Error("MapTiles.Rebuild: " + ex.Message); }
        }

        // Squares whose tiles changed since From map; everything counts as changed for an import or a new set.
        private Func<int, int, bool> ChangedSquares => _ripped == null ? null : (c, r) => ChangedSinceRipped(c, r);

        /// <summary>How many of the map's current plates Apply's terrain rebuild would replace.</summary>
        public int PlatesApplyWouldReplace()
        {
            if (_map?.bdhc == null || _set == null || !AlsoTerrain || (AlsoSaved && _saved.ContainsKey("bdhc") && !LeavesOut("bdhc"))) return 0;
            var baked = TileBake.Of(_painter.Grid, _set);
            if (baked.Whynot != null) return 0;
            PutBackUntouched(baked, _ramps);
            TileBake.JoinFlat(baked);
            if (AlsoWalls) TileBake.AddWalls(baked, _painter.Grid, _set, HeightChanged, _ramps, null,
                                             (x, z) => WalkedLayer(_painter.Grid, x, z), (x, z, l) => SurfaceOf(_painter.Grid, x, z, l));
            byte[] model = TileBake.ToModel(baked, out _, TileScale);
            var mesh = model == null ? null : MapMesh.Read(model, out _);
            if (mesh == null) return 0;
            var changed = ChangedSquares;
            Func<int, int, bool> changedOrRamp = changed == null ? null : (c, r) => changed(c, r) || _ramps.Contains((c, r));
            return BdhcBuild.Replaced(_map.bdhc, BdhcBuild.Propose(mesh, _map.collisions, _map.bdhc, _map.KeptPlates, changedOrRamp,
                                                                       WantedGround(new HashSet<(int x, int z)>(_ramps))));
        }

        /// <summary>True once a PDSMS map has been applied since the window opened.</summary>
        public bool ImportedSinceOpen { get; set; }

        // The height a changed square is meant to be walked at, in map units: its ground tile, or a ramp's middle.
        private Func<int, int, float?> WantedGround(ISet<(int x, int z)> ramps)
        {
            var grid = _painter.Grid;
            float toUnits = 16f / MapTileset.TileWidth;
            float? Ground(int x, int z)
            {
                if (x < 0 || z < 0 || x >= TileGrid.Across || z >= TileGrid.Across) return null;
                int l = GroundLayer(grid, x, z);
                if (l < 0) return null;
                return SurfaceOf(grid, x, z, l);
            }
            return (c, r) =>
            {
                if (_ripped != null && !ChangedSinceRipped(c, r) && !ramps.Contains((c, r))) return null;
                if (Ground(c, r) is not float h) return null;
                if (ramps.Contains((c, r)))
                {
                    float up = h;
                    foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                        if (Ground(c + dx, r + dz) is float n && n > up) up = n;
                    h = (h + up) / 2f;
                }
                return (grid.BaseLift + h) * toUnits;
            };
        }

        public void Apply()
        {
            if (_map == null || _set == null) return;
            if (_project != null) ImportedSinceOpen = true;

            // A ramp square's flat ground is left out and a slope laid in its place.
            var bakeGrid = _painter.Grid;
            var ramps = new HashSet<(int x, int z)>();
            var cannot = new List<(int x, int z)>();
            foreach (var (x, z) in _ramps)
            {
                int l = GroundLayer(_painter.Grid, x, z);
                var sq = l < 0 ? default : _painter.Grid.At(x, z, l);
                // A ramp replaces its square's ground, which a tile spanning several squares cannot give up alone.
                if (l < 0 || sq.Wide > 1 || sq.Deep > 1) { cannot.Add((x, z)); continue; }
                if (ReferenceEquals(bakeGrid, _painter.Grid)) bakeGrid = _painter.Grid.Clone();
                bakeGrid.Clear(x, z, l);
                ramps.Add((x, z));
            }
            var baked = TileBake.Of(bakeGrid, _set);
            if (baked.Whynot != null) { Warning = baked.Whynot; return; }
            PutBackUntouched(baked, ramps);
            TileBake.JoinFlat(baked);
            var runs = _rampRuns.Select(run => new TileRamps.Run
            {
                Squares = new HashSet<(int x, int z)>(run.Squares.Where(ramps.Contains)),
                Kind = run.Kind, Fill = run.Fill, Template = run.Template,
            }).Where(run => run.Squares.Count > 0).ToList();
            var laid = TileRamps.Lay(baked, _painter.Grid, runs, (x, z) =>
            {
                int l = GroundLayer(_painter.Grid, x, z);
                if (l < 0) return (0f, null);
                var sq = _painter.Grid.At(x, z, l);
                var tile = _set.Tiles[sq.Tile];
                float height = SurfaceOf(_painter.Grid, x, z, l), top = height - sq.Lift;
                // Use the ground face, not a decal a sliver above it or a hidden surface below.
                var skin = tile.GroundFace ?? tile.Faces.OrderBy(f => f.Corners.Max(i => Math.Abs(tile.Corners[i].Y - top))).FirstOrDefault();
                return (height, skin);
            });
            int rampsMade = laid.Made;
            var belowWalls = new List<(int x, int z)>();
            int walls = AlsoWalls ? TileBake.AddWalls(baked, _painter.Grid, _set, HeightChanged, ramps, belowWalls,
                                                      (x, z) => WalkedLayer(_painter.Grid, x, z), (x, z, l) => SurfaceOf(_painter.Grid, x, z, l)) : 0;

            byte[] model = TileBake.ToModel(baked, out string whynot, TileScale);
            if (model == null) { Warning = whynot; return; }

            _before.Add((byte[])_map.ToByteArray().Clone());
            if (_before.Count > 32) _before.RemoveAt(0);
            Raise(nameof(CanUndo));

            if (MapFile.TooBigForTheGame(model.Length, 0) is string tooBig) { Warning = tooBig + " Remove detail or split the map."; return; }
            _map.LoadMapModel(model, showMessages: false);
            _map.mapModelData = model;
            _knownModel = model;

            var brought = new List<string>();
            bool savedGround = false, savedMovement = false;
            if (AlsoSaved && _saved.Count > 0)
            {
                try
                {
                    if (_saved.TryGetValue("per", out string per)) { _map.ImportPermissions(File.ReadAllBytes(per)); savedMovement = true; brought.Add("permissions"); }
                    if (_saved.TryGetValue("bld", out string bld) && !LeavesOut("bld")) { _map.ImportBuildings(File.ReadAllBytes(bld)); brought.Add($"{_map.buildings.Count} buildings"); }
                    if (_saved.TryGetValue("bgs", out string bgs)) { _map.ImportSoundPlates(File.ReadAllBytes(bgs)); brought.Add("BGS"); }
                    if (_saved.TryGetValue("bdhc", out string bdhc) && !LeavesOut("bdhc")) { _map.ImportTerrain(File.ReadAllBytes(bdhc)); savedGround = true; brought.Add("BDHC"); }
                }
                catch (Exception ex) { Warning = "Could not read PDSMS files: " + ex.Message; }
            }

            int stamped = AlsoMovement && SetHasMovement && !savedMovement
                ? TileCollisions.Apply(_painter.Grid, _set, _map.types, _map.collisions,
                                        _ripped == null ? null : ChangedSinceRipped) : 0;

            // A wall is a cliff: nobody walks up or down it, so the square at its foot is blocked.
            int cliffs = 0;
            foreach (var (x, z) in belowWalls.Distinct())
                if ((_map.collisions[z, x] & 0x80) == 0) { _map.collisions[z, x] = 0x80; cliffs++; }
            foreach (var (x, z) in ramps)
                if ((_map.collisions[z, x] & 0x80) != 0) _map.collisions[z, x] = 0x00;

            int ungrounded = 0;
            if (AlsoTerrain && !savedGround)
            {
                var mesh = MapMesh.Read(model, out _);
                if (mesh != null)
                {
                    var changed = ChangedSquares;
                    Func<int, int, bool> changedOrRamp = changed == null ? null : (c, r) => changed(c, r) || ramps.Contains((c, r));
                    // Ramps and stairs are walked on the plates laid with them, whatever steps are drawn.
                    var kept = (_map.KeptPlates ?? new List<BdhcBuild.Piece>()).Concat(laid.Plates).ToList();
                    byte[] terrain = BdhcBuild.ForMap(mesh, _map.collisions, _map.bdhc, out _, kept, changedOrRamp, WantedGround(ramps));
                    if (terrain != null) _map.ImportTerrain(terrain);
                    ungrounded = BdhcBuild.BlockUngrounded(_map.bdhc, _map.collisions);
                }
            }

            Dirty = true;
            if (brought.Count == 0 || Warning?.StartsWith("What Map Studio") != true) Warning = null;
            var notBuilt = new List<string>(laid.Skipped);
            if (cannot.Count > 0)
                notBuilt.Add($"{cannot.Count} ramp square{(cannot.Count > 1 ? "s" : "")} ({string.Join(" ", cannot.Take(6).Select(c => $"{c.x},{c.z}"))}"
                           + $"{(cannot.Count > 6 ? " ..." : "")}) with no ground or on a tile wider than a square");
            if (notBuilt.Count > 0) Warning = "Not built: " + string.Join("; ", notBuilt) + ".";
            string bare = BuildingPicturesMissing();
            if (bare != null) Warning = Warning == null ? bare : Warning + " " + bare;
            Rebuild();
            Note = $"Applied: {baked.Faces.Count} faces, {model.Length:n0} bytes"
                 + (rampsMade > 0 ? $", {rampsMade} ramps" : "")
                 + (walls > 0 ? $", {walls} walls" : "")
                 + (cliffs + ungrounded > 0 ? $", {cliffs + ungrounded} squares blocked" : "")
                 + (AlsoTerrain && !savedGround ? ", terrain rebuilt" : "")
                 + (stamped > 0 ? $", {stamped} collision values" : "")
                 + (brought.Count > 0 ? ", PDSMS " + string.Join(", ", brought) : "");
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Undo()
        {
            if (_before.Count == 0 || _map == null) return;

            byte[] whole = _before[_before.Count - 1];
            _before.RemoveAt(_before.Count - 1);
            Raise(nameof(CanUndo));

            try
            {
                var was = new MapFile(new MemoryStream(whole), _family,
                                      discardMoveperms: false, showMessages: false);
                _map.LoadMapModel(was.mapModelData, showMessages: false);
                _map.mapModelData = was.mapModelData;
                _knownModel = was.mapModelData;
                _map.ImportTerrain(was.bdhc);
                Array.Copy(was.types, _map.types, was.types.Length);
                Array.Copy(was.collisions, _map.collisions, was.collisions.Length);
                _map.buildings = was.buildings;
                _map.bgs = was.bgs;
            }
            catch (Exception ex) { Warning = ex.Message; return; }

            Dirty = _before.Count > 0;
            Rebuild();
            Note = "Reverted.";
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private void ShowTile()
        {
            Tile3D = null;
            if (_set != null && _map != null && Brush >= 0 && Brush < _set.Tiles.Count)
            {
                try
                {
                    var tile = _set.Tiles[Brush];
                    var one = new TileGrid();
                    one.Put(12, 12, Brush, 0f, (byte)Turn, tile.Wide, tile.Deep);

                    byte[] model = TileBake.ToModel(TileBake.Of(one, _set), out _, TileScale);
                    if (model != null)
                    {
                        var shown = new MapFile(new MemoryStream(_map.ToByteArray()), _family,
                                                discardMoveperms: false, showMessages: false);
                        shown.LoadMapModel(model, showMessages: false);
                        shown.mapModelData = model;
                        shown.buildings.Clear();

                        Tile3D = MatrixSceneBuilder.BuildFromPlaced(_family,
                            new[] { (0, 0, shown, _areaId, 0f, 0f, 0f) },
                            NsbmdGeometry.MatrixStitchMode.Grid);
                    }
                }
                catch (Exception ex) { AppLogger.Error("MapTiles.ShowTile: " + ex.Message); }
            }

            Raise(nameof(Tile3D));
            TileShown?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler TileShown;

        private const float TileScale = 64f;

        private void Describe()
        {
            if (_set == null || Brush < 0 || Brush >= _set.Tiles.Count) return;
            var tile = _set.Tiles[Brush];
            Note = $"{tile.Name}  ·  {string.Join(", ", tile.Pictures)}"
                 + (tile.Spreads ? $"  ·  {tile.Wide}x{tile.Deep}" : "");
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
