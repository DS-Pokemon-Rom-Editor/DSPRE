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

        // ".macro name,a,b" then ".word 0x58, a, b" then ".endmacro". Only single-line commands with a literal opcode.
        internal static Dictionary<int, Command> ReadAnimation(string text)
        {
            Dictionary<int, Command> map = new Dictionary<int, Command>();
            foreach (AsmMacro macro in AsmSourceFile.For(text).Macros)
            {
                if (macro.End != ".endmacro" || macro.Body.Count != 1 || !macro.Body[0].Is(text, 0, ".word")) continue;
                List<CToken> words = macro.Body[0].Words;
                List<(int First, int End)> parts = CLexer.SplitOutsideBrackets(text, words, 1, words.Count, ",");
                if (parts.Count > 0 && parts[0].End - parts[0].First == 1 && CLexer.TryParseInt(words[parts[0].First].Text(text), out int op) && op >= 0)
                    map.TryAdd(op, new Command(macro.Name, parts.Count - 1));
            }
            return map;
        }

        // ".macro Name a, b" then ".long NN" and one ".long" per argument, then ".endm".
        internal static Dictionary<int, Command> ReadBattle(string text)
        {
            Dictionary<int, Command> map = new Dictionary<int, Command>();
            foreach (AsmMacro macro in AsmSourceFile.For(text).Macros)
            {
                if (macro.End != ".endm" || macro.Body.Count == 0 || macro.Body.Any(l => !l.Is(text, 0, ".long"))) continue;
                List<CToken> first = macro.Body[0].Words;
                if (first.Count == 2 && CLexer.TryParseInt(first[1].Text(text), out int op) && op >= 0)
                    map.TryAdd(op, new Command(macro.Name, macro.Body.Count - 1));
            }
            return map;
        }
    }
}
