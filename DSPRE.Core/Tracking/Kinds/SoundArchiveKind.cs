using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace DSPRE.Tracking.Kinds
{
    /// <summary>
    /// A sound archive (.sdat) as the sub-files that changed: each sequence, bank or wave archive is kept as its own
    /// file next to the JSON, so a new cry costs that cry's wave archive, not the whole archive.
    /// </summary>
    public sealed class SoundArchiveKind : ChangeKind
    {
        public override string Name => "sound-archive";
        public override string Folder => "sound";
        public override string Title(string id, ChangeContext ctx) => "Sound archive " + Path.GetFileName(id);
        // The archive's path in the ROM, its folders joined by "~" and its dots written "@": a record's name ends at
        // its first dot, the rest naming its sidecars.
        public override string Stem(string id, ChangeContext ctx) => id.Replace('/', '~').Replace('.', '@');

        public override bool TryParseStem(string stem, out string id)
        {
            id = stem.EndsWith("@sdat", StringComparison.OrdinalIgnoreCase) ? stem.Replace('@', '.').Replace('~', '/') : null;
            return id != null;
        }

        public override bool Owns(TrackedFile file, out string id)
        {
            id = file.TreePath is string path && path.EndsWith(".sdat", StringComparison.OrdinalIgnoreCase) ? path : null;
            return id != null;
        }

        public override IReadOnlyList<TrackedFile> Files(string id, ChangeContext ctx) => new[] { TrackedFile.Tree(id) };

        public override RecordFiles Export(string id, ChangeContext ctx)
        {
            TrackedFile file = Files(id, ctx)[0];
            byte[] before = ctx.Base.Read(file), now = ctx.Project.Read(file);
            if (Same(before, now)) return null;
            JsonObject changes = new JsonObject();
            RecordFiles result = new RecordFiles();
            List<(int Id, byte[] Was, byte[] Now)> parts = Parts(before, now);
            if (parts != null && Same(Replay(before, parts.Select(p => (p.Id, p.Now))), now))
            {
                JsonObject files = new JsonObject();
                foreach ((int part, byte[] was, byte[] bytes) in parts)
                {
                    string name = part.ToString("D4", CultureInfo.InvariantCulture) + ".bin";
                    files[part.ToString(CultureInfo.InvariantCulture)] = new JsonObject
                    {
                        [ObjectDiff.Was] = ChangesJson.Sha1(was), [ObjectDiff.Now] = ChangesJson.Sha1(bytes), ["file"] = name,
                    };
                    result.Sidecars["." + name] = bytes;
                }
                changes["parts"] = files;
            }
            else if (!RawChanges.Export(changes, result, new[] { file }, ctx.Base.Read, ctx.Project.Read, _ => ".sdat")) return null;
            JsonObject json = Envelope(id, Title(id, ctx), new[] { file }, "edit");
            json["changes"] = changes;
            return new RecordFiles { Json = json }.WithSidecars(result);
        }

        public override Dictionary<TrackedFile, byte[]> Apply(string id, RecordFiles change, Func<TrackedFile, byte[]> start, ChangeContext ctx, Func<Conflict, bool> resolve)
        {
            TrackedFile file = Files(id, ctx)[0];
            string title = Title(id, ctx);
            Dictionary<TrackedFile, byte[]> result = new Dictionary<TrackedFile, byte[]>();
            if (change.Json["changes"]?[RawChanges.Key] is JsonObject raw)
            {
                RawChanges.Apply(raw, change, start, title, c => resolve(c with { Record = title }), result);
                return result;
            }
            if (change.Json["changes"]?["parts"] is not JsonObject parts) return result;
            byte[] archive = start(file) ?? throw new InvalidOperationException($"{id} isn't in this ROM.");
            List<(int, byte[])> writes = new List<(int, byte[])>();
            foreach (KeyValuePair<string, JsonNode> entry in parts.OrderBy(p => int.Parse(p.Key, CultureInfo.InvariantCulture)))
            {
                int part = int.Parse(entry.Key, CultureInfo.InvariantCulture);
                string name = entry.Value["file"]?.GetValue<string>();
                if (name == null || !change.Sidecars.TryGetValue("." + name, out byte[] bytes)) throw new InvalidDataException($"Sound part {part} is missing from the changes folder.");
                byte[] current = Part(archive, part);
                string was = entry.Value[ObjectDiff.Was]?.GetValue<string>(), now = entry.Value[ObjectDiff.Now]?.GetValue<string>();
                string here = current == null ? null : ChangesJson.Sha1(current);
                if (here == now) continue;
                if (here != was && !resolve(new Conflict("part " + entry.Key, was, now, here) { Record = title })) continue;
                writes.Add((part, bytes));
            }
            byte[] built = Replay(archive, writes);
            if (built == null) throw new InvalidDataException($"{title} couldn't take the changed parts.");
            if (!Same(built, archive)) result[file] = built;
            return result;
        }

        /// <summary>Sub-files that differ, or null when the archives aren't laid out alike.</summary>
        private static List<(int, byte[], byte[])> Parts(byte[] before, byte[] now)
        {
            if (before == null || now == null || Count(before) is not int n || Count(now) != n) return null;
            List<(int, byte[], byte[])> parts = new List<(int, byte[], byte[])>();
            for (int i = 0; i < n; i++)
            {
                byte[] a = Part(before, i), b = Part(now, i);
                if (a == null || b == null) return null;
                if (!Same(a, b)) parts.Add((i, a, b));
            }
            return parts;
        }

        private static int Fat(byte[] d) => d.Length >= 0x2C ? BitConverter.ToInt32(d, 0x20) : -1;

        private static int? Count(byte[] d)
        {
            int fat = Fat(d);
            return fat > 0 && fat + 12 <= d.Length ? BitConverter.ToInt32(d, fat + 8) : null;
        }

        private static byte[] Part(byte[] d, int id)
        {
            int fat = Fat(d);
            if (fat <= 0 || Count(d) is not int n || id < 0 || id >= n || fat + 12 + id * 16 + 8 > d.Length) return null;
            int off = BitConverter.ToInt32(d, fat + 12 + id * 16), size = BitConverter.ToInt32(d, fat + 12 + id * 16 + 4);
            return off < 0 || size < 0 || off + size > d.Length ? null : d.AsSpan(off, size).ToArray();
        }

        // The audio editor's own rule: a part no bigger goes back in place, a bigger one on the end, so no other offset moves.
        private static byte[] Replay(byte[] d, IEnumerable<(int Id, byte[] Bytes)> parts)
        {
            if (d == null) return null;
            byte[] o = d;
            foreach ((int id, byte[] bytes) in parts)
            {
                int fat = Fat(o), entry = fat + 12 + id * 16;
                if (fat <= 0 || entry + 8 > o.Length) return null;
                int oldOff = BitConverter.ToInt32(o, entry), oldSize = BitConverter.ToInt32(o, entry + 4);
                int newOff;
                byte[] next;
                if (bytes.Length <= oldSize)
                {
                    next = (byte[])o.Clone();
                    newOff = oldOff;
                    bytes.CopyTo(next, newOff);
                    Array.Clear(next, newOff + bytes.Length, oldSize - bytes.Length);
                }
                else
                {
                    newOff = o.Length + ((4 - (o.Length & 3)) & 3);
                    next = new byte[newOff + bytes.Length];
                    o.CopyTo(next, 0);
                    bytes.CopyTo(next, newOff);
                }
                BitConverter.GetBytes(newOff).CopyTo(next, entry);
                BitConverter.GetBytes(bytes.Length).CopyTo(next, entry + 4);
                BitConverter.GetBytes(next.Length).CopyTo(next, 8);
                int fileBlock = BitConverter.ToInt32(next, 0x28);
                if (fileBlock > 0 && fileBlock + 8 <= next.Length) BitConverter.GetBytes(next.Length - fileBlock).CopyTo(next, fileBlock + 4);
                o = next;
            }
            return o;
        }
    }
}
