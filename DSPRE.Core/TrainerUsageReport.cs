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
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.trainerProperties, DirNames.trainerParty });

            string[] trcNames = RomInfo.GetTrainerClassNames();
            string[] pokeNames = RomInfo.GetPokemonNames();
            string[] trainerNames = GetSimpleTrainerNames();
            for (int i = 0; i < trcNames.Length; i++)
            {
                trcNames[i] = trcNames[i].Replace("♂", " M").Replace("♀", " F");
            }

            Dictionary<string, Dictionary<string, int>> trainerUsage = new Dictionary<string, Dictionary<string, int>>();

            int trainerCount = Filesystem.GetTrainerPropertiesCount();
            for (int i = 0; i < trainerCount; i++)
            {
                string suffix = Path.DirectorySeparatorChar + i.ToString("D4");

                TrainerFile f;
                using (FileStream propStream = new FileStream(RomInfo.gameDirs[DirNames.trainerProperties].unpackedDir + suffix, FileMode.Open, FileAccess.Read))
                using (FileStream partyStream = new FileStream(RomInfo.gameDirs[DirNames.trainerParty].unpackedDir + suffix, FileMode.Open, FileAccess.Read))
                {
                    f = new TrainerFile(
                        new TrainerProperties((ushort)i, propStream),
                        partyStream,
                        i < trainerNames.Length ? trainerNames[i] : TrainerFile.NAME_NOT_FOUND
                    );
                }

                if (f.party.CountNonEmptyMons() == 0)
                {
                    continue;
                }

                int classId = f.trp.trainerClass;
                string className = classId < trcNames.Length ? trcNames[classId] : $"Class {classId}";


                if (trainerUsage.TryGetValue(className, out Dictionary<string, int> innerDict) == false)
                {
                    innerDict = trainerUsage[className] = new Dictionary<string, int>();
                }

                for (int p = 0; p < f.trp.partyCount && p < TrainerFile.POKE_IN_PARTY; p++)
                {
                    PartyPokemon pp = f.party[p];
                    if (pp.CheckEmpty())
                    {
                        continue;
                    }
                    int species = (int)pp.pokeID;
                    string pokeName = species < pokeNames.Length ? pokeNames[species] : $"Species {species}";

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
            IOrderedEnumerable<string> sortedTrainerClasses = trainerUsage.Keys.OrderBy(className => className);

            using (StreamWriter sw = new StreamWriter(csvFilePath))
            {
                // Write the header row
                sw.WriteLine("Trainer Class;Pokemon Name;Occurrences");

                // Iterate over the sorted trainer class names
                foreach (string className in sortedTrainerClasses)
                {
                    Dictionary<string, int> innerDict = trainerUsage[className];

                    // Sort the Pokemon names alphabetically
                    IOrderedEnumerable<string> sortedPokemonNames = innerDict.Keys.OrderByDescending(pokeName => innerDict[pokeName]);

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
