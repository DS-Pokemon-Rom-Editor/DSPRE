using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DSPRE
{

    struct EggMoveEntry
    {
        public int speciesID;
        public List<ushort> moveIDs;
        public EggMoveEntry(int speciesID, List<ushort> moveIDs)
        {
            this.speciesID = speciesID;
            this.moveIDs = moveIDs;
        }

        public int GetSizeInBytes()
        {
            // speciesID + moveIDs (2 bytes each)
            return 2 + (2 * moveIDs.Count);
        }
    }


    public partial class EggMoveEditor : Form
    {
        private const int MAX_EGG_MOVES = 16; // Max number of egg moves per species
        private const int EGG_MOVE_OVERLAY_NUMBER = 5;
        private const int EGG_MOVES_SPECIES_CONSTANT = 20000; // Species IDs in egg move data are stored as speciesID + this constant
        private int MAX_TABLE_SIZE; // in DPPt size is limited, in HGSS it's not

        private readonly string[] monNames;
        private readonly string[] moveNames;

        private bool useSpecialFormat = false;
        private List<EggMoveEntry> eggMoveData = new List<EggMoveEntry>();
        private bool dirty = false;

        public EggMoveEditor()
        {
            monNames = RomInfo.GetPokemonNames().ToArray();
            moveNames = RomInfo.GetAttackNames().ToArray();

            InitializeComponent();
            PopulateEggMoveData();
            PopulateMonList();
            PopulateComboBoxes();

            UpdateEntryCountLabel();
            UpdateListSizeLabel();
        }

        private void PopulateEggMoveData()
        {
            try 
            {
                EndianBinaryReader reader = GetEggDataReader();
                if (useSpecialFormat)
                {
                    ReadEggMoveDataSpecial();
                }
                else
                {
                    ReadEggMoveDataNormal(reader);
                }
                reader?.Close();
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to populate egg move data: {ex.Message}");
                MessageBox.Show("An error occurred while loading egg move data. Please check the logs for more details.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private EndianBinaryReader GetEggDataReader()
        {
            EndianBinaryReader reader;

            if (RomInfo.gameFamily == RomInfo.GameFamilies.HGSS)
            {
                DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.eggMoves });
                // The NARC contains only a single file which holds the egg move data
                var path = Path.Combine(RomInfo.gameDirs[RomInfo.DirNames.eggMoves].unpackedDir, "0000");
                var baseStream = File.OpenRead(path);
                reader = new EndianBinaryReader(baseStream, Endianness.LittleEndian);

                MAX_TABLE_SIZE = ushort.MaxValue; // no real limit in HGSS
            }
            else
            {
                int offset = RomInfo.GetEggMoveTableOffset();
                MAX_TABLE_SIZE = 0xEEC; // DPPt limit

                var baseStream = File.OpenRead(OverlayUtils.GetPath(EGG_MOVE_OVERLAY_NUMBER));
                reader = new EndianBinaryReader(baseStream, Endianness.LittleEndian);
                reader.BaseStream.Seek(offset, SeekOrigin.Begin);

                // Try to determine if the special format is being used
                int magicNumber = reader.ReadInt32();
                reader.BaseStream.Seek(-4, SeekOrigin.Current);

                if (magicNumber == 0x45474700) // "EGG\0" in ASCII
                {
                    useSpecialFormat = true;
                    return null; // reader will not be used in this case
                }
            }

            return reader;

        }

        private void ReadEggMoveDataNormal(EndianBinaryReader reader)
        {
            int eggMoveIndex = -1;
            int readerStartPos = (int)reader.BaseStream.Position;

            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                ushort read = reader.ReadUInt16();

                // End of egg move data
                if (read == 0xFFFF)
                {
                    break;
                }
                // Move ID
                else if (read > EGG_MOVES_SPECIES_CONSTANT)
                {
                    int speciesID = read - EGG_MOVES_SPECIES_CONSTANT;

                    EggMoveEntry eggMoveEntry = new EggMoveEntry(speciesID, new List<ushort>());
                    eggMoveData.Add(eggMoveEntry);

                    eggMoveIndex++;
                }
                // Move for the last species read
                else
                {
                    if (eggMoveIndex < 0)
                    {
                        AppLogger.Warn("Egg move data is malformed: move ID found before any species ID.");
                    }

                    EggMoveEntry lastEntry = eggMoveData[eggMoveIndex ];
                    if (lastEntry.moveIDs.Count >= MAX_EGG_MOVES)
                    {
                        AppLogger.Warn($"Egg move data is malformed: species ID {lastEntry.speciesID} has more than the maximum allowed egg moves ({MAX_EGG_MOVES}).");
                    }

                    lastEntry.moveIDs.Add(read);
                    eggMoveData[eggMoveIndex] = lastEntry; // Update the entry in the list
                }
            }

            int totalBytesRead = (int)(reader.BaseStream.Position - readerStartPos);

            if (totalBytesRead > MAX_TABLE_SIZE)
            {
                AppLogger.Warn("Egg move data read from ROM exceeds maximum allowed size.");
            }

        }

        // In order to allow for expanding egg move data in platinum, the game's code can be modified to read a different format
        // This function accounts for that format
        private void ReadEggMoveDataSpecial()
        {
            //ToDo: actually implement this
        }

        private void SaveEggMoveData()
        {
            try
            {
                BinaryWriter writer;
                if (RomInfo.gameFamily == RomInfo.GameFamilies.HGSS)
                {
                    // The NARC contains only a single file which holds the egg move data
                    var path = Path.Combine(RomInfo.gameDirs[RomInfo.DirNames.eggMoves].unpackedDir, "0000");
                    var baseStream = File.OpenWrite(path);
                    writer = new BinaryWriter(baseStream);
                }
                else
                {
                    int offset = RomInfo.GetEggMoveTableOffset();
                    var baseStream = File.OpenWrite(OverlayUtils.GetPath(EGG_MOVE_OVERLAY_NUMBER));
                    writer = new BinaryWriter(baseStream);
                    writer.BaseStream.Seek(offset, SeekOrigin.Begin);
                }
                if (useSpecialFormat)
                {
                    WriteEggMoveDataSpecial(writer);
                }
                else
                {
                    WriteEggMoveDataNormal(writer);
                }
                writer.Close();
                SetDirty(false);
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to save egg move data: {ex.Message}");
                MessageBox.Show("An error occurred while saving egg move data. Please check the logs for more details.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void WriteEggMoveDataNormal(BinaryWriter writer)
        {
            foreach (var entry in eggMoveData)
            {
                // Write species ID
                writer.Write((ushort)(entry.speciesID + EGG_MOVES_SPECIES_CONSTANT));
                // Write move IDs
                foreach (var moveID in entry.moveIDs)
                {
                    writer.Write(moveID);
                }
            }
            // Write end marker
            writer.Write((ushort)0xFFFF);
        }

        private void WriteEggMoveDataSpecial(BinaryWriter writer)
        {
            // ToDo: actually implement this
        }

        private void SetDirty(bool value)
        {
            dirty = value;

            if (dirty)
            {
                this.Text = "Egg Move Editor*";
            }
            else
            {
                this.Text = "Egg Move Editor";
            }
        }

        private void PopulateMonList()
        {
            monListBox.BeginUpdate();
            monListBox.Items.Clear();
            foreach (var entry in eggMoveData)
            {
                string monName = (entry.speciesID >= 0 && entry.speciesID < monNames.Length) ? monNames[entry.speciesID] : $"UNK_{entry.speciesID})";
                monListBox.Items.Add(monName);
            }
            monListBox.EndUpdate();
        }

        private void PopulateMoveList(int entryIndex)
        {
            eggMoveListBox.BeginUpdate();

            eggMoveListBox.Items.Clear();
            var entry = eggMoveData[entryIndex];
            foreach (var moveID in entry.moveIDs)
            {
                string moveName = (moveID < moveNames.Length) ? moveNames[moveID] : $"UNK_{moveID}";
                eggMoveListBox.Items.Add(moveName);
            }

            eggMoveListBox.EndUpdate();
        }

        private void PopulateComboBoxes()
        {
            monComboBox.BeginUpdate();
            monComboBox.Items.Clear();
            foreach (var monName in monNames)
            {
                monComboBox.Items.Add(monName);
            }
            monComboBox.EndUpdate();

            monComboBox.BeginUpdate();
            moveComboBox.Items.Clear();
            foreach (var moveName in moveNames)
            {
                moveComboBox.Items.Add(moveName);
            }
            moveComboBox.EndUpdate();
        }

        private void UpdateMonStatus()
        {
            // Invalid or no selection
            if (!CBSelectedMonValid())
            {
                monStatusLabel.Text = "Invalid Pokémon selected.";
                addMonButton.Enabled = false;
                replaceMonButton.Enabled = false;
                return;
            }

            int speciesID = monComboBox.SelectedIndex;

            // Species already has egg moves
            if (eggMoveData.Any(entry => entry.speciesID == speciesID))
            {
                monStatusLabel.Text = "This Pokémon already has egg moves.";
                addMonButton.Enabled = false;
                replaceMonButton.Enabled = false;

            }
            // Species can be added or replace the selected one
            else
            {
                monStatusLabel.Text = "This Pokémon can be added.";
                addMonButton.Enabled = true;
                replaceMonButton.Enabled = ListSelectedMonValid();
            }
        }

        private void UpdateMoveStatus()
        {
            // Invalid or no selection
            if (!CBSelectedMoveValid())
            {
                moveStatusLabel.Text = "Invalid move selected.";
                addMoveButton.Enabled = false;
                replaceMoveButton.Enabled = false;
                return;
            }
            ushort moveID = (ushort)moveComboBox.SelectedIndex;
            int selectedMonIndex = monListBox.SelectedIndex;
            if (!ListSelectedMonValid())
            {
                moveStatusLabel.Text = "No Pokémon selected.";
                addMoveButton.Enabled = false;
                replaceMoveButton.Enabled = false;
                return;
            }
            var entry = eggMoveData[selectedMonIndex];
            // Move already exists for this species
            if (entry.moveIDs.Contains(moveID))
            {
                moveStatusLabel.Text = "Egg move already in list.";
                addMoveButton.Enabled = false;
                replaceMoveButton.Enabled = false;
            }
            // Can add move
            else
            {
                moveStatusLabel.Text = "Egg move can be added.";
                addMoveButton.Enabled = true;
                replaceMoveButton.Enabled = ListSelectedMoveValid();
            }
        }

        private void UpdateEntryIDLabel()
        {
            entryIDLabel.Text = $"Entry ID: {monListBox.SelectedIndex}";
        }

        private void UpdateEntryCountLabel()
        {
            monCountLabel.Text = $"Pokémon Count: {eggMoveData.Count}";
        }

        private void UpdateMoveCountLabel()
        {
            if (ListSelectedMonValid())
            {
                int selectedMonIndex = monListBox.SelectedIndex;
                int moveCount = eggMoveData[selectedMonIndex].moveIDs.Count;
                moveCountLabel.Text = $"Move Count: {moveCount}";

                if (moveCount > MAX_EGG_MOVES)
                {
                    moveCountLabel.ForeColor = Color.Red;
                }
                else if (moveCount == MAX_EGG_MOVES)
                {
                    moveCountLabel.ForeColor = Color.Orange;
                }
                else
                {
                    moveCountLabel.ForeColor = SystemColors.ControlText;
                }

            }
            else
            {
                moveCountLabel.Text = "Move Count: N/A";
                moveCountLabel.ForeColor = SystemColors.ControlText;
            }
        }

        private void UpdateListSizeLabel()
        {
            int totalSize = 0;
            foreach (var entry in eggMoveData)
            {
                totalSize += entry.GetSizeInBytes();
            }

            totalSize += 2; // for the end marker

            listSizeLabel.Text = $"List Size: {totalSize} / {MAX_TABLE_SIZE} bytes";

            if (totalSize > MAX_TABLE_SIZE)
            {
                listSizeLabel.ForeColor = Color.Red;
            }
            else if (totalSize == MAX_TABLE_SIZE)
            {
                listSizeLabel.ForeColor = Color.Orange;
            }
            else
            {
                listSizeLabel.ForeColor = SystemColors.ControlText;
            }

        }

        private bool ListSelectedMonValid()
        {
            int selectedMonIndex = monListBox.SelectedIndex;
            return (selectedMonIndex >= 0 && selectedMonIndex < eggMoveData.Count);
        }

        private bool ListSelectedMoveValid()
        {
            int selectedMoveIndex = eggMoveListBox.SelectedIndex;
            return (selectedMoveIndex >= 0 && selectedMoveIndex < eggMoveData[monListBox.SelectedIndex].moveIDs.Count);
        }

        private bool CBSelectedMonValid()
        {
            int selectedMonIndex = monComboBox.SelectedIndex;
            return (selectedMonIndex >= 0 && selectedMonIndex < monNames.Length);
        }

        private bool CBSelectedMoveValid()
        {
            int selectedMoveIndex = moveComboBox.SelectedIndex;
            return (selectedMoveIndex >= 0 && selectedMoveIndex < moveNames.Length);
        }

        private void monListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (Helpers.HandlersDisabled) { return; }
            if (!ListSelectedMonValid()) 
            { 
                deleteMonButton.Enabled = false;
                return; 
            }

            deleteMonButton.Enabled = true;

            Helpers.DisableHandlers();

            int speciesID = eggMoveData[monListBox.SelectedIndex].speciesID;

            if (speciesID >= 0 && speciesID < monNames.Length)
            {
                monComboBox.SelectedIndex = speciesID;
            }
            else
            {
                monComboBox.SelectedIndex = -1;
            }

            PopulateMoveList(monListBox.SelectedIndex);
            UpdateMonStatus();
            UpdateMoveStatus();
            UpdateMoveCountLabel();
            UpdateEntryIDLabel();

            Helpers.EnableHandlers();

        }
        private void eggMoveListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (Helpers.HandlersDisabled) { return; }
            if (!ListSelectedMonValid()) 
            { 
                deleteMonButton.Enabled = false;
                return; 
            }
            if (!ListSelectedMoveValid()) 
            { 
                deleteMonButton.Enabled = false;
                return; 
            }

            deleteMoveButton.Enabled = true;

            Helpers.DisableHandlers();

            ushort moveID = eggMoveData[monListBox.SelectedIndex].moveIDs[eggMoveListBox.SelectedIndex];

            if (moveID >= 0 && moveID < moveNames.Length)
            {
                moveComboBox.SelectedIndex = moveID;
            }
            else
            {
                moveComboBox.SelectedIndex = -1;
            }

            UpdateMoveStatus();

            Helpers.EnableHandlers();

        }

        private void monComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (Helpers.HandlersDisabled) { return; }

            UpdateMonStatus();
        }

        private void moveComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (Helpers.HandlersDisabled) { return; }

            UpdateMoveStatus();
        }

        private void addMonButton_Click(object sender, EventArgs e)
        {
            if (!CBSelectedMonValid()) { return; }

            int speciesID = monComboBox.SelectedIndex;

            EggMoveEntry newEntry = new EggMoveEntry(speciesID, new List<ushort>());
            eggMoveData.Add(newEntry);
            monListBox.Items.Add(monNames[speciesID]);

            // Will trigger SelectedIndexChanged event
            monListBox.SelectedIndex = eggMoveData.Count - 1;

            UpdateEntryCountLabel();
            UpdateListSizeLabel();

            SetDirty(true);

        }

        private void replaceMonButton_Click(object sender, EventArgs e)
        {
            if (!CBSelectedMonValid()) { return; }
            if (!ListSelectedMonValid()) { return; }

            int speciesID = monComboBox.SelectedIndex;
            int selectedMonIndex = monListBox.SelectedIndex;

            EggMoveEntry entry = eggMoveData[selectedMonIndex];
            entry.speciesID = speciesID;
            eggMoveData[selectedMonIndex] = entry;

            monListBox.Items[selectedMonIndex] = monNames[speciesID];

            SetDirty(true);
        }

        private void deleteMonButton_Click(object sender, EventArgs e)
        {
            if (!ListSelectedMonValid()) { return; }

            int selectedMonIndex = monListBox.SelectedIndex;
            eggMoveData.RemoveAt(selectedMonIndex);
            monListBox.Items.RemoveAt(selectedMonIndex);

            if (selectedMonIndex >= eggMoveData.Count)
            {
                monListBox.SelectedIndex = eggMoveData.Count - 1;
            }
            else
            {
                monListBox.SelectedIndex = selectedMonIndex;
            }

            UpdateEntryCountLabel();
            UpdateListSizeLabel();

            SetDirty(true);
        }

        private void addMoveButton_Click(object sender, EventArgs e)
        {
            if (!CBSelectedMoveValid()) { return; }
            if (!ListSelectedMonValid()) { return; }

            ushort moveID = (ushort)moveComboBox.SelectedIndex;
            int selectedMonIndex = monListBox.SelectedIndex;

            EggMoveEntry entry = eggMoveData[selectedMonIndex];
            entry.moveIDs.Add(moveID);
            eggMoveData[selectedMonIndex] = entry;

            eggMoveListBox.Items.Add(moveNames[moveID]);

            eggMoveListBox.SelectedIndex = entry.moveIDs.Count - 1;

            UpdateMoveCountLabel();
            UpdateListSizeLabel();

            SetDirty(true);

        }

        private void replaceMoveButton_Click(object sender, EventArgs e)
        {
            if (!CBSelectedMoveValid()) { return; }
            if (!ListSelectedMonValid()) { return; }
            if (!ListSelectedMoveValid()) { return; }

            ushort moveID = (ushort)moveComboBox.SelectedIndex;
            int selectedMonIndex = monListBox.SelectedIndex;
            int selectedMoveIndex = eggMoveListBox.SelectedIndex;

            EggMoveEntry entry = eggMoveData[selectedMonIndex];
            entry.moveIDs[selectedMoveIndex] = moveID;
            eggMoveData[selectedMonIndex] = entry;

            eggMoveListBox.Items[selectedMoveIndex] = moveNames[moveID];

            SetDirty(true);

        }

        private void deleteMoveButton_Click(object sender, EventArgs e)
        {
            if (!ListSelectedMonValid()) { return; }
            if (!ListSelectedMoveValid()) { return; }

            int selectedMonIndex = monListBox.SelectedIndex;
            int selectedMoveIndex = eggMoveListBox.SelectedIndex;

            EggMoveEntry entry = eggMoveData[selectedMonIndex];
            entry.moveIDs.RemoveAt(selectedMoveIndex);
            eggMoveData[selectedMonIndex] = entry;
            eggMoveListBox.Items.RemoveAt(selectedMoveIndex);

            if (selectedMoveIndex >= entry.moveIDs.Count)
            {
                eggMoveListBox.SelectedIndex = entry.moveIDs.Count - 1;
            }
            else
            {
                eggMoveListBox.SelectedIndex = selectedMoveIndex;
            }

            UpdateMoveCountLabel();
            UpdateListSizeLabel();

        }
    }
}
