using System;
using System.Collections.Generic;
using DSPRE.Avalonia.Data;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// The move visual-effect bytecode (WEST / we_NNN) is a word stream of opcode + args. Most opcodes are
    /// fixed-length; FUNC_CALL/SE_TASK/EX_DATA/CATS_ACT_ADD are variable — a fixed arg holds the count of trailing
    /// payload words. These pin the table and the variable-length parse decoded from west.h.
    /// </summary>
    public class WestScriptTests
    {
        private static byte[] Words(params int[] ws)
        {
            var b = new byte[ws.Length * 4];
            for (int i = 0; i < ws.Length; i++) BitConverter.GetBytes(ws[i]).CopyTo(b, i * 4);
            return b;
        }

        [Fact]
        public void OpcodeTable_KnownLowIds()
        {
            // From west.h DEF_CMD order: WEST_WAIT=0 (1 arg), WEST_SEQEND=4 (0 args).
            Assert.Equal("WEST_WAIT", WestOpcodes.Name(WazaSeqVersion.Plat, 0));
            Assert.True(WestOpcodes.TryGet(WazaSeqVersion.Plat, 0, out var wait));
            Assert.Equal(1, wait.ArgCount);
            Assert.False(wait.IsVariable);
            Assert.Equal("WEST_SEQEND", WestOpcodes.Name(WazaSeqVersion.Plat, 4));
        }

        [Fact]
        public void OpcodeTable_VersionTail()
        {
            Assert.Equal(86, WestOpcodes.Count(WazaSeqVersion.Plat));
            Assert.Equal(89, WestOpcodes.Count(WazaSeqVersion.HGSS));
            Assert.True(WestOpcodes.Id(WazaSeqVersion.HGSS, "WEST_FLASH") >= 0);   // HGSS-only
            Assert.Equal(-1, WestOpcodes.Id(WazaSeqVersion.Plat, "WEST_FLASH"));
        }

        [Fact]
        public void FuncCall_IsVariableLength()
        {
            int fc = WestOpcodes.Id(WazaSeqVersion.Plat, "WEST_FUNC_CALL");
            Assert.True(WestOpcodes.TryGet(WazaSeqVersion.Plat, fc, out var op));
            Assert.True(op.IsVariable);
            Assert.Equal(2, op.ArgCount);       // adrs, cnt
            Assert.Equal(1, op.CountIndex);     // cnt is the 2nd fixed arg
        }

        [Fact]
        public void Parse_HandlesFixedAndVariable()
        {
            int wait = 0;                                          // WEST_WAIT, 1 arg
            int fc = WestOpcodes.Id(WazaSeqVersion.Plat, "WEST_FUNC_CALL");
            int seqEnd = WestOpcodes.Id(WazaSeqVersion.Plat, "WEST_SEQEND");

            // WAIT(5); FUNC_CALL(adrs=100, cnt=3, 7,8,9); SEQEND
            var data = Words(wait, 5, fc, 100, 3, 7, 8, 9, seqEnd);
            var cmds = WestScript.Parse(data, WazaSeqVersion.Plat);

            Assert.Equal(3, cmds.Count);
            Assert.Equal(new[] { 5 }, cmds[0].Args);
            Assert.Equal(fc, cmds[1].OpId);
            Assert.Equal(new[] { 100, 3, 7, 8, 9 }, cmds[1].Args);   // fixed (adrs,cnt) + 3 payload
            Assert.Equal(seqEnd, cmds[2].OpId);
        }

        [Fact]
        public void Parse_StopsWhenVariablePayloadOverruns()
        {
            int fc = WestOpcodes.Id(WazaSeqVersion.Plat, "WEST_FUNC_CALL");
            // cnt=5 but only 2 payload words present → stop, no crash.
            var data = Words(fc, 100, 5, 1, 2);
            Assert.Empty(WestScript.Parse(data, WazaSeqVersion.Plat));
        }

        [Fact]
        public void Cats_ExtractsResourceArcIndices()
        {
            int ch = WestOpcodes.Id(WazaSeqVersion.HGSS, "WEST_CATS_CAHR_RES_LOAD");
            int pl = WestOpcodes.Id(WazaSeqVersion.HGSS, "WEST_CATS_PLTT_RES_LOAD");
            int ce = WestOpcodes.Id(WazaSeqVersion.HGSS, "WEST_CATS_CELL_RES_LOAD");
            int ca = WestOpcodes.Id(WazaSeqVersion.HGSS, "WEST_CATS_CELLANM_RES_LOAD");
            var cmds = new List<WazaSeqCommand>
            {
                new WazaSeqCommand(ch, new[] { 0, 12 }),       // res 0, arc 12
                new WazaSeqCommand(pl, new[] { 0, 13, 1 }),    // res 0, arc 13, pal 1
                new WazaSeqCommand(ce, new[] { 0, 14 }),
                new WazaSeqCommand(ca, new[] { 0, 15 }),
            };
            var r = WestCats.Extract(cmds, WazaSeqVersion.HGSS);
            Assert.True(r.HasCellAnimation);
            Assert.Equal(12, r.Char);
            Assert.Equal(13, r.Pltt);
            Assert.Equal(14, r.Cell);
            Assert.Equal(15, r.CellAnm);
        }

        [Fact]
        public void Cats_AbsentForParticleOnlyScript()
        {
            int load = WestOpcodes.Id(WazaSeqVersion.HGSS, "WEST_LOAD_PARTICLE");
            var cmds = new List<WazaSeqCommand> { new WazaSeqCommand(load, new[] { 0, 1 }) };
            Assert.False(WestCats.Extract(cmds, WazaSeqVersion.HGSS).HasCellAnimation);
        }

        [Fact]
        public void Particles_ResolveLoadAndAddToArchiveEmitterPairs()
        {
            int load = WestOpcodes.Id(WazaSeqVersion.HGSS, "WEST_LOAD_PARTICLE");
            int add = WestOpcodes.Id(WazaSeqVersion.HGSS, "WEST_ADD_PARTICLE");
            var cmds = new List<WazaSeqCommand>
            {
                new WazaSeqCommand(load, new[] { 0, 42 }),     // slot 0 ← archive file 42
                new WazaSeqCommand(add,  new[] { 0, 3, 99 }),  // spawn emitter 3 of slot 0
                new WazaSeqCommand(add,  new[] { 0, 7, 99 }),  // spawn emitter 7 of slot 0
            };
            var refs = WestParticles.Extract(cmds, WazaSeqVersion.HGSS);
            Assert.Equal(2, refs.Count);
            Assert.Equal(42, refs[0].DataNo);
            Assert.Equal(3, refs[0].EmitterNo);
            Assert.Equal(99, refs[0].Callback);   // EMTFUNC_* — drives emitter placement
            Assert.Equal(7, refs[1].EmitterNo);
        }

        [Fact]
        public void Particles_SepBeam_SpreadsEmittersWithLastArgCallback()
        {
            int load = WestOpcodes.Id(WazaSeqVersion.HGSS, "WEST_LOAD_PARTICLE");
            int sep = WestOpcodes.Id(WazaSeqVersion.HGSS, "WEST_ADD_PARTICLE_SEP");
            int seqEnd = WestOpcodes.Id(WazaSeqVersion.HGSS, "WEST_SEQEND");
            var cmds = new List<WazaSeqCommand>
            {
                new WazaSeqCommand(load, new[] { 0, 5 }),
                // ptc, e1..e6, callback(=18 SEP_POS)
                new WazaSeqCommand(sep, new[] { 0, 10, 11, 12, 13, 14, 15, 18 }),
                new WazaSeqCommand(seqEnd, Array.Empty<int>()),
            };
            var refs = WestParticles.Extract(cmds, WazaSeqVersion.HGSS);
            Assert.Equal(6, refs.Count);                 // six beam segments, not one
            Assert.Equal(10, refs[0].EmitterNo);
            Assert.Equal(15, refs[5].EmitterNo);
            Assert.All(refs, r => Assert.Equal(18, r.Callback));   // callback is the LAST arg, shared
            Assert.Equal(6, refs[0].SepCount);
            Assert.Equal(0, refs[0].SepIndex);
            Assert.Equal(5, refs[5].SepIndex);
        }

        [Fact]
        public void Particles_StopAtSeqEnd_IgnoringBranches()
        {
            int load = WestOpcodes.Id(WazaSeqVersion.HGSS, "WEST_LOAD_PARTICLE");
            int add = WestOpcodes.Id(WazaSeqVersion.HGSS, "WEST_ADD_PARTICLE");
            int seqEnd = WestOpcodes.Id(WazaSeqVersion.HGSS, "WEST_SEQEND");
            var cmds = new List<WazaSeqCommand>
            {
                new WazaSeqCommand(load, new[] { 0, 5 }),
                new WazaSeqCommand(add, new[] { 0, 3, 4 }),
                new WazaSeqCommand(seqEnd, Array.Empty<int>()),
                new WazaSeqCommand(add, new[] { 0, 9, 4 }),   // a branch after SEQEND — must be ignored
            };
            Assert.Single(WestParticles.Extract(cmds, WazaSeqVersion.HGSS));
        }

        [Fact]
        public void Particles_FollowSideJumpIntoPerSideBlocks()
        {
            // Mirrors Ominous Wind / Dark Void: ALL the ADD_PARTICLEs live in per-side blocks placed after
            // a SEQEND, reached only through SIDE_JP. A linear scan finds zero emitters for such moves.
            int sideJp = WestOpcodes.Id(WazaSeqVersion.HGSS, "WEST_SIDE_JP");
            int load = WestOpcodes.Id(WazaSeqVersion.HGSS, "WEST_LOAD_PARTICLE");
            int add = WestOpcodes.Id(WazaSeqVersion.HGSS, "WEST_ADD_PARTICLE");
            int seqEnd = WestOpcodes.Id(WazaSeqVersion.HGSS, "WEST_SEQEND");
            var cmds = new List<WazaSeqCommand>
            {
                // word layout: op+args → SIDE_JP occupies words 0..3 (offsets are relative to the word
                // holding each offset, exactly as we_sys.c's WEST_SIDE_JP consumes them).
                new WazaSeqCommand(sideJp, new[] { 0, 3, 10 }),   // player: word2+3=5 → [2]; enemy: word3+10=13 → [5]
                new WazaSeqCommand(seqEnd, Array.Empty<int>()),   // word 4 — the separator, never truly executed
                new WazaSeqCommand(load, new[] { 0, 5 }),         // words 5..7   (player block)
                new WazaSeqCommand(add, new[] { 0, 3, 4 }),       // words 8..11
                new WazaSeqCommand(seqEnd, Array.Empty<int>()),   // word 12
                new WazaSeqCommand(load, new[] { 0, 6 }),         // words 13..15 (enemy block)
                new WazaSeqCommand(add, new[] { 0, 9, 4 }),       // words 16..19
                new WazaSeqCommand(seqEnd, Array.Empty<int>()),   // word 20
            };

            var player = WestParticles.Extract(cmds, WazaSeqVersion.HGSS);
            Assert.Single(player);
            Assert.Equal(5, player[0].DataNo);
            Assert.Equal(3, player[0].EmitterNo);

            var enemy = WestParticles.Extract(cmds, WazaSeqVersion.HGSS, attackerIsEnemy: true);
            Assert.Single(enemy);
            Assert.Equal(6, enemy[0].DataNo);
            Assert.Equal(9, enemy[0].EmitterNo);
        }

        [Fact]
        public void Storyboard_IsFrameStampedAndReadable()
        {
            int wait = 0;   // WEST_WAIT
            int load = WestOpcodes.Id(WazaSeqVersion.Plat, "WEST_LOAD_PARTICLE");
            int seqEnd = WestOpcodes.Id(WazaSeqVersion.Plat, "WEST_SEQEND");
            var cmds = new List<WazaSeqCommand>
            {
                new WazaSeqCommand(wait, new[] { 3 }),
                new WazaSeqCommand(load, new[] { 0, 5 }),
                new WazaSeqCommand(seqEnd, Array.Empty<int>()),
            };
            string sb = WestStoryboard.Build(cmds, WazaSeqVersion.Plat);

            Assert.Contains("f000", sb);                 // first commands at frame 0
            Assert.Contains("wait 3", sb);
            Assert.Contains("load particle", sb);
            Assert.Contains("f003", sb);                 // SEQEND after the 3-frame wait
            Assert.Contains("end", sb);
        }

        [Fact]
        public void Serialize_RoundTripsParse()
        {
            int fc = WestOpcodes.Id(WazaSeqVersion.HGSS, "WEST_FUNC_CALL");
            int seqEnd = WestOpcodes.Id(WazaSeqVersion.HGSS, "WEST_SEQEND");
            var cmds = new List<WazaSeqCommand>
            {
                new WazaSeqCommand(0, new[] { 12 }),                 // WAIT 12
                new WazaSeqCommand(fc, new[] { 0x2000, 2, 4, 5 }),   // FUNC_CALL adrs,cnt=2,+2 payload
                new WazaSeqCommand(seqEnd, Array.Empty<int>()),
            };
            var round = WestScript.Parse(WestScript.Serialize(cmds), WazaSeqVersion.HGSS);
            Assert.Equal(cmds.Count, round.Count);
            for (int i = 0; i < cmds.Count; i++)
            {
                Assert.Equal(cmds[i].OpId, round[i].OpId);
                Assert.Equal(cmds[i].Args, round[i].Args);
            }
        }
    }
}
