using System;
using System.Collections.Generic;
using DSPRE.Avalonia.Data;
using Xunit;

namespace DSPRE.Tests
{
    public class BattleAnimScriptTests
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
            Assert.Equal("Delay", BattleAnimCommands.Name(WazaSeqVersion.Plat, 0));
            Assert.True(BattleAnimCommands.TryGet(WazaSeqVersion.Plat, 0, out var wait));
            Assert.Equal(1, wait.ArgCount);
            Assert.False(wait.IsVariable);
            Assert.Equal("End", BattleAnimCommands.Name(WazaSeqVersion.Plat, 4));
        }

        [Fact]
        public void OpcodeTable_VersionTail()
        {
            Assert.Equal(85, BattleAnimCommands.Count(WazaSeqVersion.Plat));
            Assert.Equal(88, BattleAnimCommands.Count(WazaSeqVersion.HGSS));
            Assert.True(BattleAnimCommands.Id(WazaSeqVersion.HGSS, "FlashScreen") >= 0);   // HGSS-only
            Assert.Equal(-1, BattleAnimCommands.Id(WazaSeqVersion.Plat, "FlashScreen"));
        }

        [Fact]
        public void FuncCall_IsVariableLength()
        {
            int fc = BattleAnimCommands.Id(WazaSeqVersion.Plat, "CallFunc");
            Assert.True(BattleAnimCommands.TryGet(WazaSeqVersion.Plat, fc, out var op));
            Assert.True(op.IsVariable);
            Assert.Equal(2, op.ArgCount);       // adrs, cnt
            Assert.Equal(1, op.CountIndex);     // cnt is the 2nd fixed arg
        }

        [Fact]
        public void Parse_HandlesFixedAndVariable()
        {
            int wait = 0;                                          // Delay, 1 arg
            int fc = BattleAnimCommands.Id(WazaSeqVersion.Plat, "CallFunc");
            int seqEnd = BattleAnimCommands.Id(WazaSeqVersion.Plat, "End");

            // Delay(5); CallFunc(adrs=100, cnt=3, 7,8,9); End
            var data = Words(wait, 5, fc, 100, 3, 7, 8, 9, seqEnd);
            var cmds = BattleAnimScript.Parse(data, WazaSeqVersion.Plat);

            Assert.Equal(3, cmds.Count);
            Assert.Equal(new[] { 5 }, cmds[0].Args);
            Assert.Equal(fc, cmds[1].OpId);
            Assert.Equal(new[] { 100, 3, 7, 8, 9 }, cmds[1].Args);   // fixed (adrs,cnt) + 3 payload
            Assert.Equal(seqEnd, cmds[2].OpId);
        }

        [Fact]
        public void Parse_StopsWhenVariablePayloadOverruns()
        {
            int fc = BattleAnimCommands.Id(WazaSeqVersion.Plat, "CallFunc");
            // cnt=5 but only 2 payload words present → stop, no crash.
            var data = Words(fc, 100, 5, 1, 2);
            Assert.Empty(BattleAnimScript.Parse(data, WazaSeqVersion.Plat));
        }

        [Fact]
        public void Cats_ExtractsResourceArcIndices()
        {
            int ch = BattleAnimCommands.Id(WazaSeqVersion.HGSS, "LoadCharResObj");
            int pl = BattleAnimCommands.Id(WazaSeqVersion.HGSS, "LoadPlttRes");
            int ce = BattleAnimCommands.Id(WazaSeqVersion.HGSS, "LoadCellResObj");
            int ca = BattleAnimCommands.Id(WazaSeqVersion.HGSS, "LoadAnimResObj");
            var cmds = new List<WazaSeqCommand>
            {
                new WazaSeqCommand(ch, new[] { 0, 12 }),       // res 0, arc 12
                new WazaSeqCommand(pl, new[] { 0, 13, 1 }),    // res 0, arc 13, pal 1
                new WazaSeqCommand(ce, new[] { 0, 14 }),
                new WazaSeqCommand(ca, new[] { 0, 15 }),
            };
            var r = BattleAnimSprites.Extract(cmds, WazaSeqVersion.HGSS);
            Assert.True(r.HasCellAnimation);
            Assert.Equal(12, r.Char);
            Assert.Equal(13, r.Pltt);
            Assert.Equal(14, r.Cell);
            Assert.Equal(15, r.CellAnm);
        }

        [Fact]
        public void Cats_AbsentForParticleOnlyScript()
        {
            int load = BattleAnimCommands.Id(WazaSeqVersion.HGSS, "LoadParticleSystem");
            var cmds = new List<WazaSeqCommand> { new WazaSeqCommand(load, new[] { 0, 1 }) };
            Assert.False(BattleAnimSprites.Extract(cmds, WazaSeqVersion.HGSS).HasCellAnimation);
        }

        [Fact]
        public void Particles_ResolveLoadAndAddToArchiveEmitterPairs()
        {
            int load = BattleAnimCommands.Id(WazaSeqVersion.HGSS, "LoadParticleSystem");
            int add = BattleAnimCommands.Id(WazaSeqVersion.HGSS, "CreateEmitter");
            var cmds = new List<WazaSeqCommand>
            {
                new WazaSeqCommand(load, new[] { 0, 42 }),     // slot 0 ← archive file 42
                new WazaSeqCommand(add,  new[] { 0, 3, 99 }),  // spawn emitter 3 of slot 0
                new WazaSeqCommand(add,  new[] { 0, 7, 99 }),  // spawn emitter 7 of slot 0
            };
            var refs = BattleAnimParticles.Extract(cmds, WazaSeqVersion.HGSS);
            Assert.Equal(2, refs.Count);
            Assert.Equal(42, refs[0].DataNo);
            Assert.Equal(3, refs[0].EmitterNo);
            Assert.Equal(99, refs[0].Callback);   // EMTFUNC_*, drives emitter placement
            Assert.Equal(7, refs[1].EmitterNo);
        }

        [Fact]
        public void Particles_SepBeam_SpreadsEmittersWithLastArgCallback()
        {
            int load = BattleAnimCommands.Id(WazaSeqVersion.HGSS, "LoadParticleSystem");
            int sep = BattleAnimCommands.Id(WazaSeqVersion.HGSS, "CreateEmitterForMove");
            int seqEnd = BattleAnimCommands.Id(WazaSeqVersion.HGSS, "End");
            var cmds = new List<WazaSeqCommand>
            {
                new WazaSeqCommand(load, new[] { 0, 5 }),
                // ptc, e1..e6, callback(=18)
                new WazaSeqCommand(sep, new[] { 0, 10, 11, 12, 13, 14, 15, 18 }),
                new WazaSeqCommand(seqEnd, Array.Empty<int>()),
            };
            var refs = BattleAnimParticles.Extract(cmds, WazaSeqVersion.HGSS);
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
            int load = BattleAnimCommands.Id(WazaSeqVersion.HGSS, "LoadParticleSystem");
            int add = BattleAnimCommands.Id(WazaSeqVersion.HGSS, "CreateEmitter");
            int seqEnd = BattleAnimCommands.Id(WazaSeqVersion.HGSS, "End");
            var cmds = new List<WazaSeqCommand>
            {
                new WazaSeqCommand(load, new[] { 0, 5 }),
                new WazaSeqCommand(add, new[] { 0, 3, 4 }),
                new WazaSeqCommand(seqEnd, Array.Empty<int>()),
                new WazaSeqCommand(add, new[] { 0, 9, 4 }),   // a branch after End, must be ignored
            };
            Assert.Single(BattleAnimParticles.Extract(cmds, WazaSeqVersion.HGSS));
        }

        [Fact]
        public void Particles_FollowSideJumpIntoPerSideBlocks()
        {
            // Ominous Wind and Dark Void keep every emitter after End, reachable only through the side jump.
            int sideJp = BattleAnimCommands.Id(WazaSeqVersion.HGSS, "JumpIfBattlerSide");
            int load = BattleAnimCommands.Id(WazaSeqVersion.HGSS, "LoadParticleSystem");
            int add = BattleAnimCommands.Id(WazaSeqVersion.HGSS, "CreateEmitter");
            int seqEnd = BattleAnimCommands.Id(WazaSeqVersion.HGSS, "End");
            var cmds = new List<WazaSeqCommand>
            {
                // Offsets count from the word that holds them.
                new WazaSeqCommand(sideJp, new[] { 0, 3, 10 }),   // player: word2+3=5 → [2]; enemy: word3+10=13 → [5]
                new WazaSeqCommand(seqEnd, Array.Empty<int>()),   // word 4, the separator, never truly executed
                new WazaSeqCommand(load, new[] { 0, 5 }),         // words 5..7   (player block)
                new WazaSeqCommand(add, new[] { 0, 3, 4 }),       // words 8..11
                new WazaSeqCommand(seqEnd, Array.Empty<int>()),   // word 12
                new WazaSeqCommand(load, new[] { 0, 6 }),         // words 13..15 (enemy block)
                new WazaSeqCommand(add, new[] { 0, 9, 4 }),       // words 16..19
                new WazaSeqCommand(seqEnd, Array.Empty<int>()),   // word 20
            };

            var player = BattleAnimParticles.Extract(cmds, WazaSeqVersion.HGSS);
            Assert.Single(player);
            Assert.Equal(5, player[0].DataNo);
            Assert.Equal(3, player[0].EmitterNo);

            var enemy = BattleAnimParticles.Extract(cmds, WazaSeqVersion.HGSS, attackerIsEnemy: true);
            Assert.Single(enemy);
            Assert.Equal(6, enemy[0].DataNo);
            Assert.Equal(9, enemy[0].EmitterNo);
        }

        [Fact]
        public void Storyboard_IsFrameStampedAndReadable()
        {
            int wait = 0;   // Delay
            int load = BattleAnimCommands.Id(WazaSeqVersion.Plat, "LoadParticleSystem");
            int seqEnd = BattleAnimCommands.Id(WazaSeqVersion.Plat, "End");
            var cmds = new List<WazaSeqCommand>
            {
                new WazaSeqCommand(wait, new[] { 3 }),
                new WazaSeqCommand(load, new[] { 0, 5 }),
                new WazaSeqCommand(seqEnd, Array.Empty<int>()),
            };
            var sb = BattleAnimStoryboard.Build(cmds, WazaSeqVersion.Plat);

            Assert.Equal(3, sb.Count);
            Assert.Equal("f000", sb[0].Frame);
            Assert.StartsWith("wait 3", sb[0].Text);
            Assert.Equal("f003", sb[1].Frame);           // the load runs after the 3-frame wait
            Assert.StartsWith("load particle", sb[1].Text);
            Assert.Equal("f003", sb[2].Frame);
            Assert.Equal("end", sb[2].Text);
        }

        [Fact]
        public void Serialize_RoundTripsParse()
        {
            int fc = BattleAnimCommands.Id(WazaSeqVersion.HGSS, "CallFunc");
            int seqEnd = BattleAnimCommands.Id(WazaSeqVersion.HGSS, "End");
            var cmds = new List<WazaSeqCommand>
            {
                new WazaSeqCommand(0, new[] { 12 }),                 // WAIT 12
                new WazaSeqCommand(fc, new[] { 0x2000, 2, 4, 5 }),   // CallFunc adrs,cnt=2,+2 payload
                new WazaSeqCommand(seqEnd, Array.Empty<int>()),
            };
            var round = BattleAnimScript.Parse(BattleAnimScript.Serialize(cmds), WazaSeqVersion.HGSS);
            Assert.Equal(cmds.Count, round.Count);
            for (int i = 0; i < cmds.Count; i++)
            {
                Assert.Equal(cmds[i].OpId, round[i].OpId);
                Assert.Equal(cmds[i].Args, round[i].Args);
            }
        }
    }
}
