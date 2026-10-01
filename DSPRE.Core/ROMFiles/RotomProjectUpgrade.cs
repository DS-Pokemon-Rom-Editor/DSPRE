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
    /// Brings a Rotom project up to the bundled rotom. A compile state from another rotom version makes
    /// the next compile rebuild every binary from its source, so a binary written outside rotom would be
    /// replaced by its stale source. Before that happens, each file is checked against the hashes rotom
    /// recorded, and a binary that changed gets its source regenerated first.
    /// </summary>
    public static class RotomProjectUpgrade
    {
        public sealed class Assessment
        {
            public string RecordedVersion { get; init; }
            public string CurrentVersion { get; init; }
            /// <summary>Binaries changed outside rotom; their sources are stale.</summary>
            public List<int> BinaryChanged { get; } = new();
            /// <summary>Sources edited since their last compile; they must be compiled, not regenerated.</summary>
            public List<int> SourceChanged { get; } = new();
            /// <summary>Both changed: only the user can say which side wins.</summary>
            public List<int> BothChanged { get; } = new();
        }

        private static string StatePath => Path.Combine(RotomTool.ProjectRoot, ".rotom", "status", "compile-state.json");

        /// <summary>The version rotom reports, without the "rotom " prefix.</summary>
        public static async Task<string> CurrentVersionAsync()
        {
            var result = await RotomTool.RunAsync("--version").ConfigureAwait(false);
            string text = (result.Stdout ?? "").Trim();
            return text.StartsWith("rotom ", StringComparison.OrdinalIgnoreCase) ? text.Substring(6).Trim() : text;
        }

        /// <summary>What an upgrade has to do, or null when the project is already on this rotom.</summary>
        public static async Task<Assessment> AssessAsync()
        {
            if (!RomInfo.hasRotomProject || !RotomTool.IsAvailable || !File.Exists(StatePath)) return null;
            string current = await CurrentVersionAsync().ConfigureAwait(false);

            var recordedScripts = RecordedScripts(out string recorded);
            if (string.Equals(recorded, current, StringComparison.Ordinal)) return null;

            var assessment = new Assessment { RecordedVersion = recorded, CurrentVersion = current };
            foreach (var script in recordedScripts)
            {
                bool binaryChanged = Hash(script.Binary) != script.OutputHash;
                bool sourceChanged = Hash(script.Source) != script.SourceHash;
                if (binaryChanged && sourceChanged) assessment.BothChanged.Add(script.Id);
                else if (binaryChanged) assessment.BinaryChanged.Add(script.Id);
                else if (sourceChanged) assessment.SourceChanged.Add(script.Id);
            }
            return assessment;
        }

        /// <summary>A script rotom's compile state records, with the hashes of its last compile.</summary>
        internal sealed record RecordedScript(int Id, string Source, string Binary, ulong SourceHash, ulong OutputHash);

        /// <summary>The scripts the project's compile state records whose source and binary both exist.</summary>
        internal static List<RecordedScript> RecordedScripts(out string compilerVersion)
        {
            compilerVersion = null;
            var scripts = new List<RecordedScript>();
            if (!File.Exists(StatePath)) return scripts;
            using var doc = JsonDocument.Parse(File.ReadAllText(StatePath));
            var state = doc.RootElement;
            compilerVersion = state.TryGetProperty("compiler_version", out var v) ? v.GetString() : null;
            if (!state.TryGetProperty("entries", out var entries)) return scripts;

            foreach (var entry in entries.EnumerateObject())
            {
                string source = Path.Combine(RotomTool.ProjectRoot, entry.Name.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar));
                if (!int.TryParse(Path.GetFileNameWithoutExtension(source), out int id)) continue;
                string binary = Filesystem.GetScriptPath(id);
                if (!File.Exists(source) || !File.Exists(binary)) continue;
                scripts.Add(new RecordedScript(id, source, binary,
                    entry.Value.GetProperty("source_hash").GetUInt64(), entry.Value.GetProperty("output_hash").GetUInt64()));
            }
            return scripts;
        }

        internal static ulong Hash(string path) => XxHash3.HashToUInt64(File.ReadAllBytes(path));

        /// <summary>
        /// Regenerates the stale sources, then compiles, which rebuilds every binary on the new rotom from
        /// sources that now match them. Where both sides changed, <paramref name="keepBinaries"/> decides.
        /// With <paramref name="regenerateAll"/>, every source is then regenerated once more so the whole
        /// project reads in the new rotom's style; all of them are backed up first. Returns null, or why
        /// it stopped, and the scripts whose previous binaries the compile kept.
        /// </summary>
        public static async Task<(string Problem, List<int> Kept)> ApplyAsync(Assessment assessment, bool keepBinaries, bool regenerateAll)
        {
            var regenerate = assessment.BinaryChanged.Concat(keepBinaries ? assessment.BothChanged : Enumerable.Empty<int>()).ToList();
            if (regenerate.Count > 0)
            {
                await ScriptSourceSync.RefreshAsync(regenerate).ConfigureAwait(false);
                await ScriptSourceSync.WhenIdleAsync().ConfigureAwait(false);
            }

            var compiled = await RotomTool.CompileProjectAsync().ConfigureAwait(false);
            var kept = compiled.KeptBinaries;
            if (!compiled.Success) return ("The project did not compile on the new rotom: " + RotomTool.FormatResult(compiled), kept);

            if (!regenerateAll) return (null, kept);
            string root = RotomTool.ProjectRoot;
            string sources = Path.Combine(root, "expanded", "scripts");
            string backup = Path.Combine(root, ".rotom", "backups", "dspre-upgrade-" + DateTime.Now.ToString("yyyyMMddHHmmss"));
            Directory.CreateDirectory(backup);
            foreach (string file in Directory.GetFiles(sources, "*", SearchOption.AllDirectories))
            {
                string dest = Path.Combine(backup, Path.GetRelativePath(sources, file));
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                File.Copy(file, dest, overwrite: true);
            }
            var decompiled = await RotomTool.RunAsync("decompile").ConfigureAwait(false);
            return (decompiled.Success ? null : "Regenerating the sources failed, the originals are in " + backup + ": " + RotomTool.FormatResult(decompiled), kept);
        }
    }
}
