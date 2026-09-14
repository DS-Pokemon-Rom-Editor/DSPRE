using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>A walker's pictures come back in the order the field animates them.</summary>
    [Collection("rom")]
    public class OverworldPicturesTests
    {
        private readonly ITestOutputHelper _out;
        public OverworldPicturesTests(ITestOutputHelper o) => _out = o;

        private static string Project(string name) => name switch
        {
            "Diamond" => TestRoms.Diamond, "Platinum" => TestRoms.Platinum, _ => TestRoms.HeartGold,
        };

        [SkippableTheory]
        [InlineData("ADAE", "Diamond")]
        [InlineData("CPUE", "Platinum")]
        [InlineData("IPKE", "HeartGold")]
        public void EachFacingOfAWalkerStandsOnTheSamePictureBothSteps(string code, string name)
        {
            Skip.IfNot(Directory.Exists(Project(name)), $"{name} is not unpacked here");
            new RomInfo(code, Project(name));
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.OWSprites });
            RomInfo.SetOWtable();
            RomInfo.ReadOWTable();

            string dir = RomInfo.gameDirs[RomInfo.DirNames.OWSprites].unpackedDir;
            int walkers = 0;
            foreach (var entry in RomInfo.OverworldTable.OrderBy(e => e.Key))
            {
                if (entry.Value.spriteID == 0x3D3D) continue;
                string path = Path.Combine(dir, entry.Value.spriteID.ToString("D4"));
                if (!File.Exists(path)) continue;
                var pictures = OverworldSprites.Pictures(File.ReadAllBytes(path));
                if (pictures.Count != 16) continue;

                // Standing, one foot, standing, the other foot: the two standing pictures are one drawing.
                for (int facing = 0; facing < 4; facing++)
                    Assert.Equal(pictures[facing * 4].Rgba, pictures[facing * 4 + 2].Rgba);
                Assert.NotEqual(pictures[0].Rgba, pictures[4].Rgba);
                if (++walkers == 20) break;
            }
            _out.WriteLine($"{name}: {walkers} walkers checked");
            Assert.True(walkers > 0, "no sixteen-picture walker was found");
        }
    }
}
