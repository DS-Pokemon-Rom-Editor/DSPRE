using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace DSPRE.ROMFiles {
    /// <summary>
    /// Represents a Bug Contest encounter entry (BUGMON structure).
    /// File location: data/mushi/mushi_encount.bin (HGSS only)
    /// Structure: 8 bytes per entry
    /// </summary>
    public class BugContestEncounter {
        public ushort Species { get; set; }
        public byte MinLevel { get; set; }
        public byte MaxLevel { get; set; }
        public byte Rate { get; set; }
        public byte Score { get; set; }
        /// <summary>
        /// Believed to be an end-of-encounter-data terminator or padding.
        /// Purpose not fully researched yet.
        /// </summary>
        public ushort Dummy { get; set; }

        public BugContestEncounter() {
            Species = 0;
            MinLevel = 1;
            MaxLevel = 1;
            Rate = 0;
            Score = 0;
            Dummy = 0;
        }

        public BugContestEncounter(BinaryReader br) {
            Species = br.ReadUInt16();
            MinLevel = br.ReadByte();
            MaxLevel = br.ReadByte();
            Rate = br.ReadByte();
            Score = br.ReadByte();
            Dummy = br.ReadUInt16();
        }

        public void Write(BinaryWriter bw) {
            bw.Write(Species);
            bw.Write(MinLevel);
            bw.Write(MaxLevel);
            bw.Write(Rate);
            bw.Write(Score);
            bw.Write(Dummy);
        }

        public override string ToString() {
            string[] pokemonNames = RomInfo.GetPokemonNames();
            string name = Species < pokemonNames.Length ? pokemonNames[Species] : $"Pokemon {Species}";
            return $"{name} (Lv.{MinLevel}-{MaxLevel}, Rate:{Rate}, Score:{Score})";
        }
    }

    /// <summary>
    /// Represents a set of Bug Contest encounters.
    /// Each set contains ENCOUNTERS_PER_SET (10) encounters.
    /// </summary>
    public class BugContestEncounterSet {
        public string Name { get; }
        public string Description { get; }
        public BindingList<BugContestEncounter> Encounters { get; }

        public BugContestEncounterSet(string name, string description) {
            Name = name;
            Description = description;
            Encounters = new BindingList<BugContestEncounter>();
        }

        public override string ToString() => Name;
    }

    /// <summary>
    /// Represents the Bug Contest encounter file for HeartGold/SoulSilver.
    /// Contains 4 sets of 10 encounters each (40 total).
    /// File: data/mushi/mushi_encount.bin
    /// 
    /// Set selection logic from game code:
    /// - Without National Dex: Always Set 0
    /// - With National Dex: set = day_of_week / 2
    ///   - Sunday(0)/Monday(1) -> Set 0
    ///   - Tuesday(2)/Wednesday(3) -> Set 1  
    ///   - Thursday(4)/Friday(5) -> Set 2
    ///   - Saturday(6) -> Set 3
    /// </summary>
    public class BugContestEncounterFile : RomFile {
        public const int ENTRY_SIZE = 8;
        public const int ENCOUNTERS_PER_SET = 10;
        public const int SET_COUNT = 4;
        public const int TOTAL_ENTRIES = SET_COUNT * ENCOUNTERS_PER_SET; // 40

        public List<BugContestEncounterSet> Sets { get; private set; }

        /// <summary>
        /// Gets all encounters as a flat list for backward compatibility.
        /// </summary>
        public BindingList<BugContestEncounter> Encounters {
            get {
                var all = new BindingList<BugContestEncounter>();
                foreach (var set in Sets) {
                    foreach (var enc in set.Encounters) {
                        all.Add(enc);
                    }
                }
                return all;
            }
        }

        /// <summary>
        /// Creates a new empty Bug Contest encounter file with proper structure.
        /// </summary>
        public BugContestEncounterFile() {
            Sets = new List<BugContestEncounterSet> {
                new BugContestEncounterSet("Set 0: No National Dex / Sun-Mon", 
                    "Used when player doesn't have National Dex,\nor with National Dex on Sunday/Monday."),
                new BugContestEncounterSet("Set 1: Nat Dex - Tue/Wed", 
                    "Used with National Dex on Tuesday/Wednesday."),
                new BugContestEncounterSet("Set 2: Nat Dex - Thu/Fri", 
                    "Used with National Dex on Thursday/Friday."),
                new BugContestEncounterSet("Set 3: Nat Dex - Saturday", 
                    "Used with National Dex on Saturday only.")
            };
        }

        /// <summary>
        /// Loads the Bug Contest encounter file from the default path.
        /// </summary>
        public BugContestEncounterFile(bool load) : this() {
            if (load) {
                string path = Filesystem.GetBugContestEncounterPath();
                if (File.Exists(path)) {
                    ParseFile(path);
                }
            }
        }

        /// <summary>
        /// Loads the Bug Contest encounter file from a specific path.
        /// </summary>
        public BugContestEncounterFile(string path) : this() {
            ParseFile(path);
        }

        private void ParseFile(string path) {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (BinaryReader br = new BinaryReader(fs)) {
                // Clear existing encounters in each set
                foreach (var set in Sets) {
                    set.Encounters.Clear();
                }

                int totalEntries = (int)(fs.Length / ENTRY_SIZE);
                int entryIndex = 0;

                // Read entries and distribute to sets
                while (fs.Position + ENTRY_SIZE <= fs.Length && entryIndex < TOTAL_ENTRIES) {
                    int setIndex = entryIndex / ENCOUNTERS_PER_SET;
                    if (setIndex < Sets.Count) {
                        Sets[setIndex].Encounters.Add(new BugContestEncounter(br));
                    }
                    entryIndex++;
                }

                // Ensure each set has exactly 10 entries (fill with empty if needed)
                foreach (var set in Sets) {
                    while (set.Encounters.Count < ENCOUNTERS_PER_SET) {
                        set.Encounters.Add(new BugContestEncounter());
                    }
                }
            }
        }

        public override byte[] ToByteArray() {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms)) {
                // Write all sets in order
                foreach (var set in Sets) {
                    foreach (var encounter in set.Encounters) {
                        encounter.Write(bw);
                    }
                }
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Saves the Bug Contest encounter file to the default path.
        /// </summary>
        public bool SaveToFile(bool showSuccessMessage = true) {
            string path = Filesystem.GetBugContestEncounterPath();
            return SaveToFile(path, showSuccessMessage);
        }

        /// <summary>
        /// Saves the Bug Contest encounter file to a specific path.
        /// </summary>
        public new bool SaveToFile(string path, bool showSuccessMessage = true) {
            try {
                byte[] data = ToByteArray();
                File.WriteAllBytes(path, data);

                if (showSuccessMessage) {
                    System.Windows.Forms.MessageBox.Show(
                        "Bug Contest encounters saved successfully!",
                        "Success",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Information);
                }
                return true;
            } catch (Exception ex) {
                System.Windows.Forms.MessageBox.Show(
                    $"Error saving Bug Contest encounters: {ex.Message}",
                    "Error",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>
        /// Checks if Bug Contest encounters are available (HGSS only).
        /// </summary>
        public static bool IsAvailable() {
            return RomInfo.gameFamily == RomInfo.GameFamilies.HGSS;
        }
    }
}
