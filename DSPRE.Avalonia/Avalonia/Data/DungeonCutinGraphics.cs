using System;
using System.Collections.Generic;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Reads and writes the graphics a Dungeon Cutin table row points at: standard NCLR palette + NCGR
    /// tiles + NSCR screen/tilemap trios in a/1/5/0 (HGSS only), one triple per timezone slot. Decoding
    /// uses <see cref="NitroBgCodec"/>; writing rebuilds the archive's first file of each kind around the new
    /// data, so every field the game might read keeps the value it ships with.
    ///
    /// Multiple rows/timezones can share the same archive index (most zones reuse their noon art for
    /// evening), so import always allocates brand-new NARC members instead of overwriting the slot a
    /// row currently points at, which would otherwise silently corrupt every other row/timezone sharing
    /// that index (same pattern as OverworldSpriteTableExpansion.AllocateNewMmodelSlot). New members wait
    /// in the editor's pending writes until it saves.
    /// </summary>
    public sealed class DungeonCutinGraphics
    {
        public const int MaxPaletteColors = 176; // 11 palettes of 16
        public const int MaxDimension = 256;      // keeps screen-block addressing a single 32x32-tile block

        private readonly ScriptNarc _narc = new ScriptNarc(DirNames.dungeonCutinGraphics);
        private readonly PendingArchiveWrites _pending;
        private int? _templateNclr, _templateNcgr, _templateNscr;

        public DungeonCutinGraphics(PendingArchiveWrites pending) { _pending = pending; }

        public bool Available => _narc.Available;

        private enum Kind { Unknown, Nclr, Ncgr, Nscr }

        private static Kind Classify(byte[] decompressed)
        {
            if (decompressed == null || decompressed.Length < 4) return Kind.Unknown;
            if (decompressed[0] == 'R' && decompressed[1] == 'L' && decompressed[2] == 'C' && decompressed[3] == 'N') return Kind.Nclr;
            if (decompressed[0] == 'R' && decompressed[1] == 'G' && decompressed[2] == 'C' && decompressed[3] == 'N') return Kind.Ncgr;
            if (decompressed[0] == 'R' && decompressed[1] == 'C' && decompressed[2] == 'S' && decompressed[3] == 'N') return Kind.Nscr;
            return Kind.Unknown;
        }

        /// <summary>Finds the first member of each type by magic-sniffing rather than a fixed index,
        /// since member order varies per ROM revision.</summary>
        private void EnsureTemplates()
        {
            if (_templateNclr.HasValue && _templateNcgr.HasValue && _templateNscr.HasValue) return;
            int count = _narc.Count;
            for (int i = 0; i < count && (!_templateNclr.HasValue || !_templateNcgr.HasValue || !_templateNscr.HasValue); i++)
            {
                byte[] inflated = NitroBgCodec.Inflate(_narc.Get(i));
                switch (Classify(inflated))
                {
                    case Kind.Nclr: _templateNclr ??= i; break;
                    case Kind.Ncgr: _templateNcgr ??= i; break;
                    case Kind.Nscr: _templateNscr ??= i; break;
                }
            }
        }

        /// <summary>Decodes the palette/tiles/screen trio for one timezone slot into a previewable image, or null.</summary>
        public RawImage Composite(int paletteIdx, int tilesIdx, int screenIdx)
        {
            if (!Available) return null;
            using IDisposable pending = _pending.Reading();
            byte[] pal = NitroBgCodec.Inflate(_narc.Get(paletteIdx));
            byte[] chr = NitroBgCodec.Inflate(_narc.Get(tilesIdx));
            byte[] scr = NitroBgCodec.Inflate(_narc.Get(screenIdx));
            if (pal == null || chr == null || scr == null) return null;

            NitroBgCodec.BgImage bg;
            try { bg = NitroBgCodec.Composite(chr, pal, scr); }
            catch { return null; }

            RawImage raw = new RawImage(bg.Width, bg.Height);
            byte[] src = bg.Rgba, dst = raw.Bgra;
            for (int i = 0; i + 3 < src.Length; i += 4)
            {
                dst[i] = src[i + 2]; dst[i + 1] = src[i + 1]; dst[i + 2] = src[i]; dst[i + 3] = src[i + 3];
            }
            return raw;
        }

        /// <summary>
        /// Encodes <paramref name="img"/> as a brand-new NCLR+NCGR+NSCR triple and writes it into 3 new
        /// NARC members (never touching any existing member). Returns the 3 new indices on success.
        /// </summary>
        public bool Import(RawImage img, out int newPaletteIdx, out int newTilesIdx, out int newScreenIdx, out string error)
        {
            newPaletteIdx = newTilesIdx = newScreenIdx = -1;
            error = null;

            if (!Available) { error = "Dungeon Cutin graphics archive is not available for this ROM."; return false; }
            if (img == null || img.IsEmpty) { error = "No image."; return false; }
            if (img.Width % 8 != 0 || img.Height % 8 != 0)
            { error = $"Image must be a multiple of 8 pixels in both dimensions (got {img.Width}x{img.Height})."; return false; }
            if (img.Width > MaxDimension || img.Height > MaxDimension)
            { error = $"Image must be at most {MaxDimension}x{MaxDimension} (got {img.Width}x{img.Height})."; return false; }

            EnsureTemplates();
            if (!_templateNclr.HasValue || !_templateNcgr.HasValue || !_templateNscr.HasValue)
            { error = "Could not find an existing palette/tiles/screen file in the archive to use as a format template."; return false; }

            try
            {
                if (!Encode(img, NitroBgCodec.Inflate(_narc.Get(_templateNclr.Value)), NitroBgCodec.Inflate(_narc.Get(_templateNcgr.Value)),
                            NitroBgCodec.Inflate(_narc.Get(_templateNscr.Value)), out byte[] nclrBytes, out byte[] ncgrPlain,
                            out byte[] nscrPlain, out error))
                    return false;
                byte[] ncgrBytes = DSPRE.ROMFiles.Lz77.Compress(ncgrPlain);
                byte[] nscrBytes = DSPRE.ROMFiles.Lz77.Compress(nscrPlain);

                int next = _pending.Count(DirNames.dungeonCutinGraphics);
                (newPaletteIdx, newTilesIdx, newScreenIdx) = (next, next + 1, next + 2);
                int p = newPaletteIdx, t = newTilesIdx, s = newScreenIdx;
                _pending.Change(step => step.Put(DirNames.dungeonCutinGraphics, p, nclrBytes)
                                            .Put(DirNames.dungeonCutinGraphics, t, ncgrBytes)
                                            .Put(DirNames.dungeonCutinGraphics, s, nscrBytes));
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                newPaletteIdx = newTilesIdx = newScreenIdx = -1;
                return false;
            }
        }

        /// <summary>
        /// Turns a picture into a 256-colour palette, tile sheet and screen, each built on the given template file:
        /// its outer header, unknown fields and any sections after the first are kept, and the sizes are rewritten.
        /// </summary>
        public static bool Encode(RawImage img, byte[] nclrTemplate, byte[] ncgrTemplate, byte[] nscrTemplate,
                                  out byte[] nclr, out byte[] ncgr, out byte[] nscr, out string error)
        {
            nclr = ncgr = nscr = null;
            error = null;

            // Index 0 is reserved for transparency, so at most MaxPaletteColors-1 distinct opaque
            // colours are usable.
            Dictionary<int, byte> colorToIndex = new Dictionary<int, byte>();
            ushort[] palette = new ushort[256];
            int cols = img.Width / 8, rows = img.Height / 8;
            byte[] tiles = new byte[cols * rows * 64];

            for (int ty = 0; ty < rows; ty++)
                for (int tx = 0; tx < cols; tx++)
                    for (int py = 0; py < 8; py++)
                        for (int px = 0; px < 8; px++)
                        {
                            int x = tx * 8 + px, y = ty * 8 + py;
                            int si = (y * img.Width + x) * 4;
                            byte b = img.Bgra[si], g = img.Bgra[si + 1], r = img.Bgra[si + 2], a = img.Bgra[si + 3];
                            byte index;
                            if (a < 128)
                            {
                                index = 0;
                            }
                            else
                            {
                                int key = (r << 16) | (g << 8) | b;
                                if (!colorToIndex.TryGetValue(key, out index))
                                {
                                    if (colorToIndex.Count >= MaxPaletteColors - 1)
                                    {
                                        error = $"Image uses more than {MaxPaletteColors - 1} distinct opaque colours " +
                                            "(plus transparency). Reduce the palette before importing.";
                                        return false;
                                    }
                                    index = (byte)(colorToIndex.Count + 1);
                                    colorToIndex[key] = index;
                                    palette[index] = NitroBgCodec.ToBgr555(r, g, b);
                                }
                            }
                            tiles[(ty * cols + tx) * 64 + py * 8 + px] = index;
                        }

            byte[] colourBytes = new byte[palette.Length * 2];
            for (int i = 0; i < palette.Length; i++) Put16(colourBytes, i * 2, palette[i]);
            byte[] squareBytes = new byte[cols * rows * 2];
            for (int i = 0; i < cols * rows; i++) Put16(squareBytes, i * 2, i);

            // TTLP: depth, two unknowns, colour bytes, offset to the colours.
            nclr = Rebuild(nclrTemplate, 0x18, colourBytes, false, head =>
            {
                Put32(head, 0x08, 4);
                Put32(head, 0x10, colourBytes.Length);
                Put32(head, 0x14, 0x10);
            });
            // RAHC: tiles down and across, depth, two unknowns, tiled flag, data size, offset to the data.
            ncgr = Rebuild(ncgrTemplate, 0x20, tiles, true, head =>
            {
                Put16(head, 0x08, rows);
                Put16(head, 0x0A, cols);
                Put32(head, 0x0C, 4);
                Put32(head, 0x18, tiles.Length);
                Put32(head, 0x1C, 0x18);
            });
            // NRCS: width and height in pixels, format, data size.
            nscr = Rebuild(nscrTemplate, 0x14, squareBytes, false, head =>
            {
                Put16(head, 0x08, img.Width);
                Put16(head, 0x0A, img.Height);
                Put32(head, 0x10, squareBytes.Length);
            });
            if (nclr == null || ncgr == null || nscr == null) { error = "The template files could not be read."; return false; }
            return true;
        }

        /// <summary>
        /// A template's 16-byte header and first section header around new data, with the sections after it copied
        /// when asked, and the section and file sizes rewritten.
        /// </summary>
        private static byte[] Rebuild(byte[] template, int sectionHeader, byte[] data, bool keepAfter, Action<byte[]> fill)
        {
            if (template == null || template.Length < 0x10 + sectionHeader) return null;
            int firstSize = NitroBgCodec.U32(template, 0x14);
            int afterAt = 0x10 + firstSize;
            if (firstSize < sectionHeader || afterAt > template.Length) return null;
            int afterLength = keepAfter ? template.Length - afterAt : 0;

            byte[] head = new byte[sectionHeader];
            Array.Copy(template, 0x10, head, 0, sectionHeader);
            fill(head);
            Put32(head, 0x04, sectionHeader + data.Length);

            byte[] outp = new byte[0x10 + sectionHeader + data.Length + afterLength];
            Array.Copy(template, outp, 0x10);
            Array.Copy(head, 0, outp, 0x10, sectionHeader);
            Array.Copy(data, 0, outp, 0x10 + sectionHeader, data.Length);
            Array.Copy(template, afterAt, outp, 0x10 + sectionHeader + data.Length, afterLength);
            Put32(outp, 0x08, outp.Length);
            return outp;
        }

        private static void Put16(byte[] d, int o, int v) { d[o] = (byte)v; d[o + 1] = (byte)(v >> 8); }
        private static void Put32(byte[] d, int o, int v) { Put16(d, o, v); Put16(d, o + 2, v >> 16); }
    }
}
