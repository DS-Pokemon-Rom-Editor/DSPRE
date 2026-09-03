using System.IO;
using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>Walkability across a matrix of maps. </summary>
    public class MapCollisionGridTests
    {
        private static byte[,] Grid(byte fill = 0)
        {
            var g = new byte[MapFile.mapSize, MapFile.mapSize];
            for (int z = 0; z < MapFile.mapSize; z++)
                for (int x = 0; x < MapFile.mapSize; x++) g[z, x] = fill;
            return g;
        }

        [Fact]
        public void OnlyTheTopBitClosesATileOff()
        {
            var g = Grid();
            g[3, 4] = 0x80;      // the value a map file uses for blocked
            g[3, 5] = 0x7F;      // every other bit set, but not the one that matters
            var grid = new MapCollisionGrid();
            grid.Add(0, 0, g);

            Assert.True(grid.IsBlocked(4, 3));
            Assert.False(grid.IsBlocked(5, 3));
            Assert.False(grid.IsBlocked(0, 0));
        }

        [Fact]
        public void TilesAreCountedAcrossTheWholeMatrix()
        {
            var a = Grid();
            var b = Grid();
            b[0, 0] = 0x80;
            var grid = new MapCollisionGrid();
            grid.Add(0, 0, a);
            grid.Add(1, 0, b);

            // The first tile of the second map along is tile 32 of the matrix.
            Assert.True(grid.IsBlocked(MapFile.mapSize, 0));
            Assert.False(grid.IsBlocked(MapFile.mapSize + 1, 0));
        }

        [Fact]
        public void AMapThatIsNotLoadedCountsAsClosedOff()
        {
            var grid = new MapCollisionGrid();
            grid.Add(0, 0, Grid());
            Assert.True(grid.IsBlocked(-1, 0));                    // off the left edge
            Assert.True(grid.IsBlocked(MapFile.mapSize, 0));       // the next map along, not loaded
            Assert.True(grid.IsBlocked(0, -1));
        }

        [Fact]
        public void AnEmptyGridSaysSo()
        {
            Assert.True(new MapCollisionGrid().IsEmpty);
            var grid = new MapCollisionGrid();
            grid.Add(0, 0, Grid());
            Assert.False(grid.IsEmpty);
        }

        [Fact]
        public void RealMapsHaveBothWalkableAndBlockedTiles()
        {
            string Maps = TestRoms.HeartGold + @"\unpacked\maps";
            if (!Directory.Exists(Maps)) return;

            int checkedMaps = 0, withBlocked = 0, withOpen = 0;
            foreach (var f in Directory.GetFiles(Maps).OrderBy(x => x).Take(40))
            {
                MapFile map;
                try { map = new MapFile(f, RomInfo.GameFamilies.HGSS, false, false); } catch { continue; }
                if (map.collisions == null) continue;
                checkedMaps++;
                var grid = new MapCollisionGrid();
                grid.Add(0, 0, map.collisions);

                bool anyBlocked = false, anyOpen = false;
                for (int z = 0; z < MapFile.mapSize; z++)
                    for (int x = 0; x < MapFile.mapSize; x++)
                        if (grid.IsBlocked(x, z)) anyBlocked = true; else anyOpen = true;
                if (anyBlocked) withBlocked++;
                if (anyOpen) withOpen++;
            }
            Assert.True(checkedMaps > 0);
            Assert.True(withBlocked > 0);
            Assert.True(withOpen > 0);
        }
    }
}
