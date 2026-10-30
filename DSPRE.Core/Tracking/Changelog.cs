using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DSPRE.Tracking
{
    public enum ChangelogSince { Original, Release, Uncommitted }
    public enum ChangelogDetail { Names, Areas, All }
    public enum ChangelogGrouping { Subject, Editor }
    public enum ChangelogImages { None, After, Both }
    public enum ChangelogStatus { New, Modified, Removed }

    /// <summary>The changelog's sections, basics first.</summary>
    public enum ChangelogSection { Game, Pokemon, Moves, Items, Trainers, Wild, Trades, Maps, Followers, Facilities, Text, Graphics, Sound, Technical }

    /// <summary>What a changelog holds and how it reads, kept in the changes folder so everyone on the hack shares it.</summary>
    public sealed class ChangelogOptions
    {
        public const string FileName = "changelog.json";

        public string Title { get; set; }
        public ChangelogSince Since { get; set; } = ChangelogSince.Original;
        public string Release { get; set; }
        public ChangelogDetail Detail { get; set; } = ChangelogDetail.All;
        public bool ShowBefore { get; set; } = true;
        public ChangelogGrouping GroupBy { get; set; } = ChangelogGrouping.Subject;
        public bool CountsOnly { get; set; }
        public ChangelogImages Images { get; set; } = ChangelogImages.Both;
        public bool TextLines { get; set; } = true;
        public HashSet<ChangelogSection> Off { get; set; } = new HashSet<ChangelogSection> { ChangelogSection.Technical };
        public Dictionary<ChangelogSection, ChangelogDetail> SectionDetail { get; set; } = new Dictionary<ChangelogSection, ChangelogDetail>();

        /// <summary>Records left out, by their path in the changes folder.</summary>
        public HashSet<string> Hidden { get; set; } = new HashSet<string>(StringComparer.Ordinal);

        public ChangelogDetail DetailOf(ChangelogSection section) => SectionDetail.TryGetValue(section, out ChangelogDetail d) ? d : Detail;

        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
        };

        public static ChangelogOptions Load(string folder)
        {
            string path = Path.Combine(folder, FileName);
            if (!File.Exists(path)) return new ChangelogOptions();
            try { return JsonSerializer.Deserialize<ChangelogOptions>(File.ReadAllText(path), Json) ?? new ChangelogOptions(); }
            catch (JsonException) { return new ChangelogOptions(); }
        }

        public void Save(string folder) => PlainFiles.WriteBytes(Path.Combine(folder, FileName), JsonSerializer.SerializeToUtf8Bytes(this, Json));

        public ChangelogOptions Copy() => JsonSerializer.Deserialize<ChangelogOptions>(JsonSerializer.SerializeToUtf8Bytes(this, Json), Json);
    }

    /// <summary>One changed value, an added or removed entry, or a picture.</summary>
    public sealed class ChangelogLine
    {
        public string Label { get; init; }
        public string Was { get; init; }
        public string Now { get; init; }

        /// <summary>A line that isn't a value going from one thing to another, like "Added Surf at level 5".</summary>
        public string Text { get; init; }

        public ChangelogPicture Picture { get; init; }
    }

    public sealed class ChangelogPicture
    {
        /// <summary>A name for the picture's files, without extension.</summary>
        public string Name { get; init; }
        public byte[] Before { get; init; }
        public byte[] After { get; init; }
    }

    /// <summary>A part of a subject, like Pikachu's stats, and the editor that changes it.</summary>
    public sealed class ChangelogArea
    {
        public string Name { get; init; }
        public string Editor { get; init; }
        public List<ChangelogLine> Lines { get; } = new List<ChangelogLine>();

        /// <summary>Counts for areas listed only by number, like script files, and where they are.</summary>
        public SortedDictionary<string, int> Counts { get; } = new SortedDictionary<string, int>(StringComparer.Ordinal);
        public int Count => Counts.Values.Sum();
        public SortedSet<string> Places { get; } = new SortedSet<string>(StringComparer.Ordinal);

        public bool IsText { get; init; }
    }

    /// <summary>A thing people know by name, like Pikachu or Rival Silvio, with everything changed about it.</summary>
    public sealed class ChangelogSubject
    {
        public ChangelogSection Section { get; init; }
        public string Name { get; set; }
        public string SortKey { get; init; }
        public ChangelogStatus Status { get; set; } = ChangelogStatus.Modified;
        public List<ChangelogArea> Areas { get; } = new List<ChangelogArea>();
        public HashSet<string> Records { get; } = new HashSet<string>(StringComparer.Ordinal);
    }

    /// <summary>Reads the changes folder into subjects for a changelog.</summary>
    public static class Changelog
    {
        public static string SectionTitle(ChangelogSection section) => section switch
        {
            ChangelogSection.Game => "Game",
            ChangelogSection.Pokemon => "Pokémon",
            ChangelogSection.Moves => "Moves",
            ChangelogSection.Items => "Items",
            ChangelogSection.Trainers => "Trainers",
            ChangelogSection.Wild => "Wild Pokémon",
            ChangelogSection.Trades => "Trades",
            ChangelogSection.Maps => "Maps",
            ChangelogSection.Followers => "Following Pokémon",
            ChangelogSection.Facilities => "Battle facilities",
            ChangelogSection.Text => "Text",
            ChangelogSection.Graphics => "Graphics",
            ChangelogSection.Sound => "Sound",
            _ => "Technical",
        };

        /// <summary>Every record the folder holds since the chosen point, gathered by subject. Hidden records are left out.</summary>
        public static async Task<List<ChangelogSubject>> BuildAsync(ChangeTracker tracker, ChangelogOptions options, bool pictures)
        {
            string since = options.Since switch
            {
                ChangelogSince.Release when !string.IsNullOrEmpty(options.Release) => options.Release,
                ChangelogSince.Uncommitted => "HEAD",
                _ => null,
            };
            List<(ChangeKind Kind, string Id, string Key, JsonObject Record)> records = new List<(ChangeKind, string, string, JsonObject)>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            // Only records changed since then are listed.
            HashSet<string> moved = since == null ? null : await ChangedSinceAsync(tracker.Folder, since).ConfigureAwait(false);
            foreach ((ChangeKind kind, string id) in tracker.Records().ToList())
            {
                string key = tracker.RecordKey(kind, id);
                if (key == null || !seen.Add(key) || moved != null && !moved.Contains(key)) continue;
                JsonObject record = tracker.ReadRecord(kind, id)?.Json;
                if (since != null) record = Since(record, await ReadAtAsync(tracker.Folder, since, key).ConfigureAwait(false));
                if (record != null) records.Add((kind, id, key, record));
            }
            if (since != null)
            {
                // Records gone since then were put back to the original game.
                foreach (string key in moved)
                {
                    if (seen.Contains(key) || !key.EndsWith(".json", StringComparison.Ordinal) || !tracker.Kinds.TryRecordAt(key, out ChangeKind kind, out string id)) continue;
                    JsonObject record = Since(null, await ReadAtAsync(tracker.Folder, since, key).ConfigureAwait(false));
                    if (record != null) records.Add((kind, id, key, record));
                }
            }

            return await Task.Run(() =>
            {
                ChangelogSubjects subjects = new ChangelogSubjects(tracker.Context(), tracker.Kinds, pictures);
                foreach ((ChangeKind kind, string id, string key, JsonObject record) in records)
                {
                    if (options.Hidden.Contains(key)) continue;
                    try { subjects.Add(kind, id, key, record); }
                    catch (Exception ex) when (ex is InvalidOperationException || ex is InvalidDataException || ex is FormatException || ex is IOException || ex is ArgumentException)
                    {
                        AppLogger.Warn($"Changelog: {key} was left out: {ex.Message}");
                    }
                }
                return subjects.Result();
            }).ConfigureAwait(false);
        }

        /// <summary>Every record the folder holds, by path and name, for picking which ones to leave out.</summary>
        public static List<(string Key, string Name)> RecordList(ChangeTracker tracker)
        {
            List<(string, string)> list = new List<(string, string)>();
            foreach ((ChangeKind kind, string id) in tracker.Records())
            {
                string key = tracker.RecordKey(kind, id);
                if (key == null) continue;
                string name = tracker.ReadRecord(kind, id)?.Json?["name"]?.GetValue<string>() ?? key;
                list.Add((key, name));
            }
            return list.OrderBy(r => r.Item1, StringComparer.Ordinal).ToList();
        }

        private static async Task<JsonObject> ReadAtAsync(string folder, string commit, string key)
        {
            string text = await GitCli.ShowAsync(folder, commit, key).ConfigureAwait(false);
            if (text == null) return null;
            try { return ChangesJson.Read(text); }
            catch (Exception ex) when (ex is JsonException || ex is InvalidDataException) { return null; }
        }

        /// <summary>Files changed since a commit, new ones included.</summary>
        private static async Task<HashSet<string>> ChangedSinceAsync(string folder, string commit)
        {
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (string[] args in new[]
            {
                new[] { "diff", "--name-only", "--no-renames", commit, "--" },
                new[] { "ls-files", "--others", "--exclude-standard" },
            })
            {
                CliRunner.Result r = await GitCli.RunAsync(folder, args).ConfigureAwait(false);
                if (r.Success) keys.UnionWith(r.Stdout.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0));
            }
            return keys;
        }

        /// <summary>Changes since <paramref name="then"/>: each value's before is its value then; null when nothing changed.</summary>
        public static JsonObject Since(JsonObject now, JsonObject then)
        {
            if (then == null) return now;
            JsonNode changes = Between(then["changes"], now?["changes"]);
            if (changes == null) return null;
            JsonObject record = (JsonObject)(now ?? then).DeepClone();
            record["changes"] = changes;
            if (now == null) record["op"] = then["op"]?.GetValue<string>() == "add" ? "delete" : "edit";
            return record;
        }

        private static JsonNode Between(JsonNode then, JsonNode now)
        {
            if (then == null) return now?.DeepClone();
            if (ObjectDiff.IsLeaf(then) || ObjectDiff.IsLeaf(now))
            {
                JsonNode thenValue = ObjectDiff.IsLeaf(then) ? then[ObjectDiff.Now] : null;
                if (now == null) return ObjectDiff.IsLeaf(then) ? ObjectDiff.Leaf(then[ObjectDiff.Now], then[ObjectDiff.Was]) : null;
                if (!ObjectDiff.IsLeaf(now)) return now.DeepClone();
                if (ObjectDiff.Canonical(thenValue) == ObjectDiff.Canonical(now[ObjectDiff.Now])) return null;
                JsonObject leaf = (JsonObject)now.DeepClone();
                if (ObjectDiff.IsLeaf(then)) leaf[ObjectDiff.Was] = thenValue?.DeepClone();
                return leaf;
            }
            if (then is JsonObject a && (now == null || now is JsonObject))
            {
                JsonObject b = now as JsonObject;
                JsonObject result = new JsonObject();
                foreach (string key in a.Select(e => e.Key).Concat(b?.Select(e => e.Key) ?? Enumerable.Empty<string>()).Distinct().ToList())
                {
                    JsonNode part = Between(a[key], b?[key]);
                    if (part != null) result[key] = part;
                }
                return result.Count == 0 ? null : result;
            }
            if (ObjectDiff.Canonical(then) == ObjectDiff.Canonical(now)) return null;
            return now != null ? now.DeepClone() : Reversed(then);
        }

        // Swaps inserts and removals, for a list put back to the original.
        private static JsonNode Reversed(JsonNode hunks)
        {
            if (hunks is not JsonArray list || !list.All(h => h is JsonObject o && o.ContainsKey("at"))) return null;
            return new JsonArray(list.Cast<JsonObject>().Select(h => (JsonNode)new JsonObject
            {
                ["at"] = h["at"]?.DeepClone(),
                [ObjectDiff.Remove] = h[ObjectDiff.Add]?.DeepClone() ?? new JsonArray(),
                [ObjectDiff.Add] = h[ObjectDiff.Remove]?.DeepClone() ?? new JsonArray(),
            }).ToArray());
        }
    }
}
