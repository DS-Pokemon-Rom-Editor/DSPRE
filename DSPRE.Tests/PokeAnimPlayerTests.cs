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
        // PAST constants (past_def.h)
        private const int APPLY_SET = 24, CURVE_SIN = 30, TARGET_DY = 36, CORRECT_ON_MINUS = 27;

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

        [Fact]
        public void DyCorrect_FlipsHorizontalSign()
        {
            var cmds = new List<PastCommand>
            {
                new PastCommand(PastOp.SetDyCorrect, new[] { CORRECT_ON_MINUS }),
                new PastCommand(PastOp.CallMfCurveDivTime, new[] { APPLY_SET, 0, CURVE_SIN, 35 /*TARGET_DX*/, 100, 0x10000, 0, 4 }),
                new PastCommand(PastOp.HoldCmd, new int[0]),
                new PastCommand(PastOp.End, new int[0]),
            };
            var p = new PokeAnimPlayer(cmds);
            p.Step();                       // SET_DY_CORRECT + first DX sample (90° → dx=100)
            Assert.Equal(-100, p.OffsetX);  // correction flips X
        }
    }
}
