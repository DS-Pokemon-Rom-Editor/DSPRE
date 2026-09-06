using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    public class TrainerRosterCapacityTests
    {
        [Fact]
        public void PlatinumEnglishUsesTheLowerScriptRangeCapacity()
        {
            Assert.True(TrainerRosterCapacity.TryFor(RomInfo.GameVersions.Platinum,
                RomInfo.GameLanguages.English, out TrainerRosterCapacity capacity,
                out string error), error);

            Assert.Equal(71, capacity.RemainingAdditions(928, 928));
            Assert.True(capacity.CanAdd(928, 928, out error), error);
            Assert.True(capacity.CanAdd(998, 998, out error), error);
            Assert.Equal(1, capacity.RemainingAdditions(998, 998));
            Assert.False(capacity.CanAdd(999, 999, out error));
            Assert.Contains("script range", error);
        }

        [Fact]
        public void CapacityRejectsAStaticOrOtherwiseDivergedBoundary()
        {
            Assert.True(TrainerRosterCapacity.TryFor(RomInfo.GameVersions.Platinum,
                RomInfo.GameLanguages.English, out TrainerRosterCapacity capacity,
                out string error), error);

            Assert.False(capacity.CanAdd(929, 928, out error));
            Assert.Contains("diverged", error);
            Assert.Equal(0, capacity.RemainingAdditions(929, 928));
        }

        [Fact]
        public void HeartGoldEnglishIsLimitedToTwoAuditedAdditions()
        {
            Assert.True(TrainerRosterCapacity.TryFor(RomInfo.GameVersions.HeartGold,
                RomInfo.GameLanguages.English, out TrainerRosterCapacity capacity,
                out string error), error);

            Assert.Equal(2, capacity.RemainingAdditions(738, 739));
            Assert.True(capacity.CanAdd(738, 739, out error), error);
            Assert.True(capacity.CanAdd(739, 740, out error), error);
            Assert.False(capacity.CanAdd(740, 741, out error));
            Assert.Contains("at most two", error);
        }

        [Fact]
        public void UnverifiedHeartGoldLanguageRemainsDisabled()
        {
            Assert.False(TrainerRosterCapacity.TryFor(RomInfo.GameVersions.HeartGold,
                RomInfo.GameLanguages.French, out _, out string error));
            Assert.Contains("not verified", error);
        }
    }
}
