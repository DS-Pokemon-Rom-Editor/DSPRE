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
    /// The quick name reader agrees with the full one, on every model in every game.
    ///
    /// Listing a thousand rows all reading "Buildings, outside" throws away the names GameFreak put in
    /// the files, but opening every model to fetch a name makes the list crawl. So only the name table is
    /// read, by arithmetic lifted from the full reader. Arithmetic lifted from somewhere is exactly the
    /// sort of thing that is right for the first hundred entries and wrong for the rest, so it is checked
    /// against the full reader for all of them rather than a sample.
    /// </summary>
    [Collection("rom")]
    public class ModelNameTests
    {
        private readonly ITestOutputHelper _out;
        public ModelNameTests(ITestOutputHelper o) { _out = o; }

        private const string Diamond =
            @"C:\Romhacking\ROMs\NDS\DP\Pokemon Diamond (v05) (U)(Legacy)\1015 - Pokemon Diamond (v05) (U)(Legacy)_DSPRE_contents";
        private const string Platinum =
            @"C:\Romhacking\ROMs\NDS\Plat\Pokemon - Platinum Version (USA) (Rev 1)\Pokemon - Platinum Version (USA) (Rev 1)_DSPRE_contents";
        private const string HeartGold = @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents";

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
        ///
        /// This was assumed to work the other way: that a model would be followed by its pictures and its
        /// movement, the way a Pokemon's six files sit together. Measuring says otherwise. The archives
        /// are filed by kind, in blocks: the buildings archive is 590 models and nothing else, the
        /// overworld one is 24 models and 421 sets of pictures because overworld people are flat boards
        /// wearing a texture. So the linking that matters is across archives, which is what the Pictures
        /// and Movement pickers do.
        ///
        /// Recorded as a check rather than a note so that if a game turns up filed the other way, this
        /// says so instead of the grouping quietly doing nothing.
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
            // Everything else keeps one thing per file. Named rather than allowed generally, so a fourth
            // game filed differently shows up here instead of passing quietly.
            _out.WriteLine($"{game}: {folded.Count} archives folded ({string.Join(", ", folded)})");
            var expected = game == "HeartGold" ? new[] { "Title screen" } : Array.Empty<string>();
            Assert.Equal(expected, folded.ToArray());
            Assert.True(looked >= 4, $"{game}: only {looked} archives were read");
        }

        /// <summary>What each 3D archive is actually made of, in order, kept for when the filing is
        /// questioned again.</summary>
        [Fact]
        public void ReportHowThe3DArchivesAreFiled()
        {
            if (!Directory.Exists(Platinum)) { _out.WriteLine("Platinum not unpacked here"); return; }
            new RomInfo("CPUE", Platinum);

            foreach (var a in ModelAssets.All)
            {
                int n;
                try { n = ModelAssets.Count(a); } catch { n = 0; }
                if (n == 0) continue;

                var narc = new ScriptNarc(a.Dir);
                var kinds = new List<ModelAssets.Kind>();
                for (int i = 0; i < n; i++) kinds.Add(ModelAssets.Identify(narc.Get(i)));

                var tally = kinds.GroupBy(k => k).OrderByDescending(g => g.Count())
                                 .Select(g => $"{g.Count()} {g.Key}");
                int units = ModelAssets.Units(a, n).Count;
                _out.WriteLine($"{a.Title}: {n} files into {units} rows [{string.Join(", ", tally)}]");
                _out.WriteLine("   first 24 in order: " + string.Join(", ", kinds.Take(24)));
            }
        }


        /// <summary>How long the real texture reader takes over a whole archive, and what it says.
        ///
        /// A hand written scanner found names for some texture sets and binary rubbish for others, so it
        /// is not shippable. The proper reader is already here; the only question is whether it is quick
        /// enough to name a list with.</summary>
        [Fact]
        public void TimeTheRealTextureNameReader()
        {
            if (!Directory.Exists(Platinum)) { _out.WriteLine("Platinum not unpacked here"); return; }
            new RomInfo("CPUE", Platinum);

            foreach (var a in ModelAssets.All)
            {
                int n;
                try { n = ModelAssets.Count(a); } catch { n = 0; }
                if (n == 0) continue;

                var clock = System.Diagnostics.Stopwatch.StartNew();
                int named = 0, tried = 0;
                var examples = new List<string>();
                for (int i = 0; i < n; i++)
                {
                    string got = ModelAssets.NameOf(a, i);
                    tried++;
                    if (got != null)
                    {
                        named++;
                        if (examples.Count < 5) examples.Add($"{i}={got}");
                    }
                }
                clock.Stop();
                _out.WriteLine($"{a.Title}: {named} of {tried} named in {clock.ElapsedMilliseconds} ms"
                             + (examples.Count > 0 ? "  [" + string.Join(", ", examples) + "]" : ""));
            }
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
