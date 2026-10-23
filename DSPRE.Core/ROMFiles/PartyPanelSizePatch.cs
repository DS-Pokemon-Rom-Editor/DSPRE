using System;
using System.IO;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Lets HGSS party panels be any size up to 96 tiles, by turning each size-dependent number in arm9 into an immediate, in place.
    /// </summary>
    public static class PartyPanelSizePatch
    {
        /// <summary>A panel's size in tiles and its HP bar strip, the tiles eggs blank and switches redraw.</summary>
        public sealed record Size(int Width, int Height, int StripColumn, int StripRow, int StripLength, int StripTile)
        {
            // The strip's first entry in a panel buffer, and its byte offset in the party menu's work area.
            internal int Strip => StripRow * Width + StripColumn;
            internal int StripSource => 0x3D4 + 2 * Strip;
        }

        /// <summary>16 by 6, the strip at column 6 of row 3, nine tiles long.</summary>
        public static Size Original => new Size(16, 6, 6, 3, 9, RomLocator.PartyStripTile);

        /// <summary>The panel buffers hold 96 tiles each, so no panel can be bigger.</summary>
        public const int MaxArea = 96;
        /// <summary>The panel art stacks three panels in its 32 rows.</summary>
        public const int MaxHeight = 32 / 3;

        private enum Fn { Draw, Empty, Loader, Capture, Ticks, Step, Redraw }

        private sealed record Site(Fn Function, int Offset, ushort Old, Func<Size, ushort> New);

        private static ushort Movs(int rd, int imm) => (ushort)(0x2000 | rd << 8 | imm);
        private static ushort Cmp(int rn, int imm) => (ushort)(0x2800 | rn << 8 | imm);
        private static ushort Adds(int rd, int imm) => (ushort)(0x3000 | rd << 8 | imm);
        private static ushort Adds3(int rd, int rn, int imm) => (ushort)(0x1C00 | imm << 6 | rn << 3 | rd);
        private static ushort Lsls(int rd, int rm, int shift) => (ushort)(shift << 6 | rm << 3 | rd);
        private static Func<Size, ushort> Fixed(ushort value) => _ => value;
        private const ushort Nop = 0x46C0;

        // Offsets from each function's start; pokeheartgold names in RomLocator.PartyPanelCodeSitesIn.
        private static readonly Site[] Sites =
        {
            // sub_0207A16C: the normal buffer's strip; the low three bits are added by its caller.
            new(Fn.Draw, 0x000, 0x2111, s => Movs(1, s.StripSource >> 3)),
            new(Fn.Draw, 0x002, 0x0189, Fixed(Lsls(1, 1, 3))),
            // sub_0207A174: the panel copy, the egg strip and the panel's palette.
            new(Fn.Draw, 0x044, 0x2210, s => Movs(2, s.Width)),
            new(Fn.Draw, 0x048, 0x2106, s => Movs(1, s.Height)),
            new(Fn.Draw, 0x06C, 0x1CF8, Fixed(Adds3(0, 7, 0))),
            new(Fn.Draw, 0x06E, 0x0600, s => Adds(0, s.StripRow)),
            new(Fn.Draw, 0x070, 0x0E00, Fixed(Nop)),
            new(Fn.Draw, 0x074, 0x2009, s => Movs(0, s.StripLength)),
            new(Fn.Draw, 0x07E, 0x1DB3, Fixed(Adds3(3, 6, 0))),
            new(Fn.Draw, 0x082, 0x061B, s => Adds(3, s.StripColumn)),
            new(Fn.Draw, 0x088, 0x224B, s => Movs(2, s.StripTile)),
            new(Fn.Draw, 0x08A, 0x0E1B, Fixed(Nop)),
            new(Fn.Draw, 0x090, 0x2010, s => Movs(0, s.Width)),
            new(Fn.Draw, 0x094, 0x2006, s => Movs(0, s.Height)),
            // sub_0207A780: an empty slot's panel.
            new(Fn.Empty, 0x008, 0x2110, s => Movs(1, s.Width)),
            new(Fn.Empty, 0x010, 0x2006, s => Movs(0, s.Height)),
            new(Fn.Empty, 0x034, 0x2010, s => Movs(0, s.Width)),
            new(Fn.Empty, 0x038, 0x2006, s => Movs(0, s.Height)),
            // sub_0207CAAC: the lead, normal and empty panels from arrangement 22, stacked at rows 0, H and 2H.
            new(Fn.Loader, 0x02E, 0x2220, s => Movs(2, 2 * s.Width)),
            new(Fn.Loader, 0x034, 0x1DA1, Fixed(0x9900)),
            new(Fn.Loader, 0x036, 0x018A, s => Movs(2, s.Height)),
            new(Fn.Loader, 0x038, 0x9901, Fixed(Lsls(2, 2, 6))),
            new(Fn.Loader, 0x03A, 0x1C30, Fixed(0x1889)),
            new(Fn.Loader, 0x03C, 0x1889, Fixed(0x1C30)),
            new(Fn.Loader, 0x03E, 0x2220, s => Movs(2, 2 * s.Width)),
            new(Fn.Loader, 0x044, 0x1C21, Fixed(0x9900)),
            new(Fn.Loader, 0x046, 0x310C, s => Movs(2, 2 * s.Height)),
            new(Fn.Loader, 0x048, 0x018A, Fixed(Lsls(2, 2, 6))),
            new(Fn.Loader, 0x04A, 0x9901, Fixed(0x1889)),
            new(Fn.Loader, 0x04C, 0x1C38, Fixed(0x1C38)),
            new(Fn.Loader, 0x04E, 0x1889, s => Movs(2, 2 * s.Width)),
            new(Fn.Loader, 0x050, 0x2220, Fixed(Nop)),
            new(Fn.Loader, 0x05E, 0x3520, s => Adds(5, 2 * s.Width)),
            new(Fn.Loader, 0x060, 0x3620, s => Adds(6, 2 * s.Width)),
            new(Fn.Loader, 0x062, 0x3720, s => Adds(7, 2 * s.Width)),
            new(Fn.Loader, 0x064, 0x2C06, s => Cmp(4, s.Height)),
            // sub_0207FC1C: the row step was a shift and is now a running offset.
            new(Fn.Capture, 0x0B4, 0x9803, Fixed(Movs(5, 0))),
            new(Fn.Capture, 0x0B6, 0x0165, Fixed(0x9803)),
            new(Fn.Capture, 0x0BE, 0x2220, s => Movs(2, 2 * s.Width)),
            new(Fn.Capture, 0x0D8, 0x2220, s => Movs(2, 2 * s.Width)),
            new(Fn.Capture, 0x0E0, 0x2220, s => Movs(2, 2 * s.Width)),
            new(Fn.Capture, 0x100, 0x2220, s => Movs(2, 2 * s.Width)),
            new(Fn.Capture, 0x106, 0x1C60, Fixed(Adds3(4, 4, 1))),
            new(Fn.Capture, 0x108, 0x0400, s => Adds(5, 2 * s.Width)),
            new(Fn.Capture, 0x10A, 0x0C04, Fixed(Nop)),
            new(Fn.Capture, 0x10C, 0x2C06, s => Cmp(4, s.Height)),
            new(Fn.Capture, 0x10E, 0xD3D1, Fixed(0xD3D2)),
            // sub_0207FD6C: the slide takes one step a tile.
            new(Fn.Ticks, 0x082, 0x2910, s => Cmp(1, s.Width)),
            // PartyMenu_SwapMonsPanelsStep: the fill width shared a register with the fill mode, so the mode gets its own.
            new(Fn.Step, 0x026, 0x2110, s => Movs(1, s.Width)),
            new(Fn.Step, 0x028, 0x0600, Fixed(Movs(2, 16))),
            new(Fn.Step, 0x02A, 0x0E00, Fixed(0x9203)),
            new(Fn.Step, 0x032, 0x2006, s => Movs(0, s.Height)),
            new(Fn.Step, 0x036, 0x9103, Fixed(Nop)),
            new(Fn.Step, 0x046, 0x2110, s => Movs(1, s.Width)),
            new(Fn.Step, 0x04A, 0x2006, s => Movs(0, s.Height)),
            new(Fn.Step, 0x04E, 0x9103, Fixed(Nop)),
            new(Fn.Step, 0x070, 0x2010, s => Movs(0, s.Width)),
            new(Fn.Step, 0x07C, 0x2106, s => Movs(1, s.Height)),
            new(Fn.Step, 0x09E, 0x2010, s => Movs(0, s.Width)),
            new(Fn.Step, 0x0A8, 0x2106, s => Movs(1, s.Height)),
            new(Fn.Step, 0x0AA, 0x018B, Fixed(Adds(4, 0xC0))),
            new(Fn.Step, 0x0AC, 0x18E4, Fixed(Adds(4, 0xC0))),
            new(Fn.Step, 0x0DA, 0x2010, s => Movs(0, s.Width)),
            new(Fn.Step, 0x0E6, 0x2106, s => Movs(1, s.Height)),
            new(Fn.Step, 0x10E, 0x2010, s => Movs(0, s.Width)),
            new(Fn.Step, 0x118, 0x2106, s => Movs(1, s.Height)),
            new(Fn.Step, 0x11A, 0x018B, Fixed(Adds(4, 0xC0))),
            new(Fn.Step, 0x11C, 0x18E4, Fixed(Adds(4, 0xC0))),
            // PartyMenu_RedrawMonHpBarAfterSwap: the strip copied back after a switch.
            new(Fn.Redraw, 0x014, 0x1C03, s => Adds3(3, 0, s.StripSource & 7)),
            new(Fn.Redraw, 0x032, 0x244B, s => Movs(4, s.StripTile)),
            new(Fn.Redraw, 0x038, 0x326C, s => Adds(2, 2 * s.Strip)),
            new(Fn.Redraw, 0x03E, 0x306C, s => Adds(0, 2 * s.Strip)),
            new(Fn.Redraw, 0x050, 0x2909, s => Cmp(1, s.StripLength)),
            new(Fn.Redraw, 0x062, 0x316C, s => Adds(1, 2 * s.Strip)),
            new(Fn.Redraw, 0x074, 0x316C, s => Adds(1, 2 * s.Strip)),
            new(Fn.Redraw, 0x07E, 0x2C09, s => Cmp(4, s.StripLength)),
        };

        private static uint AddressOf(RomInfo.PartyPanelCodeSites code, Fn function) => function switch
        {
            Fn.Draw => code.Draw,
            Fn.Empty => code.Empty,
            Fn.Loader => code.Loader,
            Fn.Capture => code.Capture,
            Fn.Ticks => code.Ticks,
            Fn.Step => code.Step,
            _ => code.Redraw,
        };

        private static int FileOffset(RomInfo.PartyPanelCodeSites code, Site site) => (int)(AddressOf(code, site.Function) - ARM9.Address) + site.Offset;

        private static ushort[] ReadSites(RomInfo.PartyPanelCodeSites code)
        {
            if (code == null || !File.Exists(RomInfo.Arm9Path)) return null;
            byte[] arm9 = File.ReadAllBytes(RomInfo.Arm9Path);
            ushort[] now = new ushort[Sites.Length];
            for (int i = 0; i < Sites.Length; i++)
            {
                int at = FileOffset(code, Sites[i]);
                if (at < 0 || at + 2 > arm9.Length) return null;
                now[i] = (ushort)(arm9[at] | arm9[at + 1] << 8);
            }
            return now;
        }

        /// <summary>HeartGold and SoulSilver, every language.</summary>
        public static bool SupportsCurrentRom => RomLocator.PartyPanelCodeSites != null;

        /// <summary>Null when the patch isn't in or something else changed the code.</summary>
        public static Size Read()
        {
            ushort[] now = ReadSites(RomLocator.PartyPanelCodeSites);
            if (now == null) return null;
            int Imm(Fn function, int offset) => now[Array.FindIndex(Sites, s => s.Function == function && s.Offset == offset)] & 0xFF;
            Size size = new Size(Imm(Fn.Draw, 0x044), Imm(Fn.Draw, 0x048), Imm(Fn.Draw, 0x082), Imm(Fn.Draw, 0x06E),
                                 Imm(Fn.Draw, 0x074), Imm(Fn.Draw, 0x088));
            if (WhyNotSize(size) != null) return null;
            for (int i = 0; i < Sites.Length; i++)
                if (now[i] != Sites[i].New(size)) return null;
            return size;
        }

        public static bool IsApplied => Read() != null;

        /// <summary>Why the patch can't go in, or null when it can.</summary>
        public static string WhyNotApplicable()
        {
            RomInfo.PartyPanelCodeSites code = RomLocator.PartyPanelCodeSites;
            if (code == null) return "Only HeartGold and SoulSilver have this patch.";
            ushort[] now = ReadSites(code);
            if (now == null) return "The ARM9 couldn't be read.";
            if (Read() != null) return "The patch is already applied.";
            for (int i = 0; i < Sites.Length; i++)
                if (now[i] != Sites[i].Old) return "Something else has already changed the party menu code, so DSPRE leaves it alone.";
            return null;
        }

        /// <summary>Why a size can't be used, or null when it can.</summary>
        public static string WhyNotSize(Size s)
        {
            if (s.Width < 1 || s.Height < 1) return "A panel needs at least one tile each way.";
            if (s.Width * s.Height > MaxArea) return $"A panel can have at most {MaxArea} tiles.";
            if (s.Height > MaxHeight) return $"A panel can be at most {MaxHeight} tiles tall.";
            if (s.StripLength < 1 || s.StripColumn + s.StripLength > s.Width || s.StripRow >= s.Height) return "The HP bar strip has to fit inside the panel.";
            if (s.StripTile is < 0 or > 0xFF) return "The HP bar strip's tile number has to be 255 or lower.";
            return null;
        }

        /// <summary>Writes the game's own size, so nothing changes until a size is set.</summary>
        public static void Apply()
        {
            if (WhyNotApplicable() is string why) throw new InvalidOperationException(why);
            WriteSites(Original);
        }

        public static void Write(Size size)
        {
            if (WhyNotSize(size) is string why) throw new ArgumentException(why);
            if (Read() == null) throw new InvalidOperationException("The panel size patch isn't applied.");
            WriteSites(size);
        }

        private static void WriteSites(Size size)
        {
            RomInfo.PartyPanelCodeSites code = RomLocator.PartyPanelCodeSites;
            foreach (Site site in Sites)
            {
                ushort value = site.New(size);
                DSUtils.WriteToFile(RomInfo.Arm9Path, new[] { (byte)value, (byte)(value >> 8) }, (uint)FileOffset(code, site));
            }
        }

        /// <summary>For undo.</summary>
        public static byte[] ToBytes(Size s) => new[]
        {
            (byte)s.Width, (byte)s.Height, (byte)s.StripColumn, (byte)s.StripRow, (byte)s.StripLength, (byte)s.StripTile,
        };

        public static Size FromBytes(byte[] b) => b == null || b.Length < 6 ? null : new Size(b[0], b[1], b[2], b[3], b[4], b[5]);
    }
}
