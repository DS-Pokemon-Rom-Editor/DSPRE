using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace DSPRE.Editors
{
    public class LearnsetLimitWarningForm : Form
    {
        private CheckBox dontShowAgainCheckBox;
        private Button saveAnywayButton;
        private Button cancelButton;
        private LinkLabel helpLinkLabel;

        public bool DontShowAgain => dontShowAgainCheckBox.Checked;

        private const string HelpUrl = "https://ds-pokemon-hacking.github.io/docs/generation-iv/guides/editing_moves/#maximum-move-threshold";

        public LearnsetLimitWarningForm(int currentMoveCount)
        {
            InitializeComponent(currentMoveCount);
        }

        private void InitializeComponent(int currentMoveCount)
        {
            this.Text = "Learnset Limit Warning";
            this.Size = new Size(520, 320);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowIcon = false;
            this.ShowInTaskbar = false;

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(15)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));   // Warning icon + title
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // Message
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));   // Checkbox
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));   // Buttons

            // Title with warning
            var titlePanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            var warningIcon = new PictureBox
            {
                Image = SystemIcons.Warning.ToBitmap(),
                Size = new Size(24, 24),
                SizeMode = PictureBoxSizeMode.StretchImage,
                Margin = new Padding(0, 0, 8, 0)
            };

            var titleLabel = new Label
            {
                Text = $"Learnset has {currentMoveCount} entries (exceeds limit of {LearnsetData.VanillaLimit})",
                Font = new Font(this.Font.FontFamily, 10, FontStyle.Bold),
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft
            };

            titlePanel.Controls.Add(warningIcon);
            titlePanel.Controls.Add(titleLabel);

            // Message panel
            var messagePanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true
            };

            var messageLabel = new Label
            {
                Text = "In the Generation IV Pokémon games, level-up learnsets usually have a maximum of 20 entries. " +
                       "DSPRE can enable further entries to be added, but this causes issues, including when using " +
                       "the in-game move relearner for the affected Pokémon:\n\n" +
                       "• Game crashes\n" +
                       "• Incomplete relearner lists being populated\n\n" +
                       "For information on how to resolve this issue, click the link below:",
                Dock = DockStyle.Top,
                AutoSize = true,
                MaximumSize = new Size(470, 0)
            };

            helpLinkLabel = new LinkLabel
            {
                Text = "View guide on fixing the maximum move threshold",
                AutoSize = true,
                Location = new Point(0, 120),
                LinkColor = Color.Blue,
                ActiveLinkColor = Color.DarkBlue,
                VisitedLinkColor = Color.Purple
            };
            helpLinkLabel.LinkClicked += HelpLinkLabel_LinkClicked;

            messagePanel.Controls.Add(messageLabel);
            messagePanel.Controls.Add(helpLinkLabel);

            // Don't show again checkbox
            dontShowAgainCheckBox = new CheckBox
            {
                Text = "Don't show this warning again for this project",
                AutoSize = true,
                Dock = DockStyle.Fill
            };

            // Buttons panel
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 5, 0, 0)
            };

            cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Size = new Size(90, 28)
            };

            saveAnywayButton = new Button
            {
                Text = "Save Anyway",
                DialogResult = DialogResult.OK,
                Size = new Size(100, 28)
            };

            buttonPanel.Controls.Add(cancelButton);
            buttonPanel.Controls.Add(saveAnywayButton);

            mainLayout.Controls.Add(titlePanel, 0, 0);
            mainLayout.Controls.Add(messagePanel, 0, 1);
            mainLayout.Controls.Add(dontShowAgainCheckBox, 0, 2);
            mainLayout.Controls.Add(buttonPanel, 0, 3);

            this.Controls.Add(mainLayout);
            this.AcceptButton = saveAnywayButton;
            this.CancelButton = cancelButton;
        }

        private void HelpLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = HelpUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Failed to open help URL: {ex.Message}");
                MessageBox.Show($"Failed to open the link. You can manually visit:\n{HelpUrl}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
