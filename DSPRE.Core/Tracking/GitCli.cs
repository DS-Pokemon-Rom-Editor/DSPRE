using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DSPRE.Tracking
{
    /// <summary>The installed git, run in the changes folder.</summary>
    public static class GitCli
    {
        public sealed record Entry(string Path, bool Untracked, bool Conflicted);

        public sealed record Status(string Branch, string Upstream, int Ahead, int Behind, bool HasRemote, IReadOnlyList<Entry> Entries)
        {
            public int Changed => Entries.Count(e => !e.Untracked && !e.Conflicted);
            public int Untracked => Entries.Count(e => e.Untracked);
            public int Conflicts => Entries.Count(e => e.Conflicted);
        }

        public static string Executable => CliRunner.Find(SettingsManager.Settings?.GitPath, "git");
        public static bool IsAvailable => Executable != null;

        public static Task<CliRunner.Result> RunAsync(string folder, params string[] args)
        {
            string exe = Executable;
            if (exe == null) return Task.FromResult(new CliRunner.Result { ExitCode = -1, Stderr = "git isn't installed, or Settings > git doesn't point at it." });
            return CliRunner.RunAsync(exe, folder, args);
        }

        public static async Task<string> VersionAsync()
        {
            CliRunner.Result r = await RunAsync(null, "--version").ConfigureAwait(false);
            return r.Success ? r.Stdout.Trim().Replace("git version ", "") : null;
        }

        public static bool IsRepository(string folder) => Directory.Exists(Path.Combine(folder, ".git")) || File.Exists(Path.Combine(folder, ".git"));

        public static async Task<Status> ReadStatusAsync(string folder)
        {
            CliRunner.Result r = await RunAsync(folder, "status", "--porcelain=v2", "--branch", "--untracked-files=all").ConfigureAwait(false);
            if (!r.Success) return null;
            string branch = null, upstream = null;
            int ahead = 0, behind = 0;
            List<Entry> entries = new List<Entry>();
            foreach (string line in r.Stdout.Split('\n').Select(l => l.TrimEnd('\r')))
            {
                if (line.StartsWith("# branch.head ", StringComparison.Ordinal)) branch = line.Substring(14);
                else if (line.StartsWith("# branch.upstream ", StringComparison.Ordinal)) upstream = line.Substring(18);
                else if (line.StartsWith("# branch.ab ", StringComparison.Ordinal))
                {
                    string[] parts = line.Substring(12).Split(' ');
                    ahead = int.Parse(parts[0].TrimStart('+'), CultureInfo.InvariantCulture);
                    behind = int.Parse(parts[1].TrimStart('-'), CultureInfo.InvariantCulture);
                }
                else if (line.StartsWith("? ", StringComparison.Ordinal)) entries.Add(new Entry(line.Substring(2), true, false));
                else if (line.StartsWith("u ", StringComparison.Ordinal)) entries.Add(new Entry(LastField(line, 10), false, true));
                else if (line.StartsWith("1 ", StringComparison.Ordinal)) entries.Add(new Entry(LastField(line, 8), false, false));
                else if (line.StartsWith("2 ", StringComparison.Ordinal)) entries.Add(new Entry(LastField(line, 9).Split('\t')[0], false, false));
            }
            CliRunner.Result remotes = await RunAsync(folder, "remote").ConfigureAwait(false);
            bool hasRemote = remotes.Success && !string.IsNullOrWhiteSpace(remotes.Stdout);
            return new Status(branch == "(detached)" ? null : branch, upstream, ahead, behind, hasRemote, entries);
        }

        private static string LastField(string line, int fieldsBefore)
        {
            int at = 0;
            for (int i = 0; i < fieldsBefore && at >= 0; i++) at = line.IndexOf(' ', at) + 1;
            return at <= 0 ? line : line.Substring(at);
        }

        public static Task<CliRunner.Result> InitAsync(string folder) => RunAsync(folder, "init", "-b", "main");

        /// <summary>Commits the given paths, or everything when none are given.</summary>
        public static async Task<CliRunner.Result> CommitAsync(string folder, string message, IReadOnlyCollection<string> paths = null)
        {
            List<string> add = new List<string> { "add", "-A", "--" };
            if (paths != null && paths.Count > 0) add.AddRange(paths);
            else add.Add(".");
            CliRunner.Result added = await RunAsync(folder, add.ToArray()).ConfigureAwait(false);
            if (!added.Success) return added;
            List<string> commit = new List<string> { "commit", "-m", message };
            if (paths != null && paths.Count > 0)
            {
                commit.Add("--");
                commit.AddRange(paths);
            }
            return await RunAsync(folder, commit.ToArray()).ConfigureAwait(false);
        }

        public static Task<CliRunner.Result> FetchAsync(string folder) => RunAsync(folder, "fetch", "--prune", "origin");

        public static Task<CliRunner.Result> FastForwardAsync(string folder, string upstream) => RunAsync(folder, "merge", "--ff-only", upstream);

        /// <summary>Starts a merge and leaves it open, so records can be merged before the commit.</summary>
        public static Task<CliRunner.Result> StartMergeAsync(string folder, string upstream) =>
            RunAsync(folder, "-c", "merge.conflictStyle=merge", "merge", "--no-ff", "--no-commit", upstream);

        public static Task<CliRunner.Result> AbortMergeAsync(string folder) => RunAsync(folder, "merge", "--abort");

        public static bool IsMerging(string folder) => File.Exists(Path.Combine(folder, ".git", "MERGE_HEAD"));

        public static async Task<string> MergeBaseAsync(string folder, string a, string b)
        {
            CliRunner.Result r = await RunAsync(folder, "merge-base", a, b).ConfigureAwait(false);
            return r.Success ? r.Stdout.Trim() : null;
        }

        /// <summary>Files that differ between two commits.</summary>
        public static async Task<IReadOnlyList<string>> ChangedBetweenAsync(string folder, string from, string to)
        {
            CliRunner.Result r = await RunAsync(folder, "diff", "--name-only", "--no-renames", from, to).ConfigureAwait(false);
            if (!r.Success) return Array.Empty<string>();
            return r.Stdout.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        }

        /// <summary>A file's text at a commit, or null when it isn't there.</summary>
        public static async Task<string> ShowAsync(string folder, string commit, string path)
        {
            CliRunner.Result r = await RunAsync(folder, "show", commit + ":" + path).ConfigureAwait(false);
            return r.Success ? r.Stdout : null;
        }

        /// <summary>Takes one side's copy of a file during a merge; a side without the file deletes it.</summary>
        public static async Task<CliRunner.Result> TakeSideAsync(string folder, string path, bool local)
        {
            CliRunner.Result r = await RunAsync(folder, "checkout", local ? "--ours" : "--theirs", "--", path).ConfigureAwait(false);
            if (r.Success) return await RunAsync(folder, "add", "--", path).ConfigureAwait(false);
            return await RunAsync(folder, "rm", "--quiet", "--ignore-unmatch", "--", path).ConfigureAwait(false);
        }

        public static Task<CliRunner.Result> AddAsync(string folder, IReadOnlyCollection<string> paths) =>
            RunAsync(folder, new[] { "add", "-A", "--" }.Concat(paths).ToArray());

        public static Task<CliRunner.Result> FinishMergeAsync(string folder, string message) => RunAsync(folder, "commit", "--no-edit", "-m", message);

        /// <summary>Commits in <paramref name="range"/>, newest first.</summary>
        public static async Task<IReadOnlyList<(string Sha, string Subject, string Author, DateTimeOffset When)>> LogAsync(string folder, params string[] range)
        {
            CliRunner.Result r = await RunAsync(folder, new[] { "log", "--format=%h%x09%an%x09%at%x09%s" }.Concat(range).ToArray()).ConfigureAwait(false);
            List<(string, string, string, DateTimeOffset)> list = new List<(string, string, string, DateTimeOffset)>();
            if (!r.Success) return list;
            foreach (string line in r.Stdout.Split('\n'))
            {
                string[] parts = line.TrimEnd('\r').Split('\t', 4);
                if (parts.Length < 4 || !long.TryParse(parts[2], out long unix)) continue;
                list.Add((parts[0], parts[3], parts[1], DateTimeOffset.FromUnixTimeSeconds(unix)));
            }
            return list;
        }

        public static async Task<CliRunner.Result> PushAsync(string folder)
        {
            Status status = await ReadStatusAsync(folder).ConfigureAwait(false);
            return status?.Upstream == null
                ? await RunAsync(folder, "push", "--follow-tags", "-u", "origin", "HEAD").ConfigureAwait(false)
                : await RunAsync(folder, "push", "--follow-tags").ConfigureAwait(false);
        }

        /// <summary>Release tags, newest first.</summary>
        public static async Task<IReadOnlyList<string>> TagsAsync(string folder)
        {
            CliRunner.Result r = await RunAsync(folder, "tag", "--list", "--sort=-creatordate").ConfigureAwait(false);
            if (!r.Success) return Array.Empty<string>();
            return r.Stdout.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        }

        /// <summary>Marks the last commit as a release, so later changelogs can start from it.</summary>
        public static Task<CliRunner.Result> TagAsync(string folder, string name) => RunAsync(folder, "tag", "-a", name, "-m", name);

        public static Task<CliRunner.Result> CloneAsync(string url, string folder) =>
            RunAsync(Path.GetDirectoryName(Path.GetFullPath(folder)), "clone", url, Path.GetFullPath(folder));

        public static async Task<string> RemoteUrlAsync(string folder)
        {
            CliRunner.Result r = await RunAsync(folder, "remote", "get-url", "origin").ConfigureAwait(false);
            return r.Success ? r.Stdout.Trim() : null;
        }

        public static async Task<CliRunner.Result> SetRemoteAsync(string folder, string url)
        {
            string existing = await RemoteUrlAsync(folder).ConfigureAwait(false);
            return existing == null
                ? await RunAsync(folder, "remote", "add", "origin", url).ConfigureAwait(false)
                : await RunAsync(folder, "remote", "set-url", "origin", url).ConfigureAwait(false);
        }

        /// <summary>The name and email commits are made with, or null when git has none.</summary>
        public static async Task<(string Name, string Email)?> IdentityAsync(string folder)
        {
            CliRunner.Result name = await RunAsync(folder, "config", "user.name").ConfigureAwait(false);
            CliRunner.Result email = await RunAsync(folder, "config", "user.email").ConfigureAwait(false);
            if (!name.Success || !email.Success || string.IsNullOrWhiteSpace(name.Stdout) || string.IsNullOrWhiteSpace(email.Stdout)) return null;
            return (name.Stdout.Trim(), email.Stdout.Trim());
        }

        /// <summary>Sets the commit name and email for <paramref name="folder"/>'s repo, or for every repo when it's null.</summary>
        public static async Task<CliRunner.Result> SetIdentityAsync(string name, string email, string folder)
        {
            string[] scope = folder == null ? new[] { "config", "--global" } : new[] { "config" };
            CliRunner.Result r = await RunAsync(folder, scope.Append("user.name").Append(name).ToArray()).ConfigureAwait(false);
            if (!r.Success) return r;
            return await RunAsync(folder, scope.Append("user.email").Append(email).ToArray()).ConfigureAwait(false);
        }
    }
}
