using System;
using System.IO;
using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// Material animations (NSBMA) from a real HeartGold project: the ones that fade a material in and out.
    /// </summary>
    public class MaterialColourAnimationTests
    {
        private const string AnimDir =
            @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents\unpacked\buildingAnimations";

        private static bool Ready => Directory.Exists(AnimDir);

        private static MaterialColourAnimation Load(int code)
        {
            string p = Path.Combine(AnimDir, code.ToString("D4"));
            return File.Exists(p) ? MaterialColourAnimation.Load(File.ReadAllBytes(p)) : null;
        }

        [Fact]
        public void AFadeRunsFromSolidToGone()
        {
            if (!Ready) return;

            // 0119 fades a floor out over 60 frames. The name comes from the list inside the chunk,
            // which is the materials; the outer list names the model and is a different thing.
            var anim = Load(119);
            Assert.NotNull(anim);
            Assert.True(anim.Fades);
            Assert.Equal(60, anim.FrameCount);

            int m = anim.IndexOf("yuka2_lm3");
            Assert.True(m >= 0);
            Assert.Equal(1f, anim.Evaluate(m, 0).Value, 2);
            Assert.True(anim.Evaluate(m, 30).Value < anim.Evaluate(m, 0).Value);
        }

        [Fact]
        public void APairOfAnimationsFadeOppositeWays()
        {
            if (!Ready) return;

            // hosz1 and hosz2 are a cross-fade: one comes in as the other goes out.
            var up = Load(212);
            var down = Load(214);
            if (up == null || down == null) return;

            float upStart = up.Evaluate(0, 0).Value, upLater = up.Evaluate(0, 15).Value;
            float dnStart = down.Evaluate(0, 0).Value, dnLater = down.Evaluate(0, 15).Value;

            Assert.True(upLater > upStart, "hosz1 should fade in");
            Assert.True(dnLater < dnStart, "hosz2 should fade out");
        }

        [Fact]
        public void EveryValueStaysInRange()
        {
            if (!Ready) return;

            int checkedValues = 0;
            foreach (var f in Directory.GetFiles(AnimDir))
            {
                var anim = MaterialColourAnimation.Load(File.ReadAllBytes(f));
                if (anim == null) continue;
                for (int m = 0; m < anim.MaterialNames.Count; m++)
                    for (int frame = 0; frame < anim.FrameCount; frame++)
                    {
                        var v = anim.Evaluate(m, frame);
                        if (v == null) continue;
                        checkedValues++;
                        Assert.InRange(v.Value, 0f, 1f);
                    }
            }
            Assert.True(checkedValues > 100);
        }

        [Fact]
        public void MaterialsAreNamedProperly()
        {
            if (!Ready) return;

            // A name read from the wrong place runs into the next block, so check they look like names.
            foreach (var f in Directory.GetFiles(AnimDir))
            {
                var anim = MaterialColourAnimation.Load(File.ReadAllBytes(f));
                if (anim == null) continue;
                Assert.All(anim.MaterialNames, n =>
                {
                    Assert.False(string.IsNullOrWhiteSpace(n));
                    // Every character must be ordinary printable text. A zero byte or anything past the
                    // name's end would mean the read ran into the block that follows it.
                    Assert.All(n, c => Assert.InRange(c, ' ', '~'));
                });
            }
        }

        [Fact]
        public void ManyMaterialsInOneAnimationAreAllRead()
        {
            if (!Ready) return;

            // 0147 fades fourteen materials at once, including a waterfall's spray.
            var anim = Load(147);
            if (anim == null) return;
            Assert.Equal(14, anim.MaterialNames.Count);
            Assert.Contains("wfall_wave", anim.MaterialNames);
            Assert.All(Enumerable.Range(0, anim.MaterialNames.Count), i => Assert.False(anim.IsStatic(i)));
        }

        [Fact]
        public void TheFadeNamesAMaterialItsModelActuallyHas()
        {
            if (!Ready) return;

            // This is what the whole thing turns on: the names inside the chunk are the model's own
            // material names, so the renderer can match them.
            foreach (int code in new[] { 212, 214, 216, 218 })
            {
                var anim = Load(code);
                if (anim == null) continue;
                Assert.Contains("ob_lgsz4", anim.MaterialNames);

                int m = anim.IndexOf("ob_lgsz4");
                Assert.False(anim.IsStatic(m));

                // And it really does change from frame to frame rather than sitting still.
                var seen = Enumerable.Range(0, anim.FrameCount)
                                     .Select(f => anim.Evaluate(m, f).Value).Distinct().ToArray();
                Assert.True(seen.Length > 2, $"animation {code} barely changes");
            }
        }

        [Fact]
        public void PlaybackLoops()
        {
            if (!Ready) return;
            var anim = Load(119);
            if (anim == null) return;
            Assert.Equal(anim.Evaluate(0, 9), anim.Evaluate(0, 9 + anim.FrameCount));
        }

        [Fact]
        public void EveryFadingAnimationInTheArchiveIsRead()
        {
            if (!Ready) return;

            int fading = 0, others = 0;
            foreach (var f in Directory.GetFiles(AnimDir))
            {
                byte[] b = File.ReadAllBytes(f);
                bool isFade = b.Length >= 4 && System.Text.Encoding.ASCII.GetString(b, 0, 4) == "BMA0";
                var anim = MaterialColourAnimation.Load(b);
                if (isFade) { Assert.NotNull(anim); fading++; }
                else { Assert.Null(anim); others++; }
            }
            Assert.True(fading > 0);
            Assert.True(others > 0);
        }

        [Fact]
        public void AMaterialItDoesNotTouchComesBackEmpty()
        {
            if (!Ready) return;
            var anim = Load(119);
            if (anim == null) return;
            Assert.Equal(-1, anim.IndexOf("no_such_material"));
            Assert.Null(anim.Evaluate("no_such_material", 0));
            Assert.True(anim.IsStatic(-1));
        }
    }
}
