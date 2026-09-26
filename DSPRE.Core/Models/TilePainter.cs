using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.Models
{
    /// <summary>Paint tools with PDSMS behaviour, plus undo/redo.</summary>
    public sealed class TilePainter
    {
        public enum Tool { Paint, Clear, Smart, SmartInverted, Bucket, Picker, Line, Rectangle, Ellipse, Select, Lasso, Wand }

        public enum Button { Left, Right, Middle }

        public TileGrid Grid { get; private set; } = new TileGrid();
        public MapTileset Set { get; private set; }

        /// <summary>Where the ground on a square and layer is walked, when the caller knows better than the tile.</summary>
        public Func<int, int, int, float> GroundSurface;

        public Tool Current = Tool.Paint;

        public int Layer;

        public int Brush = -1;

        public byte Turn;

        public int HeightBrush;

        public bool Heights;

        // A height lifts every layer on the square, so objects on upper layers rise with the ground.
        public bool HeightsAllLayers = true;

        public bool SmartTools = true;

        public int SmartIndex;

        public SmartDrawing Drawing
            => Set != null && SmartIndex >= 0 && SmartIndex < Set.SmartDrawings.Count ? Set.SmartDrawings[SmartIndex] : null;

        public string Warning { get; private set; }

        public IReadOnlyList<(int x, int z)> Pending => _pending;
        private List<(int x, int z)> _pending = new List<(int x, int z)>();

        private readonly List<TileGrid> _undo = new List<TileGrid>();
        private readonly List<TileGrid> _redo = new List<TileGrid>();
        private const int MostUndo = 100;

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;

        private TileGrid _before;
        private Button _button;
        private bool _pressed;
        private (int x, int z) _start;
        private List<(int x, int z)> _stroke;
        private bool _strokeInverted;
        private int _strokeTile;
        private TileGrid _strokeBase;
        private bool _shapeInverted;
        private enum Doing { Nothing, Placing, Clearing, Heights, SmartStroke, Shape, Selecting, Moving, Lassoing }
        private Doing _doing;

        public void Load(TileGrid grid, MapTileset set)
        {
            Grid = grid ?? new TileGrid();
            Set = set;
            _undo.Clear();
            _redo.Clear();
            _pending.Clear();
            _pressed = false;
            SmartIndex = 0;
            Warning = null;
        }

        private MapTileset.Tile BrushTile
            => Set != null && Brush >= 0 && Brush < Set.Tiles.Count ? Set.Tiles[Brush] : null;

        public bool SmartStroke => SmartTools && Drawing != null && Drawing.Holds(Brush);

        public bool SmartShapes => SmartTools && Drawing != null && !Drawing.IsEmpty;

        private (int across, int down) Footprint(int tile)
        {
            var t = Set != null && tile >= 0 && tile < Set.Tiles.Count ? Set.Tiles[tile] : null;
            return t == null ? (1, 1) : TileGrid.Footprint(t.Wide, t.Deep, Turn);
        }

        public (int x, int z) AnchorFor(int x, int z, int tile) => (x, z - Footprint(tile).down + 1);

        private (int x, int z) Snapped(int x, int z)
        {
            var (across, down) = Footprint(Brush);
            int n = TileGrid.Across;
            int py = n - 1 - z, sy = n - 1 - _start.z;
            int ax = ((x - _start.x % across) / across) * across + _start.x % across;
            int ay = ((py - sy % down) / down) * down + sy % down;
            return (ax, n - 1 - (ay + down - 1));
        }

        private bool PutBrush((int x, int z) at)
        {
            var tile = BrushTile;
            if (tile == null) { Warning = "Select a tile."; return false; }
            if (!Grid.Fits(at.x, at.z, tile.Wide, tile.Deep, Turn))
            {
                Warning = $"{tile.Name} doesn't fit there.";
                return false;
            }
            var was = Grid.At(at.x, at.z, Layer);
            if (was.WhereItWasPut && was.Tile == Brush && was.Turn == (Turn & 3)) return false;
            Warning = null;

            // A tile laid on an empty square of an unraised layer sits on the ground already there.
            var (_, down) = TileGrid.Footprint(tile.Wide, tile.Deep, Turn);
            int southZ = at.z + down - 1;
            if (was.Tile < 0 && Grid.HeightAt(at.x, southZ, Layer) == 0f)
                for (int layer = 0; layer < TileGrid.Layers; layer++)
                {
                    var under = Grid.At(at.x, southZ, layer);
                    if (layer == Layer || under.Tile < 0) continue;
                    // The new tile stands on the ground's surface, which can sit above that tile's lowest face.
                    float surface = GroundSurface?.Invoke(at.x, southZ, layer)
                                    ?? under.Lift + (Set != null && under.Tile < Set.Tiles.Count ? Set.Tiles[under.Tile].SurfaceY : 0f);
                    Grid.SetHeight(at.x, southZ, surface, Layer);
                    break;
                }
            return Grid.PutTile(at.x, at.z, Brush, Turn, tile.Wide, tile.Deep, Layer);
        }

        public bool Press(int x, int z, Button button, bool shift = false, bool ctrl = false)
        {
            if (Set == null) return false;
            if (Pasting)
            {
                _before = Grid.Clone();
                _pressed = true;
                _doing = Doing.Nothing;
                if (button == Button.Left) Land(_clipboard, x, z);
                Pasting = false;
                return !Grid.SameAs(_before);
            }
            _pressed = true;
            _button = button;
            _start = (x, z);
            _before = Grid.Clone();
            _doing = Doing.Nothing;
            _pending = new List<(int x, int z)>();

            if (shift && button == Button.Left && Current != Tool.Select && Current != Tool.Wand && Current != Tool.Lasso)
            {
                _doing = Doing.Selecting;
                _adding = false;
                _pending = TileShapes.RectangleFilled(_start, _start);
                return false;
            }

            switch (Current)
            {
                case Tool.Paint:
                    if (Heights)
                    {
                        if (button == Button.Left) { _doing = Doing.Heights; _stepped.Clear(); SetHeight(x, z); }
                        else if (button == Button.Middle) { if (HeightChange == HeightWay.Set) Grid.FloodFillHeight(x, z, HeightBrush * TileGrid.Step, Layer); }
                        else PickHeight(x, z);
                        break;
                    }
                    if (button == Button.Left || (button == Button.Right && SmartStroke))
                    {
                        if (SmartStroke) StartStroke(x, z, inverted: button == Button.Right);
                        else { _doing = Doing.Placing; PutBrush(AnchorFor(x, z, Brush)); }
                    }
                    else if (button == Button.Middle) Bucket(x, z);
                    else PickTile(x, z);
                    break;

                case Tool.Clear:
                    if (button == Button.Middle) Grid.FloodFillTile(x, z, -1, 0, 1, 1, Layer);
                    else if (button == Button.Left) { _doing = Doing.Clearing; Grid.Clear(x, z, Layer); }
                    break;

                case Tool.Smart:
                case Tool.SmartInverted:
                    if (button == Button.Right) break;
                    if (Drawing == null || Drawing.IsEmpty) { Warning = "No smart drawing selected."; break; }
                    Drawing.Fill(Grid, Set, x, z, Layer, Current == Tool.SmartInverted);
                    break;

                case Tool.Bucket:
                    if (button == Button.Left) Bucket(x, z);
                    break;

                case Tool.Picker:
                    if (Heights) PickHeight(x, z); else PickTile(x, z);
                    break;

                case Tool.Line:
                case Tool.Rectangle:
                case Tool.Ellipse:
                    if (button == Button.Middle) break;
                    _doing = Doing.Shape;
                    _shapeInverted = button == Button.Right;
                    _pending = ShapeCells(_start, _start);
                    break;

                case Tool.Select:
                    if (button != Button.Left) { Selection = null; break; }
                    if (Selection != null && Selection[x, z] && !shift && !ctrl)
                    {
                        _doing = Doing.Moving;
                        _moving = Take(Selection);
                        Wipe(Selection);
                        _pending = Cells(_moving, 0, 0);
                        break;
                    }
                    _doing = Doing.Selecting;
                    _adding = shift || ctrl;
                    _pending = TileShapes.RectangleFilled(_start, _start);
                    break;

                case Tool.Lasso:
                    if (button != Button.Left) { Selection = null; break; }
                    _doing = Doing.Lassoing;
                    _adding = ctrl;
                    _stroke = new List<(int x, int z)> { (x, z) };
                    _pending = new List<(int x, int z)>(_stroke);
                    break;

                case Tool.Wand:
                    if (button != Button.Left) { Selection = null; break; }
                    Wand(x, z, everywhere: shift, combine: ctrl);
                    break;
            }

            return !Grid.SameAs(_before);
        }

        public bool Drag(int x, int z)
        {
            if (!_pressed) return false;
            var was = Grid.Clone();

            switch (_doing)
            {
                case Doing.Placing: PutBrush(Snapped(x, z)); break;
                case Doing.Clearing: Grid.Clear(x, z, Layer); break;
                case Doing.Heights: SetHeight(x, z); break;
                case Doing.SmartStroke: ExtendStroke(x, z); break;
                case Doing.Shape: _pending = ShapeCells(_start, (x, z)); return false;
                case Doing.Selecting: _pending = TileShapes.RectangleFilled(_start, (x, z)); return false;
                case Doing.Moving: _pending = Cells(_moving, x - _start.x, z - _start.z); return false;
                case Doing.Lassoing: TileShapes.Extend(_stroke, (x, z)); _pending = new List<(int x, int z)>(_stroke); return false;
            }
            return !Grid.SameAs(was);
        }

        public bool Release(int x, int z)
        {
            if (!_pressed) return false;
            _pressed = false;

            if (_doing == Doing.Shape) CommitShape(_start, (x, z));
            if (_doing == Doing.Selecting)
            {
                var mask = _adding && Selection != null ? (bool[,])Selection.Clone() : new bool[TileGrid.Across, TileGrid.Across];
                foreach (var (cx, cz) in TileShapes.RectangleFilled(_start, (x, z))) mask[cx, cz] = true;
                Selection = mask;
            }
            if (_doing == Doing.Lassoing)
            {
                var mask = _adding && Selection != null ? (bool[,])Selection.Clone() : new bool[TileGrid.Across, TileGrid.Across];
                var outline = new bool[TileGrid.Across, TileGrid.Across];
                var closed = new List<(int x, int z)>(_stroke);
                TileShapes.Extend(closed, _stroke[0]);
                foreach (var (cx, cz) in closed) outline[cx, cz] = true;
                var inside = TileShapes.Enclosed(outline);
                for (int cx = 0; cx < TileGrid.Across; cx++)
                    for (int cz = 0; cz < TileGrid.Across; cz++)
                        if (inside[cx, cz]) mask[cx, cz] = true;
                Selection = mask;
            }
            if (_doing == Doing.Moving)
            {
                int dx = x - _start.x, dz = z - _start.z;
                Land(_moving, _moving.X + dx, _moving.Z + dz);
                Selection = Shifted(Selection, dx, dz);
                _moving = null;
            }
            _pending = new List<(int x, int z)>();
            _stroke = null;
            _strokeBase = null;
            _doing = Doing.Nothing;

            bool changed = !Grid.SameAs(_before);
            if (changed) Remember(_before);
            _before = null;
            return changed;
        }

        private void Remember(TileGrid before)
        {
            _undo.Add(before);
            if (_undo.Count > MostUndo) _undo.RemoveAt(0);
            _redo.Clear();
        }

        public enum HeightWay { Set, Raise, Lower }

        // Set paints the brush height; Raise and Lower move each square one step from where it is.
        public HeightWay HeightChange = HeightWay.Set;

        // Each height square moves once per stroke; a wide piece shares one, so it moves once too.
        private readonly HashSet<(int layer, int x, int z)> _stepped = new HashSet<(int layer, int x, int z)>();

        private void SetHeight(int x, int z)
        {
            float by = HeightChange == HeightWay.Raise ? TileGrid.Step : -TileGrid.Step;
            float lowest = TileGrid.LowestStep * TileGrid.Step, highest = TileGrid.HighestStep * TileGrid.Step;
            foreach (int layer in HeightsAllLayers ? Enumerable.Range(0, TileGrid.Layers) : new[] { Layer })
            {
                var (hx, hz) = Grid.HeightSquare(x, z, layer);
                float to;
                // Heights name the top you walk on, so a tile whose base sits under its surface is set by its surface.
                if (HeightChange == HeightWay.Set) to = HeightBrush * TileGrid.Step - SurfaceAbove(x, z, layer);
                else if (!_stepped.Add((layer, hx, hz))) continue;
                else to = Math.Clamp(Grid.HeightAt(hx, hz, layer) + by, lowest, highest);
                Grid.SetHeight(x, z, to, layer);
                if ((hx, hz) != (x, z)) Grid.SetHeight(hx, hz, to, layer);
            }
        }

        private float SurfaceAbove(int x, int z, int layer)
        {
            var sq = Grid.At(x, z, layer);
            return sq.Tile < 0 || GroundSurface == null ? 0f : GroundSurface(x, z, layer) - sq.Lift;
        }

        private void PickHeight(int x, int z)
        {
            var (hx, hz) = Grid.HeightSquare(x, z, Layer);
            float top = Grid.HeightAt(hx, hz, Layer) + SurfaceAbove(x, z, Layer);
            HeightBrush = Math.Clamp((int)Math.Round(top / TileGrid.Step), TileGrid.LowestStep, TileGrid.HighestStep);
        }

        private void PickTile(int x, int z)
        {
            var square = Grid.At(x, z, Layer);
            if (square.Tile < 0) return;
            Brush = square.Tile;
            Turn = square.Turn;
        }

        private void Bucket(int x, int z)
        {
            if (Heights) { Grid.FloodFillHeight(x, z, HeightBrush * TileGrid.Step, Layer); return; }
            var tile = BrushTile;
            if (tile == null) { Warning = "Select a tile."; return; }
            Grid.FloodFillTile(x, z, Brush, Turn, tile.Wide, tile.Deep, Layer, Selection);
        }

        private void StartStroke(int x, int z, bool inverted)
        {
            _doing = Doing.SmartStroke;
            _strokeInverted = inverted;
            _strokeTile = Brush;
            _strokeBase = Grid.Clone();
            _stroke = new List<(int x, int z)> { (x, z) };
            DrawStroke();
        }

        private void ExtendStroke(int x, int z)
        {
            int count = _stroke.Count;
            TileShapes.Extend(_stroke, (x, z));
            if (_stroke.Count != count) DrawStroke();
        }

        private void DrawStroke()
        {
            Grid = _strokeBase.Clone();
            Drawing?.DrawPath(Grid, Set, _stroke, _strokeTile, Layer, _strokeInverted);
        }

        private List<(int x, int z)> ShapeCells((int x, int z) a, (int x, int z) b)
        {
            bool smart = SmartShapes;
            var cells = Current switch
            {
                Tool.Line => smart ? TileShapes.EdgeToEdgeLine(a, b) : TileShapes.Line(a, b),
                Tool.Rectangle => smart ? TileShapes.RectangleFilled(a, b) : TileShapes.RectangleOutline(a, b),
                Tool.Ellipse => smart ? TileShapes.EllipseFilled(a, b) : TileShapes.EllipseOutline(a, b),
                _ => new List<(int x, int z)>(),
            };
            int n = TileGrid.Across;
            return cells.Where(c => c.x >= 0 && c.z >= 0 && c.x < n && c.z < n).ToList();
        }

        private void CommitShape((int x, int z) a, (int x, int z) b)
        {
            var cells = ShapeCells(a, b);
            if (cells.Count == 0) return;

            if (Heights)
            {
                _stepped.Clear();
                foreach (var (x, z) in cells) SetHeight(x, z);
                return;
            }

            if (SmartShapes)
            {
                if (Current == Tool.Line) Drawing.DrawPath(Grid, Set, cells, Brush, Layer, _shapeInverted);
                else Drawing.DrawShape(Grid, Set, cells, Layer, _shapeInverted);
                return;
            }

            if (BrushTile == null) { Warning = "Select a tile."; return; }
            foreach (var (x, z) in cells) PutBrush(AnchorFor(x, z, Brush));
        }

        public bool[,] Selection { get; private set; }

        private bool _adding;

        public sealed class Piece
        {
            public int X, Z, Wide, Deep;
            public List<(int dx, int dz, int tile, int turn, int wide, int deep)> Tiles = new();
            public float?[,] Heights;
        }

        private Piece _moving, _clipboard;

        public bool HasClipboard => _clipboard != null;

        public bool Pasting { get; private set; }

        public IReadOnlyList<(int x, int z)> PasteCells(int x, int z)
            => _clipboard == null ? Array.Empty<(int, int)>() : Cells(_clipboard, x - _clipboard.X, z - _clipboard.Z);

        public void SelectAll()
        {
            var all = new bool[TileGrid.Across, TileGrid.Across];
            for (int x = 0; x < TileGrid.Across; x++) for (int z = 0; z < TileGrid.Across; z++) all[x, z] = true;
            Selection = all;
        }

        public void SelectNone() => Selection = null;

        private void Wand(int x, int z, bool everywhere, bool combine)
        {
            int n = TileGrid.Across;
            int here = Grid.At(x, z, Layer).Tile;
            var alike = SmartTools && Drawing != null && Drawing.Holds(here) ? Drawing.Tiles : new HashSet<int> { here };

            var same = new bool[n, n];
            for (int cx = 0; cx < n; cx++)
                for (int cz = 0; cz < n; cz++)
                    same[cx, cz] = alike.Contains(Grid.At(cx, cz, Layer).Tile);
            var region = everywhere ? same : SmartDrawing.Joined(same, x, z);

            bool keep = (everywhere || combine) && Selection != null;
            bool remove = combine && Selection != null && Selection[x, z];
            var mask = keep ? (bool[,])Selection.Clone() : new bool[n, n];
            for (int cx = 0; cx < n; cx++)
                for (int cz = 0; cz < n; cz++)
                    if (region[cx, cz]) mask[cx, cz] = !remove;
            Selection = mask;
        }

        private Piece Take(bool[,] mask)
        {
            int n = TileGrid.Across;
            int lowX = n, lowZ = n, highX = -1, highZ = -1;
            for (int x = 0; x < n; x++)
                for (int z = 0; z < n; z++)
                    if (mask[x, z]) { lowX = Math.Min(lowX, x); lowZ = Math.Min(lowZ, z); highX = Math.Max(highX, x); highZ = Math.Max(highZ, z); }
            if (highX < 0) return null;

            var piece = new Piece { X = lowX, Z = lowZ, Wide = highX - lowX + 1, Deep = highZ - lowZ + 1 };
            piece.Heights = new float?[piece.Wide, piece.Deep];
            for (int x = lowX; x <= highX; x++)
                for (int z = lowZ; z <= highZ; z++)
                {
                    if (!mask[x, z]) continue;
                    piece.Heights[x - lowX, z - lowZ] = Grid.HeightAt(x, z, Layer);
                    int tile = Grid.PutHere(x, z, Layer);
                    if (tile < 0) continue;
                    var sq = Grid.At(x, z, Layer);
                    piece.Tiles.Add((x - lowX, z - sq.PastNorth - lowZ, tile, sq.Turn, sq.FullWide, sq.FullDeep));
                }
            return piece;
        }

        private void Wipe(bool[,] mask)
        {
            for (int x = 0; x < TileGrid.Across; x++)
                for (int z = 0; z < TileGrid.Across; z++)
                {
                    if (!mask[x, z]) continue;
                    if (Grid.PutHere(x, z, Layer) >= 0) Grid.Stamp(x, z, -1, 1, 1, Layer);
                    Grid.SetHeight(x, z, 0f, Layer);
                }
        }

        private void Land(Piece piece, int x, int z)
        {
            if (piece == null) return;
            for (int dx = 0; dx < piece.Wide; dx++)
                for (int dz = 0; dz < piece.Deep; dz++)
                    if (piece.Heights[dx, dz] is float h) Grid.SetHeight(x + dx, z + dz, h, Layer);
            foreach (var t in piece.Tiles)
                Grid.Stamp(x + t.dx, z + t.dz, t.tile, t.wide, t.deep, Layer, (byte)t.turn);
        }

        private static List<(int x, int z)> Cells(Piece piece, int dx, int dz)
        {
            var cells = new List<(int x, int z)>();
            if (piece == null) return cells;
            for (int x = 0; x < piece.Wide; x++)
                for (int z = 0; z < piece.Deep; z++)
                {
                    if (piece.Heights[x, z] == null) continue;
                    int cx = piece.X + x + dx, cz = piece.Z + z + dz;
                    if (cx >= 0 && cz >= 0 && cx < TileGrid.Across && cz < TileGrid.Across) cells.Add((cx, cz));
                }
            return cells;
        }

        private static bool[,] Shifted(bool[,] mask, int dx, int dz)
        {
            if (mask == null) return null;
            int n = TileGrid.Across;
            var moved = new bool[n, n];
            for (int x = 0; x < n; x++)
                for (int z = 0; z < n; z++)
                    if (mask[x, z] && x + dx >= 0 && z + dz >= 0 && x + dx < n && z + dz < n) moved[x + dx, z + dz] = true;
            return moved;
        }

        public bool FillSelection()
        {
            if (Heights && Selection != null)
            {
                var area = Bounds(Selection);
                return Whole(() =>
                {
                    _stepped.Clear();
                    for (int x = area.x; x < area.x + area.w; x++)
                        for (int z = area.z; z < area.z + area.h; z++)
                            if (Selection[x, z]) SetHeight(x, z);
                });
            }
            var tile = BrushTile;
            if (Selection == null || tile == null) return false;
            var (across, down) = Footprint(Brush);
            var box = Bounds(Selection);
            return Whole(() =>
            {
                for (int x = box.x; x + across <= box.x + box.w; x += across)
                    for (int z = box.z; z + down <= box.z + box.h; z += down)
                        if (Selection[x, z + down - 1]) Grid.PutTile(x, z, Brush, Turn, tile.Wide, tile.Deep, Layer);
            });
        }

        public enum Transform { Rotate, FlipAcross, FlipDown }

        public bool TransformSelection(Transform how)
        {
            if (Selection == null) return false;
            var piece = Take(Selection);
            if (piece == null) return false;
            int w = piece.Wide, h = piece.Deep;
            bool turn = how == Transform.Rotate;

            (int x, int z) Cell(int x, int z) => how switch
            {
                Transform.Rotate => (h - 1 - z, x),
                Transform.FlipAcross => (w - 1 - x, z),
                _ => (x, h - 1 - z),
            };

            var moved = new Piece { X = piece.X, Z = piece.Z, Wide = turn ? h : w, Deep = turn ? w : h };
            moved.Heights = new float?[moved.Wide, moved.Deep];
            for (int x = 0; x < w; x++)
                for (int z = 0; z < h; z++)
                {
                    var (nx, nz) = Cell(x, z);
                    moved.Heights[nx, nz] = piece.Heights[x, z];
                }
            foreach (var t in piece.Tiles)
            {
                var (ax, az) = Cell(t.dx, t.dz);
                var (bx, bz) = Cell(t.dx + t.wide - 1, t.dz + t.deep - 1);
                int across = turn ? t.deep : t.wide, down = turn ? t.wide : t.deep;
                moved.Tiles.Add((Math.Min(ax, bx), Math.Min(az, bz), t.tile, turn ? (t.turn + 1) & 3 : t.turn, across, down));
            }

            int n = TileGrid.Across;
            var mask = new bool[n, n];
            for (int x = 0; x < moved.Wide; x++)
                for (int z = 0; z < moved.Deep; z++)
                    if (moved.Heights[x, z] != null && piece.X + x < n && piece.Z + z < n) mask[piece.X + x, piece.Z + z] = true;

            return Whole(() =>
            {
                Wipe(Selection);
                Land(moved, piece.X, piece.Z);
                Selection = mask;

                if (SmartTools && Drawing != null && !Drawing.IsEmpty)
                {
                    var smart = new bool[n, n];
                    for (int x = 0; x < n; x++)
                        for (int z = 0; z < n; z++)
                            smart[x, z] = mask[x, z] && Drawing.Holds(Grid.PutHere(x, z, Layer));
                    SmartDrawing.Write(Grid, Set, Drawing.ResolveMask(smart, false), smart, Layer, writeEmpty: false);
                }
            });
        }

        private static (int x, int z, int w, int h) Bounds(bool[,] mask)
        {
            int n = TileGrid.Across, lx = n, lz = n, hx = -1, hz = -1;
            for (int x = 0; x < n; x++)
                for (int z = 0; z < n; z++)
                    if (mask[x, z]) { lx = Math.Min(lx, x); lz = Math.Min(lz, z); hx = Math.Max(hx, x); hz = Math.Max(hz, z); }
            return hx < 0 ? (0, 0, 0, 0) : (lx, lz, hx - lx + 1, hz - lz + 1);
        }

        public void CopySelection()
        {
            if (Selection != null) _clipboard = Take(Selection);
        }

        public bool CutSelection()
        {
            if (Selection == null) return false;
            _clipboard = Take(Selection);
            return Whole(() => Wipe(Selection));
        }

        public bool DeleteSelection() => Selection != null && Whole(() => Wipe(Selection));

        public void StartPaste() { if (_clipboard != null) Pasting = true; }
        public void CancelPaste() => Pasting = false;

        private bool Whole(Action change)
        {
            var before = Grid.Clone();
            change();
            if (Grid.SameAs(before)) return false;
            Remember(before);
            return true;
        }

        public bool ShiftLayer(int dx, int dz) => Whole(() => Grid.Shift(Layer, dx, dz));
        public bool RaiseLayer(int steps) => Whole(() => Grid.Raise(Layer, steps));
        public bool ClearLayer() => Whole(() => Grid.ClearLayer(Layer));

        private TileGrid.LayerCopy _copied;
        public bool HasCopy => _copied != null;
        public void CopyLayer() => _copied = Grid.CopyLayer(Layer);
        public bool PasteLayer() => _copied != null && Whole(() => Grid.PasteLayer(Layer, _copied));

        public IEnumerable<TileGrid> AllGrids => new[] { Grid }.Concat(_undo).Concat(_redo);

        public bool Undo()
        {
            if (_undo.Count == 0 || _pressed) return false;
            _redo.Add(Grid);
            Grid = _undo[^1];
            _undo.RemoveAt(_undo.Count - 1);
            return true;
        }

        public bool Redo()
        {
            if (_redo.Count == 0 || _pressed) return false;
            _undo.Add(Grid);
            Grid = _redo[^1];
            _redo.RemoveAt(_redo.Count - 1);
            return true;
        }
    }
}
