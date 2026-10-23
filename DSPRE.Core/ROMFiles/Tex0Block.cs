using System;
using System.Collections.Generic;
using System.Text;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// A TEX0 block, the textures and palettes of a texture file (BTX0) or of a model (BMD0). It reads where everything is
    /// and leaves the pixels where they are; offsets are absolute in the bytes it was read from.
    /// </summary>
    public sealed class Tex0Block
    {
        private static readonly int[] BitsPerPixel = { 0, 8, 2, 4, 8, 2, 8, 16 };

        public sealed class Texture
        {
            public string Name;
            public ushort Parameters;
            public uint DictionaryData;
            public int Format, Width, Height;
            /// <summary>Colour 0 is see-through.</summary>
            public bool Color0;
            /// <summary>The offset the dictionary stores, in bytes from the start of its data region.</summary>
            public int RelativeOffset;
            public int DataAt, DataSize;
            /// <summary>For a 4x4-compressed texture, where its palette index words start; -1 otherwise.</summary>
            public int IndexAt = -1, IndexSize;
        }

        public sealed class Palette
        {
            public string Name;
            /// <summary>The second halfword of the palette's dictionary entry.</summary>
            public ushort Flags;
            public int RelativeOffset;
            public int DataAt;
            /// <summary>Up to the next palette's start, or the end of the palette data for the last.</summary>
            public int DataSize;
        }

        public int BlockAt { get; private set; }
        public int BlockSize { get; private set; }
        public int TextureDictionaryAt { get; private set; }
        public int TextureDataAt { get; private set; }
        public int TextureDataSize { get; private set; }
        public int CompressedDataAt { get; private set; }
        public int CompressedDataSize { get; private set; }
        public int CompressedIndexAt { get; private set; }
        public int PaletteDictionaryAt { get; private set; }
        public int PaletteDataAt { get; private set; }
        public int PaletteDataSize { get; private set; }

        public List<Texture> Textures { get; } = new List<Texture>();
        public List<Palette> Palettes { get; } = new List<Palette>();

        /// <summary>The TEX0 block of a BTX0 file, or null with a reason.</summary>
        public static Tex0Block ReadBtx0(byte[] d, out string error)
        {
            error = null;
            if (d == null || d.Length < 0x14 || Encoding.ASCII.GetString(d, 0, 4) != "BTX0") { error = "The file doesn't have a BTX0 header."; return null; }
            if (U16(d, 0x0E) < 1) { error = "The BTX file has no TEX0 section."; return null; }
            return Read(d, (int)U32(d, 0x10), out error);
        }

        /// <summary>The TEX0 block at <paramref name="at"/>, or null with a reason when it isn't one or doesn't fit.</summary>
        public static Tex0Block Read(byte[] d, int at, out string error)
        {
            error = null;
            try
            {
                if (at < 0 || at + 0x3C > d.Length || Encoding.ASCII.GetString(d, at, 4) != "TEX0") throw new FormatException("There is no TEX0 block there.");
                Tex0Block b = new Tex0Block { BlockAt = at, BlockSize = (int)U32(d, at + 4) };
                if (b.BlockSize < 0x3C || at + b.BlockSize > d.Length) throw new FormatException("The TEX0 block runs past the end of the file.");
                b.TextureDataSize = U16(d, at + 0x0C) * 8;
                b.TextureDictionaryAt = at + U16(d, at + 0x0E);
                b.TextureDataAt = at + (int)U32(d, at + 0x14);
                b.CompressedDataSize = U16(d, at + 0x1C) * 8;
                b.CompressedDataAt = at + (int)U32(d, at + 0x24);
                b.CompressedIndexAt = at + (int)U32(d, at + 0x28);
                b.PaletteDataSize = U16(d, at + 0x30) * 8;
                b.PaletteDictionaryAt = at + (int)U32(d, at + 0x34);
                b.PaletteDataAt = at + (int)U32(d, at + 0x38);
                int end = at + b.BlockSize;
                Within(b.TextureDataAt, b.TextureDataSize, at, end, "texture data");
                Within(b.CompressedDataAt, b.CompressedDataSize, at, end, "compressed texture data");
                Within(b.PaletteDataAt, b.PaletteDataSize, at, end, "palette data");

                int textures = Count(d, b.TextureDictionaryAt, 8, at, end, "texture dictionary");
                int entries = b.TextureDictionaryAt + 16 + textures * 4;
                for (int i = 0; i < textures; i++)
                {
                    int e = entries + i * 8;
                    ushort p = (ushort)U16(d, e + 2);
                    Texture t = new Texture
                    {
                        Name = Name(d, entries + textures * 8 + i * 16),
                        Parameters = p,
                        DictionaryData = U32(d, e + 4),
                        Format = (p >> 10) & 7,
                        Width = 8 << ((p >> 4) & 7),
                        Height = 8 << ((p >> 7) & 7),
                        Color0 = ((p >> 13) & 1) != 0,
                        RelativeOffset = U16(d, e) * 8,
                    };
                    t.DataSize = t.Width * t.Height * BitsPerPixel[t.Format] / 8;
                    t.DataAt = (t.Format == 5 ? b.CompressedDataAt : b.TextureDataAt) + t.RelativeOffset;
                    if (t.Format == 5)
                    {
                        t.IndexAt = b.CompressedIndexAt + t.RelativeOffset / 2;
                        t.IndexSize = t.DataSize / 2;
                    }
                    b.Textures.Add(t);
                }

                int palettes = Count(d, b.PaletteDictionaryAt, 4, at, end, "palette dictionary");
                entries = b.PaletteDictionaryAt + 16 + palettes * 4;
                for (int i = 0; i < palettes; i++)
                {
                    int e = entries + i * 4;
                    b.Palettes.Add(new Palette
                    {
                        Name = Name(d, entries + palettes * 4 + i * 16),
                        Flags = (ushort)U16(d, e + 2),
                        RelativeOffset = U16(d, e) * 8,
                    });
                }
                foreach (Palette p in b.Palettes)
                {
                    int next = b.PaletteDataSize;
                    foreach (Palette q in b.Palettes)
                        if (q.RelativeOffset > p.RelativeOffset && q.RelativeOffset < next) next = q.RelativeOffset;
                    p.DataAt = b.PaletteDataAt + p.RelativeOffset;
                    p.DataSize = Math.Max(0, next - p.RelativeOffset);
                }
                return b;
            }
            catch (Exception ex) when (ex is FormatException || ex is ArgumentException || ex is IndexOutOfRangeException)
            {
                error = ex is FormatException ? ex.Message : "The TEX0 block is cut short.";
                return null;
            }
        }

        // A dictionary: a dummy byte, the count, its size, a 12-byte tree header, a 4-byte tree node per entry, then the entries
        // (each entrySize bytes) and a 16-byte name per entry.
        private static int Count(byte[] d, int at, int entrySize, int blockAt, int blockEnd, string what)
        {
            Within(at, 4, blockAt, blockEnd, what);
            int count = d[at + 1];
            Within(at, 16 + count * (4 + entrySize + 16), blockAt, blockEnd, what);
            return count;
        }

        private static void Within(int at, int length, int blockAt, int blockEnd, string what)
        {
            if (at < blockAt || length < 0 || at + length > blockEnd) throw new FormatException($"The {what} lies outside the TEX0 block.");
        }

        private static string Name(byte[] d, int at)
        {
            int n = 0;
            while (n < 16 && d[at + n] != 0) n++;
            return Encoding.ASCII.GetString(d, at, n);
        }

        private static int U16(byte[] d, int o) => d[o] | d[o + 1] << 8;
        private static uint U32(byte[] d, int o) => (uint)(d[o] | d[o + 1] << 8 | d[o + 2] << 16 | d[o + 3] << 24);
    }
}
