using System;
using System.Collections.Generic;
using System.IO;
using DSPRE.ROMFiles;

namespace DSPRE
{
    /// <summary>Writes the egg move table as CSV; the editor's import reads it back through <see cref="Csv.EggMoveTableCsv"/>.</summary>
    public static class EggMoveCsv
    {
        public static bool Export(List<EggMoveEntry> eggMoveData, string filePath, string[] pokeNames, string[] moveNames)
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(filePath))
                    Csv.EggMoveTableCsv.Write(writer, pokeNames, moveNames, eggMoveData);
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to export egg move data to CSV: {ex.Message}");
                return false;
            }
        }
    }
}
