using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// How backgrounds in the graphics browser are arranged. Narrow screens are row-major; screens
    /// wider than 32 tiles use the DS's 32-by-32 screen blocks.
    /// </summary>
    [Collection("rom")]
    public class ArrangementLayoutTests
    {
        private readonly ITestOutputHelper _out;
        public ArrangementLayoutTests(ITestOutputHelper output) { _out = output; }

        private sealed record Arrangement(string Name, byte[] Data, int Cols, int Rows, int Entries);

        private static List<Arrangement> ReadCurrentGame()
        {
            var result = new List<Arrangement>();
            foreach (var archive in GraphicAssets.All)
            {
                var narc = new ScriptNarc(archive.Dir);
                if (!narc.Available) continue;
                for (int i = 0; i < narc.Count; i++)
                {
                    byte[] data;
                    try { data = GraphicAssets.Unsqueeze(narc.Get(i)); }
                    catch { continue; }
                    if (GraphicAssets.Identify(data) != GraphicAssets.Kind.TileMap || data.Length < 0x24)
                        continue;
                    int width = NitroBgCodec.U16(data, 0x18);
                    int height = NitroBgCodec.U16(data, 0x1a);
                    if (width < 8 || height < 8) continue;
                    result.Add(new Arrangement($"{archive.Title}[{i}]", data, width / 8, height / 8,
                        NitroBgCodec.U32(data, 0x20) / 2));
                }
            }
            return result;
        }

        [SkippableFact]
        public void PlatinumArrangementsHaveTheExpectedEntryCount() =>
            CheckCounts("CPUE", TestRoms.Platinum, "Platinum");

        [SkippableFact]
        public void HeartGoldArrangementsHaveTheExpectedEntryCount() =>
            CheckCounts("IPKE", TestRoms.HeartGold, "HeartGold");

        private void CheckCounts(string code, string path, string game)
        {
            Skip.IfNot(Directory.Exists(path), $"The {game} test ROM project is not available.");
            new RomInfo(code, path);
            GraphicAssets.Forget();
            var all = ReadCurrentGame();
            Assert.True(all.Count >= 20,
                $"{game}: only {all.Count} arrangements were found through the graphics archives");

            // Some valid screens declare a full 256-pixel canvas but carry only the used top half.
            // The renderer treats missing entries as empty. A file may therefore be shorter than the
            // declared canvas, but it must never carry more entries than that canvas can address.
            var tooLarge = all.Where(a => a.Entries > NitroBgCodec.SquareCount(a.Cols, a.Rows)).ToList();
            int complete = all.Count(a => a.Entries == NitroBgCodec.SquareCount(a.Cols, a.Rows));
            int narrow = all.Count(a => a.Cols < 32);
            int wide = all.Count(a => a.Cols > 32);
            _out.WriteLine($"{game}: {all.Count} arrangements, {narrow} narrower than 32 tiles, "
                         + $"{wide} wider, {all.Count - narrow - wide} exactly 32; {complete} fill their canvas");

            Assert.True(complete >= 20, $"{game}: only {complete} complete arrangements exercised the rule");
            Assert.Empty(tooLarge);
        }

        [Fact]
        public void NarrowAndWideIndexRulesDifferAtTheBlockBoundary()
        {
            Assert.Equal(0, NitroBgCodec.SquareIndex(14, 0, 0));
            Assert.Equal(13, NitroBgCodec.SquareIndex(14, 13, 0));
            Assert.Equal(14, NitroBgCodec.SquareIndex(14, 0, 1));
            Assert.Equal(182, NitroBgCodec.SquareCount(14, 13));

            Assert.Equal(32, NitroBgCodec.SquareIndex(32, 0, 1));
            Assert.Equal(768, NitroBgCodec.SquareCount(32, 24));

            Assert.Equal(31, NitroBgCodec.SquareIndex(64, 31, 0));
            Assert.Equal(1024, NitroBgCodec.SquareIndex(64, 32, 0));
            Assert.Equal(2048, NitroBgCodec.SquareIndex(64, 0, 32));
            Assert.Equal(4096, NitroBgCodec.SquareCount(64, 64));
        }

        [SkippableFact]
        public void HeartGoldWideArrangementDrawsSolidlyOnlyInScreenBlocks()
        {
            Skip.IfNot(Directory.Exists(TestRoms.HeartGold),
                "The HeartGold test ROM project is not available.");
            new RomInfo("IPKE", TestRoms.HeartGold);
            GraphicAssets.Forget();

            var candidates = ReadCurrentGame().Where(a => a.Cols > 32).ToList();
            Skip.If(candidates.Count == 0, "HeartGold has no wide arrangement in the listed graphics archives.");
            var arrangement = candidates.FirstOrDefault(a =>
                Solidity(a, true) == 1.0 && Solidity(a, false) < 0.6);
            Assert.NotNull(arrangement);
            _out.WriteLine($"{arrangement.Name}: {arrangement.Cols} by {arrangement.Rows} tiles");
        }

        private static double Solidity(Arrangement arrangement, bool blocked)
        {
            ushort At(int index) => (ushort)NitroBgCodec.U16(arrangement.Data, 0x24 + index * 2);
            int ground = Enumerable.Range(0, arrangement.Cols * arrangement.Rows).Select(At)
                .GroupBy(value => value).OrderByDescending(group => group.Count()).First().Key;
            return Solidity(arrangement.Cols, arrangement.Rows,
                (x, y) => At(blocked
                    ? NitroBgCodec.SquareIndex(arrangement.Cols, x, y)
                    : y * arrangement.Cols + x), ground);
        }

        private static double Solidity(int cols, int rows, Func<int, int, int> at, int ground)
        {
            var xs = new List<int>();
            var ys = new List<int>();
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                    if (at(x, y) != ground) { xs.Add(x); ys.Add(y); }
            if (xs.Count == 0) return 0;
            return xs.Count / (double)((xs.Max() - xs.Min() + 1) * (ys.Max() - ys.Min() + 1));
        }
    }
}
