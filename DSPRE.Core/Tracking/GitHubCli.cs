using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DSPRE.Tracking
{
    /// <summary>GitHub CLI (gh), for signing in, listing repos and creating one. The login stays with gh.</summary>
    public static class GitHubCli
    {
        public sealed record Repo(string NameWithOwner, bool IsPrivate, bool IsEmpty, DateTime UpdatedAt, string Url);

        public static string Executable => CliRunner.Find(SettingsManager.Settings?.GitHubCliPath, "gh");
        public static bool IsAvailable => Executable != null;

        private static Task<CliRunner.Result> RunAsync(string folder, IEnumerable<string> args, Action<string> onLine = null,
            Func<System.IO.StreamWriter, string, Task> onOutput = null)
        {
            string exe = Executable;
            if (exe == null) return Task.FromResult(new CliRunner.Result { ExitCode = -1, Stderr = "GitHub CLI isn't installed, or Settings > git doesn't point at it." });
            return CliRunner.RunAsync(exe, folder, args, onLine, onOutput);
        }

        public static async Task<string> VersionAsync()
        {
            CliRunner.Result r = await RunAsync(null, new[] { "--version" }).ConfigureAwait(false);
            return r.Success ? Regex.Match(r.Stdout, @"gh version (\S+)").Groups[1].Value : null;
        }

        /// <summary>The signed-in account, or null.</summary>
        public static async Task<string> AccountAsync()
        {
            CliRunner.Result r = await RunAsync(null, new[] { "api", "user", "--jq", ".login" }).ConfigureAwait(false);
            return r.Success && !string.IsNullOrWhiteSpace(r.Stdout) ? r.Stdout.Trim() : null;
        }

        /// <summary>Signs in through the browser; <paramref name="onCode"/> gets the one-time code to show.</summary>
        public static async Task<CliRunner.Result> SignInAsync(Action<string> onCode)
        {
            bool sentEnter = false;
            CliRunner.Result login = await RunAsync(null,
                new[] { "auth", "login", "--web", "--git-protocol", "https", "--hostname", "github.com", "--scopes", "repo" },
                onLine: line =>
                {
                    Match code = Regex.Match(line, @"one-time code:\s*([A-Z0-9]{4}-[A-Z0-9]{4})");
                    if (code.Success) onCode?.Invoke(code.Groups[1].Value);
                },
                onOutput: async (stdin, line) =>
                {
                    if (sentEnter || !line.Contains("Press Enter", StringComparison.OrdinalIgnoreCase)) return;
                    sentEnter = true;
                    await stdin.WriteLineAsync().ConfigureAwait(false);
                    await stdin.FlushAsync().ConfigureAwait(false);
                }).ConfigureAwait(false);
            if (!login.Success) return login;
            return await RunAsync(null, new[] { "auth", "setup-git", "--hostname", "github.com" }).ConfigureAwait(false);
        }

        public static async Task<IReadOnlyList<Repo>> ListReposAsync()
        {
            CliRunner.Result r = await RunAsync(null, new[] { "repo", "list", "--limit", "300", "--json", "nameWithOwner,isPrivate,isEmpty,updatedAt,url" }).ConfigureAwait(false);
            if (!r.Success) throw new InvalidOperationException(r.Message);
            JsonArray list = JsonNode.Parse(r.Stdout) as JsonArray ?? new JsonArray();
            return list.OfType<JsonObject>().Select(o => new Repo(
                    o["nameWithOwner"]?.GetValue<string>(),
                    o["isPrivate"]?.GetValue<bool>() ?? false,
                    o["isEmpty"]?.GetValue<bool>() ?? false,
                    o["updatedAt"] is JsonNode u && DateTime.TryParse(u.GetValue<string>(), out DateTime t) ? t : DateTime.MinValue,
                    o["url"]?.GetValue<string>()))
                .OrderByDescending(x => x.UpdatedAt)
                .ToList();
        }

        /// <summary>The repo's dspre.json, or null when it has none.</summary>
        public static async Task<JsonObject> ManifestOfAsync(string nameWithOwner)
        {
            CliRunner.Result r = await RunAsync(null, new[] { "api", $"repos/{nameWithOwner}/contents/{ChangesManifest.FileName}",
                "-H", "Accept: application/vnd.github.raw+json" }).ConfigureAwait(false);
            if (!r.Success) return null;
            try { return ChangesJson.Read(r.Stdout); }
            catch (Exception ex) when (ex is JsonException || ex is System.IO.InvalidDataException) { return null; }
        }

        public static Task<CliRunner.Result> CreateAsync(string name, bool isPrivate, string folder) =>
            RunAsync(folder, new[] { "repo", "create", name, isPrivate ? "--private" : "--public", "--source", folder, "--remote", "origin", "--push" });

        /// <summary>A release of the folder's repo at its pushed commit, tagged <paramref name="tag"/>, with the files attached.</summary>
        public static Task<CliRunner.Result> ReleaseAsync(string folder, string tag, string notes, IEnumerable<string> files)
        {
            List<string> args = new List<string> { "release", "create", tag, "--title", tag, "--notes", notes ?? "" };
            args.AddRange(files);
            return RunAsync(folder, args);
        }
    }
}
