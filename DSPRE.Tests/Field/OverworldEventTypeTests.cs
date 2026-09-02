using System.Linq;
using DSPRE;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// The event_type table mirrors fieldobj_code.h: HGSS defines 0x00-0x09, DP/Pt adds the fleeing trainer
    /// at 0x0a.
    /// </summary>
    public class OverworldEventTypeTests
    {
        [Fact]
        public void HgssDefinesTenTypes()
        {
            var types = OverworldEventTypes.For(RomInfo.GameFamilies.HGSS);
            Assert.Equal(10, types.Count);
            Assert.Equal(0, types.First().Value);
            Assert.Equal(9, types.Last().Value);
        }

        [Fact]
        public void DppTAddsTheFleeingTrainer()
        {
            var types = OverworldEventTypes.For(RomInfo.GameFamilies.Plat);
            Assert.Equal(11, types.Count);
            Assert.Equal(10, types.Last().Value);
            Assert.True(types.Last().IsTrainer);
        }

        [Theory]
        [InlineData(1)]   // Trainer
        [InlineData(2)]   // sees all directions
        [InlineData(4)]   // glances
        [InlineData(5)]   // spin in place, anticlockwise
        [InlineData(6)]   // spin in place, clockwise
        [InlineData(7)]   // spin moving, anticlockwise
        [InlineData(8)]   // spin moving, clockwise
        public void EveryTrainerVariantCountsAsATrainer(ushort value)
        {
            Assert.True(OverworldEventTypes.Find(RomInfo.GameFamilies.HGSS, value).IsTrainer);
        }

        [Theory]
        [InlineData(0)]   // Standard
        [InlineData(3)]   // Item
        [InlineData(9)]   // Message
        public void NonTrainerTypesAreNotTrainers(ushort value)
        {
            Assert.False(OverworldEventTypes.Find(RomInfo.GameFamilies.HGSS, value).IsTrainer);
        }

        [Fact]
        public void OnlyTheGlanceAndStationarySpinTypesReadParam1()
        {
            var withParam1 = OverworldEventTypes.For(RomInfo.GameFamilies.HGSS)
                .Where(t => !string.IsNullOrEmpty(t.Param1Label))
                .Select(t => t.Value)
                .ToArray();
            Assert.Equal(new ushort[] { 4, 5, 6 }, withParam1);
        }

        [Fact]
        public void UnknownValueIsNotInvented()
        {
            Assert.Null(OverworldEventTypes.Find(RomInfo.GameFamilies.HGSS, 200));
            Assert.Null(OverworldEventTypes.Find(RomInfo.GameFamilies.HGSS, 10));   // DP/Pt only
        }
    }
}
