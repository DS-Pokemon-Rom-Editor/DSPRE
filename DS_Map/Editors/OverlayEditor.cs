using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static ScintillaNET.Style;

namespace DSPRE
{
    public partial class OverlayEditor : Form
    {

        private List<Overlay> overlays;
        private bool currentValComp = true;
        private bool currentValMark = true;

        public OverlayEditor()
        {
            InitializeComponent();
            overlays = new List<Overlay>();
            int numOverlays = OverlayUtils.OverlayTable.GetNumberOfOverlays();
            for (int i = 0; i < numOverlays; i++)
            {
                Overlay ovl = new Overlay
                {
                    number = i,
                    isCompressed = OverlayUtils.IsCompressed(i),
                    isMarkedCompressed = OverlayUtils.OverlayTable.IsDefaultCompressed(i),
                    RAMAddress = OverlayUtils.OverlayTable.GetRAMAddress(i),
                    uncompressedSize = OverlayUtils.OverlayTable.GetUncompressedSize(i)
                };
                overlays.Add(ovl);
            }
            overlayDataGrid.DataSource = overlays;
            overlayDataGrid.Columns[0].HeaderText = "Overlay ID";
            overlayDataGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.ColumnHeader;
            overlayDataGrid.AllowUserToResizeColumns = false;

            if (RomInfo.IsDsRomProject)
            {
                // For ds-rom: hide "Compressed" column, only show "Will Compress On Build"
                overlayDataGrid.Columns[1].Visible = false;
                overlayDataGrid.Columns[2].HeaderText = "Will Compress On Build";
            }
            else
            {
                // For ndstool: show both columns
                overlayDataGrid.Columns[1].HeaderText = "Compressed";
                overlayDataGrid.Columns[2].HeaderText = "Marked Compressed";
            }

            overlayDataGrid.Columns[3].HeaderText = "RAM Address";
            overlayDataGrid.Columns[4].HeaderText = "Uncompressed Size";
            overlayDataGrid.Columns[0].ReadOnly = true;
            overlayDataGrid.Columns[3].ReadOnly = true;
            overlayDataGrid.Columns[4].ReadOnly = true;


            // ====================================================
            // TEMPORARY DISABLE UNTIL THE COMPRESSION IS FIXED
            // ====================================================
            isCompressedButton.Text = "Decompress All";
            // ====================================================
            // ====================================================
            // ====================================================


            // Register the new event handler for real-time checkbox updates
            overlayDataGrid.CurrentCellDirtyStateChanged += overlayDataGrid_CurrentCellDirtyStateChanged;
            overlayDataGrid.DataBindingComplete += overlayDataGrid_DataBindingComplete;

            if (RomInfo.IsDsRomProject)
            {
                Panel infoPanel = new Panel
                {
                    BackColor = Color.FromArgb(255, 255, 225),
                    BorderStyle = BorderStyle.FixedSingle,
                    Location = new Point(12, 12),
                    Size = new Size(517, 80),
                    Padding = new Padding(10)
                };

                Label infoTitle = new Label
                {
                    Text = "READ-ONLY MODE: ds-rom Automatic Compression",
                    Font = new Font("Segoe UI", 9.75F, FontStyle.Bold),
                    AutoSize = true,
                    Location = new Point(45, 8)
                };

                Label infoText = new Label
                {
                    Text = "All overlays are stored UNCOMPRESSED on disk for editing.\n" +
                           "The \"Will Compress On Build\" column shows which overlays will be compressed when you save the ROM.\n" +
                           "Compression settings are managed in overlays.yaml and cannot be modified here.",
                    Font = new Font("Segoe UI", 8.25F),
                    AutoSize = false,
                    Size = new Size(450, 50),
                    Location = new Point(45, 28)
                };

                infoPanel.Controls.Add(infoTitle);
                infoPanel.Controls.Add(infoText);
                this.Controls.Add(infoPanel);
                infoPanel.BringToFront();

                overlayDataGrid.Location = new Point(12, 100);
                overlayDataGrid.Size = new Size(517, 183);

                this.ClientSize = new Size(539, 344);

                overlayDataGrid.Columns[1].ReadOnly = true;
                overlayDataGrid.Columns[2].ReadOnly = true;

                isCompressedButton.Enabled = false;
                isMarkedCompressedButton.Enabled = false;
                saveChangesButton.Enabled = false;
                revertChangesButton.Enabled = false;

                ToolTip tooltip = new ToolTip();
                tooltip.SetToolTip(isCompressedButton, "ds-rom automatically handles overlay compression during ROM build");
                tooltip.SetToolTip(isMarkedCompressedButton, "ds-rom automatically handles overlay compression during ROM build");
                tooltip.SetToolTip(saveChangesButton, "ds-rom automatically handles overlay compression during ROM build");
                tooltip.SetToolTip(revertChangesButton, "ds-rom automatically handles overlay compression during ROM build");
                tooltip.SetToolTip(overlayDataGrid, "Overlay compression is managed automatically by ds-rom (read-only)");

                isCompressedButton.Text = "N/A (ds-rom)";
                isMarkedCompressedButton.Text = "N/A (ds-rom)";
            }
        }

        // new event handler to ensure mismatch highlighting after data binding
        // seems that its because in the constructor the databinding is not done so cant make mismatch search work there
        private void overlayDataGrid_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            FindMismatches(); // Apply mismatch highlighting after data binding is complete
        }

        // New event handler for real-time checkbox updates
        private void overlayDataGrid_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (overlayDataGrid.CurrentCell is DataGridViewCheckBoxCell)
            {
                // Commit the edit immediately to update the underlying data
                overlayDataGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                // Refresh the mismatch highlighting
                FindMismatches();
            }
        }

        private void isMarkedCompressedButton_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow r in overlayDataGrid.Rows)
            {
                r.Cells[2].Value = currentValMark;
            }
            currentValMark = !currentValMark;
            FindMismatches(); // Update highlighting after button click
        }

        private void isCompressedButton_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow r in overlayDataGrid.Rows)
            {
                r.Cells[1].Value = currentValComp;
            }
            currentValComp = !currentValComp;
            FindMismatches(); // Update highlighting after button click
        }

        private void revertChangesButton_Click(object sender, EventArgs e)
        {
            overlays = new List<Overlay>();
            int numOverlays = OverlayUtils.OverlayTable.GetNumberOfOverlays();
            for (int i = 0; i < numOverlays; i++)
            {
                Overlay ovl = new Overlay();
                ovl.number = i;
                ovl.isCompressed = OverlayUtils.IsCompressed(i);
                ovl.isMarkedCompressed = OverlayUtils.OverlayTable.IsDefaultCompressed(i);
                ovl.RAMAddress = OverlayUtils.OverlayTable.GetRAMAddress(i);
                ovl.uncompressedSize = OverlayUtils.OverlayTable.GetUncompressedSize(i);
                overlays.Add(ovl);
            }
            overlayDataGrid.DataSource = overlays;
            FindMismatches(); // Update highlighting after button click
        }

        private void overlayDataGrid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.ColumnIndex == 3 && e.Value != null)
            {
                long value = 0;
                if (long.TryParse(e.Value.ToString(), out value))
                {
                    e.Value = "0x" + value.ToString("X");
                    e.FormattingApplied = true;
                }
            }
        }

        private void saveChangesButton_Click(object sender, EventArgs e)
        {
            if (RomInfo.IsDsRomProject)
            {
                MessageBox.Show("Overlay compression cannot be modified in ds-rom format.\n\nds-rom automatically decompresses overlays when extracting and recompresses them when building the ROM.",
                    "Read-Only Mode", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<Overlay> originalOverlays = new List<Overlay>();
            int numOverlays = OverlayUtils.OverlayTable.GetNumberOfOverlays();
            for (int i = 0; i < numOverlays; i++)
            {
                Overlay ovl = new Overlay();
                ovl.number = i;
                ovl.isCompressed = OverlayUtils.IsCompressed(i);
                ovl.isMarkedCompressed = OverlayUtils.OverlayTable.IsDefaultCompressed(i);
                ovl.RAMAddress = OverlayUtils.OverlayTable.GetRAMAddress(i);
                ovl.uncompressedSize = OverlayUtils.OverlayTable.GetUncompressedSize(i);
                originalOverlays.Add(ovl);
            }
            List<string> modifiedNumbers = new List<string>();
            List<Overlay> modifiedOverlays = new List<Overlay>();
            for (int i = 0; i < originalOverlays.Count; i++)
            {
                Overlay originalOverlay = originalOverlays[i];
                Overlay newOverlay = overlays[i];

                // Compare properties
                if (originalOverlay.isCompressed != newOverlay.isCompressed || originalOverlay.isMarkedCompressed != newOverlay.isMarkedCompressed)
                {
                    modifiedOverlays.Add(newOverlay);
                    modifiedNumbers.Add(newOverlay.number.ToString());
                }
            }

            if (FindMismatches(false))
            {
                MessageBox.Show("There are some overlays in a compression state that does not match the set value for compression in the y9 table.\n"
                    + "This may cause errors or lack of usability on hardware.\n"
                    + "You can find the mismatched cells coloured in RED.\nThis message is purely informational.", "Compression Mark Mismatch", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            DialogResult d = MessageBox.Show("This operation will modify the following overlays: " + Environment.NewLine
                + String.Join(", ", modifiedNumbers)
                + "\nProceed?", "Confirmation required", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            // ====================================================
            // TEMPORARY DISABLE UNTIL THE COMPRESSION IS FIXED
            // ====================================================
            bool hasCompressing = false;
            // ====================================================
            // ====================================================
            // ====================================================

            if (d == DialogResult.Yes)
            {
                foreach (Overlay overlay in modifiedOverlays)
                {
                    OverlayUtils.OverlayTable.SetDefaultCompressed(overlay.number, overlay.isMarkedCompressed);
                    if (overlay.isCompressed && !OverlayUtils.IsCompressed(overlay.number))
                        // OverlayUtils.Compress(overlay.number);

                        // ====================================================
                        // TEMPORARY DISABLE UNTIL THE COMPRESSION IS FIXED
                        // ====================================================
                        hasCompressing = true;
                    // ====================================================
                    // ====================================================
                    // ====================================================
                    if (!overlay.isCompressed && OverlayUtils.IsCompressed(overlay.number))
                        OverlayUtils.Decompress(overlay.number);
                }
            }
            // ====================================================
            // TEMPORARY DISABLE UNTIL THE COMPRESSION IS FIXED
            // ====================================================
            if (hasCompressing)
                MessageBox.Show("Compression is temporarily disabled until we work on a fix.", "Warning",
                    MessageBoxButtons.OK);
            // ====================================================
            // ====================================================
            // ====================================================
        }

        private bool FindMismatches(bool paintThem = true)
        {
            if (RomInfo.IsDsRomProject)
            {
                foreach (DataGridViewRow row in overlayDataGrid.Rows)
                {
                    row.Cells[1].Style.BackColor = Color.White;
                    row.Cells[2].Style.BackColor = Color.White;
                }
                return false;
            }

            foreach (DataGridViewRow row in overlayDataGrid.Rows)
            {
                if ((bool)row.Cells[1].Value != (bool)row.Cells[2].Value)
                {
                    if (paintThem)
                    {
                        row.Cells[1].Style.BackColor = Color.Red;
                        row.Cells[2].Style.BackColor = Color.Red;
                    }
                    else
                    {
                        return true;
                    }
                }
                else
                {
                    if (paintThem)
                    {
                        row.Cells[1].Style.BackColor = Color.White;
                        row.Cells[2].Style.BackColor = Color.White;
                    }
                }
            }
            return false;
        }

        private void overlayDataGrid_SelectionChanged(object sender, EventArgs e)
        {
            overlayDataGrid.ClearSelection();
        }

        private void overlayDataGrid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            FindMismatches();
        }
    }

    public class Overlay
    {
        public int number { get; set; }
        public bool isCompressed { get; set; }
        public bool isMarkedCompressed { get; set; }
        public uint RAMAddress { get; set; }
        public uint uncompressedSize { get; set; }
    }
}
