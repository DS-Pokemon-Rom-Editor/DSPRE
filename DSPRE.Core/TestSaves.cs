using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

namespace DSPRE
{
    /// <summary>
    /// The save a project's Build and Run can start from, and putting it where the emulator reads the built ROM's save.
    /// BizHawk looks for &lt;ROM name&gt;.SaveRAM in its NDS save folder, melonDS for &lt;ROM name&gt;.sav beside the ROM.
    /// DeSmuME keeps saves in its own .dsv format, so it is left alone.
    /// </summary>
    public static class TestSaves
    {
        // DeSmuME ends a .dsv with a footer that starts with this line; the raw save is everything before it.
        private const string DesmumeFooter = "|<--Snip above here to create a raw sav by excluding this DeSmuME savedata footer:";

        /// <summary>The save remembered for a project folder, or null.</summary>
        public static string For(string project)
        {
            Dictionary<string, string> saves = SettingsManager.Settings?.TestSaves;
            return project != null && saves != null && saves.TryGetValue(Key(project), out string path) ? path : null;
        }

        /// <summary>Remembers a save for a project folder; null or empty forgets it.</summary>
        public static void Remember(string project, string save)
        {
            DspreSettings settings = SettingsManager.Settings;
            if (settings == null || project == null) return;
            settings.TestSaves ??= new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(save)) settings.TestSaves.Remove(Key(project));
            else settings.TestSaves[Key(project)] = save;
            SettingsManager.Save();
        }

        private static string Key(string project) => project.TrimEnd('\\', '/');

        /// <summary>Whether DSPRE can put a save where this emulator will read it.</summary>
        public static bool Supports(EmulatorKind kind) => kind is EmulatorKind.BizHawk or EmulatorKind.MelonDS;

        /// <summary>The file the emulator reads the ROM's save from, or null when DSPRE doesn't place saves for it.</summary>
        public static string SavePathFor(EmulatorKind kind, string emulatorPath, string romPath)
        {
            string name = Path.GetFileNameWithoutExtension(romPath);
            switch (kind)
            {
                case EmulatorKind.BizHawk:
                    // BizHawk reads an underscore in the ROM's name as a space when it looks for the save.
                    return Path.Combine(BizHawkSaveFolder(emulatorPath), name.Replace('_', ' ') + ".SaveRAM");
                case EmulatorKind.MelonDS:
                    return Path.Combine(Path.GetDirectoryName(romPath) ?? "", name + ".sav");
                default:
                    return null;
            }
        }

        // BizHawk's NDS save folder from its config.ini: the save path inside the NDS base path inside the global base
        // path, each relative to the one around it, with %exe% standing for BizHawk's own folder.
        private static string BizHawkSaveFolder(string emulatorPath)
        {
            string exeDir = Path.GetDirectoryName(emulatorPath) ?? "";
            string global = ".", system = "./NDS", saves = "./SaveRAM";
            try
            {
                string config = Path.Combine(exeDir, "config.ini");
                if (File.Exists(config) && JObject.Parse(File.ReadAllText(config))["PathEntries"]?["Paths"] is JArray paths)
                    foreach (JToken entry in paths)
                    {
                        string type = (string)entry["Type"], sys = (string)entry["System"], path = (string)entry["Path"];
                        if (string.IsNullOrWhiteSpace(path)) continue;
                        if (sys == "Global_NULL" && type == "Base") global = path;
                        else if (sys == "NDS" && type == "Base") system = path;
                        else if (sys == "NDS" && type == "Save RAM") saves = path;
                    }
            }
            catch (Exception ex) { AppLogger.Warn("BizHawk config: " + ex.Message); }
            string Resolve(string path, string from) =>
                Path.GetFullPath(Path.IsPathRooted(path = path.Replace("%exe%", exeDir)) ? path : Path.Combine(from, path));
            return Resolve(saves, Resolve(system, Resolve(global, exeDir)));
        }

        /// <summary>A save file's raw bytes: a DeSmuME .dsv loses its footer, anything else is taken as it is.</summary>
        public static byte[] ReadRaw(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            byte[] marker = Encoding.ASCII.GetBytes(DesmumeFooter);
            int at = bytes.AsSpan().LastIndexOf(marker);
            return at > 0 ? bytes.AsSpan(0, at).ToArray() : bytes;
        }

        /// <summary>
        /// Puts a save where the emulator reads the ROM's save, keeping what was there as .bak. Returns where it went,
        /// or throws with why it couldn't.
        /// </summary>
        public static string Place(EmulatorKind kind, string emulatorPath, string romPath, string savePath)
        {
            string target = SavePathFor(kind, emulatorPath, romPath)
                ?? throw new InvalidOperationException($"DSPRE can't place a save for {Emulators.DisplayName(kind)}.");
            if (!File.Exists(savePath)) throw new FileNotFoundException("The save wasn't found at " + savePath);
            byte[] raw = ReadRaw(savePath);
            Directory.CreateDirectory(Path.GetDirectoryName(target) ?? ".");
            if (File.Exists(target)) PlainFiles.Copy(target, target + ".bak", overwrite: true);
            PlainFiles.WriteBytes(target, raw);
            AppLogger.Info($"Placed test save {savePath} at {target}");
            return target;
        }
    }
}
