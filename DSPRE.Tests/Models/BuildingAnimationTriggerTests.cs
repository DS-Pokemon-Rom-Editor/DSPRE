using System.IO;
using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>Which building animations run by themselves and which wait to be set off. </summary>
    public class BuildingAnimationTriggerTests
    {
        private static readonly string ListDir = TestRoms.HeartGold + @"\unpacked\buildingAnimListOut";

        private static bool Ready => Directory.Exists(ListDir);

        private static BuildingAnimationInfo[] All() =>
            Directory.GetFiles(ListDir).OrderBy(f => f)
                     .Select(f => new BuildingAnimationInfo(File.ReadAllBytes(f)))
                     .ToArray();

        [Fact]
        public void EveryDoorWaitsToBeOpened()
        {
            if (!Ready) return;

            // Not one door in the game animates on its own; they are all Type 0x03, which has the
            // bottom bit set. If this ever fails, a door is about to start flapping in the preview.
            var doors = All().Where(i => i.Animates && i.IsDoor).ToArray();
            Assert.NotEmpty(doors);
            Assert.All(doors, d =>
            {
                Assert.True(d.IsConditional, "a door that would run on its own");
                Assert.False(d.PlaysUnprompted);
            });
        }

        [Fact]
        public void NothingConditionalIsPlayedUnprompted()
        {
            if (!Ready) return;
            Assert.All(All().Where(i => i.IsConditional), i => Assert.False(i.PlaysUnprompted));
        }

        [Fact]
        public void TimeOfDayAnimationsAreNotTreatedAsAlwaysOn()
        {
            if (!Ready) return;

            var timed = All().Where(i => i.Animates && i.IsTimeOfDay).ToArray();
            Assert.NotEmpty(timed);
            Assert.All(timed, i => Assert.False(i.PlaysUnprompted));
        }

        [Fact]
        public void TheOnesThatDoRunAreStillThere()
        {
            if (!Ready) return;

            // Waterfalls, lights and the rest: plenty should still play, or the filter has gone too far.
            var auto = All().Where(i => i.PlaysUnprompted).ToArray();
            Assert.True(auto.Length > 20, $"only {auto.Length} animations left playing");
            Assert.All(auto, i => Assert.False(i.IsDoor));
        }

        [Fact]
        public void TimeOfDayIsAnExactType()
        {
            // The engine compares Type to the value rather than testing the bit, so a Type that merely
            // happens to include the bit is not a time-of-day animation.
            var timed = new BuildingAnimationInfo(Record(type: BuildingAnimationInfo.TypeTimeOfDay));
            Assert.True(timed.IsTimeOfDay);

            var other = new BuildingAnimationInfo(Record(type: BuildingAnimationInfo.TypeTimeOfDay | 0x04));
            Assert.False(other.IsTimeOfDay);
        }

        [Fact]
        public void APlayOnceAnimationDoesNotLoopAndWaitsToBeStarted()
        {
            // In the games: a Suicide animation is entered with a loop count of one and stopped,
            // while everything else is entered as LOOP_INFINIT and running.
            var once = new BuildingAnimationInfo(Record(type: 0, suicide: 1));
            Assert.True(once.PlaysOnce);
            Assert.Equal(1, once.LoopCount);
            Assert.False(once.PlaysUnprompted);

            var looping = new BuildingAnimationInfo(Record(type: 0));
            Assert.False(looping.PlaysOnce);
            Assert.Equal(BuildingAnimationInfo.LoopForever, looping.LoopCount);
        }

        [Fact]
        public void NoBuildingInHeartGoldUsesPlayOnce()
        {
            if (!Ready) return;
            // Recorded rather than assumed: the flag exists but the retail data never sets it, so the
            // rule above is there for hacks rather than for anything the games ship.
            Assert.Empty(All().Where(i => i.Animates && i.PlaysOnce));
        }

        [Fact]
        public void AnUnconditionalAnimationPlays()
        {
            var info = new BuildingAnimationInfo(Record(type: 0));
            Assert.True(info.Animates);
            Assert.True(info.PlaysUnprompted);
            Assert.False(info.IsConditional);
        }

        /// <summary>One list entry: animating, with a single animation, and the given type.</summary>
        private static byte[] Record(int type, int door = 0, int suicide = 0)
        {
            var b = new byte[BuildingAnimationInfo.Size];
            b[0] = 1;                 // Flag: it animates
            b[1] = (byte)type;
            b[2] = (byte)suicide;
            b[4] = (byte)door;
            b[6] = 1;                 // AnimationCount
            for (int i = 0; i < BuildingAnimationInfo.MaxAnimations; i++)
            {
                uint code = i == 0 ? 0u : BuildingAnimationInfo.NoAnimation;
                System.BitConverter.GetBytes(code).CopyTo(b, 8 + i * 4);
            }
            return b;
        }
    }
}
