using DSPRE.Models;
using System;
using System.IO;
using Xunit;

namespace DSPRE.Tests.Models
{
    public class TileCollisionsTests
    {
        private static MapTileset Set(int count, int wide = 1, int deep = 1)
        {
            var set = new MapTileset();
            for (int i = 0; i < count; i++)
            {
                var tile = new MapTileset.Tile { Name = $"t{i}", Wide = wide, Deep = deep };
                tile.Corners.Add(new MapTileset.Corner());
                tile.Corners.Add(new MapTileset.Corner { X = 0.25f });
                tile.Corners.Add(new MapTileset.Corner { Z = 0.25f });
                tile.Faces.Add(new MapTileset.Face { Corners = new[] { 0, 1, 2 }, Picture = "p" });
                set.Tiles.Add(tile);
            }
            return set;
        }

        [Fact]
        public void TheSidecarIsReadInBothItsForms()
        {
            string dir = Path.Combine(Path.GetTempPath(), "dspre-meta-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string tileset = Path.Combine(dir, "set.pdsts");
                File.WriteAllText(tileset + ".meta",
                    "# Pokemon DS Map Studio tile metadata v7\n"
                  + "tile|0|grass||-1|0|0|0|0|0|-1|0:02,1:00\n"
                  + "tile|2|rock||-1|0|0|1|2|0|1|1,0,0,80;1,0,1,80;0,0,1,10\n"
                  + "tile|9|nothing||-1|0|0|0|0|0|-1|0:05\n");

                var set = Set(3);
                Assert.Equal(2, TileCollisions.ReadMeta(tileset, set));

                Assert.Equal(0x02, set.Tiles[0].WholeCollision(0));
                Assert.Equal(0x00, set.Tiles[0].WholeCollision(1));
                Assert.Equal(-1, set.Tiles[1].WholeCollision(0));

                var rock = set.Tiles[2];
                Assert.Equal((1, 2), (rock.FootprintWide, rock.FootprintDeep));
                Assert.Equal(0x80, rock.WholeCollision(1));
                Assert.Equal(0x10, rock.CollisionDefaults[0][0, 1]);
                Assert.Equal(-1, rock.CollisionDefaults[0][0, 0]);
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void WrittenCollisionIsReadBackAndOtherLinesSurvive()
        {
            string dir = Path.Combine(Path.GetTempPath(), "dspre-meta-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string tileset = Path.Combine(dir, "set.pdsts");
                File.WriteAllText(tileset + ".meta",
                    "# Pokemon DS Map Studio tile metadata v6\n"
                  + "folder|Nature|4|2|0|0|1|0|0\n"
                  + "tile|0|grass|Nature|3|2|2|\n"
                  + "membership|0|Nature|3\n"
                  + "tile|1|rock%7Cbig|Nature|5|0|0|0|0|0|-1|1:80\n");

                var set = Set(3, wide: 2, deep: 2);
                TileCollisions.ReadMeta(tileset, set);
                set.Tiles[0].CollisionGrid(TileCollisions.CollisionLayer)[1, 0] = 0x80;
                set.Tiles[0].CollisionGrid(TileCollisions.TypeLayer)[0, 1] = 0x02;
                set.Tiles[1].CollisionDefaults.Clear();
                set.Tiles[2].CollisionGrid(TileCollisions.CollisionLayer)[0, 0] = 0x80;

                var places = TileCollisions.ListedPlaces(set);
                int written = TileCollisions.WriteMeta(tileset,
                    new[] { (set.Tiles[0], places[set.Tiles[0]]), (set.Tiles[1], places[set.Tiles[1]]), (set.Tiles[2], places[set.Tiles[2]]) },
                    out string whynot);
                Assert.Null(whynot);
                Assert.Equal(3, written);

                var lines = File.ReadAllLines(tileset + ".meta");
                Assert.Equal(TileCollisions.MetaHeader, lines[0]);
                Assert.Contains("folder|Nature|4|2|0|0|1|0|0", lines);
                Assert.Contains("membership|0|Nature|3", lines);
                Assert.Contains("tile|0|grass|Nature|3|2|2|0|0|0|-1|0,0,1,02;1,1,0,80", lines);
                Assert.Contains("tile|1|rock%7Cbig|Nature|5|0|0|0|0|0|-1|", lines);
                Assert.Contains("tile|2|||-1|0|0|0|0|0|-1|1,0,0,80", lines);

                var again = Set(3, wide: 2, deep: 2);
                Assert.Equal(2, TileCollisions.ReadMeta(tileset, again));
                Assert.Equal(0x80, again.Tiles[0].CollisionDefaults[TileCollisions.CollisionLayer][1, 0]);
                Assert.Equal(-1, again.Tiles[0].CollisionDefaults[TileCollisions.CollisionLayer][0, 0]);
                Assert.Equal(0x02, again.Tiles[0].CollisionDefaults[TileCollisions.TypeLayer][0, 1]);
                Assert.Empty(again.Tiles[1].CollisionDefaults);
                Assert.Equal(0x80, again.Tiles[2].CollisionDefaults[TileCollisions.CollisionLayer][0, 0]);
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void TilesAreStampedUnderWhereTheyArePaintedAndHigherLayersWin()
        {
            var set = Set(2);
            set.Tiles[0].SetWholeCollision(TileCollisions.TypeLayer, 0x02);
            set.Tiles[0].SetWholeCollision(TileCollisions.CollisionLayer, 0x00);
            set.Tiles[1].SetWholeCollision(TileCollisions.CollisionLayer, 0x80);

            var grid = new TileGrid();
            grid.PutTile(3, 4, 0, 0, layer: 0);
            grid.PutTile(5, 6, 0, 0, layer: 0);
            grid.PutTile(5, 6, 1, 0, layer: 2);

            var types = new byte[32, 32];
            var collisions = new byte[32, 32];
            types[10, 10] = 0x33;
            int changed = TileCollisions.Apply(grid, set, types, collisions);

            Assert.Equal(0x02, types[4, 3]);
            Assert.Equal(0x02, types[6, 5]);
            Assert.Equal(0x80, collisions[6, 5]);
            Assert.Equal(0x33, types[10, 10]);
            Assert.Equal(3, changed);
        }

        [Fact]
        public void TilesLearnThePermissionsTheyMostlyHaveAndMixedOnesLearnNothing()
        {
            var set = Set(2);
            var grid = new TileGrid();
            var types = new byte[32, 32];
            var collisions = new byte[32, 32];
            for (int x = 0; x < 3; x++) { grid.PutTile(x, 0, 0, 0, layer: 0); types[0, x] = 0x02; collisions[0, x] = 0x80; }
            types[0, 2] = 0x03;
            for (int x = 0; x < 2; x++) grid.PutTile(x, 5, 1, 0, layer: 0);
            collisions[5, 0] = 0x80;
            types[5, 0] = 0x04; types[5, 1] = 0x05;

            Assert.Equal(1, TileCollisions.Learn(grid, set, types, collisions));
            Assert.Equal(0x02, set.Tiles[0].WholeCollision(TileCollisions.TypeLayer));
            Assert.Equal(0x80, set.Tiles[0].WholeCollision(TileCollisions.CollisionLayer));
            Assert.Equal(-1, set.Tiles[1].WholeCollision(TileCollisions.TypeLayer));
            Assert.Equal(-1, set.Tiles[1].WholeCollision(TileCollisions.CollisionLayer));
        }

        [Fact]
        public void WideTilesLearnEachSquareOnItsOwn()
        {
            var set = Set(1, wide: 2);
            var grid = new TileGrid();
            var types = new byte[32, 32];
            var collisions = new byte[32, 32];
            foreach (int z in new[] { 0, 3 })
            {
                grid.PutTile(0, z, 0, 0, 2, 1, 0);
                collisions[z, 0] = 0x80;
            }

            Assert.Equal(1, TileCollisions.Learn(grid, set, types, collisions));

            var placed = new TileGrid();
            placed.PutTile(0, 10, 0, 0, 2, 1, 0);
            var stamped = new byte[32, 32];
            stamped[10, 1] = 0x80;
            TileCollisions.Apply(placed, set, new byte[32, 32], stamped);
            Assert.Equal(0x80, stamped[10, 0]);
            Assert.Equal(0x00, stamped[10, 1]);
        }

        [Fact]
        public void OnlyTheSquaresAskedForAreStamped()
        {
            var set = Set(1);
            set.Tiles[0].SetWholeCollision(TileCollisions.CollisionLayer, 0x80);
            var grid = new TileGrid();
            grid.PutTile(1, 1, 0, 0, layer: 0);
            grid.PutTile(2, 1, 0, 0, layer: 0);
            var collisions = new byte[32, 32];

            TileCollisions.Apply(grid, set, new byte[32, 32], collisions, (x, z) => x == 2);

            Assert.Equal(0x00, collisions[1, 1]);
            Assert.Equal(0x80, collisions[1, 2]);
        }

        [Fact]
        public void AFootprintReachesFromItsAnchorAndTurnsWithTheTile()
        {
            var set = Set(1, 1, 2);
            var tile = set.Tiles[0];
            var grid2 = tile.CollisionGrid(TileCollisions.CollisionLayer);
            grid2[0, 0] = 0x80;
            grid2[0, 1] = 0x00;

            var grid = new TileGrid();
            grid.PutTile(10, 10, 0, 0, 1, 2);
            var collisions = new byte[32, 32];
            for (int z = 0; z < 32; z++) for (int x = 0; x < 32; x++) collisions[z, x] = 0x55;
            TileCollisions.Apply(grid, set, new byte[32, 32], collisions);
            Assert.Equal(0x80, collisions[10, 10]);
            Assert.Equal(0x00, collisions[11, 10]);

            grid = new TileGrid();
            grid.PutTile(10, 10, 0, 1, 1, 2);
            for (int z = 0; z < 32; z++) for (int x = 0; x < 32; x++) collisions[z, x] = 0x55;
            TileCollisions.Apply(grid, set, new byte[32, 32], collisions);
            Assert.Equal(0x80, collisions[10, 11]);
            Assert.Equal(0x00, collisions[10, 10]);
        }

        [Fact]
        public void CollisionComesBackWithASavedSet()
        {
            var set = Set(1);
            set.Tiles[0].SetWholeCollision(TileCollisions.CollisionLayer, 0x80);
            string dir = Path.Combine(Path.GetTempPath(), "dspre-coll-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string path = Path.Combine(dir, "set.obj");
                Assert.Null(set.SaveObj(path));
                var back = MapTileset.FromObj(path, out string whynot);
                Assert.True(back != null, whynot);
                Assert.Equal(0x80, back.Tiles[0].WholeCollision(TileCollisions.CollisionLayer));
            }
            finally { Directory.Delete(dir, true); }
        }
    }
}
