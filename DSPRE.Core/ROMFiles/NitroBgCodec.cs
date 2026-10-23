using System;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The one reader of Nitro palettes, drawings and screens (NCLR, NCGR, NSCR), straight from their bytes, and
    /// the compositor that draws a screen from them. Every editor reads these formats through here, so a fix to
    /// how one is read reaches all of them.
    /// </summary>
    public static class NitroBgCodec
    {
        public sealed class BgImage { public byte[] Rgba; public int Width, Height; }

        public static byte[] Inflate(byte[] b)
        {
            if (b == null) return null;
            // A glyph font also starts with 0x10, but only a compressed file gives a nonzero size after it.
            if (b.Length > 3 && b[0] == 0x10 && (b[1] | b[2] | b[3]) != 0) { try { return Lz77.Decompress(b); } catch (Exception ex) { AppLogger.WarnOnce("NitroBgCodec.Inflate", ex); return b; } }
            return b;
        }

        public static int Find(byte[] d, string magic, int from)
        {
            for (int i = from; i + 4 <= d.Length; i += 4)
                if (d[i] == magic[0] && d[i + 1] == magic[1] && d[i + 2] == magic[2] && d[i + 3] == magic[3]) return i;
            return -1;
        }
        public static int U16(byte[] d, int o) => d[o] | (d[o + 1] << 8);
        public static int U32(byte[] d, int o) => d[o] | (d[o + 1] << 8) | (d[o + 2] << 16) | (d[o + 3] << 24);

        /// <summary>A DS colour as 8-bit channels: each 5-bit channel times eight, the same as PNG export and import use.</summary>
        public static (byte r, byte g, byte b) Colour(int bgr555)
            => ((byte)((bgr555 & 0x1F) << 3), (byte)(((bgr555 >> 5) & 0x1F) << 3), (byte)(((bgr555 >> 10) & 0x1F) << 3));

        /// <summary>A colour as stored, from 8-bit channels: each channel's top five bits.</summary>
        public static ushort ToBgr555(byte r, byte g, byte b) => (ushort)((r >> 3) | ((g >> 3) << 5) | ((b >> 3) << 10));

        // Where a palette file's colours start and how many it holds, or count -1 when it has no palette section.
        private static (int At, int Count) ColourSpan(byte[] nclr)
        {
            int pltt = nclr == null ? -1 : Find(nclr, "TTLP", 0);
            if (pltt < 0) return (0, -1);
            int size = U32(nclr, pltt + 0x10);
            int at = pltt + 0x18;
            // The section's own length is the real bound: what follows it is the PMCP section, not colours, and
            // some Diamond files claim more colours than the section holds.
            int section = pltt + 8 <= nclr.Length ? U32(nclr, pltt + 4) : 0;
            // A declared size of 0 means the whole section, as the trade demo's and some Pokétch palettes store it.
            if (section >= 0x18) size = size == 0 ? section - 0x18 : Math.Min(size, section - 0x18);
            return (at, Math.Max(0, Math.Min(size, nclr.Length - at)) / 2);
        }

        /// <summary>The raw 15-bit colours of a palette file, every row of them.</summary>
        public static ushort[] ReadColours(byte[] nclr)
        {
            (int at, int count) = ColourSpan(nclr);
            if (count <= 0) return Array.Empty<ushort>();
            ushort[] colours = new ushort[count];
            for (int i = 0; i < count; i++) colours[i] = (ushort)U16(nclr, at + i * 2);
            return colours;
        }

        /// <summary>The row size a palette file declares: 256 when its depth field says 256 colours, else 16.</summary>
        public static int DeclaredRowSize(byte[] nclr)
        {
            int pltt = nclr == null ? -1 : Find(nclr, "TTLP", 0);
            return pltt >= 0 && pltt + 10 <= nclr.Length && U16(nclr, pltt + 8) == 4 ? 256 : 16;
        }

        /// <summary>A palette file's colours in the rows it declares, whole rows only; a short file is one short row.</summary>
        public static ushort[][] ReadColourRows(byte[] nclr)
        {
            ushort[] colours = ReadColours(nclr);
            int rowSize = Math.Min(DeclaredRowSize(nclr), colours.Length);
            int rows = rowSize == 0 ? 0 : colours.Length / rowSize;
            ushort[][] outp = new ushort[rows][];
            for (int r = 0; r < rows; r++) outp[r] = colours.AsSpan(r * rowSize, rowSize).ToArray();
            return outp;
        }

        /// <summary>How many colours a palette file holds, or -1 when it has no palette section.</summary>
        public static int ColourCount(byte[] nclr) => ColourSpan(nclr).Count;

        /// <summary>Stores one colour in place, refusing a slot outside the palette section.</summary>
        public static bool PutColour(byte[] nclr, int slot, ushort colour)
        {
            (int at, int count) = ColourSpan(nclr);
            if (slot < 0 || slot >= count) return false;
            nclr[at + slot * 2] = (byte)colour;
            nclr[at + slot * 2 + 1] = (byte)(colour >> 8);
            return true;
        }

        /// <summary>Up to 256 colours of a palette file as 8-bit channels, in an array of at least 256.</summary>
        public static (byte r, byte g, byte b)[] ReadPalette(byte[] pal, out int palCount)
        {
            ushort[] raw = ReadColours(pal);
            palCount = Math.Min(256, raw.Length);
            (byte r, byte g, byte b)[] colors = new (byte r, byte g, byte b)[Math.Max(256, palCount)];
            for (int i = 0; i < palCount; i++) colors[i] = Colour(raw[i]);
            return colors;
        }

        /// <summary>A sixteen-colour drawing's pixels, half a byte each, 32 bytes a tile; empty for a 256-colour one.</summary>
        public static byte[] ReadCharacters(byte[] ncgr)
        {
            if (ncgr == null) return Array.Empty<byte>();
            DrawingHeader? head = ReadDrawingHeader(ncgr);
            if (head?.EightBit == true) return Array.Empty<byte>();
            int at = head?.DataAt ?? 0x30;
            int size = Math.Max(0, Math.Min(head?.DataSize ?? ncgr.Length - at, ncgr.Length - at));
            byte[] pixels = new byte[size];
            Array.Copy(ncgr, at, pixels, 0, size);
            return pixels;
        }

        /// <summary>A screen arrangement's entries, row after row, and how many tiles wide it says it is.</summary>
        public static (int WidthTiles, ushort[] Entries) ReadMap(byte[] nscr)
        {
            if (nscr == null) return (0, Array.Empty<ushort>());
            int nrcs = Find(nscr, "NRCS", 0);
            if (nrcs < 0) return (0, Array.Empty<ushort>());
            int w = U16(nscr, nrcs + 0x08);
            int size = U32(nscr, nrcs + 0x10);
            int at = nrcs + 0x14;
            int entryBytes = EntryBytes(nscr);
            int count = Math.Max(0, Math.Min(size, nscr.Length - at)) / entryBytes;
            ushort[] entries = new ushort[count];
            for (int i = 0; i < count; i++) entries[i] = (ushort)EntryAt(nscr, at, i, entryBytes);
            return (w / 8, entries);
        }

        /// <summary>
        /// A drawing's character section as stored. Tiles across and down are signed, because a drawing that does
        /// not record a size stores 0xFFFF there. A drawing is tiled unless the low byte at +0x14 says its pixels run
        /// row by row, as software sprites do.
        /// </summary>
        public readonly record struct DrawingHeader(int SectionAt, int SectionSize, int TilesDown, int TilesAcross,
                                                    bool EightBit, bool Tiled, int DataAt, int DataSize);

        /// <summary>The character section of a drawing, or null when it has none.</summary>
        public static DrawingHeader? ReadDrawingHeader(byte[] ncgr)
        {
            int rahc = ncgr == null ? -1 : Find(ncgr, "RAHC", 0);
            if (rahc < 0 || rahc + 0x20 > ncgr.Length) return null;
            // The data offset counts from the end of the section's own eight byte header.
            int dataAt = rahc + 8 + U32(ncgr, rahc + 0x1C);
            if (dataAt < rahc + 0x20 || dataAt > ncgr.Length) dataAt = rahc + 0x20;
            int dataSize = U32(ncgr, rahc + 0x18);
            if (dataSize < 0 || dataAt + dataSize > ncgr.Length) dataSize = ncgr.Length - dataAt;
            return new DrawingHeader(rahc, U32(ncgr, rahc + 4), (short)U16(ncgr, rahc + 0x08), (short)U16(ncgr, rahc + 0x0A),
                                     U32(ncgr, rahc + 0x0C) == 4, (U32(ncgr, rahc + 0x14) & 0xFF) == 0, dataAt, dataSize);
        }

        /// <summary>
        /// A drawing's size in pixels: what it records, or when it records none, a square when the pixel count is
        /// one and otherwise rows 256 wide.
        /// </summary>
        public static (int Width, int Height) DrawingSize(DrawingHeader head)
        {
            if (head.TilesAcross > 0 && head.TilesDown > 0) return (head.TilesAcross * 8, head.TilesDown * 8);
            int pixels = head.DataSize * 8 / (head.EightBit ? 8 : 4);
            int side = (int)Math.Sqrt(pixels);
            if (side * side == pixels) return (Math.Max(1, side), Math.Max(1, side));
            int width = Math.Max(1, Math.Min(pixels, 256));
            return (width, Math.Max(1, pixels / width));
        }

        /// <summary>Where a drawing keeps its pixels and how many colours they point at.</summary>
        public static (bool EightBit, int TilesAt) ReadTileHeader(byte[] chr)
        {
            DrawingHeader? head = ReadDrawingHeader(chr);
            return head is DrawingHeader h ? (h.EightBit, h.DataAt) : (false, 0x30);
        }

        /// <summary>How big a background is and where its arrangement starts.</summary>
        public static (int Width, int Height, int MapAt) ReadScreenHeader(byte[] scr)
        {
            // NSCR NRCS: width and height in pixels at +0x08 and +0x0A, the arrangement at +0x14.
            int nrcs = scr == null ? -1 : Find(scr, "NRCS", 0);
            int w = nrcs >= 0 ? U16(scr, nrcs + 0x08) : 256;
            int h = nrcs >= 0 ? U16(scr, nrcs + 0x0A) : 256;
            if (w <= 0 || w > 1024) w = 256;
            if (h <= 0 || h > 1024) h = 256;
            return (w, h, nrcs >= 0 ? nrcs + 0x14 : 0x24);
        }

        /// <summary>
        /// Where a square's entry sits: screens wider than 32 squares are stored in 32 by 32 blocks, narrower ones
        /// straight across. Short arrangements leave the rest empty through the bounds check.
        /// </summary>
        public static int SquareIndex(int cols, int tx, int ty)
        {
            if (cols <= 32) return ty * cols + tx;
            int blocksX = (cols + 31) / 32;
            return ((ty / 32) * blocksX + (tx / 32)) * 1024 + (ty % 32) * 32 + (tx % 32);
        }

        /// <summary>How many entries an arrangement of this size holds.</summary>
        public static int SquareCount(int cols, int rows)
            => cols <= 32 ? cols * rows : ((cols + 31) / 32) * ((rows + 31) / 32) * 1024;

        /// <summary>Bytes per arrangement entry: a plain affine screen (NRCS +0x0E is 1) stores a one-byte tile number.</summary>
        public static int EntryBytes(byte[] scr)
        {
            int nrcs = scr == null ? -1 : Find(scr, "NRCS", 0);
            return nrcs >= 0 && nrcs + 0x10 <= scr.Length && U16(scr, nrcs + 0x0E) == 1 ? 1 : 2;
        }

        /// <summary>The entry for one square, or -1 when the file stops short of it.</summary>
        public static int EntryAt(byte[] scr, int mapAt, int square, int entryBytes)
        {
            int at = mapAt + square * entryBytes;
            if (at < 0 || at + entryBytes > scr.Length) return -1;
            return entryBytes == 1 ? scr[at] : U16(scr, at);
        }

        /// <summary>Stores one square's entry, refusing a square past the file or a value its entry size cannot hold.</summary>
        public static bool PutEntry(byte[] scr, int mapAt, int square, int entryBytes, int entry)
        {
            int at = mapAt + square * entryBytes;
            if (at < 0 || at + entryBytes > scr.Length || entry < 0 || entry >= (1 << (8 * entryBytes))) return false;
            scr[at] = (byte)entry;
            if (entryBytes == 2) scr[at + 1] = (byte)(entry >> 8);
            return true;
        }

        /// <summary>How many whole tiles a drawing file has room for.</summary>
        public static int TileRoom(byte[] chr)
        {
            (bool is8, int at) = ReadTileHeader(chr);
            return chr == null ? 0 : Math.Max(0, chr.Length - at) / (is8 ? 64 : 32);
        }

        private static void ReadNcgrHeader(byte[] chr, out bool is8, out int tileBytes)
        {
            (bool EightBit, int TilesAt) head = ReadTileHeader(chr);
            is8 = head.EightBit;
            tileBytes = head.TilesAt;
        }

        private static void BlitTile(byte[] rgba, int w, byte[] chr, int tileBytes, bool is8,
            (byte r, byte g, byte b)[] colors, int palCount, int tile, int palNo, bool flipH, bool flipV,
            bool transparentZero, int dstTx, int dstTy)
        {
            int tbase = tileBytes + tile * (is8 ? 64 : 32);
            for (int py = 0; py < 8; py++)
                for (int px = 0; px < 8; px++)
                {
                    int sx = flipH ? 7 - px : px, sy = flipV ? 7 - py : py;
                    int idx;
                    if (is8) { int off = tbase + sy * 8 + sx; idx = off < chr.Length ? chr[off] : 0; }
                    else { int off = tbase + (sy * 8 + sx) / 2; int bb = off < chr.Length ? chr[off] : 0; idx = ((sx & 1) == 0) ? (bb & 0xF) : (bb >> 4); }
                    if (transparentZero && idx == 0) continue;
                    int ci = is8 ? idx : palNo * 16 + idx;
                    if (ci >= palCount) ci = idx;
                    if (ci >= colors.Length) continue;
                    (byte r, byte g, byte b) = colors[ci];
                    int dst = ((dstTy * 8 + py) * w + (dstTx * 8 + px)) * 4;
                    rgba[dst] = r; rgba[dst + 1] = g; rgba[dst + 2] = b; rgba[dst + 3] = 255;
                }
        }

        public static BgImage Composite(byte[] chr, byte[] pal, byte[] scr, bool transparentZero = true)
        {
            (byte r, byte g, byte b)[] colors = ReadPalette(pal, out int palCount);
            return Composite(chr, colors, palCount, scr, transparentZero);
        }

        /// <summary>
        /// The same, with the colours already in hand. Some screens are drawn with more than one
        /// palette file loaded over each other, row by row, and only the caller knows which.
        /// </summary>
        public static BgImage Composite(byte[] chr, (byte r, byte g, byte b)[] colors, int palCount, byte[] scr,
                                        bool transparentZero = true)
        {
            ReadNcgrHeader(chr, out bool is8, out int tileBytes);

            (int w, int h, int mapData) = ReadScreenHeader(scr);
            int entryBytes = EntryBytes(scr);

            byte[] rgba = new byte[w * h * 4];
            int cols = w / 8, rows = h / 8;
            for (int ty = 0; ty < rows; ty++)
                for (int tx = 0; tx < cols; tx++)
                {
                    int e = EntryAt(scr, mapData, SquareIndex(cols, tx, ty), entryBytes);
                    if (e < 0) continue;
                    int tile = e & 0x3FF, palNo = (e >> 12) & 0xF;
                    bool flipH = ((e >> 10) & 1) != 0, flipV = ((e >> 11) & 1) != 0;
                    BlitTile(rgba, w, chr, tileBytes, is8, colors, palCount, tile, palNo, flipH, flipV, transparentZero, tx, ty);
                }
            return new BgImage { Rgba = rgba, Width = w, Height = h };
        }
    }
}
