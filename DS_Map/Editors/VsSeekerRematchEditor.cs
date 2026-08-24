using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using static DSPRE.RomInfo;

namespace DSPRE.Editors
{
    public partial class VsSeekerRematchEditor : Form, IEditorWithUnsavedChanges
    {
        private static readonly string[] RematchLevelLabels =
        {
            "Rematch A",
            "Rematch B",
            "Rematch C",
            "Rematch D",
            "Rematch E",
        };

        private string[] trainerNames;
        private List<VsSeekerRematchTable.Row> rows;
        private HashSet<int> dirtyRows = new HashSet<int>();
        private int currentRowIndex = -1;
        private bool suppressEvents = false;
        private bool changesSaved = false;

        private ToolStrip toolStrip;
        private StatusStrip statusStrip;
        private ToolStripButton saveRowButton, saveAllButton;
        private ToolStripTextBox filterTextBoxItem;
        private ListBox rowListBox;
        private ComboBox encounterCombo;
        private ComboBox[] rematchCombos;
        private TableLayoutPanel detailPanel;

        #region IEditorWithUnsavedChanges Implementation
        public bool HasUnsavedChanges => dirtyRows.Count > 0 && !changesSaved;
        public string UnsavedChangesDescription => "Vs. Seeker Rematch Editor";
        public void SaveChanges() => SaveAll();
        public void DiscardChanges()
        {
            dirtyRows.Clear();
            changesSaved = false;
        }
        #endregion

        public VsSeekerRematchEditor(int initialRowIndex = -1)
        {
            OpenEditorsRegistry.Register(this);

            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.trainerProperties });

            trainerNames = Helpers.GetTrainerNames();
            rows = VsSeekerRematchTable.ReadAll();

            SetupControls();
            RebuildRowList();

            var indices = (List<int>)rowListBox.Tag;
            int listPosition = initialRowIndex >= 0 && indices != null ? indices.IndexOf(initialRowIndex) : -1;
            if (listPosition < 0 && rowListBox.Items.Count > 0) listPosition = 0;
            rowListBox.SelectedIndex = listPosition;

            UpdateStatus();

            this.FormClosed += (s, e) => OpenEditorsRegistry.Unregister(this);
        }

        private void SetupControls()
        {
            this.Size = new Size(900, 600);
            UpdateWindowTitle();

            toolStrip = new ToolStrip { Dock = DockStyle.Top };

            saveRowButton = new ToolStripButton("Save Row") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            saveRowButton.Click += (s, e) => SaveCurrentRow();

            saveAllButton = new ToolStripButton("Save All") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            saveAllButton.Click += (s, e) => SaveAll();

            var lblFilter = new ToolStripLabel("Filter:");
            filterTextBoxItem = new ToolStripTextBox { Width = 200 };
            filterTextBoxItem.TextChanged += (s, e) => RebuildRowList();

            toolStrip.Items.AddRange(new ToolStripItem[] {
                saveRowButton, saveAllButton, new ToolStripSeparator(),
                lblFilter, filterTextBoxItem
            });

            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 320
            };

            rowListBox = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
            rowListBox.SelectedIndexChanged += RowListBox_SelectedIndexChanged;
            splitContainer.Panel1.Controls.Add(rowListBox);

            BuildDetailControls();
            splitContainer.Panel2.Controls.Add(detailPanel);

            statusStrip = new StatusStrip { Dock = DockStyle.Bottom };
            statusStrip.Items.Add(new ToolStripStatusLabel());

            this.Controls.Add(splitContainer);
            this.Controls.Add(toolStrip);
            this.Controls.Add(statusStrip);
        }

        private void BuildDetailControls()
        {
            detailPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                Padding = new Padding(16),
                AutoScroll = true,
            };
            detailPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            void AddField(string labelText, ComboBox combo)
            {
                var lbl = new Label { Text = labelText, AutoSize = true, Margin = new Padding(0, 8, 0, 0) };
                combo.Dock = DockStyle.Top;
                combo.Margin = new Padding(0, 2, 0, 8);
                detailPanel.Controls.Add(lbl);
                detailPanel.Controls.Add(combo);
            }

            encounterCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            encounterCombo.Items.AddRange(trainerNames);
            encounterCombo.SelectedIndexChanged += (s, e) => FieldChanged();
            AddField("Encounter Trainer", encounterCombo);

            rematchCombos = new ComboBox[VsSeekerRematchTable.RematchLevelCount];
            for (int i = 0; i < VsSeekerRematchTable.RematchLevelCount; i++)
            {
                var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                combo.Items.Add("(none - 0xFFFF)");
                combo.Items.Add("(chain ends here - 0x0000)");
                combo.Items.AddRange(trainerNames);
                combo.SelectedIndexChanged += (s, e) => FieldChanged();
                rematchCombos[i] = combo;
                AddField(RematchLevelLabels[i], combo);
            }
        }

        private string RowLabel(int rowIndex)
        {
            var row = rows[rowIndex];
            bool empty = row.EncounterTrainerId == 0 && row.RematchTrainerIds.All(v => v == 0);
            string encounterName = TrainerLabel(row.EncounterTrainerId);
            return empty ? $"Row {rowIndex}: (empty)" : $"Row {rowIndex}: {encounterName}";
        }

        private string TrainerLabel(int trainerId) =>
            trainerId >= 0 && trainerId < trainerNames.Length ? trainerNames[trainerId] : $"(raw 0x{trainerId:X4})";

        private void RebuildRowList()
        {
            suppressEvents = true;
            rowListBox.BeginUpdate();
            rowListBox.Items.Clear();

            string filter = filterTextBoxItem?.Text?.Trim();
            bool hasFilter = !string.IsNullOrEmpty(filter);

            var indices = new List<int>();
            for (int r = 0; r < rows.Count; r++)
            {
                if (hasFilter && RowLabel(r).IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                rowListBox.Items.Add(RowLabel(r));
                indices.Add(r);
            }
            rowListBox.Tag = indices;

            rowListBox.EndUpdate();
            suppressEvents = false;
        }

        private void RowListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (suppressEvents) return;

            var indices = (List<int>)rowListBox.Tag;
            if (rowListBox.SelectedIndex < 0 || indices == null)
            {
                currentRowIndex = -1;
                detailPanel.Enabled = false;
                return;
            }

            currentRowIndex = indices[rowListBox.SelectedIndex];
            LoadRowIntoDetail(currentRowIndex);
            detailPanel.Enabled = true;
        }

        private void LoadRowIntoDetail(int rowIndex)
        {
            suppressEvents = true;
            var row = rows[rowIndex];

            encounterCombo.SelectedIndex = row.EncounterTrainerId < trainerNames.Length ? row.EncounterTrainerId : -1;

            for (int i = 0; i < VsSeekerRematchTable.RematchLevelCount; i++)
            {
                ushort v = row.RematchTrainerIds[i];
                if (v == VsSeekerRematchTable.NoRematch) rematchCombos[i].SelectedIndex = 0;
                else if (v == VsSeekerRematchTable.ChainEnd) rematchCombos[i].SelectedIndex = 1;
                else if (v < trainerNames.Length) rematchCombos[i].SelectedIndex = 2 + v;
                else rematchCombos[i].SelectedIndex = -1;
            }
            suppressEvents = false;
        }

        private void FieldChanged()
        {
            if (suppressEvents || currentRowIndex < 0) return;

            var row = rows[currentRowIndex];
            if (encounterCombo.SelectedIndex >= 0) row.EncounterTrainerId = (ushort)encounterCombo.SelectedIndex;

            for (int i = 0; i < VsSeekerRematchTable.RematchLevelCount; i++)
            {
                int idx = rematchCombos[i].SelectedIndex;
                if (idx == 0) row.RematchTrainerIds[i] = VsSeekerRematchTable.NoRematch;
                else if (idx == 1) row.RematchTrainerIds[i] = VsSeekerRematchTable.ChainEnd;
                else if (idx >= 2) row.RematchTrainerIds[i] = (ushort)(idx - 2);
            }
            rows[currentRowIndex] = row;

            dirtyRows.Add(currentRowIndex);
            UpdateWindowTitle();
            UpdateStatus();

            int selectedIndex = rowListBox.SelectedIndex;
            suppressEvents = true;
            rowListBox.Items[selectedIndex] = RowLabel(currentRowIndex);
            suppressEvents = false;
        }

        private void SaveCurrentRow()
        {
            if (currentRowIndex < 0) return;

            string error;
            if (!VsSeekerRematchTable.WriteRow(currentRowIndex, rows[currentRowIndex], out error))
            {
                MessageBox.Show(this, "Save failed: " + error, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            dirtyRows.Remove(currentRowIndex);
            changesSaved = dirtyRows.Count == 0;
            UpdateWindowTitle();
            UpdateStatus($"Row {currentRowIndex} saved.");
        }

        private void SaveAll()
        {
            if (dirtyRows.Count == 0)
            {
                UpdateStatus("Nothing to save.");
                return;
            }

            foreach (int r in dirtyRows.ToList())
            {
                string error;
                if (!VsSeekerRematchTable.WriteRow(r, rows[r], out error))
                {
                    MessageBox.Show(this, $"Save failed on row {r}: {error}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            int count = dirtyRows.Count;
            dirtyRows.Clear();
            changesSaved = true;
            UpdateWindowTitle();
            UpdateStatus($"Saved {count} row(s).");
            MessageBox.Show(this, "All Vs. Seeker rematch changes have been saved.", "Save Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void UpdateStatus(string message = null)
        {
            if (statusStrip.Items.Count == 0) return;
            statusStrip.Items[0].Text = message ?? $"{rows.Count} rows.{(dirtyRows.Count > 0 ? $" {dirtyRows.Count} unsaved row(s)." : "")}";
        }

        private void UpdateWindowTitle()
        {
            this.Text = "Vs. Seeker Rematch Editor" + (dirtyRows.Count > 0 ? "*" : "");
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (dirtyRows.Count > 0 && !changesSaved)
            {
                var result = MessageBox.Show(
                    "You have unsaved changes. Are you sure you want to exit?",
                    "Unsaved Changes",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning
                );
                if (result == DialogResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }
            base.OnFormClosing(e);
        }
    }
}
