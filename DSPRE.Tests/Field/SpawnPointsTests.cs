using System.IO;
using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests.Field
{
    /// <summary>Expected values are pokeplatinum's sSpawnLocations row for Twinleaf Town.</summary>
    [Collection("rom")]
    public class SpawnPointsTests
    {
        [SkippableFact]
        public void PlatinumFliesToTwinleafWhereTheGameDoes()
        {
            Skip.If(!Directory.Exists(TestRoms.Platinum), "Platinum test project not configured");
            new RomInfo("CPUE", TestRoms.Platinum);
            var points = SpawnPoints.Read();

            Assert.Equal(RomInfo.FlyTableRows * 2, points.Count);
            var fly = points.First(p => p.Kind == "Fly point");
            Assert.Equal(411, fly.Header);          // MAP_HEADER_TWINLEAF_TOWN
            Assert.Equal((0x74, 0x376), (fly.X, fly.Z));
            Assert.True(fly.Global);
            var blackout = points.First(p => p.Kind == "Blackout point");
            Assert.Equal((8, 8), (blackout.X, blackout.Z));
        }
    }
}
