using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.Models
{
    /// <summary>PDSMS smart drawing: a 5x3 template of edge, corner and middle tiles.</summary>
    public sealed class SmartDrawing
    {
        public const int Wide = 5, Tall = 3, Slots = Wide * Tall;

        public const int Middle = 6;

        private readonly int[] _slot = new int[Slots];

        public string Folder = "";

        public SmartDrawing()
        {
            for (int i = 0; i < Slots; i++) _slot[i] = -1;
        }

        public int this[int slot]
        {
            get => slot >= 0 && slot < Slots ? _slot[slot] : -1;
            set { if (slot >= 0 && slot < Slots) _slot[slot] = value < 0 ? -1 : value; }
        }

        public int this[int across, int down]
        {
            get => this[down * Wide + across];
            set => this[down * Wide + across] = value;
        }

        public ISet<int> Tiles => new HashSet<int>(_slot.Where(t => t >= 0));

        public bool Holds(int tile) => tile >= 0 && _slot.Contains(tile);

        public bool IsEmpty => _slot.All(t => t < 0);

        public int SlotOf(int tile)
        {
            for (int i = 0; i < Slots; i++) if (_slot[i] == tile) return i;
            return -1;
        }

        public SmartDrawing Clone()
        {
            SmartDrawing copy = new SmartDrawing { Folder = Folder };
            Array.Copy(_slot, copy._slot, Slots);
            return copy;
        }

        public void Renumber(Func<int, int> to)
        {
            for (int i = 0; i < Slots; i++)
                if (_slot[i] >= 0) _slot[i] = to(_slot[i]);
        }

        private readonly struct Unit
        {
            public readonly bool T, B, L, R, TL, TR, BL, BR;

            public Unit(bool t, bool b, bool l, bool r, bool tl, bool tr, bool bl, bool br)
            { T = t; B = b; L = l; R = r; TL = tl; TR = tr; BL = bl; BR = br; }

            public bool FullCross => T && B && L && R;
            public bool SameCross(Unit u) => T == u.T && B == u.B && L == u.L && R == u.R;
            public bool SameCorners(Unit u) => TL == u.TL && TR == u.TR && BL == u.BL && BR == u.BR;
        }

        private const bool Y = true, N = false;

        private static readonly Unit[] Pieces =
        {
            new Unit(Y, N, N, Y, N, N, N, N), new Unit(Y, N, Y, Y, N, N, N, N), new Unit(Y, N, Y, N, N, N, N, N),
            new Unit(Y, Y, Y, Y, Y, N, Y, Y), new Unit(Y, Y, Y, Y, N, Y, Y, Y),
            new Unit(Y, Y, N, Y, N, N, N, N), new Unit(Y, Y, Y, Y, Y, Y, Y, Y), new Unit(Y, Y, Y, N, N, N, N, N),
            new Unit(Y, Y, Y, Y, Y, Y, Y, N), new Unit(Y, Y, Y, Y, Y, Y, N, Y),
            new Unit(N, Y, N, Y, N, N, N, N), new Unit(N, Y, Y, Y, N, N, N, N), new Unit(N, Y, Y, N, N, N, N, N),
        };

        private static readonly Unit[] InsideOut =
        {
            new Unit(Y, Y, Y, Y, Y, N, Y, Y), new Unit(N, Y, Y, Y, N, N, N, N), new Unit(Y, Y, Y, Y, N, Y, Y, Y),
            new Unit(Y, N, N, Y, N, N, N, N), new Unit(Y, N, Y, N, N, N, N, N),
            new Unit(Y, Y, Y, N, N, N, N, N), new Unit(Y, Y, Y, Y, Y, Y, Y, Y), new Unit(Y, Y, N, Y, N, N, N, N),
            new Unit(N, Y, N, Y, N, N, N, N), new Unit(N, Y, Y, N, N, N, N, N),
            new Unit(Y, Y, Y, Y, Y, Y, Y, N), new Unit(Y, N, Y, Y, N, N, N, N), new Unit(Y, Y, Y, Y, Y, Y, N, Y),
        };

        private static bool Neighbour(bool[,] belongs, int x, int z, int dx, int up, bool offEdge)
        {
            // PDSMS rows count up the screen, ours count down.
            int nx = x + dx, nz = z - up;
            if (nx < 0 || nz < 0 || nx >= belongs.GetLength(0) || nz >= belongs.GetLength(1)) return offEdge;
            return belongs[nx, nz];
        }

        private static Unit UnitAt(bool[,] belongs, int x, int z, bool offEdge) => new Unit(
            Neighbour(belongs, x, z, 0, -1, offEdge), Neighbour(belongs, x, z, 0, 1, offEdge),
            Neighbour(belongs, x, z, -1, 0, offEdge), Neighbour(belongs, x, z, 1, 0, offEdge),
            Neighbour(belongs, x, z, -1, -1, offEdge), Neighbour(belongs, x, z, 1, -1, offEdge),
            Neighbour(belongs, x, z, -1, 1, offEdge), Neighbour(belongs, x, z, 1, 1, offEdge));

        private int PieceFor(Unit unit, bool insideOut)
        {
            Unit[] table = insideOut ? InsideOut : Pieces;
            int found = -1;
            for (int i = 0; i < table.Length && found < 0; i++)
                if (unit.FullCross ? table[i].SameCorners(unit) : table[i].SameCross(unit)) found = i;
            if (found < 0) found = Middle;
            return _slot[found];
        }

        private int[,] Resolve(bool[,] neighbours, bool[,] write, bool insideOut, bool offEdge)
        {
            int w = neighbours.GetLength(0), h = neighbours.GetLength(1);
            int[,] piece = new int[w, h];
            for (int x = 0; x < w; x++)
                for (int z = 0; z < h; z++)
                    piece[x, z] = write[x, z] ? PieceFor(UnitAt(neighbours, x, z, offEdge), insideOut) : -1;
            return piece;
        }

        public int[,] ResolveMask(bool[,] mask, bool insideOut) => Resolve(mask, mask, insideOut, false);

        public int Fill(TileGrid grid, MapTileset set, int x, int z, int layer, bool insideOut)
        {
            int n = TileGrid.Across;
            if (x < 0 || z < 0 || x >= n || z >= n) return 0;

            int was = grid.PutHere(x, z, layer);
            bool[,] same = new bool[n, n];
            for (int gx = 0; gx < n; gx++)
                for (int gz = 0; gz < n; gz++)
                    same[gx, gz] = grid.PutHere(gx, gz, layer) == was;

            for (int gx = 0; gx < n; gx++)
                for (int gz = 0; gz < n; gz++)
                {
                    int there = grid.PutHere(gx, gz, layer);
                    if (there < 0 || there == was) continue;
                    TileGrid.Square sq = grid.At(gx, gz, layer);
                    for (int dx = 0; dx < sq.Wide; dx++)
                        for (int dz = 0; dz < sq.Deep; dz++)
                            if (gx + dx < n && gz + dz < n) same[gx + dx, gz + dz] = false;
                }

            bool[,] joined = Joined(same, x, z);

            int[,] piece = Resolve(same, joined, insideOut, true);
            return Write(grid, set, piece, joined, layer, writeEmpty: true);
        }

        public static bool[,] Joined(bool[,] mask, int x, int z)
        {
            int w = mask.GetLength(0), h = mask.GetLength(1);
            bool[,] joined = new bool[w, h];
            if (x < 0 || z < 0 || x >= w || z >= h || !mask[x, z]) return joined;

            Queue<(int x, int z)> waiting = new Queue<(int x, int z)>();
            waiting.Enqueue((x, z));
            joined[x, z] = true;
            while (waiting.Count > 0)
            {
                (int px, int pz) = waiting.Dequeue();
                foreach ((int dx, int dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = px + dx, nz = pz + dz;
                    if (nx < 0 || nz < 0 || nx >= w || nz >= h || !mask[nx, nz] || joined[nx, nz]) continue;
                    joined[nx, nz] = true;
                    waiting.Enqueue((nx, nz));
                }
            }
            return joined;
        }

        public static int Write(TileGrid grid, MapTileset set, int[,] piece, bool[,] where, int layer, bool writeEmpty)
        {
            int written = 0;
            for (int x = 0; x < piece.GetLength(0); x++)
                for (int z = 0; z < piece.GetLength(1); z++)
                {
                    if (!where[x, z]) continue;
                    int tile = piece[x, z];
                    if (tile < 0 && !writeEmpty) continue;

                    (int wide, int deep) = SizeOf(set, tile);
                    if (grid.Stamp(x, z, tile, wide, deep, layer)) written++;
                }
            return written;
        }

        private static (int wide, int deep) SizeOf(MapTileset set, int tile)
            => set != null && tile >= 0 && tile < set.Tiles.Count
                ? (set.Tiles[tile].Wide, set.Tiles[tile].Deep)
                : (1, 1);

        public bool[,] ShapeMask(TileGrid grid, int layer, IEnumerable<(int x, int z)> cells)
        {
            int n = TileGrid.Across;
            bool[,] candidate = new bool[n, n];
            bool[,] affected = new bool[n, n];
            ISet<int> tiles = Tiles;
            for (int x = 0; x < n; x++)
                for (int z = 0; z < n; z++)
                    candidate[x, z] = tiles.Contains(grid.PutHere(x, z, layer));

            Queue<(int x, int z)> waiting = new Queue<(int x, int z)>();
            foreach ((int x, int z) in cells)
            {
                if (x < 0 || z < 0 || x >= n || z >= n) continue;
                candidate[x, z] = true;
                if (affected[x, z]) continue;
                affected[x, z] = true;
                waiting.Enqueue((x, z));
            }

            while (waiting.Count > 0)
            {
                (int px, int pz) = waiting.Dequeue();
                foreach ((int dx, int dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = px + dx, nz = pz + dz;
                    if (nx < 0 || nz < 0 || nx >= n || nz >= n || !candidate[nx, nz] || affected[nx, nz]) continue;
                    affected[nx, nz] = true;
                    waiting.Enqueue((nx, nz));
                }
            }
            return affected;
        }

        public int DrawShape(TileGrid grid, MapTileset set, IEnumerable<(int x, int z)> cells, int layer, bool insideOut)
        {
            bool[,] mask = ShapeMask(grid, layer, cells);
            return Write(grid, set, ResolveMask(mask, insideOut), mask, layer, writeEmpty: false);
        }

        public int[,] ResolvePath(IList<(int x, int z)> cells, int startTile, bool insideOut)
        {
            int n = TileGrid.Across;
            int[,] piece = new int[n, n];
            for (int x = 0; x < n; x++) for (int z = 0; z < n; z++) piece[x, z] = -1;
            if (cells == null) return piece;

            List<(int x, int z)> path = new List<(int x, int z)>();
            HashSet<(int, int)> seen = new HashSet<(int, int)>();
            foreach ((int x, int z) c in cells)
                if (c.x >= 0 && c.z >= 0 && c.x < n && c.z < n && seen.Add(c)) path.Add(c);
            if (path.Count == 0) return piece;

            int startSlot = SlotOf(startTile);
            (int x, int y)? inward = Inward(startSlot);
            int first = insideOut ? TileAt(TurnedInsideOut(startSlot), startTile) : startTile;

            if (path.Count == 1 || inward == null)
            {
                foreach ((int x, int z) in path) piece[x, z] = first;
                return piece;
            }

            (int x, int y) Dir((int x, int z) a, (int x, int z) b)
                => (Math.Sign(b.x - a.x), -Math.Sign(b.z - a.z));

            (int x, int y) d0 = Dir(path[0], path[1]);
            int leftX = -d0.y, leftY = d0.x;
            bool insideOnLeft = leftX * inward.Value.x + leftY * inward.Value.y >= 0;
            if (insideOut) insideOnLeft = !insideOnLeft;

            for (int i = 0; i < path.Count; i++)
            {
                (int cx, int cz) = path[i];
                if (i == 0) { piece[cx, cz] = first; continue; }

                int slot;
                if (i == path.Count - 1)
                    slot = EdgeSlot(Dir(path[i - 1], path[i]), insideOnLeft);
                else
                {
                    (int x, int y) into = Dir(path[i - 1], path[i]);
                    (int x, int y) outOf = Dir(path[i], path[i + 1]);
                    int cross = into.x * outOf.y - into.y * outOf.x;
                    if (cross == 0) slot = EdgeSlot(outOf, insideOnLeft);
                    else
                    {
                        int outer = OuterCorner(-into.x, -into.y, outOf.x, outOf.y);
                        bool towardInside = insideOnLeft ? cross > 0 : cross < 0;
                        slot = towardInside ? outer : InnerFor(outer);
                    }
                }
                piece[cx, cz] = TileAt(slot, startTile);
            }
            return piece;
        }

        public int DrawPath(TileGrid grid, MapTileset set, IList<(int x, int z)> cells, int startTile, int layer, bool insideOut)
        {
            int[,] piece = ResolvePath(cells, startTile, insideOut);
            int n = TileGrid.Across;
            bool[,] where = new bool[n, n];
            foreach ((int x, int z) in cells)
                if (x >= 0 && z >= 0 && x < n && z < n) where[x, z] = true;
            return Write(grid, set, piece, where, layer, writeEmpty: false);
        }

        private int TileAt(int slot, int fallback)
        {
            if (slot < 0 || slot >= Slots) return fallback;
            return _slot[slot] >= 0 ? _slot[slot] : fallback;
        }

        private static int TurnedInsideOut(int slot) => slot switch
        {
            0 => 3, 1 => 11, 2 => 4, 3 => 0, 4 => 2, 5 => 7, 7 => 5,
            8 => 10, 9 => 12, 10 => 8, 11 => 1, 12 => 9, _ => slot,
        };

        private static (int x, int y)? Inward(int slot) => slot switch
        {
            0 => (1, -1), 1 => (0, -1), 2 => (-1, -1), 3 => (-1, 1), 4 => (1, 1), 5 => (1, 0),
            7 => (-1, 0), 8 => (-1, -1), 9 => (1, -1), 10 => (1, 1), 11 => (0, 1), 12 => (-1, 1),
            _ => null,
        };

        private static int EdgeSlot((int x, int y) d, bool insideOnLeft)
        {
            int nx = insideOnLeft ? -d.y : d.y;
            int ny = insideOnLeft ? d.x : -d.x;
            if (nx > 0) return 5;
            if (nx < 0) return 7;
            if (ny > 0) return 11;
            return 1;
        }

        private static int OuterCorner(int ax, int ay, int bx, int by)
        {
            bool left = ax < 0 || bx < 0, right = ax > 0 || bx > 0;
            bool up = ay > 0 || by > 0, down = ay < 0 || by < 0;
            if (right && down) return 0;
            if (left && down) return 2;
            if (right && up) return 10;
            if (left && up) return 12;
            return Middle;
        }

        private static int InnerFor(int outer) => outer switch
        {
            0 => 3, 2 => 4, 10 => 8, 12 => 9, _ => Middle,
        };
    }
}
