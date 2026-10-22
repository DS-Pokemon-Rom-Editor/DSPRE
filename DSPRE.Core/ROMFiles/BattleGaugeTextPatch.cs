using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Clears the white box behind each HP bar's name, level, HP numbers, gender symbol, "Lv" and caught ball. The battle
    /// writes the text into a buffer filled with colour 15 and takes the rest from tiles with a colour 15 background, then
    /// copies them over the bar; with the patch the copy keeps the bar's pixel wherever they have colour 15.
    /// Source: Resources/ROMToolboxDB/HpBarText/hp_bar_text.s.
    /// </summary>
    public static class BattleGaugeTextPatch
    {
        public const string Marker = "HPBARTEXTV1\0";

        // Assembled from hp_bar_text.s.
        private static readonly byte[] Code =
        {
            0x41, 0x68, 0x09, 0x68, 0x4a, 0x68, 0x10, 0x39, 0x09, 0x68, 0x49, 0x69,
            0x89, 0x1a, 0x11, 0x4a, 0x89, 0x1a, 0x13, 0x4a, 0x11, 0x60, 0x00, 0x68,
            0x10, 0x49, 0x08, 0x47, 0xf0, 0xb5, 0x10, 0x4c, 0x24, 0x68, 0x64, 0x18,
            0x0c, 0x4b, 0x9c, 0x46, 0x10, 0xe0, 0x05, 0x88, 0x26, 0x88, 0x6f, 0x08,
            0x2f, 0x40, 0xbb, 0x08, 0x1f, 0x40, 0x63, 0x46, 0x1f, 0x40, 0x3b, 0x01,
            0xdf, 0x1b, 0x3e, 0x40, 0xbd, 0x43, 0x35, 0x43, 0x0d, 0x80, 0x02, 0x30,
            0x02, 0x31, 0x02, 0x34, 0x02, 0x3a, 0xec, 0xda, 0xf0, 0xbd, 0xc0, 0x46,
            0x00, 0x00, 0x40, 0x06, 0x11, 0x11, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        };
        private const int HookAt = 0x00, MergeAt = 0x1C, ProxyGetterAt = 0x5C, OffsetAtAt = 0x60, CopyAt = 0x64, OffsetAt = 0x68;
        private static int CodeStart => SyntheticOverlaySpace.HeaderSize;
        public static int BlockLength => CodeStart + Code.Length;
        private const ushort LoadSprite = 0x6800, Nop = 0x46C0;

        public static bool SupportsCurrentRom => RomLocator.HpBarTextSites != null;

        private static RomInfo.HpBarTextSites Sites => RomLocator.HpBarTextSites;
        private static uint OverlayAddress => OverlayUtils.OverlayTable.GetRAMAddress(Sites.Overlay);

        private static byte[] ReadOverlay()
        {
            if (OverlayUtils.IsCompressed(Sites.Overlay)) OverlayUtils.Decompress(Sites.Overlay);
            return File.ReadAllBytes(OverlayUtils.GetPath(Sites.Overlay));
        }

        // The target of a BL or BLX at at, and whether it is BLX; null when it is neither.
        private static (uint Target, bool Blx)? CallAt(byte[] d, int at)
        {
            if (at < 0 || at + 4 > d.Length) return null;
            ushort hi = BitConverter.ToUInt16(d, at), lo = BitConverter.ToUInt16(d, at + 2);
            if ((hi & 0xF800) != 0xF000 || (lo & 0xE800) != 0xE800) return null;
            int off = ((hi & 0x7FF) << 12) | ((lo & 0x7FF) << 1);
            if ((off & 0x400000) != 0) off -= 0x800000;
            uint target = (uint)(OverlayAddress + at + 4 + off);
            bool blx = (lo & 0xF800) == 0xE800;
            return (blx ? target & ~3u : target, blx);
        }

        private static byte[] Bl(int at, uint to)
        {
            int off = (int)(to - (OverlayAddress + (uint)at + 4));
            if (off < -0x400000 || off >= 0x400000) throw new InvalidOperationException("The synthetic overlay is out of the battle code's reach.");
            ushort hi = (ushort)(0xF000 | ((off >> 12) & 0x7FF)), lo = (ushort)(0xF800 | ((off >> 1) & 0x7FF));
            return BitConverter.GetBytes(hi).Concat(BitConverter.GetBytes(lo)).ToArray();
        }

        private static bool Routed(byte[] ov, uint code, int[] hooks, int[] copies)
            => hooks.All(h => CallAt(ov, h) is (uint t, false) && t == code + HookAt && BitConverter.ToUInt16(ov, h + 4) == Nop)
            && copies.All(c => CallAt(ov, c) is (uint t, false) && t == code + MergeAt);

        private static bool Untouched(byte[] ov, uint getter, uint copy, int[] hooks, int[] copies)
            => hooks.All(h => BitConverter.ToUInt16(ov, h) == LoadSprite && CallAt(ov, h + 2) is (uint t, false) && t == getter)
            && copies.All(c => CallAt(ov, c) is (uint t, true) && t == copy);

        /// <summary>The patch's code address when every text site calls into it and its marked block is there, or 0.</summary>
        private static uint PatchedCode(byte[] ov)
        {
            if (!File.Exists(Filesystem.ExpArmPath) || CallAt(ov, Sites.Hooks[0]) is not (uint hook, false)) return 0;
            uint code = hook - HookAt;
            if (!Routed(ov, code, Sites.Hooks, Sites.Copies)) return 0;
            long block = BlockOffset(code);
            if (block < 0 || block + BlockLength > new FileInfo(Filesystem.ExpArmPath).Length) return 0;
            return Encoding.ASCII.GetString(DSUtils.ReadFromFile(Filesystem.ExpArmPath, (uint)block, Marker.Length)) == Marker ? code : 0;
        }

        private static long BlockOffset(uint code) => (long)code - RomInfo.SynthOverlayLoadAddress - CodeStart;

        private static bool Probe(Func<byte[], uint, bool> test)
        {
            if (!SupportsCurrentRom) return false;
            try
            {
                byte[] ov = ReadOverlay();
                uint code = PatchedCode(ov);
                return code != 0 && test(ov, code);
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException) { return false; }
        }

        /// <summary>The name, level and HP numbers keep the bar behind them.</summary>
        public static bool TextIsClear => Probe((_, _) => true);

        /// <summary>The gender symbol, "Lv" and the caught ball keep the bar behind them too; ROMs patched before they were covered don't.</summary>
        public static bool TilesAreClear => Probe((ov, code) => Routed(ov, code, Sites.TileHooks, Sites.TileCopies));

        public static bool IsApplied => TilesAreClear;

        /// <summary>The caught ball keeps the bar behind it; Diamond and Pearl's ball has colour 15 inside it, so never there.</summary>
        public static bool CaughtIsClear => RomInfo.GameFamily != RomInfo.GameFamilies.DP && TilesAreClear;

        /// <summary>The patch is in for the text only, and the tile sites are still the game's own.</summary>
        public static bool NeedsTileUpdate => !TilesAreClear && Probe((ov, code) =>
        {
            uint block = (uint)BlockOffset(code);
            uint getter = BitConverter.ToUInt32(DSUtils.ReadFromFile(Filesystem.ExpArmPath, block + (uint)(CodeStart + ProxyGetterAt), 4), 0) & ~1u;
            uint copy = BitConverter.ToUInt32(DSUtils.ReadFromFile(Filesystem.ExpArmPath, block + (uint)(CodeStart + CopyAt), 4), 0);
            return Untouched(ov, getter, copy, Sites.TileHooks, Sites.TileCopies);
        });

        /// <summary>Why the patch can't go in now, or null.</summary>
        public static string WhyNotApplicable()
        {
            if (!SupportsCurrentRom) return "The HP bar text code isn't mapped for this ROM.";
            if (NeedsTileUpdate) return null;
            if (!SyntheticOverlaySpace.Available()) return "Apply the ARM9 expansion first.";
            byte[] ov = ReadOverlay();
            uint? getter = CallAt(ov, Sites.Hooks[0] + 2) is (uint g, false) ? g : null;
            uint? copy = CallAt(ov, Sites.Copies[0]) is (uint c, true) ? c : null;
            bool vanilla = getter != null && copy != null
                && Untouched(ov, getter.Value, copy.Value, Sites.Hooks, Sites.Copies)
                && Untouched(ov, getter.Value, copy.Value, Sites.TileHooks, Sites.TileCopies);
            return vanilla ? null : "The HP bar text code has already been changed by something else.";
        }

        /// <summary>Writes the routine into a marked block at <paramref name="blockOffset"/> and points the battle's text copies at it.</summary>
        public static void Apply(int blockOffset)
        {
            if (blockOffset % 4 != 0) throw new ArgumentException("The block must be word aligned.", nameof(blockOffset));
            byte[] ov = ReadOverlay();
            uint getter = CallAt(ov, Sites.Hooks[0] + 2).Value.Target, copy = CallAt(ov, Sites.Copies[0]).Value.Target;

            uint code = RomInfo.SynthOverlayLoadAddress + (uint)blockOffset + (uint)CodeStart;
            byte[] block = new byte[BlockLength];
            Encoding.ASCII.GetBytes(Marker).CopyTo(block, 0);
            BitConverter.GetBytes(1u).CopyTo(block, 0x0C);
            BitConverter.GetBytes((uint)BlockLength).CopyTo(block, 0x10);
            Code.CopyTo(block, CodeStart);
            BitConverter.GetBytes(getter | 1).CopyTo(block, CodeStart + ProxyGetterAt);
            BitConverter.GetBytes(code + OffsetAt).CopyTo(block, CodeStart + OffsetAtAt);
            BitConverter.GetBytes(copy).CopyTo(block, CodeStart + CopyAt);
            DSUtils.WriteToFile(Filesystem.ExpArmPath, block, (uint)blockOffset);

            Route(code, Sites.Hooks.Concat(Sites.TileHooks), Sites.Copies.Concat(Sites.TileCopies));
        }

        /// <summary>Points the gender symbol, "Lv" and caught ball at a patch that is already in for the text.</summary>
        public static void AddTiles()
        {
            uint code = PatchedCode(ReadOverlay());
            if (code == 0) throw new InvalidOperationException("The clear battle text background patch isn't in this ROM.");
            Route(code, Sites.TileHooks, Sites.TileCopies);
        }

        private static void Route(uint code, IEnumerable<int> hooks, IEnumerable<int> copies)
        {
            string path = OverlayUtils.GetPath(Sites.Overlay);
            foreach (int hook in hooks)
                DSUtils.WriteToFile(path, Bl(hook, code + HookAt).Concat(BitConverter.GetBytes(Nop)).ToArray(), (uint)hook);
            foreach (int site in copies)
                DSUtils.WriteToFile(path, Bl(site, code + MergeAt), (uint)site);
        }
    }
}
