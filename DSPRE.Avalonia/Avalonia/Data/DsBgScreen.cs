using System;
using System.Collections.Generic;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// One DS screen put together the way the hardware does it: sixteen-colour tile data at character bases,
    /// sixteen palettes of sixteen colours, and up to four 32 by 32 tile arrangements drawn in priority order
    /// over the backdrop, colour 0 of palette 0. Sprites and text are then drawn over the result.
    /// </summary>
    public sealed class DsBgScreen
    {
        public const int Width = 256, Height = 192, TileSize = 8, MapSide = 32;

        private sealed class Layer
        {
            public int Priority, CharBase;
            public bool Visible = true;
            public readonly ushort[] Map = new ushort[MapSide * MapSide];
        }

        private readonly byte[] _vram = new byte[0x10000];
        private readonly ushort[] _colours = new ushort[16 * 16];
        private readonly Layer[] _layers = new Layer[4];

        public void InitLayer(int bg, int priority, int charBase) =>
            _layers[bg] = new Layer { Priority = priority, CharBase = charBase };

        public void SetVisible(int bg, bool visible)
        {
            if (_layers[bg] != null) _layers[bg].Visible = visible;
        }

        /// <summary>Copies a sixteen-colour drawing's pixels in at a character base, from tile <paramref name="tileOffset"/> on.</summary>
        public void LoadTiles(int charBase, byte[] ncgr, int tileOffset = 0)
        {
            byte[] pixels = ReadCharacters(ncgr);
            int dst = charBase + tileOffset * 32;
            int n = Math.Min(pixels.Length, _vram.Length - dst);
            if (n > 0 && dst >= 0) Array.Copy(pixels, 0, _vram, dst, n);
        }

        /// <summary>A sixteen-colour drawing's pixels, half a byte each, 32 bytes a tile.</summary>
        public static byte[] ReadCharacters(byte[] ncgr)
        {
            if (ncgr == null) return Array.Empty<byte>();
            var (eightBit, at) = NitroBgCodec.ReadTileHeader(ncgr);
            if (eightBit) return Array.Empty<byte>();
            int rahc = NitroBgCodec.Find(ncgr, "RAHC", 0);
            int size = rahc >= 0 ? NitroBgCodec.U32(ncgr, rahc + 0x18) : ncgr.Length - at;
            size = Math.Max(0, Math.Min(size, ncgr.Length - at));
            var pixels = new byte[size];
            Array.Copy(ncgr, at, pixels, 0, size);
            return pixels;
        }

        /// <summary>The raw 15-bit colours of a palette file, every row of them.</summary>
        public static ushort[] ReadColours(byte[] nclr)
        {
            if (nclr == null) return Array.Empty<ushort>();
            int pltt = NitroBgCodec.Find(nclr, "TTLP", 0);
            if (pltt < 0) return Array.Empty<ushort>();
            int size = NitroBgCodec.U32(nclr, pltt + 0x10);
            int at = pltt + 0x18;
            int count = Math.Max(0, Math.Min(size, nclr.Length - at)) / 2;
            var colours = new ushort[count];
            for (int i = 0; i < count; i++) colours[i] = (ushort)NitroBgCodec.U16(nclr, at + i * 2);
            return colours;
        }

        /// <summary>Sixteen colours out of a palette file, row <paramref name="row"/>.</summary>
        public static ushort[] Row(ushort[] colours, int row)
        {
            var result = new ushort[16];
            if (colours != null)
                for (int i = 0; i < 16; i++)
                    if (row * 16 + i < colours.Length) result[i] = colours[row * 16 + i];
            return result;
        }

        /// <summary>A screen arrangement's entries, row after row, and how many tiles wide it says it is.</summary>
        public static (int WidthTiles, ushort[] Entries) ReadMap(byte[] nscr)
        {
            if (nscr == null) return (0, Array.Empty<ushort>());
            int nrcs = NitroBgCodec.Find(nscr, "NRCS", 0);
            if (nrcs < 0) return (0, Array.Empty<ushort>());
            int w = NitroBgCodec.U16(nscr, nrcs + 0x08);
            int size = NitroBgCodec.U32(nscr, nrcs + 0x10);
            int at = nrcs + 0x14;
            int entryBytes = NitroBgCodec.EntryBytes(nscr);
            int count = Math.Max(0, Math.Min(size, nscr.Length - at)) / entryBytes;
            var entries = new ushort[count];
            for (int i = 0; i < count; i++) entries[i] = (ushort)NitroBgCodec.EntryAt(nscr, at, i, entryBytes);
            return (w / TileSize, entries);
        }

        /// <summary>Puts sixteen colours into palette slot <paramref name="slot"/>, starting at colour <paramref name="from"/>.</summary>
        public void SetPalette(int slot, ushort[] colours, int from = 0)
        {
            for (int i = 0; i < 16; i++)
                _colours[slot * 16 + i] = colours != null && from + i < colours.Length ? colours[from + i] : (ushort)0;
        }

        public void LoadMap(int bg, ushort[] entries, int widthTiles)
        {
            var map = _layers[bg]?.Map;
            if (map == null || entries == null || widthTiles <= 0) return;
            for (int i = 0; i < entries.Length; i++)
            {
                int x = i % widthTiles, y = i / widthTiles;
                if (x < MapSide && y < MapSide) map[y * MapSide + x] = entries[i];
            }
        }

        public void Fill(int bg, int tile, int x, int y, int w, int h, int palette)
        {
            var map = _layers[bg]?.Map;
            if (map == null) return;
            for (int yy = y; yy < y + h; yy++)
                for (int xx = x; xx < x + w; xx++)
                    if (xx >= 0 && yy >= 0 && xx < MapSide && yy < MapSide)
                        map[yy * MapSide + xx] = (ushort)((palette << 12) | (tile & 0x3FF));
        }

        /// <summary>Pastes a w by h block of entries at tile x, y.</summary>
        public void Put(int bg, ushort[] block, int x, int y, int w, int h) => Copy(bg, x, y, w, h, block, 0, 0, w);

        /// <summary>Copies a w by h block out of a wider arrangement to tile dx, dy.</summary>
        public void Copy(int bg, int dx, int dy, int w, int h, ushort[] source, int sx, int sy, int sourceWidth)
        {
            var map = _layers[bg]?.Map;
            if (map == null || source == null) return;
            for (int yy = 0; yy < h; yy++)
                for (int xx = 0; xx < w; xx++)
                {
                    int from = (sy + yy) * sourceWidth + sx + xx;
                    int tx = dx + xx, ty = dy + yy;
                    if (from < source.Length && tx >= 0 && ty >= 0 && tx < MapSide && ty < MapSide)
                        map[ty * MapSide + tx] = source[from];
                }
        }

        private static byte Expand(int five) => (byte)((five << 3) | (five >> 2));

        private static void Put(byte[] rgba, int at, ushort c)
        {
            rgba[at] = Expand(c & 31);
            rgba[at + 1] = Expand((c >> 5) & 31);
            rgba[at + 2] = Expand((c >> 10) & 31);
            rgba[at + 3] = 255;
        }

        /// <summary>The screen as 256 by 192 straight RGBA.</summary>
        public byte[] Render()
        {
            var rgba = new byte[Width * Height * 4];
            var order = new List<Layer>();
            for (int bg = 0; bg < 4; bg++) if (_layers[bg] != null && _layers[bg].Visible) order.Add(_layers[bg]);
            // Equal priorities go by layer number, which is the order they were added in.
            var sorted = new List<Layer>();
            for (int p = 0; p < 4; p++) foreach (var l in order) if (l.Priority == p) sorted.Add(l);

            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    ushort c = _colours[0];
                    foreach (var layer in sorted)
                    {
                        ushort e = layer.Map[(y / TileSize) * MapSide + x / TileSize];
                        int tile = e & 0x3FF, palette = e >> 12;
                        int lx = x % TileSize, ly = y % TileSize;
                        if ((e & 0x400) != 0) lx = 7 - lx;
                        if ((e & 0x800) != 0) ly = 7 - ly;
                        int at = layer.CharBase + tile * 32 + (ly * 8 + lx) / 2;
                        if (at >= _vram.Length) continue;
                        int index = ((ly * 8 + lx) & 1) != 0 ? _vram[at] >> 4 : _vram[at] & 0xF;
                        if (index == 0) continue;
                        c = _colours[palette * 16 + index];
                        break;
                    }
                    Put(rgba, (y * Width + x) * 4, c);
                }
            return rgba;
        }

        // ── sprites ──────────────────────────────────────────────────────────────────

        /// <summary>One hardware sprite of a cell: where it sits from the cell's centre, its size and its tiles.</summary>
        public sealed class Oam
        {
            public int X, Y, Width, Height, Tile, Palette;
            public bool FlipH, FlipV;
        }

        private static readonly (int w, int h)[,] OamSizes =
        {
            { (8, 8), (16, 16), (32, 32), (64, 64) },
            { (16, 8), (32, 8), (32, 16), (64, 32) },
            { (8, 16), (8, 32), (16, 32), (32, 64) },
            { (8, 8), (8, 8), (8, 8), (8, 8) },
        };

        /// <summary>
        /// The cells of a cell bank, each a list of sprites with the first on top. Where a file carries a
        /// transfer partition per bank, tile numbers count from that bank's own slice of the sheet, and the
        /// offset is folded in here so callers need not know.
        /// </summary>
        public static List<Oam[]> ReadCells(byte[] ncer)
        {
            var cells = new List<Oam[]>();
            if (ncer == null) return cells;
            int kbec = NitroBgCodec.Find(ncer, "KBEC", 0);
            if (kbec < 0) return cells;
            int count = NitroBgCodec.U16(ncer, kbec + 8);
            int type = NitroBgCodec.U16(ncer, kbec + 10);
            int table = kbec + 8 + NitroBgCodec.U32(ncer, kbec + 12);
            int entry = type == 1 ? 16 : 8;
            int oams = table + count * entry;

            // A piece's tile number counts in units of the sprite mapping's boundary, not in single tiles.
            // Trainer layouts record mapping 1, so their numbers step in twos: a 64 by 64 body at 0 fills
            // 64 tiles and the next piece says 32, which only stops overlapping the body once doubled.
            int mapping = (int)NitroBgCodec.U32(ncer, kbec + 16) & 0xFF;
            int perUnit = 1 << Math.Clamp(mapping, 0, 3);

            // Where each bank's slice of the sheet begins, in tiles. Zero when the file keeps no partitions,
            // which is how the Pokétch and the battle objects are stored.
            var firstTile = new int[count];
            int partitions = (int)NitroBgCodec.U32(ncer, kbec + 20);
            if (partitions != 0)
            {
                int head = kbec + 8 + partitions;
                if (head + 8 <= ncer.Length)
                {
                    int list = head + (int)NitroBgCodec.U32(ncer, head + 4);
                    for (int i = 0; i < count; i++)
                    {
                        int at = list + i * 8;
                        if (at + 8 > ncer.Length) break;
                        firstTile[i] = (int)NitroBgCodec.U32(ncer, at) / 32;
                    }
                }
            }

            for (int i = 0; i < count; i++)
            {
                int at = table + i * entry;
                if (at + 8 > ncer.Length) break;
                int n = NitroBgCodec.U16(ncer, at);
                int from = oams + NitroBgCodec.U32(ncer, at + 4);
                var list = new Oam[Math.Max(0, n)];
                for (int k = 0; k < n; k++)
                {
                    int o = from + k * 6;
                    if (o + 6 > ncer.Length) { Array.Resize(ref list, k); break; }
                    int a0 = NitroBgCodec.U16(ncer, o), a1 = NitroBgCodec.U16(ncer, o + 2), a2 = NitroBgCodec.U16(ncer, o + 4);
                    int y = a0 & 0xFF; if (y > 127) y -= 256;
                    int x = a1 & 0x1FF; if (x > 255) x -= 512;
                    var (w, h) = OamSizes[(a0 >> 14) & 3, (a1 >> 14) & 3];
                    list[k] = new Oam
                    {
                        X = x, Y = y, Width = w, Height = h,
                        FlipH = ((a1 >> 12) & 1) != 0, FlipV = ((a1 >> 13) & 1) != 0,
                        Tile = (a2 & 0x3FF) * perUnit + firstTile[i], Palette = a2 >> 12,
                    };
                }
                cells.Add(list);
            }
            return cells;
        }

        /// <summary>
        /// Draws a cell centred on screen pixel ox, oy. A translucent one is blended six sixteenths over nine
        /// sixteenths of what is under it, the way the touch menu dims its buttons.
        /// </summary>
        public static void DrawCell(byte[] rgba, Oam[] cell, byte[] characters, Func<int, ushort[]> paletteFor,
                                    int ox, int oy, bool translucent = false)
        {
            if (rgba == null || cell == null || characters == null) return;
            for (int k = cell.Length - 1; k >= 0; k--)
            {
                var s = cell[k];
                if (s == null) continue;
                ushort[] palette = paletteFor(s.Palette);
                int tilesWide = Math.Max(1, s.Width / 8);
                for (int yy = 0; yy < s.Height; yy++)
                    for (int xx = 0; xx < s.Width; xx++)
                    {
                        int X = ox + s.X + xx, Y = oy + s.Y + yy;
                        if (X < 0 || Y < 0 || X >= Width || Y >= Height) continue;
                        int sx = s.FlipH ? s.Width - 1 - xx : xx, sy = s.FlipV ? s.Height - 1 - yy : yy;
                        int tile = s.Tile + (sy / 8) * tilesWide + sx / 8;
                        int bit = (sy % 8) * 8 + sx % 8;
                        int at = tile * 32 + bit / 2;
                        if (at >= characters.Length) continue;
                        int index = (bit & 1) != 0 ? characters[at] >> 4 : characters[at] & 0xF;
                        if (index == 0 || palette == null) continue;
                        ushort c = palette[index];
                        int p = (Y * Width + X) * 4;
                        if (!translucent) { Put(rgba, p, c); continue; }
                        rgba[p] = (byte)((Expand(c & 31) * 6 + rgba[p] * 9) / 16);
                        rgba[p + 1] = (byte)((Expand((c >> 5) & 31) * 6 + rgba[p + 1] * 9) / 16);
                        rgba[p + 2] = (byte)((Expand((c >> 10) & 31) * 6 + rgba[p + 2] * 9) / 16);
                    }
            }
        }

        /// <summary>
        /// Draws a cell turned and stretched about its own centre. Animations that carry a turn are the
        /// reason this exists: the Analog Watch stores its hands as one drawing at 420 different angles, so
        /// without a turn the preview shows a hand that never moves.
        ///
        /// The work is done backwards, a screen pixel at a time: each one is mapped back through the turn to
        /// ask which pixel of the drawing belongs there. Going forwards instead leaves gaps wherever the
        /// stretch spreads the source out.
        /// </summary>
        public static void DrawCellTurned(byte[] rgba, Oam[] cell, byte[] characters,
                                          Func<int, ushort[]> paletteFor, int ox, int oy,
                                          double degrees, double scaleX, double scaleY)
        {
            if (rgba == null || cell == null || characters == null) return;
            if (scaleX == 0 || scaleY == 0) return;

            // Near enough to straight and unstretched: the plain draw is sharper, so use it.
            if (Math.Abs(degrees) < 0.01 && Math.Abs(scaleX - 1) < 0.001 && Math.Abs(scaleY - 1) < 0.001)
            {
                DrawCell(rgba, cell, characters, paletteFor, ox, oy);
                return;
            }

            double radians = degrees * Math.PI / 180.0;
            double cos = Math.Cos(radians), sin = Math.Sin(radians);

            for (int k = cell.Length - 1; k >= 0; k--)
            {
                var s = cell[k];
                if (s == null) continue;
                ushort[] palette = paletteFor(s.Palette);
                if (palette == null) continue;
                int tilesWide = Math.Max(1, s.Width / 8);

                // How far the turned and stretched sprite can reach, so no pixel of it is missed.
                double spread = Math.Sqrt(s.Width * s.Width + s.Height * s.Height)
                              * Math.Max(Math.Abs(scaleX), Math.Abs(scaleY));
                int reach = (int)Math.Ceiling(spread / 2) + 2;

                // A cell turns about its own origin, not about each piece separately, so the piece's offset
                // from that origin is carried through the turn first. Leaving it out pins a clock hand in
                // place and spins it where it stands, instead of swinging it round the dial.
                double offX = (s.X + s.Width / 2.0) * scaleX, offY = (s.Y + s.Height / 2.0) * scaleY;
                double midX = ox + offX * cos - offY * sin;
                double midY = oy + offX * sin + offY * cos;

                for (int dy = -reach; dy <= reach; dy++)
                    for (int dx = -reach; dx <= reach; dx++)
                    {
                        int X = (int)Math.Round(midX) + dx, Y = (int)Math.Round(midY) + dy;
                        if (X < 0 || Y < 0 || X >= Width || Y >= Height) continue;

                        // Back through the turn, then the stretch, into the drawing's own space.
                        double ux = (X + 0.5 - midX), uy = (Y + 0.5 - midY);
                        double rx = (ux * cos + uy * sin) / scaleX;
                        double ry = (-ux * sin + uy * cos) / scaleY;

                        int px = (int)Math.Floor(rx + s.Width / 2.0);
                        int py = (int)Math.Floor(ry + s.Height / 2.0);
                        if (px < 0 || py < 0 || px >= s.Width || py >= s.Height) continue;

                        int sx = s.FlipH ? s.Width - 1 - px : px;
                        int sy = s.FlipV ? s.Height - 1 - py : py;
                        int tile = s.Tile + (sy / 8) * tilesWide + sx / 8;
                        int bit = (sy % 8) * 8 + sx % 8;
                        int at = tile * 32 + bit / 2;
                        if (at < 0 || at >= characters.Length) continue;
                        int index = (bit & 1) != 0 ? characters[at] >> 4 : characters[at] & 0xF;
                        if (index == 0 || index >= palette.Length) continue;
                        Put(rgba, (Y * Width + X) * 4, palette[index]);
                    }
            }
        }

        // ── text ─────────────────────────────────────────────────────────────────────

        /// <summary>How wide a line comes out in the ROM's font, in pixels.</summary>
        public static int MeasureText(FieldFont font, string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            if (font == null || !FieldFontCharacters.Ready) return text.Length * 6;
            return font.Measure(text, FieldFontCharacters.GlyphFor);
        }

        /// <summary>Writes a line in the ROM's font with its letter and shadow colours, as 15-bit colours.</summary>
        public static void DrawText(byte[] rgba, FieldFont font, string text, int x, int y, ushort letter, ushort shadow)
        {
            if (rgba == null || string.IsNullOrEmpty(text) || font == null || !FieldFontCharacters.Ready) return;
            int pen = x;
            foreach (char ch in text)
            {
                int g = FieldFontCharacters.GlyphFor(ch);
                int advance = font.WidthOf(g);
                if (g >= 0)
                    for (int gy = 0; gy < FieldFont.CellSize; gy++)
                        for (int gx = 0; gx < advance; gx++)
                        {
                            byte v = font.PixelAt(g, gx, gy);
                            if (v == FieldFont.Nothing || v == FieldFont.Paper) continue;
                            int X = pen + gx, Y = y + gy;
                            if (X < 0 || Y < 0 || X >= Width || Y >= Height) continue;
                            Put(rgba, (Y * Width + X) * 4, v == 1 ? letter : shadow);
                        }
                pen += advance > 0 ? advance : 6;
            }
        }

        /// <summary>Darkens the whole picture towards black, 0 for black and 1 for untouched, the way a fade does.</summary>
        public static void Fade(byte[] rgba, double brightness)
        {
            if (rgba == null || brightness >= 1) return;
            brightness = Math.Max(0, brightness);
            for (int i = 0; i < rgba.Length; i += 4)
            {
                rgba[i] = (byte)(rgba[i] * brightness);
                rgba[i + 1] = (byte)(rgba[i + 1] * brightness);
                rgba[i + 2] = (byte)(rgba[i + 2] * brightness);
            }
        }
    }
}
