using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// The move each TM, HM and TR teaches, from <c>sMachineMoves[]</c>. hg-engine hooks the game's machine lookup
    /// to read this array, so the ARM9 table DSPRE edits on other ROMs is never used. The array is positional, one
    /// <c>MOVE_X, // LABEL</c> line per machine, and ItemToMachineMoveIndex maps item ids onto it.
    /// </summary>
    public static class HgEngineMachineMoves
    {
        private const string CurrentRelPath = "data/MachineMoves.c";
        private const string OlderRelPath = "src/item.c";
        private const string MovesHeaderRelPath = "include/constants/moves.h";

        // Older checkouts keep the array inside src/item.c.
        internal static string SourceRelPath =>
            File.Exists(Path.Combine(HgEngineProject.RepoPathUnc, CurrentRelPath.Replace('/', Path.DirectorySeparatorChar)))
                ? CurrentRelPath : OlderRelPath;

        private const string TableName = "sMachineMoves";

        /// <summary>One machine: its label from the source comment, its move, and where the move is written.</summary>
        public sealed record Machine(string Label, int Move, int MoveStart, int MoveLength);

        // The editor asks for labels row by row, so the last parse is kept while the file text is unchanged.
        private static string _parsedText;
        private static List<Machine> _parsed;

        public static bool TryRead(out List<Machine> machines, out string error)
        {
            machines = new List<Machine>();
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout is linked."; return false; }

            HgEngineSymbolTable moves = HgEngineSymbolTable.Load(MovesHeaderRelPath);
            if (moves == null) { error = "Couldn't read include/constants/moves.h from the checkout."; return false; }
            string text = ReadSource(out string path);
            if (text == null) { error = $"Couldn't read {SourceRelPath} from the checkout."; return false; }

            if (_parsed != null && string.Equals(text, _parsedText, System.StringComparison.Ordinal))
            {
                machines = new List<Machine>(_parsed);
                return true;
            }

            CDeclaration table = CSourceFile.For(text).Find(TableName);
            if (table == null) { error = $"{SourceRelPath} has no {TableName} array."; return false; }

            foreach (CInitItem item in table.Init.Items)
            {
                // A row under #if would shift every machine after it depending on the build.
                string token = item.ValueText(text).Trim();
                if (item.IsConditional || item.List != null || item.Designators.Count > 0)
                { error = $"{SourceRelPath} has an entry in {TableName} DSPRE can't read: {token}"; return false; }

                if (!moves.TryGetValue(token, out int move) && !CLexer.TryParseInt(token, out move))
                { error = $"{token} in {SourceRelPath} isn't a move that moves.h names."; return false; }

                machines.Add(new Machine(LabelAfter(text, item.ValueEnd) ?? $"Machine {machines.Count}", move, item.ValueStart, item.ValueEnd - item.ValueStart));
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
            { error = $"The number of machines in {SourceRelPath} changed since the editor opened. Reopen it to edit them."; return false; }

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

        // The first word of the comment closing the entry's line: TM001 in "MOVE_X, // TM001".
        private static string LabelAfter(string text, int end)
        {
            int lineEnd = text.IndexOf('\n', end);
            foreach (CToken token in CLexer.Tokenize(text, end, lineEnd < 0 ? text.Length : lineEnd, keepComments: true))
            {
                if (token.Kind != CTokenKind.Comment) continue;
                string[] words = token.Text(text).TrimStart('/', '*').Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
                return words.Length > 0 ? words[0] : null;
            }
            return null;
        }

        private static string ReadSource(out string path)
        {
            path = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(path) ? HgEngineFileCache.GetText(path) : null;
        }
    }
}
