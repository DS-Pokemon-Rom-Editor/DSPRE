using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Watches a project's script sources and text archive JSON for edits made outside DSPRE, such as in
    /// VS Code. A changed script source is compiled right away, as the Script Editor's Save does, and a
    /// changed archive is announced so an open editor reloads it. DSPRE's own writes are skipped.
    /// </summary>
    public static class ProjectSourceWatcher
    {
        /// <summary>Raised after outside script edits were compiled, with the sources that changed.</summary>
        public static event Action<RotomTool.Result, IReadOnlyList<string>> ScriptsCompiled;

        private static readonly object Gate = new object();
        private static readonly HashSet<string> Pending = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, DateTime> OwnWrites = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private static readonly TimeSpan OwnWriteWindow = TimeSpan.FromSeconds(3);
        private static FileSystemWatcher _watcher;
        private static Timer _settle;
        private static string _scriptsDir, _textDir;
        private static bool _subscribed;
        private static int _holds;
        private static DateTime _quietUntil;

        /// <summary>Starts watching the loaded project, replacing any earlier one.</summary>
        public static void Start()
        {
            Stop();
            if (string.IsNullOrEmpty(RomInfo.dspreDir)) return;
            string expanded = Path.GetFullPath(Path.Combine(RomInfo.dspreDir, "expanded"));
            if (!Directory.Exists(expanded)) return;

            _scriptsDir = Path.Combine(expanded, "scripts");
            _textDir = Path.GetFullPath(TextConverter.GetExpandedFolderPath());
            if (!_subscribed)
            {
                TextArchive.Saved += (_, id) => ExpectArchive(id);
                _subscribed = true;
            }

            var watcher = new FileSystemWatcher(expanded)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                InternalBufferSize = 64 * 1024,
            };
            watcher.Changed += (_, e) => Note(e.FullPath);
            watcher.Created += (_, e) => Note(e.FullPath);
            watcher.Renamed += (_, e) => Note(e.FullPath);
            watcher.Error += (_, e) => AppLogger.Warn("Watching the project's sources stopped: " + e.GetException()?.Message);
            watcher.EnableRaisingEvents = true;
            lock (Gate) _watcher = watcher;
        }

        public static void Stop()
        {
            lock (Gate)
            {
                _watcher?.Dispose();
                _watcher = null;
                _settle?.Dispose();
                _settle = null;
                Pending.Clear();
                OwnWrites.Clear();
            }
        }

        /// <summary>Ignores changes while DSPRE runs rotom, which rewrites sources itself, and briefly after. Save ROM still compiles anything missed.</summary>
        public static IDisposable Hold()
        {
            lock (Gate) _holds++;
            return new Release();
        }

        private sealed class Release : IDisposable
        {
            private bool _done;
            public void Dispose()
            {
                if (_done) return;
                _done = true;
                lock (Gate) { _holds--; _quietUntil = DateTime.UtcNow + OwnWriteWindow; }
            }
        }

        /// <summary>Marks a file DSPRE is about to write, so its change is not taken for an outside edit.</summary>
        public static void Expect(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            lock (Gate) OwnWrites[Path.GetFullPath(path)] = DateTime.UtcNow;
        }

        private static void ExpectArchive(int id)
        {
            if (_textDir != null) Expect(Path.Combine(_textDir, id.ToString("D4") + ".json"));
        }

        private static void Note(string path)
        {
            bool script = IsUnder(path, _scriptsDir) && (path.EndsWith(".rotom", StringComparison.OrdinalIgnoreCase)
                                                         || path.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
            bool text = IsUnder(path, _textDir) && path.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
            if (!script && !text) return;
            lock (Gate)
            {
                if (_watcher == null) return;
                Pending.Add(Path.GetFullPath(path));
                // Editors save in several writes; one pass after they settle sees the finished file.
                _settle ??= new Timer(_ => _ = Task.Run(SettledAsync));
                _settle.Change(400, Timeout.Infinite);
            }
        }

        private static bool IsUnder(string path, string dir) =>
            dir != null && Path.GetFullPath(path).StartsWith(dir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

        private static async Task SettledAsync()
        {
            List<string> changed;
            lock (Gate)
            {
                // Saved events can arrive after the file change, so the window counts both ways.
                var now = DateTime.UtcNow;
                bool quiet = _holds > 0 || now < _quietUntil;
                changed = quiet ? new List<string>()
                    : Pending.Where(p => !(OwnWrites.TryGetValue(p, out var at) && now - at < OwnWriteWindow)).ToList();
                Pending.Clear();
                foreach (var old in OwnWrites.Where(kv => now - kv.Value > OwnWriteWindow).Select(kv => kv.Key).ToList())
                    OwnWrites.Remove(old);
            }

            foreach (string path in changed.Where(p => IsUnder(p, _textDir)))
                if (int.TryParse(Path.GetFileNameWithoutExtension(path), out int id))
                    TextArchive.RaiseSaved(null, id);

            var scripts = changed.Where(p => IsUnder(p, _scriptsDir)).ToList();
            if (scripts.Count == 0 || !RomInfo.hasRotomProject || !RotomTool.IsAvailable) return;
            try
            {
                AppLogger.Info("Compiling script sources edited outside DSPRE: " + string.Join(", ", scripts.Select(Path.GetFileName)));
                var result = await RotomTool.CompileProjectAsync().ConfigureAwait(false);
                ScriptsCompiled?.Invoke(result, scripts);
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Compiling outside script edits failed: " + ex.Message);
            }
        }
    }
}
