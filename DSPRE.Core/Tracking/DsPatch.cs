using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using static DSPRE.RomInfo;

namespace DSPRE.Tracking
{
    /// <summary>
    /// A .dspatch: one compressed file with every changed ROM file, its original and new SHA-1, and its changed bytes; text as line edits.
    /// Files the target also changed are merged field by field against the target's copy of the original ROM.
    /// </summary>
    public static class DsPatch
    {
        public const string Extension = ".dspatch";
        public const int CurrentFormat = 2;
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("DSPT");
        private const byte HasBase = 1, Removed = 2, Ranges = 4, Lines = 8, Hunks = 16;
        private const byte LineEdit = 0, LineAdd = 1, LineRemove = 2;
        // Unchanged runs shorter than this stay inside a range, since each range costs its own header.
        private const int RangeGap = 8;

        public sealed record Info(string Game, string Code, int Revision, string ProjectName, string ProjectId, string Release, string BaseSha1, string BuiltSha1);

        private sealed class Entry
        {
            public TrackedFile File;
            public byte[] BaseSha1, Bytes;
        }

        /// <summary>What applying a patch would do, worked out before anything is written.</summary>
        public sealed class Plan
        {
            public Info Info { get; init; }
            public Dictionary<TrackedFile, byte[]> Writes { get; } = new Dictionary<TrackedFile, byte[]>();
            public List<PlannedRecord> Records { get; } = new List<PlannedRecord>();
            public List<string> Clean => Titles(Outcome.Clean);
            public List<string> AlreadyThere => Titles(Outcome.AlreadyThere);
            public List<string> Clashing => Titles(Outcome.Clashing);
            public List<string> Problems => Records.Where(r => r.Outcome == Outcome.Problem).Select(r => r.Title + ": " + r.Problem).ToList();

            private List<string> Titles(Outcome outcome) => Records.Where(r => r.Outcome == outcome).Select(r => r.Title).ToList();

            internal PlannedRecord Add(string title, Outcome outcome, string problem = null)
            {
                PlannedRecord r = new PlannedRecord { Title = title, Outcome = outcome, Problem = problem };
                Records.Add(r);
                return r;
            }
        }

        public enum Outcome { Clean, AlreadyThere, Clashing, Problem }

        /// <summary>One record of a patch and how it lands on this project.</summary>
        public sealed class PlannedRecord
        {
            public string Title { get; init; }
            public Outcome Outcome { get; set; }
            public string Problem { get; set; }
            /// <summary>The patch's changes against the original game; null without the original ROM.</summary>
            public JsonNode Changes { get; set; }
            /// <summary>Files the patch writes, shown when <see cref="Changes"/> is null.</summary>
            public List<string> Files { get; } = new List<string>();
            public List<Conflict> Conflicts { get; } = new List<Conflict>();
        }

        /// <summary>Packs every file the tracked project changed into <paramref name="path"/>.</summary>
        public static void Export(ChangeTracker tracker, string path, string release, string builtSha1 = null)
        {
            ChangeContext ctx = tracker.Context();
            RecordFolder records = new RecordFolder(tracker.Folder, tracker.Kinds);
            SortedSet<TrackedFile> files = new SortedSet<TrackedFile>();
            List<(TrackedFile Member, JsonObject Changes)> texts = new List<(TrackedFile, JsonObject)>();
            foreach ((ChangeKind kind, string id) in records.Records())
            {
                // Text lives in its expanded JSON until Save ROM, and the archives are encrypted, so it goes in as line edits.
                if (kind is Kinds.TextKind)
                {
                    if (kind.Export(id, ctx)?.Json?["changes"] is JsonObject changes) texts.Add((kind.Files(id, ctx)[0], changes));
                    continue;
                }                foreach (TrackedFile f in kind.Files(id, ctx))
                {
                    if (f.IsSource) continue;
                    if (f.TryRow(out string table, out _)) { if (ctx.Project.Table(table) is CodeTable t) files.Add(TrackedFile.Tree(t.TreePath)); }
                    else files.Add(f);
                }
            }

            using MemoryStream body = new MemoryStream();
            using (BinaryWriter w = new BinaryWriter(body, Encoding.UTF8, leaveOpen: true))
            {
                w.Write(tracker.Manifest.Game ?? "");
                w.Write(tracker.Manifest.Code ?? "");
                w.Write7BitEncodedInt(tracker.Manifest.Revision);
                w.Write(tracker.Manifest.ProjectName ?? "");
                w.Write(tracker.Manifest.ProjectId ?? "");
                w.Write(release ?? "");
                WriteSha(w, tracker.Manifest.BaseSha1);
                WriteSha(w, builtSha1);
                List<(TrackedFile File, byte[] Was, byte[] Now)> changed = files
                    .Select(f => (f, ctx.Base.Read(f), ctx.Project.Read(f)))
                    .Where(c => !Same(c.Item2, c.Item3))
                    .ToList();
                w.Write7BitEncodedInt(changed.Count + texts.Count);
                foreach ((TrackedFile member, JsonObject changes) in texts)
                {
                    w.Write(member.Key);
                    if (changes["hunks"] is JsonArray hunks)
                    {
                        w.Write(Hunks);
                        w.Write7BitEncodedInt(hunks.Count);
                        foreach (JsonObject hunk in hunks.Cast<JsonObject>())
                        {
                            w.Write7BitEncodedInt(hunk["at"].GetValue<int>());
                            foreach (string side in new[] { ObjectDiff.Remove, ObjectDiff.Add })
                            {
                                JsonArray part = (JsonArray)hunk[side];
                                w.Write7BitEncodedInt(part.Count);
                                foreach (JsonNode line in part) w.Write(line.GetValue<string>());
                            }
                        }
                        continue;
                    }
                    JsonObject lines = changes["lines"] as JsonObject ?? new JsonObject();
                    w.Write(Lines);
                    w.Write7BitEncodedInt(lines.Count);
                    foreach (KeyValuePair<string, JsonNode> line in lines)
                    {
                        JsonObject op = (JsonObject)line.Value;
                        w.Write7BitEncodedInt(int.Parse(line.Key, CultureInfo.InvariantCulture));
                        if (op.ContainsKey(ObjectDiff.Add)) { w.Write(LineAdd); WriteText(w, op[ObjectDiff.Add]); }
                        else if (op.ContainsKey(ObjectDiff.Remove)) { w.Write(LineRemove); WriteText(w, op[ObjectDiff.Remove]); }
                        else { w.Write(LineEdit); WriteText(w, op[ObjectDiff.Was]); WriteText(w, op[ObjectDiff.Now]); }
                    }
                }
                foreach ((TrackedFile file, byte[] was, byte[] now) in changed)
                {
                    List<(int At, int Length)> ranges = was != null && now != null && was.Length == now.Length ? Differences(was, now) : null;
                    bool asRanges = ranges != null && ranges.Sum(r => r.Length + 8) < now.Length;
                    w.Write(file.Key);
                    w.Write((byte)((was != null ? HasBase : 0) | (now == null ? Removed : 0) | (asRanges ? Ranges : 0)));
                    if (was != null) w.Write(SHA1.HashData(was));
                    if (now == null) continue;
                    w.Write(SHA1.HashData(now));
                    if (asRanges)
                    {
                        w.Write7BitEncodedInt(ranges.Count);
                        foreach ((int at, int length) in ranges)
                        {
                            w.Write7BitEncodedInt(at);
                            w.Write7BitEncodedInt(length);
                            w.Write(now, at, length);
                        }
                    }
                    else
                    {
                        w.Write7BitEncodedInt(now.Length);
                        w.Write(now);
                    }
                }
            }

            using MemoryStream output = new MemoryStream();
            output.Write(Magic);
            output.WriteByte(CurrentFormat);
            using (BrotliStream brotli = new BrotliStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
                body.WriteTo(brotli);
            PlainFiles.WriteBytes(path, output.ToArray());
        }

        public static Info ReadInfo(string path) => Read(path, out _);

        private static Info Read(string path, out List<(TrackedFile File, byte[] BaseSha1, byte[] NewSha1, Func<byte[], byte[]> Build)> entries) =>
            Read(path, out entries, out _);

        private static Info Read(string path, out List<(TrackedFile File, byte[] BaseSha1, byte[] NewSha1, Func<byte[], byte[]> Build)> entries,
            out List<(TrackedFile Member, JsonObject Changes)> texts)
        {
            texts = new List<(TrackedFile, JsonObject)>();
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < Magic.Length + 1 || !bytes.AsSpan(0, Magic.Length).SequenceEqual(Magic)) throw new InvalidDataException("That file isn't a DSPRE patch.");
            if (bytes[Magic.Length] > CurrentFormat) throw new InvalidDataException("That patch was made by a newer DSPRE. Update DSPRE to apply it.");
            entries = new List<(TrackedFile, byte[], byte[], Func<byte[], byte[]>)>();
            try
            {
                using MemoryStream body = new MemoryStream();
                using (BrotliStream brotli = new BrotliStream(new MemoryStream(bytes, Magic.Length + 1, bytes.Length - Magic.Length - 1), CompressionMode.Decompress))
                    brotli.CopyTo(body);
                body.Position = 0;
                using BinaryReader r = new BinaryReader(body, Encoding.UTF8);
                Info info = new Info(r.ReadString(), r.ReadString(), r.Read7BitEncodedInt(), Empty(r.ReadString()), Empty(r.ReadString()), Empty(r.ReadString()),
                    ReadSha(r), ReadSha(r));
                int count = r.Read7BitEncodedInt();
                for (int i = 0; i < count; i++)
                {
                    TrackedFile file = new TrackedFile(r.ReadString());
                    byte flags = r.ReadByte();
                    if ((flags & Hunks) != 0)
                    {
                        JsonArray hunks = new JsonArray();
                        int n = r.Read7BitEncodedInt();
                        for (int k = 0; k < n; k++)
                        {
                            JsonObject hunk = new JsonObject { ["at"] = r.Read7BitEncodedInt() };
                            foreach (string side in new[] { ObjectDiff.Remove, ObjectDiff.Add })
                            {
                                JsonArray part = new JsonArray();
                                int lines = r.Read7BitEncodedInt();
                                for (int l = 0; l < lines; l++) part.Add(r.ReadString());
                                hunk[side] = part;
                            }
                            hunks.Add(hunk);
                        }
                        texts.Add((file, new JsonObject { ["hunks"] = hunks }));
                        continue;
                    }
                    if ((flags & Lines) != 0)
                    {
                        JsonObject lines = new JsonObject();
                        int n = r.Read7BitEncodedInt();
                        for (int k = 0; k < n; k++)
                        {
                            string at = r.Read7BitEncodedInt().ToString(CultureInfo.InvariantCulture);
                            lines[at] = r.ReadByte() switch
                            {
                                LineAdd => new JsonObject { [ObjectDiff.Add] = ReadText(r) },
                                LineRemove => new JsonObject { [ObjectDiff.Remove] = ReadText(r) },
                                _ => ObjectDiff.Leaf(ReadText(r), ReadText(r)),
                            };
                        }
                        texts.Add((file, new JsonObject { ["lines"] = lines }));
                        continue;
                    }
                    byte[] baseSha = (flags & HasBase) != 0 ? r.ReadBytes(20) : null;
                    if ((flags & Removed) != 0) { entries.Add((file, baseSha, null, _ => null)); continue; }
                    byte[] newSha = r.ReadBytes(20);
                    if ((flags & Ranges) != 0)
                    {
                        int n = r.Read7BitEncodedInt();
                        List<(int At, byte[] Bytes)> ranges = new List<(int, byte[])>();
                        for (int k = 0; k < n; k++)
                        {
                            int at = r.Read7BitEncodedInt();
                            ranges.Add((at, r.ReadBytes(r.Read7BitEncodedInt())));
                        }
                        entries.Add((file, baseSha, newSha, start =>
                        {
                            if (start == null) return null;
                            byte[] result = (byte[])start.Clone();
                            foreach ((int at, byte[] part) in ranges)
                            {
                                if (at + part.Length > result.Length) return null;
                                part.CopyTo(result, at);
                            }
                            return result;
                        }));
                    }
                    else
                    {
                        byte[] whole = r.ReadBytes(r.Read7BitEncodedInt());
                        entries.Add((file, baseSha, newSha, _ => whole));
                    }
                }
                return info;
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is IOException)
            {
                throw new InvalidDataException("That patch is damaged.");
            }
        }

        /// <summary>
        /// Works out every write the patch makes to the open project. <paramref name="resolve"/> answers each field the
        /// project has changed some other way: true takes the patch's value.
        /// </summary>
        public static Plan PlanApply(string path, Func<Conflict, bool> resolve)
        {
            Info info = Read(path, out List<(TrackedFile File, byte[] BaseSha1, byte[] NewSha1, Func<byte[], byte[]> Build)> raw,
                out List<(TrackedFile Member, JsonObject Changes)> texts);
            if (!string.Equals(info.Code, RomID, StringComparison.Ordinal))
                throw new InvalidDataException($"That patch is for {info.Game} ({info.Code}), but this project is {RomID}.");
            Plan plan = new Plan { Info = info };
            RomTree project = RomTree.Project();
            RomTree original = OriginalFor(info);
            ChangeKinds kinds = ChangeKinds.ForOpenGame();
            TrackingNames names = new TrackingNames();

            Dictionary<TrackedFile, byte[]> patched = new Dictionary<TrackedFile, byte[]>();
            List<Entry> entries = new List<Entry>();
            foreach ((TrackedFile file, byte[] baseSha, byte[] newSha, Func<byte[], byte[]> build) in raw)
            {
                byte[] now = null;
                if (newSha != null)
                {
                    // Ranges are laid over the original bytes; the project's copy also works while it still matches them.
                    byte[] from = original?.Read(file);
                    if (from == null || (baseSha != null && !Sha(from, baseSha))) from = project.Read(file);
                    now = build(from);
                    if (now == null || !Sha(now, newSha)) { plan.Add(file.Key, Outcome.Problem, "needs the original ROM to rebuild " + ShownPath(project, file)); continue; }
                }
                patched[file] = now;
                entries.Add(new Entry { File = file, BaseSha1 = baseSha, Bytes = now });
            }

            RomTree patchedTree = original?.WithFiles(patched);
            foreach (IGrouping<(ChangeKind Kind, string Id), Entry> record in entries
                .GroupBy(e => kinds.TryOwner(e.File, out ChangeKind k, out string id) ? (k, id) : ((ChangeKind)null, e.File.Key))
                .OrderBy(g => g.Key.Item2, StringComparer.Ordinal))
            {
                ChangeKind kind = record.Key.Kind;
                string id = record.Key.Id;
                ChangeContext projectCtx = new ChangeContext(original ?? project, project, names);
                PlannedRecord planned = plan.Add(kind?.Title(id, projectCtx) ?? id, Outcome.Clean);
                planned.Files.AddRange(record.Select(e => e.File.Key));
                try
                {
                    bool byField = kind != null && original != null && record.All(e => e.BaseSha1 == null || Sha(original.Read(e.File), e.BaseSha1));
                    ChangeContext patchCtx = new ChangeContext(original, patchedTree, names);
                    RecordFiles change = byField ? kind.Export(id, patchCtx) : null;
                    planned.Changes = change?.Json["changes"]?.DeepClone();
                    // Code skips the bytes of tables inside it, so their rows come from the table records.
                    List<(ChangeKind Kind, string Id, RecordFiles Change)> tables = new List<(ChangeKind, string, RecordFiles)>();
                    if (byField && kind is Kinds.CodeKind)
                        foreach (CodeTable t in project.Tables.Where(t => record.Any(e => e.File.Key == t.TreePath)))
                            if (kinds.TryOwner(TrackedFile.Row(t.Name, 0), out ChangeKind tableKind, out string tableId) && tableKind.Export(tableId, patchCtx) is RecordFiles rows)
                                tables.Add((tableKind, tableId, rows));
                    if (tables.Count > 0)
                    {
                        JsonObject parts = new JsonObject();
                        if (planned.Changes != null) parts["code"] = planned.Changes;
                        foreach ((ChangeKind tableKind, string tableId, RecordFiles rows) in tables) parts[tableKind.Title(tableId, projectCtx)] = rows.Json["changes"]?.DeepClone();
                        planned.Changes = parts;
                    }
                    List<Entry> pending = record.Where(e => !Same(project.Read(e.File), e.Bytes)).ToList();
                    if (pending.Count == 0) { planned.Outcome = Outcome.AlreadyThere; continue; }
                    if (pending.All(e => Untouched(project.Read(e.File), e.BaseSha1)))
                    {
                        foreach (Entry e in pending) plan.Writes[e.File] = e.Bytes;
                        continue;
                    }
                    if (byField)
                    {
                        // Both sides changed it, so the patch's fields are replayed onto this project's copy.
                        Dictionary<TrackedFile, byte[]> result = change == null ? new Dictionary<TrackedFile, byte[]>() : kind.Apply(id, change, project.Read, projectCtx, c => Clash(planned, c, resolve));
                        foreach ((ChangeKind tableKind, string tableId, RecordFiles rows) in tables)
                        {
                            CodeTable table = project.Table(tableId);
                            TrackedFile code = TrackedFile.Tree(table.TreePath);
                            byte[] Code() => result.TryGetValue(code, out byte[] b) ? b : project.Read(code);
                            // Rows read from and land in the code file, so the code's own edits and the table's build on the same bytes.
                            byte[] Row(TrackedFile f) => f.TryRow(out _, out int i) ? Code().AsSpan((int)table.RowOffset(i), table.RowSize).ToArray() : project.Read(f);
                            string tableTitle = tableKind.Title(tableId, projectCtx).Replace(". ", " ").Replace('.', ' ');
                            foreach (KeyValuePair<TrackedFile, byte[]> w in tableKind.Apply(tableId, rows, Row, projectCtx,
                                c => Clash(planned, c with { Path = c.Path.Length == 0 ? tableTitle : tableTitle + "." + c.Path }, resolve)))
                            {
                                if (!w.Key.TryRow(out _, out int i)) continue;
                                byte[] bytes = (byte[])Code().Clone();
                                w.Value.CopyTo(bytes, table.RowOffset(i));
                                result[code] = bytes;
                            }
                        }
                        List<KeyValuePair<TrackedFile, byte[]>> writes = result.Where(w => !Same(w.Value, project.Read(w.Key))).ToList();
                        foreach (KeyValuePair<TrackedFile, byte[]> w in writes) plan.Writes[w.Key] = w.Value;
                        planned.Outcome = planned.Conflicts.Count > 0 ? Outcome.Clashing : writes.Count > 0 ? Outcome.Clean : Outcome.AlreadyThere;
                        continue;
                    }
                    // Without the original ROM there are no fields to compare, so the whole record is one choice.
                    Conflict whole = new Conflict("", JsonValue.Create("original"), JsonValue.Create("incoming"), JsonValue.Create("local"));
                    if (Clash(planned, whole, resolve)) foreach (Entry e in pending) plan.Writes[e.File] = e.Bytes;
                    planned.Outcome = Outcome.Clashing;
                }
                catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is InvalidOperationException || ex is System.Text.Json.JsonException)
                {
                    planned.Outcome = Outcome.Problem;
                    planned.Problem = ex.Message;
                }
            }

            foreach ((TrackedFile member, JsonObject changes) in texts)
            {
                if (!kinds.TryOwner(member, out ChangeKind kind, out string id)) { plan.Add(member.Key, Outcome.Problem, "the local " + ShownPath(project, member) + " isn't a text archive"); continue; }
                ChangeContext projectCtx = new ChangeContext(original ?? project, project, names);
                PlannedRecord planned = plan.Add(kind.Title(id, projectCtx), Outcome.Clean);
                planned.Changes = changes.DeepClone();
                try
                {
                    RecordFiles change = new RecordFiles { Json = new JsonObject { ["changes"] = changes.DeepClone() } };
                    Dictionary<TrackedFile, byte[]> result = kind.Apply(id, change, project.Read, projectCtx, c => Clash(planned, c, resolve));
                    List<KeyValuePair<TrackedFile, byte[]>> writes = result.Where(w => !Same(w.Value, project.Read(w.Key))).ToList();
                    foreach (KeyValuePair<TrackedFile, byte[]> w in writes) plan.Writes[w.Key] = w.Value;
                    planned.Outcome = planned.Conflicts.Count > 0 ? Outcome.Clashing : writes.Count > 0 ? Outcome.Clean : Outcome.AlreadyThere;
                }
                catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is InvalidOperationException)
                {
                    planned.Outcome = Outcome.Problem;
                    planned.Problem = ex.Message;
                }
            }
            return plan;
        }

        private static bool Clash(PlannedRecord planned, Conflict c, Func<Conflict, bool> resolve)
        {
            Conflict named = c with { Record = planned.Title };
            planned.Conflicts.Add(named);
            return resolve(named);
        }

        /// <summary>Writes a plan into the open project.</summary>
        public static bool Apply(Plan plan, out string error)
        {
            ChangeTracker tracker = ChangeTracker.Current;
            if (tracker != null) return tracker.TryWrite(plan.Writes, out error);
            return ChangeTracker.WriteProject(RomTree.Project(), DspreDir.TrimEnd('\\', '/'), plan.Writes, out error);
        }

        /// <summary>The tracker's copy of the original ROM, else a cached extraction of the ROM the patch was made from.</summary>
        private static RomTree OriginalFor(Info info)
        {
            if (ChangeTracker.Current?.BaseTree is RomTree tracked) return tracked;
            if (info.BaseSha1 == null) return null;
            foreach (bool dsRom in new[] { IsDsRomProject, !IsDsRomProject })
                if (BaseRom.IsExtracted(info.BaseSha1, dsRom)) return RomTree.Base(BaseRom.FolderFor(info.BaseSha1, dsRom));
            return null;
        }

        private static List<(int At, int Length)> Differences(byte[] was, byte[] now)
        {
            List<(int, int)> ranges = new List<(int, int)>();
            int i = 0;
            while (i < now.Length)
            {
                if (was[i] == now[i]) { i++; continue; }
                int start = i, end = i + 1, same = 0;
                for (int j = i + 1; j < now.Length && same < RangeGap; j++)
                {
                    if (was[j] == now[j]) same++;
                    else { same = 0; end = j + 1; }
                }
                ranges.Add((start, end - start));
                i = end;
            }
            return ranges;
        }

        private static bool Untouched(byte[] current, byte[] baseSha) => baseSha == null ? current == null : Sha(current, baseSha);

        /// <summary>The file as the user finds it in the project folder.</summary>
        private static string ShownPath(RomTree project, TrackedFile file)
        {
            string full = project.FullPath(file);
            if (full == null) return file.Key;
            string root = (RomInfo.DspreDir ?? project.Root).TrimEnd('\\', '/');
            return Path.GetRelativePath(Path.GetDirectoryName(root) ?? root, full).Replace('\\', '/');
        }

        private static bool Sha(byte[] bytes, byte[] sha) => bytes != null && sha != null && SHA1.HashData(bytes).AsSpan().SequenceEqual(sha);

        private static void WriteSha(BinaryWriter w, string sha)
        {
            string hex = sha?.Replace("sha1:", "");
            w.Write(hex != null && hex.Length == 40 ? Convert.FromHexString(hex) : new byte[20]);
        }

        private static string ReadSha(BinaryReader r)
        {
            byte[] sha = r.ReadBytes(20);
            return sha.All(b => b == 0) ? null : Convert.ToHexString(sha).ToLowerInvariant();
        }

        private static void WriteText(BinaryWriter w, JsonNode text)
        {
            w.Write(text != null);
            if (text != null) w.Write(text.GetValue<string>());
        }

        private static JsonNode ReadText(BinaryReader r) => r.ReadBoolean() ? JsonValue.Create(r.ReadString()) : null;

        private static string Empty(string s) => string.IsNullOrEmpty(s) ? null : s;

        private static bool Same(byte[] a, byte[] b) => a == null ? b == null : b != null && a.AsSpan().SequenceEqual(b);
    }
}
