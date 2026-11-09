using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace DSPRE.Avalonia.Controls
{
    /// <summary>One overworld's pictures as bitmaps, made once each, for the controls that animate a sprite.</summary>
    internal sealed class SpritePictures
    {
        /// <summary>Every sprite animation steps at the field's frame rate.</summary>
        public const int FrameMs = 1000 / 30;

        private readonly Dictionary<int, Bitmap> _made = new();

        /// <summary>A timer ticking at the field's frame rate.</summary>
        public static DispatcherTimer Clock(Action tick)
        {
            DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(FrameMs) };
            timer.Tick += (_, _) => tick();
            return timer;
        }

        /// <summary>The picture, or null when the sprite can't be read.</summary>
        public Bitmap At(ushort entry, int facing, int picture, bool ifUnpacked)
        {
            if (_made.TryGetValue(picture, out Bitmap made)) return made;
            OverworldSprites.SpritePixels pix = OverworldSprites.Get(entry, (ushort)facing, picture, ifUnpacked);
            if (pix == null || pix.Width <= 0 || pix.Height <= 0) return null;
            Bitmap bmp = ImageConverter.FromRgba(pix.Rgba, pix.Width, pix.Height);
            _made[picture] = bmp;
            return bmp;
        }

        /// <summary>Drops the bitmaps, as when the control shows another sprite or leaves the window.</summary>
        public void Clear()
        {
            foreach (Bitmap b in _made.Values) b.Dispose();
            _made.Clear();
        }

        /// <summary>Draws a picture with hard pixel edges.</summary>
        public static void Draw(DrawingContext context, Bitmap picture, Rect where)
        {
            using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
                context.DrawImage(picture, where);
        }
    }
}
