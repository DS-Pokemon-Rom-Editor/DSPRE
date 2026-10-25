using System;
using System.IO;
using System.Linq;
using System.Text;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Lets HGSS battles print the HP bar text (the Pokémon names, and the Safari and Park ball lines) in any of the game's
    /// fonts. Battle loads fonts 2 and 5 through the summary fonts routine, and the prints go through a stub that sets the font.
    /// </summary>
    public static class HpBarFontPatch
    {
        public const string Marker = "HPBFONTSV1\0\0";

        private static int CodeStart => SyntheticOverlaySpace.HeaderSize;
        private static int StubAt => SummaryFontPatch.TableAt;
        private const int StubLength = 0x10, StubFont = 0, StubPrint = 0x0C;
        public static int BlockLength => CodeStart + StubAt + StubLength;

        public static bool SupportsCurrentRom => RomLocator.HpBarFontSites != null;

        private static RomInfo.HpBarFontSites Sites => RomLocator.HpBarFontSites;

        private static uint Address(int offset) => OverlayUtils.OverlayTable.GetRAMAddress(Sites.Overlay) + (uint)offset;

        private static byte[] ReadOverlay()
        {
            if (OverlayUtils.IsStillCompressed(Sites.Overlay)) return null;
            string path = OverlayUtils.GetPath(Sites.Overlay);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        // The target of the BL at an overlay offset, or null.
        private static uint? CallAt(byte[] overlay, int offset)
        {
            if (offset < 0 || offset + 4 > overlay.Length) return null;
            ushort hi = BitConverter.ToUInt16(overlay, offset), lo = BitConverter.ToUInt16(overlay, offset + 2);
            if ((hi & 0xF800) != 0xF000 || (lo & 0xF800) != 0xF800) return null;
            int off = ((hi & 0x7FF) << 12) | ((lo & 0x7FF) << 1);
            if ((off & 0x400000) != 0) off -= 0x800000;
            return (uint)(Address(offset) + 4 + off);
        }

        private static long BlockOffset(uint code) => (long)code - RomInfo.SynthOverlayLoadAddress - CodeStart;

        /// <summary>The routine's address when every site calls it and its block is there, or 0.</summary>
        private static uint PatchedCode(byte[] overlay)
        {
            if (!SupportsCurrentRom || overlay == null || !File.Exists(Filesystem.ExpArmPath) || CallAt(overlay, Sites.Prints[0]) is not uint stub) return 0;
            uint code = stub - (uint)StubAt;
            if (Sites.Prints.Any(a => CallAt(overlay, a) != stub)) return 0;
            if (Sites.Allocs.Any(a => CallAt(overlay, a) != code + SummaryFontPatch.LoadAt)) return 0;
            if (Sites.Releases.Any(a => CallAt(overlay, a) != code + SummaryFontPatch.ReleaseAt)) return 0;
            long block = BlockOffset(code);
            if (block < 0 || block + BlockLength > new FileInfo(Filesystem.ExpArmPath).Length) return 0;
            return Encoding.ASCII.GetString(DSUtils.ReadFromFile(Filesystem.ExpArmPath, (uint)block, Marker.Length)) == Marker ? code : 0;
        }

        private static uint PatchedCode()
        {
            try { return PatchedCode(ReadOverlay()); }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException) { return 0; }
        }

        public static bool IsApplied => PatchedCode() != 0;

        // What battle calls before the patch; null when something else changed them.
        private static (uint Print, uint Alloc, uint Release)? Vanilla(byte[] overlay)
        {
            RomInfo.HpBarFontSites s = Sites;
            uint?[] prints = s.Prints.Select(a => CallAt(overlay, a)).Distinct().ToArray();
            uint?[] allocs = s.Allocs.Select(a => CallAt(overlay, a)).Distinct().ToArray();
            uint?[] releases = s.Releases.Select(a => CallAt(overlay, a)).Distinct().ToArray();
            if (prints.Length != 1 || prints[0] == null || allocs.Length != 1 || allocs[0] == null || releases.Length != 1 || releases[0] == null) return null;
            // Each print sets r1 (the font) and r3 (x) just before the call; the stub replaces both.
            if (s.Prints.Any(a => BitConverter.ToUInt16(overlay, a - 0x18) != 0x2100 || BitConverter.ToUInt16(overlay, a - 2) != 0x1C0B)) return null;
            uint[] all = { prints[0].Value, allocs[0].Value, releases[0].Value };
            return all.Distinct().Count() == all.Length ? (prints[0].Value, allocs[0].Value, releases[0].Value) : null;
        }

        /// <summary>Why the patch can't go in now, or null.</summary>
        public static string WhyNotApplicable()
        {
            if (!SupportsCurrentRom) return "This patch doesn't support this ROM yet.";
            if (!SyntheticOverlaySpace.Available()) return "Apply the ARM9 expansion first.";
            if (OverlayUtils.IsCompressed(Sites.Overlay)) OverlayUtils.Decompress(Sites.Overlay);
            byte[] overlay = ReadOverlay();
            if (overlay == null) return "The battle overlay couldn't be read.";
            if (PatchedCode(overlay) != 0) return "The patch is already applied.";
            return Vanilla(overlay) == null ? "Something else has already changed the battle's HP bar code, so DSPRE leaves it alone." : null;
        }

        private static byte[] Bl(int from, uint to)
        {
            int off = (int)(to - (Address(from) + 4));
            if (off < -0x400000 || off >= 0x400000) throw new InvalidOperationException("The synthetic overlay is out of the battle code's reach.");
            ushort hi = (ushort)(0xF000 | ((off >> 12) & 0x7FF)), lo = (ushort)(0xF800 | ((off >> 1) & 0x7FF));
            return BitConverter.GetBytes(hi).Concat(BitConverter.GetBytes(lo)).ToArray();
        }

        /// <summary>Writes the routine and the stub (with the game's font) at <paramref name="blockOffset"/>, then routes battle's calls to them.</summary>
        public static void Apply(int blockOffset)
        {
            if (blockOffset % 4 != 0) throw new ArgumentException("The block must start on a 4-byte boundary.", nameof(blockOffset));
            if (OverlayUtils.IsCompressed(Sites.Overlay)) OverlayUtils.Decompress(Sites.Overlay);
            byte[] overlay = ReadOverlay() ?? throw new InvalidOperationException("The battle overlay couldn't be read.");
            (uint print, uint alloc, uint release) = Vanilla(overlay)
                ?? throw new InvalidOperationException("Something else has already changed the battle's HP bar code, so DSPRE leaves it alone.");

            uint code = RomInfo.SynthOverlayLoadAddress + (uint)blockOffset + (uint)CodeStart;
            byte[] block = new byte[BlockLength];
            SyntheticOverlaySpace.Header(Marker, BlockLength).CopyTo(block, 0);
            SummaryFontPatch.Code.Take(SummaryFontPatch.TableAt).ToArray().CopyTo(block, CodeStart);
            BitConverter.GetBytes(alloc | 1).CopyTo(block, CodeStart + SummaryFontPatch.AllocTarget);
            BitConverter.GetBytes(release | 1).CopyTo(block, CodeStart + SummaryFontPatch.ReleaseTarget);
            // Each print passes x = 0 in r3, so the stub clears it again after borrowing it (hp_bar_fonts.s).
            ushort[] stub = { 0x2100, 0x4B02, 0x469C, 0x2300, 0x4760, 0x46C0 };
            for (int i = 0; i < stub.Length; i++) BitConverter.GetBytes(stub[i]).CopyTo(block, CodeStart + StubAt + i * 2);
            BitConverter.GetBytes(print | 1).CopyTo(block, CodeStart + StubAt + StubPrint);
            DSUtils.WriteToFile(Filesystem.ExpArmPath, block, (uint)blockOffset);

            string path = OverlayUtils.GetPath(Sites.Overlay);
            foreach (int a in Sites.Prints) DSUtils.WriteToFile(path, Bl(a, code + (uint)StubAt), (uint)a);
            foreach (int a in Sites.Allocs) DSUtils.WriteToFile(path, Bl(a, code + SummaryFontPatch.LoadAt), (uint)a);
            foreach (int a in Sites.Releases) DSUtils.WriteToFile(path, Bl(a, code + SummaryFontPatch.ReleaseAt), (uint)a);
        }

        /// <summary>The font number the HP bar text prints in, or -1 when the patch isn't in.</summary>
        public static int ReadFont()
        {
            uint code = PatchedCode();
            if (code == 0) return -1;
            return DSUtils.ReadFromFile(Filesystem.ExpArmPath, (uint)(BlockOffset(code) + CodeStart + StubAt + StubFont), 1)[0];
        }

        public static void WriteFont(int font)
        {
            uint code = PatchedCode();
            if (code == 0) throw new InvalidOperationException("The HP bar fonts patch isn't applied.");
            if (font < 0 || font >= GameFonts.All.Count) throw new ArgumentOutOfRangeException(nameof(font));
            DSUtils.WriteToFile(Filesystem.ExpArmPath, new[] { (byte)font }, (uint)(BlockOffset(code) + CodeStart + StubAt + StubFont));
        }
    }
}
