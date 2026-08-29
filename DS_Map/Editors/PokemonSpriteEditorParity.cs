using DSPRE.Editors.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using static DSPRE.RomInfo;

namespace DSPRE.Editors {
    /// <summary>
    /// The parts of the Sprite Editor that show one animation frame at a time, let the palette be
    /// edited by clicking a colour, and move whole sprite sheets in and out.
    /// </summary>
    public partial class PokemonSpriteEditor {
        // Display cells run 0-7: slot = cell % 4 (Female Back, Male Back, Female Front, Male Front),
        // and cells 4-7 are the same four poses drawn with the shiny palette.
        private const int CellCount = 8;
        private const int CellSize = 160;

        private readonly int[] cellFrame = new int[CellCount];
        private readonly Button[,] frameButtons = new Button[CellCount, 2];
        private Button animateButton;
        private Timer animateTimer;
        private int animateFrame;

        private readonly Button[] normalSwatchButtons = new Button[16];
        private readonly Button[] shinySwatchButtons = new Button[16];
        private Label normalSwatchLabel;
        private Label shinySwatchLabel;

        private static int SlotOfCell(int cell) { return cell % 4; }
        private static bool CellIsShiny(int cell) { return cell >= 4; }

        #region Layout
        private void BuildParityUi() {
            // Two columns of poses with the frame buttons beside each, which leaves the right-hand
            // side free for the palette swatches.
            int[] colX = { 96, 300 };
            int[] rowY = { 64, 230, 396, 562 };

            for (int cell = 0; cell < CellCount; cell++) {
                PictureBox box = PictureBoxForCell(cell);
                int col = cell % 2;
                int row = (cell / 2);
                box.Location = new Point(colX[col], rowY[row]);
                box.Size = new Size(CellSize, CellSize);
                box.SizeMode = PictureBoxSizeMode.Normal;

                for (int f = 0; f < 2; f++) {
                    Button b = new Button();
                    b.Text = (f + 1).ToString();
                    b.Size = new Size(26, 24);
                    b.Location = new Point(colX[col] + CellSize + 4, rowY[row] + 52 + f * 26);
                    b.Tag = cell * 2 + f;
                    b.Click += FrameButton_Click;
                    b.Visible = false;
                    Controls.Add(b);
                    b.BringToFront();
                    frameButtons[cell, f] = b;
                }
            }

            animateButton = new Button();
            animateButton.Text = "Animate";
            animateButton.Size = new Size(70, 23);
            animateButton.Location = new Point(340, 8);
            animateButton.Click += AnimateButton_Click;
            Controls.Add(animateButton);

            Button importWizard = new Button();
            importWizard.Text = "Import Wizard…";
            importWizard.Size = new Size(104, 23);
            importWizard.Location = new Point(416, 8);
            importWizard.Click += ImportWizard_Click;
            Controls.Add(importWizard);

            Button exportWizard = new Button();
            exportWizard.Text = "Export Wizard…";
            exportWizard.Size = new Size(104, 23);
            exportWizard.Location = new Point(524, 8);
            exportWizard.Click += ExportWizard_Click;
            Controls.Add(exportWizard);

            animateTimer = new Timer();
            animateTimer.Interval = 400;
            animateTimer.Tick += AnimateTimer_Tick;

            normalSwatchLabel = BuildSwatchStrip(normalSwatchButtons, "Normal palette", 520, 64, false);
            shinySwatchLabel = BuildSwatchStrip(shinySwatchButtons, "Shiny palette", 520, 396, true);

            BuildSheetButtons();
        }

        private Label BuildSwatchStrip(Button[] target, string caption, int x, int y, bool shiny) {
            Label lbl = new Label();
            lbl.Text = caption;
            lbl.AutoSize = true;
            lbl.Location = new Point(x, y);
            Controls.Add(lbl);

            for (int i = 0; i < 16; i++) {
                Button b = new Button();
                b.Size = new Size(24, 24);
                b.Location = new Point(x + (i % 8) * 26, y + 20 + (i / 8) * 26);
                b.FlatStyle = FlatStyle.Flat;
                b.Tag = (shiny ? 100 : 0) + i;
                b.Click += Swatch_Click;
                Controls.Add(b);
                target[i] = b;
            }

            Label hint = new Label();
            hint.Text = "Click a colour to edit it.";
            hint.AutoSize = true;
            hint.ForeColor = SystemColors.GrayText;
            hint.Location = new Point(x, y + 74);
            Controls.Add(hint);
            return lbl;
        }

        private void BuildSheetButtons() {
            string[] captions = { "Export Female", "Export Male", "Export Both", "Import Female", "Import Male", "Import Both" };
            for (int i = 0; i < captions.Length; i++) {
                Button b = new Button();
                b.Text = captions[i];
                b.Size = new Size(96, 23);
                b.Location = new Point(520, 200 + (i / 3) * 27 + (i >= 3 ? 6 : 0));
                b.Left = 520 + (i % 3) * 100;
                b.Tag = i;
                b.Click += SheetButton_Click;
                Controls.Add(b);
            }

            Label lbl = new Label();
            lbl.Text = "Sprite sheets";
            lbl.AutoSize = true;
            lbl.Location = new Point(520, 180);
            Controls.Add(lbl);
        }

        private PictureBox PictureBoxForCell(int cell) {
            // displayPictureBoxes is [gender, group]; the cell order here is the PictureBox Name order.
            return displayPictureBoxes[cell % 2, cell / 2];
        }
        #endregion

        #region Frame display
        private void FrameButton_Click(object sender, EventArgs e) {
            int tag = (int)((Button)sender).Tag;
            int cell = tag / 2;
            int frame = tag % 2;
            cellFrame[cell] = frame;
            StopAnimation();
            RenderCells();
        }

        private void AnimateButton_Click(object sender, EventArgs e) {
            if (animateTimer.Enabled) {
                StopAnimation();
            } else {
                animateTimer.Start();
                animateButton.Text = "Stop";
            }
        }

        private void StopAnimation() {
            if (animateTimer != null && animateTimer.Enabled) {
                animateTimer.Stop();
            }
            if (animateButton != null) {
                animateButton.Text = "Animate";
            }
        }

        private void AnimateTimer_Tick(object sender, EventArgs e) {
            animateFrame = 1 - animateFrame;
            for (int cell = 0; cell < CellCount; cell++) {
                // A pose with only one real frame stays on it instead of blinking to blank padding.
                if (FrameToggleVisible(cell)) {
                    cellFrame[cell] = animateFrame;
                }
            }
            RenderCells();
        }

        private bool FrameToggleVisible(int cell) {
            Bitmap src = currentSprites.Sprites[SlotOfCell(cell)];
            return src != null && !IsFrameBlank(src, 0) && !IsFrameBlank(src, 1);
        }

        /// <summary>Some real ROM sprites only ever had one frame drawn; the rest is zero padding.</summary>
        private static bool IsFrameBlank(Bitmap slot, int frame) {
            if (slot == null || slot.PixelFormat != PixelFormat.Format8bppIndexed) {
                return false;
            }
            int x0 = frame * 80;
            if (x0 + 80 > slot.Width) {
                return true;
            }

            BitmapData data = slot.LockBits(new Rectangle(0, 0, slot.Width, slot.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format8bppIndexed);
            try {
                byte[] row = new byte[data.Stride];
                for (int y = 0; y < slot.Height; y++) {
                    System.Runtime.InteropServices.Marshal.Copy(
                        IntPtr.Add(data.Scan0, y * data.Stride), row, 0, data.Stride);
                    for (int x = x0; x < x0 + 80; x++) {
                        if (row[x] != 0) {
                            return false;
                        }
                    }
                }
            } finally {
                slot.UnlockBits(data);
            }
            return true;
        }

        private void UpdateFrameButtons() {
            for (int cell = 0; cell < CellCount; cell++) {
                bool show = FrameToggleVisible(cell);
                Bitmap src = currentSprites.Sprites[SlotOfCell(cell)];
                if (!show && src != null && IsFrameBlank(src, 0)) {
                    cellFrame[cell] = 1; // only the second frame is real
                } else if (!show) {
                    cellFrame[cell] = 0;
                }

                for (int f = 0; f < 2; f++) {
                    Button b = frameButtons[cell, f];
                    if (b == null) {
                        continue;
                    }
                    b.Visible = show;
                    b.UseVisualStyleBackColor = cellFrame[cell] != f;
                    b.BackColor = cellFrame[cell] == f ? SystemColors.Highlight : SystemColors.Control;
                    b.ForeColor = cellFrame[cell] == f ? SystemColors.HighlightText : SystemColors.ControlText;
                }
            }
        }

        /// <summary>Draws every cell at its own current frame, scaled 2x.</summary>
        private void RenderCells() {
            for (int cell = 0; cell < CellCount; cell++) {
                PictureBox box = PictureBoxForCell(cell);
                Bitmap old = box.Image as Bitmap;
                box.Image = null;
                if (old != null) {
                    old.Dispose();
                }

                Bitmap src = currentSprites.Sprites[SlotOfCell(cell)];
                if (src == null || currentSprites.Normal == null) {
                    continue;
                }

                ColorPalette pal = CellIsShiny(cell) ? currentSprites.Shiny : currentSprites.Normal;
                if (pal == null) {
                    pal = currentSprites.Normal;
                }
                src.Palette = pal;

                int frame = cellFrame[cell];
                int x0 = frame * 80;
                if (x0 + 80 > src.Width) {
                    x0 = 0;
                }

                using (Bitmap crop = src.Clone(new Rectangle(x0, 0, 80, 80), src.PixelFormat)) {
                    box.Image = new Bitmap(crop, CellSize, CellSize);
                }
            }
            UpdateFrameButtons();
            RefreshSwatches();
        }
        #endregion

        #region Palette swatches
        private void RefreshSwatches() {
            FillSwatchRow(normalSwatchButtons, currentSprites.Normal);
            FillSwatchRow(shinySwatchButtons, currentSprites.Shiny ?? currentSprites.Normal);
            bool any = currentSprites.Normal != null;
            normalSwatchLabel.Visible = any;
            shinySwatchLabel.Visible = any;
        }

        private static void FillSwatchRow(Button[] row, ColorPalette pal) {
            for (int i = 0; i < 16; i++) {
                if (row[i] == null) {
                    continue;
                }
                if (pal == null || i >= pal.Entries.Length) {
                    row[i].Visible = false;
                    continue;
                }
                row[i].Visible = true;
                row[i].BackColor = pal.Entries[i];
            }
        }

        private void Swatch_Click(object sender, EventArgs e) {
            int tag = (int)((Button)sender).Tag;
            bool shiny = tag >= 100;
            int index = shiny ? tag - 100 : tag;

            ColorPalette pal = shiny ? currentSprites.Shiny : currentSprites.Normal;
            if (pal == null) {
                return;
            }

            using (PaletteColorDialog dlg = new PaletteColorDialog(pal.Entries[index])) {
                if (dlg.ShowDialog(this) != DialogResult.OK) {
                    return;
                }

                // ColorPalette.Entries can't be written back through the Bitmap without reassigning it.
                ColorPalette edited = pal;
                edited.Entries[index] = dlg.SelectedColor;
                if (shiny) {
                    currentSprites.Shiny = edited;
                } else {
                    currentSprites.Normal = edited;
                }
                RenderCells();
                SetDirty(true);
            }
        }
        #endregion

        #region Wizards
        private void ImportWizard_Click(object sender, EventArgs e) {
            if (!OpenPngs.Enabled) {
                return;
            }

            bool[] slotExists = new bool[4];
            for (int i = 0; i < 4; i++) {
                slotExists[i] = currentSprites.Sprites[i] != null;
            }

            using (SpriteImportWizard dlg = new SpriteImportWizard(slotExists, !isLoadingOtherForms)) {
                if (dlg.ShowDialog(this) != DialogResult.OK) {
                    return;
                }

                int done = 0;
                foreach (KeyValuePair<int, string> pick in dlg.Chosen) {
                    if (ImportImageIntoSlot(pick.Value, pick.Key)) {
                        done++;
                    }
                }

                if (done > 0) {
                    RenderCells();
                    SetDirty(true);
                }
                MessageBox.Show(done + " of " + dlg.Chosen.Count + " image(s) imported.",
                    "Import Wizard", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ExportWizard_Click(object sender, EventArgs e) {
            if (currentSprites.Normal == null) {
                return;
            }

            string start = Path.GetDirectoryName(Application.ExecutablePath);
            using (SpriteExportWizard dlg = new SpriteExportWizard(start, !isLoadingOtherForms)) {
                if (dlg.ShowDialog(this) != DialogResult.OK) {
                    return;
                }

                int written = 0;
                foreach (int item in dlg.SelectedItems) {
                    if (WriteExportItem(item, dlg.OutputFolder)) {
                        written++;
                    }
                }
                MessageBox.Show(written + " file(s) written to " + dlg.OutputFolder + ".",
                    "Export Wizard", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // Items 0-7 are the display cells; 8, 9 and 10 are the female, male and both-gender sheets.
        private bool WriteExportItem(int item, string folder) {
            try {
                if (item < 8) {
                    Bitmap src = currentSprites.Sprites[SlotOfCell(item)];
                    if (src == null) {
                        return false;
                    }
                    ColorPalette pal = CellIsShiny(item) ? currentSprites.Shiny : currentSprites.Normal;
                    src.Palette = pal ?? currentSprites.Normal;
                    string name = string.Format("{0:D3}_{1}.png", currentLoadedId,
                        PokemonSpriteEditor.SlotCaption(item).Replace(" ", "_").Replace("(", "").Replace(")", ""));
                    src.Save(Path.Combine(folder, name), System.Drawing.Imaging.ImageFormat.Png);
                    return true;
                }

                int[] slots;
                string tag;
                if (item == 8) {
                    slots = new[] { PokemonSpriteModel.SlotFemaleBack, PokemonSpriteModel.SlotFemaleFront };
                    tag = "female";
                } else if (item == 9) {
                    slots = new[] { PokemonSpriteModel.SlotMaleBack, PokemonSpriteModel.SlotMaleFront };
                    tag = "male";
                } else {
                    slots = new[] { 0, 1, 2, 3 };
                    tag = "both";
                }
                foreach (int s in slots) {
                    if (currentSprites.Sprites[s] == null) {
                        return false;
                    }
                }

                using (Bitmap sheet = new Bitmap(160 * slots.Length, 80, System.Drawing.Imaging.PixelFormat.Format8bppIndexed)) {
                    sheet.Palette = currentSprites.Normal;
                    CopySlotsIntoSheet(sheet, slots);
                    sheet.Save(Path.Combine(folder, string.Format("{0:D3}_{1}_sheet.png", currentLoadedId, tag)),
                        System.Drawing.Imaging.ImageFormat.Png);
                }
                return true;
            } catch (Exception ex) {
                AppLogger.Error("Export wizard couldn't write item " + item + ": " + ex.Message);
                return false;
            }
        }
        #endregion

        #region Sprite sheets
        private void SheetButton_Click(object sender, EventArgs e) {
            int action = (int)((Button)sender).Tag;
            bool import = action >= 3;
            int which = action % 3; // 0 female, 1 male, 2 both

            int[] slots;
            if (which == 0) {
                slots = new[] { PokemonSpriteModel.SlotFemaleBack, PokemonSpriteModel.SlotFemaleFront };
            } else if (which == 1) {
                slots = new[] { PokemonSpriteModel.SlotMaleBack, PokemonSpriteModel.SlotMaleFront };
            } else {
                slots = new[] { 0, 1, 2, 3 };
            }

            if (import) {
                ImportSheet(slots);
            } else {
                ExportSheet(slots, which);
            }
        }

        private void ExportSheet(int[] slots, int which) {
            foreach (int s in slots) {
                if (currentSprites.Sprites[s] == null) {
                    MessageBox.Show("This Pokémon has no sprites for that gender, so there's nothing to export.",
                        "Nothing to export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }

            using (SaveFileDialog sfd = new SaveFileDialog()) {
                sfd.Title = "Save sprite sheet";
                sfd.Filter = "PNG image|*.png";
                string[] tags = { "female", "male", "both" };
                sfd.FileName = string.Format("{0:D3}_{1}_sheet.png", currentLoadedId, tags[which]);
                if (sfd.ShowDialog() != DialogResult.OK) {
                    return;
                }

                int width = 160 * slots.Length;
                using (Bitmap sheet = new Bitmap(width, 80, PixelFormat.Format8bppIndexed)) {
                    sheet.Palette = currentSprites.Normal;
                    CopySlotsIntoSheet(sheet, slots);
                    sheet.Save(sfd.FileName, ImageFormat.Png);
                }
            }
        }

        private void CopySlotsIntoSheet(Bitmap sheet, int[] slots) {
            BitmapData dst = sheet.LockBits(new Rectangle(0, 0, sheet.Width, sheet.Height),
                ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
            try {
                byte[] row = new byte[dst.Stride];
                for (int y = 0; y < 80; y++) {
                    Array.Clear(row, 0, row.Length);
                    for (int s = 0; s < slots.Length; s++) {
                        Bitmap src = currentSprites.Sprites[slots[s]];
                        BitmapData sd = src.LockBits(new Rectangle(0, y, 160, 1),
                            ImageLockMode.ReadOnly, PixelFormat.Format8bppIndexed);
                        try {
                            byte[] line = new byte[160];
                            System.Runtime.InteropServices.Marshal.Copy(sd.Scan0, line, 0, 160);
                            Array.Copy(line, 0, row, s * 160, 160);
                        } finally {
                            src.UnlockBits(sd);
                        }
                    }
                    System.Runtime.InteropServices.Marshal.Copy(row, 0,
                        IntPtr.Add(dst.Scan0, y * dst.Stride), dst.Stride);
                }
            } finally {
                sheet.UnlockBits(dst);
            }
        }

        private void ImportSheet(int[] slots) {
            using (OpenFileDialog ofd = new OpenFileDialog()) {
                ofd.Title = "Choose a sprite sheet";
                ofd.Filter = "Supported formats: *.bmp, *.gif, *.png | *.bmp; *.gif; *.png";
                if (ofd.ShowDialog() != DialogResult.OK) {
                    return;
                }

                Bitmap sheet;
                try {
                    sheet = new Bitmap(ofd.FileName);
                } catch (Exception ex) {
                    MessageBox.Show("Couldn't read that image: " + ex.Message, "Import failed",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                using (sheet) {
                    int expected = 160 * slots.Length;
                    if (sheet.Width != expected || sheet.Height != 80) {
                        MessageBox.Show(
                            string.Format("That sheet is {0}x{1}. It needs to be {2}x80, laid out left to right.",
                                sheet.Width, sheet.Height, expected),
                            "Wrong size", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    Bitmap indexed = sheet;
                    bool madeIndexed = false;
                    if (sheet.PixelFormat != PixelFormat.Format8bppIndexed) {
                        indexed = new IndexedBitmapHandler().Convert(sheet, PixelFormat.Format8bppIndexed);
                        madeIndexed = true;
                        if (indexed == null) {
                            MessageBox.Show("That sheet couldn't be converted to 16 colours.", "Import failed",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                    }

                    try {
                        for (int s = 0; s < slots.Length; s++) {
                            currentSprites.Sprites[slots[s]] =
                                indexed.Clone(new Rectangle(s * 160, 0, 160, 80), PixelFormat.Format8bppIndexed);
                        }
                        currentSprites.Normal = PadPaletteTo16(indexed.Palette);
                    } finally {
                        if (madeIndexed) {
                            indexed.Dispose();
                        }
                    }
                }
            }

            RenderCells();
            SetDirty(true);
        }
        #endregion
    }
}
