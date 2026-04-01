using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using DSPRE.ROMFiles;

namespace DSPRE.Editors {
    public partial class HoneyTreeEncounterEditor : UserControl {
        public bool honeyTreeEncounterEditorIsReady { get; set; } = false;
        private HoneyTreeEncounterFile honeyTreeEncounterFile;
        private int currentGroupIndex = 0;

        public HoneyTreeEncounterEditor() {
            InitializeComponent();
            SetupTooltips();
        }

        private void SetupTooltips() {
            toolTip1.SetToolTip(buttonSave, "Save changes to the ROM.");
            toolTip1.SetToolTip(buttonExport, "Export encounters to an external file.");
            toolTip1.SetToolTip(buttonImport, "Import encounters from an external file.");
            toolTip1.SetToolTip(buttonLocate, "Open the folder containing the encounter files.");
        }

        public void SetupHoneyTreeEncounterEditor(bool force = false) {
            if (honeyTreeEncounterEditorIsReady && !force) { return; }
            honeyTreeEncounterEditorIsReady = true;

            if (!HoneyTreeEncounterFile.IsAvailable()) {
                labelNotAvailable.Visible = true;
                panelMain.Visible = false;
                return;
            }

            labelNotAvailable.Visible = false;
            panelMain.Visible = true;

            // Unpack the encounter extended NARC
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.encounterExtended });

            // Verify the directory exists
            if (string.IsNullOrEmpty(Filesystem.encounterExtended) || !Directory.Exists(Filesystem.encounterExtended)) {
                MessageBox.Show(
                    "Honey Tree encounter files not found.\nExpected location: arc/encdata_ex.narc",
                    "Files Not Found",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.monIcons });
            RomInfo.SetMonIconsPalTableAddress();

            string[] pokemonNames = RomInfo.GetPokemonNames();
            comboBoxSpecies.Items.Clear();
            comboBoxSpecies.Items.AddRange(pokemonNames);

            LoadEncounterFile();
        }

        private void LoadEncounterFile() {
            try {
                honeyTreeEncounterFile = new HoneyTreeEncounterFile(true);
                
                Helpers.DisableHandlers();
                try {
                    comboBoxGroup.Items.Clear();
                    foreach (var group in honeyTreeEncounterFile.Groups) {
                        comboBoxGroup.Items.Add(group);
                    }
                    
                    if (comboBoxGroup.Items.Count > 0) {
                        comboBoxGroup.SelectedIndex = 0;
                    }
                } finally {
                    Helpers.EnableHandlers();
                }
                
                currentGroupIndex = 0;
                RefreshGroupDisplay();
                
            } catch (Exception ex) {
                MessageBox.Show(
                    $"Error loading Honey Tree encounters: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void RefreshGroupDisplay() {
            if (honeyTreeEncounterFile == null || currentGroupIndex < 0 || currentGroupIndex >= honeyTreeEncounterFile.Groups.Count) {
                return;
            }

            var currentGroup = honeyTreeEncounterFile.Groups[currentGroupIndex];

            labelGroupDescription.Text = currentGroup.Description;

            listBoxEncounters.Items.Clear();
            for (int i = 0; i < currentGroup.Encounters.Count; i++) {
                var encounter = currentGroup.Encounters[i];
                string rate = i < HoneyTreeEncounterGroup.SlotRates.Length 
                    ? $"{HoneyTreeEncounterGroup.SlotRates[i]}%" 
                    : "?%";
                listBoxEncounters.Items.Add($"Slot {i} ({rate}): {encounter}");
            }

            if (listBoxEncounters.Items.Count > 0) {
                listBoxEncounters.SelectedIndex = 0;
            }
        }

        private void comboBoxGroup_SelectedIndexChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) return;
            
            currentGroupIndex = comboBoxGroup.SelectedIndex;
            RefreshGroupDisplay();
        }

        private void listBoxEncounters_SelectedIndexChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) return;

            if (listBoxEncounters.SelectedIndex < 0 || honeyTreeEncounterFile == null) {
                ClearFields();
                return;
            }

            var currentGroup = honeyTreeEncounterFile.Groups[currentGroupIndex];
            if (listBoxEncounters.SelectedIndex >= currentGroup.Encounters.Count) {
                ClearFields();
                return;
            }

            var encounter = currentGroup.Encounters[listBoxEncounters.SelectedIndex];

            Helpers.DisableHandlers();
            try {
                comboBoxSpecies.SelectedIndex = encounter.Species < comboBoxSpecies.Items.Count ? encounter.Species : 0;
                
                // Display the fixed encounter rate for this slot
                int slotIndex = listBoxEncounters.SelectedIndex;
                if (slotIndex < HoneyTreeEncounterGroup.SlotRates.Length) {
                    labelEncounterRate.Text = $"Encounter Rate: {HoneyTreeEncounterGroup.SlotRates[slotIndex]}%";
                } else {
                    labelEncounterRate.Text = "Encounter Rate: N/A";
                }

                UpdatePokemonIcon(encounter.Species);
            } finally {
                Helpers.EnableHandlers();
            }
        }

        private void UpdatePokemonIcon(int species) {
            try {
                if (species <= 0) {
                    pictureBoxPokemon.Image = Properties.Resources.IconPokeball;
                    return;
                }

                Image icon = DSUtils.GetPokePic(species, pictureBoxPokemon.Width, pictureBoxPokemon.Height);
                if (icon != null) {
                    pictureBoxPokemon.Image = icon;
                } else {
                    pictureBoxPokemon.Image = Properties.Resources.IconPokeball;
                }
            } catch {
                pictureBoxPokemon.Image = Properties.Resources.IconPokeball;
            }
        }

        private void ClearFields() {
            Helpers.DisableHandlers();
            try {
                comboBoxSpecies.SelectedIndex = -1;
                pictureBoxPokemon.Image = null;
                labelEncounterRate.Text = "Encounter Rate: N/A";
            } finally {
                Helpers.EnableHandlers();
            }
        }

        private void UpdateSelectedEncounter() {
            if (Helpers.HandlersDisabled) return;
            if (honeyTreeEncounterFile == null) return;
            if (currentGroupIndex < 0 || currentGroupIndex >= honeyTreeEncounterFile.Groups.Count) return;
            if (listBoxEncounters.SelectedIndex < 0) return;

            var currentGroup = honeyTreeEncounterFile.Groups[currentGroupIndex];
            if (listBoxEncounters.SelectedIndex >= currentGroup.Encounters.Count) return;

            var encounter = currentGroup.Encounters[listBoxEncounters.SelectedIndex];

            encounter.Species = (ushort)(comboBoxSpecies.SelectedIndex >= 0 ? comboBoxSpecies.SelectedIndex : 0);

            // Refresh the display
            int selectedIndex = listBoxEncounters.SelectedIndex;
            RefreshGroupDisplay();
            if (selectedIndex < listBoxEncounters.Items.Count) {
                listBoxEncounters.SelectedIndex = selectedIndex;
            }
        }

        private void comboBoxSpecies_SelectedIndexChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) return;
            
            UpdateSelectedEncounter();
            
            // Update icon when species changes
            if (comboBoxSpecies.SelectedIndex >= 0) {
                UpdatePokemonIcon(comboBoxSpecies.SelectedIndex);
            }
        }

        private void buttonSave_Click(object sender, EventArgs e) {
            if (honeyTreeEncounterFile == null) return;
            honeyTreeEncounterFile.SaveToNarc();
        }

        private void buttonExport_Click(object sender, EventArgs e) {
            if (honeyTreeEncounterFile == null) return;

            SaveFileDialog sfd = new SaveFileDialog {
                Filter = "Binary files (*.bin)|*.bin|All files (*.*)|*.*",
                DefaultExt = "bin",
                FileName = "honey_tree_encounters.bin"
            };

            try {
                sfd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            } catch { }

            if (sfd.ShowDialog() == DialogResult.OK) {
                honeyTreeEncounterFile.ExportToFile(sfd.FileName);
            }
        }

        private void buttonImport_Click(object sender, EventArgs e) {
            OpenFileDialog ofd = new OpenFileDialog {
                Filter = "Binary files (*.bin)|*.bin|All files (*.*)|*.*",
                DefaultExt = "bin"
            };

            try {
                ofd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            } catch { }

            if (ofd.ShowDialog() == DialogResult.OK) {
                try {
                    if (honeyTreeEncounterFile.ImportFromFile(ofd.FileName)) {
                        // Re-populate group selector
                        Helpers.DisableHandlers();
                        try {
                            comboBoxGroup.Items.Clear();
                            foreach (var group in honeyTreeEncounterFile.Groups) {
                                comboBoxGroup.Items.Add(group);
                            }
                            
                            if (comboBoxGroup.Items.Count > 0) {
                                comboBoxGroup.SelectedIndex = 0;
                            }
                        } finally {
                            Helpers.EnableHandlers();
                        }
                        
                        currentGroupIndex = 0;
                        RefreshGroupDisplay();
                        
                        MessageBox.Show(
                            "Honey Tree encounters imported successfully!",
                            "Import Complete",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                } catch (Exception ex) {
                    MessageBox.Show(
                        $"Error importing file: {ex.Message}",
                        "Import Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        private void buttonLocate_Click(object sender, EventArgs e) {
            string path = Filesystem.encounterExtended;
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path)) {
                Helpers.ExplorerSelect(path);
            } else {
                MessageBox.Show(
                    "Honey Tree encounter directory not found.",
                    "Directory Not Found",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void buttonRateHelp_Click(object sender, EventArgs e) {
            string helpText = 
@"=== Honey Tree Encounter System ===

Honey Trees are special trees found across Sinnoh that can be slathered
with Honey to attract wild Pokémon. After 6+ hours, a Pokémon may appear.

ENCOUNTER GROUPS:
• Group A (Common): Standard encounters
• Group B (Uncommon): Slightly rarer encounters  
• Group C (Munchlax): Contains Munchlax (very rare)

GROUP SELECTION RATES:
Normal Trees:
  - 10% No encounter
  - 70% Group A
  - 20% Group B

Munchlax Trees (4 per save, based on Trainer ID):
  - 9% No encounter
  - 20% Group A
  - 70% Group B
  - 1% Group C (Munchlax)

SLOT ENCOUNTER RATES (within each group):
  Slot 0: 40%
  Slot 1: 20%
  Slot 2: 20%
  Slot 3: 10%
  Slot 4: 5%
  Slot 5: 5%

FILE LOCATIONS:
• Diamond/Platinum: encdata_ex_2.bin, encdata_ex_3.bin, encdata_ex_4.bin
• Pearl: encdata_ex_5.bin, encdata_ex_6.bin, encdata_ex_7.bin

Note: Platinum has copies at indices 5-7 but they are unused.";

            MessageBox.Show(
                helpText,
                "Honey Tree System Help",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
}
