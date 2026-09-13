using System;
using System.Collections.Generic;
using System.IO;

namespace DSPRE.Avalonia.Data
{
    public sealed class SpaTextureInfo
    {
        public int Index, Width, Height, Format;
        public int PaletteColors;
        public bool Color0Transparent;
        public int SharedWith = -1;
        /// <summary>Why this texture cannot be replaced, or null when it can.</summary>
        public string CannotReplace;
    }

    public sealed class SpaTextureImport
    {
        public bool Succeeded => Error == null;
        public string Error;
        /// <summary>The texture as it now draws.</summary>
        public byte[] Rgba;
        public int SourceColors, PaletteColorsUsed;
        public bool Quantized, PaletteKept;
    }

    /// <summary>An SPA particle file edited in place, so bytes it does not model pass through untouched.</summary>
    public sealed class SpaDocument
    {
        private const uint Magic = 0x53504120;
        private readonly byte[] _data;
        private readonly SpaArchive _layout;

        public int EmitterCount => _layout.Records.Count;
        public int TextureCount => _layout.Textures.Count;
        public bool IsModified { get; private set; }

        private SpaDocument(byte[] data, SpaArchive layout) { _data = data; _layout = layout; }

        public static SpaDocument Load(byte[] bytes)
            => TryLoad(bytes, out var doc, out string error) ? doc : throw new InvalidDataException(error);

        /// <summary>Refuses any file whose records and textures do not account exactly for where they sit.</summary>
        public static bool TryLoad(byte[] bytes, out SpaDocument document, out string error)
        {
            document = null;
            if (bytes == null || bytes.Length < 32) { error = "The file is too short to be a particle archive."; return false; }
            if (BitConverter.ToUInt32(bytes, 0) != Magic) { error = "The file is not a particle archive."; return false; }

            var data = (byte[])bytes.Clone();
            SpaArchive a;
            try { a = SpaArchive.Parse(data); }
            catch (Exception ex) { error = "The emitter records run past the end of the file: " + ex.Message; return false; }

            if (a.Records.Count != a.EmitterCountInHeader)
            { error = $"The header lists {a.EmitterCountInHeader} emitters but only {a.Records.Count} could be read."; return false; }
            if (a.EmittersEndAt != a.TextureOffset && !(a.EmitterCountInHeader == 0 && a.TextureCount == 0))
            { error = $"The emitter records end at 0x{a.EmittersEndAt:X} but the textures start at 0x{a.TextureOffset:X}."; return false; }
            if (a.Textures.Count != a.TextureCount)
            { error = $"The header lists {a.TextureCount} textures but only {a.Textures.Count} could be found."; return false; }

            foreach (var t in a.Textures)
            {
                int pos = t.ResourceOffset;
                int texSize = BitConverter.ToInt32(data, pos + 8);
                int palOfs = BitConverter.ToInt32(data, pos + 12), palSize = BitConverter.ToInt32(data, pos + 16);
                if (texSize < 0 || pos + 32L + texSize > data.Length || palSize < 0 || (palSize > 0 && (palOfs < 32 || pos + (long)palOfs + palSize > data.Length)))
                { error = $"Texture at 0x{pos:X} points outside the file."; return false; }
            }

            document = new SpaDocument(data, a);
            error = null;
            return true;
        }

        public byte[] ToBytes() => (byte[])_data.Clone();

        /// <summary>The file as it now reads, for previewing edits.</summary>
        public SpaArchive Parse() => SpaArchive.Parse(_data);

        public bool Has(int emitter, SpaField field)
            => field != null && Location(emitter, field) >= 0;

        public long GetRaw(int emitter, SpaField field)
        {
            int at = RequireLocation(emitter, field);
            return field.Extract(ReadWord(at, field.Size));
        }

        public void SetRaw(int emitter, SpaField field, long raw)
        {
            int at = RequireLocation(emitter, field);
            if (field.DecidesLayout)
                throw new InvalidOperationException($"{field.Name} decides which blocks the record holds and cannot be changed.");
            if (raw < field.MinRaw || raw > field.MaxRaw)
                throw new ArgumentOutOfRangeException(nameof(raw), raw, $"{field.Name} holds {field.MinRaw} to {field.MaxRaw}.");

            uint before = ReadWord(at, field.Size);
            uint after = field.Insert(before, raw);
            if (after == before) return;
            for (int i = 0; i < field.Size; i++) _data[at + i] = (byte)(after >> (8 * i));
            IsModified = true;
        }

        public double GetValue(int emitter, SpaField field) => GetRaw(emitter, field) / field.Divisor;

        public void SetValue(int emitter, SpaField field, double value)
            => SetRaw(emitter, field, (long)Math.Round(value * field.Divisor));

        public SpaTextureInfo GetTextureInfo(int index)
        {
            int pos = TexturePos(index);
            uint param = BitConverter.ToUInt32(_data, pos + 4);
            var info = new SpaTextureInfo
            {
                Index = index,
                Format = (int)(param & 0xF),
                Width = 8 << (int)((param >> 4) & 0xF),
                Height = 8 << (int)((param >> 8) & 0xF),
                Color0Transparent = ((param >> 16) & 1) != 0,
                PaletteColors = BitConverter.ToInt32(_data, pos + 16) / 2,
            };
            if (((param >> 17) & 1) != 0)
            {
                info.SharedWith = (int)((param >> 18) & 0xFF);
                info.CannotReplace = $"This texture reuses the pixels of texture {info.SharedWith}; replace that one instead.";
            }
            else if (info.Format == 5)
                info.CannotReplace = "4x4 compressed textures cannot be re-encoded.";
            else if (info.Format < 1 || info.Format > 7)
                info.CannotReplace = $"Texture format {info.Format} is not one the games draw.";
            else if (info.Format != 7 && info.PaletteColors == 0)
                info.CannotReplace = "This texture has no palette to draw its colours from.";
            return info;
        }

        /// <summary>Replaces a texture's pixels, keeping its size, format and palette length.</summary>
        public SpaTextureImport ReplaceTexture(int index, int width, int height, byte[] rgba)
        {
            var info = GetTextureInfo(index);
            if (info.CannotReplace != null) return new SpaTextureImport { Error = info.CannotReplace };
            if (width != info.Width || height != info.Height)
                return new SpaTextureImport { Error = $"The image is {width}x{height}; this texture is {info.Width}x{info.Height}." };

            int pos = TexturePos(index);
            int texSize = BitConverter.ToInt32(_data, pos + 8);
            int palOfs = BitConverter.ToInt32(_data, pos + 12), palSize = BitConverter.ToInt32(_data, pos + 16);
            var texels = new byte[texSize];
            Array.Copy(_data, pos + 32, texels, 0, texSize);
            var palette = new byte[palSize];
            if (palSize > 0) Array.Copy(_data, pos + palOfs, palette, 0, palSize);

            var enc = SpaTextureEncoder.Encode(info.Format, width, height, info.Color0Transparent, texels, palette, rgba);
            if (enc.Error != null) return new SpaTextureImport { Error = enc.Error };

            bool changed = !texels.AsSpan().SequenceEqual(enc.Texels) || !palette.AsSpan().SequenceEqual(enc.Palette);
            Array.Copy(enc.Texels, 0, _data, pos + 32, texSize);
            if (palSize > 0) Array.Copy(enc.Palette, 0, _data, pos + palOfs, palSize);
            if (changed) IsModified = true;

            return new SpaTextureImport
            {
                Rgba = SpaArchive.DecodeResource(_data, pos, Array.Empty<SpaTexture>()).Rgba,
                SourceColors = enc.SourceColors, PaletteColorsUsed = enc.PaletteColorsUsed,
                Quantized = enc.Quantized, PaletteKept = enc.PaletteKept,
            };
        }

        private int TexturePos(int index)
        {
            if (index < 0 || index >= _layout.Textures.Count) throw new ArgumentOutOfRangeException(nameof(index));
            return _layout.Textures[index].ResourceOffset;
        }

        private int Location(int emitter, SpaField field)
        {
            if (emitter < 0 || emitter >= _layout.Records.Count) throw new ArgumentOutOfRangeException(nameof(emitter));
            int block = _layout.Records[emitter].BlockOffset(field.Block);
            return block < 0 ? -1 : block + field.Offset;
        }

        private int RequireLocation(int emitter, SpaField field)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));
            int at = Location(emitter, field);
            if (at < 0) throw new InvalidOperationException($"Emitter {emitter} has no {field.Block} block.");
            return at;
        }

        private uint ReadWord(int at, int size)
        {
            uint v = 0;
            for (int i = 0; i < size; i++) v |= (uint)_data[at + i] << (8 * i);
            return v;
        }
    }
}
