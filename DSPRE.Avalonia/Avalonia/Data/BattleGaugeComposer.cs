using System;
using System.Collections.Generic;
using System.IO;
using DSPRE;
using DSPRE.ROMFiles;
using Images;   // NCGR / NCLR / NCER readers

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
        }

        // Tile numbers, not bytes: multiplied up when used. Straight from the gauge's own tables.
        private static readonly Dictionary<Kind, Layout> Layouts = new()
        {
            [Kind.PlayerSingle] = new Layout           // the games call this AA
            {
                Graphic = "SINGLE_GAGE2", X = 192, Y = 116,
                Name = new[] { (0x13, 5), (0x1b, 5), (0x50, 3), (0x58, 3) },
                LvMark = new[] { (0x53, 2), (0x5B, 2) },
                LvDigits = new[] { (0x55, 3), (0x5d, 3) },
                Hp = new[] { (0, 0), (0x68, 3) },
                HpMax = (0x6c, 3),
                Status = 0x24,
            },
            [Kind.OpponentSingle] = new Layout          // BB
            {
                Graphic = "SINGLE_GAGE1", X = 58, Y = 36,
                Name = new[] { (0x11, 7), (0x19, 7), (0x50, 1), (0x58, 1) },
                LvMark = new[] { (0x51, 2), (0x59, 2) },
                LvDigits = new[] { (0x53, 3), (0x5b, 3) },
                Hp = new[] { (0x31, 3), (0, 0) },
                HpMax = (0x35, 3),
                Status = 0x22,
            },
            [Kind.PlayerNear] = new Layout              // A
            {
                Graphic = "DOUBLE_GAGE3", X = 192, Y = 103,
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
                Graphic = "DOUBLE_GAGE1", X = 64, Y = 16,
                Name = new[] { (0x11, 7), (0x19, 7), (0x50, 1), (0x58, 1) },
                LvMark = new[] { (0x51, 2), (0x59, 2) },
                LvDigits = new[] { (0x53, 3), (0x5b, 3) },
                Hp = new[] { (0x31, 3), (0, 0) },
                HpMax = (0x35, 3),
                Status = 0x22,
            },
            [Kind.PlayerFar] = new Layout               // C
            {
                Graphic = "DOUBLE_GAGE4", X = 198, Y = 132,
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
                Graphic = "DOUBLE_GAGE2", X = 58, Y = 45,
                Name = new[] { (0x11, 7), (0x19, 7), (0x50, 1), (0x58, 1) },
                LvMark = new[] { (0x51, 2), (0x59, 2) },
                LvDigits = new[] { (0x53, 3), (0x5b, 3) },
                Hp = new[] { (0x31, 3), (0, 0) },
                HpMax = (0x35, 3),
                Status = 0x22,
            },
        };

        /// <summary>What to write on a gauge.</summary>
        public sealed class Showing
        {
            public string Name = "";
            public int Level = 5;
            public BattleGaugeText.Gender Gender = BattleGaugeText.Gender.Genderless;
            public BattleGaugeText.Status Status = BattleGaugeText.Status.None;
            public int Health = 20, MostHealth = 20;
            public bool ShowHealthNumbers;      // only your own side shows them
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
            Layouts.TryGetValue(kind, out var l) ? l.Graphic : null;

        public static string NameOf(Kind kind) => kind switch
        {
            Kind.PlayerSingle => "HP bar, your side",
            Kind.OpponentSingle => "HP bar, their side",
            Kind.PlayerNear => "HP bar, yours in front",
            Kind.PlayerFar => "HP bar, your partner",
            Kind.OpponentFar => "HP bar, theirs behind",
            Kind.OpponentNear => "HP bar, their partner",
            _ => "HP bar",
        };

        private const int Canvas = 256;
        private const int CharDataAt = 0x30;    // an NCGR keeps its tiles after a 48 byte header
        private const int TileBytes = 32;       // 8 by 8 at four bits a pixel

        /// <summary>
        /// Builds one gauge with its text already in it, or null when this ROM is not one we read.
        /// </summary>
        public static Drawn Build(Kind kind, Showing showing)
        {
            if (!BattleGaugeText.IsAvailable || !Layouts.TryGetValue(kind, out var layout)) return null;

            var narc = new ScriptNarc(RomInfo.DirNames.battleObj);
            if (!narc.Available) return null;

            int drawing = BattleObjects.Find(layout.Graphic, "Drawing");
            int cells = BattleObjects.Find(layout.Graphic, "As it appears");
            int colours = BattleObjects.Find("GAGE_PALETTE", "Colours");
            if (drawing < 0 || cells < 0 || colours < 0) return null;

            byte[] tiles;
            try { tiles = GraphicAssets.Unsqueeze(narc.Get(drawing)); }
            catch { return null; }
            if (tiles == null || tiles.Length < CharDataAt + TileBytes) return null;

            WriteTheText(tiles, layout, showing);

            var temps = new List<string>();
            try
            {
                var nclr = new NCLR(Temp(narc.Get(colours), temps), colours, "gauge.nclr");
                var ncgr = new NCGR(Temp(tiles, temps), drawing, "gauge.ncgr");
                var ncer = new NCER(Temp(narc.Get(cells), temps), cells, "gauge.ncer");

                var raw = ncer.Get_RawImage(ncgr, nclr, 0, Canvas, Canvas, true, -1, null);
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
            finally { foreach (string t in temps) { try { File.Delete(t); } catch { } } }
        }

        // ── writing into the gauge's tiles ────────────────────────────────────────────────────────

        private static void WriteTheText(byte[] tiles, Layout layout, Showing showing)
        {
            // The name is eight tiles across and two down, and goes in as four pieces: the top row and
            // the bottom row, each cut where the first sprite piece runs out and the second begins.
            byte[] name = BattleGaugeGlyphs.NameBlock(showing.Name);
            if (name != null)
            {
                var n = layout.Name;
                Copy(name, 0, tiles, n[0], showing: n[0].size);
                Copy(name, 8 * TileBytes, tiles, n[1], showing: n[1].size);
                Copy(name, n[0].size * TileBytes, tiles, n[2], showing: n[2].size);
                Copy(name, (8 + n[1].size) * TileBytes, tiles, n[3], showing: n[3].size);
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
            if (level != null) Interleave(level, tiles, layout.LvDigits[0], layout.LvDigits[1]);

            if (showing.ShowHealthNumbers)
            {
                // A double battle's bars normally show the bar and no numbers; the games swap the two
                // over on a press, and only then do they put the "/" in. A single battle's bars have
                // it in the picture already, which is why only these two write one.
                if (layout.Slash > 0)
                {
                    var slash = BattleGaugeText.Slash();
                    if (slash != null)
                    {
                        var one = new byte[TileBytes];
                        for (int y = 0; y < 8; y++)
                            for (int x = 0; x < 8; x += 2)
                                one[y * 4 + x / 2] = (byte)((BattleGaugeGlyphs.Ink(slash.At(x, y)) & 0xF)
                                                          | (BattleGaugeGlyphs.Ink(slash.At(x + 1, y)) << 4));
                        Copy(one, 0, tiles, (layout.Slash, 1), showing: 1);
                    }
                }

                var hp = layout.Hp[0].size > 0 ? layout.Hp[0] : layout.Hp[1];
                if (hp.size > 0)
                {
                    // The games pad this one on the left so it sits against the "/".
                    byte[] now = BattleGaugeGlyphs.NumberRow(showing.Health, hp.size, againstTheRight: true);
                    if (now != null) Copy(now, 0, tiles, hp, showing: hp.size);
                }
                if (layout.HpMax.size > 0)
                {
                    byte[] most = BattleGaugeGlyphs.NumberRow(showing.MostHealth, layout.HpMax.size);
                    if (most != null) Copy(most, 0, tiles, layout.HpMax, showing: layout.HpMax.size);
                }
            }

            byte[] status = BattleGaugeGlyphs.StatusRow(showing.Status);
            if (status != null)
                Copy(status, 0, tiles, (layout.Status, BattleGaugeText.StatusTiles),
                     showing: BattleGaugeText.StatusTiles);
        }

        private static void Copy(byte[] from, int fromAt, byte[] into, (int pos, int size) where, int showing)
        {
            int bytes = Math.Min(showing, where.size) * TileBytes;
            int to = CharDataAt + where.pos * TileBytes;
            if (bytes <= 0 || fromAt < 0 || fromAt + bytes > from.Length) return;
            if (to < 0 || to + bytes > into.Length) return;
            Array.Copy(from, fromAt, into, to, bytes);
        }

        /// <summary>
        /// Writes a row of digits four rows down, across two rows of tiles, keeping whatever the gauge
        /// already had above and below. This is what the games do so the level sits between the two.
        /// </summary>
        private static void Interleave(byte[] digits, byte[] tiles, (int pos, int size) upper, (int pos, int size) lower)
        {
            int half = TileBytes / 2;
            for (int t = 0; t < upper.size && t < lower.size; t++)
            {
                int from = t * TileBytes;
                if (from + TileBytes > digits.Length) return;

                int up = CharDataAt + (upper.pos + t) * TileBytes;
                int down = CharDataAt + (lower.pos + t) * TileBytes;
                if (up + TileBytes > tiles.Length || down + TileBytes > tiles.Length) return;

                Array.Copy(digits, from, tiles, up + half, half);          // its top half, low in the tile
                Array.Copy(digits, from + half, tiles, down, half);        // its bottom half, high in the next
            }
        }

        private static string Temp(byte[] bytes, List<string> temps)
        {
            if (bytes != null && bytes.Length >= 4 && bytes[0] == 0x10)
            { try { bytes = NSMBe4.ROM.LZ77_Decompress(bytes); } catch { } }
            string path = Path.Combine(Path.GetTempPath(), "dspre_gauge_" + Guid.NewGuid().ToString("N") + ".bin");
            File.WriteAllBytes(path, bytes ?? Array.Empty<byte>());
            temps.Add(path);
            return path;
        }

        private static byte[] ToRgba(RawImage raw, int size)
        {
            var made = new byte[size * size * 4];
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
