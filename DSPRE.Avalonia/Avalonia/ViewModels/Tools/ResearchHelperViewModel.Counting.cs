using Avalonia.Controls;
using DSPRE.HgEngine;
using DSPRE.Avalonia;
using DSPRE.Editors.Utils;
using DSPRE.Resources;
using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Tools
{
    public partial class ResearchHelperViewModel
    {
        private static bool TryParseNumber(string text, bool hexMode, out int result)
        {
            if (hexMode)
            {
                string hex = text;
                if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) hex = hex.Substring(2);
                return int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result);
            }
            return int.TryParse(text, out result);
        }

        private static int CountVariableInScriptFile(ScriptFile sf, int varNum, Dictionary<ushort, ScriptCommandInfo> dict)
        {
            int count = 0;
            if (sf.AllScripts != null)
                foreach (ScriptCommandContainer s in sf.AllScripts) count += CountVariableInCommands(s.Commands, varNum, dict);
            if (sf.AllFunctions != null)
                foreach (ScriptCommandContainer f in sf.AllFunctions) count += CountVariableInCommands(f.Commands, varNum, dict);
            return count;
        }

        private static int CountVariableInCommands(List<ScriptCommand> commands, int varNum, Dictionary<ushort, ScriptCommandInfo> dict)
        {
            int count = 0;
            if (commands == null) return 0;
            foreach (ScriptCommand cmd in commands)
            {
                if (cmd.Id == null || cmd.CmdParams == null) continue;
                ScriptCommandInfo info = null;
                dict?.TryGetValue(cmd.Id.Value, out info);
                List<ScriptParameter.ParameterType> paramTypes = info?.ParameterTypes;
                for (int i = 0; i < cmd.CmdParams.Count; i++)
                {
                    byte[] p = cmd.CmdParams[i];
                    ScriptParameter.ParameterType pt = (paramTypes != null && i < paramTypes.Count)
                        ? paramTypes[i]
                        : ScriptParameter.ParameterType.Integer;
                    int val = ScriptCommand.ParamValue(p);
                    if (pt == ScriptParameter.ParameterType.Variable || pt == ScriptParameter.ParameterType.Flex)
                    { if (val == varNum) count++; }
                    else if (p.Length >= 2 && val >= 0x4000 && val == varNum)
                        count++;
                }
            }
            return count;
        }

        private static int CountVariableInLevelScript(LevelScriptFile ls, int varNum)
        {
            int count = 0;
            if (ls.BufferSet == null) return 0;
            foreach (LevelScriptTrigger t in ls.BufferSet)
                if (t is VariableValueTrigger vt && vt.VariableToWatch == varNum) count++;
            return count;
        }

        private static int CountVariableInEventFile(EventFile ev, int varNum)
        {
            int count = 0;
            if (ev.Triggers != null)
                foreach (Trigger tr in ev.Triggers)
                    if (tr.VariableWatched == varNum) count++;
            return count;
        }

        private static int CountFlagInScriptFile(ScriptFile sf, int flagNum, Dictionary<ushort, ScriptCommandInfo> dict)
        {
            int count = 0;
            if (sf.AllScripts != null)
                foreach (ScriptCommandContainer s in sf.AllScripts) count += CountFlagInCommands(s.Commands, flagNum, dict);
            if (sf.AllFunctions != null)
                foreach (ScriptCommandContainer f in sf.AllFunctions) count += CountFlagInCommands(f.Commands, flagNum, dict);
            return count;
        }

        // The only commands with a literal flag, always first. The FromVar ones take a variable.
        private static readonly HashSet<string> FlagCommands = new HashSet<string> { "SetFlag", "ClearFlag", "CheckFlag" };

        private static bool TakesFlag(ScriptCommand cmd, Dictionary<ushort, ScriptCommandInfo> dict)
        {
            ScriptCommandInfo info = null;
            if (cmd.Id is ushort id) dict?.TryGetValue(id, out info);
            string name = info?.LegacyName;
            return FlagCommands.Contains(string.IsNullOrEmpty(name) ? ScriptWalker.BareName(cmd) : name);
        }

        private static int CountFlagInCommands(List<ScriptCommand> commands, int flagNum, Dictionary<ushort, ScriptCommandInfo> dict)
        {
            int count = 0;
            if (commands == null) return 0;
            foreach (ScriptCommand cmd in commands)
            {
                if (cmd.CmdParams == null || cmd.CmdParams.Count == 0 || !TakesFlag(cmd, dict)) continue;
                byte[] p = cmd.CmdParams[0];
                if (p.Length >= 2 && ScriptCommand.ParamValue(p) == flagNum && flagNum < 0x4000) count++;
            }
            return count;
        }
    }
}
