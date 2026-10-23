using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace DSPRE
{
    public enum EmulatorKind { BizHawk, DeSmuME, MelonDS }

    /// <summary>Starts a desktop DS emulator on a ROM, and remembers which one the user runs.</summary>
    public static class Emulators
    {
        public static readonly EmulatorKind[] All = { EmulatorKind.BizHawk, EmulatorKind.DeSmuME, EmulatorKind.MelonDS };

        public static string DisplayName(EmulatorKind kind) => kind switch
        {
            EmulatorKind.BizHawk => "BizHawk",
            EmulatorKind.DeSmuME => "DeSmuME",
            _ => "melonDS",
        };

        /// <summary>Which emulator an executable looks like, from its file name.</summary>
        public static EmulatorKind? Guess(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path ?? "").ToLowerInvariant();
            if (name.Contains("emuhawk") || name.Contains("bizhawk")) return EmulatorKind.BizHawk;
            if (name.Contains("desmume")) return EmulatorKind.DeSmuME;
            if (name.Contains("melon")) return EmulatorKind.MelonDS;
            return null;
        }

        /// <summary>The preferred emulator and its path, or null when none is set or its file has gone.</summary>
        public static (EmulatorKind Kind, string Path)? Preferred()
        {
            DspreSettings settings = SettingsManager.Settings;
            if (settings == null || !Enum.TryParse(settings.PreferredEmulator, out EmulatorKind kind)) return null;
            string path = PathFor(kind);
            return Exists(path) ? (kind, path) : null;
        }

        public static string PathFor(EmulatorKind kind)
        {
            Dictionary<string, string> paths = SettingsManager.Settings?.EmulatorPaths;
            return paths != null && paths.TryGetValue(kind.ToString(), out string path) ? path : null;
        }

        /// <summary>Remembers where an emulator is, and optionally makes it the one Build and Run uses without asking.</summary>
        public static void Remember(EmulatorKind kind, string path, bool preferred)
        {
            DspreSettings settings = SettingsManager.Settings;
            if (settings == null) return;
            settings.EmulatorPaths ??= new Dictionary<string, string>();
            settings.EmulatorPaths[kind.ToString()] = path;
            if (preferred) settings.PreferredEmulator = kind.ToString();
            SettingsManager.Save();
        }

        // A macOS app bundle is a folder.
        public static bool Exists(string path) => !string.IsNullOrWhiteSpace(path) && (File.Exists(path) || (path.EndsWith(".app", StringComparison.OrdinalIgnoreCase) && Directory.Exists(path)));

        /// <summary>How to start the emulator on a ROM. All three take the ROM as their only argument.</summary>
        public static ProcessStartInfo StartInfo(string emulatorPath, string romPath)
        {
            ProcessStartInfo info;
            if (OperatingSystem.IsMacOS() && emulatorPath.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
            {
                info = new ProcessStartInfo("open");
                foreach (string arg in new[] { "-a", emulatorPath, "--args", romPath }) info.ArgumentList.Add(arg);
            }
            else
            {
                info = new ProcessStartInfo(emulatorPath);
                info.ArgumentList.Add(romPath);
                info.WorkingDirectory = Path.GetDirectoryName(emulatorPath) ?? "";
            }
            info.UseShellExecute = false;
            return info;
        }

        /// <summary>Starts the emulator on a ROM. Null on success, or why it could not start.</summary>
        public static string Launch(EmulatorKind kind, string emulatorPath, string romPath)
        {
            if (!Exists(emulatorPath)) return $"{DisplayName(kind)} wasn't found at {emulatorPath}.";
            if (!File.Exists(romPath)) return $"The ROM wasn't found at {romPath}.";
            try
            {
                Process.Start(StartInfo(emulatorPath, romPath))?.Dispose();
                AppLogger.Info($"Started {DisplayName(kind)} on {romPath}");
                return null;
            }
            catch (Exception ex)
            {
                return $"{DisplayName(kind)} couldn't be started: {ex.Message}";
            }
        }
    }
}
