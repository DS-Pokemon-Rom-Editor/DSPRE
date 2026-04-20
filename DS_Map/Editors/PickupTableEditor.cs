using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using static DSPRE.RomInfo;

namespace DSPRE.Editors
{
    public partial class PickupTableEditor : UserControl, IEditorWithUnsavedChanges
    {
        private const int OVERLAY_NUMBER = 12;
        private const int COMMON_ITEMS_OFFSET = 0x34B44;
        private const int RARE_ITEMS_OFFSET = 0x34A4C;
        private const int ACTIVATION_DIVISOR_OFFSET = 0xC852; // _s32_div_f divisor for activation chance
        private const int WEIGHT_TABLE_OFFSET = 0x3518C; // sPickupWeightTable
        private const int COMMON_ITEMS_COUNT = 18; // 18 pairs
        private const int RARE_ITEMS_COUNT = 11;   // 11 pairs
        private const int WEIGHT_TABLE_SIZE = 9;   // 9 weight thresholds

        private bool pickupTableEditorIsReady = false;
        private bool isDirty = false;
        private string[] itemNames;

        // Store the item IDs
        private List<ushort> commonItemIDs = new List<ushort>();
        private List<ushort> rareItemIDs = new List<ushort>();

        // Store the pickup activation divisor (must be multiple of 10)
        private int activationDivisor = 10;

        // Store the pickup activation weight table (9 bytes)
        private byte[] pickupWeightTable = new byte[WEIGHT_TABLE_SIZE];

        #region IEditorWithUnsavedChanges Implementation
        public bool HasUnsavedChanges => isDirty;
        public string UnsavedChangesDescription => "Pickup Table Editor";
        public void SaveChanges() => SavePickupTable();
        public void DiscardChanges()
        {
            isDirty = false;
        }
        #endregion

        private void SetDirty()
        {
            if (!isDirty)
            {
                isDirty = true;
            }
        }

        private void SetClean()
        {
            if (isDirty)
            {
                isDirty = false;
            }
        }

        public PickupTableEditor()
        {
            InitializeComponent();
        }

        public void SetupPickupTableEditor(bool force = false)
        {
            if (pickupTableEditorIsReady && !force) { return; }

            // Check if this is HeartGold US
            if (!IsHeartGoldUS())
            {
                ShowNotAvailableMessage();
                return;
            }

            itemNames = RomInfo.GetItemNames();

            // Initialize combo box columns with item names
            InitializeComboBoxColumns();

            // Decompress overlay 12 if needed
            if (OverlayUtils.IsCompressed(OVERLAY_NUMBER))
            {
                OverlayUtils.Decompress(OVERLAY_NUMBER);
            }

            LoadPickupTable();
            LoadActivationOdds();
            pickupTableEditorIsReady = true;
        }

        private void InitializeComboBoxColumns()
        {
            // Create item list with ID: Name format
            var itemList = new List<string>();
            for (int i = 0; i < itemNames.Length; i++)
            {
                itemList.Add($"{i}: {itemNames[i]}");
            }

            // Set up common items combo boxes
            for (int i = 1; i < dataGridViewCommon.Columns.Count; i++)
            {
                if (dataGridViewCommon.Columns[i] is DataGridViewComboBoxColumn comboCol)
                {
                    comboCol.DataSource = itemList.ToArray();
                }
            }

            // Set up rare items combo boxes
            for (int i = 1; i < dataGridViewRare.Columns.Count; i++)
            {
                if (dataGridViewRare.Columns[i] is DataGridViewComboBoxColumn comboCol)
                {
                    comboCol.DataSource = itemList.ToArray();
                }
            }
        }

        private bool IsHeartGoldUS()
        {
            return RomInfo.romID == "IPKE" && RomInfo.gameFamily == GameFamilies.HGSS;
        }

        private void ShowNotAvailableMessage()
        {
            // Create a label to show "not available"
            Label notAvailableLabel = new Label
            {
                Text = "Pickup Table Editor is only available for HeartGold (US) version.\n\n" +
                       "The correct offsets for other game versions are not yet known.",
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(Font.FontFamily, 10, FontStyle.Bold)
            };
            Controls.Clear();
            Controls.Add(notAvailableLabel);
        }

        private void LoadPickupTable()
        {
            string overlayPath = OverlayUtils.GetPath(OVERLAY_NUMBER);

            // Read common items
            commonItemIDs.Clear();
            byte[] commonData = DSUtils.ReadFromFile(overlayPath, COMMON_ITEMS_OFFSET, COMMON_ITEMS_COUNT * 2);
            for (int i = 0; i < COMMON_ITEMS_COUNT; i++)
            {
                ushort itemID = BitConverter.ToUInt16(commonData, i * 2);
                commonItemIDs.Add(itemID);
            }

            // Read rare items
            rareItemIDs.Clear();
            byte[] rareData = DSUtils.ReadFromFile(overlayPath, RARE_ITEMS_OFFSET, RARE_ITEMS_COUNT * 2);
            for (int i = 0; i < RARE_ITEMS_COUNT; i++)
            {
                ushort itemID = BitConverter.ToUInt16(rareData, i * 2);
                rareItemIDs.Add(itemID);
            }

            PopulateCommonItemsUI();
            PopulateRareItemsUI();
        }

        private void LoadActivationOdds()
        {
            string overlayPath = OverlayUtils.GetPath(OVERLAY_NUMBER);

            // Read the activation divisor (1 byte, should be 0x0A = 10)
            // Note: Even though it's used in _s32_div_f, the actual value stored is just 1 byte
            byte[] divisorData = DSUtils.ReadFromFile(overlayPath, ACTIVATION_DIVISOR_OFFSET, 1);
            activationDivisor = divisorData[0];

            // Read the 9-byte pickup weight table
            byte[] weightData = DSUtils.ReadFromFile(overlayPath, WEIGHT_TABLE_OFFSET, WEIGHT_TABLE_SIZE);
            Array.Copy(weightData, pickupWeightTable, WEIGHT_TABLE_SIZE);

            PopulateActivationOddsUI();
        }

        private void PopulateActivationOddsUI()
        {
            Helpers.DisableHandlers();
            try
            {
                dataGridViewActivation.Rows.Clear();

                // Calculate real probabilities based on the game code logic:
                // 1. First check: activationChance% to activate (BattleSystem_Random(battleSystem) % divisor == 0)
                // 2. If activated, roll 0-99 for slot selection
                // 3. For slots 1-9: check if roll < threshold (cumulative)
                // 4. For rare items: check if roll >= 98 && roll <= 99 (2% of activation)

                double activationChance = (100.0 / activationDivisor); // Calculated from divisor

                // Add activation divisor row first
                var divisorRow = new DataGridViewRow();
                divisorRow.CreateCells(dataGridViewActivation);
                divisorRow.Cells[0].Value = "Activation %";
                divisorRow.Cells[0].Style.BackColor = Color.LightYellow;
                divisorRow.Cells[0].Style.Font = new Font(dataGridViewActivation.Font, FontStyle.Bold);
                divisorRow.Cells[1].Value = activationDivisor;
                divisorRow.Cells[1].Style.BackColor = Color.LightYellow;
                divisorRow.Cells[2].Value = $"{activationChance:F2}%";
                divisorRow.Cells[2].Style.BackColor = Color.LightYellow;
                divisorRow.Cells[2].Style.Font = new Font(dataGridViewActivation.Font, FontStyle.Bold);
                divisorRow.Cells[3].Value = "Divisor (must be multiple of 10)";
                divisorRow.Cells[3].Style.BackColor = Color.LightYellow;
                dataGridViewActivation.Rows.Add(divisorRow);

                int prevThreshold = 0;

                for (int i = 0; i < WEIGHT_TABLE_SIZE; i++)
                {
                    int threshold = pickupWeightTable[i];
                    int range = threshold - prevThreshold;

                    // Real probability = (activation chance 10%) * (threshold range / 100)
                    double slotProbability = (activationChance / 100.0) * (range / 100.0) * 100.0;

                    var row = new DataGridViewRow();
                    row.CreateCells(dataGridViewActivation);
                    row.Cells[0].Value = $"Slot {i + 1}";
                    row.Cells[1].Value = threshold;
                    row.Cells[2].Value = $"{slotProbability:F2}%";
                    row.Cells[3].Value = $"{prevThreshold}-{threshold - 1} ({range} values)";

                    dataGridViewActivation.Rows.Add(row);
                    prevThreshold = threshold;
                }

                // Add info for remaining range (98-99 is rare items, 1% overall chance)
                if (prevThreshold < 100)
                {
                    // Values 98-99 trigger rare items (2% of 10% = 0.2% per rare slot)
                    var rareRow = new DataGridViewRow();
                    rareRow.CreateCells(dataGridViewActivation);
                    rareRow.Cells[0].Value = "Rare (98-99)";
                    rareRow.Cells[1].Value = "---";
                    rareRow.Cells[2].Value = "1.0%";
                    rareRow.Cells[3].Value = "98-99 (2 values, 0.5% each slot)";
                    dataGridViewActivation.Rows.Add(rareRow);

                    // If there's a gap between last threshold and 98
                    if (prevThreshold < 98)
                    {
                        int missRange = 98 - prevThreshold;
                        double missProbability = (activationChance / 100.0) * (missRange / 100.0) * 100.0;
                        var missRow = new DataGridViewRow();
                        missRow.CreateCells(dataGridViewActivation);
                        missRow.Cells[0].Value = "No Item";
                        missRow.Cells[1].Value = "---";
                        missRow.Cells[2].Value = $"{missProbability:F2}%";
                        missRow.Cells[3].Value = $"{prevThreshold}-97 ({missRange} values)";
                        dataGridViewActivation.Rows.Add(missRow);
                    }
                }
            }
            finally
            {
                Helpers.EnableHandlers();
            }
        }

        private void PopulateCommonItemsUI()
        {
            Helpers.DisableHandlers();
            try
            {
                dataGridViewCommon.Rows.Clear();

                // According to the tutorial, the table shifts by 1 value for each bracket
                // Reading 9 values at a time
                for (int bracket = 0; bracket < 10; bracket++)
                {
                    int startIndex = bracket;
                    string levelRange = GetLevelRange(bracket);

                    var row = new DataGridViewRow();
                    row.CreateCells(dataGridViewCommon);
                    row.Cells[0].Value = levelRange;

                    // Add 9 item slots for this bracket
                    for (int slot = 0; slot < 9; slot++)
                    {
                        int itemIndex = startIndex + slot;
                        if (itemIndex < commonItemIDs.Count)
                        {
                            row.Cells[slot + 1].Value = GetItemName(commonItemIDs[itemIndex]);
                        }
                    }

                    dataGridViewCommon.Rows.Add(row);
                }
            }
            finally
            {
                Helpers.EnableHandlers();
            }
        }

        private void PopulateRareItemsUI()
        {
            Helpers.DisableHandlers();
            try
            {
                dataGridViewRare.Rows.Clear();

                // Rare table reads 2 values at a time per bracket, shifting by 1
                for (int bracket = 0; bracket < 10; bracket++)
                {
                    int startIndex = bracket;
                    string levelRange = GetLevelRange(bracket);

                    var row = new DataGridViewRow();
                    row.CreateCells(dataGridViewRare);
                    row.Cells[0].Value = levelRange;

                    // Add 2 item slots for this bracket
                    for (int slot = 0; slot < 2; slot++)
                    {
                        int itemIndex = startIndex + slot;
                        if (itemIndex < rareItemIDs.Count)
                        {
                            row.Cells[slot + 1].Value = GetItemName(rareItemIDs[itemIndex]);
                        }
                    }

                    dataGridViewRare.Rows.Add(row);
                }
            }
            finally
            {
                Helpers.EnableHandlers();
            }
        }

        private string GetLevelRange(int bracket)
        {
            // Level brackets: 1-10, 11-20, 21-30, ..., 91-100
            int minLevel = bracket * 10 + 1;
            int maxLevel = (bracket + 1) * 10;
            return $"{minLevel}-{maxLevel}";
        }

        private string GetItemName(ushort itemID)
        {
            if (itemID < itemNames.Length)
            {
                return $"{itemID}: {itemNames[itemID]}";
            }
            return $"{itemID}: ???";
        }

        private void SavePickupTable()
        {
            try
            {
                string overlayPath = OverlayUtils.GetPath(OVERLAY_NUMBER);

                // Write common items
                byte[] commonData = new byte[COMMON_ITEMS_COUNT * 2];
                for (int i = 0; i < commonItemIDs.Count; i++)
                {
                    byte[] itemBytes = BitConverter.GetBytes(commonItemIDs[i]);
                    Array.Copy(itemBytes, 0, commonData, i * 2, 2);
                }
                DSUtils.WriteToFile(overlayPath, commonData, COMMON_ITEMS_OFFSET);

                // Write rare items
                byte[] rareData = new byte[RARE_ITEMS_COUNT * 2];
                for (int i = 0; i < rareItemIDs.Count; i++)
                {
                    byte[] itemBytes = BitConverter.GetBytes(rareItemIDs[i]);
                    Array.Copy(itemBytes, 0, rareData, i * 2, 2);
                }
                DSUtils.WriteToFile(overlayPath, rareData, RARE_ITEMS_OFFSET);

                // Write activation divisor (1 byte)
                byte[] divisorData = new byte[1] { (byte)activationDivisor };
                DSUtils.WriteToFile(overlayPath, divisorData, ACTIVATION_DIVISOR_OFFSET);

                // Write activation weight table
                DSUtils.WriteToFile(overlayPath, pickupWeightTable, WEIGHT_TABLE_OFFSET);

                SetClean();
                MessageBox.Show("Pickup table saved successfully!", "Save Complete", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving pickup table: {ex.Message}", "Save Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void dataGridViewCommon_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (Helpers.HandlersDisabled || e.RowIndex < 0 || e.ColumnIndex < 1) return;

            // Calculate which item in the list this cell represents
            int bracket = e.RowIndex;
            int slot = e.ColumnIndex - 1; // Subtract 1 for level range column
            int itemIndex = bracket + slot;

            if (itemIndex < commonItemIDs.Count)
            {
                string cellValue = dataGridViewCommon.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString();
                ushort itemID = ParseItemIDFromString(cellValue);
                commonItemIDs[itemIndex] = itemID;
                SetDirty();
            }
        }

        private void dataGridViewRare_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (Helpers.HandlersDisabled || e.RowIndex < 0 || e.ColumnIndex < 1) return;

            // Calculate which item in the list this cell represents
            int bracket = e.RowIndex;
            int slot = e.ColumnIndex - 1; // Subtract 1 for level range column
            int itemIndex = bracket + slot;

            if (itemIndex < rareItemIDs.Count)
            {
                string cellValue = dataGridViewRare.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString();
                ushort itemID = ParseItemIDFromString(cellValue);
                rareItemIDs[itemIndex] = itemID;
                SetDirty();
            }
        }

        private ushort ParseItemIDFromString(string itemString)
        {
            if (string.IsNullOrEmpty(itemString)) return 0;

            // Extract ID from format "ID: Name"
            int colonIndex = itemString.IndexOf(':');
            if (colonIndex > 0)
            {
                string idPart = itemString.Substring(0, colonIndex).Trim();
                if (ushort.TryParse(idPart, out ushort id))
                {
                    return id;
                }
            }

            return 0;
        }

        private void dataGridViewActivation_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (Helpers.HandlersDisabled || e.RowIndex < 0 || e.ColumnIndex != 1) return;

            // First row is activation divisor
            if (e.RowIndex == 0)
            {
                var cellValue = dataGridViewActivation.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
                if (cellValue != null && byte.TryParse(cellValue.ToString(), out byte newDivisor))
                {
                    // Validate divisor is a multiple of 10 and positive
                    if (newDivisor > 0 && newDivisor % 10 == 0)
                    {
                        activationDivisor = newDivisor;
                        SetDirty();
                        PopulateActivationOddsUI(); // Refresh all probabilities
                    }
                    else
                    {
                        MessageBox.Show("Activation divisor must be a positive multiple of 10 (e.g., 10, 20, 30).\n\n" +
                            "This is used in _s32_div_f(random, divisor) to determine activation chance.\n" +
                            "A divisor of 10 = 10% chance, 20 = 5% chance, etc.\n" +
                            "Valid range: 10-250 (must be multiple of 10).",
                            "Invalid Divisor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        PopulateActivationOddsUI(); // Reset to previous value
                    }
                }
                return;
            }

            // Rows 1-9 are threshold values (adjust index by -1 for the divisor row)
            int thresholdIndex = e.RowIndex - 1;
            if (thresholdIndex < WEIGHT_TABLE_SIZE)
            {
                var cellValue = dataGridViewActivation.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
                if (cellValue != null && byte.TryParse(cellValue.ToString(), out byte newThreshold))
                {
                    // Validate threshold is between 0-100 and greater than previous
                    if (newThreshold <= 100)
                    {
                        byte prevThreshold = (thresholdIndex > 0) ? pickupWeightTable[thresholdIndex - 1] : (byte)0;
                        byte nextThreshold = (thresholdIndex < WEIGHT_TABLE_SIZE - 1) ? pickupWeightTable[thresholdIndex + 1] : (byte)100;

                        if (newThreshold > prevThreshold && newThreshold < nextThreshold)
                        {
                            pickupWeightTable[thresholdIndex] = newThreshold;
                            SetDirty();
                            PopulateActivationOddsUI(); // Refresh probabilities
                        }
                        else
                        {
                            MessageBox.Show($"Threshold must be between {prevThreshold} and {nextThreshold}.",
                                "Invalid Threshold", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            PopulateActivationOddsUI(); // Reset to previous value
                        }
                    }
                    else
                    {
                        MessageBox.Show("Threshold must be between 0 and 100.",
                            "Invalid Threshold", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        PopulateActivationOddsUI(); // Reset to previous value
                    }
                }
            }
        }

        public void Reset()
        {
            Helpers.DisableHandlers();
            try
            {
                isDirty = false;
                pickupTableEditorIsReady = false;
                commonItemIDs.Clear();
                rareItemIDs.Clear();
                activationDivisor = 10;
                Array.Clear(pickupWeightTable, 0, WEIGHT_TABLE_SIZE);

                if (dataGridViewCommon != null)
                    dataGridViewCommon.Rows.Clear();
                if (dataGridViewRare != null)
                    dataGridViewRare.Rows.Clear();
                if (dataGridViewActivation != null)
                    dataGridViewActivation.Rows.Clear();
            }
            finally
            {
                Helpers.EnableHandlers();
            }
        }
    }
}
