using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Views.Controls
{
    /// <summary>
    /// A line of writing in the font the open ROM carries, so a preview shows the real letterforms
    /// rather than the computer's own. Falls back to a plain face when the ROM's font cannot be read.
    /// </summary>
    public sealed class RomTextBlock : Control
    {
        public static readonly StyledProperty<string> TextProperty =
            AvaloniaProperty.Register<RomTextBlock, string>(nameof(Text), "");

        public static readonly StyledProperty<IBrush> InkProperty =
            AvaloniaProperty.Register<RomTextBlock, IBrush>(nameof(Ink), Brushes.Black);

        /// <summary>Height of one line in ROM pixels; the games write at twelve.</summary>
        public static readonly StyledProperty<double> LineHeightProperty =
            AvaloniaProperty.Register<RomTextBlock, double>(nameof(LineHeight), 12);

        public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
        public IBrush Ink { get => GetValue(InkProperty); set => SetValue(InkProperty, value); }
        public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }

        static RomTextBlock()
        {
            AffectsRender<RomTextBlock>(TextProperty, InkProperty, LineHeightProperty);
            AffectsMeasure<RomTextBlock>(TextProperty, LineHeightProperty);
        }

        private static FieldFont Font => FieldMessageBoxView.Font;
        private static bool RomFontReady => Font != null && FieldFontCharacters.Ready;

        protected override Size MeasureOverride(Size available)
        {
            string text = Text ?? "";
            if (text.Length == 0) return new Size(0, LineHeight);
            if (RomFontReady) return new Size(Font.Measure(text, FieldFontCharacters.GlyphFor), LineHeight);
            return new Size(text.Length * LineHeight * 0.5, LineHeight);
        }

        public override void Render(DrawingContext ctx)
        {
            string text = Text ?? "";
            if (text.Length == 0) return;

            if (RomFontReady)
            {
                DrawWithRomFont(ctx, text);
                return;
            }
            var face = new Typeface(new FontFamily("Verdana, Segoe UI, DejaVu Sans, sans-serif"));
            ctx.DrawText(new FormattedText(text, System.Globalization.CultureInfo.CurrentCulture,
                                           FlowDirection.LeftToRight, face, LineHeight, Ink), new Point(0, 0));
        }

        // One glyph at a time, the way the games lay a line out: each letter is drawn at its own width,
        // not on a fixed grid.
        private void DrawWithRomFont(DrawingContext ctx, string text)
        {
            var font = Font;
            double x = 0;
            var ink = Ink ?? Brushes.Black;
            foreach (char c in text)
            {
                int glyph = FieldFontCharacters.GlyphFor(c);
                if (glyph < 0) { x += font.MaxWidth / 2.0; continue; }
                int width = font.WidthOf(glyph);
                for (int py = 0; py < font.Height; py++)
                    for (int px = 0; px < width; px++)
                    {
                        byte v = font.PixelAt(glyph, px, py);
                        if (v == FieldFont.Nothing || v == FieldFont.Paper) continue;
                        ctx.FillRectangle(ink, new Rect(x + px, py, 1, 1));
                    }
                x += width;
            }
        }
    }
}
