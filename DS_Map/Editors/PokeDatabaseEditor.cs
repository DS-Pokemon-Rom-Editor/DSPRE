using DSPRE.Resources;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DSPRE.Editors
{
    public partial class PokeDatabaseEditor : Form
    {
        private PokeDatabaseManager.CustomPokeDatabaseData workingData;
        private bool hasUnsavedChanges = false;

        public PokeDatabaseEditor()
        {
            InitializeComponent();

            // Create a snapshot of current database state
            workingData = PokeDatabaseManager.CreateSnapshot();

            // Initialize all tabs
            InitializeWeatherTab();
            InitializeCameraTab();
            InitializeMusicTab();
            InitializeAreaTab();

            UpdateTitle();
        }

        #region Initialization

        private void InitializeWeatherTab()
        {
            // DP Weather
            dpWeatherListView.View = View.Details;
            dpWeatherListView.FullRowSelect = true;
            dpWeatherListView.GridLines = true;
            dpWeatherListView.Columns.Add("ID", 60);
            dpWeatherListView.Columns.Add("Description", 250);

            foreach (var kvp in workingData.DPWeatherDict.OrderBy(x => x.Key))
            {
                var item = new ListViewItem(kvp.Key.ToString());
                item.SubItems.Add(kvp.Value);
                item.Tag = kvp.Key;
                dpWeatherListView.Items.Add(item);
            }

            // Pt Weather
            ptWeatherListView.View = View.Details;
            ptWeatherListView.FullRowSelect = true;
            ptWeatherListView.GridLines = true;
            ptWeatherListView.Columns.Add("ID", 60);
            ptWeatherListView.Columns.Add("Description", 250);

            foreach (var kvp in workingData.PtWeatherDict.OrderBy(x => x.Key))
            {
                var item = new ListViewItem(kvp.Key.ToString());
                item.SubItems.Add(kvp.Value);
                item.Tag = kvp.Key;
                ptWeatherListView.Items.Add(item);
            }

            // HGSS Weather
            hgssWeatherListView.View = View.Details;
            hgssWeatherListView.FullRowSelect = true;
            hgssWeatherListView.GridLines = true;
            hgssWeatherListView.Columns.Add("ID", 60);
            hgssWeatherListView.Columns.Add("Description", 250);

            foreach (var kvp in workingData.HGSSWeatherDict.OrderBy(x => x.Key))
            {
                var item = new ListViewItem(kvp.Key.ToString());
                item.SubItems.Add(kvp.Value);
                item.Tag = kvp.Key;
                hgssWeatherListView.Items.Add(item);
            }
        }

        private void InitializeCameraTab()
        {
            // DPPt Camera
            dpptCameraListView.View = View.Details;
            dpptCameraListView.FullRowSelect = true;
            dpptCameraListView.GridLines = true;
            dpptCameraListView.Columns.Add("ID", 60);
            dpptCameraListView.Columns.Add("Description", 250);

            foreach (var kvp in workingData.DPPtCameraDict.OrderBy(x => x.Key))
            {
                var item = new ListViewItem(kvp.Key.ToString());
                item.SubItems.Add(kvp.Value);
                item.Tag = kvp.Key;
                dpptCameraListView.Items.Add(item);
            }

            // HGSS Camera
            hgssCameraListView.View = View.Details;
            hgssCameraListView.FullRowSelect = true;
            hgssCameraListView.GridLines = true;
            hgssCameraListView.Columns.Add("ID", 60);
            hgssCameraListView.Columns.Add("Description", 250);

            foreach (var kvp in workingData.HGSSCameraDict.OrderBy(x => x.Key))
            {
                var item = new ListViewItem(kvp.Key.ToString());
                item.SubItems.Add(kvp.Value);
                item.Tag = kvp.Key;
                hgssCameraListView.Items.Add(item);
            }
        }

        private void InitializeMusicTab()
        {
            // DP Music
            dpMusicListView.View = View.Details;
            dpMusicListView.FullRowSelect = true;
            dpMusicListView.GridLines = true;
            dpMusicListView.Columns.Add("ID", 60);
            dpMusicListView.Columns.Add("Description", 300);

            foreach (var kvp in workingData.DPMusicDict.OrderBy(x => x.Key))
            {
                var item = new ListViewItem(kvp.Key.ToString());
                item.SubItems.Add(kvp.Value);
                item.Tag = kvp.Key;
                dpMusicListView.Items.Add(item);
            }

            // Pt Music
            ptMusicListView.View = View.Details;
            ptMusicListView.FullRowSelect = true;
            ptMusicListView.GridLines = true;
            ptMusicListView.Columns.Add("ID", 60);
            ptMusicListView.Columns.Add("Description", 300);

            foreach (var kvp in workingData.PtMusicDict.OrderBy(x => x.Key))
            {
                var item = new ListViewItem(kvp.Key.ToString());
                item.SubItems.Add(kvp.Value);
                item.Tag = kvp.Key;
                ptMusicListView.Items.Add(item);
            }

            // HGSS Music
            hgssMusicListView.View = View.Details;
            hgssMusicListView.FullRowSelect = true;
            hgssMusicListView.GridLines = true;
            hgssMusicListView.Columns.Add("ID", 60);
            hgssMusicListView.Columns.Add("Description", 300);

            foreach (var kvp in workingData.HGSSMusicDict.OrderBy(x => x.Key))
            {
                var item = new ListViewItem(kvp.Key.ToString());
                item.SubItems.Add(kvp.Value);
                item.Tag = kvp.Key;
                hgssMusicListView.Items.Add(item);
            }
        }

        private void InitializeAreaTab()
        {
            // This is a simpler tab for now, can be enhanced later
            areaInfoLabel.Text = "Area data editing will be implemented in future updates.\n\n" +
                                "Current data includes:\n" +
                                $"- Pt Area Icon Values: {workingData.PtAreaIconValues?.Length ?? 0} entries\n" +
                                $"- HGSS Area Icons: {workingData.HGSSAreaIconsDict?.Count ?? 0} entries\n" +
                                $"- HGSS Area Properties: {workingData.HGSSAreaProperties?.Length ?? 0} entries";
        }

        #endregion

        #region Edit Operations

        private void EditSelectedItem(ListView listView, Action<int, string> updateAction)
        {
            if (listView.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select an item to edit.", "No Selection", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selectedItem = listView.SelectedItems[0];
            int id = (int)selectedItem.Tag;
            string currentValue = selectedItem.SubItems[1].Text;

            using (var editForm = new TextInputDialog("Edit Entry", "Description:", currentValue))
            {
                if (editForm.ShowDialog() == DialogResult.OK)
                {
                    string newValue = editForm.InputText;
                    selectedItem.SubItems[1].Text = newValue;
                    updateAction(id, newValue);
                    hasUnsavedChanges = true;
                    UpdateTitle();
                }
            }
        }

        private void EditSelectedMusicItem(ListView listView, Action<ushort, string> updateAction)
        {
            if (listView.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select an item to edit.", "No Selection", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selectedItem = listView.SelectedItems[0];
            ushort id = ushort.Parse(selectedItem.Tag.ToString());
            string currentValue = selectedItem.SubItems[1].Text;

            using (var editForm = new TextInputDialog("Edit Music Entry", "Description:", currentValue))
            {
                if (editForm.ShowDialog() == DialogResult.OK)
                {
                    string newValue = editForm.InputText;
                    selectedItem.SubItems[1].Text = newValue;
                    updateAction(id, newValue);
                    hasUnsavedChanges = true;
                    UpdateTitle();
                }
            }
        }

        private void AddNewEntry(ListView listView, Dictionary<int, string> dict, string entryType)
        {
            // Find the next available ID
            int nextId = dict.Keys.Any() ? dict.Keys.Max() + 1 : 0;

            using (var idDialog = new TextInputDialog("Add New Entry", "Enter ID (leave blank for next available):", nextId.ToString()))
            {
                if (idDialog.ShowDialog() == DialogResult.OK)
                {
                    if (!int.TryParse(idDialog.InputText, out int newId))
                    {
                        MessageBox.Show("Invalid ID. Please enter a valid integer.", "Invalid ID",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    if (dict.ContainsKey(newId))
                    {
                        var overwriteResult = MessageBox.Show(
                            $"An entry with ID {newId} already exists. Do you want to overwrite it?",
                            "Duplicate ID",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning);

                        if (overwriteResult == DialogResult.No)
                            return;
                    }

                    using (var valueDialog = new TextInputDialog($"Add {entryType} Entry", "Description:", ""))
                    {
                        if (valueDialog.ShowDialog() == DialogResult.OK)
                        {
                            string newValue = valueDialog.InputText;
                            dict[newId] = newValue;

                            // Add or update in the list view
                            var existingItem = listView.Items.Cast<ListViewItem>().FirstOrDefault(i => (int)i.Tag == newId);
                            if (existingItem != null)
                            {
                                existingItem.SubItems[1].Text = newValue;
                            }
                            else
                            {
                                var newItem = new ListViewItem(newId.ToString());
                                newItem.SubItems.Add(newValue);
                                newItem.Tag = newId;
                                listView.Items.Add(newItem);

                                // Re-sort
                                var items = listView.Items.Cast<ListViewItem>().OrderBy(i => (int)i.Tag).ToList();
                                listView.Items.Clear();
                                listView.Items.AddRange(items.ToArray());
                            }

                            hasUnsavedChanges = true;
                            UpdateTitle();
                        }
                    }
                }
            }
        }

        private void AddNewMusicEntry(ListView listView, Dictionary<ushort, string> dict, string entryType)
        {
            // Find the next available ID
            ushort nextId = dict.Keys.Any() ? (ushort)(dict.Keys.Max() + 1) : (ushort)0;

            using (var idDialog = new TextInputDialog("Add New Music Entry", "Enter ID (leave blank for next available):", nextId.ToString()))
            {
                if (idDialog.ShowDialog() == DialogResult.OK)
                {
                    if (!ushort.TryParse(idDialog.InputText, out ushort newId))
                    {
                        MessageBox.Show("Invalid ID. Please enter a valid ushort (0-65535).", "Invalid ID",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    if (dict.ContainsKey(newId))
                    {
                        var overwriteResult = MessageBox.Show(
                            $"An entry with ID {newId} already exists. Do you want to overwrite it?",
                            "Duplicate ID",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning);

                        if (overwriteResult == DialogResult.No)
                            return;
                    }

                    using (var valueDialog = new TextInputDialog($"Add {entryType} Music Entry", "Description:", ""))
                    {
                        if (valueDialog.ShowDialog() == DialogResult.OK)
                        {
                            string newValue = valueDialog.InputText;
                            dict[newId] = newValue;

                            // Add or update in the list view
                            var existingItem = listView.Items.Cast<ListViewItem>().FirstOrDefault(i => ushort.Parse(i.Tag.ToString()) == newId);
                            if (existingItem != null)
                            {
                                existingItem.SubItems[1].Text = newValue;
                            }
                            else
                            {
                                var newItem = new ListViewItem(newId.ToString());
                                newItem.SubItems.Add(newValue);
                                newItem.Tag = newId;
                                listView.Items.Add(newItem);

                                // Re-sort
                                var items = listView.Items.Cast<ListViewItem>().OrderBy(i => ushort.Parse(i.Tag.ToString())).ToList();
                                listView.Items.Clear();
                                listView.Items.AddRange(items.ToArray());
                            }

                            hasUnsavedChanges = true;
                            UpdateTitle();
                        }
                    }
                }
            }
        }

        #endregion

        #region Event Handlers

        private void saveButton_Click(object sender, EventArgs e)
        {
            using (var saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "JSON files (*.json)|*.json";
                saveDialog.Title = "Save Custom PokeDatabase";
                saveDialog.InitialDirectory = System.IO.Path.Combine(Program.DspreDataPath, "customPokedatabase");

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    string databaseName = System.IO.Path.GetFileNameWithoutExtension(saveDialog.FileName);
                    if (PokeDatabaseManager.SaveCustomDatabase(databaseName, workingData))
                    {
                        hasUnsavedChanges = false;
                        UpdateTitle();
                        MessageBox.Show($"Custom database '{databaseName}' saved successfully!", "Save Successful",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);

                        // Update setting
                        SettingsManager.Settings.customPokeDatabaseName = databaseName;
                        SettingsManager.Save();
                    }
                    else
                    {
                        MessageBox.Show("Failed to save custom database. Check the log for details.", "Save Failed",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void loadButton_Click(object sender, EventArgs e)
        {
            var databases = PokeDatabaseManager.GetAvailableDatabases();

            if (databases.Count == 0)
            {
                MessageBox.Show("No custom databases found.", "No Databases", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var selectForm = new ListSelectionDialog("Load Custom Database", "Select a database:", databases))
            {
                if (selectForm.ShowDialog() == DialogResult.OK && !string.IsNullOrEmpty(selectForm.SelectedItem))
                {
                    if (PokeDatabaseManager.LoadCustomDatabase(selectForm.SelectedItem))
                    {
                        // Reload working data from now-updated PokeDatabase
                        workingData = PokeDatabaseManager.CreateSnapshot();

                        // Clear and reinitialize all tabs
                        ClearAllTabs();
                        InitializeWeatherTab();
                        InitializeCameraTab();
                        InitializeMusicTab();
                        InitializeAreaTab();

                        hasUnsavedChanges = false;
                        UpdateTitle();

                        MessageBox.Show($"Custom database '{selectForm.SelectedItem}' loaded successfully!\n\n" +
                                      "All open editors have been notified to refresh their dropdowns.", 
                                      "Load Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        // Update setting
                        SettingsManager.Settings.customPokeDatabaseName = selectForm.SelectedItem;
                        SettingsManager.Save();
                    }
                    else
                    {
                        MessageBox.Show("Failed to load custom database. Check the log for details.", "Load Failed",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void applyButton_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "This will apply the current working data to the active PokeDatabase and notify all open editors.\n\n" +
                "This does NOT save to a file. Use 'Save' to create a custom database file.\n\n" +
                "Continue?",
                "Apply Changes",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                // Apply the working data using the manager
                PokeDatabaseManager.SaveCustomDatabase("_temp_apply", workingData);
                PokeDatabaseManager.LoadCustomDatabase("_temp_apply");

                MessageBox.Show("Changes applied successfully! All open editors have been refreshed.", 
                    "Apply Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        #endregion

        #region Helper Methods

        private void UpdateTitle()
        {
            string unsavedMarker = hasUnsavedChanges ? "*" : "";
            this.Text = $"PokeDatabase Editor{unsavedMarker}";
        }

        private void ClearAllTabs()
        {
            dpWeatherListView.Items.Clear();
            ptWeatherListView.Items.Clear();
            hgssWeatherListView.Items.Clear();
            dpptCameraListView.Items.Clear();
            hgssCameraListView.Items.Clear();
            dpMusicListView.Items.Clear();
            ptMusicListView.Items.Clear();
            hgssMusicListView.Items.Clear();
        }

        private void PokeDatabaseEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (hasUnsavedChanges)
            {
                var result = MessageBox.Show(
                    "You have unsaved changes. Are you sure you want to close?",
                    "Unsaved Changes",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.No)
                {
                    e.Cancel = true;
                }
            }
        }

        #endregion
    }

    #region Helper Dialogs

    public class TextInputDialog : Form
    {
        public string InputText => textBox.Text;
        private TextBox textBox;

        public TextInputDialog(string title, string label, string defaultValue)
        {
            this.Text = title;
            this.Size = new Size(400, 150);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            Label lbl = new Label { Text = label, Left = 10, Top = 10, Width = 360 };
            textBox = new TextBox { Left = 10, Top = 40, Width = 360, Text = defaultValue };

            Button okButton = new Button { Text = "OK", Left = 210, Top = 75, DialogResult = DialogResult.OK };
            Button cancelButton = new Button { Text = "Cancel", Left = 290, Top = 75, DialogResult = DialogResult.Cancel };

            this.Controls.AddRange(new Control[] { lbl, textBox, okButton, cancelButton });
            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;
        }
    }

    public class ListSelectionDialog : Form
    {
        public string SelectedItem => listBox.SelectedItem?.ToString();
        private ListBox listBox;

        public ListSelectionDialog(string title, string label, List<string> items)
        {
            this.Text = title;
            this.Size = new Size(400, 300);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            Label lbl = new Label { Text = label, Left = 10, Top = 10, Width = 360 };
            listBox = new ListBox { Left = 10, Top = 40, Width = 360, Height = 180 };

            foreach (var item in items)
                listBox.Items.Add(item);

            Button okButton = new Button { Text = "OK", Left = 210, Top = 230, DialogResult = DialogResult.OK };
            Button cancelButton = new Button { Text = "Cancel", Left = 290, Top = 230, DialogResult = DialogResult.Cancel };

            this.Controls.AddRange(new Control[] { lbl, listBox, okButton, cancelButton });
            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;
        }
    }

    #endregion
}
