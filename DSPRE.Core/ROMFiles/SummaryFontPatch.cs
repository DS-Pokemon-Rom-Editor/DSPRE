using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Lets each HGSS summary screen text box print in any of the game's six fonts. The screen's prints and width measurements go
    /// through a routine in the synthetic overlay that looks the box up in a table of fonts.
    /// </summary>
    public static class SummaryFontPatch
    {
        public const string Marker = "SUMFONTSV1\0\0";

        // Assembled from summary_fonts.s.
        private static readonly byte[] Code =
        {
            0xf0, 0xb4, 0x06, 0x1c, 0x75, 0x68, 0xb6, 0x68, 0x76, 0x00, 0x76, 0x08,
            0xb4, 0x46, 0x57, 0x4c, 0x26, 0x68, 0x00, 0x2e, 0x08, 0xd0, 0xae, 0x42,
            0x04, 0xd1, 0x66, 0x68, 0x66, 0x45, 0x01, 0xd1, 0xa1, 0x68, 0x01, 0xe0,
            0x0c, 0x34, 0xf3, 0xe7, 0x4b, 0x4c, 0x03, 0x94, 0x70, 0xbc, 0x00, 0xbd,
            0xf0, 0xb4, 0x75, 0x68, 0xb6, 0x68, 0x76, 0x00, 0x76, 0x08, 0xb4, 0x46,
            0x4b, 0x4c, 0x26, 0x68, 0x00, 0x2e, 0x08, 0xd0, 0xae, 0x42, 0x04, 0xd1,
            0x66, 0x68, 0x66, 0x45, 0x01, 0xd1, 0xa0, 0x68, 0x01, 0xe0, 0x0c, 0x34,
            0xf3, 0xe7, 0x41, 0x4c, 0x03, 0x94, 0x70, 0xbc, 0x00, 0xbd, 0xf0, 0xb4,
            0xee, 0x19, 0x75, 0x68, 0xb6, 0x68, 0x76, 0x00, 0x76, 0x08, 0xb4, 0x46,
            0x3f, 0x4c, 0x26, 0x68, 0x00, 0x2e, 0x08, 0xd0, 0xae, 0x42, 0x04, 0xd1,
            0x66, 0x68, 0x66, 0x45, 0x01, 0xd1, 0xa0, 0x68, 0x01, 0xe0, 0x0c, 0x34,
            0xf3, 0xe7, 0x35, 0x4c, 0x03, 0x94, 0x70, 0xbc, 0x00, 0xbd, 0xf0, 0xb4,
            0xf6, 0x19, 0x75, 0x68, 0xb6, 0x68, 0x76, 0x00, 0x76, 0x08, 0xb4, 0x46,
            0x33, 0x4c, 0x26, 0x68, 0x00, 0x2e, 0x08, 0xd0, 0xae, 0x42, 0x04, 0xd1,
            0x66, 0x68, 0x66, 0x45, 0x01, 0xd1, 0xa0, 0x68, 0x01, 0xe0, 0x0c, 0x34,
            0xf3, 0xe7, 0x29, 0x4c, 0x03, 0x94, 0x70, 0xbc, 0x00, 0xbd, 0xf0, 0xb4,
            0x89, 0x26, 0xb6, 0x00, 0xa6, 0x59, 0x10, 0x36, 0x75, 0x68, 0xb6, 0x68,
            0x76, 0x00, 0x76, 0x08, 0xb4, 0x46, 0x26, 0x4c, 0x26, 0x68, 0x00, 0x2e,
            0x08, 0xd0, 0xae, 0x42, 0x04, 0xd1, 0x66, 0x68, 0x66, 0x45, 0x01, 0xd1,
            0xa0, 0x68, 0x01, 0xe0, 0x0c, 0x34, 0xf3, 0xe7, 0x1c, 0x4c, 0x03, 0x94,
            0x70, 0xbc, 0x00, 0xbd, 0xf0, 0xb4, 0x89, 0x26, 0xb6, 0x00, 0xa6, 0x59,
            0x20, 0x36, 0x75, 0x68, 0xb6, 0x68, 0x76, 0x00, 0x76, 0x08, 0xb4, 0x46,
            0x18, 0x4c, 0x26, 0x68, 0x00, 0x2e, 0x08, 0xd0, 0xae, 0x42, 0x04, 0xd1,
            0x66, 0x68, 0x66, 0x45, 0x01, 0xd1, 0xa0, 0x68, 0x01, 0xe0, 0x0c, 0x34,
            0xf3, 0xe7, 0x0f, 0x4c, 0x03, 0x94, 0x70, 0xbc, 0x00, 0xbd, 0x10, 0xb5,
            0x0c, 0x1c, 0x0d, 0x4b, 0x98, 0x47, 0x02, 0x20, 0x21, 0x1c, 0x0b, 0x4b,
            0x98, 0x47, 0x05, 0x20, 0x21, 0x1c, 0x09, 0x4b, 0x98, 0x47, 0x10, 0xbd,
            0x08, 0xb5, 0x08, 0x4b, 0x98, 0x47, 0x02, 0x20, 0x06, 0x4b, 0x98, 0x47,
            0x05, 0x20, 0x05, 0x4b, 0x98, 0x47, 0x08, 0xbd, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        };
        private const int PrintAt = 0x00, WidthR6At = 0x30, WidthR5R7At = 0x5E, WidthR6R7At = 0x8E, CentreNameAt = 0xBE,
                          CentreTrainerAt = 0xF4, LoadAt = 0x12A, ReleaseAt = 0x144;
        private const int PrintTarget = 0x158, WidthTarget = 0x15C, CentreTarget = 0x160, AllocTarget = 0x164, ReleaseTarget = 0x168,
                          TableAddress = 0x16C, TableAt = 0x170;
        private const int EntrySize = 12;

        /// <summary>Room for every window the summary screen has.</summary>
        public static int Capacity => RomLocator.SummaryScreenSites?.Windows.Sum(t => t.Count) ?? 0;
        private static int CodeStart => SyntheticOverlaySpace.HeaderSize;
        public static int BlockLength => CodeStart + TableAt + Capacity * EntrySize + 4;

        /// <summary>The game's fonts by number: the archive entry each loads and what DSPRE calls it.</summary>
        public static readonly (int Entry, string Name)[] Fonts =
        {
            (0, "System font"), (1, "Dialogue font"), (2, "Touch screen font"), (3, "Unown font"), (4, "Button font"), (10, "Pokéwalker font"),
        };

        public static bool SupportsCurrentRom => RomLocator.SummaryFontSites != null && RomLocator.SummaryScreenSites != null;

        private static RomInfo.SummaryFontSites Sites => RomLocator.SummaryFontSites;

        private static IEnumerable<(uint Site, int Entry)> Routes(RomInfo.SummaryFontSites s) =>
            s.Prints.Select(a => (a, PrintAt))
             .Concat(s.WidthR6.Select(a => (a, WidthR6At)))
             .Concat(s.WidthR5R7.Select(a => (a, WidthR5R7At)))
             .Concat(s.WidthR6R7.Select(a => (a, WidthR6R7At)))
             .Append((s.CentreName, CentreNameAt))
             .Concat(s.CentreTrainer.Select(a => (a, CentreTrainerAt)))
             .Append((s.Load, LoadAt))
             .Append((s.Release, ReleaseAt));

        private static int At(uint address) => (int)(address - ARM9.Address);

        // The target of the BL at address, or null when there is none.
        private static uint? CallAt(byte[] arm9, uint address)
        {
            int at = At(address);
            if (at < 0 || at + 4 > arm9.Length) return null;
            ushort hi = BitConverter.ToUInt16(arm9, at), lo = BitConverter.ToUInt16(arm9, at + 2);
            if ((hi & 0xF800) != 0xF000 || (lo & 0xF800) != 0xF800) return null;
            int off = ((hi & 0x7FF) << 12) | ((lo & 0x7FF) << 1);
            if ((off & 0x400000) != 0) off -= 0x800000;
            return (uint)(address + 4 + off);
        }

        private static byte[] Bl(uint from, uint to)
        {
            int off = (int)(to - (from + 4));
            if (off < -0x400000 || off >= 0x400000) throw new InvalidOperationException("The synthetic overlay is out of the summary screen's reach.");
            ushort hi = (ushort)(0xF000 | ((off >> 12) & 0x7FF)), lo = (ushort)(0xF800 | ((off >> 1) & 0x7FF));
            return BitConverter.GetBytes(hi).Concat(BitConverter.GetBytes(lo)).ToArray();
        }

        private static long BlockOffset(uint code) => (long)code - RomInfo.SynthOverlayLoadAddress - CodeStart;

        /// <summary>The routine's address when every site calls it and its block is there, or 0.</summary>
        private static uint PatchedCode(byte[] arm9)
        {
            if (!SupportsCurrentRom || !File.Exists(Filesystem.ExpArmPath) || CallAt(arm9, Sites.Prints[0]) is not uint print) return 0;
            uint code = print - PrintAt;
            if (Routes(Sites).Any(r => CallAt(arm9, r.Site) != code + (uint)r.Entry)) return 0;
            long block = BlockOffset(code);
            if (block < 0 || block + BlockLength > new FileInfo(Filesystem.ExpArmPath).Length) return 0;
            return Encoding.ASCII.GetString(DSUtils.ReadFromFile(Filesystem.ExpArmPath, (uint)block, Marker.Length)) == Marker ? code : 0;
        }

        private static uint PatchedCode()
        {
            try { return File.Exists(RomInfo.Arm9Path) ? PatchedCode(File.ReadAllBytes(RomInfo.Arm9Path)) : 0; }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException) { return 0; }
        }

        public static bool IsApplied => PatchedCode() != 0;

        // What the screen calls before the patch, one function for each kind of site; null when something else changed them.
        private static (uint Print, uint Width, uint Centre, uint Alloc, uint Release)? Vanilla(byte[] arm9)
        {
            RomInfo.SummaryFontSites s = Sites;
            uint? One(IEnumerable<uint> sites)
            {
                List<uint?> targets = sites.Select(a => CallAt(arm9, a)).Distinct().ToList();
                return targets.Count == 1 ? targets[0] : null;
            }
            uint? print = One(s.Prints), width = One(s.WidthR6.Concat(s.WidthR5R7).Concat(s.WidthR6R7));
            uint? centre = One(s.CentreTrainer.Append(s.CentreName)), alloc = CallAt(arm9, s.Load), release = CallAt(arm9, s.Release);
            if (print == null || width == null || centre == null || alloc == null || release == null) return null;
            uint[] all = { print.Value, width.Value, centre.Value, alloc.Value, release.Value };
            return all.Distinct().Count() == all.Length ? (print.Value, width.Value, centre.Value, alloc.Value, release.Value) : null;
        }

        /// <summary>Why the patch can't go in now, or null.</summary>
        public static string WhyNotApplicable()
        {
            if (!SupportsCurrentRom) return "This patch doesn't support this ROM yet.";
            if (!SyntheticOverlaySpace.Available()) return "Apply the ARM9 expansion first.";
            byte[] arm9 = File.ReadAllBytes(RomInfo.Arm9Path);
            if (PatchedCode(arm9) != 0) return "The patch is already applied.";
            return Vanilla(arm9) == null ? "Something else has already changed the summary screen code, so DSPRE leaves it alone." : null;
        }

        /// <summary>Writes the routine and an empty font table into a marked block at <paramref name="blockOffset"/>, then routes the screen's calls to it.</summary>
        public static void Apply(int blockOffset)
        {
            if (blockOffset % 4 != 0) throw new ArgumentException("The block must start on a 4-byte boundary.", nameof(blockOffset));
            byte[] arm9 = File.ReadAllBytes(RomInfo.Arm9Path);
            (uint print, uint width, uint centre, uint alloc, uint release) = Vanilla(arm9)
                ?? throw new InvalidOperationException("Something else has already changed the summary screen code, so DSPRE leaves it alone.");

            uint code = RomInfo.SynthOverlayLoadAddress + (uint)blockOffset + (uint)CodeStart;
            byte[] block = new byte[BlockLength];
            SyntheticOverlaySpace.Header(Marker, BlockLength).CopyTo(block, 0);
            Code.CopyTo(block, CodeStart);
            BitConverter.GetBytes(print | 1).CopyTo(block, CodeStart + PrintTarget);
            BitConverter.GetBytes(width | 1).CopyTo(block, CodeStart + WidthTarget);
            BitConverter.GetBytes(centre | 1).CopyTo(block, CodeStart + CentreTarget);
            BitConverter.GetBytes(alloc | 1).CopyTo(block, CodeStart + AllocTarget);
            BitConverter.GetBytes(release | 1).CopyTo(block, CodeStart + ReleaseTarget);
            BitConverter.GetBytes(code + TableAt).CopyTo(block, CodeStart + TableAddress);
            DSUtils.WriteToFile(Filesystem.ExpArmPath, block, (uint)blockOffset);

            foreach ((uint site, int entry) in Routes(Sites))
                DSUtils.WriteToFile(RomInfo.Arm9Path, Bl(site, code + (uint)entry), (uint)At(site));
        }

        // A window's first eight bytes as the routine compares them, from a template.
        private static (uint, uint) Key(SummaryScreenLayout.Window w) =>
            ((uint)(w.Layer | w.X << 8 | w.Y << 16 | w.Width << 24), (uint)(w.Height | w.Palette << 8 | (w.BaseTile & 0x7FFF) << 16));

        private static long TableOffset(uint code) => BlockOffset(code) + CodeStart + TableAt;

        /// <summary>The font each window is listed with, or -1 for the game's; null when the patch isn't in.</summary>
        public static int[] ReadFonts(IReadOnlyList<SummaryScreenLayout.Window> windows)
        {
            uint code = PatchedCode();
            if (code == 0) return null;
            byte[] table = DSUtils.ReadFromFile(Filesystem.ExpArmPath, (uint)TableOffset(code), Capacity * EntrySize + 4);
            Dictionary<(uint, uint), int> fonts = new();
            for (int at = 0; at + EntrySize <= table.Length; at += EntrySize)
            {
                uint first = BitConverter.ToUInt32(table, at);
                if (first == 0) break;
                fonts.TryAdd((first, BitConverter.ToUInt32(table, at + 4)), (int)BitConverter.ToUInt32(table, at + 8));
            }
            return windows.Select(w => fonts.TryGetValue(Key(w), out int font) ? font : -1).ToArray();
        }

        /// <summary>Lists each window given a font; the rest print in the game's.</summary>
        public static void WriteFonts(IReadOnlyList<SummaryScreenLayout.Window> windows, IReadOnlyList<int> fonts)
        {
            uint code = PatchedCode();
            if (code == 0) throw new InvalidOperationException("The summary screen fonts patch isn't applied.");
            byte[] table = new byte[Capacity * EntrySize + 4];
            int at = 0;
            for (int i = 0; i < windows.Count && i < fonts.Count; i++)
            {
                if (fonts[i] < 0 || fonts[i] >= Fonts.Length || windows[i].Width == 0) continue;
                if (at + EntrySize > table.Length - 4) break;
                (uint first, uint second) = Key(windows[i]);
                BitConverter.GetBytes(first).CopyTo(table, at);
                BitConverter.GetBytes(second).CopyTo(table, at + 4);
                BitConverter.GetBytes((uint)fonts[i]).CopyTo(table, at + 8);
                at += EntrySize;
            }
            DSUtils.WriteToFile(Filesystem.ExpArmPath, table, (uint)TableOffset(code));
        }
    }
}
