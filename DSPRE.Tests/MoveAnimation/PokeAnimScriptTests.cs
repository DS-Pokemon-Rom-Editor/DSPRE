using System;
using System.Collections.Generic;
using DSPRE.Avalonia.Data;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// The Pokémon animation bytecode is a stream of little-endian 32-bit words: an opcode followed by
    /// that opcode's fixed argument words, ending at <see cref="PokemonAnimOp.End"/>. These pin the parse / serialize
    /// round-trip and the tolerant stop conditions.
    /// </summary>
    public class PokeAnimScriptTests
    {
        private static byte[] Words(params int[] ws)
        {
            var b = new byte[ws.Length * 4];
            for (int i = 0; i < ws.Length; i++) BitConverter.GetBytes(ws[i]).CopyTo(b, i * 4);
            return b;
        }

        [Fact]
        public void Parse_ReadsOpcodeAndFixedArgs()
        {
            // command 27 (8 args) then command 31 (1 arg) then END (0).
            var data = Words(27, 24, 0, 30, 36, 10, 0x8000, 0, 16, 31, 8, 0);
            var cmds = PokeAnimScript.Parse(data);

            Assert.Equal(3, cmds.Count);
            Assert.Equal(PokemonAnimOp.TransformCurveEven, cmds[0].Op);
            Assert.Equal(new[] { 24, 0, 30, 36, 10, 0x8000, 0, 16 }, cmds[0].Args);
            Assert.Equal(PokemonAnimOp.SetStartDelay, cmds[1].Op);
            Assert.Equal(new[] { 8 }, cmds[1].Args);
            Assert.Equal(PokemonAnimOp.End, cmds[2].Op);
        }

        [Fact]
        public void Parse_StopsAtEnd_IgnoringTrailingBytes()
        {
            var data = Words(24 /*HoldCmd, 0 args*/, 0 /*End*/, 27 /*garbage after End*/, 1, 2);
            var cmds = PokeAnimScript.Parse(data);
            Assert.Equal(2, cmds.Count);
            Assert.Equal(PokemonAnimOp.WaitTransform, cmds[0].Op);
            Assert.Equal(PokemonAnimOp.End, cmds[1].Op);
        }

        [Fact]
        public void Parse_StopsWhenArgsWouldOverrun()
        {
            // Command 27 claims 8 args but only 3 words follow → parse nothing, no crash.
            var data = Words(27, 1, 2, 3);
            Assert.Empty(PokeAnimScript.Parse(data));
        }

        [Fact]
        public void ArgNames_MatchArgCounts()
        {
            foreach (PokemonAnimOp op in System.Enum.GetValues(typeof(PokemonAnimOp)))
            {
                var names = PokeAnimScript.ArgNames(op);
                if (names.Length > 0)   // labelled opcodes must name exactly their argument words
                    Assert.Equal(PokeAnimScript.ArgsFor(op), names.Length);
            }
        }

        [Fact]
        public void Serialize_RoundTripsParse()
        {
            var cmds = new List<PokemonAnimCommand>
            {
                new PokemonAnimCommand(PokemonAnimOp.SetYNormalization, new[] { 27 }),
                new PokemonAnimCommand(PokemonAnimOp.TransformCurveEven, new[] { 24, 0, 32, 38, 0x20, 0x18000, 0, 48 }),
                new PokemonAnimCommand(PokemonAnimOp.WaitTransform, Array.Empty<int>()),
                new PokemonAnimCommand(PokemonAnimOp.End, Array.Empty<int>()),
            };
            var round = PokeAnimScript.Parse(PokeAnimScript.Serialize(cmds));
            Assert.Equal(cmds.Count, round.Count);
            for (int i = 0; i < cmds.Count; i++)
            {
                Assert.Equal(cmds[i].Op, round[i].Op);
                Assert.Equal(cmds[i].Args, round[i].Args);
            }
        }
    }
}
