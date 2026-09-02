using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>Choosing where a walk starts. </summary>
    public class FieldStartPlacementTests
    {
        private static MapCollisionGrid GridWith(params (int cellX, int cellY)[] cells)
        {
            var g = new MapCollisionGrid();
            foreach (var c in cells) g.Add(c.cellX, c.cellY, new byte[MapFile.mapSize, MapFile.mapSize]);
            return g;
        }

        [Fact]
        public void OneMapOffersEveryOneOfItsTiles()
        {
            var g = GridWith((0, 0));
            var tiles = g.Tiles.ToList();

            Assert.Equal(MapFile.mapSize * MapFile.mapSize, tiles.Count);
            Assert.Equal(tiles.Count, tiles.Distinct().Count());
            Assert.Contains((0, 0), tiles);
            Assert.Contains((MapFile.mapSize - 1, MapFile.mapSize - 1), tiles);
        }

        [Fact]
        public void TilesAreCountedFromWhereTheMapSitsInTheMatrix()
        {
            var g = GridWith((2, 3));
            var tiles = g.Tiles.ToList();

            int baseX = 2 * MapFile.mapSize, baseZ = 3 * MapFile.mapSize;
            Assert.Contains((baseX, baseZ), tiles);
            Assert.Contains((baseX + MapFile.mapSize - 1, baseZ + MapFile.mapSize - 1), tiles);
            Assert.DoesNotContain((0, 0), tiles);
        }

        [Fact]
        public void SeveralMapsAllShowUpAndNoneOverlap()
        {
            var g = GridWith((0, 0), (1, 0), (0, 1));
            var tiles = g.Tiles.ToList();

            Assert.Equal(3 * MapFile.mapSize * MapFile.mapSize, tiles.Count);
            Assert.Equal(tiles.Count, tiles.Distinct().Count());
        }

        [Fact]
        public void AnEmptyGridOffersNothingRatherThanBlowingUp()
            => Assert.Empty(new MapCollisionGrid().Tiles);

        [Fact]
        public void AnEventsTileIsItsMatrixCellPlusItsPlaceInTheMap()
        {
            var ow = new Overworld(0, 2, 1) { xMapPosition = 5, yMapPosition = 7 };

            Assert.Equal(2 * MapFile.mapSize + 5, FieldInteraction.TileX(ow));
            Assert.Equal(1 * MapFile.mapSize + 7, FieldInteraction.TileZ(ow));
        }

        [Fact]
        public void EveryKindOfEventGivesUpItsTileTheSameWay()
        {
            // Triggers, warps and spawnables all carry the same position fields, so starting next to one
            // works no matter which kind it is.
            var trigger = new Trigger(1, 0) { xMapPosition = 3, yMapPosition = 4 };
            var warp = new Warp(0, 2) { xMapPosition = 9, yMapPosition = 1 };
            var spawn = new Spawnable(3, 3) { xMapPosition = 0, yMapPosition = 0 };

            Assert.Equal(MapFile.mapSize + 3, FieldInteraction.TileX(trigger));
            Assert.Equal(4, FieldInteraction.TileZ(trigger));
            Assert.Equal(9, FieldInteraction.TileX(warp));
            Assert.Equal(2 * MapFile.mapSize + 1, FieldInteraction.TileZ(warp));
            Assert.Equal(3 * MapFile.mapSize, FieldInteraction.TileX(spawn));
            Assert.Equal(3 * MapFile.mapSize, FieldInteraction.TileZ(spawn));
        }

        [Fact]
        public void APlayerDroppedOnATileStartsThereFacingThatWay()
        {
            var g = GridWith((0, 0));
            var p = new FieldPlayer(12, 9, MoveFacing.Left, g);

            Assert.Equal(12, p.TileX);
            Assert.Equal(9, p.TileZ);
            Assert.Equal(MoveFacing.Left, p.Facing);
        }
    }
}
