using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DSPRE.Editors {
    /// <summary>
    /// Picks which poses and sheets to write out in one go, so a full set doesn't need eleven
    /// separate Save PNG trips.
    /// </summary>
    public class SpriteExportWizard : Form {
        private readonly CheckedListBox list = new CheckedListBox();
        private readonly TextBox folderBox = new TextBox();

        /// <summary>Item keys in list order: 0-7 are the display cells, 8-10 are the female/male/both sheets.</summary>
        public List<int> SelectedItems { get; private set; }
        public string OutputFolder { get { return folderBox.Text; } }

        public SpriteExportWizard(string startingFolder, bool allowFullSheet) {
            Text = "Export Wizard";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(380, 380);

            Label what = new Label();
            what.Text = "Choose what to export:";
            what.AutoSize = true;
            what.Location = new Point(12, 12);
            Controls.Add(what);

            list.Location = new Point(12, 32);
            list.Size = new Size(356, 214);
            list.CheckOnClick = true;
            for (int i = 0; i < 8; i++) {
                list.Items.Add(PokemonSpriteEditor.SlotCaption(i), true);
            }
            list.Items.Add("Female sprite sheet", true);
            list.Items.Add("Male sprite sheet", true);
            if (allowFullSheet) {
                list.Items.Add("Both genders sprite sheet", true);
            }
            Controls.Add(list);

            Button all = new Button();
            all.Text = "Select all";
            all.Location = new Point(12, 252);
            all.Size = new Size(84, 23);
            all.Click += (s, e) => SetAll(true);
            Controls.Add(all);

            Button none = new Button();
            none.Text = "Select none";
            none.Location = new Point(102, 252);
            none.Size = new Size(84, 23);
            none.Click += (s, e) => SetAll(false);
            Controls.Add(none);

            Label folder = new Label();
            folder.Text = "Save into:";
            folder.AutoSize = true;
            folder.Location = new Point(12, 290);
            Controls.Add(folder);

            folderBox.Location = new Point(12, 308);
            folderBox.Size = new Size(266, 20);
            folderBox.Text = startingFolder;
            Controls.Add(folderBox);

            Button browse = new Button();
            browse.Text = "Browse…";
            browse.Location = new Point(284, 306);
            browse.Size = new Size(84, 23);
            browse.Click += Browse;
            Controls.Add(browse);

            Button ok = new Button();
            ok.Text = "Export";
            ok.DialogResult = DialogResult.OK;
            ok.Location = new Point(196, 344);
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = "Cancel";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Location = new Point(284, 344);
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
            FormClosing += (s, e) => {
                if (DialogResult != DialogResult.OK) {
                    return;
                }
                SelectedItems = new List<int>();
                for (int i = 0; i < list.Items.Count; i++) {
                    if (list.GetItemChecked(i)) {
                        SelectedItems.Add(i);
                    }
                }
                if (SelectedItems.Count == 0) {
                    MessageBox.Show("Nothing is ticked, so there's nothing to export.", "Nothing selected",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    e.Cancel = true;
                } else if (!Directory.Exists(folderBox.Text)) {
                    MessageBox.Show("That folder doesn't exist.", "Folder not found",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    e.Cancel = true;
                }
            };
        }

        private void SetAll(bool value) {
            for (int i = 0; i < list.Items.Count; i++) {
                list.SetItemChecked(i, value);
            }
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
    }

    /// <summary>
    /// Picks a file per pose so a whole set can be brought in at once instead of one cell at a time.
    /// </summary>
    public class SpriteImportWizard : Form {
        private readonly CheckBox[] enabled = new CheckBox[8];
        private readonly TextBox[] paths = new TextBox[8];

        /// <summary>Display-cell index to source file, for every row the user filled in.</summary>
        public Dictionary<int, string> Chosen { get; private set; }

        public SpriteImportWizard(bool[] slotExists, bool allowFemale) {
            Text = "Import Wizard";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(560, 330);

            Label intro = new Label();
            intro.Text = "Tick a pose and choose the image to import into it. Shiny rows take their palette\n" +
                         "from the image; they don't replace the artwork.";
            intro.AutoSize = true;
            intro.Location = new Point(12, 12);
            Controls.Add(intro);

            int y = 52;
            for (int i = 0; i < 8; i++) {
                bool female = (i % 4) == 0 || (i % 4) == 2;
                bool usable = slotExists[i % 4] && (allowFemale || !female);

                CheckBox cb = new CheckBox();
                cb.Text = PokemonSpriteEditor.SlotCaption(i);
                cb.AutoSize = false;
                cb.Size = new Size(168, 22);
                cb.Location = new Point(12, y);
                cb.Enabled = usable;
                Controls.Add(cb);
                enabled[i] = cb;

                TextBox tb = new TextBox();
                tb.Location = new Point(186, y);
                tb.Size = new Size(272, 20);
                tb.Enabled = usable;
                Controls.Add(tb);
                paths[i] = tb;

                Button br = new Button();
                br.Text = "Browse…";
                br.Location = new Point(464, y - 1);
                br.Size = new Size(84, 23);
                br.Enabled = usable;
                int captured = i;
                br.Click += (s, e) => Browse(captured);
                Controls.Add(br);

                if (!usable) {
                    tb.Text = female ? "this Pokémon has no separate female art" : "no sprite in this slot";
                }
                y += 28;
            }

            Button ok = new Button();
            ok.Text = "Import";
            ok.DialogResult = DialogResult.OK;
            ok.Location = new Point(384, 292);
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = "Cancel";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Location = new Point(472, 292);
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
            FormClosing += (s, e) => {
                if (DialogResult != DialogResult.OK) {
                    return;
                }
                Chosen = new Dictionary<int, string>();
                for (int i = 0; i < 8; i++) {
                    if (!enabled[i].Checked) {
                        continue;
                    }
                    if (!File.Exists(paths[i].Text)) {
                        MessageBox.Show(PokemonSpriteEditor.SlotCaption(i) + " is ticked but its file wasn't found.",
                            "File not found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        e.Cancel = true;
                        return;
                    }
                    Chosen[i] = paths[i].Text;
                }
                if (Chosen.Count == 0) {
                    MessageBox.Show("No poses are ticked, so there's nothing to import.", "Nothing selected",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    e.Cancel = true;
                }
            };
        }

        private void Browse(int index) {
            using (OpenFileDialog dlg = new OpenFileDialog()) {
                dlg.Title = "Image for " + PokemonSpriteEditor.SlotCaption(index);
                dlg.Filter = "Supported formats: *.bmp, *.gif, *.png | *.bmp; *.gif; *.png";
                if (dlg.ShowDialog(this) == DialogResult.OK) {
                    paths[index].Text = dlg.FileName;
                    enabled[index].Checked = true;
                }
            }
        }
    }
}
