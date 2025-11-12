using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DSPRE
{

    public partial class TMEditor : Form
    {
        private static readonly int machineCount = PokemonPersonalData.tmsCount + PokemonPersonalData.hmsCount;

        private int selectedTMIndex = -1;        
        private int[] curMachineMoves = new int[machineCount];

        public TMEditor()
        {
            InitializeComponent();
            curMachineMoves = ReadMachineMoves();
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
            // Read 200 bytes (100 moves x 2 bytes each little endian) from ARM9
            var reader = new ARM9.Reader(GetMachineMoveOffset());
            int[] moves = new int[machineCount];
            for (int i = 0; i < moves.Length; i++)
            {
                moves[i] = reader.ReadUInt16();
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
            return (index < PokemonPersonalData.tmsCount) ? $"TM{index + 1:00}" : $"HM{index - 91}";
        }

        #endregion

        

        private static int GetMachineMoveOffset()
        {
            switch (RomInfo.gameFamily)
            {
                case RomInfo.GameFamilies.DP:
                    switch (RomInfo.gameLanguage)
                    {
                        case RomInfo.GameLanguages.English:
                            return 0xF84EC;
                        case RomInfo.GameLanguages.Japanese:
                            return 0xFA458;
                        case RomInfo.GameLanguages.French:
                            return 0xF8530;
                        case RomInfo.GameLanguages.German:
                            return 0xF8500;
                        case RomInfo.GameLanguages.Italian:
                            return 0xF84A4;
                        case RomInfo.GameLanguages.Spanish:
                            return 0xF853C;
                        default:
                            return 0xF84EC;
                    }
                case RomInfo.GameFamilies.Plat:
                    switch (RomInfo.gameLanguage)
                    {
                        case RomInfo.GameLanguages.English:
                            return 0xF0BFC;
                        case RomInfo.GameLanguages.Japanese:
                            return 0xF028C;
                        case RomInfo.GameLanguages.French:
                            return 0xF0C84;
                        case RomInfo.GameLanguages.German:
                            return 0xF0C54;
                        case RomInfo.GameLanguages.Italian:
                            return 0xF0C18;
                        case RomInfo.GameLanguages.Spanish:
                            return 0xF0C90;
                        default:
                            return 0xF0BFC;
                    }
                case RomInfo.GameFamilies.HGSS:
                    switch (RomInfo.gameLanguage)
                    {
                        case RomInfo.GameLanguages.English:
                            return 0x1000CC;
                        case RomInfo.GameLanguages.Japanese:
                            return 0xFF84C;
                        case RomInfo.GameLanguages.French:
                            return 0x1000B0;
                        case RomInfo.GameLanguages.German:
                            return 0x100080;
                        case RomInfo.GameLanguages.Italian:
                            return 0x100044;
                        case RomInfo.GameLanguages.Spanish:
                            return 0x1000B4;
                        default:
                            return 0x1000CC;
                    }
                default:
                    AppLogger.Error("GetMachineMoveOffset: Unsupported game family.");
                    throw new NotImplementedException();
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

    }

}
