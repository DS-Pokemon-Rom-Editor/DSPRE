using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace DSPRE {
    /// <summary>
    /// The "a new version is available" prompt. Replaces a plain MessageBox so the release notes are
    /// rendered rather than shown as raw Markdown, and so a long changelog can be scrolled.
    /// </summary>
    public class UpdateAvailableForm : Form {
        private readonly RichTextBox notesBox = new RichTextBox();

        /// <param name="preview">Dev preview: describe what is being shown and drop the install button.</param>
        public UpdateAvailableForm(string currentVersion, string availableVersion, string updateType,
                string changelog, bool preview) {
            Text = preview ? "Update Prompt Preview" : "New Update Available";
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(640, 520);
            MinimumSize = new Size(480, 360);
            FormBorderStyle = FormBorderStyle.Sizable;

            Label headline = new Label();
            headline.Text = preview
                ? "This is how the update prompt will look for this release."
                : "A new DSPRE version is available.";
            headline.Font = new Font(Font.FontFamily, Font.Size + 2f, FontStyle.Bold);
            headline.AutoSize = true;
            headline.Location = new Point(12, 12);
            Controls.Add(headline);

            Label details = new Label();
            details.Text = string.Format("Installed: {0}          Available: {1}          {2}",
                currentVersion, availableVersion, updateType);
            details.AutoSize = true;
            details.ForeColor = SystemColors.GrayText;
            details.Location = new Point(12, 38);
            Controls.Add(details);

            notesBox.Location = new Point(12, 64);
            notesBox.Size = new Size(ClientSize.Width - 24, ClientSize.Height - 116);
            notesBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            notesBox.BorderStyle = BorderStyle.FixedSingle;
            notesBox.BackColor = SystemColors.Window;
            notesBox.ReadOnly = true;
            notesBox.LinkClicked += (s, e) => {
                try {
                    Process.Start(e.LinkText);
                } catch (Exception ex) {
                    AppLogger.Warn("Couldn't open " + e.LinkText + ": " + ex.Message);
                }
            };
            Controls.Add(notesBox);

            Button close = new Button();
            close.Text = preview ? "Close" : "Not now";
            close.DialogResult = DialogResult.No;
            close.Size = new Size(96, 26);
            close.Location = new Point(ClientSize.Width - 108, ClientSize.Height - 38);
            close.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            Controls.Add(close);
            CancelButton = close;

            if (!preview) {
                Button install = new Button();
                install.Text = "Install now";
                install.DialogResult = DialogResult.Yes;
                install.Size = new Size(96, 26);
                install.Location = new Point(ClientSize.Width - 210, ClientSize.Height - 38);
                install.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
                Controls.Add(install);
                AcceptButton = install;
            } else {
                AcceptButton = close;
            }

            MarkdownRichText.Render(notesBox, string.IsNullOrWhiteSpace(changelog)
                ? "Release notes are not available for this version."
                : changelog);
        }
    }
}
