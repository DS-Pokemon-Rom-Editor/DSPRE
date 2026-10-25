using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Gives HGSS room for 15 fonts instead of 6. The font tables move to the synthetic overlay, and added fonts load at boot and
    /// stay loaded, like the system, dialogue and Unown fonts, so every screen can use them.
    /// </summary>
    public static class ExtraFontsPatch
    {
        public const string Marker = "EXTRAFONTSV1";

        public const int GameFontCount = 6, FontCount = 15;
        public static int Capacity => FontCount - GameFontCount;

        // Assembled from extra_fonts.s: FontID_Alloc(3, 3) as before, then FontID_Alloc(i, 3) for each added font.
        private static readonly ushort[] Loader =
        {
            0xB570, 0x2003, 0x2103, 0x4E07, 0x47B0, 0x4D07, 0x682D, 0x3506,
            0x2406, 0xE003, 0x1C20, 0x2103, 0x47B0, 0x3401, 0x42AC, 0xDBF9,
            0xBD70, 0x46C0,
        };
        private const int AllocTarget = 0x24, CountAddress = 0x28, CountAt = 0x2C, ArcsAt = 0x30, ArcRow = 4, InfosAt = ArcsAt + FontCount * ArcRow,
                          InfoRow = 8, WorkAt = InfosAt + (FontCount + 1) * InfoRow;

        // FontWork: a 0x84-byte glyph buffer, then extraData, fontDataMan and fontDataRefCount, one entry per font.
        private const int GlyphBuffer = 0x84, WorkLength = (GlyphBuffer + FontCount * 9 + 3) & ~3;
        private const int GameManagers = GlyphBuffer + GameFontCount * 4, GameCounts = GlyphBuffer + GameFontCount * 8;
        private const int Managers = GlyphBuffer + FontCount * 4, Counts = GlyphBuffer + FontCount * 8;

        private static int CodeStart => SyntheticOverlaySpace.HeaderSize;
        public static int BlockLength => CodeStart + WorkAt + WorkLength;

        public static bool SupportsCurrentRom => RomLocator.ExtraFontsSites != null;

        private static RomInfo.ExtraFontsSites Sites => RomLocator.ExtraFontsSites;

        private static int At(uint address) => (int)(address - ARM9.Address);

        private static ushort Half(byte[] arm9, uint address) => BitConverter.ToUInt16(arm9, At(address));

        private static uint Word(byte[] arm9, uint address) => BitConverter.ToUInt32(arm9, At(address));

        private static long BlockOffset(uint code) => (long)code - RomInfo.SynthOverlayLoadAddress - CodeStart;

        /// <summary>The block's code address when the boot call goes to it and its block is there, or 0.</summary>
        private static uint PatchedCode(byte[] arm9)
        {
            if (!SupportsCurrentRom || !File.Exists(Filesystem.ExpArmPath) || SummaryFontPatch.CallAt(arm9, Sites.BootAlloc) is not uint code) return 0;
            long block = BlockOffset(code);
            if (block < 0 || block + BlockLength > new FileInfo(Filesystem.ExpArmPath).Length) return 0;
            if (Word(arm9, Sites.Work) != code + WorkAt) return 0;
            return Encoding.ASCII.GetString(DSUtils.ReadFromFile(Filesystem.ExpArmPath, (uint)block, Marker.Length)) == Marker ? code : 0;
        }

        // Keyed by the files' write times, since the pickers ask on every redraw.
        private static (string Arm9, DateTime Arm9Time, DateTime OverlayTime, uint Code) _seen;

        private static uint PatchedCode()
        {
            try
            {
                if (!File.Exists(RomInfo.Arm9Path)) return 0;
                DateTime arm9Time = File.GetLastWriteTimeUtc(RomInfo.Arm9Path);
                DateTime overlayTime = File.Exists(Filesystem.ExpArmPath) ? File.GetLastWriteTimeUtc(Filesystem.ExpArmPath) : default;
                if (_seen.Arm9 == RomInfo.Arm9Path && _seen.Arm9Time == arm9Time && _seen.OverlayTime == overlayTime) return _seen.Code;
                uint code = PatchedCode(File.ReadAllBytes(RomInfo.Arm9Path));
                _seen = (RomInfo.Arm9Path, arm9Time, overlayTime, code);
                return code;
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException) { return 0; }
        }

        public static bool IsApplied => PatchedCode() != 0;

        // The game's archive and metrics tables and FontID_Alloc; null when something else changed the module.
        private static (uint Arcs, uint Infos, uint Alloc)? Vanilla(byte[] arm9)
        {
            RomInfo.ExtraFontsSites s = Sites;
            bool Immediate(uint site, int op, int value) => Half(arm9, site) >> 11 == op && (Half(arm9, site) & 0xFF) == value;
            const int Adds = 6, Movs = 4, Cmp = 5;
            if (!s.Managers.All(a => Immediate(a, Adds, GameManagers) || Immediate(a, Movs, GameManagers))) return null;
            if (!s.Counts.All(a => Immediate(a, Adds, GameCounts) || Immediate(a, Movs, GameCounts))) return null;
            if (!s.Limits.All(a => Immediate(a, Cmp, GameFontCount))) return null;
            List<uint> arcs = s.Arcs.Select(a => Word(arm9, a)).Distinct().ToList(), infos = s.Infos.Select(a => Word(arm9, a)).Distinct().ToList();
            if (arcs.Count != 1 || infos.Count != 1 || Word(arm9, s.ArcFlags) != arcs[0] + 2) return null;
            if (arcs[0] < ARM9.Address || At(arcs[0]) + GameFontCount * ArcRow > arm9.Length) return null;
            if (infos[0] < ARM9.Address || At(infos[0]) + (GameFontCount + 1) * InfoRow > arm9.Length) return null;
            if (SummaryFontPatch.CallAt(arm9, s.BootAlloc) is not uint alloc || Half(arm9, s.BootAlloc - 4) != 0x2003) return null;
            return (arcs[0], infos[0], alloc);
        }

        /// <summary>Why the patch can't go in now, or null.</summary>
        public static string WhyNotApplicable()
        {
            if (!SupportsCurrentRom) return "This patch doesn't support this ROM yet.";
            if (!SyntheticOverlaySpace.Available()) return "Apply the ARM9 expansion first.";
            byte[] arm9 = File.ReadAllBytes(RomInfo.Arm9Path);
            if (PatchedCode(arm9) != 0) return "The patch is already applied.";
            return Vanilla(arm9) == null ? "Something else has already changed the font code, so DSPRE leaves it alone." : null;
        }

        /// <summary>Writes the loader and the moved tables into a marked block at <paramref name="blockOffset"/>, then points the font code at them.</summary>
        public static void Apply(int blockOffset)
        {
            if (blockOffset % 4 != 0) throw new ArgumentException("The block must start on a 4-byte boundary.", nameof(blockOffset));
            byte[] arm9 = File.ReadAllBytes(RomInfo.Arm9Path);
            (uint arcs, uint infos, uint alloc) = Vanilla(arm9)
                ?? throw new InvalidOperationException("Something else has already changed the font code, so DSPRE leaves it alone.");

            uint code = RomInfo.SynthOverlayLoadAddress + (uint)blockOffset + (uint)CodeStart;
            byte[] block = new byte[BlockLength];
            SyntheticOverlaySpace.Header(Marker, BlockLength).CopyTo(block, 0);
            for (int i = 0; i < Loader.Length; i++) BitConverter.GetBytes(Loader[i]).CopyTo(block, CodeStart + i * 2);
            BitConverter.GetBytes(alloc | 1).CopyTo(block, CodeStart + AllocTarget);
            BitConverter.GetBytes(code + CountAt).CopyTo(block, CodeStart + CountAddress);
            // The rows past the game's six copy font 0 until a font is added, so a stray font number still prints.
            for (int i = 0; i < FontCount; i++)
            {
                int from = i < GameFontCount ? i : 0;
                Array.Copy(arm9, At(arcs) + from * ArcRow, block, CodeStart + ArcsAt + i * ArcRow, ArcRow);
                Array.Copy(arm9, At(infos) + from * InfoRow, block, CodeStart + InfosAt + i * InfoRow, InfoRow);
            }
            Array.Copy(arm9, At(infos) + GameFontCount * InfoRow, block, CodeStart + InfosAt + FontCount * InfoRow, InfoRow);
            DSUtils.WriteToFile(Filesystem.ExpArmPath, block, (uint)blockOffset);

            RomInfo.ExtraFontsSites s = Sites;
            void Literal(uint site, uint value) => DSUtils.WriteToFile(RomInfo.Arm9Path, BitConverter.GetBytes(value), (uint)At(site));
            void Immediate(uint site, int value) =>
                DSUtils.WriteToFile(RomInfo.Arm9Path, BitConverter.GetBytes((ushort)(Half(arm9, site) & 0xFF00 | value)), (uint)At(site));
            Literal(s.Work, code + WorkAt);
            foreach (uint site in s.Arcs) Literal(site, code + ArcsAt);
            Literal(s.ArcFlags, code + ArcsAt + 2);
            foreach (uint site in s.Infos) Literal(site, code + InfosAt);
            foreach (uint site in s.Managers) Immediate(site, Managers);
            foreach (uint site in s.Counts) Immediate(site, Counts);
            foreach (uint site in s.Limits) Immediate(site, FontCount);
            DSUtils.WriteToFile(RomInfo.Arm9Path, SummaryFontPatch.Bl(s.BootAlloc, code), (uint)At(s.BootAlloc));
        }

        private static long Offset(uint code, int at) => BlockOffset(code) + CodeStart + at;

        /// <summary>The archive entry of each added font, in font number order from 6; empty when the patch is not in.</summary>
        public static int[] AddedEntries()
        {
            uint code = PatchedCode();
            if (code == 0) return Array.Empty<int>();
            byte[] read = DSUtils.ReadFromFile(Filesystem.ExpArmPath, (uint)Offset(code, CountAt), 4 + FontCount * ArcRow);
            int count = Math.Clamp(BitConverter.ToInt32(read, 0), 0, Capacity);
            return Enumerable.Range(GameFontCount, count).Select(n => (int)BitConverter.ToUInt16(read, 4 + n * ArcRow)).ToArray();
        }

        /// <summary>Makes archive entry <paramref name="entry"/> the next font number, with font <paramref name="likeFont"/>'s metrics.</summary>
        public static int Add(int entry, int likeFont)
        {
            uint code = PatchedCode();
            if (code == 0) throw new InvalidOperationException("The extra fonts patch isn't applied.");
            int number = GameFontCount + AddedEntries().Length;
            if (number >= FontCount) throw new InvalidOperationException($"This ROM already has all {FontCount} fonts.");
            if (likeFont < 0 || likeFont >= number) throw new ArgumentOutOfRangeException(nameof(likeFont));

            byte[] row = DSUtils.ReadFromFile(Filesystem.ExpArmPath, (uint)Offset(code, ArcsAt + likeFont * ArcRow), ArcRow);
            BitConverter.GetBytes((ushort)entry).CopyTo(row, 0);
            DSUtils.WriteToFile(Filesystem.ExpArmPath, row, (uint)Offset(code, ArcsAt + number * ArcRow));
            byte[] info = DSUtils.ReadFromFile(Filesystem.ExpArmPath, (uint)Offset(code, InfosAt + likeFont * InfoRow), InfoRow);
            DSUtils.WriteToFile(Filesystem.ExpArmPath, info, (uint)Offset(code, InfosAt + number * InfoRow));
            DSUtils.WriteToFile(Filesystem.ExpArmPath, BitConverter.GetBytes(number + 1 - GameFontCount), (uint)Offset(code, CountAt));
            return number;
        }
    }
}
