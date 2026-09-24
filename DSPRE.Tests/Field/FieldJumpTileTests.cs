using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests.Field
{
    public class FieldJumpTileTests
    {
        private const byte JumpSouthTwice = 91;
        private const byte JumpNorth = 58;

        private static MapCollisionGrid GapCrossedBy(byte behaviour)
        {
            var collisions = new byte[MapFile.mapSize, MapFile.mapSize];
            var types = new byte[MapFile.mapSize, MapFile.mapSize];

            for (int row = 0; row < MapFile.mapSize; row++)
                for (int col = 0; col < MapFile.mapSize; col++)
                    collisions[row, col] = MapCollisionGrid.BlockedBit;

            collisions[10, 10] = 0;
            collisions[11, 10] = 0;
            types[11, 10] = behaviour;
            collisions[13, 10] = 0;

            var grid = new MapCollisionGrid();
            grid.Add(0, 0, collisions);
            grid.AddTypes(0, 0, types);
            return grid;
        }

        [Fact]
        public void WalkingOntoAJumpTileCarriesYouOverTheGap()
        {
            var player = new FieldPlayer(10, 10, MoveFacing.Down, GapCrossedBy(JumpSouthTwice));

            Assert.Equal(StepResult.Jumped, player.Go(MoveFacing.Down));
            Assert.Equal(13, player.TileZ);
            Assert.Equal(10, player.TileX);

            Assert.True(player.IsWalking);
            player.Advance(FieldPlayer.WalkFrames);
            Assert.True(player.HopHeight > 0f, "The jump never left the ground.");

            player.Advance(FieldPlayer.WalkFrames * 3);
            Assert.False(player.IsWalking);
            Assert.Equal(0f, player.HopHeight);
        }

        [Fact]
        public void AJumpOnlyWorksTheWayTheTileFaces()
        {
            var player = new FieldPlayer(10, 13, MoveFacing.Up, GapCrossedBy(JumpSouthTwice));

            Assert.Equal(StepResult.Blocked, player.Go(MoveFacing.Up));
            Assert.Equal(13, player.TileZ);
        }

        [Fact]
        public void AOneTileJumpLandsOneTileOn()
        {
            var collisions = new byte[MapFile.mapSize, MapFile.mapSize];
            var types = new byte[MapFile.mapSize, MapFile.mapSize];
            for (int row = 0; row < MapFile.mapSize; row++)
                for (int col = 0; col < MapFile.mapSize; col++)
                    collisions[row, col] = MapCollisionGrid.BlockedBit;

            collisions[10, 10] = 0;
            collisions[9, 10] = 0;
            types[9, 10] = JumpNorth;
            collisions[8, 10] = 0;

            var grid = new MapCollisionGrid();
            grid.Add(0, 0, collisions);
            grid.AddTypes(0, 0, types);

            var player = new FieldPlayer(10, 10, MoveFacing.Up, grid);
            Assert.Equal(StepResult.Jumped, player.Go(MoveFacing.Up));
            Assert.Equal(8, player.TileZ);
        }

        [Fact]
        public void AJumpWithNowhereToLandIsRefused()
        {
            var collisions = new byte[MapFile.mapSize, MapFile.mapSize];
            var types = new byte[MapFile.mapSize, MapFile.mapSize];
            for (int row = 0; row < MapFile.mapSize; row++)
                for (int col = 0; col < MapFile.mapSize; col++)
                    collisions[row, col] = MapCollisionGrid.BlockedBit;

            collisions[10, 10] = 0;
            collisions[11, 10] = 0;
            types[11, 10] = JumpSouthTwice;

            var grid = new MapCollisionGrid();
            grid.Add(0, 0, collisions);
            grid.AddTypes(0, 0, types);

            var player = new FieldPlayer(10, 10, MoveFacing.Down, grid);
            Assert.Equal(StepResult.Walked, player.Go(MoveFacing.Down));
            Assert.Equal(11, player.TileZ);
        }
    }
}
