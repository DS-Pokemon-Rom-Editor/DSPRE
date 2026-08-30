using System.Collections.Generic;
using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// Reading a movement and playing it out, so somebody writing one can watch it happen instead of
    /// guessing. Durations come from the leak's own action names, which spell out the grid and the
    /// frame count: AC_DASH_x_4F, AC_JUMP_x_1G_8F, AC_JUMP_x_2G_16F, AC_JUMPHI_x_3G_32F.
    /// </summary>
    public class FieldMovementScriptTests
    {
        private static ScriptAction Act(string name, ushort? repeats = null)
            => new ScriptAction { name = name, repetitionCount = repeats };

        [Theory]
        [InlineData("WalkNorth8", FieldActionKind.Walk, 1, 8)]
        [InlineData("WalkSouth32", FieldActionKind.Walk, 1, 32)]
        [InlineData("WalkEast1", FieldActionKind.Walk, 1, 1)]
        [InlineData("WalkOnSpotWest16", FieldActionKind.Walk, 0, 16)]
        [InlineData("JumpNorth8", FieldActionKind.Jump, 1, 8)]
        [InlineData("JumpWest16", FieldActionKind.Jump, 1, 16)]
        [InlineData("JumpOnSpotEast8", FieldActionKind.Jump, 0, 8)]
        [InlineData("JumpFarSouth", FieldActionKind.Jump, 2, 16)]
        [InlineData("JumpVeryFarWest", FieldActionKind.Jump, 3, 32)]
        [InlineData("RunEast", FieldActionKind.Walk, 1, 4)]
        [InlineData("FaceNorth", FieldActionKind.Face, 0, 1)]
        [InlineData("Delay16", FieldActionKind.Delay, 0, 16)]
        public void EachActionSaysHowFarItGoesAndHowLongItTakes(
            string name, FieldActionKind kind, int tiles, int frames)
        {
            var step = FieldMovementScript.ParseOne(name);
            Assert.NotNull(step);
            Assert.Equal(kind, step.Kind);
            Assert.Equal(tiles, step.Tiles);
            Assert.Equal(frames, step.Frames);
        }

        [Theory]
        [InlineData("FaceNorth", MoveFacing.Up)]
        [InlineData("WalkSouth8", MoveFacing.Down)]
        [InlineData("JumpWest16", MoveFacing.Left)]
        [InlineData("RunEast", MoveFacing.Right)]
        public void NorthSouthWestAndEastMapOntoTheWayItFaces(string name, MoveFacing facing)
            => Assert.Equal(facing, FieldMovementScript.ParseOne(name).Facing);

        [Fact]
        public void TheEndMarkerStopsTheMovement()
        {
            Assert.Null(FieldMovementScript.ParseOne("End"));

            var steps = FieldMovementScript.Parse(new[]
                { Act("WalkNorth8"), Act("End"), Act("WalkSouth8") });
            Assert.Single(steps);
        }

        [Fact]
        public void AnActionThatRepeatsIsPlayedThatManyTimes()
        {
            var steps = FieldMovementScript.Parse(new[] { Act("WalkNorth8", 3), Act("End") });
            Assert.Equal(3, steps.Count);
            Assert.All(steps, s => Assert.Equal(MoveFacing.Up, s.Facing));
            Assert.Equal(24, FieldMovementScript.TotalFrames(steps));
        }

        [Fact]
        public void SomethingUnknownStillTakesAFrameSoTheTimingAddsUp()
        {
            var step = FieldMovementScript.ParseOne("NurseJoyBow");
            Assert.Equal(FieldActionKind.Other, step.Kind);
            Assert.Equal(1, step.Frames);
            Assert.Equal(0, step.Tiles);
        }

        [Fact]
        public void ShowingAndHidingAreRead()
        {
            Assert.False(FieldMovementScript.ParseOne("SetInvisible").Visible);
            Assert.True(FieldMovementScript.ParseOne("SetVisible").Visible);
        }

        // ── playing it out ──────────────────────────────────────────────────────────────
        private static OverworldAnimator Idle()
            => new OverworldAnimator(OverworldMovements.Find(0x00), MoveFacing.Down);

        [Fact]
        public void AWalkedMovementEndsUpExactlyWhereItsStepsSaidItWould()
        {
            var steps = FieldMovementScript.Parse(new[]
            {
                Act("WalkNorth8", 2),     // two tiles up
                Act("WalkEast8"),         // one tile right
                Act("End"),
            });

            var a = Idle();
            a.PlayScript(steps);
            Assert.True(a.IsScripted);

            a.Advance(FieldMovementScript.TotalFrames(steps));
            Assert.False(a.IsScripted);

            Assert.Equal(1, a.OffsetX);
            Assert.Equal(-2, a.OffsetZ);        // up is negative z
            Assert.Equal(MoveFacing.Right, a.Facing);
        }

        [Fact]
        public void ItSlidesBetweenTilesRatherThanJumpingBetweenThem()
        {
            var steps = FieldMovementScript.Parse(new[] { Act("WalkEast8"), Act("End") });
            var a = Idle();
            a.PlayScript(steps);

            float last = a.DrawOffsetX;
            for (int i = 0; i < 8; i++)
            {
                a.Advance(1);
                float moved = a.DrawOffsetX - last;
                Assert.InRange(moved, 0f, 1f / 8f + 0.001f);
                last = a.DrawOffsetX;
            }
            Assert.Equal(1f, a.DrawOffsetX, 3);
        }

        [Fact]
        public void AFarJumpCoversTwoTilesAndArcsOffTheGround()
        {
            var steps = FieldMovementScript.Parse(new[] { Act("JumpFarEast"), Act("End") });
            var a = Idle();
            a.PlayScript(steps);

            a.Advance(8);                            // half way through the sixteen frames
            Assert.True(a.HopHeight > 0.4f, "it should be off the ground at the top of the hop");

            a.Advance(8);
            Assert.Equal(2, a.OffsetX);
            Assert.Equal(0f, a.HopHeight, 3);        // and back down again
        }

        [Fact]
        public void WalkingOnTheSpotTurnsButGoesNowhere()
        {
            var steps = FieldMovementScript.Parse(new[] { Act("WalkOnSpotWest16"), Act("End") });
            var a = Idle();
            a.PlayScript(steps);
            a.Advance(16);

            Assert.Equal(0, a.OffsetX);
            Assert.Equal(0, a.OffsetZ);
            Assert.Equal(MoveFacing.Left, a.Facing);
        }

        [Fact]
        public void HidingAndShowingTakeEffect()
        {
            var a = Idle();
            Assert.True(a.Visible);

            a.PlayScript(FieldMovementScript.Parse(new[] { Act("SetInvisible"), Act("Delay8"), Act("End") }));
            Assert.False(a.Visible);

            a.PlayScript(FieldMovementScript.Parse(new[] { Act("SetVisible"), Act("Delay8"), Act("End") }));
            Assert.True(a.Visible);
        }

        [Fact]
        public void AMovementTakesOverFromWhateverTheEventWasDoingAndHandsBackAfter()
        {
            // A wandering overworld told to walk somewhere should do exactly that, then go back to
            // wandering rather than staying frozen.
            var a = new OverworldAnimator(OverworldMovements.Find(0x03), MoveFacing.Down, 8, 8, 0, 1);
            a.PlayScript(FieldMovementScript.Parse(new[] { Act("WalkEast8", 3), Act("End") }));

            a.Advance(24);
            Assert.False(a.IsScripted);
            Assert.Equal(3, a.OffsetX);

            // It is idling again, so given long enough it moves on its own.
            int before = a.OffsetX + a.OffsetZ;
            a.Advance(600);
            Assert.NotEqual(before, a.OffsetX + a.OffsetZ);
        }

        [Fact]
        public void StoppingAMovementPartWayLeavesItOnAWholeTile()
        {
            var a = Idle();
            a.PlayScript(FieldMovementScript.Parse(new[] { Act("WalkEast32"), Act("End") }));
            a.Advance(10);
            a.StopScript();

            Assert.False(a.IsScripted);
            Assert.Equal(a.OffsetX, a.DrawOffsetX, 3);
            Assert.Equal(a.OffsetZ, a.DrawOffsetZ, 3);
        }

        [Fact]
        public void NothingToPlayIsNotAScript()
        {
            var a = Idle();
            a.PlayScript(new List<FieldMovementStep>());
            Assert.False(a.IsScripted);
            a.PlayScript(null);
            Assert.False(a.IsScripted);
        }
    }
}
