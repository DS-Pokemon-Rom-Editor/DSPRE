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
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<GuideEntry> list = new List<GuideEntry>();
            foreach (BattleAnimCommand op in BattleAnimCommands.Table(WazaSeqVersion.Plat)) Add(list, seen, op.Name, false);
            foreach (BattleAnimCommand op in BattleAnimCommands.Table(WazaSeqVersion.HGSS)) Add(list, seen, op.Name, false);
            list.Sort((a, b) => string.CompareOrdinal(a.Command, b.Command));
            return list;
        }

        public static IReadOnlyList<GuideEntry> ForWazaSeq()
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<GuideEntry> list = new List<GuideEntry>();
            foreach (WazaSeqVersion v in new[] { WazaSeqVersion.DP, WazaSeqVersion.Plat, WazaSeqVersion.HGSS })
                foreach (WazaSeqOp op in WazaSeqOpcodes.Table(v)) Add(list, seen, op.Name, true);
            list.Sort((a, b) => string.CompareOrdinal(a.Command, b.Command));
            return list;
        }

        /// <summary>The loaded ROM's event-script commands, from the active script command database.</summary>
        public static IReadOnlyList<GuideEntry> ForEventScripts()
        {
            List<GuideEntry> list = new List<GuideEntry>();
            foreach (KeyValuePair<ushort, DSPRE.Resources.ScriptCommandInfo> kv in RomInfo.GetScriptCommandInfoDict())
            {
                DSPRE.Resources.ScriptCommandInfo info = kv.Value;
                List<string> ps = new List<string>();
                if (info.HasConditionalParameters) ps.Add("depends on the first value");
                else
                    for (int i = 0; i < info.ParameterCount; i++)
                    {
                        // Database names already carry their type; an unnamed one shows its size instead.
                        bool named = info.ParameterNames != null && i < info.ParameterNames.Count && !string.IsNullOrWhiteSpace(info.ParameterNames[i]);
                        ps.Add(named ? info.ParameterNames[i] : $"{info.ParameterSizes[i]} bytes");
                    }
                list.Add(new GuideEntry(info.Name, $"0x{kv.Key:X4}", string.Join(", ", ps), info.Description ?? ""));
            }
            list.Sort((a, b) => string.CompareOrdinal(a.Title, b.Title));
            return list;
        }

        public static IReadOnlyList<GuideEntry> ForMovements()
        {
            List<GuideEntry> list = new List<GuideEntry>();
            foreach (KeyValuePair<ushort, DSPRE.Resources.MovementCommandInfo> kv in DSPRE.Resources.ScriptDatabase.movementsDict)
                list.Add(new GuideEntry(kv.Value.Name, $"0x{kv.Key:X4}", "", kv.Value.Description ?? ""));
            list.Sort((a, b) => string.CompareOrdinal(a.Title, b.Title));
            return list;
        }

        public static IReadOnlyList<GuideEntry> ForComparisonOperators()
        {
            List<GuideEntry> list = new List<GuideEntry>();
            foreach (KeyValuePair<ushort, string> kv in DSPRE.Resources.ScriptDatabase.comparisonOperatorsDict)
                list.Add(new GuideEntry(kv.Value, kv.Key.ToString(), "", ""));
            list.Sort((a, b) => string.CompareOrdinal(a.Title, b.Title));
            return list;
        }

        private static void Add(List<GuideEntry> list, HashSet<string> seen, string opName, bool script)
        {
            if (!seen.Add(opName)) return;
            string command = BattleAnimSchema.CommandName(opName, script);
            string title = BattleAnimSchema.OpcodeDisplay(opName, script);
            string desc = BattleAnimSchema.OpcodeDoc(opName, script);
            List<string> ps = new List<string>();
            for (int i = 0; i < 16; i++)
            {
                string label = BattleAnimSchema.ParamName(opName, i, script);
                if (label.StartsWith("Param ", StringComparison.Ordinal)) break;
                ps.Add(BattleAnimSchema.ArgToken(opName, i, script));
            }
            list.Add(new GuideEntry(command, title, string.Join(", ", ps), desc));
        }
    }
}
