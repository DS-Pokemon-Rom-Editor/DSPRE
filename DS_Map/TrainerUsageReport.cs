using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE
{
    /// <summary>
    /// "Generate CSV" trainer-usage report (which Pokémon each trainer class uses, with counts).
    /// Extracted from the WinForms main window; core, UI-free.
    /// </summary>
    public static class TrainerUsageReport
    {
        public static void Generate(string csvFilePath)
        {
            string[] trcNames = RomInfo.GetTrainerClassNames();
            string[] pokeNames = RomInfo.GetPokemonNames();
            string[] trainerNames = GetSimpleTrainerNames();
            for (int i = 0; i < trcNames.Length; i++)
            {
                trcNames[i] = trcNames[i].Replace("♂", " M").Replace("♀", " F");
            }

            Dictionary<string, Dictionary<string, int>> trainerUsage = new Dictionary<string, Dictionary<string, int>>();

            for (int i = 0; i < trainerNames.Length; i++)
            {
                if (trainerNames[i].Equals("Angelica") || trainerNames[i].Equals("Mickey"))
                {
                    continue;
                }
                string suffix = Path.DirectorySeparatorChar + i.ToString("D4");

                TrainerFile f = new TrainerFile(
                    new TrainerProperties(
                        (ushort)i,
                        new FileStream(RomInfo.gameDirs[DirNames.trainerProperties].unpackedDir + suffix, FileMode.Open)
                    ),
                    new FileStream(RomInfo.gameDirs[DirNames.trainerParty].unpackedDir + suffix, FileMode.Open),
                    trainerNames[i]
                );

                if (f.party.CountNonEmptyMons() == 0)
                {
                    continue;
                }

                string className = trcNames[f.trp.trainerClass];


                if (trainerUsage.TryGetValue(className, out Dictionary<string, int> innerDict) == false)
                {
                    innerDict = trainerUsage[className] = new Dictionary<string, int>();
                }

                for (int p = 0; p < f.trp.partyCount; p++)
                {
                    PartyPokemon pp = f.party[p];
                    if (pp.CheckEmpty())
                    {
                        continue;
                    }
                    string pokeName = pokeNames[(int)pp.pokeID];

                    if (innerDict.TryGetValue(pokeName, out int occurrences))
                    {
                        innerDict[pokeName]++;
                    }
                    else
                    {
                        innerDict[pokeName] = 1;
                    }
                }
            }

            WriteCsv(trainerUsage, csvFilePath);
        }

        public static void WriteCsv(Dictionary<string, Dictionary<string, int>> trainerUsage, string csvFilePath)
        {
            // Create the StreamWriter to write data to the CSV file
            var sortedTrainerClasses = trainerUsage.Keys.OrderBy(className => className);

            using (StreamWriter sw = new StreamWriter(csvFilePath))
            {
                // Write the header row
                sw.WriteLine("Trainer Class;Pokemon Name;Occurrences");

                // Iterate over the sorted trainer class names
                foreach (string className in sortedTrainerClasses)
                {
                    Dictionary<string, int> innerDict = trainerUsage[className];

                    // Sort the Pokemon names alphabetically
                    var sortedPokemonNames = innerDict.Keys.OrderByDescending(pokeName => innerDict[pokeName]);

                    // Iterate over the sorted mon names
                    foreach (string pokeName in sortedPokemonNames)
                    {
                        int occurrences = innerDict[pokeName];

                        // Write the data row
                        sw.WriteLine($"{className};{pokeName};{occurrences}");
                    }
                    sw.WriteLine($"-;-;-");
                }
            }

            AppLogger.Info("CSV file exported successfully.");
        }
    }
}
