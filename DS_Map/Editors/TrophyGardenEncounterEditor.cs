using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using DSPRE.ROMFiles;

namespace DSPRE.Editors {
    public partial class TrophyGardenEncounterEditor : UserControl, IEditorWithUnsavedChanges {
        public bool trophyGardenEncounterEditorIsReady { get; set; } = false;
        private TrophyGardenEncounterFile trophyGardenEncounterFile;
        private bool isDirty = false;

        #region IEditorWithUnsavedChanges Implementation
        public bool HasUnsavedChanges => isDirty;
        public string UnsavedChangesDescription => "Trophy Garden Encounter Editor";
        public void SaveChanges() => buttonSave_Click(null, null);
        public void DiscardChanges() => SetClean();
        #endregion

        private void SetDirty() {
            isDirty = true;
        }

        private void SetClean() {
            isDirty = false;
        }

        public TrophyGardenEncounterEditor() {
            InitializeComponent();
            SetupTooltips();
        }

        private void SetupTooltips() {
            toolTip1.SetToolTip(buttonSave, "Save changes to the ROM.");
            toolTip1.SetToolTip(buttonExport, "Export encounters to an external file.");
            toolTip1.SetToolTip(buttonImport, "Import encounters from an external file.");
            toolTip1.SetToolTip(buttonLocate, "Open the folder containing the encounter files.");
        }

        public void SetupTrophyGardenEncounterEditor(bool force = false) {
            if (trophyGardenEncounterEditorIsReady && !force) { return; }
            trophyGardenEncounterEditorIsReady = true;

            if (!TrophyGardenEncounterFile.IsAvailable()) {
                labelNotAvailable.Visible = true;
                panelMain.Visible = false;
                return;
            }

            labelNotAvailable.Visible = false;
            panelMain.Visible = true;

            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.encounterExtended });

            if (string.IsNullOrEmpty(Filesystem.encounterExtended) || !Directory.Exists(Filesystem.encounterExtended)) {
                MessageBox.Show(
                    "Trophy Garden encounter files not found.\nExpected location: arc/encdata_ex.narc",
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
                trophyGardenEncounterFile = new TrophyGardenEncounterFile(true);
                RefreshEncounterDisplay();
            } catch (Exception ex) {
                MessageBox.Show(
                    $"Error loading Trophy Garden encounters: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void RefreshEncounterDisplay() {
            if (trophyGardenEncounterFile == null) return;

            listBoxEncounters.Items.Clear();
            for (int i = 0; i < trophyGardenEncounterFile.Encounters.Count; i++) {
                var encounter = trophyGardenEncounterFile.Encounters[i];
                listBoxEncounters.Items.Add($"Slot {i:D2}: {encounter}");
            }

            if (listBoxEncounters.Items.Count > 0) {
                listBoxEncounters.SelectedIndex = 0;
            }
        }

        private void listBoxEncounters_SelectedIndexChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) return;

            if (listBoxEncounters.SelectedIndex < 0 || trophyGardenEncounterFile == null ||
                listBoxEncounters.SelectedIndex >= trophyGardenEncounterFile.Encounters.Count) {
                ClearFields();
                return;
            }

            var encounter = trophyGardenEncounterFile.Encounters[listBoxEncounters.SelectedIndex];

            Helpers.DisableHandlers();
            try {
                comboBoxSpecies.SelectedIndex = encounter.Species < comboBoxSpecies.Items.Count ? encounter.Species : 0;

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
                pictureBoxPokemon.Image = icon ?? Properties.Resources.IconPokeball;
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
            if (trophyGardenEncounterFile == null) return;
            if (listBoxEncounters.SelectedIndex < 0 || listBoxEncounters.SelectedIndex >= trophyGardenEncounterFile.Encounters.Count) return;

            var encounter = trophyGardenEncounterFile.Encounters[listBoxEncounters.SelectedIndex];
            encounter.Species = (ushort)(comboBoxSpecies.SelectedIndex >= 0 ? comboBoxSpecies.SelectedIndex : 0);

            int selectedIndex = listBoxEncounters.SelectedIndex;
            RefreshEncounterDisplay();
            if (selectedIndex < listBoxEncounters.Items.Count) {
                listBoxEncounters.SelectedIndex = selectedIndex;
            }
        }

        private void comboBoxSpecies_SelectedIndexChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) return;
            if (listBoxEncounters.SelectedIndex < 0) return;

            UpdateSelectedEncounter();
            SetDirty();

            if (comboBoxSpecies.SelectedIndex >= 0) {
                UpdatePokemonIcon(comboBoxSpecies.SelectedIndex);
            }
        }

        private void buttonSave_Click(object sender, EventArgs e) {
            if (trophyGardenEncounterFile == null) return;
            trophyGardenEncounterFile.SaveToNarc();
            SetClean();
        }

        private void buttonExport_Click(object sender, EventArgs e) {
            if (trophyGardenEncounterFile == null) return;

            SaveFileDialog sfd = new SaveFileDialog {
                Filter = "Binary files (*.bin)|*.bin|All files (*.*)|*.*",
                DefaultExt = "bin",
                FileName = "trophy_garden_encounters.bin"
            };

            try {
                sfd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            } catch { }

            if (sfd.ShowDialog() == DialogResult.OK) {
                trophyGardenEncounterFile.ExportToFile(sfd.FileName);
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
                    if (trophyGardenEncounterFile.ImportFromFile(ofd.FileName)) {
                        RefreshEncounterDisplay();
                        SetDirty();

                        MessageBox.Show(
                            "Trophy Garden encounters imported successfully!",
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
                    "Trophy Garden encounter directory not found.",
                    "Directory Not Found",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void buttonSystemHelp_Click(object sender, EventArgs e) {
            string helpText =
@"=== Trophy Garden Daily Changing Pokemon System ===

After speaking with Mr. Backlot (Pokemon Mansion, Route 212 North) with the National
Dex obtained, the Trophy Garden starts offering a special Pokemon each day.

HOW IT WORKS:
- Each day, the game randomly picks from this 16-species list (avoiding repeats
  of the currently active picks)
- Up to two daily Pokemon can be active at once
- The active picks replace the Trophy Garden's two 5% grass encounter slots

This editor only changes the pool of 16 possible species. Which ones are active
right now is stored in your save file, not the ROM, so it isn't shown here.

DATA FORMAT:
- 16 Pokemon slots
- Each slot: 4 bytes (2-byte species ID + 2-byte padding)
- Total: 64 bytes

FILE LOCATION:
- encdata_ex.narc index 8";

            MessageBox.Show(
                helpText,
                "Trophy Garden System Help",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
}
