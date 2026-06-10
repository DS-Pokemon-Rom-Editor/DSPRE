using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using NSMBe4.NSBMD;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Resolves an overworld's sprite bitmap (by overlay-table entry + orientation) the same
    /// way the WinForms event editor does — 3D-overworld resource images, the NSBTX frame
    /// banks under <c>OWSprites</c>, or a fallback bounding-box — and decodes it to top-down
    /// RGBA for upload as a GL billboard texture. Results are cached per (entry, orientation).
    /// </summary>
    public static class OverworldSprites
    {
        public sealed class SpritePixels { public byte[] Rgba; public int Width, Height; }

        private static readonly Dictionary<(ushort, ushort), SpritePixels> _cache = new Dictionary<(ushort, ushort), SpritePixels>();

        public static SpritePixels Get(ushort eventEntryID, ushort orientation)
        {
            var key = (eventEntryID, orientation);
            if (_cache.TryGetValue(key, out var cached)) return cached;
            SpritePixels result = null;
            try
            {
                using var bmp = LoadBitmap(eventEntryID, orientation);
                if (bmp != null) result = ToRgba(bmp);
            }
            catch (Exception ex) { AppLogger.Error("OW sprite load failed: " + ex.Message); }
            _cache[key] = result;
            return result;
        }

        public static void ClearCache() => _cache.Clear();

        // Mirrors EventEditor.GetOverworldImage.
        private static Bitmap LoadBitmap(ushort eventEntryID, ushort orientation)
        {
            if (ow3DSpriteDict.TryGetValue(eventEntryID, out string imageName))
                return (Bitmap)DSPRE.Properties.Resources.ResourceManager.GetObject(imageName);

            if (!OverworldTable.TryGetValue(eventEntryID, out (uint spriteID, ushort properties) result))
                return (Bitmap)DSPRE.Properties.Resources.ResourceManager.GetObject("overworld");

            try
            {
                using var stream = new FileStream(gameDirs[DirNames.OWSprites].unpackedDir + "\\" + result.spriteID.ToString("D4"), FileMode.Open, FileAccess.Read);
                var nsbtx = new NSBTX_File(stream);
                int n = nsbtx.texInfo.num_objs;
                if (n <= 1) return nsbtx.GetBitmap(0, 0).bmp;
                if (n <= 4) return nsbtx.GetBitmap(orientation switch { 0 => 0, 1 => 1, 2 => 2, _ => 3 }, 0).bmp;
                if (n <= 8) return nsbtx.GetBitmap(orientation switch { 0 => 0, 1 => 2, 2 => 4, _ => 6 }, 0).bmp;
                if (n <= 16) return nsbtx.GetBitmap(orientation switch { 0 => 0, 1 => 11, 2 => 2, _ => 4 }, 0).bmp;
                return nsbtx.GetBitmap(orientation switch { 0 => 0, 1 => 27, 2 => 2, _ => 4 }, 0).bmp;
            }
            catch
            {
                return (Bitmap)DSPRE.Properties.Resources.ResourceManager.GetObject("overworldUnreadable");
            }
        }

        // Convert to RGBA (top row first), treating the top-left pixel's colour as transparent
        // (matching WinForms Bitmap.MakeTransparent()).
        private static SpritePixels ToRgba(Bitmap src)
        {
            int w = src.Width, h = src.Height;
            Color keyColor = src.GetPixel(0, 0);
            byte kr = keyColor.R, kg = keyColor.G, kb = keyColor.B;

            var bmp = src.PixelFormat == PixelFormat.Format32bppArgb ? src : new Bitmap(src);
            var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var rgba = new byte[w * h * 4];
            var rowBuf = new byte[Math.Abs(data.Stride)];
            try
            {
                for (int y = 0; y < h; y++)
                {
                    System.Runtime.InteropServices.Marshal.Copy(data.Scan0 + y * data.Stride, rowBuf, 0, w * 4);
                    int o = y * w * 4;
                    for (int x = 0; x < w; x++)
                    {
                        // Source is BGRA in memory (Format32bppArgb, little-endian).
                        byte b = rowBuf[x * 4 + 0], g = rowBuf[x * 4 + 1], r = rowBuf[x * 4 + 2], a = rowBuf[x * 4 + 3];
                        if (a != 0 && r == kr && g == kg && b == kb) a = 0;   // colour-key transparency
                        rgba[o + x * 4 + 0] = r;
                        rgba[o + x * 4 + 1] = g;
                        rgba[o + x * 4 + 2] = b;
                        rgba[o + x * 4 + 3] = a;
                    }
                }
            }
            finally
            {
                bmp.UnlockBits(data);
                if (!ReferenceEquals(bmp, src)) bmp.Dispose();
            }
            return new SpritePixels { Rgba = rgba, Width = w, Height = h };
        }
    }
}
