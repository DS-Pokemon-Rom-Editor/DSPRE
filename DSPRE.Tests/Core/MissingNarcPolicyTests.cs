using DSPRE;
using Xunit;

namespace DSPRE.Tests
{
    public class MissingNarcPolicyTests
    {
        [Theory]
        [InlineData(RomInfo.GameFamilies.DP, true)]
        [InlineData(RomInfo.GameFamilies.Plat, true)]
        [InlineData(RomInfo.GameFamilies.HGSS, false)]
        public void EggMoveNarcIsOptionalOnlyWhenRetailGamesUseAnOverlay(
            RomInfo.GameFamilies family,
            bool expected)
        {
            Assert.Equal(expected, DSUtils.IsOptionalNarc(RomInfo.DirNames.eggMoves, family));
            Assert.False(DSUtils.IsOptionalNarc(RomInfo.DirNames.scripts, family));
        }
    }
}
