using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// A new rotom or command database makes rotom rebuild every script, and a wrong database entry then
    /// rewrites scripts nobody edited (retail Diamond loses part of a script this way). Around a compile,
    /// a script whose source and dependencies did not change gets its previous binary back when such a
    /// rebuild changed it, and its recorded output hash follows so later compiles leave it alone.
    /// </summary>
    public sealed class RotomRebuildGuard
    {
        private sealed class Entry
        {
            public ulong SourceHash;
            public Dictionary<string, string> Dependencies;
            public byte[] Binary;
        }

        private readonly string _statePath;
        private readonly string _version;
        private readonly ulong _dbHash;
        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();

        private RotomRebuildGuard(string statePath, string version, ulong dbHash)
        {
            _statePath = statePath;
            _version = version;
            _dbHash = dbHash;
        }

        /// <summary>Records the scripts as they are before a compile, or null when there is no compile state.</summary>
        public static RotomRebuildGuard Before(string projectRoot)
        {
            string statePath = Path.Combine(projectRoot, ".rotom", "status", "compile-state.json");
            if (!File.Exists(statePath)) return null;
            try
            {
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(statePath));
                JsonElement root = doc.RootElement;
                RotomRebuildGuard guard = new RotomRebuildGuard(statePath, Version(root), DbHash(root));
                if (!root.TryGetProperty("entries", out JsonElement entries)) return guard;
                foreach (JsonProperty e in entries.EnumerateObject())
                {
                    string binary = BinaryFor(e.Name);
                    if (binary == null || !File.Exists(binary)) continue;
                    guard._entries[e.Name] = new Entry
                    {
                        SourceHash = e.Value.GetProperty("source_hash").GetUInt64(),
                        Dependencies = Dependencies(e.Value),
                        Binary = File.ReadAllBytes(binary),
                    };
                }
                return guard;
            }
            catch (Exception ex) when (ex is IOException || ex is JsonException || ex is InvalidOperationException || ex is KeyNotFoundException)
            {
                AppLogger.Warn("Could not read the Rotom compile state before compiling: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// After a compile: when it was a whole-project rebuild, restores the binaries of unedited scripts it
        /// changed. Returns their ids, empty when nothing needed restoring.
        /// </summary>
        public List<int> After()
        {
            List<int> kept = new List<int>();
            if (!File.Exists(_statePath)) return kept;
            try
            {
                JsonObject state = JsonNode.Parse(File.ReadAllText(_statePath)) as JsonObject;
                if (state == null) return kept;
                bool rebuilt = (string)state["compiler_version"] != _version || (state["db_hash"]?.GetValue<ulong>() ?? 0) != _dbHash;
                if (!rebuilt || !(state["entries"] is JsonObject entries)) return kept;

                foreach ((string name, JsonNode node) in entries)
                {
                    if (!(node is JsonObject entry) || !_entries.TryGetValue(name, out Entry before)) continue;
                    if (entry["source_hash"]?.GetValue<ulong>() != before.SourceHash) continue;
                    if (DependencyChanged(before.Dependencies, Dependencies(JsonDocument.Parse(entry.ToJsonString()).RootElement))) continue;

                    string binary = BinaryFor(name);
                    if (binary == null || !File.Exists(binary)) continue;
                    if (File.ReadAllBytes(binary).AsSpan().SequenceEqual(before.Binary)) continue;

                    File.WriteAllBytes(binary, before.Binary);
                    entry["output_hash"] = XxHash3.HashToUInt64(before.Binary);
                    kept.Add(int.Parse(Path.GetFileNameWithoutExtension(name)));
                }

                kept.Sort();
                if (kept.Count > 0)
                {
                    File.WriteAllText(_statePath, state.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                    AppLogger.Warn("Rotom rebuilt scripts nobody edited into different bytes; kept the previous binaries of "
                                   + string.Join(", ", kept.Select(id => id.ToString("D4"))) + ".");
                }
            }
            catch (Exception ex) when (ex is IOException || ex is JsonException || ex is InvalidOperationException || ex is FormatException)
            {
                AppLogger.Warn("Could not check the Rotom rebuild against the previous binaries: " + ex.Message);
            }
            return kept;
        }

        private static string Version(JsonElement root)
            => root.TryGetProperty("compiler_version", out JsonElement v) ? v.GetString() : null;

        private static ulong DbHash(JsonElement root)
            => root.TryGetProperty("db_hash", out JsonElement v) && v.ValueKind == JsonValueKind.Number ? v.GetUInt64() : 0;

        private static Dictionary<string, string> Dependencies(JsonElement entry)
        {
            Dictionary<string, string> found = new Dictionary<string, string>(StringComparer.Ordinal);
            if (entry.TryGetProperty("dependency_hashes", out JsonElement deps) && deps.ValueKind == JsonValueKind.Object)
                foreach (JsonProperty d in deps.EnumerateObject()) found[d.Name] = d.Value.GetRawText();
            return found;
        }

        // A dependency the script already had that now differs is a real change; one first recorded by this
        // rebuild is not, since a decompiled source starts with none.
        private static bool DependencyChanged(Dictionary<string, string> before, Dictionary<string, string> after)
            => before.Any(d => !after.TryGetValue(d.Key, out string now) || now != d.Value);

        private static string BinaryFor(string entryName)
        {
            string file = Path.GetFileNameWithoutExtension(entryName.Replace('\\', '/'));
            return int.TryParse(file, out int id) ? Filesystem.GetScriptPath(id) : null;
        }
    }
}
