using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// The move each TM, HM and TR teaches, from src/item.c's <c>sMachineMoves[]</c>. hg-engine hooks the game's
    /// machine lookup to read this array, so the ARM9 table DSPRE edits on other ROMs is never used. The array is
    /// positional, one <c>MOVE_X, // LABEL</c> line per machine, and ItemToMachineMoveIndex maps item ids onto it.
    /// </summary>
    public static class HgEngineMachineMoves
    {
        private const string SourceRelPath = "src/item.c";
        private const string MovesHeaderRelPath = "include/constants/moves.h";

        private static readonly Regex Table = new(@"sMachineMoves\s*\[\s*\]\s*=\s*\{(?<body>.*?)^\s*\};",
            RegexOptions.Singleline | RegexOptions.Multiline);
        private static readonly Regex Entry = new(@"^[ \t]*(?<move>[A-Za-z_][A-Za-z0-9_]*|\d+)[ \t]*,[ \t]*(?://[ \t]*(?<label>\S+))?[ \t]*\r?$");

        /// <summary>One machine: its label from the source comment, its move, and where the move is written.</summary>
        public sealed record Machine(string Label, int Move, int MoveStart, int MoveLength);

        // The editor asks for labels row by row, so the last parse is kept while the file text is unchanged.
        private static string _parsedText;
        private static List<Machine> _parsed;

        public static bool TryRead(out List<Machine> machines, out string error)
        {
            machines = new List<Machine>();
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine folder is open."; return false; }

            HgEngineSymbolTable moves = HgEngineSymbolTable.Load(MovesHeaderRelPath);
            if (moves == null) { error = "Could not read include/constants/moves.h from the checkout."; return false; }
            string text = ReadSource(out string path);
            if (text == null) { error = $"Could not read {SourceRelPath} from the checkout."; return false; }

            if (_parsed != null && string.Equals(text, _parsedText, System.StringComparison.Ordinal))
            {
                machines = new List<Machine>(_parsed);
                return true;
            }

            Match table = Table.Match(text);
            if (!table.Success) { error = $"{SourceRelPath} has no sMachineMoves array."; return false; }

            int at = table.Groups["body"].Index;
            foreach (string line in table.Groups["body"].Value.Split('\n'))
            {
                int lineStart = at;
                at += line.Length + 1;
                string trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("//")) continue;

                // Anything else (a #if, two entries on a line) would make the positions this editor writes by unreliable.
                Match m = Entry.Match(line);
                if (!m.Success) { error = $"{SourceRelPath} has a line in sMachineMoves DSPRE can't read: {trimmed}"; return false; }

                string token = m.Groups["move"].Value;
                if (!moves.TryGetValue(token, out int move) && !int.TryParse(token, out move))
                { error = $"{token} in {SourceRelPath} is not a move moves.h names."; return false; }

                string label = m.Groups["label"].Success ? m.Groups["label"].Value : $"Machine {machines.Count}";
                machines.Add(new Machine(label, move, lineStart + m.Groups["move"].Index, token.Length));
            }
            _parsedText = text;
            _parsed = new List<Machine>(machines);
            return true;
        }

        /// <summary>Writes every machine's move back by name, leaving the comments and layout as they were.</summary>
        public static bool TryWrite(IReadOnlyList<int> moves, out string error)
        {
            if (!TryRead(out List<Machine> machines, out error)) return false;
            if (moves.Count != machines.Count)
            { error = "The number of machines in src/item.c changed since the editor opened. Reopen it to edit them."; return false; }

            HgEngineSymbolTable table = HgEngineSymbolTable.Load(MovesHeaderRelPath);
            string text = ReadSource(out string path);
            StringBuilder sb = new System.Text.StringBuilder(text);
            // From the end, so earlier positions stay valid as names change length.
            for (int i = machines.Count - 1; i >= 0; i--)
            {
                if (machines[i].Move == moves[i]) continue;
                if (!table.TryGetNameWithPrefix(moves[i], "MOVE_", out string name))
                { error = $"moves.h has no MOVE_ name for move {moves[i]}."; return false; }
                sb.Remove(machines[i].MoveStart, machines[i].MoveLength).Insert(machines[i].MoveStart, name);
            }
            HgEngineFileCache.WriteText(path, sb.ToString());
            return true;
        }

        private static string ReadSource(out string path)
        {
            path = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(path) ? HgEngineFileCache.GetText(path) : null;
        }
    }
}
