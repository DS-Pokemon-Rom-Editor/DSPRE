using System;
using System.ComponentModel;
using System.IO;

namespace DSPRE.ROMFiles {
    // Trophy Garden's "daily changing Pokemon" pool for Diamond, Pearl, and Platinum.
    // File location: arc/encdata_ex.narc, index 8. 16 slots, 4 bytes each (species + padding),
    // same container and entry format as GreatMarshEncounterFile.
    public class TrophyGardenEncounterFile : RomFile {
        public const int ENTRY_SIZE = 4;
        public const int SLOT_COUNT = 16;
        public const int FILE_INDEX = 8;

        public BindingList<GreatMarshEncounter> Encounters { get; private set; }

        public TrophyGardenEncounterFile() {
            Encounters = new BindingList<GreatMarshEncounter>();
        }

        public TrophyGardenEncounterFile(bool load) : this() {
            if (load) {
                LoadFromNarc();
            }
        }

        private void LoadFromNarc() {
            string narcDir = Filesystem.encounterExtended;
            if (string.IsNullOrEmpty(narcDir) || !Directory.Exists(narcDir)) {
                return;
            }

            string filePath = Filesystem.GetPath(narcDir, FILE_INDEX);
            Encounters.Clear();

            if (File.Exists(filePath)) {
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (BinaryReader br = new BinaryReader(fs)) {
                    for (int slot = 0; slot < SLOT_COUNT && fs.Position + ENTRY_SIZE <= fs.Length; slot++) {
                        Encounters.Add(new GreatMarshEncounter(br));
                    }
                }
            }

            while (Encounters.Count < SLOT_COUNT) {
                Encounters.Add(new GreatMarshEncounter());
            }
        }

        public override byte[] ToByteArray() {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms)) {
                foreach (var encounter in Encounters) {
                    encounter.Write(bw);
                }
                return ms.ToArray();
            }
        }

        public bool SaveToNarc(bool showSuccessMessage = true) {
            try {
                string narcDir = Filesystem.encounterExtended;
                if (string.IsNullOrEmpty(narcDir) || !Directory.Exists(narcDir)) {
                    AppMessages.Error("Trophy Garden encounter directory not found.", "Error");
                    return false;
                }

                string filePath = Filesystem.GetPath(narcDir, FILE_INDEX);
                File.WriteAllBytes(filePath, ToByteArray());

                if (showSuccessMessage) {
                    AppMessages.Info("Trophy Garden encounters saved successfully!", "Success");
                }
                return true;
            } catch (Exception ex) {
                AppMessages.Error($"Error saving Trophy Garden encounters: {ex.Message}", "Error");
                return false;
            }
        }

        public bool ExportToFile(string path, bool showSuccessMessage = true) {
            try {
                File.WriteAllBytes(path, ToByteArray());

                if (showSuccessMessage) {
                    AppMessages.Info("Trophy Garden encounters exported successfully!", "Success");
                }
                return true;
            } catch (Exception ex) {
                AppMessages.Error($"Error exporting Trophy Garden encounters: {ex.Message}", "Error");
                return false;
            }
        }

        public bool ImportFromFile(string path) {
            try {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                using (BinaryReader br = new BinaryReader(fs)) {
                    Encounters.Clear();
                    for (int slot = 0; slot < SLOT_COUNT && fs.Position + ENTRY_SIZE <= fs.Length; slot++) {
                        Encounters.Add(new GreatMarshEncounter(br));
                    }
                    while (Encounters.Count < SLOT_COUNT) {
                        Encounters.Add(new GreatMarshEncounter());
                    }
                }
                return true;
            } catch (Exception ex) {
                AppMessages.Error($"Error importing Trophy Garden encounters: {ex.Message}", "Error");
                return false;
            }
        }

        public static bool IsAvailable() {
            return RomInfo.gameFamily == RomInfo.GameFamilies.DP ||
                   RomInfo.gameFamily == RomInfo.GameFamilies.Plat;
        }
    }
}
