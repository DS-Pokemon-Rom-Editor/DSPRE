using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.ROMFiles;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// Putting the gauge's letters in colour. The number font's pixels are not palette indices but
    /// three placeholders the game swaps for letter 0xe, shadow 2 and background 0xf as it loads them.
    /// Drawing the placeholders straight gives washed out digits that look plausible enough to ship.
    /// </summary>
    [Collection("rom")]
    public class BattleGaugeTextRenderTests
    {
        private readonly ITestOutputHelper _out;
        public BattleGaugeTextRenderTests(ITestOutputHelper o) => _out = o;

        private static bool Open(string code, string project)
        {
            if (!Directory.Exists(project)) return false;
            SettingsManager.Load();
            new RomInfo(code, project);
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames>
                { RomInfo.DirNames.fonts, RomInfo.DirNames.battleObj });
            BattleGaugeTextRenderer.Reset();
            return true;
        }

        /// <summary>Every colour actually used in a picture, so a flat or empty one is obvious.</summary>
        private static HashSet<(byte, byte, byte)> Colours(BattleGaugeTextRenderer.Drawn d)
        {
            var seen = new HashSet<(byte, byte, byte)>();
            for (int i = 0; i < d.Width * d.Height; i++)
                if (d.Rgba[i * 4 + 3] != 0) seen.Add((d.Rgba[i * 4], d.Rgba[i * 4 + 1], d.Rgba[i * 4 + 2]));
            return seen;
        }

        [Theory]
        [InlineData("CPUE", "Platinum")]
        [InlineData("IPKE", "HeartGold")]
        public void ADigitIsPaintedWithTheThreeColoursABattleAsksFor(string code, string name)
        {
            string project = name == "Platinum" ? TestRoms.Platinum : TestRoms.HeartGold;
            if (!Open(code, project)) { _out.WriteLine($"{name}: not unpacked here, skipped"); return; }
            Assert.True(BattleGaugeTextRenderer.IsAvailable, $"{name}: {BattleGaugeTextRenderer.Unavailable}");

            var hp = BattleGaugeTextRenderer.HealthNumbers(128, 255);
            Assert.NotNull(hp);
            Assert.Equal(8, hp.Height);
            Assert.Equal(7 * 8, hp.Width);           // 128 / 255 is three, a slash, three

            var used = Colours(hp);
            _out.WriteLine($"{name}: the HP numbers use {used.Count} colours: "
                         + string.Join("  ", used.Select(c => $"{c.Item1},{c.Item2},{c.Item3}")));

            // A digit is a light letter, a dark shadow and the panel behind it. Fewer than three
            // colours means the placeholders were drawn straight, which is the mistake this guards.
            Assert.True(used.Count >= 3,
                        $"{name}: a number came out in {used.Count} colours, so it was not recoloured");

            // The letter has to be far brighter than the shadow, or the digits vanish into the panel.
            int Brightness((byte r, byte g, byte b) c) => (c.r * 299 + c.g * 587 + c.b * 114) / 1000;
            int lightest = used.Max(Brightness), darkest = used.Min(Brightness);
            _out.WriteLine($"{name}: brightest {lightest}, darkest {darkest}");
            Assert.True(lightest - darkest > 100,
                        $"{name}: the digits have no contrast, brightest {lightest} against darkest {darkest}");

            // Every pixel is painted: writing a number into the gauge covers what was there.
            for (int i = 0; i < hp.Width * hp.Height; i++)
                Assert.Equal(255, hp.Rgba[i * 4 + 3]);
        }

        /// <summary>
        /// The level, with the gender symbol and "Lv" beside it. The symbol is the gauge's own picture,
        /// so it keeps the gauge's colours, and the two genders have to come out looking different.
        /// </summary>
        [Theory]
        [InlineData("CPUE", "Platinum")]
        [InlineData("IPKE", "HeartGold")]
        public void TheTwoGendersComeOutInDifferentColours(string code, string name)
        {
            string project = name == "Platinum" ? TestRoms.Platinum : TestRoms.HeartGold;
            if (!Open(code, project)) { _out.WriteLine($"{name}: not unpacked here, skipped"); return; }
            if (!BattleGaugeTextRenderer.IsAvailable) { Assert.Fail(BattleGaugeTextRenderer.Unavailable); }

            var female = BattleGaugeTextRenderer.LevelWithGender(5, BattleGaugeText.Gender.Female);
            var male = BattleGaugeTextRenderer.LevelWithGender(5, BattleGaugeText.Gender.Male);
            Assert.NotNull(female);
            Assert.NotNull(male);
            Assert.Equal(16 + 8, female.Width);      // the block, then one digit
            Assert.Equal(16, female.Height);

            // Only the left half carries the symbol, so that is what to compare.
            static HashSet<(byte, byte, byte)> LeftHalf(BattleGaugeTextRenderer.Drawn d)
            {
                var seen = new HashSet<(byte, byte, byte)>();
                for (int y = 0; y < d.Height; y++)
                    for (int x = 0; x < 8; x++)
                    {
                        int at = (y * d.Width + x) * 4;
                        if (d.Rgba[at + 3] != 0) seen.Add((d.Rgba[at], d.Rgba[at + 1], d.Rgba[at + 2]));
                    }
                return seen;
            }

            var f = LeftHalf(female);
            var m = LeftHalf(male);
            _out.WriteLine($"{name}: female symbol colours {f.Count}, male {m.Count}, "
                         + $"shared {f.Intersect(m).Count()}");
            Assert.False(f.SetEquals(m), $"{name}: both genders came out in exactly the same colours");
        }

        /// <summary>
        /// The five status words and the blank, each its own picture. They are the gauge's own tiles, so
        /// they keep the gauge's colours, and the games give each one a different one.
        /// </summary>
        [Theory]
        [InlineData("CPUE", "Platinum")]
        [InlineData("IPKE", "HeartGold")]
        public void EachStatusWordComesOutInItsOwnColour(string code, string name)
        {
            string project = name == "Platinum" ? TestRoms.Platinum : TestRoms.HeartGold;
            if (!Open(code, project)) { _out.WriteLine($"{name}: not unpacked here, skipped"); return; }
            if (!BattleGaugeTextRenderer.IsAvailable) { Assert.Fail(BattleGaugeTextRenderer.Unavailable); }

            var words = new[]
            {
                BattleGaugeText.Status.Paralysis, BattleGaugeText.Status.Freeze,
                BattleGaugeText.Status.Sleep, BattleGaugeText.Status.Poison,
                BattleGaugeText.Status.Burn,
            };

            var brightest = new List<(BattleGaugeText.Status s, (byte r, byte g, byte b) c)>();
            foreach (var status in words)
            {
                var drawn = BattleGaugeTextRenderer.StatusWord(status);
                Assert.NotNull(drawn);
                Assert.Equal(BattleGaugeText.StatusTiles * 8, drawn.Width);

                var used = Colours(drawn);
                Assert.True(used.Count >= 2, $"{name}: {status} came out flat");

                // The badge colour is the most common one that is not the near-black outline.
                var counts = new Dictionary<(byte, byte, byte), int>();
                for (int i = 0; i < drawn.Width * drawn.Height; i++)
                {
                    if (drawn.Rgba[i * 4 + 3] == 0) continue;
                    var key = (drawn.Rgba[i * 4], drawn.Rgba[i * 4 + 1], drawn.Rgba[i * 4 + 2]);
                    counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
                }
                var most = counts.OrderByDescending(kv => kv.Value).First().Key;
                brightest.Add((status, most));
                _out.WriteLine($"{name}: {status} is mostly {most.Item1},{most.Item2},{most.Item3}");
            }

            // Five conditions, and the games do not paint them all the same.
            Assert.True(brightest.Select(b => b.c).Distinct().Count() >= 4,
                        $"{name}: the status words are not being told apart by colour");
        }
    }
}
