using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace DSPRE
{
    public class SyntheticOverlayOffsetDialog : Form
    {
        private readonly string overlayPath;
        private readonly byte[] expectedBytes;
        private readonly uint loadAddress;

        private TextBox offsetTextBox;
        private Label rangeLabel;
        private Label runtimeAddressLabel;
        private Label statusLabel;
        private Button okButton;
        private Button cancelButton;

        private bool selectedRangeOccupied;

        public uint SelectedOffset { get; private set; }

        public SyntheticOverlayOffsetDialog(string patchName, string overlayPath, uint defaultOffset, byte[] expectedBytes, uint loadAddress)
        {
            this.overlayPath = overlayPath;
            this.expectedBytes = expectedBytes;
            this.loadAddress = loadAddress;

            InitializeComponent(patchName, defaultOffset);
            EvaluateOffset();
        }

        private void InitializeComponent(string patchName, uint defaultOffset)
        {
            Text = "Choose synthetic overlay offset";
            Size = new Size(470, 245);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 6,
                Padding = new Padding(12)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

            var messageLabel = new Label
            {
                Text = patchName + " will be written to the synthetic overlay. Enter the file offset to use.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            layout.Controls.Add(messageLabel, 0, 0);
            layout.SetColumnSpan(messageLabel, 2);

            layout.Controls.Add(new Label { Text = "Offset:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 1);
            offsetTextBox = new TextBox { Dock = DockStyle.Fill, Text = defaultOffset.ToString("X") };
            offsetTextBox.TextChanged += (sender, args) => EvaluateOffset();
            layout.Controls.Add(offsetTextBox, 1, 1);

            rangeLabel = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            layout.Controls.Add(new Label { Text = "File range:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 2);
            layout.Controls.Add(rangeLabel, 1, 2);

            runtimeAddressLabel = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            layout.Controls.Add(new Label { Text = "Runtime address:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 3);
            layout.Controls.Add(runtimeAddressLabel, 1, 3);

            statusLabel = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            layout.Controls.Add(new Label { Text = "Status:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 4);
            layout.Controls.Add(statusLabel, 1, 4);

            var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90 };
            okButton = new Button { Text = "OK", Width = 90 };
            okButton.Click += OkButton_Click;
            buttonPanel.Controls.Add(cancelButton);
            buttonPanel.Controls.Add(okButton);
            layout.Controls.Add(buttonPanel, 0, 5);
            layout.SetColumnSpan(buttonPanel, 2);

            Controls.Add(layout);
            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        private void EvaluateOffset()
        {
            okButton.Enabled = false;
            selectedRangeOccupied = false;
            statusLabel.ForeColor = SystemColors.ControlText;

            string value = offsetTextBox.Text.Trim();
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(2);
            }

            if (!uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint offset))
            {
                SetStatus("Enter a valid hexadecimal offset.", Color.Firebrick);
                return;
            }

            SelectedOffset = offset;
            uint endOffset = offset + (uint)expectedBytes.Length - 1;
            rangeLabel.Text = "0x" + offset.ToString("X") + " - 0x" + endOffset.ToString("X");
            runtimeAddressLabel.Text = "0x" + (loadAddress + offset).ToString("X8");

            if (!File.Exists(overlayPath))
            {
                SetStatus("Synthetic overlay file was not found.", Color.Firebrick);
                return;
            }

            long fileLength = new FileInfo(overlayPath).Length;
            if (offset >= fileLength || (long)offset + expectedBytes.Length > fileLength)
            {
                SetStatus("Selected range is outside the synthetic overlay file.", Color.Firebrick);
                return;
            }

            byte[] currentBytes = DSUtils.ReadFromFile(overlayPath, offset, expectedBytes.Length);
            if (currentBytes.Length != expectedBytes.Length)
            {
                SetStatus("Could not read the selected range.", Color.Firebrick);
                return;
            }

            if (currentBytes.SequenceEqual(expectedBytes))
            {
                SetStatus("This range already contains the expected patch bytes.", Color.DarkGreen);
                okButton.Enabled = true;
                return;
            }

            if (currentBytes.All(b => b == 0))
            {
                SetStatus("This range is empty.", Color.DarkGreen);
                okButton.Enabled = true;
                return;
            }

            selectedRangeOccupied = true;
            SetStatus("This range already contains data. Continuing will overwrite it.", Color.Firebrick);
            okButton.Enabled = true;
        }

        private void SetStatus(string status, Color color)
        {
            statusLabel.Text = status;
            statusLabel.ForeColor = color;
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            if (selectedRangeOccupied)
            {
                DialogResult result = MessageBox.Show(
                    "The selected synthetic overlay range already contains data.\n\n" +
                    "Overwriting it can break another patch or custom code.\n\n" +
                    "Do you want to overwrite this range anyway?",
                    "Overwrite occupied synthetic overlay range?",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result != DialogResult.Yes)
                {
                    return;
                }
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
