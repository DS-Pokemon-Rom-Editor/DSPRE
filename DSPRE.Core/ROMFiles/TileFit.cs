using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>Fits pictures into 16-colour background tiles, shared by every picture import onto a background.</summary>
    public static class TileFit
    {
        public const int Pixels = 64, TileBytes = 32, RowSize = 16;
        public const int FlipH = 0x400, FlipV = 0x800;
        public static readonly int[] FlipBits = { 0, FlipH, FlipV, FlipH | FlipV };

        /// <summary>One tile's colour numbers, a byte a pixel.</summary>
        public static byte[] Numbers(byte[] sheet, int tilesAt, int tile, bool eightBit = false)
        {
            byte[] p = new byte[Pixels];
            int size = eightBit ? Pixels : TileBytes, at = tilesAt + tile * size;
            if (at < 0 || at + size > sheet.Length) return p;
            if (eightBit)
            {
                Array.Copy(sheet, at, p, 0, Pixels);
                return p;
            }
            for (int i = 0; i < TileBytes; i++)
            {
                p[i * 2] = (byte)(sheet[at + i] & 15);
                p[i * 2 + 1] = (byte)(sheet[at + i] >> 4);
            }
            return p;
        }

        public static void Put(byte[] sheet, int tilesAt, int tile, byte[] numbers, bool eightBit = false)
        {
            if (eightBit)
            {
                Array.Copy(numbers, 0, sheet, tilesAt + tile * Pixels, Pixels);
                return;
            }
            for (int i = 0; i < TileBytes; i++)
                sheet[tilesAt + tile * TileBytes + i] = (byte)(numbers[i * 2] & 15 | numbers[i * 2 + 1] << 4);
        }

        /// <summary>The 15-bit colour of one RGBA pixel, or -1 when it is clear.</summary>
        public static int Colour(byte[] rgba, int at) =>
            rgba[at + 3] < 128 ? -1 : NitroBgCodec.ToBgr555(rgba[at], rgba[at + 1], rgba[at + 2]) & 0x7FFF;

        public static byte[] Flip(byte[] p, int bits)
        {
            byte[] o = new byte[Pixels];
            bool h = (bits & FlipH) != 0, v = (bits & FlipV) != 0;
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++) o[y * 8 + x] = p[(v ? 7 - y : y) * 8 + (h ? 7 - x : x)];
            return o;
        }

        public static int Difference(byte[] a, byte[] b)
        {
            int d = 0;
            for (int i = 0; i < Pixels; i++) if (a[i] != b[i]) d++;
            return d;
        }

        /// <summary>The same for a square and its flips. A square holds its pixels in each drawing that shares the arrangement.</summary>
        public static string Key(byte[][] sheets) =>
            FlipBits.Select(bits => string.Concat(sheets.Select(s => Convert.ToHexString(Flip(s, bits))))).Min(StringComparer.Ordinal);

        /// <summary>
        /// One palette row's colour numbers. Colours keep the game's number when they can, so unchanged pixels need no new tiles.
        /// When the row runs out, the most used colours get numbers and the rest show as the nearest one.
        /// </summary>
        public sealed class RowColours
        {
            // Colours fill 15 bits. The caller handles a value with this bit set.
            public const int CallerMark = 0x8000;

            private readonly Dictionary<ushort, int> _numbers = new();
            private readonly Dictionary<ushort, int> _wanted = new();
            private readonly ushort[] _colours, _original;
            private readonly int _row;
            private readonly bool[] _taken;
            // 16 for a palette row, 256 for an extended palette.
            private readonly int _size;
            private readonly IReadOnlyCollection<ushort> _keep;
            public readonly List<(int Number, ushort Colour)> Added = new();
            /// <summary>Colours with no number, the colour shown in their place, and how many pixels use each.</summary>
            public readonly List<(ushort Colour, ushort Shown, int Pixels)> Swaps = new();
            public int Refused => Swaps.Count;
            /// <summary>Places given to new colours, which Keep can hand to another colour.</summary>
            public int Open { get; private set; }

            /// <param name="locked">Numbers something else shows, which can't change.</param>
            /// <param name="keep">Colours to give a number first when the row runs out.</param>
            public RowColours(ushort[] colours, ushort[] original, int row, bool[] locked, IReadOnlyCollection<ushort> keep = null)
            {
                _colours = colours;
                _original = original;
                _row = row;
                _keep = keep ?? Array.Empty<ushort>();
                _taken = (bool[])locked.Clone();
                _size = locked.Length;
                for (int n = locked.Length - 1; n >= 1; n--)
                    if (locked[n]) _numbers[(ushort)(colours[row * locked.Length + n] & 0x7FFF)] = n;
            }

            public void Want(int colour)
            {
                if (colour < 0 || (colour & CallerMark) != 0) return;
                _wanted[(ushort)colour] = _wanted.GetValueOrDefault((ushort)colour) + 1;
            }

            public void Settle()
            {
                List<ushort> left = _wanted.Keys.Where(c => !_numbers.ContainsKey(c)).ToList();
                foreach (ushort colour in left.ToList())
                    for (int n = 1; n < _size; n++)
                    {
                        if (_taken[n] || (_original[_row * _size + n] & 0x7FFF) != colour) continue;
                        Take(n, colour);
                        left.Remove(colour);
                        break;
                    }
                // Only a row that runs out is reordered, so a picture that fits keeps the numbers it always got.
                if (left.Count > _taken.Count(t => !t))
                    left = left.OrderByDescending(c => _keep.Contains(c)).ThenByDescending(c => _wanted[c]).ThenBy(c => c).ToList();
                List<ushort> over = new();
                foreach (ushort colour in left)
                {
                    int n = Array.IndexOf(_taken, false);
                    if (n < 0) over.Add(colour);
                    else
                    {
                        Take(n, colour);
                        Open++;
                    }
                }
                foreach (ushort colour in over)
                {
                    int n = Nearest(colour);
                    _numbers[colour] = n;
                    Swaps.Add((colour, Shows(n), _wanted[colour]));
                }
            }

            private void Take(int n, ushort colour)
            {
                _taken[n] = true;
                _numbers[colour] = n;
                Added.Add((n, colour));
            }

            /// <summary>The colour a number shows once the row is settled.</summary>
            public ushort Shows(int n)
            {
                foreach ((int number, ushort colour) in Added) if (number == n) return colour;
                return (ushort)(_colours[_row * _size + n] & 0x7FFF);
            }

            private int Nearest(ushort colour) =>
                Enumerable.Range(1, _size - 1).OrderBy(n => Distance(Shows(n), colour)).First();

            /// <summary>The number for a colour. A colour without one gets the number of the nearest colour.</summary>
            public int Number(int colour) => _numbers.TryGetValue((ushort)colour, out int n) ? n : Nearest((ushort)colour);
        }

        /// <summary>How far apart two 15-bit colours look.</summary>
        public static int Distance(int a, int b)
        {
            int d(int s) => (a >> s & 31) - (b >> s & 31);
            return d(0) * d(0) + d(5) * d(5) + d(10) * d(10);
        }

        public sealed class Square
        {
            public byte[][] Sheets;
            public int Owner;
            /// <summary>The tile and flip bits the square shows now, or -1. Kept when that tile still shows these pixels.</summary>
            public int Was = -1;
        }

        public sealed class Placement
        {
            public int Need, Room, Merged;
            public bool Fits;
            public Dictionary<int, int> TilesByOwner = new();
            public List<(int Slot, byte[][] Sheets)> NewTiles = new();
            /// <summary>Per square, in the order given: the tile and its flip bits.</summary>
            public (int Tile, int Flip)[] Squares;
        }

        /// <summary>Reuses kept tiles that match, merges shapes within <paramref name="merge"/> pixels, and gives the rest free slots, most used first.</summary>
        /// <param name="current">What some free slots hold now. A shape already there stays in its slot.</param>
        public static Placement Place(IReadOnlyList<Square> squares, IEnumerable<(int Tile, byte[][] Sheets)> kept, IReadOnlyList<int> free, int merge,
                                      IEnumerable<(int Tile, byte[][] Sheets)> current = null)
        {
            Placement plan = new Placement { Squares = new (int, int)[squares.Count] };
            Dictionary<string, int> existing = new();
            Dictionary<int, byte[][]> keptSheets = new();
            foreach ((int tile, byte[][] sheets) in kept)
            {
                keptSheets[tile] = sheets;
                existing.TryAdd(Key(sheets), tile);
            }

            List<(byte[][] Sheets, int Owner, string Key)> shapes = new();
            Dictionary<string, (byte[][] Sheets, int Tile)> resolved = new();
            bool Close(byte[][] a, byte[][] b, int bits) => a.Select((s, i) => Difference(Flip(s, bits), b[i])).Max() <= merge;

            List<IGrouping<string, int>> groups = Enumerable.Range(0, squares.Count).GroupBy(i => Key(squares[i].Sheets)).OrderByDescending(g => g.Count()).ToList();
            foreach (IGrouping<string, int> g in groups)
            {
                Square first = squares[g.First()];
                if (existing.TryGetValue(g.Key, out int tile))
                {
                    resolved[g.Key] = (first.Sheets, tile);
                    continue;
                }
                if (merge > 0)
                {
                    (byte[][] Sheets, int Owner, string Key)? near = shapes
                        .Cast<(byte[][] Sheets, int Owner, string Key)?>()
                        .FirstOrDefault(x => FlipBits.Any(bits => Close(x.Value.Sheets, first.Sheets, bits)));
                    if (near != null)
                    {
                        resolved[g.Key] = (near.Value.Sheets, -1);
                        plan.Merged++;
                        continue;
                    }
                }
                shapes.Add((first.Sheets, first.Owner, g.Key));
                resolved[g.Key] = (first.Sheets, -1);
                plan.TilesByOwner[first.Owner] = plan.TilesByOwner.GetValueOrDefault(first.Owner) + 1;
            }

            HashSet<int> freeSet = new HashSet<int>(free);
            Dictionary<string, (int Tile, byte[][] Sheets)> already = new();
            Dictionary<int, byte[][]> holding = new(keptSheets);
            foreach ((int tile, byte[][] sheets) in current ?? Enumerable.Empty<(int, byte[][])>())
                if (freeSet.Contains(tile))
                {
                    already.TryAdd(Key(sheets), (tile, sheets));
                    holding.TryAdd(tile, sheets);
                }
            Dictionary<string, int> slots = new();
            for (int k = 0; k < shapes.Count; k++)
            {
                if (!already.TryGetValue(shapes[k].Key, out (int Tile, byte[][] Sheets) here)) continue;
                slots[shapes[k].Key] = here.Tile;
                shapes[k] = (here.Sheets, shapes[k].Owner, shapes[k].Key);
                resolved[shapes[k].Key] = (here.Sheets, -1);
            }
            // Shapes already in their slot need no new tile.
            plan.Room = free.Count - slots.Count;
            plan.Need = shapes.Count - slots.Count;
            plan.Fits = plan.Need <= plan.Room;
            if (!plan.Fits) return plan;

            Queue<int> slotsLeft = new Queue<int>(free.Where(t => !slots.ContainsValue(t)));
            foreach ((byte[][] sheets, _, string key) in shapes)
            {
                if (!slots.TryGetValue(key, out int slot)) slots[key] = slot = slotsLeft.Dequeue();
                plan.NewTiles.Add((slot, sheets));
                holding[slot] = sheets;
            }

            foreach (IGrouping<string, int> g in groups)
                foreach (int i in g)
                {
                    Square w = squares[i];
                    int was = w.Was & 0x3FF, wasFlip = w.Was & (FlipH | FlipV);
                    if (w.Was >= 0 && holding.TryGetValue(was, out byte[][] there) && there.Select((s, k) => Flip(s, wasFlip).AsSpan().SequenceEqual(w.Sheets[k])).All(same => same))
                    {
                        plan.Squares[i] = (was, wasFlip);
                        continue;
                    }
                    (byte[][] sheets, int tile) = resolved[g.Key];
                    int slot = tile >= 0 ? tile : slots.GetValueOrDefault(g.Key, -1);
                    byte[][] source = tile >= 0 ? keptSheets[tile] : sheets;
                    if (slot < 0)
                    {
                        (byte[][] Sheets, int Owner, string Key) merged = shapes.First(x => FlipBits.Any(bits => Close(x.Sheets, w.Sheets, bits)));
                        source = merged.Sheets;
                        slot = slots[merged.Key];
                    }
                    int best = FlipBits.OrderBy(bits => source.Select((s, k) => Difference(Flip(s, bits), w.Sheets[k])).Sum()).First();
                    plan.Squares[i] = (slot, best);
                }
            return plan;
        }
    }
}
