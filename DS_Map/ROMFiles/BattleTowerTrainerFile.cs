using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace DSPRE.ROMFiles {
    // Battle Tower trainer: class ID + a list of Pokemon-set indices they can randomly
    // draw from. Variable length (4 + 2*SetIDs.Count bytes), one file per trainer.
    public class BattleTowerTrainer {
        public ushort TrainerType { get; set; }
        public List<ushort> SetIDs { get; set; } = new List<ushort>();
        public string Name { get; set; } = "";
        public string[] Messages { get; set; } = new string[3];

        public BattleTowerTrainer() { }

        public static BattleTowerTrainer Read(BinaryReader br) {
            var t = new BattleTowerTrainer();
            t.TrainerType = br.ReadUInt16();
            ushort numSets = br.ReadUInt16();
            for (int i = 0; i < numSets; i++) {
                t.SetIDs.Add(br.ReadUInt16());
            }
            return t;
        }

        public void Write(BinaryWriter bw) {
            bw.Write(TrainerType);
            bw.Write((ushort)SetIDs.Count);
            foreach (ushort id in SetIDs) {
                bw.Write(id);
            }
        }

        public byte[] ToByteArray() {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms)) {
                Write(bw);
                return ms.ToArray();
            }
        }

        public override string ToString() {
            string[] classNames = RomInfo.GetTrainerClassNames();
            string className = TrainerType < classNames.Length ? classNames[TrainerType] : $"Class {TrainerType}";
            return string.IsNullOrEmpty(Name) ? className : $"{className} {Name}";
        }
    }

    public class BattleTowerTrainerFile : RomFile {
        public BindingList<BattleTowerTrainer> Trainers { get; private set; } = new BindingList<BattleTowerTrainer>();

        public BattleTowerTrainerFile() { }

        public BattleTowerTrainerFile(bool load) {
            if (load) {
                LoadFromNarc();
            }
        }

        private void LoadFromNarc() {
            string narcDir = Filesystem.battleTowerTrainers;
            if (string.IsNullOrEmpty(narcDir) || !Directory.Exists(narcDir)) {
                return;
            }

            Trainers.Clear();
            string[] files = Directory.GetFiles(narcDir);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            List<string> names = null;
            List<string> messages = null;
            try { names = new TextArchive(RomInfo.battleTowerTrainerNamesMessageNumber).messages; } catch { }
            try { messages = new TextArchive(RomInfo.battleTowerTrainerMessagesNumber).messages; } catch { }

            for (int i = 0; i < files.Length; i++) {
                using (FileStream fs = new FileStream(files[i], FileMode.Open, FileAccess.Read))
                using (BinaryReader br = new BinaryReader(fs)) {
                    if (fs.Length < 4) continue;
                    BattleTowerTrainer trainer = BattleTowerTrainer.Read(br);

                    if (names != null && i < names.Count) {
                        trainer.Name = names[i];
                    }
                    if (messages != null) {
                        for (int m = 0; m < 3; m++) {
                            int idx = i * 3 + m;
                            trainer.Messages[m] = idx < messages.Count ? messages[idx] : "";
                        }
                    }

                    Trainers.Add(trainer);
                }
            }
        }

        public override byte[] ToByteArray() {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms)) {
                foreach (var trainer in Trainers) {
                    trainer.Write(bw);
                }
                return ms.ToArray();
            }
        }

        public bool SaveToNarc(bool showSuccessMessage = true) {
            try {
                string narcDir = Filesystem.battleTowerTrainers;
                if (string.IsNullOrEmpty(narcDir) || !Directory.Exists(narcDir)) {
                    System.Windows.Forms.MessageBox.Show(
                        "Battle Tower trainer directory not found.",
                        "Error",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Error);
                    return false;
                }

                // Read-modify-write, same as TrainerEditor's own name/class archive saves,
                // so the archive's existing encryption key is preserved.
                TextArchive namesArchive = new TextArchive(RomInfo.battleTowerTrainerNamesMessageNumber);
                TextArchive messagesArchive = new TextArchive(RomInfo.battleTowerTrainerMessagesNumber);

                for (int i = 0; i < Trainers.Count; i++) {
                    string filePath = Filesystem.GetPath(narcDir, i);
                    File.WriteAllBytes(filePath, Trainers[i].ToByteArray());

                    if (i < namesArchive.messages.Count) {
                        namesArchive.messages[i] = Trainers[i].Name ?? "";
                    } else {
                        namesArchive.messages.Add(Trainers[i].Name ?? "");
                    }
                    for (int m = 0; m < 3; m++) {
                        int idx = i * 3 + m;
                        if (idx < messagesArchive.messages.Count) {
                            messagesArchive.messages[idx] = Trainers[i].Messages[m] ?? "";
                        } else {
                            messagesArchive.messages.Add(Trainers[i].Messages[m] ?? "");
                        }
                    }
                }

                namesArchive.SaveToExpandedDir(RomInfo.battleTowerTrainerNamesMessageNumber, showSuccessMessage: false);
                messagesArchive.SaveToExpandedDir(RomInfo.battleTowerTrainerMessagesNumber, showSuccessMessage: false);

                if (showSuccessMessage) {
                    System.Windows.Forms.MessageBox.Show(
                        "Battle Tower trainers saved successfully!",
                        "Success",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Information);
                }
                return true;
            } catch (Exception ex) {
                System.Windows.Forms.MessageBox.Show(
                    $"Error saving Battle Tower trainers: {ex.Message}",
                    "Error",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
                return false;
            }
        }

        public bool ExportToFile(string path, bool showSuccessMessage = true) {
            try {
                File.WriteAllBytes(path, ToByteArray());

                if (showSuccessMessage) {
                    System.Windows.Forms.MessageBox.Show(
                        "Battle Tower trainers exported successfully!",
                        "Success",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Information);
                }
                return true;
            } catch (Exception ex) {
                System.Windows.Forms.MessageBox.Show(
                    $"Error exporting Battle Tower trainers: {ex.Message}",
                    "Error",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
                return false;
            }
        }

        public bool ImportFromFile(string path) {
            try {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                using (BinaryReader br = new BinaryReader(fs)) {
                    var oldTrainers = Trainers;
                    Trainers = new BindingList<BattleTowerTrainer>();
                    while (fs.Position + 4 <= fs.Length) {
                        BattleTowerTrainer trainer = BattleTowerTrainer.Read(br);
                        if (Trainers.Count < oldTrainers.Count) {
                            trainer.Name = oldTrainers[Trainers.Count].Name;
                            trainer.Messages = oldTrainers[Trainers.Count].Messages;
                        }
                        Trainers.Add(trainer);
                    }
                }
                return true;
            } catch (Exception ex) {
                System.Windows.Forms.MessageBox.Show(
                    $"Error importing Battle Tower trainers: {ex.Message}",
                    "Error",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
                return false;
            }
        }

        public static bool IsAvailable() {
            return !RomInfo.isHGE && RomInfo.gameDirs.ContainsKey(RomInfo.DirNames.battleTowerTrainers);
        }
    }
}
