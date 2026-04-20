using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using static DSPRE.RomInfo;

namespace DSPRE.Editors
{
    public partial class HiddenItemsEditor : UserControl, IEditorWithUnsavedChanges
    {
        // ARM9 offsets for HeartGold US
        private const int POINTER_OFFSET_1 = 0x405A8;
        private const int POINTER_OFFSET_2 = 0x40610;
        private const int TABLE_LENGTH_OFFSET_1 = 0x405E4;
        private const int TABLE_LENGTH_OFFSET_2 = 0x405E8;
        private const int TABLE_OFFSET = 0xFA558;
        private const int ENTRY_SIZE = 8; // 8 bytes per entry
        private const int MAX_ENTRIES = 256; // Vanilla limit

        private bool hiddenItemsEditorIsReady = false;
        private bool isDirty = false;
        private string[] itemNames;
        private int maxCapacity = MAX_ENTRIES; // Read from ROM

        // Hidden item entries
        private List<HiddenItemEntry> hiddenItems = new List<HiddenItemEntry>();

        private class HiddenItemEntry
        {
            public ushort ItemID { get; set; }
            public ushort Amount { get; set; }
            public ushort ScriptID { get; set; }

            public HiddenItemEntry(ushort itemID, ushort amount, ushort scriptID)
            {
                ItemID = itemID;
                Amount = amount;
                ScriptID = scriptID;
            }

            public byte[] ToByteArray()
            {
                byte[] data = new byte[ENTRY_SIZE];

                // Item ID (2 bytes)
                Array.Copy(BitConverter.GetBytes(ItemID), 0, data, 0, 2);

                // Amount (1-2 bytes, we'll use 1 byte for simplicity)
                data[2] = (byte)Amount;

                // Filler zeroes (2-3 bytes)
                data[3] = 0;
                data[4] = 0;
                data[5] = 0;

                // Script ID (2 bytes)
                Array.Copy(BitConverter.GetBytes(ScriptID), 0, data, 6, 2);

                return data;
            }

            public static HiddenItemEntry FromByteArray(byte[] data, int offset)
            {
                ushort itemID = BitConverter.ToUInt16(data, offset);
                byte amount = data[offset + 2];
                ushort scriptID = BitConverter.ToUInt16(data, offset + 6);

                return new HiddenItemEntry(itemID, amount, scriptID);
            }
        }

        #region IEditorWithUnsavedChanges Implementation
        public bool HasUnsavedChanges => isDirty;
        public string UnsavedChangesDescription => "Hidden Items Editor";
        public void SaveChanges() => SaveHiddenItems();
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

        public HiddenItemsEditor()
        {
            InitializeComponent();
        }

        public void SetupHiddenItemsEditor(bool force = false)
        {
            if (hiddenItemsEditorIsReady && !force) { return; }

            // Check if this is HeartGold US
            if (!IsHeartGoldUS())
            {
                ShowNotAvailableMessage();
                return;
            }

            itemNames = RomInfo.GetItemNames();

            // Populate item combo box
            comboBoxItem.Items.Clear();
            comboBoxItem.Items.AddRange(itemNames);

            // Decompress ARM9 if needed
            if (ARM9.CheckCompressionMark())
            {
                ARM9.Decompress(RomInfo.arm9Path);
            }

            LoadHiddenItems();
            hiddenItemsEditorIsReady = true;
        }

        private bool IsHeartGoldUS()
        {
            return RomInfo.romID == "IPKE" && RomInfo.gameFamily == GameFamilies.HGSS;
        }

        private void ShowNotAvailableMessage()
        {
            Label notAvailableLabel = new Label
            {
                Text = "Hidden Items Editor is only available for HeartGold (US) version.\n\n" +
                       "The correct offsets for other game versions are not yet known.",
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(Font.FontFamily, 10, FontStyle.Bold)
            };
            Controls.Clear();
            Controls.Add(notAvailableLabel);
        }

        private void LoadHiddenItems()
        {
            hiddenItems.Clear();

            // Read the current table length from ARM9 (1 byte)
            byte[] lengthData = ARM9.ReadBytes(TABLE_LENGTH_OFFSET_1, 1);
            int tableLength = lengthData[0];

            // Read the max capacity from ARM9 (1 byte)
            byte[] maxCapacityData = ARM9.ReadBytes(TABLE_LENGTH_OFFSET_2, 1);
            maxCapacity = maxCapacityData[0];

            // Sanity check: table length should be reasonable
            if (tableLength < 0 || tableLength > maxCapacity)
            {
                MessageBox.Show($"Invalid hidden items table length: {tableLength}. Using fallback method.",
                    "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                tableLength = Math.Min(MAX_ENTRIES, maxCapacity);
            }

            // Read the table from ARM9
            byte[] tableData = ARM9.ReadBytes(TABLE_OFFSET, tableLength * ENTRY_SIZE);

            // Parse entries (stop at first entry with ItemID = 0 OR at table length)
            for (int i = 0; i < tableLength; i++)
            {
                int offset = i * ENTRY_SIZE;
                ushort itemID = BitConverter.ToUInt16(tableData, offset);

                // Stop at first empty entry
                if (itemID == 0) break;

                HiddenItemEntry entry = HiddenItemEntry.FromByteArray(tableData, offset);
                hiddenItems.Add(entry);
            }

            PopulateUI();
        }

        private void PopulateUI()
        {
            Helpers.DisableHandlers();
            try
            {
                listBoxHiddenItems.Items.Clear();

                for (int i = 0; i < hiddenItems.Count; i++)
                {
                    var item = hiddenItems[i];
                    string displayText = $"Script {item.ScriptID} (8{item.ScriptID:D3}): " +
                                       $"{GetItemName(item.ItemID)} x{item.Amount}";
                    listBoxHiddenItems.Items.Add(displayText);
                }

                if (listBoxHiddenItems.Items.Count > 0)
                {
                    listBoxHiddenItems.SelectedIndex = 0;
                }

                UpdateEntryCount();
            }
            finally
            {
                Helpers.EnableHandlers();
            }
        }

        private void UpdateEntryCount()
        {
            labelEntryCount.Text = $"Entries: {hiddenItems.Count} / {maxCapacity} (Max capacity from ROM)";
        }

        private string GetItemName(ushort itemID)
        {
            if (itemID < itemNames.Length)
            {
                return itemNames[itemID];
            }
            return "???";
        }

        private void listBoxHiddenItems_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (Helpers.HandlersDisabled || listBoxHiddenItems.SelectedIndex < 0) return;

            int index = listBoxHiddenItems.SelectedIndex;
            if (index < hiddenItems.Count)
            {
                var item = hiddenItems[index];

                Helpers.DisableHandlers();
                try
                {
                    comboBoxItem.SelectedIndex = item.ItemID < itemNames.Length ? item.ItemID : 0;
                    numericAmount.Value = item.Amount;
                    numericScriptID.Value = item.ScriptID;
                    labelScriptCall.Text = $"Use in spawnable: 8{item.ScriptID:D3}";
                }
                finally
                {
                    Helpers.EnableHandlers();
                }
            }
        }

        private void comboBoxItem_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (Helpers.HandlersDisabled || listBoxHiddenItems.SelectedIndex < 0) return;

            int index = listBoxHiddenItems.SelectedIndex;
            if (index < hiddenItems.Count && comboBoxItem.SelectedIndex >= 0)
            {
                hiddenItems[index].ItemID = (ushort)comboBoxItem.SelectedIndex;
                UpdateListBoxItem(index);
                SetDirty();
            }
        }

        private void numericAmount_ValueChanged(object sender, EventArgs e)
        {
            if (Helpers.HandlersDisabled || listBoxHiddenItems.SelectedIndex < 0) return;

            int index = listBoxHiddenItems.SelectedIndex;
            if (index < hiddenItems.Count)
            {
                hiddenItems[index].Amount = (ushort)numericAmount.Value;
                UpdateListBoxItem(index);
                SetDirty();
            }
        }

        private void numericScriptID_ValueChanged(object sender, EventArgs e)
        {
            if (Helpers.HandlersDisabled || listBoxHiddenItems.SelectedIndex < 0) return;

            int index = listBoxHiddenItems.SelectedIndex;
            if (index < hiddenItems.Count)
            {
                hiddenItems[index].ScriptID = (ushort)numericScriptID.Value;
                labelScriptCall.Text = $"Use in spawnable: 8{hiddenItems[index].ScriptID:D3}";
                UpdateListBoxItem(index);
                SetDirty();
            }
        }

        private void UpdateListBoxItem(int index)
        {
            Helpers.DisableHandlers();
            try
            {
                var item = hiddenItems[index];
                string displayText = $"Script {item.ScriptID} (8{item.ScriptID:D3}): " +
                                   $"{GetItemName(item.ItemID)} x{item.Amount}";
                listBoxHiddenItems.Items[index] = displayText;
            }
            finally
            {
                Helpers.EnableHandlers();
            }
        }

        private void buttonAdd_Click(object sender, EventArgs e)
        {
            if (hiddenItems.Count >= maxCapacity)
            {
                MessageBox.Show($"Maximum number of entries ({maxCapacity}) reached.\n\n" +
                    "This limit is defined in the ROM at offset 0x405E8.", 
                    "Cannot Add", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Find the next available script ID
            ushort newScriptID = 95; // Start from 95 (vanilla starts here)
            var usedScriptIDs = new HashSet<ushort>(hiddenItems.Select(h => h.ScriptID));
            while (usedScriptIDs.Contains(newScriptID) && newScriptID < 256)
            {
                newScriptID++;
            }

            var newEntry = new HiddenItemEntry(0, 1, newScriptID);
            hiddenItems.Add(newEntry);

            Helpers.DisableHandlers();
            try
            {
                string displayText = $"Script {newEntry.ScriptID} (8{newEntry.ScriptID:D3}): " +
                                   $"{GetItemName(newEntry.ItemID)} x{newEntry.Amount}";
                listBoxHiddenItems.Items.Add(displayText);
                listBoxHiddenItems.SelectedIndex = listBoxHiddenItems.Items.Count - 1;
                UpdateEntryCount();
            }
            finally
            {
                Helpers.EnableHandlers();
            }

            SetDirty();
        }

        private void buttonRemove_Click(object sender, EventArgs e)
        {
            int index = listBoxHiddenItems.SelectedIndex;
            if (index < 0) return;

            var result = MessageBox.Show(
                "Are you sure you want to remove this hidden item entry?\n\n" +
                "Note: You'll need to manually remove or update any spawnables that reference this script ID.",
                "Confirm Remove", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                hiddenItems.RemoveAt(index);

                Helpers.DisableHandlers();
                try
                {
                    listBoxHiddenItems.Items.RemoveAt(index);
                    if (listBoxHiddenItems.Items.Count > 0)
                    {
                        listBoxHiddenItems.SelectedIndex = Math.Min(index, listBoxHiddenItems.Items.Count - 1);
                    }
                    UpdateEntryCount();
                }
                finally
                {
                    Helpers.EnableHandlers();
                }

                SetDirty();
            }
        }

        private void SaveHiddenItems()
        {
            try
            {
                // Create table data
                byte[] tableData = new byte[maxCapacity * ENTRY_SIZE];

                // Write entries
                for (int i = 0; i < hiddenItems.Count; i++)
                {
                    byte[] entryData = hiddenItems[i].ToByteArray();
                    Array.Copy(entryData, 0, tableData, i * ENTRY_SIZE, ENTRY_SIZE);
                }

                // Fill remaining with zeros (already done by array initialization)

                // Write to ARM9
                ARM9.WriteBytes(tableData, TABLE_OFFSET);

                // Write current table length (1 byte) to the count offset only
                // Do NOT write to TABLE_LENGTH_OFFSET_2 - that's the max capacity
                byte tableLength = (byte)hiddenItems.Count;
                ARM9.WriteBytes(new byte[] { tableLength }, TABLE_LENGTH_OFFSET_1);

                SetClean();
                MessageBox.Show($"Hidden items table saved successfully!\n\n" +
                    $"Entries saved: {hiddenItems.Count} / {maxCapacity}", 
                    "Save Complete",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving hidden items table: {ex.Message}", "Save Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void Reset()
        {
            Helpers.DisableHandlers();
            try
            {
                isDirty = false;
                hiddenItemsEditorIsReady = false;
                hiddenItems.Clear();

                if (listBoxHiddenItems != null)
                    listBoxHiddenItems.Items.Clear();
                if (comboBoxItem != null)
                    comboBoxItem.SelectedIndex = -1;
            }
            finally
            {
                Helpers.EnableHandlers();
            }
        }
    }
}
