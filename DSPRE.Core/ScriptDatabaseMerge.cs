using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using LibGit2Sharp;

namespace DSPRE
{
    /// <summary>
    /// Brings fixes from the shared script database into a project's own copy, which is made once and otherwise never
    /// refreshed. An entry that still equals a version the shared database has had is the user's untouched copy and
    /// takes the current one; any other entry is the user's edit and stays. Entries the shared database added are added.
    /// </summary>
    internal static class ScriptDatabaseMerge
    {
        // Every text each entry has had in the shared database's history, per shared file and content.
        private static readonly Dictionary<string, Dictionary<(string Section, string Key), HashSet<string>>> HistoryCache = new();

        /// <returns>How many entries were brought up to date or added.</returns>
        public static int Merge(string projectCopy, string shared, string repoPath)
        {
            string marker = projectCopy + ".base";
            string sharedHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(shared)));
            if (File.Exists(marker) && File.ReadAllText(marker).Trim() == sharedHash) return 0;

            JsonObject project = Parse(File.ReadAllText(projectCopy));
            JsonObject current = Parse(File.ReadAllText(shared));
            if (project == null || current == null) return 0;
            Dictionary<(string Section, string Key), HashSet<string>> known = History(shared, sharedHash, repoPath, current);

            int updated = 0;
            foreach (KeyValuePair<string, JsonNode> section in current)
            {
                if (section.Value is not JsonObject upstream) continue;
                if (project[section.Key] is not JsonObject mine)
                {
                    if (project[section.Key] != null) continue;
                    project[section.Key] = upstream.DeepClone();
                    updated += upstream.Count;
                    continue;
                }
                foreach (KeyValuePair<string, JsonNode> entry in upstream)
                {
                    JsonNode have = mine[entry.Key];
                    if (have == null)
                    {
                        mine[entry.Key] = entry.Value?.DeepClone();
                        updated++;
                        continue;
                    }
                    string haveText = have.ToJsonString();
                    if (haveText == entry.Value?.ToJsonString()) continue;
                    if (known.TryGetValue((section.Key, entry.Key), out HashSet<string> versions) && versions.Contains(haveText))
                    {
                        mine[entry.Key] = entry.Value?.DeepClone();
                        updated++;
                    }
                }
            }

            if (updated > 0)
            {
                JsonSerializerOptions options = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
                File.WriteAllText(projectCopy, project.ToJsonString(options));
            }
            File.WriteAllText(marker, sharedHash);
            return updated;
        }

        private static JsonObject Parse(string text)
        {
            try { return JsonNode.Parse(text) as JsonObject; }
            catch (JsonException) { return null; }
        }

        private static Dictionary<(string Section, string Key), HashSet<string>> History(string shared, string sharedHash, string repoPath, JsonObject current)
        {
            string cacheKey = shared + "|" + sharedHash;
            if (HistoryCache.TryGetValue(cacheKey, out Dictionary<(string Section, string Key), HashSet<string>> cached)) return cached;

            Dictionary<(string Section, string Key), HashSet<string>> known = new();
            void Add(JsonObject version)
            {
                foreach (KeyValuePair<string, JsonNode> section in version)
                {
                    if (section.Value is not JsonObject entries) continue;
                    foreach (KeyValuePair<string, JsonNode> entry in entries)
                    {
                        (string Section, string Key) id = (section.Key, entry.Key);
                        if (!known.TryGetValue(id, out HashSet<string> set)) known[id] = set = new HashSet<string>();
                        set.Add(entry.Value?.ToJsonString() ?? "null");
                    }
                }
            }
            Add(current);

            // Without the shared database's git history only new entries can be brought in.
            try
            {
                if (Repository.IsValid(repoPath))
                {
                    using Repository repo = new Repository(repoPath);
                    string relative = Path.GetRelativePath(repoPath, shared).Replace('\\', '/');
                    foreach (LogEntry change in repo.Commits.QueryBy(relative))
                        if (change.Commit[change.Path]?.Target is Blob blob && Parse(blob.GetContentText()) is JsonObject version)
                            Add(version);
                }
            }
            catch (Exception e) when (e is LibGit2SharpException || e is IOException || e is InvalidOperationException)
            {
                AppLogger.Warn("Script database history: " + e.Message);
            }

            HistoryCache[cacheKey] = known;
            return known;
        }
    }
}
