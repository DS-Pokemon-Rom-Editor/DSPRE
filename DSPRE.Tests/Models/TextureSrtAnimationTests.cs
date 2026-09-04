using System;
using System.IO;
using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>Plays back the real HeartGold terrain animations. </summary>
    public class TextureSrtAnimationTests
    {
        private static readonly string Archive = TestRoms.HeartGold + @"\files\a\1\4\0";

        private static byte[][] ReadNarc(string path)
        {
            byte[] b = File.ReadAllBytes(path);
            int o = 16;
            int count = BitConverter.ToInt32(b, o + 8);
            int fat = o + 12;
            o += BitConverter.ToInt32(b, o + 4);
            o += BitConverter.ToInt32(b, o + 4);
            int img = o + 8;
            return Enumerable.Range(0, count).Select(i =>
            {
                int start = BitConverter.ToInt32(b, fat + i * 8);
                int end = BitConverter.ToInt32(b, fat + i * 8 + 4);
                return b.Skip(img + start).Take(end - start).ToArray();
            }).ToArray();
        }

        private static TextureSrtAnimation First() =>
            File.Exists(Archive) ? TextureSrtAnimation.Load(ReadNarc(Archive)[0]) : null;

        [SkippableFact]
        public void LoadsTheWaterMaterials()
        {
            Skip.If(!File.Exists(Archive), "HeartGold not unpacked here");
            var a = First();
            Assert.NotNull(a);

            Assert.Contains("river", a.MaterialNames);
            Assert.Contains("sea_on", a.MaterialNames);
            Assert.Equal(360, a.FrameCount);          // a six second loop at 60fps
        }

        [SkippableFact]
        public void UnknownMaterialIsLeftAlone()
        {
            Skip.If(!File.Exists(Archive), "HeartGold not unpacked here");
            var a = First();
            Assert.NotNull(a);

            Assert.Equal(-1, a.IndexOf("no_such_material"));

            var srt = a.Evaluate("no_such_material", 0);
            Assert.Equal(1f, srt.ScaleS);
            Assert.Equal(0f, srt.TranslateS);
        }

        [SkippableFact]
        public void WaterScrollsOverTime()
        {
            Skip.If(!File.Exists(Archive), "HeartGold not unpacked here");
            var a = First();
            Assert.NotNull(a);

            var start = a.Evaluate("river", 0);
            var later = a.Evaluate("river", 30);
            Assert.NotEqual(start.TranslateS, later.TranslateS);

            // Scale is constant across the whole loop for these materials.
            Assert.Equal(start.ScaleS, later.ScaleS);
        }

        [SkippableFact]
        public void PlaybackLoops()
        {
            Skip.If(!File.Exists(Archive), "HeartGold not unpacked here");
            var a = First();
            Assert.NotNull(a);

            Assert.Equal(a.Evaluate("river", 7).TranslateS,
                         a.Evaluate("river", 7 + a.FrameCount).TranslateS);
        }

        [SkippableFact]
        public void MovingMaterialsAreNotReportedStatic()
        {
            Skip.If(!File.Exists(Archive), "HeartGold not unpacked here");
            var a = First();
            Assert.NotNull(a);

            Assert.False(a.IsStatic(a.IndexOf("river")));
            Assert.True(a.IsStatic(-1));   // nothing selected: nothing to animate
        }

        [SkippableFact]
        public void EveryMaterialOnlyScrolls()
        {
            Skip.If(!File.Exists(Archive), "HeartGold not unpacked here");
            var a = First();
            Assert.NotNull(a);

            // The whole archive is scrolling water: nothing scales or rotates. The renderer relies on
            // this, so if a ROM ever does something else the matrix path needs a real look.
            for (int m = 0; m < a.MaterialNames.Count; m++)
                for (int f = 0; f < a.FrameCount; f += 37)
                {
                    var s = a.Evaluate(m, f);
                    Assert.Equal(1f, s.ScaleS);
                    Assert.Equal(1f, s.ScaleT);
                    Assert.Equal(0f, s.SinRotation);
                    Assert.Equal(1f, s.CosRotation);
                }
        }

        [SkippableFact]
        public void IdentitySrtIsAnIdentityMatrix()
        {
            Assert.Equal(new[] { 1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f },
                         TextureSrtAnimation.Srt.Identity.ToMatrix3());
        }

        [SkippableFact]
        public void ScrollingShowsUpAsTheMatrixTranslation()
        {
            Skip.If(!File.Exists(Archive), "HeartGold not unpacked here");
            var a = First();
            Assert.NotNull(a);

            var srt = a.Evaluate("river", 60);
            float[] m = srt.ToMatrix3();
            Assert.Equal(srt.TranslateS, m[6]);
            Assert.Equal(srt.TranslateT, m[7]);
            Assert.NotEqual(0f, m[6]);          // the river really has moved by frame 60
            Assert.Equal(1f, m[8]);
        }
    }
}
