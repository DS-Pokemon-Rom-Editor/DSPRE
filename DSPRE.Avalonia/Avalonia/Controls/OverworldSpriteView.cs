using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Controls
{
    /// <summary>
    /// One overworld from the sprite table standing still and facing one way, drawn pixel for pixel. A pair Pokémon
    /// bobs to the game's always-running clock as it does in the field.
    /// </summary>
    public class OverworldSpriteView : Control
    {
        public static readonly StyledProperty<int> EntryProperty = AvaloniaProperty.Register<OverworldSpriteView, int>(nameof(Entry), -1);
        public static readonly StyledProperty<int> FacingProperty = AvaloniaProperty.Register<OverworldSpriteView, int>(nameof(Facing), (int)MoveFacing.Down);
        public static readonly StyledProperty<int> ScaleProperty = AvaloniaProperty.Register<OverworldSpriteView, int>(nameof(Scale), 2);

        /// <summary>The overworld table entry, or -1 for nothing.</summary>
        public int Entry { get => GetValue(EntryProperty); set => SetValue(EntryProperty, value); }
        public int Facing { get => GetValue(FacingProperty); set => SetValue(FacingProperty, value); }

        /// <summary>Screen pixels per sprite pixel; 0 fills the space with the largest whole number that fits, centred.</summary>
        public int Scale { get => GetValue(ScaleProperty); set => SetValue(ScaleProperty, value); }

        private const int FrameMs = 1000 / 30;
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(FrameMs) };
        private readonly FieldWalkCycle _cycle = new();
        private readonly Dictionary<int, Bitmap> _pictures = new();
        private int _frames;
        private Bitmap _shown;

        static OverworldSpriteView()
        {
            AffectsMeasure<OverworldSpriteView>(EntryProperty, ScaleProperty);
        }

        public OverworldSpriteView()
        {
            IsHitTestVisible = false;
            _timer.Tick += (_, _) => Step();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == EntryProperty || change.Property == FacingProperty) Reload();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            Reload();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            _timer.Stop();
            Forget();
        }

        private void Forget()
        {
            foreach (Bitmap b in _pictures.Values) b.Dispose();
            _pictures.Clear();
            _shown = null;
        }

        private void Reload()
        {
            _timer.Stop();
            Forget();
            _frames = Entry >= 0 && VisualRoot != null ? OverworldSprites.FrameCount((ushort)Entry) : 0;
            Step();
            // Only banks with more than one picture for a facing move while standing.
            if (FieldSpriteAnimation.PerFacing(_frames) > 1) _timer.Start();
        }

        private void Step()
        {
            _cycle.Tick();
            _shown = Entry >= 0 && VisualRoot != null ? PictureAt(FieldSpriteAnimation.PictureFor(_frames, Facing, _cycle)) : null;
            InvalidateMeasure();
            InvalidateVisual();
        }

        private Bitmap PictureAt(int picture)
        {
            if (_pictures.TryGetValue(picture, out Bitmap b)) return b;
            OverworldSprites.SpritePixels pix = OverworldSprites.Get((ushort)Entry, (ushort)Facing, picture);
            if (pix == null || pix.Width <= 0 || pix.Height <= 0) return null;
            Bitmap bmp = ImageConverter.FromRgba(pix.Rgba, pix.Width, pix.Height);
            _pictures[picture] = bmp;
            return bmp;
        }

        protected override Size MeasureOverride(Size availableSize) =>
            _shown == null || Scale <= 0 ? default : new Size(_shown.PixelSize.Width * Scale, _shown.PixelSize.Height * Scale);

        public override void Render(DrawingContext context)
        {
            if (_shown == null) return;
            int sw = _shown.PixelSize.Width, sh = _shown.PixelSize.Height;
            int scale = Scale > 0 ? Scale : Math.Max(1, (int)Math.Min(Bounds.Width / sw, Bounds.Height / sh));
            double w = sw * scale, h = sh * scale;
            double top = Scale > 0 ? Bounds.Height - h : (Bounds.Height - h) / 2;
            using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
                context.DrawImage(_shown, new Rect((Bounds.Width - w) / 2, top, w, h));
        }
    }
}
