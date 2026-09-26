using DSPRE.Models;
using System.Linq;
using Xunit;

namespace DSPRE.Tests.Models
{
    public class TilePainterTests
    {
        private static MapTileset Set(int count = 20)
        {
            var set = new MapTileset();
            for (int i = 0; i < count; i++)
            {
                var tile = new MapTileset.Tile { Name = $"t{i}" };
                if (i == count - 1) { tile.Wide = 2; tile.Deep = 2; }
                float w = tile.Wide * 0.25f, d = tile.Deep * 0.25f;
                foreach (var (x, z) in new[] { (0f, 0f), (w, 0f), (w, d), (0f, d) })
                    tile.Corners.Add(new MapTileset.Corner { X = x, Z = z });
                tile.Faces.Add(new MapTileset.Face { Corners = new[] { 0, 1, 2, 3 }, Picture = "p" });
                set.Tiles.Add(tile);
            }
            return set;
        }

        private static TilePainter Painter(MapTileset set = null)
        {
            var p = new TilePainter();
            p.Load(new TileGrid(), set ?? Set());
            return p;
        }

        private static void Click(TilePainter p, int x, int z, TilePainter.Button b = TilePainter.Button.Left)
        {
            p.Press(x, z, b);
            p.Release(x, z);
        }

        [Fact]
        public void ALargeTileClickedReachesNorthAndEastOfTheSquareAsInMapStudio()
        {
            var p = Painter();
            p.Brush = 19;
            Click(p, 5, 10);

            Assert.Equal(19, p.Grid.PutHere(5, 9));
            Assert.Equal(19, p.Grid.At(5, 10).Tile);
            Assert.Equal(19, p.Grid.At(6, 9).Tile);
            Assert.Equal(-1, p.Grid.At(5, 11).Tile);
        }

        [Fact]
        public void ALargeTileDraggedAlongLaysANeatRow()
        {
            var p = Painter();
            p.Brush = 19;
            p.Press(4, 10, TilePainter.Button.Left);
            for (int x = 4; x <= 11; x++) p.Drag(x, 10);
            p.Release(11, 10);

            var anchors = p.Grid.Placed().Select(q => (q.x, q.z)).OrderBy(a => a.x).ToList();
            Assert.Equal(new[] { (4, 9), (6, 9), (8, 9), (10, 9) }, anchors);
        }

        [Fact]
        public void ARightClickPicksUpTheTileAndItsTurn()
        {
            var p = Painter();
            p.Brush = 3; p.Turn = 2;
            Click(p, 1, 1);
            p.Brush = 7; p.Turn = 0;

            Click(p, 1, 1, TilePainter.Button.Right);
            Assert.Equal(3, p.Brush);
            Assert.Equal(2, p.Turn);
            Assert.False(p.CanRedo);
        }

        [Fact]
        public void TheMiddleButtonFillsAndTheClearToolTakesAway()
        {
            var p = Painter();
            p.Brush = 2;
            Click(p, 0, 0, TilePainter.Button.Middle);
            Assert.Equal(TileGrid.Across * TileGrid.Across, p.Grid.Painted);

            p.Current = TilePainter.Tool.Clear;
            p.Press(3, 3, TilePainter.Button.Left);
            p.Drag(4, 3);
            p.Release(4, 3);
            Assert.Equal(-1, p.Grid.PutHere(3, 3));
            Assert.Equal(-1, p.Grid.PutHere(4, 3));
            Assert.Equal(2, p.Grid.PutHere(5, 3));
        }

        [Fact]
        public void HeightsArePaintedFilledAndPickedUpInTheHeightView()
        {
            var p = Painter();
            p.Heights = true;
            p.HeightBrush = 3;
            p.Press(2, 2, TilePainter.Button.Left);
            p.Drag(3, 2);
            p.Release(3, 2);
            Assert.Equal(3 * TileGrid.Step, p.Grid.HeightAt(2, 2), 5);
            Assert.Equal(3 * TileGrid.Step, p.Grid.HeightAt(3, 2), 5);

            p.HeightBrush = 0;
            Click(p, 2, 2, TilePainter.Button.Right);
            Assert.Equal(3, p.HeightBrush);

            Assert.Equal(0, p.Grid.Painted);
        }

        [Fact]
        public void RaisingPartOfAWidePieceRaisesTheWholePieceOnce()
        {
            var set = Set();
            var p = Painter(set);
            p.Grid.PutTile(4, 4, set.Tiles.Count - 1, 0, 2, 2, 0);
            p.Heights = true;
            p.HeightChange = TilePainter.HeightWay.Raise;
            p.Press(4, 4, TilePainter.Button.Left);
            p.Drag(5, 4);
            p.Drag(5, 5);
            p.Release(5, 5);

            Assert.Equal(TileGrid.Step, p.Grid.At(4, 4).Lift, 5);
            Assert.Equal(TileGrid.Step, p.Grid.At(5, 5).Lift, 5);
        }

        [Fact]
        public void ShapesInTheHeightViewSetHeightsNotTiles()
        {
            var p = Painter();
            p.Brush = 1;
            p.Heights = true;
            p.HeightBrush = 4;
            p.Current = TilePainter.Tool.Rectangle;
            p.Press(2, 2, TilePainter.Button.Left);
            p.Drag(5, 4);
            p.Release(5, 4);
            Assert.Equal(4 * TileGrid.Step, p.Grid.HeightAt(2, 2), 5);
            Assert.Equal(4 * TileGrid.Step, p.Grid.HeightAt(5, 4), 5);
            Assert.Equal(0f, p.Grid.HeightAt(6, 4), 5);
            Assert.Equal(0, p.Grid.Painted);
        }

        [Fact]
        public void FillingASelectionInTheHeightViewSetsItsHeights()
        {
            var p = Painter();
            p.Brush = 1;
            p.Current = TilePainter.Tool.Select;
            p.Press(2, 2, TilePainter.Button.Left);
            p.Drag(4, 3);
            p.Release(4, 3);
            p.Heights = true;
            p.HeightBrush = 5;
            Assert.True(p.FillSelection());
            Assert.Equal(5 * TileGrid.Step, p.Grid.HeightAt(3, 3), 5);
            Assert.Equal(5 * TileGrid.Step, p.Grid.HeightAt(4, 2), 5);
            Assert.Equal(0f, p.Grid.HeightAt(5, 3), 5);
            Assert.Equal(0, p.Grid.Painted);
        }

        [Fact]
        public void HeightsLiftEveryLayerUnlessAskedForOne()
        {
            var p = Painter();
            p.Heights = true;
            p.HeightBrush = 2;
            p.Layer = 0;
            Click(p, 4, 4);
            Assert.Equal(2 * TileGrid.Step, p.Grid.HeightAt(4, 4, 0), 5);
            Assert.Equal(2 * TileGrid.Step, p.Grid.HeightAt(4, 4, 3), 5);

            p.HeightsAllLayers = false;
            p.HeightBrush = 5;
            Click(p, 4, 4);
            Assert.Equal(5 * TileGrid.Step, p.Grid.HeightAt(4, 4, 0), 5);
            Assert.Equal(2 * TileGrid.Step, p.Grid.HeightAt(4, 4, 3), 5);
        }

        [Fact]
        public void EveryStrokeUndoesAndRedoesWhole()
        {
            var p = Painter();
            p.Brush = 1;
            p.Press(0, 0, TilePainter.Button.Left);
            for (int x = 0; x < 6; x++) p.Drag(x, 0);
            p.Release(5, 0);
            Click(p, 10, 10);
            Assert.Equal(7, p.Grid.Painted);

            Assert.True(p.Undo());
            Assert.Equal(6, p.Grid.Painted);
            Assert.True(p.Undo());
            Assert.Equal(0, p.Grid.Painted);
            Assert.False(p.CanUndo);

            Assert.True(p.Redo());
            Assert.Equal(6, p.Grid.Painted);

            Click(p, 0, 0);
            Assert.True(p.CanRedo);
        }

        [Fact]
        public void ARectangleIsItsOutlineWithoutASmartDrawingAndFilledWithOne()
        {
            var p = Painter();
            p.Brush = 1;
            p.Current = TilePainter.Tool.Rectangle;
            p.Press(2, 2, TilePainter.Button.Left);
            p.Drag(5, 5);
            Assert.Equal(12, p.Pending.Count);
            Assert.Equal(0, p.Grid.Painted);
            p.Release(5, 5);
            Assert.Equal(12, p.Grid.Painted);
            Assert.Equal(-1, p.Grid.PutHere(3, 3));

            var set = Set();
            var d = new SmartDrawing();
            for (int slot = 0; slot < 13; slot++) d[slot] = slot;
            set.SmartDrawings.Add(d);
            p = Painter(set);
            p.Brush = 6;
            p.Current = TilePainter.Tool.Rectangle;
            p.Press(2, 2, TilePainter.Button.Left);
            p.Release(5, 4);
            Assert.Equal(0, p.Grid.PutHere(2, 2));
            Assert.Equal(6, p.Grid.PutHere(3, 3));
            Assert.Equal(12, p.Grid.PutHere(5, 4));
        }

        [Fact]
        public void ASmartStrokeFollowsThePointerAndTurnsItsCorners()
        {
            var set = Set();
            var d = new SmartDrawing();
            for (int slot = 0; slot < 13; slot++) d[slot] = slot;
            set.SmartDrawings.Add(d);

            var p = Painter(set);
            p.Brush = 1;
            p.Press(0, 0, TilePainter.Button.Left);
            p.Drag(2, 0);
            p.Drag(2, 2);
            p.Release(2, 2);

            Assert.Equal(1, p.Grid.PutHere(1, 0));
            Assert.Equal(2, p.Grid.PutHere(2, 0));
            Assert.Equal(7, p.Grid.PutHere(2, 2));
            Assert.True(p.CanUndo);

            p.SmartTools = false;
            Click(p, 10, 10);
            Assert.Equal(1, p.Grid.PutHere(10, 10));
        }

        [Fact]
        public void LayersAreShiftedRaisedClearedAndCopiedAsOneStepEach()
        {
            var p = Painter();
            p.Brush = 4;
            p.Layer = 3;
            Click(p, 0, 0);

            Assert.True(p.ShiftLayer(1, 0));
            Assert.Equal(4, p.Grid.PutHere(1, 0, 3));
            Assert.True(p.RaiseLayer(2));
            Assert.Equal(2 * TileGrid.Step, p.Grid.HeightAt(1, 0, 3), 5);

            p.CopyLayer();
            p.Layer = 5;
            Assert.True(p.PasteLayer());
            Assert.Equal(4, p.Grid.PutHere(1, 0, 5));

            p.Layer = 3;
            Assert.True(p.ClearLayer());
            Assert.False(p.Grid.HasAnything(3));

            Assert.True(p.Undo());
            Assert.True(p.Grid.HasAnything(3));
        }
            [Fact]
        public void ARectangleSelectedIsDeletedCopiedAndPastedWhereClicked()
        {
            var p = Painter();
            p.Brush = 1;
            Click(p, 2, 2, TilePainter.Button.Middle);
            p.Brush = 5; p.Turn = 1;
            Click(p, 3, 3);

            p.Current = TilePainter.Tool.Select;
            p.Press(3, 3, TilePainter.Button.Left);
            p.Drag(4, 4);
            Assert.Equal(4, p.Pending.Count);
            p.Release(4, 4);
            Assert.True(p.Selection[3, 3] && p.Selection[4, 4] && !p.Selection[5, 5]);

            p.CopySelection();
            p.StartPaste();
            Assert.True(p.Pasting);
            Assert.Equal(4, p.PasteCells(20, 20).Count);
            Click(p, 20, 20);
            Assert.False(p.Pasting);
            Assert.Equal(5, p.Grid.PutHere(20, 20));
            Assert.Equal(1, p.Grid.At(20, 20).Turn);
            Assert.Equal(1, p.Grid.PutHere(21, 21));

            Assert.True(p.DeleteSelection());
            Assert.Equal(-1, p.Grid.PutHere(3, 3));
            Assert.Equal(-1, p.Grid.PutHere(4, 4));
            Assert.Equal(1, p.Grid.PutHere(5, 5));

            Assert.True(p.Undo());
            Assert.Equal(5, p.Grid.PutHere(3, 3));
            Assert.True(p.Undo());
            Assert.Equal(1, p.Grid.PutHere(20, 20));
        }

        [Fact]
        public void ASelectionDraggedFromInsideMovesWithItsHeights()
        {
            var p = Painter();
            p.Brush = 2;
            Click(p, 1, 1);
            p.Heights = true; p.HeightBrush = 4;
            Click(p, 1, 1);
            p.Heights = false;

            p.Current = TilePainter.Tool.Select;
            Click(p, 1, 1);
            p.Press(1, 1, TilePainter.Button.Left);
            p.Drag(6, 3);
            Assert.Contains((6, 3), p.Pending);
            p.Release(6, 3);

            Assert.Equal(-1, p.Grid.PutHere(1, 1));
            Assert.Equal(0f, p.Grid.HeightAt(1, 1));
            Assert.Equal(2, p.Grid.PutHere(6, 3));
            Assert.Equal(4 * TileGrid.Step, p.Grid.HeightAt(6, 3), 5);
            Assert.True(p.Selection[6, 3] && !p.Selection[1, 1]);
        }

        [Fact]
        public void TheWandPicksAJoinedStretchAndCanAddOrTakeAway()
        {
            var p = Painter();
            p.Brush = 3;
            p.Current = TilePainter.Tool.Rectangle;
            p.Press(0, 0, TilePainter.Button.Left); p.Release(0, 0);
            p.Current = TilePainter.Tool.Paint;
            p.Press(0, 0, TilePainter.Button.Left); p.Drag(1, 0); p.Drag(2, 0); p.Release(2, 0);
            Click(p, 10, 10);

            p.Current = TilePainter.Tool.Wand;
            Click(p, 1, 0);
            int picked = 0;
            for (int x = 0; x < 32; x++) for (int z = 0; z < 32; z++) if (p.Selection[x, z]) picked++;
            Assert.Equal(3, picked);
            Assert.False(p.Selection[10, 10]);

            p.Press(1, 0, TilePainter.Button.Left, shift: true); p.Release(1, 0);
            Assert.True(p.Selection[10, 10]);

            p.Press(1, 0, TilePainter.Button.Left, ctrl: true); p.Release(1, 0);
            Assert.False(p.Selection[0, 0]);
            Assert.True(p.Selection[10, 10]);
        }
            [Fact]
        public void ALassoSelectsWhatItIsDrawnRound()
        {
            var p = Painter();
            p.Current = TilePainter.Tool.Lasso;
            p.Press(2, 2, TilePainter.Button.Left);
            p.Drag(8, 2);
            p.Drag(8, 8);
            p.Drag(2, 8);
            p.Release(2, 8);

            Assert.True(p.Selection[2, 5]);
            Assert.True(p.Selection[5, 5]);
            Assert.True(p.Selection[8, 8]);
            Assert.False(p.Selection[9, 5]);
            Assert.False(p.Selection[1, 1]);
        }
            [Fact]
        public void ASelectionTurnsAQuarterWithItsTilesAndMirrors()
        {
            var p = Painter();
            p.Brush = 19;
            Click(p, 0, 1);
            p.Brush = 3;
            Click(p, 2, 0);
            p.Current = TilePainter.Tool.Select;
            p.Press(0, 0, TilePainter.Button.Left); p.Drag(2, 1); p.Release(2, 1);

            Assert.True(p.TransformSelection(TilePainter.Transform.Rotate));
            Assert.Equal(3, p.Grid.PutHere(1, 2));
            Assert.Equal(19, p.Grid.PutHere(0, 0));
            Assert.Equal(1, p.Grid.At(0, 0).Turn);
            Assert.True(p.Selection[1, 2] && !p.Selection[2, 0]);

            Assert.True(p.TransformSelection(TilePainter.Transform.FlipDown));
            Assert.Equal(3, p.Grid.PutHere(1, 0));
            Assert.Equal(19, p.Grid.PutHere(0, 1));

            Assert.True(p.Undo());
            Assert.True(p.Undo());
            Assert.Equal(3, p.Grid.PutHere(2, 0));
        }

        [Fact]
        public void AFillStaysInsideTheSelectionAndShiftSelectsFromAnyTool()
        {
            var p = Painter();
            p.Brush = 1;
            p.Press(3, 3, TilePainter.Button.Left, shift: true);
            p.Drag(5, 5);
            p.Release(5, 5);
            Assert.Equal(0, p.Grid.Painted);
            Assert.True(p.Selection[4, 4]);

            Click(p, 4, 4, TilePainter.Button.Middle);
            Assert.Equal(9, p.Grid.Painted);
            Click(p, 20, 20, TilePainter.Button.Middle);
            Assert.Equal(9, p.Grid.Painted);

            p.Brush = 2;
            Assert.True(p.FillSelection());
            Assert.Equal(2, p.Grid.PutHere(5, 5));
        }
    }
}
