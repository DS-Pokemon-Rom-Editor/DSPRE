using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Draws a gauge the way a battle builds one: the name, level, HP numbers and status word go into
    /// the gauge's own tiles, then its cell layout puts them on screen. So the writing cannot land
    /// outside the bar, and a double battle is the same code with a different row of the tables.
    /// </summary>
    public static class BattleGaugeComposer
    {
        /// <summary>The six gauges a battle can show: two in a single battle, four in a double.</summary>
        public enum Kind { PlayerSingle, OpponentSingle, PlayerNear, OpponentFar, PlayerFar, OpponentNear }

        private sealed class Layout
        {
            public string Graphic;             // which gauge picture, by the name the game gives it
            public int X, Y;                   // where its middle sits on screen
            public (int pos, int size)[] Name; // four: first piece upper and lower, then second piece
            public (int pos, int size)[] LvMark;   // the gender and "Lv" block, upper then lower
            public (int pos, int size)[] LvDigits; // the level number, upper then lower
            public (int pos, int size)[] Hp;       // the current HP number; one entry is unused
            public (int pos, int size) HpMax;      // the maximum HP number
            public int Status;                     // the word for what is wrong, three tiles
            public int Slash;                      // the "/" between the two numbers, when it is not
                                                   // already part of the picture (double battles only)
            public (int pos, int size)[] Gauge;    // the health fill, first piece then second
            public int Caught;                     // the caught ball, on their side
            public int HpLeft, HpRight;            // the bar's ends the doubles swap for the numbers' ends
        }

        // Tile numbers, not bytes: multiplied up when used. Straight from the gauge's own tables.
        private static readonly Dictionary<Kind, Layout> Layouts = new()
        {
            [Kind.PlayerSingle] = new Layout           // the games call this AA
            {
                Graphic = "HpBar.Yours", X = 192, Y = 116,
                Name = new[] { (0x13, 5), (0x1b, 5), (0x50, 3), (0x58, 3) },
                LvMark = new[] { (0x53, 2), (0x5B, 2) },
                LvDigits = new[] { (0x55, 3), (0x5d, 3) },
                Hp = new[] { (0, 0), (0x68, 3) },
                HpMax = (0x6c, 3),
                Status = 0x24,
            },
            [Kind.OpponentSingle] = new Layout          // BB
            {
                Graphic = "HpBar.Theirs", X = 58, Y = 36,
                Name = new[] { (0x11, 7), (0x19, 7), (0x50, 1), (0x58, 1) },
                LvMark = new[] { (0x51, 2), (0x59, 2) },
                LvDigits = new[] { (0x53, 3), (0x5b, 3) },
                Hp = new[] { (0x31, 3), (0, 0) },
                HpMax = (0x35, 3),
                Status = 0x22,
            },
            [Kind.PlayerNear] = new Layout              // A
            {
                Graphic = "HpBar.YoursDouble", X = 192, Y = 103,
                Name = new[] { (0x12, 6), (0x1a, 6), (0x50, 2), (0x58, 2) },
                LvMark = new[] { (0x52, 2), (0x5a, 2) },
                LvDigits = new[] { (0x54, 3), (0x5c, 3) },
                Hp = new[] { (0, 0), (0x60, 3) },
                HpMax = (0x64, 3),
                Status = 0x23,
                Slash = 0x63,
            },
            [Kind.OpponentFar] = new Layout             // B
            {
                Graphic = "HpBar.TheirsDouble", X = 64, Y = 16,
                Name = new[] { (0x11, 7), (0x19, 7), (0x50, 1), (0x58, 1) },
                LvMark = new[] { (0x51, 2), (0x59, 2) },
                LvDigits = new[] { (0x53, 3), (0x5b, 3) },
                Hp = new[] { (0x31, 3), (0, 0) },
                HpMax = (0x35, 3),
                Status = 0x22,
            },
            [Kind.PlayerFar] = new Layout               // C
            {
                Graphic = "HpBar.YourPartner", X = 198, Y = 132,
                Name = new[] { (0x12, 6), (0x1a, 6), (0x50, 2), (0x58, 2) },
                LvMark = new[] { (0x52, 2), (0x5a, 2) },
                LvDigits = new[] { (0x54, 3), (0x5c, 3) },
                Hp = new[] { (0, 0), (0x60, 3) },
                HpMax = (0x64, 3),
                Status = 0x23,
                Slash = 0x63,
            },
            [Kind.OpponentNear] = new Layout            // D
            {
                Graphic = "HpBar.TheirPartner", X = 58, Y = 45,
                Name = new[] { (0x11, 7), (0x19, 7), (0x50, 1), (0x58, 1) },
                LvMark = new[] { (0x51, 2), (0x59, 2) },
                LvDigits = new[] { (0x53, 3), (0x5b, 3) },
                Hp = new[] { (0x31, 3), (0, 0) },
                HpMax = (0x35, 3),
                Status = 0x22,
            },
        };

        // Diamond and Pearl's name field is a tile narrower; their other gauges match Platinum.
        private static readonly Layout DiamondPearlPlayerSingle = new()
        {
            Graphic = "HpBar.Yours", X = 198, Y = 116,
            Name = new[] { (0x12, 6), (0x1a, 6), (0x50, 2), (0x58, 2) },
            LvMark = new[] { (0x52, 2), (0x5a, 2) },
            LvDigits = new[] { (0x54, 3), (0x5c, 3) },
            Hp = new[] { (0, 0), (0x68, 3) },
            HpMax = (0x6c, 3),
            Status = 0x23,
        };

        internal static BattleGaugePieces.Bar BarOf(Kind kind) => kind switch
        {
            Kind.PlayerSingle => BattleGaugePieces.Bar.YoursSingle,
            Kind.OpponentSingle => BattleGaugePieces.Bar.TheirsSingle,
            Kind.PlayerNear => BattleGaugePieces.Bar.YoursDouble,
            Kind.OpponentFar => BattleGaugePieces.Bar.TheirsDouble,
            Kind.PlayerFar => BattleGaugePieces.Bar.YourPartner,
            _ => BattleGaugePieces.Bar.TheirPartner,
        };

        /// <summary>The slots as this ROM's tables have them, or null when they aren't found.</summary>
        private static Layout FromRom(Kind kind, Layout known)
        {
            BattleGaugePieces.Bar bar = BarOf(kind);
            (int pos, int size)[] Tiles(BattleGaugePieces.Slot slot) =>
                BattleGaugePieces.ReadSlot(slot, bar)?.Select(s => (s.FirstTile, s.Tiles)).ToArray();

            (int pos, int size)[] name = Tiles(BattleGaugePieces.Slot.Name);
            if (name == null) return null;
            return new Layout
            {
                Graphic = known.Graphic,
                X = BattleGaugePieces.ReadPosition(bar)?.X ?? known.X,
                Y = BattleGaugePieces.ReadPosition(bar)?.Y ?? known.Y,
                Name = name,
                LvMark = Tiles(BattleGaugePieces.Slot.LvMark),
                LvDigits = Tiles(BattleGaugePieces.Slot.LvDigits),
                Hp = Tiles(BattleGaugePieces.Slot.CurrentHp),
                HpMax = Tiles(BattleGaugePieces.Slot.MaxHp)[0],
                Status = Tiles(BattleGaugePieces.Slot.Status)[0].pos,
                Slash = Tiles(BattleGaugePieces.Slot.Slash)[0].pos,
                Gauge = Tiles(BattleGaugePieces.Slot.HpGauge),
                Caught = Tiles(BattleGaugePieces.Slot.Caught)[0].pos,
                HpLeft = Tiles(BattleGaugePieces.Slot.HpLeft)[0].pos,
                HpRight = Tiles(BattleGaugePieces.Slot.HpRight)[0].pos,
            };
        }

        private static bool TryLayout(Kind kind, out Layout layout)
        {
            // The slots are read from the game; these tables are the fallback when its slot tables aren't found.
            Layout known = kind == Kind.PlayerSingle && RomInfo.gameFamily == RomInfo.GameFamilies.DP ? DiamondPearlPlayerSingle
                         : Layouts.GetValueOrDefault(kind);
            if (known != null && FromRom(kind, known) is Layout read)
            {
                layout = read;
                return true;
            }
            if (kind == Kind.PlayerSingle && RomInfo.gameFamily == RomInfo.GameFamilies.DP)
            {
                layout = DiamondPearlPlayerSingle;
                return true;
            }
            return Layouts.TryGetValue(kind, out layout);
        }

        /// <summary>Where a gauge's centre sits on screen in this game.</summary>
        public static (int X, int Y) CentreOf(Kind kind) =>
            TryLayout(kind, out Layout l) ? (l.X, l.Y) : (0, 0);

        /// <summary>Where the green health fill starts on screen: Diamond and Pearl's bar begins a tile earlier.</summary>
        public static int HealthFillLeft(Kind kind) =>
            CentreOf(kind).X + (kind == Kind.PlayerSingle && RomInfo.gameFamily == RomInfo.GameFamilies.DP ? 0 : 8);

        /// <summary>What to write on a gauge.</summary>
        public sealed class Showing
        {
            public string Name = "";
            public int Level = 5;
            public BattleGaugeText.Gender Gender = BattleGaugeText.Gender.Genderless;
            public BattleGaugeText.Status Status = BattleGaugeText.Status.None;
            public int Health = 20, MostHealth = 20;
            public bool ShowHealthNumbers;      // only your own side shows them
            public bool FillGauges;             // copy in the health and experience fill as a battle does
            public double Experience = 0.5;     // how far to the next level, for your single battle bar
            public bool Caught;                 // their Pokemon is one you have caught
        }

        public sealed class Drawn
        {
            public byte[] Rgba;
            public int Width, Height, Left, Top;
        }

        /// <summary>Which gauges a battle of this shape puts on the screen.</summary>
        public static IReadOnlyList<Kind> ForDoubleBattle(bool doubles) => doubles
            ? new[] { Kind.OpponentFar, Kind.OpponentNear, Kind.PlayerNear, Kind.PlayerFar }
            : new[] { Kind.OpponentSingle, Kind.PlayerSingle };

        /// <summary>Which gauge picture a slot uses, by the name the game gives it.</summary>
        public static string GraphicOf(Kind kind) =>
            TryLayout(kind, out Layout l) ? l.Graphic : null;

        public static string NameOf(Kind kind) => kind switch
        {
            Kind.PlayerSingle => "Your HP bar",
            Kind.OpponentSingle => "Their HP bar",
            Kind.PlayerNear => "Your front HP bar",
            Kind.PlayerFar => "Your partner's HP bar",
            Kind.OpponentFar => "Their back HP bar",
            Kind.OpponentNear => "Their partner's HP bar",
            _ => "HP bar",
        };

        private const int Canvas = 256;
        private static int CharDataAt(byte[] ncgr) => NitroBgCodec.ReadTileHeader(ncgr).TilesAt;
        private const int TileBytes = 32;       // 8 by 8 at four bits a pixel

        /// <summary>
        /// Builds one gauge with its text already in it, or null when this ROM is not one we read.
        /// </summary>
        public static Drawn Build(Kind kind, Showing showing)
        {
            if (!BattleGaugeText.IsAvailable || !TryLayout(kind, out Layout layout)) return null;

            ScriptNarc narc = new ScriptNarc(RomInfo.DirNames.battleObj);
            if (!narc.Available) return null;

            int drawing = BattleObjects.Find(layout.Graphic, "Drawing");
            int cells = BattleObjects.Find(layout.Graphic, "As it appears");
            int colours = BattleObjects.Find("HpBar.Shared", "Colours");
            if (drawing < 0 || cells < 0 || colours < 0) return null;

            byte[] tiles;
            try { tiles = GraphicAssets.Unsqueeze(narc.Get(drawing)); }
            catch { return null; }
            if (tiles == null || tiles.Length < CharDataAt(tiles) + TileBytes) return null;

            WriteTheText(tiles, layout, showing);
            if (showing.FillGauges) WriteThePieces(tiles, kind, layout, showing);

            try
            {
                RawImage raw = CellRenderer.Draw(NcerFile.Read(NitroBgCodec.Inflate(narc.Get(cells))), 0, NitroBgCodec.Inflate(tiles),
                    NitroBgCodec.ReadColours(NitroBgCodec.Inflate(narc.Get(colours))), Canvas, Canvas);
                if (raw == null || raw.IsEmpty) return null;

                return new Drawn
                {
                    Rgba = ToRgba(raw, Canvas),
                    Width = Canvas,
                    Height = Canvas,
                    Left = layout.X - Canvas / 2,
                    Top = layout.Y - Canvas / 2,
                };
            }
            catch (Exception ex)
            {
                AppLogger.Error("A battle gauge could not be put together: " + ex.Message);
                return null;
            }
        }

        // ── writing into the gauge's tiles ────────────────────────────────────────────────────────

        private static void WriteTheText(byte[] tiles, Layout layout, Showing showing)
        {
            // With the clear text background patch the battle keeps the bar's own pixels wherever the text's background would go.
            bool clearBackground = BattleGaugeTextPatch.IsApplied;

            // The name is eight tiles across and two down, and goes in as four pieces: the top row and
            // the bottom row, each cut where the first sprite piece runs out and the second begins.
            byte[] name = BattleGaugeGlyphs.NameBlock(showing.Name);
            if (name != null)
            {
                (int pos, int size)[] n = layout.Name;
                Copy(name, 0, tiles, n[0], showing: n[0].size, clearBackground);
                Copy(name, 8 * TileBytes, tiles, n[1], showing: n[1].size, clearBackground);
                Copy(name, n[0].size * TileBytes, tiles, n[2], showing: n[2].size, clearBackground);
                Copy(name, (8 + n[1].size) * TileBytes, tiles, n[3], showing: n[3].size, clearBackground);
            }

            // The gender symbol and "Lv" are one block the game keeps ready made, two tiles across and
            // two down, so they go straight in.
            byte[] mark = BattleGaugeGlyphs.GenderAndLvBlock(showing.Gender);
            if (mark != null)
            {
                Copy(mark, 0, tiles, layout.LvMark[0], showing: layout.LvMark[0].size);
                Copy(mark, 2 * TileBytes, tiles, layout.LvMark[1], showing: layout.LvMark[1].size);
            }

            // The level sits four rows lower than the tiles it lands in, so each digit straddles two of
            // them and what is above and below it has to be kept.
            byte[] level = BattleGaugeGlyphs.NumberRow(showing.Level, layout.LvDigits[0].size);
            if (level != null) Interleave(level, tiles, layout.LvDigits[0], layout.LvDigits[1], clearBackground);

            if (showing.ShowHealthNumbers)
            {
                // A double battle's bars normally show the bar and no numbers; the games swap the two
                // over on a press, and only then do they put the "/" in. A single battle's bars have
                // it in the picture already, which is why only these two write one.
                if (layout.Slash > 0)
                {
                    // The games copy this tile as it is, so its colours are the sheet's own.
                    byte[] sheet = BattleGaugePieces.ReadSheet();
                    if (sheet != null) Copy(sheet, BattleGaugePieces.Slash * TileBytes, tiles, (layout.Slash, 1), showing: 1);
                }

                (int pos, int size) hp = layout.Hp[0].size > 0 ? layout.Hp[0] : layout.Hp[1];
                if (hp.size > 0)
                {
                    // The games pad this one on the left so it sits against the "/".
                    byte[] now = BattleGaugeGlyphs.NumberRow(showing.Health, hp.size, againstTheRight: true);
                    if (now != null) Copy(now, 0, tiles, hp, showing: hp.size, clearBackground);
                }
                if (layout.HpMax.size > 0)
                {
                    byte[] most = BattleGaugeGlyphs.NumberRow(showing.MostHealth, layout.HpMax.size);
                    if (most != null) Copy(most, 0, tiles, layout.HpMax, showing: layout.HpMax.size, clearBackground);
                }
            }

            byte[] status = BattleGaugeGlyphs.StatusRow(showing.Status);
            if (status != null)
                Copy(status, 0, tiles, (layout.Status, BattleGaugeText.StatusTiles),
                     showing: BattleGaugeText.StatusTiles);
        }

        private static void WriteThePieces(byte[] tiles, Kind kind, Layout layout, Showing showing)
        {
            byte[] sheet = BattleGaugePieces.ReadSheet();
            if (sheet == null || layout.Gauge == null) return;
            bool theirs = kind == Kind.OpponentSingle || kind == Kind.OpponentFar || kind == Kind.OpponentNear;
            bool yourDouble = kind == Kind.PlayerNear || kind == Kind.PlayerFar;

            if (yourDouble && showing.ShowHealthNumbers)
            {
                // Swapping to the numbers puts the numbers' own ends where the bar's ends were; Diamond and Pearl,
                // which have none, blank the HP tag with the first two status squares instead.
                if (BattleGaugePieces.HasNumberEnds)
                {
                    CopyPiece(sheet, BattleGaugePieces.NumbersLeft, tiles, layout.HpLeft + 1);
                    CopyPiece(sheet, BattleGaugePieces.NumbersRight, tiles, layout.HpRight + 1);
                }
                else
                {
                    CopyPiece(sheet, BattleGaugePieces.NoStatus, tiles, layout.HpLeft);
                    CopyPiece(sheet, BattleGaugePieces.NoStatus + 1, tiles, layout.HpLeft + 1);
                }
            }
            else
            {
                int most = Math.Max(1, showing.MostHealth), now = Math.Clamp(showing.Health, 0, most);
                int[] cells = Cells(now, most, BattleGaugePieces.HpCells, out int pixels);
                int room = BattleGaugePieces.HpCells * 8;
                int colour = pixels * 2 > room ? BattleGaugePieces.HpGreen
                           : pixels * 5 > room ? BattleGaugePieces.HpYellow
                           : BattleGaugePieces.HpRed;
                int firstRun = layout.Gauge[0].size;
                for (int i = 0; i < cells.Length; i++)
                {
                    int at = i < firstRun ? layout.Gauge[0].pos + i : layout.Gauge[1].pos + i - firstRun;
                    CopyPiece(sheet, colour + cells[i], tiles, at);
                }
            }

            if (kind == Kind.PlayerSingle)
            {
                const int scale = 1000;
                int[] cells = showing.Level >= 100 ? new int[BattleGaugePieces.ExpCells]
                    : Cells((int)(Math.Clamp(showing.Experience, 0, 1) * scale), scale, BattleGaugePieces.ExpCells, out _);
                BattleGaugePieces.Span[] runs = BattleGaugePieces.ExpGauge;
                for (int i = 0; i < cells.Length; i++)
                {
                    int at = i < runs[0].Tiles ? runs[0].FirstTile + i : runs[1].FirstTile + i - runs[0].Tiles;
                    CopyPiece(sheet, BattleGaugePieces.ExpFill + cells[i], tiles, at);
                }
            }

            // A wild Pokemon's bar always gets this tile: the ball when it has been caught, a blank when not.
            if (theirs) CopyPiece(sheet, showing.Caught ? BattleGaugePieces.CaughtBall : BattleGaugePieces.NoStatus, tiles, layout.Caught);
        }

        /// <summary>How full each cell of a gauge is, 0 to 8 pixels, the way the games count it.</summary>
        private static int[] Cells(int now, int most, int count, out int pixels)
        {
            int[] cells = new int[count];
            pixels = (int)((long)now * 8 * count / most);
            if (pixels == 0 && now > 0) { cells[0] = 1; pixels = 1; return cells; }
            int left = pixels;
            for (int i = 0; i < count && left > 0; i++)
            {
                cells[i] = Math.Min(8, left);
                left -= cells[i];
            }
            return cells;
        }

        private static void CopyPiece(byte[] sheet, int piece, byte[] tiles, int tile)
        {
            if (tile <= 0) return;
            Copy(sheet, piece * TileBytes, tiles, (tile, 1), showing: 1);
        }

        private static void Copy(byte[] from, int fromAt, byte[] into, (int pos, int size) where, int showing, bool clearBackground = false)
        {
            int bytes = Math.Min(showing, where.size) * TileBytes;
            int to = CharDataAt(into) + where.pos * TileBytes;
            if (bytes <= 0 || fromAt < 0 || fromAt + bytes > from.Length) return;
            if (to < 0 || to + bytes > into.Length) return;
            if (clearBackground) Merge(from, fromAt, into, to, bytes);
            else Array.Copy(from, fromAt, into, to, bytes);
        }

        // What the clear text background patch does in the game: a pixel of the text's background keeps the bar's own pixel under it.
        private static void Merge(byte[] from, int fromAt, byte[] into, int to, int bytes)
        {
            for (int i = 0; i < bytes; i++)
            {
                byte b = from[fromAt + i], under = into[to + i];
                int low = (b & 0xF) == BattleGaugeGlyphs.Background ? under & 0xF : b & 0xF;
                int high = (b >> 4) == BattleGaugeGlyphs.Background ? under >> 4 : b >> 4;
                into[to + i] = (byte)(low | high << 4);
            }
        }

        /// <summary>
        /// Writes a row of digits four rows down, across two rows of tiles, keeping whatever the gauge
        /// already had above and below. This is what the games do so the level sits between the two.
        /// </summary>
        private static void Interleave(byte[] digits, byte[] tiles, (int pos, int size) upper, (int pos, int size) lower, bool clearBackground)
        {
            int half = TileBytes / 2;
            for (int t = 0; t < upper.size && t < lower.size; t++)
            {
                int from = t * TileBytes;
                if (from + TileBytes > digits.Length) return;

                int up = CharDataAt(tiles) + (upper.pos + t) * TileBytes;
                int down = CharDataAt(tiles) + (lower.pos + t) * TileBytes;
                if (up + TileBytes > tiles.Length || down + TileBytes > tiles.Length) return;

                if (clearBackground)
                {
                    Merge(digits, from, tiles, up + half, half);
                    Merge(digits, from + half, tiles, down, half);
                    continue;
                }
                Array.Copy(digits, from, tiles, up + half, half);          // its top half, low in the tile
                Array.Copy(digits, from + half, tiles, down, half);        // its bottom half, high in the next
            }
        }

        private static byte[] ToRgba(RawImage raw, int size)
        {
            byte[] made = new byte[size * size * 4];
            if (raw == null || raw.IsEmpty) return made;
            int wide = Math.Min(size, raw.Width), tall = Math.Min(size, raw.Height);
            for (int y = 0; y < tall; y++)
                for (int x = 0; x < wide; x++)
                {
                    int from = (y * raw.Width + x) * 4, to = (y * size + x) * 4;
                    made[to] = raw.Bgra[from + 2];
                    made[to + 1] = raw.Bgra[from + 1];
                    made[to + 2] = raw.Bgra[from];
                    made[to + 3] = raw.Bgra[from + 3];
                }
            return made;
        }
    }
}
