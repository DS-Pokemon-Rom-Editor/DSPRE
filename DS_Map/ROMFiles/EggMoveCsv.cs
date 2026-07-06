using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.ROMFiles;

namespace DSPRE
{
    /// <summary>Egg-move CSV import/export (extracted from the WinForms DocTool; core, UI-free).</summary>
    public static class EggMoveCsv
    {
        public static bool Export(List<EggMoveEntry> eggMoveData, string filePath, string[] pokeNames, string[] moveNames)
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(filePath))
                {
                    // Write CSV header
                    writer.WriteLine("SpeciesID,SpeciesName,MoveID,MoveName");

                    // Write egg move data
                    foreach (var entry in eggMoveData)
                    {
                        string speciesName = (entry.speciesID >= 0 && entry.speciesID < pokeNames.Length) ? pokeNames[entry.speciesID] : $"SPECIES_{entry.speciesID}";
                        foreach (var moveID in entry.moveIDs)
                        {
                            string moveName = (moveID >= 0 && moveID < moveNames.Length) ? moveNames[moveID] : $"MOVE_{moveID}";
                            writer.WriteLine($"{entry.speciesID},{speciesName},{moveID},{moveName}");
                        }
                    }

                }
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to export egg move data to CSV: {ex.Message}");
                return false;
            }
        }

        public static bool Import(ref List<EggMoveEntry> eggMoveData, string filePath)
        {
            try
            {
                var lines = File.ReadAllLines(filePath);
                var speciesDict = new Dictionary<int, EggMoveEntry>();

                foreach (var line in lines.Skip(1))
                {
                    var values = line.Split(',');
                    if (values.Length < 4) continue;

                    int speciesID = int.Parse(values[0].Trim());
                    int moveID = int.Parse(values[2].Trim());

                    if (!speciesDict.ContainsKey(speciesID))
                    {
                        speciesDict[speciesID] = new EggMoveEntry(speciesID, new List<ushort>());
                    }

                    speciesDict[speciesID].moveIDs.Add((ushort)moveID);
                }

                eggMoveData = speciesDict.Values.ToList();

                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to import egg move data from CSV: {ex.Message}");
                return false;
            }
        }
    }
}
