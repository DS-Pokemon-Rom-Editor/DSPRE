using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Views.Controls
{
    /// <summary>
    /// The box an NPC talks to you from, drawn where the games draw it and, when the ROM's own font can be
    /// read, written with the letters the game itself would use.
    /// </summary>
    public sealed class FieldMessageBoxView : Control
    {
        public static readonly StyledProperty<string> TextProperty =
            AvaloniaProperty.Register<FieldMessageBoxView, string>(nameof(Text));

        public static readonly StyledProperty<bool> HasMoreProperty =
            AvaloniaProperty.Register<FieldMessageBoxView, bool>(nameof(HasMore));

        public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
        public bool HasMore { get => GetValue(HasMoreProperty); set => SetValue(HasMoreProperty, value); }

        static FieldMessageBoxView()
        {
            AffectsRender<FieldMessageBoxView>(TextProperty, HasMoreProperty, FitToFrameProperty);
        }

        public FieldMessageBoxView()
        {
            IsHitTestVisible = false;
            RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        }

        // ── the letters ──────────────────────────────────────────────────────────────────

        /// <summary>The game's own font, read out of the loaded ROM. </summary>
        public static FieldFont Font { get; set; }

        private static bool RomFontReady => Font != null && FieldFontCharacters.Ready;

        private static FieldWindowFrame _frame;
        private static WriteableBitmap _frameBitmap;

        /// <summary>The border the games draw round the box, read out of the ROM. </summary>
        public static FieldWindowFrame Frame
        {
            get => _frame;
            set { _frame = value; _frameBitmap?.Dispose(); _frameBitmap = null; }
        }

        /// <summary>The type used when the ROM's own font is not to hand.</summary>
        public static readonly FontFamily Face =
            new FontFamily("Verdana, Segoe UI, DejaVu Sans, sans-serif");

        public const double FontPixels = 12;
        private static readonly Typeface Fallback = new Typeface(Face);

        /// <summary>How wide a run of letters is, in DS pixels. </summary>
        public static int Measure(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            if (RomFontReady) return Font.Measure(text, FieldFontCharacters.GlyphFor);

            var f = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                                      Fallback, FontPixels, Brushes.Black);
            return (int)Math.Ceiling(f.Width);
        }

        // ── drawing ──────────────────────────────────────────────────────────────────────

        private WriteableBitmap _page;      // the current page's letters, at DS size
        private string _pageText;
        private FieldFont _pageFont;

        public static readonly StyledProperty<bool> FitToFrameProperty =
            AvaloniaProperty.Register<FieldMessageBoxView, bool>(nameof(FitToFrame));

        /// <summary>Fits just the box to the control rather than the whole DS screen. </summary>
        public bool FitToFrame { get => GetValue(FitToFrameProperty); set => SetValue(FitToFrameProperty, value); }

        /// <summary>
        /// Where the DS screen lands inside the control: the top left corner in control coordinates and how
        /// much everything is blown up by.
        /// </summary>
        private (double x, double y, double scale) Screen()
        {
            double w = Bounds.Width, h = Bounds.Height;
            if (w <= 0 || h <= 0) return (0, 0, 1);

            double fitW = FitToFrame ? FieldMessageWindow.FrameWidth : FieldMessageWindow.ScreenWidth;
            double fitH = FitToFrame ? FieldMessageWindow.FrameHeight : FieldMessageWindow.ScreenHeight;
            double scale = Math.Min(w / fitW, h / fitH);

            double left = (w - fitW * scale) / 2;
            double top = (h - fitH * scale) / 2;
            if (!FitToFrame) return (left, top, scale);

            // Shift the origin so the frame, not the screen, is what lands in the middle.
            return (left - FieldMessageWindow.FrameLeft * scale,
                    top - FieldMessageWindow.FrameTop * scale,
                    scale);
        }

        public override void Render(DrawingContext ctx)
        {
            string text = Text;
            if (string.IsNullOrEmpty(text)) return;

            var (ox, oy, s) = Screen();
            var frame = new Rect(ox + FieldMessageWindow.FrameLeft * s, oy + FieldMessageWindow.FrameTop * s,
                                 FieldMessageWindow.FrameWidth * s, FieldMessageWindow.FrameHeight * s);

            var textArea = new Rect(ox + FieldMessageWindow.TextLeft * s, oy + FieldMessageWindow.TextTop * s,
                                    FieldMessageWindow.TextWidth * s, FieldMessageWindow.TextHeight * s);

            if (!DrawRomFrame(ctx, frame, textArea)) DrawPlainFrame(ctx, frame, s);

            if (RomFontReady) DrawWithRomFont(ctx, text, textArea);
            else DrawWithOrdinaryType(ctx, text, ox, oy, s);

            if (HasMore) DrawMoreArrow(ctx, frame, s);
        }

        // The games' own border, which also paints the paper the writing sits on.
        private static bool DrawRomFrame(DrawingContext ctx, Rect frame, Rect textArea)
        {
            var f = _frame;
            if (f == null) return false;

            // The border never covers the middle, so the paper the writing sits on goes down first.
            uint paper = f.PaperArgb;
            ctx.FillRectangle(new SolidColorBrush(Color.FromArgb(
                (byte)(paper >> 24), (byte)(paper >> 16), (byte)(paper >> 8), (byte)paper)),
                textArea.Inflate(1));

            if (_frameBitmap == null)
            {
                byte[] rgba = f.Compose(FieldMessageWindow.TilesWide, FieldMessageWindow.TilesHigh,
                                        out int w, out int h);
                _frameBitmap = FromRgba(rgba, w, h);
            }
            if (_frameBitmap == null) return false;

            ctx.DrawImage(_frameBitmap,
                new Rect(0, 0, _frameBitmap.PixelSize.Width, _frameBitmap.PixelSize.Height), frame);
            return true;
        }

        // A plain stand-in, for when the ROM's own border cannot be read.
        private static void DrawPlainFrame(DrawingContext ctx, Rect frame, double s)
        {
            var paper = new SolidColorBrush(Color.FromRgb(0xF8, 0xF8, 0xF8));
            var edge = new SolidColorBrush(Color.FromRgb(0x28, 0x30, 0x48));
            var inner = new SolidColorBrush(Color.FromRgb(0x88, 0x98, 0xC0));

            double round = 3 * s;
            ctx.DrawRectangle(paper, new Pen(edge, Math.Max(1, 2 * s)), frame, round, round);
            ctx.DrawRectangle(null, new Pen(inner, Math.Max(1, 1 * s)), frame.Deflate(3 * s), round, round);
        }

        private static WriteableBitmap FromRgba(byte[] rgba, int w, int h)
        {
            if (rgba == null || w <= 0 || h <= 0) return null;
            var bmp = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96),
                                          PixelFormat.Bgra8888, AlphaFormat.Unpremul);
            using var buf = bmp.Lock();
            unsafe
            {
                for (int y = 0; y < h; y++)
                {
                    var row = (byte*)buf.Address + y * buf.RowBytes;
                    for (int x = 0; x < w; x++)
                    {
                        int at = (y * w + x) * 4;
                        row[x * 4 + 0] = rgba[at + 2];   // b
                        row[x * 4 + 1] = rgba[at + 1];   // g
                        row[x * 4 + 2] = rgba[at + 0];   // r
                        row[x * 4 + 3] = rgba[at + 3];   // a
                    }
                }
            }
            return bmp;
        }

        private void DrawWithRomFont(DrawingContext ctx, string text, Rect textArea)
        {
            if (_page == null || _pageText != text || !ReferenceEquals(_pageFont, Font))
            {
                _page?.Dispose();
                _page = RenderPage(text);
                _pageText = text;
                _pageFont = Font;
            }
            if (_page == null) return;

            ctx.DrawImage(_page,
                new Rect(0, 0, FieldMessageWindow.TextWidth, FieldMessageWindow.TextHeight),
                textArea);
        }

        // Paints one page of letters at the size the DS would, so it can be blown up without blurring.
        private static WriteableBitmap RenderPage(string text)
        {
            int w = FieldMessageWindow.TextWidth, h = FieldMessageWindow.TextHeight;
            var bmp = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96),
                                          PixelFormat.Bgra8888, AlphaFormat.Premul);

            using var buf = bmp.Lock();
            unsafe
            {
                var row = (byte*)buf.Address;
                for (int y = 0; y < h; y++)
                {
                    var p = (uint*)(row + y * buf.RowBytes);
                    for (int x = 0; x < w; x++) p[x] = 0;
                }

                string[] lines = text.Replace("\r\n", "\n").Split('\n');
                for (int line = 0; line < lines.Length && line < FieldMessageWindow.LinesPerPage; line++)
                {
                    int penX = 0;
                    int top = line * FieldMessageWindow.LineHeight;
                    foreach (char c in lines[line])
                    {
                        int g = FieldFontCharacters.GlyphFor(c);
                        int advance = Font.WidthOf(g);
                        if (g < 0) { penX += advance > 0 ? advance : 6; continue; }

                        for (int y = 0; y < FieldFont.CellSize; y++)
                        {
                            int py = top + y;
                            if (py < 0 || py >= h) continue;
                            var p = (uint*)((byte*)buf.Address + py * buf.RowBytes);
                            for (int x = 0; x < advance; x++)
                            {
                                int px = penX + x;
                                if (px < 0 || px >= w) continue;
                                byte v = Font.PixelAt(g, x, y);
                                // 0 is outside the letter and 3 is the paper it sits on; the box has
                                // already painted that, so only the ink is drawn here.
                                if (v == FieldFont.Nothing || v == FieldFont.Paper) continue;
                                p[px] = v == 1 ? 0xFF282828u : 0xFF9098A8u;
                            }
                        }
                        penX += advance;
                        if (penX >= w) break;
                    }
                }
            }
            return bmp;
        }

        private static void DrawWithOrdinaryType(DrawingContext ctx, string text, double ox, double oy, double s)
        {
            var ink = new SolidColorBrush(Color.FromRgb(0x28, 0x28, 0x28));
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length && i < FieldMessageWindow.LinesPerPage; i++)
            {
                if (lines[i].Length == 0) continue;
                var f = new FormattedText(lines[i], CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                                          Fallback, FontPixels * s, ink);
                double y = FieldMessageWindow.TextTop + i * FieldMessageWindow.LineHeight + 2;
                ctx.DrawText(f, new Point(ox + FieldMessageWindow.TextLeft * s, oy + y * s));
            }
        }

        // The little triangle that says there is another page waiting.
        private static void DrawMoreArrow(DrawingContext ctx, Rect frame, double s)
        {
            double size = 5 * s;
            double x = frame.Right - 8 * s;
            double y = frame.Bottom - 7 * s;

            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                c.BeginFigure(new Point(x - size, y - size / 2), true);
                c.LineTo(new Point(x, y - size / 2));
                c.LineTo(new Point(x - size / 2, y + size / 2));
                c.EndFigure(true);
            }
            ctx.DrawGeometry(new SolidColorBrush(Color.FromRgb(0x28, 0x30, 0x48)), null, g);
        }
    }
}
