using System;
using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>Finding the tile under a point on screen. </summary>
    public class FieldTilePickerTests
    {
        private static MapCollisionGrid Grid(int cells)
        {
            var g = new MapCollisionGrid();
            for (int i = 0; i < cells; i++) g.Add(i % 20, i / 20, new byte[MapFile.mapSize, MapFile.mapSize]);
            return g;
        }

        private static (float x, float y, float z) ToWorld(float x, float z) => (x, 0f, z);
        private static (float sx, float sy)? Project(float x, float y, float z) => (x * 4f, z * 4f);

        [Fact]
        public void ThePreparedFormFindsTheSameTileAsWalkingThemAll()
        {
            var g = Grid(4);
            var prepared = FieldTilePicker.Prepared.Build(g, ToWorld, Project);

            // Several points, including ones between tiles and near an edge.
            foreach (var (px, py) in new[] { (0.0, 0.0), (17.9, 42.1), (100.0, 4.0), (250.0, 250.0), (3.3, 199.7) })
            {
                var walked = FieldTilePicker.NearestTile(g, ToWorld, Project, px, py);
                var bucketed = prepared.Nearest(px, py);
                Assert.Equal(walked, bucketed);
            }
        }

        [Fact]
        public void ItKnowsHowManyTilesItPrepared()
        {
            var prepared = FieldTilePicker.Prepared.Build(Grid(2), ToWorld, Project);
            Assert.Equal(2 * MapFile.mapSize * MapFile.mapSize, prepared.TileCount);
        }

        [Fact]
        public void APointMilesAwayFindsNothing()
        {
            var prepared = FieldTilePicker.Prepared.Build(Grid(1), ToWorld, Project);
            Assert.Null(prepared.Nearest(90000, 90000));
        }

        [Fact]
        public void TheReachIsHonouredRatherThanRoundedToABucket()
        {
            // A tile sits at (0,0) on screen. Asking from 70 pixels away with a reach of 80 must find it
            // even though that is more than one bucket away; asking with a reach of 50 must not.
            var g = new MapCollisionGrid();
            g.Add(0, 0, new byte[MapFile.mapSize, MapFile.mapSize]);
            var prepared = FieldTilePicker.Prepared.Build(g, ToWorld, (x, y, z) => (x * 1000f, z * 1000f));

            Assert.NotNull(prepared.Nearest(70, 0, 80));
            Assert.Null(prepared.Nearest(70, 0, 50));
        }

        [Fact]
        public void ATileTheCameraCannotSeeIsLeftOut()
        {
            // A projection that refuses everything leaves nothing to find, rather than throwing.
            var prepared = FieldTilePicker.Prepared.Build(Grid(1), ToWorld, (x, y, z) => null);
            Assert.Equal(0, prepared.TileCount);
            Assert.Null(prepared.Nearest(0, 0));
        }

        [Fact]
        public void AnEmptyMapPreparesToNothingWithoutBlowingUp()
        {
            var prepared = FieldTilePicker.Prepared.Build(new MapCollisionGrid(), ToWorld, Project);
            Assert.Equal(0, prepared.TileCount);
            Assert.Null(prepared.Nearest(1, 1));

            var nothing = FieldTilePicker.Prepared.Build(null, ToWorld, Project);
            Assert.Equal(0, nothing.TileCount);
        }
    }
}
