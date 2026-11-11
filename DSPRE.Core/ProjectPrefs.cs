using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using static DSPRE.RomInfo;

namespace DSPRE
{
    /// <summary>
    /// Small editor choices that belong to one project, not to DSPRE as a whole, such as which header a new
    /// header copies. Kept in <c>dspreDir/dspre_prefs.json</c> so they travel with the extracted ROM, like
    /// dspre_labels.json.
    /// </summary>
    public static class ProjectPrefs
    {
        private static readonly object Gate = new();
        private static Dictionary<string, string> _values = new();
        private static string _loadedFor;

        private static string PathNow => string.IsNullOrEmpty(DspreDir) ? null : Path.Combine(DspreDir, "dspre_prefs.json");

        private static void Ensure()
        {
            string path = PathNow;
            if (path == _loadedFor) return;
            _loadedFor = path;
            _values = new Dictionary<string, string>();
            if (path == null || !File.Exists(path)) return;
            try { _values = PlainFiles.ReadJson<Dictionary<string, string>>(path) ?? new Dictionary<string, string>(); }
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

        public static bool GetBool(string key, bool fallback) => Get(key, fallback ? "true" : "false") == "true";

        public static void SetBool(string key, bool value) => Set(key, value ? "true" : "false");

        public static int GetInt(string key, int fallback) =>
            int.TryParse(Get(key, ""), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int value) ? value : fallback;

        public static void SetInt(string key, int value) => Set(key, value.ToString(System.Globalization.CultureInfo.InvariantCulture));

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
                try { PlainFiles.WriteJson(path, _values); }
                catch (Exception ex) { AppLogger.Error("ProjectPrefs: " + ex.Message); }
            }
        }
    }
}
