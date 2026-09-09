using System.Collections.Generic;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// Reading the tile under a point when several maps are stitched together, which is what the event
    /// editor's coordinate readout asks. Local is the tile within its own map, global is that tile's
    /// absolute position, cell times 32 plus local, the way an event stores it.
    /// </summary>
    public class StitchedTileAtScreenTests
    {
        private const int Tiles = 32;
        private const float CellSize = 8f;   // raw world units per map, as the scene stitches them
        private const float Zoom = 4f;       // screen pixels per raw unit

        /// <summary>A plain top-down view, so a raw point lands somewhere predictable on screen.</summary>
        private static (bool, float, float) Project(float rawX, float rawZ) => (true, rawX * Zoom, rawZ * Zoom);

        private static List<FieldTilePicker.CellQuad> Grid(int wide, int high)
        {
            var cells = new List<FieldTilePicker.CellQuad>();
            for (int y = 0; y < high; y++)
                for (int x = 0; x < wide; x++)
                    cells.Add(new FieldTilePicker.CellQuad(x, y, x * CellSize, y * CellSize, CellSize, CellSize));
            return cells;
        }

        /// <summary>Screen position of one tile's centre, so a query can be aimed at a known tile.</summary>
        private static (double px, double py) TileCentre(int cellX, int cellY, int col, int row)
        {
            float t = CellSize / Tiles;
            return ((cellX * CellSize + (col + 0.5f) * t) * Zoom,
                    (cellY * CellSize + (row + 0.5f) * t) * Zoom);
        }

        [Theory]
        [InlineData(0, 0, 0, 0)]
        [InlineData(0, 0, 31, 31)]
        [InlineData(2, 1, 5, 7)]
        [InlineData(3, 3, 17, 2)]
        public void ThePointerLandsOnTheTileItIsOver(int cellX, int cellY, int col, int row)
        {
            var cells = Grid(4, 4);
            var (px, py) = TileCentre(cellX, cellY, col, row);

            Assert.True(FieldTilePicker.TileAtScreen(cells, Project, px, py,
                out int gotCellX, out int gotCellY, out int gotCol, out int gotRow, Tiles));

            Assert.Equal(cellX, gotCellX);
            Assert.Equal(cellY, gotCellY);
            Assert.Equal(col, gotCol);
            Assert.Equal(row, gotRow);
        }

        [Fact]
        public void TheGlobalTileIsTheCellTimesThirtyTwoPlusTheLocalOne()
        {
            var cells = Grid(4, 4);
            var (px, py) = TileCentre(2, 1, 5, 7);

            Assert.True(FieldTilePicker.TileAtScreen(cells, Project, px, py,
                out int cellX, out int cellY, out int col, out int row, Tiles));

            Assert.Equal(2 * Tiles + 5, cellX * Tiles + col);
            Assert.Equal(1 * Tiles + 7, cellY * Tiles + row);
        }

        [Fact]
        public void JustOffTheEdgeOfAMapIsOverNothing()
        {
            // A tile is CellSize/32 * Zoom = 1 pixel across here, so a few pixels past the edge is
            // several tiles out and must not be claimed as the nearest edge tile.
            var cells = Grid(1, 1);
            var (px, py) = TileCentre(0, 0, 31, 31);

            Assert.False(FieldTilePicker.TileAtScreen(cells, Project, px, py + 20,
                out _, out _, out _, out _, Tiles));
        }

        [Fact]
        public void APointWellAwayFromEveryMapIsOverNothing()
        {
            var cells = Grid(2, 2);
            Assert.False(FieldTilePicker.TileAtScreen(cells, Project, 100000, 100000,
                out _, out _, out _, out _, Tiles));
        }

        [Fact]
        public void ACellStretchedToADifferentSizeStillReadsRight()
        {
            // Continuous stitching gives a map whatever footprint its geometry needs, so the tile size
            // is per cell rather than one figure for the whole matrix.
            var cells = new List<FieldTilePicker.CellQuad>
            {
                new FieldTilePicker.CellQuad(0, 0, 0f, 0f, CellSize, CellSize),
                new FieldTilePicker.CellQuad(1, 0, CellSize, 0f, CellSize * 3f, CellSize),
            };

            float wideTile = CellSize * 3f / Tiles;
            double px = (CellSize + (10 + 0.5f) * wideTile) * Zoom;
            double py = ((4 + 0.5f) * (CellSize / Tiles)) * Zoom;

            Assert.True(FieldTilePicker.TileAtScreen(cells, Project, px, py,
                out int cellX, out int cellY, out int col, out int row, Tiles));

            Assert.Equal(1, cellX);
            Assert.Equal(0, cellY);
            Assert.Equal(10, col);
            Assert.Equal(4, row);
        }

        [Fact]
        public void ACellTheCameraCannotSeeIsSkippedRatherThanGuessed()
        {
            var cells = Grid(2, 1);
            // The second map is behind the camera, so only the first one can be landed on.
            (bool, float, float) OneVisible(float rawX, float rawZ)
                => rawX >= CellSize ? (false, 0f, 0f) : (true, rawX * Zoom, rawZ * Zoom);

            var (px, py) = TileCentre(0, 0, 3, 3);
            Assert.True(FieldTilePicker.TileAtScreen(cells, OneVisible, px, py,
                out int cellX, out _, out int col, out int row, Tiles));
            Assert.Equal(0, cellX);
            Assert.Equal(3, col);
            Assert.Equal(3, row);
        }
    }
}
