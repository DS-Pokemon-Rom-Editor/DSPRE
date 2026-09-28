using System.Linq;
using DSPRE;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// The overworld type values: 0-9 in every game, plus type 10 in Platinum only. Type 10 jumps when
    /// the player comes near and never battles, so it isn't a trainer.
    /// </summary>
    public class OverworldEventTypeTests
    {
        [Theory]
        [InlineData(RomInfo.GameFamilies.HGSS)]
        [InlineData(RomInfo.GameFamilies.DP)]
        public void DpAndHgssDefineTenTypes(RomInfo.GameFamilies family)
        {
            var types = OverworldEventTypes.For(family);
            Assert.Equal(Enumerable.Range(0, 10).Select(i => (ushort)i), types.Select(t => t.Value));
        }

        [Fact]
        public void PlatinumAddsTheJumperAsANonTrainer()
        {
            var types = OverworldEventTypes.For(RomInfo.GameFamilies.Plat);
            Assert.Equal(11, types.Count);
            var jumper = types.Last();
            Assert.Equal(10, jumper.Value);
            Assert.False(jumper.IsTrainer);
            Assert.False(string.IsNullOrEmpty(jumper.Param0Label));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        [InlineData(8)]
        public void EveryTrainerVariantCountsAsATrainer(ushort value)
        {
            Assert.True(OverworldEventTypes.Find(RomInfo.GameFamilies.HGSS, value).IsTrainer);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(3)]
        [InlineData(9)]
        public void NonTrainerTypesAreNotTrainers(ushort value)
        {
            Assert.False(OverworldEventTypes.Find(RomInfo.GameFamilies.HGSS, value).IsTrainer);
        }

        [Fact]
        public void OnlyTheLookingTypesReadParam1()
        {
            var withParam1 = OverworldEventTypes.For(RomInfo.GameFamilies.Plat)
                .Where(t => !string.IsNullOrEmpty(t.Param1Label))
                .Select(t => t.Value)
                .ToArray();
            Assert.Equal(new ushort[] { 4, 5, 6 }, withParam1);
        }

        [Fact]
        public void UnknownValueIsNotInvented()
        {
            Assert.Null(OverworldEventTypes.Find(RomInfo.GameFamilies.HGSS, 200));
            Assert.Null(OverworldEventTypes.Find(RomInfo.GameFamilies.HGSS, 10));
            Assert.Null(OverworldEventTypes.Find(RomInfo.GameFamilies.DP, 10));
            Assert.Null(OverworldEventTypes.Find(RomInfo.GameFamilies.Plat, 11));
        }
    }
}
