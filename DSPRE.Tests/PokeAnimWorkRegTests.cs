using System.Collections.Generic;
using DSPRE.Avalonia.Data;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// The back animations drive motion through work-register math + SET_D rather than move-functions. This
    /// replicates poke_anm_b001_1 (a horizontal shake: dx = sin(work1)·12 with a per-frame sign flip) and
    /// checks the interpreter produces an alternating, non-zero horizontal offset.
    /// </summary>
    public class PokeAnimWorkRegTests
    {
        // PAST work-register constants (past_def.h)
        private const int WORK0 = 0, WORK1 = 1, WORK2 = 2;
        private const int CALC_VAL = 18, CALC_WORK = 19, USE_VAL = 20, PARAM_DX = 10;

        [Fact]
        public void BackShake_WorkRegMathDrivesAlternatingOffsetX()
        {
            var cmds = new List<PastCommand>
            {
                new PastCommand(PastOp.SetWorkVal, new[] { WORK1, 0 }),
                new PastCommand(PastOp.SetWorkVal, new[] { WORK2, 1 }),
                new PastCommand(PastOp.StartLoop, new[] { 32 }),
                new PastCommand(PastOp.AddWorkVal, new[] { WORK1, CALC_VAL, WORK1, 1024 }),
                new PastCommand(PastOp.SetWorkValSin, new[] { WORK0, WORK1, USE_VAL, 12, USE_VAL, 0 }),
                new PastCommand(PastOp.MulWorkVal, new[] { WORK0, CALC_WORK, WORK0, WORK2 }),
                new PastCommand(PastOp.SetD, new[] { WORK0, PARAM_DX }),
                new PastCommand(PastOp.MulWorkVal, new[] { WORK2, CALC_VAL, WORK2, -1 }),
                new PastCommand(PastOp.ApplyTrans, new int[0]),
                new PastCommand(PastOp.SetRequest, new int[0]),
                new PastCommand(PastOp.EndLoop, new int[0]),
                new PastCommand(PastOp.End, new int[0]),
            };
            var p = new PokeAnimPlayer(cmds);

            p.Step(); double x1 = p.OffsetX;   // iter 0
            p.Step(); double x2 = p.OffsetX;   // iter 1
            p.Step(); double x3 = p.OffsetX;   // iter 2

            Assert.True(x1 > 0, $"x1={x1}");
            Assert.True(x2 < 0, $"x2={x2}");   // sign flips each frame (work2 *= -1)
            Assert.True(x3 > 0, $"x3={x3}");
            Assert.False(p.Finished);          // 32-iteration loop is still running
        }
    }
}
