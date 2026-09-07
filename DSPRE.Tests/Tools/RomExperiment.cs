using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using NarcAPI;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>
    /// Shared lifecycle for staging a disposable ROM: copy the configured project, point RomInfo at
    /// the copy, edit through the real readers and writers, repack, and clean up.
    ///
    /// Every stager under this folder used to reimplement that, and six of them pinned their output
    /// to one machine's scratch directory. Output location now comes from DSPRE_EXPERIMENT_OUTPUT or
    /// falls back to the system temp directory, so nothing machine-specific belongs in the source.
    ///
    /// A ROM edit is only proven by what the game does with it, so the useful unit is an experiment:
    /// a control ROM plus one or more variants differing in a single field. Open the experiment twice
    /// for that, once without applying the edit; a difference in the running game means nothing
    /// without the control to attribute it against. A sweep helper belongs here once a real sweep
    /// needs one, so that it arrives with a caller exercising it.
    /// </summary>
    public sealed class RomExperiment : IDisposable
    {
        private readonly List<string> _notes = new List<string>();
        private bool _disposed;

        private RomExperiment(string name, string sourceProject, string gameCode, string workPath)
        {
            Name = name;
            SourceProject = sourceProject;
            GameCode = gameCode;
            WorkPath = workPath;
        }

        public string Name { get; }
        public string SourceProject { get; }
        public string GameCode { get; }

        /// <summary>The disposable project copy. Every edit happens here, never in the source.</summary>
        public string WorkPath { get; }

        /// <summary>Where ROMs, manifests and evidence are written. Never inside the project copy.</summary>
        public static string OutputRoot
        {
            get
            {
                string configured = Environment.GetEnvironmentVariable("DSPRE_EXPERIMENT_OUTPUT");
                return string.IsNullOrWhiteSpace(configured)
                    ? Path.Combine(Path.GetTempPath(), "DSPRE", "experiments")
                    : Path.GetFullPath(configured);
            }
        }

        private static string WorkRoot => Path.Combine(Path.GetTempPath(), "DSPRE", "experiments-work");

        /// <summary>
        /// Copies the configured project somewhere disposable and initialises RomInfo against the
        /// copy. Dispose deletes the copy; the operator's project is never written to.
        /// </summary>
        public static RomExperiment Open(string name, string sourceProject, string gameCode)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name is required", nameof(name));
            if (!Directory.Exists(sourceProject))
                throw new InvalidOperationException($"the source project for '{name}' is not available");

            string work = Path.Combine(WorkRoot, $"{Sanitize(name)}-{Guid.NewGuid():N}");
            CopyTree(sourceProject, work);

            SettingsManager.Load();
            new RomInfo(gameCode, work);

            return new RomExperiment(name, sourceProject, gameCode, work);
        }

        public string OutputDirectory
        {
            get
            {
                string dir = Path.Combine(OutputRoot, Sanitize(Name));
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public void Unpack(params DirNames[] archives) =>
            DSUtils.TryUnpackNarcs(new List<DirNames>(archives));

        public void Repack(params DirNames[] archives)
        {
            foreach (var archive in archives)
            {
                var directory = gameDirs[archive];
                Narc.FromFolder(directory.unpackedDir).Save(directory.packedDir);
            }
        }

        /// <summary>
        /// Repacks the project into a ROM under the output directory and returns its path. Throws if
        /// the repack did not produce a file, because a missing ROM is not a runnable fixture.
        /// </summary>
        public string BuildRom(string fileName)
        {
            if (!fileName.EndsWith(".nds", StringComparison.OrdinalIgnoreCase)) fileName += ".nds";
            string rom = Path.Combine(OutputDirectory, fileName);
            if (!DSUtils.RepackROM(rom) || !File.Exists(rom))
                throw new InvalidOperationException($"repack failed for {fileName}");
            Note($"ROM: {fileName}");
            return rom;
        }

        /// <summary>Records a manifest line. Keep values and counts here, never machine paths.</summary>
        public void Note(string line) => _notes.Add(line);

        public void Note(string label, object before, object after) =>
            _notes.Add($"{label}: {before} -> {after}");

        public string WriteManifest(string fileName = null)
        {
            string path = Path.Combine(OutputDirectory, fileName ?? Sanitize(Name) + ".txt");
            File.WriteAllLines(path, new[] { $"{Name} ({GameCode})" }.Concat(_notes));
            return path;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // RomInfo is static and still points at the copy about to be deleted. Leaving it aimed at
            // a directory that no longer exists strands whatever runs next in the same collection, so
            // put it back on the source project before the copy goes.
            try { new RomInfo(GameCode, SourceProject); }
            catch { /* the caller's assertions matter more than tidy static state */ }

            string full = Path.GetFullPath(WorkPath);
            string safeRoot = Path.GetFullPath(WorkRoot).TrimEnd(Path.DirectorySeparatorChar)
                              + Path.DirectorySeparatorChar;
            if (!full.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"refusing to delete outside the work root: {full}");
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
        }

        private static void CopyTree(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(directory.Replace(source, destination));
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
                File.Copy(file, file.Replace(source, destination), overwrite: true);
        }

        private static string Sanitize(string value) =>
            string.Concat((value ?? "unnamed").Select(c =>
                char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '-'));
    }
}
