using System.Linq;
using System;
using System.Collections.Generic;
using System.IO;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// The move-animation and battle-script commands a checkout defines, read from its own macros: armips/include/
    /// animscriptcmd.s (".word 0xNN, args") and asm/include/battle_commands.inc (".long NN" then one ".long" per
    /// argument). hg-engine adds commands past the game's own, which readers that only know the retail tables stop at.
    /// </summary>
    public static class HgEngineScriptCommands
    {
        public const string AnimMacrosRelPath = "armips/include/animscriptcmd.s";
        public const string BattleMacrosRelPath = "asm/include/battle_commands.inc";

        public readonly record struct Command(string Name, int ArgCount);

        private static readonly Dictionary<string, (DateTime Stamp, Dictionary<int, Command> Commands)> Cache = new();

        /// <summary>Animation commands by opcode, or an empty map when no checkout is open or the file is missing.</summary>
        public static IReadOnlyDictionary<int, Command> Animation() => Load(AnimMacrosRelPath, ReadAnimation);

        /// <summary>Battle-script commands by opcode, or an empty map when no checkout is open or the file is missing.</summary>
        public static IReadOnlyDictionary<int, Command> Battle() => Load(BattleMacrosRelPath, ReadBattle);

        private static Dictionary<int, Command> Load(string rel, Func<string, Dictionary<int, Command>> read)
        {
            if (!HgEngineProject.IsActive) return new Dictionary<int, Command>();
            string path = Path.Combine(HgEngineProject.RepoPathUnc, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) return new Dictionary<int, Command>();
            DateTime stamp = File.GetLastWriteTimeUtc(path);
            string key = path;
            lock (Cache)
            {
                if (Cache.TryGetValue(key, out (DateTime Stamp, Dictionary<int, Command> Commands) hit) && hit.Stamp == stamp) return hit.Commands;
                Dictionary<int, Command> commands = read(HgEngineFileCache.GetText(path));
                Cache[key] = (stamp, commands);
                return commands;
            }
        }

        // The name of a ".macro name ..." line, or null.
        private static string MacroName(string line)
        {
            if (!line.StartsWith(".macro", StringComparison.Ordinal) || line.Length < 7 || !char.IsWhiteSpace(line[6])) return null;
            string rest = line.Substring(6).TrimStart();
            int end = 0;
            while (end < rest.Length && HgEngineMakeFile.IsNameChar(rest[end])) end++;
            return end > 0 ? rest.Substring(0, end) : null;
        }

        private static string[] MacroLines(string text) =>
            text.Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();

        private static bool IsDirective(string line, string directive) =>
            line.StartsWith(directive, StringComparison.Ordinal) && (line.Length == directive.Length || char.IsWhiteSpace(line[directive.Length]));

        // ".macro name,a,b" then ".word 0x58, a, b" then ".endmacro". Only single-line commands with a literal opcode.
        internal static Dictionary<int, Command> ReadAnimation(string text)
        {
            Dictionary<int, Command> map = new Dictionary<int, Command>();
            string[] lines = MacroLines(text);
            for (int i = 0; i + 2 < lines.Length; i++)
            {
                string name = MacroName(lines[i]);
                if (name == null || !IsDirective(lines[i + 1], ".word") || !lines[i + 2].StartsWith(".endmacro", StringComparison.Ordinal)) continue;
                string[] parts = lines[i + 1].Substring(5).Split(',');
                int op = Number(parts[0].Trim());
                if (op >= 0) map.TryAdd(op, new Command(name, parts.Length - 1));
            }
            return map;
        }

        // ".macro Name a, b" then ".long NN" and one ".long" per argument, then ".endm".
        internal static Dictionary<int, Command> ReadBattle(string text)
        {
            Dictionary<int, Command> map = new Dictionary<int, Command>();
            string[] lines = MacroLines(text);
            for (int i = 0; i < lines.Length; i++)
            {
                string name = MacroName(lines[i]);
                if (name == null) continue;
                int end = i + 1;
                while (end < lines.Length && lines[end].StartsWith(".long", StringComparison.Ordinal)) end++;
                if (end == i + 1 || end >= lines.Length || !IsDirective(lines[end], ".endm")) continue;
                string[] first = HgEngineMakeFile.Words(lines[i + 1]);
                int op = first.Length == 2 && first[0] == ".long" ? Number(first[1]) : -1;
                if (op >= 0) map.TryAdd(op, new Command(name, end - i - 2));
            }
            return map;
        }

        private static int Number(string token) =>
            token.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? (int.TryParse(token.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out int h) ? h : -1)
                : (int.TryParse(token, out int d) ? d : -1);
    }
}
