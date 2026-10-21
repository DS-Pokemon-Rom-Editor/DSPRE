using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Where the battle writes each part of an HP bar, as boxes on the bar's 16 by 8 grid of squares. Every bar in
    /// the three games is two 64 by 64 pieces side by side, tiles 0 to 63 on the left and 64 to 127 on the right,
    /// row by row, so a square always has the same tile number and moving a part only changes the slot tables
    /// (and, for experience, a few numbers in the code). The cell layouts are never touched.
    /// </summary>
    public sealed class BattleGaugeLayout
    {
        public const int Columns = 16, Rows = 8, Half = 8;

        public enum Part { Name, LvMark, LvDigits, CurrentHp, MaxHp, Slash, HpGauge, Status, Caught, HpTag, BarEnd, Exp }

        public BattleGaugePieces.Bar Bar;
        public Dictionary<Part, (int X, int Y)> At = new();
        public int ScreenX, ScreenY;
        public int? ExtraPiece;                       // Pt and HGSS never create this piece, so it is only kept

        public static int TileAt(int x, int y) => (x >= Half ? 64 : 0) + y * Half + x % Half;

        public static (int X, int Y) SquareOf(int tile) => (tile >= 64 ? Half + tile % Half : tile % Half, tile % 64 / Half);

        public static Part[] PartsOf(BattleGaugePieces.Bar bar) => bar switch
        {
            BattleGaugePieces.Bar.YoursSingle => new[] { Part.Name, Part.LvMark, Part.LvDigits, Part.CurrentHp, Part.MaxHp, Part.HpGauge, Part.Status, Part.Exp },
            BattleGaugePieces.Bar.YoursDouble or BattleGaugePieces.Bar.YourPartner =>
                new[] { Part.Name, Part.LvMark, Part.LvDigits, Part.HpGauge, Part.Status, Part.CurrentHp, Part.Slash, Part.MaxHp, Part.HpTag, Part.BarEnd },
            _ => new[] { Part.Name, Part.LvMark, Part.LvDigits, Part.HpGauge, Part.Status, Part.Caught },
        };

        public static (int W, int H) SizeOf(Part part) => part switch
        {
            Part.Name => (8, 2),
            Part.LvMark => (2, 2),
            Part.LvDigits => (3, 2),
            Part.CurrentHp or Part.MaxHp or Part.Status => (3, 1),
            Part.HpGauge => (BattleGaugePieces.HpCells, 1),
            Part.HpTag => (2, 1),
            // Platinum and HeartGold also write the numbers' right end in the square after the bar end.
            Part.BarEnd => (BattleGaugePieces.HasNumberEnds ? 2 : 1, 1),
            Part.Exp => (BattleGaugePieces.ExpCells, 1),
            _ => (1, 1),
        };

        public static string NameOf(Part part) => part switch
        {
            Part.Name => "Name", Part.LvMark => "Gender and Lv", Part.LvDigits => "Level", Part.CurrentHp => "HP",
            Part.MaxHp => "Max HP", Part.Slash => "Slash", Part.HpGauge => "Health", Part.Status => "Status",
            Part.Caught => "Caught ball", Part.HpTag => "HP tag", Part.BarEnd => "Bar end", Part.Exp => "Experience",
            _ => part.ToString(),
        };

        public static BattleGaugeLayout Read(BattleGaugePieces.Bar bar)
        {
            if (BattleGaugePieces.Locate()?.HasSlots != true) return null;
            BattleGaugeLayout layout = new BattleGaugeLayout { Bar = bar, ExtraPiece = BattleGaugePieces.ReadExtraPiece(bar) };
            if (BattleGaugePieces.ReadPosition(bar) is { } pos) (layout.ScreenX, layout.ScreenY) = pos;
            foreach (Part part in PartsOf(bar))
            {
                int first = FirstTile(part, bar);
                if (first >= 0) layout.At[part] = SquareOf(first);
            }
            return layout;
        }

        private static int FirstTile(Part part, BattleGaugePieces.Bar bar)
        {
            if (part == Part.Exp) return BattleGaugePieces.ReadExp() is { } e ? e.FirstTile : -1;
            BattleGaugePieces.Slot? slot = SlotOf(part);
            BattleGaugePieces.Span[] spans = slot == null ? null : BattleGaugePieces.ReadSlot(slot.Value, bar);
            BattleGaugePieces.Span first = spans?.FirstOrDefault(s => s.Size > 0) ?? default;
            return first.Size > 0 ? first.FirstTile : -1;
        }

        private static BattleGaugePieces.Slot? SlotOf(Part part) => part switch
        {
            Part.Name => BattleGaugePieces.Slot.Name, Part.LvMark => BattleGaugePieces.Slot.LvMark,
            Part.LvDigits => BattleGaugePieces.Slot.LvDigits, Part.CurrentHp => BattleGaugePieces.Slot.CurrentHp,
            Part.MaxHp => BattleGaugePieces.Slot.MaxHp, Part.Slash => BattleGaugePieces.Slot.Slash,
            Part.HpGauge => BattleGaugePieces.Slot.HpGauge, Part.Status => BattleGaugePieces.Slot.Status,
            Part.Caught => BattleGaugePieces.Slot.Caught, Part.HpTag => BattleGaugePieces.Slot.HpLeft,
            Part.BarEnd => BattleGaugePieces.Slot.HpRight,
            _ => null,
        };

        /// <summary>A run of squares that has to be one unbroken stretch of tiles, so it cannot cross the middle.</summary>
        private static bool OneRun(Part part) => part is Part.LvMark or Part.LvDigits or Part.MaxHp or Part.Status or Part.HpTag or Part.BarEnd;

        /// <summary>What is wrong with where a part sits, in the user's words, or null.</summary>
        public string Problem(Part part)
        {
            if (!At.TryGetValue(part, out (int X, int Y) at)) return null;
            (int w, int h) = SizeOf(part);
            if (at.X < 0 || at.Y < 0 || at.X + w > Columns || at.Y + h > Rows) return NameOf(part) + " is outside the bar.";
            bool crosses = at.X < Half && at.X + w > Half;
            if (crosses && OneRun(part)) return NameOf(part) + " cannot cross the middle of the bar.";
            if (part == Part.Exp)
            {
                int first = Half - at.X;
                if (!crosses || first > 7 || w - first > Half)
                    return $"Experience must cross the middle of the bar, starting at column {Half - 7} to {Half - 1}.";
            }
            foreach ((Part other, (int X, int Y) o) in At)
            {
                if (other == part || !Overlaps(at, (w, h), o, SizeOf(other))) continue;
                if (MayOverlap(part, other)) continue;
                return $"{NameOf(part)} overlaps {NameOf(other)}.";
            }
            return null;
        }

        /// <summary>A double battle bar swaps its gauge for the numbers, so those may share squares.</summary>
        private static bool MayOverlap(Part a, Part b)
        {
            bool Numbers(Part p) => p is Part.CurrentHp or Part.Slash or Part.MaxHp;
            bool Bar(Part p) => p is Part.HpGauge or Part.HpTag or Part.BarEnd;
            return (Numbers(a) && Bar(b)) || (Numbers(b) && Bar(a));
        }

        private static bool Overlaps((int X, int Y) a, (int W, int H) sa, (int X, int Y) b, (int W, int H) sb) =>
            a.X < b.X + sb.W && b.X < a.X + sa.W && a.Y < b.Y + sb.H && b.Y < a.Y + sa.H;

        public IEnumerable<string> Problems() => At.Keys.Select(Problem).Where(p => p != null);

        /// <summary>
        /// Parts drawn over another part, over empty squares of the bar, or over bar art (HP letters, the slash) no part
        /// covers in <paramref name="saved"/>. Only a double battle's bars swap the numbers for the gauge.
        /// </summary>
        public HashSet<Part> Clashes(Func<int, bool> empty, Func<int, bool> plain = null, BattleGaugeLayout saved = null)
        {
            HashSet<int> art = new();
            if (saved != null && empty != null && plain != null)
            {
                HashSet<int> under = new();
                foreach ((Part part, (int X, int Y) at) in saved.At)
                {
                    (int w, int h) = SizeOf(part);
                    for (int y = at.Y; y < at.Y + h; y++)
                        for (int x = at.X; x < at.X + w; x++) under.Add(TileAt(x, y));
                }
                for (int tile = 0; tile < Columns * Rows; tile++)
                    if (!empty(tile) && !plain(tile) && !under.Contains(tile)) art.Add(tile);
            }
            HashSet<Part> clashes = new();
            bool swaps = Bar is BattleGaugePieces.Bar.YoursDouble or BattleGaugePieces.Bar.YourPartner;
            foreach ((Part part, (int X, int Y) at) in At)
            {
                (int w, int h) = SizeOf(part);
                foreach ((Part other, (int X, int Y) o) in At)
                    if (other != part && !swaps && MayOverlap(part, other) && Overlaps(at, (w, h), o, SizeOf(other)))
                        clashes.Add(part);
                for (int y = at.Y; y < at.Y + h && empty != null; y++)
                    for (int x = at.X; x < at.X + w; x++)
                        if (x >= 0 && y >= 0 && x < Columns && y < Rows && (empty(TileAt(x, y)) || art.Contains(TileAt(x, y)))) clashes.Add(part);
            }
            return clashes;
        }

        /// <summary>A row of squares as table spans: the part left of the middle, then the part right of it.</summary>
        private static List<BattleGaugePieces.Span> Runs(int x, int y, int w)
        {
            List<BattleGaugePieces.Span> runs = new();
            int left = Math.Clamp(Half - x, 0, w);
            if (left > 0) runs.Add(new(TileAt(x, y) * BattleGaugePieces.TileBytes, left * BattleGaugePieces.TileBytes));
            if (w - left > 0) runs.Add(new(TileAt(x + left, y) * BattleGaugePieces.TileBytes, (w - left) * BattleGaugePieces.TileBytes));
            return runs;
        }

        public string Write(byte[] overlay)
        {
            string problem = Problems().FirstOrDefault();
            if (problem != null) return problem;
            BattleGaugePieces.Span none = new(0, 0);
            foreach ((Part part, (int x, int y)) in At)
            {
                (int w, int h) = SizeOf(part);
                List<BattleGaugePieces.Span> top = Runs(x, y, w);
                List<BattleGaugePieces.Span> spans = part switch
                {
                    // Name: top first part, bottom first part, top second part, bottom second part.
                    Part.Name => new() { top[0], Runs(x, y + 1, w)[0], top.Count > 1 ? top[1] : none, top.Count > 1 ? Runs(x, y + 1, w)[1] : none },
                    Part.LvMark or Part.LvDigits => new() { top[0], Runs(x, y + 1, w)[0] },
                    Part.CurrentHp or Part.HpGauge => new() { top[0], top.Count > 1 ? top[1] : none },
                    Part.BarEnd => new() { new(top[0].Pos, BattleGaugePieces.TileBytes) },
                    _ => new() { top[0] },
                };
                if (part == Part.Exp)
                {
                    string trouble = BattleGaugePieces.PutExp(overlay, top[0].FirstTile, top[0].Tiles, top[1].FirstTile);
                    if (trouble != null) return trouble;
                    continue;
                }
                BattleGaugePieces.Slot? slot = SlotOf(part);
                if (slot != null) BattleGaugePieces.PutSlot(overlay, slot.Value, Bar, spans.ToArray());
            }
            BattleGaugePieces.PutPosition(overlay, Bar, ScreenX, ScreenY);
            if (ExtraPiece is int extra) BattleGaugePieces.PutExtraPiece(overlay, Bar, extra);
            return null;
        }
    }
}
