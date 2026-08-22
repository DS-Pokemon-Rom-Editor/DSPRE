using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace DSPRE.Editors
{
    public partial class RockSmashEditor : UserControl, IEditorWithUnsavedChanges
    {
        private const int ItemTableSlotCount = 8;

        private bool isReady = false;
        private bool isDirty = false;
        private string[] itemNames;
        private List<RockSmashData> headerData = new List<RockSmashData>();
        private GroupBox[] tableGroups;
        private ComboBox[,] itemCombos; // [table, slot]

        #region IEditorWithUnsavedChanges Implementation
        public bool HasUnsavedChanges => isDirty;
        public string UnsavedChangesDescription => "Rock Smash Editor";
        public void SaveChanges() => buttonSave_Click(null, null);
        public void DiscardChanges() { isDirty = false; }
        #endregion

        public RockSmashEditor()
        {
            InitializeComponent();
        }

        public void SetupRockSmashEditor(bool force = false)
        {
            if (isReady && !force) { return; }

            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.rockSmash });
            itemNames = RomInfo.GetItemNames();

            LoadHeaders();
            SetupTypeComboBox();
            SetupItemTables();

            if (listBoxHeaders.Items.Count > 0)
            {
                listBoxHeaders.SelectedIndex = 0;
            }

            isReady = true;
        }

        private void LoadHeaders()
        {
            headerData.Clear();
            listBoxHeaders.Items.Clear();

            List<string> headerNames = Helpers.getHeaderListBoxNames();
            int count = headerNames.Count;

            for (int i = 0; i < count; i++)
            {
                var data = new RockSmashData((ushort)i);
                headerData.Add(data);
                listBoxHeaders.Items.Add(BuildHeaderLabel(headerNames[i], data));
            }
        }

        private string BuildHeaderLabel(string headerName, RockSmashData data)
        {
            return data.Existed ? headerName : headerName + " (will be created on save)";
        }

        private void SetupTypeComboBox()
        {
            comboBoxType.Items.Clear();
            comboBoxType.Items.Add("Default");
            comboBoxType.Items.Add("Ruins of Alph");
            comboBoxType.Items.Add("Cliff Cave");
        }

        private void SetupItemTables()
        {
            bool available = RomInfo.IsRockSmashItemTableAvailable();

            groupBoxRuinsOfAlph.Visible = available;
            groupBoxDefault.Visible = available;
            groupBoxCliffCave.Visible = available;
            labelItemTablesUnavailable.Visible = !available;

            if (!available) { return; }

            if (OverlayUtils.IsCompressed(RomInfo.rockSmashItemTableOverlayNumber))
            {
                OverlayUtils.Decompress(RomInfo.rockSmashItemTableOverlayNumber);
            }

            tableGroups = new GroupBox[] { groupBoxRuinsOfAlph, groupBoxDefault, groupBoxCliffCave };
            uint[] tableOffsets = new uint[] {
                RomInfo.rockSmashItemTableRuinsOfAlphOffset,
                RomInfo.rockSmashItemTableDefaultOffset,
                RomInfo.rockSmashItemTableCliffCaveOffset
            };

            itemCombos = new ComboBox[3, ItemTableSlotCount];
            string overlayPath = OverlayUtils.GetPath(RomInfo.rockSmashItemTableOverlayNumber);

            for (int t = 0; t < 3; t++)
            {
                tableGroups[t].Controls.Clear();
                byte[] tableData = DSUtils.ReadFromFile(overlayPath, tableOffsets[t], ItemTableSlotCount * 2);

                for (int slot = 0; slot < ItemTableSlotCount; slot++)
                {
                    var combo = new ComboBox
                    {
                        DropDownStyle = ComboBoxStyle.DropDownList,
                        Location = new System.Drawing.Point(15 + (slot % 4) * 170, 25 + (slot / 4) * 35),
                        Size = new System.Drawing.Size(160, 23),
                        Tag = (t, slot)
                    };
                    combo.Items.AddRange(itemNames);

                    ushort itemID = BitConverter.ToUInt16(tableData, slot * 2);
                    combo.SelectedIndex = itemID < itemNames.Length ? itemID : 0;

                    combo.SelectedIndexChanged += ItemCombo_SelectedIndexChanged;
                    tableGroups[t].Controls.Add(combo);
                    itemCombos[t, slot] = combo;
                }
            }
        }

        private void ItemCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (Helpers.HandlersDisabled) { return; }
            SetDirty();
        }

        private void listBoxHeaders_SelectedIndexChanged(object sender, EventArgs e)
        {
            int index = listBoxHeaders.SelectedIndex;
            if (index < 0 || index >= headerData.Count) { return; }

            Helpers.DisableHandlers();
            try
            {
                RockSmashData data = headerData[index];
                numericOdds.Value = Math.Min(data.Odds, (ushort)100);
                comboBoxType.SelectedIndex = (int)data.Type;
                labelStatus.Text = data.Existed ? "" : "Will be created on save";
            }
            finally
            {
                Helpers.EnableHandlers();
            }
        }

        private void numericOdds_ValueChanged(object sender, EventArgs e)
        {
            if (Helpers.HandlersDisabled) { return; }
            int index = listBoxHeaders.SelectedIndex;
            if (index < 0 || index >= headerData.Count) { return; }

            headerData[index].Odds = (ushort)numericOdds.Value;
            SetDirty();
        }

        private void comboBoxType_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (Helpers.HandlersDisabled) { return; }
            int index = listBoxHeaders.SelectedIndex;
            if (index < 0 || index >= headerData.Count) { return; }

            headerData[index].Type = (RockSmashData.TableType)comboBoxType.SelectedIndex;
            SetDirty();
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            foreach (RockSmashData data in headerData)
            {
                data.SaveToFile();
            }

            if (RomInfo.IsRockSmashItemTableAvailable() && itemCombos != null)
            {
                string overlayPath = OverlayUtils.GetPath(RomInfo.rockSmashItemTableOverlayNumber);
                uint[] tableOffsets = new uint[] {
                    RomInfo.rockSmashItemTableRuinsOfAlphOffset,
                    RomInfo.rockSmashItemTableDefaultOffset,
                    RomInfo.rockSmashItemTableCliffCaveOffset
                };

                for (int t = 0; t < 3; t++)
                {
                    byte[] tableData = new byte[ItemTableSlotCount * 2];
                    for (int slot = 0; slot < ItemTableSlotCount; slot++)
                    {
                        BitConverter.GetBytes((ushort)itemCombos[t, slot].SelectedIndex).CopyTo(tableData, slot * 2);
                    }
                    DSUtils.WriteToFile(overlayPath, tableData, tableOffsets[t]);
                }
            }

            // Refresh the "will be created on save" labels now that everything's on disk.
            int selected = listBoxHeaders.SelectedIndex;
            LoadHeaders();
            if (selected >= 0 && selected < listBoxHeaders.Items.Count)
            {
                listBoxHeaders.SelectedIndex = selected;
            }

            isDirty = false;
            MessageBox.Show("Rock Smash data saved!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SetDirty()
        {
            isDirty = true;
        }

        public void Reset()
        {
            isReady = false;
            isDirty = false;
            headerData.Clear();
            listBoxHeaders.Items.Clear();
            groupBoxRuinsOfAlph.Controls.Clear();
            groupBoxDefault.Controls.Clear();
            groupBoxCliffCave.Controls.Clear();
            itemCombos = null;
            labelStatus.Text = "";
        }
    }
}
