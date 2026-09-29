using System;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    public sealed class WazaSeqCommand
    {
        public int OpId;
        public int[] Args;
        public int WordPos;
        public WazaSeqCommand(int opId, int[] args) { OpId = opId; Args = args ?? Array.Empty<int>(); }
        public override string ToString() =>
            Args.Length == 0 ? $"#{OpId}" : $"#{OpId} {string.Join(", ", Args)}";
    }

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
                if (n < 0) break;
                if (pos + 1 + n > words) break;
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
