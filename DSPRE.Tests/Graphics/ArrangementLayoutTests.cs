using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// How a background's arrangement is laid out, checked against every arrangement the ROM carries
    /// rather than against a rule copied from somewhere. A wide one is kept in blocks of 32 by 32 and
    /// a narrow one straight across at its own width, and getting that backwards scrambles the picture.
    /// </summary>
    public class ArrangementLayoutTests
    {
        private readonly ITestOutputHelper _out;
        public ArrangementLayoutTests(ITestOutputHelper o) { _out = o; }

        private static readonly string Unpacked = TestRoms.HeartGold + @"\unpacked";

        private static IEnumerable<(string path, int cols, int rows, int entries)> Real()
        {
            if (!Directory.Exists(Unpacked)) yield break;
            foreach (string dir in Directory.GetDirectories(Unpacked))
                foreach (string f in Directory.GetFiles(dir))
                {
                    byte[] d;
                    try
                    {
                        var info = new FileInfo(f);
                        if (info.Length < 0x24 || info.Length > 200000) continue;
                        d = File.ReadAllBytes(f);
                    }
                    catch { continue; }
                    if (d[0] != 'R' || d[1] != 'C' || d[2] != 'S' || d[3] != 'N') continue;
                    int w = NitroBgCodec.U16(d, 0x18), h = NitroBgCodec.U16(d, 0x1A);
                    if (w < 8 || h < 8) continue;
                    yield return (Path.GetFileName(dir) + "/" + Path.GetFileName(f),
                                  w / 8, h / 8, NitroBgCodec.U32(d, 0x20) / 2);
                }
        }

        [Fact]
        public void EveryArrangementInTheRomHoldsTheNumberOfEntriesTheRuleSaysItShould()
        {
            var all = Real().ToList();
            Assert.True(all.Count >= 20,
                $"only {all.Count} arrangements were found, so this proved nothing. "
                + $"It reads {Unpacked}.");

            var wrong = all.Where(a => NitroBgCodec.SquareCount(a.cols, a.rows) != a.entries).ToList();
            int narrow = all.Count(a => a.cols < 32), wide = all.Count(a => a.cols > 32);
            _out.WriteLine($"{all.Count} arrangements: {narrow} narrower than 32 squares, "
                         + $"{wide} wider, {all.Count - narrow - wide} exactly 32.");

            // The rule and the old one only part company on the narrow ones, so a run with none of those
            // in it would pass while testing nothing.
            Assert.True(narrow > 0, "no arrangement narrower than 32 squares was found, and those are the "
                                  + "only ones that tell the two layouts apart");
            Assert.True(wrong.Count == 0, string.Join("\n", wrong.Select(a =>
                $"{a.path} is {a.cols}x{a.rows} and holds {a.entries} entries, "
                + $"but the rule says {NitroBgCodec.SquareCount(a.cols, a.rows)}")));
        }

        [Fact]
        public void ANarrowArrangementIsReadStraightAcrossAndAWideOneInBlocks()
        {
            // Fourteen squares across: entry after entry, at fourteen a row.
            Assert.Equal(0, NitroBgCodec.SquareIndex(14, 0, 0));
            Assert.Equal(13, NitroBgCodec.SquareIndex(14, 13, 0));
            Assert.Equal(14, NitroBgCodec.SquareIndex(14, 0, 1));
            Assert.Equal(182, NitroBgCodec.SquareCount(14, 13));

            // Exactly 32 across: the two layouts say the same thing, which is why they were confusable.
            Assert.Equal(32, NitroBgCodec.SquareIndex(32, 0, 1));
            Assert.Equal(768, NitroBgCodec.SquareCount(32, 24));

            // Sixty-four across: the first block of 32 by 32, then the second beside it.
            Assert.Equal(31, NitroBgCodec.SquareIndex(64, 31, 0));
            Assert.Equal(1024, NitroBgCodec.SquareIndex(64, 32, 0));
            Assert.Equal(2048, NitroBgCodec.SquareIndex(64, 0, 32));
            Assert.Equal(4096, NitroBgCodec.SquareCount(64, 64));
        }

        /// <summary>
        /// The one arrangement in the ROM wide enough to tell the layouts apart, checked the way it was
        /// worked out: the squares it draws on should sit in a solid block, not scattered.
        /// </summary>
        [Fact]
        public void TheWideArrangementInTheRomDrawsASolidShapeOnlyWhenReadInBlocks()
        {
            string path = Path.Combine(Unpacked, "battleBg", "0268");
            if (!File.Exists(path)) { Assert.Fail($"{path} is not there, so this proved nothing."); return; }

            byte[] d = File.ReadAllBytes(path);
            int cols = NitroBgCodec.U16(d, 0x18) / 8, rows = NitroBgCodec.U16(d, 0x1A) / 8;
            Assert.True(cols > 32, $"this arrangement is {cols} squares across, which cannot tell the "
                                 + "layouts apart");

            ushort At(int index) => (ushort)NitroBgCodec.U16(d, 0x24 + index * 2);
            int ground = Enumerable.Range(0, cols * rows).Select(At)
                                   .GroupBy(v => v).OrderByDescending(g => g.Count()).First().Key;

            Assert.Equal(1.0, Solidity(cols, rows, (x, y) => At(NitroBgCodec.SquareIndex(cols, x, y)), ground));
            Assert.True(Solidity(cols, rows, (x, y) => At(y * cols + x), ground) < 0.6,
                        "read straight across, the drawn squares happen to be solid too, so this test "
                        + "cannot tell the layouts apart any more");
        }

        /// <summary>How much of the box the drawn squares sit in is actually drawn on. A real picture
        /// fills its own box; a scrambled one leaves holes.</summary>
        private static double Solidity(int cols, int rows, Func<int, int, int> at, int ground)
        {
            var xs = new List<int>(); var ys = new List<int>();
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                    if (at(x, y) != ground) { xs.Add(x); ys.Add(y); }
            if (xs.Count == 0) return 0;
            int x0 = xs.Min(), x1 = xs.Max(), y0 = ys.Min(), y1 = ys.Max();
            return xs.Count / (double)((x1 - x0 + 1) * (y1 - y0 + 1));
        }
    }
}
