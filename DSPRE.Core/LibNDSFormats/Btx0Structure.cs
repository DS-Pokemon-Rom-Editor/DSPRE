using System;
using System.Collections.Generic;
using System.Text;

namespace DSPRE.LibNDSFormats
{
    /// <summary>
    /// Immutable structural description of a BTX0/TEX0 file. This describes the storage layout
    /// needed by an overworld graphics profile; it does not decode pixels or mutate parser state.
    /// </summary>
    public sealed class Btx0Structure
    {
        private Btx0Structure(
            int textureDataSize,
            int compressedTextureDataSize,
            int paletteDataSize,
            Btx0TextureEntry[] textures,
            Btx0PaletteEntry[] palettes,
            int? sheetWidth,
            int? sheetHeight)
        {
            TextureDataSize = textureDataSize;
            CompressedTextureDataSize = compressedTextureDataSize;
            PaletteDataSize = paletteDataSize;
            Textures = Array.AsReadOnly(textures);
            Palettes = Array.AsReadOnly(palettes);
            SheetWidth = sheetWidth;
            SheetHeight = sheetHeight;
        }

        public int TextureDataSize { get; }
        public int CompressedTextureDataSize { get; }
        public int PaletteDataSize { get; }
        public IReadOnlyList<Btx0TextureEntry> Textures { get; }
        public IReadOnlyList<Btx0PaletteEntry> Palettes { get; }

        /// <summary>
        /// Width of the flattened image accepted by DSPRE's current 4bpp BTX importer, or null when
        /// this BTX is not a homogeneous COLOR_16 layout that importer can safely write.
        /// </summary>
        public int? SheetWidth { get; }

        /// <summary>Height paired with <see cref="SheetWidth"/>.</summary>
        public int? SheetHeight { get; }

        public int UniqueTextureBlockCount
        {
            get
            {
                int max = -1;
                for (int i = 0; i < Textures.Count; i++)
                    max = Math.Max(max, Textures[i].OffsetGroup);
                return max + 1;
            }
        }

        public bool HasSameProfileAs(Btx0Structure other) => ProfileDifference(other) == null;

        /// <summary>The first way <paramref name="other"/> is laid out differently, said as "its ..." against "this one's", or null.</summary>
        public string ProfileDifference(Btx0Structure other)
        {
            if (other is null) return "it could not be read";
            if (Textures.Count != other.Textures.Count)
                return $"it has {Count(other.Textures.Count, "frame")} and this one has {Textures.Count}";
            for (int i = 0; i < Textures.Count; i++)
            {
                Btx0TextureEntry left = Textures[i];
                Btx0TextureEntry right = other.Textures[i];
                if (left.Width != right.Width || left.Height != right.Height)
                    return $"its frames are {right.Width}×{right.Height} and this one's are {left.Width}×{left.Height}";
                if (left.Name != right.Name)
                    return $"its frame {i + 1} is named {right.Name} and this one's is {left.Name}";
                if (left.Format != right.Format)
                    return "its frames use a different colour format";
            }
            if (Palettes.Count != other.Palettes.Count)
                return $"it has {Count(other.Palettes.Count, "palette")} and this one has {Palettes.Count}";
            for (int i = 0; i < Palettes.Count; i++)
                if (Palettes[i].Name != other.Palettes[i].Name)
                    return $"its palette {i + 1} is named {other.Palettes[i].Name} and this one's is {Palettes[i].Name}";
            return SameLayout(other) ? null : "its pictures are packed differently";
        }

        private static string Count(int n, string noun) => n + " " + noun + (n == 1 ? "" : "s");

        private bool SameLayout(Btx0Structure other)
        {
            if (other is null ||
                TextureDataSize != other.TextureDataSize ||
                CompressedTextureDataSize != other.CompressedTextureDataSize ||
                PaletteDataSize != other.PaletteDataSize ||
                SheetWidth != other.SheetWidth ||
                SheetHeight != other.SheetHeight ||
                Textures.Count != other.Textures.Count ||
                Palettes.Count != other.Palettes.Count)
                return false;

            for (int i = 0; i < Textures.Count; i++)
            {
                Btx0TextureEntry left = Textures[i];
                Btx0TextureEntry right = other.Textures[i];
                if (left.Name != right.Name || left.Width != right.Width || left.Height != right.Height ||
                    left.Format != right.Format || left.OffsetGroup != right.OffsetGroup ||
                    left.DataLength != right.DataLength || left.Parameters != right.Parameters ||
                    left.DictionaryData != right.DictionaryData)
                    return false;
            }

            for (int i = 0; i < Palettes.Count; i++)
            {
                Btx0PaletteEntry left = Palettes[i];
                Btx0PaletteEntry right = other.Palettes[i];
                if (left.Name != right.Name || left.OffsetGroup != right.OffsetGroup ||
                    left.ColorCapacity != right.ColorCapacity || left.Color0 != right.Color0)
                    return false;
            }

            return true;
        }

        public static bool TryInspect(byte[] data, out Btx0Structure structure, out string error)
        {
            structure = null;
            error = null;
            if (data is null)
            {
                error = "BTX data is missing.";
                return false;
            }

            try
            {
                ReadOnlySpan<byte> bytes = data;
                Require(bytes, 0, 16, "BTX header");
                int declaredFileSize = ReadInt32(bytes, 8, "BTX file size");
                if (declaredFileSize < 0)
                    throw new FormatException("The declared BTX file size is invalid.");
                if (declaredFileSize != 0 && declaredFileSize > bytes.Length)
                    throw new FormatException("The BTX file is truncated before its declared size.");

                DSPRE.ROMFiles.Tex0Block block = DSPRE.ROMFiles.Tex0Block.ReadBtx0(data, out string why);
                if (block == null) throw new FormatException(why);

                Btx0TextureEntry[] textures = TextureEntries(block);
                Btx0PaletteEntry[] palettes = PaletteEntries(block);

                int? sheetWidth = null;
                int? sheetHeight = null;
                if (textures.Length > 0 && block.TextureDataSize > 0)
                {
                    bool allColor16 = true;
                    for (int i = 0; i < textures.Length; i++)
                        allColor16 &= textures[i].Format == 3;

                    int width = textures[0].Width;
                    int pixelCount = checked(block.TextureDataSize * 2);
                    if (allColor16 && width > 0 && pixelCount % width == 0)
                    {
                        sheetWidth = width;
                        sheetHeight = pixelCount / width;
                    }
                }

                structure = new Btx0Structure(
                    block.TextureDataSize, block.CompressedDataSize, block.PaletteDataSize,
                    textures, palettes, sheetWidth, sheetHeight);
                return true;
            }
            catch (Exception ex) when (ex is FormatException || ex is OverflowException || ex is ArgumentOutOfRangeException)
            {
                error = ex.Message;
                return false;
            }
        }

        private static Btx0TextureEntry[] TextureEntries(DSPRE.ROMFiles.Tex0Block block)
        {
            Dictionary<int, int> groups = new Dictionary<int, int>();
            Btx0TextureEntry[] result = new Btx0TextureEntry[block.Textures.Count];
            for (int i = 0; i < result.Length; i++)
            {
                DSPRE.ROMFiles.Tex0Block.Texture t = block.Textures[i];
                int regionSize = t.Format == 5 ? block.CompressedDataSize : block.TextureDataSize;
                if (t.DataSize <= 0 || t.RelativeOffset > regionSize || t.DataSize > regionSize - t.RelativeOffset)
                    throw new FormatException($"Texture {i} points outside its declared data region.");

                if (!groups.TryGetValue(t.RelativeOffset, out int offsetGroup))
                {
                    offsetGroup = groups.Count;
                    groups.Add(t.RelativeOffset, offsetGroup);
                }
                result[i] = new Btx0TextureEntry(t.Name, t.Width, t.Height, t.Format, t.DataSize, offsetGroup, t.Parameters, t.DictionaryData);
            }
            return result;
        }

        private static Btx0PaletteEntry[] PaletteEntries(DSPRE.ROMFiles.Tex0Block block)
        {
            Dictionary<int, int> groups = new Dictionary<int, int>();
            Btx0PaletteEntry[] result = new Btx0PaletteEntry[block.Palettes.Count];
            for (int i = 0; i < result.Length; i++)
            {
                DSPRE.ROMFiles.Tex0Block.Palette p = block.Palettes[i];
                if (p.RelativeOffset < 0 || p.RelativeOffset >= block.PaletteDataSize)
                    throw new FormatException($"Palette {i} points outside the declared palette data region.");
                if ((p.DataSize & 1) != 0)
                    throw new FormatException($"Palette {i} has an odd byte length.");

                if (!groups.TryGetValue(p.RelativeOffset, out int offsetGroup))
                {
                    offsetGroup = groups.Count;
                    groups.Add(p.RelativeOffset, offsetGroup);
                }
                result[i] = new Btx0PaletteEntry(p.Name, p.DataSize / 2, offsetGroup, p.Flags);
            }
            return result;
        }

        private static uint ReadUInt32(ReadOnlySpan<byte> bytes, int offset, string field)
        {
            Require(bytes, offset, 4, field);
            return (uint)(bytes[offset] |
                bytes[offset + 1] << 8 |
                bytes[offset + 2] << 16 |
                bytes[offset + 3] << 24);
        }

        private static int ReadInt32(ReadOnlySpan<byte> bytes, int offset, string field) =>
            unchecked((int)ReadUInt32(bytes, offset, field));

        private static void Require(ReadOnlySpan<byte> bytes, int offset, int length, string field)
        {
            if (offset < 0 || length < 0 || offset > bytes.Length || length > bytes.Length - offset)
                throw new FormatException($"The BTX file is truncated while reading the {field}.");
        }
    }

    public sealed class Btx0TextureEntry
    {
        internal Btx0TextureEntry(string name, int width, int height, int format, int dataLength, int offsetGroup, ushort parameters, uint dictionaryData)
        {
            Name = name;
            Width = width;
            Height = height;
            Format = format;
            DataLength = dataLength;
            OffsetGroup = offsetGroup;
            Parameters = parameters;
            DictionaryData = dictionaryData;
        }

        public string Name { get; }
        public int Width { get; }
        public int Height { get; }
        public int Format { get; }
        public int DataLength { get; }
        public int OffsetGroup { get; }
        public ushort Parameters { get; }
        public uint DictionaryData { get; }
    }

    public sealed class Btx0PaletteEntry
    {
        internal Btx0PaletteEntry(string name, int colorCapacity, int offsetGroup, ushort color0)
        {
            Name = name;
            ColorCapacity = colorCapacity;
            OffsetGroup = offsetGroup;
            Color0 = color0;
        }

        public string Name { get; }
        public int ColorCapacity { get; }
        public int OffsetGroup { get; }
        public ushort Color0 { get; }
    }
}
