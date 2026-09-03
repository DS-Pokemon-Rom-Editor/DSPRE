using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>
    /// A graphic taken out and put straight back comes back the same, and a painted change survives.
    /// </summary>
    [Collection("rom")]
    public class GraphicRoundTripTests : IDisposable
    {
        private readonly ITestOutputHelper _out;
        public GraphicRoundTripTests(ITestOutputHelper o) { _out = o; }

        private static readonly string Source = TestRoms.Platinum;

        private string _work;

        private bool OpenACopy()
        {
            if (!Directory.Exists(Source)) return false;
            _work = Path.Combine(Path.GetTempPath(), "dspre_gfx_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(_work);
            foreach (var d in Directory.GetDirectories(Source, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(d.Replace(Source, _work));
            foreach (var f in Directory.GetFiles(Source, "*", SearchOption.AllDirectories))
                File.Copy(f, f.Replace(Source, _work), true);
            new RomInfo("CPUE", _work);
            GraphicAssets.Forget();
            return true;
        }

        public void Dispose()
        {
            if (_work != null && Directory.Exists(_work))
                try { Directory.Delete(_work, true); } catch { }
        }

        [Fact]
        public void SavingAndPuttingBackChangesNothing()
        {
            if (!OpenACopy()) { _out.WriteLine("the Platinum project is not here"); return; }

            int tried = 0, same = 0;
            var wrong = new List<string>();          // came back different: a real fault
            var refused = new List<string>();        // would not go back, but said why: allowed
            var formats = new SortedSet<string>();   // which kinds of drawing were proved
            string png = Path.Combine(_work, "one.png");

            foreach (var a in GraphicAssets.All)
            {
                int count = GraphicAssets.Count(a);
                if (count == 0) continue;

                // one drawing from each archive is enough to prove the loop holds for that archive's format
                int picked = -1;
                for (int i = 0; i < count && i < 400; i++)
                {
                    var ix = GraphicAssets.ReadIndexed(a, i, out _);
                    if (ix != null && ix.Width > 0) { picked = i; break; }
                }
                if (picked < 0) continue;

                var before = GraphicAssets.ReadIndexed(a, picked, out _);
                string err = GraphicAssets.ExportPng(a, picked, png);
                if (err != null) { refused.Add($"{a.Title}[{picked}] would not save: {err}"); continue; }

                err = GraphicAssets.ImportPng(a, picked, png);
                if (err != null) { refused.Add($"{a.Title}[{picked}]: {err}"); continue; }

                var after = GraphicAssets.ReadIndexed(a, picked, out _);
                tried++;
                if (after != null && before != null && after.Indices.SequenceEqual(before.Indices))
                {
                    same++;
                    formats.Add($"{before.BitsPerPixel} bits per pixel");
                }
                else wrong.Add($"{a.Title}[{picked}] came back different");
            }

            _out.WriteLine($"{tried} archives sent round the loop, {same} came back the same");
            _out.WriteLine($"  kinds of drawing proved: {string.Join(", ", formats)}");
            foreach (var w in wrong) _out.WriteLine("  came back wrong: " + w);
            _out.WriteLine($"  {refused.Count} refused, each with a reason:");
            foreach (var r in refused) _out.WriteLine("    " + r);

            Assert.True(tried > 0, "no drawing could be sent round the loop, so this proves nothing");
            Assert.True(wrong.Count == 0,
                "a drawing came back different from what went in: " + string.Join("; ", wrong.Take(10)));
            Assert.True(formats.Count >= 2,
                $"only {formats.Count} kind of drawing was proved ({string.Join(", ", formats)}); both the "
                + "sixteen colour and the two hundred and fifty six colour kinds should round trip");
            Assert.True(refused.All(r => r.Contains(':')), "a refusal came back without a reason");
        }


        /// <summary>
        /// Every Pokemon battle sprite in the game reads as a picture rather than as noise.
        /// </summary>
        public static IEnumerable<object[]> Games => new[]
        {
            // Diamond runs the key backwards from the last two bytes; the other two run it forwards.
            new object[] { "ADAE",
                TestRoms.Diamond,
                "Diamond", 4 },
            new object[] { "CPUE",
                TestRoms.Platinum,
                "Platinum", 4 },
            new object[] { "IPKE",
                TestRoms.HeartGold,
                "HeartGold", 4 },
        };

        [Theory]
        [MemberData(nameof(Games))]
        public void EveryBattleSpriteReadsAsAPictureAndNotAsNoise(string code, string path, string game, int dummies)
        {
            if (!Directory.Exists(path)) { _out.WriteLine($"{game}: not unpacked here"); return; }
            new RomInfo(code, path);
            GraphicAssets.Forget();

            var archive = GraphicAssets.All.First(a => a.Dir == DirNames.pokemonBattleSprites);
            int count = GraphicAssets.Count(archive);
            Assert.True(count > 0, $"{game}: the battle sprite archive is empty, so this proves nothing");

            int looked = 0;
            var noisy = new List<string>();
            for (int i = 0; i < count; i++)
            {
                var ix = GraphicAssets.ReadIndexed(archive, i, out _);
                if (ix == null || ix.Indices.Length < 64) continue;   // the palettes, every sixth pair
                looked++;
                if (!LooksDrawn(ix.Indices)) noisy.Add(i.ToString());
            }

            _out.WriteLine($"{game}: {looked} battle sprites read, {noisy.Count} came out as noise");
            if (noisy.Count > 0) _out.WriteLine("  noisy: " + string.Join(", ", noisy.Take(20)));

            Assert.True(looked > 1000, $"{game}: only {looked} sprites were read; the archive holds far more");

            // Entries 0 to 3 are the slot for Pokemon number 0, which does not exist.
            var allowed = Enumerable.Range(0, dummies).Select(i => i.ToString()).ToArray();
            Assert.Equal(allowed, noisy.ToArray());
        }

        /// <summary>A drawn picture repeats itself: most pixels match the one before them, because shapes
        /// are made of runs of one colour. Static does not.</summary>
        private static bool LooksDrawn(byte[] pixels)
        {
            int same = 0;
            for (int i = 1; i < pixels.Length; i++)
                if (pixels[i] == pixels[i - 1]) same++;
            return same * 2 > pixels.Length;   // over half of the pixels repeat their neighbour
        }

        /// <summary>The check above with the unscrambling taken away, to show it can actually fail. If a
        /// raw reading passed it, the check would be measuring nothing.</summary>
        [Theory]
        [MemberData(nameof(Games))]
        public void TheNoiseCheckRejectsAnUnscrambledReading(string code, string path, string game, int dummies)
        {
            if (!Directory.Exists(path)) { _out.WriteLine($"{game}: not unpacked here"); return; }
            new RomInfo(code, path);
            GraphicAssets.Forget();

            var archive = GraphicAssets.All.First(a => a.Dir == DirNames.pokemonBattleSprites);
            var real = GraphicAssets.ReadIndexed(archive, 18, out _);
            Assert.NotNull(real);
            Assert.True(LooksDrawn(real.Indices), $"{game}: sprite 18 does not read as a picture even unscrambled");

            // The same archive described as if the pixels were not scrambled, which is what DSPRE did
            // before, so the reading below is the exact wrong one this test exists to catch.
            var asRaw = new GraphicAssets.Archive
            {
                Dir = archive.Dir, Title = archive.Title, In = archive.In, What = archive.What,
                Colours = archive.Colours, ScrambledPixels = false,
            };
            GraphicAssets.Forget();
            var raw = GraphicAssets.ReadIndexed(asRaw, 18, out _);
            Assert.NotNull(raw);
            _out.WriteLine($"{game}: unscrambled reading repeats {raw.Indices.Where((v, i) => i > 0 && v == raw.Indices[i - 1]).Count()} of {raw.Indices.Length} pixels");
            Assert.False(LooksDrawn(raw.Indices), $"{game}: the noise check passed a reading that was never unscrambled, so it cannot fail");
        }

        [Fact]
        public void APaintedChangeSurvivesBeingSavedAndReopened()
        {
            if (!OpenACopy()) { _out.WriteLine("the Platinum project is not here"); return; }

            int painted = 0;
            var wrong = new List<string>();
            var refused = new List<string>();
            var formats = new SortedSet<string>();

            foreach (var a in GraphicAssets.All)
            {
                int count = GraphicAssets.Count(a);
                if (count == 0) continue;

                int picked = -1;
                GraphicAssets.Indexed ix = null;
                for (int i = 0; i < count && i < 400; i++)
                {
                    ix = GraphicAssets.ReadIndexed(a, i, out _);
                    if (ix != null && ix.Width >= 8 && ix.Height >= 8 && ix.ColourCount > 1) { picked = i; break; }
                }
                if (picked < 0) continue;

                // Paint a short line in a colour the drawing already has, which is what the brush does.
                var want = (byte[])ix.Indices.Clone();
                byte ink = (byte)(ix.ColourCount - 1);
                int start = ix.Width;
                for (int x = 0; x < 8 && start + x < want.Length; x++) want[start + x] = ink;

                string err = GraphicAssets.WriteIndices(a, picked, want, ix);
                if (err != null) { refused.Add($"{a.Title}[{picked}]: {err}"); continue; }

                GraphicAssets.Forget();
                var after = GraphicAssets.ReadIndexed(a, picked, out string why);
                if (after == null) { wrong.Add($"{a.Title}[{picked}] could not be reopened: {why}"); continue; }

                painted++;
                formats.Add($"{ix.BitsPerPixel} bits per pixel, {(ix.Width % 8 == 0 ? "in blocks" : "plain rows")}");
                if (!after.Indices.SequenceEqual(want))
                {
                    int differ = after.Indices.Where((v, i) => i < want.Length && v != want[i]).Count();
                    wrong.Add($"{a.Title}[{picked}] came back with {differ} pixels different from what was painted");
                }
            }

            _out.WriteLine($"{painted} archives painted and reopened");
            _out.WriteLine($"  kinds of drawing proved: {string.Join(", ", formats)}");
            foreach (var w in wrong) _out.WriteLine("  wrong after painting: " + w);
            _out.WriteLine($"  {refused.Count} refused, each with a reason:");
            foreach (var r in refused) _out.WriteLine("    " + r);

            Assert.True(painted > 0, "nothing could be painted, so this proves nothing");
            Assert.True(wrong.Count == 0,
                "paint did not survive being saved and reopened: " + string.Join("; ", wrong.Take(10)));
            Assert.True(refused.All(r => r.Contains(':')), "a refusal came back without a reason");
        }
    }
}
