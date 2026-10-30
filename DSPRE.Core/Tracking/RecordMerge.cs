using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;

namespace DSPRE.Tracking
{
    /// <summary>One field, line range or file that local and remote changed two different ways.</summary>
    public sealed record FieldConflict(string Path, JsonNode Base, JsonNode Local, JsonNode Remote);

    /// <summary>
    /// Three-way merge of change records. Local and remote are both diffs from the same original ROM, so a field
    /// either side left alone is null, and only a field both sides changed differently needs a person.
    /// </summary>
    public static class RecordMerge
    {
        /// <summary>
        /// The merged "changes" of a record. <paramref name="takeLocal"/> answers each conflict: true keeps local,
        /// false takes remote. Null means no changes are left, so the record is back to the original.
        /// </summary>
        public static JsonNode Merge(JsonNode common, JsonNode local, JsonNode remote, Func<FieldConflict, bool> takeLocal, string path = "")
        {
            if (JsonNode.DeepEquals(local, remote)) return local?.DeepClone();
            if (JsonNode.DeepEquals(local, common)) return remote?.DeepClone();
            if (JsonNode.DeepEquals(remote, common)) return local?.DeepClone();
            if (IsRangeList(local) || IsRangeList(remote))
                return MergeRanges(common as JsonArray, local as JsonArray, remote as JsonArray, takeLocal, path);
            if (IsBranch(common) && IsBranch(local) && IsBranch(remote))
            {
                JsonObject result = new JsonObject();
                IEnumerable<string> keys = Keys(common).Concat(Keys(local)).Concat(Keys(remote)).Distinct(StringComparer.Ordinal);
                foreach (string key in keys.OrderBy(k => k, KeyOrder.Instance))
                {
                    JsonNode merged = Merge(Child(common, key), Child(local, key), Child(remote, key), takeLocal, Join(path, key));
                    if (merged != null) result[key] = merged;
                }
                return result.Count == 0 ? null : result;
            }
            bool keep = takeLocal(new FieldConflict(path, common?.DeepClone(), local?.DeepClone(), remote?.DeepClone()));
            return (keep ? local : remote)?.DeepClone();
        }

        /// <summary>A whole record envelope merged; null when neither side keeps any change.</summary>
        public static JsonObject MergeRecord(JsonObject common, JsonObject local, JsonObject remote, Func<FieldConflict, bool> takeLocal)
        {
            JsonNode changes = Merge(common?["changes"], local?["changes"], remote?["changes"], takeLocal);
            if (changes == null) return null;
            JsonObject envelope = (JsonObject)(local ?? remote ?? common).DeepClone();
            envelope["changes"] = changes;
            return envelope;
        }

        // A branch is a group of fields or list entries, never a was/now pair or a list add or remove.
        private static bool IsBranch(JsonNode node) =>
            node == null || node is JsonObject o && !ObjectDiff.IsLeaf(o) && !IsListOp(o);

        private static bool IsListOp(JsonObject o) => o.Count == 1 && (o.ContainsKey(ObjectDiff.Add) || o.ContainsKey(ObjectDiff.Remove));

        private static IEnumerable<string> Keys(JsonNode node) => node is JsonObject o ? o.Select(e => e.Key) : Enumerable.Empty<string>();

        private static JsonNode Child(JsonNode node, string key) => node is JsonObject o && o.TryGetPropertyValue(key, out JsonNode v) ? v : null;

        // Script hunks and code ranges: lists of edits placed by "at".
        private static bool IsRangeList(JsonNode node) => node is JsonArray a && a.Count > 0 && a.All(e => e is JsonObject o && o.ContainsKey("at"));

        private static JsonNode MergeRanges(JsonArray common, JsonArray local, JsonArray remote, Func<FieldConflict, bool> takeLocal, string path)
        {
            List<JsonObject> b = Items(common), l = Items(local), r = Items(remote);
            List<JsonObject> localOnly = l.Where(x => !b.Any(y => JsonNode.DeepEquals(x, y))).ToList();
            List<JsonObject> remoteOnly = r.Where(x => !b.Any(y => JsonNode.DeepEquals(x, y))).ToList();
            // An edit from the common side survives unless either side dropped it.
            List<JsonObject> result = b.Where(x => l.Any(y => JsonNode.DeepEquals(x, y)) && r.Any(y => JsonNode.DeepEquals(x, y))).ToList();
            List<JsonObject> remoteTaken = new List<JsonObject>(remoteOnly);
            foreach (JsonObject mine in localOnly)
            {
                JsonObject twin = remoteTaken.FirstOrDefault(x => JsonNode.DeepEquals(x, mine));
                if (twin != null)
                {
                    remoteTaken.Remove(twin);
                    result.Add(mine);
                    continue;
                }
                List<JsonObject> clashes = remoteTaken.Where(x => Overlaps(mine, x)).ToList();
                if (clashes.Count == 0)
                {
                    result.Add(mine);
                    continue;
                }
                JsonArray theirs = new JsonArray(clashes.Select(c => (JsonNode)c.DeepClone()).ToArray());
                JsonArray before = new JsonArray(b.Where(x => Overlaps(mine, x) || clashes.Any(c => Overlaps(c, x))).Select(x => (JsonNode)x.DeepClone()).ToArray());
                string where = Join(path, Text(mine["at"]));
                if (takeLocal(new FieldConflict(where, before.Count == 0 ? null : before, new JsonArray(mine.DeepClone()), theirs)))
                {
                    result.Add(mine);
                    foreach (JsonObject c in clashes) remoteTaken.Remove(c);
                }
            }
            result.AddRange(remoteTaken);
            if (result.Count == 0) return null;
            return new JsonArray(result.OrderBy(x => Start(x)).Select(x => (JsonNode)x.DeepClone()).ToArray());
        }

        private static List<JsonObject> Items(JsonArray array) => array?.OfType<JsonObject>().ToList() ?? new List<JsonObject>();

        private static bool Overlaps(JsonObject a, JsonObject b)
        {
            long a0 = Start(a), a1 = a0 + Length(a), b0 = Start(b), b1 = b0 + Length(b);
            // Two inserts at one place clash too, since their order would be a guess.
            if (a0 == b0) return true;
            return a0 < b1 && b0 < a1;
        }

        private static long Start(JsonObject o) => o["at"] is JsonValue v && v.TryGetValue(out long n) ? n : ChangesJson.ParseAddress(o["at"]);

        private static long Length(JsonObject o)
        {
            if (o[ObjectDiff.Remove] is JsonArray removed) return removed.Count;
            if (o[ObjectDiff.Was] is JsonValue was && was.TryGetValue(out string hex)) return hex.Replace(" ", "").Length / 2;
            return 0;
        }

        private static string Text(JsonNode node) => node is JsonValue v && v.TryGetValue(out string s) ? s : node?.ToJsonString() ?? "";

        private static string Join(string path, string key) => string.IsNullOrEmpty(path) ? key : path + "." + key;

        /// <summary>Numbers in numeric order, so list entries and lines stay in order.</summary>
        private sealed class KeyOrder : IComparer<string>
        {
            public static readonly KeyOrder Instance = new KeyOrder();
            public int Compare(string x, string y)
            {
                bool nx = int.TryParse(x, NumberStyles.None, CultureInfo.InvariantCulture, out int ix);
                bool ny = int.TryParse(y, NumberStyles.None, CultureInfo.InvariantCulture, out int iy);
                if (nx && ny) return ix.CompareTo(iy);
                if (nx != ny) return nx ? -1 : 1;
                return string.CompareOrdinal(x, y);
            }
        }
    }
}
