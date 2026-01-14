using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace DSPRE.CharMaps
{
    public partial class CharMapManagerForm : Form
    {
        private CharMap currentMap;
        private bool dirty = false;

        public CharMapManagerForm()
        {
            InitializeComponent();
            LoadCharMap();
            PopulateListsFromMap();
        }

        private void SetDirty(bool isDirty)
        {
            dirty = isDirty;

            if (dirty)
            {
                this.Text = "Character Map Manager*";
            }
            else
            {
                this.Text = "Character Map Manager";
            }
        }

        private void LoadCharMap()
        {
            // Check if custom charmap exists
            if (!File.Exists(CharMapManager.customCharmapFilePath))
            {
                // No custom map exists
                currentMap = null;
                EnableDisableControls(false);
                return;
            }

            try
            {
                currentMap = CharMapManager.DeserializeCharMap(CharMapManager.customCharmapFilePath);
                EnableDisableControls(true);
                SetDirty(false);
            }
            catch (Exception ex)
            {
                currentMap = null;
                EnableDisableControls(false);
                AppLogger.Error("Failed to load custom charmap: " + ex.ToString());
                MessageBox.Show("Failed to load custom charmap: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PopulateListsFromMap()
        {
            charMapListBox.Items.Clear();
            aliasListBox.Items.Clear();
            codeComboBox.Items.Clear();
            codeComboBox.SelectedIndex = -1;

            if (currentMap == null)
            {
                return;
            }

            // Get all codes sorted
            var sortedCodes = currentMap.GetAllCodes().OrderBy(c => c).ToList();

            charMapListBox.BeginUpdate();
            aliasListBox.BeginUpdate();
            codeComboBox.BeginUpdate();

            foreach (ushort code in sortedCodes)
            {
                CharMapEntry entry = currentMap.GetEntry(code);
                if (entry == null) continue;

                string codeStr = $"0x{code:X4}";
                string displayStr = $"{codeStr} <-> {entry.Character}";

                charMapListBox.Items.Add(displayStr);
                codeComboBox.Items.Add(displayStr);

                // Add aliases if present
                if (entry.Aliases != null && entry.Aliases.Count > 0)
                {
                    foreach (string alias in entry.Aliases)
                    {
                        string aliasDisplayStr = $"{codeStr} <- {alias} (alias)";
                        charMapListBox.Items.Add(aliasDisplayStr);

                        string aliasListDisplayStr = $"{alias} -> {entry.Character} <-> {codeStr}";
                        aliasListBox.Items.Add(aliasListDisplayStr);
                    }
                }
            }

            charMapListBox.EndUpdate();
            aliasListBox.EndUpdate();
            codeComboBox.EndUpdate();
        }

        private void EnableDisableControls(bool enableControls)
        {
            addAliasButton.Enabled = enableControls;
            removeAliasButton.Enabled = enableControls;
            saveButton.Enabled = enableControls;
            deleteCustomMapButton.Enabled = enableControls;
            rebaseButton.Enabled = enableControls;
        }

        private bool CheckUnsavedChanges()
        {
            if (dirty)
            {
                var result = MessageBox.Show("You have unsaved changes. Do you want to save them before proceeding?",
                    "Unsaved Changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    saveButton_Click(null, null);
                    return true;
                }
                else if (result == DialogResult.No)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        private void addAliasButton_Click(object sender, EventArgs e)
        {
            if (currentMap == null)
            {
                return;
            }

            string alias = newAliasTextBox.Text.Trim();

            if (string.IsNullOrEmpty(alias))
            {
                MessageBox.Show("Alias name cannot be empty.", "Invalid Alias", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Warn if single character alias without brackets is used
            if (alias.Length == 1)
            {
                var result = MessageBox.Show("Unbracketed single character aliases are not recommended and may lead to encoding issues. " +
                    "Do you want to enclose the character in brackets?", "Single Character Alias", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    alias = "[" + alias + "]";
                }
                else if (result == DialogResult.Cancel)
                {
                    return;
                }
            }
            // Ensure that multi character aliases are enclosed in []
            else if (alias.Length > 1 && !(alias.StartsWith("[") && alias.EndsWith("]")))
            {
                alias = "[" + alias + "]";
            }

            // Check if alias already exists anywhere in the charmap
            ushort? existingCode = currentMap.FindCode(alias);
            if (existingCode != null)
            {
                MessageBox.Show("This alias or character already exists in the charmap.", "Duplicate Alias", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Get selected code
            if (codeComboBox.SelectedItem == null)
            {
                MessageBox.Show("Please select a character code to alias.", "No Code Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string selectedCodeStr = codeComboBox.SelectedItem.ToString().Split(' ')[0];
            ushort selectedCode = ushort.Parse(selectedCodeStr.Substring(2), System.Globalization.NumberStyles.HexNumber);

            // Add alias to the entry
            CharMapEntry entry = currentMap.GetEntry(selectedCode);
            if (entry != null)
            {
                entry.AddAlias(alias);
                SetDirty(true);

                // Refresh the display
                PopulateListsFromMap();

                // Select the newly added alias in the list
                for (int i = 0; i < aliasListBox.Items.Count; i++)
                {
                    if (aliasListBox.Items[i].ToString().StartsWith(alias + " "))
                    {
                        aliasListBox.SelectedIndex = i;
                        break;
                    }
                }

                newAliasTextBox.Clear();
            }
        }

        private void removeAliasButton_Click(object sender, EventArgs e)
        {
            if (currentMap == null)
            {
                return;
            }

            if (aliasListBox.SelectedItem == null)
            {
                MessageBox.Show("Please select an alias to remove.", "No Alias Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string selectedAliasStr = aliasListBox.SelectedItem.ToString();
            string aliasName = selectedAliasStr.Split(new string[] { " -> " }, StringSplitOptions.None)[0];

            // Find the code for this alias
            ushort? code = currentMap.FindCode(aliasName);

            if (code == null)
            {
                MessageBox.Show("Could not find the code for this alias.", "Alias Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Remove the alias
            CharMapEntry entry = currentMap.GetEntry(code.Value);
            if (entry != null && entry.RemoveAlias(aliasName))
            {
                SetDirty(true);
                PopulateListsFromMap();
            }
            else
            {
                MessageBox.Show("Failed to remove alias.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void createCustomMapButton_Click(object sender, EventArgs e)
        {
            if (File.Exists(CharMapManager.customCharmapFilePath))
            {
                var result = MessageBox.Show("A custom charmap already exists. Do you want to overwrite it?",
                    "Custom Charmap Exists", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (result != DialogResult.Yes)
                {
                    return;
                }
            }

            if (CharMapManager.CreateCustomCharMapFile())
            {
                LoadCharMap();
                PopulateListsFromMap();
                MessageBox.Show("Custom charmap created successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Failed to create custom charmap.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void deleteCustomMapButton_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to delete the custom charmap? This action cannot be undone.",
                "Delete Custom Charmap", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                if (CharMapManager.DeleteCustomCharMapFile())
                {
                    currentMap = null;
                    EnableDisableControls(false);
                    PopulateListsFromMap();
                    SetDirty(false);
                    MessageBox.Show("Custom charmap deleted successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Failed to delete custom charmap.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void reloadButton_Click(object sender, EventArgs e)
        {
            if (!CheckUnsavedChanges())
            {
                return;
            }

            LoadCharMap();
            PopulateListsFromMap();
        }

        private void saveButton_Click(object sender, EventArgs e)
        {
            if (currentMap == null)
            {
                return;
            }

            try
            {
                CharMapManager.SaveCharMap(currentMap, saveToCustomPath: true);
                SetDirty(false);
                MessageBox.Show("Charmap saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to save custom charmap: " + ex.ToString());
                MessageBox.Show("Failed to save custom charmap: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void charMapListBox_DoubleClick(object sender, EventArgs e)
        {
            // Get clicked item
            if (charMapListBox.SelectedItem == null)
            {
                return;
            }

            string selectedItemStr = charMapListBox.SelectedItem.ToString();

            // Extract code
            string codeStr = selectedItemStr.Split(' ')[0];
            ushort code = ushort.Parse(codeStr.Substring(2), System.Globalization.NumberStyles.HexNumber);

            // Try to select corresponding code in combo box
            for (int i = 0; i < codeComboBox.Items.Count; i++)
            {
                string comboItemStr = codeComboBox.Items[i].ToString();
                if (comboItemStr.StartsWith(codeStr))
                {
                    codeComboBox.SelectedIndex = i;
                    break;
                }
            }

            // Copy value to clipboard
            if (selectedItemStr.Contains("(alias)"))
            {
                // It's an alias, copy the alias name
                string alias = selectedItemStr.Split(new string[] { " <- " }, StringSplitOptions.None)[1]
                    .Replace(" (alias)", "");
                Clipboard.SetText(alias);
            }
            else
            {
                // It's a normal char, copy the character
                CharMapEntry entry = currentMap.GetEntry(code);
                if (entry != null)
                {
                    Clipboard.SetText(entry.Character);
                }
            }
        }

        private void searchButton_Click(object sender, EventArgs e)
        {
            string searchTerm = searchTextBox.Text.Trim();

            // Remove any filter if search term is empty
            if (string.IsNullOrEmpty(searchTerm))
            {
                PopulateListsFromMap();
                return;
            }

            // Filter charmap list based on search term
            charMapListBox.Items.Clear();

            string searchTermLower = searchTerm.ToLower();
            bool searchByCode = searchTermLower.StartsWith("0x");

            foreach (ushort code in currentMap.GetAllCodes().OrderBy(c => c))
            {
                CharMapEntry entry = currentMap.GetEntry(code);
                if (entry == null) continue;

                string codeStr = $"0x{code:X4}";
                bool matches = false;

                // Check if code matches
                if (searchByCode && codeStr.ToLower().Contains(searchTermLower))
                {
                    matches = true;
                }
                // Check if character contains search term
                else if (!searchByCode && entry.Character.Contains(searchTerm))
                {
                    matches = true;
                }

                if (matches)
                {
                    string displayStr = $"{codeStr} <-> {entry.Character}";
                    charMapListBox.Items.Add(displayStr);
                }

                // Check aliases
                if (entry.Aliases != null && entry.Aliases.Count > 0)
                {
                    foreach (string alias in entry.Aliases)
                    {
                        bool aliasMatches = false;

                        if (searchByCode && codeStr.ToLower().Contains(searchTermLower))
                        {
                            aliasMatches = true;
                        }
                        else if (!searchByCode && (alias.Contains(searchTerm) || entry.Character.Contains(searchTerm)))
                        {
                            aliasMatches = true;
                        }

                        if (aliasMatches)
                        {
                            string aliasDisplayStr = $"{codeStr} <- {alias} (alias)";
                            charMapListBox.Items.Add(aliasDisplayStr);
                        }
                    }
                }
            }
        }

        private void searchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                searchButton.PerformClick();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void CharMapManagerForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!CheckUnsavedChanges())
            {
                e.Cancel = true;
            }
        }

        private void openFileButton_Click(object sender, EventArgs e)
        {
            if (!File.Exists(CharMapManager.customCharmapFilePath))
            {
                MessageBox.Show("No custom charmap file exists to open.", "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Helpers.OpenFileWithDefaultApp(CharMapManager.customCharmapFilePath);
        }

        private void rebaseButton_Click(object sender, EventArgs e)
        {
            if (!File.Exists(CharMapManager.customCharmapFilePath))
            {
                MessageBox.Show("No custom charmap exists to merge. Please create one first.",
                    "No Custom Charmap", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Confirm rebase if custom map is not outdated
            if (!CharMapManager.IsCustomMapOutdated())
            {
                var result = MessageBox.Show("The custom charmap is already up to date with the default charmap. Do you still want to rebase it?",
                    "Rebase Charmap", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (result != DialogResult.Yes) return;
            }

            // Open merge dialog
            var mergeDialog = new MergeCharmapDialog();
            mergeDialog.ShowDialog();

            if (mergeDialog.DialogResult != DialogResult.OK)
            {
                return;
            }

            try
            {
                MergeResult result = CharMapManager.MergeCustomWithDefault(mergeDialog.SelectedStrategy);

                ShowMergeResultDialog(result);

                currentMap = result.MergedMap;
                PopulateListsFromMap();
                SetDirty(true);
                MessageBox.Show("Custom charmap rebased successfully!\n" +
                    "Remember to save.", "Rebase Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to rebase charmap: " + ex.ToString());
                MessageBox.Show("Failed to rebase charmap: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private void ShowMergeResultDialog(MergeResult result)
        {
            StringBuilder messageBuilder = new StringBuilder();
            messageBuilder.AppendLine("Charmap Merge Summary:");
            messageBuilder.AppendLine(result.GetSummary());
            
            if (result.Conflicts.Count > 0)
            {
                messageBuilder.AppendLine();
                messageBuilder.AppendLine("There were conflicts, do you want to view them?");
                var viewConflictsResult = MessageBox.Show(messageBuilder.ToString(), "Merge Result", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (viewConflictsResult != DialogResult.Yes)
                {
                    return;
                }

                // Show conflicts in a scrollable dialog
                Form conflictForm = new Form
                {
                    Text = "Merge Conflicts",
                    Size = new System.Drawing.Size(600, 400),
                    StartPosition = FormStartPosition.CenterParent
                };

                TextBox conflictTextBox = new TextBox
                {
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Vertical,
                    Dock = DockStyle.Fill,
                    Text = result.GetConflictDetails(),
                    Font = new System.Drawing.Font("Consolas", 14),
                };

                conflictForm.Controls.Add(conflictTextBox);
                conflictForm.ShowDialog();

            }
            else
            {
                MessageBox.Show(messageBuilder.ToString(), "Merge Result", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
                
        }

        // Dialog for selecting merge strategy
        internal class MergeCharmapDialog : Form
        {
            private RadioButton rbPreferCustom;
            private RadioButton rbPreferBase;
            private RadioButton rbReplaceBase;
            private Button btnOK;
            private Button btnCancel;

            public MergeStrategy SelectedStrategy { get; private set; }

            public MergeCharmapDialog()
            {
                InitializeComponent();
            }

            private void InitializeComponent()
            {
                this.Text = "Merge Charmap";
                this.Size = new System.Drawing.Size(450, 280);
                this.FormBorderStyle = FormBorderStyle.FixedDialog;
                this.StartPosition = FormStartPosition.CenterParent;
                this.MaximizeBox = false;
                this.MinimizeBox = false;

                Label lblTitle = new Label
                {
                    Text = "Select merge strategy for conflicting entries:",
                    Location = new System.Drawing.Point(20, 20),
                    Size = new System.Drawing.Size(400, 20),
                    Font = new System.Drawing.Font(this.Font, System.Drawing.FontStyle.Bold)
                };

                rbPreferCustom = new RadioButton
                {
                    Text = "Prefer Custom (Keep your custom characters, merge aliases)",
                    Location = new System.Drawing.Point(30, 50),
                    Size = new System.Drawing.Size(390, 50),
                    Checked = true
                };

                rbPreferBase = new RadioButton
                {
                    Text = "Prefer Base (Keep default characters, add your custom aliases)",
                    Location = new System.Drawing.Point(30, 100),
                    Size = new System.Drawing.Size(390, 50)
                };

                rbReplaceBase = new RadioButton
                {
                    Text = "Replace Base (Use only your custom characters and aliases)",
                    Location = new System.Drawing.Point(30, 150),
                    Size = new System.Drawing.Size(390, 50)
                };

                btnOK = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Location = new System.Drawing.Point(250, 210),
                    Size = new System.Drawing.Size(75, 25)
                };

                btnCancel = new Button
                {
                    Text = "Cancel",
                    DialogResult = DialogResult.Cancel,
                    Location = new System.Drawing.Point(335, 210),
                    Size = new System.Drawing.Size(75, 25)
                };

                btnOK.Click += (s, e) =>
                {
                    if (rbPreferCustom.Checked)
                        SelectedStrategy = MergeStrategy.PreferCustom;
                    else if (rbPreferBase.Checked)
                        SelectedStrategy = MergeStrategy.PreferBase;
                    else
                        SelectedStrategy = MergeStrategy.ReplaceBase;
                };

                this.Controls.AddRange(new Control[] {
                lblTitle, rbPreferCustom, rbPreferBase, rbReplaceBase, btnOK, btnCancel
            });

                this.AcceptButton = btnOK;
                this.CancelButton = btnCancel;
            }
        }
    }
}

