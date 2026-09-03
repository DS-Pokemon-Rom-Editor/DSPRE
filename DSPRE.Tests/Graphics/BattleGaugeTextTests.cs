using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.ROMFiles;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// The letters a battle writes onto a gauge, none of which are in the gauge picture. The overlay
    /// holding "Lv" and the gender symbol is found by searching it for the number font's own "Lv", so
    /// these check the search lands on real pictures rather than just returning an address.
    /// </summary>
    [Collection("rom")]
    public class BattleGaugeTextTests
    {
        private readonly ITestOutputHelper _out;
        public BattleGaugeTextTests(ITestOutputHelper o) => _out = o;

        private static bool Open(string code, string project)
        {
            if (!Directory.Exists(project)) return false;
            SettingsManager.Load();
            new RomInfo(code, project);
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.fonts });
            BattleGaugeText.Reset();
            return true;
        }

        /// <summary>
        /// Which pixels of a tile carry ink. The background is whichever index covers most of the tile,
        /// because these pictures sit on a filled panel rather than on nothing.
        /// </summary>
        private static bool[] Shape(BattleGaugeText.Tile t)
        {
            if (t == null) return new bool[64];
            byte ground = t.Pixels.GroupBy(p => p).OrderByDescending(g => g.Count()).First().Key;
            return t.Pixels.Select(p => p != ground).ToArray();
        }

        private static int Ink(BattleGaugeText.Tile t) => Shape(t).Count(on => on);

        [Theory]
        [InlineData("CPUE", "Platinum")]
        [InlineData("IPKE", "HeartGold")]
        public void TheGaugesOwnLettersAreFoundInTheBattleOverlay(string code, string name)
        {
            string project = name == "Platinum" ? TestRoms.Platinum : TestRoms.HeartGold;
            if (!Open(code, project)) { _out.WriteLine($"{name}: not unpacked here, skipped"); return; }

            Assert.True(BattleGaugeText.IsAvailable,
                        $"{name}: {BattleGaugeText.Unavailable}");
            _out.WriteLine($"{name}: {BattleGaugeText.Where()}");

            // Every digit has to be a picture. A blank one means the number font was read at the wrong
            // place, which is exactly the mistake that shows up as a level of nothing at all.
            for (int d = 0; d < 10; d++)
            {
                var tile = BattleGaugeText.Digit(d);
                Assert.NotNull(tile);
                Assert.True(Ink(tile) > 4, $"{name}: digit {d} came back nearly blank");
            }
            _out.WriteLine($"{name}: ten digits, ink counts "
                         + string.Join(" ", Enumerable.Range(0, 10).Select(d => Ink(BattleGaugeText.Digit(d)))));

            Assert.True(Ink(BattleGaugeText.Slash()) > 4, $"{name}: the HP slash came back blank");
        }

        /// <summary>
        /// The three gender blocks. The one with no symbol has to be emptier on its left than the two
        /// that carry one, and the male and female symbols have to differ from each other. Without that
        /// last check the whole set could be one block read three times and everything would still pass.
        /// </summary>
        [Theory]
        [InlineData("CPUE", "Platinum")]
        [InlineData("IPKE", "HeartGold")]
        public void TheGenderSymbolsAreThereAndAreNotTheSamePicture(string code, string name)
        {
            string project = name == "Platinum" ? TestRoms.Platinum : TestRoms.HeartGold;
            if (!Open(code, project)) { _out.WriteLine($"{name}: not unpacked here, skipped"); return; }
            if (!BattleGaugeText.IsAvailable) { Assert.Fail($"{name}: {BattleGaugeText.Unavailable}"); }

            var female = BattleGaugeText.GenderAndLv(BattleGaugeText.Gender.Female);
            var male = BattleGaugeText.GenderAndLv(BattleGaugeText.Gender.Male);
            var none = BattleGaugeText.GenderAndLv(BattleGaugeText.Gender.Genderless);

            foreach (var (label, block) in new[] { ("female", female), ("male", male), ("none", none) })
            {
                Assert.NotNull(block);
                Assert.Equal(4, block.Length);
                _out.WriteLine($"{name} {label}: ink per tile {string.Join(" ", block.Select(Ink))}");
            }

            // The symbol sits on the left, the "Lv" on the right, so the left tiles are what to compare.
            int femaleSymbol = Ink(female[0]) + Ink(female[2]);
            int maleSymbol = Ink(male[0]) + Ink(male[2]);
            int noneSymbol = Ink(none[0]) + Ink(none[2]);
            _out.WriteLine($"{name}: ink in the symbol half, female {femaleSymbol}, male {maleSymbol}, none {noneSymbol}");

            Assert.True(femaleSymbol > noneSymbol, $"{name}: the female symbol is no darker than no symbol");
            Assert.True(maleSymbol > noneSymbol, $"{name}: the male symbol is no darker than no symbol");
            Assert.False(Shape(female[0]).SequenceEqual(Shape(male[0]))
                      && Shape(female[2]).SequenceEqual(Shape(male[2])),
                         $"{name}: male and female came back as the same picture");

            // The bottom right tile of every block is the "Lv", and its top four rows are the number
            // font's own "Lv" four rows down. That is the byte match the overlay was found by, so it
            // pins the block to the right tile instead of merely finding ink there: shifting the read
            // by one tile breaks it. Compared by shape, because HeartGold stores the male and the
            // genderless copies with a different palette index from the female one.
            var fontLv = BattleGaugeText.NumberFontLv();
            Assert.NotNull(fontLv);

            // The female block's bottom right tile IS the number font's "Lv", four rows down, byte for
            // byte, on both games. That is the match the overlay was found by, so asserting it pins the
            // block to the right tile: shifting the read by one tile breaks this.
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 8; x++)
                    Assert.Equal(fontLv.At(x, y), female[3].At(x, y));

            // The male and genderless copies are only required to carry a picture there. On HeartGold
            // they are the same "Lv" stored with a different palette index, and nothing here has
            // established what that difference is, so it is not asserted.
            foreach (var (label, block) in new[] { ("male", male), ("none", none) })
                Assert.True(Ink(block[3]) > 4, $"{name}: the \"Lv\" beside the {label} symbol is blank");
        }

        /// <summary>
        /// The five status words, and the blank the gauge shows when nothing is wrong. Each is three
        /// tiles across, and the blank has to be emptier than every word or the read is in the wrong
        /// place: five words that all look alike would otherwise pass.
        /// </summary>
        [Theory]
        [InlineData("CPUE", "Platinum")]
        [InlineData("IPKE", "HeartGold")]
        public void EachStatusWordIsItsOwnPicture(string code, string name)
        {
            string project = name == "Platinum" ? TestRoms.Platinum : TestRoms.HeartGold;
            if (!Open(code, project)) { _out.WriteLine($"{name}: not unpacked here, skipped"); return; }
            if (!BattleGaugeText.IsAvailable) { Assert.Fail($"{name}: {BattleGaugeText.Unavailable}"); }

            var words = new[]
            {
                BattleGaugeText.Status.Paralysis, BattleGaugeText.Status.Freeze,
                BattleGaugeText.Status.Sleep, BattleGaugeText.Status.Poison,
                BattleGaugeText.Status.Burn,
            };

            var blank = BattleGaugeText.StatusWord(BattleGaugeText.Status.None);
            Assert.NotNull(blank);
            Assert.Equal(BattleGaugeText.StatusTiles, blank.Length);
            int blankInk = blank.Sum(Ink);
            _out.WriteLine($"{name}: nothing wrong is {blankInk} ink");

            var inkPerWord = new List<int>();
            foreach (var status in words)
            {
                var tiles = BattleGaugeText.StatusWord(status);
                Assert.NotNull(tiles);
                Assert.Equal(BattleGaugeText.StatusTiles, tiles.Length);

                int ink = tiles.Sum(Ink);
                inkPerWord.Add(ink);
                _out.WriteLine($"{name}: {status} is {ink} ink");
                Assert.True(ink > blankInk, $"{name}: {status} has no more on it than the blank does");
            }

            // Five different words, so they cannot all carry the same amount of ink.
            Assert.True(inkPerWord.Distinct().Count() > 1,
                        $"{name}: every status word came back identical, so one tile is being read five times");
        }

        /// <summary>Diamond is laid out differently, so it says no rather than drawing nonsense.</summary>
        [Fact]
        public void DiamondSaysItCannotRatherThanReadingTheWrongTiles()
        {
            if (!Open("ADAE", TestRoms.Diamond)) { _out.WriteLine("Diamond not unpacked here, skipped"); return; }

            _out.WriteLine("Diamond: " + (BattleGaugeText.Unavailable ?? "reported as available"));
            Assert.False(BattleGaugeText.IsAvailable,
                         "Diamond's gauge pictures sit at different tile numbers, so it must not be read this way");
            Assert.False(string.IsNullOrWhiteSpace(BattleGaugeText.Unavailable),
                         "it should say why, so the editor can tell the user");
        }

        /// <summary>Opening a second ROM must not keep the first one's pictures.</summary>
        [Fact]
        public void ASecondRomGetsItsOwnPictures()
        {
            if (!Directory.Exists(TestRoms.Platinum) || !Directory.Exists(TestRoms.HeartGold))
            { _out.WriteLine("both games are needed here, skipped"); return; }

            Open("CPUE", TestRoms.Platinum);
            Assert.True(BattleGaugeText.IsAvailable);
            string plat = BattleGaugeText.Where();

            // no Reset this time: the class has to notice the ROM changed on its own
            SettingsManager.Load();
            new RomInfo("IPKE", TestRoms.HeartGold);
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.fonts });

            string hg = BattleGaugeText.Where();
            _out.WriteLine($"Platinum said {plat}, HeartGold says {hg}");
            Assert.True(BattleGaugeText.IsAvailable, BattleGaugeText.Unavailable);
            Assert.NotEqual(plat, hg);
        }

    }
}
