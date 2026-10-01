using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// A Rotom project keeps its own copy of the script command database, made when the project was set up,
    /// while DSPRE's shared copy follows scrcmd-database. When they differ the user decides: take the newer
    /// database, which fixes how changed commands are read and written, or keep the project exactly as it is.
    /// A decision to keep holds until the shared database changes again.
    /// </summary>
    public static class RotomDatabaseUpdate
    {
        public sealed class Offer
        {
            public string ProjectFile { get; init; }
            public string LatestFile { get; init; }
            public string LatestHash { get; init; }

            /// <summary>Command names whose definition differs, sorted.</summary>
            public List<string> ChangedCommands { get; init; }

            /// <summary>Whether anything besides commands differs, such as variable or movement names.</summary>
            public bool OtherChanges { get; init; }
        }

        private static string DecisionPath => Path.Combine(RotomTool.ProjectRoot, ".rotom", "status", "dspre-database.json");

        /// <summary>The newer database the project could take, or null when there is nothing to offer.</summary>
        public static Offer Check()
        {
            try
            {
                if (!RomInfo.hasRotomProject) return null;
                string project = ProjectDatabaseFile();
                if (project == null || !File.Exists(project)) return null;
                string latest = LatestDatabaseFile(Path.GetFileName(project));
                if (latest == null) return null;

                byte[] mine = File.ReadAllBytes(project), theirs = File.ReadAllBytes(latest);
                if (mine.AsSpan().SequenceEqual(theirs)) return null;
                string hash = Convert.ToHexString(XxHash3.Hash(theirs));
                if (DeclinedHash() == hash) return null;

                var (changed, other) = Compare(mine, theirs);
                if (changed.Count == 0 && !other) return null;
                return new Offer { ProjectFile = project, LatestFile = latest, LatestHash = hash, ChangedCommands = changed, OtherChanges = other };
            }
            catch (Exception ex)
            {
                AppLogger.Warn("The Rotom database check failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>Keeps the project's database and stops asking until a newer one arrives.</summary>
        public static void Decline(Offer offer)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DecisionPath));
            File.WriteAllText(DecisionPath, JsonSerializer.Serialize(new Dictionary<string, string> { ["declined"] = offer.LatestHash }));
        }

        /// <summary>
        /// Takes the newer database, backing the old one up under .rotom/backups. Sources nobody edited are made
        /// again from their binaries with it, so a source the old database decoded wrongly is replaced; edited
        /// sources are kept and compiled as they are. Returns null, or why it stopped, and the scripts whose binaries the compile kept.
        /// </summary>
        public static async Task<(string Problem, List<int> Kept)> ApplyAsync(Offer offer)
        {
            string backup = Path.Combine(RotomTool.ProjectRoot, ".rotom", "backups", "dspre-database-" + DateTime.Now.ToString("yyyyMMddHHmmss"));
            Directory.CreateDirectory(backup);
            File.Copy(offer.ProjectFile, Path.Combine(backup, Path.GetFileName(offer.ProjectFile)), overwrite: true);
            File.Copy(offer.LatestFile, offer.ProjectFile, overwrite: true);
            if (File.Exists(DecisionPath)) File.Delete(DecisionPath);

            var unedited = UneditedScripts();
            if (unedited.Count > 0)
            {
                await ScriptSourceSync.RefreshAsync(unedited).ConfigureAwait(false);
                await ScriptSourceSync.WhenIdleAsync().ConfigureAwait(false);
            }

            var compiled = await RotomTool.CompileProjectAsync().ConfigureAwait(false);
            return (compiled.Success ? null
                        : "The project did not compile with the newer database. The old one is in " + backup + ".\n" + RotomTool.FormatResult(compiled),
                    compiled.KeptBinaries);
        }

        // The database rotom.toml names under [database], relative to the project.
        private static string ProjectDatabaseFile()
        {
            string toml = Path.Combine(RotomTool.ProjectRoot, "rotom.toml");
            if (!File.Exists(toml)) return null;
            bool inDatabase = false;
            foreach (string raw in File.ReadLines(toml))
            {
                string line = raw.Trim();
                if (line.StartsWith("[")) { inDatabase = line == "[database]"; continue; }
                if (!inDatabase || !line.StartsWith("default_file")) continue;
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                string value = line.Substring(eq + 1).Trim().Trim('"');
                return Path.Combine(RotomTool.ProjectRoot, value.Replace('/', Path.DirectorySeparatorChar));
            }
            return null;
        }

        // DSPRE's shared copy of the same file, at the top of the checkout or among the hack databases.
        private static string LatestDatabaseFile(string name)
        {
            foreach (string candidate in new[]
                     {
                         Path.Combine(AppPaths.DatabasePath, name),
                         Path.Combine(AppPaths.DatabasePath, "custom_databases", name),
                     })
                if (File.Exists(candidate)) return candidate;
            return null;
        }

        private static string DeclinedHash()
        {
            if (!File.Exists(DecisionPath)) return null;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(DecisionPath));
                return doc.RootElement.TryGetProperty("declined", out var v) ? v.GetString() : null;
            }
            catch { return null; }
        }

        private static (List<string> Commands, bool Other) Compare(byte[] mine, byte[] theirs)
        {
            using var a = JsonDocument.Parse(mine);
            using var b = JsonDocument.Parse(theirs);
            var changed = new SortedSet<string>(StringComparer.Ordinal);
            var oldCommands = Section(a.RootElement, "commands");
            var newCommands = Section(b.RootElement, "commands");
            foreach (string name in oldCommands.Keys.Union(newCommands.Keys))
                if (!oldCommands.TryGetValue(name, out var o) || !newCommands.TryGetValue(name, out var n) || o != n)
                    changed.Add(name);

            bool other = false;
            // The meta block only says when and how the file was built.
            var oldRest = a.RootElement.EnumerateObject().Where(p => p.Name is not ("commands" or "meta")).ToDictionary(p => p.Name, p => Canonical(p.Value));
            var newRest = b.RootElement.EnumerateObject().Where(p => p.Name is not ("commands" or "meta")).ToDictionary(p => p.Name, p => Canonical(p.Value));
            foreach (string key in oldRest.Keys.Union(newRest.Keys))
                if (!oldRest.TryGetValue(key, out var o) || !newRest.TryGetValue(key, out var n) || o != n) { other = true; break; }
            return (changed.ToList(), other);
        }

        private static Dictionary<string, string> Section(JsonElement root, string name) =>
            root.TryGetProperty(name, out var section) && section.ValueKind == JsonValueKind.Object
                ? section.EnumerateObject().ToDictionary(p => p.Name, p => Canonical(p.Value))
                : new Dictionary<string, string>();

        // The same entry written with other spacing or escapes reads the same.
        private static string Canonical(JsonElement element) =>
            System.Text.Json.Nodes.JsonNode.Parse(element.GetRawText())?.ToJsonString() ?? "null";

        // Scripts whose source nobody edited since rotom last compiled it. Their binary is the truth either way:
        // the game's own, kept because the old source did not build back to it, or built from that same source.
        private static List<int> UneditedScripts() =>
            RotomProjectUpgrade.RecordedScripts(out _)
                .Where(s => RotomProjectUpgrade.Hash(s.Source) == s.SourceHash)
                .Select(s => s.Id)
                .ToList();
    }
}
