using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

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

        private static readonly Regex Head = new(@"\bsTutorMoves\s*\[\s*\]\s*=\s*\{");
        private static readonly Regex Row = new(@"\{\s*(\w+)\s*,\s*(\w+)\s*,\s*(\w+)\s*\}");

        public static bool TryRead(out List<Tutor> tutors, out string error)
        {
            tutors = null;
            if (!TryRows(out string text, out var rows, out var moves, out var npcs, out error)) return false;
            tutors = new List<Tutor>();
            foreach (Match m in rows)
            {
                int move = Value(m.Groups[1].Value, moves), cost = Value(m.Groups[2].Value, null), npc = Value(m.Groups[3].Value, npcs);
                if (move < 0 || cost < 0 || npc < 0) { error = $"{SourceRelPath}: {m.Value} couldn't be read."; tutors = null; return false; }
                tutors.Add(new Tutor(move, cost, npc));
            }
            return true;
        }

        /// <summary>Rewrites the rows that changed; the list must have as many rows as the file.</summary>
        public static bool TryWrite(IReadOnlyList<Tutor> tutors, out string error)
        {
            if (!TryRows(out string text, out var rows, out var moves, out var npcs, out error)) return false;
            if (rows.Count != tutors.Count) { error = $"{SourceRelPath} has {rows.Count} tutor moves, not {tutors.Count}."; return false; }
            string updated = text;
            for (int i = rows.Count - 1; i >= 0; i--)
            {
                Match m = rows[i];
                var t = tutors[i];
                string move = Token(t.Move, m.Groups[1].Value, moves, "MOVE_"), cost = Token(t.Cost, m.Groups[2].Value, null, null),
                       npc = Token(t.Npc, m.Groups[3].Value, npcs, "MOVE_TUTOR_NPC_");
                if (move == m.Groups[1].Value && cost == m.Groups[2].Value && npc == m.Groups[3].Value) continue;
                updated = updated.Substring(0, m.Index) + $"{{ {move}, {cost}, {npc} }}" + updated.Substring(m.Index + m.Length);
            }
            if (updated == text) return true;
            try { HgEngineFileCache.WriteText(Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', Path.DirectorySeparatorChar)), updated); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { error = ex.Message; return false; }
            return true;
        }

        private static bool TryRows(out string text, out List<Match> rows, out HgEngineSymbolTable moves, out HgEngineSymbolTable npcs, out string error)
        {
            text = null; rows = null; npcs = null; error = null;
            moves = HgEngineSymbolTable.Load(MovesHeaderRelPath);
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }
            string path = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path) || moves == null) { error = $"{SourceRelPath} or {MovesHeaderRelPath} is missing from the checkout."; return false; }
            text = HgEngineFileCache.GetText(path);
            // The tutor NPC names are #defines in the same file.
            npcs = HgEngineSymbolTable.Parse(text);
            Match head = Head.Match(text);
            if (!head.Success) { error = $"{SourceRelPath} has no sTutorMoves."; return false; }
            int open = head.Index + head.Length - 1;
            if (!BraceScanner.TryFindMatchingBrace(text, open, out int close)) { error = "sTutorMoves has no end."; return false; }
            string masked = HgEngineMusicTables.MaskComments(text);
            string source = text;
            rows = Row.Matches(masked.Substring(0, close), open + 1).Cast<Match>()
                .Select(m => Row.Match(source, m.Index)).ToList();
            return true;
        }

        private static int Value(string token, HgEngineSymbolTable symbols)
        {
            if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && int.TryParse(token.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out int hex)) return hex;
            if (int.TryParse(token, out int dec)) return dec;
            return symbols != null && symbols.TryGetValue(token, out int v) ? v : -1;
        }

        // Keeps the token as written when its value is unchanged.
        private static string Token(int value, string current, HgEngineSymbolTable symbols, string prefix)
        {
            if (Value(current, symbols) == value) return current;
            return prefix != null && symbols != null && symbols.TryGetNameWithPrefix(value, prefix, out string name) ? name : value.ToString();
        }
    }
}
