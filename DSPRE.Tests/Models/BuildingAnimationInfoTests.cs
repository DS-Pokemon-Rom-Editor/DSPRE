using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// Records taken verbatim from HeartGold's outdoor building-animation list, so the layout is pinned
    /// against real data rather than only against the struct definition.
    /// </summary>
    public class BuildingAnimationInfoTests
    {
        // file 0: no animation at all
        private static readonly byte[] Silent =
        {
            0xff, 0xff, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
            0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
        };

        // file 1: animates, one animation, code 3
        private static readonly byte[] OneAnimation =
        {
            0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x01,
            0x03, 0x00, 0x00, 0x00, 0xff, 0xff, 0xff, 0xff,
            0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
        };

        [Fact]
        public void RecordIsTwentyFourBytes()
        {
            Assert.Equal(24, BuildingAnimationInfo.Size);
            Assert.Equal(BuildingAnimationInfo.Size, Silent.Length);
        }

        [Fact]
        public void ModelWithoutAnimationReportsNone()
        {
            var info = new BuildingAnimationInfo(Silent);
            Assert.False(info.Animates);
            Assert.Empty(info.UsedCodes);
        }

        [Fact]
        public void SingleAnimationIsReadFromTheFirstSlot()
        {
            var info = new BuildingAnimationInfo(OneAnimation);
            Assert.True(info.Animates);
            Assert.Equal(new[] { 3 }, info.UsedCodes.ToArray());
        }

        [Fact]
        public void SlotsNotAnimationCountDecideWhatPlays()
        {
            // Real entries exist with AnimationCount 1 and two filled slots, so the slots win.
            var twoCodes = (byte[])OneAnimation.Clone();
            twoCodes[12] = 5; twoCodes[13] = 0; twoCodes[14] = 0; twoCodes[15] = 0;
            var info = new BuildingAnimationInfo(twoCodes);
            Assert.Equal(new[] { 3, 5 }, info.UsedCodes.ToArray());
            Assert.True(info.Animates);
        }

        [Fact]
        public void TimeOfDayFlagIsReadFromType()
        {
            var info = new BuildingAnimationInfo(OneAnimation);
            Assert.False(info.IsTimeOfDay);

            var withFlag = (byte[])OneAnimation.Clone();
            withFlag[1] = BuildingAnimationInfo.TypeTimeOfDay;
            Assert.True(new BuildingAnimationInfo(withFlag).IsTimeOfDay);
        }

        [Theory]
        [MemberData(nameof(Records))]
        public void RoundTripsUnchanged(byte[] record)
        {
            Assert.Equal(record, new BuildingAnimationInfo(record).ToByteArray());
        }

        public static TheoryData<byte[]> Records => new TheoryData<byte[]> { Silent, OneAnimation };
    }
}
