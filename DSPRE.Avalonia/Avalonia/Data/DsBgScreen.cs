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

        // A BG engine can place characters up to 256 KB in.
        private readonly byte[] _vram = new byte[0x40000];
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
            byte[] pixels = NitroBgCodec.ReadCharacters(ncgr);
            int dst = charBase + tileOffset * 32;
            int n = Math.Min(pixels.Length, _vram.Length - dst);
            if (n > 0 && dst >= 0) Array.Copy(pixels, 0, _vram, dst, n);
        }

        /// <summary>Sixteen colours out of a palette file, row <paramref name="row"/>.</summary>
        public static ushort[] Row(ushort[] colours, int row)
        {
            ushort[] result = new ushort[16];
            if (colours != null)
                for (int i = 0; i < 16; i++)
                    if (row * 16 + i < colours.Length) result[i] = colours[row * 16 + i];
            return result;
        }

        /// <summary>Puts sixteen colours into palette slot <paramref name="slot"/>, starting at colour <paramref name="from"/>.</summary>
        public void SetPalette(int slot, ushort[] colours, int from = 0)
        {
            for (int i = 0; i < 16; i++)
                _colours[slot * 16 + i] = colours != null && from + i < colours.Length ? colours[from + i] : (ushort)0;
        }

        public void LoadMap(int bg, ushort[] entries, int widthTiles)
        {
            ushort[] map = _layers[bg]?.Map;
            if (map == null || entries == null || widthTiles <= 0) return;
            for (int i = 0; i < entries.Length; i++)
            {
                int x = i % widthTiles, y = i / widthTiles;
                if (x < MapSide && y < MapSide) map[y * MapSide + x] = entries[i];
            }
        }

        public void Fill(int bg, int tile, int x, int y, int w, int h, int palette)
        {
            ushort[] map = _layers[bg]?.Map;
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
            ushort[] map = _layers[bg]?.Map;
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

        private static void Put(byte[] rgba, int at, ushort c)
        {
            (rgba[at], rgba[at + 1], rgba[at + 2]) = NitroBgCodec.Colour(c);
            rgba[at + 3] = 255;
        }

        /// <summary>The screen as 256 by 192 straight RGBA.</summary>
        public byte[] Render()
        {
            byte[] rgba = new byte[Width * Height * 4];
            List<Layer> order = new List<Layer>();
            for (int bg = 0; bg < 4; bg++) if (_layers[bg] != null && _layers[bg].Visible) order.Add(_layers[bg]);
            // Equal priorities go by layer number, which is the order they were added in.
            List<Layer> sorted = new List<Layer>();
            for (int p = 0; p < 4; p++) foreach (Layer l in order) if (l.Priority == p) sorted.Add(l);

            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    ushort c = _colours[0];
                    foreach (Layer layer in sorted)
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

        /// <summary>The cells of a cell bank, each a list of sprites with the first on top, tile numbers in characters.</summary>
        public static List<Oam[]> ReadCells(byte[] ncer)
        {
            List<Oam[]> cells = new List<Oam[]>();
            NcerFile file = NcerFile.Read(ncer);
            if (file == null) return cells;
            foreach (NcerFile.Cell cell in file.Cells)
            {
                Oam[] list = new Oam[cell.Pieces.Count];
                for (int k = 0; k < list.Length; k++)
                {
                    NcerFile.Piece p = cell.Pieces[k];
                    list[k] = new Oam
                    {
                        X = p.X, Y = p.Y, Width = p.Width, Height = p.Height, FlipH = p.FlipH, FlipV = p.FlipV,
                        Tile = file.CharacterOf(cell, p), Palette = p.Palette,
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
        /// <param name="under">
        /// The backgrounds alone, to blend against. The hardware blends a sprite with the background under
        /// it, never with another sprite, so two dimmed sprites overlapping do not darken each other.
        /// </param>
        public static void DrawCell(byte[] rgba, Oam[] cell, byte[] characters, Func<int, ushort[]> paletteFor,
                                    int ox, int oy, bool translucent = false, byte[] under = null)
        {
            under ??= rgba;
            if (rgba == null || cell == null || characters == null) return;
            for (int k = cell.Length - 1; k >= 0; k--)
            {
                Oam s = cell[k];
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
                        (byte r, byte g, byte b) = NitroBgCodec.Colour(c);
                        rgba[p] = (byte)((r * 6 + under[p] * 9) / 16);
                        rgba[p + 1] = (byte)((g * 6 + under[p + 1] * 9) / 16);
                        rgba[p + 2] = (byte)((b * 6 + under[p + 2] * 9) / 16);
                        rgba[p + 3] = 255;
                    }
            }
        }

        /// <summary>Pastes a picture with its top left at x, y, leaving its see-through pixels alone.</summary>
        public static void DrawImage(byte[] rgba, DSPRE.RawImage image, int x, int y)
        {
            if (rgba == null || image == null || image.IsEmpty) return;
            for (int yy = 0; yy < image.Height; yy++)
                for (int xx = 0; xx < image.Width; xx++)
                {
                    int X = x + xx, Y = y + yy;
                    if (X < 0 || Y < 0 || X >= Width || Y >= Height) continue;
                    int s = (yy * image.Width + xx) * 4;
                    if (image.Bgra[s + 3] < 128) continue;
                    int p = (Y * Width + X) * 4;
                    rgba[p] = image.Bgra[s + 2];
                    rgba[p + 1] = image.Bgra[s + 1];
                    rgba[p + 2] = image.Bgra[s];
                    rgba[p + 3] = 255;
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
                Oam s = cell[k];
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
