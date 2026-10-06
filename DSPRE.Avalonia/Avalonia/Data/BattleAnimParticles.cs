using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    public readonly struct BattleAnimParticleRef
    {
        public readonly int DataNo;
        public readonly int EmitterNo;
        public readonly int Callback;
        public readonly int SepIndex;
        public readonly int SepCount;
        public BattleAnimParticleRef(int dataNo, int emitterNo, int callback, int sepIndex = 0, int sepCount = 1)
        { DataNo = dataNo; EmitterNo = emitterNo; Callback = callback; SepIndex = sepIndex; SepCount = sepCount; }
    }

    public static class BattleAnimParticles
    {
        public static List<BattleAnimParticleRef> Extract(IReadOnlyList<WazaSeqCommand> cmds, WazaSeqVersion version,
                                                    bool attackerIsEnemy = false)
        {
            Dictionary<int, int> slot = new Dictionary<int, int>();
            List<BattleAnimParticleRef> refs = new List<BattleAnimParticleRef>();
            if (cmds == null || cmds.Count == 0) return refs;

            Dictionary<int, int> wordToIndex = new Dictionary<int, int>();
            int[] wordPos = new int[cmds.Count];
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

            List<int> callStack = new List<int>();
            HashSet<int> visited = new HashSet<int>();
            int pc = 0, guard = 0;
            while (pc >= 0 && pc < cmds.Count && guard++ < 100000)
            {
                WazaSeqCommand c = cmds[pc];
                if (!visited.Add(pc) && callStack.Count == 0) break;
                int cur = pc;
                pc++;

                string name = BattleAnimCommands.Name(version, c.OpId);
                if (name == null) continue;
                int n = c.Args.Length;

                switch (name)
                {
                    case "End":
                        return refs;

                    case "Jump":
                        if (n >= 1) Jump(ref pc, wordPos[cur] + 1, c.Args[0]);
                        break;

                    case "JumpIfBattlerSide":
                        if (n >= 3)
                        {
                            bool checkedIsEnemy = c.Args[0] == 0 ? attackerIsEnemy : !attackerIsEnemy;
                            if (checkedIsEnemy) Jump(ref pc, wordPos[cur] + 3, c.Args[2]);
                            else Jump(ref pc, wordPos[cur] + 2, c.Args[1]);
                        }
                        break;

                    case "JumpByTurn":
                        if (n >= 1 && Jump(ref pc, wordPos[cur] + 1, c.Args[0])) break;
                        if (n >= 2) Jump(ref pc, wordPos[cur] + 2, c.Args[1]);
                        break;

                    case "Call":
                        if (n >= 1) { callStack.Add(pc); Jump(ref pc, wordPos[cur] + 1, c.Args[0]); }
                        break;
                    case "Return":
                        if (callStack.Count > 0) { pc = callStack[callStack.Count - 1]; callStack.RemoveAt(callStack.Count - 1); }
                        break;

                    case "LoadParticleSystem":
                    case "LoadDebugParticleSystem":
                        if (n >= 2) slot[c.Args[0]] = c.Args[1];
                        break;

                    case "CreateEmitter":
                        if (n >= 2 && slot.TryGetValue(c.Args[0], out int d0))
                            refs.Add(new BattleAnimParticleRef(d0, c.Args[1], n >= 3 ? c.Args[2] : 0));
                        break;

                    case "CreateEmitterEx":
                        if (n >= 4 && slot.TryGetValue(c.Args[0], out int d1))
                            refs.Add(new BattleAnimParticleRef(d1, c.Args[2], c.Args[3]));
                        break;

                    case "CreateEmitterForMove":
                    case "CreateEmitterForFriendlyFire":
                        if (n >= 3 && slot.TryGetValue(c.Args[0], out int d2))
                        {
                            int cb = c.Args[n - 1];
                            int count = n - 2;
                            for (int k = 0; k < count; k++)
                                refs.Add(new BattleAnimParticleRef(d2, c.Args[1 + k], cb, k, count));
                        }
                        break;
                }
            }
            return refs;
        }
    }
}
