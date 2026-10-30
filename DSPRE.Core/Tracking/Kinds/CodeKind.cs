using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using DSPRE.HgEngine;

namespace DSPRE.Tracking.Kinds
{
    /// <summary>Changed byte ranges of arm9 and the overlays, each with the bytes it replaced and what wrote it.</summary>
    public sealed class CodeKind : ChangeKind
    {
        // Equal runs shorter than this join the ranges around them.
        private const int Join = 16;

        public override string Name => "code";
        public override string Folder => "code";
        public override string Title(string id, ChangeContext ctx) => id == "arm9" ? "arm9" : "Overlay " + int.Parse(id.Substring(8), System.Globalization.CultureInfo.InvariantCulture);

        public override bool Owns(TrackedFile file, out string id)
        {
            id = null;
            string path = file.TreePath;
            if (path == null || !HgEngineWriteGuard.TryIdentify(path, out int overlay)) return false;
            if (!path.EndsWith(".bin", StringComparison.OrdinalIgnoreCase)) return false;
            id = overlay < 0 ? "arm9" : "overlay-" + overlay.ToString("D4");
            return true;
        }

        public override string Stem(string id, ChangeContext ctx) => id;

        public override bool TryParseStem(string stem, out string id)
        {
            id = stem == "arm9" || stem.StartsWith("overlay-", StringComparison.Ordinal) && stem.Length == 12 ? stem : null;
            return id != null;
        }

        public override IReadOnlyList<TrackedFile> Files(string id, ChangeContext ctx)
        {
            string path = id == "arm9"
                ? System.IO.Path.GetRelativePath(ctx.Project.Root, RomInfo.Arm9Path)
                : System.IO.Path.GetRelativePath(ctx.Project.Root, OverlayUtils.GetPath(int.Parse(id.Substring(8))));
            return new[] { TrackedFile.Tree(path) };
        }

        public override RecordFiles Export(string id, ChangeContext ctx)
        {
            TrackedFile file = Files(id, ctx)[0];
            byte[] before = ctx.Base.Read(file), now = ctx.Project.Read(file);
            // Tables kept as records of their own aren't code changes.
            if (before != null && now != null)
            {
                now = (byte[])now.Clone();
                foreach (CodeTable table in ctx.Project.Tables.Where(t => t.TreePath == file.Key))
                    foreach (long i in table.Bytes()) if (i < now.Length && i < before.Length) now[i] = before[i];
            }
            if (before == null ? now == null : now != null && before.AsSpan().SequenceEqual(now)) return null;
            RecordFiles result = new RecordFiles();
            JsonObject changes = new JsonObject();
            List<(int At, int Length)> ranges = before == null || now == null ? null : Ranges(before, now);
            int changed = ranges?.Sum(r => r.Length) ?? int.MaxValue;
            if (ranges == null || changed > now.Length / 2)
            {
                byte[] masked = now;
                if (!RawChanges.Export(changes, result, new[] { file }, ctx.Base.Read, _ => masked, _ => ".bin")) return null;
            }
            else
            {
                IReadOnlyList<SyncState.CodeWrite> writes = ctx.CodeWrites(file.Key);
                JsonArray list = new JsonArray();
                foreach ((int at, int length) in ranges)
                {
                    JsonObject range = new JsonObject
                    {
                        ["at"] = ChangesJson.Address(at),
                        [ObjectDiff.Was] = ChangesJson.Hex(Slice(before, at, length)),
                        [ObjectDiff.Now] = ChangesJson.Hex(Slice(now, at, length)),
                    };
                    string by = writes.LastOrDefault(w => w.At < at + length && at < w.At + w.Length)?.By;
                    if (by != null) range["by"] = by;
                    list.Add(range);
                }
                changes["ranges"] = list;
            }
            JsonObject json = Envelope(id, Title(id, ctx), new[] { file }, "edit");
            json["changes"] = changes;
            return new RecordFiles { Json = json }.WithSidecars(result);
        }

        public override Dictionary<TrackedFile, byte[]> Apply(string id, RecordFiles change, Func<TrackedFile, byte[]> start, ChangeContext ctx, Func<Conflict, bool> resolve)
        {
            string title = Title(id, ctx);
            TrackedFile file = Files(id, ctx)[0];
            Dictionary<TrackedFile, byte[]> result = new Dictionary<TrackedFile, byte[]>();
            JsonObject changes = change.Json["changes"] as JsonObject ?? new JsonObject();
            if (changes[RawChanges.Key] is JsonObject raw)
            {
                RawChanges.Apply(raw, change, start, title, c => resolve(c with { Record = title }), result);
                return result;
            }
            byte[] code = start(file) ?? throw new InvalidOperationException($"{title} isn't in this ROM.");
            List<byte> grown = code.ToList();
            foreach (JsonObject range in ((JsonArray)changes["ranges"]).Cast<JsonObject>())
            {
                int at = (int)ChangesJson.ParseAddress(range["at"]);
                byte[] was = ChangesJson.FromHex(range[ObjectDiff.Was].GetValue<string>());
                byte[] now = ChangesJson.FromHex(range[ObjectDiff.Now].GetValue<string>());
                byte[] here = Slice(grown.ToArray(), at, Math.Max(was.Length, now.Length));
                bool matches = here.AsSpan(0, Math.Min(here.Length, was.Length)).SequenceEqual(was) && here.Length >= was.Length;
                bool done = here.Length >= now.Length && here.AsSpan(0, now.Length).SequenceEqual(now);
                if (done) continue;
                if (!matches && !resolve(new Conflict(range["at"].GetValue<string>(), range[ObjectDiff.Was].DeepClone(), range[ObjectDiff.Now].DeepClone(), ChangesJson.Hex(here)) { Record = title }))
                    continue;
                while (grown.Count < at + now.Length) grown.Add(0);
                for (int i = 0; i < now.Length; i++) grown[at + i] = now[i];
                if (now.Length < was.Length && at + was.Length >= code.Length) grown.RemoveRange(at + now.Length, grown.Count - (at + now.Length));
            }
            byte[] built = grown.ToArray();
            if (!built.AsSpan().SequenceEqual(code)) result[file] = built;
            return result;
        }

        /// <summary>Differing spans; a length change ends with a span reaching the longer file's end.</summary>
        private static List<(int, int)> Ranges(byte[] a, byte[] b)
        {
            List<(int, int)> ranges = new List<(int, int)>();
            int shared = Math.Min(a.Length, b.Length);
            int start = -1, last = -1;
            for (int i = 0; i < shared; i++)
            {
                if (a[i] == b[i]) continue;
                if (start >= 0 && i - last - 1 >= Join)
                {
                    ranges.Add((start, last - start + 1));
                    start = -1;
                }
                if (start < 0) start = i;
                last = i;
            }
            if (start >= 0) ranges.Add((start, last - start + 1));
            if (a.Length != b.Length) ranges.Add((shared, Math.Max(a.Length, b.Length) - shared));
            return ranges;
        }

        private static byte[] Slice(byte[] data, int at, int length)
        {
            if (at >= data.Length) return Array.Empty<byte>();
            return data.AsSpan(at, Math.Min(length, data.Length - at)).ToArray();
        }
    }
}
