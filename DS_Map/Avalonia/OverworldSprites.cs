using System;
using System.Collections.Generic;
using System.IO;
using NSMBe4.NSBMD;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Resolves an overworld's sprite bitmap (by overlay-table entry + orientation) the same
    /// way the WinForms event editor does (3D-overworld resource images, the NSBTX frame
    /// banks under <c>OWSprites</c>, or a fallback bounding-box), and decodes it to top-down
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
                var raw = LoadRaw(eventEntryID, orientation);
                if (raw != null) result = ToRgba(raw);
            }
            catch (Exception ex) { AppLogger.Error("OW sprite load failed: " + ex.Message); }
            _cache[key] = result;
            return result;
        }

        public static void ClearCache() => _cache.Clear();

        // Mirrors EventEditor.GetOverworldImage. NSBTX (ROM) frames decode GDI-free via GetRawImage;
        // the fixed fallback images come from the avares assets (see ResourceImages), no GDI anywhere.
        private static DSPRE.RawImage LoadRaw(ushort eventEntryID, ushort orientation)
        {
            // The lookup tables are populated during ROM/event setup; make sure they exist.
            if (ow3DSpriteDict == null) try { Set3DOverworldsDict(); } catch { }
            if (OverworldTable == null) try { SetOWtable(); ReadOWTable(); } catch { }

            if (ow3DSpriteDict != null && ow3DSpriteDict.TryGetValue(eventEntryID, out string imageName))
                return ResourceImages.GetRaw(imageName);

            if (OverworldTable == null || !OverworldTable.TryGetValue(eventEntryID, out (uint spriteID, ushort properties) result))
                return ResourceImages.GetRaw("overworld");

            try
            {
                using var stream = new FileStream(Path.Combine(gameDirs[DirNames.OWSprites].unpackedDir, result.spriteID.ToString("D4")), FileMode.Open, FileAccess.Read);
                var nsbtx = new NSBTX_File(stream);
                int n = nsbtx.texInfo.num_objs;
                if (n <= 1) return nsbtx.GetRawImage(0, 0).bmp;
                if (n <= 4) return nsbtx.GetRawImage(orientation switch { 0 => 0, 1 => 1, 2 => 2, _ => 3 }, 0).bmp;
                if (n <= 8) return nsbtx.GetRawImage(orientation switch { 0 => 0, 1 => 2, 2 => 4, _ => 6 }, 0).bmp;
                if (n <= 16) return nsbtx.GetRawImage(orientation switch { 0 => 0, 1 => 11, 2 => 2, _ => 4 }, 0).bmp;
                return nsbtx.GetRawImage(orientation switch { 0 => 0, 1 => 27, 2 => 2, _ => 4 }, 0).bmp;
            }
            catch
            {
                return ResourceImages.GetRaw("overworldUnreadable");
            }
        }

        // Convert to RGBA (top row first), treating the top-left pixel's colour as transparent
        // (matching WinForms Bitmap.MakeTransparent()).
        private static SpritePixels ToRgba(DSPRE.RawImage src)
        {
            if (src == null || src.IsEmpty) return null;
            int w = src.Width, h = src.Height;
            byte kb = src.Bgra[0], kg = src.Bgra[1], kr = src.Bgra[2];   // top-left pixel = colour key

            var rgba = new byte[w * h * 4];
            for (int i = 0; i < w * h; i++)
            {
                byte b = src.Bgra[i * 4 + 0], g = src.Bgra[i * 4 + 1], r = src.Bgra[i * 4 + 2], a = src.Bgra[i * 4 + 3];
                if (a != 0 && r == kr && g == kg && b == kb) a = 0;   // colour-key transparency
                rgba[i * 4 + 0] = r;
                rgba[i * 4 + 1] = g;
                rgba[i * 4 + 2] = b;
                rgba[i * 4 + 3] = a;
            }
            return new SpritePixels { Rgba = rgba, Width = w, Height = h };
        }
    }
}
