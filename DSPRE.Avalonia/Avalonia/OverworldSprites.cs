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

        /// <summary>
        /// One tile is sixteen pixels of overworld art, so a 32 by 32 sprite stands two tiles tall.
        /// </summary>
        public const int PixelsPerTile = 16;

        /// <summary>One picture out of a sprite bank. </summary>
        public static SpritePixels Get(ushort eventEntryID, ushort orientation, int picture = 0)
        {
            // The picture is part of what is being cached, so it gets its own room in the key.
            ushort cacheDir = (ushort)(orientation | ((picture & 0x1F) << 8));
            return GetCached(eventEntryID, cacheDir, orientation, picture);
        }

        /// <summary>How many pictures the bank has for each way of facing, so a caller can pace them.</summary>
        public static int FrameCount(ushort eventEntryID)
        {
            if (_frameCounts.TryGetValue(eventEntryID, out int n)) return n;
            n = 0;
            try
            {
                if (OverworldTable == null) { SetOWtable(); ReadOWTable(); }
                if (OverworldTable != null && OverworldTable.TryGetValue(eventEntryID, out var r) && r.spriteID != 0x3D3D)
                {
                    using var s = new FileStream(Path.Combine(gameDirs[DirNames.OWSprites].unpackedDir, r.spriteID.ToString("D4")), FileMode.Open, FileAccess.Read);
                    n = new NSBTX_File(s).texInfo.num_objs;
                }
            }
            catch { }
            if (n > 0) _frameCounts[eventEntryID] = n;
            return n;
        }

        private static readonly Dictionary<ushort, int> _frameCounts = new Dictionary<ushort, int>();

        private static SpritePixels GetCached(ushort eventEntryID, ushort cacheDir, ushort orientation, int picture)
        {
            var key = (eventEntryID, cacheDir);
            if (_cache.TryGetValue(key, out var cached)) return cached;
            SpritePixels result = null;
            try
            {
                var raw = LoadRaw(eventEntryID, orientation, picture);
                if (raw != null) result = ToRgba(raw);
            }
            catch (Exception ex) { AppLogger.Error("OW sprite load failed: " + ex.Message); }
            // Only keep what actually loaded. A sprite asked for before the ROM's folders are ready
            // comes back empty, and remembering that would leave the person invisible from then on.
            if (result != null) _cache[key] = result;
            return result;
        }

        public static void ClearCache() { _cache.Clear(); _frameCounts.Clear(); }

        // Mirrors EventEditor.GetOverworldImage. NSBTX (ROM) frames decode GDI-free via GetRawImage;
        // the fixed fallback images come from the avares assets (see ResourceImages), no GDI anywhere.
        private static DSPRE.RawImage LoadRaw(ushort eventEntryID, ushort orientation, int picture)
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
                if (n < 8) return nsbtx.GetRawImage(Math.Min(orientation, (ushort)(n - 1)), 0).bmp;

                // The pictures are named "thing.1" upwards but the bank stores them sorted as text, so
                // ".10" sits before ".2". Put them back in the artist's order before picking one.
                int at = picture;
                if (at < 0 || at >= n) at = 0;
                return nsbtx.GetRawImage(NumericOrder(nsbtx)[at], 0).bmp;
            }
            catch
            {
                return ResourceImages.GetRaw("overworldUnreadable");
            }
        }

        /// <summary>
        /// The bank's pictures in the order their names number them, since the file keeps them sorted as
        /// text and ".10" then lands before ".2".
        /// </summary>
        private static int[] NumericOrder(NSBTX_File nsbtx)
        {
            int n = nsbtx.texInfo.num_objs;
            var order = new int[n];
            var rank = new int[n];
            for (int i = 0; i < n; i++)
            {
                string name = nsbtx.texInfo.names[i] ?? "";
                int dot = name.LastIndexOf('.');
                rank[i] = dot >= 0 && int.TryParse(name.Substring(dot + 1), out int v) ? v : i;
                order[i] = i;
            }
            Array.Sort(rank, order);
            return order;
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
