using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DSPRE.ROMFiles
{
    /// <summary>The Options screen patch: more text speeds, choices locked to one value, rows taken off the screen, and new-save defaults.</summary>
    public static class OptionsMenuPatch
    {
        public const string Marker = "OPTIONSMENU1";
        public const int BlockLength = 0x700, MaxSpeeds = 8;

        /// <summary>A pace below this is frames per letter. From here, the low bits are letters per frame.</summary>
        public const byte LettersFlag = 0x80;
        /// <summary>A page at a time.</summary>
        public const byte Page = 0xFE;

        public enum Row { TextSpeed, Sound, Scene, Style, Button, Frame }

        /// <summary>A row's name as the editor shows it.</summary>
        public static string Label(Row row) => row switch
        {
            Row.TextSpeed => "Text speed", Row.Sound => "Sound", Row.Scene => "Battle scene",
            Row.Style => "Battle style", Row.Button => "Button mode", _ => "Frame",
        };

        public sealed record Speed(string Name, byte Pace);

        /// <param name="Locks">Each row's locked choice, or null. Text speed is set by <paramref name="Speeds"/> instead.</param>
        /// <param name="Removed">Rows taken off the screen. A removed row other than Frame needs a lock.</param>
        /// <param name="Defaults">Each row's value in a new save; text speed's is the speed's place in the list.</param>
        public sealed record Settings(List<Speed> Speeds, int?[] Locks, bool[] Removed, int[] Defaults);

        // Block layout. Base, the payload's one literal, is the block + 0x30. HGSS reads its label, highlight and touch tables
        // from the copies here, so the Buttons tab keeps editing the overlay's own.
        private const int PacesAt = 0x20, LettersAt = 0x30, CountAt = 0x39, ArrowsAt = 0x3A, FirstLinesAt = 0x3C, SlotsAt = 0x44,
                          LocksAt = 0x4C, DefaultsAt = 0x54, MapMemberAt = 0x5C, CodeAt = 0x60, GettersAt = 0x1E0, TemplatesAt = 0x200,
                          SnapshotAt = 0x3C0, LabelsCopyAt = 0x540, HighlightsCopyAt = 0x570, TouchCopyAt = 0x5B0;

        // Assembled from options_menu.s.
        private static readonly byte[] Code =
        {
            0x00, 0xb5, 0x20, 0x1c, 0xff, 0xf7, 0xcc, 0xff, 0x59, 0x4a, 0x01, 0x21, 0xff, 0x2e, 0x0b, 0xd0,
            0x80, 0x2e, 0x09, 0xd3, 0x7f, 0x21, 0x31, 0x40, 0x01, 0x26, 0x23, 0x1c, 0x29, 0x33, 0x18, 0x78,
            0xc0, 0x09, 0xc0, 0x01, 0x01, 0x30, 0x18, 0x70, 0x11, 0x72, 0x00, 0xbd, 0x21, 0x1c, 0x2c, 0x31,
            0x08, 0x70, 0x08, 0x28, 0x03, 0xd2, 0x4e, 0x4a, 0x13, 0x7a, 0x11, 0x18, 0x0b, 0x70, 0x70, 0x47,
            0x60, 0xb5, 0x20, 0x1c, 0x2c, 0x30, 0x00, 0x78, 0x01, 0x25, 0x08, 0x28, 0x05, 0xd2, 0x48, 0x49,
            0x09, 0x18, 0x0d, 0x78, 0x00, 0x2d, 0x00, 0xd1, 0x01, 0x25, 0x7e, 0x2d, 0x00, 0xd1, 0x2d, 0x01,
            0x00, 0x26, 0x20, 0x1c, 0xff, 0xf7, 0x9c, 0xff, 0x00, 0x28, 0x03, 0xd1, 0x01, 0x26, 0x01, 0x3d,
            0xf7, 0xd1, 0x60, 0xbd, 0x00, 0x2e, 0x04, 0xd0, 0x01, 0xb4, 0x60, 0x68, 0xff, 0xf7, 0x90, 0xff,
            0x01, 0xbc, 0x60, 0xbd, 0x00, 0x20, 0x29, 0x1c, 0x5c, 0x31, 0x0a, 0x88, 0x4b, 0x88, 0x00, 0x2a,
            0x03, 0xd0, 0x93, 0x42, 0x01, 0xd3, 0x53, 0x1e, 0x4b, 0x80, 0x54, 0x31, 0x01, 0x30, 0x07, 0x28,
            0xf3, 0xd3, 0x02, 0xb0, 0xf8, 0xbd, 0x00, 0x20, 0x31, 0x1c, 0x84, 0x31, 0x0a, 0x88, 0x4b, 0x88,
            0x00, 0x2a, 0x03, 0xd0, 0x93, 0x42, 0x01, 0xd3, 0x53, 0x1e, 0x4b, 0x80, 0x54, 0x31, 0x01, 0x30,
            0x06, 0x28, 0xf3, 0xd3, 0x02, 0xb0, 0xf8, 0xbd, 0x54, 0x20, 0x05, 0x2f, 0x04, 0xd0, 0x00, 0x2f,
            0x02, 0xd1, 0x27, 0x49, 0x89, 0x7a, 0x01, 0x29, 0x70, 0x47, 0x02, 0x1c, 0x05, 0x28, 0x04, 0xd0,
            0x00, 0x28, 0x02, 0xd1, 0x22, 0x49, 0x89, 0x7a, 0x01, 0x29, 0x70, 0x47, 0x20, 0x1c, 0x20, 0x4b,
            0x1b, 0x18, 0x18, 0x7d, 0xff, 0x28, 0x01, 0xd1, 0xc0, 0x20, 0x00, 0xe0, 0x00, 0x01, 0x00, 0x90,
            0x70, 0x47, 0x38, 0x1c, 0x1a, 0x4b, 0x1b, 0x18, 0x18, 0x7d, 0xff, 0x28, 0x01, 0xd1, 0xc0, 0x20,
            0x00, 0xe0, 0x00, 0x01, 0x05, 0x90, 0x70, 0x47, 0x15, 0x48, 0xc0, 0x18, 0x03, 0x7d, 0x1b, 0x01,
            0x18, 0x33, 0x70, 0x47, 0x06, 0x22, 0x00, 0xe0, 0x01, 0x22, 0x11, 0x4b, 0x07, 0x28, 0x01, 0xd3,
            0x07, 0x38, 0xfb, 0xe7, 0x19, 0x18, 0x09, 0x7d, 0xff, 0x29, 0x01, 0xd1, 0x80, 0x18, 0xf5, 0xe7,
            0x01, 0x1c, 0x70, 0x47, 0x20, 0x1c, 0x00, 0xe0, 0x30, 0x1c, 0x09, 0x4b, 0x1b, 0x18, 0x18, 0x7d,
            0xff, 0x28, 0x03, 0xd0, 0x18, 0x23, 0x58, 0x43, 0x05, 0x30, 0x70, 0x47, 0xc0, 0x20, 0x70, 0x47,
            0xb0, 0x6a, 0x01, 0x9a, 0x02, 0x4b, 0x9b, 0x18, 0x19, 0x7b, 0x09, 0x19, 0x70, 0x47, 0xc0, 0x46,
            0x00, 0x00, 0x00, 0x00,
        };
        // Block offsets of the routines, the calls DSPRE points at the game, the parts it fills per game, and Base.
        private const int AddPrinterAt = 0x60, PrinterIdAt = 0x8C, RenderAt = 0xA0, PtTailAt = 0xE4, HgTailAt = 0x106,
                          PtArrowsAt = 0x128, JpArrowsAt = 0x13A, PtLabelAt = 0x14C, PtChoiceAt = 0x162, PtScrollAt = 0x178,
                          UpAt = 0x184, DownAt = 0x188, HgLabelAt = 0x1A4, HgChoiceAt = 0x1A8, HgRowYAt = 0x1AA, HgLinesAt = 0x1C0;
        private const int ZeroCall = 0x64, RenderCall = 0xC4, CopyCall = 0xDC, RowCmp5 = 0x12A, RowCmp0 = 0x12E,
                          PtLabelStr = 0x15E, PtChoiceStr = 0x174, BaseLiteral = 0x1D0;

        // The game's own bytes at each hook, before the patch.
        private static readonly byte[] GameIdStore = { 0x21, 0x1C, 0x2C, 0x31, 0x08, 0x70 };
        private static readonly byte[] GameDelayTail =
            { 0x00, 0x28, 0x01, 0xD1, 0x08, 0x20, 0x08, 0xBD, 0x01, 0x28, 0x01, 0xD1, 0x04, 0x20, 0x08, 0xBD, 0x01, 0x20, 0x08, 0xBD };
        private static readonly byte[] GameTail = { 0x02, 0xB0, 0xF8, 0xBD };
        private static readonly byte[] GameScroll = { 0x1B, 0x01, 0x18, 0x33 };
        // Options_Init: textSpeed = 1 and the other fields cleared, one ldrh/bic/orr/strh after another.
        private static readonly byte[] GameInit = { 0x21, 0x88, 0x0F, 0x20, 0x81, 0x43, 0x01, 0x20, 0x08, 0x43, 0x20, 0x80, 0x21, 0x88, 0x30, 0x20 };
        private const int DelayLength = 26, GetterLength = 8, InitLength = 0x34, InitLiteral = 0x38;

        /// <summary>Slow, Mid and Fast, as the game ships them.</summary>
        public static readonly byte[] GamePaces = { 8, 4, 1 };
        /// <summary>The game's new-save values by row: Mid, Stereo, On, Shift, Normal, frame 1.</summary>
        public static readonly int[] GameDefaults = { 1, 0, 0, 0, 0, 0 };

        private static bool Hg => RomInfo.GameFamily == RomInfo.GameFamilies.HGSS;
        private static bool Japanese => RomInfo.GameLanguage == RomInfo.GameLanguages.Japanese;

        // The game's row of each option, its choice count, and HGSS's first text line per row.
        private static int GameRow(Row row) => Hg ? row switch { Row.Scene => 1, Row.Style => 2, Row.Sound => 3, _ => (int)row } : (int)row;
        public static int ChoiceCount(Row row) => row switch { Row.TextSpeed => 3, Row.Frame => 20, Row.Button => Hg ? 2 : 3, _ => 2 };
        private static readonly byte[] HgFirstLines = { 9, 12, 14, 16, 18, 20 };
        // The getter for each lockable row, as listed in RomLocator.OptionsGetters.
        private static int Getter(Row row) => row switch { Row.Sound => 0, Row.Style => 1, Row.Scene => 2, Row.Button => 3, _ => -1 };
        public static bool Lockable(Row row) => Getter(row) >= 0;
        // Each row's place in the saved options halfword.
        private static int Shift(Row row) => row switch { Row.TextSpeed => 0, Row.Sound => 4, Row.Style => 6, Row.Scene => 7, Row.Button => 8, _ => 10 };

        // Japanese is measured but left out: the Screens editor is closed for Japanese ROMs.
        public static bool Supports => !Japanese && RomLocator.Where(RomLocator.Thing.OptionsTextPrinter) != null && RomLocator.Where(RomLocator.Thing.OptionsGetters) != null
                                       && RomLocator.Where(RomLocator.Thing.OptionsInit) != null
                                       && (Hg ? RomLocator.Where(RomLocator.Thing.OptionsMenuCodeHgss) != null && RomLocator.HgssOptionsSites != null
                                                && RomLocator.Where(RomLocator.Thing.OptionsTablesHgss) != null
                                              : RomLocator.Where(RomLocator.Thing.OptionsMenuCode) != null);

        private static RomLocator.Spot Printer => RomLocator.Where(RomLocator.Thing.OptionsTextPrinter);
        private static RomLocator.Spot Getters => RomLocator.Where(RomLocator.Thing.OptionsGetters);
        private static uint Init => (uint)RomLocator.Where(RomLocator.Thing.OptionsInit).Offset;
        private static RomLocator.Spot Menu => RomLocator.Where(Hg ? RomLocator.Thing.OptionsMenuCodeHgss : RomLocator.Thing.OptionsMenuCode);
        private static int[] Tables => RomLocator.Where(RomLocator.Thing.OptionsTablesHgss).Offsets;
        private static uint Delay => (uint)Printer.Offsets[3];
        private static uint MenuAddress(int at) => RomLocator.RamBase(Menu) + (uint)at;
        private static string MenuPath => OverlayUtils.GetPath(Menu.Overlay);

        /// <summary>The Options screen's overlay.</summary>
        public static int MenuOverlay => Menu.Overlay;
        private static byte[] ReadMenu() => RomLocator.ReadFile(Hg ? RomLocator.Thing.OptionsMenuCodeHgss : RomLocator.Thing.OptionsMenuCode);

        /// <summary>The text archive with the Options screen's words.</summary>
        public static int TextBankId => RomInfo.TextBank(Hg ? 45 : 220);

        /// <summary>Platinum's description of the text speed row, in <see cref="TextBankId"/>. HGSS has none.</summary>
        public static int SpeedDescriptionLine => Hg ? -1 : 43;

        // Platinum: counts, first lines and x offsets. HGSS: counts and border y.
        private static int Counts => Hg ? Menu.Offsets[10] : Menu.Offsets[8];
        private static int PtFirstLines => Menu.Offsets[9];
        private static int PtXOffsets => Menu.Offsets[10];
        private static int HgBorder => Menu.Offsets[11];

        /// <summary>Where the block is in the synthetic overlay when the patch is in, or -1.</summary>
        public static long BlockOffset()
        {
            if (!Supports || !File.Exists(Filesystem.ExpArmPath)) return -1;
            try
            {
                byte[] f = ARM9.ReadBytes(Delay, DelayLength);
                if (f[7] != 0x49) return -1;
                uint start = ARM9.Address + Delay;
                int literal = (int)(((start + 10) & ~3u) + f[6] * 4u - start);
                if (literal + 4 > DelayLength) return -1;
                long block = (long)BitConverter.ToUInt32(f, literal) - RomInfo.SynthOverlayLoadAddress - PacesAt;
                if (block < 0 || block + BlockLength > new FileInfo(Filesystem.ExpArmPath).Length) return -1;
                return Encoding.ASCII.GetString(DSUtils.ReadFromFile(Filesystem.ExpArmPath, (uint)block, Marker.Length)) == Marker ? block : -1;
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException) { return -1; }
        }

        public static bool IsApplied => BlockOffset() >= 0;

        /// <summary>Where Platinum's cursor bar routine keeps its adds r3, #24 in the synthetic overlay, or -1 when the patch isn't in.</summary>
        public static long PtScrollStepAt() => !Hg && BlockOffset() is long block and >= 0 ? block + PtScrollAt + 8 : -1;

        /// <summary>Why the patch can't go in now, or null.</summary>
        public static string WhyNotApplicable()
        {
            if (!Supports) return "This patch doesn't support this ROM yet.";
            if (!SyntheticOverlaySpace.Available()) return "Apply the ARM9 expansion first.";
            if (IsApplied) return "The patch is already applied.";
            return GameCode() == null ? "Another patch already changed the text speed or the Options screen." : null;
        }

        private static bool Is(byte[] data, int at, params byte[] expected) =>
            at >= 0 && at + expected.Length <= data.Length && data.AsSpan(at, expected.Length).SequenceEqual(expected);

        // The game's routines the payload calls, or null when any hook site isn't as the game ships it.
        private static (uint Zero, uint Render, uint Copy)? GameCode()
        {
            int[] at = Printer.Offsets;
            byte[] add = ARM9.ReadBytes((uint)at[0], 6), id = ARM9.ReadBytes((uint)at[1], 6), task = ARM9.ReadBytes((uint)at[2], 0x1A);
            byte[] delay = ARM9.ReadBytes(Delay, DelayLength);
            if (!Is(add, 0, 0x20, 0x1C) || !Is(task, 0, 0x20, 0x1C) || !id.SequenceEqual(GameIdStore)) return null;
            if (!Is(delay, 0, 0x08, 0xB5) || !Is(delay, 6, GameDelayTail)) return null;
            if (ThumbBl.Target(add, 2, ARM9.Address + (uint)at[0] + 2) is not uint zero) return null;
            if (ThumbBl.Target(task, 2, ARM9.Address + (uint)at[2] + 2) is not uint render) return null;
            // The task's own window copy: ldr r0, [r4, #4]; bl Window_CopyToVRAM.
            if (!Is(task, 0x14, 0x60, 0x68) || ThumbBl.Target(task, 0x16, ARM9.Address + (uint)at[2] + 0x16) is not uint copy) return null;
            foreach (int g in Getters.Offsets)
            {
                byte[] b = ARM9.ReadBytes((uint)g, GetterLength);
                if (!Is(b, 0, 0x00, 0x88) || !Is(b, 6, 0x70, 0x47)) return null;
            }
            byte[] init = ARM9.ReadBytes(Init, InitLiteral + 8);
            if (!Is(init, 0, GameInit) || !Is(init, InitLength, 0x10, 0xBD) || !Is(init, InitLiteral, 0xFF, 0xFC, 0xFF, 0xFF)) return null;
            return MenuIsGame(ReadMenu()) ? (zero, render, copy) : null;
        }

        private static bool MenuIsGame(byte[] m)
        {
            int[] o = Menu.Offsets;
            if (!Hg)
            {
                // lsls r0, rX, #4 then str r0, [sp, #N], for the label and the choice y.
                bool YThenStore(int at) => m[at + 1] == 0x01 && (m[at] & 0xC7) == 0 && m[at + 3] == 0x90;
                return Is(m, o[0], GameTail) && m[o[1]] == 0x05 && (m[o[1] + 1] & 0xF8) == 0x28 && Is(m, o[1] + 3, 0xD1, 0x54, 0x20)
                    && YThenStore(o[2]) && YThenStore(o[3]) && CallsRemainder(m, o[4]) && CallsRemainder(m, o[5])
                    && Is(m, o[6], GameScroll) && Is(m, o[7], GameScroll);
            }
            RomInfo.HgssOptionsSites h = RomLocator.HgssOptionsSites;
            int[] t = Tables;
            bool Points(int literal, long table) => literal > 0 && BitConverter.ToUInt32(m, literal) == MenuAddress((int)table);
            bool tables = (h.Labels == 0 || Points(t[0], h.Labels)) && Points(t[1], h.Highlights) && Points(t[2], h.Touch)
                          && Points(t[3], h.Touch + 0x40) && Points(t[4], h.Touch + 0x44) && Is(m, t[5], 0x11, 0x21);
            bool choice = Japanese ? Is(m, o[3], 0x02, 0x1C, 0x4A, 0x43, 0x50, 0x1D) : Is(m, o[3], 0x30, 0x1C, 0x48, 0x43, 0x40, 0x1D);
            bool frame = !Japanese || (Is(m, o[6], 0x05, 0x28) && m[o[6] + 3] == 0xD1 && Is(m, o[6] + 4, 0x02, 0x1C));
            return tables && Is(m, o[0], 0xB0, 0x6A, 0x09, 0x31) && Is(m, o[1], GameTail) && Is(m, o[2], 0x18, 0x20, 0x60, 0x43, 0x40, 0x1D)
                && choice && frame && CallsRemainder(m, o[4]) && CallsRemainder(m, o[5]) && Is(m, o[7], 0x09, 0x22) && Is(m, o[8], 0x09, 0x2C)
                && Points(o[9], h.Sprites);
        }

        // A blx to the remainder routine, where the cursor wraps.
        private static bool CallsRemainder(byte[] m, int at) => at + 4 <= m.Length && (m[at + 1] & 0xF8) == 0xF0 && (m[at + 3] & 0xF8) == 0xE8;

        /// <summary>Writes the block at <paramref name="blockOffset"/> with the game's own settings and hooks the game to it.</summary>
        public static void Apply(int blockOffset)
        {
            if (WhyNotApplicable() is string why) throw new InvalidOperationException(why);
            (uint zero, uint render, uint copy) = GameCode().Value;
            uint block = RomInfo.SynthOverlayLoadAddress + (uint)blockOffset;
            byte[] menu = ReadMenu();
            int[] o = Menu.Offsets;

            byte[] data = new byte[BlockLength];
            SyntheticOverlaySpace.Header(Marker, BlockLength).CopyTo(data, 0);
            Code.CopyTo(data, CodeAt);
            ThumbBl.Make(block + ZeroCall, zero).CopyTo(data, ZeroCall);
            ThumbBl.Make(block + RenderCall, render).CopyTo(data, RenderCall);
            ThumbBl.Make(block + CopyCall, copy).CopyTo(data, CopyCall);
            BitConverter.GetBytes(block + LettersAt).CopyTo(data, BaseLiteral);
            PaceTable(GamePaces).CopyTo(data, PacesAt);
            data[CountAt] = (byte)GamePaces.Length;
            for (int i = 0; i < 8; i++) { data[SlotsAt + i] = (byte)i; data[LocksAt + i] = 0xFF; }
            for (int i = 0; i < GameDefaults.Length; i++) data[DefaultsAt + i] = (byte)GameDefaults[i];
            for (int g = 0; g < 4; g++) ARM9.ReadBytes((uint)Getters.Offsets[g], GetterLength).CopyTo(data, GettersAt + g * GetterLength);
            Snapshot(menu).CopyTo(data, SnapshotAt);
            if (Hg)
            {
                HgFirstLines.CopyTo(data, FirstLinesAt);
                // The game's screen 17 is left to the Buttons tab; the game shows a copy built from it in a new member.
                DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.OptionsGraphics });
                int member = ArchiveMembers.Count(RomInfo.DirNames.OptionsGraphics);
                data[MapMemberAt] = (byte)member;
                ArchiveMembers.Put(RomInfo.DirNames.OptionsGraphics, member, File.ReadAllBytes(ArchiveMembers.PathOf(RomInfo.DirNames.OptionsGraphics, 17)));
                DeriveHg(data, menu, Enumerable.Range(0, 7).ToArray(), Enumerable.Repeat(-1, 6).ToArray(), false);
            }
            else
            {
                // Each routine reads the row from the game's own register, and the game's own str ends it.
                data[RowCmp5 + 1] = data[RowCmp0 + 1] = (byte)(0x28 | (menu[o[1] + 1] & 7));
                data[PtLabelAt] = (byte)(menu[o[2]] & 0x38);
                data[PtChoiceAt] = (byte)(menu[o[3]] & 0x38);
                Array.Copy(menu, o[2] + 2, data, PtLabelStr, 2);
                Array.Copy(menu, o[3] + 2, data, PtChoiceStr, 2);
            }
            DSUtils.WriteToFile(Filesystem.ExpArmPath, data, (uint)blockOffset);

            int[] at = Printer.Offsets;
            ARM9.WriteBytes(Hook(ARM9.Address + (uint)at[0], block + AddPrinterAt), (uint)at[0]);
            ARM9.WriteBytes(Hook(ARM9.Address + (uint)at[1], block + PrinterIdAt), (uint)at[1]);
            ARM9.WriteBytes(Hook(ARM9.Address + (uint)at[2], block + RenderAt), (uint)at[2]);
            ARM9.WriteBytes(DelayFunction(ARM9.ReadBytes(Delay, DelayLength), block + PacesAt), Delay);
            WriteDefaults(GameDefaults);

            if (Hg)
            {
                Put(Call(o[0], block + HgLinesAt), o[0]);
                Put(Call(o[1], block + HgTailAt), o[1]);
                Put(Hook(MenuAddress(o[2]), block + HgLabelAt), o[2]);
                Put(Hook(MenuAddress(o[3]), block + (uint)(Japanese ? HgRowYAt : HgChoiceAt)), o[3]);
                Put(Call(o[4], block + UpAt), o[4]);
                Put(Call(o[5], block + DownAt), o[5]);
                if (Japanese) Put(CheckHook(menu, o[6], block + JpArrowsAt), o[6]);
                // Two more sprites, the text speed row's arrows, from a template table in the block.
                Put(new byte[] { 11 }, o[7]);
                Put(new byte[] { 11 }, o[8]);
                Put(BitConverter.GetBytes(block + TemplatesAt), o[9]);
                // The label, highlight and touch tables and screen 17 come from the patch's copies.
                int[] t = Tables;
                if (t[0] > 0) Put(BitConverter.GetBytes(block + LabelsCopyAt), t[0]);
                Put(BitConverter.GetBytes(block + HighlightsCopyAt), t[1]);
                Put(BitConverter.GetBytes(block + TouchCopyAt), t[2]);
                Put(BitConverter.GetBytes(block + TouchCopyAt + 0x40), t[3]);
                Put(BitConverter.GetBytes(block + TouchCopyAt + 0x44), t[4]);
                Put(new byte[] { data[MapMemberAt] }, t[5]);
            }
            else
            {
                Put(Call(o[0], block + PtTailAt), o[0]);
                Put(CheckHook(menu, o[1], block + PtArrowsAt), o[1]);
                Put(Call(o[2], block + PtLabelAt), o[2]);
                Put(Call(o[3], block + PtChoiceAt), o[3]);
                Put(Call(o[4], block + UpAt), o[4]);
                Put(Call(o[5], block + DownAt), o[5]);
                Put(Call(o[6], block + PtScrollAt), o[6]);
                Put(Call(o[7], block + PtScrollAt), o[7]);
            }
            PlaceSpeedNames();
        }

        // The speed names get eight lines of their own at the end of the Options text, starting as the game's three.
        private static void PlaceSpeedNames()
        {
            long block = BlockOffset();
            if (block < 0) return;
            byte[] data = DSUtils.ReadFromFile(Filesystem.ExpArmPath, (uint)block, BlockLength);
            int now = SpeedNamesFirstLine(data), game = Hg ? HgFirstLines[0] : data[SnapshotAt + SnapPtFirst];
            if (now != game) return;
            TextArchive bank = new TextArchive(TextBankId);
            int first = bank.Messages.Count;
            if (first + MaxSpeeds > 0x100) throw new InvalidOperationException("The Options text has no room for the speed names.");
            bank.Messages.AddRange(Enumerable.Range(0, MaxSpeeds).Select(i => i < GamePaces.Length ? bank.Messages[game + i] : ""));
            bank.SaveToExpandedDir(TextBankId, false);
            if (Hg) DSUtils.WriteToFile(Filesystem.ExpArmPath, new[] { (byte)first }, (uint)(block + FirstLinesAt));
            else Put(new[] { (byte)first }, PtFirstLines);
        }

        private static void Put(byte[] bytes, int at) => DSUtils.WriteToFile(MenuPath, bytes, (uint)at);
        private static byte[] Call(int at, uint to) => ThumbBl.Make(MenuAddress(at), to);

        // A bl to the routine, then a nop over the rest of the six bytes it replaces.
        private static byte[] Hook(uint from, uint to) => ThumbBl.Make(from, to).Concat(new byte[] { 0xC0, 0x46 }).ToArray();

        // In place of `cmp rX, #5; bne; <one instruction>`: a bl, then the bne two bytes down.
        private static byte[] CheckHook(byte[] menu, int at, uint to)
        {
            byte[] check = new byte[6];
            ThumbBl.Make(MenuAddress(at), to).CopyTo(check, 0);
            check[4] = (byte)(menu[at + 2] - 1);
            check[5] = 0xD1;
            return check;
        }

        // push {r3, lr} and the call for the saved speed stay. Then ldr r1, =paces; ldrb r0, [r1, r0]; pop {r3, pc}.
        private static byte[] DelayFunction(byte[] game, uint paces)
        {
            uint start = ARM9.Address + Delay;
            byte[] f = new byte[DelayLength];
            for (int i = 0; i < DelayLength; i += 2) { f[i] = 0xC0; f[i + 1] = 0x46; }
            Array.Copy(game, f, 6);
            int literal = (start + 12) % 4 == 0 ? 12 : 14;
            uint pc = (start + 6 + 4) & ~3u;
            f[6] = (byte)((start + (uint)literal - pc) / 4);
            f[7] = 0x49;
            f[8] = 0x08; f[9] = 0x5C;
            f[10] = 0x08; f[11] = 0xBD;
            BitConverter.GetBytes(paces).CopyTo(f, literal);
            return f;
        }

        // Options_Init's stores become ldr r0, =defaults; strh r0, [r4]. The literal takes the first of the game's two.
        private static void WriteDefaults(int[] defaults)
        {
            int packed = Enum.GetValues<Row>().Sum(r => defaults[(int)r] << Shift(r));
            uint start = ARM9.Address + Init;
            byte[] body = new byte[InitLength];
            for (int i = 0; i < InitLength; i += 2) { body[i] = 0xC0; body[i + 1] = 0x46; }
            body[0] = (byte)((start + InitLiteral - ((start + 4) & ~3u)) / 4);
            body[1] = 0x48;
            body[2] = 0x20; body[3] = 0x80;
            ARM9.WriteBytes(body, Init);
            ARM9.WriteBytes(BitConverter.GetBytes((uint)packed), Init + InitLiteral);
        }

        // Sixteen paces, one per saved speed. Saved speeds past the list read the last one.
        private static byte[] PaceTable(IReadOnlyList<byte> paces)
        {
            byte[] table = new byte[16];
            for (int i = 0; i < table.Length; i++) table[i] = paces[Math.Min(i, paces.Count - 1)];
            return table;
        }

        // ── The game's tables the patch changes, kept in the block to rebuild settings from ──

        private const int SnapCounts = 0x00, SnapPtFirst = 0x20, SnapPtX = 0x28, SnapBorder = 0x30, SnapJump = 0x50, SnapLength = 0x60;
        private const int Template = 0x28, Hitboxes = 15;

        private static byte[] Snapshot(byte[] menu)
        {
            byte[] s = new byte[SnapLength];
            Array.Copy(menu, Counts, s, SnapCounts, 28);
            if (!Hg)
            {
                Array.Copy(menu, PtFirstLines, s, SnapPtFirst, 7);
                Array.Copy(menu, PtXOffsets, s, SnapPtX, 7);
                return s;
            }
            Array.Copy(menu, HgBorder, s, SnapBorder, 28);
            if (!Japanese) Array.Copy(menu, Menu.Offsets[6], s, SnapJump, 12);
            return s;
        }

        // ── Reading and writing the settings ──

        /// <summary>The settings as the game has them now, or null when the patch isn't in.</summary>
        public static Settings Read()
        {
            long block = BlockOffset();
            if (block < 0) return null;
            byte[] data = DSUtils.ReadFromFile(Filesystem.ExpArmPath, (uint)block, BlockLength);
            int count = Math.Clamp((int)data[CountAt], 1, MaxSpeeds);
            List<string> lines = new TextArchive(TextBankId).Messages;
            int first = SpeedNamesFirstLine(data);
            List<Speed> speeds = Enumerable.Range(0, count)
                .Select(i => new Speed(first + i < lines.Count ? lines[first + i] : "", data[PacesAt + i])).ToList();
            Row[] rows = Enum.GetValues<Row>();
            int?[] locks = rows.Select(r => data[LocksAt + (int)r] == 0xFF ? (int?)null : data[LocksAt + (int)r]).ToArray();
            bool[] removed = rows.Select(r => data[SlotsAt + GameRow(r)] == 0xFF).ToArray();
            int[] defaults = rows.Select(r => (int)data[DefaultsAt + (int)r]).ToArray();
            return new Settings(speeds, locks, removed, defaults);
        }

        private static int SpeedNamesFirstLine(byte[] data) => Hg ? data[FirstLinesAt] : ReadMenu()[PtFirstLines];

        /// <summary>The first text line of the speed names in <see cref="TextBankId"/>, or -1 when the patch isn't in.</summary>
        public static int SpeedNamesLine()
        {
            long block = BlockOffset();
            return block < 0 ? -1 : SpeedNamesFirstLine(DSUtils.ReadFromFile(Filesystem.ExpArmPath, (uint)block, BlockLength));
        }

        /// <summary>What the screen shows with the patch, by the game's row order. Slots and locks are -1 when removed or free.</summary>
        public sealed record ScreenLayout(List<string> SpeedNames, bool Arrows, int[] Slots, int[] Locks);

        private static (string Rom, DateTime Block, DateTime Menu, ScreenLayout Layout, int Member)? _shown;

        static OptionsMenuPatch() => TextArchive.Saved += (_, id) => { if (id == TextBankId) _shown = null; };

        /// <summary>The layout the previews draw, or null when the patch isn't in. Read again only after a file changes.</summary>
        public static ScreenLayout Layout() => Shown().Layout;

        /// <summary>The screen 17 member the game shows with the patch in, or 17.</summary>
        public static int MapMember() => Shown().Member;

        private static (ScreenLayout Layout, int Member) Shown()
        {
            if (!Supports || !File.Exists(Filesystem.ExpArmPath)) return (null, 17);
            DateTime block = File.GetLastWriteTimeUtc(Filesystem.ExpArmPath), menu = File.GetLastWriteTimeUtc(MenuPath);
            if (_shown is { } c && c.Rom == ProjectSession.LoadId && c.Block == block && c.Menu == menu) return (c.Layout, c.Member);
            long at = BlockOffset();
            ScreenLayout layout = Read() is Settings s ? Layout(s) : null;
            int member = at < 0 || !Hg ? 17 : DSUtils.ReadFromFile(Filesystem.ExpArmPath, (uint)(at + MapMemberAt), 1)[0];
            _shown = (ProjectSession.LoadId, block, menu, layout, member);
            return (layout, member);
        }

        /// <summary>The layout the given settings show.</summary>
        public static ScreenLayout Layout(Settings s)
        {
            int[] slots = Enumerable.Repeat(-1, 7).ToArray(), locks = Enumerable.Repeat(-1, 7).ToArray();
            int slot = 0;
            Row[] rows = Enum.GetValues<Row>();
            for (int g = 0; g < 6; g++)
                if (!s.Removed[(int)rows.First(r => GameRow(r) == g)]) slots[g] = slot++;
            slots[6] = Hg ? 6 : slot;
            foreach (Row r in rows) if (s.Locks[(int)r] is int v) locks[GameRow(r)] = v;
            return new ScreenLayout(s.Speeds.Select(p => p.Name).ToList(), s.Speeds.Count > 3, slots, locks);
        }

        /// <summary>The game's own settings, shown before the patch is in.</summary>
        public static Settings GameSettings()
        {
            if (!Supports) return null;
            List<string> lines = new TextArchive(TextBankId).Messages;
            int first = Hg ? HgFirstLines[0] : ReadMenu()[PtFirstLines];
            List<Speed> speeds = GamePaces.Select((p, i) => new Speed(first + i < lines.Count ? lines[first + i] : "", p)).ToList();
            int rows = Enum.GetValues<Row>().Length;
            return new Settings(speeds, new int?[rows], new bool[rows], GameDefaults.ToArray());
        }

        /// <summary>The names of a row's choices, as the game ships them.</summary>
        public static List<string> ChoiceNames(Row row)
        {
            if (!Supports) return new List<string>();
            long block = BlockOffset();
            int first = Hg ? HgFirstLines[GameRow(row)]
                      : block < 0 ? ReadMenu()[PtFirstLines + GameRow(row)]
                      : DSUtils.ReadFromFile(Filesystem.ExpArmPath, (uint)(block + SnapshotAt + SnapPtFirst + GameRow(row)), 1)[0];
            List<string> lines = new TextArchive(TextBankId).Messages;
            return Enumerable.Range(first, ChoiceCount(row)).Select(i => i < lines.Count ? lines[i] : "").ToList();
        }

        /// <summary>Why the settings can't be written, or null.</summary>
        public static string WhyNot(Settings s)
        {
            if (s.Speeds == null || s.Speeds.Count < 1 || s.Speeds.Count > MaxSpeeds) return $"Text speed needs 1 to {MaxSpeeds} speeds.";
            foreach (Row r in Enum.GetValues<Row>())
            {
                if (s.Removed[(int)r] && r == Row.TextSpeed) return "Text speed can't be removed.";
                if (s.Removed[(int)r] && Lockable(r) && s.Locks[(int)r] == null) return $"{Label(r)} needs a locked choice before it's removed.";
                if (s.Locks[(int)r] is int v && (!Lockable(r) || v < 0 || v >= ChoiceCount(r))) return $"{Label(r)} can't be locked to that choice.";
                int most = r == Row.TextSpeed ? s.Speeds.Count : ChoiceCount(r);
                if (s.Defaults[(int)r] < 0 || s.Defaults[(int)r] >= most) return $"The {Label(r)} value for new saves is out of range.";
            }
            return null;
        }

        /// <summary>The game's tables rebuilt from the Buttons tab's, after it saves.</summary>
        public static void Refresh()
        {
            if (Read() is Settings s) Write(s);
        }

        /// <summary>Writes the settings: paces, locks, defaults and the rows on screen. The names are text, saved as text.</summary>
        public static void Write(Settings s)
        {
            if (WhyNot(s) is string why) throw new ArgumentException(why);
            long block = BlockOffset();
            if (block < 0) throw new InvalidOperationException("The Options patch isn't applied.");
            // A patch applied before the names had lines of their own gets them now.
            PlaceSpeedNames();
            byte[] data = DSUtils.ReadFromFile(Filesystem.ExpArmPath, (uint)block, BlockLength);
            byte[] snap = data.AsSpan(SnapshotAt, SnapLength).ToArray();
            byte[] menu = ReadMenu();
            Row[] rows = Enum.GetValues<Row>();
            int n = s.Speeds.Count;
            bool arrows = n > 3;
            int speedLine = SpeedNamesFirstLine(data);

            // The game files go back if a later step fails.
            string mapPath = Hg ? ArchiveMembers.PathOf(RomInfo.DirNames.OptionsGraphics, data[MapMemberAt]) : null;
            (string Path, byte[] Bytes)[] before = new[] { RomInfo.Arm9Path, MenuPath, mapPath }
                .Where(f => f != null && File.Exists(f)).Select(f => (f, File.ReadAllBytes(f))).ToArray();
            try
            {
                PaceTable(s.Speeds.Select(p => p.Pace).ToList()).CopyTo(data, PacesAt);
                data[CountAt] = (byte)n;
                data[ArrowsAt] = (byte)(arrows ? 1 : 0);
                foreach (Row r in rows)
                {
                    data[LocksAt + (int)r] = s.Locks[(int)r] is int v ? (byte)v : (byte)0xFF;
                    data[DefaultsAt + (int)r] = (byte)s.Defaults[(int)r];
                }
                // A locked row's new saves hold the locked value too.
                WriteDefaults(rows.Select(r => s.Locks[(int)r] ?? s.Defaults[(int)r]).ToArray());

                // Slots: shown rows in order, from the top. Platinum's Close row moves up with them.
                int slot = 0;
                for (int g = 0; g < 6; g++)
                {
                    Row r = rows.First(x => GameRow(x) == g);
                    data[SlotsAt + g] = s.Removed[(int)r] ? (byte)0xFF : (byte)slot++;
                }
                data[SlotsAt + 6] = (byte)(Hg ? 6 : slot);

                // Getters return a locked value whatever the save holds: movs r0, #v; bx lr.
                foreach (Row r in rows.Where(Lockable))
                {
                    byte[] getter = s.Locks[(int)r] is int v ? new byte[] { (byte)v, 0x20, 0x70, 0x47, 0xC0, 0x46, 0xC0, 0x46 }
                                                             : data.AsSpan(GettersAt + Getter(r) * GetterLength, GetterLength).ToArray();
                    ARM9.WriteBytes(getter, (uint)Getters.Offsets[Getter(r)]);
                }

                int[] counts = Enumerable.Range(0, 7).Select(i => BitConverter.ToInt32(snap, SnapCounts + i * 4)).ToArray();
                foreach (Row r in rows)
                {
                    if (r == Row.TextSpeed) counts[GameRow(r)] = n;
                    else if (s.Locks[(int)r] != null) counts[GameRow(r)] = 1;
                }
                Put(counts.SelectMany(BitConverter.GetBytes).ToArray(), Counts);

                if (Hg)
                {
                    int[] slots = Enumerable.Range(0, 7).Select(g => data[SlotsAt + g] == 0xFF ? -1 : data[SlotsAt + g]).ToArray();
                    int[] locked = Enumerable.Repeat(-1, 6).ToArray();
                    foreach (Row r in rows) if (s.Locks[(int)r] is int v) locked[GameRow(r)] = v;
                    for (int g = 0; g < 6; g++) data[FirstLinesAt + g] = (byte)(HgFirstLines[g] + Math.Max(locked[g], 0));
                    data[FirstLinesAt] = (byte)speedLine;
                    // The jump table's text speed case goes to the frame row's when the row has arrows.
                    if (!Japanese) Put(snap.AsSpan(SnapJump + (arrows ? 10 : 0), 2).ToArray(), Menu.Offsets[6]);
                    byte[] border = snap.AsSpan(SnapBorder, 28).ToArray();
                    for (int g = 0; g < 6; g++)
                        if (slots[g] >= 0) Array.Copy(snap, SnapBorder + slots[g] * 4, border, g * 4, 4);
                    Put(border, HgBorder);
                    DeriveHg(data, menu, slots, locked, arrows);
                }
                else WritePt(s, snap, speedLine);
            }
            catch
            {
                foreach ((string path, byte[] bytes) in before) DSUtils.WriteWholeFile(path, bytes);
                throw;
            }
            DSUtils.WriteToFile(Filesystem.ExpArmPath, data, (uint)block);
        }

        private static void WritePt(Settings s, byte[] snap, int speedLine)
        {
            byte[] first = snap.AsSpan(SnapPtFirst, 7).ToArray(), x = snap.AsSpan(SnapPtX, 7).ToArray();
            first[0] = (byte)speedLine;
            foreach (Row r in Enum.GetValues<Row>())
            {
                if (s.Locks[(int)r] is not int v) continue;
                int g = GameRow(r);
                first[g] = (byte)(first[g] + v);
                // The lone choice stays where it was. Button mode spaces its choices by their width, so it starts the row.
                x[g] = r == Row.Button ? (byte)0 : (byte)(v * 48);
            }
            Put(first, PtFirstLines);
            Put(x, PtXOffsets);
        }

        // ── HGSS: the copies the game reads, built from the Buttons tab's tables and screen 17 each time ──

        private const int FirstMapRow = 3, MapRows = 21, RowTiles = 3, Hidden = 208;

        private static void DeriveHg(byte[] data, byte[] menu, int[] slots, int[] locked, bool arrows)
        {
            RomInfo.HgssOptionsSites h = RomLocator.HgssOptionsSites;
            byte[] labels = h.Labels == 0 ? null : menu.AsSpan((int)h.Labels, 42).ToArray();
            byte[] highlights = menu.AsSpan((int)h.Highlights, 60).ToArray();
            byte[] touch = menu.AsSpan((int)h.Touch, 64 + Hitboxes * 8).ToArray();
            byte[] templates = menu.AsSpan((int)h.Sprites, 9 * Template).ToArray();

            // Label x: a locked row shows its choice where it was, and the arrow row centres its name as Frame does.
            if (labels != null)
            {
                byte[] shown = (byte[])labels.Clone();
                for (int g = 0; g < 6; g++)
                    if (locked[g] >= 0) Array.Copy(labels, g * 6 + locked[g] * 2, shown, g * 6, 2);
                if (arrows) Array.Copy(labels, 5 * 6, shown, 0, 2);
                shown.CopyTo(data, LabelsCopyAt);
            }
            byte[] lit = (byte[])highlights.Clone();
            for (int g = 0; g < 5; g++)
                if (locked[g] >= 0) Array.Copy(highlights, g * 12 + locked[g] * 4, lit, g * 12, 4);
            lit.CopyTo(data, HighlightsCopyAt);

            Templates(templates, arrows, slots).CopyTo(data, TemplatesAt);
            Touch(touch, slots, locked, arrows).CopyTo(data, TouchCopyAt);
            WriteMap(data[MapMemberAt], slots, locked, arrows, touch);
        }

        // The eleven sprite templates: the game's nine, each row's at its slot or hidden, then the text speed row's two arrows.
        private static byte[] Templates(byte[] game, bool arrows, int[] slots)
        {
            byte[] t = new byte[11 * Template];
            Array.Copy(game, t, game.Length);
            Array.Copy(game, 5 * Template, t, 9 * Template, Template);
            Array.Copy(game, 6 * Template, t, 10 * Template, Template);
            void Y(int sprite, int y) => BitConverter.GetBytes((short)y).CopyTo(t, sprite * Template + 6);
            int RowY(int sprite, int g) => slots[g] < 0 ? Hidden : BitConverter.ToInt16(game, sprite * Template + 6) + (slots[g] - g) * 24;
            for (int g = 0; g < 5; g++) Y(g, RowY(g, g));
            Y(5, RowY(5, 5));
            Y(6, RowY(6, 5));
            if (arrows) Y(0, Hidden);
            Y(9, arrows ? BitConverter.ToInt16(game, 5 * Template + 6) - 5 * 24 : Hidden);
            Y(10, arrows ? BitConverter.ToInt16(game, 6 * Template + 6) - 5 * 24 : Hidden);
            return t;
        }

        // The touch areas and their rows: each row's moved to its slot, a removed row's and a locked row's other boxes dead, and
        // the text speed row's three boxes turned into the frame row's two arrows.
        private static byte[] Touch(byte[] game, int[] slots, int[] locked, bool arrows)
        {
            byte[] rects = game.AsSpan(0, 64).ToArray();
            byte[] map = game.AsSpan(64, Hitboxes * 8).ToArray();
            void Dead(int i) { rects[i * 4] = 200; rects[i * 4 + 1] = 200; }
            for (int i = 0; i < 13; i++)
            {
                int g = BitConverter.ToInt32(map, i * 8), value = BitConverter.ToInt32(map, i * 8 + 4);
                if (g < 0 || g > 5) continue;
                if (slots[g] < 0) { Dead(i); continue; }
                int down = (slots[g] - g) * 24;
                rects[i * 4] = (byte)(rects[i * 4] + down);
                rects[i * 4 + 1] = (byte)(rects[i * 4 + 1] + down);
                if (locked[g] >= 0 && value != locked[g] && value < 3) Dead(i);
                else if (locked[g] >= 0) BitConverter.GetBytes(0).CopyTo(map, i * 8 + 4);
            }
            if (arrows)
            {
                Array.Copy(game, 11 * 4, rects, 0, 4);
                Array.Copy(game, 12 * 4, rects, 2 * 4, 4);
                for (int i = 0; i <= 2; i += 2) { rects[i * 4] = (byte)(rects[i * 4] - 5 * 24); rects[i * 4 + 1] = (byte)(rects[i * 4 + 1] - 5 * 24); }
                Dead(1);
                BitConverter.GetBytes(3).CopyTo(map, 4);
                BitConverter.GetBytes(4).CopyTo(map, 2 * 8 + 4);
            }
            return rects.Concat(map).ToArray();
        }

        // The shown screen from screen 17: each shown row's boxes on its slot's stripe, the text speed row as the frame row's box
        // when it has arrows, a locked row's other boxes painted over, and the panel's bottom edge under the last row.
        private static void WriteMap(int member, int[] slots, int[] locked, bool arrows, byte[] touch)
        {
            (byte[] map, bool packed) = BuildMap(slots, locked, arrows, touch);
            ArchiveMembers.Put(RomInfo.DirNames.OptionsGraphics, member, packed ? Lz77.Compress(map) : map);
        }

        /// <summary>The HGSS screen the given settings show, unpacked, or null without the patch.</summary>
        public static byte[] HgShownMap(Settings s)
        {
            if (!Hg || BlockOffset() < 0) return null;
            ScreenLayout l = Layout(s);
            byte[] touch = ReadMenu().AsSpan((int)RomLocator.HgssOptionsSites.Touch, 64 + Hitboxes * 8).ToArray();
            return BuildMap(l.Slots, l.Locks.Take(6).ToArray(), l.Arrows, touch).Map;
        }

        private static (byte[] Map, bool Packed) BuildMap(int[] slots, int[] locked, bool arrows, byte[] touch)
        {
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.OptionsGraphics });
            byte[] raw = File.ReadAllBytes(ArchiveMembers.PathOf(RomInfo.DirNames.OptionsGraphics, 17));
            byte[] plainMap = WindowTileArt.Unpacked(raw);
            ushort[] saved = NitroBgCodec.ReadSquares(plainMap, 0, FirstMapRow, 32, MapRows);
            ushort Saved(int y, int x) => saved[y * 32 + x];
            const int Square = 0x3FF;
            // Each row's boxes, in columns, from its touch areas: rows 0 and 1 give the light and the dark row's plain square and
            // box squares, so a moved box is still told from the plain squares around it.
            List<(int Left, int Right)> Boxes(int g) => Enumerable.Range(0, 13)
                .Where(i => BitConverter.ToInt32(touch, 64 + i * 8) == g && BitConverter.ToInt32(touch, 64 + i * 8 + 4) < 3)
                .Select(i => (touch[i * 4 + 2] / 8, touch[i * 4 + 3] / 8)).ToList();
            int PlainColumn(int g) => Enumerable.Range(14, 17).First(x => !Boxes(g).Any(b => x >= b.Left && x <= b.Right));
            ushort Gap(int g, int t) => Saved((g % 2) * RowTiles + t, PlainColumn(g % 2));
            bool IsGap(ushort tile, int g, int t) => (tile & Square) == (Gap(g, t) & Square);
            int shown = slots.Take(6).Count(x => x >= 0);
            // Boxes are shaded for their row too: a light box's edge and fill squares pair with a dark box's.
            Dictionary<int, int> shade = new Dictionary<int, int>();
            int lightLeft = Boxes(0).Min(b => b.Left), darkLeft = Boxes(1).Min(b => b.Left);
            for (int t = 0; t < RowTiles; t++)
                for (int k = 0; k < 2; k++)
                {
                    int light = Saved(t, lightLeft + k) & Square, dark = Saved(RowTiles + t, darkLeft + k) & Square;
                    if (light == dark) continue;
                    shade.TryAdd(light, dark);
                    shade.TryAdd(dark, light);
                }
            ushort[] rows = (ushort[])saved.Clone();

            for (int g = 0; g < 6; g++)
            {
                if (slots[g] < 0) continue;
                int slot = slots[g], from = g == 0 && arrows ? 5 : g;
                (int Left, int Right)[] dead = DeadBoxes(touch, g, locked[g]);
                for (int t = 0; t < RowTiles; t++)
                    for (int x = 14; x <= 30; x++)
                    {
                        ushort tile = Saved(from * RowTiles + t, x);
                        bool plain = IsGap(tile, from, t) || dead.Any(b => x >= b.Left && x <= b.Right);
                        if (!plain && from % 2 != slot % 2 && shade.TryGetValue(tile & Square, out int other)) tile = (ushort)((tile & ~Square) | other);
                        rows[(slot * RowTiles + t) * 32 + x] = plain ? Gap(slot, t) : tile;
                    }
            }
            if (shown < 6)
            {
                // The panel's bottom corners move under the last row, then its edge line and the plain background.
                int last = (shown - 1) * RowTiles + 2, bottom = 6 * RowTiles - 1;
                rows[last * 32] = Saved(bottom, 0);
                rows[last * 32 + 31] = Saved(bottom, 31);
                for (int y = shown * RowTiles; y < 6 * RowTiles; y++)
                    for (int x = 0; x < 32; x++) rows[y * 32 + x] = Saved(y == shown * RowTiles ? 18 : 19, x);
            }

            return (NitroBgCodec.PutSquares(plainMap, 0, FirstMapRow, 32, MapRows, rows), !ReferenceEquals(plainMap, raw));
        }

        // A locked row's other boxes, in columns, from the touch areas.
        private static (int Left, int Right)[] DeadBoxes(byte[] touch, int g, int locked)
        {
            if (locked < 0) return Array.Empty<(int, int)>();
            List<(int, int)> boxes = new List<(int, int)>();
            for (int i = 0; i < 13; i++)
            {
                int row = BitConverter.ToInt32(touch, 64 + i * 8), value = BitConverter.ToInt32(touch, 64 + i * 8 + 4);
                if (row == g && value != locked && value < 3) boxes.Add((touch[i * 4 + 2] / 8, touch[i * 4 + 3] / 8));
            }
            return boxes.ToArray();
        }
    }
}
