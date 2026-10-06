using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// Puts hg-engine's tables back at the member indices its code reads, by dropping members an earlier
    /// build left behind. Only the data tables in a/0/2/8 are touched; hg-engine's code is in overlay
    /// 129 and nothing here goes near it. DSPRE packs this archive from the unpacked directory in
    /// file-name order, so the kept members are renumbered to a gapless run and their names go on
    /// matching their indices.
    /// </summary>
    public static class HgEngineCodeAddonRepair
    {
        /// <summary>Applies the repair to an unpacked archive directory. The caller owns asking first.</summary>
        public static bool TryRepair(string unpackedDir, IReadOnlyList<int> staleMembers, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(unpackedDir) || !Directory.Exists(unpackedDir))
            { error = "The archive has not been unpacked."; return false; }
            if (staleMembers == null || staleMembers.Count == 0)
            { error = "There is nothing to drop."; return false; }

            string dir = unpackedDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string building = dir + ".repair", previous = dir + ".old";
            try
            {
                // Only the numbered members; anything else left in the folder is not part of the archive.
                List<string> members = Directory.GetFiles(dir)
                    .Where(p => Path.GetFileName(p).Length == 4 && Path.GetFileName(p).All(char.IsDigit))
                    .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                List<string> keep = members.Where((_, i) => !staleMembers.Contains(i)).ToList();
                if (keep.Count == 0) { error = "That would drop every member."; return false; }

                // The new layout is built beside the archive and swapped in whole, so a failure part way
                // leaves the archive as it was.
                if (Directory.Exists(building)) Directory.Delete(building, true);
                Directory.CreateDirectory(building);
                for (int i = 0; i < keep.Count; i++)
                    File.Copy(keep[i], Path.Combine(building, i.ToString("D4")));

                if (Directory.Exists(previous)) Directory.Delete(previous, true);
                Directory.Move(dir, previous);
                try { Directory.Move(building, dir); }
                catch { Directory.Move(previous, dir); throw; }
                Directory.Delete(previous, true);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                AppLogger.Error("HgEngineCodeAddonRepair.TryRepair: " + ex.Message);
                try { if (Directory.Exists(dir) && Directory.Exists(building)) Directory.Delete(building, true); } catch { }
                return false;
            }
        }

        /// <summary>What the repair will do, for the confirmation the user answers.</summary>
        public static string Describe(HgEngineCodeAddons.Layout layout)
        {
            if (layout == null || layout.StaleMembers.Count == 0) return "There is nothing to repair.";

            int kept = layout.Members.Count - layout.StaleMembers.Count;
            return $"Drops {layout.StaleMembers.Count} members left over from earlier builds and keeps {kept}, "
                 + "which puts hg-engine's tables back where its code reads them. The ROM itself only changes "
                 + "when you save it.";
        }
    }
}
