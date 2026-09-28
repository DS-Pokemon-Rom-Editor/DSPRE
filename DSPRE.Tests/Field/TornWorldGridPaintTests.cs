using DSPRE;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests.Field
{
    public class TornWorldGridPaintTests
    {
        private const int GridBand = 8;

        // Six down the grid, four across it: rows and columns are not interchangeable.
        private static TornWorldFile.FloatingPlatform NarrowFloor() => new TornWorldFile.FloatingPlatform
        {
            Kind = TornWorldFile.PlatformKind.Floor,
            AttributeId = 2,
            Bounds = new TornWorldFile.Bounds { StartX = 10, StartY = 200, StartZ = 40, SizeX = 5, SizeY = 0, SizeZ = 3 },
            TilesVertical = 6,
            TilesHorizontal = 4,
        };

        private static TornWorldSurfaces.Surface SurfaceFor(TornWorldFile.FloatingPlatform platform, ushort[] grid)
        {
            var data = new TornWorldFile();
            data.Platforms.Add(platform);
            var surfaces = TornWorldSurfaces.ForFloor(new TornWorldMapTable.Floor(), data, null, _ => grid, 0, 0, 0, GridBand);
            Assert.Single(surfaces);
            return surfaces[0];
        }

        [Fact]
        public void APaintedCellOfANonSquareGridIsSavedWhereTheGameReadsIt()
        {
            var platform = NarrowFloor();
            var grid = new ushort[6 * 4];
            var surface = SurfaceFor(platform, grid);

            var (columns, rows) = TornWorldSurfaces.PaintedExtent(platform);
            Assert.Equal(6, columns);
            Assert.Equal(4, rows);

            // The last row and column only exist when rows follow the horizontal count.
            var collisions = (byte[,])surface.Collisions.Clone();
            var types = (byte[,])surface.Types.Clone();
            collisions[3, 5] = MapCollisionGrid.BlockedBit;
            types[3, 5] = 0x15;
            types[0, 1] = 0x08;

            TornWorldSurfaces.WriteBack(platform, grid, collisions, types);
            var saved = TornWorldSurfaces.GridToBytes(grid);
            var readBack = TornWorldSurfaces.GridFromBytes(saved);

            var (x, y, z) = surface.WorldAt(5, 3);
            Assert.Equal((ushort)0x8015, TornWorldFile.AttributeAt(platform, readBack, x, y, z));
            var (x1, y1, z1) = surface.WorldAt(1, 0);
            Assert.Equal((ushort)0x0008, TornWorldFile.AttributeAt(platform, readBack, x1, y1, z1));

            var again = SurfaceFor(platform, readBack);
            Assert.False(again.CanWalk(5, 3));
            Assert.Equal(0x15, again.Types[3, 5]);
            Assert.Equal(0x08, again.Types[0, 1]);
            Assert.True(again.CanWalk(4, 3));
        }

        [Fact]
        public void CellsPastTheGridStayBlockedAndAreNeverWritten()
        {
            var platform = NarrowFloor();
            var grid = new ushort[6 * 4];
            var surface = SurfaceFor(platform, grid);

            Assert.False(surface.CanWalk(6, 0));
            Assert.False(surface.CanWalk(0, 4));

            var collisions = (byte[,])surface.Collisions.Clone();
            var types = (byte[,])surface.Types.Clone();
            collisions[4, 0] = 0;
            types[4, 0] = 0x33;
            TornWorldSurfaces.WriteBack(platform, grid, collisions, types);

            Assert.All(grid, cell => Assert.Equal(0, cell));
        }

        [Fact]
        public void ResizingAGridKeepsWhatStillFitsAndBlocksTheRest()
        {
            var grid = new ushort[3 * 2];
            for (int h = 0; h < 2; h++)
                for (int v = 0; v < 3; v++)
                    grid[v + h * 3] = (ushort)(v * 10 + h);

            var resized = TornWorldSurfaces.ResizeGrid(grid, 3, 2, 2, 3);

            Assert.Equal(6, resized.Length);
            Assert.Equal((ushort)0, resized[0 + 0 * 2]);
            Assert.Equal((ushort)10, resized[1 + 0 * 2]);
            Assert.Equal((ushort)1, resized[0 + 1 * 2]);
            Assert.Equal((ushort)11, resized[1 + 1 * 2]);
            Assert.Equal((ushort)0x8000, resized[0 + 2 * 2]);
            Assert.Equal((ushort)0x8000, resized[1 + 2 * 2]);
        }
    }
}
