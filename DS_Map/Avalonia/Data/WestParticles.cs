using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    /// <summary>One particle spawn a WEST script requests: <see cref="DataNo"/> = the SPA archive file index in
    /// waza_particle.narc (from LOAD_PARTICLE), <see cref="EmitterNo"/> = which emitter resource in that archive
    /// (from ADD_PARTICLE).</summary>
    public readonly struct WestParticleRef
    {
        public readonly int DataNo;
        public readonly int EmitterNo;
        public readonly int Callback;   // EMTFUNC_* — places the emitter (attacker/defender) + sets its travel axis
        public readonly int SepIndex;   // for ADD_PARTICLE_SEP beams: this segment's index…
        public readonly int SepCount;   // …out of how many (1 = not a SEP beam). Used to spread along the path.
        public WestParticleRef(int dataNo, int emitterNo, int callback, int sepIndex = 0, int sepCount = 1)
        { DataNo = dataNo; EmitterNo = emitterNo; Callback = callback; SepIndex = sepIndex; SepCount = sepCount; }
    }

    /// <summary>
    /// Resolves a WEST move-effect script's particle usage: <c>LOAD_PARTICLE ptc_no, data_no</c> binds a particle
    /// slot to an SPA archive file; <c>ADD_PARTICLE ptc_no, emitter_no, cb</c> spawns emitter <c>emitter_no</c> of
    /// whatever archive that slot holds. Returns the (archive file, emitter) pairs the move actually emits.
    /// </summary>
    public static class WestParticles
    {
        /// <summary>
        /// Walks the script the way the game's sequencer would for the previewed side, following the branch
        /// opcodes (SIDE_JP / TURN_CHK / SEQ_JP / SEQ_CALL), and stops at the SEQEND that actually terminates
        /// that path. A plain linear scan that stops at the FIRST SEQEND misses most side-branched moves
        /// entirely — e.g. Dark Void's and Ominous Wind's ADD_PARTICLEs all live in per-side blocks placed
        /// after a SEQEND and reached only via SIDE_JP.
        /// </summary>
        public static List<WestParticleRef> Extract(IReadOnlyList<WazaSeqCommand> cmds, WazaSeqVersion version,
                                                    bool attackerIsEnemy = false)
        {
            var slot = new Dictionary<int, int>();   // ptc_no → data_no
            var refs = new List<WestParticleRef>();
            if (cmds == null || cmds.Count == 0) return refs;

            // Recompute word positions (commands may come from the UI grid without parsed WordPos) —
            // identical layout math to WestPlayer's constructor.
            var wordToIndex = new Dictionary<int, int>();
            var wordPos = new int[cmds.Count];
            int wp = 0;
            for (int i = 0; i < cmds.Count; i++)
            {
                wordPos[i] = wp;
                wordToIndex[wp] = i;
                wp += 1 + cmds[i].Args.Length;
            }
            bool Jump(ref int pc, int argWord, int offset)
            {
                if (wordToIndex.TryGetValue(argWord + offset, out int idx)) { pc = idx; return true; }
                return false;
            }

            var callStack = new List<int>();
            var visited = new HashSet<int>();
            int pc = 0, guard = 0;
            while (pc >= 0 && pc < cmds.Count && guard++ < 100000)
            {
                var c = cmds[pc];
                // A revisited command outside a call continuation means a loop we don't model — stop.
                if (!visited.Add(pc) && callStack.Count == 0) break;
                int cur = pc;
                pc++;

                string name = WestOpcodes.Name(version, c.OpId);
                if (name == null) continue;
                int n = c.Args.Length;

                switch (name)
                {
                    case "WEST_SEQEND":
                        return refs;

                    case "WEST_SEQ_JP":
                        if (n >= 1) Jump(ref pc, wordPos[cur] + 1, c.Args[0]);
                        break;

                    // type, adrsPlayer, adrsEnemy (relative to the word holding each offset)
                    case "WEST_SIDE_JP":
                        if (n >= 3)
                        {
                            bool checkedIsEnemy = c.Args[0] == 0 ? attackerIsEnemy : !attackerIsEnemy;
                            if (checkedIsEnemy) Jump(ref pc, wordPos[cur] + 3, c.Args[2]);
                            else Jump(ref pc, wordPos[cur] + 2, c.Args[1]);
                        }
                        break;

                    // WEST_TURN_CHK: pick ONE branch by waza_eff_cnt parity (fresh battle = even = the
                    // first branch), exactly like the game — mirrors WestPlayer.
                    case "WEST_TURN_CHK":
                        if (n >= 1 && Jump(ref pc, wordPos[cur] + 1, c.Args[0])) break;
                        if (n >= 2) Jump(ref pc, wordPos[cur] + 2, c.Args[1]);
                        break;

                    case "WEST_SEQ_CALL":
                        if (n >= 1) { callStack.Add(pc); Jump(ref pc, wordPos[cur] + 1, c.Args[0]); }
                        break;
                    case "WEST_END_CALL":
                        if (callStack.Count > 0) { pc = callStack[callStack.Count - 1]; callStack.RemoveAt(callStack.Count - 1); }
                        break;

                    case "WEST_LOAD_PARTICLE":
                    case "WEST_LOAD_PARTICLE_EX":
                        if (n >= 2) slot[c.Args[0]] = c.Args[1];
                        break;

                    // ptc_no, emitter, callback
                    case "WEST_ADD_PARTICLE":
                        if (n >= 2 && slot.TryGetValue(c.Args[0], out int d0))
                            refs.Add(new WestParticleRef(d0, c.Args[1], n >= 3 ? c.Args[2] : 0));
                        break;

                    // ptc_no, emit_no, data_no, callback
                    case "WEST_ADD_PARTICLE_EMIT_SET":
                        if (n >= 4 && slot.TryGetValue(c.Args[0], out int d1))
                            refs.Add(new WestParticleRef(d1, c.Args[2], c.Args[3]));
                        break;

                    // ptc_no, data1..N, callback — a SEP beam / party fan: N emitters spread along the path.
                    case "WEST_ADD_PARTICLE_SEP":
                    case "WEST_ADD_PARTICLE_PTAT":
                        if (n >= 3 && slot.TryGetValue(c.Args[0], out int d2))
                        {
                            int cb = c.Args[n - 1];
                            int count = n - 2;   // emitters between ptc_no and callback
                            for (int k = 0; k < count; k++)
                                refs.Add(new WestParticleRef(d2, c.Args[1 + k], cb, k, count));
                        }
                        break;
                }
            }
            return refs;
        }
    }
}
