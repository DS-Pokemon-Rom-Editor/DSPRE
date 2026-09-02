using System.Collections.Generic;
using DSPRE.Avalonia.Data;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// The PAST interpreter reproduces the runtime motion math. A CALL_MF_CURVE_DIVTIME(SIN, TARGET_DY, L=100,
    /// rad=0x10000, ofs=0, loop=4) sampled at 90/180/270/360° must give DY = 100, 0, −100, 0 then finish.
    /// </summary>
    public class PokeAnimPlayerTests
    {
        // Pokémon-animation constants
        private const int APPLY_SET = 24, CURVE_SIN = 30, CURVE_SIN_MINUS = 32;
        private const int TARGET_DX = 35, TARGET_DY = 36, TARGET_RY = 38, CORRECT_ON_MINUS = 27;

        [Fact]
        public void CurveDivTime_SineBob_MatchesRuntimeMath()
        {
            var cmds = new List<PastCommand>
            {
                // apply, wait, type, target, L, rad(total angle), ofs, loop
                new PastCommand(PastOp.CallMfCurveDivTime, new[] { APPLY_SET, 0, CURVE_SIN, TARGET_DY, 100, 0x10000, 0, 4 }),
                new PastCommand(PastOp.HoldCmd, new int[0]),
                new PastCommand(PastOp.End, new int[0]),
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
                new PastCommand(PastOp.SetWait, new[] { 3 }),
                new PastCommand(PastOp.End, new int[0]),
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
                new PastCommand(PastOp.SetDyCorrect, new[] { CORRECT_ON_MINUS }),
                // shrink vertically: CURVE_SIN_MINUS on RY, L=80 → at 90° ry = -80
                new PastCommand(PastOp.CallMfCurveDivTime, new[] { APPLY_SET, 0, CURVE_SIN_MINUS, TARGET_RY, 80, 0x10000, 0, 4 }),
                new PastCommand(PastOp.HoldCmd, new int[0]),
                new PastCommand(PastOp.End, new int[0]),
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
                new PastCommand(PastOp.CallMfCurveDivTime, new[] { APPLY_SET, 0, CURVE_SIN, TARGET_DX, 100, 0x10000, 0, 4 }),
                new PastCommand(PastOp.HoldCmd, new int[0]),
                new PastCommand(PastOp.End, new int[0]),
            };
            var p = new PokeAnimPlayer(cmds) { Reverse = true };
            p.Step();                       // 90°: dx = 100
            Assert.Equal(-100, p.OffsetX);  // PokeReverse negates X
        }
    }
}
