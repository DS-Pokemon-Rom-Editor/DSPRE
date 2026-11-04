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
                int[] p = Percents(Bounds(text.Substring(open + 1, close - open - 1)), slots);
                if (p == null)
                {
                    error = $"{function} isn't a chain of roll comparisons DSPRE can read; edit it in {RelPath}.";
                    return false;
                }
                percents.Add(p);
            }
            return true;
        }

        private static int[] Percents(List<SlotBound> slots, int count)
        {
            if (slots == null || slots.Count != count - 1) return null;
            List<int> bounds = slots.Select(s => s.Bound).ToList();
            if (bounds.Zip(bounds.Skip(1)).Any(p => p.Second < p.First) || bounds[^1] > 100) return null;
            int[] p = new int[count];
            int previous = 0;
            for (int i = 0; i < bounds.Count; i++) { p[i] = bounds[i] - previous; previous = bounds[i]; }
            p[^1] = 100 - previous;
            return p;
        }

        private enum Edge { Upper, Equal, Lower }

        // Editable is false when the condition has a comparison the in-place writer can't move.
        private sealed record SlotBound(int Bound, List<(CToken Op, CToken Number, Edge Edge)> Numbers, bool Editable);

        // The upper bound of each slot but the last: "rnd < N" gives N, "rnd == N" gives N + 1, and Rock Smash's
        // "rnd >= N ? 1 : 0" gives N. Lower bounds ("rnd >= a &&") repeat the previous slot's.
        private static List<SlotBound> Bounds(string body)
        {
            CSourceFile source = CSourceFile.For(body);
            List<CToken> code = source.Tokens;
            List<string> tokens = code.Select(t => t.Text(body)).ToList();
            int? Number(int t) => t < tokens.Count && CLexer.TryParseInt(tokens[t], out int n) ? n : null;
            string At(int t) => t >= 0 && t < tokens.Count ? tokens[t] : "";

            List<SlotBound> bounds = new List<SlotBound>();
            for (int t = 0; t < tokens.Count; t++)
            {
                if (tokens[t] == "return" && t + 7 < tokens.Count && tokens[t + 1] == "rnd" && tokens[t + 2] == ">=" && Number(t + 3) is int rare
                    && tokens[t + 4] == "?" && tokens[t + 5] == "1" && tokens[t + 6] == ":" && tokens[t + 7] == "0")
                    return new List<SlotBound> { new SlotBound(rare, new List<(CToken, CToken, Edge)> { (code[t + 2], code[t + 3], Edge.Upper) }, true) };

                if (tokens[t] != "if" || t + 1 >= tokens.Count || tokens[t + 1] != "(") continue;
                int? bound = null;
                int close = source.Match(t + 1);
                bool editable = close >= 0;
                List<(CToken, CToken, Edge)> numbers = new List<(CToken, CToken, Edge)>();
                for (int k = t + 2; k < (close < 0 ? tokens.Count : close); k++)
                {
                    if (tokens[k] != "rnd") continue;
                    string op = At(k + 1);
                    bool plain = At(k - 1) is "(" or "&&" && Number(k + 2) != null && At(k + 3) is ")" or "&&";
                    if (bound == null && op == "<") bound = Number(k + 2);
                    else if (bound == null && op == "==") bound = Number(k + 2) + 1;
                    else if (op != ">=") plain = false;
                    if (!plain) { editable = false; continue; }
                    numbers.Add((code[k + 1], code[k + 2], op == "<" ? Edge.Upper : op == "==" ? Edge.Equal : Edge.Lower));
                }
                if (bound == null) return null;
                bounds.Add(new SlotBound(bound.Value, numbers, editable));
            }
            return bounds.Count > 0 ? bounds : null;
        }

        // Changes only the numbers in the existing chain, so layout and comments stay. Null when the new odds don't fit it.
        private static string InPlace(string body, int[] percents)
        {
            // A 0% first slot needs the signed variable the full rewrite declares.
            if (percents[0] == 0) return null;
            List<SlotBound> slots = Bounds(body);
            if (slots == null || slots.Count != percents.Length - 1 || slots.Any(s => !s.Editable)) return null;
            List<(int Start, int End, string Text)> edits = new List<(int, int, string)>();
            int previous = 0, oldPrevious = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                int bound = previous + percents[i];
                foreach ((CToken op, CToken number, Edge edge) in slots[i].Numbers)
                {
                    if (edge == Edge.Equal)
                    {
                        // "rnd == N" only reads as one slot when the previous slot ends at N.
                        if (slots[i].Bound - oldPrevious != 1) return null;
                        edits.Add(bound - previous == 1 ? (number.Start, number.End, (bound - 1).ToString()) : (op.Start, number.End, $"< {bound}"));
                        continue;
                    }
                    if (edge == Edge.Lower && (!CLexer.TryParseInt(number.Text(body), out int lower) || lower != oldPrevious)) return null;
                    edits.Add((number.Start, number.End, (edge == Edge.Lower ? previous : bound).ToString()));
                }
                oldPrevious = slots[i].Bound;
                previous = bound;
            }
            StringBuilder b = new StringBuilder(body);
            foreach ((int start, int end, string text) in edits.OrderByDescending(e => e.Start))
                b.Remove(start, end - start).Insert(start, text);
            string edited = b.ToString();
            return Percents(Bounds(edited), percents.Length) is int[] check && check.SequenceEqual(percents) ? edited : null;
        }

        private static bool TryBody(string text, string function, out int open, out int close)
        {
            open = close = -1;
            if (CSourceFile.For(text).FunctionBody(function) is not (int, int) body) return false;
            (open, close) = body;
            return true;
        }

        /// <summary>
        /// Changes the numbers of each function whose odds changed, or rewrites it as a plain chain of comparisons when
        /// its chain can't hold the new odds. Null on success.
        /// </summary>
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
                string body = InPlace(text.Substring(open + 1, close - open - 1), percents[r]) ?? Body(percents[r], nl);
                text = text.Substring(0, open + 1) + body + text.Substring(close);
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
