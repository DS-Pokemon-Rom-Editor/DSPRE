using DSPRE.Models;
using System.Linq;
using Xunit;

namespace DSPRE.Tests.Models
{
    public class TilesetEditingTests
    {
        private static MapTileset Set(int count)
        {
            var set = new MapTileset();
            for (int i = 0; i < count; i++)
            {
                var tile = new MapTileset.Tile { Name = $"t{i}" };
                foreach (var (x, z) in new[] { (0f, 0f), (0.25f, 0f), (0.25f, 0.125f), (0f, 0.125f) })
                    tile.Corners.Add(new MapTileset.Corner { X = x, Z = z, Faces = true, NX = 1f });
                tile.Faces.Add(new MapTileset.Face { Corners = new[] { 0, 1, 2, 3 }, Picture = i % 2 == 0 ? "even" : "odd" });
                set.Tiles.Add(tile);
            }
            return set;
        }

        [Fact]
        public void TakingATileOutRenumbersTheGridAndTheDrawingsAfterIt()
        {
            var set = Set(4);
            var grid = new TileGrid();
            for (int t = 0; t < 4; t++) grid.PutTile(t, 0, t, 0);
            var drawing = new SmartDrawing { [0] = 1, [1] = 3 };
            set.SmartDrawings.Add(drawing);

            set.RemoveTile(1, grid);

            Assert.Equal(3, set.Tiles.Count);
            Assert.Equal(new[] { 0, -1, 1, 2 }, Enumerable.Range(0, 4).Select(x => grid.PutHere(x, 0)));
            Assert.Equal(-1, drawing[0]);
            Assert.Equal(2, drawing[1]);
        }

        [Fact]
        public void SwappingTwoTilesSwapsWhatIsPaintedWithThem()
        {
            var set = Set(3);
            var grid = new TileGrid();
            grid.PutTile(0, 0, 0, 0);
            grid.PutTile(1, 0, 2, 0);
            set.SwapTiles(0, 2, grid);
            Assert.Equal("t2", set.Tiles[0].Name);
            Assert.Equal(2, grid.PutHere(0, 0));
            Assert.Equal(0, grid.PutHere(1, 0));
        }

        [Fact]
        public void AnotherSetAddedAfterKeepsItsDrawingsPointingAtItsOwnTiles()
        {
            var set = Set(2);
            var other = Set(3);
            other.SmartDrawings.Add(new SmartDrawing { [6] = 2 });

            int first = set.Append(other);
            Assert.Equal(2, first);
            Assert.Equal(5, set.Tiles.Count);
            Assert.Equal(4, set.SmartDrawings.Single()[6]);
            Assert.Equal(5, set.Tiles.Select(t => t.Name).Distinct().Count());
        }

        [Fact]
        public void ATurnedShapeStaysOnItsSquaresAndFacesTurnWithIt()
        {
            var tile = Set(1).Tiles[0];
            tile.Wide = 2; tile.Deep = 1;
            MapTileset.TurnShape(tile);

            Assert.Equal((1, 2), (tile.Wide, tile.Deep));
            Assert.All(tile.Corners, c => Assert.InRange(c.X, -1e-5f, 0.25f + 1e-5f));
            Assert.All(tile.Corners, c => Assert.InRange(c.Z, -1e-5f, 0.5f + 1e-5f));
            Assert.All(tile.Corners, c => { Assert.Equal(0f, c.NX, 5); Assert.Equal(1f, c.NZ, 5); });

            for (int i = 0; i < 3; i++) MapTileset.TurnShape(tile);
            Assert.Equal((2, 1), (tile.Wide, tile.Deep));
            Assert.Equal(0.25f, tile.Corners[1].X, 5);
        }

        [Fact]
        public void AMirroredShapeIsWoundTheOtherWaySoItStillFacesOut()
        {
            var tile = Set(1).Tiles[0];
            var before = tile.Faces[0].Corners.ToArray();
            MapTileset.Mirror(tile);
            Assert.Equal(before.Reverse(), tile.Faces[0].Corners);
            Assert.Equal(0.25f, tile.Corners[0].X, 5);
            Assert.Equal(-1f, tile.Corners[0].NX, 5);
        }

        [Fact]
        public void ALookIsChangedForEveryFacePaintedWithThePicture()
        {
            var set = Set(4);
            set.ChangeLook("odd", l => l.With(alpha: 12));
            Assert.All(set.Tiles.Where((t, i) => i % 2 == 1), t => Assert.Equal(12, t.Faces[0].Look.Alpha));
            Assert.All(set.Tiles.Where((t, i) => i % 2 == 0), t => Assert.Null(t.Faces[0].Look));
        }

        [Fact]
        public void ATileMadeBiggerGrowsNorthAndEastFromItsSquareAsInMapStudio()
        {
            var grid = new TileGrid();
            grid.PutTile(4, 10, 0, 0);
            grid.PutTile(31, 0, 0, 0);
            Assert.Equal(2, grid.Resized(0, 2, 2));

            Assert.Equal(0, grid.At(4, 9).Tile);
            Assert.Equal(0, grid.At(5, 10).Tile);
            Assert.Equal(-1, grid.At(4, 11).Tile);

            var corner = grid.At(31, 0);
            Assert.Equal(0, corner.Tile);
            Assert.Equal((1, 2, 1, 2), (corner.Wide, corner.FullWide, corner.PastNorth, corner.FullDeep));
        }
    }
}
