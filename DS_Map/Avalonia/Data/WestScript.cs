using System;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Reads/writes a single move VISUAL-effect script: one <c>we_NNN</c> file in the effect NARC, where the file
    /// index is the move number. The bytecode is a stream of little-endian 32-bit words: an opcode id (see
    /// <see cref="WestOpcodes"/>) followed by its argument words. Most opcodes are fixed-length; the four variable
    /// ones carry a count word (at <see cref="WestOp.CountIndex"/>) giving how many extra payload words trail the
    /// fixed args. Reuses <see cref="WazaSeqCommand"/> (opcode id + flattened args). Linear, tolerant parse.
    /// </summary>
    public static class WestScript
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
                if (!WestOpcodes.TryGet(version, op, out var info)) break;   // unknown opcode → stop
                int n = info.ArgCount;
                if (pos + 1 + n > words) break;                             // fixed args overrun → stop

                var args = new List<int>(n);
                for (int i = 0; i < n; i++) args.Add(BitConverter.ToInt32(data, (pos + 1 + i) * 4));

                int total = n;
                if (info.IsVariable)
                {
                    int count = (info.CountIndex >= 0 && info.CountIndex < n) ? args[info.CountIndex] : 0;
                    if (count < 0 || pos + 1 + n + count > words) break;    // payload overrun / bad count → stop
                    for (int i = 0; i < count; i++) args.Add(BitConverter.ToInt32(data, (pos + 1 + n + i) * 4));
                    total = n + count;
                }

                cmds.Add(new WazaSeqCommand(op, args.ToArray()) { WordPos = pos });
                pos += 1 + total;
            }
            return cmds;
        }

        /// <summary>Serializes commands back to a little-endian word blob. The count word of a variable opcode is
        /// just one of its args, so writing opcode + args round-trips correctly.</summary>
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
