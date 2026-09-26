using System.Linq;
using DSPRE.ROMFiles;
using Xunit;
using T = DSPRE.ROMFiles.HeadbuttRules.Table;

namespace DSPRE.Tests.Pokemon
{
    /// <summary>Expected values are pokeheartgold's sRareTreeLUT tables and EncounterSlot_WildMonSlotRoll_Headbutt.</summary>
    public class HeadbuttRulesTests
    {
        [Fact]
        public void SlotChancesAreTheGamesAndAddUpToAHundred()
        {
            Assert.Equal(new[] { 50, 15, 15, 10, 5, 5 }, HeadbuttRules.SlotChance);
            Assert.Equal(100, HeadbuttRules.SlotChance.Sum());
        }

        [Theory]
        [InlineData(0, 1, 0, T.Common)]
        [InlineData(0, 1, 1, T.Rare)]
        [InlineData(1, 2, 0, T.Rare)]
        [InlineData(2, 3, 0, T.None)]
        [InlineData(0, 3, 4, T.None)]
        [InlineData(3, 4, 0, T.Rare)]
        [InlineData(3, 4, 9, T.Rare)]
        [InlineData(0, 11, 0, T.None)]
        [InlineData(4, 11, 3, T.Common)]
        public void NormalTreesUseTheTableTheGamePicks(int tree, int count, int digit, T expected)
        {
            Assert.Equal(expected, HeadbuttRules.NormalTreeTable(tree, count, digit));
        }

        [Fact]
        public void FromFiveTreesThePatternRepeatsEveryFive()
        {
            for (int digit = 0; digit <= 9; digit++)
                for (int tree = 0; tree < 20; tree++)
                    Assert.Equal(HeadbuttRules.NormalTreeTable(tree % 5, 20, digit), HeadbuttRules.NormalTreeTable(tree, 20, digit));
        }

        [Fact]
        public void OutOfRangeTreesFindNothing()
        {
            Assert.Equal(T.None, HeadbuttRules.NormalTreeTable(0, 0, 0));
            Assert.Equal(T.None, HeadbuttRules.NormalTreeTable(3, 3, 0));
            Assert.Equal(T.None, HeadbuttRules.NormalTreeTable(0, 3, 10));
        }
    }
}
