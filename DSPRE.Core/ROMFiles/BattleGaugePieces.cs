using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The tiles battle copies into an HP bar and the tables saying where. Both live in the battle overlay and are
    /// found through the function that reads them, so repainting doesn't lose them.
    /// </summary>
    public static class BattleGaugePieces
    {
        public const int TileBytes = 32;

        // ldr r1,[pc,#4]; lsl r0,r0,#5; add r0,r1,r0; bx lr, then the sheet's address.
        private static readonly byte[] SheetGetter = { 0x01, 0x49, 0x40, 0x01, 0x08, 0x18, 0x70, 0x47 };

        public enum Family { DiamondPearl, PlatinumAndJohto }

        /// <summary>The six bars, in the order the game's tables keep them.</summary>
        public enum Bar { YoursSingle, TheirsSingle, YoursDouble, TheirsDouble, YourPartner, TheirPartner }
        public const int BarCount = 6;

        public enum Slot { Name, LvMark, LvDigits, CurrentHp, MaxHp, HpGauge, Caught, Status, HpLeft, HpRight, Slash }

        /// <summary>One copy into the bar: a byte offset into its tiles and how many bytes go there.</summary>
        public readonly record struct Span(int Pos, int Size)
        {
            public int FirstTile => Pos / TileBytes;
            public int Tiles => Size / TileBytes;
        }

        // Each table's offset from the sheet, and how many spans one bar has in it. Platinum and HeartGold
        // keep the same tables in the same order just before the sheet; every one is read through a pointer
        // in the code, which is checked before any of them is trusted.
        private static readonly Dictionary<Slot, (int From, int PerBar)> Tables = new()
        {
            [Slot.Status] = (-0x350, 1),
            [Slot.HpLeft] = (-0x338, 1),
            [Slot.Caught] = (-0x320, 1),
            [Slot.HpRight] = (-0x308, 1),
            [Slot.MaxHp] = (-0x2F0, 1),
            [Slot.Slash] = (-0x2D8, 1),
            [Slot.LvDigits] = (-0x2C0, 2),
            [Slot.CurrentHp] = (-0x290, 2),
            [Slot.HpGauge] = (-0x260, 2),
            [Slot.LvMark] = (-0x230, 2),
            [Slot.Name] = (-0x60, 4),
        };

        private static bool Sinnoh => Locate()?.Family == Family.DiamondPearl;

        /// <summary>
        /// Where the experience fill goes in your single battle bar. The code holds these as numbers, not a table: five
        /// tiles then seven in Platinum and HeartGold, four then seven in Diamond and Pearl.
        /// </summary>
        public static Span[] ExpGauge => ReadExp() is { } e
            ? new[] { new Span(e.FirstTile * TileBytes, e.FirstCount * TileBytes), new Span(e.SecondTile * TileBytes, e.SecondCount * TileBytes) }
            : Sinnoh
            ? new[] { new Span(0x680, 4 * TileBytes), new Span(0xE00, 7 * TileBytes) }
            : new[] { new Span(0x660, 5 * TileBytes), new Span(0xE00, 7 * TileBytes) };

        public const int GameHpCells = 6;
        public static int GameExpCells => Sinnoh ? 11 : 12;

        /// <summary>The health gauge's current length in squares.</summary>
        public static int HpCells => ReadLengths()?.Hp ?? GameHpCells;
        public static int ExpCells => ReadLengths()?.Exp ?? GameExpCells;

        // The instruction each length constant sits in (movs, cmp), and for health how many pixels a square counts for.
        private static readonly (byte Op, int Times)[] HpLengthOps = { (0x20, 1), (0x20, 1), (0x21, 8), (0x2C, 1) };
        private static readonly byte[] ExpLengthOps = { 0x23, 0x21, 0x20, 0x28, 0x2C };

        private static RomInfo.GaugeLengthSites LengthSites()
        {
            RomInfo.GaugeLengthSites sites = RomLocator.GaugeLengthSites;
            Found f = Locate();
            return sites != null && f != null && sites.Overlay == f.Overlay
                && sites.Health.Length == HpLengthOps.Length && sites.Experience.Length == ExpLengthOps.Length ? sites : null;
        }

        /// <summary>Both gauges' lengths in squares, or null when this dump's code isn't laid out as DSPRE expects.</summary>
        public static (int Hp, int Exp)? ReadLengths(byte[] overlay = null)
        {
            RomInfo.GaugeLengthSites sites = LengthSites();
            byte[] bytes = sites == null ? null : overlay ?? ReadOverlay(sites.Overlay);
            if (bytes == null || sites.Health.Concat(sites.Experience).Any(at => at < 0 || at + 2 > bytes.Length)) return null;
            int hp = bytes[sites.Health[0]], exp = bytes[sites.Experience[0]];
            for (int i = 0; i < HpLengthOps.Length; i++)
                if (bytes[sites.Health[i] + 1] != HpLengthOps[i].Op || bytes[sites.Health[i]] != hp * HpLengthOps[i].Times) return null;
            for (int i = 0; i < ExpLengthOps.Length; i++)
                if (bytes[sites.Experience[i] + 1] != ExpLengthOps[i] || bytes[sites.Experience[i]] != exp) return null;
            return (hp, exp);
        }

        /// <summary>Sets both gauges' lengths, never longer than the game's. Returns the error message, or null.</summary>
        public static string PutLengths(byte[] overlay, int hp, int exp)
        {
            RomInfo.GaugeLengthSites sites = LengthSites();
            if (sites == null || ReadLengths(overlay) == null) return "The gauges' lengths weren't found in this game's code.";
            if (hp < 1 || hp > GameHpCells) return $"Health has to be 1 to {GameHpCells} squares long.";
            if (exp < 1 || exp > GameExpCells) return $"Experience has to be 1 to {GameExpCells} squares long.";
            for (int i = 0; i < HpLengthOps.Length; i++) overlay[sites.Health[i]] = (byte)(hp * HpLengthOps[i].Times);
            foreach (int at in sites.Experience) overlay[at] = (byte)exp;
            return null;
        }

        // The sheet, tile by tile. The fills, the status words and the doubles pieces sit at the same tiles in all
        // three games; Diamond and Pearl keep their blank status after the words and have no ends for the numbers.
        public const int HpGreen = 2, HpYellow = 11, HpRed = 20, ExpFill = 29;
        public const int CaughtBall = 0x3B, DoublesHp = 0x42, BarEnd = 0x44, Slash = 0x45;
        public const int NumbersLeft = 0x46, NumbersRight = 0x47;
        public const int FirstWord = 0x29, Words = 5;
        public static int NoStatus => Sinnoh ? 0x38 : 0x26;
        public static bool HasNumberEnds => !Sinnoh;

        /// <summary>
        /// The gender and Lv pieces, one array per gender (female, male, none). Platinum and HeartGold keep each
        /// as a 2 by 2 block (top left, top right, bottom left, bottom right); Diamond and Pearl as one row of two
        /// that the battle draws four rows lower.
        /// </summary>
        public static int[][] GenderPieces => Sinnoh
            ? new[] { new[] { 0x3C, 0x3D }, new[] { 0x3E, 0x3F }, new[] { 0x40, 0x41 } }
            : new[] { new[] { 0x3C, 0x3D, 0x48, 0x49 }, new[] { 0x3E, 0x3F, 0x4A, 0x4B }, new[] { 0x40, 0x41, 0x4C, 0x4D } };

        public sealed class Found
        {
            public int Overlay;
            public int SheetAt;          // file offset of tile 0
            public int TileCount;
            public Family Family;
            public bool HasSlots;        // the tables were found and checked
            public int ExpCodeAt = -1;   // the experience gauge's tile numbers in the code, or -1
            public bool HasExtraPieces;  // the extra piece table was found and checked
        }

        private static Found _found;
        private static string _foundFor;
        private static string _why;

        /// <summary>Where this ROM keeps the sheet, or null; <see cref="WhyNot"/> says why.</summary>
        public static Found Locate()
        {
            string forRom = RomInfo.WorkDir ?? "";
            if (_foundFor == forRom) return _found;
            _foundFor = forRom;
            _found = null;
            _why = null;

            Family family;
            switch (RomInfo.GameFamily)
            {
                case RomInfo.GameFamilies.DP: family = Family.DiamondPearl; break;
                case RomInfo.GameFamilies.Plat:
                case RomInfo.GameFamilies.HGSS: family = Family.PlatinumAndJohto; break;
                default: _why = "HP bar pieces aren't supported for this game."; return null;
            }

            int overlays;
            try { overlays = OverlayUtils.OverlayTable.GetNumberOfOverlays(); }
            catch (Exception ex) { _why = "The overlay table couldn't be read: " + ex.Message; return null; }

            for (int ov = 0; ov < overlays; ov++)
            {
                byte[] bytes = ReadOverlay(ov);
                if (bytes == null) continue;
                uint ram;
                try { ram = OverlayUtils.OverlayTable.GetRAMAddress(ov); }
                catch (Exception ex) { AppLogger.WarnOnce("BattleGaugePieces.Locate", ex); continue; }

                for (int i = 0; i + SheetGetter.Length + 6 <= bytes.Length; i += 2)
                {
                    if (!bytes.AsSpan(i, SheetGetter.Length).SequenceEqual(SheetGetter)) continue;
                    int literal = (i + SheetGetter.Length + 3) & ~3;
                    long at = (long)BitConverter.ToUInt32(bytes, literal) - ram;
                    int tiles = family == Family.DiamondPearl ? 0x46 : 0x4E;
                    if (at < 0 || at + tiles * TileBytes > bytes.Length) continue;

                    _found = new Found
                    {
                        Overlay = ov,
                        SheetAt = (int)at,
                        TileCount = tiles,
                        Family = family,
                        // All three games keep the same tables at the same distance before the sheet.
                        HasSlots = TablesCheckOut(bytes, ram, (int)at),
                        ExpCodeAt = FindExpCode(bytes, i),
                        HasExtraPieces = family == Family.PlatinumAndJohto && HasPointerTo(bytes, (uint)(ram + at + ExtraPiecesFrom)),
                    };
                    return _found;
                }
            }

            _why = "The battle code that draws the HP bar pieces wasn't found.";
            return null;
        }

        public static string WhyNot => Locate() != null ? null : _why;

        private static bool TablesCheckOut(byte[] bytes, uint ram, int sheetAt)
        {
            foreach ((int from, int perBar) in Tables.Values)
            {
                int at = sheetAt + from;
                if (at < 0 || at + BarCount * perBar * 4 > bytes.Length) return false;
                if (!HasPointerTo(bytes, (uint)(ram + at))) return false;
            }
            foreach (int from in Templates.Values)
                if (sheetAt + from < 0 || !HasPointerTo(bytes, (uint)(ram + sheetAt + from))) return false;
            return true;
        }

        // Each bar's sprite template starts with its screen position, at the same distance before the sheet in all
        // three games; the code points at every one. The one at -0x130 is the Safari Zone and Pal Park bar's.
        private static readonly Dictionary<Bar, int> Templates = new()
        {
            [Bar.YoursSingle] = -0xFC, [Bar.TheirsSingle] = -0xC8, [Bar.YoursDouble] = -0x200,
            [Bar.TheirsDouble] = -0x1CC, [Bar.YourPartner] = -0x164, [Bar.TheirPartner] = -0x94,
        };

        // How far left of each bar its second sprite sits, one signed byte a bar (Platinum and HeartGold).
        private const int ExtraPiecesFrom = -0x378;

        /// <summary>Where a bar's middle sits on screen, or null.</summary>
        public static (int X, int Y)? ReadPosition(Bar bar)
        {
            Found f = Locate();
            byte[] bytes = f?.HasSlots == true ? ReadOverlay(f.Overlay) : null;
            if (bytes == null) return null;
            int at = f.SheetAt + Templates[bar];
            return (BitConverter.ToInt16(bytes, at), BitConverter.ToInt16(bytes, at + 2));
        }

        public static void PutPosition(byte[] overlay, Bar bar, int x, int y)
        {
            Found f = Locate();
            int at = f.SheetAt + Templates[bar];
            BitConverter.GetBytes((short)x).CopyTo(overlay, at);
            BitConverter.GetBytes((short)y).CopyTo(overlay, at + 2);
        }

        /// <summary>How far left of the bar its extra piece sits, or null when this game's table is not known.</summary>
        public static int? ReadExtraPiece(Bar bar)
        {
            Found f = Locate();
            byte[] bytes = f?.HasExtraPieces == true ? ReadOverlay(f.Overlay) : null;
            return bytes == null ? null : (sbyte)bytes[f.SheetAt + ExtraPiecesFrom + (int)bar];
        }

        public static void PutExtraPiece(byte[] overlay, Bar bar, int offset)
        {
            Found f = Locate();
            if (f?.HasExtraPieces == true) overlay[f.SheetAt + ExtraPiecesFrom + (int)bar] = (byte)(sbyte)Math.Clamp(offset, -128, 127);
        }

        // The call that fetches the experience pieces, then: movs r1,#0x19; movs r0,#a; lsls r1,#22; lsls r0,#s;
        // adds r0,r1,r0; str; movs r0,#b; lsls r0,#t; movs r4,#0; adds r7,r1,r0; cmp r4,#n. Further on, subs r2,r4,#n
        // counts into the second run. The same in all three games; a, s, b, t and n are what DSPRE rewrites.
        private const int ExpFirst = 10, ExpFirstShift = 14, ExpSecond = 20, ExpSecondShift = 22, ExpSplit = 28, ExpSplitAgain = 60;

        private static int FindExpCode(byte[] bytes, int getterAt)
        {
            for (int a = 0; a + 64 <= bytes.Length; a += 2)
            {
                if (bytes[a] != 0x1D || bytes[a + 1] != 0x20) continue;
                ushort hi = BitConverter.ToUInt16(bytes, a + 2), lo = BitConverter.ToUInt16(bytes, a + 4);
                if ((hi & 0xF800) != 0xF000 || (lo & 0xF800) != 0xF800) continue;
                int off = ((hi & 0x7FF) << 12) | ((lo & 0x7FF) << 1);
                if ((off & 0x400000) != 0) off -= 0x800000;
                if (a + 6 + off != getterAt) continue;
                bool shape = bytes[a + 6] == 0x06 && bytes[a + 7] == 0x1C && bytes[a + 8] == 0x19 && bytes[a + 9] == 0x21
                          && bytes[a + 11] == 0x20 && bytes[a + 12] == 0x89 && bytes[a + 13] == 0x05
                          && (BitConverter.ToUInt16(bytes, a + ExpFirstShift) & 0xF83F) == 0
                          && bytes[a + 21] == 0x20 && (BitConverter.ToUInt16(bytes, a + ExpSecondShift) & 0xF83F) == 0
                          && bytes[a + ExpSplit + 1] == 0x2C
                          && (BitConverter.ToUInt16(bytes, a + ExpSplitAgain) & 0xFE3F) == 0x1E22;
                if (shape) return a;
            }
            return -1;
        }

        /// <summary>The experience gauge as the code has it: first tile and length of each run, or null.</summary>
        public static (int FirstTile, int FirstCount, int SecondTile, int SecondCount)? ReadExp()
        {
            Found f = Locate();
            byte[] bytes = f?.ExpCodeAt >= 0 ? ReadOverlay(f.Overlay) : null;
            if (bytes == null) return null;
            int a = f.ExpCodeAt;
            int first = bytes[a + ExpFirst] << ((BitConverter.ToUInt16(bytes, a + ExpFirstShift) >> 6) & 31);
            int second = bytes[a + ExpSecond] << ((BitConverter.ToUInt16(bytes, a + ExpSecondShift) >> 6) & 31);
            int length = ReadLengths(bytes)?.Exp ?? GameExpCells;
            int split = Math.Min(bytes[a + ExpSplit], length);
            return (first / TileBytes, split, second / TileBytes, length - split);
        }

        /// <summary>
        /// Moves the experience gauge: the first run from <paramref name="firstTile"/>, <paramref name="firstCount"/>
        /// long (0 to 7), the rest from <paramref name="secondTile"/>. Returns the error message, or null.
        /// </summary>
        public static string PutExp(byte[] overlay, int firstTile, int firstCount, int secondTile)
        {
            Found f = Locate();
            if (f == null || f.ExpCodeAt < 0) return "The experience gauge's code wasn't found in this game.";
            if (firstCount < 0 || firstCount > 7) return "The experience gauge's first part has to be 0 to 7 squares long.";
            if (firstTile is < 0 or > 255 || secondTile is < 0 or > 255) return "The experience gauge is outside the bar.";
            int a = f.ExpCodeAt;
            overlay[a + ExpFirst] = (byte)firstTile;
            BitConverter.GetBytes((ushort)(5 << 6)).CopyTo(overlay, a + ExpFirstShift);
            overlay[a + ExpSecond] = (byte)secondTile;
            BitConverter.GetBytes((ushort)(5 << 6)).CopyTo(overlay, a + ExpSecondShift);
            overlay[a + ExpSplit] = (byte)firstCount;
            ushort subs = BitConverter.ToUInt16(overlay, a + ExpSplitAgain);
            BitConverter.GetBytes((ushort)((subs & ~(7 << 6)) | firstCount << 6)).CopyTo(overlay, a + ExpSplitAgain);
            return null;
        }

        private static bool HasPointerTo(byte[] bytes, uint address)
        {
            for (int i = 0; i + 4 <= bytes.Length; i += 4)
                if (BitConverter.ToUInt32(bytes, i) == address) return true;
            return false;
        }

        [ThreadStatic] private static byte[] _staged;

        /// <summary>The sheet a preview has staged on this thread, or null.</summary>
        public static byte[] StagedSheet => _staged;

        /// <summary>Reads on this thread see these tiles as the sheet until the result is disposed, for a preview.</summary>
        public static IDisposable Staged(byte[] sheet)
        {
            byte[] before = _staged;
            _staged = sheet;
            return new Restore(() => _staged = before);
        }

        private sealed class Restore : IDisposable
        {
            private Action _undo;
            public Restore(Action undo) => _undo = undo;
            public void Dispose() { _undo?.Invoke(); _undo = null; }
        }

        /// <summary>The sheet's tiles, 4 bits a pixel, or null.</summary>
        public static byte[] ReadSheet()
        {
            if (_staged != null) return (byte[])_staged.Clone();
            Found f = Locate();
            byte[] bytes = f == null ? null : ReadOverlay(f.Overlay);
            if (bytes == null || f.SheetAt + f.TileCount * TileBytes > bytes.Length) return null;
            return bytes.AsSpan(f.SheetAt, f.TileCount * TileBytes).ToArray();
        }

        public static string WriteSheet(byte[] tiles)
        {
            Found f = Locate();
            if (f == null) return WhyNot;
            if (tiles == null || tiles.Length != f.TileCount * TileBytes)
                return $"The HP bar pieces are {f.TileCount} tiles, and this picture isn't.";
            return Patch(f.Overlay, f.SheetAt, tiles);
        }

        /// <summary>What a tile of the sheet is for, or null when this game's are not named.</summary>
        public static string PartName(int tile)
        {
            Found f = Locate();
            string[] names = f?.Family == Family.DiamondPearl ? SinnohPartNames : PartNames;
            return f == null || tile < 0 || tile >= names.Length ? null : names[tile];
        }

        private static readonly string[] PartNames = BuildPartNames();

        private static readonly string[] SinnohPartNames = BuildSinnohPartNames();

        private static string[] BuildSinnohPartNames()
        {
            List<string> n = new List<string> { "Left half of HP", "Right half of HP" };
            foreach (string bar in new[] { "Green health", "Yellow health", "Red health", "Experience" })
                for (int px = 0; px <= 8; px++) n.Add($"{bar} at {px} of 8");
            foreach (string word in new[] { "Spare word", "Paralysis", "Freeze", "Sleep", "Poison", "Burn", "No status" })
                for (int i = 1; i <= 3; i++) n.Add($"{word} {i}");
            n.Add("Caught ball");
            foreach (string who in new[] { "Female", "Male", "No gender" })
                for (int i = 1; i <= 2; i++) n.Add($"{who} Lv {i}");
            n.AddRange(new[] { "Left half of HP in double battles", "Right half of HP in double battles", "Bar end", "Slash" });
            return n.ToArray();
        }

        private static string[] BuildPartNames()
        {
            List<string> n = new List<string> { "Left half of HP", "Right half of HP" };
            foreach (string bar in new[] { "Green health", "Yellow health", "Red health", "Experience" })
                for (int px = 0; px <= 8; px++) n.Add($"{bar} at {px} of 8");
            foreach (string word in new[] { "No status", "Paralysis", "Freeze", "Sleep", "Poison", "Burn", "Blank" })
                for (int i = 1; i <= 3; i++) n.Add($"{word} {i}");
            n.Add("Caught ball");
            AddLv(n, "top");
            n.AddRange(new[] { "Left half of HP in double battles", "Right half of HP in double battles", "Bar end", "Slash", "Left end of the numbers", "Right end of the numbers" });
            AddLv(n, "bottom");
            return n.ToArray();
        }

        private static void AddLv(List<string> n, string half)
        {
            foreach (string who in new[] { "Female", "Male", "No gender" })
                for (int i = 1; i <= 2; i++) n.Add($"{who} Lv {i} ({half} half)");
        }

        public static Span[] ReadSlot(Slot slot, Bar bar)
        {
            Found f = Locate();
            if (f == null || !f.HasSlots || !Tables.TryGetValue(slot, out (int From, int PerBar) t)) return null;
            byte[] bytes = ReadOverlay(f.Overlay);
            if (bytes == null) return null;

            Span[] spans = new Span[t.PerBar];
            int at = f.SheetAt + t.From + (int)bar * t.PerBar * 4;
            for (int i = 0; i < t.PerBar; i++)
                spans[i] = new Span(BitConverter.ToUInt16(bytes, at + i * 4), BitConverter.ToUInt16(bytes, at + i * 4 + 2));
            return spans;
        }

        public static void PutSlot(byte[] overlay, Slot slot, Bar bar, Span[] spans)
        {
            Found f = Locate();
            if (f == null || !f.HasSlots || !Tables.TryGetValue(slot, out (int From, int PerBar) t)) return;
            int at = f.SheetAt + t.From + (int)bar * t.PerBar * 4;
            for (int i = 0; i < t.PerBar; i++)
            {
                Span s = i < spans.Length ? spans[i] : new Span(0, 0);
                BitConverter.GetBytes((ushort)s.Pos).CopyTo(overlay, at + i * 4);
                BitConverter.GetBytes((ushort)s.Size).CopyTo(overlay, at + i * 4 + 2);
            }
        }

        public static int SpansOf(Slot slot) => Tables.TryGetValue(slot, out (int From, int PerBar) t) ? t.PerBar : 0;

        [ThreadStatic] private static byte[] _stagedOverlay;

        /// <summary>Reads on this thread see this copy of the battle overlay until the result is disposed.</summary>
        public static IDisposable StagedOverlay(byte[] overlay)
        {
            byte[] before = _stagedOverlay;
            _stagedOverlay = overlay;
            return new Restore(() => _stagedOverlay = before);
        }

        public static bool IsOverlayStaged => _stagedOverlay != null;

        /// <summary>A copy of the battle overlay as it is now, staged or on disk, or null.</summary>
        public static byte[] ReadOverlayCopy()
        {
            Found f = Locate();
            byte[] bytes = f == null ? null : ReadOverlay(f.Overlay);
            return bytes == null ? null : (byte[])bytes.Clone();
        }

        /// <summary>The battle overlay as it is on disk, ignoring any staged copy, or null.</summary>
        public static byte[] ReadOverlayOnDisk()
        {
            Found f = Locate();
            if (f == null) return null;
            try
            {
                if (OverlayUtils.IsStillCompressed(f.Overlay)) return null;
                string path = OverlayUtils.GetPath(f.Overlay);
                return File.Exists(path) ? File.ReadAllBytes(path) : null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { return null; }
        }

        // Only these parts of an edited copy are written back, so code changed on disk since the copy was taken stays.
        private static (int At, int Length)[] Owned(Found f)
        {
            int tables = f.SheetAt + ExtraPiecesFrom;
            (int, int) data = (tables, f.SheetAt + f.TileCount * TileBytes - tables);
            List<(int, int)> owned = new() { data };
            if (f.ExpCodeAt >= 0) owned.Add((f.ExpCodeAt, ExpSplitAgain + 2));
            if (LengthSites() is RomInfo.GaugeLengthSites sites)
                owned.AddRange(sites.Health.Concat(sites.Experience).Select(at => (at, 1)));
            return owned.ToArray();
        }

        public static string WriteOverlay(byte[] overlay)
        {
            Found f = Locate();
            if (f == null) return WhyNot;
            foreach ((int at, int length) in Owned(f))
                if (Patch(f.Overlay, at, overlay.AsSpan(at, length).ToArray()) is string trouble) return trouble;
            return null;
        }

        private static byte[] ReadOverlay(int number)
        {
            if (_stagedOverlay != null && _found != null && number == _found.Overlay) return _stagedOverlay;
            try
            {
                // A still-packed overlay is not unpacked to look at it; it simply does not match.
                if (OverlayUtils.IsStillCompressed(number)) return null;
                string path = OverlayUtils.GetPath(number);
                return File.Exists(path) ? File.ReadAllBytes(path) : null;
            }
            catch (Exception ex) { AppLogger.WarnOnce("BattleGaugePieces.ReadOverlay", ex); return null; }
        }

        private static string Patch(int overlay, int at, byte[] with)
        {
            try
            {
                if (OverlayUtils.IsCompressed(overlay)) OverlayUtils.Decompress(overlay);
                string path = OverlayUtils.GetPath(overlay);
                byte[] bytes = File.ReadAllBytes(path);
                if (at < 0 || at + with.Length > bytes.Length) return "The overlay is smaller than where the pieces go.";
                Array.Copy(with, 0, bytes, at, with.Length);
                DSUtils.WriteWholeFile(path, bytes);
                BattleGaugeText.Reset();
                return null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return "The overlay couldn't be written: " + ex.Message;
            }
        }
    }
}
