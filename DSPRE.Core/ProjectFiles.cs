using System.Collections.Generic;
using System.IO;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using NarcAPI;

namespace DSPRE
{
    public static class ProjectFiles
    {
        // Packs expanded and unpacked files back, since a build reads the packed side.
        public static bool WriteBack(out string error)
        {
            if (!TextArchive.BuildRequiredBins(out string textError)) { error = textError ?? "Rebuilding text archives failed."; return false; }
            if (!ScriptFile.BuildRequiredBins(out string scriptError)) { error = scriptError ?? "Rebuilding script files failed."; return false; }

            foreach (KeyValuePair<RomInfo.DirNames, (string packedDir, string unpackedDir)> kvp in RomInfo.GameDirs)
            {
                // hg-engine builds these from its own source, where DSPRE's edits to them go.
                if (HgEngineDomains.IsOwned(kvp.Key)) continue;
                if (Directory.Exists(kvp.Value.unpackedDir)) Narc.FromFolder(kvp.Value.unpackedDir).Save(kvp.Value.packedDir);
            }
            error = null;
            return true;
        }

        public static bool PackRom(string ndsPath) => DSUtils.RepackROM(ndsPath);

        public static int ConvertToDsRom(string folder) => DSUtils.ConvertNdstoolToDsRom(folder);

        public static string BuildAndRunRomPath()
        {
            string folder = SettingsManager.Settings?.ExportPath;
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                folder = Path.GetDirectoryName(RomInfo.WorkDir.TrimEnd('\\', '/'));
            return Path.Combine(folder, (RomInfo.ProjectName ?? "rom") + " (DSPRE build).nds");
        }
    }
}
