using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>Gives each HGSS field menu button its own pair of colours from the button palette file.</summary>
    public static class FieldMenuColoursPatch
    {
        public const int ButtonPalettes = 14, ItemCount = 13, RowsPerItem = 2, RowBytes = 0x20;
        public const int ItemColours = ItemCount * RowsPerItem * 16;

        private const int Overlay = 27;
        private static readonly RomLocator.TouchMenuColourCode At = RomLocator.TouchMenuColours;
        private const uint PaletteCopy = 0x4CC, PalettePointer = 0x4D0;

        // Keeps the palette file loaded: flush it, then store the file and its colours.
        private static readonly ushort[] KeepBefore = { 0x1C04, 0x9804, 0x6881, 0x68C0 };
        private static readonly ushort[] KeepAfter = { 0x4906, 0x506C, 0x9804, 0x68C0, 0x1D09, 0x5068, 0x46C0, 0x46C0, 0x46C0, 0x46C0 };

        // src = colours + item * 0x40 + (selected ? 0x20 : 0); dest = sprite[slot]'s palette * 0x20.
        private static readonly ushort[] ApplyBefore =
        {
            0xB5F8, 0x1C06, 0x9100, 0x4814, 0x5837, 0x2400, 0x4813, 0x5830, 0x06C0, 0x0F00, 0x00C1, 0x4812, 0x1840, 0x5C21, 0x290D, 0xD016,
            0x008A, 0x4810, 0x5A80, 0x4A10, 0x4290, 0xD010, 0x0188, 0x9900, 0x428C, 0xD100, 0x3020, 0x1838, 0x00A1, 0x1871, 0x22E4, 0x0092,
            0x5889, 0x222A, 0x5C89, 0x0149, 0x2220,
        };
        private static readonly ushort[] ApplyAfter = { 0x3401, 0x2C07, 0xDBDB, 0xBDF8, 0x46C0 };

        private static uint Ram => OverlayUtils.OverlayTable.GetRAMAddress(Overlay);

        private static byte[] ReadOverlay()
        {
            if (OverlayUtils.IsCompressed(Overlay)) OverlayUtils.Decompress(Overlay);
            return File.ReadAllBytes(OverlayUtils.GetPath(Overlay));
        }

        private static uint Target(byte[] d, int at)
        {
            ushort hi = BitConverter.ToUInt16(d, at), lo = BitConverter.ToUInt16(d, at + 2);
            int off = (hi & 0x7FF) << 12 | (lo & 0x7FF) << 1;
            if ((off & 0x400000) != 0) off -= 0x800000;
            uint to = (uint)(Ram + at + 4 + off);
            return (lo & 0xF800) == 0xE800 ? to & ~3u : to;
        }

        private static void Call(byte[] d, int at, uint to, bool exchange)
        {
            int off = (int)(to - ((Ram + (uint)at + 4) & (exchange ? ~3u : ~0u)));
            BitConverter.TryWriteBytes(d.AsSpan(at), (ushort)(0xF000 | (off >> 12) & 0x7FF));
            BitConverter.TryWriteBytes(d.AsSpan(at + 2), (ushort)((exchange ? 0xE800 : 0xF800) | (off >> 1) & 0x7FF));
        }

        private static void Put(byte[] d, int at, IEnumerable<ushort> code)
        {
            foreach (ushort op in code)
            {
                BitConverter.TryWriteBytes(d.AsSpan(at), op);
                at += 2;
            }
        }

        public static bool SupportsCurrentRom => GameFamily == GameFamilies.HGSS && RomLocator.TouchMenuSites?.MenuRects == RomLocator.TouchMenuCodedSite;

        private static bool Vanilla(byte[] d) =>
            BitConverter.ToUInt32(d, At.MenuOffset) == PaletteCopy && BitConverter.ToUInt16(d, At.ApplyColours) == 0xB5F8 && BitConverter.ToUInt16(d, At.ApplyColours + 6) == 0x2907
            && BitConverter.ToUInt16(d, At.ExitCheck) == 0x4826 && BitConverter.ToUInt16(d, At.KeepPalette) == 0x1C04 && BitConverter.ToUInt16(d, At.KeepPalette + 4) == 0x2140;

        public static bool IsApplied
        {
            get
            {
                if (!SupportsCurrentRom) return false;
                try
                {
                    if (OverlayUtils.IsStillCompressed(Overlay)) return false;
                    byte[] d = File.ReadAllBytes(OverlayUtils.GetPath(Overlay));
                    return At.MenuOffset + 4 <= d.Length && BitConverter.ToUInt32(d, At.MenuOffset) == PalettePointer;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { return false; }
            }
        }

        public static string WhyNotApplicable()
        {
            if (!SupportsCurrentRom) return "This ROM isn't supported.";
            return Vanilla(ReadOverlay()) ? null : "Something else has already changed the menu's colour code, so DSPRE leaves it alone.";
        }

        // Every item starts on the game's pair, so nothing changes until a colour does.
        private static byte[] WithEveryItem(byte[] nclr)
        {
            int section = NitroBgCodec.Find(nclr, "TTLP", 0), at = section + 0x18;
            byte[] pair = nclr.Skip(at).Take(RowBytes * RowsPerItem).ToArray();
            byte[] outp = new byte[at + ItemCount * pair.Length];
            Array.Copy(nclr, outp, at);
            for (int item = 0; item < ItemCount; item++) pair.CopyTo(outp, at + item * pair.Length);
            BitConverter.TryWriteBytes(outp.AsSpan(8), outp.Length);
            BitConverter.TryWriteBytes(outp.AsSpan(section + 4), outp.Length - section);
            BitConverter.TryWriteBytes(outp.AsSpan(section + 0x10), ItemCount * pair.Length);
            return outp;
        }

        public static void Apply()
        {
            byte[] d = ReadOverlay();
            uint load = Target(d, At.LoadCall), original = Target(d, At.InitCall);

            Put(d, At.KeepPalette, KeepBefore);
            Put(d, At.FlushCall + 4, KeepAfter);

            Put(d, At.ApplyColours, ApplyBefore);
            Call(d, At.LoadCallNew, load, true);
            Put(d, At.LoadCallNew + 4, ApplyAfter);
            BitConverter.TryWriteBytes(d.AsSpan(At.MenuOffset), PalettePointer);

            // This code overwrites the emulator check on exit. Colours go in once the sprites exist, otherwise only a cursor move sets them.
            Put(d, At.ExitCheck, new ushort[] { 0xE008, 0xB502 });
            Call(d, At.Cave + 2, original, false);
            Put(d, At.Cave + 6, new ushort[] { 0xBC01, 0x6941 });
            Call(d, At.Cave + 10, Ram + (uint)At.ApplyColours + 1, false);
            Put(d, At.Cave + 14, new ushort[] { 0xBD00, 0x46C0 });
            Call(d, At.InitCall, Ram + (uint)At.Cave + 1, false);
            DSUtils.WriteWholeFile(OverlayUtils.GetPath(Overlay), d);

            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.FieldTouchMenu });
            string path = ArchiveMembers.PathOf(DirNames.FieldTouchMenu, ButtonPalettes);
            ArchiveMembers.Put(DirNames.FieldTouchMenu, ButtonPalettes, WithEveryItem(File.ReadAllBytes(path)));
        }
    }
}
