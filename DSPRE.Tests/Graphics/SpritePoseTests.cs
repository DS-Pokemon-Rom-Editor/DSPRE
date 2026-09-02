using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>Picking the walking picture out of a sprite bank.</summary>
    public class SpritePoseTests
    {
        [Theory]
        [InlineData(8, 2)]      // a following Pokemon
        [InlineData(16, 4)]     // an ordinary person
        [InlineData(32, 4)]     // the hero, whose walk is the first sixteen
        public void GroupSizesMatchWhatTheBanksHold(int frames, int expected)
            => Assert.Equal(expected, FieldSpriteAnimation.PerFacing(frames));

        [Fact]
        public void EachFacingStartsAtItsOwnGroup()
        {
            // Standing still, so the first picture of the group every time.
            Assert.Equal(0, FieldSpriteAnimation.PictureFor(16, 0, 0, false));   // up
            Assert.Equal(4, FieldSpriteAnimation.PictureFor(16, 1, 0, false));   // down
            Assert.Equal(8, FieldSpriteAnimation.PictureFor(16, 2, 0, false));   // left
            Assert.Equal(12, FieldSpriteAnimation.PictureFor(16, 3, 0, false));  // right
        }

        [Fact]
        public void AFollowingPokemonHasTwoPicturesPerFacing()
        {
            Assert.Equal(0, FieldSpriteAnimation.PictureFor(8, 0, 0, false));
            Assert.Equal(2, FieldSpriteAnimation.PictureFor(8, 1, 0, false));
            Assert.Equal(4, FieldSpriteAnimation.PictureFor(8, 2, 0, false));
            Assert.Equal(6, FieldSpriteAnimation.PictureFor(8, 3, 0, false));
        }

        [Fact]
        public void APersonHoldsEachPictureForFourFrames()
        {
            // Facing down, so the group runs 4,5,6,7.
            for (int f = 0; f < 4; f++) Assert.Equal(4, FieldSpriteAnimation.PictureFor(16, 1, f, true));
            for (int f = 4; f < 8; f++) Assert.Equal(5, FieldSpriteAnimation.PictureFor(16, 1, f, true));
            for (int f = 8; f < 12; f++) Assert.Equal(6, FieldSpriteAnimation.PictureFor(16, 1, f, true));
            for (int f = 12; f < 16; f++) Assert.Equal(7, FieldSpriteAnimation.PictureFor(16, 1, f, true));
            Assert.Equal(4, FieldSpriteAnimation.PictureFor(16, 1, 16, true));    // and round again
        }

        [Fact]
        public void OneTileOfWalkingGetsThroughTwoPictures()
        {
            // A step is eight frames, so the first tile shows the standing picture then the first step, and
            // the tile after shows the standing picture then the other step.
            var firstTile = new System.Collections.Generic.HashSet<int>();
            for (int f = 0; f < 8; f++) firstTile.Add(FieldSpriteAnimation.PictureFor(16, 1, f, true));
            var secondTile = new System.Collections.Generic.HashSet<int>();
            for (int f = 8; f < 16; f++) secondTile.Add(FieldSpriteAnimation.PictureFor(16, 1, f, true));

            Assert.Equal(new[] { 4, 5 }, firstTile.OrderBy(x => x).ToArray());
            Assert.Equal(new[] { 6, 7 }, secondTile.OrderBy(x => x).ToArray());
        }

        [Fact]
        public void AFollowingPokemonHoldsEachPictureForTenFrames()
        {
            for (int f = 0; f < 10; f++) Assert.Equal(2, FieldSpriteAnimation.PictureFor(8, 1, f, true));
            for (int f = 10; f < 20; f++) Assert.Equal(3, FieldSpriteAnimation.PictureFor(8, 1, f, true));
            Assert.Equal(2, FieldSpriteAnimation.PictureFor(8, 1, 20, true));
        }

        [Fact]
        public void TheHeroWalksOnTheFirstSixteenPicturesNotTheRunningOnes()
        {
            for (int f = 0; f < 64; f++)
                Assert.InRange(FieldSpriteAnimation.PictureFor(32, 3, f, true), 12, 15);
        }

        [Fact]
        public void StandingStillNeverAnimates()
        {
            for (int f = 0; f < 100; f++)
                Assert.Equal(4, FieldSpriteAnimation.PictureFor(16, 1, f, false));
        }

        [Fact]
        public void AnOddBankStaysOnSomethingThatExists()
        {
            for (int n = 1; n <= 40; n++)
                for (int facing = 0; facing < 4; facing++)
                    for (int cell = 0; cell < 25; cell++)
                    {
                        int p = FieldSpriteAnimation.PictureFor(n, facing, cell, true);
                        Assert.InRange(p, 0, n - 1);
                    }
        }

        [Fact]
        public void TheWalkerCountsFramesOnlyWhileItIsMoving()
        {
            var a = new OverworldAnimator(OverworldMovements.Find(0x00), MoveFacing.Down);
            a.Advance(120);
            Assert.Equal(0, a.AnimationCell);        // a static event never walks, so it never animates

            var w = new OverworldAnimator(OverworldMovements.Find(0x03), MoveFacing.Down, 6, 6, 0, 3);
            int guard = 0;
            while (!w.IsWalking && guard++ < 500) w.Advance(1);
            Assert.True(w.IsWalking, "it never started walking");
            int before = w.AnimationCell;
            w.Advance(4);
            Assert.Equal(before + 4, w.AnimationCell);
        }

        [Fact]
        public void ThePlayerCountsFramesWhileWalkingAndKeepsCountingAcrossSteps()
        {
            var open = new MapCollisionGrid();
            open.Add(0, 0, new byte[MapFile.mapSize, MapFile.mapSize]);
            var p = new FieldPlayer(5, 5, MoveFacing.Right, open);

            Assert.Equal(0, p.AnimationCell);
            p.Advance(10);
            Assert.Equal(0, p.AnimationCell);         // standing still costs nothing

            p.Go(MoveFacing.Right);
            p.Advance(FieldPlayer.WalkFrames);
            Assert.Equal(FieldPlayer.WalkFrames, p.AnimationCell);

            // The count carries over, which is what makes the next tile use the other foot.
            p.Go(MoveFacing.Right);
            p.Advance(FieldPlayer.WalkFrames);
            Assert.Equal(FieldPlayer.WalkFrames * 2, p.AnimationCell);
        }
    }
}
