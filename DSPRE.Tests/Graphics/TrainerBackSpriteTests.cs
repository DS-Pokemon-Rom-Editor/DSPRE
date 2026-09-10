using System.IO;
using System.Linq;
using DSPRE.Avalonia;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;
using Xunit;
using Xunit.Abstractions;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>
    /// Every back sprite is named and animated, and a colour change is written back.
    /// </summary>
    [Collection("rom")]
    public class TrainerBackSpriteTests
    {
        private readonly ITestOutputHelper _out;
        public TrainerBackSpriteTests(ITestOutputHelper o) => _out = o;

        private static string Project(string game) => game == "HeartGold" ? TestRoms.HeartGold : TestRoms.Platinum;

        [SkippableTheory]
        [InlineData("IPKE", "HeartGold", 17, "Ethan")]
        [InlineData("CPUE", "Platinum", 11, "Lucas")]
        public void EveryBackSpriteIsNamedAndAnimated(string code, string game, int expected, string first)
        {
            Skip.If(!Directory.Exists(Project(game)), $"{game} test project not configured");
            new RomInfo(code, Project(game));
            DSUtils.TryUnpackNarcs(new System.Collections.Generic.List<DirNames> { DirNames.trainerBackGraphics });

            int count = RomFiles.Settled(gameDirs[DirNames.trainerBackGraphics].unpackedDir).Length / TrainerBackSprites.FilesPerSprite;
            var names = TrainerBackSprites.Names(count);
            Assert.Equal(expected, count);
            Assert.Equal(first, names[0]);
            Assert.DoesNotContain(names, n => n.StartsWith("Back sprite"));

            for (int i = 0; i < count; i++)
            {
                var r = new TrainerClassSpriteRenderer();
                r.Load(i, DirNames.trainerBackGraphics);
                _out.WriteLine($"{game} {i} {names[i]}: {r.FrameCount} frames, {r.SequenceCount} sequences");
                Assert.True(r.HasSprite, $"{names[i]} has no layout");
                Assert.Equal(2, r.SequenceCount);
                Assert.True(r.Sequence(1).Length > 1, $"{names[i]}'s throw has no frames");
            }
        }

        [SkippableFact]
        public void AColourChangeIsWrittenIntoItsOwnSlotOnly()
        {
            Skip.If(!Directory.Exists(TestRoms.HeartGold), "HeartGold test project not configured");
            new RomInfo("IPKE", TestRoms.HeartGold);
            DSUtils.TryUnpackNarcs(new System.Collections.Generic.List<DirNames> { DirNames.trainerBackGraphics });
            string path = Path.Combine(gameDirs[DirNames.trainerBackGraphics].unpackedDir, "0001");
            byte[] original = File.ReadAllBytes(path);

            var before = new Images.NCLR(path, 1, "0001").Palette.SelectMany(p => p).ToArray();
            var colours = before.Select(c => 0xFF000000u | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B).ToArray();
            colours[3] = 0xFFF80800u;

            byte[] edited = (byte[])original.Clone();
            Assert.Null(GraphicAssets.PatchPalette(ref edited, colours));
            Assert.Equal(original.Length, edited.Length);

            string copy = Path.Combine(Path.GetTempPath(), $"dspre-back-palette-{System.Guid.NewGuid():N}");
            try
            {
                File.WriteAllBytes(copy, edited);
                var after = new Images.NCLR(copy, 1, "copy").Palette.SelectMany(p => p).ToArray();
                Assert.Equal(before.Length, after.Length);
                for (int i = 0; i < before.Length; i++)
                {
                    if (i == 3) Assert.Equal((0xF8, 0x08, 0x00), (after[i].R, after[i].G, after[i].B));
                    else Assert.Equal(before[i].ToArgb(), after[i].ToArgb());
                }
            }
            finally { File.Delete(copy); }
            Assert.Equal(original, File.ReadAllBytes(path));
        }
    }
}
