using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.Models
{
    /// <summary>A map as 9 layers of tiles and heights on a 32x32 grid, as in PDSMS.</summary>
    public sealed class TileGrid
    {
        public const int Across = 32;

        public const int Layers = 9;

        public const float Step = MapTileset.TileWidth;

        public const int LowestStep = -15, HighestStep = 15;

        public struct Square
        {
            public int Tile;

            // Height of the tile's south-west square, where PDSMS anchors it.
            public float Lift;

            public byte Turn;

            public int FromX, FromZ;

            public bool WhereItWasPut;

            public int Wide, Deep;

            public int FullWide, FullDeep;

            // Rows beyond the north edge; such tiles are stored on row 0.
            public int PastNorth;

            public int Layer;
        }

        private readonly int[,,] _tile = new int[Layers, Across, Across];
        private readonly byte[,,] _turn = new byte[Layers, Across, Across];
        private readonly byte[,,] _wide = new byte[Layers, Across, Across];
        private readonly byte[,,] _deep = new byte[Layers, Across, Across];
        private readonly float[,,] _height = new float[Layers, Across, Across];

        private readonly byte[,,] _fullWide = new byte[Layers, Across, Across];
        private readonly byte[,,] _fullDeep = new byte[Layers, Across, Across];
        private readonly byte[,,] _pastNorth = new byte[Layers, Across, Across];

        public TileGrid()
        {
            for (int l = 0; l < Layers; l++)
                for (int z = 0; z < Across; z++)
                    for (int x = 0; x < Across; x++)
                        _tile[l, z, x] = -1;
        }

        private static bool On(int x, int z) => x >= 0 && z >= 0 && x < Across && z < Across;
        private static bool OnLayer(int layer) => layer >= 0 && layer < Layers;

        private (int x, int z)? Anchor(int x, int z, int layer)
        {
            if (!On(x, z) || !OnLayer(layer)) return null;
            if (_tile[layer, z, x] >= 0) return (x, z);

            (int x, int z)? best = null;
            int nearest = int.MaxValue;
            for (int dz = 0; dz < MapTileset.MostSquares; dz++)
                for (int dx = 0; dx < MapTileset.MostSquares; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int ax = x - dx, az = z - dz;
                    if (!On(ax, az) || _tile[layer, az, ax] < 0) continue;
                    if (_wide[layer, az, ax] <= dx || _deep[layer, az, ax] <= dz) continue;
                    if (dx + dz < nearest) { nearest = dx + dz; best = (ax, az); }
                }
            return best;
        }

        public Square At(int x, int z, int layer = 0)
        {
            if (!On(x, z) || !OnLayer(layer))
                return new Square { Tile = -1, FromX = -1, FromZ = -1, Layer = layer };

            var anchor = Anchor(x, z, layer);
            if (anchor is not (int ax, int az))
                return new Square
                {
                    Tile = -1, FromX = -1, FromZ = -1, Layer = layer, Lift = _height[layer, z, x],
                };

            return new Square
            {
                Tile = _tile[layer, az, ax],
                Lift = _height[layer, az + Math.Max(1, (int)_deep[layer, az, ax]) - 1, ax],
                Turn = _turn[layer, az, ax],
                FromX = ax, FromZ = az,
                WhereItWasPut = ax == x && az == z,
                Wide = _wide[layer, az, ax], Deep = _deep[layer, az, ax],
                FullWide = Math.Max(_wide[layer, az, ax], _fullWide[layer, az, ax]),
                FullDeep = Math.Max(_deep[layer, az, ax], _fullDeep[layer, az, ax]),
                PastNorth = _pastNorth[layer, az, ax],
                Layer = layer,
            };
        }

        public int PutHere(int x, int z, int layer = 0)
            => On(x, z) && OnLayer(layer) ? _tile[layer, z, x] : -1;

        /// <summary>The square whose height a piece covering this square is drawn at; the square itself when empty.</summary>
        public (int x, int z) HeightSquare(int x, int z, int layer = 0)
        {
            if (!On(x, z) || !OnLayer(layer) || Anchor(x, z, layer) is not (int ax, int az)) return (x, z);
            return (ax, az + Math.Max(1, (int)_deep[layer, az, ax]) - 1);
        }

        public float HeightAt(int x, int z, int layer = 0)
            => On(x, z) && OnLayer(layer) ? _height[layer, z, x] : 0f;

        public IEnumerable<(int x, int z, Square square)> Placed()
        {
            for (int l = 0; l < Layers; l++)
                for (int z = 0; z < Across; z++)
                    for (int x = 0; x < Across; x++)
                        if (_tile[l, z, x] >= 0) yield return (x, z, At(x, z, l));
        }

        public int PaintedOn(int layer)
        {
            if (!OnLayer(layer)) return 0;
            int n = 0;
            for (int z = 0; z < Across; z++)
                for (int x = 0; x < Across; x++)
                    if (Anchor(x, z, layer) != null) n++;
            return n;
        }

        public int Painted
        {
            get
            {
                int n = 0;
                for (int z = 0; z < Across; z++)
                    for (int x = 0; x < Across; x++)
                        for (int l = 0; l < Layers; l++)
                            if (Anchor(x, z, l) != null) { n++; break; }
                return n;
            }
        }

        public bool HasAnything(int layer)
        {
            if (!OnLayer(layer)) return false;
            for (int z = 0; z < Across; z++)
                for (int x = 0; x < Across; x++)
                    if (_tile[layer, z, x] >= 0) return true;
            return false;
        }

        public static (int across, int down) Footprint(int wide, int deep, byte turn)
            => (turn & 1) == 0
                ? (Math.Max(1, wide), Math.Max(1, deep))
                : (Math.Max(1, deep), Math.Max(1, wide));

        public bool Fits(int x, int z, int wide, int deep, byte turn)
        {
            var (_, down) = Footprint(wide, deep, turn);
            return On(x, z + down - 1);
        }

        public void Put(int x, int z, int tile, float lift, byte turn, int wide = 1, int deep = 1, int layer = 0)
        {
            if (!PutTile(x, z, tile, turn, wide, deep, layer)) return;
            if (tile >= 0) _height[layer, z + Footprint(wide, deep, turn).down - 1, x] = lift;
        }

        public bool PutTile(int x, int z, int tile, byte turn, int wide = 1, int deep = 1, int layer = 0)
        {
            if (!OnLayer(layer)) return false;
            if (tile < 0) { if (On(x, z)) Clear(x, z, layer); return On(x, z); }

            var (across, down) = Footprint(wide, deep, turn);
            if (!On(x, z + down - 1)) return false;

            for (int dz = Math.Max(0, z); dz < Math.Min(Across, z + down); dz++)
                for (int dx = x; dx < Math.Min(Across, x + across); dx++)
                    Forget(layer, dx, dz);

            return Keep(layer, x, z, tile, turn, across, down);
        }

        public bool Stamp(int x, int z, int tile, int wide, int deep, int layer = 0, byte turn = 0)
        {
            if (!OnLayer(layer)) return false;
            if (tile < 0) { if (!On(x, z)) return false; Forget(layer, x, z); return true; }
            return Keep(layer, x, z, tile, turn, Math.Max(1, wide), Math.Max(1, deep));
        }

        private bool Keep(int layer, int x, int z, int tile, byte turn, int across, int down)
        {
            if (!On(x, z + down - 1)) return false;
            int past = Math.Max(0, -z);
            int az = z + past;
            _tile[layer, az, x] = tile;
            _turn[layer, az, x] = (byte)(turn & 3);
            _wide[layer, az, x] = (byte)Math.Min(across, Across - x);
            _deep[layer, az, x] = (byte)(down - past);
            _fullWide[layer, az, x] = (byte)across;
            _fullDeep[layer, az, x] = (byte)down;
            _pastNorth[layer, az, x] = (byte)past;
            return true;
        }

        private void Forget(int layer, int x, int z)
        {
            _tile[layer, z, x] = -1;
            _turn[layer, z, x] = 0;
            _wide[layer, z, x] = 0;
            _deep[layer, z, x] = 0;
            _fullWide[layer, z, x] = 0;
            _fullDeep[layer, z, x] = 0;
            _pastNorth[layer, z, x] = 0;
        }

        public void Clear(int x, int z, int layer = 0)
        {
            if (Anchor(x, z, layer) is not (int ax, int az)) return;
            Forget(layer, ax, az);
        }

        public void SetHeight(int x, int z, float height, int layer = 0)
        {
            if (On(x, z) && OnLayer(layer)) _height[layer, z, x] = height;
        }

        public void ClearLayer(int layer)
        {
            if (!OnLayer(layer)) return;
            for (int z = 0; z < Across; z++)
                for (int x = 0; x < Across; x++)
                {
                    Forget(layer, x, z);
                    _height[layer, z, x] = 0f;
                }
        }

        private List<(int x, int z, int tile, byte turn, int across, int down)> Whole(int layer)
        {
            var all = new List<(int, int, int, byte, int, int)>();
            for (int z = 0; z < Across; z++)
                for (int x = 0; x < Across; x++)
                    if (_tile[layer, z, x] >= 0)
                        all.Add((x, z - _pastNorth[layer, z, x], _tile[layer, z, x], _turn[layer, z, x],
                                 Math.Max(_wide[layer, z, x], _fullWide[layer, z, x]),
                                 Math.Max(_deep[layer, z, x], _fullDeep[layer, z, x])));
            return all;
        }

        public void Shift(int layer, int dx, int dz)
        {
            if (!OnLayer(layer) || (dx == 0 && dz == 0)) return;

            var tiles = Whole(layer);
            var height = new float[Across, Across];
            for (int z = 0; z < Across; z++)
                for (int x = 0; x < Across; x++)
                {
                    int tx = x + dx, tz = z + dz;
                    if (On(tx, tz)) height[tz, tx] = _height[layer, z, x];
                }

            for (int z = 0; z < Across; z++)
                for (int x = 0; x < Across; x++)
                {
                    Forget(layer, x, z);
                    _height[layer, z, x] = height[z, x];
                }

            foreach (var t in tiles)
                if (On(t.x + dx, t.z + t.down - 1 + dz))
                    Keep(layer, t.x + dx, t.z + dz, t.tile, t.turn, t.across, t.down);
        }

        public void Raise(int layer, int steps)
        {
            if (!OnLayer(layer) || steps == 0) return;
            float lowest = LowestStep * Step, highest = HighestStep * Step;
            for (int z = 0; z < Across; z++)
                for (int x = 0; x < Across; x++)
                {
                    float was = _height[layer, z, x];
                    float now = was + steps * Step;

                    if (steps > 0 && was < highest) now = Math.Min(now, highest);
                    else if (steps < 0 && was > lowest) now = Math.Max(now, lowest);
                    else now = was;

                    _height[layer, z, x] = now;
                }
        }

        public int FloodFillTile(int x, int z, int tile, byte turn, int wide, int deep, int layer = 0, bool[,] within = null)
        {
            if (!On(x, z) || !OnLayer(layer)) return 0;
            if (within != null && !within[x, z]) return 0;

            int was = _tile[layer, z, x];
            if (was == tile) return 0;

            var (across, down) = Footprint(wide, deep, turn);

            var free = new bool[Across, Across];
            for (int gz = 0; gz < Across; gz++)
                for (int gx = 0; gx < Across; gx++)
                    free[gz, gx] = true;
            for (int gz = 0; gz < Across; gz++)
                for (int gx = 0; gx < Across; gx++)
                {
                    int there = _tile[layer, gz, gx];
                    if (there < 0 || there == was) continue;
                    for (int dz = 0; dz < _deep[layer, gz, gx]; dz++)
                        for (int dx = 0; dx < _wide[layer, gz, gx]; dx++)
                            if (On(gx + dx, gz + dz)) free[gz + dz, gx + dx] = false;
                }

            bool Room(int px, int pz)
            {
                if (px < 0 || pz < 0 || px + across > Across || pz + down > Across) return false;
                for (int dz = 0; dz < down; dz++)
                    for (int dx = 0; dx < across; dx++)
                        if (_tile[layer, pz + dz, px + dx] != was || !free[pz + dz, px + dx]
                            || (within != null && !within[px + dx, pz + dz])) return false;
                return true;
            }

            int filled = 0;
            var waiting = new Stack<(int x, int z)>();
            waiting.Push((x, z));
            while (waiting.Count > 0)
            {
                var (px, pz) = waiting.Pop();
                if (!Room(px, pz)) continue;

                PutTile(px, pz, tile, turn, wide, deep, layer);
                filled++;

                waiting.Push((px + across, pz));
                waiting.Push((px - across, pz));
                waiting.Push((px, pz + down));
                waiting.Push((px, pz - down));
            }
            return filled;
        }

        public int FloodFillHeight(int x, int z, float height, int layer = 0)
        {
            if (!On(x, z) || !OnLayer(layer)) return 0;

            float was = _height[layer, z, x];
            if (Math.Abs(was - height) < 1e-6f) return 0;

            int filled = 0;
            var waiting = new Stack<(int x, int z)>();
            waiting.Push((x, z));
            while (waiting.Count > 0)
            {
                var (px, pz) = waiting.Pop();
                if (!On(px, pz) || Math.Abs(_height[layer, pz, px] - was) > 1e-6f) continue;

                _height[layer, pz, px] = height;
                filled++;

                waiting.Push((px + 1, pz));
                waiting.Push((px - 1, pz));
                waiting.Push((px, pz + 1));
                waiting.Push((px, pz - 1));
            }
            return filled;
        }

        public sealed class LayerCopy
        {
            internal int[,] Tile = new int[Across, Across];
            internal byte[,] Turn = new byte[Across, Across];
            internal byte[,] Wide = new byte[Across, Across];
            internal byte[,] Deep = new byte[Across, Across];
            internal float[,] Height = new float[Across, Across];
            internal byte[,] FullWide = new byte[Across, Across];
            internal byte[,] FullDeep = new byte[Across, Across];
            internal byte[,] PastNorth = new byte[Across, Across];
        }

        public LayerCopy CopyLayer(int layer)
        {
            var copy = new LayerCopy();
            if (!OnLayer(layer)) return copy;
            for (int z = 0; z < Across; z++)
                for (int x = 0; x < Across; x++)
                {
                    copy.Tile[z, x] = _tile[layer, z, x];
                    copy.Turn[z, x] = _turn[layer, z, x];
                    copy.Wide[z, x] = _wide[layer, z, x];
                    copy.Deep[z, x] = _deep[layer, z, x];
                    copy.Height[z, x] = _height[layer, z, x];
                    copy.FullWide[z, x] = _fullWide[layer, z, x];
                    copy.FullDeep[z, x] = _fullDeep[layer, z, x];
                    copy.PastNorth[z, x] = _pastNorth[layer, z, x];
                }
            return copy;
        }

        public void PasteLayer(int layer, LayerCopy copy)
        {
            if (!OnLayer(layer) || copy == null) return;
            for (int z = 0; z < Across; z++)
                for (int x = 0; x < Across; x++)
                {
                    _tile[layer, z, x] = copy.Tile[z, x];
                    _turn[layer, z, x] = copy.Turn[z, x];
                    _wide[layer, z, x] = copy.Wide[z, x];
                    _deep[layer, z, x] = copy.Deep[z, x];
                    _height[layer, z, x] = copy.Height[z, x];
                    _fullWide[layer, z, x] = copy.FullWide[z, x];
                    _fullDeep[layer, z, x] = copy.FullDeep[z, x];
                    _pastNorth[layer, z, x] = copy.PastNorth[z, x];
                }
        }

        public bool SameAs(TileGrid other)
        {
            if (other == null) return false;
            for (int l = 0; l < Layers; l++)
                for (int z = 0; z < Across; z++)
                    for (int x = 0; x < Across; x++)
                        if (_tile[l, z, x] != other._tile[l, z, x] || _turn[l, z, x] != other._turn[l, z, x]
                            || _wide[l, z, x] != other._wide[l, z, x] || _deep[l, z, x] != other._deep[l, z, x]
                            || _height[l, z, x] != other._height[l, z, x]
                            || _fullWide[l, z, x] != other._fullWide[l, z, x] || _fullDeep[l, z, x] != other._fullDeep[l, z, x]
                            || _pastNorth[l, z, x] != other._pastNorth[l, z, x])
                            return false;
            return true;
        }

        public void Renumber(Func<int, int> to)
        {
            for (int l = 0; l < Layers; l++)
                for (int z = 0; z < Across; z++)
                    for (int x = 0; x < Across; x++)
                    {
                        if (_tile[l, z, x] < 0) continue;
                        int now = to(_tile[l, z, x]);
                        if (now >= 0) { _tile[l, z, x] = now; continue; }
                        _tile[l, z, x] = -1; _turn[l, z, x] = 0; _wide[l, z, x] = 0; _deep[l, z, x] = 0;
                    }
        }

        public int Resized(int tile, int wide, int deep)
        {
            int changed = 0;
            for (int l = 0; l < Layers; l++)
                foreach (var t in Whole(l))
                {
                    if (t.tile != tile) continue;
                    var (across, down) = Footprint(wide, deep, t.turn);
                    int sw = t.z + t.down - 1;
                    Forget(l, t.x, Math.Max(0, t.z));
                    Keep(l, t.x, sw - down + 1, t.tile, t.turn, across, down);
                    changed++;
                }
            return changed;
        }

        // Heights are kept relative to this, so a map's own ground reads 0 wherever the map sits.
        public float BaseLift;

        public TileGrid Clone()
        {
            var grid = new TileGrid { BaseLift = BaseLift };
            for (int l = 0; l < Layers; l++) grid.PasteLayer(l, CopyLayer(l));
            return grid;
        }

        /// <param name="left">Collects the faces of pieces that found no tile or no free layer, so they can be kept.</param>
        public static TileGrid Of(MapMesh mesh, MapTileset set, Func<int, string> pictureOf,
                                  out int unmatched, Func<int, string> paletteOf = null, List<MapMesh.Face> left = null)
        {
            unmatched = 0;
            var grid = new TileGrid();
            if (mesh == null || set == null) return grid;

            var known = new Dictionary<string, int>();
            for (int i = 0; i < set.Tiles.Count; i++) known[MapTileset.Print(set.Tiles[i])] = i;

            var pieces = MapTileset.PiecesOf(mesh);
            // Heights count from the ground most of the map stands on, weighed by how much of it there is.
            var grounds = pieces.Select(p => { var t = p.TileOf(mesh, pictureOf, paletteOf, out float y); return (t, y, area: p.Wide * p.Deep); })
                                .Where(g => g.t.IsGround).ToList();
            if (grounds.Count == 0)
                grounds = pieces.Where(p => !p.Spans).Select(p => { var t = p.TileOf(mesh, pictureOf, paletteOf, out float y); return (t, y, area: 1); }).ToList();
            grid.BaseLift = grounds.Count == 0 ? 0f
                : grounds.GroupBy(g => MathF.Round(g.y * 64f) / 64f).OrderByDescending(g => g.Sum(e => e.area)).First().Key;

            // Layer 0 takes ground nearest the base height first, so buried sheets lose to the real ground.
            // Everything else goes on the first layer free under its whole footprint.
            var laid = pieces.Select(piece =>
            {
                var tile = piece.TileOf(mesh, pictureOf, paletteOf, out float baseY);
                return (piece, tile, baseY);
            }).ToList();
            foreach (var (piece, tile, y) in laid.OrderByDescending(e => e.tile.IsGround)
                                                .ThenBy(e => MathF.Round(MathF.Abs(e.baseY - grid.BaseLift) * 64f))
                                                .ThenBy(e => e.baseY + e.tile.SurfaceY))
            {
                if (!known.TryGetValue(MapTileset.Print(tile), out int at)) { unmatched++; left?.AddRange(piece.Faces); continue; }
                bool Free(int l) => Enumerable.Range(piece.Z, piece.Deep).All(z =>
                                        Enumerable.Range(piece.X, piece.Wide).All(x => grid.At(x, z, l).Tile < 0));
                int layer = tile.IsGround && Free(0) ? 0
                          : Enumerable.Range(1, Layers - 1).FirstOrDefault(Free, Free(0) ? 0 : -1);
                if (layer < 0) { unmatched++; left?.AddRange(piece.Faces); continue; }
                grid.Put(piece.X, piece.Z, at, y - grid.BaseLift, 0, piece.Wide, piece.Deep, layer);
            }

            return grid;
        }

    }
}
