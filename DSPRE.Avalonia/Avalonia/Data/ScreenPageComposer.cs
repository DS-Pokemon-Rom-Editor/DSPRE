using System;
using System.Collections.Generic;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Data
{
    public enum ScreenLayerKind { Background, Sprite, Pokemon }

    /// <summary>One layer of a composed page on its own, transparent where it draws nothing.</summary>
    public sealed class ScreenLayerImage
    {
        public string Name;
        public ScreenLayerKind Kind;
        /// <summary>The background's number, or the sprite's or picture's position in its list.</summary>
        public int Index;
        public int Priority;
        public byte[] Rgba;
        /// <summary>Blend weights in sixteenths when the layer is translucent: its own, then what is under it.</summary>
        public int Blend, BlendUnder;
    }

    public sealed class ComposedScreen
    {
        public byte[] Rgba;
        /// <summary>Back to front, the order they were drawn in.</summary>
        public List<ScreenLayerImage> Layers = new();
    }

    public sealed class ScreenPicture
    {
        public byte[] Rgba;
        public int Width, Height;
    }

    /// <summary>
    /// Puts a page of a DS screen together the way the hardware draws it (the Pokédex pages, the summary screen): each background from its drawing, its pasted
    /// arrangements and the colours copied into palette memory; then sprites, the sample Pokémon and text.
    /// Higher priority numbers are further back, a sprite draws over a background of equal priority, and of
    /// two backgrounds with equal priority the lower-numbered one is in front.
    /// </summary>
    public sealed class ScreenPageComposer
    {
        public const int Width = 256, Height = 192;

        private readonly Func<int, byte[]> _member;
        private readonly Func<int, FieldFont> _font;
        private readonly Func<ScreenText, string, string> _text;
        private readonly Func<ScreenMon, ScreenPicture> _mon;
        private readonly Func<int, int> _type;
        private readonly Func<int, int> _bank;

        /// <summary>The sample's size data for a page state, for the pieces of a size page.</summary>
        public Func<string, DexSizeInfo> Size { get; set; }

        /// <summary>The sample's habitat for a page state, where the page has a habitat map.</summary>
        public Func<string, DexHabitatData> Habitat { get; set; }
        /// <summary>A member of another archive, for a sprite whose files live there.</summary>
        public Func<DSPRE.RomInfo.DirNames, int, byte[]> Other { get; set; }
        private readonly Dictionary<(DSPRE.RomInfo.DirNames, int), byte[]> _others = new();

        // A sprite's file, from the archive it names for that file.
        private byte[] File(DSPRE.RomInfo.DirNames? from, int index)
        {
            if (from is not DSPRE.RomInfo.DirNames dir) return File(index);
            if (index < 0) return null;
            if (_others.TryGetValue((dir, index), out byte[] known)) return known;
            byte[] data = null;
            try { data = NitroBgCodec.Inflate(Other?.Invoke(dir, index)); } catch { }
            if (data != null) _others[(dir, index)] = data;
            return data;
        }

        /// <summary>A sprite's picture from another archive, for a sprite that names one.</summary>
        public Func<ScreenSprite, ScreenPicture> Picture { get; set; }
        /// <summary>A digit sprite's animation and its offset from the sprite's position, or null when it is hidden.</summary>
        public Func<DexReadout, int, int, (int Seq, int Dx)?> Digit { get; set; }
        private readonly Dictionary<int, byte[]> _files = new();

        /// <param name="type">For a type slot (1 or 2), the type's place in the Pokédex's type order, or -1.</param>
        /// <param name="bank">For a drawing, the colours row the game shows it with (HGSS type icons).</param>
        /// <param name="text">A label's words in a page state.</param>
        public ScreenPageComposer(Func<int, byte[]> member, Func<int, FieldFont> font, Func<ScreenText, string, string> text,
                               Func<ScreenMon, ScreenPicture> mon, Func<int, int> type, Func<int, int> bank)
        {
            _member = member;
            _font = font;
            _text = text;
            _mon = mon;
            _type = type;
            _bank = bank;
        }

        /// <summary>Forgets the archive files read so far, so an edited file is read again.</summary>
        public void Forget(int index = -1)
        {
            if (index < 0) { _files.Clear(); _others.Clear(); }
            else _files.Remove(index);
        }

        private byte[] File(int index)
        {
            if (index < 0) return null;
            if (_files.TryGetValue(index, out byte[] known)) return known;
            byte[] raw = null;
            try { raw = _member(index); } catch { }
            byte[] data = NitroBgCodec.Inflate(raw);
            // A read that failed (a file another window is still writing) is tried again next time.
            if (data != null) _files[index] = data;
            return data;
        }

        private string _variant = "";

        public ComposedScreen Compose(ScreenPage page, string variant, ScreenSide side)
        {
            _variant = variant ?? "";
            ScreenLayout screen = page.Side(side);
            ushort[] palette = BgPalette(screen, variant);
            List<(int Back, ScreenLayerImage Image)> drawn = new();

            foreach (ScreenBg bg in screen.Bgs)
            {
                if (!ScreenPage.Shows(bg.When, variant)) continue;
                byte[] rgba = new byte[Width * Height * 4];
                DrawBg(rgba, bg, palette, variant);
                DexHabitatData habitat = screen.Habitat != null && screen.Habitat.Bg == bg.Bg ? Habitat?.Invoke(variant) : null;
                if (habitat != null) DrawHabitat(rgba, screen.Habitat, habitat, palette);
                // A text window takes the layer's squares under it: unless the game fills it with the
                // layer's own pattern, what was there is gone.
                foreach (ScreenText t in screen.Texts)
                    if (t.Colours < 0 && t.Bg == bg.Bg && t.WinW > 0 && !t.KeepUnder && ScreenPage.Shows(t.When, variant))
                        Clear(rgba, t.WinX * 8, t.WinY * 8, t.WinW * 8, t.WinH * 8);
                foreach (ScreenText t in screen.Texts)
                    if (t.Colours < 0 && t.Bg == bg.Bg && ScreenPage.Shows(t.When, variant)) DrawText(rgba, t, palette);
                foreach (ScreenRect r in screen.Rects)
                    if (r.Bg == bg.Bg && ScreenPage.Shows(r.When, variant)) FillRect(rgba, r, palette);
                ScreenLayerImage image = new() { Name = "Background " + bg.Bg, Kind = ScreenLayerKind.Background, Index = bg.Bg, Priority = bg.Priority, Rgba = rgba };
                // Backgrounds of one priority sit behind its sprites; among them the lower number is in front.
                drawn.Add((bg.Priority * 1000 + 900 + bg.Bg, image));
            }

            for (int i = 0; i < screen.Mons.Count; i++)
            {
                ScreenMon m = screen.Mons[i];
                if (!ScreenPage.Shows(m.When, variant)) continue;
                ScreenPicture pic = _mon?.Invoke(m);
                if (pic?.Rgba == null) continue;
                byte[] rgba = new byte[Width * Height * 4];
                (int mx, int my, double scale, _) = Place(m.Size, m.X, m.Y);
                if (Math.Abs(scale - 1) < 0.001) Paste(rgba, pic, mx - pic.Width / 2, my - pic.Height / 2);
                else PasteScaled(rgba, pic, mx, my, scale);
                if (m.Darken > 0) Darken(rgba, m.Darken);
                ScreenLayerImage image = new() { Name = MonName(m.Kind), Kind = ScreenLayerKind.Pokemon, Index = i, Priority = m.Priority, Rgba = rgba };
                // The battle-style picture is drawn on the 3D layer, background 0.
                int back = m.Kind is ScreenMonKind.Front or ScreenMonKind.Back ? m.Priority * 1000 + 900 : m.Priority * 1000 + 50 + i;
                drawn.Add((back, image));
            }

            for (int i = 0; i < screen.Sprites.Count; i++)
            {
                ScreenSprite s = screen.Sprites[i];
                if (!ScreenPage.Shows(s.When, variant)) continue;
                byte[] rgba = new byte[Width * Height * 4];
                DrawSprite(rgba, s);
                ScreenLayerImage image = new() { Name = s.Name ?? "Sprite " + i, Kind = ScreenLayerKind.Sprite, Index = i, Priority = s.Priority, Rgba = rgba };
                // Earlier sprites are in front of later ones.
                drawn.Add((s.Priority * 1000 + 100 + Math.Min(399, i) * 2, image));
            }

            if (screen.Habitat != null && Habitat?.Invoke(variant) is DexHabitatData places)
            {
                DexHabitat h = screen.Habitat;
                if (places.Dungeons.Count > 0)
                {
                    byte[] rgba = new byte[Width * Height * 4];
                    foreach ((int x, int y, int seq) in places.Dungeons)
                        DrawSprite(rgba, new ScreenSprite { Cells = h.Cells, Drawing = h.Drawing, Anim = h.Anim, Seq = seq, Colours = h.Colours,
                                                         X = h.Absolute ? x : h.DungeonX + x * h.Cell,
                                                         Y = h.Absolute ? y : h.DungeonY + y * h.Cell });
                    drawn.Add((h.Priority * 1000 + 99, new ScreenLayerImage { Name = h.Absolute ? "Places" : "Caves and buildings", Kind = ScreenLayerKind.Sprite,
                                                                         Index = -1000, Priority = h.Priority, Rgba = rgba,
                                                                         Blend = h.Blend, BlendUnder = h.BlendUnder }));
                }
                if (h.Face >= 0 && places.Player is (int px, int py))
                {
                    byte[] rgba = new byte[Width * Height * 4];
                    DrawSprite(rgba, new ScreenSprite { Cells = h.Face, Drawing = h.FaceDrawing, Anim = h.FaceAnim, Colours = h.FaceColours, X = px, Y = py });
                    drawn.Add((h.Priority * 1000 + 98, new ScreenLayerImage { Name = "Player", Kind = ScreenLayerKind.Sprite, Index = -1001,
                                                                         Priority = h.Priority, Rgba = rgba }));
                }
            }

            for (int i = 0; i < screen.Texts.Count; i++)
            {
                ScreenText t = screen.Texts[i];
                if (t.Colours < 0 || !ScreenPage.Shows(t.When, variant)) continue;
                byte[] rgba = new byte[Width * Height * 4];
                DrawText(rgba, t, NitroBgCodec.ReadColours(File(t.Colours)));
                ScreenLayerImage image = new() { Name = LabelName(t), Kind = ScreenLayerKind.Sprite, Index = -1 - i, Priority = t.Priority, Rgba = rgba };
                // A label sits just in front of the sprite it belongs to, or in front of all of them.
                drawn.Add((t.Above >= 0 ? t.Priority * 1000 + 100 + Math.Min(399, t.Above) * 2 - 1 : t.Priority * 1000 + 10, image));
            }

            drawn.Sort((a, b) => b.Back.CompareTo(a.Back));
            ComposedScreen result = new() { Rgba = new byte[Width * Height * 4] };
            Fill(result.Rgba, palette.Length > 0 ? palette[0] : (ushort)0);
            foreach ((int _, ScreenLayerImage image) in drawn)
            {
                if (image.Blend > 0) Blend(result.Rgba, image.Rgba, image.Blend, image.BlendUnder);
                else Over(result.Rgba, image.Rgba);
                result.Layers.Add(image);
            }
            if (screen.Darken > 0) Darken(result.Rgba, screen.Darken);
            return result;
        }

        // A label is named by what it says, on one line.
        private string LabelName(ScreenText t)
        {
            string text = null;
            try { text = _text?.Invoke(t, _variant); } catch { }
            if (string.IsNullOrWhiteSpace(text)) return "Label";
            text = text.Replace((char)13, ' ').Replace((char)10, ' ').Trim();
            return "Label: " + (text.Length > 24 ? text.Substring(0, 24) + "…" : text);
        }

        private static string MonName(ScreenMonKind kind) => kind switch
        {
            ScreenMonKind.Front => "Pokémon",
            ScreenMonKind.Footprint => "Footprint",
            ScreenMonKind.Back => "Back of the Pokémon",
            _ => "Pokémon icon",
        };

        // ── backgrounds ──────────────────────────────────────────────────────────────────────────

        private ushort[] BgPalette(ScreenLayout screen, string variant)
        {
            ushort[] palette = new ushort[256];
            foreach (ScreenPaletteLoad load in screen.Palettes)
            {
                if (!ScreenPage.Shows(load.When, variant)) continue;
                ushort[] colours = NitroBgCodec.ReadColours(File(load.File));
                for (int r = 0; r < load.Rows; r++)
                    for (int c = 0; c < 16; c++)
                    {
                        int from = (load.FromRow + r) * 16 + c, to = (load.ToRow + r) * 16 + c;
                        if (from < colours.Length && to < palette.Length) palette[to] = colours[from];
                    }
            }
            return palette;
        }

        private void DrawBg(byte[] rgba, ScreenBg bg, ushort[] palette, string variant)
        {
            int cols = Math.Max(32, bg.MapWidth), rows = Math.Max(32, bg.MapHeight);
            int[] map = new int[NitroBgCodec.SquareCount(cols, rows)];
            foreach (ScreenPaste piece in bg.Pieces)
                if (ScreenPage.Shows(piece.When, variant)) Paste(map, cols, rows, piece);

            byte[] chr = File(bg.Drawing);
            if (chr == null) return;
            (bool eightBit, int tilesAt) = NitroBgCodec.ReadTileHeader(chr);
            bool wide = eightBit || bg.Bpp == 8;
            int tileBytes = wide ? 64 : 32;

            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    int mx = Mod(x + bg.ScrollX, cols * 8), my = Mod(y + bg.ScrollY, rows * 8);
                    int e = map[NitroBgCodec.SquareIndex(cols, mx / 8, my / 8)];
                    if (e < 0) continue;
                    int tile = e & 0x3FF, row = (e >> 12) & 0xF;
                    int lx = mx % 8, ly = my % 8;
                    if ((e & 0x400) != 0) lx = 7 - lx;
                    if ((e & 0x800) != 0) ly = 7 - ly;
                    int index;
                    if (wide)
                    {
                        int at = tilesAt + tile * tileBytes + ly * 8 + lx;
                        if (at >= chr.Length) continue;
                        index = chr[at];
                    }
                    else
                    {
                        int at = tilesAt + tile * tileBytes + (ly * 8 + lx) / 2;
                        if (at >= chr.Length) continue;
                        index = (lx & 1) != 0 ? chr[at] >> 4 : chr[at] & 0xF;
                    }
                    if (index == 0) continue;
                    int colour = wide ? index : row * 16 + index;
                    Put(rgba, (y * Width + x) * 4, palette[colour & 0xFF]);
                }
        }

        private void Paste(int[] map, int cols, int rows, ScreenPaste piece)
        {
            if (piece.Screen < 0)
            {
                if (piece.Fill < 0) return;
                for (int y = 0; y < Math.Max(0, piece.Height); y++)
                    for (int x = 0; x < Math.Max(0, piece.Width); x++)
                        Set(map, cols, rows, piece.TileX + x, piece.TileY + y, piece.FillStride < 0 ? piece.Fill : piece.Fill + y * piece.FillStride + x);
                return;
            }
            byte[] scr = File(piece.Screen);
            if (scr == null) return;
            (int w, int h, int mapAt) = NitroBgCodec.ReadScreenHeader(scr);
            int sCols = w / 8, sRows = h / 8;
            int entryBytes = NitroBgCodec.EntryBytes(scr);
            int width = piece.Width < 0 ? sCols - piece.FromX : piece.Width;
            int height = piece.Height < 0 ? sRows - piece.FromY : piece.Height;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int sx = piece.FromX + x, sy = piece.FromY + y;
                    if (sx >= sCols || sy >= sRows) continue;
                    int e = NitroBgCodec.EntryAt(scr, mapAt, NitroBgCodec.SquareIndex(sCols, sx, sy), entryBytes);
                    if (e < 0) continue;
                    if (piece.Palette >= 0) e = (e & 0x0FFF) | (piece.Palette << 12);
                    Set(map, cols, rows, piece.TileX + x, piece.TileY + y, e);
                }
        }

        private static void Set(int[] map, int cols, int rows, int x, int y, int e)
        {
            if (x < 0 || y < 0 || x >= cols || y >= rows) return;
            map[NitroBgCodec.SquareIndex(cols, x, y)] = e;
        }

        // ── sprites, pictures and text ───────────────────────────────────────────────────────────

        private void DrawSprite(byte[] rgba, ScreenSprite s)
        {
            if (s.Picture != null)
            {
                ScreenPicture pic = Picture?.Invoke(s);
                if (pic?.Rgba != null) Paste(rgba, pic, s.X - pic.Width / 2, s.Y - pic.Height / 2);
                return;
            }
            int seq = s.Seq, drawing = s.Drawing, row = s.Row, shift = 0;
            if (s.Readout != DexReadout.None)
            {
                if (Digit?.Invoke(s.Readout, s.Step, s.Place) is not (int digitSeq, int dx)) return;
                (seq, shift) = (digitSeq, dx);
            }
            if (s.TypeSlot > 0)
            {
                int type = _type?.Invoke(s.TypeSlot) ?? -1;
                if (type < 0) return;
                if (s.TypeDrawing >= 0)
                {
                    drawing = s.TypeDrawing + type;
                    row = Math.Max(0, _bank?.Invoke(drawing) ?? 0);
                }
                else seq = type;
            }
            if (s.Cells < 0)
            {
                DrawWhole(rgba, s, File(s.DrawingArchive, drawing), NitroBgCodec.ReadColours(File(s.ColoursArchive, s.Colours)), row);
                return;
            }
            byte[] ncer = File(s.Archive, s.Cells);
            byte[] chr = File(s.DrawingArchive, drawing);
            if (ncer == null || chr == null) return;
            List<DsBgScreen.Oam[]> cells = DsBgScreen.ReadCells(ncer);
            int cell = seq;
            if (s.Anim >= 0)
            {
                byte[] nanr = File(s.Archive, s.Anim);
                try { if (nanr != null) cell = NanrFile.Read(nanr).CellOf(seq, s.Frame); } catch { }
            }
            if (cell < 0 || cell >= cells.Count) return;
            ushort[] colours = NitroBgCodec.ReadColours(File(s.ColoursArchive, s.Colours));
            byte[] characters = NitroBgCodec.ReadCharacters(chr);
            (int x, int y, double scale, double degrees) = Place(s.Size, s.X + shift, s.Y);
            int fixedRow = s.Palette;
            DsBgScreen.DrawCellTurned(rgba, cells[cell], characters, p => DsBgScreen.Row(colours, fixedRow >= 0 ? fixedRow : p + row), x, y, degrees, scale, scale);
        }

        // Each cell copies a 5 by 5 stamp out of a strip drawing, the piece's number times 5 along it.
        private void DrawHabitat(byte[] rgba, DexHabitat h, DexHabitatData data, ushort[] palette)
        {
            Stamp(rgba, h, data.Special, File(h.SpecialStamps), palette);
            Stamp(rgba, h, data.Normal, File(h.Stamps), palette);
        }

        private static void Stamp(byte[] rgba, DexHabitat h, byte[] map, byte[] chr, ushort[] palette)
        {
            if (chr == null) return;
            int tilesWide = NitroBgCodec.ReadDrawingHeader(chr)?.TilesAcross ?? 32;
            byte[] pixels = NitroBgCodec.ReadCharacters(chr);
            int stripWidth = Math.Max(8, tilesWide * 8);
            for (int x = 0; x < DexHabitatBuilder.MapSide; x++)
                for (int y = 0; y < DexHabitatBuilder.MapSide; y++)
                {
                    int piece = map[x * DexHabitatBuilder.MapSide + y];
                    if (piece == 0) continue;
                    int sx0 = piece * h.Cell % stripWidth, sy0 = piece * h.Cell / stripWidth;
                    for (int py = 0; py < h.Cell; py++)
                        for (int px = 0; px < h.Cell; px++)
                        {
                            int sx = sx0 + px, sy = sy0 + py;
                            int at = ((sy / 8) * tilesWide + sx / 8) * 32 + ((sy % 8) * 8 + sx % 8) / 2;
                            if (at >= pixels.Length) continue;
                            int index = (sx & 1) != 0 ? pixels[at] >> 4 : pixels[at] & 0xF;
                            if (index == 0) continue;
                            int X = h.X + y * h.Cell + px, Y = h.Y + x * h.Cell + py;
                            if (X < 0 || Y < 0 || X >= Width || Y >= Height) continue;
                            Put(rgba, (Y * Width + X) * 4, palette[(h.Row * 16 + index) & 0xFF]);
                        }
                }
        }

        // Where a size-page piece goes for the sample: the height check lifts and scales the Pokémon and the player
        // by the Pokédex data; on the weight check the pans and icons swing about the scale's middle by its tilt,
        // and the beam turns with it. Anything else stays put.
        private (int X, int Y, double Scale, double Degrees) Place(DexSizeRole role, int x, int y)
        {
            if (role == DexSizeRole.None) return (x, y, 1, 0);
            DexSizeInfo info = Size?.Invoke(_variant);
            if (info == null) return (x, y, 1, 0);
            double turn = info.Tilt * 2 * Math.PI / 65536.0;
            int arm = x - 128;
            switch (role)
            {
                case DexSizeRole.PokemonHeight:
                    return (x, y + info.PokemonOffset, 256.0 / Math.Max(1, info.PokemonScale), 0);
                case DexSizeRole.TrainerHeight:
                    return (x, y + info.TrainerOffset, 256.0 / Math.Max(1, info.TrainerScale), 0);
                case DexSizeRole.Beam:
                    return (x, y, 1, info.Tilt * 360.0 / 65536.0);
                default:
                    return (128 + (int)Math.Round(arm * Math.Cos(turn)), y + (int)Math.Round(arm * Math.Sin(turn)), 1, 0);
            }
        }

        private static void PasteScaled(byte[] rgba, ScreenPicture pic, int cx, int cy, double scale)
        {
            int w = (int)Math.Round(pic.Width * scale), h = (int)Math.Round(pic.Height * scale);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int sx = (int)(x / scale), sy = (int)(y / scale);
                    if (sx >= pic.Width || sy >= pic.Height) continue;
                    int from = (sy * pic.Width + sx) * 4;
                    if (pic.Rgba[from + 3] == 0) continue;
                    int X = cx - w / 2 + x, Y = cy - h / 2 + y;
                    if (X < 0 || Y < 0 || X >= Width || Y >= Height) continue;
                    int to = (Y * Width + X) * 4;
                    rgba[to] = pic.Rgba[from];
                    rgba[to + 1] = pic.Rgba[from + 1];
                    rgba[to + 2] = pic.Rgba[from + 2];
                    rgba[to + 3] = 255;
                }
        }

        /// <summary>A sixteen-colour drawing laid out in tiles, row after row, its top left at X, Y, turned about its middle on a weight check.</summary>
        private void DrawWhole(byte[] rgba, ScreenSprite s, byte[] chr, ushort[] colours, int row)
        {
            if (chr == null || s.Width <= 0 || s.Height <= 0) return;
            byte[] pixels = NitroBgCodec.ReadCharacters(chr);
            ushort[] palette = DsBgScreen.Row(colours, row);
            int tilesWide = s.Width / 8;
            // Software sprites (the Platinum beam) are textures whose pixels are stored row by row, not in tiles.
            bool linear = NitroBgCodec.ReadDrawingHeader(chr)?.Tiled == false;
            double degrees = Place(s.Size, s.X, s.Y).Degrees, turn = degrees * Math.PI / 180.0;
            double cos = Math.Cos(turn), sin = Math.Sin(turn);
            double midX = s.X + s.Width / 2.0, midY = s.Y + s.Height / 2.0;
            int reach = (int)Math.Ceiling(Math.Sqrt(s.Width * s.Width + s.Height * s.Height) / 2) + 1;
            for (int Y = (int)midY - reach; Y <= midY + reach; Y++)
                for (int X = (int)midX - reach; X <= midX + reach; X++)
                {
                    if (X < 0 || Y < 0 || X >= Width || Y >= Height) continue;
                    // Back through the turn into the drawing's own pixels.
                    double ux = X + 0.5 - midX, uy = Y + 0.5 - midY;
                    int x = (int)Math.Floor(ux * cos + uy * sin + s.Width / 2.0);
                    int y = (int)Math.Floor(-ux * sin + uy * cos + s.Height / 2.0);
                    if (x < 0 || y < 0 || x >= s.Width || y >= s.Height) continue;
                    int at = linear ? (y * s.Width + x) / 2 : ((y / 8) * tilesWide + x / 8) * 32 + ((y % 8) * 8 + x % 8) / 2;
                    if (at >= pixels.Length) continue;
                    int index = (x & 1) != 0 ? pixels[at] >> 4 : pixels[at] & 0xF;
                    if (index == 0 || palette == null) continue;
                    Put(rgba, (Y * Width + X) * 4, palette[index]);
                }
        }

        private void DrawText(byte[] rgba, ScreenText t, ushort[] palette)
        {
            string text = _text?.Invoke(t, _variant);
            FieldFont font = _font?.Invoke(t.Font);
            if (string.IsNullOrEmpty(text) || font == null) return;
            int inkAt = t.Row * 16 + t.Ink, shadowAt = t.Row * 16 + t.Shadow;
            ushort ink = inkAt < palette.Length ? palette[inkAt] : (ushort)0;
            ushort shadow = shadowAt < palette.Length ? palette[shadowAt] : (ushort)0;
            string[] lines = text.Replace("\r", "").Split('\n');
            int top = t.BoxHeight > 0 ? t.Y + (t.BoxHeight - lines.Length * 16) / 2 : t.Y;
            for (int i = 0; i < lines.Length; i++)
            {
                int width = font.Measure(lines[i], FieldFontCharacters.GlyphFor);
                int slash = t.Align == ScreenTextAlign.Slash ? lines[i].IndexOf('/') : -1;
                if (slash >= 0)
                {
                    // The slash sits centred on X; the first number ends where it starts, the second starts where it ends.
                    string first = lines[i].Substring(0, slash), second = lines[i].Substring(slash + 1);
                    int slashWidth = font.Measure("/", FieldFontCharacters.GlyphFor);
                    int at = t.X - slashWidth / 2;
                    DsBgScreen.DrawText(rgba, font, first, at - font.Measure(first, FieldFontCharacters.GlyphFor), top + i * 16, ink, shadow);
                    DsBgScreen.DrawText(rgba, font, "/", at, top + i * 16, ink, shadow);
                    DsBgScreen.DrawText(rgba, font, second, at + slashWidth, top + i * 16, ink, shadow);
                    continue;
                }
                int x = t.Align switch
                {
                    ScreenTextAlign.Centre => t.X - width / 2,
                    ScreenTextAlign.Right => t.X - width,
                    ScreenTextAlign.Block => t.X - BlockWidth(font, lines) / 2,
                    ScreenTextAlign.BoxCentre => t.WinX * 8 + Math.Max(0, t.WinW * 8 - width) / 2,
                    ScreenTextAlign.BoxRight => t.WinX * 8 + t.WinW * 8 - width,
                    _ => t.X,
                };
                DsBgScreen.DrawText(rgba, font, lines[i], x, top + i * 16, ink, shadow);
            }
        }

        private static void Clear(byte[] rgba, int left, int top, int w, int h)
        {
            for (int y = Math.Max(0, top); y < Math.Min(Height, top + h); y++)
                for (int x = Math.Max(0, left); x < Math.Min(Width, left + w); x++)
                    rgba[(y * Width + x) * 4 + 3] = 0;
        }

        private static int BlockWidth(FieldFont font, string[] lines)
        {
            int widest = 0;
            foreach (string line in lines) widest = Math.Max(widest, font.Measure(line, FieldFontCharacters.GlyphFor));
            return widest;
        }

        private static void Darken(byte[] rgba, int sixteenths)
        {
            int keep = 16 - Math.Clamp(sixteenths, 0, 16);
            for (int at = 0; at < rgba.Length; at += 4)
            {
                rgba[at] = (byte)(rgba[at] * keep / 16);
                rgba[at + 1] = (byte)(rgba[at + 1] * keep / 16);
                rgba[at + 2] = (byte)(rgba[at + 2] * keep / 16);
            }
        }

        private static void Paste(byte[] rgba, ScreenPicture pic, int left, int top)
        {
            for (int y = 0; y < pic.Height; y++)
                for (int x = 0; x < pic.Width; x++)
                {
                    int X = left + x, Y = top + y;
                    if (X < 0 || Y < 0 || X >= Width || Y >= Height) continue;
                    int from = (y * pic.Width + x) * 4;
                    if (from + 3 >= pic.Rgba.Length || pic.Rgba[from + 3] == 0) continue;
                    int to = (Y * Width + X) * 4;
                    rgba[to] = pic.Rgba[from];
                    rgba[to + 1] = pic.Rgba[from + 1];
                    rgba[to + 2] = pic.Rgba[from + 2];
                    rgba[to + 3] = 255;
                }
        }

        // ── pixels ───────────────────────────────────────────────────────────────────────────────

        private static int Mod(int a, int m) => ((a % m) + m) % m;

        private static void Put(byte[] rgba, int at, ushort c)
        {
            (rgba[at], rgba[at + 1], rgba[at + 2]) = NitroBgCodec.Colour(c);
            rgba[at + 3] = 255;
        }

        private static void Fill(byte[] rgba, ushort c)
        {
            for (int at = 0; at < rgba.Length; at += 4) Put(rgba, at, c);
        }

        private static void FillRect(byte[] rgba, ScreenRect r, ushort[] palette)
        {
            int colour = r.Row * 16 + r.Colour;
            if (colour <= 0 || colour >= palette.Length) return;
            for (int y = Math.Max(0, r.Y); y < Math.Min(Height, r.Y + r.Height); y++)
                for (int x = Math.Max(0, r.X); x < Math.Min(Width, r.X + r.Width); x++)
                    Put(rgba, (y * Width + x) * 4, palette[colour]);
        }

        // The DS's alpha blend: each channel is the two weighted in sixteenths and added, up to full.
        private static void Blend(byte[] under, byte[] over, int weight, int underWeight)
        {
            for (int at = 0; at < under.Length; at += 4)
            {
                if (over[at + 3] == 0) continue;
                for (int c = 0; c < 3; c++)
                    under[at + c] = (byte)Math.Min(255, (over[at + c] * weight + under[at + c] * underWeight) / 16);
                under[at + 3] = 255;
            }
        }

        private static void Over(byte[] under, byte[] over)
        {
            for (int at = 0; at < under.Length; at += 4)
            {
                if (over[at + 3] == 0) continue;
                under[at] = over[at];
                under[at + 1] = over[at + 1];
                under[at + 2] = over[at + 2];
                under[at + 3] = 255;
            }
        }
    }
}
