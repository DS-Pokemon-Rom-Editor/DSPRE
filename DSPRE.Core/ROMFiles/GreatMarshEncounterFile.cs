using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace DSPRE.ROMFiles
{
    /// <summary>A daily slot: species (u16) and two zero bytes.</summary>
    public class GreatMarshEncounter
    {
        public ushort Species { get; set; }

        /// <summary>
        /// Padding bytes (always 0x0000 in vanilla games).
        /// </summary>
        public ushort Padding { get; set; }

        public GreatMarshEncounter()
        {
            Species = 0;
            Padding = 0;
        }

        public GreatMarshEncounter(BinaryReader br)
        {
            Species = br.ReadUInt16();
            Padding = br.ReadUInt16();
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write(Species);
            bw.Write(Padding);
        }

        public override string ToString()
        {
            string[] pokemonNames = RomInfo.GetPokemonNames();
            string name = Species < pokemonNames.Length ? pokemonNames[Species] : $"Pokémon {Species}";
            return name;
        }
    }

    /// <summary>A group of 32 slots, before or after the National Dex.</summary>
    public class GreatMarshEncounterGroup
    {
        public string Name { get; }
        public string Description { get; }
        public BindingList<GreatMarshEncounter> Encounters { get; }

        public const int SlotsPerGroup = 32;

        public GreatMarshEncounterGroup(string name, string description)
        {
            Name = name;
            Description = description;
            Encounters = new BindingList<GreatMarshEncounter>();
        }

        public override string ToString() => Name;
    }

    /// <summary>
    /// The Great Marsh's daily Pokémon (DPPt) in encdata_ex files 9 (after the National Dex) and 10 (before), 32 slots
    /// each. Each day's pick replaces two 5% slots in each of the six areas.
    /// </summary>
    public class GreatMarshEncounterFile : RomFile
    {
        public const int EntrySize = 4; // 2 bytes species + 2 bytes padding
        public const int SlotsPerGroup = 32;
        public const int GroupCount = 2;

        /// <summary>
        /// File index in encdata_ex.narc for Post-National Dex encounters.
        /// </summary>
        public const int PostNationalDexFileIndex = 9;

        /// <summary>
        /// File index in encdata_ex.narc for Pre-National Dex encounters.
        /// </summary>
        public const int PreNationalDexFileIndex = 10;

        public List<GreatMarshEncounterGroup> Groups { get; private set; }

        public GreatMarshEncounterFile()
        {
            Groups = new List<GreatMarshEncounterGroup> {
                new GreatMarshEncounterGroup("Post-National Dex", 
                    "Pokémon you can find once you have the National Pokédex, mostly from other regions."),
                new GreatMarshEncounterGroup("Pre-National Dex", 
                    "Pokémon you can find before you have the National Pokédex, all from Sinnoh.")
            };
        }

        public GreatMarshEncounterFile(bool load) : this()
        {
            if (load)
            {
                LoadFromNarc();
            }
        }

        /// <summary>Index 0 is after the National Dex (file 9), index 1 before it (file 10).</summary>
        public static int[] GetFileIndices()
        {
            return new int[] { PostNationalDexFileIndex, PreNationalDexFileIndex };
        }

        private void LoadFromNarc()
        {
            string narcDir = Filesystem.EncounterExtended;
            if (string.IsNullOrEmpty(narcDir) || !Directory.Exists(narcDir))
            {
                return;
            }

            int[] fileIndices = GetFileIndices();

            for (int groupIndex = 0; groupIndex < GroupCount; groupIndex++)
            {
                string filePath = Filesystem.GetPath(narcDir, fileIndices[groupIndex]);

                if (!File.Exists(filePath))
                {
                    while (Groups[groupIndex].Encounters.Count < SlotsPerGroup)
                    {
                        Groups[groupIndex].Encounters.Add(new GreatMarshEncounter());
                    }
                    continue;
                }

                Groups[groupIndex].Encounters.Clear();

                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (BinaryReader br = new BinaryReader(fs))
                {
                    for (int slot = 0; slot < SlotsPerGroup && fs.Position + EntrySize <= fs.Length; slot++)
                    {
                        Groups[groupIndex].Encounters.Add(new GreatMarshEncounter(br));
                    }
                }

                // Exactly 32 slots.
                while (Groups[groupIndex].Encounters.Count < SlotsPerGroup)
                {
                    Groups[groupIndex].Encounters.Add(new GreatMarshEncounter());
                }
            }
        }

        public override byte[] ToByteArray()
        {
            // Each group saves to its own file.
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                foreach (GreatMarshEncounterGroup group in Groups)
                {
                    foreach (GreatMarshEncounter encounter in group.Encounters)
                    {
                        encounter.Write(bw);
                    }
                }
                return ms.ToArray();
            }
        }

        public byte[] GroupToByteArray(int groupIndex)
        {
            if (groupIndex < 0 || groupIndex >= Groups.Count)
            {
                return new byte[0];
            }

            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                foreach (GreatMarshEncounter encounter in Groups[groupIndex].Encounters)
                {
                    encounter.Write(bw);
                }
                return ms.ToArray();
            }
        }

        public bool SaveToNarc(bool showSuccessMessage = true)
        {
            try
            {
                string narcDir = Filesystem.EncounterExtended;
                if (string.IsNullOrEmpty(narcDir) || !Directory.Exists(narcDir))
                {
                    AppMessages.Error("Great Marsh encounter directory not found.", "Special encounters editor");
                    return false;
                }

                int[] fileIndices = GetFileIndices();

                for (int groupIndex = 0; groupIndex < GroupCount; groupIndex++)
                {
                    ArchiveMembers.Put(RomInfo.DirNames.EncounterExtended, fileIndices[groupIndex], GroupToByteArray(groupIndex));
                }

                if (showSuccessMessage)
                {
                    AppMessages.Notice("Great Marsh encounters saved.");
                }
                return true;
            }
            catch (Exception ex)
            {
                AppMessages.Error($"Couldn't save Great Marsh encounters: {ex.Message}", "Special encounters editor");
                return false;
            }
        }

        public bool ExportToFile(string path, bool showSuccessMessage = true)
        {
            try
            {
                byte[] data = ToByteArray();
                ArchiveMembers.Write(path, data);

                if (showSuccessMessage)
                {
                    AppMessages.Notice("Great Marsh encounters exported.");
                }
                return true;
            }
            catch (Exception ex)
            {
                AppMessages.Error($"Couldn't export Great Marsh encounters: {ex.Message}", "Special encounters editor");
                return false;
            }
        }

        public bool ImportFromFile(string path)
        {
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                using (BinaryReader br = new BinaryReader(fs))
                {
                    foreach (GreatMarshEncounterGroup group in Groups)
                    {
                        group.Encounters.Clear();
                        for (int slot = 0; slot < SlotsPerGroup && fs.Position + EntrySize <= fs.Length; slot++)
                        {
                            group.Encounters.Add(new GreatMarshEncounter(br));
                        }
                        // Exactly 32 slots.
                        while (group.Encounters.Count < SlotsPerGroup)
                        {
                            group.Encounters.Add(new GreatMarshEncounter());
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                AppMessages.Error($"Couldn't import Great Marsh encounters: {ex.Message}", "Special encounters editor");
                return false;
            }
        }

        /// <summary>Whether the game has Great Marsh encounters (DPPt only).</summary>
        public static bool IsAvailable()
        {
            return RomInfo.GameFamily == RomInfo.GameFamilies.DP || 
                   RomInfo.GameFamily == RomInfo.GameFamilies.Plat;
        }
    }
}
