using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace DSPRE.ROMFiles {
    /// <summary>
    /// Represents a single Great Marsh daily encounter slot.
    /// Structure: 4 bytes total - 2 bytes species ID (little-endian) + 2 bytes padding (00 00)
    /// </summary>
    public class GreatMarshEncounter {
        public ushort Species { get; set; }

        /// <summary>
        /// Padding bytes (always 0x0000 in vanilla games).
        /// </summary>
        public ushort Padding { get; set; }

        public GreatMarshEncounter() {
            Species = 0;
            Padding = 0;
        }

        public GreatMarshEncounter(BinaryReader br) {
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
    /// Represents a Great Marsh encounter group (Pre-National Dex or Post-National Dex).
    /// Each group contains 32 Pokemon slots.
    /// </summary>
    public class GreatMarshEncounterGroup {
        public string Name { get; }
        public string Description { get; }
        public BindingList<GreatMarshEncounter> Encounters { get; }

        public const int SLOTS_PER_GROUP = 32;

        public GreatMarshEncounterGroup(string name, string description) {
            Name = name;
            Description = description;
            Encounters = new BindingList<GreatMarshEncounter>();
        }

        public override string ToString() => Name;
    }

    /// <summary>
    /// Represents the Great Marsh Daily Changing Pokemon encounter data for Diamond, Pearl, and Platinum.
    /// 
    /// The Great Marsh (Safari Zone in Sinnoh) features special Pokemon every day that are 
    /// randomly selected from a list. The random selections are independent for each of the 6 areas.
    /// 
    /// File location: files/arc/encdata_ex.narc (or data/arc/encdata_ex.narc)
    /// 
    /// File indices in the NARC (same for all DPPt versions):
    /// - Post-National Dex: encdata_ex_9.bin (index 9)
    /// - Pre-National Dex: encdata_ex_10.bin (index 10)
    /// 
    /// Each file contains 32 slots (4 bytes each = 128 bytes total):
    /// - 2 bytes: Pokemon National Dex ID (little-endian)
    /// - 2 bytes: Padding (always 0x0000)
    /// 
    /// The featured Pokemon replaces two 5% encounter slots from the regular 
    /// Wild Encounters pool in each of the 6 Great Marsh areas.
    /// </summary>
    public class GreatMarshEncounterFile : RomFile {
        public const int ENTRY_SIZE = 4; // 2 bytes species + 2 bytes padding
        public const int SLOTS_PER_GROUP = 32;
        public const int GROUP_COUNT = 2;

        /// <summary>
        /// File index in encdata_ex.narc for Post-National Dex encounters.
        /// </summary>
        public const int POST_NATIONAL_DEX_FILE_INDEX = 9;

        /// <summary>
        /// File index in encdata_ex.narc for Pre-National Dex encounters.
        /// </summary>
        public const int PRE_NATIONAL_DEX_FILE_INDEX = 10;

        public List<GreatMarshEncounterGroup> Groups { get; private set; }

        /// <summary>
        /// Creates a new empty Great Marsh encounter file with proper structure.
        /// </summary>
        public GreatMarshEncounterFile() {
            Groups = new List<GreatMarshEncounterGroup> {
                new GreatMarshEncounterGroup("Post-National Dex", 
                    "Pokemon available after obtaining the National Pokédex.\n" +
                    "These are typically rarer Pokemon from other regions."),
                new GreatMarshEncounterGroup("Pre-National Dex", 
                    "Pokemon available before obtaining the National Pokédex.\n" +
                    "Limited to Sinnoh-native Pokemon.")
            };
        }

        /// <summary>
        /// Loads the Great Marsh encounter files from the unpacked NARC directory.
        /// </summary>
        public GreatMarshEncounterFile(bool load) : this() {
            if (load) {
                LoadFromNarc();
            }
        }

        /// <summary>
        /// Gets the file indices for the two encounter groups.
        /// Index 0 = Post-National Dex (file 9)
        /// Index 1 = Pre-National Dex (file 10)
        /// </summary>
        public static int[] GetFileIndices() {
            return new int[] { POST_NATIONAL_DEX_FILE_INDEX, PRE_NATIONAL_DEX_FILE_INDEX };
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
                        Groups[groupIndex].Encounters.Add(new GreatMarshEncounter());
                    }
                    continue;
                }

                Groups[groupIndex].Encounters.Clear();

                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (BinaryReader br = new BinaryReader(fs)) {
                    for (int slot = 0; slot < SLOTS_PER_GROUP && fs.Position + ENTRY_SIZE <= fs.Length; slot++) {
                        Groups[groupIndex].Encounters.Add(new GreatMarshEncounter(br));
                    }
                }

                // Ensure exactly 32 slots
                while (Groups[groupIndex].Encounters.Count < SLOTS_PER_GROUP) {
                    Groups[groupIndex].Encounters.Add(new GreatMarshEncounter());
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
        /// Saves all Great Marsh encounter groups to the NARC directory.
        /// </summary>
        public bool SaveToNarc(bool showSuccessMessage = true) {
            try {
                string narcDir = Filesystem.encounterExtended;
                if (string.IsNullOrEmpty(narcDir) || !Directory.Exists(narcDir)) {
                    System.Windows.Forms.MessageBox.Show(
                        "Great Marsh encounter directory not found.",
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
                        "Great Marsh encounters saved successfully!",
                        "Success",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Information);
                }
                return true;
            } catch (Exception ex) {
                System.Windows.Forms.MessageBox.Show(
                    $"Error saving Great Marsh encounters: {ex.Message}",
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
                        "Great Marsh encounters exported successfully!",
                        "Success",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Information);
                }
                return true;
            } catch (Exception ex) {
                System.Windows.Forms.MessageBox.Show(
                    $"Error exporting Great Marsh encounters: {ex.Message}",
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
                            group.Encounters.Add(new GreatMarshEncounter(br));
                        }
                        // Ensure exactly 32 slots
                        while (group.Encounters.Count < SLOTS_PER_GROUP) {
                            group.Encounters.Add(new GreatMarshEncounter());
                        }
                    }
                }
                return true;
            } catch (Exception ex) {
                System.Windows.Forms.MessageBox.Show(
                    $"Error importing Great Marsh encounters: {ex.Message}",
                    "Error",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>
        /// Checks if Great Marsh encounters are available (DPPt only, not HGSS).
        /// </summary>
        public static bool IsAvailable() {
            return RomInfo.gameFamily == RomInfo.GameFamilies.DP || 
                   RomInfo.gameFamily == RomInfo.GameFamilies.Plat;
        }
    }
}
