using System.Collections.Generic;
using System.Text;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Renders a battle move / effect-sequence (WS_* ServerControl VM) command list as a plain-English summary:
    /// one numbered line per command with its friendly name, labelled arguments and a short description of what it
    /// does. The counterpart to <see cref="WestStoryboard"/> (which covers the WEST visual-effect scripts), so a
    /// reader can follow "what the move does" without decoding raw opcodes.
    /// </summary>
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
                sb.Append((i + 1).ToString("D3")).Append(".  ").Append(WestParamSchema.OpcodeDisplay(op));

                if (c.Args != null && c.Args.Length > 0)
                {
                    sb.Append("  (");
                    for (int a = 0; a < c.Args.Length; a++)
                    {
                        if (a > 0) sb.Append(", ");
                        string label = WestParamSchema.ParamName(op, a);
                        if (label.StartsWith("Param ")) sb.Append(c.Args[a]);          // unlabelled → just the value
                        else sb.Append(label).Append(' ').Append(c.Args[a]);
                    }
                    sb.Append(')');
                }
                string doc = WestParamSchema.OpcodeDoc(op);
                if (!string.IsNullOrEmpty(doc)) sb.Append("\n        ").Append(doc);   // wrap the description under it
                sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
