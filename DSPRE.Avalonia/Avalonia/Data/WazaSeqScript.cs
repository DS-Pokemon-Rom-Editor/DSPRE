using System;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    public sealed class WazaSeqCommand
    {
        public int OpId;
        public int[] Args;
        public int WordPos;
        /// <summary>Words the reader could not decode, kept as read: OpId is the first, Args the rest.</summary>
        public bool Raw;
        /// <summary>Bytes after the last whole word, written back after this command.</summary>
        public byte[] Tail;
        /// <summary>A file shorter than one word: only <see cref="Tail"/> is written.</summary>
        public bool OnlyTail;
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
                int at = pos;
                int n = WazaSeqOpcodes.ArgCount(version, op, i => at + 1 + i < words ? BitConverter.ToInt32(data, (at + 1 + i) * 4) : 0);
                if (n < 0) break;
                if (pos + 1 + n > words) break;
                var args = new int[n];
                for (int i = 0; i < n; i++) args[i] = BitConverter.ToInt32(data, (pos + 1 + i) * 4);
                cmds.Add(new WazaSeqCommand(op, args) { WordPos = pos });
                pos += 1 + n;
            }
            KeepRest(cmds, data, pos);
            return cmds;
        }

        /// <summary>
        /// Whatever the reader stopped at stays in the script as one raw command, and bytes past the last whole word
        /// ride on the last command, so saving an entry never drops what wasn't understood.
        /// </summary>
        internal static void KeepRest(List<WazaSeqCommand> cmds, byte[] data, int pos)
        {
            int words = data.Length / 4;
            byte[] tail = data.Length % 4 == 0 ? null : data[(words * 4)..];
            if (pos < words)
            {
                var rest = new int[words - pos - 1];
                for (int i = 0; i < rest.Length; i++) rest[i] = BitConverter.ToInt32(data, (pos + 1 + i) * 4);
                cmds.Add(new WazaSeqCommand(BitConverter.ToInt32(data, pos * 4), rest) { WordPos = pos, Raw = true, Tail = tail });
            }
            else if (tail != null)
            {
                if (cmds.Count > 0) cmds[^1].Tail = tail;
                else cmds.Add(new WazaSeqCommand(0, null) { Raw = true, Tail = tail, OnlyTail = true });
            }
        }

        public static byte[] Serialize(IReadOnlyList<WazaSeqCommand> cmds)
        {
            using var ms = new System.IO.MemoryStream();
            foreach (var c in cmds)
            {
                if (!c.OnlyTail)
                {
                    ms.Write(BitConverter.GetBytes(c.OpId));
                    foreach (var a in c.Args) ms.Write(BitConverter.GetBytes(a));
                }
                if (c.Tail != null) ms.Write(c.Tail);
            }
            return ms.ToArray();
        }
    }
}
