using DSPRE.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Screens
{
    /// <summary>
    /// Fits pictures to the battle Command buttons. Each press frame is the art plus the pixels the game changes in that frame,
    /// so a frame that covers the button stays as it was and one that only touches the edge keeps the art.
    /// </summary>
    public sealed class TouchButtonFit
    {
        public enum FlashKind { Automatic, Game, Lighter, Colour }

        public const int ButtonCount = 4;
        private const int Tile = 8, TileBytes = BattlePressFrames.TileBytes, Columns = BattlePressFrames.Columns, ScreenRows = 24, RowColours = 16;
        // Past this share of the button, a frame would hide the art.
        private const double CoversArt = 0.5;

        public static readonly string[] Names = { "Fight", "Bag", "Pokémon", "Run" };

        /// <summary>What the user picked. Pictures are RGBA, each the size of its button, and null means no change.</summary>
        public sealed class Settings
        {
            public byte[][] Pictures = new byte[ButtonCount][];
            public int Merge;
            public FlashKind[] Kind = new FlashKind[ButtonCount];
            public ushort[][] Flash = new ushort[ButtonCount][];     // 16 colours for FlashKind.Colour
            public byte[] Whole;                                     // the imported picture, for art past the buttons
            public int WholeWidth;
            /// <summary>Colours to give a place first when a button's row runs out.</summary>
            public IReadOnlyCollection<ushort> Keep;
        }

        public sealed class Plan
        {
            public int[] Tiles = new int[ButtonCount], Squares = new int[ButtonCount], NewColours = new int[ButtonCount], NearestColours = new int[ButtonCount];
            public int Need, Room, Merged;
            public bool Fits;
            public List<int> NewSquares = new();                     // screen squares that point at new tiles
            public List<int> ChangedSquares = new();                 // screen squares showing a colour that didn't fit
            public List<(ushort Colour, ushort Shown, int Pixels)> Swaps = new();
            public bool CanKeep;
            internal List<(int Slot, byte[] Numbers, byte[] Frontier)> NewTiles = new();
            internal List<(int Page, int Square, int Entry)> Entries = new();
            internal List<(int Row, int Number, ushort Colour)> Colours = new();
        }

        private sealed class Wanted
        {
            public int Button, Page, Square, Row;
            public byte[] Numbers, Frontier;
        }

        private readonly BattlePressFrames.Members _panel = BattlePressFrames.Panel;
        private readonly ScriptNarc _narc = new ScriptNarc(DirNames.BattleBg);
        private readonly byte[] _sheet, _frontierSheet, _commandFile;
        private readonly byte[] _storedSheet, _storedFrontierSheet, _storedCommand;
        private readonly int _tilesAt, _frontierTilesAt;
        private readonly ushort[][] _command;
        private readonly List<ushort[][]> _others = new();
        private readonly ushort[] _colours, _frontierColours, _original;

        public TouchButtonFit()
        {
            _storedSheet = _narc.Get(_panel.Sheet);
            _storedCommand = _narc.Get(_panel.Command);
            _storedFrontierSheet = _narc.Get(_panel.FrontierSheet);
            _sheet = GraphicAssets.Unsqueeze(_storedSheet);
            _frontierSheet = GraphicAssets.Unsqueeze(_storedFrontierSheet);
            _commandFile = GraphicAssets.Unsqueeze(_storedCommand);
            (_, _tilesAt) = NitroBgCodec.ReadTileHeader(_sheet);
            (_, _frontierTilesAt) = NitroBgCodec.ReadTileHeader(_frontierSheet);
            _command = BattlePressFrames.ReadPages(_commandFile);
            _colours = NitroBgCodec.ReadColours(GraphicAssets.Unsqueeze(_narc.Get(_panel.Palette)));
            _frontierColours = NitroBgCodec.ReadColours(GraphicAssets.Unsqueeze(_narc.Get(_panel.FrontierPalette)));
            _original = _command[BattlePressFrames.OriginalColours];
            foreach (int id in new[] { _panel.Background, _panel.FrontierBackground, _panel.Silhouette, _panel.Moves, _panel.Target, _panel.Outlines, _panel.YesNo, _panel.Stop })
                _others.Add(BattlePressFrames.ReadPages(GraphicAssets.Unsqueeze(_narc.Get(id))));
        }

        public static (int X, int Y, int Width, int Height) PixelRect(int button)
        {
            BattlePressFrames.Rect r = BattlePressFrames.CommandRects[button];
            return (r.Left * Tile, r.Top * Tile, (r.Right - r.Left + 1) * Tile, (r.Bottom - r.Top + 1) * Tile);
        }

        public static byte[] CutButton(byte[] rgba, int width, int button)
        {
            (int x, int y, int w, int h) = PixelRect(button);
            byte[] cut = new byte[w * h * 4];
            for (int row = 0; row < h; row++)
                Array.Copy(rgba, ((y + row) * width + x) * 4, cut, row * w * 4, w * 4);
            return cut;
        }

        private byte[] Normal(int tile) => TileFit.Numbers(_sheet, _tilesAt, tile);
        private byte[] Frontier(int tile) => TileFit.Numbers(_frontierSheet, _frontierTilesAt, tile);

        private byte[] Square(int entry, bool frontier) => BattlePressFrames.Flip(frontier ? Frontier(entry & 0x3FF) : Normal(entry & 0x3FF), entry & 0xC00);

        private static int Row(int entry) => entry >> 12 & 15;

        private ushort Original(int row, int number) => _original[row * RowColours + number];

        // Keeps every tile still on show: other screens, the saved originals and squares the art doesn't replace.
        private bool[] KeptTiles(HashSet<int> replaced)
        {
            bool[] keep = new bool[BattlePressFrames.LoadedTiles];
            keep[BattlePressFrames.BlankTile] = true;
            void Keep(ushort entry) { if ((entry & 0x3FF) < keep.Length) keep[entry & 0x3FF] = true; }
            foreach (ushort[][] pages in _others)
                foreach (ushort[] page in pages.Where((_, p) => p != BattlePressFrames.OriginalColours))
                    foreach (ushort e in page) Keep(e);
            foreach (int p in new[] { BattlePressFrames.OriginalNormal, BattlePressFrames.OriginalPale, BattlePressFrames.OriginalWhite })
                foreach (ushort e in _command[p]) Keep(e);
            foreach (int p in new[] { BattlePressFrames.NormalPage, BattlePressFrames.PalePage, BattlePressFrames.WhitePage, BattlePressFrames.SlotPage })
                for (int i = 0; i < _command[p].Length; i++)
                    if (!replaced.Contains(i)) Keep(_command[p][i]);
            return keep;
        }

        // Something else shows these colour numbers in this row, so their colours can't change.
        private bool[] FixedNumbers(int row, HashSet<int> replaced)
        {
            bool[] used = new bool[RowColours];
            void Note(ushort entry)
            {
                if (Row(entry) != row || (entry & 0x3FF) == BattlePressFrames.BlankTile) return;
                foreach (byte n in Normal(entry & 0x3FF)) used[n] = true;
            }
            foreach (ushort[][] pages in _others)
                foreach (ushort[] page in pages.Take(BattlePressFrames.GamePages))
                    foreach (ushort e in page) Note(e);
            foreach (int p in new[] { BattlePressFrames.NormalPage, BattlePressFrames.PalePage, BattlePressFrames.WhitePage })
                for (int i = 0; i < _command[p].Length; i++)
                    if (!replaced.Contains(i)) Note(_command[p][i]);
            used[0] = true;
            return used;
        }

        private sealed class Frame
        {
            public int Page, Row;
            public int[] Number;       // colour number in the button's row, -1 for clear
            public int[] Frontier;
        }

        private static ushort Lighter(ushort c)
        {
            int f(int v) => Math.Min(31, (int)Math.Round(v + (31 - v) * 0.62));
            return (ushort)(f(c & 31) | f(c >> 5 & 31) << 5 | f(c >> 10 & 31) << 10);
        }

        /// <summary>A light-to-dark set from one colour, following the shades of the Fight button.</summary>
        public ushort[] FromColour(ushort colour)
        {
            ushort[] set = new ushort[RowColours];
            for (int n = 0; n < RowColours; n++)
            {
                ushort c = _original[RowColours + n];
                double light = Math.Min(1, (20 + ((c & 31) * 0.3 + (c >> 5 & 31) * 0.6 + (c >> 10 & 31) * 0.1) * 11 / 31) / 31);
                int f(int v) => (int)Math.Round(v * light);
                set[n] = (ushort)(f(colour & 31) | f(colour >> 5 & 31) << 5 | f(colour >> 10 & 31) << 10);
            }
            return set;
        }

        public IEnumerable<ushort> ButtonColours() =>
            Enumerable.Range(0, ButtonCount).SelectMany(b => _original.Skip(BattlePressFrames.Rows[b] * RowColours + 1).Take(RowColours - 1)).Where(c => c != 0).Distinct();

        /// <summary>Works out what the picked settings need. Nothing is written.</summary>
        public Plan Make(Settings s)
        {
            Plan plan = new Plan();
            List<Wanted> wanted = new List<Wanted>();
            HashSet<int> replaced = new HashSet<int>();
            for (int b = 0; b < ButtonCount; b++)
            {
                if (s.Pictures[b] == null) continue;
                BattlePressFrames.Rect r = BattlePressFrames.CommandRects[b];
                for (int y = r.Top; y <= r.Bottom; y++)
                    for (int x = r.Left; x <= r.Right; x++) replaced.Add(y * Columns + x);
            }
            List<int>[] around = Around(s);
            for (int b = 0; b < ButtonCount; b++)
            {
                replaced.UnionWith(around[b]);
                if (s.Pictures[b] != null) FitButton(s, plan, b, replaced, wanted, around[b]);
            }

            Allocate(s, plan, wanted, KeptTiles(replaced));
            return plan;
        }

        // Original pixels of one frame of a button, in screen order: the colour, or -1 for clear.
        private int[] OriginalPixels(int page, int b, bool frontier)
        {
            BattlePressFrames.Rect r = BattlePressFrames.CommandRects[b];
            int width = (r.Right - r.Left + 1) * Tile, height = (r.Bottom - r.Top + 1) * Tile, row = BattlePressFrames.Rows[b];
            int[] pixels = new int[width * height];
            for (int y = r.Top; y <= r.Bottom; y++)
                for (int x = r.Left; x <= r.Right; x++)
                {
                    int entry = _command[page][y * Columns + x];
                    byte[] numbers = Square(entry, frontier);
                    for (int i = 0; i < 64; i++)
                        pixels[((y - r.Top) * Tile + i / Tile) * width + (x - r.Left) * Tile + i % Tile] = numbers[i] == 0 ? -1 : Original(row, numbers[i]) & 0x7FFF;
                }
            return pixels;
        }

        private static int Distance(BattlePressFrames.Rect r, int x, int y) =>
            Math.Max(Math.Max(r.Left - x, x - r.Right), 0) + Math.Max(Math.Max(r.Top - y, y - r.Bottom), 0);

        // Gives each picture square outside the buttons to the nearest button, so it can use that button's colour row.
        private List<int>[] Around(Settings s)
        {
            List<int>[] squares = Enumerable.Range(0, ButtonCount).Select(_ => new List<int>()).ToArray();
            if (s.Whole == null) return squares;
            BattlePressFrames.Rect[] rects = BattlePressFrames.CommandRects;
            for (int y = 0; y < ScreenRows; y++)
                for (int x = 0; x < Columns; x++)
                {
                    if (rects.Any(r => Distance(r, x, y) == 0) || WholeSquare(s, x, y).All(c => c < 0)) continue;
                    int owner = Enumerable.Range(0, ButtonCount).OrderBy(b => Distance(rects[b], x, y)).First();
                    if (s.Pictures[owner] != null) squares[owner].Add(y * Columns + x);
                }
            return squares;
        }

        private static int[] WholeSquare(Settings s, int x, int y)
        {
            int[] pixels = new int[Tile * Tile];
            int height = s.Whole.Length / 4 / s.WholeWidth;
            for (int i = 0; i < pixels.Length; i++)
            {
                int px = x * Tile + i % Tile, py = y * Tile + i / Tile, at = (py * s.WholeWidth + px) * 4;
                pixels[i] = px >= s.WholeWidth || py >= height ? -1 : TileFit.Colour(s.Whole, at);
            }
            return pixels;
        }

        private void FitButton(Settings s, Plan plan, int b, HashSet<int> replaced, List<Wanted> wanted, List<int> around)
        {
            BattlePressFrames.Rect r = BattlePressFrames.CommandRects[b];
            int row = BattlePressFrames.Rows[b], width = (r.Right - r.Left + 1) * Tile, height = (r.Bottom - r.Top + 1) * Tile, count = width * height;
            byte[] rgba = s.Pictures[b];
            int[] art = new int[count];
            for (int p = 0; p < count; p++)
                art[p] = TileFit.Colour(rgba, p * 4);

            int[] normal = OriginalPixels(BattlePressFrames.OriginalNormal, b, false), frontierNormal = OriginalPixels(BattlePressFrames.OriginalNormal, b, true);
            // A frame that covers the button replaces it. A smaller one only touches squares the art left as they were.
            int[] FrameOver(int[] picture, int[] first, int[] frame, bool covers)
            {
                bool[] drawnOn = new bool[count / (Tile * Tile)];
                int SquareOf(int p) => p / width / Tile * (width / Tile) + p % width / Tile;
                for (int p = 0; p < count; p++) if (picture[p] != first[p]) drawnOn[SquareOf(p)] = true;
                int[] outp = new int[count];
                for (int p = 0; p < count; p++) outp[p] = frame[p] != first[p] && (covers || !drawnOn[SquareOf(p)]) ? frame[p] : picture[p];
                return outp;
            }
            // Frontier draws Fight a pixel larger, so art goes over Frontier's own pixels, unless the art gives the button another shape.
            bool reshaped = Enumerable.Range(0, count).Any(p => art[p] < 0 && normal[p] >= 0);
            int[] frontierArt = new int[count];
            for (int p = 0; p < count; p++) frontierArt[p] = reshaped || art[p] != normal[p] ? art[p] : frontierNormal[p];

            TileFit.RowColours colours = new TileFit.RowColours(_colours, _original, row, FixedNumbers(row, replaced), s.Keep);
            List<(int Page, int[] Normal, int[] Frontier, bool Flash)> frames = new() { (BattlePressFrames.NormalPage, art, frontierArt, false) };
            bool usesFlash = false;
            FlashKind kind = s.Kind[b];
            foreach ((int page, int source) in new[] { (BattlePressFrames.PalePage, BattlePressFrames.OriginalPale), (BattlePressFrames.WhitePage, BattlePressFrames.OriginalWhite) })
            {
                int[] frame = OriginalPixels(source, b, false), frontierFrame = OriginalPixels(source, b, true);
                int changed = 0;
                for (int p = 0; p < count; p++) if (frame[p] != normal[p]) changed++;
                bool covers = changed > count * CoversArt;
                bool flash = kind != FlashKind.Game && (covers || kind != FlashKind.Automatic);
                if (!flash)
                {
                    frames.Add((page, FrameOver(art, normal, frame, covers), FrameOver(frontierArt, frontierNormal, frontierFrame, covers), false));
                    continue;
                }
                usesFlash = true;
                // The flash shows the art. It keeps the frame's edge only where the art left the button alone, and reshaped art gets no edge.
                // The colour the frame paints most is its body, so that one goes.
                int body = frame.Where((c, p) => c != normal[p] && c >= 0).GroupBy(c => c).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault(-1);
                int[] Edge(int[] picture, int[] first, int[] drawn)
                {
                    int[] outp = (int[])picture.Clone();
                    for (int p = 0; p < count; p++)
                        if (!reshaped && drawn[p] != first[p] && drawn[p] != body && picture[p] == first[p]) outp[p] = drawn[p] | EdgeMark;
                    return outp;
                }
                frames.Add((page, Edge(art, normal, frame), Edge(frontierArt, frontierNormal, frontierFrame), true));
            }

            foreach ((_, int[] pictureN, int[] pictureF, _) in frames)
                for (int p = 0; p < count; p++)
                {
                    colours.Want(pictureN[p]);
                    colours.Want(pictureF[p]);
                }
            foreach (int square in around)
                foreach (int colour in WholeSquare(s, square % Columns, square / Columns)) colours.Want(colour);
            colours.Settle();
            plan.NearestColours[b] = colours.Refused;
            plan.Swaps.AddRange(colours.Swaps);
            plan.CanKeep |= colours.Open > 0 && colours.Refused > 0;
            HashSet<int> swapped = colours.Swaps.Select(w => (int)w.Colour).ToHashSet();
            plan.NewColours[b] = colours.Added.Count(a => a.Colour != Original(row, a.Number));
            foreach ((int number, ushort colour) in colours.Added) plan.Colours.Add((row, number, colour));

            int flashRow = BattlePressFrames.FirstPaleRow + b;
            ushort[] rowNow = (ushort[])_colours.Clone();
            foreach ((int number, ushort colour) in colours.Added) rowNow[row * RowColours + number] = colour;
            ushort[] flashSet = null;
            Dictionary<ushort, int> edges = new();
            if (usesFlash)
            {
                bool[] taken = new bool[RowColours];
                taken[0] = true;
                foreach ((_, int[] a, int[] c, _) in frames)
                    foreach (int[] pixels in new[] { a, c })
                        foreach (int v in pixels.Where(v => v >= 0 && (v & EdgeMark) == 0)) taken[colours.Number(v)] = true;
                flashSet = new ushort[RowColours];
                for (int n = 1; n < RowColours; n++)
                    flashSet[n] = kind == FlashKind.Colour && s.Flash[b] != null ? s.Flash[b][n] : Lighter(rowNow[row * RowColours + n]);
                Stack<int> open = new Stack<int>(Enumerable.Range(1, RowColours - 1).Where(n => !taken[n]));
                foreach ((_, int[] a, int[] c, _) in frames)
                    foreach (int[] pixels in new[] { a, c })
                        foreach (int v in pixels.Where(v => v >= 0 && (v & EdgeMark) != 0).Distinct())
                        {
                            ushort colour = (ushort)(v & 0x7FFF);
                            if (edges.ContainsKey(colour) || open.Count == 0) continue;
                            int number = open.Pop();
                            edges[colour] = number;
                            flashSet[number] = colour;
                        }
                for (int n = 1; n < RowColours; n++) plan.Colours.Add((flashRow, n, flashSet[n]));
            }

            int Num(int value)
            {
                if (value < 0) return 0;
                if ((value & EdgeMark) != 0) return edges.TryGetValue((ushort)(value & 0x7FFF), out int e) ? e : 1;
                return colours.Number(value);
            }
            foreach ((int page, int[] pictureN, int[] pictureF, bool flashes) in frames)
            {
                for (int y = r.Top; y <= r.Bottom; y++)
                    for (int x = r.Left; x <= r.Right; x++)
                    {
                        int square = y * Columns + x;
                        byte[] n = new byte[64], f = new byte[64];
                        for (int i = 0; i < 64; i++)
                        {
                            int at = ((y - r.Top) * Tile + i / Tile) * width + (x - r.Left) * Tile + i % Tile;
                            n[i] = (byte)Num(pictureN[at]);
                            f[i] = (byte)Num(pictureF[at]);
                        }
                        if (page == BattlePressFrames.NormalPage)
                        {
                            plan.Squares[b]++;
                            if (swapped.Count > 0 && Enumerable.Range(0, 64).Any(i => swapped.Contains(pictureN[((y - r.Top) * Tile + i / Tile) * width + (x - r.Left) * Tile + i % Tile])))
                                plan.ChangedSquares.Add(square);
                        }
                        wanted.Add(new Wanted { Button = b, Page = page, Square = square, Row = flashes ? flashRow : row, Numbers = n, Frontier = f });
                    }
            }

            // A press copies only its own squares, so art past them looks the same in every frame. Fight's reach is wider.
            BattlePressFrames.Rect reach = BattlePressFrames.FightReach;
            foreach (int square in around)
            {
                int[] whole = WholeSquare(s, square % Columns, square / Columns);
                if (whole.Any(swapped.Contains)) plan.ChangedSquares.Add(square);
                byte[] n = whole.Select(c => (byte)Num(c)).ToArray();
                int x = square % Columns, y = square / Columns;
                bool pressed = b == 0 && x >= reach.Left && x <= reach.Right && y >= reach.Top && y <= reach.Bottom;
                foreach ((int page, _, _, bool flashes) in frames)
                    wanted.Add(new Wanted { Button = b, Page = page, Square = square, Row = flashes && pressed ? flashRow : row, Numbers = n, Frontier = n });
            }
        }

        // Marks a pixel the flash row has to show in its exact colour. Colours fill only 15 bits.
        private const int EdgeMark = TileFit.RowColours.CallerMark;

        private void Allocate(Settings s, Plan plan, List<Wanted> wanted, bool[] keep)
        {
            IEnumerable<(int, byte[][])> kept = Enumerable.Range(0, BattlePressFrames.BlankTile).Where(t => keep[t]).Select(t => (t, new[] { Normal(t), Frontier(t) }));
            // The replay screen's text box draws over these.
            List<int> free = Enumerable.Range(0, BattlePressFrames.BlankTile).Where(t => !keep[t] && (t < 0x20 || t > 0x28)).ToList();
            List<TileFit.Square> squares = wanted.Select(w => new TileFit.Square { Sheets = new[] { w.Numbers, w.Frontier }, Owner = w.Button }).ToList();
            TileFit.Placement placed = TileFit.Place(squares, kept, free, s.Merge);
            plan.Room = placed.Room;
            plan.Need = placed.Need;
            plan.Merged = placed.Merged;
            foreach ((int owner, int count) in placed.TilesByOwner) plan.Tiles[owner] += count;
            plan.Fits = placed.Fits;
            if (!plan.Fits) return;
            foreach ((int slot, byte[][] sheets) in placed.NewTiles) plan.NewTiles.Add((slot, sheets[0], sheets[1]));
            for (int i = 0; i < wanted.Count; i++)
            {
                (int tile, int flip) = placed.Squares[i];
                plan.Entries.Add((wanted[i].Page, wanted[i].Square, wanted[i].Row << 12 | flip | tile));
                if (wanted[i].Page == BattlePressFrames.NormalPage) plan.NewSquares.Add(wanted[i].Square);
            }
        }

        private void PutColours(int member, Plan plan)
        {
            byte[] stored = _narc.Get(member);
            byte marker = GraphicAssets.SqueezeMarker(stored);
            byte[] file = (byte[])GraphicAssets.Unsqueeze(stored).Clone();
            foreach ((int row, int number, ushort colour) in plan.Colours) NitroBgCodec.PutColour(file, row * RowColours + number, colour);
            _narc.Put(member, marker != 0 ? GraphicAssets.Squeeze(file, marker) : file);
        }

        private void PutSheet(int member, byte[] sheet, int tilesAt, byte[] stored, Plan plan, bool compress, bool frontier)
        {
            byte[] outp = (byte[])sheet.Clone();
            foreach ((int slot, byte[] normal, byte[] other) in plan.NewTiles)
            {
                byte[] numbers = frontier ? other : normal;
                for (int i = 0; i < TileBytes; i++)
                    outp[tilesAt + slot * TileBytes + i] = (byte)(numbers[i * 2] & 15 | numbers[i * 2 + 1] << 4);
            }
            byte marker = GraphicAssets.SqueezeMarker(stored);
            _narc.Put(member, compress && marker != 0 ? GraphicAssets.Squeeze(outp, marker) : outp);
        }

        /// <summary>Writes the plan through whatever staging is in use. Previews leave the files uncompressed.</summary>
        public string Apply(Plan plan, bool compress)
        {
            if (!plan.Fits) return "This art doesn't fit on the buttons.";
            PutSheet(_panel.Sheet, _sheet, _tilesAt, _storedSheet, plan, compress, false);
            PutSheet(_panel.FrontierSheet, _frontierSheet, _frontierTilesAt, _storedFrontierSheet, plan, compress, true);

            ushort[][] pages = _command.Select(p => (ushort[])p.Clone()).ToArray();
            foreach ((int page, int square, int entry) in plan.Entries) pages[page][square] = (ushort)entry;
            byte[] screen = BattlePressFrames.WithPages(_commandFile, pages);
            byte marker = GraphicAssets.SqueezeMarker(_storedCommand);
            _narc.Put(_panel.Command, compress && marker != 0 ? GraphicAssets.Squeeze(screen, marker) : screen);

            PutColours(_panel.Palette, plan);
            PutColours(_panel.FrontierPalette, plan);
            return null;
        }
    }
}
