using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using DSPRE.ROMFiles;

namespace DSPRE.Editors {
    public partial class BugContestEncounterEditor : UserControl {
        public bool bugContestEncounterEditorIsReady { get; set; } = false;
        private BugContestEncounterFile bugContestEncounterFile;
        private int currentSetIndex = 0;

        public BugContestEncounterEditor() {
            InitializeComponent();
            SetupTooltips();
        }

        private void SetupTooltips() {
            // Add/Remove buttons are disabled with explanatory tooltips
            toolTip1.SetToolTip(buttonAdd, 
                "Add a new encounter to this set.\n\n" +
                "NOT YET AVAILABLE: Research into how to expand or reduce\n" +
                "the encounter file is not yet finished. Each set must have\n" +
                "exactly 10 encounters until a proper patching method exists.");

            toolTip1.SetToolTip(buttonRemove, 
                "Remove the selected encounter from this set.\n\n" +
                "NOT YET AVAILABLE: Research into how to expand or reduce\n" +
                "the encounter file is not yet finished. Each set must have\n" +
                "exactly 10 encounters until a proper patching method exists.");

            toolTip1.SetToolTip(buttonSave, "Save changes to the ROM.");
            toolTip1.SetToolTip(buttonExport, "Export encounters to an external file.");
            toolTip1.SetToolTip(buttonImport, "Import encounters from an external file.");
            toolTip1.SetToolTip(buttonLocate, "Open the folder containing the encounter file.");

            toolTip1.SetToolTip(numericUpDownDummy, 
                "This field is read-only. We believe this to be simply the\n" +
                "'end of encounter data' terminator or padding.");
        }

        public void SetupBugContestEncounterEditor(bool force = false) {
            if (bugContestEncounterEditorIsReady && !force) { return; }
            bugContestEncounterEditorIsReady = true;

            if (!BugContestEncounterFile.IsAvailable()) {
                labelNotAvailable.Visible = true;
                panelMain.Visible = false;
                return;
            }

            labelNotAvailable.Visible = false;
            panelMain.Visible = true;

            if (!Filesystem.BugContestEncounterFileExists()) {
                MessageBox.Show(
                    "Bug Contest encounter file not found.\nExpected location: data/mushi/mushi_encount.bin",
                    "File Not Found",
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
                bugContestEncounterFile = new BugContestEncounterFile(true);
                
                Helpers.DisableHandlers();
                try {
                    comboBoxSet.Items.Clear();
                    foreach (var set in bugContestEncounterFile.Sets) {
                        comboBoxSet.Items.Add(set);
                    }
                    
                    if (comboBoxSet.Items.Count > 0) {
                        comboBoxSet.SelectedIndex = 0;
                    }
                } finally {
                    Helpers.EnableHandlers();
                }
                
                currentSetIndex = 0;
                RefreshSetDisplay();
                
            } catch (Exception ex) {
                MessageBox.Show(
                    $"Error loading Bug Contest encounters: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void RefreshSetDisplay() {
            if (bugContestEncounterFile == null || currentSetIndex < 0 || currentSetIndex >= bugContestEncounterFile.Sets.Count) {
                return;
            }

            var currentSet = bugContestEncounterFile.Sets[currentSetIndex];
            
            labelSetDescription.Text = currentSet.Description;
            
            listBoxEncounters.DataSource = null;
            listBoxEncounters.DataSource = currentSet.Encounters;
            
            if (listBoxEncounters.Items.Count > 0) {
                listBoxEncounters.SelectedIndex = 0;
            }
        }

        private void comboBoxSet_SelectedIndexChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) return;
            
            currentSetIndex = comboBoxSet.SelectedIndex;
            RefreshSetDisplay();
        }

        private void listBoxEncounters_SelectedIndexChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) return;
            
            if (listBoxEncounters.SelectedIndex < 0 || bugContestEncounterFile == null) {
                ClearFields();
                return;
            }

            var currentSet = bugContestEncounterFile.Sets[currentSetIndex];
            if (listBoxEncounters.SelectedIndex >= currentSet.Encounters.Count) {
                ClearFields();
                return;
            }

            var encounter = currentSet.Encounters[listBoxEncounters.SelectedIndex];
            
            Helpers.DisableHandlers();
            try {
                comboBoxSpecies.SelectedIndex = encounter.Species < comboBoxSpecies.Items.Count ? encounter.Species : 0;
                numericUpDownMinLevel.Value = Math.Max(numericUpDownMinLevel.Minimum, Math.Min(numericUpDownMinLevel.Maximum, encounter.MinLevel));
                numericUpDownMaxLevel.Value = Math.Max(numericUpDownMaxLevel.Minimum, Math.Min(numericUpDownMaxLevel.Maximum, encounter.MaxLevel));
                numericUpDownRate.Value = encounter.Rate;
                numericUpDownScore.Value = encounter.Score;
                numericUpDownDummy.Value = encounter.Dummy;
                
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
                numericUpDownMinLevel.Value = 1;
                numericUpDownMaxLevel.Value = 1;
                numericUpDownRate.Value = 0;
                numericUpDownScore.Value = 0;
                numericUpDownDummy.Value = 0;
                pictureBoxPokemon.Image = null;
            } finally {
                Helpers.EnableHandlers();
            }
        }

        private void UpdateSelectedEncounter() {
            if (Helpers.HandlersDisabled) return;
            if (bugContestEncounterFile == null) return;
            if (currentSetIndex < 0 || currentSetIndex >= bugContestEncounterFile.Sets.Count) return;
            if (listBoxEncounters.SelectedIndex < 0) return;

            var currentSet = bugContestEncounterFile.Sets[currentSetIndex];
            if (listBoxEncounters.SelectedIndex >= currentSet.Encounters.Count) return;

            var encounter = currentSet.Encounters[listBoxEncounters.SelectedIndex];

            encounter.Species = (ushort)(comboBoxSpecies.SelectedIndex >= 0 ? comboBoxSpecies.SelectedIndex : 0);
            encounter.MinLevel = (byte)numericUpDownMinLevel.Value;
            encounter.MaxLevel = (byte)numericUpDownMaxLevel.Value;
            encounter.Rate = (byte)numericUpDownRate.Value;
            encounter.Score = (byte)numericUpDownScore.Value;
            // Dummy/Terminator is read-only, don't update it

            // Refresh the display
            int selectedIndex = listBoxEncounters.SelectedIndex;
            listBoxEncounters.DataSource = null;
            listBoxEncounters.DataSource = currentSet.Encounters;
            listBoxEncounters.SelectedIndex = selectedIndex;
        }

        private void comboBoxSpecies_SelectedIndexChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) return;
            
            UpdateSelectedEncounter();
            
            // Update icon when species changes
            if (comboBoxSpecies.SelectedIndex >= 0) {
                UpdatePokemonIcon(comboBoxSpecies.SelectedIndex);
            }
        }

        private void numericUpDownMinLevel_ValueChanged(object sender, EventArgs e) {
            UpdateSelectedEncounter();
        }

        private void numericUpDownMaxLevel_ValueChanged(object sender, EventArgs e) {
            UpdateSelectedEncounter();
        }

        private void numericUpDownRate_ValueChanged(object sender, EventArgs e) {
            UpdateSelectedEncounter();
        }

        private void numericUpDownScore_ValueChanged(object sender, EventArgs e) {
            UpdateSelectedEncounter();
        }

        private void buttonSave_Click(object sender, EventArgs e) {
            if (bugContestEncounterFile == null) return;
            bugContestEncounterFile.SaveToFile();
        }

        private void buttonExport_Click(object sender, EventArgs e) {
            if (bugContestEncounterFile == null) return;

            SaveFileDialog sfd = new SaveFileDialog {
                Filter = "Binary files (*.bin)|*.bin|All files (*.*)|*.*",
                DefaultExt = "bin",
                FileName = "mushi_encount.bin"
            };

            try {
                sfd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            } catch { }

            if (sfd.ShowDialog() == DialogResult.OK) {
                bugContestEncounterFile.SaveToFile(sfd.FileName);
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
                    bugContestEncounterFile = new BugContestEncounterFile(ofd.FileName);
                    
                    // Re-populate set selector
                    Helpers.DisableHandlers();
                    try {
                        comboBoxSet.Items.Clear();
                        foreach (var set in bugContestEncounterFile.Sets) {
                            comboBoxSet.Items.Add(set);
                        }
                        
                        if (comboBoxSet.Items.Count > 0) {
                            comboBoxSet.SelectedIndex = 0;
                        }
                    } finally {
                        Helpers.EnableHandlers();
                    }
                    
                    currentSetIndex = 0;
                    RefreshSetDisplay();
                    
                    MessageBox.Show(
                        "Bug Contest encounters imported successfully!",
                        "Import Complete",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
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
            string path = Filesystem.GetBugContestEncounterPath();
            if (File.Exists(path)) {
                Helpers.ExplorerSelect(path);
            } else {
                MessageBox.Show(
                    "Bug Contest encounter file not found.",
                    "File Not Found",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
    }
}
