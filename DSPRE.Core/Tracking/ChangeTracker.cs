using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using DSPRE.HgEngine;
using DSPRE.Tracking.Kinds;
using static DSPRE.RomInfo;

namespace DSPRE.Tracking
{
    /// <summary>A record whose file in the changes folder differs from what DSPRE last wrote or applied.</summary>
    public sealed record IncomingRecord(ChangeKind Kind, string Id, string Title, bool AlsoChangedHere);

    /// <summary>
    /// Keeps the changes folder equal to the project minus the base ROM. Saved project files are exported as they
    /// change. Changes folder files edited outside DSPRE show as incoming.
    /// </summary>
    public sealed class ChangeTracker
    {
        public static ChangeTracker Current { get; private set; }

        /// <summary>Raised after a scan, export or apply, on the thread that did it.</summary>
        public static event Action StateChanged;

        /// <summary>Raised with the project files an apply rewrote, after it wrote them.</summary>
        public static event Action<IReadOnlyCollection<TrackedFile>> Applied;

        /// <summary>Raised when the tracker starts and stops a scan, export or apply.</summary>
        public static event Action<bool> WorkingChanged;

        private static int _working;
        public static bool IsWorking => Volatile.Read(ref _working) > 0;

        private static IDisposable Working()
        {
            if (Interlocked.Increment(ref _working) == 1) WorkingChanged?.Invoke(true);
            return new Done();
        }

        private sealed class Done : IDisposable
        {
            private int _done;
            public void Dispose()
            {
                if (Interlocked.Exchange(ref _done, 1) == 1) return;
                if (Interlocked.Decrement(ref _working) == 0) WorkingChanged?.Invoke(false);
            }
        }

        private TrackingNames _names;

        public string Folder { get; }
        public ChangesManifest Manifest { get; }
        public IReadOnlyList<IncomingRecord> Incoming { get; private set; } = Array.Empty<IncomingRecord>();

        /// <summary>A git merge is open in the changes folder. Nothing is exported until it ends.</summary>
        public bool Merging { get; private set; }

        /// <summary>Records that have a JSON kind but were kept as whole files.</summary>
        public IReadOnlyList<string> KeptWhole { get; private set; } = Array.Empty<string>();

        private readonly RomTree _project, _base;
        private readonly ChangeKinds _kinds;
        private readonly RecordFolder _records;
        private SyncState _state;
        private readonly string _projectDir;
        private readonly object _gate = new object();
        private Timer _debounce;
        private int _applying;

        private ChangeTracker(string folder, ChangesManifest manifest, string baseRoot)
        {
            Folder = Path.GetFullPath(folder).TrimEnd('\\', '/');
            Manifest = manifest;
            _projectDir = DspreDir.TrimEnd('\\', '/');
            _project = RomTree.Project();
            _base = RomTree.Base(baseRoot);
            _kinds = ChangeKinds.ForOpenGame();
            _records = new RecordFolder(Folder, _kinds);
            _state = SyncState.Load(_projectDir);
            if (_state.Repo == null || !SamePath(_state.Repo, Folder) || _state.BaseSha1 != null && _state.BaseSha1 != manifest.BaseSha1)
                _state = new SyncState();
            _state.Repo = Folder;
            _state.BaseSha1 = manifest.BaseSha1;
            _state.ProjectId = manifest.ProjectId;
        }

        /// <summary>The extracted original ROM the changes are measured against.</summary>
        public RomTree BaseTree => _base;

        public ChangeContext Context() => new ChangeContext(_base, _project, _names ??= new TrackingNames()) { CodeWrites = CodeWritesFor };

        public ChangeKinds Kinds => _kinds;

        /// <summary>Starts tracking the open project into <paramref name="folder"/>; the base ROM must already be extracted.</summary>
        public static ChangeTracker Start(string folder, ChangesManifest manifest, string baseRoot)
        {
            Stop();
            ChangeTracker tracker = new ChangeTracker(folder, manifest, baseRoot);
            Current = tracker;
            ProjectWrites.Written += tracker.OnWritten;
            ProjectWrites.CodeWritten += tracker.OnCodeWritten;
            return tracker;
        }

        public static void Stop()
        {
            ChangeTracker tracker = Current;
            if (tracker == null) return;
            ProjectWrites.Written -= tracker.OnWritten;
            ProjectWrites.CodeWritten -= tracker.OnCodeWritten;
            tracker._debounce?.Dispose();
            Current = null;
            StateChanged?.Invoke();
        }

        private void OnWritten(string path)
        {
            if (Volatile.Read(ref _applying) > 0 || !IsTrackedPath(path)) return;
            _debounce ??= new Timer(_ => SafeRefresh(), null, Timeout.Infinite, Timeout.Infinite);
            _debounce.Change(600, Timeout.Infinite);
        }

        private void OnCodeWritten(string path, long at, int length, string by)
        {
            if (string.IsNullOrEmpty(by) || !IsTrackedPath(path)) return;
            string key = Path.GetRelativePath(_project.Root, path).Replace('\\', '/');
            lock (_gate)
            {
                if (!_state.CodeWrites.TryGetValue(key, out List<SyncState.CodeWrite> list)) _state.CodeWrites[key] = list = new List<SyncState.CodeWrite>();
                list.RemoveAll(w => w.At >= at && w.At + w.Length <= at + length);
                list.Add(new SyncState.CodeWrite { At = at, Length = length, By = by });
            }
        }

        private IReadOnlyList<SyncState.CodeWrite> CodeWritesFor(string treePath)
        {
            lock (_gate) return _state.CodeWrites.TryGetValue(treePath, out List<SyncState.CodeWrite> list) ? list.ToList() : Array.Empty<SyncState.CodeWrite>();
        }

        private bool IsTrackedPath(string path)
        {
            string full = Path.GetFullPath(path);
            if (Path.GetFileName(full).StartsWith(".dspre", StringComparison.Ordinal)) return false;
            return Inside(full, _project.Root) || Inside(full, Path.Combine(_projectDir, "unpacked")) || Inside(full, Path.Combine(_projectDir, "expanded"));
        }

        private void SafeRefresh()
        {
            try { Refresh(); }
            catch (Exception ex) { AppLogger.Error("Track changes: the changes folder couldn't be updated: " + ex); }
        }

        /// <summary>Finds incoming records, then exports every project change the changes folder doesn't have yet.</summary>
        public void Refresh()
        {
            using IDisposable busy = Working();
            // Writing records into a half-merged folder would mix them into the merge.
            Merging = GitCli.IsMerging(Folder);
            if (Merging)
            {
                StateChanged?.Invoke();
                return;
            }
            lock (_gate)
            {
                _names = null;
                ChangeContext ctx = Context();
                _project.Forget();
                HashSet<(ChangeKind Kind, string Id)> incoming = FindIncoming();
                HashSet<(ChangeKind Kind, string Id)> changedHere = ScanProject(out List<(TrackedFile File, (long, long) Stamp, string Sha1)> marks);
                changedHere.UnionWith(MoveOrphans());
                // A project built from these changes already holds them; there's nothing to apply.
                foreach ((ChangeKind kind, string id) in incoming.ToList())
                {
                    if (!SameAsProject(kind, id, ctx)) continue;
                    incoming.Remove((kind, id));
                    changedHere.Remove((kind, id));
                    string stem = ExistingStem(kind, id);
                    if (stem != null)
                        foreach (string path in RecordFilePaths(kind, stem)) _state.RepoFiles[RepoKey(path)] = ChangesJson.Sha1(File.ReadAllBytes(path));
                }

                List<IncomingRecord> waiting = incoming
                    .Select(r => new IncomingRecord(r.Kind, r.Id, r.Kind.Title(r.Id, ctx), changedHere.Contains(r) && DiffersHere(r.Kind, r.Id, ctx)))
                    .ToList();
                List<string> keptWhole = KeptWhole.ToList();
                foreach ((ChangeKind kind, string id) in changedHere)
                {
                    if (incoming.Contains((kind, id))) continue;
                    string title = kind.Title(id, ctx);
                    keptWhole.Remove(title);
                    if (Export(kind, id, ctx)) keptWhole.Add(title);
                }
                HashSet<string> held = new HashSet<string>(waiting.Where(w => w.AlsoChangedHere)
                    .SelectMany(w => w.Kind.Files(w.Id, ctx)).Select(f => f.Key), StringComparer.Ordinal);
                foreach ((TrackedFile file, (long size, long ticks), string sha) in marks)
                    if (!held.Contains(file.Key)) _state.Files[file.Key] = new SyncState.FileMark { Size = size, Ticks = ticks, Sha1 = sha };
                Incoming = waiting.OrderBy(w => w.Title, StringComparer.Ordinal).ToList();
                KeptWhole = keptWhole.Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList();
                _state.Save(_projectDir);
            }
            StateChanged?.Invoke();
        }

        // A first scan can't tell an untouched source file from an edited one, and a project built from base plus
        // these changes already holds them; only a project record that holds something else is a clash.
        private bool DiffersHere(ChangeKind kind, string id, ChangeContext ctx)
        {
            RecordFiles mine;
            try { mine = kind.Export(id, ctx); }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is InvalidOperationException) { return true; }
            bool firstScan = kind.Files(id, ctx).All(f => !_state.Files.ContainsKey(f.Key));
            if (mine == null && firstScan) return false;
            RecordFiles theirs;
            try { theirs = ReadRecord(kind, id); }
            catch (Exception ex) when (ex is IOException || ex is System.Text.Json.JsonException) { return true; }
            if (mine == null || theirs == null) return mine != null || theirs != null;
            return !JsonNode.DeepEquals(WithoutFileNames(mine.Json["changes"]), WithoutFileNames(theirs.Json["changes"]));
        }

        private bool SameAsProject(ChangeKind kind, string id, ChangeContext ctx)
        {
            try
            {
                RecordFiles mine = kind.Export(id, ctx), theirs = ReadRecord(kind, id);
                if (mine == null || theirs == null) return mine == null && theirs == null;
                return JsonNode.DeepEquals(WithoutFileNames(mine.Json["changes"]), WithoutFileNames(theirs.Json["changes"]));
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is InvalidOperationException || ex is System.Text.Json.JsonException)
            {
                return false;
            }
        }

        private static JsonNode WithoutFileNames(JsonNode node)
        {
            JsonNode copy = node?.DeepClone();
            foreach (JsonNode d in Descendants(copy).ToList())
                if (d is JsonObject o && o.ContainsKey("file")) o.Remove("file");
            return copy;
        }

        /// <summary>
        /// Records written by a kind this DSPRE no longer has, such as whole-file assets that now have fields: their
        /// files are removed and the records they name are exported again under their current kind.
        /// </summary>
        private HashSet<(ChangeKind, string)> MoveOrphans()
        {
            // A new build rewrites the records it wrote once, so a changed record format reaches existing folders.
            string writer = typeof(ChangeTracker).Assembly.ManifestModule.ModuleVersionId.ToString();
            bool rewrite = _state.Writer != writer;
            _state.Writer = writer;
            HashSet<(ChangeKind, string)> records = new HashSet<(ChangeKind, string)>();
            foreach (string key in _records.Files().Where(k => k.EndsWith(".json", StringComparison.Ordinal)).ToList())
            {
                bool recognised = _kinds.TryRecordAt(key, out ChangeKind recordKind, out string recordId);
                string path = Path.Combine(Folder, key);
                byte[] bytes = File.ReadAllBytes(path);
                // Only records this project wrote: one pulled from someone else holds changes this project may not have.
                if (!_state.RepoFiles.TryGetValue(key, out string written) || written != ChangesJson.Sha1(bytes)) continue;
                if (JsonNode.Parse(bytes) is not JsonObject json || json["members"] is not JsonArray members) continue;
                List<(ChangeKind Kind, string Id)> owners = new List<(ChangeKind, string)>();
                foreach (JsonNode m in members)
                    if (m?.GetValue<string>() is string member && _kinds.TryOwner(new TrackedFile(member), out ChangeKind kind, out string id))
                        owners.Add((kind, id));
                if (rewrite && recognised) owners.Add((recordKind, recordId));
                // A record still read by the kind that owns its files stays; one whose files have a new owner moves.
                if (owners.Count == 0 || !rewrite && recognised && owners.All(o => o.Kind == recordKind)) continue;
                records.UnionWith(owners);
                string stem = Path.GetFileNameWithoutExtension(path), dir = Path.GetDirectoryName(path);
                foreach (string file in Directory.EnumerateFiles(dir).Where(p => Path.GetFileName(p) is string n && (n == stem + ".json" || n.StartsWith(stem + ".", StringComparison.Ordinal))).ToList())
                {
                    File.Delete(file);
                    _state.RepoFiles.Remove(RepoKey(file));
                }
                if (!Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir);
            }
            return records;
        }

        private HashSet<(ChangeKind, string)> ScanProject(out List<(TrackedFile, (long, long), string)> marks)
        {
            marks = new List<(TrackedFile, (long, long), string)>();
            HashSet<(ChangeKind, string)> records = new HashSet<(ChangeKind, string)>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (TrackedFile file in ProjectFiles())
            {
                seen.Add(file.Key);
                (long size, long ticks) stamp = _project.Stamp(file);
                if (_state.Files.TryGetValue(file.Key, out SyncState.FileMark mark) && mark.Size == stamp.size && mark.Ticks == stamp.ticks) continue;
                byte[] now = _project.Read(file);
                string sha = now == null ? null : ChangesJson.Sha1(now);
                marks.Add((file, stamp, sha));
                // A table seen for the first time takes its bytes out of the code file's record, so that record is redone.
                if (mark == null && file.TryRow(out string tableName, out _) && _project.Table(tableName) is CodeTable table
                    && _kinds.TryOwner(TrackedFile.Tree(table.TreePath), out ChangeKind codeKind, out string codeId))
                    records.Add((codeKind, codeId));
                if (mark != null && mark.Sha1 == sha) continue;
                if (mark == null && !file.IsSource && Same(now, _base.Read(file))) continue;
                if (_kinds.TryOwner(file, out ChangeKind kind, out string id)) records.Add((kind, id));
            }
            foreach (string key in _state.Files.Keys.ToList())
            {
                if (seen.Contains(key)) continue;
                TrackedFile gone = new TrackedFile(key);
                _state.Files.Remove(key);
                if (_kinds.TryOwner(gone, out ChangeKind kind, out string id)) records.Add((kind, id));
            }
            return records;
        }

        private IEnumerable<TrackedFile> ProjectFiles()
        {
            foreach (TrackedFile f in _project.TreeFiles()) yield return f;
            foreach (TrackedFile f in _project.Rows()) yield return f;
            foreach (DirNames dir in _project.Archives.Keys)
                foreach (TrackedFile f in _project.Members(dir)) yield return f;
            foreach (string sub in new[] { "expanded/textArchives", "expanded/scripts" })
            {
                string dir = Path.Combine(_projectDir, sub);
                if (!Directory.Exists(dir)) continue;
                foreach (string file in Directory.EnumerateFiles(dir))
                {
                    string ext = Path.GetExtension(file);
                    if (ext != ".json" && ext != ".rotom") continue;
                    yield return TrackedFile.Source(sub + "/" + Path.GetFileName(file));
                }
            }
        }

        /// <summary>Writes or removes the record's files in the changes folder; true when it had to be kept as whole files.</summary>
        private bool Export(ChangeKind kind, string id, ChangeContext ctx)
        {
            RecordFiles files;
            try { files = kind.Export(id, ctx); }
            catch (Exception ex)
            {
                AppLogger.Error($"Track changes: {kind.Title(id, ctx)} couldn't be written to the changes folder: {ex.Message}");
                return false;
            }
            string stem = ExistingStem(kind, id) ?? kind.Stem(id, ctx);
            RemoveRecordFiles(kind, stem);
            if (files == null) return false;

            string jsonPath = Path.Combine(Folder, kind.Folder, stem + ".json");
            string dir = Path.GetDirectoryName(jsonPath);
            Directory.CreateDirectory(dir);
            string baseName = Path.GetFileName(stem);
            foreach (JsonNode node in Descendants(files.Json))
                if (node is JsonObject o && o["file"] is JsonValue v && v.TryGetValue(out string suffix) && files.Sidecars.ContainsKey(suffix))
                    o["file"] = baseName + suffix;
            WriteRepoFile(jsonPath, ChangesJson.WriteBytes(files.Json));
            foreach (KeyValuePair<string, byte[]> sidecar in files.Sidecars)
                WriteRepoFile(Path.Combine(dir, baseName + sidecar.Key), sidecar.Value);
            return files.Json["changes"]?[RawChanges.Key] != null && kind is not WholeFileKind;
        }

        private static IEnumerable<JsonNode> Descendants(JsonNode node)
        {
            if (node == null) yield break;
            yield return node;
            if (node is JsonObject o) foreach (KeyValuePair<string, JsonNode> e in o) foreach (JsonNode d in Descendants(e.Value)) yield return d;
            if (node is JsonArray a) foreach (JsonNode e in a) foreach (JsonNode d in Descendants(e)) yield return d;
        }

        private string ExistingStem(ChangeKind kind, string id) => _records.ExistingStem(kind, id);

        private void RemoveRecordFiles(ChangeKind kind, string stem)
        {
            foreach (string path in RecordFilePaths(kind, stem))
            {
                File.Delete(path);
                _state.RepoFiles.Remove(RepoKey(path));
            }
        }

        private List<string> RecordFilePaths(ChangeKind kind, string stem) => _records.RecordFilePaths(kind, stem);

        private void WriteRepoFile(string path, byte[] bytes)
        {
            PlainFiles.WriteBytes(path, bytes);
            _state.RepoFiles[RepoKey(path)] = ChangesJson.Sha1(bytes);
        }

        private string RepoKey(string path) => _records.Key(path);

        private IEnumerable<string> RepoFiles() => _records.Files();

        private HashSet<(ChangeKind, string)> FindIncoming()
        {
            HashSet<(ChangeKind, string)> records = new HashSet<(ChangeKind, string)>();
            HashSet<string> present = new HashSet<string>(StringComparer.Ordinal);
            foreach (string key in RepoFiles())
            {
                present.Add(key);
                if (_state.RepoFiles.TryGetValue(key, out string known) && known == ChangesJson.Sha1(File.ReadAllBytes(Path.Combine(Folder, key)))) continue;
                if (_kinds.TryRecordAt(key, out ChangeKind kind, out string id)) records.Add((kind, id));
            }
            foreach (string key in _state.RepoFiles.Keys)
                if (!present.Contains(key) && _kinds.TryRecordAt(key, out ChangeKind kind, out string id)) records.Add((kind, id));
            return records;
        }

        /// <summary>The record as the changes folder holds it, or null when it has no file there.</summary>
        public RecordFiles ReadRecord(ChangeKind kind, string id) => _records.Read(kind, id);

        /// <summary>Every record the changes folder holds.</summary>
        public IEnumerable<(ChangeKind Kind, string Id)> Records() => _records.Records();

        /// <summary>The record's JSON as a path inside the changes folder, or null when it has none.</summary>
        public string RecordKey(ChangeKind kind, string id) =>
            _records.ExistingStem(kind, id) is string stem ? kind.Folder + "/" + stem + ".json" : null;

        /// <summary>Applies incoming records over the base ROM, replacing the project's version of each.</summary>
        public bool ApplyIncoming(IReadOnlyCollection<IncomingRecord> records, out string error)
        {
            using IDisposable busy = Working();
            error = null;
            lock (_gate)
            {
                ChangeContext ctx = Context();
                Dictionary<TrackedFile, byte[]> writes = new Dictionary<TrackedFile, byte[]>();
                List<IncomingRecord> done = new List<IncomingRecord>();
                List<string> skipped = new List<string>();
                // One unreadable record doesn't hold back the others.
                foreach (IncomingRecord r in records)
                {
                    try
                    {
                        RecordFiles change = ReadRecord(r.Kind, r.Id);
                        IReadOnlyList<TrackedFile> files = r.Kind.Files(r.Id, ctx);
                        Dictionary<TrackedFile, byte[]> result = change == null ? new Dictionary<TrackedFile, byte[]>() : r.Kind.Apply(r.Id, change, _base.Read, ctx, _ => true);
                        foreach (TrackedFile f in files) writes[f] = result.TryGetValue(f, out byte[] b) ? b : _base.Read(f);
                        foreach (KeyValuePair<TrackedFile, byte[]> extra in result) writes[extra.Key] = extra.Value;
                        done.Add(r);
                    }
                    catch (System.Text.Json.JsonException)
                    {
                        skipped.Add(r.Title + ": its file isn't valid JSON");
                    }
                    catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is InvalidOperationException)
                    {
                        skipped.Add(r.Title + ": " + ex.Message);
                    }
                }
                if (skipped.Count > 0) error = "These weren't applied:\n" + string.Join("\n", skipped);
                if (done.Count == 0) return false;
                if (!TryWrite(writes, out string writeError))
                {
                    error = writeError;
                    return false;
                }
                records = done;
                foreach (IncomingRecord r in records)
                {
                    foreach (TrackedFile f in r.Kind.Files(r.Id, ctx)) _state.Files.Remove(f.Key);
                    string stem = ExistingStem(r.Kind, r.Id);
                    if (stem == null) continue;
                    foreach (string path in RecordFilePaths(r.Kind, stem)) _state.RepoFiles[RepoKey(path)] = ChangesJson.Sha1(File.ReadAllBytes(path));
                }
                foreach (string key in _state.RepoFiles.Keys.ToList())
                    if (!File.Exists(Path.Combine(Folder, key))) _state.RepoFiles.Remove(key);
                _state.Save(_projectDir);
            }
            Refresh();
            return true;
        }

        /// <summary>Keeps the project's version of records that came in while also changed here, writing it over the changes folder's.</summary>
        public void KeepProject(IReadOnlyCollection<IncomingRecord> records)
        {
            lock (_gate)
            {
                ChangeContext ctx = Context();
                foreach (IncomingRecord r in records)
                {
                    foreach (TrackedFile f in r.Kind.Files(r.Id, ctx)) _state.Files.Remove(f.Key);
                    Export(r.Kind, r.Id, ctx);
                }
                _state.Save(_projectDir);
            }
            Refresh();
        }

        /// <summary>
        /// Every record of the changes folder applied over the project's current files. Conflicts go to
        /// <paramref name="resolve"/>; records that can't be applied at all are listed in <paramref name="problems"/>.
        /// </summary>
        public Dictionary<TrackedFile, byte[]> PlanApplyAll(Func<Conflict, bool> resolve, out List<string> problems)
        {
            problems = new List<string>();
            ChangeContext ctx = Context();
            Dictionary<TrackedFile, byte[]> writes = new Dictionary<TrackedFile, byte[]>();
            HashSet<(ChangeKind Kind, string Id)> records = new HashSet<(ChangeKind, string)>();
            foreach (string key in RepoFiles())
                if (_kinds.TryRecordAt(key, out ChangeKind kind, out string id)) records.Add((kind, id));
            foreach ((ChangeKind kind, string id) in records.OrderBy(r => r.Kind.Folder, StringComparer.Ordinal).ThenBy(r => r.Id, StringComparer.Ordinal))
            {
                try
                {
                    RecordFiles change = ReadRecord(kind, id);
                    if (change == null) continue;
                    foreach (KeyValuePair<TrackedFile, byte[]> w in kind.Apply(id, change, _project.Read, ctx, resolve)) writes[w.Key] = w.Value;
                }
                catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is InvalidOperationException || ex is System.Text.Json.JsonException)
                {
                    problems.Add($"{kind.Title(id, ctx)}: {ex.Message}");
                }
            }
            return writes;
        }

        /// <summary>Forgets what was exported, so the next refresh writes the whole changes folder again.</summary>
        public void ExportEverything()
        {
            using IDisposable busy = Working();
            lock (_gate)
            {
                _state.Files.Clear();
                foreach (string key in RepoFiles().ToList())
                {
                    if (!_kinds.TryRecordAt(key, out ChangeKind _, out string _)) continue;
                    File.Delete(Path.Combine(Folder, key));
                }
                _state.RepoFiles.Clear();
                _state.Save(_projectDir);
            }
            Refresh();
        }

        /// <summary>Writes files into the project through DSPRE's own writers, all or nothing.</summary>
        public bool TryWrite(IReadOnlyDictionary<TrackedFile, byte[]> writes, out string error)
        {
            Interlocked.Increment(ref _applying);
            try { return WriteProject(_project, _projectDir, writes, out error); }
            finally
            {
                Interlocked.Decrement(ref _applying);
                _project.Forget();
            }
        }

        /// <summary>The files an apply replaced, as they were, so Undo apply can put them back.</summary>
        public sealed record AppliedWrites(string ProjectDir, IReadOnlyDictionary<TrackedFile, byte[]> Before);

        /// <summary>The last apply into a project in this session, until it is undone.</summary>
        public static AppliedWrites LastApply { get; private set; }
        public static event Action LastApplyChanged;

        /// <summary>Whether the open project's last apply can still be undone.</summary>
        public static bool CanUndoApply => LastApply is AppliedWrites last && DspreDir != null
            && string.Equals(last.ProjectDir, Path.GetFullPath(DspreDir.TrimEnd('\\', '/')), StringComparison.OrdinalIgnoreCase);

        /// <summary>Puts back every file the last apply replaced, through the tracker when it runs so the folder follows.</summary>
        public static bool UndoLastApply(out string error)
        {
            error = null;
            if (!CanUndoApply) { error = "There's no apply to undo in this project."; return false; }
            IReadOnlyDictionary<TrackedFile, byte[]> before = LastApply.Before;
            bool ok = Current != null ? Current.TryWrite(before, out error) : WriteProject(RomTree.Project(), DspreDir.TrimEnd('\\', '/'), before, out error);
            if (ok)
            {
                // The folder records the undo as a change.
                Current?.SafeRefresh();
                LastApply = null;
                LastApplyChanged?.Invoke();
            }
            return ok;
        }

        /// <summary>Writes into a project through DSPRE's own writers, all or nothing, whether or not it's tracked.</summary>
        public static bool WriteProject(RomTree project, string projectDir, IReadOnlyDictionary<TrackedFile, byte[]> writes, out string error)
        {
            error = null;
            if (writes.Count == 0) return true;
            List<DirNames> archives = writes.Keys.Select(f => f.TryMember(out DirNames d, out _) ? d : (DirNames?)null)
                .Where(d => d != null).Select(d => d.Value).Distinct().ToList();
            if (archives.Count > 0) DSUtils.TryUnpackNarcs(archives);

            // Table rows are laid into their code file, over any whole-file change to it.
            Dictionary<TrackedFile, byte[]> files = writes.Where(w => !w.Key.TryRow(out _, out _)).ToDictionary(w => w.Key, w => w.Value);
            foreach (KeyValuePair<TrackedFile, byte[]> row in writes.Where(w => w.Key.TryRow(out _, out _)))
            {
                row.Key.TryRow(out string tableName, out int index);
                CodeTable table = project.Table(tableName) ?? throw new InvalidOperationException($"{row.Key} has no table in this project.");
                TrackedFile code = TrackedFile.Tree(table.TreePath);
                if (!files.TryGetValue(code, out byte[] bytes) || bytes == null) bytes = project.Read(code);
                bytes = (byte[])bytes.Clone();
                if (row.Value == null || row.Value.Length != table.RowSize) throw new InvalidOperationException($"{row.Key} must be {table.RowSize} bytes.");
                row.Value.CopyTo(bytes, table.RowOffset(index));
                files[code] = bytes;
            }

            Dictionary<TrackedFile, byte[]> before = files.Keys.ToDictionary(k => k, k => project.Read(k));
            ArchiveMembers.Batch batch = new ArchiveMembers.Batch();
            foreach (KeyValuePair<TrackedFile, byte[]> w in files.OrderBy(w => w.Key))
            {
                string path = project.FullPath(w.Key) ?? throw new InvalidOperationException($"{w.Key} has no place in this project.");
                // File names come from the changes folder, which may be someone else's repo.
                string full = Path.GetFullPath(path);
                if (!Inside(full, project.Root) && !Inside(full, projectDir))
                {
                    error = $"{w.Key} points outside the project, so nothing was applied.";
                    return false;
                }
                if (w.Key.TryMember(out DirNames dir, out int id))
                {
                    if (w.Value == null) batch.Delete(dir, id);
                    else batch.Put(dir, id, w.Value);
                }
                else if (w.Value == null)
                {
                    if (File.Exists(path)) batch.DeleteFile(path);
                }
                else if (HgEngineWriteGuard.TryIdentify(path, out int _)) batch.PutCode(path, w.Value);
                else batch.PutFile(path, w.Value);
            }
            bool ok = batch.TryCommit(out error);
            if (ok)
            {
                LastApply = new AppliedWrites(Path.GetFullPath(projectDir), before);
                LastApplyChanged?.Invoke();
                // The project's own decompile keeps its names; the tracker's text has none.
                foreach (TrackedFile f in writes.Keys)
                    if (f.TryMember(out DirNames dir, out int script) && dir == DirNames.Scripts) _ = DSPRE.ROMFiles.ScriptSourceSync.BinaryWrittenAsync(script);
                try { Applied?.Invoke(writes.Keys.ToList()); }
                catch (Exception ex) { AppLogger.WarnOnce("ChangeTracker.Applied", ex); }
            }
            return ok;
        }

        private static bool Inside(string full, string root)
        {
            string r = Path.GetFullPath(root).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            return full.StartsWith(r, StringComparison.OrdinalIgnoreCase);
        }

        private static bool SamePath(string a, string b) =>
            string.Equals(Path.GetFullPath(a).TrimEnd('\\', '/'), Path.GetFullPath(b).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);

        private static bool Same(byte[] a, byte[] b) => a == null ? b == null : b != null && a.AsSpan().SequenceEqual(b);
    }
}
