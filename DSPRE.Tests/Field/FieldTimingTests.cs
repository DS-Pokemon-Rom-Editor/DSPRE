using System;
using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>How fast things happen, and where they are while they happen. </summary>
    public class FieldTimingTests
    {
        private static OverworldAnimator Walker(byte code = 0x03, MoveFacing facing = MoveFacing.Down,
                                                int rx = 4, int rz = 4, int seed = 1,
                                                Func<int, int, bool> blocked = null)
            => new OverworldAnimator(OverworldMovements.Find(code), facing, rx, rz, 0, seed, blocked);

        [Fact]
        public void AStepTakesEightFramesAndIsInBetweenWhileItRuns()
        {
            var a = Walker(seed: 3);

            // Wind on until it starts walking.
            int guard = 0;
            while (!a.IsWalking && guard++ < 500) a.Advance(1);
            Assert.True(a.IsWalking, "it never started walking");

            // Part way through it sits between the two tiles, not on either.
            a.Advance(OverworldAnimator.WalkFrames / 2);
            float x = a.DrawOffsetX, z = a.DrawOffsetZ;
            bool between = Math.Abs(x - Math.Round(x)) > 0.01f || Math.Abs(z - Math.Round(z)) > 0.01f;
            Assert.True(between, $"it was on a whole tile mid-step: {x},{z}");

            // And by the eighth frame it has arrived.
            a.Advance(OverworldAnimator.WalkFrames);
            Assert.False(a.IsWalking);
            Assert.Equal(a.OffsetX, a.DrawOffsetX, 3);
            Assert.Equal(a.OffsetZ, a.DrawOffsetZ, 3);
        }

        [Fact]
        public void ItNeverMovesMoreThanOneTileAtATime()
        {
            var a = Walker(rx: 6, rz: 6, seed: 11);
            int lastX = a.OffsetX, lastZ = a.OffsetZ;

            for (int i = 0; i < 4000; i++)
            {
                a.Advance(1);
                int stepped = Math.Abs(a.OffsetX - lastX) + Math.Abs(a.OffsetZ - lastZ);
                Assert.True(stepped <= 1, $"it moved {stepped} tiles in one frame");
                lastX = a.OffsetX; lastZ = a.OffsetZ;
            }
        }

        [Fact]
        public void WhereItIsDrawnNeverJumps()
        {
            // The drawn position must creep, never leap: at most an eighth of a tile per frame.
            var a = Walker(rx: 6, rz: 6, seed: 5);
            float lastX = a.DrawOffsetX, lastZ = a.DrawOffsetZ;

            for (int i = 0; i < 4000; i++)
            {
                a.Advance(1);
                float moved = Math.Abs(a.DrawOffsetX - lastX) + Math.Abs(a.DrawOffsetZ - lastZ);
                Assert.True(moved <= 1f / OverworldAnimator.WalkFrames + 0.001f,
                    $"the sprite jumped {moved} of a tile in one frame");
                lastX = a.DrawOffsetX; lastZ = a.DrawOffsetZ;
            }
        }

        [Fact]
        public void ARefusedStepLeavesItExactlyWhereItWas()
        {
            var a = Walker(rx: 4, rz: 4, seed: 2, blocked: (x, z) => true);
            a.Advance(2000);
            Assert.False(a.IsWalking);
            Assert.Equal(0, a.OffsetX);
            Assert.Equal(0, a.OffsetZ);
            Assert.Equal(0f, a.DrawOffsetX);
            Assert.Equal(0f, a.DrawOffsetZ);
        }

        [Fact]
        public void TurningOnTheSpotNeverLeavesTheTile()
        {
            var a = new OverworldAnimator(OverworldMovements.Find(0x02), MoveFacing.Down, 5, 5, 0, 4);
            for (int i = 0; i < 2000; i++)
            {
                a.Advance(1);
                Assert.False(a.IsWalking);
                Assert.Equal(0f, a.DrawOffsetX);
                Assert.Equal(0f, a.DrawOffsetZ);
            }
        }

        // ── the player ──────────────────────────────────────────────────────────────────
        private static MapCollisionGrid OpenMap()
        {
            var grid = new MapCollisionGrid();
            grid.Add(0, 0, new byte[MapFile.mapSize, MapFile.mapSize]);
            return grid;
        }

        [Fact]
        public void ThePlayerWalksRatherThanJumping()
        {
            var p = new FieldPlayer(5, 5, MoveFacing.Right, OpenMap());
            Assert.Equal(StepResult.Walked, p.Go(MoveFacing.Right));
            Assert.True(p.IsWalking);

            // Half way there it is between the tiles.
            p.Advance(FieldPlayer.WalkFrames / 2);
            Assert.InRange(p.DrawX, 5.4f, 5.6f);

            p.Advance(FieldPlayer.WalkFrames);
            Assert.False(p.IsWalking);
            Assert.Equal(6f, p.DrawX);
        }

        [Fact]
        public void AnotherStepHasToWaitForTheLastOneToFinish()
        {
            var p = new FieldPlayer(5, 5, MoveFacing.Right, OpenMap());
            p.Go(MoveFacing.Right);
            Assert.Equal(StepResult.Walking, p.Go(MoveFacing.Right));
            Assert.Equal(6, p.TileX);            // still only the one step booked

            p.Advance(FieldPlayer.WalkFrames);
            Assert.Equal(StepResult.Walked, p.Go(MoveFacing.Right));
            Assert.Equal(7, p.TileX);
        }

        // ── time of day ─────────────────────────────────────────────────────────────────
        [Theory]
        [InlineData(0, FieldTimeZone.Midnight)]
        [InlineData(3, FieldTimeZone.Midnight)]
        [InlineData(4, FieldTimeZone.Morning)]
        [InlineData(9, FieldTimeZone.Morning)]
        [InlineData(10, FieldTimeZone.Noon)]
        [InlineData(16, FieldTimeZone.Noon)]
        [InlineData(17, FieldTimeZone.Evening)]
        [InlineData(19, FieldTimeZone.Evening)]
        [InlineData(20, FieldTimeZone.Night)]
        [InlineData(23, FieldTimeZone.Night)]
        public void EveryHourFallsWhereTheGamesPutIt(int hour, FieldTimeZone expected)
        {
            Assert.Equal(expected, FieldTimeOfDay.ZoneForHour(hour));
        }

        [Fact]
        public void EachPartOfTheDayPicksItsOwnAnimation()
        {
            // TimeZoneAnmIdxTbl: morning, day and evening take one each; night and the small hours share.
            Assert.Equal(0, FieldTimeOfDay.AnimationIndexForZone(FieldTimeZone.Morning));
            Assert.Equal(1, FieldTimeOfDay.AnimationIndexForZone(FieldTimeZone.Noon));
            Assert.Equal(2, FieldTimeOfDay.AnimationIndexForZone(FieldTimeZone.Evening));
            Assert.Equal(3, FieldTimeOfDay.AnimationIndexForZone(FieldTimeZone.Night));
            Assert.Equal(3, FieldTimeOfDay.AnimationIndexForZone(FieldTimeZone.Midnight));
        }

        [Fact]
        public void TheClockRunsAtThirtyFramesASecond()
        {
            // A normal walking step is eight frames and the games call that 3.75 tiles a second.
            float tilesPerSecond = 30f / OverworldAnimator.WalkFrames;
            Assert.Equal(3.75f, tilesPerSecond, 2);
        }

        // ── camera ──────────────────────────────────────────────────────────────────────
        [Fact]
        public void TheFieldCameraMatchesTheGamesOwnNumbers()
        {
            // From the live table in field_camera.dat, entry 0.
            Assert.InRange(FieldCamera.DistanceInTiles, 41.5f, 42f);
            Assert.InRange(FieldCamera.PitchDegrees, 48.5f, 48.9f);

            // PerspWay is half the view angle, not all of it.
            Assert.InRange(FieldCamera.HalfFieldOfViewDegrees, 8f, 8.2f);
            Assert.InRange(FieldCamera.FieldOfViewDegrees, 16.1f, 16.3f);

            // The camera does not turn with the player. It keeps up with them across the ground and
            // lags six frames on height only, which is what CAM_TRACE_MASK_Y asks for.
            Assert.Equal(0f, FieldCamera.YawDegrees);
            Assert.Equal(6, FieldCamera.TrailFrames);
            Assert.True(FieldCamera.HeightLagsBehind);

            // Those together should show about a dozen tiles top to bottom, which is roughly what a DS
            // screen shows. If any of the conversions were out this would not come close.
            Assert.InRange(FieldCamera.Normal.VisibleTilesAtTarget, 11f, 13f);
        }

        [Fact]
        public void EveryCameraInTheGamesTableIsReadableAndSane()
        {
            // ZoneData_GetCameraID hands a header's camera number straight to this table, so every row
            // has to come out usable, not just the one most maps use.
            Assert.Equal(17, FieldCamera.Count);

            int flat = 0;
            foreach (var c in FieldCamera.Entries)
            {
                Assert.False(string.IsNullOrWhiteSpace(c.Name));
                Assert.InRange(c.DistanceInTiles, 5f, 110f);
                Assert.InRange(c.PitchDegrees, 4f, 65f);          // all of them look downwards
                Assert.InRange(c.FieldOfViewDegrees, 3f, 40f);

                // Every row frames roughly the same amount of map, between about ten and twenty tiles, even
                // though the distances range from 20 tiles to nearly 100.
                Assert.InRange(c.VisibleTilesAtTarget, 10f, 20f);
                Assert.Equal(c.VisibleTilesAtTarget / 2f, c.OrthoHalfHeightInTiles, 4);
                Assert.True(c.FarClip > c.NearClip);
                if (c.Orthographic) flat++;
            }

            // Two of them are flat views, the indoor one and the dance theatre.
            Assert.Equal(2, flat);
            Assert.True(FieldCamera.Entry(4).Orthographic);
            Assert.True(FieldCamera.Entry(15).Orthographic);

            // A header with a nonsense number falls back to the ordinary camera rather than throwing.
            Assert.Equal(FieldCamera.Normal.Id, FieldCamera.Entry(200).Id);
            Assert.Equal(FieldCamera.Normal.Id, FieldCamera.Entry(-3).Id);
        }

        [Fact]
        public void TheGymCamerasReallyDifferFromTheOrdinaryOne()
        {
            // If the table were being ignored and everything fell back to row 0, this would not hold.
            var normal = FieldCamera.Normal;
            Assert.NotEqual(normal.PitchDegrees, FieldCamera.Entry(9).PitchDegrees);      // Vermilion
            Assert.NotEqual(normal.DistanceInTiles, FieldCamera.Entry(1).DistanceInTiles); // Violet
            Assert.NotEqual(normal.FieldOfViewDegrees, FieldCamera.Entry(8).FieldOfViewDegrees);
            Assert.NotEqual(0f, FieldCamera.Entry(3).ShiftZInTiles);
        }
    }
}
