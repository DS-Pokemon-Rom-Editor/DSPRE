using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>A background screen whose tiles move with some of a page's buttons.</summary>
    /// <param name="FromColumn">A cleared tile copies the first uncovered tile in its row, starting at this column.</param>
    /// <param name="Grid">How many rows the plain pattern repeats over. A cleared tile copies the tile in the same column and the same row
    /// of the repeat.</param>
    /// <param name="PlainFromRow">The first row of the plain background below the buttons, used with <paramref name="Grid"/>.</param>
    /// <param name="Left">How many tiles the art reaches past the touch area. Top, Right and Bottom work the same way.</param>
    /// <param name="Rebuild">Fixes a moved box's tiles by copying them from a box on the same kind of grid spot, for screens whose boxes are all drawn alike.</param>
    /// <param name="Bands">Buttons that slide along a band, which stretches behind them.</param>
    public sealed record ScreenTiles(RomInfo.DirNames Dir, int Member, int[] Buttons, int FromColumn = 0, int Grid = 0, int PlainFromRow = 0,
                                     int Left = 0, int Top = 0, int Right = 0, int Bottom = 0, bool Rebuild = false, ScreenBands Bands = null);

    /// <summary>Bands that start at column 0. Each has a label, a button and an end cap, and shares its top and bottom rows with the bands next to it.</summary>
    /// <param name="LabelEnds">Per button, the first column after its label, or -1 for no band.</param>
    /// <param name="Plain">Per button, a plain column of its band to stretch it with.</param>
    /// <param name="Lead">How many columns in front of the button, such as its arrow, slide with it.</param>
    /// <param name="Characters">The screen's tile sheet, where shared-row tiles the game doesn't have are added. -1 for none.</param>
    /// <param name="Room">How many tiles the sheet can hold.</param>
    public sealed record ScreenBands(int[] LabelEnds, int[] Plain, int Lead = 0, int Characters = -1, int Room = 0);

    /// <summary>Buttons drawn on a background screen whose squares can be set by hand.</summary>
    public interface IScreenArt
    {
        bool HasArt(int page);

        /// <summary>The square at a tile spot, or null when the page has no screen.</summary>
        ushort? SquareAt(int page, int x, int y);

        /// <summary>Puts a square on the page's screen; later moves keep it.</summary>
        void SetSquare(int page, int x, int y, ushort square);
    }

    /// <summary>A <see cref="ScreenTiles"/> screen read into a page's <see cref="ScreenTables"/>, plain or LZ-packed.</summary>
    internal sealed class ScreenTileArt
    {
        // An NSCR's tile entries start after its two headers; the screens edited here are 32 tiles wide.
        private const int ScreenData = 0x24, Columns = 32, Rows = 24, Tile = 8;

        public ScreenTiles Spec { get; }
        public int Start { get; }
        public int Length { get; }

        private ScreenTileArt(ScreenTiles spec, int start, int length)
        {
            Spec = spec;
            Start = start;
            Length = length;
        }

        /// <summary>Adds the screen to <paramref name="tables"/>. Null when it isn't a 256-pixel-wide screen.</summary>
        public static ScreenTileArt Take(ScreenTiles spec, ScreenTables tables)
        {
            if (spec == null || Read(spec) is not byte[] screen) return null;
            int start = tables.Add(screen, () => Read(spec));
            return new ScreenTileArt(spec, start, screen.Length) { _sheet = spec.Bands?.Characters >= 0 ? Sheet.Read(Inflate(Raw(spec.Dir, spec.Bands.Characters))) : null };
        }

        private static byte[] Raw(RomInfo.DirNames dir, int member)
        {
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { dir });
            string path = ArchiveMembers.PathOf(dir, member);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        private static byte[] Raw(ScreenTiles t) => Raw(t.Dir, t.Member);

        private static byte[] Inflate(byte[] raw) => Packed(raw) ? Lz77.Decompress(raw) : raw;

        // A 4bpp NCGR. Tiles joined for this screen are added at the end, in whole rows.
        private sealed class Sheet
        {
            public byte[] File;
            public int Rahc, Data, Count, Width;
            public List<byte[]> Added = new List<byte[]>();

            public static Sheet Read(byte[] file)
            {
                int rahc = file == null ? -1 : FindMagic(file, "RAHC");
                if (rahc < 0 || BitConverter.ToInt32(file, rahc + 0x0C) != 3) return null;
                int data = rahc + 8 + BitConverter.ToInt32(file, rahc + 0x1C);
                return new Sheet { File = file, Rahc = rahc, Data = data, Count = BitConverter.ToInt32(file, rahc + 0x18) / 32,
                                   Width = BitConverter.ToUInt16(file, rahc + 0x0A) };
            }

            private static int FindMagic(byte[] d, string magic)
            {
                for (int i = 0; i + 4 <= d.Length; i += 4)
                    if (d[i] == magic[0] && d[i + 1] == magic[1] && d[i + 2] == magic[2] && d[i + 3] == magic[3]) return i;
                return -1;
            }

            public byte[] Pixels(int tile) => tile < Count ? File.Skip(Data + tile * 32).Take(32).ToArray()
                                                           : tile - Count < Added.Count ? Added[tile - Count] : new byte[32];

            /// <summary>Finds the tile's number, adding the tile if the sheet doesn't have it. -1 when the sheet is full.</summary>
            public int Find(byte[] pixels, int room)
            {
                for (int t = 0; t < Count + Added.Count; t++)
                    if (Pixels(t).AsSpan().SequenceEqual(pixels)) return t;
                if (Count + Added.Count >= room) return -1;
                Added.Add(pixels);
                return Count + Added.Count - 1;
            }

            /// <summary>The sheet with the added tiles, or null when none were added.</summary>
            public byte[] WithAdded()
            {
                if (Added.Count == 0) return null;
                int rows = (Added.Count + Width - 1) / Width, extra = rows * Width * 32, end = Data + Count * 32;
                byte[] grown = new byte[File.Length + extra];
                Buffer.BlockCopy(File, 0, grown, 0, end);
                for (int i = 0; i < Added.Count; i++) Buffer.BlockCopy(Added[i], 0, grown, end + i * 32, 32);
                Buffer.BlockCopy(File, end, grown, end + extra, File.Length - end);
                void Grow(int at, int by) => BitConverter.TryWriteBytes(grown.AsSpan(at), BitConverter.ToInt32(grown, at) + by);
                Grow(0x08, extra);
                Grow(Rahc + 4, extra);
                Grow(Rahc + 0x18, extra);
                BitConverter.TryWriteBytes(grown.AsSpan(Rahc + 8), (ushort)(BitConverter.ToUInt16(grown, Rahc + 8) + rows));
                int sopc = FindMagic(grown, "SOPC");
                if (sopc >= 0) BitConverter.TryWriteBytes(grown.AsSpan(sopc + 0x0E), (ushort)(BitConverter.ToUInt16(grown, sopc + 0x0E) + rows));
                return grown;
            }
        }

        private Sheet _sheet;

        /// <summary>The tile sheet with the tiles this screen added, or null when it added none.</summary>
        public byte[] SheetOf() => _sheet?.WithAdded();

        // Rows 0 to 3 of a shared-row tile belong to the band above, rows 4 to 7 to the band below.
        private ushort? Join(ushort upper, ushort lower)
        {
            if (_sheet == null || upper >> 12 != lower >> 12) return null;
            byte[] Rows(ushort entry)
            {
                byte[] p = _sheet.Pixels(entry & 0x3FF), o = new byte[32];
                bool h = (entry >> 10 & 1) != 0, v = (entry >> 11 & 1) != 0;
                for (int y = 0; y < 8; y++)
                    for (int x = 0; x < 8; x++)
                    {
                        int sx = h ? 7 - x : x, sy = v ? 7 - y : y;
                        int px = p[sy * 4 + sx / 2] >> 4 * (sx & 1) & 15;
                        o[y * 4 + x / 2] |= (byte)(px << 4 * (x & 1));
                    }
                return o;
            }
            byte[] joined = Rows(upper).Take(16).Concat(Rows(lower).Skip(16)).ToArray();
            int tile = _sheet.Find(joined, Spec.Bands.Room);
            return tile < 0 ? null : (ushort)(tile | upper & 0xF000);
        }

        private static bool Packed(byte[] raw) => raw != null && raw.Length > 4 && raw[0] == 0x10 && Lz77.EndsAtDeclaredSize(raw);

        private static byte[] Read(ScreenTiles t)
        {
            byte[] raw = Raw(t);
            byte[] screen = Packed(raw) ? Lz77.Decompress(raw) : raw;
            // Screens of any other size are left alone.
            return screen != null && screen.Length >= ScreenData + Columns * Rows * 2 && System.Text.Encoding.ASCII.GetString(screen, 0, 4) == "RCSN"
                   && BitConverter.ToUInt16(screen, 0x18) == Columns * Tile ? screen : null;
        }

        public bool Moves(int button) => Spec.Buttons.Contains(button);

        /// <summary>The tiles a button's art covers, from its touch area.</summary>
        public (int X0, int Y0, int X1, int Y1) ArtOf((int Top, int Bottom, int Left, int Right) area) =>
            (Math.Max(0, area.Left / Tile - Spec.Left), Math.Max(0, area.Top / Tile - Spec.Top),
             Math.Min(Columns - 1, (area.Right - 1) / Tile + Spec.Right), Math.Min(Rows - 1, (area.Bottom - 1) / Tile + Spec.Bottom));

        public byte[] Screen(byte[] data) => data.Skip(Start).Take(Length).ToArray();

        private int Entry(int x, int y) => Start + ScreenData + (y * Columns + x) * 2;

        private ushort At(byte[] data, int x, int y) => BitConverter.ToUInt16(data, Entry(x, y));
        private void Set(byte[] data, int x, int y, ushort tile) => BitConverter.TryWriteBytes(data.AsSpan(Entry(x, y)), tile);

        // Which columns and rows of the repeat are grid lines. In the plain rows these never show the cell tile.
        private (bool[] Columns, bool[] Rows) Lines(byte[] data)
        {
            bool[] columns = new bool[Spec.Grid], rows = new bool[Spec.Grid];
            List<int> plain = Enumerable.Range(Spec.PlainFromRow, Rows - Spec.PlainFromRow).ToList();
            ushort cell = plain.SelectMany(y => Enumerable.Range(0, Columns).Select(x => At(data, x, y))).GroupBy(t => t)
                               .OrderByDescending(g => g.Count()).First().Key;
            for (int x = 0; x < Columns; x++)
                if (plain.All(y => At(data, x, y) != cell)) columns[x % Spec.Grid] = true;
            foreach (int y in plain)
                if (Enumerable.Range(0, Columns).Where(x => !columns[x % Spec.Grid]).All(x => At(data, x, y) != cell)) rows[y % Spec.Grid] = true;
            return (columns, rows);
        }

        private byte[] _asRead;
        private Dictionary<int, (int Top, int Bottom, int Left, int Right)> _was;

        /// <summary>Keeps a copy of the screen and button areas. Every move redraws from it, so boxes that pass over each other stay whole.</summary>
        public void Remember(byte[] data, Func<int, (int Top, int Bottom, int Left, int Right)> area)
        {
            _asRead = (byte[])data.Clone();
            _was = Spec.Buttons.ToDictionary(b => b, area);
        }

        /// <summary>Redraws the screen from the kept copy, with each button's art at its current area.</summary>
        public void Compose(byte[] data, Func<int, (int Top, int Bottom, int Left, int Right)> area)
        {
            Buffer.BlockCopy(_asRead, Start, data, Start, Length);
            List<int> moved = Spec.Buttons.Where(b => !Slides(b) && area(b) != _was[b]).ToList();
            List<(int X0, int Y0, int X1, int Y1)> now = Spec.Buttons.Select(b => ArtOf(area(b))).ToList();
            List<(int X0, int Y0, int X1, int Y1)> all = now.Concat(moved.Select(b => ArtOf(_was[b]))).ToList();
            bool Covered(int x, int y) => all.Any(c => x >= c.X0 && x <= c.X1 && y >= c.Y0 && y <= c.Y1);
            List<(int X, int Y, ushort Tile)> plain = moved.Select(b => ArtOf(_was[b])).SelectMany(Tiles).Distinct()
                                                           .Select(t => (t.X, t.Y, Plain(data, t.X, t.Y, Covered))).ToList();
            foreach ((int x, int y, ushort tile) in plain) Set(data, x, y, tile);
            foreach (int b in moved)
            {
                (int X0, int Y0, int X1, int Y1) from = ArtOf(_was[b]), to = ArtOf(area(b));
                foreach ((int x, int y) in Tiles(from))
                {
                    int nx = x - from.X0 + to.X0, ny = y - from.Y0 + to.Y0;
                    if (nx >= 0 && ny >= 0 && nx < Columns && ny < Rows) Set(data, nx, ny, At(_asRead, x, y));
                }
            }
            if (Spec.Rebuild && Spec.Grid > 0)
                foreach (int b in moved) Rebuild(data, now, ArtOf(_was[b]), ArtOf(area(b)));
            // Slide bands one at a time, so each one sees the earlier ones already moved.
            List<int> bands = Spec.Buttons.Where(Slides).ToList();
            Dictionary<int, (int Top, int Bottom, int Left, int Right)> at = bands.ToDictionary(b => b, b => _was[b]);
            foreach (int b in bands)
            {
                int dx = (area(b).Left - _was[b].Left) / Tile;
                if (dx != 0) Slide(data, b, at.Select(kv => (kv.Key, kv.Value)).ToList(), dx);
                at[b] = area(b);
            }
        }

        private static IEnumerable<(int X, int Y)> Tiles((int X0, int Y0, int X1, int Y1) r) =>
            Enumerable.Range(r.Y0, r.Y1 - r.Y0 + 1).SelectMany(y => Enumerable.Range(r.X0, r.X1 - r.X0 + 1).Select(x => (x, y)));

        // A box tile that moves onto or off a grid line copies the tile at the same spot in a box with the same grid lines under it.
        private void Rebuild(byte[] data, IReadOnlyList<(int X0, int Y0, int X1, int Y1)> boxes, (int X0, int Y0, int X1, int Y1) from,
                             (int X0, int Y0, int X1, int Y1) to)
        {
            (bool[] columns, bool[] rows) = Lines(_asRead);
            int g = Spec.Grid;
            (bool, bool) Kind(int x, int y) => (columns[x % g], rows[y % g]);
            List<(int X0, int Y0, int X1, int Y1)> alike = _was.Values.Select(ArtOf)
                .Where(b => b.X1 - b.X0 == to.X1 - to.X0 && b.Y1 - b.Y0 == to.Y1 - to.Y0).ToList();
            for (int ry = 0; ry <= to.Y1 - to.Y0; ry++)
                for (int rx = 0; rx <= to.X1 - to.X0; rx++)
                {
                    int x = to.X0 + rx, y = to.Y0 + ry;
                    if (x < 0 || y < 0 || x >= Columns || y >= Rows || Kind(x, y) == Kind(from.X0 + rx, from.Y0 + ry)) continue;
                    foreach ((int X0, int Y0, int X1, int Y1) b in alike)
                        if (Kind(b.X0 + rx, b.Y0 + ry) == Kind(x, y))
                        {
                            Set(data, x, y, At(_asRead, b.X0 + rx, b.Y0 + ry));
                            break;
                        }
                }
        }

        public int WasLeft(int button) => _was[button].Left;

        public ushort? SquareAt(byte[] data, int x, int y) => x >= 0 && y >= 0 && x < Columns && y < Rows ? At(data, x, y) : null;

        // Put the square in the kept copy too, so a later move doesn't undo it.
        public void SetSquare(byte[] data, int x, int y, ushort square)
        {
            if (x < 0 || y < 0 || x >= Columns || y >= Rows) return;
            Set(data, x, y, square);
            if (_asRead != null) Set(_asRead, x, y, square);
        }

        public bool Slides(int button) => Spec.Bands is ScreenBands b && button < b.LabelEnds.Length && b.LabelEnds[button] >= 0;

        private sealed record Band(int Top, int Bottom, int X0, int X1, int End);

        private bool Backdrop(byte[] data, int x, int y) => At(data, x, y) == Plain(data, x, y, (_, _) => false);

        // Find the band's rows inside its touch area and where it ends. The cap runs on from the button until the background shows.
        private Band BandOf(byte[] data, (int Top, int Bottom, int Left, int Right) area)
        {
            int top = area.Top / Tile, bottom = (area.Bottom - 1) / Tile, x1 = (area.Right - 1) / Tile, end = x1;
            while (end + 1 < Columns && !Backdrop(data, end + 1, top)) end++;
            return new Band(top, bottom, area.Left / Tile, x1, end);
        }

        /// <summary>How many tiles a band button can slide left, back to its label, and right, until it reaches other art.</summary>
        public (int Least, int Most) Room(int button)
        {
            Band b = BandOf(_asRead, _was[button]);
            int last = b.End;
            while (last + 1 < Columns && Enumerable.Range(b.Top, b.Bottom - b.Top + 1).All(y => Backdrop(_asRead, last + 1, y))) last++;
            return (Spec.Bands.LabelEnds[button] - (b.X0 - Spec.Bands.Lead), last - b.End);
        }

        // 0 past the band's end, 1 before the button, 2 under it, then 3, 4 and so on for each cap column.
        private static int StateOf(Band b, int x) => b == null || x > b.End ? 0 : x < b.X0 ? 1 : x <= b.X1 ? 2 : 3 + x - b.X1 - 1;

        /// <summary>Slides a band button along its band, which stretches or shrinks behind it.</summary>
        /// <param name="bands">Every band button's area, with this button still at its old one.</param>
        private void Slide(byte[] data, int button, IReadOnlyList<(int Button, (int Top, int Bottom, int Left, int Right) Area)> bands, int dx)
        {
            List<(int Button, Band Band)> all = bands.Select(b => (b.Button, BandOf(data, b.Area))).ToList();
            Band old = all.First(b => b.Button == button).Band;
            Band moved = old with { X0 = old.X0 + dx, X1 = old.X1 + dx, End = old.End + dx };
            // Keep each shared row and the bands above and below it, to copy tiles from.
            Dictionary<int, (Band Above, Band Below, ushort[] Tiles)> shared = new Dictionary<int, (Band, Band, ushort[])>();
            foreach (int y in all.SelectMany(b => new[] { b.Band.Top - 1, b.Band.Bottom + 1 }).Where(y => y >= 0 && y < Rows).Distinct())
                shared[y] = (all.Select(b => b.Band).FirstOrDefault(b => b.Bottom + 1 == y), all.Select(b => b.Band).FirstOrDefault(b => b.Top - 1 == y),
                             Enumerable.Range(0, Columns).Select(x => At(data, x, y)).ToArray());

            int labelEnd = Spec.Bands.LabelEnds[button], last = Math.Max(old.End, moved.End);
            for (int y = old.Top; y <= old.Bottom; y++)
            {
                ushort plain = At(data, Spec.Bands.Plain[button], y);
                int lead = Spec.Bands.Lead, from = old.X0 - lead, to = moved.X0 - lead;
                ushort[] block = Enumerable.Range(from, old.End - from + 1).Select(x => At(data, x, y)).ToArray();
                for (int x = labelEnd; x <= last; x++)
                    Set(data, x, y, x < to ? plain : x <= moved.End ? block[x - to] : Plain(data, x, y, (_, _) => false));
            }

            (bool[] columns, bool[] _) = Lines(data);
            foreach (int y in new[] { old.Top - 1, old.Bottom + 1 }.Where(shared.ContainsKey))
            {
                bool mineAbove = shared[y].Above == old;
                Band other = mineAbove ? shared[y].Below : shared[y].Above;
                for (int x = Math.Min(old.X0, moved.X0); x <= last; x++)
                {
                    int mine = StateOf(moved, x), theirs = StateOf(other, x);
                    if (mine == 0 && theirs == 0) { Set(data, x, y, Plain(data, x, y, (_, _) => false)); continue; }
                    // Look for a tile with the same pair of states, in this row first, then the other shared rows. If either half is empty,
                    // the tile must also sit on the same kind of grid column.
                    ushort? found = null;
                    foreach ((Band Above, Band Below, ushort[] Tiles) s in shared.OrderBy(kv => kv.Key != y).Select(kv => kv.Value))
                    {
                        Band mineThere = mineAbove ? s.Above : s.Below, otherThere = mineAbove ? s.Below : s.Above;
                        for (int c = 0; c < Columns && found == null; c++)
                            if (StateOf(mineThere, c) == mine && StateOf(otherThere, c) == theirs
                                && (mine != 0 && theirs != 0 || columns[c % Spec.Grid] == columns[x % Spec.Grid]))
                                found = s.Tiles[c];
                        if (found != null) break;
                    }
                    // If no tile matches both halves, join a top and a bottom half from tiles that each match one band.
                    if (found == null)
                    {
                        ushort? Half(bool upper)
                        {
                            int want = upper == mineAbove ? mine : theirs;
                            foreach ((Band Above, Band Below, ushort[] Tiles) s in shared.OrderBy(kv => kv.Key != y).Select(kv => kv.Value))
                                for (int c = 0; c < Columns; c++)
                                    if (StateOf(upper ? s.Above : s.Below, c) == want && (want != 0 || columns[c % Spec.Grid] == columns[x % Spec.Grid]))
                                        return s.Tiles[c];
                            return null;
                        }
                        if (Half(true) is ushort top && Half(false) is ushort bottom) found = Join(top, bottom);
                    }
                    if (found is ushort tile) Set(data, x, y, tile);
                }
            }
        }

        private ushort Plain(byte[] data, int x, int row, Func<int, int, bool> covered)
        {
            if (Spec.Grid > 0)
            {
                int first = Spec.PlainFromRow + ((row - Spec.PlainFromRow) % Spec.Grid + Spec.Grid) % Spec.Grid;
                for (int y = first; y < Rows; y += Spec.Grid)
                    if (!covered(x, y)) return BitConverter.ToUInt16(data, Entry(x, y));
            }
            int column = Enumerable.Range(Spec.FromColumn, Columns - Spec.FromColumn).FirstOrDefault(c => !covered(c, row), -1);
            return column >= 0 ? BitConverter.ToUInt16(data, Entry(column, row)) : (ushort)0;
        }

        /// <summary>Writes the screen only if it changed, since a write tells every editor that holds it.</summary>
        public void Write(byte[] bytes)
        {
            byte[] screen = bytes.Skip(Start).Take(Length).ToArray();
            if (Read(Spec) is byte[] now && now.AsSpan().SequenceEqual(screen)) return;
            ArchiveMembers.Put(Spec.Dir, Spec.Member, Packed(Raw(Spec)) ? Lz77.Compress(screen) : screen);
            if (SheetOf() is byte[] sheet)
            {
                int member = Spec.Bands.Characters;
                ArchiveMembers.Put(Spec.Dir, member, Packed(Raw(Spec.Dir, member)) ? Lz77.Compress(sheet) : sheet);
                _sheet = Sheet.Read(sheet);
            }
        }
    }
}
