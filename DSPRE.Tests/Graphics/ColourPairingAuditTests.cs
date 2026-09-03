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
    /// Which archives are told where their colours are, and which are still guessing.
    ///
    /// Pairing a drawing with the nearest palette in the same archive is a guess, and it has been wrong
    /// before: the battle sprites were handing most Pokemon the previous Pokemon's shiny colours, and the
    /// battle gauges were being assembled out of the previous thing's tiles. Where the games carry a table
    /// saying which colours go with which drawing, that is read instead.
    ///
    /// This reports the split rather than asserting a target, because some archives genuinely have no such
    /// table and guessing is all there is. What it does assert is that the ones already settled stay
    /// settled.
    /// </summary>
    [Collection("rom")]
    public class ColourPairingAuditTests
    {
        private readonly ITestOutputHelper _out;
        public ColourPairingAuditTests(ITestOutputHelper o) { _out = o; }

        public static IEnumerable<object[]> Games => new List<object[]>
        {
            new object[] { "IPKE", TestRoms.HeartGold, "HeartGold" },
            new object[] { "CPUE", TestRoms.Platinum, "Platinum" },
        };

        [Theory]
        [MemberData(nameof(Games))]
        public void ReportWhichArchivesAreToldTheirColours(string code, string path, string game)
        {
            if (!Directory.Exists(path)) { _out.WriteLine($"{game}: not unpacked here"); return; }
            new RomInfo(code, path);
            GraphicAssets.Forget();

            var told = new List<string>();
            var guessing = new List<string>();
            var noColours = new List<string>();
            int looked = 0;

            foreach (var a in GraphicAssets.All)
            {
                int n;
                try { n = GraphicAssets.Count(a); } catch { continue; }
                if (n == 0) continue;
                looked++;

                if (a.Colours == GraphicAssets.Pairing.NotKnown) { noColours.Add(a.Title); continue; }

                // Does anything in this archive actually get a told answer?
                bool anyTold = false;
                if (a.ColourEntry != null)
                {
                    int step = n > 200 ? n / 200 : 1;
                    for (int i = 0; i < n && !anyTold; i += step)
                    {
                        try { anyTold = a.ColourEntry(i) >= 0; } catch { }
                    }
                }

                if (anyTold) told.Add(a.Title);
                else if (a.Colours == GraphicAssets.Pairing.NearestInSameArchive) guessing.Add(a.Title);
                else told.Add(a.Title + " (" + a.Colours + ")");
            }

            _out.WriteLine($"{game}: {looked} archives with files in them");
            _out.WriteLine($"   {told.Count} are told where their colours are:");
            foreach (var t in told) _out.WriteLine("      " + t);
            _out.WriteLine($"   {guessing.Count} still take the nearest palette:");
            foreach (var g in guessing) _out.WriteLine("      " + g);
            if (noColours.Count > 0)
                _out.WriteLine($"   {noColours.Count} hold no pictures to colour: "
                             + string.Join(", ", noColours));

            Assert.True(looked >= 12, $"{game}: only {looked} archives were read");

            // The ones already settled must stay settled. Each of these was a real fault once.
            foreach (string must in new[] { "Pokemon battle sprites", "Item icons", "Battle furniture",
                                            "Text box frames" })
                Assert.Contains(told, t => t.StartsWith(must, StringComparison.Ordinal));
        }
    }
}
