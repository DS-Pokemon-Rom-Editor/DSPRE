using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// hg-engine's wild slot rolls in src/field/encounter_check.c: each EncounterSlot_WildMonSlotRoll_* function takes
    /// a roll of 0 to 99 and returns a slot by comparing it against boundaries. Slot hooks in overlay 2 send the game's
    /// own rolls to them, so the overlay's compares never run. Hooks put in arm9 never apply.
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

        /// <summary>The hg-engine pull request that gives the slot roll hooks overlay 2's number.</summary>
        public const string HookFixUrl = "https://github.com/BluRosie/hg-engine/pull/1039";

        /// <summary>
        /// Slot roll hooks the checkout's hooks file labels arm9. The rolls are in overlay 2, so make.py writes those hooks
        /// past the ARM9's end and the game keeps rolling with overlay 2's own odds.
        /// </summary>
        public static List<(string Symbol, int Line)> MislabelledHooks()
        {
            HgEnginePatchList hooks = HgEnginePatchList.ReadAll().FirstOrDefault(l => l.Kind == HgEnginePatchKind.Hook);
            if (hooks == null) return new List<(string, int)>();
            // The list keeps one entry per line of the file, so an entry's place is its line.
            return hooks.Entries.Select((e, i) => (Entry: e, Line: i + 1))
                .Where(x => x.Entry.Parsed && x.Entry.OverlayNumber < 0 && x.Entry.Built != false && Rolls.Any(r => r.Function == x.Entry.Symbol))
                .Select(x => (x.Entry.Symbol, x.Line)).ToList();
        }

        private static string FullPath => Path.Combine(HgEngineProject.RepoRootWindows, RelPath.Replace('/', Path.DirectorySeparatorChar));

        /// <summary>Why the rolls can't be edited in this checkout, or null.</summary>
        public static string WhyNot()
        {
            if (!HgEngineProject.IsActive) return "No hg-engine checkout is linked.";
            if (!File.Exists(FullPath)) return $"{RelPath} isn't in the checkout.";
            return TryLoad(out _, out string error) ? null : error;
        }

        /// <summary>Each roll's slot percentages, in <see cref="Rolls"/> order.</summary>
        public static bool TryLoad(out List<int[]> percents, out string error)
        {
            percents = new List<int[]>();
            error = null;
            string text;
            try { text = HgEngineFileCache.GetText(FullPath); }
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
            List<string> tokens = CLexer.Tokenize(body).Select(t => t.Text(body)).ToList();
            int? Number(int t) => t < tokens.Count && int.TryParse(tokens[t], out int n) ? n : null;

            List<int> bounds = new List<int>();
            for (int t = 0; t < tokens.Count; t++)
            {
                if (tokens[t] == "return" && t + 7 < tokens.Count && tokens[t + 1] == "rnd" && tokens[t + 2] == ">=" && Number(t + 3) is int rare
                    && tokens[t + 4] == "?" && tokens[t + 5] == "1" && tokens[t + 6] == ":" && tokens[t + 7] == "0")
                    return new List<int> { rare };

                if (tokens[t] != "if") continue;
                int? bound = null;
                int depth = 0;
                for (int k = t + 1; k < tokens.Count; k++)
                {
                    if (tokens[k] == "(") depth++;
                    else if (tokens[k] == ")" && --depth == 0) break;
                    else if (tokens[k] == "rnd" && bound == null)
                    {
                        if (k + 2 >= tokens.Count) break;
                        if (tokens[k + 1] == "<") bound = Number(k + 2);
                        else if (tokens[k + 1] == "==") bound = Number(k + 2) + 1;
                    }
                }
                if (bound == null) return null;
                bounds.Add(bound.Value);
            }
            return bounds.Count > 0 ? bounds : null;
        }

        private static bool TryBody(string text, string function, out int open, out int close)
        {
            open = close = -1;
            if (CSourceFile.For(text).FunctionBody(function) is not (int, int) body) return false;
            (open, close) = body;
            return true;
        }

        /// <summary>Rewrites the functions whose odds changed as a plain chain of comparisons. Null on success.</summary>
        public static string Write(IReadOnlyList<int[]> percents)
        {
            if (!TryLoad(out List<int[]> current, out string error)) return error;
            string text = HgEngineFileCache.GetText(FullPath);
            string nl = text.Contains("\r\n") ? "\r\n" : "\n";
            for (int r = Rolls.Length - 1; r >= 0; r--)
            {
                if (current[r].SequenceEqual(percents[r])) continue;
                if (percents[r].Length != Rolls[r].Slots || percents[r].Sum() != 100 || percents[r].Any(p => p < 0))
                    return $"{Rolls[r].Name}: the slots have to add up to 100%.";
                TryBody(text, Rolls[r].Function, out int open, out int close);
                text = text.Substring(0, open + 1) + Body(percents[r], nl) + text.Substring(close);
            }
            try { HgEngineFileCache.WriteText(FullPath, text); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { return ex.Message; }
            return null;
        }

        private static string Body(int[] percents, string nl)
        {
            StringBuilder b = new StringBuilder();
            // A leading 0% slot compares rnd < 0, which -Wextra flags on an unsigned type and debug builds make an error.
            string type = percents[0] == 0 ? "int" : "u8";
            b.Append(nl).Append($"    {type} rnd = LCRandRange(100);").Append(nl).Append(nl);
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
