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
    /// <summary>The quick name reader agrees with the full one, on every model in every game.</summary>
    [Collection("rom")]
    public class ModelNameTests
    {
        private readonly ITestOutputHelper _out;
        public ModelNameTests(ITestOutputHelper o) { _out = o; }

        private static readonly string Diamond = TestRoms.Diamond;
        private static readonly string Platinum = TestRoms.Platinum;
        private static readonly string HeartGold = TestRoms.HeartGold;

        public static IEnumerable<object[]> Games => new[]
        {
            new object[] { "ADAE", Diamond, "Diamond" },
            new object[] { "CPUE", Platinum, "Platinum" },
            new object[] { "IPKE", HeartGold, "HeartGold" },
        };

        [Theory]
        [MemberData(nameof(Games))]
        public void TheQuickNameMatchesWhatTheFullReaderSays(string code, string path, string game)
        {
            if (!Directory.Exists(path)) { _out.WriteLine($"{game}: not unpacked here"); return; }
            new RomInfo(code, path);

            int models = 0, agreed = 0, quickSaidNothing = 0;
            var disagreed = new List<string>();
            var examples = new List<string>();

            foreach (var a in ModelAssets.All)
            {
                int n;
                try { n = ModelAssets.Count(a); } catch { n = 0; }
                for (int i = 0; i < n; i++)
                {
                    var full = ModelAssets.LoadModel(a, i);
                    if (full?.models == null || full.models.Length == 0) continue;
                    string want = full.models[0].Name;
                    if (string.IsNullOrEmpty(want)) continue;
                    models++;

                    string got = ModelAssets.NameOf(a, i);
                    if (got == null) { quickSaidNothing++; continue; }
                    if (got == want)
                    {
                        agreed++;
                        if (examples.Count < 6) examples.Add($"{a.Title}[{i}] = {got}");
                    }
                    else disagreed.Add($"{a.Title}[{i}]: quick said \"{got}\", full says \"{want}\"");
                }
            }

            _out.WriteLine($"{game}: {models} models read the long way, {agreed} names matched, "
                         + $"{quickSaidNothing} the quick reader would not name, {disagreed.Count} disagreed");
            foreach (var e in examples) _out.WriteLine("  " + e);
            foreach (var d in disagreed.Take(6)) _out.WriteLine("  MISMATCH " + d);

            Assert.True(models > 100, $"{game}: only {models} models had a name at all");
            Assert.Empty(disagreed);

            // A reader that names nothing would pass the check above by saying nothing, so it has to
            // actually name almost everything.
            Assert.True(agreed * 20 >= models * 19,
                $"{game}: the quick reader only named {agreed} of {models}, so it is mostly giving up");
        }

        /// <summary>
        /// The 3D archives keep one thing per file, so grouping within one has nothing to do.
        /// </summary>
        [Theory]
        [MemberData(nameof(Games))]
        public void The3DArchivesKeepOneThingPerFile(string code, string path, string game)
        {
            if (!Directory.Exists(path)) { _out.WriteLine($"{game}: not unpacked here"); return; }
            new RomInfo(code, path);

            int looked = 0;
            var folded = new List<string>();
            foreach (var a in ModelAssets.All)
            {
                int n;
                try { n = ModelAssets.Count(a); } catch { n = 0; }
                if (n == 0) continue;
                looked++;
                int rows = ModelAssets.Units(a, n).Count;
                _out.WriteLine($"{game} / {a.Title}: {n} files into {rows} rows");
                if (rows != n) folded.Add(a.Title);
            }

            // HeartGold's title screen is the one place in these three games where a model really is
            // followed by the pictures painted on it, and there the rule does fold: 44 files into 32.
            _out.WriteLine($"{game}: {folded.Count} archives folded ({string.Join(", ", folded)})");
            var expected = game == "HeartGold" ? new[] { "Title screen" } : Array.Empty<string>();
            Assert.Equal(expected, folded.ToArray());
            Assert.True(looked >= 4, $"{game}: only {looked} archives were read");
        }

        /// <summary>The names are worth having: they differ from each other and are not the archive title
        /// repeated. A reader returning one constant would satisfy every check above.</summary>
        [Fact]
        public void TheNamesAreRealAndDistinct()
        {
            if (!Directory.Exists(Platinum)) { _out.WriteLine("Platinum not unpacked here"); return; }
            new RomInfo("CPUE", Platinum);

            var a = ModelAssets.All.First(x => x.Title == "Buildings, outside");
            var names = new List<string>();
            for (int i = 0; i < Math.Min(200, ModelAssets.Count(a)); i++)
            {
                string n = ModelAssets.NameOf(a, i);
                if (n != null) names.Add(n);
            }

            _out.WriteLine($"{names.Count} names, {names.Distinct().Count()} of them different");
            _out.WriteLine("  " + string.Join(", ", names.Take(10)));
            Assert.True(names.Count > 50, $"only {names.Count} names came back");
            Assert.True(names.Distinct().Count() * 2 > names.Count,
                "over half the names are duplicates, so they are not telling the models apart");
            Assert.DoesNotContain(names, n => n == a.Title);
        }
    }
}
