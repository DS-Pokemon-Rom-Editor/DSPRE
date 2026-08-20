using System;
using System.ComponentModel;
using System.IO;

namespace DSPRE.ROMFiles {
    // Battle Tower Pokemon set: species/moves/nature/EVs/item/form. 16 bytes, one file per set.
    public class BattleTowerPokemonSet {
        public ushort Species { get; set; }
        public ushort[] Moves { get; set; } = new ushort[4];
        public byte EvFlags { get; set; }
        public byte Nature { get; set; }
        public ushort Item { get; set; }
        public ushort Form { get; set; }

        public static readonly string[] NatureNames = {
            "Hardy", "Lonely", "Brave", "Adamant", "Naughty",
            "Bold", "Docile", "Relaxed", "Impish", "Lax",
            "Timid", "Hasty", "Serious", "Jolly", "Naive",
            "Modest", "Mild", "Quiet", "Bashful", "Rash",
            "Calm", "Gentle", "Sassy", "Careful", "Quirky"
        };

        public static readonly string[] EvStatNames = { "HP", "Attack", "Defense", "Speed", "Sp. Atk", "Sp. Def" };

        public BattleTowerPokemonSet() { }

        public BattleTowerPokemonSet(BinaryReader br) {
            Species = br.ReadUInt16();
            for (int i = 0; i < 4; i++) {
                Moves[i] = br.ReadUInt16();
            }
            EvFlags = br.ReadByte();
            Nature = br.ReadByte();
            Item = br.ReadUInt16();
            Form = br.ReadUInt16();
        }

        public void Write(BinaryWriter bw) {
            bw.Write(Species);
            for (int i = 0; i < 4; i++) {
                bw.Write(Moves[i]);
            }
            bw.Write(EvFlags);
            bw.Write(Nature);
            bw.Write(Item);
            bw.Write(Form);
        }

        public override string ToString() {
            string[] pokemonNames = RomInfo.GetPokemonNames();
            return Species < pokemonNames.Length ? pokemonNames[Species] : $"Pokemon {Species}";
        }
    }

    public class BattleTowerPokemonSetFile : RomFile {
        public const int ENTRY_SIZE = 16;

        public BindingList<BattleTowerPokemonSet> Sets { get; private set; } = new BindingList<BattleTowerPokemonSet>();

        public BattleTowerPokemonSetFile() { }

        public BattleTowerPokemonSetFile(bool load) {
            if (load) {
                LoadFromNarc();
            }
        }

        private void LoadFromNarc() {
            string narcDir = Filesystem.battleTowerPokemon;
            if (string.IsNullOrEmpty(narcDir) || !Directory.Exists(narcDir)) {
                return;
            }

            Sets.Clear();
            string[] files = Directory.GetFiles(narcDir);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            foreach (string filePath in files) {
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (BinaryReader br = new BinaryReader(fs)) {
                    if (fs.Length < ENTRY_SIZE) continue;
                    Sets.Add(new BattleTowerPokemonSet(br));
                }
            }
        }

        public override byte[] ToByteArray() {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms)) {
                foreach (var set in Sets) {
                    set.Write(bw);
                }
                return ms.ToArray();
            }
        }

        public bool SaveToNarc(bool showSuccessMessage = true) {
            try {
                string narcDir = Filesystem.battleTowerPokemon;
                if (string.IsNullOrEmpty(narcDir) || !Directory.Exists(narcDir)) {
                    System.Windows.Forms.MessageBox.Show(
                        "Battle Tower Pokemon set directory not found.",
                        "Error",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Error);
                    return false;
                }

                for (int i = 0; i < Sets.Count; i++) {
                    string filePath = Filesystem.GetPath(narcDir, i);
                    using (MemoryStream ms = new MemoryStream())
                    using (BinaryWriter bw = new BinaryWriter(ms)) {
                        Sets[i].Write(bw);
                        File.WriteAllBytes(filePath, ms.ToArray());
                    }
                }

                if (showSuccessMessage) {
                    System.Windows.Forms.MessageBox.Show(
                        "Battle Tower Pokemon sets saved successfully!",
                        "Success",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Information);
                }
                return true;
            } catch (Exception ex) {
                System.Windows.Forms.MessageBox.Show(
                    $"Error saving Battle Tower Pokemon sets: {ex.Message}",
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
                        "Battle Tower Pokemon sets exported successfully!",
                        "Success",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Information);
                }
                return true;
            } catch (Exception ex) {
                System.Windows.Forms.MessageBox.Show(
                    $"Error exporting Battle Tower Pokemon sets: {ex.Message}",
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
                    Sets.Clear();
                    while (fs.Position + ENTRY_SIZE <= fs.Length) {
                        Sets.Add(new BattleTowerPokemonSet(br));
                    }
                }
                return true;
            } catch (Exception ex) {
                System.Windows.Forms.MessageBox.Show(
                    $"Error importing Battle Tower Pokemon sets: {ex.Message}",
                    "Error",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
                return false;
            }
        }

        public static bool IsAvailable() {
            return RomInfo.gameDirs.ContainsKey(RomInfo.DirNames.battleTowerPokemon);
        }
    }
}
