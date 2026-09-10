using System.Collections.Generic;
using DSPRE.Avalonia.Data;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// The movement-script player reproduces the battle engine's interpreter: sprite values change only when
    /// the script applies them, End puts the sprite back, and every curve uses the game's fixed-point sine.
    /// </summary>
    public class PokeAnimPlayerTests
    {
        // Pokémon-animation constants
        private const int APPLY_SET = 24, CURVE_SIN = 30, CURVE_SIN_MINUS = 32;
        private const int TARGET_DX = 35, TARGET_DY = 36, TARGET_RY = 38, CORRECT_ON_MINUS = 27, CORRECT_ON_NOT_EQ = 29;
        private const int PARAM_DX = 10, PARAM_RY = 13, USE_VAL = 20, PARAM_SET = 22;

        private static PastCommand Cmd(PastOp op, params int[] args) => new PastCommand(op, args);

        [Fact]
        public void CurveDivTime_SineBob_MatchesRuntimeMath()
        {
            var cmds = new List<PastCommand>
            {
                // apply, wait, type, target, L, rad(total angle), ofs, loop
                Cmd(PastOp.CallMfCurveDivTime, APPLY_SET, 0, CURVE_SIN, TARGET_DY, 100, 0x10000, 0, 4),
                Cmd(PastOp.HoldCmd),
                Cmd(PastOp.End),
            };
            var p = new PokeAnimPlayer(cmds);

            p.Step(); Assert.Equal(100, p.OffsetY);   // 90°
            p.Step(); Assert.Equal(0, p.OffsetY);     // 180°
            p.Step(); Assert.Equal(-100, p.OffsetY);  // 270°
            p.Step(); Assert.Equal(0, p.OffsetY);     // 360°

            // Move-func exhausted → HOLD releases → END within a couple of frames.
            for (int i = 0; i < 4 && !p.Finished; i++) p.Step();
            Assert.True(p.Finished);
        }

        [Fact]
        public void SetWait_FreezesForGivenFrames()
        {
            var cmds = new List<PastCommand>
            {
                Cmd(PastOp.SetWait, 3),
                Cmd(PastOp.End),
            };
            var p = new PokeAnimPlayer(cmds);
            p.Step();                       // runs SET_WAIT (wait=3, yields this frame)
            Assert.False(p.Finished);
            p.Step(); p.Step(); p.Step();   // the 3 wait frames (3→2→1→0), no command runs
            Assert.False(p.Finished);
            p.Step();                       // wait elapsed → END executes
            Assert.True(p.Finished);
        }

        // SET_DY_CORRECT keeps a *scaling* sprite anchored: when ry<0 (shrinking) it nudges POS_Y by -ry/8.
        // It does NOT touch X (the DY-correction only adjusts POS_Y).
        [Fact]
        public void DyCorrect_AnchorsScalingSprite_NotX()
        {
            var cmds = new List<PastCommand>
            {
                Cmd(PastOp.SetDyCorrect, CORRECT_ON_MINUS),
                // shrink vertically: CURVE_SIN_MINUS on RY, L=80 → at 90° ry = -80
                Cmd(PastOp.CallMfCurveDivTime, APPLY_SET, 0, CURVE_SIN_MINUS, TARGET_RY, 80, 0x10000, 0, 4),
                Cmd(PastOp.HoldCmd),
                Cmd(PastOp.End),
            };
            var p = new PokeAnimPlayer(cmds);
            p.Step();                       // 90°: ry = -80
            Assert.Equal(0, p.OffsetX);     // correction never affects X
            Assert.Equal(10, p.OffsetY);    // POS_Y nudged by -ry/8 = 80/8 = 10 to anchor the shrinking sprite
        }

        // The PokeReverse flag (set per-sprite by the caller) mirrors the X translation. Battle uses it off, but the
        // status screen / some species turn it on, so the interpreter must honour it.
        [Fact]
        public void Reverse_MirrorsXTranslation()
        {
            var cmds = new List<PastCommand>
            {
                Cmd(PastOp.CallMfCurveDivTime, APPLY_SET, 0, CURVE_SIN, TARGET_DX, 100, 0x10000, 0, 4),
                Cmd(PastOp.HoldCmd),
                Cmd(PastOp.End),
            };
            var p = new PokeAnimPlayer(cmds) { Reverse = true };
            p.Step();                       // 90°: dx = 100
            Assert.Equal(-100, p.OffsetX);  // PokeReverse negates X
        }

        [Fact]
        public void EndPutsTheSpriteBackOnTheTickItRuns()
        {
            var cmds = new List<PastCommand>
            {
                Cmd(PastOp.SetAddParam, PARAM_DX, USE_VAL, 20, PARAM_SET),
                Cmd(PastOp.SetAddParam, PARAM_RY, USE_VAL, -64, PARAM_SET),
                Cmd(PastOp.ApplyTrans),
                Cmd(PastOp.ApplyAffine),
                Cmd(PastOp.SetRequest),
                Cmd(PastOp.End),
            };
            var p = new PokeAnimPlayer(cmds);

            p.Step();
            Assert.Equal(20, p.OffsetX);
            Assert.Equal(0.75, p.ScaleY);

            p.Step();
            Assert.True(p.Finished);
            Assert.Equal(0, p.OffsetX);
            Assert.Equal(1.0, p.ScaleY);
        }

        [Fact]
        public void WorkingValuesDoNotMoveTheSpriteUntilApplied()
        {
            var cmds = new List<PastCommand>
            {
                Cmd(PastOp.SetAddParam, PARAM_DX, USE_VAL, 20, PARAM_SET),
                Cmd(PastOp.SetRequest),
                Cmd(PastOp.ApplyTrans),
                Cmd(PastOp.SetRequest),
                Cmd(PastOp.End),
            };
            var p = new PokeAnimPlayer(cmds);

            p.Step();
            Assert.Equal(0, p.OffsetX);
            p.Step();
            Assert.Equal(20, p.OffsetX);
        }

        [Fact]
        public void YCorrectionAddsUpWhenScaleIsAppliedWithoutTranslation()
        {
            var cmds = new List<PastCommand>
            {
                Cmd(PastOp.SetDyCorrect, CORRECT_ON_NOT_EQ),
                Cmd(PastOp.SetAddParam, PARAM_RY, USE_VAL, -80, PARAM_SET),
                Cmd(PastOp.StartLoop, 3),
                Cmd(PastOp.ApplyAffine),
                Cmd(PastOp.SetRequest),
                Cmd(PastOp.EndLoop),
                Cmd(PastOp.End),
            };
            var p = new PokeAnimPlayer(cmds);

            p.Step(); Assert.Equal(10, p.OffsetY);
            p.Step(); Assert.Equal(20, p.OffsetY);
            p.Step(); Assert.Equal(30, p.OffsetY);
        }

        [Fact]
        public void StartDelayHoldsTheWholeScript()
        {
            var cmds = new List<PastCommand>
            {
                Cmd(PastOp.SetAddParam, PARAM_DX, USE_VAL, 5, PARAM_SET),
                Cmd(PastOp.ApplyTrans),
                Cmd(PastOp.SetRequest),
                Cmd(PastOp.End),
            };
            var p = new PokeAnimPlayer(cmds, startDelay: 2);

            p.Step(); Assert.Equal(0, p.OffsetX);
            p.Step(); Assert.Equal(0, p.OffsetX);
            p.Step(); Assert.Equal(5, p.OffsetX);
        }

        [Fact]
        public void CurvesFloorTheFixedPointProductInsteadOfRounding()
        {
            // 0x2AAA is just under 60°: sin × 10 is 8.65, which the game's shift floors to 8.
            var cmds = new List<PastCommand>
            {
                Cmd(PastOp.CallMfCurve, APPLY_SET, 0, CURVE_SIN, TARGET_DX, 10, 0x2AAA, 0, 2),
                Cmd(PastOp.CallMfCurve, APPLY_SET, 0, CURVE_SIN_MINUS, TARGET_DY, 10, 0x2AAA, 0, 2),
                Cmd(PastOp.HoldCmd),
                Cmd(PastOp.End),
            };
            var p = new PokeAnimPlayer(cmds);

            p.Step();
            Assert.Equal(8, p.OffsetX);
            Assert.Equal(-8, p.OffsetY);
        }

        [Fact]
        public void PaletteFadeStepsOnceEveryWaitPlusOneTicksAndHoldsTheScript()
        {
            var cmds = new List<PastCommand>
            {
                Cmd(PastOp.PaletteFade, 0, 2, 1, 0x7FFF),
                Cmd(PastOp.WaitPaletteFade),
                Cmd(PastOp.End),
            };
            var p = new PokeAnimPlayer(cmds);

            var shown = new List<double>();
            for (int i = 0; i < 5; i++) { p.Step(); shown.Add(p.FadeStrength * 16); }

            Assert.Equal(new double[] { 0, 0, 1, 1, 2 }, shown);
            Assert.False(p.Finished);
            p.Step();
            Assert.True(p.Finished);
            Assert.False(p.Active);
        }
    }
}
