using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace DSPRE.Tracking
{
    /// <summary>
    /// A record read into named fields by its format class. A change the fields can't express is kept as whole
    /// files instead, so nothing is lost.
    /// </summary>
    public abstract class FieldKind : ChangeKind
    {
        /// <summary>The record as JSON, or null when the files don't hold one.</summary>
        protected abstract JsonObject Decode(string id, Func<TrackedFile, byte[]> read, ChangeContext ctx);

        /// <summary>The files' bytes for <paramref name="record"/>, built over <paramref name="start"/> to keep fields the JSON doesn't name.</summary>
        protected abstract Dictionary<TrackedFile, byte[]> Encode(string id, JsonObject record, Func<TrackedFile, byte[]> start, ChangeContext ctx);

        protected virtual string RawExtension(TrackedFile file) => ".bin";

        // Binary parts of a record (a map's model, a picture) are written into the record as their SHA-1 and kept as files next to the JSON.
        private readonly Dictionary<string, byte[]> _blobs = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        protected string Blob(byte[] bytes)
        {
            if (bytes == null) return null;
            string sha = ChangesJson.Sha1(bytes);
            _blobs[sha] = bytes;
            return sha;
        }

        protected byte[] BlobBytes(string sha)
        {
            if (sha == null) return null;
            return _blobs.TryGetValue(sha, out byte[] bytes) ? bytes : throw new InvalidDataException($"The file with {sha} is missing from the changes folder.");
        }

        /// <summary>The extension a binary part is kept with, by its key in the record.</summary>
        protected virtual string BlobExtension(string key) => ".bin";

        /// <summary>The whole record as the project or the original ROM holds it, with its pictures and other binary parts by SHA-1.</summary>
        public JsonObject Current(string id, ChangeContext ctx, bool original, out IReadOnlyDictionary<string, byte[]> parts)
        {
            lock (_blobs)
            {
                _blobs.Clear();
                JsonObject json = null;
                try { json = Decode(id, original ? ctx.Base.Read : ctx.Project.Read, ctx); }
                catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is InvalidOperationException || ex is ArgumentException || ex is IndexOutOfRangeException)
                {
                    json = null;
                }
                parts = new Dictionary<string, byte[]>(_blobs, StringComparer.Ordinal);
                return json;
            }
        }

        public override RecordFiles Export(string id, ChangeContext ctx)
        {
            lock (_blobs)
            {
                _blobs.Clear();
                return ExportLocked(id, ctx);
            }
        }

        private RecordFiles ExportLocked(string id, ChangeContext ctx)
        {
            IReadOnlyList<TrackedFile> files = Files(id, ctx);
            Func<TrackedFile, byte[]> before = ctx.Base.Read, now = ctx.Project.Read;
            Dictionary<TrackedFile, byte[]> baseBytes = files.ToDictionary(f => f, before), nowBytes = files.ToDictionary(f => f, now);
            if (files.All(f => Same(baseBytes[f], nowBytes[f]))) return null;

            JsonObject changes = null;
            string op = "edit";
            try
            {
                JsonObject baseRecord = Decode(id, f => baseBytes.TryGetValue(f, out byte[] b) ? b : before(f), ctx);
                JsonObject nowRecord = Decode(id, f => nowBytes.TryGetValue(f, out byte[] b) ? b : now(f), ctx);
                if (nowRecord != null && Rebuilds(id, nowRecord, baseBytes, nowBytes, ctx))
                {
                    if (baseRecord == null)
                    {
                        op = "add";
                        changes = new JsonObject { [ObjectDiff.Now] = nowRecord };
                    }
                    else changes = ObjectDiff.Diff(baseRecord, nowRecord) as JsonObject;
                }
                else if (nowRecord == null && baseRecord != null && files.All(f => nowBytes[f] == null))
                {
                    op = "delete";
                    changes = new JsonObject { [ObjectDiff.Was] = baseRecord };
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Track changes: {Title(id, ctx)} is kept as whole files: {ex.Message}");
                changes = null;
                op = "edit";
            }

            RecordFiles result = new RecordFiles();
            if (changes == null)
            {
                changes = new JsonObject();
                op = "edit";
                if (!RawChanges.Export(changes, result, files, f => baseBytes[f], f => nowBytes[f], RawExtension)) return null;
            }
            if (!changes.ContainsKey(RawChanges.Key)) AttachBlobs(changes, null, result);
            JsonObject json = Envelope(id, Title(id, ctx), files, op);
            json["changes"] = changes;
            return new RecordFiles { Json = json }.WithSidecars(result);
        }

        /// <summary>Puts each binary part the changes now hold next to the JSON, and names its file beside its SHA-1.</summary>
        private void AttachBlobs(JsonNode node, string key, RecordFiles into)
        {
            if (node is JsonObject leaf && ObjectDiff.IsLeaf(leaf))
            {
                if (leaf[ObjectDiff.Now] is JsonValue v && v.TryGetValue(out string sha) && _blobs.TryGetValue(sha, out byte[] bytes))
                {
                    string suffix = Suffix(key, into, bytes);
                    leaf["file"] = suffix;
                }
                return;
            }
            if (node is JsonObject o)
            {
                foreach (KeyValuePair<string, JsonNode> e in o.ToList())
                {
                    if (e.Key == ObjectDiff.Was || e.Key == ObjectDiff.Remove) continue;
                    if (e.Value is JsonValue value && value.TryGetValue(out string s) && _blobs.TryGetValue(s, out byte[] b)) Suffix(e.Key, into, b);
                    else AttachBlobs(e.Value, int.TryParse(e.Key, out _) || e.Key == ObjectDiff.Now || e.Key == ObjectDiff.Add ? key : e.Key, into);
                }
            }
            else if (node is JsonArray a)
                foreach (JsonNode item in a) AttachBlobs(item, key, into);
        }

        private string Suffix(string key, RecordFiles into, byte[] bytes)
        {
            string ext = BlobExtension(key);
            string suffix = ext;
            if (into.Sidecars.TryGetValue(suffix, out byte[] taken) && !Same(taken, bytes)) suffix = "." + key + ext;
            for (int n = 2; into.Sidecars.TryGetValue(suffix, out taken) && !Same(taken, bytes); n++) suffix = "." + key + n + ext;
            into.Sidecars[suffix] = bytes;
            return suffix;
        }

        private bool Rebuilds(string id, JsonObject record, Dictionary<TrackedFile, byte[]> before, Dictionary<TrackedFile, byte[]> now, ChangeContext ctx)
        {
            Func<TrackedFile, byte[]> start = f => before.TryGetValue(f, out byte[] b) && b != null ? b : now.TryGetValue(f, out byte[] n) ? n : null;
            Dictionary<TrackedFile, byte[]> built = Encode(id, (JsonObject)record.DeepClone(), start, ctx);
            return now.Keys.All(f => Same(built.TryGetValue(f, out byte[] b) ? b : start(f), now[f]));
        }

        /// <summary>Why the project's record doesn't rebuild exactly from its JSON, built over its own files or <paramref name="over"/>'s; null when it does.</summary>
        public string WhyNotRoundTrip(string id, ChangeContext ctx, string over = null)
        {
            lock (_blobs)
            try
            {
                JsonObject record = Decode(id, ctx.Project.Read, ctx);
                if (record == null) return null;
                JsonObject again = (JsonObject)JsonNode.Parse(ChangesJson.Write(record));
                Dictionary<TrackedFile, TrackedFile> swap = over == null ? null : Files(id, ctx).Zip(Files(over, ctx)).ToDictionary(p => p.First, p => p.Second);
                Func<TrackedFile, byte[]> start = swap == null ? ctx.Project.Read : f => ctx.Project.Read(swap.TryGetValue(f, out TrackedFile o) ? o : f);
                Dictionary<TrackedFile, byte[]> built = Encode(id, again, start, ctx);
                foreach (TrackedFile f in Files(id, ctx))
                    if (built.TryGetValue(f, out byte[] b) && !Same(b, ctx.Project.Read(f)))
                    {
                        byte[] want = ctx.Project.Read(f);
                        int at = 0;
                        while (at < b.Length && at < want.Length && b[at] == want[at]) at++;
                        return $"{f.Key} rebuilds differently from byte {at} ({b.Length} bytes, should be {want.Length})";
                    }
                return null;
            }
            catch (Exception ex) { return ex.GetType().Name + ": " + ex.Message; }
        }

        public override Dictionary<TrackedFile, byte[]> Apply(string id, RecordFiles change, Func<TrackedFile, byte[]> start, ChangeContext ctx, Func<Conflict, bool> resolve)
        {
            lock (_blobs)
            {
                _blobs.Clear();
                foreach (byte[] sidecar in change.Sidecars.Values) _blobs[ChangesJson.Sha1(sidecar)] = sidecar;
                return ApplyLocked(id, change, start, ctx, resolve);
            }
        }

        private Dictionary<TrackedFile, byte[]> ApplyLocked(string id, RecordFiles change, Func<TrackedFile, byte[]> start, ChangeContext ctx, Func<Conflict, bool> resolve)
        {
            string title = Title(id, ctx);
            Func<Conflict, bool> named = c => resolve(c with { Record = title });
            JsonObject changes = change.Json["changes"] as JsonObject ?? new JsonObject();
            Dictionary<TrackedFile, byte[]> result = new Dictionary<TrackedFile, byte[]>();
            if (changes[RawChanges.Key] is JsonObject raw)
            {
                RawChanges.Apply(raw, change, start, title, named, result);
                return result;
            }

            string op = change.Json["op"]?.GetValue<string>() ?? "edit";
            JsonObject current = Decode(id, start, ctx);
            JsonObject target;
            if (op == "add")
            {
                target = (JsonObject)changes[ObjectDiff.Now].DeepClone();
                if (current != null && !JsonNode.DeepEquals(current, target) && !named(new Conflict("", null, target, current))) return result;
            }
            else if (op == "delete")
            {
                if (current != null && !JsonNode.DeepEquals(current, changes[ObjectDiff.Was])
                    && !named(new Conflict("", changes[ObjectDiff.Was], null, current))) return result;
                foreach (TrackedFile f in Files(id, ctx)) result[f] = null;
                return result;
            }
            else
            {
                if (current == null) throw new InvalidOperationException($"{title} isn't in this ROM, so its changes can't be applied.");
                target = (JsonObject)ObjectDiff.Apply(current, changes, "", named);
            }
            foreach (KeyValuePair<TrackedFile, byte[]> built in Encode(id, target, start, ctx))
                if (!Same(built.Value, start(built.Key))) result[built.Key] = built.Value;
            return result;
        }
    }

    internal static class RecordFilesExtensions
    {
        public static RecordFiles WithSidecars(this RecordFiles target, RecordFiles from)
        {
            foreach (KeyValuePair<string, byte[]> s in from.Sidecars) target.Sidecars[s.Key] = s.Value;
            return target;
        }
    }
}
