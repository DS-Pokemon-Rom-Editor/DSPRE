using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using DSPRE;
using DSPRE.ROMFiles;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// The gauge against a frame captured from a real Platinum battle. Two things about that frame:
    /// it has to come from an unpatched ROM, since an earlier one was captured from a ROM we had
    /// painted a block into, and it has to catch your own bar at rest, since it drifts a pixel up and
    /// down the whole time it is animating.
    /// </summary>
    [Collection("rom")]
    public class BattleGaugeAgainstTheGameTests
    {
        private readonly ITestOutputHelper _out;
        public BattleGaugeAgainstTheGameTests(ITestOutputHelper o) => _out = o;

        /// <summary>Where the enemy's level sits on the captured frame.</summary>
        private const int LevelAtX = 66, LevelAtY = 24;

        /// <summary>And where the name sits on the same frame.</summary>
        private const int NameAtX = 2, NameAtY = 24;

        private static string FramePath =>
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "platinum_battle_frame.png");

        /// <summary>
        /// The DS mixes its colours in five bits a channel and the capture is eight, so the same colour
        /// can land a couple of steps out. Anything further apart is a different colour.
        /// </summary>
        private static bool Same(Color a, byte r, byte g, byte b) =>
            Math.Abs(a.R - r) <= 6 && Math.Abs(a.G - g) <= 6 && Math.Abs(a.B - b) <= 6;

        /// <summary>
        /// The two bars, built the way the editor builds them, against the captured frame.
        ///
        /// A bar with nothing written on it is scored as well. Without that, a check like this passes
        /// on the bar's picture alone and says nothing about the writing, which is the part being made.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ABarBuiltHereMatchesTheOneInARealBattle(bool player)
        {
            if (!Directory.Exists(TestRoms.Platinum))
            { _out.WriteLine("Platinum not unpacked here, skipped"); return; }
            Assert.True(File.Exists(FramePath), "the captured battle frame did not come with the tests");

            SettingsManager.Load();
            new RomInfo("CPUE", TestRoms.Platinum);
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames>
                { RomInfo.DirNames.fonts, RomInfo.DirNames.battleObj });
            BattleGaugeText.Reset();
            Assert.True(BattleGaugeText.IsAvailable, BattleGaugeText.Unavailable);

            // The frame shows your own female TURTWIG on 21 of 21 against a male CHIMCHAR, both level 5.
            var real = player
                ? new BattleGaugeComposer.Showing
                  { Name = "TURTWIG", Level = 5, Gender = BattleGaugeText.Gender.Female,
                    Health = 21, MostHealth = 21, ShowHealthNumbers = true }
                : new BattleGaugeComposer.Showing
                  { Name = "CHIMCHAR", Level = 5, Gender = BattleGaugeText.Gender.Male };
            var nothing = new BattleGaugeComposer.Showing
                  { Name = "", Level = 0, ShowHealthNumbers = player, Health = 0, MostHealth = 0 };

            var kind = player ? BattleGaugeComposer.Kind.PlayerSingle
                              : BattleGaugeComposer.Kind.OpponentSingle;
            using var frame = new Bitmap(FramePath);

            double written = Matching(BattleGaugeComposer.Build(kind, real), frame, out int drawn);
            double blank = Matching(BattleGaugeComposer.Build(kind, nothing), frame, out _);

            _out.WriteLine($"{(player ? "your" : "their")} bar: {written:F1}% of {drawn} pixels match the "
                         + $"real battle, against {blank:F1}% with nothing written on it");

            Assert.True(drawn > 3000, $"only {drawn} pixels were drawn, so this proved little");

            // What is left over is the green health fill and the arrow, both of which the game draws
            // on top of the bar rather than into it.
            Assert.True(written > 92, $"only {written:F1}% of the bar matches the real battle");
            Assert.True(written - blank > 5,
                        $"a bar with nothing written on it scores {blank:F1}% against {written:F1}%, "
                       + "so this check cannot tell whether the writing is right");
        }

        /// <summary>How much of a built bar matches the frame, ignoring what it does not paint.</summary>
        private static double Matching(BattleGaugeComposer.Drawn g, Bitmap frame, out int drawn)
        {
            Assert.NotNull(g);
            int hits = 0;
            drawn = 0;
            for (int y = 0; y < g.Height; y++)
                for (int x = 0; x < g.Width; x++)
                {
                    int sx = g.Left + x, sy = g.Top + y;
                    if (sx < 0 || sy < 0 || sx >= 256 || sy >= 192) continue;
                    int at = (y * g.Width + x) * 4;
                    if (g.Rgba[at + 3] == 0) continue;
                    drawn++;
                    if (Same(frame.GetPixel(sx, sy), g.Rgba[at], g.Rgba[at + 1], g.Rgba[at + 2])) hits++;
                }
            return 100.0 * hits / Math.Max(1, drawn);
        }

        /// <summary>
        /// The ten digits are the ten numerals, not ten slices of a sheet read at the wrong offset. Each
        /// has to differ from every other, which a misread does not manage: reading them a tile and a
        /// half early gave ten pictures that were each half of two numerals, and several came out alike.
        /// </summary>
        [Fact]
        public void TheTenDigitsAreTenDifferentPictures()
        {
            if (!Directory.Exists(TestRoms.Platinum))
            { _out.WriteLine("Platinum not unpacked here, skipped"); return; }

            SettingsManager.Load();
            new RomInfo("CPUE", TestRoms.Platinum);
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.fonts });
            BattleGaugeText.Reset();
            Assert.True(BattleGaugeText.IsAvailable, BattleGaugeText.Unavailable);

            var seen = new List<byte[]>();
            for (int d = 0; d < 10; d++)
            {
                var tile = BattleGaugeText.Digit(d);
                Assert.NotNull(tile);

                // A numeral leaves its top row clear and has ink below it.
                for (int x = 0; x < 8; x++)
                    Assert.Equal(0, tile.At(x, 0));

                int ink = 0;
                foreach (byte p in tile.Pixels) if (p != 0) ink++;
                Assert.True(ink > 20, $"digit {d} has only {ink} pixels of ink");

                // Only the three placeholders may appear: anything else is header or another picture.
                foreach (byte p in tile.Pixels)
                    Assert.True(p <= 2, $"digit {d} holds value {p}, so it is not the number font's picture");

                seen.Add((byte[])tile.Pixels.Clone());
            }

            for (int a = 0; a < seen.Count; a++)
                for (int b = a + 1; b < seen.Count; b++)
                    Assert.False(System.Linq.Enumerable.SequenceEqual(seen[a], seen[b]),
                                 $"digits {a} and {b} came back as the same picture");
            _out.WriteLine("all ten digits are distinct, and each holds only the three placeholders");
        }
    }
}
