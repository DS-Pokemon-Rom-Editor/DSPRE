using System;
using System.Collections.Generic;
using DSPRE.Avalonia.Data;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// The battle move-sequence bytecode (waza_seq / be_seq / sub_seq) is a stream of little-endian 32-bit words:
    /// an opcode id then that opcode's fixed argument words, with the per-word arg count coming from the
    /// version-specific <see cref="WazaSeqOpcodes"/> table for each game.
    /// </summary>
    public class WazaSeqScriptTests
    {
        private static byte[] Words(params int[] ws)
        {
            var b = new byte[ws.Length * 4];
            for (int i = 0; i < ws.Length; i++) BitConverter.GetBytes(ws[i]).CopyTo(b, i * 4);
            return b;
        }

        [Fact]
        public void OpcodeTable_KnownIdsAndNames()
        {
            // Stable low ids shared by every version.
            Assert.Equal("PlayEncounterAnimation", WazaSeqOpcodes.Name(WazaSeqVersion.Plat, 0));
            Assert.Equal(0, WazaSeqOpcodes.ArgCount(WazaSeqVersion.Plat, 0));
            Assert.Equal("ThrowPokeball", WazaSeqOpcodes.Name(WazaSeqVersion.Plat, 7));
            Assert.Equal(2, WazaSeqOpcodes.ArgCount(WazaSeqVersion.Plat, 7));
            Assert.Equal(7, WazaSeqOpcodes.Id(WazaSeqVersion.HGSS, "ThrowPokeball"));
        }

        [Fact]
        public void OpcodeTable_VersionTailsDiverge()
        {
            // Platinum ends at End (id 222); HGSS inserts two commands before it.
            Assert.Equal("End", WazaSeqOpcodes.Name(WazaSeqVersion.Plat, 222));
            Assert.Equal("CheckTrainerMessage", WazaSeqOpcodes.Name(WazaSeqVersion.HGSS, 222));
            Assert.Equal("PrintWhiteOutMessage", WazaSeqOpcodes.Name(WazaSeqVersion.HGSS, 223));

            // DP is the shared prefix (no Platinum additions) with End right after the last DP command.
            Assert.Equal(219, WazaSeqOpcodes.Count(WazaSeqVersion.DP));
            Assert.Equal("End", WazaSeqOpcodes.Name(WazaSeqVersion.DP, 218));
            Assert.Equal(-1, WazaSeqOpcodes.Id(WazaSeqVersion.DP, "WaitTime")); // Platinum-only
        }

        [Fact]
        public void Parse_ReadsOpcodesAndFixedArgs()
        {
            // ThrowPokeball(side=1,type=2) then PokemonSendOut(side=5).
            int appear = WazaSeqOpcodes.Id(WazaSeqVersion.Plat, "PokemonSendOut");
            var data = Words(7, 1, 2, appear, 5);
            var cmds = WazaSeqScript.Parse(data, WazaSeqVersion.Plat);

            Assert.Equal(2, cmds.Count);
            Assert.Equal(7, cmds[0].OpId);
            Assert.Equal(new[] { 1, 2 }, cmds[0].Args);
            Assert.Equal(appear, cmds[1].OpId);
            Assert.Equal(new[] { 5 }, cmds[1].Args);
        }

        [Fact]
        public void Parse_StopsOnUnknownOpcode()
        {
            var data = Words(0 /*ENCOUNT_EFFECT, 0 args*/, 999999 /*not an opcode*/, 1, 2);
            var cmds = WazaSeqScript.Parse(data, WazaSeqVersion.Plat);
            Assert.Single(cmds);
            Assert.Equal(0, cmds[0].OpId);
        }

        [Fact]
        public void Parse_StopsWhenArgsWouldOverrun()
        {
            // ThrowPokeball claims 2 args but only 1 word follows.
            var data = Words(7, 1);
            Assert.Empty(WazaSeqScript.Parse(data, WazaSeqVersion.Plat));
        }

        [Fact]
        public void Serialize_RoundTripsParse()
        {
            int ifop = WazaSeqOpcodes.Id(WazaSeqVersion.HGSS, "CompareVarToValue");      // 4 args
            int seqEnd = WazaSeqOpcodes.Id(WazaSeqVersion.HGSS, "End"); // 0 args
            var cmds = new List<WazaSeqCommand>
            {
                new WazaSeqCommand(ifop, new[] { 1, 2, 3, 4 }),
                new WazaSeqCommand(7, new[] { 0, 1 }),
                new WazaSeqCommand(seqEnd, Array.Empty<int>()),
            };
            var round = WazaSeqScript.Parse(WazaSeqScript.Serialize(cmds), WazaSeqVersion.HGSS);
            Assert.Equal(cmds.Count, round.Count);
            for (int i = 0; i < cmds.Count; i++)
            {
                Assert.Equal(cmds[i].OpId, round[i].OpId);
                Assert.Equal(cmds[i].Args, round[i].Args);
            }
        }
    }
}
