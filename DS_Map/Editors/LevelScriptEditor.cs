using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using static DSPRE.RomInfo;

namespace DSPRE.Editors
{
    public partial class LevelScriptEditor : UserControl
    {
        public bool levelScriptEditorIsReady { get; set; } = false;
        LevelScriptFile _levelScriptFile;
        MainProgram _parent;

        public LevelScriptEditor()
        {
            InitializeComponent();

            toolTip1.SetToolTip(buttonOpenSelectedScript, "Open this script in the Script Editor.\nThis button will be disabled for real level scripts.");
            toolTip1.SetToolTip(buttonOpenHeaderScript, "Open the script of the Header this level script is associated to.\nWill only display the first result!");
            toolTip1.SetToolTip(buttonLocate, "Open the folder containing the selected level script file.");
            toolTip1.SetToolTip(buttonImport, "Import a level script file.\nThis will overwrite the current level script file.");
            toolTip1.SetToolTip(buttonSave, "Save the current level script file.\nThis will overwrite the current level script file.");
            toolTip1.SetToolTip(buttonExport, "Export the current level script file.\nThis will not overwrite the current level script file.");
            toolTip1.SetToolTip(buttonAdd, "Add a new trigger to the current level script file.\nMake sure to fill in the required fields first.");

        }

        public void SetUpLevelScriptEditor(MainProgram parent, bool force = false)
        {
            if (levelScriptEditorIsReady && !force) { return; }
            levelScriptEditorIsReady = true;
            this._parent = parent;
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.scripts }); //12 = scripts Narc Dir
            populate_selectScriptFileComboBox();
        }

        public void OpenLevelScriptEditor(MainProgram parent, int levelScriptID)
        {

            SetUpLevelScriptEditor(parent);

            selectScriptFileComboBox.SelectedIndex = levelScriptID;
            if (EditorPanels.PopoutRegistry.TryGetHost(this, out var host))
            {
                host.Focus();
            }
            else
            {
                EditorPanels.mainTabControl.SelectedTab = EditorPanels.levelScriptEditorTabPage;
            }
        }

        public int AddLevelScript() 
        {
            // need to add a script file through the script editor to keep them in sync
            int newScriptID = _parent.scriptEditor.AddScriptFile();
            var levelScriptFile = new LevelScriptFile();

            // Add a default trigger so the level script is not empty
            var trigger = new MapScreenLoadTrigger(LevelScriptTrigger.MAPCHANGE, 1);
            levelScriptFile.bufferSet.Add(trigger);
            levelScriptFile.write_file(Filesystem.GetScriptPath(newScriptID));

            return newScriptID;
        }
        public void populate_selectScriptFileComboBox(int selectedIndex = 0)
        {
            selectScriptFileComboBox.BeginUpdate();
            selectScriptFileComboBox.Items.Clear();
            int scriptCount = Filesystem.GetScriptCount();
            for (int i = 0; i < scriptCount; i++)
            {
                // ScriptFile currentScriptFile = new ScriptFile(i, true, true);
                // selectScriptFileComboBox.Items.Add(currentScriptFile);
                selectScriptFileComboBox.Items.Add($"Script File {i}");
            }
            selectScriptFileComboBox.EndUpdate();

            selectScriptFileComboBox.SelectedIndex = selectedIndex;
        }

        void disableButtons(bool usurp = false)
        {
            if (!usurp && isEmptyLevelScript())
            {
                enableButtons();
            }
            else
            {
                buttonOpenSelectedScript.Enabled = true;
                buttonOpenHeaderScript.Enabled = false;

                listBoxTriggers.DataSource = null;

                textBoxScriptID.Clear();
                textBoxVariableName.Clear();
                textBoxVariableValue.Clear();

                radioButtonVariableValue.Checked = false;
                radioButtonMapChange.Checked = false;
                radioButtonScreenReset.Checked = false;
                radioButtonLoadGame.Checked = false;

                textBoxScriptID.Enabled = false;

                radioButtonVariableValue.Enabled = false;
                radioButtonMapChange.Enabled = false;
                radioButtonScreenReset.Enabled = false;
                radioButtonLoadGame.Enabled = false;

                radioButtonAuto.Enabled = false;
                radioButtonHex.Enabled = false;
                radioButtonDecimal.Enabled = false;

                buttonImport.Enabled = false;
                buttonSave.Enabled = false;
                buttonExport.Enabled = false;
                checkBoxPadding.Enabled = false;

                buttonAdd.Enabled = false;
                buttonRemove.Enabled = false;

                buttonOpenSelectedScript.Enabled = true;
            }

        }

        void enableButtons()
        {
            buttonOpenHeaderScript.Enabled = true;
            buttonOpenSelectedScript.Enabled = false;

            textBoxScriptID.Enabled = true;
            textBoxVariableName.Enabled = true;
            textBoxVariableValue.Enabled = true;

            radioButtonVariableValue.Enabled = true;
            radioButtonMapChange.Enabled = true;
            radioButtonScreenReset.Enabled = true;
            radioButtonLoadGame.Enabled = true;

            radioButtonAuto.Enabled = true;
            radioButtonHex.Enabled = true;
            radioButtonDecimal.Enabled = true;

            buttonImport.Enabled = true;
            buttonSave.Enabled = true;
            buttonExport.Enabled = true;
            checkBoxPadding.Enabled = true;

            //buttonAdd.Enabled = true;
            //buttonRemove.Enabled = true;

            buttonOpenSelectedScript.Enabled = false;
        }

        void buttonAdd_logic()
        {
            buttonAdd.Enabled = false;

            if (radioButtonVariableValue.Checked)
            {
                if (!string.IsNullOrEmpty(textBoxScriptID.Text) && !string.IsNullOrEmpty(textBoxVariableName.Text) && !string.IsNullOrEmpty(textBoxVariableValue.Text))
                {
                    buttonAdd.Enabled = true;
                }
            }
            else if (radioButtonMapChange.Checked || radioButtonScreenReset.Checked || radioButtonLoadGame.Checked)
            {
                if (!string.IsNullOrEmpty(textBoxScriptID.Text))
                {
                    buttonAdd.Enabled = true;
                }
            }
        }

        private void selectScriptFileComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (selectScriptFileComboBox.SelectedIndex == -1)
            {
                buttonLocate.Enabled = false;
            }
            else
            {
                buttonLocate.Enabled = true;
            }

            disableButtons(true);

            try
            {
                _levelScriptFile = new LevelScriptFile(selectScriptFileComboBox.SelectedIndex);

                listBoxTriggers.DataSource = _levelScriptFile.bufferSet;
                if (listBoxTriggers.Items.Count > 0) { listBoxTriggers.SelectedIndex = 0; }
                // Check for 318767104
                enableButtons();
            }
            catch (InvalidDataException ex)
            { //not a level script
                disableButtons();
                AppLogger.Info(ex.Message);
            }
        }

        void listBoxTriggers_SelectedValueChanged(object sender, EventArgs e)
        {
            if (listBoxTriggers.SelectedItem == null)
            {
                buttonRemove.Enabled = false;
                return;
            }

            if (listBoxTriggers.SelectedItem is MapScreenLoadTrigger mapScreenLoadTrigger)
            {
                if (mapScreenLoadTrigger.triggerType == LevelScriptTrigger.LOADGAME)
                {
                    radioButtonLoadGame.Checked = true;
                }
                else if (mapScreenLoadTrigger.triggerType == LevelScriptTrigger.MAPCHANGE)
                {
                    radioButtonMapChange.Checked = true;
                }
                else if (mapScreenLoadTrigger.triggerType == LevelScriptTrigger.SCREENRESET)
                {
                    radioButtonScreenReset.Checked = true;
                }
            }
            else if (listBoxTriggers.SelectedItem is VariableValueTrigger variableValueTrigger)
            {
                if (variableValueTrigger.triggerType == LevelScriptTrigger.VARIABLEVALUE)
                {
                    radioButtonVariableValue.Checked = true;
                }
            }

            handleAutoFormat();
            handleHexFormat();
            handleDecimalFormat();

            ValidateAllInputs();

            textBoxScriptID.Enabled = true;
            buttonRemove.Enabled = true;
        }

        private static readonly Color ValidColor = SystemColors.Window;
        private static readonly Color InvalidColor = Color.MistyRose;

        /// <summary>
        /// Validates a text input and returns whether it's valid for the current mode.
        /// Also updates the textbox background color.
        /// </summary>
        private bool ValidateInput(TextBox textBox, out int result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(textBox.Text))
            {
                textBox.BackColor = ValidColor;
                return false; // Empty is not an error, but not valid for parsing
            }

            string input = textBox.Text.Trim();
            bool isValid = false;

            if (radioButtonHex.Checked)
            {
                isValid = TryParseHex(input, out result);
            }
            else if (radioButtonDecimal.Checked)
            {
                isValid = TryParseDecimal(input, out result);
            }
            else // Auto mode - accept either
            {
                isValid = TryParseAuto(input, out result);
            }

            textBox.BackColor = isValid ? ValidColor : InvalidColor;
            return isValid;
        }

        /// <summary>
        /// Parses input in hex mode. Accepts 0x prefix or plain hex digits.
        /// </summary>
        private bool TryParseHex(string input, out int result)
        {
            result = 0;
            string s = input.Trim();

            // Remove 0x prefix if present
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                s = s.Substring(2);
            }

            // Try to parse as hex
            try
            {
                result = Convert.ToInt32(s, 16);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Parses input in decimal mode. Only accepts decimal digits.
        /// Shows warning if input looks like hex.
        /// </summary>
        private bool TryParseDecimal(string input, out int result)
        {
            result = 0;
            string s = input.Trim();

            // Check if user entered hex format in decimal mode
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return false; // 0x prefix not allowed in decimal mode
            }

            // Check if input contains hex letters (A-F)
            foreach (char c in s.ToUpperInvariant())
            {
                if (c >= 'A' && c <= 'F')
                {
                    return false; // Hex letters not allowed in decimal mode
                }
            }

            // Try to parse as decimal
            try
            {
                result = Convert.ToInt32(s, 10);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Parses input in auto mode. Accepts 0x prefix for hex, otherwise decimal.
        /// </summary>
        private bool TryParseAuto(string input, out int result)
        {
            result = 0;
            string s = input.Trim();

            // If 0x prefix, parse as hex
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return TryParseHex(s, out result);
            }

            // Otherwise try decimal first
            try
            {
                result = Convert.ToInt32(s, 10);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validates all input fields and updates their visual state.
        /// </summary>
        private void ValidateAllInputs()
        {
            ValidateInput(textBoxScriptID, out _);
            ValidateInput(textBoxVariableName, out _);
            ValidateInput(textBoxVariableValue, out _);
        }

        /// <summary>
        /// Gets the tooltip message for invalid input based on current mode.
        /// </summary>
        private string GetValidationErrorMessage()
        {
            if (radioButtonHex.Checked)
            {
                return "Invalid hex format. Use digits 0-9 and A-F, optionally prefixed with 0x.";
            }
            else if (radioButtonDecimal.Checked)
            {
                return "Invalid decimal format. Use only digits 0-9.\nIf you want to enter hex, switch to Hex mode.";
            }
            else
            {
                return "Invalid number format. Use decimal digits or hex with 0x prefix.";
            }
        }

        private void buttonAdd_Click(object sender, EventArgs e)
        {
            if (_levelScriptFile == null)
            {
                _levelScriptFile = new LevelScriptFile();
            }

            if (radioButtonVariableValue.Checked)
            {
                // Validate all three inputs for variable value trigger
                if (!ValidateInput(textBoxScriptID, out int scriptID))
                {
                    MessageBox.Show(GetValidationErrorMessage(), "Invalid Script ID", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    textBoxScriptID.Focus();
                    return;
                }
                if (!ValidateInput(textBoxVariableName, out int variableName))
                {
                    MessageBox.Show(GetValidationErrorMessage(), "Invalid Variable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    textBoxVariableName.Focus();
                    return;
                }
                if (!ValidateInput(textBoxVariableValue, out int variableValue))
                {
                    MessageBox.Show(GetValidationErrorMessage(), "Invalid Value", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    textBoxVariableValue.Focus();
                    return;
                }

                VariableValueTrigger trigger = new VariableValueTrigger(scriptID, variableName, variableValue);
                _levelScriptFile.bufferSet.Add(trigger);
            }
            else
            {
                // Validate script ID only for map/screen/load triggers
                if (!ValidateInput(textBoxScriptID, out int scriptID))
                {
                    MessageBox.Show(GetValidationErrorMessage(), "Invalid Script ID", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    textBoxScriptID.Focus();
                    return;
                }

                if (radioButtonMapChange.Checked)
                {
                    MapScreenLoadTrigger mapScreenLoadTrigger = new MapScreenLoadTrigger(LevelScriptTrigger.MAPCHANGE, scriptID);
                    _levelScriptFile.bufferSet.Add(mapScreenLoadTrigger);
                }
                else if (radioButtonScreenReset.Checked)
                {
                    MapScreenLoadTrigger mapScreenLoadTrigger = new MapScreenLoadTrigger(LevelScriptTrigger.SCREENRESET, scriptID);
                    _levelScriptFile.bufferSet.Add(mapScreenLoadTrigger);
                }
                else if (radioButtonLoadGame.Checked)
                {
                    MapScreenLoadTrigger mapScreenLoadTrigger = new MapScreenLoadTrigger(LevelScriptTrigger.LOADGAME, scriptID);
                    _levelScriptFile.bufferSet.Add(mapScreenLoadTrigger);
                }
            }

            textBoxScriptID.Clear();
            textBoxVariableName.Clear();
            textBoxVariableValue.Clear();
            ValidateAllInputs(); // Reset colors after clearing
        }

        private void buttonRemove_Click(object sender, EventArgs e)
        {
            _levelScriptFile.bufferSet.RemoveAt(listBoxTriggers.SelectedIndex);
        }

        private void buttonOpenHeaderScript_Click(object sender, EventArgs e)
        {
            HashSet<string> result;
            result = HeaderSearch.AdvancedSearch(0, (ushort)EditorPanels.headerEditor.internalNames.Count, EditorPanels.headerEditor.internalNames, (int)MapHeader.SearchableFields.LevelScriptID, (int)HeaderSearch.NumOperators.Equal, EditorPanels.levelScriptEditor.selectScriptFileComboBox.SelectedIndex.ToString());
            AppLogger.Debug($"Found {result.Count} headers with script ID {EditorPanels.levelScriptEditor.selectScriptFileComboBox.SelectedIndex}");
            AppLogger.Debug($"Searching for script file {EditorPanels.levelScriptEditor.selectScriptFileComboBox.SelectedIndex} in headers: {string.Join(", ", result)}");
            if (result.Count == 0)
            {
                MessageBox.Show($"No headers found with level-script ID {EditorPanels.levelScriptEditor.selectScriptFileComboBox.SelectedIndex}.", "No headers found");
                return;
            }
            string[] arr = new string[result.Count];
            result.CopyTo(arr);
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = arr[i].Remove(0, 3).Replace(MapHeader.nameSeparator, "");
            }
            AppLogger.Debug($"Found {arr.Length} headers with script ID {EditorPanels.levelScriptEditor.selectScriptFileComboBox.SelectedIndex} in internal names: {string.Join(", ", arr)}");
            ushort index = (ushort)EditorPanels.headerEditor.internalNames.IndexOf(arr[0]);
            MapHeader h;
            if (PatchToolboxDialog.flag_DynamicHeadersPatchApplied || PatchToolboxDialog.CheckFilesDynamicHeadersPatchApplied())
            {
                h = MapHeader.LoadFromFile(RomInfo.gameDirs[DirNames.dynamicHeaders].unpackedDir + "\\" + index.ToString("D4"), index, 0);
            }
            else
            {
                h = MapHeader.LoadFromARM9(index);
            }
            EditorPanels.scriptEditor.OpenScriptEditor(this._parent, (int)h.scriptFileID);
        }

        private bool isEmptyLevelScript()
        {
            ScriptFile script = new ScriptFile((int)EditorPanels.levelScriptEditor.selectScriptFileComboBox.SelectedIndex, true, true);
            return script.isLevelScript;
        }

        private void buttonOpenSelectedScript_Click(object sender, EventArgs e)
        {
            EditorPanels.scriptEditor.OpenScriptEditor(this._parent, (int)EditorPanels.levelScriptEditor.selectScriptFileComboBox.SelectedIndex);
        }

        void buttonLocate_Click(object sender, EventArgs e)
        {
            if (_levelScriptFile == null) { return; }
            string path = Filesystem.GetScriptPath(_levelScriptFile.ID);
            Helpers.ExplorerSelect(path);
        }

        void buttonImport_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    LevelScriptFile importedFile = new LevelScriptFile();
                    importedFile.parse_file(ofd.FileName);
                    _levelScriptFile.bufferSet.Clear();
                    foreach (LevelScriptTrigger trigger in importedFile.bufferSet)
                    {
                        _levelScriptFile.bufferSet.Add(trigger);
                    }
                }
                catch (InvalidDataException ex)
                {
                    MessageBox.Show(ex.Message, ex.GetType().ToString());
                }
            }
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            string path = Filesystem.GetScriptPath(_levelScriptFile.ID);
            saveFile(path);
        }

        private void buttonExport_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            try
            {
                sfd.InitialDirectory = Path.GetDirectoryName(sfd.FileName);
                sfd.FileName = Path.GetFileName(sfd.FileName);
            }
            catch (Exception ex)
            {
                sfd.InitialDirectory = Path.GetDirectoryName(Environment.SpecialFolder.UserProfile.ToString());
                sfd.FileName = Path.GetFileName(sfd.FileName);
            }

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                saveFile(sfd.FileName);
            }
        }

        void saveFile(string path)
        {
            try
            {
                long bytes_written = _levelScriptFile.write_file(path);
                if (bytes_written <= 4)
                {
                    MessageBox.Show("Empty level script file was correctly saved.", "Success!");
                }
                else
                {
                    MessageBox.Show("File was correctly saved.", "Success!");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, ex.GetType().ToString());
            }
        }

        private void handleAutoFormat()
        {
            if (!radioButtonAuto.Checked) { return; }

            textBoxScriptID.Clear();
            textBoxVariableName.Clear();
            textBoxVariableValue.Clear();

            if (listBoxTriggers.SelectedItem is MapScreenLoadTrigger mapScreenLoadTrigger)
            {
                textBoxScriptID.Text = mapScreenLoadTrigger.scriptTriggered.ToString();
            }
            else if (listBoxTriggers.SelectedItem is VariableValueTrigger variableValueTrigger)
            {
                textBoxScriptID.Text = variableValueTrigger.scriptTriggered.ToString();
                textBoxVariableName.Text = "" + variableValueTrigger.variableToWatch.ToString("D");
                textBoxVariableValue.Text = "" + variableValueTrigger.expectedValue.ToString("D");
            }
        }

        private void handleHexFormat()
        {
            if (!radioButtonHex.Checked) { return; }

            textBoxScriptID.Clear();
            textBoxVariableName.Clear();
            textBoxVariableValue.Clear();

            if (listBoxTriggers.SelectedItem is MapScreenLoadTrigger mapScreenLoadTrigger)
            {
                textBoxScriptID.Text = mapScreenLoadTrigger.scriptTriggered.ToString();
            }
            else if (listBoxTriggers.SelectedItem is VariableValueTrigger variableValueTrigger)
            {
                textBoxScriptID.Text = variableValueTrigger.scriptTriggered.ToString();
                textBoxVariableName.Text = "0x" + variableValueTrigger.variableToWatch.ToString("X");
                textBoxVariableValue.Text = "0x" + variableValueTrigger.expectedValue.ToString("X");
            }
        }

        private void handleDecimalFormat()
        {
            if (!radioButtonDecimal.Checked) { return; }

            textBoxScriptID.Clear();
            textBoxVariableName.Clear();
            textBoxVariableValue.Clear();

            if (listBoxTriggers.SelectedItem is MapScreenLoadTrigger mapScreenLoadTrigger)
            {
                textBoxScriptID.Text = mapScreenLoadTrigger.scriptTriggered.ToString();
            }
            else if (listBoxTriggers.SelectedItem is VariableValueTrigger variableValueTrigger)
            {
                textBoxScriptID.Text = variableValueTrigger.scriptTriggered.ToString();
                textBoxVariableName.Text = "" + variableValueTrigger.variableToWatch.ToString("D");
                textBoxVariableValue.Text = "" + variableValueTrigger.expectedValue.ToString("D");
            }
        }

        private void radioButtonAuto_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButtonAuto.Checked)
            {
                LevelScriptTrigger.DisplayInHex = false;
                RefreshTriggerList();
            }
            handleAutoFormat();
            ValidateAllInputs();
        }

        private void radioButtonHex_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButtonHex.Checked)
            {
                LevelScriptTrigger.DisplayInHex = true;
                RefreshTriggerList();
            }
            handleHexFormat();
            ValidateAllInputs();
        }

        private void radioButtonDecimal_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButtonDecimal.Checked)
            {
                LevelScriptTrigger.DisplayInHex = false;
                RefreshTriggerList();
            }
            handleDecimalFormat();
            ValidateAllInputs();
        }

        /// <summary>
        /// Refreshes the trigger listbox to update display formatting.
        /// </summary>
        private void RefreshTriggerList()
        {
            if (listBoxTriggers.DataSource == null) return;

            int selectedIndex = listBoxTriggers.SelectedIndex;

            // Force refresh by reassigning DataSource
            var source = listBoxTriggers.DataSource;
            listBoxTriggers.DataSource = null;
            listBoxTriggers.DataSource = source;

            if (selectedIndex >= 0 && selectedIndex < listBoxTriggers.Items.Count)
            {
                listBoxTriggers.SelectedIndex = selectedIndex;
            }
        }
        private void AssignGroupBoxScriptText()
        {
            if (radioButtonVariableValue.Checked)
            {
                groupBoxScript.Text = "Keep running this Script";
            }
            else
            {
                groupBoxScript.Text = "Run this Script";
            }
        }

        private void radioButtonVariableValue_CheckedChanged(object sender, EventArgs e)
        {
            groupBoxVariable.Visible = true;
            groupBoxValue.Visible = true;
            buttonAdd_logic();
            AssignGroupBoxScriptText();
        }

        private void radioButtonMapChange_CheckedChanged(object sender, EventArgs e)
        {
            groupBoxVariable.Visible = false;
            groupBoxValue.Visible = false;
            buttonAdd_logic();
            AssignGroupBoxScriptText();
        }

        private void radioButtonScreenReset_CheckedChanged(object sender, EventArgs e)
        {
            groupBoxVariable.Visible = false;
            groupBoxValue.Visible = false;
            buttonAdd_logic();
            AssignGroupBoxScriptText();
        }

        private void radioButtonLoadGame_CheckedChanged(object sender, EventArgs e)
        {
            groupBoxVariable.Visible = false;
            groupBoxValue.Visible = false;
            buttonAdd_logic();
            AssignGroupBoxScriptText();
        }

        void textBoxScriptID_TextChanged(object sender, EventArgs e)
        {
            ValidateInput(textBoxScriptID, out _);
            buttonAdd_logic();
        }

        void textBoxVariableName_TextChanged(object sender, EventArgs e)
        {
            ValidateInput(textBoxVariableName, out _);
            buttonAdd_logic();
        }

        void textBoxVariableValue_TextChanged(object sender, EventArgs e)
        {
            ValidateInput(textBoxVariableValue, out _);
            buttonAdd_logic();
        }
    }
}
