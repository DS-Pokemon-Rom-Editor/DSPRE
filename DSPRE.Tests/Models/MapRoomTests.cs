using System;
using System.IO;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests.Models
{
    public class MapRoomTests
    {
        [SkippableTheory]
        [InlineData("Platinum")]
        [InlineData("HeartGold")]
        [InlineData("Diamond")]
        public void EveryRetailMapFitsTheRoomTheGameKeepsForIt(string game)
        {
            string project = game == "Platinum" ? TestRoms.Platinum : game == "Diamond" ? TestRoms.Diamond : TestRoms.HeartGold;
            Skip.If(!Directory.Exists(project), $"{game} test project not configured");
            int maps = 0, largest = 0;
            foreach (string path in RomFiles.Settled(Path.Combine(project, "unpacked", "maps")))
            {
                byte[] map = File.ReadAllBytes(path);
                if (map.Length < 16) continue;
                int model = BitConverter.ToInt32(map, 8), terrain = BitConverter.ToInt32(map, 12);
                Assert.Null(MapFile.TooBigForTheGame(model, terrain));
                largest = Math.Max(largest, model);
                maps++;
            }
            Assert.True(maps > 0, $"No {game} map was read.");
            // The largest retail models come within a few hundred bytes of the room, which is what makes it the room.
            Assert.True(largest > MapFile.ModelRoom - 1024, $"largest {game} model is {largest}");
            Assert.NotNull(MapFile.TooBigForTheGame(MapFile.ModelRoom + 1, 0));
        }
    }
}
