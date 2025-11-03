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
            int numOverlays = LegacyOverlayUtils.OverlayTable.GetNumberOfOverlays();
            for (int i = 0; i < numOverlays; i++)
            {
                Overlay ovl = new Overlay
                {
                    number = i,
                    isCompressed = LegacyOverlayUtils.IsCompressed(i),
                    isMarkedCompressed = LegacyOverlayUtils.OverlayTable.IsDefaultCompressed(i),
                    RAMAddress = LegacyOverlayUtils.OverlayTable.GetRAMAddress(i),
                    uncompressedSize = LegacyOverlayUtils.OverlayTable.GetUncompressedSize(i)
                };
                overlays.Add(ovl);
            }
            overlayDataGrid.DataSource = overlays;
            overlayDataGrid.Columns[0].HeaderText = "Overlay ID";
            overlayDataGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.ColumnHeader;
            overlayDataGrid.AllowUserToResizeColumns = false;
            overlayDataGrid.Columns[3].HeaderText = "RAM Address";
            overlayDataGrid.Columns[4].HeaderText = "Uncompressed Size";
            overlayDataGrid.Columns[0].ReadOnly = true;
            overlayDataGrid.Columns[3].ReadOnly = true;
            overlayDataGrid.Columns[4].ReadOnly = true;

            // Configure UI based on mode
            if (DSUtils.legacyMode)
            {
                // Legacy mode: Show both columns and both buttons
                overlayDataGrid.Columns[1].HeaderText = "Compressed";
                overlayDataGrid.Columns[2].HeaderText = "Marked Compressed";
                overlayDataGrid.Columns[1].Visible = true;
                overlayDataGrid.Columns[2].Visible = true;
                isCompressedButton.Visible = true;
                isMarkedCompressedButton.Text = "Mark/Unmark Compression All";
            }
            else
            {
                // dsrom mode: Hide "Compressed" column and compress/decompress button
                overlayDataGrid.Columns[1].Visible = false;
                overlayDataGrid.Columns[2].HeaderText = "Recompress";
                overlayDataGrid.Columns[2].Visible = true;
                isCompressedButton.Visible = false; // Hide compress/decompress button in dsrom mode
                isMarkedCompressedButton.Text = "Toggle Recompression All";
            }

            // Register the new event handler for real-time checkbox updates
            overlayDataGrid.CurrentCellDirtyStateChanged += overlayDataGrid_CurrentCellDirtyStateChanged;
            overlayDataGrid.DataBindingComplete += overlayDataGrid_DataBindingComplete;
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
            int numOverlays = LegacyOverlayUtils.OverlayTable.GetNumberOfOverlays();
            for (int i = 0; i < numOverlays; i++)
            {
                Overlay ovl = new Overlay();
                ovl.number = i;
                ovl.isCompressed = LegacyOverlayUtils.IsCompressed(i);
                ovl.isMarkedCompressed = LegacyOverlayUtils.OverlayTable.IsDefaultCompressed(i);
                ovl.RAMAddress = LegacyOverlayUtils.OverlayTable.GetRAMAddress(i);
                ovl.uncompressedSize = LegacyOverlayUtils.OverlayTable.GetUncompressedSize(i);
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
            // This whole function needs proper optimizing, im just making dumb lists
            List<Overlay> originalOverlays = new List<Overlay>();
            int numOverlays = LegacyOverlayUtils.OverlayTable.GetNumberOfOverlays();
            for (int i = 0; i < numOverlays; i++)
            {
                Overlay ovl = new Overlay();
                ovl.number = i;
                ovl.isCompressed = LegacyOverlayUtils.IsCompressed(i);
                ovl.isMarkedCompressed = LegacyOverlayUtils.OverlayTable.IsDefaultCompressed(i);
                ovl.RAMAddress = LegacyOverlayUtils.OverlayTable.GetRAMAddress(i);
                ovl.uncompressedSize = LegacyOverlayUtils.OverlayTable.GetUncompressedSize(i);
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

            // In dsrom mode, overlays are always decompressed on disk, so only warn if marked compressed doesn't match user's intent
            // In legacy mode, warn if actual compression state doesn't match marked state
            if (!DSUtils.legacyMode)
            {
                // dsrom mode: No mismatch warning needed - overlays are always decompressed, user is just setting recompression flag
            }
            else if (FindMismatches(false))
            {
                // Legacy mode: Warn about mismatches
                MessageBox.Show("There are some overlays in a compression state that does not match the set value for compression in the y9 table.\n"
                    + "This may cause errors or lack of usability on hardware.\n"
                    + "You can find the mismatched cells coloured in RED.\nThis message is purely informational.", "Compression Mark Mismatch", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            if (modifiedNumbers.Count == 0)
            {
                MessageBox.Show("No changes to save.", "No Changes", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult d = MessageBox.Show("This operation will modify the following overlays: " + Environment.NewLine
                + String.Join(", ", modifiedNumbers)
                + "\nProceed?", "Confirmation required", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (d == DialogResult.Yes)
            {
                foreach (Overlay overlay in modifiedOverlays)
                {
                    if (DSUtils.legacyMode)
                    {
                        // Legacy mode: Update y9.bin and compress/decompress files
                        LegacyOverlayUtils.OverlayTable.SetDefaultCompressed(overlay.number, overlay.isMarkedCompressed);
                        if (overlay.isCompressed && !LegacyOverlayUtils.IsCompressed(overlay.number))
                            LegacyOverlayUtils.Compress(overlay.number);
                        if (!overlay.isCompressed && LegacyOverlayUtils.IsCompressed(overlay.number))
                            LegacyOverlayUtils.Decompress(overlay.number);
                    }
                    else
                    {
                        // dsrom mode: Only update the YAML recompression flag
                        OverlayUtils.OverlayTable.SetRecompress(overlay.number, overlay.isMarkedCompressed);
                    }
                }

                // Save the overlay table
                if (!DSUtils.legacyMode)
                {
                    if (OverlayUtils.OverlayTable.SaveOverlayTable())
                    {
                        MessageBox.Show("Overlay table saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                else
                {
                    MessageBox.Show("Changes saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private bool FindMismatches(bool paintThem = true)
        {
            bool foundMismatch = false;

            foreach (DataGridViewRow row in overlayDataGrid.Rows)
            {
                bool isCompressed = (bool)row.Cells[1].Value;
                bool isMarkedCompressed = (bool)row.Cells[2].Value;
                bool shouldHighlight = false;

                if (!DSUtils.legacyMode)
                {
                    // dsrom mode: Overlays are always decompressed on disk, so "Compressed" column will always be false
                    // Don't highlight anything - the mismatch between false/true is expected and normal
                    shouldHighlight = false;
                }
                else
                {
                    // Legacy mode: Highlight when actual compression doesn't match marked compression
                    shouldHighlight = (isCompressed != isMarkedCompressed);
                }

                if (shouldHighlight)
                {
                    if (paintThem)
                    {
                        row.Cells[1].Style.BackColor = Color.Red;
                        row.Cells[2].Style.BackColor = Color.Red;
                    }
                    foundMismatch = true;
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
            return foundMismatch;
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
