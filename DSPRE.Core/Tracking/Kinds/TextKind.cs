using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using DSPRE.CharMaps;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Tracking.Kinds
{
    /// <summary>A text archive as changed, added and removed lines, one line per message.</summary>
    public sealed class TextKind : ChangeKind
    {
        private const string SourceDir = "expanded/textArchives";
        private const string BaseTextDir = ".dspre-text";

        public override string Name => "text";
        public override string Folder => "text";
        public override string Title(string id, ChangeContext ctx) => "Text archive " + id;

        public override bool Owns(TrackedFile file, out string id)
        {
            id = null;
            if (file.TryMember(out DirNames dir, out int member) && dir == DirNames.TextArchives) id = Id(member);
            else if (file.IsSource && file.SourcePath.StartsWith(SourceDir + "/", StringComparison.Ordinal)
                     && file.SourcePath.EndsWith(".json", StringComparison.Ordinal)
                     && int.TryParse(Path.GetFileNameWithoutExtension(file.SourcePath), NumberStyles.None, CultureInfo.InvariantCulture, out int n))
                id = Id(n);
            return id != null;
        }

        public override IReadOnlyList<TrackedFile> Files(string id, ChangeContext ctx) => new[] { Member(id), Source(id) };

        private static TrackedFile Member(string id) => TrackedFile.Member(DirNames.TextArchives, Number(id));
        private static TrackedFile Source(string id) => TrackedFile.Source(SourceDir + "/" + Number(id).ToString("D4", CultureInfo.InvariantCulture) + ".json");

        public override RecordFiles Export(string id, ChangeContext ctx)
        {
            List<string> before = Messages(id, ctx.Base.Read, ctx), now = Messages(id, ctx.Project.Read, ctx);
            if (before == null && now == null || before != null && now != null && before.SequenceEqual(now, StringComparer.Ordinal)) return null;

            string op = before == null ? "add" : now == null ? "delete" : "edit";
            // Messages inserted or removed are hunks, so the lines after them don't all read as changed.
            if (before != null && now != null && before.Count != now.Count)
            {
                JsonObject edited = Envelope(id, Title(id, ctx), new[] { Member(id) }, op);
                edited["changes"] = new JsonObject { ["hunks"] = LineDiff.Hunks(before, now) };
                return new RecordFiles { Json = edited };
            }
            JsonObject lines = new JsonObject();
            List<string> from = before ?? new List<string>(), to = now ?? new List<string>();
            for (int i = 0; i < Math.Max(from.Count, to.Count); i++)
            {
                string key = i.ToString(CultureInfo.InvariantCulture);
                if (i >= from.Count) lines[key] = new JsonObject { [ObjectDiff.Add] = to[i] };
                else if (i >= to.Count) lines[key] = new JsonObject { [ObjectDiff.Remove] = from[i] };
                else if (from[i] != to[i]) lines[key] = ObjectDiff.Leaf(from[i], to[i]);
            }
            JsonObject json = Envelope(id, Title(id, ctx), new[] { Member(id) }, op);
            json["changes"] = new JsonObject { ["lines"] = lines };
            return new RecordFiles { Json = json };
        }

        public override Dictionary<TrackedFile, byte[]> Apply(string id, RecordFiles change, Func<TrackedFile, byte[]> start, ChangeContext ctx, Func<Conflict, bool> resolve)
        {
            string title = Title(id, ctx);
            List<string> messages = Messages(id, start, ctx) ?? new List<string>();
            if (change.Json["changes"]?["hunks"] is JsonArray hunks)
                return new Dictionary<TrackedFile, byte[]> { [Source(id)] = new TextArchive(Number(id), LineDiff.Apply(messages, hunks, title, resolve)).ToExpandedJsonBytes(Number(id)) };
            JsonObject lines = change.Json["changes"]?["lines"] as JsonObject ?? new JsonObject();
            List<int> removals = new List<int>();
            foreach (KeyValuePair<string, JsonNode> entry in lines.OrderBy(e => int.Parse(e.Key, CultureInfo.InvariantCulture)))
            {
                int i = int.Parse(entry.Key, CultureInfo.InvariantCulture);
                JsonObject op = (JsonObject)entry.Value;
                string at = "line " + entry.Key;
                if (op.ContainsKey(ObjectDiff.Add))
                {
                    string added = op[ObjectDiff.Add].GetValue<string>();
                    if (i < messages.Count)
                    {
                        if (messages[i] != added && resolve(new Conflict(at, null, added, messages[i]) { Record = title })) messages[i] = added;
                    }
                    else
                    {
                        while (messages.Count < i) messages.Add("");
                        messages.Add(added);
                    }
                }
                else if (op.ContainsKey(ObjectDiff.Remove))
                {
                    if (i >= messages.Count) continue;
                    string removed = op[ObjectDiff.Remove].GetValue<string>();
                    if (messages[i] == removed || resolve(new Conflict(at, removed, null, messages[i]) { Record = title })) removals.Add(i);
                }
                else
                {
                    string was = op[ObjectDiff.Was]?.GetValue<string>(), now = op[ObjectDiff.Now]?.GetValue<string>();
                    string current = i < messages.Count ? messages[i] : null;
                    if (current == was || current == now || resolve(new Conflict(at, was, now, current) { Record = title }))
                    {
                        while (messages.Count <= i) messages.Add("");
                        messages[i] = now ?? "";
                    }
                }
            }
            foreach (int i in removals.OrderByDescending(i => i)) messages.RemoveAt(i);
            return new Dictionary<TrackedFile, byte[]> { [Source(id)] = new TextArchive(Number(id), messages).ToExpandedJsonBytes(Number(id)) };
        }

        /// <summary>The archive's messages from its expanded JSON, or from its member when it has none.</summary>
        private List<string> Messages(string id, Func<TrackedFile, byte[]> read, ChangeContext ctx)
        {
            byte[] source = read(Source(id));
            if (source != null) return FromTemp(source, ".json");
            byte[] member = read(Member(id));
            if (member == null) return null;
            byte[] baseMember = ctx.Base.Read(Member(id));
            if (baseMember != null && member.AsSpan().SequenceEqual(baseMember) && BaseJson(id, ctx) is string cached) return TextArchive.ReadMessagesFrom(cached);
            return FromTemp(member, "");
        }

        private static List<string> FromTemp(byte[] bytes, string extension)
        {
            string temp = Path.Combine(Path.GetTempPath(), "dspre_track_" + Guid.NewGuid().ToString("N") + extension);
            try
            {
                PlainFiles.WriteBytes(temp, bytes);
                return TextArchive.ReadMessagesFrom(temp);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        /// <summary>The base ROM's text decoded once into its extracted folder, in a single chatot run.</summary>
        private static string BaseJson(string id, ChangeContext ctx)
        {
            string jsonDir = Path.Combine(ctx.Base.Root, BaseTextDir);
            string json = Path.Combine(jsonDir, Number(id).ToString("D4", CultureInfo.InvariantCulture) + ".json");
            if (File.Exists(json)) return json;
            if (File.Exists(Path.Combine(jsonDir, ".done"))) return null;
            string binDir = Path.Combine(ctx.Base.Root, BaseTextDir + "-bin");
            Directory.CreateDirectory(binDir);
            Directory.CreateDirectory(jsonDir);
            foreach (TrackedFile member in ctx.Base.Members(DirNames.TextArchives))
            {
                member.TryMember(out _, out int n);
                PlainFiles.WriteBytes(Path.Combine(binDir, n.ToString("D4", CultureInfo.InvariantCulture)), ctx.Base.Read(member));
            }
            TextConverter.FolderToJSON(binDir, jsonDir, CharMapManager.GetCharMapPath());
            PlainFiles.WriteText(Path.Combine(jsonDir, ".done"), "");
            return File.Exists(json) ? json : null;
        }
    }
}
