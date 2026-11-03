using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// hg-engine's move tutors, src/field/move_tutor.c's sTutorMoves { move, cost, tutorNpc }, which the battle
    /// frontier tutors and the headbutt tutor read once GetLearnableTutorMoves is hooked. Which species learn each
    /// is learnsets.json's TutorMoves, turned into bits over this list by build_learnsets.py.
    /// </summary>
    public static class HgEngineMoveTutors
    {
        public const string SourceRelPath = "src/field/move_tutor.c";
        private const string MovesHeaderRelPath = "include/constants/moves.h";

        public sealed record Tutor(int Move, int Cost, int Npc);

        private const string TableName = "sTutorMoves";

        public static bool TryRead(out List<Tutor> tutors, out string error)
        {
            tutors = null;
            if (!TryRows(out string text, out List<CInitList> rows, out HgEngineSymbolTable moves, out HgEngineSymbolTable npcs, out error)) return false;
            tutors = new List<Tutor>();
            foreach (CInitList row in rows)
            {
                int move = Value(Cell(text, row, 0), moves), cost = Value(Cell(text, row, 1), null), npc = Value(Cell(text, row, 2), npcs);
                if (move < 0 || cost < 0 || npc < 0)
                { error = $"{SourceRelPath}: {text.Substring(row.Open, row.Close + 1 - row.Open)} couldn't be read."; tutors = null; return false; }
                tutors.Add(new Tutor(move, cost, npc));
            }
            return true;
        }

        private static string Cell(string text, CInitList row, int column) => row.Items[column].ValueText(text).Trim();

        /// <summary>Rewrites the rows that changed; the list must have as many rows as the file.</summary>
        public static bool TryWrite(IReadOnlyList<Tutor> tutors, out string error)
        {
            if (!TryRows(out string text, out List<CInitList> rows, out HgEngineSymbolTable moves, out HgEngineSymbolTable npcs, out error)) return false;
            if (rows.Count != tutors.Count) { error = $"{SourceRelPath} has {rows.Count} tutor moves, not {tutors.Count}."; return false; }
            string updated = text;
            for (int i = rows.Count - 1; i >= 0; i--)
            {
                CInitList row = rows[i];
                Tutor t = tutors[i];
                string move = Token(t.Move, Cell(text, row, 0), moves, "MOVE_"), cost = Token(t.Cost, Cell(text, row, 1), null, null),
                       npc = Token(t.Npc, Cell(text, row, 2), npcs, "MOVE_TUTOR_NPC_");
                if (move == Cell(text, row, 0) && cost == Cell(text, row, 1) && npc == Cell(text, row, 2)) continue;
                updated = updated.Substring(0, row.Open) + $"{{ {move}, {cost}, {npc} }}" + updated.Substring(row.Close + 1);
            }
            if (updated == text) return true;
            try { HgEngineFileCache.WriteText(Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', Path.DirectorySeparatorChar)), updated); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { error = ex.Message; return false; }
            return true;
        }

        private static bool TryRows(out string text, out List<CInitList> rows, out HgEngineSymbolTable moves, out HgEngineSymbolTable npcs, out string error)
        {
            text = null; rows = null; npcs = null; error = null;
            moves = HgEngineSymbolTable.Load(MovesHeaderRelPath);
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout is linked."; return false; }
            string path = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path) || moves == null) { error = $"{SourceRelPath} or {MovesHeaderRelPath} is missing from the checkout."; return false; }
            text = HgEngineFileCache.GetText(path);
            // The tutor NPC names are #defines in the same file.
            npcs = HgEngineSymbolTable.Parse(text);
            CDeclaration table = CSourceFile.For(text).Find(TableName);
            if (table == null) { error = $"{SourceRelPath} has no {TableName}."; return false; }
            CInitItem bad = table.Init.Items.FirstOrDefault(i => i.IsConditional || i.List == null || i.List.Items.Count != 3);
            if (bad != null) { error = $"{SourceRelPath}: {bad.ValueText(text).Trim()} couldn't be read."; return false; }
            rows = table.Init.Items.Select(i => i.List).ToList();
            return true;
        }

        private static int Value(string token, HgEngineSymbolTable symbols) => HgEngineSymbolTable.TryValue(token, out int v, symbols) ? v : -1;

        // Keeps the token as written when its value is unchanged.
        private static string Token(int value, string current, HgEngineSymbolTable symbols, string prefix)
        {
            if (Value(current, symbols) == value) return current;
            return prefix != null && symbols != null && symbols.TryGetNameWithPrefix(value, prefix, out string name) ? name : value.ToString();
        }
    }
}
