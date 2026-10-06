using System;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    public static class BattleAnimScript
    {
        public static List<WazaSeqCommand> Parse(byte[] data, WazaSeqVersion version)
        {
            List<WazaSeqCommand> cmds = new List<WazaSeqCommand>();
            if (data == null) return cmds;
            int words = data.Length / 4;
            int pos = 0;
            while (pos < words)
            {
                int op = BitConverter.ToInt32(data, pos * 4);
                if (!BattleAnimCommands.TryGet(version, op, out BattleAnimCommand info)) break;
                int n = info.ArgCount;
                if (pos + 1 + n > words) break;

                List<int> args = new List<int>(n);
                for (int i = 0; i < n; i++) args.Add(BitConverter.ToInt32(data, (pos + 1 + i) * 4));

                int total = n;
                if (info.IsVariable)
                {
                    int count = (info.CountIndex >= 0 && info.CountIndex < n) ? args[info.CountIndex] : 0;
                    if (count < 0 || pos + 1 + n + count > words) break;
                    for (int i = 0; i < count; i++) args.Add(BitConverter.ToInt32(data, (pos + 1 + n + i) * 4));
                    total = n + count;
                }

                cmds.Add(new WazaSeqCommand(op, args.ToArray()) { WordPos = pos });
                pos += 1 + total;
            }
            WazaSeqScript.KeepRest(cmds, data, pos);
            return cmds;
        }

        public static byte[] Serialize(IReadOnlyList<WazaSeqCommand> cmds) => WazaSeqScript.Serialize(cmds);
    }
}
