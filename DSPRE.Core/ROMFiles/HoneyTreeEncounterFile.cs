using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace DSPRE.ROMFiles
{
    /// <summary>A slot: species (u16) and two zero bytes.</summary>
    public class HoneyTreeEncounter
    {
        public ushort Species { get; set; }

        /// <summary>
        /// Padding bytes (always 0x0000 in vanilla games).
        /// </summary>
        public ushort Padding { get; set; }

        public HoneyTreeEncounter()
        {
            Species = 0;
            Padding = 0;
        }

        public HoneyTreeEncounter(BinaryReader br)
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

    /// <summary>A group of six slots at fixed odds: 40, 20, 20, 10, 5 and 5%.</summary>
    public class HoneyTreeEncounterGroup
    {
        public string Name { get; }
        public string Description { get; }
        public BindingList<HoneyTreeEncounter> Encounters { get; }

        /// <summary>
        /// Fixed encounter rates for each slot (percentages).
        /// </summary>
        public static readonly int[] SlotRates = { 40, 20, 20, 10, 5, 5 };

        public const int SlotsPerGroup = 6;

        public HoneyTreeEncounterGroup(string name, string description)
        {
            Name = name;
            Description = description;
            Encounters = new BindingList<HoneyTreeEncounter>();
        }

        public override string ToString() => Name;
    }

    /// <summary>
    /// Honey tree groups A, B and C (DPPt) in encdata_ex: 2 to 4 in Diamond and Platinum, 5 to 7 in Pearl. Normal trees
    /// pick nothing 10%, A 70% and B 20%; Munchlax trees pick nothing 9%, A 20%, B 70% and C, the Munchlax group, 1%.
    /// </summary>
    /// <remarks>Platinum's files 5 to 7 are copies of 2 to 4 that the game never reads.</remarks>
    public class HoneyTreeEncounterFile : RomFile
    {
        public const int EntrySize = 4; // 2 bytes species + 2 bytes padding
        public const int SlotsPerGroup = 6;
        public const int GroupCount = 3;

        /// <summary>
        /// File indices in encdata_ex.narc for Diamond/Platinum.
        /// </summary>
        public static readonly int[] DiamondPlatinumFileIndices = { 2, 3, 4 };

        /// <summary>
        /// File indices in encdata_ex.narc for Pearl.
        /// </summary>
        public static readonly int[] PearlFileIndices = { 5, 6, 7 };

        public List<HoneyTreeEncounterGroup> Groups { get; private set; }

        public HoneyTreeEncounterFile()
        {
            Groups = new List<HoneyTreeEncounterGroup> {
                new HoneyTreeEncounterGroup("Group A (Common)", 
                    "70% on normal trees, 20% on Munchlax trees"),
                new HoneyTreeEncounterGroup("Group B (Uncommon)", 
                    "20% on normal trees, 70% on Munchlax trees"),
                new HoneyTreeEncounterGroup("Group C (Munchlax)", 
                    "1% on Munchlax trees only. Each player has 4 Munchlax trees, set by Trainer ID.")
            };
        }

        public HoneyTreeEncounterFile(bool load) : this()
        {
            if (load)
            {
                LoadFromNarc();
            }
        }

        /// <summary>The group file numbers for this game version.</summary>
        public static int[] GetFileIndices()
        {
            if (RomInfo.GameVersion == RomInfo.GameVersions.Pearl)
            {
                return PearlFileIndices;
            }
            return DiamondPlatinumFileIndices;
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
                        Groups[groupIndex].Encounters.Add(new HoneyTreeEncounter());
                    }
                    continue;
                }

                Groups[groupIndex].Encounters.Clear();

                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (BinaryReader br = new BinaryReader(fs))
                {
                    for (int slot = 0; slot < SlotsPerGroup && fs.Position + EntrySize <= fs.Length; slot++)
                    {
                        Groups[groupIndex].Encounters.Add(new HoneyTreeEncounter(br));
                    }
                }

                // Exactly 6 slots.
                while (Groups[groupIndex].Encounters.Count < SlotsPerGroup)
                {
                    Groups[groupIndex].Encounters.Add(new HoneyTreeEncounter());
                }
            }
        }

        public override byte[] ToByteArray()
        {
            // Each group saves to its own file.
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                foreach (HoneyTreeEncounterGroup group in Groups)
                {
                    foreach (HoneyTreeEncounter encounter in group.Encounters)
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
                foreach (HoneyTreeEncounter encounter in Groups[groupIndex].Encounters)
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
                    AppMessages.Error("Honey Tree encounter directory not found.", "Special encounters editor");
                    return false;
                }

                int[] fileIndices = GetFileIndices();

                for (int groupIndex = 0; groupIndex < GroupCount; groupIndex++)
                {
                    ArchiveMembers.Put(RomInfo.DirNames.EncounterExtended, fileIndices[groupIndex], GroupToByteArray(groupIndex));
                }

                if (showSuccessMessage)
                {
                    AppMessages.Notice("Honey tree encounters saved.");
                }
                return true;
            }
            catch (Exception ex)
            {
                AppMessages.Error($"Couldn't save Honey Tree encounters: {ex.Message}", "Special encounters editor");
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
                    AppMessages.Notice("Honey tree encounters exported.");
                }
                return true;
            }
            catch (Exception ex)
            {
                AppMessages.Error($"Couldn't export Honey Tree encounters: {ex.Message}", "Special encounters editor");
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
                    foreach (HoneyTreeEncounterGroup group in Groups)
                    {
                        group.Encounters.Clear();
                        for (int slot = 0; slot < SlotsPerGroup && fs.Position + EntrySize <= fs.Length; slot++)
                        {
                            group.Encounters.Add(new HoneyTreeEncounter(br));
                        }
                        // Exactly 6 slots.
                        while (group.Encounters.Count < SlotsPerGroup)
                        {
                            group.Encounters.Add(new HoneyTreeEncounter());
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                AppMessages.Error($"Couldn't import Honey Tree encounters: {ex.Message}", "Special encounters editor");
                return false;
            }
        }

        /// <summary>Whether the game has honey tree encounters (DPPt only).</summary>
        public static bool IsAvailable()
        {
            return RomInfo.GameFamily == RomInfo.GameFamilies.DP || 
                   RomInfo.GameFamily == RomInfo.GameFamilies.Plat;
        }
    }
}
