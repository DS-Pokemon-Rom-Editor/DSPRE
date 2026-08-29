using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace DSPRE.Editors {
    /// <summary>
    /// Edits one palette colour by RGB or hex, with the colours picked most recently and a row the
    /// user can pin, so building a palette doesn't mean retyping the same values.
    /// </summary>
    public class PaletteColorDialog : Form {
        private const int FavouriteCount = 16;
        private const int RecentCount = 8;

        // Kept for the lifetime of the app so they carry across Pokémon.
        private static readonly List<Color> Recent = new List<Color>();
        private static readonly Color[] Favourites = new Color[FavouriteCount];

        private readonly NumericUpDown redBox = new NumericUpDown();
        private readonly NumericUpDown greenBox = new NumericUpDown();
        private readonly NumericUpDown blueBox = new NumericUpDown();
        private readonly TextBox hexBox = new TextBox();
        private readonly Panel preview = new Panel();
        private bool updating;

        public Color SelectedColor { get; private set; }

        public PaletteColorDialog(Color initial) {
            SelectedColor = initial;

            Text = "Edit colour";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(310, 300);

            preview.Location = new Point(12, 12);
            preview.Size = new Size(80, 80);
            preview.BorderStyle = BorderStyle.FixedSingle;
            preview.BackColor = initial;
            Controls.Add(preview);

            AddChannel(redBox, "R", 104, 12, initial.R);
            AddChannel(greenBox, "G", 104, 40, initial.G);
            AddChannel(blueBox, "B", 104, 68, initial.B);

            Label hexLabel = new Label();
            hexLabel.Text = "Hex";
            hexLabel.AutoSize = true;
            hexLabel.Location = new Point(196, 14);
            Controls.Add(hexLabel);

            hexBox.Location = new Point(226, 11);
            hexBox.Size = new Size(70, 20);
            hexBox.Text = ToHex(initial);
            hexBox.TextChanged += HexChanged;
            Controls.Add(hexBox);

            Button pick = new Button();
            pick.Text = "More colours…";
            pick.Location = new Point(196, 40);
            pick.Size = new Size(100, 23);
            pick.Click += PickFromSystemDialog;
            Controls.Add(pick);

            BuildRow("Recently used", 108, RecentCount, RecentAt, UseRecent, null);
            BuildRow("Favourites", 176, FavouriteCount / 2, i => Favourites[i], UseFavourite, PinFavourite);
            BuildRow("", 214, FavouriteCount / 2, i => Favourites[i + 8], i => UseFavourite(i + 8), i => PinFavourite(i + 8));

            Label hint = new Label();
            hint.Text = "Right-click a favourite slot to store the current colour.";
            hint.AutoSize = true;
            hint.ForeColor = SystemColors.GrayText;
            hint.Location = new Point(12, 246);
            Controls.Add(hint);

            Button ok = new Button();
            ok.Text = "OK";
            ok.DialogResult = DialogResult.OK;
            ok.Location = new Point(136, 266);
            ok.Click += (s, e) => Commit();
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = "Cancel";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Location = new Point(220, 266);
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }

        private static Color RecentAt(int i) {
            return i < Recent.Count ? Recent[i] : Color.Empty;
        }

        private void AddChannel(NumericUpDown box, string caption, int x, int y, byte value) {
            Label lbl = new Label();
            lbl.Text = caption;
            lbl.AutoSize = true;
            lbl.Location = new Point(x - 16, y + 2);
            Controls.Add(lbl);

            box.Minimum = 0;
            box.Maximum = 255;
            box.Value = value;
            box.Location = new Point(x, y);
            box.Size = new Size(60, 20);
            box.ValueChanged += ChannelChanged;
            Controls.Add(box);
        }

        private void BuildRow(string caption, int y, int count, Func<int, Color> get, Action<int> use, Action<int> pin) {
            if (!string.IsNullOrEmpty(caption)) {
                Label lbl = new Label();
                lbl.Text = caption;
                lbl.AutoSize = true;
                lbl.Location = new Point(12, y);
                Controls.Add(lbl);
                y += 18;
            }

            for (int i = 0; i < count; i++) {
                Button b = new Button();
                b.Size = new Size(30, 26);
                b.Location = new Point(12 + i * 34, y);
                b.FlatStyle = FlatStyle.Flat;
                Color c = get(i);
                b.BackColor = c.IsEmpty ? SystemColors.Control : c;
                int captured = i;
                b.Click += (s, e) => use(captured);
                if (pin != null) {
                    b.MouseUp += (s, e) => {
                        if (e.Button == MouseButtons.Right) {
                            pin(captured);
                            ((Button)s).BackColor = SelectedColor;
                        }
                    };
                }
                Controls.Add(b);
            }
        }

        private void UseRecent(int i) {
            if (i < Recent.Count) {
                Apply(Recent[i]);
            }
        }

        private void UseFavourite(int i) {
            if (!Favourites[i].IsEmpty) {
                Apply(Favourites[i]);
            }
        }

        private void PinFavourite(int i) {
            Favourites[i] = SelectedColor;
        }

        private void ChannelChanged(object sender, EventArgs e) {
            if (updating) {
                return;
            }
            Apply(Color.FromArgb(255, (int)redBox.Value, (int)greenBox.Value, (int)blueBox.Value));
        }

        private void HexChanged(object sender, EventArgs e) {
            if (updating) {
                return;
            }
            string text = hexBox.Text.Trim().TrimStart('#');
            int value;
            if (text.Length == 6 && int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value)) {
                Apply(Color.FromArgb(255, (value >> 16) & 0xFF, (value >> 8) & 0xFF, value & 0xFF));
            }
        }

        private void PickFromSystemDialog(object sender, EventArgs e) {
            using (ColorDialog dlg = new ColorDialog()) {
                dlg.Color = SelectedColor;
                dlg.FullOpen = true;
                if (dlg.ShowDialog(this) == DialogResult.OK) {
                    Apply(dlg.Color);
                }
            }
        }

        private void Apply(Color c) {
            SelectedColor = Color.FromArgb(255, c.R, c.G, c.B);
            updating = true;
            redBox.Value = c.R;
            greenBox.Value = c.G;
            blueBox.Value = c.B;
            hexBox.Text = ToHex(c);
            preview.BackColor = SelectedColor;
            updating = false;
        }

        private void Commit() {
            Recent.RemoveAll(c => c.ToArgb() == SelectedColor.ToArgb());
            Recent.Insert(0, SelectedColor);
            while (Recent.Count > RecentCount) {
                Recent.RemoveAt(Recent.Count - 1);
            }
        }

        private static string ToHex(Color c) {
            return string.Format("{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
        }
    }
}
