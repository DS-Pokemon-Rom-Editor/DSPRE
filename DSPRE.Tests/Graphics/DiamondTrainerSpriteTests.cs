using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Images;
using Xunit;
using Xunit.Abstractions;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>Diamond's scrambled trainer drawings descramble and write back unchanged.</summary>
    [Collection("rom")]
    public class DiamondTrainerSpriteTests
    {
        private readonly ITestOutputHelper _out;
        public DiamondTrainerSpriteTests(ITestOutputHelper o) => _out = o;

        [SkippableTheory]
        [InlineData(DirNames.trainerGraphics, 98)]
        [InlineData(DirNames.trainerBackGraphics, 8)]
        public void EveryDrawingReadsAsAPictureAndWritesBackUnchanged(DirNames archive, int expected)
        {
            Skip.If(!Directory.Exists(TestRoms.Diamond), "Diamond test project not configured");
            new RomInfo("ADAE", TestRoms.Diamond);
            DSUtils.TryUnpackNarcs(new List<DirNames> { archive });

            string dir = gameDirs[archive].unpackedDir;
            int count = RomFiles.Settled(dir).Length / TrainerGraphicsLayout.Stride;
            Assert.Equal(expected, count);

            var noise = new List<int>();
            var changed = new List<string>();
            string copy = Path.Combine(Path.GetTempPath(), $"dspre-dp-trainer-{Guid.NewGuid():N}");
            try
            {
                for (int i = 0; i < count; i++)
                {
                    int id = TrainerGraphicsLayout.DrawingEntry(i);
                    string path = Path.Combine(dir, id.ToString("D4"));
                    byte[] original = File.ReadAllBytes(path);

                    var tile = new NCGR(path, id, id.ToString("D4"));
                    byte[] pixels = (byte[])tile.Tiles.Clone();
                    ushort seed = SpriteScrambling.Seed(pixels, 0, pixels.Length);
                    SpriteScrambling.Unscramble(pixels, 0, pixels.Length);
                    if (!LooksDrawn(pixels)) noise.Add(i);

                    SpriteScrambling.Scramble(pixels, 0, pixels.Length, seed);
                    tile.Set_Tiles(pixels);
                    if (File.Exists(copy)) File.Delete(copy);
                    tile.Write(copy, null);

                    byte[] written = File.ReadAllBytes(copy);
                    if (!written.SequenceEqual(original))
                    {
                        int at = Enumerable.Range(0, Math.Min(written.Length, original.Length))
                            .FirstOrDefault(k => written[k] != original[k]);
                        changed.Add($"{i}: {original.Length} to {written.Length} bytes, first difference at 0x{at:X}");
                    }
                }
            }
            finally
            {
                if (File.Exists(copy)) File.Delete(copy);
            }

            _out.WriteLine($"{archive}: {count} drawings, {noise.Count} read as noise, {changed.Count} changed on write");
            foreach (var c in changed.Take(5)) _out.WriteLine("  " + c);

            Assert.True(noise.Count == 0, "still noise after descrambling: " + string.Join(", ", noise));
            Assert.True(changed.Count == 0, "did not write back unchanged: " + string.Join("; ", changed.Take(5)));
        }

        // A drawing is mostly flat areas, so neighbouring pixels usually match; noise almost never does.
        private static bool LooksDrawn(byte[] fourBitPixels)
        {
            int total = fourBitPixels.Length * 2, same = 0, previous = -1;
            for (int i = 0; i < total; i++)
            {
                int colour = i % 2 == 0 ? fourBitPixels[i / 2] & 0x0F : fourBitPixels[i / 2] >> 4;
                if (colour == previous) same++;
                previous = colour;
            }
            return same > total / 2;
        }
    }
}
