using System.Collections.Generic;
using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>Picking the walking picture out of a sprite bank, frame by frame the way the field does.</summary>
    public class SpritePoseTests
    {
        [Theory]
        [InlineData(8, 2)]      // a pair Pokemon
        [InlineData(16, 4)]     // an ordinary person
        [InlineData(32, 4)]     // the hero, whose walk is the first sixteen
        public void GroupSizesMatchWhatTheBanksHold(int frames, int expected)
            => Assert.Equal(expected, FieldSpriteAnimation.PerFacing(frames));

        [Fact]
        public void EachFacingStartsAtItsOwnGroup()
        {
            Assert.Equal(0, FieldSpriteAnimation.PictureFor(16, 0, null));   // up
            Assert.Equal(4, FieldSpriteAnimation.PictureFor(16, 1, null));   // down
            Assert.Equal(8, FieldSpriteAnimation.PictureFor(16, 2, null));   // left
            Assert.Equal(12, FieldSpriteAnimation.PictureFor(16, 3, null));  // right
        }

        // Pictures within the facing, 1 standing, 2 a foot, 3 standing, 4 the other foot, one a frame.
        private static string Step(FieldWalkCycle c, int stepFrames, bool onSpot = false)
        {
            var pictures = new List<int>();
            int frames = onSpot ? stepFrames + 1 : stepFrames;
            for (int i = 0; i < frames; i++)
            {
                if (!onSpot || i < stepFrames) c.Walk(stepFrames);
                pictures.Add(FieldSpriteAnimation.PictureFor(16, 1, c) - 4 + 1);
            }
            return string.Join(" ", pictures);
        }

        private static FieldWalkCycle Fresh()
        {
            var c = new FieldWalkCycle();
            c.Face(MoveFacing.Down);
            return c;
        }

        [Fact]
        public void AnOrdinaryWalkPutsOneFootForwardATile()
        {
            var c = Fresh();
            Assert.Equal("1 1 1 2 2 2 2 3", Step(c, 8));
            Assert.Equal("3 3 3 4 4 4 4 1", Step(c, 8));
            Assert.Equal("1 1 1 2 2 2 2 3", Step(c, 8));
        }

        [Fact]
        public void FastAndSlowWalksStillTakeOneFootATile()
        {
            var fast = Fresh();
            Assert.Equal("1 2 2 3", Step(fast, 4));
            Assert.Equal("3 4 4 1", Step(fast, 4));

            var faster = Fresh();
            Assert.Equal("2 3", Step(faster, 2));
            Assert.Equal("4 1", Step(faster, 2));

            var slow = Fresh();
            Assert.Equal("1 1 1 1 1 1 1 2 2 2 2 2 2 2 2 3", Step(slow, 16));
        }

        [Theory]
        [InlineData(6)]
        [InlineData(3)]
        [InlineData(7)]
        public void TheUnevenSpeedsAlsoAddUpToOneFoot(int frames)
        {
            var c = Fresh();
            Step(c, frames);
            Assert.Equal(8, c.Frame);
        }

        [Fact]
        public void WalkingOnTheSpotHoldsItsLastFrame()
        {
            Assert.Equal("1 1 1 2 2 2 2 3 3", Step(Fresh(), 8, onSpot: true));
        }

        [Fact]
        public void TurningStartsAgainFromTheFirstFoot()
        {
            var c = Fresh();
            Step(c, 8);
            Assert.Equal(8, c.Frame);
            c.Face(MoveFacing.Left);
            Assert.Equal(0, c.Frame);
            c.Face(MoveFacing.Left);                 // the same way again changes nothing
            Assert.Equal(0, c.Frame);
        }

        [Fact]
        public void StoppingPartWaySettlesBackOntoAFoot()
        {
            var c = Fresh();
            for (int i = 0; i < 5; i++) c.Walk(8);
            c.Rest();
            Assert.Equal(0, c.Frame);

            for (int i = 0; i < 8; i++) c.Walk(8);
            for (int i = 0; i < 3; i++) c.Walk(8);
            c.Rest();
            Assert.Equal(8, c.Frame);
            // Standing on the second foot shows the second standing picture.
            Assert.Equal(6, FieldSpriteAnimation.PictureFor(16, 1, c));
        }

        [Fact]
        public void APairPokemonBobsWhetherItMovesOrNot()
        {
            var c = Fresh();
            var down = new List<int>();
            var up = new List<int>();
            for (int i = 0; i < 20; i++)
            {
                down.Add(FieldSpriteAnimation.PictureFor(8, 1, c));
                up.Add(FieldSpriteAnimation.PictureFor(8, 0, c));
                c.Tick();
            }
            Assert.Equal(Enumerable.Repeat(2, 5).Concat(Enumerable.Repeat(3, 10)).Concat(Enumerable.Repeat(2, 5)), down);
            Assert.Equal(Enumerable.Repeat(0, 10).Concat(Enumerable.Repeat(1, 10)), up);
        }

        [Fact]
        public void TheHeroRunsOnTheRunningPictures()
        {
            var c = Fresh();
            c.Face(MoveFacing.Right);
            var seen = new HashSet<int>();
            for (int i = 0; i < 16; i++) { c.Dash(); seen.Add(FieldSpriteAnimation.PictureFor(32, 3, c)); }
            Assert.All(seen, p => Assert.InRange(p, 28, 31));
            Assert.True(seen.Count > 1, "a run should move through its pictures");

            c.Rest();
            Assert.InRange(FieldSpriteAnimation.PictureFor(32, 3, c), 12, 15);
        }

        [Fact]
        public void AnOddBankStaysOnSomethingThatExists()
        {
            var c = Fresh();
            for (int n = 1; n <= 40; n++)
                for (int facing = 0; facing < 4; facing++)
                    for (int frame = 0; frame < 25; frame++)
                    {
                        c.Walk(8);
                        c.Tick();
                        Assert.InRange(FieldSpriteAnimation.PictureFor(n, facing, c), 0, n - 1);
                    }
        }

        [Fact]
        public void AStaticEventNeverWalksButAWandererDoes()
        {
            var a = new OverworldAnimator(OverworldMovements.Find(0x00), MoveFacing.Down);
            a.Advance(120);
            Assert.Equal(0, a.Cycle.Frame);

            var w = new OverworldAnimator(OverworldMovements.Find(0x03), MoveFacing.Down, 6, 6, 0, 3);
            int guard = 0;
            while (!w.IsWalking && guard++ < 500) w.Advance(1);
            Assert.True(w.IsWalking, "it never started walking");
            int before = w.Cycle.Frame;
            w.Advance(4);
            Assert.Equal(before + 4, w.Cycle.Frame);
        }

        [Fact]
        public void AScriptedWalkAnimatesAtTheStepsOwnSpeed()
        {
            var a = new OverworldAnimator(null, MoveFacing.Right);
            a.PlayScript(FieldMovementScript.Parse(new[]
            {
                new ScriptAction { name = "WalkEast4", repetitionCount = 2 },
            }));
            var pictures = new List<int>();
            for (int i = 0; i < 8; i++)
            {
                a.Advance(1);
                pictures.Add(FieldSpriteAnimation.PictureFor(16, 3, a.Cycle) - 12 + 1);
            }
            Assert.Equal("1 2 2 3 3 4 4 1", string.Join(" ", pictures));
        }

        [Fact]
        public void ThePlayersFeetAlternateFromOneTileToTheNext()
        {
            var open = new MapCollisionGrid();
            open.Add(0, 0, new byte[MapFile.mapSize, MapFile.mapSize]);
            var p = new FieldPlayer(5, 5, MoveFacing.Right, open);

            p.Advance(10);
            Assert.Equal(0, p.Cycle.Frame);           // standing still costs nothing

            p.Go(MoveFacing.Right);
            p.Advance(FieldPlayer.WalkFrames);
            Assert.Equal(8, p.Cycle.Frame);

            // The next tile leads with the other foot and comes back round to the first.
            p.Go(MoveFacing.Right);
            p.Advance(FieldPlayer.WalkFrames);
            Assert.Equal(0, p.Cycle.Frame);
        }
    }
}
