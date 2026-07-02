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
        public static List<WestParticleRef> Extract(IReadOnlyList<WazaSeqCommand> cmds, WazaSeqVersion version)
        {
            var slot = new Dictionary<int, int>();   // ptc_no → data_no
            var refs = new List<WestParticleRef>();
            if (cmds == null) return refs;
            foreach (var c in cmds)
            {
                string name = WestOpcodes.Name(version, c.OpId);
                if (name == null) continue;

                // Only the default fall-through path emits in the preview; conditional branches (PTAT/contest)
                // live after the first SEQEND and are reached by jumps we don't follow.
                if (name == "WEST_SEQEND") break;

                int n = c.Args.Length;
                switch (name)
                {
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
