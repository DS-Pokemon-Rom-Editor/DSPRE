using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Lets the HGSS bag's six item buttons move. Routines in the synthetic overlay clear empty slots using lists DSPRE writes
    /// for the current spots.
    /// </summary>
    public static class BagButtonsPatch
    {
        public const string Marker = "BAGBUTTONV1\0";

        // Assembled from bag_buttons.s.
        private static readonly byte[] Code =
        {
            0xf0, 0xb5, 0x04, 0x1c, 0x0d, 0x06, 0x2d, 0x0e, 0x16, 0x1c, 0x29, 0x1c, 0x24, 0x4b, 0x98, 0x47,
            0x07, 0x1c, 0x06, 0x2e, 0x04, 0xd8, 0xb6, 0x00, 0x24, 0x48, 0x86, 0x59, 0x00, 0xf0, 0x1a, 0xf8,
            0xf0, 0xbd, 0xf8, 0xb5, 0x04, 0x1c, 0x0d, 0x06, 0x2d, 0x0e, 0x16, 0x1c, 0x29, 0x1c, 0x1c, 0x4b,
            0x98, 0x47, 0x07, 0x1c, 0x06, 0x2e, 0x04, 0xd8, 0xb6, 0x00, 0x1d, 0x48, 0x86, 0x59, 0x00, 0xf0,
            0x09, 0xf8, 0x00, 0x9e, 0x05, 0x2e, 0x04, 0xd8, 0xb6, 0x00, 0x1a, 0x48, 0x86, 0x59, 0x00, 0xf0,
            0x01, 0xf8, 0xf8, 0xbd, 0x00, 0xb5, 0x85, 0xb0, 0x04, 0x96, 0x30, 0x78, 0x00, 0x28, 0x1c, 0xd0,
            0x31, 0x79, 0x00, 0x91, 0x71, 0x79, 0x01, 0x91, 0xb1, 0x79, 0x02, 0x91, 0xf3, 0x78, 0x01, 0x28,
            0x0a, 0xd1, 0xb2, 0x78, 0x52, 0x01, 0x71, 0x78, 0x52, 0x18, 0x52, 0x00, 0xba, 0x18, 0x20, 0x1c,
            0x29, 0x1c, 0x08, 0x4e, 0xb0, 0x47, 0x05, 0xe0, 0x00, 0x22, 0x03, 0x92, 0x20, 0x1c, 0x29, 0x1c,
            0x05, 0x4e, 0xb0, 0x47, 0x04, 0x9e, 0x08, 0x36, 0xde, 0xe7, 0x05, 0xb0, 0x00, 0xbd, 0xc0, 0x46,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        };
        private const int MoveEntry = 0x22, GetBufferAt = 0xA0, CopyAt = 0xA4, FillAt = 0xA8, NormalListsAt = 0xAC, MoveListsAt = 0xB0, PickedListsAt = 0xB4;

        // After the code: the six buttons' tile spots, then the list pointers and the lists, each a fixed size.
        public const int Buttons = 6;
        private const int SlotsAt = 0xB8, NormalPointers = SlotsAt + Buttons * 2, MovePointers = NormalPointers + 7 * 4, PickedPointers = MovePointers + 7 * 4;
        private const int Command = 8, NormalCommands = 16, MoveCommands = 8, PickedCommands = 4;
        private const int NormalAt = PickedPointers + Buttons * 4, MoveAt = NormalAt + 7 * NormalCommands * Command,
                          PickedAt = MoveAt + 7 * MoveCommands * Command, CodeLength = PickedAt + Buttons * PickedCommands * Command;
        private static int CodeStart => SyntheticOverlaySpace.HeaderSize;
        public static int BlockLength => CodeStart + CodeLength;

        // Button sizes and the rows they may use: the pocket tabs end at row 3, the bottom bar starts at row 21.
        public const int Width = 16, Height = 6, TopRow = 4, BottomRow = 20;
        public static readonly (int X, int Y)[] GameSpots = { (0, 4), (16, 4), (0, 9), (16, 9), (0, 14), (16, 14) };

        // Map 43 has the buttons over 39's empty-slot outlines, and 44 the move-mode frames over 42. All are 32 by 32 with the
        // bottom 8 rows off screen.
        public const int ButtonMap = 43, BackMap = 39, MoveMap = 44, MoveBackMap = 42;
        public static readonly int[] Maps = { ButtonMap, BackMap, MoveMap, MoveBackMap };
        private static bool Framed(int map) => map is MoveMap or MoveBackMap;
        private const int ScreenData = 0x24, MapRows = 32;
        public const int Columns = 32;
        // Each button map keeps 16-tile strips off screen: top edge, four middle rows, a shared edge, bottom edge, then the plain
        // square for where a button was.
        private const int StripRow = 24, Top = 0, Middle = 1, Shared = 5, Bottom = 6, Strips = 7;
        // Each frame map keeps a copy of a button's 14 by 4 frame at row 30, then its plain square. Map 44 rows 24 and 27 hold the
        // picked button alone and under another button.
        private const int FrameRow = 30, FrameLeft = 2, FrameTop = 2, FrameWidth = 14, FrameHeight = 4, FramePlain = 56, PickedAlone = 24, PickedUnder = 27;

        public static bool Supports => RomLocator.Where(RomLocator.Thing.BagButtonDraws) is { Offsets.Length: 6 }
                                       && RomLocator.Where(RomLocator.Thing.BagSpriteTemplates) != null;

        private static RomLocator.Spot Draws => RomLocator.Where(RomLocator.Thing.BagButtonDraws);
        private static string CodePath => OverlayUtils.GetPath(Draws.Overlay);

        private static byte[] ReadCode() => RomLocator.ReadFile(RomLocator.Thing.BagButtonDraws);

        private static uint Address(int at) => RomLocator.RamBase(Draws) + (uint)at;

        private static uint? CallAt(byte[] code, int at) => ThumbBl.Target(code, at, Address(at));

        /// <summary>Where the block is in the synthetic overlay when the patch is in, or -1.</summary>
        public static long BlockOffset()
        {
            if (!Supports || !File.Exists(Filesystem.ExpArmPath)) return -1;
            try
            {
                if (CallAt(ReadCode(), Draws.Offsets[2]) is not uint routine) return -1;
                long block = (long)routine - RomInfo.SynthOverlayLoadAddress - CodeStart;
                if (block < 0 || block + BlockLength > new FileInfo(Filesystem.ExpArmPath).Length) return -1;
                return Encoding.ASCII.GetString(DSUtils.ReadFromFile(Filesystem.ExpArmPath, (uint)block, Marker.Length)) == Marker ? block : -1;
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException) { return -1; }
        }

        public static bool IsApplied => BlockOffset() >= 0;

        /// <summary>Why the patch can't go in now, or null.</summary>
        public static string WhyNotApplicable()
        {
            if (!Supports) return "This patch doesn't support this ROM yet.";
            if (!SyntheticOverlaySpace.Available()) return "Apply the ARM9 expansion first.";
            if (IsApplied) return "The patch is already applied.";
            if (GameCalls(ReadCode()) == null) return "Another patch already changed how the bag draws its item buttons.";
            foreach (int member in Maps)
            {
                byte[] map = ReadMap(member);
                if (map == null) return "The bag's button screens couldn't be read.";
                if (!(Framed(member) ? Blank(map, FrameRow, 2) : Blank(map, StripRow, 4))) return "The bag's button screens already have art below the screen.";
            }
            return null;
        }

        // The game's tilemap calls from the normal-mode routine, or null if any draw call no longer goes to the game's routines.
        private static (uint Buffer, uint Copy, uint Fill)? GameCalls(byte[] code)
        {
            int[] at = Draws.Offsets;
            uint normal = Address(at[0]), move = Address(at[1]);
            if (at.Skip(2).Take(3).Any(c => CallAt(code, c) != normal) || CallAt(code, at[5]) != move) return null;
            if (CallAt(code, at[0] + 0x0E) is not uint buffer || CallAt(code, at[0] + 0x4C) is not uint copy || CallAt(code, at[0] + 0x6E) is not uint fill) return null;
            return (buffer, copy, fill);
        }

        /// <summary>Writes the block at <paramref name="blockOffset"/>, sends the four draw calls to it and keeps the art strips the moves need.</summary>
        public static void Apply(int blockOffset)
        {
            if (WhyNotApplicable() is string why) throw new InvalidOperationException(why);
            byte[] code = ReadCode();
            (uint buffer, uint copy, uint fill) = GameCalls(code).Value;
            uint start = RomInfo.SynthOverlayLoadAddress + (uint)blockOffset + (uint)CodeStart;
            byte[] block = new byte[BlockLength];
            SyntheticOverlaySpace.Header(Marker, BlockLength).CopyTo(block, 0);
            Code.CopyTo(block, CodeStart);
            void Word(int at, uint value) => BitConverter.GetBytes(value).CopyTo(block, CodeStart + at);
            Word(GetBufferAt, buffer | 1);
            Word(CopyAt, copy | 1);
            Word(FillAt, fill | 1);
            Word(NormalListsAt, start + NormalPointers);
            Word(MoveListsAt, start + MovePointers);
            Word(PickedListsAt, start + PickedPointers);
            for (int n = 0; n <= Buttons; n++)
            {
                Word(NormalPointers + n * 4, start + (uint)(NormalAt + n * NormalCommands * Command));
                Word(MovePointers + n * 4, start + (uint)(MoveAt + n * MoveCommands * Command));
            }
            for (int b = 0; b < Buttons; b++) Word(PickedPointers + b * 4, start + (uint)(PickedAt + b * PickedCommands * Command));
            Spots(GameSpots).CopyTo(block, CodeStart + SlotsAt);
            Lists(GameSpots).CopyTo(block, CodeStart + NormalAt);
            DSUtils.WriteToFile(Filesystem.ExpArmPath, block, (uint)blockOffset);

            // Strips are copied from button 1's rows, the edge it shares with button 3, and button 5's bottom row. Backdrops fill
            // moved-from spots with the tile below the buttons, button maps with tile 0.
            int[] rows = { 4, 5, 6, 7, 8, 9, 19 };
            foreach (int member in Maps)
            {
                byte[] map = ReadMap(member);
                bool back = member is BackMap or MoveBackMap;
                if (Framed(member))
                {
                    for (int i = 0; i < FrameWidth * FrameHeight; i++)
                        Put(map, FrameRow, i, Get(map, 4 + FrameTop + i / FrameWidth, FrameLeft + i % FrameWidth));
                    Put(map, FrameRow, FramePlain, back ? Get(map, 5, 0) : (ushort)0);
                }
                else
                {
                    for (int s = 0; s < Strips; s++)
                        for (int c = 0; c < Width; c++) Put(map, StripRow, s * Width + c, Get(map, rows[s], c));
                    Put(map, StripRow, Strips * Width, back ? Get(map, 20, 0) : (ushort)0);
                }
                WriteMap(member, map);
            }

            int[] at = Draws.Offsets;
            foreach (int call in at.Skip(2).Take(3)) DSUtils.WriteToFile(CodePath, SummaryFontPatch.Bl(Address(call), start), (uint)call);
            DSUtils.WriteToFile(CodePath, SummaryFontPatch.Bl(Address(at[5]), start + MoveEntry), (uint)at[5]);
        }

        /// <summary>Where the spots and lists sit in the synthetic overlay. A button move rewrites both.</summary>
        public static (long Spots, long Lists) Places(long block) => (block + CodeStart + SlotsAt, block + CodeStart + NormalAt);

        public static int ListsLength => CodeLength - NormalAt;

        public static byte[] Spots((int X, int Y)[] spots) => spots.SelectMany(s => new[] { (byte)s.X, (byte)s.Y }).ToArray();

        /// <summary>The command lists for buttons at <paramref name="spots"/>, as the block holds them.</summary>
        public static byte[] Lists((int X, int Y)[] spots)
        {
            byte[] lists = new byte[ListsLength];
            void Commands(int at, int room, IEnumerable<byte[]> commands)
            {
                int n = 0;
                foreach (byte[] c in commands)
                {
                    if (n == room - 1) throw new InvalidOperationException("The button lists don't fit.");
                    c.CopyTo(lists, at - NormalAt + n++ * Command);
                }
            }
            for (int shown = 0; shown <= Buttons; shown++)
            {
                Commands(NormalAt + shown * NormalCommands * Command, NormalCommands, NormalList(spots, shown));
                Commands(MoveAt + shown * MoveCommands * Command, MoveCommands,
                         Enumerable.Range(shown, Buttons - shown).Select(b => Fill(spots[b].X + FrameLeft, spots[b].Y + FrameTop, FrameWidth, FrameHeight)));
            }
            for (int b = 0; b < Buttons; b++)
                Commands(PickedAt + b * PickedCommands * Command, PickedCommands,
                         new[] { Copy(0, Above(spots, b) >= 0 ? PickedUnder : PickedAlone, spots[b].X, spots[b].Y, Width, Height) });
            return lists;
        }

        // Clears unused buttons, then redraws any edge a shown button shared with one.
        private static IEnumerable<byte[]> NormalList((int X, int Y)[] spots, int shown)
        {
            for (int b = shown; b < Buttons; b++) yield return Fill(spots[b].X, spots[b].Y, Width, Height);
            for (int b = 0; b < shown; b++)
            {
                if (Below(spots, b) is int under && under >= shown) yield return CopyStrip(Bottom, spots[b].X, spots[b].Y + Height - 1);
                if (Above(spots, b) is int over && over >= shown) yield return CopyStrip(Top, spots[b].X, spots[b].Y);
            }
        }

        // A strip row holds two strips.
        private static (int Column, int Row) StripAt(int strip) => (strip % 2 * Width, StripRow + strip / 2);

        private static byte[] CopyStrip(int strip, int x, int y)
        {
            (int column, int row) = StripAt(strip);
            return Copy(column, row, x, y, Width, 1);
        }

        private static byte[] Copy(int fromX, int fromY, int x, int y, int w, int h) =>
            new byte[] { 1, (byte)fromX, (byte)fromY, (byte)x, (byte)y, (byte)w, (byte)h, 0 };

        private static byte[] Fill(int x, int y, int w, int h) => new byte[] { 2, 0, 0, (byte)x, (byte)y, (byte)w, (byte)h, 0 };

        /// <summary>The button whose bottom edge is this button's top edge, or -1.</summary>
        public static int Above((int X, int Y)[] spots, int b) =>
            Array.FindIndex(spots, s => s.X == spots[b].X && s.Y + Height - 1 == spots[b].Y);

        /// <summary>The button whose top edge is this button's bottom edge, or -1.</summary>
        public static int Below((int X, int Y)[] spots, int b) =>
            Array.FindIndex(spots, s => s.X == spots[b].X && s.Y == spots[b].Y + Height - 1);

        /// <summary>The buttons' spots as the block holds them, or the game's when the patch isn't in.</summary>
        public static (int X, int Y)[] ReadSpots()
        {
            long block = BlockOffset();
            if (block < 0) return ((int, int)[])GameSpots.Clone();
            return FromBytes(DSUtils.ReadFromFile(Filesystem.ExpArmPath, (uint)Places(block).Spots, Buttons * 2));
        }

        public static (int X, int Y)[] FromBytes(byte[] b, int at = 0) =>
            Enumerable.Range(0, Buttons).Select(i => ((int)b[at + i * 2], (int)b[at + i * 2 + 1])).ToArray();

        /// <summary>Why buttons can't sit at these spots, or null.</summary>
        public static string WhyNot((int X, int Y)[] spots)
        {
            for (int b = 0; b < Buttons; b++)
            {
                (int x, int y) = spots[b];
                if (x < 0 || x + Width > Columns || y < TopRow || y + Height - 1 > BottomRow)
                    return $"Item {b + 1} has to stay on the screen between the pocket tabs and the bottom bar.";
                for (int o = 0; o < b; o++)
                {
                    (int ox, int oy) = spots[o];
                    bool across = x < ox + Width && ox < x + Width, down = y < oy + Height && oy < y + Height;
                    bool shareEdge = x == ox && (y == oy + Height - 1 || oy == y + Height - 1);
                    if (across && down && !shareEdge) return $"Item {b + 1} overlaps item {o + 1}.";
                }
            }
            return null;
        }

        /// <summary>Redraws one of <see cref="Maps"/> with the buttons at new spots, filling where they were with its plain square.</summary>
        public static void Compose(int member, byte[] map, (int X, int Y)[] was, (int X, int Y)[] now)
        {
            bool framed = Framed(member);
            ushort plain = framed ? Get(map, FrameRow, FramePlain) : Get(map, StripRow, Strips * Width);
            foreach ((int x, int y) in was)
                for (int r = 0; r < Height; r++)
                    for (int c = 0; c < Width; c++) Put(map, y + r, x + c, plain);
            for (int b = 0; b < now.Length; b++)
            {
                (int x, int y) = now[b];
                if (framed)
                {
                    for (int i = 0; i < FrameWidth * FrameHeight; i++)
                        Put(map, y + FrameTop + i / FrameWidth, x + FrameLeft + i % FrameWidth, Get(map, FrameRow, i));
                    continue;
                }
                for (int r = 0; r < Height; r++)
                {
                    int strip = r == 0 ? (Above(now, b) >= 0 ? Shared : Top) : r == Height - 1 ? (Below(now, b) >= 0 ? Shared : Bottom) : Middle + r - 1;
                    (int column, int row) = StripAt(strip);
                    for (int c = 0; c < Width; c++) Put(map, y + r, x + c, Get(map, row, column + c));
                }
            }
        }

        private static bool Blank(byte[] map, int row, int rows)
        {
            for (int i = 0; i < rows * Columns; i++) if (Get(map, row, i) != 0) return false;
            return true;
        }

        // A tilemap entry. A column past 31 runs on into the next row, which the frame copy relies on.
        private static ushort Get(byte[] map, int row, int column) => BitConverter.ToUInt16(map, ScreenData + (row * Columns + column) * 2);

        private static void Put(byte[] map, int row, int column, ushort entry)
        {
            int at = ScreenData + (row * Columns + column) * 2;
            if (row < 0 || at + 2 > map.Length) return;
            BitConverter.GetBytes(entry).CopyTo(map, at);
        }

        public static byte[] ReadMap(int member)
        {
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.BagGraphics });
            string path = ArchiveMembers.PathOf(DirNames.BagGraphics, member);
            if (!File.Exists(path)) return null;
            byte[] map = File.ReadAllBytes(path);
            return map.Length >= ScreenData + Columns * MapRows * 2 ? map : null;
        }

        public static void WriteMap(int member, byte[] map) => ArchiveMembers.Put(DirNames.BagGraphics, member, map);
    }
}
