using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using DSPRE.Avalonia.ViewModels;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Views.Controls
{
    /// <summary>
    /// One letter, big enough to paint. Two bits a pixel gives four shades: nothing, the paper the box
    /// has already painted, and two inks. Left button paints the shade picked, right button rubs out.
    /// </summary>
    public sealed class GlyphPainter : Control
    {
        public static readonly StyledProperty<FontEditorViewModel> ModelProperty =
            AvaloniaProperty.Register<GlyphPainter, FontEditorViewModel>(nameof(Model));

        /// <summary>Bumped by the model whenever the letter changes, so this redraws.</summary>
        public static readonly StyledProperty<int> RevisionProperty =
            AvaloniaProperty.Register<GlyphPainter, int>(nameof(Revision));

        /// <summary>Which of the four shades the left button paints.</summary>
        public static readonly StyledProperty<int> ShadeProperty =
            AvaloniaProperty.Register<GlyphPainter, int>(nameof(Shade), 1);

        /// <summary>How wide the letter is, drawn as a line so you can see what runs past it.</summary>
        public static readonly StyledProperty<int> LetterWidthProperty =
            AvaloniaProperty.Register<GlyphPainter, int>(nameof(LetterWidth));

        public FontEditorViewModel Model { get => GetValue(ModelProperty); set => SetValue(ModelProperty, value); }
        public int Revision { get => GetValue(RevisionProperty); set => SetValue(RevisionProperty, value); }
        public int Shade { get => GetValue(ShadeProperty); set => SetValue(ShadeProperty, value); }
        public int LetterWidth { get => GetValue(LetterWidthProperty); set => SetValue(LetterWidthProperty, value); }

        static GlyphPainter()
        {
            AffectsRender<GlyphPainter>(ModelProperty, RevisionProperty, LetterWidthProperty);
        }

        public GlyphPainter()
        {
            Focusable = true;
            Width = FieldFont.CellSize * Zoom;
            Height = FieldFont.CellSize * Zoom;
        }

        private const int Zoom = 20;

        // The four shades a letter can hold. Paper is what the box behind the writing already painted,
        // so it shows here as the box's own colour rather than as ink.
        private static readonly IBrush[] Shades =
        {
            new SolidColorBrush(Color.FromRgb(0xF4, 0xF4, 0xF4)),   // nothing
            new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30)),   // ink
            new SolidColorBrush(Color.FromRgb(0x90, 0x90, 0x90)),   // second ink
            new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)),   // paper
        };

        public override void Render(DrawingContext ctx)
        {
            int n = FieldFont.CellSize;
            var model = Model;

            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    byte v = model?.PixelAt(x, y) ?? 0;
                    ctx.FillRectangle(Shades[v & 3], new Rect(x * Zoom, y * Zoom, Zoom, Zoom));
                }

            var grid = new Pen(new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)));
            for (int i = 0; i <= n; i++)
            {
                ctx.DrawLine(grid, new Point(i * Zoom, 0), new Point(i * Zoom, n * Zoom));
                ctx.DrawLine(grid, new Point(0, i * Zoom), new Point(n * Zoom, i * Zoom));
            }

            // Where the next letter starts. Anything drawn past this line is not rubbed out, it simply
            // sits under whatever comes next.
            int w = Math.Clamp(LetterWidth, 0, n);
            var edge = new Pen(new SolidColorBrush(Color.FromRgb(0xE0, 0x60, 0x40)), 2);
            ctx.DrawLine(edge, new Point(w * Zoom, 0), new Point(w * Zoom, n * Zoom));
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            Paint(e.GetPosition(this), e.GetCurrentPoint(this).Properties.IsRightButtonPressed);
            e.Pointer.Capture(this);
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            var props = e.GetCurrentPoint(this).Properties;
            if (props.IsLeftButtonPressed || props.IsRightButtonPressed)
                Paint(e.GetPosition(this), props.IsRightButtonPressed);
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            e.Pointer.Capture(null);
        }

        private void Paint(Point p, bool rubOut)
        {
            var model = Model;
            if (model == null || !model.HasGlyph) return;
            int x = (int)(p.X / Zoom), y = (int)(p.Y / Zoom);
            if (x < 0 || y < 0 || x >= FieldFont.CellSize || y >= FieldFont.CellSize) return;
            model.SetPixel(x, y, (byte)(rubOut ? 0 : Shade));
            InvalidateVisual();
        }
    }
}
