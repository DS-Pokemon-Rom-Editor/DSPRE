using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using DSPRE.ROMFiles;

namespace DSPRE.Editors {
    public partial class GreatMarshEncounterEditor : UserControl, IEditorWithUnsavedChanges {
        public bool greatMarshEncounterEditorIsReady { get; set; } = false;
        private GreatMarshEncounterFile greatMarshEncounterFile;
        private int currentGroupIndex = 0;
        private bool isDirty = false;

        #region IEditorWithUnsavedChanges Implementation
        public bool HasUnsavedChanges => isDirty;
        public string UnsavedChangesDescription => "Great Marsh Encounter Editor";
        public void SaveChanges() => buttonSave_Click(null, null);
        public void DiscardChanges() => SetClean();
        #endregion

        private void SetDirty() {
            isDirty = true;
        }

        private void SetClean() {
            isDirty = false;
        }

        public GreatMarshEncounterEditor() {
            InitializeComponent();
            SetupTooltips();
        }

        private void SetupTooltips() {
            toolTip1.SetToolTip(buttonSave, "Save changes to the ROM.");
            toolTip1.SetToolTip(buttonExport, "Export encounters to an external file.");
            toolTip1.SetToolTip(buttonImport, "Import encounters from an external file.");
            toolTip1.SetToolTip(buttonLocate, "Open the folder containing the encounter files.");
        }

        public void SetupGreatMarshEncounterEditor(bool force = false) {
            if (greatMarshEncounterEditorIsReady && !force) { return; }
            greatMarshEncounterEditorIsReady = true;

            if (!GreatMarshEncounterFile.IsAvailable()) {
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
                    "Great Marsh encounter files not found.\nExpected location: arc/encdata_ex.narc",
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
                greatMarshEncounterFile = new GreatMarshEncounterFile(true);

                Helpers.DisableHandlers();
                try {
                    comboBoxGroup.Items.Clear();
                    foreach (var group in greatMarshEncounterFile.Groups) {
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
                    $"Error loading Great Marsh encounters: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void RefreshGroupDisplay() {
            if (greatMarshEncounterFile == null || currentGroupIndex < 0 || currentGroupIndex >= greatMarshEncounterFile.Groups.Count) {
                return;
            }

            var currentGroup = greatMarshEncounterFile.Groups[currentGroupIndex];

            labelGroupDescription.Text = currentGroup.Description;

            listBoxEncounters.Items.Clear();
            for (int i = 0; i < currentGroup.Encounters.Count; i++) {
                var encounter = currentGroup.Encounters[i];
                listBoxEncounters.Items.Add($"Slot {i:D2}: {encounter}");
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

            if (listBoxEncounters.SelectedIndex < 0 || greatMarshEncounterFile == null) {
                ClearFields();
                return;
            }

            var currentGroup = greatMarshEncounterFile.Groups[currentGroupIndex];
            if (listBoxEncounters.SelectedIndex >= currentGroup.Encounters.Count) {
                ClearFields();
                return;
            }

            var encounter = currentGroup.Encounters[listBoxEncounters.SelectedIndex];

            Helpers.DisableHandlers();
            try {
                comboBoxSpecies.SelectedIndex = encounter.Species < comboBoxSpecies.Items.Count ? encounter.Species : 0;

                // Display the slot index
                int slotIndex = listBoxEncounters.SelectedIndex;
                labelSlotInfo.Text = $"Slot number: {slotIndex:D2}";

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
                labelSlotInfo.Text = "Slot: N/A";
            } finally {
                Helpers.EnableHandlers();
            }
        }

        private void UpdateSelectedEncounter() {
            if (Helpers.HandlersDisabled) return;
            if (greatMarshEncounterFile == null) return;
            if (currentGroupIndex < 0 || currentGroupIndex >= greatMarshEncounterFile.Groups.Count) return;
            if (listBoxEncounters.SelectedIndex < 0) return;

            var currentGroup = greatMarshEncounterFile.Groups[currentGroupIndex];
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
            if (listBoxEncounters.SelectedIndex < 0) return;

            UpdateSelectedEncounter();
            SetDirty();

            // Update icon when species changes
            if (comboBoxSpecies.SelectedIndex >= 0) {
                UpdatePokemonIcon(comboBoxSpecies.SelectedIndex);
            }
        }

        private void buttonSave_Click(object sender, EventArgs e) {
            if (greatMarshEncounterFile == null) return;
            greatMarshEncounterFile.SaveToNarc();
            SetClean();
        }

        private void buttonExport_Click(object sender, EventArgs e) {
            if (greatMarshEncounterFile == null) return;

            SaveFileDialog sfd = new SaveFileDialog {
                Filter = "Binary files (*.bin)|*.bin|All files (*.*)|*.*",
                DefaultExt = "bin",
                FileName = "great_marsh_encounters.bin"
            };

            try {
                sfd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            } catch { }

            if (sfd.ShowDialog() == DialogResult.OK) {
                greatMarshEncounterFile.ExportToFile(sfd.FileName);
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
                    if (greatMarshEncounterFile.ImportFromFile(ofd.FileName)) {
                        // Re-populate group selector
                        Helpers.DisableHandlers();
                        try {
                            comboBoxGroup.Items.Clear();
                            foreach (var group in greatMarshEncounterFile.Groups) {
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
                        SetDirty();

                        MessageBox.Show(
                            "Great Marsh encounters imported successfully!",
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
                    "Great Marsh encounter directory not found.",
                    "Directory Not Found",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void buttonSystemHelp_Click(object sender, EventArgs e) {
            string helpText = 
@"=== Great Marsh Daily Changing Pokemon System ===

The Great Marsh (Sinnoh's Safari Zone) features special Pokemon every day that are randomly selected from this encounter list.

HOW IT WORKS:
• Each day, the game randomly selects Pokemon from this list
• Each of the 6 Great Marsh areas gets an independent random selection
• The selected Pokemon replaces two 5% encounter slots in that area
• Use the binoculars on the 2nd floor of the Safari Zone gate to see which Pokemon are available that day

ENCOUNTER LISTS:
• Post-National Dex (encdata_ex_9.bin):
  Pokemon available after obtaining the National Pokédex.
  Typically includes rarer Pokemon from other regions.

• Pre-National Dex (encdata_ex_10.bin):
  Pokemon available before obtaining the National Pokédex.
  Limited to Sinnoh-native Pokemon.

DATA FORMAT:
• 32 Pokemon slots per list
• Each slot: 4 bytes (2-byte species ID + 2-byte padding)
• Total: 128 bytes per file

GREAT MARSH AREAS:
• Area 1-6 each has independent daily Pokemon selection
• The selection algorithm uses a daily seed value

FILE LOCATIONS:
• Post-National Dex: encdata_ex.narc index 9
• Pre-National Dex: encdata_ex.narc index 10";

            MessageBox.Show(
                helpText,
                "Great Marsh System Help",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
}
