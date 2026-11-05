using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>Battle touch buttons draw each press frame from its own page of the screen file.</summary>
    public static class BattlePressFrames
    {
        public sealed record Members(int Sheet, int FrontierSheet, int Palette, int FrontierPalette, int Background, int FrontierBackground,
                                     int Command, int Silhouette, int Moves, int Target, int Outlines, int YesNo, int Stop);

        public sealed record Rect(int Top, int Bottom, int Left, int Right);

        public const int BlankTile = 767, LoadedTiles = 768, FirstPaleRow = 8, TileBytes = 32, Columns = 32;
        public const int PageEntries = 0x400, PageBytes = PageEntries * 2;

        // The game loads pages 0 to 3. Pages 4 to 7 keep the original look, so importing again starts from the game's frames.
        public const int NormalPage = 0, PalePage = 1, WhitePage = 2, SlotPage = 3, GamePages = 4;
        public const int OriginalNormal = 4, OriginalPale = 5, OriginalWhite = 6, OriginalColours = 7, Pages = 8;

        // The text box on the replay screen draws over these tiles.
        private const int FirstFrameTile = 0x20, LastFrameTile = 0x28;

        // The press loop reads the table value as a byte offset into the screen buffer, and its add becomes a nop.
        private const ushort LoadEntry = 0x8815, LoadAtOffset = 0x5A55, AddValue = 0x194D, Nop = 0x46C0;
        // The size shifts go from 0x800 to 0x2000, the four pages the game loads.
        private const ushort AllocShift = 0x0289, LargeAllocShift = 0x0309, CopyShift = 0x0292, LargeCopyShift = 0x0312;
        private const int CommandCount = 4, TargetCount = 5, MoveCount = 5, EntryBytes = 6;

        /// <summary>Each Command button's palette row.</summary>
        public static readonly int[] Rows = { 1, 2, 3, 4 };

        public static readonly Rect[] CommandRects = { new(4, 15, 2, 29), new(17, 23, 0, 9), new(17, 23, 22, 31), new(18, 23, 11, 20) };
        /// <summary>The squares a Fight press copies once widened. Every page has the same rows above and below Fight.</summary>
        public static readonly Rect FightReach = new(2, 16, 2, 29);
        public static readonly Rect[] MoveRects = { new(2, 9, 0, 15), new(2, 9, 16, 31), new(10, 17, 0, 15), new(10, 17, 16, 31), new(18, 23, 1, 30) };
        public static readonly Rect[] TargetRects = { new(10, 17, 0, 14), new(0, 9, 17, 31), new(10, 17, 17, 31), new(0, 9, 0, 14), new(18, 23, 1, 30) };
        public static readonly Rect YesRect = new(4, 11, 0, 31), NoRect = new(13, 20, 0, 31), StopRect = new(18, 23, 0, 31);
        public static readonly (Rect Rect, int Add)[] EmptyMoveSlots = { (new(3, 9, 0, 15), 0x22F), (new(3, 9, 16, 31), 0x22C), (new(11, 17, 0, 15), 0x229), (new(11, 17, 16, 31), 0x226) };
        public static readonly (Rect Rect, int Add)[] ClearedTargets = { (new(11, 17, 0, 14), 0x238), (new(1, 9, 17, 31), 0x235), (new(11, 17, 17, 31), 0x232), (new(1, 9, 0, 14), 0x23B) };

        public static Members Panel => GameFamily switch
        {
            GameFamilies.Plat => new Members(28, 169, 242, 340, 49, 170, 42, 47, 43, 44, 48, 45, 46),
            GameFamilies.HGSS => new Members(28, 173, 246, 349, 43, 174, 36, 41, 37, 38, 42, 39, 40),
            _ => null,
        };

        // Only the Screens editor imports button art, so this needs the same ROMs.
        public static bool SupportsCurrentRom => RomLocator.ButtonPressSites != null && Panel != null && GameLanguage != GameLanguages.Japanese;

        private static (ButtonPressSites Sites, RomLocator.ButtonPressCode Code) Where =>
            (RomLocator.ButtonPressSites, RomLocator.ButtonPressDistances(RomLocator.ButtonPressSites.Command));

        private static ushort At(byte[] d, int at) => BitConverter.ToUInt16(d, at);

        private static byte[] ReadOverlay()
        {
            int overlay = RomLocator.ButtonPressSites.Overlay;
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
                    (ButtonPressSites sites, RomLocator.ButtonPressCode code) = Where;
                    if (OverlayUtils.IsStillCompressed(sites.Overlay)) return false;
                    byte[] overlay = File.ReadAllBytes(OverlayUtils.GetPath(sites.Overlay));
                    return code.Loop + 2 <= overlay.Length && At(overlay, code.Loop) == LoadAtOffset;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { return false; }
            }
        }

        public static string WhyNotApplicable()
        {
            if (!SupportsCurrentRom) return "This ROM isn't supported.";
            (_, RomLocator.ButtonPressCode code) = Where;
            byte[] overlay = ReadOverlay();
            bool vanilla = At(overlay, code.Loop) == LoadEntry && At(overlay, code.Loop + 6) == AddValue && At(overlay, code.Alloc) == AllocShift
                && At(overlay, code.Copy) == CopyShift && At(overlay, code.StopCopy) == CopyShift;
            vanilla = vanilla && overlay[FightRect] == OriginalFightTop && overlay[FightRect + 1] == OriginalFightBottom;
            return vanilla ? null : "Something else has already changed the battle button code, so DSPRE leaves it alone.";
        }

        private static byte[] Stored(int id) => File.ReadAllBytes(ArchiveMembers.PathOf(DirNames.BattleBg, id));

        private static byte[] Plain(byte[] stored) => NitroBgCodec.Inflate(stored);

        private static byte[] AsStored(byte[] plain, byte[] original)
        {
            if (original[0] != 0x10) return plain;
            byte[] packed = Lz77.Compress(plain);
            if (packed == null || !NitroBgCodec.Inflate(packed).SequenceEqual(plain)) throw new IOException("A battle screen file couldn't be compressed.");
            return packed;
        }

        /// <summary>Every page in a screen file, in order. An untouched file has one.</summary>
        public static ushort[][] ReadPages(byte[] screen)
        {
            (_, _, int mapAt) = NitroBgCodec.ReadScreenHeader(screen);
            int count = (screen.Length - mapAt) / PageBytes;
            ushort[][] pages = new ushort[count][];
            for (int p = 0; p < count; p++)
            {
                pages[p] = new ushort[PageEntries];
                Buffer.BlockCopy(screen, mapAt + p * PageBytes, pages[p], 0, PageBytes);
            }
            return pages;
        }

        /// <summary>Writes these pages into the file. The header still declares one page, so other readers see what they always did.</summary>
        public static byte[] WithPages(byte[] screen, ushort[][] pages)
        {
            (_, _, int mapAt) = NitroBgCodec.ReadScreenHeader(screen);
            int section = NitroBgCodec.Find(screen, "NRCS", 0);
            byte[] outp = new byte[mapAt + pages.Length * PageBytes];
            Array.Copy(screen, outp, mapAt);
            for (int p = 0; p < pages.Length; p++) Buffer.BlockCopy(pages[p], 0, outp, mapAt + p * PageBytes, PageBytes);
            BitConverter.TryWriteBytes(outp.AsSpan(8), outp.Length);
            BitConverter.TryWriteBytes(outp.AsSpan(section + 4), outp.Length - section);
            return outp;
        }

        public static bool HoldsFrames(int id) => Panel is Members p && IsApplied && new[]
            { p.Background, p.FrontierBackground, p.Silhouette, p.Outlines, p.Command, p.Moves, p.Target, p.YesNo, p.Stop }.Contains(id);

        /// <summary>A new screen only replaces page 0, because the game loads the other pages whatever the file holds.</summary>
        public static byte[] KeepingFrames(int id, byte[] incoming, byte[] current)
        {
            if (incoming == null || current == null || !HoldsFrames(id)) return incoming;
            byte[] plain = Plain(incoming);
            ushort[][] now = ReadPages(Plain(current)), fresh = ReadPages(plain);
            if (now.Length <= 1 || fresh.Length != 1) return incoming;
            ushort[][] pages = now.Select(p => (ushort[])p.Clone()).ToArray();
            pages[NormalPage] = fresh[0];
            return AsStored(WithPages(plain, pages), incoming);
        }

        private static void Offset(ushort[] page, Rect rect, int add)
        {
            for (int y = rect.Top; y <= rect.Bottom; y++)
                for (int x = rect.Left; x <= rect.Right; x++) page[y * Columns + x] = (ushort)(page[y * Columns + x] + add);
        }

        private static ushort[][] FramesOf(ushort[] normal, ushort[] colours, params (Rect Rect, int Up, int Down)[] buttons)
        {
            ushort[][] pages = new ushort[Pages][];
            for (int p = 0; p < GamePages; p++) pages[p] = (ushort[])normal.Clone();
            foreach ((Rect rect, int up, int down) in buttons)
            {
                Offset(pages[PalePage], rect, up);
                Offset(pages[WhitePage], rect, down);
            }
            pages[OriginalNormal] = (ushort[])normal.Clone();
            pages[OriginalPale] = (ushort[])pages[PalePage].Clone();
            pages[OriginalWhite] = (ushort[])pages[WhitePage].Clone();
            pages[OriginalColours] = new ushort[PageEntries];
            Array.Copy(colours, pages[OriginalColours], Math.Min(colours.Length, PageEntries));
            return pages;
        }

        private static byte[] Tile(byte[] sheet, int tilesAt, int tile)
        {
            byte[] p = new byte[64];
            int at = tilesAt + tile * TileBytes;
            if (at + TileBytes > sheet.Length) return p;
            for (int i = 0; i < TileBytes; i++)
            {
                p[i * 2] = (byte)(sheet[at + i] & 15);
                p[i * 2 + 1] = (byte)(sheet[at + i] >> 4);
            }
            return p;
        }

        public static byte[] Flip(byte[] p, int bits) => TileFit.Flip(p, bits);

        public static readonly int[] FlipBits = TileFit.FlipBits;

        // Maps each tile to its twin: the tile number in the low bits, the flip above them.
        private static int[] OnePerShape(IEnumerable<ushort[]> pages, byte[] sheet, int tilesAt, byte[] frontier, int frontierAt)
        {
            int[] map = Enumerable.Range(0, LoadedTiles).ToArray();
            HashSet<int> shown = new HashSet<int>();
            foreach (ushort[] page in pages)
                foreach (ushort e in page)
                    if ((e & 0x3FF) < LoadedTiles) shown.Add(e & 0x3FF);

            Dictionary<string, int> first = new();
            bool Protected(int t) => t >= FirstFrameTile && t <= LastFrameTile;
            foreach (int tile in shown.Where(t => t != BlankTile).OrderBy(Protected).ThenBy(t => t))
            {
                byte[] a = Tile(sheet, tilesAt, tile), b = Tile(frontier, frontierAt, tile);
                bool found = false;
                foreach (int bits in FlipBits)
                {
                    string key = Convert.ToHexString(Flip(a, bits)) + Convert.ToHexString(Flip(b, bits));
                    if (!first.TryGetValue(key, out int kept)) continue;
                    map[tile] = kept | bits << 16;
                    found = true;
                    break;
                }
                if (!found && !Protected(tile)) first[Convert.ToHexString(a) + Convert.ToHexString(b)] = tile;
            }
            return map;
        }

        private static ushort Remapped(ushort entry, int[] map)
        {
            int tile = entry & 0x3FF;
            if (tile >= LoadedTiles) return entry;
            int to = map[tile];
            return (ushort)(entry & 0xF000 | (entry & 0xC00) ^ (to >> 16) | to & 0x3FF);
        }

        /// <summary>Turns every press into pages. The game looks the same, and each tile shape keeps one slot.</summary>
        public static void Apply()
        {
            Members panel = Panel;
            (ButtonPressSites sites, RomLocator.ButtonPressCode code) = Where;
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.BattleBg });

            byte[] sheet = Plain(Stored(panel.Sheet)), frontier = Plain(Stored(panel.FrontierSheet));
            (_, int tilesAt) = NitroBgCodec.ReadTileHeader(sheet);
            (_, int frontierAt) = NitroBgCodec.ReadTileHeader(frontier);
            ushort[] colours = NitroBgCodec.ReadColours(Plain(Stored(panel.Palette)));

            int[] still = { panel.Background, panel.FrontierBackground, panel.Silhouette, panel.Outlines };
            Dictionary<int, ushort[]> normal = still.Concat(new[] { panel.Command, panel.Moves, panel.Target, panel.YesNo, panel.Stop })
                .ToDictionary(id => id, id => ReadPages(Plain(Stored(id)))[0]);

            (Rect, int, int)[] Press(IEnumerable<Rect> rects) => rects.Select(r => (r, (int)OriginalUp, (int)OriginalDown)).ToArray();
            Dictionary<int, ushort[][]> pages = new()
            {
                [panel.Command] = FramesOf(normal[panel.Command], colours, Press(CommandRects)),
                [panel.Moves] = FramesOf(normal[panel.Moves], colours, Press(MoveRects)),
                [panel.Target] = FramesOf(normal[panel.Target], colours, Press(TargetRects)),
                [panel.YesNo] = FramesOf(normal[panel.YesNo], colours, (YesRect, OriginalUp, OriginalDown), (NoRect, 5, 10)),
                [panel.Stop] = FramesOf(normal[panel.Stop], colours, (StopRect, OriginalUp, OriginalDown)),
            };
            foreach ((Rect rect, int add) in EmptyMoveSlots) Offset(pages[panel.Moves][SlotPage], rect, add);
            foreach ((Rect rect, int add) in ClearedTargets) Offset(pages[panel.Target][SlotPage], rect, add);
            foreach (int id in still)
            {
                pages[id] = new ushort[GamePages][];
                pages[id][0] = normal[id];
                for (int p = 1; p < GamePages; p++) pages[id][p] = new ushort[PageEntries];
            }

            int[] map = OnePerShape(pages.Values.SelectMany(p => p.Where((_, i) => i != OriginalColours)), sheet, tilesAt, frontier, frontierAt);
            foreach ((int id, ushort[][] filePages) in pages)
            {
                for (int p = 0; p < filePages.Length; p++)
                    if (p != OriginalColours) filePages[p] = filePages[p].Select(e => Remapped(e, map)).ToArray();
                byte[] stored = Stored(id);
                ArchiveMembers.Put(DirNames.BattleBg, id, AsStored(WithPages(Plain(stored), filePages), stored));
            }

            byte[] overlay = ReadOverlay();
            void Put(int at, ushort value) => BitConverter.TryWriteBytes(overlay.AsSpan(at), value);
            void Frames(int table, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    Put(table + i * EntryBytes + 2, PalePage * PageBytes);
                    Put(table + i * EntryBytes + 4, WhitePage * PageBytes);
                }
            }
            Frames(sites.Command, CommandCount);
            Frames(sites.Target, TargetCount);
            Frames(sites.Fight, MoveCount);
            Frames(code.YesNo, 2);
            Frames(code.Stop, 1);
            for (int i = 0; i < EmptyMoveSlots.Length; i++) Put(code.Empty + i * 2, SlotPage * PageBytes);
            for (int i = 0; i < ClearedTargets.Length; i++) Put(code.Clear + i * 2, SlotPage * PageBytes);
            Put(code.Loop, LoadAtOffset);
            Put(code.Loop + 6, Nop);
            Put(code.Alloc, LargeAllocShift);
            Put(code.Copy, LargeCopyShift);
            Put(code.StopCopy, LargeCopyShift);
            WidenFight(overlay);
            DSUtils.WriteWholeFile(OverlayUtils.GetPath(sites.Overlay), overlay);
        }

        private const int OriginalFightTop = 4, OriginalFightBottom = 15;

        private static int FightRect => (int)RomLocator.BattleCommandSites.TargetGrid + RomLocator.CommandPressRects(GameFamily == GameFamilies.HGSS);

        private static void WidenFight(byte[] overlay)
        {
            overlay[FightRect] = (byte)FightReach.Top;
            overlay[FightRect + 1] = (byte)FightReach.Bottom;
        }

        /// <summary>True on projects that got the patch before it widened Fight's press.</summary>
        public static bool NeedsFightUpdate
        {
            get
            {
                if (!IsApplied) return false;
                byte[] overlay = ReadOverlay();
                return overlay[FightRect] == OriginalFightTop && overlay[FightRect + 1] == OriginalFightBottom;
            }
        }

        public static void WidenFightPress()
        {
            byte[] overlay = ReadOverlay();
            WidenFight(overlay);
            DSUtils.WriteWholeFile(OverlayUtils.GetPath(RomLocator.ButtonPressSites.Overlay), overlay);
        }

        private const int OriginalUp = 192, OriginalDown = 384;
    }
}
