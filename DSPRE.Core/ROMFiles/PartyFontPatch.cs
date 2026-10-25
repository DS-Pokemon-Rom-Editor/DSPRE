using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Lets the HGSS party menu print each Pokémon's name and compatibility line in any of the game's fonts. Those
    /// prints go through a copy of the summary fonts routine, with a table of the party menu's windows.
    /// </summary>
    public static class PartyFontPatch
    {
        public const string Marker = "PTYFONTSV1\0\0";

        /// <summary>The party windows whose prints the patch reroutes.</summary>
        public static readonly string[] Parts = { "name", "comment" };

        private const int Capacity = 64;
        private static int CodeStart => SyntheticOverlaySpace.HeaderSize;
        private static int TableLength => Capacity * SummaryFontPatch.EntrySize + 4;
        // The party menu draws its panels before its own FontID_Alloc(4), so this stub loads fonts 2 and 5 on the way into
        // PartyMenu_AddAllWindows. The routine's release entry frees them with font 4 when the menu closes.
        private static int StubAt => SummaryFontPatch.TableAt + TableLength;
        private const int StubLength = 0x24, StubHeap = 0x06, StubAlloc = 0x1C, StubWindows = 0x20;
        public static int BlockLength => CodeStart + StubAt + StubLength;

        private static byte[] Stub(byte heap, uint alloc, uint addWindows)
        {
            ushort[] code =
            {
                0xB510,              // push {r4, lr}
                0x1C04,              // adds r4, r0, #0
                0x2002, 0x2100,      // movs r0, #2; movs r1, #heap
                0x4B04, 0x4798,      // ldr r3, Alloc; blx r3
                0x2005, 0x2100,      // movs r0, #5; movs r1, #heap
                0x4B02, 0x4798,      // ldr r3, Alloc; blx r3
                0x1C20,              // adds r0, r4, #0
                0x4B02, 0x4798,      // ldr r3, AddWindows; blx r3
                0xBD10,              // pop {r4, pc}
            };
            byte[] stub = new byte[StubLength];
            for (int i = 0; i < code.Length; i++) BitConverter.GetBytes(code[i]).CopyTo(stub, i * 2);
            stub[StubHeap] = heap;
            stub[StubHeap + 8] = heap;
            BitConverter.GetBytes(alloc | 1).CopyTo(stub, StubAlloc);
            BitConverter.GetBytes(addWindows | 1).CopyTo(stub, StubWindows);
            return stub;
        }

        public static bool SupportsCurrentRom => RomLocator.PartyFontSites != null;

        private static RomInfo.PartyFontSites Sites => RomLocator.PartyFontSites;

        private static IEnumerable<(uint Site, int Entry)> Routes(RomInfo.PartyFontSites s) =>
            s.Prints.Select(a => (a, SummaryFontPatch.PrintAt))
             .Append((s.AddWindows, StubAt))
             .Append((s.Release, SummaryFontPatch.ReleaseAt));

        private static int At(uint address) => (int)(address - ARM9.Address);

        private static long BlockOffset(uint code) => (long)code - RomInfo.SynthOverlayLoadAddress - CodeStart;

        /// <summary>The routine's address when every site calls it and its block is there, or 0.</summary>
        private static uint PatchedCode(byte[] arm9)
        {
            if (!SupportsCurrentRom || !File.Exists(Filesystem.ExpArmPath) || SummaryFontPatch.CallAt(arm9, Sites.Prints[0]) is not uint print) return 0;
            uint code = print - SummaryFontPatch.PrintAt;
            if (Routes(Sites).Any(r => SummaryFontPatch.CallAt(arm9, r.Site) != code + (uint)r.Entry)) return 0;
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

        // What the party menu calls before the patch, and the heap its fonts load on; null when something else changed them.
        private static (uint Print, uint AddWindows, uint Alloc, uint Release, byte Heap)? Vanilla(byte[] arm9)
        {
            RomInfo.PartyFontSites s = Sites;
            List<uint?> prints = s.Prints.Select(a => SummaryFontPatch.CallAt(arm9, a)).Distinct().ToList();
            uint? windows = SummaryFontPatch.CallAt(arm9, s.AddWindows), alloc = SummaryFontPatch.CallAt(arm9, s.Alloc);
            uint? release = SummaryFontPatch.CallAt(arm9, s.Release);
            if (prints.Count != 1 || prints[0] == null || windows == null || alloc == null || release == null) return null;
            // The heap is the movs r1 right before FontID_Alloc(4).
            int at = At(s.Alloc) - 2;
            if (at < 0 || arm9[at + 1] != 0x21) return null;
            uint[] all = { prints[0].Value, windows.Value, alloc.Value, release.Value };
            return all.Distinct().Count() == all.Length ? (prints[0].Value, windows.Value, alloc.Value, release.Value, arm9[at]) : null;
        }

        /// <summary>Why the patch can't go in now, or null.</summary>
        public static string WhyNotApplicable()
        {
            if (!SupportsCurrentRom) return "This patch doesn't support this ROM yet.";
            if (!SyntheticOverlaySpace.Available()) return "Apply the ARM9 expansion first.";
            byte[] arm9 = File.ReadAllBytes(RomInfo.Arm9Path);
            if (PatchedCode(arm9) != 0) return "The patch is already applied.";
            return Vanilla(arm9) == null ? "Something else has already changed the party menu code, so DSPRE leaves it alone." : null;
        }

        /// <summary>Writes the routine and an empty font table into a marked block at <paramref name="blockOffset"/>, then routes the party menu's calls to it.</summary>
        public static void Apply(int blockOffset)
        {
            if (blockOffset % 4 != 0) throw new ArgumentException("The block must start on a 4-byte boundary.", nameof(blockOffset));
            byte[] arm9 = File.ReadAllBytes(RomInfo.Arm9Path);
            (uint print, uint addWindows, uint alloc, uint release, byte heap) = Vanilla(arm9)
                ?? throw new InvalidOperationException("Something else has already changed the party menu code, so DSPRE leaves it alone.");

            uint code = RomInfo.SynthOverlayLoadAddress + (uint)blockOffset + (uint)CodeStart;
            byte[] block = new byte[BlockLength];
            SyntheticOverlaySpace.Header(Marker, BlockLength).CopyTo(block, 0);
            SummaryFontPatch.Code.CopyTo(block, CodeStart);
            BitConverter.GetBytes(print | 1).CopyTo(block, CodeStart + SummaryFontPatch.PrintTarget);
            BitConverter.GetBytes(alloc | 1).CopyTo(block, CodeStart + SummaryFontPatch.AllocTarget);
            BitConverter.GetBytes(release | 1).CopyTo(block, CodeStart + SummaryFontPatch.ReleaseTarget);
            BitConverter.GetBytes(code + SummaryFontPatch.TableAt).CopyTo(block, CodeStart + SummaryFontPatch.TableAddress);
            Stub(heap, alloc, addWindows).CopyTo(block, CodeStart + StubAt);
            DSUtils.WriteToFile(Filesystem.ExpArmPath, block, (uint)blockOffset);

            foreach ((uint site, int entry) in Routes(Sites))
                DSUtils.WriteToFile(RomInfo.Arm9Path, SummaryFontPatch.Bl(site, code + (uint)entry), (uint)At(site));
        }

        // The first eight bytes of a window, as the routine compares them.
        private static (uint, uint) Key(ScreenWindowLayout.Window w) =>
            ((uint)(w.Layer | w.X << 8 | w.Y << 16 | w.Width << 24), (uint)(w.Height | w.Palette << 8 | (w.BaseTile & 0x7FFF) << 16));

        private static long TableOffset(uint code) => BlockOffset(code) + CodeStart + SummaryFontPatch.TableAt;

        /// <summary>The font table as the synthetic overlay holds it, or null when the patch isn't in.</summary>
        public static byte[] ReadTable()
        {
            uint code = PatchedCode();
            return code == 0 ? null : DSUtils.ReadFromFile(Filesystem.ExpArmPath, (uint)TableOffset(code), TableLength);
        }

        public static void WriteTable(byte[] table)
        {
            uint code = PatchedCode();
            if (code == 0) throw new InvalidOperationException("The party menu fonts patch isn't applied.");
            DSUtils.WriteToFile(Filesystem.ExpArmPath, table, (uint)TableOffset(code));
        }

        /// <summary>The table that gives each window of a part its font; a part at -1 keeps the game's.</summary>
        public static byte[] Table(IEnumerable<ScreenWindowLayout.Window> windows, IReadOnlyDictionary<string, int> fonts)
        {
            byte[] table = new byte[TableLength];
            int at = 0, known = GameFonts.All.Count;
            foreach (ScreenWindowLayout.Window w in windows)
            {
                if (w.Part == null || !fonts.TryGetValue(w.Part, out int font) || font < 0 || font >= known) continue;
                if (at + SummaryFontPatch.EntrySize > table.Length - 4) break;
                (uint first, uint second) = Key(w);
                BitConverter.GetBytes(first).CopyTo(table, at);
                BitConverter.GetBytes(second).CopyTo(table, at + 4);
                BitConverter.GetBytes((uint)font).CopyTo(table, at + 8);
                at += SummaryFontPatch.EntrySize;
            }
            return table;
        }

        /// <summary>The font of each part in <paramref name="table"/>, from its first window listed there; -1 when it keeps the game's.</summary>
        public static Dictionary<string, int> FontsIn(byte[] table, IEnumerable<ScreenWindowLayout.Window> windows)
        {
            Dictionary<(uint, uint), int> listed = new();
            for (int at = 0; table != null && at + SummaryFontPatch.EntrySize <= table.Length; at += SummaryFontPatch.EntrySize)
            {
                uint first = BitConverter.ToUInt32(table, at);
                if (first == 0) break;
                listed.TryAdd((first, BitConverter.ToUInt32(table, at + 4)), (int)BitConverter.ToUInt32(table, at + 8));
            }
            Dictionary<string, int> fonts = Parts.ToDictionary(p => p, _ => -1);
            foreach (ScreenWindowLayout.Window w in windows)
                if (w.Part != null && fonts.TryGetValue(w.Part, out int f) && f < 0 && listed.TryGetValue(Key(w), out int font)) fonts[w.Part] = font;
            return fonts;
        }
    }
}
