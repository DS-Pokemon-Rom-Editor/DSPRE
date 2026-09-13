using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DSPRE;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.ViewModels.Graphics;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// Goes through every animation the picker offers, in both games, and says for each one whether it can
    /// actually be drawn and whether what it draws looks right. This exists because "the round trip is
    /// byte-perfect" says nothing about whether the picture on screen is the picture the game shows.
    ///
    /// The soft findings are reported rather than asserted: a game is allowed to hold an animation whose
    /// frames repeat, or one nothing references. What is asserted is the part that would be a defect in
    /// DSPRE: a file the picker calls drawable that cannot be drawn.
    /// </summary>
    [Collection("rom")]
    public class CellAnimationAuditTests
    {
        private readonly ITestOutputHelper _out;
        public CellAnimationAuditTests(ITestOutputHelper output) => _out = output;

        private static string Where => Environment.GetEnvironmentVariable("DSPRE_AUDIT_OUT");

        private static bool Open(string project, string id)
        {
            if (!Directory.Exists(project)) return false;
            try { new RomInfo(id, project); GraphicAssets.Forget(); } catch { return false; }
            return true;
        }

        private sealed class Row
        {
            public string Archive;
            public int Animation, Cells, Sprites, Palette;
            public int Sequences, Frames, Banks, SheetTiles, Colours;
            public bool Extended, Drawable;
            public int MaxTileWanted = -1;
            public int EmptySequences, FrozenSequences, ZeroHolds, CellsOutOfRange;
            public string Elements = "", Modes = "";

            /// <summary>Set only for a sequence that drew nothing, to say why.</summary>
            public bool? BlankTiles;
            public bool? AnyRowDraws;
            /// <summary>The shifts a never-changing sequence carries, so a dropped one would show.</summary>
            public string FrozenShifts;
            public bool FrozenCarriesShift;

            /// <summary>Things that would be a defect here.</summary>
            public readonly List<string> Notes = new();

            /// <summary>Things that are true of the games' own data and need no fixing.</summary>
            public readonly List<string> Facts = new();

            public string Line =>
                $"{Archive}\t#{Animation}\tlayout {Cells}\tsheet {Sprites}\tcolours {Palette}\t"
              + $"{Sequences} seq\t{Frames} fr\tbanks {Banks}\tsheet {SheetTiles} tiles\t"
              + $"wants {MaxTileWanted}\tempty {EmptySequences}\tfrozen {FrozenSequences}\t"
              + $"zero holds {ZeroHolds}\tbad cells {CellsOutOfRange}\telem {Elements}\tmode {Modes}\t"
              + (BlankTiles == null ? "" : $"tiles blank {BlankTiles} anyrow {AnyRowDraws}\t")
              + (FrozenShifts == null ? "" : $"shifts {FrozenShifts}\t")
              + (Notes.Count > 0 ? "FIX: " + string.Join("; ", Notes) + (Facts.Count > 0 ? "  |  " : "") : "")
              + (Facts.Count > 0 ? "rom: " + string.Join("; ", Facts) : "")
              + (Notes.Count == 0 && Facts.Count == 0 ? "ok" : "");
        }

        private static byte[] Member(ScriptNarc narc, int i)
        {
            try { return NitroBgCodec.Inflate(narc.Get(i)); } catch { return null; }
        }

        /// <summary>
        /// The sheet as sprite memory holds it. Some Pokétch screens have a sheet of shared figures loaded
        /// ahead of their own and count tiles across both, so without laying that out first their cells look
        /// like they read past the end of a sheet that is really the second half of one.
        /// </summary>
        private static byte[] Sheet(ScriptNarc narc, CellAnimationFound f)
        {
            byte[] mine = DsBgScreen.ReadCharacters(Member(narc, f.Sprites));
            if (f.Archive != RomInfo.DirNames.poketch) return mine;

            var app = PoketchApps.All.FirstOrDefault(a => a.Animation == f.Animation || a.Cells == f.Cells);
            int shared = app == null ? -1 : PoketchApps.SharedSheetFor(app);
            if (shared < 0) return mine;

            byte[] first = DsBgScreen.ReadCharacters(Member(narc, shared));
            if (first.Length == 0) return mine;
            var both = new byte[first.Length + mine.Length];
            first.CopyTo(both, 0);
            mine.CopyTo(both, first.Length);
            return both;
        }

        private static bool AnythingDrawn(byte[] rgba)
        {
            for (int i = 3; i < rgba.Length; i += 4) if (rgba[i] != 0) return true;
            return false;
        }

        /// <summary>
        /// Whether a sheet reads as a drawing rather than as static. A drawn picture repeats itself, because
        /// shapes are runs of one colour; pixels that almost never match their neighbour mean the bytes are
        /// being read the wrong way. Without this, a sheet of noise passes every other check here, which is
        /// exactly what let the trainer sheets through.
        /// </summary>
        private static bool SheetLooksDrawn(byte[] chars)
        {
            if (chars == null || chars.Length < 64) return true;
            int same = 0, n = chars.Length * 2;
            byte last = (byte)(chars[0] & 0x0F);
            for (int i = 0; i < chars.Length; i++)
            {
                byte lo = (byte)(chars[i] & 0x0F), hi = (byte)(chars[i] >> 4);
                if (lo == last) same++;
                if (hi == lo) same++;
                last = hi;
            }
            return same * 2 > n;
        }

        /// <summary>
        /// Whether every tile those drawings read is entirely colour zero, which is the transparent slot.
        /// True means the game itself stores nothing there, so an empty preview is correct.
        /// </summary>
        private static bool TilesAreBlank(List<DsBgScreen.Oam[]> banks, byte[] chars, IEnumerable<int> used)
        {
            foreach (int b in used)
            {
                if (b < 0 || b >= banks.Count) continue;
                foreach (var piece in banks[b])
                {
                    if (piece == null) continue;
                    int wide = Math.Max(1, piece.Width / 8), tall = Math.Max(1, piece.Height / 8);
                    for (int t = piece.Tile; t < piece.Tile + wide * tall; t++)
                        for (int at = t * 32; at < (t + 1) * 32 && at < chars.Length; at++)
                            if (chars[at] != 0) return false;
                }
            }
            return true;
        }

        /// <summary>Whether any row of the paired colours would draw something, which says the row is wrong.</summary>
        private static bool SomeRowDraws(ushort[] all, List<DsBgScreen.Oam[]> banks, byte[] chars, IEnumerable<int> used)
        {
            if (all == null || all.Length == 0) return false;
            int rows = Math.Max(1, all.Length / 16);
            var cells = used.Where(b => b >= 0 && b < banks.Count).ToList();
            for (int row = 0; row < rows; row++)
            {
                ushort[] colours = DsBgScreen.Row(all, row);
                foreach (int b in cells)
                {
                    var rgba = new byte[DsBgScreen.Width * DsBgScreen.Height * 4];
                    DsBgScreen.DrawCell(rgba, banks[b], chars, _ => colours,
                                        DsBgScreen.Width / 2, DsBgScreen.Height / 2);
                    if (AnythingDrawn(rgba)) return true;
                }
            }
            return false;
        }

        /// <summary>The highest tile any piece of these banks reads, so a short sheet shows up.</summary>
        private static int HighestTile(List<DsBgScreen.Oam[]> banks, IEnumerable<int> used)
        {
            int top = -1;
            foreach (int b in used)
            {
                if (b < 0 || b >= banks.Count) continue;
                foreach (var s in banks[b])
                {
                    if (s == null) continue;
                    int wide = Math.Max(1, s.Width / 8), tall = Math.Max(1, s.Height / 8);
                    top = Math.Max(top, s.Tile + wide * tall - 1);
                }
            }
            return top;
        }

        private Row Audit(ScriptNarc narc, CellAnimationFound f)
        {
            var row = new Row
            {
                Archive = f.ArchiveName, Animation = f.Animation, Cells = f.Cells,
                Sprites = f.Sprites, Palette = f.Palette, Sequences = f.Sequences,
                Frames = f.Frames, Extended = f.Extended, Drawable = f.Drawable,
            };

            var file = NanrFile.Read(Member(narc, f.Animation));
            if (file == null) { row.Notes.Add("the animation would not read"); return row; }

            var banks = f.Cells >= 0 ? DsBgScreen.ReadCells(Member(narc, f.Cells)) : new List<DsBgScreen.Oam[]>();
            byte[] chars = f.Sprites >= 0 ? Sheet(narc, f) : Array.Empty<byte>();
            ushort[] narcColours = f.Palette >= 0
                ? DsBgScreen.ReadColours(Member(narc, f.Palette))
                : Array.Empty<ushort>();
            ushort[] colours = narcColours.Length > 0
                ? DsBgScreen.Row(narcColours, 0)
                : Array.Empty<ushort>();

            row.Banks = banks.Count;
            row.SheetTiles = chars.Length / 32;
            row.Colours = colours.Length;

            if (f.Cells >= 0 && banks.Count == 0) row.Notes.Add("the layout it names holds no drawings");
            if (f.Sprites >= 0 && chars.Length == 0) row.Notes.Add("the sheet it names holds no tiles");
            if (chars.Length > 0 && !SheetLooksDrawn(chars))
                row.Notes.Add("its sheet reads as noise, so the pixels are being read the wrong way");
            if (f.Palette >= 0 && colours.Length == 0) row.Notes.Add("the colours it names are empty");

            var elements = new SortedSet<int>();
            var modes = new SortedSet<uint>();
            var wanted = new SortedSet<int>();

            for (int s = 0; s < file.Sequences.Count; s++)
            {
                var seq = file.Sequences[s];
                elements.Add(seq.ElementType);
                modes.Add(seq.PlayMode);

                var shots = new List<byte[]>();
                for (int fr = 0; fr < seq.Frames.Count; fr++)
                {
                    if (seq.Frames[fr].Delay == 0) row.ZeroHolds++;
                    int cell = file.CellOf(s, fr);
                    wanted.Add(cell);
                    if (banks.Count > 0 && (cell < 0 || cell >= banks.Count)) row.CellsOutOfRange++;

                    if (banks.Count == 0 || chars.Length == 0 || colours.Length == 0) continue;
                    if (cell < 0 || cell >= banks.Count) continue;

                    var rgba = new byte[DsBgScreen.Width * DsBgScreen.Height * 4];
                    var (sx, sy) = file.ShiftOf(s, fr);
                    var (deg, kx, ky) = file.TurnOf(s, fr);
                    DsBgScreen.DrawCellTurned(rgba, banks[cell], chars, _ => colours,
                                              DsBgScreen.Width / 2 + sx, DsBgScreen.Height / 2 + sy,
                                              deg, kx, ky);
                    shots.Add(rgba);
                }

                if (shots.Count > 0 && !shots.Any(AnythingDrawn))
                {
                    row.EmptySequences++;
                    // Nothing drawn has two very different causes: the tiles those drawings read are blank
                    // in the game's own data, or the colours paired with them are the wrong ones. Saying
                    // which is the difference between a defect here and a fact about the ROM.
                    if (row.BlankTiles == null)
                    {
                        var reads = new SortedSet<int>();
                        for (int f2 = 0; f2 < seq.Frames.Count; f2++) reads.Add(file.CellOf(s, f2));
                        row.BlankTiles = TilesAreBlank(banks, chars, reads);
                        row.AnyRowDraws = SomeRowDraws(narcColours, banks, chars, reads);
                    }
                }
                else if (shots.Count > 1 && shots.Skip(1).All(x => x.SequenceEqual(shots[0])))
                {
                    row.FrozenSequences++;
                    // A run that holds one drawing still is normal. One that carries a shift and still
                    // never moves is not, so the shifts are recorded rather than guessed at.
                    var moves = new SortedSet<string>();
                    for (int f2 = 0; f2 < seq.Frames.Count; f2++)
                    {
                        var (mx, my) = file.ShiftOf(s, f2);
                        moves.Add($"{mx},{my}");
                    }
                    if (moves.Any(m => m != "0,0")) row.FrozenCarriesShift = true;
                    if (row.FrozenShifts == null) row.FrozenShifts = string.Join(" ", moves);
                }
            }

            row.MaxTileWanted = HighestTile(banks, wanted);
            row.Elements = string.Join("/", elements);
            row.Modes = string.Join("/", modes);

            if (row.MaxTileWanted >= 0 && row.SheetTiles > 0 && row.MaxTileWanted >= row.SheetTiles)
                row.Notes.Add($"reads tile {row.MaxTileWanted} from a {row.SheetTiles} tile sheet");
            if (row.CellsOutOfRange > 0)
                row.Notes.Add($"{row.CellsOutOfRange} frames name a drawing the layout does not have");
            // Drawing nothing is only a defect when there was something to draw. Tiles that are entirely the
            // transparent index, with no palette row that would change that, are blank in the game itself.
            if (row.EmptySequences > 0)
            {
                if (row.BlankTiles == true && row.AnyRowDraws == false)
                    row.Facts.Add($"{row.EmptySequences} sequences are blank in the ROM");
                else
                    row.Notes.Add($"{row.EmptySequences} sequences draw nothing though their tiles are not blank");
            }

            // A run that holds one drawing still is normal. One that carries a shift and still never moves
            // would mean the shift is being dropped.
            if (row.FrozenSequences > 0)
            {
                if (row.FrozenCarriesShift)
                    row.Notes.Add($"{row.FrozenSequences} sequences carry a shift and still never move");
                else
                    row.Facts.Add($"{row.FrozenSequences} sequences hold one drawing still");
            }

            if (row.ZeroHolds > 0)
                row.Facts.Add($"{row.ZeroHolds} frames have no hold, which playback clamps to one tick");

            return row;
        }

        private void Sweep(string project, string id, string game)
        {
            Skip.If(!Open(project, id), $"{game} is not unpacked here");

            var rows = new List<Row>();
            foreach (var dir in RomInfo.gameDirs.Keys.ToList())
            {
                var found = CellAnimationPickerViewModel.InArchive(dir);
                if (found.Count == 0) continue;
                ScriptNarc narc;
                try { narc = new ScriptNarc(dir); } catch { continue; }
                if (!narc.Available) continue;
                foreach (var f in found) rows.Add(Audit(narc, f));
            }

            Assert.True(rows.Count > 0, $"{game}: no animations were found to audit");

            var drawable = rows.Where(r => r.Drawable).ToList();
            var broken = drawable.Where(r => r.Notes.Count > 0).ToList();

            var report = new StringBuilder();
            report.AppendLine($"{game}: {rows.Count} animation files, {drawable.Count} the picker calls drawable");
            report.AppendLine($"  no layout found: {rows.Count(r => r.Cells < 0)}");
            report.AppendLine($"  no sheet found: {rows.Count(r => r.Sprites < 0)}");
            report.AppendLine($"  no colours found: {rows.Count(r => r.Palette < 0)}");
            report.AppendLine($"  drawable with something to fix: {broken.Count}");
            report.AppendLine($"  drawable with only facts about the ROM: "
                            + drawable.Count(r => r.Notes.Count == 0 && r.Facts.Count > 0));
            report.AppendLine($"  reading past the end of their sheet: {drawable.Count(r => r.MaxTileWanted >= r.SheetTiles && r.SheetTiles > 0)}");
            report.AppendLine($"  naming a drawing the layout lacks: {drawable.Count(r => r.CellsOutOfRange > 0)}");
            report.AppendLine($"  drawing nothing at all: {drawable.Count(r => r.EmptySequences > 0)}");
            report.AppendLine($"  never changing picture: {drawable.Count(r => r.FrozenSequences > 0)}");
            report.AppendLine($"  holding a frame for no time: {drawable.Count(r => r.ZeroHolds > 0)}");
            report.AppendLine();
            foreach (var r in rows.OrderByDescending(r => r.Notes.Count)
                                  .ThenByDescending(r => r.Facts.Count)
                                  .ThenBy(r => r.Archive))
                report.AppendLine(r.Line);

            string text = report.ToString();
            foreach (var line in text.Split('\n').Take(24)) _out.WriteLine(line.TrimEnd());

            string outDir = Where;
            if (outDir != null)
            {
                Directory.CreateDirectory(outDir);
                File.WriteAllText(Path.Combine(outDir, $"animation-audit-{game}.txt"), text);
                _out.WriteLine($"full report written to {outDir}");
            }

            // The hard rule: anything the picker offers as drawable must actually draw.
            var cannot = drawable.Where(r => r.Banks == 0 || r.SheetTiles == 0).ToList();
            Assert.True(cannot.Count == 0,
                $"{game}: {cannot.Count} files are offered as drawable but have no drawing: "
              + string.Join("; ", cannot.Take(6).Select(r => $"{r.Archive}#{r.Animation}")));
        }

        [SkippableFact]
        public void EveryAnimationPlatinumOffersIsWorthOffering()
            => Sweep(TestRoms.Platinum, "CPUE", "Platinum");

        [SkippableFact]
        public void EveryAnimationHeartGoldOffersIsWorthOffering()
            => Sweep(TestRoms.HeartGold, "IPKE", "HeartGold");
    }
}
