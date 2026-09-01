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
    /// Every 2D graphic the browser lists can either be shown or says why not.
    ///
    /// The point of the browser is that nothing is silently blank. So this walks every entry of every
    /// archive in every game and requires each one to come back with either a picture or a sentence
    /// explaining itself. A blank with no reason is the failure.
    /// </summary>
    [Collection("rom")]
    public class GraphicAssetsTests
    {
        private readonly ITestOutputHelper _out;
        public GraphicAssetsTests(ITestOutputHelper o) { _out = o; }

        private const string Diamond =
            @"C:\Romhacking\ROMs\NDS\DP\Pokemon Diamond (v05) (U)(Legacy)\1015 - Pokemon Diamond (v05) (U)(Legacy)_DSPRE_contents";
        private const string Platinum =
            @"C:\Romhacking\ROMs\NDS\Plat\Pokemon - Platinum Version (USA) (Rev 1)\Pokemon - Platinum Version (USA) (Rev 1)_DSPRE_contents";
        private const string HeartGold =
            @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents";

        public static IEnumerable<object[]> Games => new[]
        {
            new object[] { "ADAE", Diamond,  "Diamond" },
            new object[] { "CPUE", Platinum, "Platinum" },
            new object[] { "IPKE", HeartGold, "HeartGold" },
        };

        [Theory]
        [MemberData(nameof(Games))]
        public void EveryEntryEitherDrawsOrSaysWhyNot(string code, string path, string game)
        {
            if (!Directory.Exists(path)) { _out.WriteLine($"{game}: not unpacked here"); return; }
            new RomInfo(code, path);
            GraphicAssets.Forget();   // the last game's palette positions do not apply to this one

            int archives = 0, looked = 0, drew = 0, explained = 0;
            var silent = new List<string>();
            var reasons = new Dictionary<string, int>(StringComparer.Ordinal);
            var perArchive = new List<string>();

            foreach (var a in GraphicAssets.All)
            {
                int count = GraphicAssets.Count(a);
                if (count == 0) continue;   // this game does not have it; the census says which
                archives++;

                // Every entry, not a sample. The thing being checked is that NO entry anywhere comes back
                // blank with nothing said, and a sample cannot show that: the one that fails is exactly the
                // one a sample skips. It is slow, and that is the price of the claim.
                const int step = 1;
                int aDrew = 0, aSaid = 0;
                for (int i = 0; i < count; i += step)
                {
                    var p = GraphicAssets.Render(a, i);
                    looked++;
                    if (p.Rgba != null && p.Width > 0 && p.Height > 0) { drew++; aDrew++; }
                    else if (!string.IsNullOrWhiteSpace(p.Whynot))
                    {
                        explained++; aSaid++;
                        string key = p.Whynot.Length > 70 ? p.Whynot.Substring(0, 70) : p.Whynot;
                        reasons[key] = reasons.TryGetValue(key, out int rn) ? rn + 1 : 1;
                    }
                    else silent.Add($"{a.Title}[{i}]");
                }
                perArchive.Add($"  {a.Title,-32} {count,6} entries, of {aDrew + aSaid} looked at: {aDrew} drawn, {aSaid} explained");
            }

            _out.WriteLine($"{game}: {archives} archives, {looked} entries looked at, {drew} drawn, {explained} explained");
            foreach (var l in perArchive) _out.WriteLine(l);
            _out.WriteLine("  why the rest were not drawn:");
            foreach (var r in reasons.OrderByDescending(r => r.Value))
                _out.WriteLine($"    {r.Value,4}  {r.Key}");

            Assert.True(looked > 2000, $"{game}: only {looked} entries were looked at, which is not the whole set");
            Assert.True(silent.Count == 0,
                $"{game}: {silent.Count} entries came back with no picture and no reason: "
                + string.Join(", ", silent.Take(15)));
        }
    }
}
