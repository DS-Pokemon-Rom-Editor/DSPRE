using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;

namespace DSPRE.Tracking
{
    /// <summary>The base and project trees a kind reads, and the names it writes beside numbers.</summary>
    public sealed class ChangeContext
    {
        public RomTree Base { get; }
        public RomTree Project { get; }
        public TrackingNames Names { get; }

        /// <summary>What wrote each code range, by the code file's path in the project.</summary>
        public Func<string, IReadOnlyList<SyncState.CodeWrite>> CodeWrites { get; init; } = _ => Array.Empty<SyncState.CodeWrite>();

        public ChangeContext(RomTree baseTree, RomTree project, TrackingNames names)
        {
            Base = baseTree;
            Project = project;
            Names = names;
        }
    }

    /// <summary>A record's JSON and the files that sit next to it, by suffix (".png").</summary>
    public sealed class RecordFiles
    {
        public JsonObject Json { get; init; }
        public Dictionary<string, byte[]> Sidecars { get; } = new Dictionary<string, byte[]>(StringComparer.Ordinal);
    }

    /// <summary>One kind of record in the changes folder: which ROM files make it up, and how its change is written and applied.</summary>
    public abstract class ChangeKind
    {
        public abstract string Name { get; }

        /// <summary>The kind's folder in the changes folder, like "trainers" or "pokemon/personal".</summary>
        public abstract string Folder { get; }

        /// <summary>The record as people know it, like "Trainer 12 Joey".</summary>
        public abstract string Title(string id, ChangeContext ctx);

        public abstract bool Owns(TrackedFile file, out string id);

        public abstract IReadOnlyList<TrackedFile> Files(string id, ChangeContext ctx);

        /// <summary>The record's change against the base, or null when it has none.</summary>
        public abstract RecordFiles Export(string id, ChangeContext ctx);

        /// <summary>Each file's bytes once the change is applied over <paramref name="start"/>; null deletes the file.</summary>
        public abstract Dictionary<TrackedFile, byte[]> Apply(string id, RecordFiles change, Func<TrackedFile, byte[]> start, ChangeContext ctx, Func<Conflict, bool> resolve);

        /// <summary>The file name without extension; numbered records start with the number.</summary>
        public virtual string Stem(string id, ChangeContext ctx)
        {
            if (!int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out int number)) return id;
            string slug = ChangesJson.Slug(SlugName(id, ctx));
            return number.ToString("D4", CultureInfo.InvariantCulture) + (slug.Length > 0 ? "-" + slug : "");
        }

        /// <summary>The name in a numbered record's file name.</summary>
        protected virtual string SlugName(string id, ChangeContext ctx) => null;

        /// <summary>The record a file name in the kind's folder belongs to.</summary>
        public virtual bool TryParseStem(string stem, out string id)
        {
            int end = 0;
            while (end < stem.Length && char.IsDigit(stem[end])) end++;
            id = end > 0 && (end == stem.Length || stem[end] == '-') ? int.Parse(stem.AsSpan(0, end), CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) : null;
            return id != null;
        }

        protected JsonObject Envelope(string id, string title, IEnumerable<TrackedFile> members, string op)
        {
            JsonObject json = new JsonObject
            {
                ["$schema"] = SchemaPath(),
                ["kind"] = Name,
                ["id"] = int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out int number) ? (JsonNode)number : id,
            };
            if (!string.IsNullOrEmpty(title)) json["name"] = title;
            // A whole table's rows are named once, by the table.
            List<TrackedFile> list = members.ToList();
            string table = null;
            bool oneTable = list.Count > 1 && list.All(m => m.TryRow(out string t, out _) && (table ??= t) == t);
            json["members"] = oneTable
                ? new JsonArray((JsonNode)("table:" + table))
                : new JsonArray(list.Select(m => (JsonNode)m.Key).ToArray());
            json["op"] = op;
            return json;
        }

        private string SchemaPath()
        {
            int depth = Folder.Count(c => c == '/') + 1;
            return string.Concat(Enumerable.Repeat("../", depth)) + ".schema/" + Name + ".json";
        }

        protected static int Number(string id) => int.Parse(id, CultureInfo.InvariantCulture);
        protected static string Id(int number) => number.ToString(CultureInfo.InvariantCulture);

        protected static bool Same(byte[] a, byte[] b) => a == null ? b == null : b != null && a.AsSpan().SequenceEqual(b);
    }
}
