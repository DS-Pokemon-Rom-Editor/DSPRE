using System;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    /// <summary>One entry in the move-script command guide: a command's single-word name, friendly title,
    /// parameter list and one-line description.</summary>
    public readonly struct GuideEntry
    {
        public string Command { get; }
        public string Title { get; }
        public string Params { get; }
        public string Description { get; }

        public GuideEntry(string command, string title, string paramsText, string description)
        {
            Command = command; Title = title; Params = paramsText; Description = description;
        }
    }

    /// <summary>Builds the reference list shown by the move-script command guide, covering every opcode known
    /// to either game version so the guide stays useful regardless of which ROM is loaded.</summary>
    public static class ScriptCommandGuide
    {
        public static IReadOnlyList<GuideEntry> ForWest()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list = new List<GuideEntry>();
            foreach (var op in WestOpcodes.Table(WazaSeqVersion.Plat)) Add(list, seen, op.Name);
            foreach (var op in WestOpcodes.Table(WazaSeqVersion.HGSS)) Add(list, seen, op.Name);
            list.Sort((a, b) => string.CompareOrdinal(a.Command, b.Command));
            return list;
        }

        public static IReadOnlyList<GuideEntry> ForWazaSeq()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list = new List<GuideEntry>();
            foreach (var v in new[] { WazaSeqVersion.DP, WazaSeqVersion.Plat, WazaSeqVersion.HGSS })
                foreach (var op in WazaSeqOpcodes.Table(v)) Add(list, seen, op.Name);
            list.Sort((a, b) => string.CompareOrdinal(a.Command, b.Command));
            return list;
        }

        private static void Add(List<GuideEntry> list, HashSet<string> seen, string opName)
        {
            if (!seen.Add(opName)) return;
            string command = WestParamSchema.CommandName(opName);
            string title = WestParamSchema.OpcodeDisplay(opName);
            string desc = WestParamSchema.OpcodeDoc(opName);
            var ps = new List<string>();
            for (int i = 0; i < 16; i++)
            {
                string label = WestParamSchema.ParamName(opName, i);
                if (label.StartsWith("Param ", StringComparison.Ordinal)) break;
                ps.Add(WestParamSchema.ArgToken(opName, i));
            }
            list.Add(new GuideEntry(command, title, string.Join(", ", ps), desc));
        }
    }
}
