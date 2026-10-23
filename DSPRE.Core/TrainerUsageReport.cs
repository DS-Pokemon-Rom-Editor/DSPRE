using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE
{
    /// <summary>
    /// The trainer usage CSV: which Pokémon each trainer class uses, with counts.
    /// </summary>
    public static class TrainerUsageReport
    {
        public static void Generate(string csvFilePath)
        {
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.TrainerProperties, DirNames.TrainerParty });

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
                using (FileStream propStream = new FileStream(RomInfo.GameDirs[DirNames.TrainerProperties].unpackedDir + suffix, FileMode.Open, FileAccess.Read))
                using (FileStream partyStream = new FileStream(RomInfo.GameDirs[DirNames.TrainerParty].unpackedDir + suffix, FileMode.Open, FileAccess.Read))
                {
                    f = new TrainerFile(
                        new TrainerProperties((ushort)i, propStream),
                        partyStream,
                        i < trainerNames.Length ? trainerNames[i] : TrainerFile.NAME_NOT_FOUND
                    );
                }

                if (f.Party.CountNonEmptyMons() == 0)
                {
                    continue;
                }

                int classId = f.Trp.TrainerClass;
                string className = classId < trcNames.Length ? trcNames[classId] : $"Class {classId}";


                if (trainerUsage.TryGetValue(className, out Dictionary<string, int> innerDict) == false)
                {
                    innerDict = trainerUsage[className] = new Dictionary<string, int>();
                }

                for (int p = 0; p < f.Trp.PartyCount && p < TrainerFile.PokeInParty; p++)
                {
                    PartyPokemon pp = f.Party[p];
                    if (pp.CheckEmpty())
                    {
                        continue;
                    }
                    int species = (int)pp.PokeID;
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
            IOrderedEnumerable<string> sortedTrainerClasses = trainerUsage.Keys.OrderBy(className => className);

            using (StreamWriter sw = new StreamWriter(csvFilePath))
            {
                sw.WriteLine("Trainer class;Pokémon;Count");

                foreach (string className in sortedTrainerClasses)
                {
                    Dictionary<string, int> innerDict = trainerUsage[className];

                    IOrderedEnumerable<string> sortedPokemonNames = innerDict.Keys.OrderByDescending(pokeName => innerDict[pokeName]);

                    foreach (string pokeName in sortedPokemonNames)
                    {
                        int occurrences = innerDict[pokeName];

                        sw.WriteLine($"{className};{pokeName};{occurrences}");
                    }
                    sw.WriteLine($"-;-;-");
                }
            }

            AppLogger.Info("CSV file exported successfully.");
        }
    }
}
