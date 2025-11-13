using DSPRE.ROMFiles;
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
using static DSPRE.RomInfo;

namespace DSPRE
{

    public partial class TMEditor : Form
    {
        // This should make it easier to change in the future if expanding the number of TMs/HMs becomes possible
        private static readonly int machineCount = PokemonPersonalData.tmsCount + PokemonPersonalData.hmsCount;

        private int selectedTMIndex = -1;        
        private int[] curMachineMoves = new int[machineCount];
        private int[] curMachinePalettes = new int[machineCount];
        private bool dirty = false;

        public TMEditor()
        {
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.moveData });

            InitializeComponent();

            PopulateMoveComboBox();
            PopulateTypeComboBox();

            curMachineMoves = ReadMachineMoves();
            curMachinePalettes = ReadMachinePalettes();
            RefreshMachineMoveList();
        }

        #region Public Static Methods

        /// <summary>
        /// Reads the machine moves from the ARM9 memory and returns them as an array of integers.
        /// </summary>
        /// <remarks>This method reads 200 bytes from the ARM9 memory, interpreting them as 100 machine
        /// moves, each represented by a 16-bit unsigned integer in little-endian format. Index 0 to 91 are TMs, 92 to 99 are HMs.</remarks>
        /// <returns>An array of 100 integers representing the ids of the machine moves.</returns>
        public static int[] ReadMachineMoves()
        {

            int[] moves = new int[machineCount];

            try
            {
                // Read 200 bytes (100 moves x 2 bytes each little endian) from ARM9
                var reader = new ARM9.Reader(RomInfo.GetMachineMoveOffset());
                
                for (int i = 0; i < moves.Length; i++)
                {
                    moves[i] = reader.ReadUInt16();
                }

                reader.Close();
            }
            catch (Exception ex)
            {
                AppLogger.Error($"ReadMachineMoves: Failed to read machine moves. Exception: {ex.Message}");
            }

            return moves;

        }

        /// <summary>
        /// Converts an array of machine move IDs into their corresponding move names.
        /// </summary>
        /// <remarks>This method retrieves the move names from the underlying data source and maps each
        /// machine move ID to its corresponding name. If any invalid IDs are encountered, a warning is logged, and the
        /// placeholder string "UNK_{ID}" is used for those entries. You may want ReadMachineMoveNames() instead for a more straightforward approach.
        /// </remarks>
        /// <param name="machineMoves">An array of integers representing machine move IDs. Each ID corresponds to an index in the move name list.</param>
        /// <returns>An array of strings containing the names of the moves corresponding to the provided machine move IDs. If an
        /// ID is invalid (i.e., it does not correspond to a valid move), the resulting array will contain a placeholder
        /// string in the format "UNK_{ID}" at the respective position.</returns>
        public static string[] GetMachineMoveNames(int[] machineMoves)
        {
            string[] moveNames = RomInfo.GetAttackNames();
            string[] machineMoveNames = new string[machineMoves.Length];

            int invalidMoveCount = 0;

            for (int i = 0; i < machineMoves.Length; i++)
            {
                // Catch invalid move ids
                if (machineMoves[i] >= moveNames.Length)
                {
                    machineMoveNames[i] = $"UNK_{machineMoves[i]}";
                    invalidMoveCount++;
                    continue;
                }

                machineMoveNames[i] = moveNames[machineMoves[i]];
            }

            if (invalidMoveCount > 0)
            {
                AppLogger.Warn($"GetMachineMoveNames: Found {invalidMoveCount} invalid machine move IDs.");
            }

            return machineMoveNames;
        }

        /// <summary>
        /// Reads the names of all machine moves from the ROM and returns them as an array of strings.
        /// </summary>
        /// <remarks>
        /// This method combines the functionality of ReadMachineMoves and GetMachineMoveNames and should be preferred.
        /// </remarks>
        /// <returns>
        /// An array of strings representing the names of all machine moves (TMs and HMs) in the ROM.
        /// </returns>
        public static string[] ReadMachineMoveNames()
        {
            int[] machineMoves = ReadMachineMoves();
            return GetMachineMoveNames(machineMoves);
        }

        /// <summary>
        /// Generates a machine label based on the specified index.
        /// </summary>
        /// <param name="index">The zero-based index used to determine the machine label. Must be a non-negative integer.</param>
        /// <returns>A string representing the machine label. The label is in the format "TMXX" for indices less than 92, where
        /// "XX" is the index incremented by 1 and zero-padded to two digits. For indices 92 and above, the label is in
        /// the format "HMYY", where "YY" is the index minus 91.</returns>
        public static string MachineLabelFromIndex(int index)
        {
            return (index < PokemonPersonalData.tmsCount) ? $"TM{index + 1:00}" : $"HM{index - PokemonPersonalData.tmsCount + 1:00}";
        }

        public static int[] ReadMachinePalettes()
        {
            uint itemTableOffset = RomInfo.GetItemTableOffset();
            int startIndex = 328; // TMs/HMs start at item ID 328

            int[] paletteIds = new int[machineCount];

            try
            {
                for (int i = 0; i < machineCount; i++)
                {
                    paletteIds[i] = ARM9.ReadWordLE((uint)(itemTableOffset + (startIndex + i) * 8 + 4));
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"TM Editor: Failed to read palette IDs. Exception: {ex.Message}");
                MessageBox.Show("An error occurred while reading the palette IDs. There may have been an issue when reading the ARM9.",
                    "Read Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return new int[machineCount];
            }

            return paletteIds;

        }

        #endregion       

        private void SetDirty(bool isDirty)
        {
            dirty = isDirty;

            if (dirty)
            {
                this.Text = "TM/HM Editor*";
            }
            else
            {
                this.Text = "TM/HM Editor";
            }
        }

        private bool CheckDiscardChanges()
        {
            if (!dirty)
                return true;

            var result = MessageBox.Show("You have unsaved changes. Do you want to save them?",
                "Unsaved Changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                // Save changes
                SaveChanges();
                return true;
            }

            if (result == DialogResult.No)
            {
                // Discard changes
                return true;
            }
            
            return false;
        }

        private void SaveChanges()
        {
            try
            {
                // Write moves
                var writer = new ARM9.Writer(RomInfo.GetMachineMoveOffset());
                for (int i = 0; i < curMachineMoves.Length; i++)
                {
                    writer.Write((ushort)curMachineMoves[i]);
                }
                writer.Close();

                // Write palettes
                for (int i = 0; i < curMachinePalettes.Length; i++)
                {
                    WritePaletteID(i, curMachinePalettes[i]);
                }

                SetDirty(false);
            }
            catch (Exception ex)
            {
                AppLogger.Error($"TM Editor: Failed to save machine moves or palettes. Exception: {ex.Message}");
                MessageBox.Show("An error occurred while saving the machine moves or palettes. Please try again.",
                    "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshMachineMoveList()
        {
            machineListBox.Items.Clear();
            string[] machineMoveNames = GetMachineMoveNames(curMachineMoves);

            for (int i = 0; i < machineMoveNames.Length; i++)
            {
                string machineLabel = MachineLabelFromIndex(i);
                machineListBox.Items.Add($"{machineLabel} - {machineMoveNames[i]}");
            }
        }

        private void PopulateMoveComboBox()
        {
            moveComboBox.Items.Clear();
            string[] moveNames = RomInfo.GetAttackNames();
            for (int i = 0; i < moveNames.Length; i++)
            {
                moveComboBox.Items.Add($"{moveNames[i]}");
            }
        }
        
        private void PopulateTypeComboBox()
        {
            string[] typeNames = RomInfo.GetTypeNames();
            for (int i = 0; i < typeNames.Length; i++)
            {
                paletteComboBox.Items.Add($"{typeNames[i]}");
            }
        }

        private string GetMoveNameFromID(int moveId)
        {
            string[] moveNames = RomInfo.GetAttackNames();
            if (moveId >= 0 && moveId < moveNames.Length)
            {
                return moveNames[moveId];
            }
            return $"UNK_{moveId}";
        }

        private void WritePaletteID(int machineIndex, int paletteID)
        {
            uint itemTableOffset = RomInfo.GetItemTableOffset();
            int adjustedIndex = machineIndex + 328; // TMs/HMs start at item ID 328
            try
            {
                ARM9.WriteBytes(BitConverter.GetBytes((ushort)paletteID), (uint)(itemTableOffset + adjustedIndex * 8 + 4));
            }
            catch (Exception ex)
            {
                AppLogger.Error($"TM Editor: Failed to write palette ID for machine index {machineIndex}. Exception: {ex.Message}");
                MessageBox.Show("An error occurred while writing the palette ID. There may have been an issue when writing to the ARM9.",
                    "Write Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private int PaletteToTypeIndex(int paletteID)
        {
            // Maps palette IDs to type indices, these have no sensible order nor are they sequential
            switch (paletteID)
            {
                case 398: return 1;  // Fighting
                case 399: return 16; // Dragon
                case 400: return 11; // Water
                case 401: return 14; // Psychic
                case 402: return 0;  // Normal
                case 403: return 3;  // Poison
                case 404: return 15; // Ice
                case 405: return 12; // Grass
                case 406: return 10; // Fire
                case 407: return 17; // Dark
                case 408: return 8;  // Steel
                case 409: return 13; // Electric
                case 410: return 4;  // Ground
                case 411: return 7;  // Ghost
                case 412: return 5;  // Rock
                case 413: return 2;  // Flying
                case 610: return 6;  // Bug
                default: return 0;   // Fallback to Normal
            }
        }

        private int TypeIndexToPalette(int typeIndex)
        {
            // Reverse mapping of PaletteToTypeIndex
            switch (typeIndex)
            {
                case 0: return 402;  // Normal
                case 1: return 398;  // Fighting
                case 2: return 413;  // Flying
                case 3: return 403;  // Poison
                case 4: return 410;  // Ground
                case 5: return 412;  // Rock
                case 6: return 610;  // Bug
                case 7: return 411;  // Ghost
                case 8: return 408;  // Steel
                                     // Unknown type does not have a palette
                case 10: return 406; // Fire
                case 11: return 400; // Water
                case 12: return 405; // Grass
                case 13: return 409; // Electric
                case 14: return 401; // Psychic
                case 15: return 404; // Ice
                case 16: return 399; // Dragon
                case 17: return 407; // Dark
                default: return 402; // Fallback to Normal
            }
        }

        private int GetMoveType(int moveId)
        {
            var moveData = new MoveData(moveId);
            return (int)moveData.movetype;
        }


        private void machineListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (machineListBox.SelectedIndex >= 0 
                && machineListBox.SelectedIndex < curMachineMoves.Length 
                && machineListBox.SelectedIndex < curMachinePalettes.Length)
            {
                Helpers.DisableHandlers();

                selectedTMIndex = machineListBox.SelectedIndex;
                int moveId = curMachineMoves[selectedTMIndex];
                moveComboBox.SelectedIndex = moveId;

                int paletteId = curMachinePalettes[selectedTMIndex];
                paletteComboBox.SelectedIndex = PaletteToTypeIndex(paletteId);

                Helpers.EnableHandlers();
            }
        }

        private void moveComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (Helpers.HandlersDisabled)
                return;

            if (selectedTMIndex < 0 || selectedTMIndex >= curMachineMoves.Length)
                return;

            int selectedMoveId = moveComboBox.SelectedIndex;
            curMachineMoves[selectedTMIndex] = selectedMoveId;

            // Update the listbox entry
            string machineLabel = MachineLabelFromIndex(selectedTMIndex);
            machineListBox.Items[selectedTMIndex] = $"{machineLabel} - {GetMoveNameFromID(selectedMoveId)}";

            SetDirty(true);
        }
        private void paletteComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (Helpers.HandlersDisabled)
                return;

            if (selectedTMIndex < 0 || selectedTMIndex >= curMachineMoves.Length)
                return;

            int selectedTypeIndex = paletteComboBox.SelectedIndex;
            int paletteId = TypeIndexToPalette(selectedTypeIndex);

            curMachinePalettes[selectedTMIndex] = paletteId;

            SetDirty(true);
        }

        private void saveButton_Click(object sender, EventArgs e)
        {
            SaveChanges();
        }

        private void TMEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!CheckDiscardChanges())
            {
                e.Cancel = true;
            }
        }

        private void autoPaletteButton_Click(object sender, EventArgs e)
        {            
            if (selectedTMIndex < 0 || selectedTMIndex >= curMachineMoves.Length)
                return;

            int moveId = curMachineMoves[selectedTMIndex];
            int typeIndex = GetMoveType(moveId);
            paletteComboBox.SelectedIndex = typeIndex;
        }

        private void exportButton_Click(object sender, EventArgs e)
        {

            var saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*";
            saveFileDialog.Title = "Export Machine Data";

            if (saveFileDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }            
            
            string filePath = saveFileDialog.FileName;

            try
            {
                using (StreamWriter writer = new StreamWriter(filePath))
                {
                    writer.WriteLine("Machine,Move ID,Move Name,Palette ID");
                    for (int i = 0; i < curMachineMoves.Length; i++)
                    {
                        string machineLabel = MachineLabelFromIndex(i);
                        int moveId = curMachineMoves[i];
                        string moveName = GetMoveNameFromID(moveId);
                        int paletteId = curMachinePalettes[i];
                        writer.WriteLine($"{machineLabel},{moveId},{moveName},{paletteId}");
                    }
                }
                MessageBox.Show("Machine data exported successfully.", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppLogger.Error($"TM Editor: Failed to export machine data. Exception: {ex.Message}");
                MessageBox.Show("An error occurred while exporting the machine data. Please try again.",
                    "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private void importButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*";
            openFileDialog.Title = "Import Machine Data";

            if (openFileDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            string filePath = openFileDialog.FileName;

            try
            {
                var lines = File.ReadAllLines(filePath);
                for (int i = 1; i < lines.Length; i++) // Skip header
                {
                    var parts = lines[i].Split(',');

                    if (parts.Length < 4)
                        continue;

                    // Read label, move ID, and palette ID
                    string machineLabel = parts[0].Trim();
                    int moveId = int.Parse(parts[1].Trim());
                    int paletteId = int.Parse(parts[3].Trim());
                    int machineIndex;

                    if (machineLabel.StartsWith("TM"))
                    {
                        machineIndex = int.Parse(machineLabel.Substring(2)) - 1;
                    }
                    else if (machineLabel.StartsWith("HM"))
                    {
                        machineIndex = int.Parse(machineLabel.Substring(2)) + PokemonPersonalData.tmsCount - 1;
                    }
                    else
                    {
                        continue; // Invalid label
                    }

                    if (machineIndex >= 0 && machineIndex < curMachineMoves.Length)
                    {
                        curMachineMoves[machineIndex] = moveId;
                        curMachinePalettes[machineIndex] = paletteId;
                    }
                }
                RefreshMachineMoveList();
                SetDirty(true);

                MessageBox.Show("Machine data imported successfully.", "Import Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);

            }
            catch (Exception ex)
            {
                AppLogger.Error($"TM Editor: Failed to import machine data. Exception: {ex.Message}");
                MessageBox.Show("An error occurred while importing the machine data. Please ensure the file format is correct.",
                    "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }


        }
    }

}
