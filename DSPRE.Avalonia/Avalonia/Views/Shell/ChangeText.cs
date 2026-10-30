using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DSPRE.Tracking;

namespace DSPRE.Avalonia.Views.Shell
{
    /// <summary>A record's changes as rows of field, before and after, and the tables the merge and patch windows show them in.</summary>
    internal static class ChangeText
    {
        /// <summary>"party.0.level" as Party 0 level.</summary>
        public static string FieldName(string path)
        {
            // "rows" only holds a table's rows, and a table of one value repeats its name.
            List<string> parts = path.Split('.').Where(p => p.Length > 0 && p != "rows").Select(p => p.Replace('_', ' ').Replace(KeyDot, '.')).ToList();
            string text = string.Join(" ", parts.Where((p, i) => i == 0 || !string.Equals(p, parts[i - 1], System.StringComparison.OrdinalIgnoreCase)));
            return text.Length == 0 ? "Whole record" : char.ToUpperInvariant(text[0]) + text.Substring(1);
        }

        // Marks a dot inside a key, like "Vs. Seeker", that isn't a path step.
        private const char KeyDot = '\u0001';

        public static JsonObject Leaf(JsonNode node) => node is JsonObject o && ObjectDiff.IsLeaf(o) ? o : null;

        public static string ValueText(JsonNode node, string none)
        {
            if (node == null) return none;
            if (node is JsonObject single && single.Count == 1) node = single.First().Value;
            string text = node is JsonValue v && v.TryGetValue(out string s) ? s : node.ToJsonString();
            if (text.StartsWith("sha1:", System.StringComparison.Ordinal)) return "file " + text.Substring(5, 7);
            return text.Length > 140 ? text.Substring(0, 140) + "…" : text;
        }

        /// <summary>Every changed field with its value before and after; inserted and removed entries as one row each.</summary>
        public static List<(string Field, string Was, string Now)> Rows(JsonNode changes)
        {
            List<(string, string, string)> rows = new List<(string, string, string)>();
            void Walk(JsonNode node, string path)
            {
                if (Leaf(node) is JsonObject leaf) rows.Add((FieldName(path), ValueText(leaf[ObjectDiff.Was], "none"), ValueText(leaf[ObjectDiff.Now], "removed")));
                else if (node is JsonObject hunk && hunk.ContainsKey("at") && (hunk.ContainsKey(ObjectDiff.Add) || hunk.ContainsKey(ObjectDiff.Remove)))
                    rows.Add((FieldName(path) + " at " + ValueText(hunk["at"], ""), Lines(hunk[ObjectDiff.Remove]), Lines(hunk[ObjectDiff.Add])));
                else if (node is JsonObject branch) foreach (KeyValuePair<string, JsonNode> e in branch) Walk(e.Value, path.Length == 0 ? e.Key.Replace('.', KeyDot) : path + "." + e.Key.Replace('.', KeyDot));
                else if (node is JsonArray list && list.All(i => i is JsonObject)) foreach (JsonNode item in list) Walk(item, path);
                else rows.Add((FieldName(path), "", ValueText(node, "")));
            }
            Walk(changes, "");
            return rows;
        }

        private static string Lines(JsonNode node) =>
            node is JsonArray a && a.Count > 0 ? string.Join("\n", a.Take(8).Select(n => ValueText(Entry(n), ""))) + (a.Count > 8 ? "\n…" : "") : "none";

        // Inserted entries are kept as lines of JSON.
        private static JsonNode Entry(JsonNode line)
        {
            if (line is not JsonValue v || !v.TryGetValue(out string text) || !text.StartsWith('{')) return line;
            try { return JsonNode.Parse(text); }
            catch (System.Text.Json.JsonException) { return line; }
        }

        public static void AddRow(Grid table, params Control[] cells)
        {
            int row = table.RowDefinitions.Count;
            table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (int i = 0; i < cells.Length; i++)
            {
                Grid.SetRow(cells[i], row);
                Grid.SetColumn(cells[i], i);
                cells[i].Margin = new global::Avalonia.Thickness(0, 3, 10, 3);
                cells[i].VerticalAlignment = VerticalAlignment.Center;
                table.Children.Add(cells[i]);
            }
        }

        public static TextBlock Header(string text)
        {
            TextBlock t = Subtle(text);
            t.FontWeight = FontWeight.SemiBold;
            return t;
        }

        public static TextBlock Cell(string text) => new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };

        public static TextBlock Subtle(string text)
        {
            TextBlock t = new TextBlock { Text = text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            t.Bind(TextBlock.ForegroundProperty, t.GetResourceObservable("Editor.Subtle"));
            return t;
        }
    }
}
