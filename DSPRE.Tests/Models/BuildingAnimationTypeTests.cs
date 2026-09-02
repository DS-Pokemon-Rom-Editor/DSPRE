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
    /// What a building animation's type byte means, in each family, traced rather than assumed.
    /// </summary>
    public class BuildingAnimationTypeTests
    {
        private readonly ITestOutputHelper _out;
        public BuildingAnimationTypeTests(ITestOutputHelper o) { _out = o; }

        private static BuildingAnimationInfo Make(byte type, bool shortLayout)
        {
            // The record as the games write it: flag, type, suicide, then a byte of padding, then the
            // four animation codes. The longer one carries a few more fields after the flag block.
            int size = shortLayout ? BuildingAnimationInfo.ShortSize : BuildingAnimationInfo.Size;
            var bytes = new byte[size];
            bytes[0] = 1;        // it animates
            bytes[1] = type;
            int codesAt = shortLayout ? 4 : 8;
            for (int i = 0; i < 4; i++)
            {
                uint code = i == 0 ? 1u : BuildingAnimationInfo.NoAnimation;
                for (int b = 0; b < 4; b++) bytes[codesAt + i * 4 + b] = (byte)(code >> (8 * b));
            }
            return new BuildingAnimationInfo(bytes);
        }

        [Fact]
        public void WaitingToBeSetOffIsReadInBothFamilies()
        {
            foreach (bool shortLayout in new[] { true, false })
            {
                string family = shortLayout ? "Diamond, Pearl and Platinum" : "HeartGold and SoulSilver";

                Assert.True(Make(0x01, shortLayout).IsConditional, $"{family}: type 1 should wait to be set off");
                Assert.True(Make(0x03, shortLayout).IsConditional, $"{family}: type 3 should wait to be set off");
                Assert.False(Make(0x00, shortLayout).IsConditional, $"{family}: type 0 should run by itself");
                Assert.False(Make(0x02, shortLayout).IsConditional, $"{family}: type 2 should run by itself");
                _out.WriteLine($"{family}: the bottom bit means it waits to be set off");
            }
        }

        [Fact]
        public void NeedingToBePutOnTheMapIsReadInBothFamilies()
        {
            foreach (bool shortLayout in new[] { true, false })
            {
                Assert.True(Make(0x02, shortLayout).NeedsSetting);
                Assert.True(Make(0x03, shortLayout).NeedsSetting);
                Assert.False(Make(0x00, shortLayout).NeedsSetting);
                Assert.False(Make(0x01, shortLayout).NeedsSetting);
            }
            _out.WriteLine("the second bit means something has to put it on the map, in both families");
        }

        /// <summary>
        /// Type 8 is the time-of-day animation in HeartGold and nothing in particular in Platinum, so the
        /// two families must disagree about it.
        /// </summary>
        [Fact]
        public void TimeOfDayIsHeartGoldOnlyAndTheFamiliesDisagreeAboutTypeEight()
        {
            var johto = Make(BuildingAnimationInfo.TypeTimeOfDay, shortLayout: false);
            var sinnoh = Make(BuildingAnimationInfo.TypeTimeOfDay, shortLayout: true);

            Assert.True(johto.IsTimeOfDay, "HeartGold's type 8 is the time-of-day animation");
            Assert.False(sinnoh.IsTimeOfDay, "Platinum has no time-of-day animation type");

            // HeartGold takes the time-of-day type out of the waiting-to-be-set-off test and counts it as
            // needing to be put on the map. Platinum reads plain bits, so 8 is neither.
            Assert.False(johto.IsConditional, "HeartGold's time-of-day type does not wait to be set off");
            Assert.True(johto.NeedsSetting, "HeartGold's time-of-day type has to be put on the map");
            Assert.False(sinnoh.IsConditional);
            Assert.False(sinnoh.NeedsSetting);

            _out.WriteLine("type 8: HeartGold reads it as time of day, Platinum reads it as plain bits");
        }

        /// <summary>The shorter record has no Door field, so nothing may claim to have read one.</summary>
        [Fact]
        public void TheShorterRecordNeverClaimsToKnowAboutDoors()
        {
            Assert.False(Make(0x01, shortLayout: true).IsDoor);
            Assert.False(Make(0x08, shortLayout: true).IsDoor);
            _out.WriteLine("Diamond, Pearl and Platinum records never report a door, because they hold none");
        }

        /// <summary>A model marked as not animating is never read as anything.</summary>
        [Fact]
        public void AModelWithNoAnimationIsNeverConditional()
        {
            foreach (bool shortLayout in new[] { true, false })
            {
                var none = Make(0xFF, shortLayout);
                Assert.False(none.IsConditional);
                Assert.False(none.NeedsSetting);
                Assert.False(none.IsTimeOfDay);
            }
            _out.WriteLine("a type of 0xFF, which is what the generator writes for no animation, reads as nothing");
        }
    }
}
