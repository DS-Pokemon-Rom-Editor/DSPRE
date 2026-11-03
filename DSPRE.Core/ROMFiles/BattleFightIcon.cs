using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>How the Pokémon icon beside FIGHT is drawn.</summary>
    public sealed record IconLook(int Percent, int Degrees, bool FlipAcross, bool FlipDown)
    {
        public const int MinPercent = 25, MaxPercent = 200;
    }

    /// <summary>What a preview needs to draw the icon.</summary>
    public sealed record FightIconView(int X, int Y, IconLook Look);

    /// <summary>
    /// New code for the Pokémon icon beside FIGHT, with its spots, size and angle built in. It fits in the old code's space, and a
    /// negative scale flips the sprite.
    /// </summary>
    public static class BattleFightIcon
    {
        /// <summary>Length of the code that gets replaced. It starts at the label lookup and stops before the icon's position call.</summary>
        public const int BlockBytes = 0x3E;
        public const int FirstX = 0x1C, FirstY = 0x1E, SecondX = 0x24, SecondY = 0x26;

        private const int AngleAt = 0x14, MarkAt = 0x2E, TypeCheckAt = 0x20, ScaleXAt = 0x32, ScaleYAt = 0x36;
        private const ushort VanillaStart = 0x204D, VanillaNext = 0x0100, TypeCheck = 0x2F04, OverLiterals = 0xE006, Nop = 0x46C0;
        private const int VanillaTypeCheckAt = 0xE, DoubleSize = 2, IconHalf = 16, LabelRise = 8;

        public static bool SupportsCurrentRom => RomLocator.BattleCommandSites != null && (GameFamily == GameFamilies.Plat || GameFamily == GameFamilies.HGSS)
            && GameLanguage != GameLanguages.Japanese && !IsHGE;

        private static RomLocator.FightIconCode Code => RomLocator.FightIconDistances(GameFamily == GameFamilies.HGSS);

        /// <summary>Where the code starts in the overlay.</summary>
        public static int Block => (int)RomLocator.BattleCommandSites.TargetGrid + Code.Block;

        private static ushort At(byte[] d, int at) => BitConverter.ToUInt16(d, at);

        /// <summary>True when the block starting at <paramref name="at"/> is this patch's code.</summary>
        public static bool Holds(byte[] d, int at) => at >= 0 && At(d, at + MarkAt) == OverLiterals && At(d, at + TypeCheckAt) == TypeCheck;

        private static byte[] ReadOverlay()
        {
            int overlay = RomLocator.BattleCommandSites.Overlay;
            if (OverlayUtils.IsCompressed(overlay)) OverlayUtils.Decompress(overlay);
            return File.ReadAllBytes(OverlayUtils.GetPath(overlay));
        }

        public static bool IsApplied
        {
            get
            {
                if (!SupportsCurrentRom) return false;
                try
                {
                    int overlay = RomLocator.BattleCommandSites.Overlay;
                    if (OverlayUtils.IsStillCompressed(overlay)) return false;
                    byte[] file = File.ReadAllBytes(OverlayUtils.GetPath(overlay));
                    return Block + BlockBytes <= file.Length && Holds(file, Block);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { return false; }
            }
        }

        // The tail of the block is a bl, and the literals need a word boundary at an offset the code assumes.
        private static bool IsVanilla(byte[] d, int b) =>
            b % 4 == 2 && b + BlockBytes + 4 <= d.Length && At(d, b) == VanillaStart && At(d, b + 2) == VanillaNext && At(d, b + VanillaTypeCheckAt) == TypeCheck
            && At(d, b + BlockBytes) >> 11 == 0x1E && At(d, b + BlockBytes + 2) >> 11 == 0x1F;

        public static string WhyNotApplicable()
        {
            if (!SupportsCurrentRom) return "This ROM isn't supported.";
            return IsVanilla(ReadOverlay(), Block) ? null : "Something else already changed how the FIGHT icon is placed, so this patch can't be applied.";
        }

        // Both offsets count from the block's start.
        private static (ushort High, ushort Low) Call(int at, int target)
        {
            int distance = target - (at + 4);
            return ((ushort)(0xF000 | distance >> 12 & 0x7FF), (ushort)(0xF800 | distance >> 1 & 0x7FF));
        }

        private static int Called(ushort high, ushort low)
        {
            int distance = (high & 0x7FF) << 12 | (low & 0x7FF) << 1;
            return (distance & 0x400000) != 0 ? distance - 0x800000 : distance;
        }

        /// <summary><paramref name="position"/> is where the placing call lands, counted from the block's start.</summary>
        private static byte[] Build(RomLocator.FightIconCode code, int position, (int X, int Y) first, (int X, int Y) second)
        {
            ushort sprite = (ushort)(0x1C00 | code.Sprite << 3);    // add r0, sprite, #0
            ushort[] words = new ushort[BlockBytes / 2];
            int pen = 0;
            void Put(params ushort[] w) { foreach (ushort word in w) words[pen++] = word; }
            void Bl(int target) { (ushort high, ushort low) = Call(pen * 2, position + target); Put(high, low); }

            ushort R1(int value) => ThumbImm8.Make(ThumbImm8.Op.Movs, 1, value);
            ushort R2(int value) => ThumbImm8.Make(ThumbImm8.Op.Movs, 2, value);
            Put(sprite, R1(DoubleSize));
            Bl(code.OverwriteMode);
            Put(sprite, 0x4909, 0x4A0A);                            // ldr r1, ldr r2: the scales below
            Bl(code.Scale);
            Put(sprite, R1(0), 0x0209);                             // movs r1, #angle; lsls r1, r1, #8
            Bl(code.Rotation);
            Put(R1(first.X), R2(first.Y), TypeCheck, 0xD101);
            Put(R1(second.X), R2(second.Y));
            Put(0x2311, 0x041B, sprite, OverLiterals, Nop);         // movs r3, #17; lsls r3, r3, #16: the subscreen offset
            byte[] bytes = new byte[BlockBytes];
            Buffer.BlockCopy(words, 0, bytes, 0, pen * 2);
            for (int i = pen * 2; i < BlockBytes; i += 2) BitConverter.TryWriteBytes(bytes.AsSpan(i), Nop);
            SetLook(bytes, 0, new IconLook(100, 0, false, false));
            return bytes;
        }

        // The icon sits left of the label's first letter, and right of its last in a double battle's second slot.
        private static int LabelWidth()
        {
            try
            {
                FieldFont font = FieldFont.LoadFromArchive(BattleTouchFontEntry);
                return font?.Measure(new TextArchive(BattleStringsTextNumber).Messages[BattleFightLine], FieldFontCharacters.GlyphFor) ?? 0;
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is InvalidDataException) { AppLogger.WarnOnce("BattleFightIcon.LabelWidth", ex); return 0; }
        }

        public static void Apply()
        {
            byte[] overlay = ReadOverlay();
            int block = Block, tail = block + BlockBytes, anchor = (int)RomLocator.BattleCommandSites.TargetGrid;
            RomLocator.BattleTextSpot label = RomLocator.BattleTextSpots(GameFamily == GameFamilies.HGSS).First();
            int x = overlay[anchor + label.X], y = overlay[anchor + label.Y], width = LabelWidth();
            int left = x - width / 2 - IconHalf, right = x - width / 2 + width + IconHalf, top = Math.Clamp(y - LabelRise, 0, 191);
            int position = BlockBytes + 4 + Called(At(overlay, tail), At(overlay, tail + 2));
            byte[] code = Build(Code, position, (Math.Clamp(left, 0, 255), top), (Math.Clamp(right, 0, 255), top));
            Array.Copy(code, 0, overlay, block, code.Length);
            DSUtils.WriteWholeFile(OverlayUtils.GetPath(RomLocator.BattleCommandSites.Overlay), overlay);
        }

        public static IconLook LookOf(byte[] d, int at)
        {
            float across = BitConverter.ToSingle(d, at + ScaleXAt), down = BitConverter.ToSingle(d, at + ScaleYAt);
            return new IconLook((int)Math.Round(Math.Abs(across) * 100), (int)Math.Round(d[at + AngleAt] * 360 / 256.0) % 360, across < 0, down < 0);
        }

        public static void SetLook(byte[] d, int at, IconLook look)
        {
            float scale = Math.Clamp(look.Percent, IconLook.MinPercent, IconLook.MaxPercent) / 100f;
            BitConverter.TryWriteBytes(d.AsSpan(at + ScaleXAt), look.FlipAcross ? -scale : scale);
            BitConverter.TryWriteBytes(d.AsSpan(at + ScaleYAt), look.FlipDown ? -scale : scale);
            d[at + AngleAt] = (byte)Math.Round(((look.Degrees % 360 + 360) % 360) * 256 / 360.0);
        }
    }
}
