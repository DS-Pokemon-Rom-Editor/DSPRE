using System;
using System.Collections.Generic;
using System.IO;
using DSPRE.ROMFiles;
using LibNDSFormats.NSBMD;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia
{
    /// <summary>An overworld's sprite for one entry and facing, as top-down RGBA for a GL billboard, cached per entry and facing.</summary>
    public static class OverworldSprites
    {
        public sealed class SpritePixels { public byte[] Rgba; public int Width, Height; }

        private static readonly Dictionary<(ushort, ushort), SpritePixels> Cache = new Dictionary<(ushort, ushort), SpritePixels>();

        /// <summary>
        /// One tile is sixteen pixels of overworld art, so a 32 by 32 sprite stands two tiles tall.
        /// </summary>
        public const int PixelsPerTile = 16;

        /// <summary>One picture out of a sprite bank; with no picture given, the one standing facing <paramref name="orientation"/>.</summary>
        public static SpritePixels Get(ushort eventEntryID, ushort orientation, int picture = -1)
        {
            eventEntryID = (ushort)VariableOverworldStandIn(eventEntryID);
            if (picture < 0) picture = FieldSpriteAnimation.PictureFor(FrameCount(eventEntryID), orientation, null);
            // The picture is part of what is being cached, so it gets its own room in the key.
            ushort cacheDir = (ushort)(orientation | ((picture & 0x1F) << 8));
            return GetCached(eventEntryID, cacheDir, orientation, picture);
        }

        /// <summary>How many pictures the bank has for each way of facing, so a caller can pace them.</summary>
        public static int FrameCount(ushort eventEntryID)
        {
            eventEntryID = (ushort)VariableOverworldStandIn(eventEntryID);
            if (FrameCounts.TryGetValue(eventEntryID, out int n)) return n;
            n = 0;
            try
            {
                if (OverworldTable == null) { SetOWtable(); ReadOWTable(); }
                if (OverworldTable != null && OverworldTable.TryGetValue(eventEntryID, out (uint spriteID, ushort properties) r) && r.spriteID != 0x3D3D)
                {
                    n = Tex0Block.ReadBtx0(File.ReadAllBytes(Path.Combine(GameDirs[DirNames.OWSprites].unpackedDir, r.spriteID.ToString("D4"))), out _)?.Textures.Count ?? 0;
                }
            }
            catch { }   // a sprite that can't be read reports no frames and is tried again next time
            if (n > 0) FrameCounts[eventEntryID] = n;
            return n;
        }

        private static readonly Dictionary<ushort, int> FrameCounts = new Dictionary<ushort, int>();

        private static SpritePixels GetCached(ushort eventEntryID, ushort cacheDir, ushort orientation, int picture)
        {
            (ushort eventEntryID, ushort cacheDir) key = (eventEntryID, cacheDir);
            if (Cache.TryGetValue(key, out SpritePixels cached)) return cached;
            SpritePixels result = null;
            try
            {
                RawImage raw = LoadRaw(eventEntryID, orientation, picture);
                if (raw != null) result = ToRgba(raw);
            }
            catch (Exception ex) { AppLogger.Error("OW sprite load failed: " + ex.Message); }
            // Only keep what actually loaded. A sprite asked for before the ROM's folders are ready
            // comes back empty, and remembering that would leave the person invisible from then on.
            if (result != null) Cache[key] = result;
            return result;
        }

        public static void ClearCache() { Cache.Clear(); FrameCounts.Clear(); _outlined.Clear(); }

        private static readonly Dictionary<SpritePixels, SpritePixels> _outlined = new Dictionary<SpritePixels, SpritePixels>();

        /// <summary>The picture one pixel bigger on each side, with an amber line around everything that shows.</summary>
        public static SpritePixels Outlined(SpritePixels src)
        {
            if (_outlined.TryGetValue(src, out SpritePixels done)) return done;
            int w = src.Width + 2, h = src.Height + 2;
            byte[] rgba = new byte[w * h * 4];
            bool Shows(int x, int y) => x >= 0 && y >= 0 && x < src.Width && y < src.Height && src.Rgba[(y * src.Width + x) * 4 + 3] != 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int at = (y * w + x) * 4;
                    if (Shows(x - 1, y - 1))
                    {
                        Array.Copy(src.Rgba, ((y - 1) * src.Width + x - 1) * 4, rgba, at, 4);
                        continue;
                    }
                    if (!Shows(x - 2, y - 1) && !Shows(x, y - 1) && !Shows(x - 1, y - 2) && !Shows(x - 1, y)) continue;
                    rgba[at] = 0xFF; rgba[at + 1] = 0xC7; rgba[at + 2] = 0x1A; rgba[at + 3] = 0xFF;
                }
            done = new SpritePixels { Rgba = rgba, Width = w, Height = h };
            _outlined[src] = done;
            return done;
        }

        // A sprite saved in another editor is read again; this runs ahead of the editors' background redraws.
        static OverworldSprites()
        {
            AppEvents.ArchiveMemberSaved += (_, dir) =>
            {
                if (dir == RomInfo.DirNames.OWSprites) global::Avalonia.Threading.Dispatcher.UIThread.Post(ClearCache);
            };
        }

        // Sprite bank frames use the bank's first palette; the fixed fallbacks come from ResourceImages.
        private static DSPRE.RawImage LoadRaw(ushort eventEntryID, ushort orientation, int picture)
        {
            // The lookup tables are populated during ROM/event setup; make sure they exist.
            if (Ow3DSpriteDict == null) try { Set3DOverworldsDict(); } catch (Exception ex) { AppLogger.Warn("Overworld 3D sprite table: " + ex.Message); }
            if (OverworldTable == null) try { SetOWtable(); ReadOWTable(); } catch (Exception ex) { AppLogger.Warn("Overworld table: " + ex.Message); }

            if (Ow3DSpriteDict != null && Ow3DSpriteDict.TryGetValue(eventEntryID, out string imageName))
                return ResourceImages.GetRaw(imageName);

            if (OverworldTable == null || !OverworldTable.TryGetValue(eventEntryID, out (uint spriteID, ushort properties) result))
                return ResourceImages.GetRaw("overworld");

            try
            {
                byte[] bank = File.ReadAllBytes(Path.Combine(GameDirs[DirNames.OWSprites].unpackedDir, result.spriteID.ToString("D4")));
                Tex0Block block = Tex0Block.ReadBtx0(bank, out _);
                if (block == null) return ResourceImages.GetRaw("overworldUnreadable");
                int n = block.Textures.Count;
                if (n <= 1) return Draw(bank, block, 0, 0);
                if (n < 8) return Draw(bank, block, Math.Min(orientation, (ushort)(n - 1)), 0);

                // The pictures are named "thing.1" upwards but the bank stores them sorted as text, so
                // ".10" sits before ".2". Put them back in the artist's order before picking one.
                int at = picture;
                if (at < 0 || at >= n) at = 0;
                return Draw(bank, block, NumericOrder(block)[at], 0);
            }
            catch (Exception ex)
            {
                AppLogger.WarnOnce("OverworldSprites.LoadRaw", ex);
                return ResourceImages.GetRaw("overworldUnreadable");
            }
        }

        /// <summary>Every picture of a sprite bank in the order their names number them, drawn with one of its palettes.</summary>
        public static List<SpritePixels> Pictures(byte[] btx0, int palette = 0)
        {
            List<SpritePixels> pictures = new List<SpritePixels>();
            if (btx0 == null || btx0.Length <= 4) return pictures;
            Tex0Block block = Tex0Block.ReadBtx0(btx0, out _);
            if (block == null || block.Textures.Count <= 0) return pictures;
            int pal = palette > 0 && palette < block.Palettes.Count ? palette : 0;
            foreach (int i in NumericOrder(block))
            {
                RawImage picture = Draw(btx0, block, i, pal);
                if (picture != null) pictures.Add(ToRgba(picture));
            }
            return pictures;
        }

        /// <summary>One texture of a sprite bank in one of its palettes, or null when either is missing.</summary>
        private static RawImage Draw(byte[] btx0, Tex0Block block, int texture, int palette)
        {
            if (texture < 0 || texture >= block.Textures.Count || palette < 0 || palette >= block.Palettes.Count) return null;
            Tex0Block.Texture t = block.Textures[texture];
            Tex0Block.Palette p = block.Palettes[palette];
            // A palette colour's top bit isn't see-through; the DS ignores it.
            RGBA[] colours = new RGBA[p.DataSize / 2];
            for (int i = 0; i < colours.Length; i++)
            {
                (byte r, byte g, byte b) = NitroBgCodec.Colour(btx0[p.DataAt + i * 2] | btx0[p.DataAt + i * 2 + 1] << 8);
                colours[i] = new RGBA { R = r, G = g, B = b, A = 0xFF };
            }
            NSBMDMaterial material = new NSBMDMaterial
            {
                Format = t.Format, Width = t.Width, Height = t.Height, Color0 = t.Color0 ? 1 : 0,
                Texdata = btx0.AsSpan(t.DataAt, t.DataSize).ToArray(),
                Spdata = t.Format == 5 ? btx0.AsSpan(t.IndexAt, t.IndexSize).ToArray() : null,
                Paldata = colours,
            };
            DSPRE.Avalonia.Gl.NsbmdTextureData decoded = DSPRE.Avalonia.Gl.NsbmdTextureDecoder.Decode(material);
            if (decoded == null) return null;
            RawImage image = new RawImage(decoded.Width, decoded.Height);
            for (int i = 0; i + 3 < decoded.Rgba.Length; i += 4)
            {
                image.Bgra[i] = decoded.Rgba[i + 2]; image.Bgra[i + 1] = decoded.Rgba[i + 1];
                image.Bgra[i + 2] = decoded.Rgba[i]; image.Bgra[i + 3] = decoded.Rgba[i + 3];
            }
            return image;
        }

        /// <summary>
        /// The bank's pictures in the order their names number them, since the file keeps them sorted as
        /// text and ".10" then lands before ".2".
        /// </summary>
        private static int[] NumericOrder(Tex0Block block)
        {
            int n = block.Textures.Count;
            int[] order = new int[n];
            int[] rank = new int[n];
            for (int i = 0; i < n; i++)
            {
                string name = block.Textures[i].Name ?? "";
                int dot = name.LastIndexOf('.');
                rank[i] = dot >= 0 && int.TryParse(name.Substring(dot + 1), out int v) ? v : i;
                order[i] = i;
            }
            Array.Sort(rank, order);
            return order;
        }

        // To RGBA, top row first, with the top-left pixel's colour see-through.
        private static SpritePixels ToRgba(DSPRE.RawImage src)
        {
            if (src == null || src.IsEmpty) return null;
            int w = src.Width, h = src.Height;
            byte kb = src.Bgra[0], kg = src.Bgra[1], kr = src.Bgra[2];   // top-left pixel = colour key

            byte[] rgba = new byte[w * h * 4];
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
