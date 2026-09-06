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
        private static readonly int[] BitsPerPixel = { 0, 8, 2, 4, 8, 2, 8, 16 };

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

        public bool HasSameProfileAs(Btx0Structure other)
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
                if (!HasId(bytes, 0, "BTX0"))
                    throw new FormatException("The file does not have a BTX0 header.");

                int declaredFileSize = ReadInt32(bytes, 8, "BTX file size");
                if (declaredFileSize < 0)
                    throw new FormatException("The declared BTX file size is invalid.");
                if (declaredFileSize != 0 && declaredFileSize > bytes.Length)
                    throw new FormatException("The BTX file is truncated before its declared size.");

                int sectionCount = ReadUInt16(bytes, 14, "BTX section count");
                if (sectionCount < 1)
                    throw new FormatException("The BTX file has no TEX0 section.");

                Require(bytes, 16, checked(sectionCount * 4), "BTX section table");
                int tex0Offset = checked((int)ReadUInt32(bytes, 16, "TEX0 offset"));
                Require(bytes, tex0Offset, 60, "TEX0 header");
                if (!HasId(bytes, tex0Offset, "TEX0"))
                    throw new FormatException("The first BTX section is not TEX0.");

                int sectionSize = ReadInt32(bytes, tex0Offset + 4, "TEX0 size");
                if (sectionSize < 60)
                    throw new FormatException("The TEX0 section size is invalid.");
                Require(bytes, tex0Offset, sectionSize, "TEX0 section");

                int textureDataSize = checked(ReadUInt16(bytes, tex0Offset + 12, "texture data size") * 8);
                int textureDictionaryOffset = ReadUInt16(bytes, tex0Offset + 14, "texture dictionary offset");
                int textureDataOffset = checked((int)ReadUInt32(bytes, tex0Offset + 20, "texture data offset"));
                int compressedDataSize = checked(ReadUInt16(bytes, tex0Offset + 28, "compressed texture data size") * 8);
                int compressedDataOffset = checked((int)ReadUInt32(bytes, tex0Offset + 36, "compressed texture data offset"));
                int paletteDataSize = checked(ReadUInt16(bytes, tex0Offset + 48, "palette data size") * 8);
                int paletteDictionaryOffset = ReadUInt16(bytes, tex0Offset + 52, "palette dictionary offset");
                int paletteDataOffset = checked((int)ReadUInt32(bytes, tex0Offset + 56, "palette data offset"));

                RequireWithinSection(bytes, tex0Offset, sectionSize, textureDataOffset, textureDataSize, "texture data");
                RequireWithinSection(bytes, tex0Offset, sectionSize, compressedDataOffset, compressedDataSize, "compressed texture data");
                RequireWithinSection(bytes, tex0Offset, sectionSize, paletteDataOffset, paletteDataSize, "palette data");

                Btx0TextureEntry[] textures = ReadTextures(
                    bytes, tex0Offset, sectionSize, textureDictionaryOffset,
                    textureDataSize, compressedDataSize);
                Btx0PaletteEntry[] palettes = ReadPalettes(
                    bytes, tex0Offset, sectionSize, paletteDictionaryOffset, paletteDataSize);

                int? sheetWidth = null;
                int? sheetHeight = null;
                if (textures.Length > 0 && textureDataSize > 0)
                {
                    bool allColor16 = true;
                    for (int i = 0; i < textures.Length; i++)
                        allColor16 &= textures[i].Format == 3;

                    int width = textures[0].Width;
                    int pixelCount = checked(textureDataSize * 2);
                    if (allColor16 && width > 0 && pixelCount % width == 0)
                    {
                        sheetWidth = width;
                        sheetHeight = pixelCount / width;
                    }
                }

                structure = new Btx0Structure(
                    textureDataSize, compressedDataSize, paletteDataSize,
                    textures, palettes, sheetWidth, sheetHeight);
                return true;
            }
            catch (Exception ex) when (ex is FormatException || ex is OverflowException || ex is ArgumentOutOfRangeException)
            {
                error = ex.Message;
                return false;
            }
        }

        private static Btx0TextureEntry[] ReadTextures(
            ReadOnlySpan<byte> bytes,
            int tex0Offset,
            int sectionSize,
            int dictionaryOffset,
            int textureDataSize,
            int compressedDataSize)
        {
            int dictionary = checked(tex0Offset + dictionaryOffset);
            RequireWithinSection(bytes, tex0Offset, sectionSize, dictionaryOffset, 4, "texture dictionary");
            int count = bytes[dictionary + 1];
            int entryStart = checked(dictionary + 16 + count * 4);
            int namesStart = checked(entryStart + count * 8);
            int dictionaryLength = checked(16 + count * 28);
            Require(bytes, dictionary, dictionaryLength, "texture dictionary");
            RequireWithinSection(bytes, tex0Offset, sectionSize, dictionaryOffset, dictionaryLength, "texture dictionary");

            var groups = new Dictionary<int, int>();
            var result = new Btx0TextureEntry[count];
            for (int i = 0; i < count; i++)
            {
                int entry = checked(entryStart + i * 8);
                int offset = checked(ReadUInt16(bytes, entry, "texture offset") * 8);
                ushort parameters = ReadUInt16(bytes, entry + 2, "texture parameters");
                uint dictionaryData = ReadUInt32(bytes, entry + 4, "texture dictionary data");
                int format = (parameters >> 10) & 7;
                int width = 8 << ((parameters >> 4) & 7);
                int height = 8 << ((parameters >> 7) & 7);
                int dataLength = checked(width * height * BitsPerPixel[format] / 8);
                int regionSize = format == 5 ? compressedDataSize : textureDataSize;
                if (dataLength <= 0 || offset > regionSize || dataLength > regionSize - offset)
                    throw new FormatException($"Texture {i} points outside its declared data region.");

                if (!groups.TryGetValue(offset, out int offsetGroup))
                {
                    offsetGroup = groups.Count;
                    groups.Add(offset, offsetGroup);
                }

                string name = ReadName(bytes, checked(namesStart + i * 16));
                result[i] = new Btx0TextureEntry(name, width, height, format, dataLength, offsetGroup, parameters, dictionaryData);
            }
            return result;
        }

        private static Btx0PaletteEntry[] ReadPalettes(
            ReadOnlySpan<byte> bytes,
            int tex0Offset,
            int sectionSize,
            int dictionaryOffset,
            int paletteDataSize)
        {
            int dictionary = checked(tex0Offset + dictionaryOffset);
            RequireWithinSection(bytes, tex0Offset, sectionSize, dictionaryOffset, 4, "palette dictionary");
            int count = bytes[dictionary + 1];
            int entryStart = checked(dictionary + 16 + count * 4);
            int namesStart = checked(entryStart + count * 4);
            int dictionaryLength = checked(16 + count * 24);
            Require(bytes, dictionary, dictionaryLength, "palette dictionary");
            RequireWithinSection(bytes, tex0Offset, sectionSize, dictionaryOffset, dictionaryLength, "palette dictionary");

            var offsets = new int[count];
            for (int i = 0; i < count; i++)
            {
                offsets[i] = checked(ReadUInt16(bytes, entryStart + i * 4, "palette offset") * 8);
                if (offsets[i] < 0 || offsets[i] >= paletteDataSize)
                    throw new FormatException($"Palette {i} points outside the declared palette data region.");
            }

            var groups = new Dictionary<int, int>();
            var result = new Btx0PaletteEntry[count];
            for (int i = 0; i < count; i++)
            {
                int next = paletteDataSize;
                for (int j = 0; j < count; j++)
                {
                    if (offsets[j] > offsets[i] && offsets[j] < next)
                        next = offsets[j];
                }
                int byteCapacity = next - offsets[i];
                if ((byteCapacity & 1) != 0)
                    throw new FormatException($"Palette {i} has an odd byte length.");

                if (!groups.TryGetValue(offsets[i], out int offsetGroup))
                {
                    offsetGroup = groups.Count;
                    groups.Add(offsets[i], offsetGroup);
                }

                string name = ReadName(bytes, checked(namesStart + i * 16));
                ushort color0 = ReadUInt16(bytes, entryStart + i * 4 + 2, "palette flags");
                result[i] = new Btx0PaletteEntry(name, byteCapacity / 2, offsetGroup, color0);
            }
            return result;
        }

        private static string ReadName(ReadOnlySpan<byte> bytes, int offset)
        {
            Require(bytes, offset, 16, "dictionary name");
            int length = 0;
            while (length < 16 && bytes[offset + length] != 0)
                length++;
            return Encoding.ASCII.GetString(bytes.Slice(offset, length));
        }

        private static bool HasId(ReadOnlySpan<byte> bytes, int offset, string id)
        {
            Require(bytes, offset, id.Length, id + " identifier");
            for (int i = 0; i < id.Length; i++)
            {
                if (bytes[offset + i] != id[i])
                    return false;
            }
            return true;
        }

        private static ushort ReadUInt16(ReadOnlySpan<byte> bytes, int offset, string field)
        {
            Require(bytes, offset, 2, field);
            return (ushort)(bytes[offset] | bytes[offset + 1] << 8);
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

        private static void RequireWithinSection(
            ReadOnlySpan<byte> bytes,
            int sectionOffset,
            int sectionSize,
            int relativeOffset,
            int length,
            string field)
        {
            if (relativeOffset < 0 || length < 0 || relativeOffset > sectionSize || length > sectionSize - relativeOffset)
                throw new FormatException($"The {field} lies outside the TEX0 section.");
            Require(bytes, checked(sectionOffset + relativeOffset), length, field);
        }

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
