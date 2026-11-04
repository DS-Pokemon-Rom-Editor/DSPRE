using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.HgEngine
{
    /// <summary>Small hg-engine source tables edited by name: a list of names, or rows of a key and a value.</summary>
    public static class HgEngineSourceTables
    {
        /// <param name="Header">Where the names with <paramref name="Prefix"/> are defined.</param>
        /// <param name="IsList">A list of names. Otherwise each row is [key] = value, or { key, value }.</param>
        /// <param name="Skip">Parts of names with the prefix that aren't values, such as TYPE_MUL_.</param>
        /// <param name="KeyPrefix">What every key starts with, left off when the key is shown.</param>
        public sealed record Info(string Label, string File, string Symbol, string Header, string Prefix, bool IsList = false, string[] Skip = null, string KeyPrefix = null);

        private static readonly string[] NotTypes = { "_INTERNAL", "TYPE_MUL_", "TYPE_RING_TARGET", "TYPE_FORESIGHT", "TYPE_ENDTABLE" };
        private static readonly string[] NotSpecies = { "_START", "MAX_", "_NUM", "_END" };

        public static readonly Info[] All =
        {
            new("Moon Ball species", "src/individual/CalculateBallShakes.c", "MoonBallSpecies", "include/constants/species.h", "SPECIES_", IsList: true, Skip: NotSpecies),
            new("Camouflage type", "src/battle/battle_input.c", "sCamouflageTypeTable", "include/constants/battle_constants.h", "TYPE_", Skip: NotTypes, KeyPrefix: "TERRAIN_"),
            new("Secret Power effect", "src/battle/battle_input.c", "sSecretPowerEffectTable", "include/constants/move_effects.h", "MOVE_SUBSCRIPT_PTR_", KeyPrefix: "TERRAIN_"),
            new("Type-boosting hold effects", "src/battle/other_battle_calculators.c", "HeldItemPowerUpTable", "include/constants/battle_constants.h", "TYPE_", Skip: NotTypes, KeyPrefix: "HOLD_EFFECT_STRENGTHEN_"),
            new("Nature mints", "src/individual/PartyMenu_HandleUseItemOnMon.c", "NatureToMintItem", "include/constants/item.h", "ITEM_", KeyPrefix: "NATURE_"),
        };

        /// <summary>One row: the key as written (empty in a list), the value's name, and whether an #if decides it.</summary>
        public sealed record Row(string Key, string Value, bool Conditional);

        private static string PathOf(Info info) => System.IO.Path.Combine(HgEngineProject.RepoPathUnc, info.File.Replace('/', System.IO.Path.DirectorySeparatorChar));

        public static string WhyNot(Info info)
        {
            if (!HgEngineProject.IsActive) return "No hg-engine checkout is linked.";
            string path = PathOf(info);
            if (!File.Exists(path) || CSourceFile.For(HgEngineFileCache.GetText(path)).Find(info.Symbol)?.Init == null) return $"{info.File} has no {info.Symbol}.";
            return null;
        }

        /// <summary>The names a value can take, in the header's order.</summary>
        public static List<string> Choices(Info info)
        {
            HgEngineSymbolTable table = HgEngineSymbolTable.Load(info.Header);
            return table == null ? new List<string>() : table.ByName
                .Where(p => p.Key.StartsWith(info.Prefix, StringComparison.Ordinal) && !(info.Skip ?? Array.Empty<string>()).Any(s => p.Key.Contains(s, StringComparison.Ordinal)))
                .OrderBy(p => p.Value).Select(p => p.Key).ToList();
        }

        // Only the token with the table's prefix is edited, so the rest of an expression stays as written.
        private static (int Start, int End)? TokenIn(Info info, string text, int from, int to)
        {
            foreach (CToken token in CLexer.Tokenize(text, from, to))
                if (token.Kind == CTokenKind.Identifier && string.CompareOrdinal(text, token.Start, info.Prefix, 0, info.Prefix.Length) == 0)
                    return (token.Start, token.End);
            return null;
        }

        private static (string Key, int Start, int End)? Parse(Info info, CInitItem item, string text)
        {
            if (info.IsList) return TokenIn(info, text, item.ValueStart, item.ValueEnd) is (int s, int e) ? ("", s, e) : null;
            if (item.List != null && item.List.Items.Count == 2)
            {
                CInitItem key = item.List.Items[0], value = item.List.Items[1];
                return TokenIn(info, text, value.ValueStart, value.ValueEnd) is (int s, int e) ? (key.ValueText(text).Trim(), s, e) : null;
            }
            return item.IndexText != null && TokenIn(info, text, item.ValueStart, item.ValueEnd) is (int vs, int ve) ? (item.IndexText.Trim(), vs, ve) : null;
        }

        private static List<Row> Rows(Info info, string text)
        {
            List<Row> rows = new List<Row>();
            foreach (CInitItem item in CSourceFile.For(text).Find(info.Symbol)?.Init?.Items ?? new List<CInitItem>())
                if (Parse(info, item, text) is (string key, int start, int end))
                    rows.Add(new Row(key, text.Substring(start, end - start), item.IsConditional));
            return rows;
        }

        // Saved whole, so the save checks the file still holds what the editor loaded.
        private static string LoadedKey(Info info) => "@" + info.Symbol;
        private static string RowsText(IEnumerable<Row> rows) => string.Join("\n", rows.Select(r => $"{r.Key}={r.Value}{(r.Conditional ? "?" : "")}"));

        /// <summary>Reads the rows for editing.</summary>
        public static bool TryRead(Info info, out List<Row> rows, out string error)
        {
            rows = new List<Row>();
            if ((error = WhyNot(info)) != null) return false;
            string path = PathOf(info);
            rows = Rows(info, HgEngineFileCache.GetText(path));
            HgEngineLoadedEntries.Store(path, LoadedKey(info), RowsText(rows));
            return true;
        }

        /// <summary>Writes the rows. A row under an #if is never changed, and new list names go before the first one.</summary>
        public static bool TryWrite(Info info, IReadOnlyList<Row> wanted, out string error)
        {
            if ((error = WhyNot(info)) != null) return false;
            string path = PathOf(info), text = HgEngineFileCache.GetText(path), original = text, nl = text.Contains("\r\n") ? "\r\n" : "\n";
            if (HgEngineLoadedEntries.ChangedOutside(path, LoadedKey(info), RowsText(Rows(info, text))))
            { error = HgEngineLoadedEntries.ConflictMessage(Path.GetFileName(info.File), new[] { info.Symbol }); return false; }
            List<CInitItem> items = CSourceFile.For(text).Find(info.Symbol).Init.Items;
            List<(int At, int Length, string With)> edits = new List<(int, int, string)>();
            List<CInitItem> removed = new List<CInitItem>();

            if (info.IsList)
            {
                HashSet<string> keep = new HashSet<string>(wanted.Select(r => r.Value), StringComparer.Ordinal);
                HashSet<string> present = new HashSet<string>(StringComparer.Ordinal);
                foreach (CInitItem item in items)
                {
                    if (Parse(info, item, text) is not (string _, int s, int e)) continue;
                    string name = text.Substring(s, e - s);
                    present.Add(name);
                    if (keep.Contains(name)) continue;
                    if (item.IsConditional) { error = $"{name} in {info.Symbol} sits under an #if. Remove it in {info.File}."; return false; }
                    removed.Add(item);
                }
                List<string> add = wanted.Select(r => r.Value).Where(n => !present.Contains(n)).ToList();
                if (add.Count > 0)
                {
                    CInitItem firstConditional = items.FirstOrDefault(i => i.IsConditional);
                    CInitItem lastPlain = items.LastOrDefault(i => !i.IsConditional && (firstConditional == null || i.Start < firstConditional.Start));
                    int at = HgEngineSourcePatcher.LineEnd(text, lastPlain?.End ?? CSourceFile.For(text).Find(info.Symbol).Init.Open) + 1;
                    string indent = lastPlain != null ? HgEngineSourcePatcher.IndentOfLine(text, lastPlain.Start) : "    ";
                    edits.Add((at, 0, string.Concat(add.Select(n => $"{indent}{n},{nl}"))));
                }
            }
            else
            {
                Dictionary<string, string> want = wanted.GroupBy(r => r.Key).ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.Ordinal);
                foreach (CInitItem item in items)
                {
                    if (Parse(info, item, text) is not (string key, int s, int e) || !want.TryGetValue(key, out string value)) continue;
                    if (text.Substring(s, e - s) == value) continue;
                    if (item.IsConditional) { error = $"{key} in {info.Symbol} sits under an #if. Change it in {info.File}."; return false; }
                    edits.Add((s, e - s, value));
                }
            }
            // Later positions first, so each edit's offsets still hold; a removal can take its whole line.
            foreach ((int at, int length, string with, CInitItem gone) in edits.Select(e => (e.At, e.Length, e.With, (CInitItem)null))
                         .Concat(removed.Select(r => (r.Start, 0, (string)null, r))).OrderByDescending(e => e.Item1))
                text = gone != null ? HgEngineSourcePatcher.RemoveElement(text, gone.Start, gone.End) : text.Remove(at, length).Insert(at, with);
            if (text == original) return true;

            if (!HgEngineVerifiedWrite.TryWrite(path, info.File, text, written => SameRows(info, Rows(info, written), wanted) ? null : $"{info.Symbol} differs", out error))
                return false;
            HgEngineLoadedEntries.Remember(path, LoadedKey(info), RowsText(Rows(info, text)));
            return true;
        }

        private static bool SameRows(Info info, List<Row> back, IReadOnlyList<Row> wanted) => info.IsList
            ? back.Where(r => !r.Conditional).Select(r => r.Value).OrderBy(v => v).SequenceEqual(wanted.Where(r => !r.Conditional).Select(r => r.Value).OrderBy(v => v))
            : wanted.All(w => back.Any(b => b.Key == w.Key && b.Value == w.Value));
    }
}
