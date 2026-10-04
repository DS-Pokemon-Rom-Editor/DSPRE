using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

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

        private static readonly Dictionary<string, (DateTime Stamp, Dictionary<int, Command> Commands)> _cache = new();

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
            lock (_cache)
            {
                if (_cache.TryGetValue(key, out var hit) && hit.Stamp == stamp) return hit.Commands;
                var commands = read(HgEngineFileCache.GetText(path));
                _cache[key] = (stamp, commands);
                return commands;
            }
        }

        // ".macro name,a,b" then ".word 0x58, a, b" then ".endmacro"; only single-line commands with a literal opcode.
        private static readonly Regex AnimMacro = new(@"^\s*\.macro\s+(\w+)[^\n]*\n\s*\.word\s+(0x[0-9A-Fa-f]+|\d+)\s*((?:,[^\n]*)?)\n\s*\.endmacro", RegexOptions.Multiline);

        internal static Dictionary<int, Command> ReadAnimation(string text)
        {
            var map = new Dictionary<int, Command>();
            foreach (Match m in AnimMacro.Matches(text.Replace("\r\n", "\n")))
            {
                int op = Number(m.Groups[2].Value);
                string rest = m.Groups[3].Value.Trim();
                int args = rest.Length == 0 ? 0 : rest.Split(',').Length - 1;
                if (op >= 0) map.TryAdd(op, new Command(m.Groups[1].Value, args));
            }
            return map;
        }

        // ".macro Name a, b" then ".long NN" and one ".long" per argument, then ".endm".
        private static readonly Regex BattleMacro = new(@"^\s*\.macro\s+(\w+)[^\n]*\n((?:\s*\.long[^\n]*\n)+)\s*\.endm\b", RegexOptions.Multiline);

        internal static Dictionary<int, Command> ReadBattle(string text)
        {
            var map = new Dictionary<int, Command>();
            foreach (Match m in BattleMacro.Matches(text.Replace("\r\n", "\n")))
            {
                var longs = m.Groups[2].Value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var first = Regex.Match(longs[0], @"\.long\s+(0x[0-9A-Fa-f]+|\d+)\s*$");
                if (!first.Success) continue;
                int op = Number(first.Groups[1].Value);
                if (op >= 0) map.TryAdd(op, new Command(m.Groups[1].Value, longs.Length - 1));
            }
            return map;
        }

        private static int Number(string token) =>
            token.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? (int.TryParse(token.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out int h) ? h : -1)
                : (int.TryParse(token, out int d) ? d : -1);
    }
}
