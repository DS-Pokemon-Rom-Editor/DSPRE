using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Small editor choices that belong to one project rather than to DSPRE as a whole, such as which header a new
    /// header copies. Kept in <c>dspreDir/dspre_prefs.json</c> so they travel with the extracted ROM, like
    /// dspre_labels.json.
    /// </summary>
    public static class ProjectPrefs
    {
        private static readonly object Gate = new();
        private static Dictionary<string, string> _values = new();
        private static string _loadedFor;

        private static string PathNow => string.IsNullOrEmpty(dspreDir) ? null : Path.Combine(dspreDir, "dspre_prefs.json");

        private static void Ensure()
        {
            string path = PathNow;
            if (path == _loadedFor) return;
            _loadedFor = path;
            _values = new Dictionary<string, string>();
            if (path == null || !File.Exists(path)) return;
            try { _values = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? new Dictionary<string, string>(); }
            catch (Exception ex) { AppLogger.Error("ProjectPrefs: " + ex.Message); }
        }

        public static string Get(string key, string fallback)
        {
            lock (Gate)
            {
                Ensure();
                return _values.TryGetValue(key, out string value) ? value : fallback;
            }
        }

        /// <summary>Stores a choice for this project; does nothing with no project open.</summary>
        public static void Set(string key, string value)
        {
            lock (Gate)
            {
                Ensure();
                string path = PathNow;
                if (path == null) return;
                if (_values.TryGetValue(key, out string old) && old == value) return;
                _values[key] = value;
                try { File.WriteAllText(path, JsonSerializer.Serialize(_values, new JsonSerializerOptions { WriteIndented = true })); }
                catch (Exception ex) { AppLogger.Error("ProjectPrefs: " + ex.Message); }
            }
        }
    }
}
