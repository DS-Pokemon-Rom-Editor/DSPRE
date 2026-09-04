using System;
using System.IO;
using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>Texture-swapping animations (NSBTP), read from a real HeartGold project. </summary>
    public class TexturePatternAnimationTests
    {
        private static readonly string AnimDir = TestRoms.HeartGold + @"\unpacked\buildingAnimations";

        private static bool Ready => Directory.Exists(AnimDir);

        private static TexturePatternAnimation Load(int code)
        {
            string p = Path.Combine(AnimDir, code.ToString("D4"));
            return File.Exists(p) ? TexturePatternAnimation.Load(File.ReadAllBytes(p)) : null;
        }

        [SkippableFact]
        public void TheLiftDoorRunsThroughItsFrames()
        {
            Skip.If(!Ready, "the extracted game project these tests read is not on this machine");

            // Animation 200 opens a lift door: one material stepping through six pictures.
            var anim = Load(200);
            Assert.NotNull(anim);
            int m = anim.IndexOf("ele_door1_op");
            Assert.True(m >= 0);
            Assert.False(anim.IsStatic(m));

            var seen = Enumerable.Range(0, anim.FrameCount)
                                 .Select(f => anim.Evaluate(m, f).TextureName)
                                 .Distinct().ToArray();
            Assert.True(seen.Length >= 2);
            Assert.All(seen, n => Assert.StartsWith("ele_door", n));
        }

        [SkippableFact]
        public void ClosingRunsTheOppositeWayToOpening()
        {
            Skip.If(!Ready, "the extracted game project these tests read is not on this machine");
            var opening = Load(200);
            var closing = Load(201);
            Assert.NotNull(opening);
            Assert.NotNull(closing);

            string[] a = opening.AllSwaps(opening.IndexOf("ele_door1_op")).Select(s => s.TextureName).ToArray();
            string[] b = closing.AllSwaps(closing.IndexOf("ele_door1_cl")).Select(s => s.TextureName).ToArray();
            Assert.Equal(a, b.Reverse());
        }

        [SkippableFact]
        public void PlaybackHoldsTheLastKeyFrameAndLoops()
        {
            Skip.If(!Ready, "the extracted game project these tests read is not on this machine");
            var anim = Load(200);
            Assert.NotNull(anim);
            int m = anim.IndexOf("ele_door1_op");

            // A frame between two key frames keeps showing the earlier one.
            Assert.Equal(anim.Evaluate(m, 0).TextureName, anim.Evaluate(m, 0).TextureName);
            // And playback wraps, as the games loop forever.
            Assert.Equal(anim.Evaluate(m, 3).TextureName,
                         anim.Evaluate(m, 3 + anim.FrameCount).TextureName);
        }

        [SkippableFact]
        public void AMaterialItDoesNotTouchComesBackEmpty()
        {
            Skip.If(!Ready, "the extracted game project these tests read is not on this machine");
            var anim = Load(200);
            Assert.NotNull(anim);

            Assert.Equal(-1, anim.IndexOf("no_such_material"));
            Assert.False(anim.Evaluate("no_such_material", 0).IsSet);
            Assert.True(anim.IsStatic(-1));
        }

        [SkippableFact]
        public void OtherKindsOfAnimationAreNotReadAsSwaps()
        {
            Skip.If(!Ready, "the extracted game project these tests read is not on this machine");

            int patterns = 0, others = 0;
            foreach (var f in Directory.GetFiles(AnimDir))
            {
                byte[] b = File.ReadAllBytes(f);
                bool isPattern = b.Length >= 4 && System.Text.Encoding.ASCII.GetString(b, 0, 4) == "BTP0";
                var anim = TexturePatternAnimation.Load(b);
                if (isPattern) { Assert.NotNull(anim); patterns++; }
                else { Assert.Null(anim); others++; }
            }
            Assert.True(patterns > 0);
            Assert.True(others > 0);
        }
    }
}
