using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DSPRE.LibNDSFormats;
using static DSPRE.RomInfo;

namespace DSPRE.Editors.BtxEditor
{
    /// <summary>
    /// "Add Custom Entry..." dialog for the Overworld Editor. Only usable once
    /// <see cref="OverworldSpriteTableExpansion"/> has detected hzla's PlatPatches expansion patch
    /// on the loaded Platinum ROM. Both the texture slot and the clone-source are picked from real
    /// existing data (never typed as a raw internal number), and once an image is picked, the
    /// texture-slot list is re-sorted with any slot that actually fits it marked and pushed first.
    /// DSPRE can't create a brand-new texture slot, only reuse an existing one at its own exact
    /// size/colour budget.
    /// </summary>
    public partial class AddOverworldEntryForm : Form
    {
        private class SlotOption
        {
            public uint Id;
            public int Width, Height;
            public uint ColorLimit;
            public string BaseLabel;
            public string Label;
            public override string ToString() { return Label; }
        }

        private class CloneOption
        {
            public uint Id;
            public override string ToString() { return "OW Entry " + Id; }
        }

        private readonly List<SlotOption> allSlots = new List<SlotOption>();
        private string pngPath;
        private string rawBtxPath;
        private int targetWidth, targetHeight;
        private uint targetColors;

        public uint? AddedAppearanceId { get; private set; }

        public AddOverworldEntryForm()
        {
            InitializeComponent();
            LoadOptions();
        }

        // mmodel.narc holds a mix of file types (flat billboard textures AND full 3D models for
        // "3D model" draw-type overworlds), so an "unused" member number is not necessarily a
        // texture at all. Only offer/measure ones BTX0 can actually read.
        private const int MaxUnusedSlotCandidatesToScan = 400;
        private const int MaxUnusedSlotOptions = 60;

        private void LoadOptions()
        {
            var used = new HashSet<uint>(RomInfo.OverworldTable.Values.Select(v => v.spriteID));
            string dir = RomInfo.gameDirs[DirNames.OWSprites].unpackedDir;
            List<uint> unusedCandidates = Directory.Exists(dir)
                ? Directory.GetFiles(dir)
                    .Select(Path.GetFileName)
                    .Select(n => { uint id; return uint.TryParse(n, out id) ? (uint?)id : null; })
                    .Where(id => id.HasValue).Select(id => id.Value)
                    .Where(id => !used.Contains(id))
                    .OrderBy(id => id).ToList()
                : new List<uint>();

            int scanned = 0, added = 0;
            foreach (uint id in unusedCandidates)
            {
                if (added >= MaxUnusedSlotOptions || scanned >= MaxUnusedSlotCandidatesToScan) break;
                scanned++;
                int w, h; uint colorLimit;
                if (!OverworldSpriteTableExpansion.TryReadTextureInfo(Path.Combine(dir, id.ToString("D4")), out w, out h, out colorLimit)) continue;
                allSlots.Add(new SlotOption { Id = id, Width = w, Height = h, ColorLimit = colorLimit, BaseLabel = "Unused slot #" + id });
                added++;
            }
            foreach (var kv in RomInfo.OverworldTable)
            {
                string path = Path.Combine(dir, kv.Value.spriteID.ToString("D4"));
                int w, h; uint colorLimit;
                if (!OverworldSpriteTableExpansion.TryReadTextureInfo(path, out w, out h, out colorLimit)) continue;
                allSlots.Add(new SlotOption { Id = kv.Value.spriteID, Width = w, Height = h, ColorLimit = colorLimit, BaseLabel = "Existing art from OW Entry " + kv.Key + " (slot #" + kv.Value.spriteID + ")" });
            }
            RebuildSlotOptions();

            var cloneOptions = RomInfo.OverworldTable.Keys.Select(k => new CloneOption { Id = k }).ToList();
            foreach (var c in cloneOptions) cloneCombo.Items.Add(c);
            var defaultClone = cloneOptions.FirstOrDefault(c => c.Id == 0x78) ?? cloneOptions.FirstOrDefault();
            if (defaultClone != null) cloneCombo.SelectedItem = defaultClone;

            uint? suggested = OverworldSpriteTableExpansion.SuggestNewAppearanceId();
            if (suggested.HasValue) appearanceIdTextBox.Text = "0x" + suggested.Value.ToString("X");

            UpdateSlotLabel();
        }

        /// <summary>With an image picked, the imported pixels always land in a brand-new mmodel
        /// slot (see <see cref="OverworldSpriteTableExpansion.AllocateNewMmodelSlot"/>). The slot
        /// picked below is read-only, just a size/color-count template BTX0.Write needs, and is
        /// never modified. Without an image, the picked slot IS the destination and its existing art
        /// is shared on purpose (no write happens either way).</summary>
        private void UpdateSlotLabel()
        {
            bool hasImage = pngPath != null || rawBtxPath != null;
            slotLabel.Text = hasImage
                ? "Format template (existing texture to copy dimensions/colors from)"
                : "Texture slot (this entry's art)";
        }

        private void RebuildSlotOptions()
        {
            uint? previouslySelectedId = null;
            var previouslySelected = slotCombo.SelectedItem as SlotOption;
            if (previouslySelected != null) previouslySelectedId = previouslySelected.Id;

            slotCombo.Items.Clear();
            bool haveTarget = targetWidth > 0;

            IEnumerable<SlotOption> ordered = haveTarget
                ? (IEnumerable<SlotOption>)allSlots.OrderByDescending(s => Fits(s))
                : allSlots;

            SlotOption toSelect = null;
            foreach (var s in ordered)
            {
                bool fits = haveTarget && Fits(s);
                s.Label = haveTarget
                    ? (fits ? "[fits] " : "") + s.BaseLabel + " - " + s.Width + "x" + s.Height + ", up to " + s.ColorLimit + " colors" + (fits ? " (fits your image)" : " (different size/palette)")
                    : s.BaseLabel + " - " + s.Width + "x" + s.Height + ", up to " + s.ColorLimit + " colors";
                slotCombo.Items.Add(s);
                if (previouslySelectedId.HasValue && s.Id == previouslySelectedId.Value) toSelect = s;
            }

            if (toSelect != null) slotCombo.SelectedItem = toSelect;
            else if (slotCombo.Items.Count > 0) slotCombo.SelectedIndex = 0;
        }

        private bool Fits(SlotOption s)
        {
            return s.Width == targetWidth && s.Height == targetHeight && s.ColorLimit >= targetColors;
        }

        private void ChoosePngButton_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog { Title = "Choose overworld image (PNG)", Filter = "PNG Image (*.png)|*.png" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                rawBtxPath = null;
                pngPath = dlg.FileName;
                try
                {
                    using (var bmp = new Bitmap(dlg.FileName))
                    {
                        targetWidth = bmp.Width;
                        targetHeight = bmp.Height;
                        targetColors = GetColorCount(bmp);
                        imagePreview.Image = new Bitmap(bmp);
                        imageInfoLabel.Text = "Your image: " + targetWidth + "x" + targetHeight + ", " + targetColors + " unique colors - pick a slot below marked [fits] (fits your image).";
                        statusLabel.Text = "";
                    }
                }
                catch (Exception ex)
                {
                    pngPath = null;
                    targetWidth = targetHeight = 0;
                    imagePreview.Image = null;
                    imageInfoLabel.Text = "";
                    statusLabel.Text = "Could not read that image: " + ex.Message;
                }
                clearImageButton.Enabled = pngPath != null || rawBtxPath != null;
                UpdateSlotLabel();
                RebuildSlotOptions();
            }
        }

        private void ChooseRawBtxButton_Click(object sender, EventArgs e)
        {
            // Raw NSBTX/BTX0 dumps (e.g. extracted from another ROM) commonly have no file
            // extension at all, so no filter here, any file can be picked and it's validated as
            // a real BTX0 texture right after selection.
            using (var dlg = new OpenFileDialog { Title = "Choose a raw texture file (BTX0)", Filter = "All files (*.*)|*.*" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                pngPath = null;
                rawBtxPath = dlg.FileName;
                try
                {
                    using (var bmp = BTX0.Read(File.ReadAllBytes(dlg.FileName)))
                    {
                        if (bmp == null)
                        {
                            rawBtxPath = null;
                            imagePreview.Image = null;
                            imageInfoLabel.Text = "";
                            statusLabel.Text = "That file isn't a texture DSPRE can read (BTX0, 16-color format).";
                        }
                        else
                        {
                            targetWidth = bmp.Width;
                            targetHeight = bmp.Height;
                            targetColors = BTX0.ColorCount;
                            imagePreview.Image = new Bitmap(bmp);
                            imageInfoLabel.Text = "Your texture: " + targetWidth + "x" + targetHeight + ", " + targetColors + " colors (already ROM-native) - pick a slot below marked [fits] (fits your image).";
                            statusLabel.Text = "";
                        }
                    }
                }
                catch (Exception ex)
                {
                    rawBtxPath = null;
                    targetWidth = targetHeight = 0;
                    imagePreview.Image = null;
                    imageInfoLabel.Text = "";
                    statusLabel.Text = "Could not read that file: " + ex.Message;
                }
                clearImageButton.Enabled = pngPath != null || rawBtxPath != null;
                UpdateSlotLabel();
                RebuildSlotOptions();
            }
        }

        private void ClearImageButton_Click(object sender, EventArgs e)
        {
            pngPath = null;
            rawBtxPath = null;
            targetWidth = targetHeight = 0;
            targetColors = 0;
            imagePreview.Image = null;
            imageInfoLabel.Text = "";
            statusLabel.Text = "";
            clearImageButton.Enabled = false;
            UpdateSlotLabel();
            RebuildSlotOptions();
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            statusLabel.Text = "";

            uint appearanceId;
            if (!TryParseId(appearanceIdTextBox.Text, out appearanceId))
            {
                statusLabel.Text = "Appearance ID: not a valid number (decimal or 0x hex).";
                return;
            }

            var slot = slotCombo.SelectedItem as SlotOption;
            var clone = cloneCombo.SelectedItem as CloneOption;
            if (slot == null || clone == null)
            {
                statusLabel.Text = "Choose a texture slot and a clone source.";
                return;
            }

            // If an image was picked, the chosen slot is only a read-only structural template
            // (BTX0.Write needs a matching width/height/color-budget donor to patch). The actual
            // pixels always land in a brand-new, independent mmodel slot so importing an image can
            // never overwrite another overworld entry's texture. Without an image, the entry just
            // points at the picked slot directly and shares that art on purpose (no write happens).
            bool hasImage = rawBtxPath != null || pngPath != null;
            uint mmodelMember = hasImage ? OverworldSpriteTableExpansion.AllocateNewMmodelSlot() : slot.Id;

            string error;
            if (!OverworldSpriteTableExpansion.AddEntry(appearanceId, mmodelMember, clone.Id, out error))
            {
                statusLabel.Text = error;
                return;
            }

            string imageError = null;
            if (rawBtxPath != null) imageError = StageRawBtx(slot.Id, mmodelMember, rawBtxPath);
            else if (pngPath != null) imageError = StagePng(slot.Id, mmodelMember, pngPath);

            AddedAppearanceId = appearanceId;

            if (imageError != null)
            {
                MessageBox.Show(this, "The entry was added, but the image import failed:\n\n" + imageError, "Partial success", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        /// <summary>Reads <paramref name="templateMember"/>'s existing BTX0 file purely as a
        /// read-only structural template (its bytes are never written back to that slot) and writes
        /// the patched result into <paramref name="destMember"/>'s own (already-allocated,
        /// independent) file. Returns null on success.</summary>
        private static string StagePng(uint templateMember, uint destMember, string pngPath)
        {
            string templatePath = Path.Combine(RomInfo.gameDirs[DirNames.OWSprites].unpackedDir, templateMember.ToString("D4"));
            string destPath = Path.Combine(RomInfo.gameDirs[DirNames.OWSprites].unpackedDir, destMember.ToString("D4"));
            if (!File.Exists(templatePath)) return "Template texture slot file not found.";
            try
            {
                byte[] btxData = File.ReadAllBytes(templatePath); // fresh read every call, safe for BTX0.Write to mutate in place
                using (var target = BTX0.Read(btxData))
                using (var import = new Bitmap(pngPath))
                {
                    if (target == null) return "Template texture slot is unreadable.";
                    if (import.Width != target.Width || import.Height != target.Height)
                        return "Size mismatch. Template slot: " + target.Width + "x" + target.Height + ", PNG: " + import.Width + "x" + import.Height;

                    uint colors = GetColorCount(import);
                    if (colors > BTX0.ColorCount)
                        return "Too many colors. Limit: " + BTX0.ColorCount + ", PNG: " + colors;

                    byte[] newData = BTX0.Write(btxData, import);
                    File.WriteAllBytes(destPath, newData);
                    return null;
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>Reads <paramref name="templateMember"/>'s existing BTX0 file purely as a
        /// read-only structural template (never modified) and copies <paramref name="rawBtxPath"/>'s
        /// texture data pixel-perfect into <paramref name="destMember"/>'s own (already-allocated,
        /// independent) file. Returns null on success.</summary>
        private static string StageRawBtx(uint templateMember, uint destMember, string rawBtxPath)
        {
            string templatePath = Path.Combine(RomInfo.gameDirs[DirNames.OWSprites].unpackedDir, templateMember.ToString("D4"));
            string destPath = Path.Combine(RomInfo.gameDirs[DirNames.OWSprites].unpackedDir, destMember.ToString("D4"));
            if (!File.Exists(templatePath)) return "Template texture slot file not found.";
            try
            {
                byte[] sourceData = File.ReadAllBytes(rawBtxPath);
                using (var target = BTX0.Read(File.ReadAllBytes(templatePath)))
                using (var source = BTX0.Read(sourceData))
                {
                    if (target == null) return "Template texture slot is unreadable.";
                    if (source == null) return "Source file isn't a texture DSPRE can read (BTX0, 16-color format).";
                    if (source.Width != target.Width || source.Height != target.Height)
                        return "Size mismatch. Template slot: " + target.Width + "x" + target.Height + ", source texture: " + source.Width + "x" + source.Height;

                    File.WriteAllBytes(destPath, sourceData);
                    return null;
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static bool TryParseId(string text, out uint value)
        {
            text = (text ?? "").Trim();
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return uint.TryParse(text.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            return uint.TryParse(text, out value);
        }

        private static uint GetColorCount(Bitmap bmp)
        {
            var seen = new HashSet<Color>();
            for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < bmp.Width; x++)
                    seen.Add(bmp.GetPixel(x, y));
            return (uint)seen.Count;
        }
    }
}
