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
        /// (BGRA8888, straight alpha) by copying straight into a <see cref="WriteableBitmap"/>, no GDI+,
        /// no PNG round-trip. This is the permanent cross-platform render seam; as decoders start emitting
        /// <see cref="DSPRE.RawImage"/>, they render through here directly.
        /// </summary>
        /// <summary>Builds a bitmap from plain red, green, blue, alpha bytes, which is what the graphic
        /// browser's decoders hand back. RawImage wants them the other way round, so swap as we go.</summary>
        public static AvaloniaBitmap FromRgba(byte[] rgba, int width, int height)
        {
            if (rgba == null || width <= 0 || height <= 0) return null;
            int n = width * height;
            var bgra = new byte[n * 4];
            for (int i = 0; i < n && i * 4 + 3 < rgba.Length; i++)
            {
                bgra[i * 4] = rgba[i * 4 + 2];
                bgra[i * 4 + 1] = rgba[i * 4 + 1];
                bgra[i * 4 + 2] = rgba[i * 4];
                bgra[i * 4 + 3] = rgba[i * 4 + 3];
            }
            return ToAvaloniaBitmap(new DSPRE.RawImage(width, height, bgra));
        }

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
                    // Destination rows may be padded to a wider stride, so copy row by row.
                    for (int y = 0; y < img.Height; y++)
                        Marshal.Copy(img.Bgra, y * srcStride, IntPtr.Add(fb.Address, y * fb.RowBytes), srcStride);
                }
            }

            return wb;
        }

        /// <summary>
        /// Loads an hg-engine icon.png (data/graphics/sprites/&lt;name&gt;/icon.png), which is a
        /// vertical N-frame bounce-animation strip with no real alpha channel; crops to the first
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

        /// <summary>Loads a vertical N-frame overworld sprite strip (same layout as icon.png: each frame
        /// Width×Width, color-keyed, no real alpha) and returns every frame instead of just the first.</summary>
        public static AvaloniaBitmap[] LoadHgeOverworldFrames(string pngPath)
        {
            using var fs = System.IO.File.OpenRead(pngPath);
            var raw = DecodeRawImage(fs);
            if (raw == null || raw.Width <= 0) return Array.Empty<AvaloniaBitmap>();

            int frameSize = raw.Width;
            if (raw.Height <= frameSize || raw.Height % frameSize != 0)
            {
                ApplyCornerColorKeyTransparency(raw);
                return new[] { ToAvaloniaBitmap(raw) };
            }

            int count = raw.Height / frameSize;
            int frameBytes = raw.Stride * frameSize;
            var frames = new AvaloniaBitmap[count];
            for (int i = 0; i < count; i++)
            {
                var frame = new DSPRE.RawImage(frameSize, frameSize);
                Array.Copy(raw.Bgra, i * frameBytes, frame.Bgra, 0, frameBytes);
                ApplyCornerColorKeyTransparency(frame);
                frames[i] = ToAvaloniaBitmap(frame);
            }
            return frames;
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
        /// codecs; the cross-platform replacement for <c>new System.Drawing.Bitmap(stream)</c>.
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
