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
        private const int COMMON_ITEMS_COUNT = 18; // 18 pairs
        private const int RARE_ITEMS_COUNT = 11;   // 11 pairs
        private const int WEIGHT_TABLE_SIZE = 9;   // 9 weight thresholds

        private bool pickupTableEditorIsReady = false;
        private bool isDirty = false;
        private string[] itemNames;

        // Store the item IDs
        private List<ushort> commonItemIDs = new List<ushort>();
        private List<ushort> rareItemIDs = new List<ushort>();

        // Store the pickup activation divisor (used in modulo operation: BattleSystem_Random() % divisor)
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

            itemNames = RomInfo.GetItemNames();

            // Initialize combo box columns with item names
            InitializeComboBoxColumns();

            // Decompress overlay if needed
            if (OverlayUtils.IsCompressed(RomInfo.pickupTableOverlayNumber))
            {
                OverlayUtils.Decompress(RomInfo.pickupTableOverlayNumber);
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

        private void LoadPickupTable()
        {
            string overlayPath = OverlayUtils.GetPath(RomInfo.pickupTableOverlayNumber);

            // Read common items
            commonItemIDs.Clear();
            byte[] commonData = DSUtils.ReadFromFile(overlayPath, RomInfo.pickupCommonItemsOffset, COMMON_ITEMS_COUNT * 2);
            for (int i = 0; i < COMMON_ITEMS_COUNT; i++)
            {
                ushort itemID = BitConverter.ToUInt16(commonData, i * 2);
                commonItemIDs.Add(itemID);
            }

            // Read rare items
            rareItemIDs.Clear();
            byte[] rareData = DSUtils.ReadFromFile(overlayPath, RomInfo.pickupRareItemsOffset, RARE_ITEMS_COUNT * 2);
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
            string overlayPath = OverlayUtils.GetPath(RomInfo.pickupTableOverlayNumber);

            // Read the activation divisor (1 byte, should be 0x0A = 10)
            // Note: Even though it's used in _s32_div_f, the actual value stored is just 1 byte
            byte[] divisorData = DSUtils.ReadFromFile(overlayPath, RomInfo.pickupActivationDivisorOffset, 1);
            activationDivisor = divisorData[0];

            // Read the 9-byte pickup weight table
            byte[] weightData = DSUtils.ReadFromFile(overlayPath, RomInfo.pickupWeightTableOffset, WEIGHT_TABLE_SIZE);
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
                // 1. First check: activationChance% to activate (BattleSystem_Random(battleSystem) % divisor)
                //    - Modulo operation: % 10 = 10% (1/10), % 3 = 33.33% (1/3), % 5 = 20% (1/5), etc.
                // 2. If activated, roll 0-99 for slot selection
                // 3. For slots 1-9: check if roll < threshold (cumulative)
                // 4. For rare items: check if roll >= 98 && roll <= 99 (2% of activation)

                double activationChance = (100.0 / activationDivisor); // 1/divisor converted to percentage

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
                divisorRow.Cells[3].Value = "Modulo divisor (1-255)";
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
                string overlayPath = OverlayUtils.GetPath(RomInfo.pickupTableOverlayNumber);

                // Write common items
                byte[] commonData = new byte[COMMON_ITEMS_COUNT * 2];
                for (int i = 0; i < commonItemIDs.Count; i++)
                {
                    byte[] itemBytes = BitConverter.GetBytes(commonItemIDs[i]);
                    Array.Copy(itemBytes, 0, commonData, i * 2, 2);
                }
                DSUtils.WriteToFile(overlayPath, commonData, RomInfo.pickupCommonItemsOffset);

                // Write rare items
                byte[] rareData = new byte[RARE_ITEMS_COUNT * 2];
                for (int i = 0; i < rareItemIDs.Count; i++)
                {
                    byte[] itemBytes = BitConverter.GetBytes(rareItemIDs[i]);
                    Array.Copy(itemBytes, 0, rareData, i * 2, 2);
                }
                DSUtils.WriteToFile(overlayPath, rareData, RomInfo.pickupRareItemsOffset);

                // Write activation divisor (1 byte)
                byte[] divisorData = new byte[1] { (byte)activationDivisor };
                DSUtils.WriteToFile(overlayPath, divisorData, RomInfo.pickupActivationDivisorOffset);

                // Write activation weight table
                DSUtils.WriteToFile(overlayPath, pickupWeightTable, RomInfo.pickupWeightTableOffset);

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
                    // Validate divisor is positive (1-255)
                    if (newDivisor > 0)
                    {
                        activationDivisor = newDivisor;
                        SetDirty();
                        // Defer UI refresh until cell editing completes to avoid NullReferenceException
                        BeginInvoke(new Action(() => PopulateActivationOddsUI()));
                    }
                    else
                    {
                        MessageBox.Show("Activation divisor must be a positive value (1-255).\n\n" +
                            "This is used in the modulo operation: BattleSystem_Random() % divisor\n" +
                            "Probability = 1/divisor converted to percentage:\n" +
                            "  % 10 = 10% chance (1/10)\n" +
                            "  % 3 = 33.33% chance (1/3)\n" +
                            "  % 5 = 20% chance (1/5)\n" +
                            "  etc.",
                            "Invalid Divisor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        // Defer UI refresh until cell editing completes to avoid NullReferenceException
                        BeginInvoke(new Action(() => PopulateActivationOddsUI()));
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
                            // Defer UI refresh until cell editing completes to avoid NullReferenceException
                            BeginInvoke(new Action(() => PopulateActivationOddsUI()));
                        }
                        else
                        {
                            MessageBox.Show($"Threshold must be between {prevThreshold} and {nextThreshold}.",
                                "Invalid Threshold", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            // Defer UI refresh until cell editing completes to avoid NullReferenceException
                            BeginInvoke(new Action(() => PopulateActivationOddsUI()));
                        }
                    }
                    else
                    {
                        MessageBox.Show("Threshold must be between 0 and 100.",
                            "Invalid Threshold", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        // Defer UI refresh until cell editing completes to avoid NullReferenceException
                        BeginInvoke(new Action(() => PopulateActivationOddsUI()));
                    }
                }
            }
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            SavePickupTable();
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
