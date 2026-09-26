using DSPRE.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace DSPRE.Tests.Models
{
    public class SmartDrawingTests
    {
        private const int Wall = 50;

        private static SmartDrawing Numbered()
        {
            var d = new SmartDrawing();
            for (int slot = 0; slot < 13; slot++) d[slot] = 100 + slot;
            return d;
        }

        private static TileGrid Walled(int x0, int z0, int x1, int z1)
        {
            var grid = new TileGrid();
            for (int x = x0 - 1; x <= x1 + 1; x++) { grid.PutTile(x, z0 - 1, Wall, 0); grid.PutTile(x, z1 + 1, Wall, 0); }
            for (int z = z0; z <= z1; z++) { grid.PutTile(x0 - 1, z, Wall, 0); grid.PutTile(x1 + 1, z, Wall, 0); }
            return grid;
        }

        private static int Slot(TileGrid grid, int x, int z, int layer = 0) => grid.PutHere(x, z, layer) - 100;

        [Fact]
        public void AFilledPatchGetsItsCornersEdgesAndMiddleWithNorthAtTheTop()
        {
            var grid = Walled(2, 2, 5, 4);
            int written = Numbered().Fill(grid, null, 3, 3, 0, insideOut: false);
            Assert.Equal(12, written);

            Assert.Equal(0, Slot(grid, 2, 2));  Assert.Equal(1, Slot(grid, 3, 2));  Assert.Equal(2, Slot(grid, 5, 2));
            Assert.Equal(5, Slot(grid, 2, 3));  Assert.Equal(6, Slot(grid, 4, 3));  Assert.Equal(7, Slot(grid, 5, 3));
            Assert.Equal(10, Slot(grid, 2, 4)); Assert.Equal(11, Slot(grid, 4, 4)); Assert.Equal(12, Slot(grid, 5, 4));

            Assert.Equal(Wall, grid.PutHere(1, 1));
            Assert.Equal(Wall, grid.PutHere(6, 3));
        }

        [Fact]
        public void TheOtherWayOutTakesMapStudiosSecondTable()
        {
            var grid = Walled(2, 2, 5, 4);
            Numbered().Fill(grid, null, 3, 3, 0, insideOut: true);

            Assert.Equal(3, Slot(grid, 2, 2));
            Assert.Equal(11, Slot(grid, 3, 2));
            Assert.Equal(6, Slot(grid, 4, 3));
        }

        [Fact]
        public void ASquareWhoseEdgesAllBelongIsPickedByItsCorners()
        {
            int n = TileGrid.Across;
            var mask = new bool[n, n];
            for (int x = 0; x < 4; x++) for (int z = 0; z < 4; z++) mask[x, z] = true;
            mask[3, 3] = false;

            var piece = Numbered().ResolveMask(mask, false);
            Assert.Equal(103, piece[2, 2]);
            Assert.Equal(106, piece[1, 1]);
            Assert.Equal(-1, piece[3, 3]);
        }

        [Fact]
        public void AFillOnlyReachesSquaresJoinedEdgeToEdge()
        {
            var grid = Walled(2, 2, 5, 4);
            Numbered().Fill(grid, null, 3, 3, 0, false);
            Assert.Equal(-1, grid.PutHere(20, 20));
            Assert.Equal(-1, grid.PutHere(10, 10));
        }

        [Fact]
        public void AFillStopsAtWhereALargeTileReaches()
        {
            var grid = Walled(2, 2, 5, 4);
            grid.PutTile(4, 3, 9, 0, 2, 2);

            Numbered().Fill(grid, null, 2, 2, 0, false);

            Assert.Equal(9, grid.PutHere(4, 3));
            Assert.Equal(-1, grid.PutHere(5, 4));
            Assert.Equal(9, grid.At(5, 4).Tile);
        }

        [Fact]
        public void AFillLeavesHeightsAlone()
        {
            var grid = Walled(2, 2, 5, 4);
            grid.SetHeight(3, 3, 2 * TileGrid.Step);
            Numbered().Fill(grid, null, 3, 3, 0, false);
            Assert.Equal(2 * TileGrid.Step, grid.HeightAt(3, 3), 5);
        }

        [Fact]
        public void APathKeepsItsInsideOnTheSideItStartedOnThroughATurn()
        {
            var d = Numbered();
            var cells = new List<(int x, int z)> { (0, 0), (1, 0), (2, 0), (2, 1), (2, 2) };

            var piece = d.ResolvePath(cells, 101, false);
            Assert.Equal(101, piece[0, 0]);
            Assert.Equal(101, piece[1, 0]);
            Assert.Equal(102, piece[2, 0]);
            Assert.Equal(107, piece[2, 1]);
            Assert.Equal(107, piece[2, 2]);
            Assert.Equal(-1, piece[3, 0]);
        }

        [Fact]
        public void APathTurningAwayFromItsInsideTakesAnInnerCorner()
        {
            var d = Numbered();

            var cells = new List<(int x, int z)> { (0, 5), (1, 5), (2, 5), (2, 4) };
            var piece = d.ResolvePath(cells, 101, false);
            Assert.Equal(109, piece[2, 5]);

            int n = TileGrid.Across;
            var mask = new bool[n, n];
            for (int x = 0; x < 6; x++) for (int z = 0; z < 6; z++) mask[x, z] = !(x < 3 && z < 3);
            Assert.Equal(109, d.ResolveMask(mask, false)[3, 3]);
        }

        [Fact]
        public void APathOfTheMiddlePieceIsTheMiddleAllAlong()
        {
            var piece = Numbered().ResolvePath(new List<(int, int)> { (0, 0), (1, 0), (1, 1) }, 106, false);
            Assert.Equal(106, piece[0, 0]);
            Assert.Equal(106, piece[1, 0]);
            Assert.Equal(106, piece[1, 1]);
        }

        [Fact]
        public void ADrawnShapeRedoesTheEdgesOfThePatchItJoins()
        {
            var grid = new TileGrid();
            var d = Numbered();
            d.DrawShape(grid, null, TileShapes.RectangleFilled((2, 2), (4, 4)), 0, false);
            Assert.Equal(7, Slot(grid, 4, 3));

            d.DrawShape(grid, null, TileShapes.RectangleFilled((5, 2), (7, 4)), 0, false);

            Assert.Equal(6, Slot(grid, 4, 3));
            Assert.Equal(7, Slot(grid, 7, 3));
            Assert.Equal(1, Slot(grid, 4, 2));
        }

        [Fact]
        public void ADrawingWithEmptySlotsNeverWritesNothingOverADrawnShape()
        {
            var grid = new TileGrid();
            grid.PutTile(3, 3, Wall, 0);
            var d = new SmartDrawing { [SmartDrawing.Middle] = 200 };

            d.DrawShape(grid, null, TileShapes.RectangleFilled((2, 2), (4, 4)), 0, false);

            Assert.Equal(200, grid.PutHere(3, 3));
            Assert.Equal(-1, grid.PutHere(2, 2));
        }

        [Fact]
        public void ALineWithNoDiagonalStepsHasEveryPairOfSquaresSharingAnEdge()
        {
            var line = TileShapes.EdgeToEdgeLine((0, 0), (7, 3));
            Assert.Equal((0, 0), line[0]);
            Assert.Equal((7, 3), line[^1]);
            for (int i = 1; i < line.Count; i++)
                Assert.Equal(1, Math.Abs(line[i].x - line[i - 1].x) + Math.Abs(line[i].z - line[i - 1].z));
        }

        [Fact]
        public void AStrokeDraggedFastHasNoGaps()
        {
            var stroke = new List<(int x, int z)> { (0, 0) };
            TileShapes.Extend(stroke, (5, 0));
            TileShapes.Extend(stroke, (5, 0));
            Assert.Equal(6, stroke.Count);
            Assert.Equal((5, 0), stroke[^1]);
        }

        [Fact]
        public void TheShapesCoverWhatMapStudiosDo()
        {
            Assert.Equal(16, TileShapes.RectangleFilled((1, 1), (4, 4)).Count);
            Assert.Equal(12, TileShapes.RectangleOutline((1, 1), (4, 4)).Count);
            Assert.Single(TileShapes.RectangleOutline((3, 3), (3, 3)));

            var fill = TileShapes.EllipseFilled((0, 0), (9, 5));
            var edge = TileShapes.EllipseOutline((0, 0), (9, 5));
            Assert.All(edge, c => Assert.Contains(c, fill));
            Assert.True(edge.Count < fill.Count);
            Assert.DoesNotContain((0, 0), fill);
            Assert.Contains((4, 2), fill);
        }

        [Fact]
        public void ADrawingIsKeptWithTheSetWrittenOutAndBroughtBack()
        {
            var set = new MapTileset();
            foreach (string name in new[] { "grass", "edge", "corner" })
            {
                var tile = new MapTileset.Tile { Name = name };
                foreach (var (x, z) in new[] { (0f, 0f), (0.25f, 0f), (0.25f, 0.25f), (0f, 0.25f) })
                    tile.Corners.Add(new MapTileset.Corner { X = x, Z = z });
                tile.Faces.Add(new MapTileset.Face { Corners = new[] { 0, 1, 2, 3 }, Picture = "p" });
                set.Tiles.Add(tile);
            }
            var drawing = new SmartDrawing { [0] = 2, [1] = 1, [SmartDrawing.Middle] = 0 };
            set.SmartDrawings.Add(drawing);

            string dir = Path.Combine(Path.GetTempPath(), "dspre-smart-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string path = Path.Combine(dir, "set.obj");
                Assert.Null(set.SaveObj(path));

                var back = MapTileset.FromObj(path, out string whynot);
                Assert.True(back != null, whynot);
                var again = Assert.Single(back.SmartDrawings);
                for (int slot = 0; slot < SmartDrawing.Slots; slot++)
                    Assert.Equal(drawing[slot], again[slot]);
            }
            finally { Directory.Delete(dir, true); }
        }
    }
}
