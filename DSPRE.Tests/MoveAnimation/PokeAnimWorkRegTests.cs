using System.Collections.Generic;
using DSPRE.Avalonia.Data;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// The back animations drive motion through work-register math rather than move-functions. This
    /// replicates poke_anm_b001_1 (a horizontal shake: dx = sin(work1)·12 with a per-frame sign flip) and
    /// checks the interpreter produces an alternating, non-zero horizontal offset.
    /// </summary>
    public class PokeAnimWorkRegTests
    {
        // Pokémon-animation work-register constants
        private const int WORK0 = 0, WORK1 = 1, WORK2 = 2;
        private const int CALC_VAL = 18, CALC_WORK = 19, USE_VAL = 20, PARAM_DX = 10;

        [Fact]
        public void BackShake_WorkRegMathDrivesAlternatingOffsetX()
        {
            var cmds = new List<PokemonAnimCommand>
            {
                new PokemonAnimCommand(PokemonAnimOp.SetVar, new[] { WORK1, 0 }),
                new PokemonAnimCommand(PokemonAnimOp.SetVar, new[] { WORK2, 1 }),
                new PokemonAnimCommand(PokemonAnimOp.Loop, new[] { 32 }),
                new PokemonAnimCommand(PokemonAnimOp.Add, new[] { WORK1, CALC_VAL, WORK1, 1024 }),
                new PokemonAnimCommand(PokemonAnimOp.Sin, new[] { WORK0, WORK1, USE_VAL, 12, USE_VAL, 0 }),
                new PokemonAnimCommand(PokemonAnimOp.Multiply, new[] { WORK0, CALC_WORK, WORK0, WORK2 }),
                new PokemonAnimCommand(PokemonAnimOp.SetOffset, new[] { WORK0, PARAM_DX }),
                new PokemonAnimCommand(PokemonAnimOp.Multiply, new[] { WORK2, CALC_VAL, WORK2, -1 }),
                new PokemonAnimCommand(PokemonAnimOp.ApplyTranslation, new int[0]),
                new PokemonAnimCommand(PokemonAnimOp.WaitFrame, new int[0]),
                new PokemonAnimCommand(PokemonAnimOp.LoopEnd, new int[0]),
                new PokemonAnimCommand(PokemonAnimOp.End, new int[0]),
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
