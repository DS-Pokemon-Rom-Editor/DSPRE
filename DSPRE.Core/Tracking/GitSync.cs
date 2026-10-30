using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DSPRE.Tracking
{
    /// <summary>A record both sides changed since they split, with what each side has.</summary>
    public sealed class RecordClash
    {
        public string Path { get; init; }
        public string Title { get; init; }
        public ChangeKind Kind { get; init; }
        public string Id { get; init; }
        public JsonObject Common { get; init; }
        public JsonObject Local { get; init; }
        public JsonObject Remote { get; init; }
        public IReadOnlyList<FieldConflict> Conflicts { get; init; }

        /// <summary>Field path to true for local, false for remote. A conflict with no choice keeps local.</summary>
        public Dictionary<string, bool> Choices { get; } = new Dictionary<string, bool>(StringComparer.Ordinal);

        public bool Resolved => Conflicts.All(c => Choices.ContainsKey(c.Path));
    }

    /// <summary>A merge left open until its clashes are picked.</summary>
    public sealed class PendingMerge
    {
        public string Folder { get; init; }
        public string Upstream { get; init; }
        public string LocalSubject { get; init; }
        public string RemoteSubject { get; init; }
        public IReadOnlyList<RecordClash> Records { get; init; }
        public IEnumerable<RecordClash> Clashing => Records.Where(r => r.Conflicts.Count > 0);
    }

    /// <summary>Pull for a changes folder: git moves the commits, DSPRE merges the records field by field.</summary>
    public static class GitSync
    {
        public enum Outcome { UpToDate, FastForwarded, Merged, NeedsChoices, NeedsCommit, Failed }

        public sealed record PullResult(Outcome Outcome, string Error = null, PendingMerge Merge = null);

        public static async Task<PullResult> PullAsync(ChangeTracker tracker)
        {
            string folder = tracker.Folder;
            GitCli.Status status = await GitCli.ReadStatusAsync(folder).ConfigureAwait(false);
            if (status == null) return new PullResult(Outcome.Failed, "git couldn't read the changes folder.");
            if (GitCli.IsMerging(folder)) return await ResumeAsync(tracker).ConfigureAwait(false);
            if (status.Entries.Count > 0) return new PullResult(Outcome.NeedsCommit);
            if (status.Upstream == null) return new PullResult(Outcome.Failed, "This branch has no remote branch to pull from yet. Push it first.");

            CliRunner.Result fetched = await GitCli.FetchAsync(folder).ConfigureAwait(false);
            if (!fetched.Success) return new PullResult(Outcome.Failed, GitMessages.Explain(fetched));
            status = await GitCli.ReadStatusAsync(folder).ConfigureAwait(false);
            if (status.Behind == 0) return new PullResult(Outcome.UpToDate);
            if (status.Ahead == 0)
            {
                CliRunner.Result ff = await GitCli.FastForwardAsync(folder, status.Upstream).ConfigureAwait(false);
                return ff.Success ? new PullResult(Outcome.FastForwarded) : new PullResult(Outcome.Failed, GitMessages.Explain(ff));
            }

            CliRunner.Result started = await GitCli.StartMergeAsync(folder, status.Upstream).ConfigureAwait(false);
            if (!GitCli.IsMerging(folder))
                return new PullResult(Outcome.Failed, started.Success ? "git finished the merge on its own." : GitMessages.Explain(started));
            return await ResumeAsync(tracker).ConfigureAwait(false);
        }

        /// <summary>Works out the clashes of the merge in progress; finishes it when none need a person.</summary>
        public static async Task<PullResult> ResumeAsync(ChangeTracker tracker)
        {
            PendingMerge merge = await ReadAsync(tracker).ConfigureAwait(false);
            if (merge == null) return new PullResult(Outcome.Failed, "The merge in progress couldn't be read.");
            if (merge.Clashing.Any()) return new PullResult(Outcome.NeedsChoices, null, merge);
            string error = await FinishAsync(merge).ConfigureAwait(false);
            return error == null ? new PullResult(Outcome.Merged) : new PullResult(Outcome.Failed, error, merge);
        }

        public static async Task<PendingMerge> ReadAsync(ChangeTracker tracker)
        {
            string folder = tracker.Folder;
            if (!GitCli.IsMerging(folder)) return null;
            GitCli.Status status = await GitCli.ReadStatusAsync(folder).ConfigureAwait(false);
            string common = await GitCli.MergeBaseAsync(folder, "HEAD", "MERGE_HEAD").ConfigureAwait(false);
            if (common == null) return null;
            IReadOnlyList<string> mine = await GitCli.ChangedBetweenAsync(folder, common, "HEAD").ConfigureAwait(false);
            IReadOnlyList<string> theirs = await GitCli.ChangedBetweenAsync(folder, common, "MERGE_HEAD").ConfigureAwait(false);
            HashSet<string> both = new HashSet<string>(mine.Intersect(theirs, StringComparer.Ordinal), StringComparer.Ordinal);
            foreach (GitCli.Entry e in status?.Entries ?? Array.Empty<GitCli.Entry>()) if (e.Conflicted) both.Add(e.Path);

            ChangeContext ctx = tracker.Context();
            List<RecordClash> records = new List<RecordClash>();
            foreach (string path in both.Where(p => p.EndsWith(".json", StringComparison.Ordinal)).OrderBy(p => p, StringComparer.Ordinal))
            {
                if (!tracker.Kinds.TryRecordAt(path, out ChangeKind kind, out string id)) continue;
                JsonObject b = Parse(await GitCli.ShowAsync(folder, common, path).ConfigureAwait(false));
                JsonObject l = Parse(await GitCli.ShowAsync(folder, "HEAD", path).ConfigureAwait(false));
                JsonObject r = Parse(await GitCli.ShowAsync(folder, "MERGE_HEAD", path).ConfigureAwait(false));
                List<FieldConflict> conflicts = new List<FieldConflict>();
                RecordMerge.MergeRecord(b, l, r, c => { conflicts.Add(c); return true; });
                records.Add(new RecordClash
                {
                    Path = path, Kind = kind, Id = id, Title = kind.Title(id, ctx),
                    Common = b, Local = l, Remote = r, Conflicts = conflicts,
                });
            }
            IReadOnlyList<(string Sha, string Subject, string Author, DateTimeOffset When)> localLog = await GitCli.LogAsync(folder, "-1", "HEAD").ConfigureAwait(false);
            IReadOnlyList<(string Sha, string Subject, string Author, DateTimeOffset When)> remoteLog = await GitCli.LogAsync(folder, "-1", "MERGE_HEAD").ConfigureAwait(false);
            return new PendingMerge
            {
                Folder = folder,
                Upstream = status?.Upstream ?? "the remote",
                LocalSubject = localLog.Count > 0 ? localLog[0].Sha + " " + localLog[0].Subject : null,
                RemoteSubject = remoteLog.Count > 0 ? remoteLog[0].Sha + " " + remoteLog[0].Subject + " · " + remoteLog[0].Author : null,
                Records = records,
            };
        }

        /// <summary>Writes every merged record with the picks made, settles the files around them and commits.</summary>
        public static async Task<string> FinishAsync(PendingMerge merge)
        {
            string folder = merge.Folder;
            List<string> touched = new List<string>();
            foreach (RecordClash record in merge.Records)
            {
                JsonObject merged = RecordMerge.MergeRecord(record.Common, record.Local, record.Remote,
                    c => !record.Choices.TryGetValue(c.Path, out bool local) || local);
                string full = Path.Combine(folder, record.Path);
                if (merged == null)
                {
                    if (File.Exists(full)) File.Delete(full);
                }
                else PlainFiles.WriteBytes(full, ChangesJson.WriteBytes(merged));
                touched.Add(record.Path);
                touched.AddRange(await SettleSidecarsAsync(folder, record, merged).ConfigureAwait(false));
            }
            if (touched.Count > 0)
            {
                CliRunner.Result added = await GitCli.AddAsync(folder, touched).ConfigureAwait(false);
                if (!added.Success) return GitMessages.Explain(added);
            }
            // Generated files (schemas, the manifest) clash only on formatting; the remote's copy is as good as ours.
            GitCli.Status status = await GitCli.ReadStatusAsync(folder).ConfigureAwait(false);
            foreach (GitCli.Entry e in status?.Entries.Where(e => e.Conflicted) ?? Enumerable.Empty<GitCli.Entry>())
            {
                CliRunner.Result r = await GitCli.TakeSideAsync(folder, e.Path, local: !IsGenerated(e.Path)).ConfigureAwait(false);
                if (!r.Success) return GitMessages.Explain(r);
            }
            string bad = FirstUnreadableRecord(folder);
            if (bad != null) return bad + " still isn't valid JSON, so the merge wasn't committed.";
            CliRunner.Result done = await GitCli.FinishMergeAsync(folder, "Merge changes from " + merge.Upstream).ConfigureAwait(false);
            return done.Success ? null : GitMessages.Explain(done);
        }

        public static async Task<string> AbortAsync(string folder)
        {
            CliRunner.Result r = await GitCli.AbortMergeAsync(folder).ConfigureAwait(false);
            return r.Success ? null : GitMessages.Explain(r);
        }

        // Files kept whole sit beside their record; the side whose record won supplies them.
        private static async Task<List<string>> SettleSidecarsAsync(string folder, RecordClash record, JsonObject merged)
        {
            List<string> settled = new List<string>();
            string dir = Path.GetDirectoryName(record.Path)?.Replace('\\', '/') ?? "";
            string stem = Path.GetFileNameWithoutExtension(record.Path);
            GitCli.Status status = await GitCli.ReadStatusAsync(folder).ConfigureAwait(false);
            IEnumerable<string> sidecars = (status?.Entries ?? Array.Empty<GitCli.Entry>())
                .Where(e => e.Conflicted)
                .Select(e => e.Path)
                .Where(p => (Path.GetDirectoryName(p)?.Replace('\\', '/') ?? "") == dir && Path.GetFileName(p).StartsWith(stem + ".", StringComparison.Ordinal) && !p.EndsWith(".json", StringComparison.Ordinal));
            foreach (string sidecar in sidecars)
            {
                string name = Path.GetFileName(sidecar);
                bool local = merged == null || ShaFor(merged, name) is not string want || want == ShaFor(record.Local, name);
                CliRunner.Result r = await GitCli.TakeSideAsync(folder, sidecar, local).ConfigureAwait(false);
                if (r.Success) settled.Add(sidecar);
            }
            return settled;
        }

        // The SHA-1 a record names for one of its files.
        private static string ShaFor(JsonNode node, string name)
        {
            if (node is JsonObject o)
            {
                if (o["file"] is JsonValue f && f.TryGetValue(out string file) && file == name && o[ObjectDiff.Now] is JsonValue n && n.TryGetValue(out string sha)) return sha;
                foreach (KeyValuePair<string, JsonNode> e in o) if (ShaFor(e.Value, name) is string found) return found;
            }
            if (node is JsonArray a) foreach (JsonNode e in a) if (ShaFor(e, name) is string found) return found;
            return null;
        }

        private static bool IsGenerated(string path) => path == ChangesManifest.FileName || path.StartsWith(".schema/", StringComparison.Ordinal);

        /// <summary>The first record JSON in the folder that doesn't parse or still holds conflict markers, or null.</summary>
        public static string FirstUnreadableRecord(string folder, IEnumerable<string> only = null)
        {
            IEnumerable<string> files = only?.Select(p => Path.Combine(folder, p)).Where(File.Exists)
                                        ?? Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories)
                                            .Where(p => !Path.GetRelativePath(folder, p).StartsWith(".", StringComparison.Ordinal));
            foreach (string file in files.Where(f => f.EndsWith(".json", StringComparison.Ordinal)))
            {
                string text = File.ReadAllText(file);
                string rel = Path.GetRelativePath(folder, file).Replace('\\', '/');
                if (text.Contains("<<<<<<<", StringComparison.Ordinal) || text.Contains(">>>>>>>", StringComparison.Ordinal)) return rel;
                try { JsonNode.Parse(text); }
                catch (JsonException) { return rel; }
            }
            return null;
        }

        private static JsonObject Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            try { return JsonNode.Parse(text) as JsonObject; }
            catch (JsonException) { return null; }
        }
    }
}
