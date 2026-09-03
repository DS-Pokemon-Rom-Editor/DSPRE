using System;
using System.IO;
using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// Joint animations (NSBCA) from a real HeartGold project: the ones that move a model's separate parts
    /// about rather than its textures.
    /// </summary>
    public class JointAnimationTests
    {
        private static readonly string AnimDir = TestRoms.HeartGold + @"\unpacked\buildingAnimations";

        private static bool Ready => Directory.Exists(AnimDir);

        private static JointAnimation Load(int code)
        {
            string p = Path.Combine(AnimDir, code.ToString("D4"));
            return File.Exists(p) ? JointAnimation.Load(File.ReadAllBytes(p)) : null;
        }

        private static byte[] Raw(int code)
        {
            string p = Path.Combine(AnimDir, code.ToString("D4"));
            return File.Exists(p) ? File.ReadAllBytes(p) : null;
        }

        [Fact]
        public void APartThatTurnsGivesADifferentMatrixEachFrame()
        {
            if (!Ready) return;

            // Animation 3 turns one part right round over 180 frames.
            var anim = Load(3);
            Assert.NotNull(anim);
            Assert.True(anim.Moves);
            Assert.Equal(180, anim.FrameCount);

            int part = anim.AnimatedObjects.First();
            var start = anim.MatrixFor(part, 0);
            var quarter = anim.MatrixFor(part, 45);
            Assert.NotNull(start);
            Assert.NotNull(quarter);
            Assert.False(start.SequenceEqual(quarter));
        }

        [Fact]
        public void ATurnKeepsItsShape()
        {
            if (!Ready) return;
            var anim = Load(3);
            if (anim == null) return;
            int part = anim.AnimatedObjects.First();

            // A turn should not stretch anything: each row of the 3x3 keeps unit length.
            for (int f = 0; f < anim.FrameCount; f += 11)
            {
                var m = anim.MatrixFor(part, f);
                double r0 = Math.Sqrt(m[0] * m[0] + m[1] * m[1] + m[2] * m[2]);
                double r1 = Math.Sqrt(m[4] * m[4] + m[5] * m[5] + m[6] * m[6]);
                Assert.InRange(r0, 0.98, 1.02);
                Assert.InRange(r1, 0.98, 1.02);
            }
        }

        [Fact]
        public void PlaybackLoops()
        {
            if (!Ready) return;
            var anim = Load(3);
            if (anim == null) return;
            int part = anim.AnimatedObjects.First();
            Assert.Equal(anim.MatrixFor(part, 7), anim.MatrixFor(part, 7 + anim.FrameCount));
        }

        [Fact]
        public void PartsTheAnimationDoesNotTouchAreLeftAlone()
        {
            if (!Ready) return;
            var anim = Load(3);
            if (anim == null) return;
            Assert.Null(anim.MatrixFor(999, 0));
        }

        [Fact]
        public void EveryMatrixItProducesIsWellFormed()
        {
            if (!Ready) return;

            int checkedMatrices = 0;
            foreach (var f in Directory.GetFiles(AnimDir))
            {
                var anim = JointAnimation.Load(File.ReadAllBytes(f));
                if (anim == null || !anim.Moves) continue;

                foreach (int part in anim.AnimatedObjects)
                    for (int frame = 0; frame < Math.Min(anim.FrameCount, 120); frame += 13)
                    {
                        var m = anim.MatrixFor(part, frame);
                        if (m == null) continue;
                        checkedMatrices++;

                        Assert.All(m, v => Assert.False(float.IsNaN(v) || float.IsInfinity(v)));
                        // Nothing should be squashed flat: the first two rows keep a real length.
                        double r0 = Math.Sqrt(m[0] * m[0] + m[1] * m[1] + m[2] * m[2]);
                        double r1 = Math.Sqrt(m[4] * m[4] + m[5] * m[5] + m[6] * m[6]);
                        Assert.True(r0 > 0.01, $"{Path.GetFileName(f)} part {part} frame {frame} row 0 collapsed");
                        Assert.True(r1 > 0.01, $"{Path.GetFileName(f)} part {part} frame {frame} row 1 collapsed");
                    }
            }
            Assert.True(checkedMatrices > 100);
        }

        [Fact]
        public void MostOfTheArchivesJointAnimationsActuallyMove()
        {
            if (!Ready) return;

            int total = 0, moving = 0;
            foreach (var f in Directory.GetFiles(AnimDir))
            {
                var anim = JointAnimation.Load(File.ReadAllBytes(f));
                if (anim == null) continue;
                total++;
                if (anim.Moves) moving++;
            }
            Assert.True(total > 50);
            Assert.True(moving > total / 2);
        }

        [Fact]
        public void OtherKindsOfAnimationAreNotReadAsJointAnimations()
        {
            if (!Ready) return;

            int joint = 0, others = 0;
            foreach (var f in Directory.GetFiles(AnimDir))
            {
                byte[] b = File.ReadAllBytes(f);
                bool isJoint = b.Length >= 4 && System.Text.Encoding.ASCII.GetString(b, 0, 4) == "BCA0";
                var anim = JointAnimation.Load(b);
                if (isJoint) { Assert.NotNull(anim); joint++; }
                else { Assert.Null(anim); others++; }
            }
            Assert.True(joint > 0);
            Assert.True(others > 0);
        }

        [Fact]
        public void APivotRecordIsSixBytes()
        {
            // Every turn in the games' building animations is a pivot, and the pools divide exactly by six.
            Assert.Equal(6, JointAnimation.PivotEntrySize);
        }
    }
}
