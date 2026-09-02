using System;
using System.IO;
using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// Buildings that move, read straight out of a real HeartGold project's already-unpacked archives.
    /// </summary>
    public class BuildingAnimationSetTests
    {
        private const string Project = @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents";
        private const string ListDir = Project + @"\unpacked\buildingAnimListOut";
        private const string AnimDir = Project + @"\unpacked\buildingAnimations";

        private static bool Ready => Directory.Exists(ListDir) && Directory.Exists(AnimDir);

        private static BuildingAnimationInfo Info(int model)
        {
            string p = Path.Combine(ListDir, model.ToString("D4"));
            return File.Exists(p) ? new BuildingAnimationInfo(File.ReadAllBytes(p)) : null;
        }

        private static TextureSrtAnimation Anim(int code)
        {
            string p = Path.Combine(AnimDir, code.ToString("D4"));
            return File.Exists(p) ? TextureSrtAnimation.Load(File.ReadAllBytes(p)) : null;
        }

        [Fact]
        public void TheWaterfallBuildingScrollsItsOwnMaterials()
        {
            if (!Ready) return;

            // Model 170 is the waterfall used on map 77. Its one animation moves the falling water.
            var info = Info(170);
            Assert.NotNull(info);
            Assert.True(info.Animates);

            var names = info.UsedCodes.Select(Anim).Where(a => a != null)
                                      .SelectMany(a => a.MaterialNames).ToArray();
            Assert.Contains("wfall_wave", names);
        }

        [Fact]
        public void OnlyTheScrollingAnimationsComeBack()
        {
            if (!Ready) return;

            // The archive holds four kinds of animation and only the scrolling ones parse, so every
            // entry either loads as a texture animation or is left alone.
            int loaded = 0, skipped = 0;
            foreach (var f in Directory.GetFiles(AnimDir))
            {
                byte[] b = File.ReadAllBytes(f);
                bool isSrt = b.Length >= 4 && System.Text.Encoding.ASCII.GetString(b, 0, 4) == "BTA0";
                var anim = TextureSrtAnimation.Load(b);
                if (isSrt) { Assert.NotNull(anim); loaded++; }
                else { Assert.Null(anim); skipped++; }
            }
            Assert.True(loaded > 0);
            Assert.True(skipped > 0);
        }

        [Fact]
        public void ModelsThatDoNotAnimateSaySo()
        {
            if (!Ready) return;

            int animating = Directory.GetFiles(ListDir).Select(f => new BuildingAnimationInfo(File.ReadAllBytes(f)))
                                     .Count(i => i.Animates);
            int total = Directory.GetFiles(ListDir).Length;
            Assert.True(animating > 0);
            Assert.True(animating < total);   // most buildings just stand there
        }

        [Fact]
        public void EveryListEntryIsOneRecordLong()
        {
            if (!Ready) return;

            foreach (var f in Directory.GetFiles(ListDir))
                Assert.Equal(BuildingAnimationInfo.Size, new FileInfo(f).Length);
        }

        [Fact]
        public void UnusedAnimationSlotsAreSkipped()
        {
            if (!Ready) return;

            foreach (var f in Directory.GetFiles(ListDir))
            {
                var info = new BuildingAnimationInfo(File.ReadAllBytes(f));
                foreach (int code in info.UsedCodes)
                {
                    Assert.NotEqual(unchecked((int)BuildingAnimationInfo.NoAnimation), code);
                    Assert.InRange(code, 0, Directory.GetFiles(AnimDir).Length - 1);
                }
            }
        }
    }
}
