using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Ekona;
using Ekona.Images;
using Images;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Reads and writes the graphics a Dungeon Cutin table row points at: standard NCLR palette + NCGR
    /// tiles + NSCR screen/tilemap trios in a/1/5/0 (HGSS only), one triple per timezone slot. Decoding
    /// uses <see cref="NitroBgCodec"/>; writing goes through the full Images.NCLR/NCGR/NSCR classes so
    /// header/section sizes come out correctly formed.
    ///
    /// Multiple rows/timezones can share the same archive index (most zones reuse their noon art for
    /// evening), so import always allocates brand-new NARC members instead of overwriting the slot a
    /// row currently points at, which would otherwise silently corrupt every other row/timezone sharing
    /// that index (same pattern as OverworldSpriteTableExpansion.AllocateNewMmodelSlot).
    /// </summary>
    public sealed class DungeonCutinGraphics
    {
        public const int MaxPaletteColors = 176; // PALLETE_MAX(11) * 16
        public const int MaxDimension = 256;      // keeps screen-block addressing a single 32x32-tile block

        private readonly ScriptNarc _narc = new ScriptNarc(DirNames.dungeonCutinGraphics);
        private int? _templateNclr, _templateNcgr, _templateNscr;
        private int _nextFreeIndex = -1;

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
            _nextFreeIndex = count;
        }

        private string MemberPath(int id) => Path.Combine(gameDirs[DirNames.dungeonCutinGraphics].unpackedDir, id.ToString("D4"));

        private static string WriteTemp(byte[] data)
        {
            string path = Path.GetTempFileName();
            File.WriteAllBytes(path, data);
            return path;
        }

        /// <summary>Decompresses member <paramref name="id"/> to a temp file (Images.* classes read
        /// straight from disk and have no LZ awareness), returning the temp path.</summary>
        private string DecompressedTemplatePath(int id) => WriteTemp(NitroBgCodec.Inflate(_narc.Get(id)));

        /// <summary>Decodes the palette/tiles/screen trio for one timezone slot into a previewable image, or null.</summary>
        public RawImage Composite(int paletteIdx, int tilesIdx, int screenIdx)
        {
            if (!Available) return null;
            byte[] pal = NitroBgCodec.Inflate(_narc.Get(paletteIdx));
            byte[] chr = NitroBgCodec.Inflate(_narc.Get(tilesIdx));
            byte[] scr = NitroBgCodec.Inflate(_narc.Get(screenIdx));
            if (pal == null || chr == null || scr == null) return null;

            NitroBgCodec.BgImage bg;
            try { bg = NitroBgCodec.Composite(chr, pal, scr); }
            catch { return null; }

            var raw = new RawImage(bg.Width, bg.Height);
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

            // Index 0 is reserved for transparency, so at most MaxPaletteColors-1 distinct opaque
            // colours are usable.
            var colorToIndex = new Dictionary<int, byte>();
            var palette = new Color[256];
            int cols = img.Width / 8, rows = img.Height / 8;
            var tiles = new byte[cols * rows * 64];

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
                                    palette[index] = Color.FromArgb(r, g, b);
                                }
                            }
                            tiles[(ty * cols + tx) * 64 + py * 8 + px] = index;
                        }

            EnsureTemplates();
            if (!_templateNclr.HasValue || !_templateNcgr.HasValue || !_templateNscr.HasValue)
            { error = "Could not find an existing palette/tiles/screen file in the archive to use as a format template."; return false; }

            string tmpNclrIn = null, tmpNcgrIn = null, tmpNscrIn = null, tmpNclrOut = null, tmpNcgrOut = null, tmpNscrOut = null;
            try
            {
                tmpNclrIn = MemberPath(_templateNclr.Value); // NCLR members are never compressed on disk
                var nclr = new NCLR(tmpNclrIn, _templateNclr.Value);
                nclr.Set_Palette(palette, ColorFormat.colors256, true);
                tmpNclrOut = Path.GetTempFileName();
                nclr.Write(tmpNclrOut);
                byte[] nclrBytes = File.ReadAllBytes(tmpNclrOut);

                tmpNcgrIn = DecompressedTemplatePath(_templateNcgr.Value);
                var ncgr = new NCGR(tmpNcgrIn, _templateNcgr.Value);
                ncgr.Set_Tiles(tiles, img.Width, img.Height, ColorFormat.colors256, TileForm.Horizontal, true);
                tmpNcgrOut = Path.GetTempFileName();
                ncgr.Write(tmpNcgrOut, nclr);
                byte[] ncgrBytes = NSMBe4.ROM.LZ77_Compress(File.ReadAllBytes(tmpNcgrOut));

                var map = new NTFS[cols * rows];
                for (int ty = 0; ty < rows; ty++)
                    for (int tx = 0; tx < cols; tx++)
                        map[ty * cols + tx] = new NTFS { nPalette = 0, xFlip = 0, yFlip = 0, nTile = (ushort)(ty * cols + tx) };

                tmpNscrIn = DecompressedTemplatePath(_templateNscr.Value);
                var nscr = new NSCR(tmpNscrIn, _templateNscr.Value);
                nscr.Set_Map(map, true, img.Width, img.Height);
                tmpNscrOut = Path.GetTempFileName();
                nscr.Write(tmpNscrOut, ncgr, nclr);
                byte[] nscrBytes = NSMBe4.ROM.LZ77_Compress(File.ReadAllBytes(tmpNscrOut));

                newPaletteIdx = _nextFreeIndex++;
                newTilesIdx = _nextFreeIndex++;
                newScreenIdx = _nextFreeIndex++;
                _narc.Put(newPaletteIdx, nclrBytes);
                _narc.Put(newTilesIdx, ncgrBytes);
                _narc.Put(newScreenIdx, nscrBytes);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                newPaletteIdx = newTilesIdx = newScreenIdx = -1;
                return false;
            }
            finally
            {
                foreach (var p in new[] { tmpNcgrIn, tmpNscrIn, tmpNclrOut, tmpNcgrOut, tmpNscrOut })
                    if (p != null) { try { File.Delete(p); } catch { } }
            }
        }
    }
}
