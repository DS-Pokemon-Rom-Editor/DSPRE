using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace DSPRE.ROMFiles {
    /// <summary>
    /// Represents a single Honey Tree encounter slot.
    /// Structure: 2 bytes species ID + 2 bytes padding (00 00)
    /// </summary>
    public class HoneyTreeEncounter {
        public ushort Species { get; set; }

        /// <summary>
        /// Padding bytes (always 0x0000 in vanilla games).
        /// </summary>
        public ushort Padding { get; set; }

        public HoneyTreeEncounter() {
            Species = 0;
            Padding = 0;
        }

        public HoneyTreeEncounter(BinaryReader br) {
            Species = br.ReadUInt16();
            Padding = br.ReadUInt16();
        }

        public void Write(BinaryWriter bw) {
            bw.Write(Species);
            bw.Write(Padding);
        }

        public override string ToString() {
            string[] pokemonNames = RomInfo.GetPokemonNames();
            string name = Species < pokemonNames.Length ? pokemonNames[Species] : $"Pokemon {Species}";
            return name;
        }
    }

    /// <summary>
    /// Represents a Honey Tree encounter group with its 6 slots.
    /// Slot encounter rates are fixed: 40%, 20%, 20%, 10%, 5%, 5%
    /// </summary>
    public class HoneyTreeEncounterGroup {
        public string Name { get; }
        public string Description { get; }
        public BindingList<HoneyTreeEncounter> Encounters { get; }

        /// <summary>
        /// Fixed encounter rates for each slot (percentages).
        /// </summary>
        public static readonly int[] SlotRates = { 40, 20, 20, 10, 5, 5 };

        public const int SLOTS_PER_GROUP = 6;

        public HoneyTreeEncounterGroup(string name, string description) {
            Name = name;
            Description = description;
            Encounters = new BindingList<HoneyTreeEncounter>();
        }

        public override string ToString() => Name;
    }

    /// <summary>
    /// Represents the Honey Tree encounter data for Diamond, Pearl, and Platinum.
    /// 
    /// File location: files/arc/encdata_ex.narc (or data/arc/encdata_ex.narc)
    /// 
    /// File indices in the NARC:
    /// - Diamond/Platinum: Group A = 2, Group B = 3, Group C = 4
    /// - Pearl: Group A = 5, Group B = 6, Group C = 7
    /// 
    /// Note: Platinum has files 5-7 that are copies of 2-4, but the game
    /// always uses 2-4 (version check was copied but doesn't apply).
    /// 
    /// Group selection logic (from decompiled code):
    /// - Normal trees: 10% no encounter, 70% Group A, 20% Group B
    /// - Munchlax trees: 9% no encounter, 20% Group A, 70% Group B, 1% Group C
    /// 
    /// Group C contains Munchlax (only obtainable on player's 4 special trees).
    /// </summary>
    public class HoneyTreeEncounterFile : RomFile {
        public const int ENTRY_SIZE = 4; // 2 bytes species + 2 bytes padding
        public const int SLOTS_PER_GROUP = 6;
        public const int GROUP_COUNT = 3;

        /// <summary>
        /// File indices in encdata_ex.narc for Diamond/Platinum.
        /// </summary>
        public static readonly int[] DiamondPlatinumFileIndices = { 2, 3, 4 };

        /// <summary>
        /// File indices in encdata_ex.narc for Pearl.
        /// </summary>
        public static readonly int[] PearlFileIndices = { 5, 6, 7 };

        public List<HoneyTreeEncounterGroup> Groups { get; private set; }

        /// <summary>
        /// Creates a new empty Honey Tree encounter file with proper structure.
        /// </summary>
        public HoneyTreeEncounterFile() {
            Groups = new List<HoneyTreeEncounterGroup> {
                new HoneyTreeEncounterGroup("Group A (Common)", 
                    "Most common group (70% on normal trees, 20% on Munchlax trees)."),
                new HoneyTreeEncounterGroup("Group B (Uncommon)", 
                    "Uncommon group (20% on normal trees, 70% on Munchlax trees)."),
                new HoneyTreeEncounterGroup("Group C (Munchlax)", 
                    "Rare group containing Munchlax (1% on Munchlax trees only).\n" +
                    "Each player has 4 unique Munchlax trees based on Trainer ID.")
            };
        }

        /// <summary>
        /// Loads the Honey Tree encounter files from the unpacked NARC directory.
        /// </summary>
        public HoneyTreeEncounterFile(bool load) : this() {
            if (load) {
                LoadFromNarc();
            }
        }

        /// <summary>
        /// Gets the file indices for the current game version.
        /// </summary>
        public static int[] GetFileIndices() {
            if (RomInfo.gameVersion == RomInfo.GameVersions.Pearl) {
                return PearlFileIndices;
            }
            return DiamondPlatinumFileIndices;
        }

        private void LoadFromNarc() {
            string narcDir = Filesystem.encounterExtended;
            if (string.IsNullOrEmpty(narcDir) || !Directory.Exists(narcDir)) {
                return;
            }

            int[] fileIndices = GetFileIndices();

            for (int groupIndex = 0; groupIndex < GROUP_COUNT; groupIndex++) {
                string filePath = Filesystem.GetPath(narcDir, fileIndices[groupIndex]);
                
                if (!File.Exists(filePath)) {
                    // Fill with empty encounters if file doesn't exist
                    while (Groups[groupIndex].Encounters.Count < SLOTS_PER_GROUP) {
                        Groups[groupIndex].Encounters.Add(new HoneyTreeEncounter());
                    }
                    continue;
                }

                Groups[groupIndex].Encounters.Clear();

                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (BinaryReader br = new BinaryReader(fs)) {
                    for (int slot = 0; slot < SLOTS_PER_GROUP && fs.Position + ENTRY_SIZE <= fs.Length; slot++) {
                        Groups[groupIndex].Encounters.Add(new HoneyTreeEncounter(br));
                    }
                }

                // Ensure exactly 6 slots
                while (Groups[groupIndex].Encounters.Count < SLOTS_PER_GROUP) {
                    Groups[groupIndex].Encounters.Add(new HoneyTreeEncounter());
                }
            }
        }

        public override byte[] ToByteArray() {
            // This method returns all groups concatenated, but for saving
            // we need to save each group to its respective file.
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms)) {
                foreach (var group in Groups) {
                    foreach (var encounter in group.Encounters) {
                        encounter.Write(bw);
                    }
                }
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Converts a single group to byte array.
        /// </summary>
        public byte[] GroupToByteArray(int groupIndex) {
            if (groupIndex < 0 || groupIndex >= Groups.Count) {
                return new byte[0];
            }

            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms)) {
                foreach (var encounter in Groups[groupIndex].Encounters) {
                    encounter.Write(bw);
                }
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Saves all Honey Tree encounter groups to the NARC directory.
        /// </summary>
        public bool SaveToNarc(bool showSuccessMessage = true) {
            try {
                string narcDir = Filesystem.encounterExtended;
                if (string.IsNullOrEmpty(narcDir) || !Directory.Exists(narcDir)) {
                    System.Windows.Forms.MessageBox.Show(
                        "Honey Tree encounter directory not found.",
                        "Error",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Error);
                    return false;
                }

                int[] fileIndices = GetFileIndices();

                for (int groupIndex = 0; groupIndex < GROUP_COUNT; groupIndex++) {
                    string filePath = Filesystem.GetPath(narcDir, fileIndices[groupIndex]);
                    byte[] data = GroupToByteArray(groupIndex);
                    File.WriteAllBytes(filePath, data);
                }

                if (showSuccessMessage) {
                    System.Windows.Forms.MessageBox.Show(
                        "Honey Tree encounters saved successfully!",
                        "Success",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Information);
                }
                return true;
            } catch (Exception ex) {
                System.Windows.Forms.MessageBox.Show(
                    $"Error saving Honey Tree encounters: {ex.Message}",
                    "Error",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>
        /// Exports all groups to a single file.
        /// </summary>
        public bool ExportToFile(string path, bool showSuccessMessage = true) {
            try {
                byte[] data = ToByteArray();
                File.WriteAllBytes(path, data);

                if (showSuccessMessage) {
                    System.Windows.Forms.MessageBox.Show(
                        "Honey Tree encounters exported successfully!",
                        "Success",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Information);
                }
                return true;
            } catch (Exception ex) {
                System.Windows.Forms.MessageBox.Show(
                    $"Error exporting Honey Tree encounters: {ex.Message}",
                    "Error",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>
        /// Imports all groups from a single file.
        /// </summary>
        public bool ImportFromFile(string path) {
            try {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                using (BinaryReader br = new BinaryReader(fs)) {
                    foreach (var group in Groups) {
                        group.Encounters.Clear();
                        for (int slot = 0; slot < SLOTS_PER_GROUP && fs.Position + ENTRY_SIZE <= fs.Length; slot++) {
                            group.Encounters.Add(new HoneyTreeEncounter(br));
                        }
                        // Ensure exactly 6 slots
                        while (group.Encounters.Count < SLOTS_PER_GROUP) {
                            group.Encounters.Add(new HoneyTreeEncounter());
                        }
                    }
                }
                return true;
            } catch (Exception ex) {
                System.Windows.Forms.MessageBox.Show(
                    $"Error importing Honey Tree encounters: {ex.Message}",
                    "Error",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>
        /// Checks if Honey Tree encounters are available (DPPt only, not HGSS).
        /// </summary>
        public static bool IsAvailable() {
            return RomInfo.gameFamily == RomInfo.GameFamilies.DP || 
                   RomInfo.gameFamily == RomInfo.GameFamilies.Plat;
        }
    }
}
