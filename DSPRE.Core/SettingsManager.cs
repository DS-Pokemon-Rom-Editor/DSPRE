using Newtonsoft.Json;
using NSMBe4.DSFileSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MKDS_Course_Editor.NSBTP.NSBTP.NSBTP_File;
using Directory = System.IO.Directory;
using File = System.IO.File;

namespace DSPRE
{

    public class DspreSettings
    {
        public byte menuLayout { get; set; } = 2;
        public string lastColorTablePath { get; set; } = "";
        public bool textEditorPreferHex { get; set; } = false;
        public int scriptEditorFormatPreference { get; set; } = 0;
        // Which of the move-animation editor's three ways of reading a script was last used:
        // 0 guided, 1 script, 2 raw.
        public int moveAnimationViewMode { get; set; } = 0;
        // Flat 2D event view, off by default so the 3D scene stays the first impression.
        public bool eventEditorFlat2D { get; set; } = false;
        public bool mapEditorFlat2D { get; set; } = false;
        public bool renderSpawnables { get; set; } = true;
        public bool renderOverworlds { get; set; } = true;
        public bool renderWarps { get; set; } = true;
        public bool renderTriggers { get; set; } = true;
        public string exportPath { get; set; } = "";
        public string mapImportStarterPoint { get; set; } = "";
        public string openDefaultRom { get; set; } = "";
        public bool neverAskForOpening { get; set; } = false;
        public bool databasesPulled { get; set; } = false;
        public bool automaticallyCheckForUpdates { get; set; } = true;
        public bool automaticallyUpdateDBs { get; set; } = true;
        public bool convertLegacyText { get; set; } = true;
        public string rotomEditorTheme { get; set; } = "OneDark";

        /// <summary>The light or dark skin, so the choice survives closing the program.</summary>
        public bool darkTheme { get; set; } = true;

        /// <summary>Avalonia scale override (0 = use the platform scale).</summary>
        public double uiScale { get; set; } = 0;

        // 3D-view camera behaviour (mouse). Speeds are multipliers (1.0 = default); invert flags flip an axis.
        public float camPanSpeed { get; set; } = 1.0f;
        public float camOrbitSpeed { get; set; } = 1.0f;
        public float camZoomSpeed { get; set; } = 1.0f;
        public bool camInvertPanX { get; set; } = false;
        public bool camInvertPanY { get; set; } = true;
        public bool camInvertOrbitX { get; set; } = false;
        public bool camInvertOrbitY { get; set; } = false;
        public bool camInvertZoom { get; set; } = false;

        /// <summary>Show the Welcome &amp; Tutorial window when the Avalonia shell starts.</summary>
        public bool showWelcomeOnStartup { get; set; } = true;

        /// <summary>Whether the first-time guided tour has already run (or been skipped). While false,
        /// the tour starts automatically after the next successful ROM load in the Avalonia shell.</summary>
        public bool guidedTourShown { get; set; } = false;

        /// <summary>Main-window placement, saved on close and restored at startup (0 = unset).</summary>
        public double mainWindowWidth { get; set; } = 0;
        public double mainWindowHeight { get; set; } = 0;
        public bool mainWindowMaximized { get; set; } = false;

        /// <summary>Most-recently-opened projects (.nds files or extracted folders), newest first.</summary>
        public List<string> recentProjects { get; set; } = new List<string>();
    }

    public static class SettingsManager
    {
        public static DspreSettings Settings { get; private set; }

        private static readonly string SettingsFile = Path.Combine(AppPaths.DspreDataPath, "userSettings.json");

        public static void Load()
        {
            AppLogger.Info("Loading app settings");
            if (System.IO.File.Exists(SettingsFile))
            {
                string json = System.IO.File.ReadAllText(SettingsFile);
                Settings = JsonConvert.DeserializeObject<DspreSettings>(json);
            }
            else
            {
                Settings = new DspreSettings();
                Save();
            }
        }

        public static void Save()
        {
            var directory = Path.GetDirectoryName(SettingsFile);

            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string json = JsonConvert.SerializeObject(Settings, Formatting.Indented);
            File.WriteAllText(SettingsFile, json);
        }

        public const int MaxRecentProjects = 10;

        /// <summary>Puts <paramref name="path"/> (a .nds file or extracted project folder) at the top of the recent list.</summary>
        public static void RecordRecentProject(string path)
        {
            if (Settings == null || string.IsNullOrWhiteSpace(path)) return;
            var list = Settings.recentProjects ?? (Settings.recentProjects = new List<string>());
            list.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            list.Insert(0, path);
            if (list.Count > MaxRecentProjects)
            {
                list.RemoveRange(MaxRecentProjects, list.Count - MaxRecentProjects);
            }
            Save();
        }

        /// <summary>Removes a stale entry (deleted/moved project) from the recent list.</summary>
        public static void RemoveRecentProject(string path)
        {
            if (Settings?.recentProjects == null) return;
            Settings.recentProjects.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            Save();
        }
    }
}
