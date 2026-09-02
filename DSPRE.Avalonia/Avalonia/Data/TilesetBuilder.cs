using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Turns a plain picture into the three files a background is drawn from: the colours, the tile
    /// sheet, and the arrangement that says which tile goes in each square of the screen.
    ///
    /// Nothing here quietly changes a picture to make it fit. When a picture asks for more than the
    /// screen can hold, the answer is a refusal that names the number, so the picture can be redrawn
    /// rather than silently worsened.
    /// </summary>
    public static class TilesetBuilder
    {
        /// <summary>The most tiles one arrangement can point at: the square keeps the number in ten bits.</summary>
        public const int MostTiles = 1024;
        /// <summary>The most colour banks a screen holds: the square keeps the bank in four bits.</summary>
        public const int MostBanks = 16;

        public sealed class Result
        {
            /// <summary>Why nothing was made, when nothing was. Everything else is unset when this is set.</summary>
            public string Whynot;

            public byte[] Colours;        // NCLR
            public byte[] Tiles;          // NCGR
            public byte[] Arrangement;    // NSCR

            public int Width, Height;
            public int Squares;               // squares of the screen
            public int TilesKept;             // tiles actually written
            public int RepeatedAsIs;          // squares that reused an earlier tile unturned
            public int RepeatedTurnedOver;    // squares that reused an earlier tile turned over
            public int Banks;                 // colour banks used
            public int ColoursKept;           // distinct colours written
            public int ColoursMergedByScreen; // colours the screen's five bits a channel ran together
            public bool ClearSlotKept;        // whether the first colour of every bank was left clear
            public bool EightBit;

            /// <summary>Things worth saying that are not refusals.</summary>
            public List<string> Notes = new();

            public string Summary => Whynot ?? $"{Width} by {Height}, {Squares} squares drawn from "
                + $"{TilesKept} tiles in {Banks} colour {(Banks == 1 ? "bank" : "banks")}.";
        }

        /// <param name="rgba">The picture, four bytes a pixel.</param>
        /// <param name="eightBit">One bank of 256 colours instead of sixteen banks of sixteen.</param>
        /// <param name="keepClearSlot">Leave the first colour of every bank clear, so whatever is behind
        /// shows through where the picture is see-through.</param>
        public static Result Build(byte[] rgba, int width, int height, bool eightBit, bool keepClearSlot)
        {
            var r = new Result { Width = width, Height = height, EightBit = eightBit };

            if (rgba == null || width <= 0 || height <= 0 || rgba.Length < width * height * 4)
                return Fail(r, "That picture has no pixels in it.");
            if (width % 8 != 0 || height % 8 != 0)
                return Fail(r, "A background is drawn in squares of eight pixels, so its size has to divide "
                             + $"by eight. That picture is {width} by {height}. The nearest that would fit "
                             + $"is {Round8(width)} by {Round8(height)}.");
            if (width > 1024 || height > 1024)
                return Fail(r, $"The widest and tallest a background goes is 1024. That picture is {width} "
                             + $"by {height}.");

            int cols = width / 8, rows = height / 8;
            r.Squares = cols * rows;

            // The screen keeps five bits of each channel, so colours that differ only below that come out
            // as one. Counting both tells the caller what the screen ran together.
            var fullColours = new HashSet<int>();
            var shortColour = new ushort[width * height];
            var clear = new bool[width * height];
            bool anyClear = false;
            for (int i = 0; i < width * height; i++)
            {
                if (rgba[i * 4 + 3] < 128) { clear[i] = true; anyClear = true; continue; }
                int cr = rgba[i * 4], cg = rgba[i * 4 + 1], cb = rgba[i * 4 + 2];
                fullColours.Add((cr << 16) | (cg << 8) | cb);
                shortColour[i] = (ushort)((cr >> 3) | ((cg >> 3) << 5) | ((cb >> 3) << 10));
            }

            r.ClearSlotKept = keepClearSlot && anyClear;
            int perBank = eightBit ? 256 : 16;
            int budget = perBank - (r.ClearSlotKept ? 1 : 0);

            if (anyClear && !r.ClearSlotKept)
            {
                // No slot is being held clear, so see-through pixels are not see-through any more. They
                // become black, which is what they already hold, and take up a colour like any other.
                for (int i = 0; i < clear.Length; i++) clear[i] = false;
                fullColours.Add(0);
                r.Notes.Add("See-through pixels were written as black, taking a colour of their own.");
            }

            var distinct = new HashSet<ushort>();
            for (int i = 0; i < shortColour.Length; i++) if (!clear[i]) distinct.Add(shortColour[i]);
            r.ColoursKept = distinct.Count;
            r.ColoursMergedByScreen = Math.Max(0, fullColours.Count - distinct.Count);
            if (r.ColoursMergedByScreen > 0)
                r.Notes.Add($"The screen keeps five bits a channel, so {fullColours.Count} colours "
                          + $"came out as {distinct.Count}.");

            // What each square asks for.
            var wants = new List<HashSet<ushort>>(r.Squares);
            for (int ty = 0; ty < rows; ty++)
                for (int tx = 0; tx < cols; tx++)
                {
                    var set = new HashSet<ushort>();
                    for (int py = 0; py < 8; py++)
                        for (int px = 0; px < 8; px++)
                        {
                            int i = (ty * 8 + py) * width + tx * 8 + px;
                            if (!clear[i]) set.Add(shortColour[i]);
                        }
                    wants.Add(set);
                }

            List<HashSet<ushort>> banks;
            int[] bankOf;

            if (eightBit)
            {
                // One list for the whole picture, so the only thing that can be too much is the number of
                // colours in it. No square has its own list to overflow.
                if (distinct.Count > budget)
                    return Fail(r, $"One list holds {budget} colours and that picture uses "
                                 + $"{distinct.Count}. Reduce it to {budget} and try again.");
                banks = new List<HashSet<ushort>> { distinct };
                bankOf = new int[r.Squares];
            }
            else
            {
                var tooRich = new List<(int x, int y, int count)>();
                for (int s = 0; s < wants.Count; s++)
                    if (wants[s].Count > budget) tooRich.Add(((s % cols) * 8, (s / cols) * 8, wants[s].Count));
                if (tooRich.Count > 0)
                {
                    string where = string.Join("; ", tooRich.OrderByDescending(t => t.count).Take(3)
                        .Select(t => $"the square at {t.x},{t.y} wants {t.count}"));
                    return Fail(r, $"A square of eight pixels can only draw from {budget} "
                                 + $"{(budget == 1 ? "colour" : "colours")} at once, and {tooRich.Count} "
                                 + $"{(tooRich.Count == 1 ? "square asks" : "squares ask")} for more: {where}. "
                                 + "Reduce those squares, or build it with 256 colours instead.");
                }

                if (!PackBanks(wants, budget, out banks, out bankOf))
                    return Fail(r, $"That picture needs more than {MostBanks} colour banks. Fewer colours in "
                                 + "a square, or more colours shared between squares, would bring it down, "
                                 + "and with 256 colours every square would draw from one list instead.");
            }
            r.Banks = banks.Count;

            // Colour numbers within each bank, settled once so the tiles and the colour list agree.
            var numberIn = new List<Dictionary<ushort, int>>();
            var bankColours = new List<List<ushort>>();
            foreach (var bank in banks)
            {
                var order = bank.OrderBy(c => c).ToList();
                var map = new Dictionary<ushort, int>();
                for (int i = 0; i < order.Count; i++) map[order[i]] = i + (r.ClearSlotKept ? 1 : 0);
                numberIn.Add(map);
                bankColours.Add(order);
            }

            // The tiles, sharing whatever repeats, including what only repeats turned over.
            var seen = new Dictionary<string, int>();
            var tilePixels = new List<byte[]>();
            var squares = new ushort[r.Squares];

            for (int ty = 0; ty < rows; ty++)
                for (int tx = 0; tx < cols; tx++)
                {
                    int s = ty * cols + tx;
                    int bank = bankOf[s];
                    var map = numberIn[bank];
                    var cell = new byte[64];
                    for (int py = 0; py < 8; py++)
                        for (int px = 0; px < 8; px++)
                        {
                            int i = (ty * 8 + py) * width + tx * 8 + px;
                            cell[py * 8 + px] = clear[i] && r.ClearSlotKept
                                ? (byte)0 : (byte)map[shortColour[i]];
                        }

                    int tile = -1;
                    bool flipH = false, flipV = false;
                    foreach (var (variant, h, v) in Turns(cell))
                        if (seen.TryGetValue(Key(variant, bank), out int found))
                        { tile = found; flipH = h; flipV = v; break; }

                    if (tile < 0)
                    {
                        tile = tilePixels.Count;
                        tilePixels.Add(cell);
                        seen[Key(cell, bank)] = tile;
                    }
                    else if (flipH || flipV) r.RepeatedTurnedOver++;
                    else r.RepeatedAsIs++;

                    squares[s] = (ushort)((tile & 0x3FF) | (flipH ? 1 << 10 : 0) | (flipV ? 1 << 11 : 0)
                                          | ((bank & 0xF) << 12));
                }

            r.TilesKept = tilePixels.Count;
            if (r.TilesKept > MostTiles)
                return Fail(r, $"An arrangement can only point at {MostTiles} tiles, and that picture needs "
                             + $"{r.TilesKept}. A smaller picture, or one that repeats itself more, would fit.");

            var palette = new ushort[eightBit ? 256 : r.Banks * 16];
            for (int b = 0; b < r.Banks; b++)
                for (int i = 0; i < bankColours[b].Count; i++)
                    palette[b * perBank + i + (r.ClearSlotKept ? 1 : 0)] = bankColours[b][i];

            var pixels = new byte[tilePixels.Count * 64];
            for (int t = 0; t < tilePixels.Count; t++) Array.Copy(tilePixels[t], 0, pixels, t * 64, 64);

            r.Colours = NitroBgWrite.Palette(palette, eightBit);
            r.Tiles = NitroBgWrite.Tiles(pixels, tilePixels.Count, eightBit);
            r.Arrangement = NitroBgWrite.Arrangement(ToBlocks(squares, cols, rows), width, height);
            return r;
        }

        private static Result Fail(Result r, string why) { r.Whynot = why; return r; }
        private static int Round8(int v) => Math.Max(8, v - v % 8);

        /// <summary>Fits every square's colours into as few banks as will hold them.</summary>
        private static bool PackBanks(List<HashSet<ushort>> wants, int budget,
                                      out List<HashSet<ushort>> banks, out int[] bankOf)
        {
            var made = new List<HashSet<ushort>>();
            banks = made;
            bankOf = new int[wants.Count];

            // Hardest squares first: a square wanting many colours has the fewest banks that can take it,
            // and settling those first leaves the easy ones to fill the gaps.
            foreach (int s in Enumerable.Range(0, wants.Count).OrderByDescending(i => wants[i].Count))
            {
                int best = -1, bestCost = int.MaxValue;
                for (int b = 0; b < made.Count; b++)
                {
                    var bank = made[b];
                    int added = wants[s].Count(c => !bank.Contains(c));
                    if (bank.Count + added > budget) continue;
                    if (added < bestCost) { bestCost = added; best = b; }
                    if (added == 0) break;
                }
                if (best < 0)
                {
                    if (made.Count >= MostBanks) return false;
                    best = made.Count;
                    made.Add(new HashSet<ushort>());
                }
                foreach (ushort c in wants[s]) made[best].Add(c);
                bankOf[s] = best;
            }
            return true;
        }

        // A tile and its three turnings. The same pair of flags turns the stored tile into what the
        // square wants and the square's picture back into the stored tile, because turning twice undoes it.
        private static IEnumerable<(byte[] pixels, bool h, bool v)> Turns(byte[] cell)
        {
            yield return (cell, false, false);
            yield return (Turn(cell, true, false), true, false);
            yield return (Turn(cell, false, true), false, true);
            yield return (Turn(cell, true, true), true, true);
        }

        private static byte[] Turn(byte[] cell, bool h, bool v)
        {
            var o = new byte[64];
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    o[y * 8 + x] = cell[(v ? 7 - y : y) * 8 + (h ? 7 - x : x)];
            return o;
        }

        // Two squares can only share a tile when they read it from the same bank, so the bank is part of
        // what makes a tile the same tile.
        private static string Key(byte[] cell, int bank)
        {
            var chars = new char[65];
            chars[0] = (char)('A' + bank);
            for (int i = 0; i < 64; i++) chars[i + 1] = (char)('0' + cell[i]);
            return new string(chars);
        }

        /// <summary>Lays the squares out the way the games store them, which NitroBgCodec explains.</summary>
        private static ushort[] ToBlocks(ushort[] squares, int cols, int rows)
        {
            var o = new ushort[NitroBgCodec.SquareCount(cols, rows)];
            for (int ty = 0; ty < rows; ty++)
                for (int tx = 0; tx < cols; tx++)
                    o[NitroBgCodec.SquareIndex(cols, tx, ty)] = squares[ty * cols + tx];
            return o;
        }
    }
}
