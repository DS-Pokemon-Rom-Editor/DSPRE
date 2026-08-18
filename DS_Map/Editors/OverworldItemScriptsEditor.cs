using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DSPRE.Editors
{
    public partial class OverworldItemScriptsEditor : Form
    {
        private const int itemScrMin = 7000;
        private const int itemScrMax = 8000;

        private ScriptFile itemScript;
        private string[] itemNames;
        private HashSet<int> usedScriptNumbers = new HashSet<int>();
        private List<(int scriptIndex, int itemId, int quantity)> entries = new List<(int, int, int)>();

        private DataGridView grid;
        private InputComboBox itemPickerCombo;
        private NumericUpDown qtyUpDown;
        private Button addButton;
        private Button removeButton;
        private Button closeButton;

        public OverworldItemScriptsEditor()
        {
            BuildUi();
            Text = "Ground Item Scripts";
            LoadData();
        }

        private void BuildUi()
        {
            Width = 560;
            Height = 520;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;

            var infoLabel = new Label
            {
                Text = "These are the item + quantity combinations Overworld Item events can pick from.\n" +
                       "Entries still in use by an Overworld event can't be removed.",
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(8, 8, 8, 0)
            };

            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false
            };
            grid.Columns.Add("Item", "Item");
            grid.Columns.Add("Quantity", "Quantity");
            grid.Columns.Add("InUse", "In use");
            grid.SelectionChanged += (s, e) => removeButton.Enabled = grid.SelectedRows.Count > 0;

            var addPanel = new Panel { Dock = DockStyle.Bottom, Height = 40 };
            itemPickerCombo = new InputComboBox { Location = new Point(8, 8), Width = 260 };
            var qtyLabel = new Label { Text = "Qty:", Location = new Point(276, 12), AutoSize = true };
            qtyUpDown = new NumericUpDown { Location = new Point(312, 8), Width = 60, Minimum = 1, Maximum = 99, Value = 1 };
            addButton = new Button { Text = "Add", Location = new Point(384, 7), Width = 70 };
            addButton.Click += AddButton_Click;
            addPanel.Controls.Add(itemPickerCombo);
            addPanel.Controls.Add(qtyLabel);
            addPanel.Controls.Add(qtyUpDown);
            addPanel.Controls.Add(addButton);

            var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 40 };
            removeButton = new Button { Text = "Remove selected", Location = new Point(8, 6), Width = 130, Enabled = false };
            removeButton.Click += RemoveButton_Click;
            closeButton = new Button { Text = "Close", Location = new Point(464, 6), Width = 80, DialogResult = DialogResult.OK };
            bottomPanel.Controls.Add(removeButton);
            bottomPanel.Controls.Add(closeButton);

            Controls.Add(grid);
            Controls.Add(bottomPanel);
            Controls.Add(addPanel);
            Controls.Add(infoLabel);
            AcceptButton = null;
            CancelButton = closeButton;
        }

        private void LoadData()
        {
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.scripts });
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.eventFiles });

            itemScript = new ScriptFile(RomInfo.itemScriptFileNumber);
            itemNames = RomInfo.GetItemNames();

            Helpers.DisableHandlers();
            itemPickerCombo.Items.Clear();
            itemPickerCombo.Items.AddRange(itemNames);
            itemPickerCombo.RefreshMasterList();
            Helpers.EnableHandlers();

            ComputeUsedScriptNumbers();
            RefreshGrid();
        }

        private void ComputeUsedScriptNumbers()
        {
            usedScriptNumbers.Clear();
            int fileCount = Filesystem.GetEventFileCount();
            for (int i = 0; i < fileCount; i++)
            {
                EventFile ev = new EventFile(i);
                foreach (Overworld ow in ev.overworlds)
                {
                    bool isItem = ow.type == (ushort)Overworld.OwType.ITEM || (ow.scriptNumber >= itemScrMin && ow.scriptNumber <= itemScrMax);
                    if (isItem)
                    {
                        usedScriptNumbers.Add(ow.scriptNumber);
                    }
                }
            }
        }

        private void RefreshGrid()
        {
            entries = DSUtils.GetGroundItemScriptEntries(itemScript);

            grid.Rows.Clear();
            foreach (var entry in entries)
            {
                int scriptNumber = itemScrMin + entry.scriptIndex;
                bool inUse = usedScriptNumbers.Contains(scriptNumber);
                string itemName = entry.itemId >= 0 && entry.itemId < itemNames.Length ? itemNames[entry.itemId] : $"Item {entry.itemId}";
                int rowIndex = grid.Rows.Add(itemName, entry.quantity, inUse ? "Yes" : "No");
                grid.Rows[rowIndex].Tag = entry.scriptIndex;
            }
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            string typedName = (itemPickerCombo.Text ?? "").Trim();
            int itemId = Array.FindIndex(itemNames, n => string.Equals(n, typedName, StringComparison.OrdinalIgnoreCase));
            if (itemId < 0)
            {
                MessageBox.Show("Pick an item first.", "Nothing selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int quantity = (int)qtyUpDown.Value;

            int insertAt = itemScript.allScripts.FindLastIndex(DSUtils.IsGroundItemScriptEntry) + 1;
            var cmdList = new List<ScriptCommand>
            {
                new ScriptCommand("SetVar 0x8008 " + itemId),
                new ScriptCommand("SetVar 0x8009 " + quantity),
                new ScriptCommand("Jump Function_#1")
            };
            var newEntry = new ScriptCommandContainer(uint.MaxValue, ScriptFile.ContainerTypes.Script, commandList: cmdList);
            itemScript.allScripts.Insert(insertAt, newEntry);
            itemScript.RenumberContainers();

            if (!itemScript.SaveToFileDefaultDir(RomInfo.itemScriptFileNumber, showSuccessMessage: false))
            {
                return;
            }

            itemScript = new ScriptFile(RomInfo.itemScriptFileNumber);
            RefreshGrid();
        }

        private void RemoveButton_Click(object sender, EventArgs e)
        {
            if (grid.SelectedRows.Count == 0)
            {
                return;
            }

            int scriptIndex = (int)grid.SelectedRows[0].Tag;
            int scriptNumber = itemScrMin + scriptIndex;

            if (usedScriptNumbers.Contains(scriptNumber))
            {
                MessageBox.Show("This entry is currently used by an Overworld Item event and can't be removed.\n" +
                    "Change or delete that event first.", "In use", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult confirm = MessageBox.Show("Remove this ground item entry?", "Confirm to proceed", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
            {
                return;
            }

            itemScript.allScripts.RemoveAt(scriptIndex);
            itemScript.RenumberContainers();

            if (!itemScript.SaveToFileDefaultDir(RomInfo.itemScriptFileNumber, showSuccessMessage: false))
            {
                return;
            }

            ShiftOverworldReferencesAfterRemoval(scriptNumber);

            itemScript = new ScriptFile(RomInfo.itemScriptFileNumber);
            ComputeUsedScriptNumbers();
            RefreshGrid();
        }

        // Entries after the removed one shifted down a slot, so references past it must shift too.
        private void ShiftOverworldReferencesAfterRemoval(int removedScriptNumber)
        {
            int fileCount = Filesystem.GetEventFileCount();
            for (int i = 0; i < fileCount; i++)
            {
                EventFile ev = new EventFile(i);
                bool dirty = false;

                foreach (Overworld ow in ev.overworlds)
                {
                    bool isItem = ow.type == (ushort)Overworld.OwType.ITEM || (ow.scriptNumber >= itemScrMin && ow.scriptNumber <= itemScrMax);
                    if (isItem && ow.scriptNumber > removedScriptNumber)
                    {
                        ow.scriptNumber--;
                        dirty = true;
                    }
                }

                if (dirty)
                {
                    ev.SaveToFileDefaultDir(i, showSuccessMessage: false);
                }
            }

            if (RomInfo.gameFamily == RomInfo.GameFamilies.Plat)
            {
                string ow9path = OverlayUtils.GetPath(9);
                int ow9offs = 0x8E20 + 10;

                ushort currentValue;
                using (DSUtils.EasyReader reader = new DSUtils.EasyReader(ow9path, ow9offs))
                {
                    currentValue = reader.ReadUInt16();
                }

                if (currentValue > removedScriptNumber)
                {
                    using (DSUtils.EasyWriter writer = new DSUtils.EasyWriter(ow9path, ow9offs))
                    {
                        writer.Write((ushort)(currentValue - 1));
                    }
                }
            }
        }
    }
}
