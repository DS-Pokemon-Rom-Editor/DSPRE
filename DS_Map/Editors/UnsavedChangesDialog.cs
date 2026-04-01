using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DSPRE.Editors
{
    /// <summary>
    /// Dialog that displays a summary of all editors with unsaved changes
    /// and allows the user to choose which to save before opening a new ROM.
    /// </summary>
    public class UnsavedChangesDialog : Form
    {
        private CheckedListBox editorsListBox;
        private Button saveSelectedButton;
        private Button discardAllButton;
        private Button cancelButton;
        private Label messageLabel;
        private Label instructionLabel;

        private readonly List<UnsavedEditorInfo> _editors;

        /// <summary>
        /// Information about an editor with unsaved changes.
        /// </summary>
        public class UnsavedEditorInfo
        {
            public string EditorName { get; set; }
            public string Description { get; set; }
            public IEditorWithUnsavedChanges Editor { get; set; }
            public bool ShouldSave { get; set; } = true;

            public override string ToString()
            {
                return string.IsNullOrEmpty(Description)
                    ? EditorName
                    : $"{EditorName} - {Description}";
            }
        }

        /// <summary>
        /// Gets the list of editors that were selected to be saved.
        /// </summary>
        public IEnumerable<UnsavedEditorInfo> EditorsToSave =>
            _editors.Where((e, i) => editorsListBox.GetItemChecked(i));

        public UnsavedChangesDialog(IEnumerable<UnsavedEditorInfo> editorsWithChanges)
        {
            _editors = editorsWithChanges.ToList();
            InitializeComponent();
            PopulateList();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Form settings
            this.Text = "Unsaved Changes";
            this.Size = new Size(450, 380);
            this.MinimumSize = new Size(400, 300);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.Icon = SystemIcons.Warning;

            // Warning icon and message
            var warningIcon = new PictureBox
            {
                Image = SystemIcons.Warning.ToBitmap(),
                Size = new Size(32, 32),
                Location = new Point(12, 12),
                SizeMode = PictureBoxSizeMode.StretchImage
            };
            this.Controls.Add(warningIcon);

            messageLabel = new Label
            {
                Text = "The following editors have unsaved changes:",
                Location = new Point(52, 12),
                Size = new Size(370, 32),
                TextAlign = ContentAlignment.MiddleLeft
            };
            this.Controls.Add(messageLabel);

            // Editors list
            editorsListBox = new CheckedListBox
            {
                Location = new Point(12, 52),
                Size = new Size(410, 200),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                CheckOnClick = true,
                IntegralHeight = false
            };
            this.Controls.Add(editorsListBox);

            // Instruction label
            instructionLabel = new Label
            {
                Text = "Select which changes to save before opening the new ROM.",
                Location = new Point(12, 260),
                Size = new Size(410, 20),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            this.Controls.Add(instructionLabel);

            // Buttons panel
            var buttonPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Location = new Point(12, 290),
                Size = new Size(410, 40),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            cancelButton = new Button
            {
                Text = "Cancel",
                Size = new Size(90, 30),
                DialogResult = DialogResult.Cancel
            };
            buttonPanel.Controls.Add(cancelButton);

            discardAllButton = new Button
            {
                Text = "Discard All",
                Size = new Size(90, 30)
            };
            discardAllButton.Click += DiscardAllButton_Click;
            buttonPanel.Controls.Add(discardAllButton);

            saveSelectedButton = new Button
            {
                Text = "Save Selected",
                Size = new Size(100, 30)
            };
            saveSelectedButton.Click += SaveSelectedButton_Click;
            buttonPanel.Controls.Add(saveSelectedButton);

            this.Controls.Add(buttonPanel);

            this.AcceptButton = saveSelectedButton;
            this.CancelButton = cancelButton;

            this.ResumeLayout(false);
        }

        private void PopulateList()
        {
            editorsListBox.Items.Clear();
            foreach (var editor in _editors)
            {
                editorsListBox.Items.Add(editor, true); // All checked by default
            }
        }

        private void SaveSelectedButton_Click(object sender, EventArgs e)
        {
            // Save the selected editors
            for (int i = 0; i < _editors.Count; i++)
            {
                var editorInfo = _editors[i];
                if (editorsListBox.GetItemChecked(i))
                {
                    try
                    {
                        AppLogger.Info($"UnsavedChangesDialog: Saving {editorInfo.EditorName}");
                        editorInfo.Editor.SaveChanges();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error($"UnsavedChangesDialog: Failed to save {editorInfo.EditorName}: {ex.Message}");
                        MessageBox.Show(
                            $"Failed to save {editorInfo.EditorName}:\n{ex.Message}",
                            "Save Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }
                else
                {
                    AppLogger.Info($"UnsavedChangesDialog: Discarding changes for {editorInfo.EditorName}");
                    editorInfo.Editor.DiscardChanges();
                }
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void DiscardAllButton_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to discard ALL unsaved changes?\n\nThis cannot be undone.",
                "Confirm Discard",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (result == DialogResult.Yes)
            {
                foreach (var editorInfo in _editors)
                {
                    try
                    {
                        AppLogger.Info($"UnsavedChangesDialog: Discarding changes for {editorInfo.EditorName}");
                        editorInfo.Editor.DiscardChanges();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error($"UnsavedChangesDialog: Failed to discard {editorInfo.EditorName}: {ex.Message}");
                    }
                }

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }

        /// <summary>
        /// Shows the unsaved changes dialog if there are any editors with unsaved changes.
        /// Returns true if the user chose to proceed (save/discard), false if cancelled.
        /// </summary>
        public static bool ShowIfNeeded(IEnumerable<UnsavedEditorInfo> editorsWithChanges)
        {
            var editors = editorsWithChanges.ToList();

            if (editors.Count == 0)
            {
                return true; // No unsaved changes, proceed
            }

            using (var dialog = new UnsavedChangesDialog(editors))
            {
                return dialog.ShowDialog() == DialogResult.OK;
            }
        }
    }
}
