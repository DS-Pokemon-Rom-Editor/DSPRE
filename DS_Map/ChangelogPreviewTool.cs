#if DEBUG
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DSPRE {
    /// <summary>
    /// Debug-only. Shows the update prompt exactly as a given release would produce it, reading the
    /// changelog files in the working tree so notes for a version that has not shipped yet can be
    /// checked before tagging. Nothing here contacts GitHub or installs anything.
    /// </summary>
    public class ChangelogPreviewTool : Form {
        private readonly TextBox versionBox = new TextBox();
        private readonly TextBox folderBox = new TextBox();
        private readonly ComboBox headingBox = new ComboBox();
        private readonly Label resolved = new Label();
        private bool updating;
        private string loadedFor;

        public ChangelogPreviewTool() {
            Text = "Generate Update Prompt Preview";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(560, 210);

            Label intro = new Label();
            intro.Text = "Builds the release notes the same way the release workflow does, then shows the\n" +
                         "update prompt with them. Reads the changelog files in the working tree.";
            intro.AutoSize = true;
            intro.Location = new Point(12, 12);
            Controls.Add(intro);

            AddLabel("Changelogs folder", 12, 56);
            folderBox.Location = new Point(130, 53);
            folderBox.Size = new Size(324, 20);
            folderBox.Text = ReleaseNotes.FindChangelogFolder() ?? string.Empty;
            folderBox.TextChanged += (s, e) => RefreshHeadings();
            Controls.Add(folderBox);

            Button browse = new Button();
            browse.Text = "Browse...";
            browse.Location = new Point(460, 51);
            browse.Size = new Size(84, 23);
            browse.Click += Browse;
            Controls.Add(browse);

            AddLabel("Version", 12, 88);
            versionBox.Location = new Point(130, 85);
            versionBox.Size = new Size(120, 20);
            versionBox.Text = Application.ProductVersion;
            versionBox.TextChanged += (s, e) => RefreshHeadings();
            Controls.Add(versionBox);

            AddLabel("Or pick a section", 262, 88);
            headingBox.Location = new Point(370, 85);
            headingBox.Size = new Size(174, 21);
            headingBox.DropDownStyle = ComboBoxStyle.DropDownList;
            headingBox.SelectedIndexChanged += HeadingPicked;
            Controls.Add(headingBox);

            resolved.Location = new Point(12, 118);
            resolved.Size = new Size(532, 34);
            resolved.ForeColor = SystemColors.GrayText;
            Controls.Add(resolved);

            Button preview = new Button();
            preview.Text = "Show preview";
            preview.Location = new Point(348, 166);
            preview.Size = new Size(110, 26);
            preview.Click += ShowPreview;
            Controls.Add(preview);
            AcceptButton = preview;

            Button close = new Button();
            close.Text = "Close";
            close.DialogResult = DialogResult.Cancel;
            close.Location = new Point(464, 166);
            close.Size = new Size(80, 26);
            Controls.Add(close);
            CancelButton = close;

            RefreshHeadings();
        }

        private void AddLabel(string text, int x, int y) {
            Label lbl = new Label();
            lbl.Text = text;
            lbl.AutoSize = true;
            lbl.Location = new Point(x, y);
            Controls.Add(lbl);
        }

        private void Browse(object sender, EventArgs e) {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog()) {
                if (Directory.Exists(folderBox.Text)) {
                    dlg.SelectedPath = folderBox.Text;
                }
                if (dlg.ShowDialog(this) == DialogResult.OK) {
                    folderBox.Text = dlg.SelectedPath;
                }
            }
        }

        // Only rebuilt when the changelog file itself changes, so typing a version doesn't clear the list
        // out from under the picker.
        private void RefreshHeadings() {
            if (updating) {
                return;
            }

            Version version;
            if (Version.TryParse(PadVersion(versionBox.Text), out version) && Directory.Exists(folderBox.Text)) {
                string path = ReleaseNotes.ChangelogPathFor(folderBox.Text, version);
                if (path != loadedFor) {
                    loadedFor = path;
                    updating = true;
                    headingBox.Items.Clear();
                    foreach (string heading in ReleaseNotes.HeadingsIn(path)) {
                        headingBox.Items.Add(heading);
                    }
                    updating = false;
                }
            }
            RefreshResolved();
        }

        private void HeadingPicked(object sender, EventArgs e) {
            if (updating || headingBox.SelectedItem == null) {
                return;
            }
            versionBox.Text = headingBox.SelectedItem.ToString();
        }

        // AssemblyVersion is four parts; a heading like "2.3" needs padding before it will parse.
        private static string PadVersion(string text) {
            string trimmed = (text ?? string.Empty).Trim();
            int parts = trimmed.Split('.').Length;
            for (int i = parts; i < 4; i++) {
                trimmed += ".0";
            }
            return trimmed;
        }

        private bool TryResolve(out Version version, out string notes, out string path, out bool sectionOnly) {
            notes = null;
            path = null;
            sectionOnly = false;

            if (!Version.TryParse(PadVersion(versionBox.Text), out version)) {
                return false;
            }
            if (!Directory.Exists(folderBox.Text)) {
                return false;
            }
            notes = ReleaseNotes.Build(folderBox.Text, version, out path, out sectionOnly);
            return true;
        }

        private void RefreshResolved() {
            Version version;
            string notes, path;
            bool sectionOnly;

            if (!TryResolve(out version, out notes, out path, out sectionOnly)) {
                resolved.Text = "Enter a version like 2.3 or 2.2.2.1, and point at a Changelogs folder.";
                return;
            }

            string file = Path.GetFileName(path);
            if (notes == null) {
                resolved.Text = "No changelog at " + file + ", so this release would publish with no notes.";
                return;
            }

            resolved.Text = string.Format("Release {0} would use {1}{2}.",
                ReleaseNotes.DisplayVersion(version), file,
                sectionOnly ? ", section \"## " + ReleaseNotes.DisplayVersion(version) + "\" only" : ", whole file");
        }

        private void ShowPreview(object sender, EventArgs e) {
            Version version;
            string notes, path;
            bool sectionOnly;

            if (!TryResolve(out version, out notes, out path, out sectionOnly)) {
                MessageBox.Show("Enter a version like 2.3 and pick a folder that holds the changelog files.",
                    "Nothing to preview", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string display = ReleaseNotes.DisplayVersion(version);
            using (UpdateAvailableForm form = new UpdateAvailableForm(
                    Helpers.GetDSPREVersion(), display, "Preview, nothing will be installed", notes, true)) {
                form.ShowDialog(this);
            }
        }
    }
}
#endif
