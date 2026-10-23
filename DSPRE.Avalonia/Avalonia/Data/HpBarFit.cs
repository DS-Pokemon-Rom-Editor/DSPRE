using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Makes an imported HP bar look in battle the way it was drawn. The battle paints over parts of every bar:
    /// it writes the name, level and HP numbers on colour 15 with letters in 14 and shadow in 2, and copies the
    /// gauge, experience, status, Lv and caught pieces in from the battle code. So the import sets those three
    /// colours from the picture and rebuilds the pieces from the picture's own squares at those places, keeping
    /// only the game's fill, words and marks on top.
    /// </summary>
    public static class HpBarFit
    {
        private const int Tile = 8, TileBytes = 32;
        private const int TextBack = BattleGaugeGlyphs.Background, TextInk = BattleGaugeGlyphs.Letter, TextShadow = BattleGaugeGlyphs.Shadow;

        public sealed class Plan
        {
            public BattleGaugeComposer.Kind Kind;
            public uint Background, Ink, Shadow;                    // 0xRRGGBB at the DS's five bits a channel
            public SortedDictionary<int, uint> Colours = new();     // colour number to write, and its colour
            public List<string> Notes = new();                      // where the picture cannot be matched
            public bool ClearTextBackground;                        // made for a ROM with the clear battle text background patch
            public string CannotBecause;
        }

        public static BattleGaugeComposer.Kind? KindOf(int index)
        {
            if (BattleGaugePieces.Locate()?.HasSlots != true) return null;
            foreach (BattleGaugeComposer.Kind kind in Enum.GetValues<BattleGaugeComposer.Kind>())
            {
                string thing = BattleGaugeComposer.GraphicOf(kind);
                if (thing != null && (BattleObjects.Find(thing, "As it appears") == index || BattleObjects.Find(thing, "Drawing") == index))
                    return kind;
            }
            return null;
        }

        public static int ColoursEntry => BattleObjects.Find("HpBar.Shared", "Colours");

        private static bool YourDouble(BattleGaugeComposer.Kind k) =>
            k is BattleGaugeComposer.Kind.PlayerNear or BattleGaugeComposer.Kind.PlayerFar;

        /// <summary>Your bars show HP numbers: the single battle one always, the double battle ones after Start.</summary>
        private static bool ShowsNumbers(BattleGaugeComposer.Kind k) => k == BattleGaugeComposer.Kind.PlayerSingle || YourDouble(k);

        private static bool Theirs(BattleGaugeComposer.Kind k) =>
            k is BattleGaugeComposer.Kind.OpponentSingle or BattleGaugeComposer.Kind.OpponentFar or BattleGaugeComposer.Kind.OpponentNear;

        /// <summary>Tile number to its square in the bar's "as it appears" picture, from the bar's cell layout.</summary>
        private static Dictionary<int, (int X, int Y)> Squares(BattleGaugeComposer.Kind kind)
        {
            int layout = BattleObjects.Find(BattleGaugeComposer.GraphicOf(kind), "As it appears");
            byte[] ncer = layout >= 0 ? GraphicAssets.Unsqueeze(new ScriptNarc(RomInfo.DirNames.BattleObj).Get(layout)) : null;
            List<CellPlacement.Piece> pieces = CellPlacement.Pieces(ncer, 4);
            Dictionary<int, (int, int)> at = new();
            if (pieces == null) return at;
            int left = pieces.Min(p => p.X), top = pieces.Min(p => p.Y);
            foreach (CellPlacement.Piece p in pieces)
            {
                int across = p.Width / Tile;
                for (int k = 0; k < across * (p.Height / Tile); k++)
                    at[p.FirstTile + k] = (p.X - left + k % across * Tile, p.Y - top + k / across * Tile);
            }
            return at;
        }

        private static IEnumerable<int> TilesOf(BattleGaugePieces.Slot slot, BattleGaugeComposer.Kind kind) =>
            BattleGaugePieces.ReadSlot(slot, BattleGaugeComposer.BarOf(kind))?
                .SelectMany(s => Enumerable.Range(s.FirstTile, s.Tiles)) ?? Enumerable.Empty<int>();

        /// <summary>The squares the battle repaints in one flat colour with writing on it, for this bar.</summary>
        private static List<int> TextTiles(BattleGaugeComposer.Kind kind)
        {
            List<int> tiles = TilesOf(BattleGaugePieces.Slot.Name, kind).Concat(TilesOf(BattleGaugePieces.Slot.LvMark, kind))
                .Concat(TilesOf(BattleGaugePieces.Slot.LvDigits, kind)).ToList();
            // A double battle bar shows its numbers over the gauge, which is then this background too, so only the
            // single battle bar's number area is read as part of the design.
            if (kind == BattleGaugeComposer.Kind.PlayerSingle)
                tiles.AddRange(TilesOf(BattleGaugePieces.Slot.CurrentHp, kind).Concat(TilesOf(BattleGaugePieces.Slot.MaxHp, kind)));
            return tiles;
        }

        /// <summary>Works out the colours from a picture of the bar as it appears, without writing anything.</summary>
        public static Plan Make(BattleGaugeComposer.Kind kind, byte[] rgba, int width, int height)
        {
            Plan plan = new Plan { Kind = kind };
            System.Drawing.Color[] palette = BattleGaugeTextRenderer.Colours();
            Dictionary<int, (int X, int Y)> squares = Squares(kind);
            if (palette == null || squares.Count == 0) { plan.CannotBecause = "This bar's colours or layout couldn't be read."; return plan; }

            uint At(int x, int y) => rgba[(y * width + x) * 4 + 3] < 128 ? 0xFF000000u
                : Ds((uint)(rgba[(y * width + x) * 4] << 16 | rgba[(y * width + x) * 4 + 1] << 8 | rgba[(y * width + x) * 4 + 2]));

            Dictionary<uint, int> inText = new();
            foreach (int t in TextTiles(kind))
            {
                if (!squares.TryGetValue(t, out (int X, int Y) sq) || sq.X + Tile > width || sq.Y + Tile > height) continue;
                for (int y = 0; y < Tile; y++)
                    for (int x = 0; x < Tile; x++)
                    {
                        uint c = At(sq.X + x, sq.Y + y);
                        if (c != 0xFF000000u) inText[c] = inText.GetValueOrDefault(c) + 1;
                    }
            }
            if (inText.Count == 0)
            {
                if (!BattleGaugeTextPatch.TextIsClear) { plan.CannotBecause = "The picture has nothing where the name and level go."; return plan; }
                // With the patch the text background is never drawn, so a see-through text area keeps the current text colours.
                inText[Ds(Pack(palette[TextBack]))] = 1;
            }

            List<KeyValuePair<uint, int>> ranked = inText.OrderByDescending(kv => kv.Value).ToList();
            plan.Background = ranked[0].Key;
            int total = ranked.Sum(kv => kv.Value);
            HashSet<uint> all = new();
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    uint c = At(x, y);
                    if (c != 0xFF000000u) all.Add(c);
                }

            // All six bars share the text colours, so a background an earlier import already set keeps the letters and
            // shadow that import chose. Otherwise the letters take whichever of the picture's colours stands out most
            // against the background, and the shadow a plain shade between the two.
            List<uint> others = all.Where(c => c != plan.Background).ToList();
            uint inkNow = Ds(Pack(palette[TextInk])), shadowNow = Ds(Pack(palette[TextShadow]));
            if (Ds(Pack(palette[TextBack])) == plan.Background && others.Contains(inkNow))
            {
                plan.Ink = inkNow;
                plan.Shadow = shadowNow;
            }
            else
            {
                plan.Ink = others.Count > 0 ? others.OrderByDescending(c => Math.Abs(Light(c) - Light(plan.Background))).First() : inkNow;
                List<uint> left = others.Where(c => c != plan.Ink).ToList();
                List<uint> plain = left.Where(c => Saturation(c) < 0.5).ToList();
                double between = Light(plan.Ink) * 0.35 + Light(plan.Background) * 0.65;
                plan.Shadow = (plain.Count > 0 ? plain : left).OrderBy(c => Math.Abs(Light(c) - between)).FirstOrDefault(shadowNow);
            }
            plan.ClearTextBackground = BattleGaugeTextPatch.TextIsClear;
            if (!plan.ClearTextBackground && ranked[0].Value * 10 < total * 9)
                plan.Notes.Add("The name" + (ShowsNumbers(kind) ? ", level and HP numbers" : " and level")
                             + " sit on one flat colour, so only the main colour of those areas shows.");

            plan.Colours[TextBack] = plan.Background;
            plan.Colours[TextInk] = plan.Ink;
            plan.Colours[TextShadow] = plan.Shadow;

            // The rest go where the bars' own colours are, then wherever the fewest things draw.
            uint[] now = palette.Take(16).Select(c => Ds(Pack(c))).ToArray();
            List<int> barSlots = BarSlots().Where(s => s != TextBack && s != TextInk && s != TextShadow).ToList();
            List<int> spare = barSlots.Concat(Enumerable.Range(1, 15).Reverse().Where(s => !barSlots.Contains(s) && s != TextBack && s != TextInk && s != TextShadow)).ToList();
            foreach (uint c in all.Where(c => !plan.Colours.ContainsValue(c)))
            {
                int keep = Array.FindIndex(now, 1, x => x == c);
                // Already in the palette at a number nothing else here changes: keep it there, and keep that number.
                if (keep >= 1 && !plan.Colours.ContainsKey(keep)) { plan.Colours[keep] = c; spare.Remove(keep); continue; }
                if (spare.Count == 0) { plan.CannotBecause = "The picture has more colours than the HP bar palette can hold."; return plan; }
                plan.Colours[spare[0]] = c;
                spare.RemoveAt(0);
            }
            if (plan.Colours.Keys.Any(s => !barSlots.Contains(s) && s != TextBack && s != TextInk && s != TextShadow && Ds(Pack(palette[s])) != plan.Colours[s]))
                plan.Notes.Add("Some colours share numbers with the status words or the health fill, so those change colour too.");

            if (Theirs(kind))
                plan.Notes.Add("In a wild battle the caught box shows the first status tile until that Pokémon is caught.");
            else if (YourDouble(kind))
                plan.Notes.Add(plan.ClearTextBackground ? "In doubles, Start shows the HP numbers where the gauge was."
                                                        : "In doubles, Start shows the HP numbers on the text background where the gauge was.");
            plan.Notes.Add("The other bars share these colours and pieces, so import them too for a matching set.");
            return plan;
        }

        private static List<int> BarSlots()
        {
            bool[] used = new bool[16];
            ScriptNarc narc = new ScriptNarc(RomInfo.DirNames.BattleObj);
            foreach (BattleGaugeComposer.Kind kind in Enum.GetValues<BattleGaugeComposer.Kind>())
            {
                int at = BattleObjects.Find(BattleGaugeComposer.GraphicOf(kind), "Drawing");
                byte[] ncgr = at >= 0 ? GraphicAssets.Unsqueeze(narc.Get(at)) : null;
                if (ncgr == null) continue;
                int start = NitroBgCodec.ReadTileHeader(ncgr).TilesAt;
                for (int i = Math.Max(0, start); i < ncgr.Length; i++) { used[ncgr[i] & 0xF] = true; used[ncgr[i] >> 4] = true; }
            }
            return Enumerable.Range(1, 15).Where(s => used[s]).ToList();
        }

        public static string WriteColours(GraphicAssets.Archive archive, Plan plan)
        {
            foreach (KeyValuePair<int, uint> kv in plan.Colours)
            {
                string trouble = GraphicAssets.WritePaletteColour(archive, ColoursEntry, kv.Key, (byte)(kv.Value >> 16), (byte)(kv.Value >> 8), (byte)kv.Value);
                if (trouble != null) return trouble;
            }
            return null;
        }

        /// <summary>
        /// The bar pieces rebuilt from the bar's drawing as it now is: wherever a piece only shows the game's plain
        /// background, it takes the bar's own square from that place. Null when the drawing or pieces cannot be read.
        /// </summary>
        public static byte[] FitPieces(BattleGaugeComposer.Kind kind, byte[] sheet, List<string> notes)
        {
            BattleGaugePieces.Found found = BattleGaugePieces.Locate();
            if (sheet == null || found == null || !found.HasSlots) return null;
            int drawing = BattleObjects.Find(BattleGaugeComposer.GraphicOf(kind), "Drawing");
            byte[] ncgr = drawing >= 0 ? GraphicAssets.Unsqueeze(new ScriptNarc(RomInfo.DirNames.BattleObj).Get(drawing)) : null;
            if (ncgr == null) return null;
            int tilesAt = NitroBgCodec.ReadTileHeader(ncgr).TilesAt;

            byte[] Bar(int tile)
            {
                int at = tilesAt + tile * TileBytes;
                return at >= 0 && at + TileBytes <= ncgr.Length ? ncgr.AsSpan(at, TileBytes).ToArray() : null;
            }
            byte[] Old(int piece) => sheet.AsSpan(piece * TileBytes, TileBytes).ToArray();
            byte[] fitted = (byte[])sheet.Clone();

            void Copy(int piece, byte[] square)
            {
                if (square != null) Array.Copy(square, 0, fitted, piece * TileBytes, TileBytes);
            }

            // Every pixel of a piece that matches its plain counterpart is background, and takes the bar's square.
            void Lay(int piece, byte[] plain, byte[] square)
            {
                if (square == null) return;
                byte[] old = Old(piece);
                for (int p = 0; p < 64; p++)
                {
                    int was = Pixel(old, p);
                    Put(fitted, piece * TileBytes, p, was == Pixel(plain, p) ? Pixel(square, p) : was);
                }
            }

            // The gauge is one piece repeated along it, so it takes the look of its middle square.
            List<int> gauge = TilesOf(BattleGaugePieces.Slot.HpGauge, kind).ToList();
            if (gauge.Count > 0)
            {
                byte[] square = Bar(gauge[gauge.Count / 2]);
                if (gauge.Select(Bar).Any(t => t == null || !t.AsSpan().SequenceEqual(square)))
                    notes.Add("The health gauge is one piece repeated along its length, so it takes the look of its middle tile.");
                foreach (int colour in new[] { BattleGaugePieces.HpGreen, BattleGaugePieces.HpYellow, BattleGaugePieces.HpRed })
                {
                    byte[] plain = Old(colour);
                    for (int k = 0; k <= 8; k++) Lay(colour + k, plain, square);
                }
            }

            if (kind == BattleGaugeComposer.Kind.PlayerSingle)
            {
                BattleGaugePieces.Span second = BattleGaugePieces.ExpGauge[1];
                byte[] square = Bar(second.FirstTile + second.Tiles / 2);
                byte[] plain = Old(BattleGaugePieces.ExpFill);
                for (int k = 0; k <= 8; k++) Lay(BattleGaugePieces.ExpFill + k, plain, square);
            }

            List<int> status = TilesOf(BattleGaugePieces.Slot.Status, kind).ToList();
            if (status.Count == 3)
            {
                byte[][] plain = Enumerable.Range(0, 3).Select(i => Old(BattleGaugePieces.NoStatus + i)).ToArray();
                // Seven three-square words from 0x26 in all three games: the five conditions, the plain one and a spare.
                for (int word = 0; word < 7; word++)
                    for (int i = 0; i < 3; i++)
                        Lay(0x26 + word * 3 + i, plain[i], Bar(status[i]));
            }

            List<int> mark = TilesOf(BattleGaugePieces.Slot.LvMark, kind).ToList();
            if (mark.Count == 4)
            {
                // The Lv blocks are drawn on the text background, so their plain parts are that colour.
                byte[] plain = Enumerable.Repeat((byte)(TextBack | TextBack << 4), TileBytes).ToArray();
                foreach (int[] block in BattleGaugePieces.GenderPieces)
                {
                    if (block.Length == 4)
                        for (int k = 0; k < 4; k++) Lay(block[k], plain, Bar(mark[k]));
                    else
                        // One row of two, drawn four rows lower: its top half lands in the lower half of the upper
                        // squares and its bottom half in the upper half of the lower ones.
                        for (int k = 0; k < 2; k++) Lay(block[k], plain, Lowered(Bar(mark[k]), Bar(mark[k + 2])));
                }
            }

            if (YourDouble(kind))
            {
                // Start puts the numbers' ends where the HP tag and the bar end are, and going back copies the tag,
                // the bar end and the first status square in again, so those take this bar's own squares.
                List<int> left = TilesOf(BattleGaugePieces.Slot.HpLeft, kind).ToList();
                List<int> right = TilesOf(BattleGaugePieces.Slot.HpRight, kind).ToList();
                if (left.Count == 2 && right.Count == 1)
                {
                    byte[] tag0 = Bar(left[0]), tag1 = Bar(left[1]), end = Bar(right[0]), after = Bar(right[0] + 1);
                    if (BattleGaugePieces.HasNumberEnds)
                    {
                        Lay(BattleGaugePieces.NumbersLeft, Old(BattleGaugePieces.DoublesHp + 1), tag1);
                        Lay(BattleGaugePieces.NumbersRight, Old(BattleGaugePieces.NoStatus), after);
                    }
                    Copy(BattleGaugePieces.DoublesHp, tag0);
                    Copy(BattleGaugePieces.DoublesHp + 1, tag1);
                    Copy(BattleGaugePieces.BarEnd, end);
                    List<int> statusTiles = TilesOf(BattleGaugePieces.Slot.Status, kind).ToList();
                    byte[] firstStatus = statusTiles.Count > 0 ? Bar(statusTiles[0]) : null;
                    if (BattleGaugePieces.HasNumberEnds && after != null && firstStatus != null && !after.AsSpan().SequenceEqual(firstStatus))
                        notes.Add("After Start is pressed twice, the tile right of the bar end shows the first status tile.");
                    if (!BattleGaugePieces.HasNumberEnds)
                        notes.Add("With the numbers shown, the HP tag is covered by the first two status tiles.");
                }
            }

            if (Theirs(kind))
            {
                List<int> caught = TilesOf(BattleGaugePieces.Slot.Caught, kind).ToList();
                if (caught.Count == 1) Lay(BattleGaugePieces.CaughtBall, Old(BattleGaugePieces.NoStatus), Bar(caught[0]));
            }
            return fitted;
        }

        /// <summary>The 8 by 8 square a lowered row covers: the bottom half of one square over the top half of the next.</summary>
        private static byte[] Lowered(byte[] upper, byte[] lower)
        {
            if (upper == null || lower == null) return null;
            byte[] square = new byte[TileBytes];
            Array.Copy(upper, 16, square, 0, 16);
            Array.Copy(lower, 0, square, 16, 16);
            return square;
        }

        private static int Pixel(byte[] tile, int p) => p % 2 == 0 ? tile[p / 2] & 0xF : tile[p / 2] >> 4;

        private static void Put(byte[] into, int at, int p, int value)
        {
            int b = at + p / 2;
            into[b] = p % 2 == 0 ? (byte)((into[b] & 0xF0) | value) : (byte)((into[b] & 0x0F) | value << 4);
        }

        private static uint Pack(System.Drawing.Color c) => (uint)(c.R << 16 | c.G << 8 | c.B);
        private static uint Ds(uint rgb) => rgb & 0xF8F8F8;
        private static double Saturation(uint c)
        {
            int r = (int)(c >> 16 & 0xFF), g = (int)(c >> 8 & 0xFF), b = (int)(c & 0xFF);
            int most = Math.Max(r, Math.Max(g, b)), least = Math.Min(r, Math.Min(g, b));
            return most == 0 ? 0 : (most - least) / (double)most;
        }

        private static double Light(uint c) => 0.299 * (c >> 16 & 0xFF) + 0.587 * (c >> 8 & 0xFF) + 0.114 * (c & 0xFF);
    }
}
