using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Media.Imaging;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;

namespace DSPRE.Avalonia
{
    internal static class ImageConverter
    {
        /// <summary>
        /// Builds an Avalonia <see cref="AvaloniaBitmap"/> from a toolkit-agnostic <see cref="DSPRE.RawImage"/>
        /// (BGRA8888, straight alpha) by copying straight into a <see cref="WriteableBitmap"/> — no GDI+,
        /// no PNG round-trip. This is the permanent cross-platform render seam; as decoders start emitting
        /// <see cref="DSPRE.RawImage"/>, they render through here directly.
        /// </summary>
        public static AvaloniaBitmap ToAvaloniaBitmap(DSPRE.RawImage img)
        {
            if (img == null || img.IsEmpty) return null;

            var wb = new WriteableBitmap(
                new PixelSize(img.Width, img.Height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Unpremul);

            using (var fb = wb.Lock())
            {
                int srcStride = img.Stride;
                if (fb.RowBytes == srcStride)
                {
                    Marshal.Copy(img.Bgra, 0, fb.Address, srcStride * img.Height);
                }
                else
                {
                    // Destination rows may be padded to a wider stride — copy row by row.
                    for (int y = 0; y < img.Height; y++)
                        Marshal.Copy(img.Bgra, y * srcStride, IntPtr.Add(fb.Address, y * fb.RowBytes), srcStride);
                }
            }

            return wb;
        }

        /// <summary>
        /// Loads an hg-engine icon.png (data/graphics/sprites/&lt;name&gt;/icon.png), which is a
        /// vertical N-frame bounce-animation strip with no real alpha channel — crops to the first
        /// Width×Width frame and color-keys the corner pixel to transparent.
        /// </summary>
        public static AvaloniaBitmap LoadHgeIconFirstFrame(string pngPath)
        {
            using var fs = System.IO.File.OpenRead(pngPath);
            var raw = DecodeRawImage(fs);
            if (raw == null) return null;

            int frameHeight = raw.Width;
            DSPRE.RawImage frame;
            if (frameHeight <= 0 || raw.Height <= frameHeight || raw.Height % frameHeight != 0)
            {
                frame = raw;
            }
            else
            {
                frame = new DSPRE.RawImage(raw.Width, frameHeight);
                Array.Copy(raw.Bgra, 0, frame.Bgra, 0, frame.Bgra.Length);
            }

            ApplyCornerColorKeyTransparency(frame);
            return ToAvaloniaBitmap(frame);
        }

        private static void ApplyCornerColorKeyTransparency(DSPRE.RawImage img)
        {
            if (img.IsEmpty) return;
            byte[] px = img.Bgra;
            byte keyB = px[0], keyG = px[1], keyR = px[2];
            for (int i = 0; i < px.Length; i += 4)
            {
                if (px[i] == keyB && px[i + 1] == keyG && px[i + 2] == keyR) px[i + 3] = 0;
            }
        }

        /// <summary>
        /// Decodes an encoded image stream (PNG/GIF/…) to a <see cref="DSPRE.RawImage"/> via Avalonia's
        /// codecs — the cross-platform replacement for <c>new System.Drawing.Bitmap(stream)</c>.
        /// Returns null on an unexpected pixel format.
        /// </summary>
        public static DSPRE.RawImage DecodeRawImage(System.IO.Stream stream)
        {
            using var wb = WriteableBitmap.Decode(stream);
            using ILockedFramebuffer fb = wb.Lock();
            int w = fb.Size.Width, h = fb.Size.Height;
            var raw = new DSPRE.RawImage(w, h);
            int stride = raw.Stride;
            for (int y = 0; y < h; y++)
                Marshal.Copy(fb.Address + y * fb.RowBytes, raw.Bgra, y * stride, stride);

            if (fb.Format == PixelFormat.Rgba8888)
            {
                byte[] px = raw.Bgra;
                for (int i = 0; i < px.Length; i += 4)
                    (px[i], px[i + 2]) = (px[i + 2], px[i]);   // RGBA → BGRA
            }
            else if (fb.Format != PixelFormat.Bgra8888)
            {
                AppLogger.Error($"DecodeRawImage: unexpected pixel format {fb.Format}");
                return null;
            }
            return raw;
        }
    }
}
