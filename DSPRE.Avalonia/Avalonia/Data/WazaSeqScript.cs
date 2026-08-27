using System;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    /// <summary>One decoded battle move-sequence command: an opcode id (index into the version's
    /// <see cref="WazaSeqOpcodes"/> table) plus its fixed argument words.</summary>
    public sealed class WazaSeqCommand
    {
        public int OpId;
        public int[] Args;
        public int WordPos;   // starting word index in the source blob; lets branch opcodes resolve word-relative targets
        public WazaSeqCommand(int opId, int[] args) { OpId = opId; Args = args ?? Array.Empty<int>(); }
        public override string ToString() =>
            Args.Length == 0 ? $"#{OpId}" : $"#{OpId} {string.Join(", ", Args)}";
    }

    /// <summary>
    /// Reads/writes a single battle move-sequence script: one file in waza_seq.narc (per move), be_seq.narc (per
    /// move effect) or sub_seq.narc (shared subroutines). The bytecode is a stream of little-endian 32-bit words:
    /// an opcode id followed by that opcode's fixed argument words (count from <see cref="WazaSeqOpcodes"/> for the
    /// game version). Parsing is linear over the WHOLE blob; branch/jump opcodes target later commands by relative
    /// word offset, so it does not stop at WS_SEQ_END. Tolerant: stops if it hits an unknown opcode or the args
    /// would overrun (returns what parsed so far).
    /// </summary>
    public static class WazaSeqScript
    {
        public static List<WazaSeqCommand> Parse(byte[] data, WazaSeqVersion version)
        {
            var cmds = new List<WazaSeqCommand>();
            if (data == null) return cmds;
            int words = data.Length / 4;
            int pos = 0;
            while (pos < words)
            {
                int op = BitConverter.ToInt32(data, pos * 4);
                int n = WazaSeqOpcodes.ArgCount(version, op);
                if (n < 0) break;                   // not a valid opcode for this version → stop
                if (pos + 1 + n > words) break;     // args would overrun → stop
                var args = new int[n];
                for (int i = 0; i < n; i++) args[i] = BitConverter.ToInt32(data, (pos + 1 + i) * 4);
                cmds.Add(new WazaSeqCommand(op, args));
                pos += 1 + n;
            }
            return cmds;
        }

        public static byte[] Serialize(IReadOnlyList<WazaSeqCommand> cmds)
        {
            int words = 0;
            foreach (var c in cmds) words += 1 + c.Args.Length;
            var data = new byte[words * 4];
            int pos = 0;
            void W(int v) { BitConverter.GetBytes(v).CopyTo(data, pos * 4); pos++; }
            foreach (var c in cmds) { W(c.OpId); foreach (var a in c.Args) W(a); }
            return data;
        }
    }
}
