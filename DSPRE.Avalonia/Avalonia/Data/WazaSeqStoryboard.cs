using System.Collections.Generic;
using System.Text;

namespace DSPRE.Avalonia.Data
{
    public static class WazaSeqStoryboard
    {
        public static string Build(IReadOnlyList<WazaSeqCommand> cmds, WazaSeqVersion version)
        {
            if (cmds == null || cmds.Count == 0) return "(empty script)";
            var sb = new StringBuilder();
            for (int i = 0; i < cmds.Count; i++)
            {
                var c = cmds[i];
                string op = WazaSeqOpcodes.Name(version, c.OpId) ?? ("op" + c.OpId);
                sb.Append((i + 1).ToString("D3")).Append(".  ").Append(BattleAnimSchema.OpcodeDisplay(op, script: true));

                if (c.Args != null && c.Args.Length > 0)
                {
                    sb.Append("  (");
                    for (int a = 0; a < c.Args.Length; a++)
                    {
                        if (a > 0) sb.Append(", ");
                        string label = BattleAnimSchema.ParamName(op, a, script: true);
                        if (label.StartsWith("Param ")) sb.Append(c.Args[a]);
                        else sb.Append(label).Append(' ').Append(c.Args[a]);
                    }
                    sb.Append(')');
                }
                string doc = BattleAnimSchema.OpcodeDoc(op, script: true);
                if (!string.IsNullOrEmpty(doc)) sb.Append("\n        ").Append(doc);
                sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
