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
            { error = "The archive hasn't been unpacked."; return false; }
            if (staleMembers == null || staleMembers.Count == 0)
            { error = "There is nothing to drop."; return false; }

            try
            {
                // Only the numbered members; anything else left in the folder is not part of the archive.
                List<string> members = Directory.GetFiles(unpackedDir)
                    .Where(p => Path.GetFileName(p).Length == 4 && Path.GetFileName(p).All(char.IsDigit))
                    .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                List<byte[]> keep = members.Where((_, i) => !staleMembers.Contains(i)).Select(File.ReadAllBytes).ToList();
                if (keep.Count == 0) { error = "That would drop every member."; return false; }

                ArchiveMembers.ReplaceAll(unpackedDir, keep);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                AppLogger.Error("HgEngineCodeAddonRepair.TryRepair: " + ex.Message);
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
