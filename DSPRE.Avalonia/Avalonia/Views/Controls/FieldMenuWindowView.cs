using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Views.Controls
{
    /// <summary>
    /// A yes/no box or a script's menu, drawn on the DS top screen where the games put it, with the ROM's
    /// own system border, font and font colours.
    /// </summary>
    public sealed class FieldMenuWindowView : Control
    {
        public static readonly StyledProperty<IReadOnlyList<string>> ItemsProperty =
            AvaloniaProperty.Register<FieldMenuWindowView, IReadOnlyList<string>>(nameof(Items));
        public static readonly StyledProperty<IReadOnlyList<string>> WidthItemsProperty =
            AvaloniaProperty.Register<FieldMenuWindowView, IReadOnlyList<string>>(nameof(WidthItems));
        public static readonly StyledProperty<int> CursorProperty =
            AvaloniaProperty.Register<FieldMenuWindowView, int>(nameof(Cursor));
        public static readonly StyledProperty<int> TileLeftProperty =
            AvaloniaProperty.Register<FieldMenuWindowView, int>(nameof(TileLeft));
        public static readonly StyledProperty<int> TileTopProperty =
            AvaloniaProperty.Register<FieldMenuWindowView, int>(nameof(TileTop));
        public static readonly StyledProperty<int> TilesWideProperty =
            AvaloniaProperty.Register<FieldMenuWindowView, int>(nameof(TilesWide));
        public static readonly StyledProperty<int> TilesHighProperty =
            AvaloniaProperty.Register<FieldMenuWindowView, int>(nameof(TilesHigh));
        public static readonly StyledProperty<int> IndentProperty =
            AvaloniaProperty.Register<FieldMenuWindowView, int>(nameof(Indent), 11);
        public static readonly StyledProperty<int> CursorXProperty =
            AvaloniaProperty.Register<FieldMenuWindowView, int>(nameof(CursorX));
        public static readonly StyledProperty<int> RowOffsetProperty =
            AvaloniaProperty.Register<FieldMenuWindowView, int>(nameof(RowOffset));
        public static readonly StyledProperty<bool> ShowCursorProperty =
            AvaloniaProperty.Register<FieldMenuWindowView, bool>(nameof(ShowCursor), true);
        public static readonly StyledProperty<bool> FitToWindowProperty =
            AvaloniaProperty.Register<FieldMenuWindowView, bool>(nameof(FitToWindow));

        /// <summary>The rows on show.</summary>
        public IReadOnlyList<string> Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }
        /// <summary>Every entry, rows off screen included, which is what a list's width is measured from.</summary>
        public IReadOnlyList<string> WidthItems { get => GetValue(WidthItemsProperty); set => SetValue(WidthItemsProperty, value); }
        /// <summary>The row the cursor is on, counted from the first row on show.</summary>
        public int Cursor { get => GetValue(CursorProperty); set => SetValue(CursorProperty, value); }
        /// <summary>The writing area's top-left corner, in tiles.</summary>
        public int TileLeft { get => GetValue(TileLeftProperty); set => SetValue(TileLeftProperty, value); }
        public int TileTop { get => GetValue(TileTopProperty); set => SetValue(TileTopProperty, value); }
        /// <summary>A fixed size in tiles, or 0 to fit the entries.</summary>
        public int TilesWide { get => GetValue(TilesWideProperty); set => SetValue(TilesWideProperty, value); }
        public int TilesHigh { get => GetValue(TilesHighProperty); set => SetValue(TilesHighProperty, value); }
        /// <summary>How far in from the writing area's left the words start.</summary>
        public int Indent { get => GetValue(IndentProperty); set => SetValue(IndentProperty, value); }
        /// <summary>How far in the cursor sits.</summary>
        public int CursorX { get => GetValue(CursorXProperty); set => SetValue(CursorXProperty, value); }
        /// <summary>How far down the first row starts; lists start one pixel lower.</summary>
        public int RowOffset { get => GetValue(RowOffsetProperty); set => SetValue(RowOffsetProperty, value); }
        /// <summary>False for windows that only show a message.</summary>
        public bool ShowCursor { get => GetValue(ShowCursorProperty); set => SetValue(ShowCursorProperty, value); }
        /// <summary>Fits only the window to the control instead of the whole DS screen.</summary>
        public bool FitToWindow { get => GetValue(FitToWindowProperty); set => SetValue(FitToWindowProperty, value); }

        /// <summary>Two rows of tiles per entry.</summary>
        public const int EntryPixels = 16;

        /// <summary>What a menu adds to its widest entry before rounding up to whole tiles.</summary>
        public const int WidthPadding = 12;

        /// <summary>The system font's cursor, character 0x011F.</summary>
        public const char CursorCharacter = '‣';

        // The system font palette's letter, shadow and paper, for when the ROM's cannot be read.
        public const uint LetterFallback = 0xFF5A5A52, ShadowFallback = 0xFFACBDBD, PaperFallback = 0xFFFFFFFF;

        static FieldMenuWindowView()
        {
            AffectsRender<FieldMenuWindowView>(ItemsProperty, WidthItemsProperty, CursorProperty, TileLeftProperty, TileTopProperty,
                                               TilesWideProperty, TilesHighProperty, IndentProperty, CursorXProperty, RowOffsetProperty,
                                               ShowCursorProperty, FitToWindowProperty);
        }

        public FieldMenuWindowView()
        {
            IsHitTestVisible = false;
            RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        }

        /// <summary>The system font, read out of the ROM that is open.</summary>
        public static FieldFont Font { get; set; }

        /// <summary>The standard window border, read out of the ROM that is open.</summary>
        public static FieldWindowFrame Frame { get; set; }

        /// <summary>The system font's colours, which fill the window and colour its writing.</summary>
        public static uint[] Colours { get; set; }

        private static uint Colour(int index, uint fallback) =>
            Colours != null && index < Colours.Length && Colours[index] != 0 ? Colours[index] : fallback;

        private WriteableBitmap _picture;
        private string _pictureKey;

        public static int Measure(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            if (Font != null && FieldFontCharacters.Ready) return Font.Measure(text, FieldFontCharacters.GlyphFor);
            return text.Length * 6;
        }

        /// <summary>How many tiles wide a menu of these entries is, the way the games size one.</summary>
        public static int WidthInTiles(IEnumerable<string> entries)
        {
            int widest = entries?.Select(Measure).DefaultIfEmpty(0).Max() ?? 0;
            return (widest + WidthPadding + FieldMessageWindow.TileSize - 1) / FieldMessageWindow.TileSize;
        }

        public override void Render(DrawingContext ctx)
        {
            IReadOnlyList<string> items = Items;
            if (items == null || items.Count == 0) return;

            double w = Bounds.Width, h = Bounds.Height;
            if (w <= 0 || h <= 0) return;
            double scale = Math.Min(w / FieldMessageWindow.ScreenWidth, h / FieldMessageWindow.ScreenHeight);
            double ox = (w - FieldMessageWindow.ScreenWidth * scale) / 2;
            double oy = (h - FieldMessageWindow.ScreenHeight * scale) / 2;

            int tilesWide = TilesWide > 0 ? TilesWide : WidthInTiles(WidthItems ?? items);
            int tilesHigh = TilesHigh > 0 ? TilesHigh : items.Count * 2;

            string key = $"{string.Join("\u0001", items)}|{Cursor}|{ShowCursor}|{tilesWide}x{tilesHigh}|{Indent}|{CursorX}|{RowOffset}"
                       + $"|{Font?.GetHashCode()}|{Frame?.GetHashCode()}|{Colours?.GetHashCode()}";
            if (_picture == null || _pictureKey != key)
            {
                _picture?.Dispose();
                _picture = Paint(items, tilesWide, tilesHigh);
                _pictureKey = key;
            }
            if (_picture == null) return;

            if (FitToWindow)
            {
                double fit = Math.Min(w / _picture.PixelSize.Width, h / _picture.PixelSize.Height);
                double fw = _picture.PixelSize.Width * fit, fh = _picture.PixelSize.Height * fit;
                ctx.DrawImage(_picture, new Rect(0, 0, _picture.PixelSize.Width, _picture.PixelSize.Height),
                              new Rect((w - fw) / 2, (h - fh) / 2, fw, fh));
                return;
            }

            // The picture includes the one-tile border round the writing area.
            double left = ox + (TileLeft - 1) * FieldMessageWindow.TileSize * scale;
            double top = oy + (TileTop - 1) * FieldMessageWindow.TileSize * scale;
            ctx.DrawImage(_picture,
                new Rect(0, 0, _picture.PixelSize.Width, _picture.PixelSize.Height),
                new Rect(left, top, _picture.PixelSize.Width * scale, _picture.PixelSize.Height * scale));
        }

        private WriteableBitmap Paint(IReadOnlyList<string> items, int tilesWide, int tilesHigh)
        {
            byte[] rgba = PaintRgba(items, tilesWide, tilesHigh, Indent, RowOffset, CursorX, ShowCursor ? Cursor : -1, out int width, out int height);
            return ToBitmap(rgba, width, height);
        }

        /// <summary>The window and its border as straight RGBA. <paramref name="cursor"/> is the cursor's row, or -1.</summary>
        public static byte[] PaintRgba(IReadOnlyList<string> items, int tilesWide, int tilesHigh, int indent, int rowOffset,
                                       int cursorX, int cursor, out int width, out int height)
        {
            int tile = FieldMessageWindow.TileSize;
            width = (tilesWide + 2) * tile;
            height = (tilesHigh + 2) * tile;
            uint paper = Colour(15, PaperFallback), letter = Colour(1, LetterFallback), shadow = Colour(2, ShadowFallback);
            byte[] rgba = Frame?.ComposeStandard(tilesWide, tilesHigh, out width, out height, paper)
                          ?? PlainWindow(width, height, paper);
            int w = width, h = height;

            void Dot(int x, int y, uint argb) => DotAt(rgba, w, h, x, y, argb);
            bool Write(string text, int penX, int penY) => WriteAt(rgba, w, h, text, penX, penY, letter, shadow);

            for (int i = 0; i < items.Count; i++)
                Write(items[i], tile + indent, tile + rowOffset + i * EntryPixels);

            if (cursor < 0) return rgba;

            int cy = tile + rowOffset + Math.Min(cursor, items.Count - 1) * EntryPixels;
            bool glyph = Font != null && FieldFontCharacters.Ready && FieldFontCharacters.GlyphFor(CursorCharacter) >= 0
                         && Write(CursorCharacter.ToString(), tile + cursorX, cy);
            if (!glyph)
                for (int y = 0; y < 7; y++)
                    for (int x = 0; x <= Math.Min(y, 6 - y); x++) Dot(tile + cursorX + 1 + x, cy + 4 + y, letter);

            return rgba;
        }

        /// <summary>
        /// A window with text pieces placed in pixels from its text area's corner, as straight RGBA. A right-aligned piece ends at its x;
        /// -1 means no cursor.
        /// </summary>
        public static byte[] PaintPlaced(int tilesWide, int tilesHigh, IReadOnlyList<(string Text, int X, int Y, bool Right)> pieces,
                                         int cursor, out int width, out int height)
        {
            byte[] rgba = PaintRgba(Array.Empty<string>(), tilesWide, tilesHigh, 0, 0, 0, -1, out width, out height);
            int tile = FieldMessageWindow.TileSize, w = width, h = height;
            uint letter = Colour(1, LetterFallback), shadow = Colour(2, ShadowFallback);
            for (int i = 0; i < pieces.Count; i++)
            {
                (string text, int x, int y, bool right) = pieces[i];
                int left = right ? x - Measure(text) : x;
                WriteAt(rgba, w, h, text ?? "", tile + left, tile + y, letter, shadow);
                if (i != cursor) continue;
                bool glyph = Font != null && FieldFontCharacters.Ready && FieldFontCharacters.GlyphFor(CursorCharacter) >= 0
                             && WriteAt(rgba, w, h, CursorCharacter.ToString(), tile, tile + y, letter, shadow);
                if (!glyph)
                    for (int cy = 0; cy < 7; cy++)
                        for (int cx = 0; cx <= Math.Min(cy, 6 - cy); cx++) DotAt(rgba, w, h, tile + 1 + cx, tile + y + 4 + cy, letter);
            }
            return rgba;
        }

        private static void DotAt(byte[] rgba, int w, int h, int x, int y, uint argb)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            int at = (y * w + x) * 4;
            rgba[at] = (byte)(argb >> 16); rgba[at + 1] = (byte)(argb >> 8); rgba[at + 2] = (byte)argb; rgba[at + 3] = 0xFF;
        }

        private static bool WriteAt(byte[] rgba, int w, int h, string text, int penX, int penY, uint letter, uint shadow)
        {
            if (Font == null || !FieldFontCharacters.Ready)
            {
                // Without the ROM's font there is nothing faithful to draw, so a block marks each letter.
                for (int i = 0; i < text.Length; i++)
                    if (text[i] != ' ')
                        for (int y = 4; y < 12; y++) for (int x = 0; x < 4; x++) DotAt(rgba, w, h, penX + i * 6 + x, penY + y, letter);
                return false;
            }
            bool drew = false;
            foreach (char c in text)
            {
                int g = FieldFontCharacters.GlyphFor(c);
                int advance = Font.WidthOf(g);
                if (g >= 0)
                {
                    drew = true;
                    for (int y = 0; y < FieldFont.CellSize; y++)
                        for (int x = 0; x < advance; x++)
                        {
                            byte v = Font.PixelAt(g, x, y);
                            if (v == FieldFont.Nothing || v == FieldFont.Paper) continue;
                            DotAt(rgba, w, h, penX + x, penY + y, v == 1 ? letter : shadow);
                        }
                }
                penX += advance > 0 ? advance : 6;
            }
            return drew;
        }

        private static WriteableBitmap ToBitmap(byte[] rgba, int width, int height)
        {
            WriteableBitmap bmp = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
            using (ILockedFramebuffer buf = bmp.Lock())
            {
                unsafe
                {
                    for (int y = 0; y < height; y++)
                    {
                        byte* row = (byte*)buf.Address + y * buf.RowBytes;
                        for (int x = 0; x < width; x++)
                        {
                            int at = (y * width + x) * 4;
                            row[x * 4] = rgba[at + 2];
                            row[x * 4 + 1] = rgba[at + 1];
                            row[x * 4 + 2] = rgba[at];
                            row[x * 4 + 3] = rgba[at + 3];
                        }
                    }
                }
            }
            return bmp;
        }

        // A stand-in window for when the ROM's border cannot be read.
        private static byte[] PlainWindow(int width, int height, uint paper)
        {
            byte[] rgba = new byte[width * height * 4];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    bool edge = x < 2 || y < 2 || x >= width - 2 || y >= height - 2;
                    uint c = edge ? 0xFF4A627Bu : paper;
                    int at = (y * width + x) * 4;
                    rgba[at] = (byte)(c >> 16); rgba[at + 1] = (byte)(c >> 8); rgba[at + 2] = (byte)c; rgba[at + 3] = 0xFF;
                }
            return rgba;
        }
    }
}
