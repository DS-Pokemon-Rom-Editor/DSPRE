using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;

namespace DSPRE.Tracking
{
    /// <summary>
    /// Changes between two records as was/now pairs. Arrays of objects are lists: diffed by index while their length
    /// holds, as hunks of entries when it changes; any other array is one value.
    /// </summary>
    public static class ObjectDiff
    {
        public const string Was = "was", Now = "now", Add = "add", Remove = "remove";

        /// <summary>The changes from <paramref name="from"/> to <paramref name="to"/>, or null when they're the same.</summary>
        public static JsonNode Diff(JsonNode from, JsonNode to)
        {
            if (from is JsonObject a && to is JsonObject b) return DiffObjects(a, b);
            if (IsList(from) && IsList(to)) return DiffLists((JsonArray)from, (JsonArray)to);
            if (JsonNode.DeepEquals(from, to)) return null;
            return Leaf(from, to);
        }

        public static JsonObject Leaf(JsonNode was, JsonNode now) => new JsonObject { [Was] = was?.DeepClone(), [Now] = now?.DeepClone() };

        public static bool IsLeaf(JsonNode node) => node is JsonObject o && o.ContainsKey(Was) && o.ContainsKey(Now)
                                                     && (o.Count == 2 || o.Count == 3 && o.ContainsKey("file"));

        private static JsonObject DiffObjects(JsonObject a, JsonObject b)
        {
            JsonObject result = new JsonObject();
            foreach (KeyValuePair<string, JsonNode> entry in b)
            {
                a.TryGetPropertyValue(entry.Key, out JsonNode before);
                bool had = a.ContainsKey(entry.Key);
                JsonNode change = had ? Diff(before, entry.Value) : Leaf(null, entry.Value);
                if (change != null) result[entry.Key] = change;
            }
            foreach (KeyValuePair<string, JsonNode> entry in a)
                if (!b.ContainsKey(entry.Key)) result[entry.Key] = Leaf(entry.Value, null);
            return result.Count == 0 ? null : result;
        }

        private static JsonNode DiffLists(JsonArray a, JsonArray b)
        {
            // An entry inserted or removed is a hunk of whole entries, so the entries after it don't all read as changed.
            if (a.Count != b.Count)
            {
                JsonArray hunks = new JsonArray();
                int shift = 0;
                foreach (JsonObject h in LineDiff.Hunks(a.Select(Canonical).ToList(), b.Select(Canonical).ToList()).Cast<JsonObject>())
                {
                    int at = h["at"].GetValue<int>() - 1, removed = ((JsonArray)h[Remove]).Count, added = ((JsonArray)h[Add]).Count;
                    hunks.Add(new JsonObject
                    {
                        ["at"] = at + 1,
                        [Remove] = new JsonArray(a.Skip(at).Take(removed).Select(n => n?.DeepClone()).ToArray()),
                        [Add] = new JsonArray(b.Skip(at + shift).Take(added).Select(n => n?.DeepClone()).ToArray()),
                    });
                    shift += added - removed;
                }
                return hunks.Count == 0 ? null : hunks;
            }
            JsonObject result = new JsonObject();
            int count = Math.Max(a.Count, b.Count);
            for (int i = 0; i < count; i++)
            {
                string key = i.ToString(CultureInfo.InvariantCulture);
                if (i >= a.Count) result[key] = new JsonObject { [Add] = b[i]?.DeepClone() };
                else if (i >= b.Count) result[key] = new JsonObject { [Remove] = a[i]?.DeepClone() };
                else if (Diff(a[i], b[i]) is JsonNode change) result[key] = change;
            }
            return result.Count == 0 ? null : result;
        }

        private static bool IsList(JsonNode node) => node is JsonArray array && array.Count > 0 && array.All(e => e is JsonObject)
                                                     || node is JsonArray empty && empty.Count == 0;

        /// <summary>
        /// Applies changes to a copy of <paramref name="start"/>. Where the start holds neither the change's was nor
        /// its now, <paramref name="resolve"/> decides: true takes the change, false keeps the start's value.
        /// </summary>
        public static JsonNode Apply(JsonNode start, JsonNode changes, string path, Func<Conflict, bool> resolve)
        {
            if (changes == null) return start?.DeepClone();
            if (IsLeaf(changes))
            {
                JsonNode was = changes[Was], now = changes[Now];
                if (JsonNode.DeepEquals(start, was) || JsonNode.DeepEquals(start, now)) return now?.DeepClone();
                return resolve(new Conflict(path, was, now, start)) ? now?.DeepClone() : start?.DeepClone();
            }
            if (start is JsonArray list && changes is JsonObject byIndex) return ApplyList(list, byIndex, path, resolve);
            if (start is JsonArray entries && changes is JsonArray hunks)
            {
                List<string> rows = LineDiff.Apply(entries.Select(e => e?.ToJsonString() ?? "null").ToList(), AsLines(hunks), path, resolve, Canonical);
                return new JsonArray(rows.Select(r => JsonNode.Parse(r)).ToArray());
            }
            if (start is JsonObject record && changes is JsonObject fields)
            {
                JsonObject result = (JsonObject)record.DeepClone();
                foreach (KeyValuePair<string, JsonNode> entry in fields)
                {
                    result.TryGetPropertyValue(entry.Key, out JsonNode current);
                    JsonNode next = Apply(current, entry.Value, Join(path, entry.Key), resolve);
                    if (next == null && IsLeaf(entry.Value) && entry.Value[Now] == null) result.Remove(entry.Key);
                    else result[entry.Key] = next;
                }
                return result;
            }
            throw new InvalidOperationException($"{path}: the change doesn't fit the record's shape here.");
        }

        private static JsonArray ApplyList(JsonArray start, JsonObject changes, string path, Func<Conflict, bool> resolve)
        {
            List<JsonNode> items = start.Select(n => n?.DeepClone()).ToList();
            List<int> removals = new List<int>();
            foreach (KeyValuePair<string, JsonNode> entry in changes.OrderBy(e => int.Parse(e.Key, CultureInfo.InvariantCulture)))
            {
                int index = int.Parse(entry.Key, CultureInfo.InvariantCulture);
                string at = Join(path, entry.Key);
                if (entry.Value is JsonObject op && op.Count == 1 && op.ContainsKey(Add))
                {
                    JsonNode added = op[Add];
                    if (index < items.Count)
                    {
                        if (!JsonNode.DeepEquals(items[index], added) && resolve(new Conflict(at, null, added, items[index]))) items[index] = added?.DeepClone();
                    }
                    else
                    {
                        while (items.Count < index) items.Add(null);
                        items.Add(added?.DeepClone());
                    }
                }
                else if (entry.Value is JsonObject rem && rem.Count == 1 && rem.ContainsKey(Remove))
                {
                    if (index >= items.Count) continue;
                    if (JsonNode.DeepEquals(items[index], rem[Remove]) || resolve(new Conflict(at, rem[Remove], null, items[index]))) removals.Add(index);
                }
                else if (index < items.Count) items[index] = Apply(items[index], entry.Value, at, resolve);
                else resolve(new Conflict(at, null, entry.Value, null));
            }
            foreach (int index in removals.OrderByDescending(i => i)) items.RemoveAt(index);
            return new JsonArray(items.ToArray());
        }

        /// <summary>Hunks of lines, each a line of JSON, as hunks of the entries they hold.</summary>
        public static JsonArray AsEntries(JsonArray hunks) => new JsonArray(hunks.Cast<JsonObject>().Select(h => (JsonNode)new JsonObject
        {
            ["at"] = h["at"]?.DeepClone(),
            [Remove] = new JsonArray(((JsonArray)h[Remove]).Select(n => JsonNode.Parse(n.GetValue<string>())).ToArray()),
            [Add] = new JsonArray(((JsonArray)h[Add]).Select(n => JsonNode.Parse(n.GetValue<string>())).ToArray()),
        }).ToArray());

        // Hunks of entries as hunks of lines, each entry written as one line of JSON.
        public static JsonArray AsLines(JsonArray hunks) => new JsonArray(hunks.Cast<JsonObject>().Select(h => (JsonNode)new JsonObject
        {
            ["at"] = h["at"]?.DeepClone(),
            [Remove] = new JsonArray(((JsonArray)h[Remove]).Select(n => (JsonNode)(n?.ToJsonString() ?? "null")).ToArray()),
            [Add] = new JsonArray(((JsonArray)h[Add]).Select(n => (JsonNode)(n?.ToJsonString() ?? "null")).ToArray()),
        }).ToArray());

        /// <summary>An entry as text to compare by, with names left out of references so a rename isn't a difference.</summary>
        public static string Canonical(JsonNode node) => Strip(node?.DeepClone())?.ToJsonString() ?? "null";

        public static string Canonical(string json) => Canonical(JsonNode.Parse(json));

        private static JsonNode Strip(JsonNode node)
        {
            if (node is JsonObject o)
                foreach (KeyValuePair<string, JsonNode> e in o.ToList()) o[e.Key] = Strip(e.Value?.DeepClone());
            else if (node is JsonArray a)
                return new JsonArray(a.Select(e => Strip(e?.DeepClone())).ToArray());
            else if (node is JsonValue v && v.TryGetValue(out string s) && s.Length > 0 && (char.IsDigit(s[0]) || s[0] == '-' && s.Length > 1 && char.IsDigit(s[1])))
            {
                int end = s[0] == '-' ? 1 : 0;
                while (end < s.Length && char.IsDigit(s[end])) end++;
                if (end == s.Length || s[end] == ' ') return JsonValue.Create(long.Parse(s.AsSpan(0, end), CultureInfo.InvariantCulture));
            }
            return node;
        }

        private static string Join(string path, string key) => string.IsNullOrEmpty(path) ? key : path + "." + key;
    }

    /// <summary>A change whose was doesn't match the ROM it's applied to.</summary>
    public sealed record Conflict(string Path, JsonNode Was, JsonNode Now, JsonNode Rom)
    {
        public string Record { get; init; }
    }
}
