using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DSPRE.ROMFiles;

namespace DSPRE.Editors {
    public partial class BugContestEncounterEditor : UserControl, IEditorWithUnsavedChanges {
        public bool bugContestEncounterEditorIsReady { get; set; } = false;
        private BugContestEncounterFile bugContestEncounterFile;
        private int currentSetIndex = 0;
        private bool isDirty = false;

        #region IEditorWithUnsavedChanges Implementation
        public bool HasUnsavedChanges => isDirty;
        public string UnsavedChangesDescription => "Bug Contest Encounter Editor";
        public void SaveChanges() => buttonSave_Click(null, null);
        public void DiscardChanges() => SetClean();
        #endregion

        private void SetDirty() {
            isDirty = true;
        }

        private void SetClean() {
            isDirty = false;
        }

        public BugContestEncounterEditor() {
            InitializeComponent();
            SetupTooltips();
        }

        private void SetupTooltips() {
            toolTip1.SetToolTip(buttonSave, "Save changes to the ROM.");
            toolTip1.SetToolTip(buttonExport, "Export encounters to an external file.");
            toolTip1.SetToolTip(buttonImport, "Import encounters from an external file.");
            toolTip1.SetToolTip(buttonLocate, "Open the folder containing the encounter file.");

            toolTip1.SetToolTip(numericUpDownDummy, 
                "This field is read-only. We believe this to be simply the\n" +
                "'end of encounter data' terminator or padding.");

            toolTip1.SetToolTip(buttonRateHelp,
                "Click for detailed explanation of the Rate system.");

            toolTip1.SetToolTip(numericUpDownRate,
                "Rate threshold (0-99). The game rolls 0-99 and checks\n" +
                "entries from top to bottom. First entry where roll >= rate wins.");

            toolTip1.SetToolTip(labelEffectiveRate,
                "Shows the effective encounter rate percentage for the selected entry,\n" +
                "based on its Rate and the Rates of previous entries.");

            toolTip1.SetToolTip(labelDummy,
                "* We believe this to be simply the 'end of encounter data' terminator.\n" +
                "Its exact purpose is not fully researched, but it may be\n" +
                "relevant in future discoveries.");

            toolTip1.SetToolTip(numericUpDownDummy,
                "* We believe this to be simply the 'end of encounter data' terminator.\n" +
                "Its exact purpose is not fully researched, but it may be\n" +
                "relevant in future discoveries.");
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
                SetClean();

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

            UpdateRateDisplay();
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
                numericUpDownRate.Value = Math.Min(numericUpDownRate.Maximum, encounter.Rate);
                numericUpDownScore.Value = encounter.Score;
                numericUpDownDummy.Value = encounter.Dummy;

                UpdatePokemonIcon(encounter.Species);
                UpdateRateDisplay();
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
                labelEffectiveRate.Text = "Effective: ~0%";
                labelEffectiveRate.ForeColor = Color.Gray;
                labelRateWarning.Text = "";
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

            SetDirty();

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
            UpdateRateDisplay();
        }

        private void numericUpDownScore_ValueChanged(object sender, EventArgs e) {
            UpdateSelectedEncounter();
        }

        private void buttonSave_Click(object sender, EventArgs e) {
            if (bugContestEncounterFile == null) return;
            bugContestEncounterFile.SaveToFile();
            SetClean();
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
                    SetDirty();

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

        private void buttonRateHelp_Click(object sender, EventArgs e) {
            string helpText = 
@"=== Bug Contest Rate System ===

The Rate value is a THRESHOLD, not a percentage.

HOW IT WORKS:
1. The game generates a random number from 0 to 99.
2. It checks encounters from TOP to BOTTOM.
3. The FIRST entry where (random >= rate) is selected.

EXAMPLE with rates [80, 60, 40, 20, 0, 0, 0, 0, 0, 0]:
- Roll 85: 85 >= 80 YES -> Entry 0 selected (first match)
- Roll 70: 70 >= 80 NO, 70 >= 60 YES -> Entry 1 selected
- Roll 15: fails entries 0-3, 15 >= 0 YES -> Entry 4 selected

EFFECTIVE RATES (with above example):
- Entry 0 (rate 80): 100-80 = 20% chance
- Entry 1 (rate 60): 80-60 = 20% chance  
- Entry 2 (rate 40): 60-40 = 20% chance
- Entry 3 (rate 20): 40-20 = 20% chance
- Entry 4 (rate 0): 20-0 = 20% chance
- Entries 5-9 (rate 0): 0% (never reached)

WARNING - COMMON MISTAKES:
- Duplicate rates: Only the first entry with that rate triggers.
- Non-descending rates: If entry N has a higher or equal rate
  than N-1, entry N will NEVER trigger (already caught by N-1).

FORMULA:
- First entry (index 0): effective = 100 - rate
- Other entries: effective = rate[previous] - rate[current]
- If result is zero or negative, that entry never triggers.";

            MessageBox.Show(
                helpText,
                "Rate System Help",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        /// <summary>
        /// Calculates the effective encounter rate percentage for a given entry index.
        /// </summary>
        private int CalculateEffectiveRate(int index) {
            if (bugContestEncounterFile == null || currentSetIndex < 0 || currentSetIndex >= bugContestEncounterFile.Sets.Count) {
                return 0;
            }

            var currentSet = bugContestEncounterFile.Sets[currentSetIndex];
            if (index < 0 || index >= currentSet.Encounters.Count) {
                return 0;
            }

            int currentRate = currentSet.Encounters[index].Rate;

            // For the first entry, effective rate is 100 - rate
            // (catches all rolls from rate to 99)
            if (index == 0) {
                return Math.Max(0, 100 - currentRate);
            }

            // For other entries, effective rate is previousRate - currentRate
            // (catches rolls from currentRate to previousRate-1)
            int previousRate = currentSet.Encounters[index - 1].Rate;
            return Math.Max(0, previousRate - currentRate);
        }

        /// <summary>
        /// Validates rates in the current set and returns warning messages.
        /// </summary>
        private string ValidateRates() {
            if (bugContestEncounterFile == null || currentSetIndex < 0 || currentSetIndex >= bugContestEncounterFile.Sets.Count) {
                return "";
            }

            var currentSet = bugContestEncounterFile.Sets[currentSetIndex];
            var warnings = new List<string>();

            // Check for duplicate rates (only first will trigger)
            var rateGroups = currentSet.Encounters
                .Select((enc, idx) => new { enc.Rate, Index = idx })
                .Where(x => x.Rate > 0) // Ignore rate 0 duplicates
                .GroupBy(x => x.Rate)
                .Where(g => g.Count() > 1);

            foreach (var group in rateGroups) {
                var indices = group.Select(x => x.Index + 1).ToArray();
                warnings.Add($"⚠ Rate {group.Key} duplicated at entries {string.Join(", ", indices)} - only first triggers!");
            }

            // Check for non-descending rates (entry will never trigger)
            for (int i = 1; i < currentSet.Encounters.Count; i++) {
                int prevRate = currentSet.Encounters[i - 1].Rate;
                int currRate = currentSet.Encounters[i].Rate;

                if (currRate >= prevRate && currRate > 0) {
                    warnings.Add($"⚠ Entry {i + 1} (rate {currRate}) never triggers - rate must be < {prevRate}.");
                }
            }

            return string.Join("\n", warnings);
        }

        /// <summary>
        /// Updates the effective rate display and validation warnings.
        /// </summary>
        private void UpdateRateDisplay() {
            if (listBoxEncounters.SelectedIndex >= 0) {
                int effectiveRate = CalculateEffectiveRate(listBoxEncounters.SelectedIndex);
                labelEffectiveRate.Text = $"Effective: ~{effectiveRate}%";

                if (effectiveRate == 0) {
                    labelEffectiveRate.ForeColor = Color.DarkRed;
                } else if (effectiveRate < 5) {
                    labelEffectiveRate.ForeColor = Color.DarkOrange;
                } else {
                    labelEffectiveRate.ForeColor = Color.DarkGreen;
                }
            } else {
                labelEffectiveRate.Text = "Effective: ~0%";
                labelEffectiveRate.ForeColor = Color.Gray;
            }

            // Update warnings
            string warnings = ValidateRates();
            labelRateWarning.Text = warnings;
        }
    }
}
