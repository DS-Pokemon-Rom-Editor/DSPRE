using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using DSPRE.Avalonia.ViewModels;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Views.Controls
{
    /// <summary>
    /// A sentence in the font being edited, at the size the game writes it and again at three times
    /// that. Reads the font the editor is holding, so an unsaved change shows straight away.
    /// </summary>
    public sealed class FontSampleLine : Control
    {
        public static readonly StyledProperty<FontEditorViewModel> ModelProperty =
            AvaloniaProperty.Register<FontSampleLine, FontEditorViewModel>(nameof(Model));

        public static readonly StyledProperty<string> TextProperty =
            AvaloniaProperty.Register<FontSampleLine, string>(nameof(Text), "");

        /// <summary>Bumped by the model when a letter changes, so this redraws.</summary>
        public static readonly StyledProperty<int> RevisionProperty =
            AvaloniaProperty.Register<FontSampleLine, int>(nameof(Revision));

        public FontEditorViewModel Model { get => GetValue(ModelProperty); set => SetValue(ModelProperty, value); }
        public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
        public int Revision { get => GetValue(RevisionProperty); set => SetValue(RevisionProperty, value); }

        private const int Big = 3;

        static FontSampleLine()
        {
            AffectsRender<FontSampleLine>(ModelProperty, TextProperty, RevisionProperty);
            AffectsMeasure<FontSampleLine>(ModelProperty, TextProperty, RevisionProperty);
        }

        private FieldFont Font => Model?.Font;

        protected override Size MeasureOverride(Size available)
        {
            var font = Font;
            if (font == null) return new Size(240, 40);
            int w = Math.Max(1, Width(font, Text));
            return new Size(w * Big, font.Height * (1 + Big) + 8);
        }

        public override void Render(DrawingContext ctx)
        {
            var font = Font;
            if (font == null)
            {
                ctx.DrawText(new FormattedText("No font is loaded.", System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, new Typeface(FontFamily.Default), 12,
                    new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60))), new Point(0, 0));
                return;
            }
            if (!FieldFontCharacters.Ready) return;

            var ink = new SolidColorBrush(Color.FromRgb(0x28, 0x28, 0x28));
            var pale = new SolidColorBrush(Color.FromRgb(0x98, 0x98, 0x98));

            Draw(ctx, font, Text, 0, 0, 1, ink, pale);
            Draw(ctx, font, Text, 0, font.Height + 8, Big, ink, pale);
        }

        private static int Width(FieldFont font, string text)
        {
            if (string.IsNullOrEmpty(text)) return 1;
            int w = 0;
            foreach (char c in text) w += font.WidthOf(FieldFontCharacters.GlyphFor(c));
            return w;
        }

        // One letter at a time at its own width, the way the games lay a line out.
        private static void Draw(DrawingContext ctx, FieldFont font, string text,
                                 double left, double top, int zoom, IBrush ink, IBrush pale)
        {
            if (string.IsNullOrEmpty(text)) return;
            double x = left;
            foreach (char c in text)
            {
                int glyph = FieldFontCharacters.GlyphFor(c);
                if (glyph < 0) { x += font.MaxWidth / 2.0 * zoom; continue; }
                int w = font.WidthOf(glyph);
                for (int py = 0; py < font.Height; py++)
                    for (int px = 0; px < w; px++)
                    {
                        byte v = font.PixelAt(glyph, px, py);
                        if (v == FieldFont.Nothing || v == FieldFont.Paper) continue;
                        ctx.FillRectangle(v == 1 ? ink : pale,
                                          new Rect(x + px * zoom, top + py * zoom, zoom, zoom));
                    }
                x += w * zoom;
            }
        }
    }
}
