using DSPRE.Editors.Utils;
using DSPRE.HgEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// One battle-sprite archive of fixed-size members: a 160x80 4bpp scrambled sprite (6448 bytes) or a 16-colour
    /// palette (72 bytes). Reads and writes go to the unpacked copy that Save ROM repacks and the Graphics browser
    /// reads, and each write also patches the packed file in place so both agree. An hg-engine-owned archive is
    /// rebuilt from the checkout and skipped by Save ROM, so it is only used unpacked when it already is.
    /// </summary>
    public sealed class PokemonBattleSpriteArchive
    {
        public const int SpriteWidth = 160, SpriteHeight = 80;
        public const int SpriteEntrySize = 6448, PaletteEntrySize = 72;
        private const int Words = SpriteWidth * SpriteHeight / 4;

        private readonly string _packed;
        private readonly string _unpacked;

        private PokemonBattleSpriteArchive(string packed, string unpacked) { _packed = packed; _unpacked = unpacked; }

        public static PokemonBattleSpriteArchive Open(RomInfo.DirNames dir)
        {
            if (RomInfo.gameDirs == null || !RomInfo.gameDirs.TryGetValue(dir, out (string packedDir, string unpackedDir) paths)) return null;
            if (!HgEngineDomains.IsOwned(dir))
                DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { dir });
            string unpacked = Directory.Exists(paths.unpackedDir) && Directory.EnumerateFiles(paths.unpackedDir).Any()
                ? paths.unpackedDir : null;
            string packed = File.Exists(paths.packedDir) ? paths.packedDir : null;
            return unpacked == null && packed == null ? null : new PokemonBattleSpriteArchive(packed, unpacked);
        }

        private string MemberPath(int idx) => Path.Combine(_unpacked, idx.ToString("D4"));

        private byte[] Read(int idx, int size)
        {
            if (idx < 0) return null;
            if (_unpacked != null)
            {
                FileInfo file = new FileInfo(MemberPath(idx));
                return file.Exists && file.Length == size ? File.ReadAllBytes(file.FullName) : null;
            }
            NarcReader narc = new NarcReader(_packed);
            if (idx >= narc.fe.Length || narc.fe[idx].Size != size) return null;
            narc.OpenEntry(idx);
            try
            {
                byte[] buffer = new byte[size];
                narc.fs.ReadExactly(buffer);
                return buffer;
            }
            finally { narc.Close(); }
        }

        // False when the member is missing or not the size of the record being written.
        private bool Write(int idx, byte[] data)
        {
            if (idx < 0) return false;
            bool written = false;
            if (_unpacked != null)
            {
                FileInfo file = new FileInfo(MemberPath(idx));
                if (!file.Exists || file.Length != data.Length) return false;
                File.WriteAllBytes(file.FullName, data);
                written = true;
            }
            if (_packed != null)
            {
                NarcReader narc = new NarcReader(_packed);
                if (idx < narc.fe.Length && narc.fe[idx].Size == data.Length)
                {
                    narc.OpenEntry(idx);
                    try { narc.fs.Write(data, 0, data.Length); }
                    finally { narc.Close(); }
                    written = true;
                }
            }
            return written;
        }

        /// <summary>The sprite as one palette index per pixel, or null when the member is missing.</summary>
        public byte[] ReadSprite(int idx)
        {
            byte[] bytes = Read(idx, SpriteEntrySize);
            return bytes == null ? null : DecodeSprite(bytes);
        }

        /// <summary>16 opaque ARGB colours, or null when the member is missing.</summary>
        public uint[] ReadPalette(int idx)
        {
            byte[] bytes = Read(idx, PaletteEntrySize);
            return bytes == null ? null : DecodePalette(bytes);
        }

        // An unchanged sprite or palette is left as the game has it; a changed one keeps the member's own header,
        // scrambling seed and colour top bits.
        public bool WriteSprite(int idx, byte[] indices)
        {
            byte[] existing = Read(idx, SpriteEntrySize);
            if (existing != null && DecodeSprite(existing).AsSpan().SequenceEqual(indices)) return true;
            return Write(idx, EncodeSprite(indices, existing));
        }

        public bool WritePalette(int idx, uint[] palette)
        {
            byte[] existing = Read(idx, PaletteEntrySize);
            if (existing != null && DecodePalette(existing).AsSpan().SequenceEqual(palette)) return true;
            return Write(idx, EncodePalette(palette, existing));
        }

        public static byte[] DecodeSprite(byte[] entry)
        {
            ushort[] arr = new ushort[Words];
            for (int i = 0; i < Words; i++) arr[i] = (ushort)(entry[48 + i * 2] | (entry[49 + i * 2] << 8));

            unchecked
            {
                if (RomInfo.gameFamily != RomInfo.GameFamilies.DP)
                {
                    uint num = arr[0];
                    for (int j = 0; j < Words; j++) { arr[j] = (ushort)(arr[j] ^ (ushort)(num & 0xFFFF)); num = num * 1103515245 + 24691; }
                }
                else
                {
                    uint num = arr[Words - 1];
                    for (int j = Words - 1; j >= 0; j--) { arr[j] = (ushort)(arr[j] ^ (ushort)(num & 0xFFFF)); num = num * 1103515245 + 24691; }
                }
            }

            byte[] pixels = new byte[SpriteWidth * SpriteHeight];
            for (int k = 0; k < Words; k++)
            {
                pixels[k * 4] = (byte)(arr[k] & 0xF);
                pixels[k * 4 + 1] = (byte)((arr[k] >> 4) & 0xF);
                pixels[k * 4 + 2] = (byte)((arr[k] >> 8) & 0xF);
                pixels[k * 4 + 3] = (byte)((arr[k] >> 12) & 0xF);
            }
            return pixels;
        }

        public static uint[] DecodePalette(byte[] entry)
        {
            uint[] pal = new uint[16];
            for (int j = 0; j < 16; j++)
            {
                ushort v = (ushort)(entry[40 + j * 2] | (entry[41 + j * 2] << 8));
                uint r = (uint)((v & 0x1F) << 3);
                uint g = (uint)(((v >> 5) & 0x1F) << 3);
                uint b = (uint)(((v >> 10) & 0x1F) << 3);
                pal[j] = 0xFF000000u | (r << 16) | (g << 8) | b;
            }
            return pal;
        }

        private static readonly byte[] SpriteHeader =
        {
            82, 71, 67, 78, 255, 254, 0, 1, 48, 25, 0, 0, 16, 0, 1, 0,
            82, 65, 72, 67, 32, 25, 0, 0, 10, 0, 20, 0, 3, 0, 0, 0,
            0, 0, 0, 0, 1, 0, 0, 0, 0, 25, 0, 0, 24, 0, 0, 0,
        };

        private static readonly byte[] PaletteHeader =
        {
            82, 76, 67, 78, 255, 254, 0, 1, 72, 0, 0, 0, 16, 0, 1, 0,
            84, 84, 76, 80, 56, 0, 0, 0, 4, 0, 10, 0, 0, 0, 0, 0,
            32, 0, 0, 0, 16, 0, 0, 0,
        };

        /// <param name="template">The member being replaced, whose header and seed are kept; null for the defaults.</param>
        public static byte[] EncodeSprite(byte[] indices, byte[] template = null)
        {
            bool dp = RomInfo.gameFamily == RomInfo.GameFamilies.DP;
            int seedAt = 48 + (dp ? Words - 1 : 0) * 2;
            ushort? kept = template != null && template.Length == SpriteEntrySize
                ? (ushort)(template[seedAt] | (template[seedAt + 1] << 8)) : null;

            ushort[] packed = new ushort[Words];
            for (int i = 0; i < Words; i++)
                packed[i] = (ushort)((indices[i * 4] & 0xF) | ((indices[i * 4 + 1] & 0xF) << 4) |
                                     ((indices[i * 4 + 2] & 0xF) << 8) | ((indices[i * 4 + 3] & 0xF) << 12));

            // The decoder reads its seed straight back from the first (or, in DP, last) word, so that word is the seed itself.
            unchecked
            {
                if (!dp)
                {
                    uint num = kept ?? 0u;
                    packed[0] = (ushort)num;
                    num = num * 1103515245 + 24691;
                    for (int j = 1; j < Words; j++) { packed[j] = (ushort)(packed[j] ^ (ushort)(num & 0xFFFF)); num = num * 1103515245 + 24691; }
                }
                else
                {
                    uint num = 31315u;
                    for (int k = Words - 1; k >= 0; k--) num += packed[k];
                    if (kept is ushort seed) num = seed;
                    packed[Words - 1] = (ushort)(num & 0xFFFF);
                    num = num * 1103515245 + 24691;
                    for (int k = Words - 2; k >= 0; k--) { packed[k] = (ushort)(packed[k] ^ (ushort)(num & 0xFFFF)); num = num * 1103515245 + 24691; }
                }
            }

            byte[] entry = new byte[SpriteEntrySize];
            if (kept != null) Array.Copy(template, entry, 48);
            else SpriteHeader.CopyTo(entry, 0);
            for (int l = 0; l < Words; l++) { entry[48 + l * 2] = (byte)packed[l]; entry[49 + l * 2] = (byte)(packed[l] >> 8); }
            return entry;
        }

        /// <param name="template">The member being replaced: its header is kept, and so is each colour word that still decodes to the same colour.</param>
        public static byte[] EncodePalette(uint[] palette, byte[] template = null)
        {
            byte[] entry = new byte[PaletteEntrySize];
            bool keep = template != null && template.Length == PaletteEntrySize;
            uint[] was = keep ? DecodePalette(template) : null;
            if (keep) Array.Copy(template, entry, 40);
            else PaletteHeader.CopyTo(entry, 0);
            for (int i = 0; i < 16; i++)
            {
                if (keep && was[i] == palette[i])
                {
                    entry[40 + i * 2] = template[40 + i * 2];
                    entry[41 + i * 2] = template[41 + i * 2];
                    continue;
                }
                byte r = (byte)(palette[i] >> 16), g = (byte)(palette[i] >> 8), b = (byte)palette[i];
                ushort v = (ushort)(((r >> 3) & 0x1F) | (((g >> 3) & 0x1F) << 5) | (((b >> 3) & 0x1F) << 10));
                entry[40 + i * 2] = (byte)v;
                entry[41 + i * 2] = (byte)(v >> 8);
            }
            return entry;
        }
    }
}
