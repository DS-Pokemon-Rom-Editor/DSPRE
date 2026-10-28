using System;
using System.IO;
using System.Linq;
using System.Text;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Routes the place-name banner's slide steps through tables of where it sits and how faded or pixelated it is at each step.
    /// Source: Resources/ROMToolboxDB/BannerMotion/banner_motion.s.
    /// </summary>
    public static class BannerMotionPatch
    {
        public const string Marker = "BANNERMOTN1\0";

        public enum Direction { FromTop, FromBottom, InPlace }
        public enum Motion { Straight, Ease, Overshoot, Bounce }
        public enum Effect { None, Fade, Pixelate }

        public sealed record Style(Direction Direction, Motion Motion, Effect Effect);

        public static readonly Style Game = new Style(Direction.FromTop, Motion.Straight, Effect.None);

        private static readonly byte[] Code =
        {
            0xf0, 0xb5, 0x1c, 0x06, 0x24, 0x0e, 0x1e, 0xa5, 0x61, 0xa6, 0x67, 0x00, 0xf3, 0x5f, 0x2f, 0x19, 0x3e, 0x7c, 0x6f, 0x7a, 0x01, 0x2f,
            0x02, 0xd0, 0x02, 0x2f, 0x20, 0xd0, 0x2c, 0xe0, 0x80, 0x27, 0xff, 0x04, 0x50, 0x37, 0x00, 0x2e, 0x09, 0xd1, 0x2c, 0x7a, 0x00, 0x2c,
            0x24, 0xd0, 0xac, 0x88, 0x3c, 0x80, 0xec, 0x88, 0x7c, 0x80, 0x00, 0x24, 0x2c, 0x72, 0x1d, 0xe0, 0x2c, 0x7a, 0x00, 0x2c, 0x05, 0xd1,
            0x3c, 0x88, 0xac, 0x80, 0x7c, 0x88, 0xec, 0x80, 0x01, 0x24, 0x2c, 0x72, 0x6c, 0x89, 0x3c, 0x80, 0x10, 0x24, 0xa4, 0x1b, 0x36, 0x02,
            0x34, 0x43, 0x7c, 0x80, 0x0c, 0xe0, 0x80, 0x27, 0xff, 0x04, 0xfc, 0x89, 0x40, 0x25, 0xac, 0x43, 0x00, 0x2e, 0x00, 0xd0, 0x2c, 0x43,
            0xfc, 0x81, 0x34, 0x01, 0x34, 0x43, 0x4c, 0x37, 0x3c, 0x70, 0x01, 0xa5, 0x2f, 0x68, 0xb8, 0x47, 0xf0, 0xbd,
        };

        // The data holds the original call, the saved blend registers, a saved flag, the effect, the blend control and the style, then
        // a fade or mosaic level and a scroll for each offset 0 to 255.
        private const int DataAt = 0x80, CallAt = 0, EffectAt = 9, BlendAt = 10, StyleAt = 12, LevelsAt = 16, ScrollAt = 0x110;
        private const int Steps = 256, DataLength = ScrollAt + Steps * 2;
        private static int CodeStart => SyntheticOverlaySpace.HeaderSize;
        public static int BlockLength => CodeStart + DataAt + DataLength;

        // Blends BG3 over the 3D layer, the other backgrounds, sprites and the backdrop.
        private const ushort BlendControl = 0x3748;
        public const int ScreenHeight = 192;

        public static bool SupportsCurrentRom => RomLocator.PlaceNameBannerSites != null;

        private static RomInfo.PlaceNameBannerSites Sites => RomLocator.PlaceNameBannerSites;
        private static int[] Hooks => RomLocator.BannerDistances.SlideCalls.Select(d => (int)Sites.Task + d).ToArray();
        private static uint OverlayAddress => OverlayUtils.OverlayTable.GetRAMAddress(Sites.Overlay);

        private static byte[] ReadOverlay()
        {
            if (OverlayUtils.IsCompressed(Sites.Overlay)) OverlayUtils.Decompress(Sites.Overlay);
            return File.ReadAllBytes(OverlayUtils.GetPath(Sites.Overlay));
        }

        private static uint? BlTarget(byte[] d, int at)
        {
            if (at < 0 || at + 4 > d.Length) return null;
            ushort hi = BitConverter.ToUInt16(d, at), lo = BitConverter.ToUInt16(d, at + 2);
            if ((hi & 0xF800) != 0xF000 || (lo & 0xF800) != 0xF800) return null;
            int off = ((hi & 0x7FF) << 12) | ((lo & 0x7FF) << 1);
            if ((off & 0x400000) != 0) off -= 0x800000;
            return (uint)(OverlayAddress + at + 4 + off);
        }

        private static byte[] Bl(int at, uint to)
        {
            int off = (int)(to - (OverlayAddress + (uint)at + 4));
            if (off < -0x400000 || off >= 0x400000) throw new InvalidOperationException("The synthetic overlay is out of the banner code's reach.");
            ushort hi = (ushort)(0xF000 | ((off >> 12) & 0x7FF)), lo = (ushort)(0xF800 | ((off >> 1) & 0x7FF));
            return BitConverter.GetBytes(hi).Concat(BitConverter.GetBytes(lo)).ToArray();
        }

        private static long BlockOffset(uint code) => (long)code - RomInfo.SynthOverlayLoadAddress - CodeStart;

        /// <summary>Where the block starts in the synthetic overlay when both steps call it, or -1.</summary>
        private static long PatchedBlock()
        {
            if (!SupportsCurrentRom || !File.Exists(Filesystem.ExpArmPath)) return -1;
            try
            {
                byte[] ov = ReadOverlay();
                if (BlTarget(ov, Hooks[0]) is not uint code || BlTarget(ov, Hooks[1]) != code) return -1;
                long block = BlockOffset(code);
                if (block < 0 || block + BlockLength > new FileInfo(Filesystem.ExpArmPath).Length) return -1;
                return Encoding.ASCII.GetString(DSUtils.ReadFromFile(Filesystem.ExpArmPath, (uint)block, Marker.Length)) == Marker ? block : -1;
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException) { return -1; }
        }

        public static bool IsApplied => PatchedBlock() >= 0;

        public static string WhyNotApplicable()
        {
            if (!SupportsCurrentRom) return "This patch doesn't support this ROM yet.";
            if (!SyntheticOverlaySpace.Available()) return "Apply the ARM9 expansion first.";
            byte[] ov = ReadOverlay();
            uint? first = BlTarget(ov, Hooks[0]);
            return first != null && BlTarget(ov, Hooks[1]) == first ? null : "Something else has already changed the banner code, so DSPRE leaves it alone.";
        }

        /// <summary>Writes the routine into a marked block at <paramref name="blockOffset"/> and points both slide steps at it.</summary>
        public static void Apply(int blockOffset)
        {
            if (blockOffset % 4 != 0) throw new ArgumentException("The block must be word aligned.", nameof(blockOffset));
            byte[] ov = ReadOverlay();
            uint call = BlTarget(ov, Hooks[0]) ?? throw new InvalidOperationException("The banner's slide call wasn't found.");
            uint code = RomInfo.SynthOverlayLoadAddress + (uint)blockOffset + (uint)CodeStart;

            byte[] block = new byte[BlockLength];
            SyntheticOverlaySpace.Header(Marker, BlockLength).CopyTo(block, 0);
            Code.CopyTo(block, CodeStart);
            int data = CodeStart + DataAt;
            BitConverter.GetBytes(call | 1).CopyTo(block, data + CallAt);
            BitConverter.GetBytes(BlendControl).CopyTo(block, data + BlendAt);
            DSUtils.WriteToFile(Filesystem.ExpArmPath, block, (uint)blockOffset);

            string path = OverlayUtils.GetPath(Sites.Overlay);
            foreach (int hook in Hooks) DSUtils.WriteToFile(path, Bl(hook, code), (uint)hook);
            PlaceNameBanner banner = PlaceNameBanner.Read();
            if (banner != null) WriteStyle(Game, banner.Current, banner.HiddenScroll(banner.Current));
        }

        /// <summary>The style the ROM's tables were last written for, or null without the patch.</summary>
        public static Style Read()
        {
            long block = PatchedBlock();
            if (block < 0) return null;
            byte[] s = DSUtils.ReadFromFile(Filesystem.ExpArmPath, (uint)(block + CodeStart + DataAt + StyleAt), 3);
            return new Style((Direction)Math.Min((int)s[0], 2), (Motion)Math.Min((int)s[1], 3), (Effect)Math.Min((int)s[2], 2));
        }

        /// <summary>Rewrites the tables for a style and the banner's place and size; does nothing without the patch.</summary>
        public static void WriteStyle(Style style, PlaceNameBanner.Look look, int hidden)
        {
            long block = PatchedBlock();
            if (block < 0) return;
            byte[] data = new byte[DataLength - EffectAt];
            data[0] = (byte)style.Effect;
            BitConverter.GetBytes(BlendControl).CopyTo(data, BlendAt - EffectAt);
            data[StyleAt - EffectAt] = (byte)style.Direction;
            data[StyleAt - EffectAt + 1] = (byte)style.Motion;
            data[StyleAt - EffectAt + 2] = (byte)style.Effect;
            for (int v = 0; v < Steps; v++)
            {
                (int scroll, int level) = Step(style, look, hidden, v);
                data[LevelsAt - EffectAt + v] = (byte)level;
                BitConverter.GetBytes((short)scroll).CopyTo(data, ScrollAt - EffectAt + v * 2);
            }
            DSUtils.WriteToFile(Filesystem.ExpArmPath, data, (uint)(block + CodeStart + DataAt + EffectAt));
        }

        /// <summary>The layer's scroll (up when positive) and the fade or mosaic level (0 is fully shown) at one of the game's offsets.</summary>
        public static (int Scroll, int Level) Step(Style style, PlaceNameBanner.Look look, int hidden, int offset)
        {
            int below = ScreenHeight - 8 * look.Down;
            if (hidden <= 0 || offset <= 0) return (0, 0);
            // At the hidden offset the banner is scrolled away and the registers go back to how they were.
            if (offset >= hidden) return (style.Direction == Direction.FromBottom ? -below : hidden, 0);
            double f = (double)offset / hidden, p = Curve(style.Motion, f);
            int scroll = style.Direction switch
            {
                Direction.FromTop => (int)Math.Round(p * hidden),
                Direction.FromBottom => -(int)Math.Round(p * below),
                _ => 0,
            };
            int level = style.Effect switch
            {
                Effect.Fade => Math.Clamp((int)Math.Round(16 * f), 0, 16),
                Effect.Pixelate => Math.Clamp((int)Math.Round(15 * f), 0, 15),
                _ => 0,
            };
            return (scroll, level);
        }

        // How far out the banner is at f: 0 is shown, 1 is out of sight. Sliding in plays it backwards.
        private static double Curve(Motion motion, double f) => motion switch
        {
            Motion.Ease => f * f,
            Motion.Overshoot => 2.70158 * f * f * f - 1.70158 * f * f,
            Motion.Bounce => 1 - BounceOut(1 - f),
            _ => f,
        };

        private static double BounceOut(double t)
        {
            const double n = 7.5625, d = 2.75;
            if (t < 1 / d) return n * t * t;
            if (t < 2 / d) { t -= 1.5 / d; return n * t * t + 0.75; }
            if (t < 2.5 / d) { t -= 2.25 / d; return n * t * t + 0.9375; }
            t -= 2.625 / d;
            return n * t * t + 0.984375;
        }
    }
}
