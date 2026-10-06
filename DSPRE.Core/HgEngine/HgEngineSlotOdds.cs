using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// hg-engine's wild slot rolls in src/field/encounter_check.c: each EncounterSlot_WildMonSlotRoll_* function takes
    /// a roll of 0 to 99 and returns a slot by comparing it against boundaries. They only run when the checkout's
    /// hooks list sends the game's own rolls in overlay 2 to them.
    /// </summary>
    public static class HgEngineSlotOdds
    {
        public const string RelPath = "src/field/encounter_check.c";

        /// <summary>Each roll, by its function, with the method name and slot count the vanilla editor uses.</summary>
        public static readonly (string Function, string Name, int Slots)[] Rolls =
        {
            ("EncounterSlot_WildMonSlotRoll_Land", "Walking", 12),
            ("EncounterSlot_WildMonSlotRoll_Surfing", "Surfing", 5),
            ("EncounterSlot_WildMonSlotRoll_Fishing", "Fishing (all rods)", 5),
            ("EncounterSlot_WildMonSlotRoll_RockSmash", "Rock Smash", 2),
            ("EncounterSlot_WildMonSlotRoll_Headbutt", "Headbutt", 6),
        };

        private static string FullPath => Path.Combine(HgEngineProject.RepoRootWindows, RelPath.Replace('/', Path.DirectorySeparatorChar));

        /// <summary>Why the rolls can't be edited in this checkout, or null.</summary>
        public static string WhyNot()
        {
            if (!HgEngineProject.IsActive) return "No hg-engine checkout is open.";
            if (!File.Exists(FullPath)) return $"{RelPath} isn't in the checkout.";
            HgEnginePatchList hooks = HgEnginePatchList.ReadAll().FirstOrDefault(l => l.Kind == HgEnginePatchKind.Hook);
            List<HgEnginePatchEntry> wrong = hooks?.Entries.Where(e => e.Parsed && e.Symbol.StartsWith("EncounterSlot_WildMonSlotRoll_") && e.OverlayNumber != 2).ToList();
            if (wrong != null && wrong.Count > 0)
                return $"The checkout's hooks list sends {wrong.Count} slot rolls to arm9 rather than overlay 2, where the game's rolls are, "
                     + "so the odds in encounter_check.c are never used. Change those hooks to 0002 in hg-engine Patches first.";
            return TryLoad(out _, out string error) ? null : error;
        }

        /// <summary>Each roll's slot percentages, in <see cref="Rolls"/> order.</summary>
        public static bool TryLoad(out List<int[]> percents, out string error)
        {
            percents = new List<int[]>();
            error = null;
            string text;
            try { text = File.ReadAllText(FullPath); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { error = ex.Message; return false; }

            foreach ((string function, string name, int slots) in Rolls)
            {
                if (!TryBody(text, function, out int open, out int close)) { error = $"{function} isn't in {RelPath}."; return false; }
                List<int> bounds = Bounds(text.Substring(open + 1, close - open - 1));
                if (bounds == null || bounds.Count != slots - 1 || bounds.Zip(bounds.Skip(1)).Any(p => p.Second < p.First) || bounds[^1] > 100)
                {
                    error = $"{function} isn't a chain of roll comparisons DSPRE can read; edit it in {RelPath}.";
                    return false;
                }
                int[] p = new int[slots];
                int previous = 0;
                for (int i = 0; i < bounds.Count; i++) { p[i] = bounds[i] - previous; previous = bounds[i]; }
                p[^1] = 100 - previous;
                percents.Add(p);
            }
            return true;
        }

        // The upper bound of each slot but the last: "rnd < N" gives N, "rnd == N" gives N + 1, and Rock Smash's
        // "rnd >= N ? 1 : 0" gives N. Lower bounds ("rnd >= a &&") repeat the previous slot's and are skipped.
        private static List<int> Bounds(string body)
        {
            Match ternary = Regex.Match(body, @"return\s+rnd\s*>=\s*(\d+)\s*\?\s*1\s*:\s*0\s*;");
            if (ternary.Success) return new List<int> { int.Parse(ternary.Groups[1].Value) };
            List<int> bounds = new List<int>();
            foreach (Match m in Regex.Matches(body, @"\bif\s*\(([^)]*)\)"))
            {
                string cond = m.Groups[1].Value;
                Match lt = Regex.Match(cond, @"rnd\s*<\s*(\d+)");
                Match eq = Regex.Match(cond, @"rnd\s*==\s*(\d+)");
                if (lt.Success) bounds.Add(int.Parse(lt.Groups[1].Value));
                else if (eq.Success) bounds.Add(int.Parse(eq.Groups[1].Value) + 1);
                else return null;
            }
            return bounds.Count > 0 ? bounds : null;
        }

        private static bool TryBody(string text, string function, out int open, out int close)
        {
            open = close = -1;
            Match m = Regex.Match(text, @"\b" + Regex.Escape(function) + @"\s*\([^)]*\)\s*\{");
            if (!m.Success) return false;
            open = m.Index + m.Length - 1;
            return BraceScanner.TryFindMatchingBrace(text, open, out close);
        }

        /// <summary>Rewrites the functions whose odds changed as a plain chain of comparisons. Null on success.</summary>
        public static string Write(IReadOnlyList<int[]> percents)
        {
            if (!TryLoad(out List<int[]> current, out string error)) return error;
            string text = File.ReadAllText(FullPath);
            string nl = text.Contains("\r\n") ? "\r\n" : "\n";
            for (int r = Rolls.Length - 1; r >= 0; r--)
            {
                if (current[r].SequenceEqual(percents[r])) continue;
                if (percents[r].Length != Rolls[r].Slots || percents[r].Sum() != 100 || percents[r].Any(p => p < 0))
                    return $"{Rolls[r].Name}: the slots have to add up to 100%.";
                TryBody(text, Rolls[r].Function, out int open, out int close);
                text = text.Substring(0, open + 1) + Body(percents[r], nl) + text.Substring(close);
            }
            try { HgEngineFileCache.WriteText(FullPath, text); return null; }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { return ex.Message; }
        }

        private static string Body(int[] percents, string nl)
        {
            StringBuilder b = new StringBuilder();
            b.Append(nl).Append("    u8 rnd = LCRandRange(100);").Append(nl).Append(nl);
            int bound = 0;
            for (int i = 0; i < percents.Length - 1; i++)
            {
                bound += percents[i];
                b.Append(i == 0 ? "    if" : " else if").Append($" (rnd < {bound}) {{").Append(nl)
                 .Append($"        return {i};").Append(nl).Append("    }");
            }
            b.Append(" else {").Append(nl).Append($"        return {percents.Length - 1};").Append(nl).Append("    }").Append(nl);
            return b.ToString();
        }
    }
}
