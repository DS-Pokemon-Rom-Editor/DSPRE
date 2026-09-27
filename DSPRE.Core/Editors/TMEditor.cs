using DSPRE.ROMFiles;
using System;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE
{
    /// <summary>Machine moves and disc colours. Indices 0-91 are TM01-TM92, 92-99 HM01-HM08, and 100 onwards
    /// PlatPatches' extra TMs, so the HMs keep their indices whether or not that patch is installed.</summary>
    public static class TMEditor
    {
        /// <summary>TM01-TM92 and HM01-HM08, the machines every retail game has, at indices 0-99.</summary>
        public static readonly int VanillaMachineCount = PokemonPersonalData.tmsCount + PokemonPersonalData.hmsCount;
        private const int FirstMachineItem = 328;

        /// <summary>All machines, including PlatPatches' TM93 onwards at indices 100 and up.</summary>
        public static int MachineCount => VanillaMachineCount + (PlatPatches.Tms()?.Count ?? 0);

        /// <summary>Machine indices in the order people read them: every TM by number, then the HMs.</summary>
        public static int[] DisplayOrder()
        {
            int total = MachineCount;
            return Enumerable.Range(0, PokemonPersonalData.tmsCount)
                .Concat(Enumerable.Range(VanillaMachineCount, total - VanillaMachineCount))
                .Concat(Enumerable.Range(PokemonPersonalData.tmsCount, PokemonPersonalData.hmsCount))
                .ToArray();
        }

        /// <summary>The item a machine index is: TM01 is 328, and extra TMs use the item ids PlatPatches gave them.</summary>
        public static int MachineItemId(int index)
        {
            if (index < VanillaMachineCount) return FirstMachineItem + index;
            var t = PlatPatches.Tms();
            return t == null ? -1 : t.ItemIds[index - VanillaMachineCount];
        }

        /// <summary>Parses "TM05", "HM03" or "TM93"; -1 when the label isn't a machine this ROM has.</summary>
        public static int MachineIndexFromLabel(string label)
        {
            label = label.Split('-')[0].Trim();
            if (label.Length < 3 || !int.TryParse(label.Substring(2), out int n) || n < 1) return -1;
            if (label.StartsWith("HM")) return n <= PokemonPersonalData.hmsCount ? PokemonPersonalData.tmsCount + n - 1 : -1;
            if (!label.StartsWith("TM")) return -1;
            if (n <= PokemonPersonalData.tmsCount) return n - 1;
            int row = n - PlatPatches.FirstExtraTmNumber;
            return row < (PlatPatches.Tms()?.Count ?? 0) ? VanillaMachineCount + row : -1;
        }

        #region Public Static Methods

        /// <summary>Every machine's move: the ARM9 table for TM01-HM08, then PlatPatches' extra TMs.</summary>
        public static int[] ReadMachineMoves()
        {
            int[] moves = new int[MachineCount];

            try
            {
                var reader = new ARM9.Reader(RomInfo.GetMachineMoveOffset());
                
                for (int i = 0; i < VanillaMachineCount; i++)
                {
                    moves[i] = reader.ReadUInt16();
                }

                reader.Close();

                var extra = PlatPatches.Tms();
                for (int i = VanillaMachineCount; i < moves.Length && extra != null; i++)
                    moves[i] = extra.MoveIds[i - VanillaMachineCount];
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

        /// <summary>"TM01"-"TM92" for 0-91, "HM01"-"HM08" for 92-99, "TM93" onwards for PlatPatches' extra TMs.</summary>
        public static string MachineLabelFromIndex(int index)
        {
            if (index >= VanillaMachineCount) return PlatPatches.ExtraTms.Label(index - VanillaMachineCount);
            return (index < PokemonPersonalData.tmsCount) ? $"TM{index + 1:00}" : $"HM{index - PokemonPersonalData.tmsCount + 1:00}";
        }

        /// <summary>Every machine's disc palette, or null when an item row can't be read (saving then leaves palettes alone).</summary>
        public static int[] ReadMachinePalettes()
        {
            int count = MachineCount;
            int[] paletteIds = new int[count];
            for (int i = 0; i < count; i++)
            {
                int item = MachineItemId(i);
                if (!ItemTable.Exists(item)) { AppLogger.Error($"TM Editor: machine {i} has no item row."); return null; }
                paletteIds[i] = (int)ItemTable.Read(item).itemPalette;
            }
            return paletteIds;
        }

        /// <summary>Writes every machine's move and disc colour: the ARM9 move table for TM01-HM08, PlatPatches'
        /// move list for extra TMs, and each machine item's palette wherever its item row lives.</summary>
        /// <remarks>Everything is checked before anything is written. <paramref name="palettes"/> may be null.</remarks>
        public static void WriteMachines(int[] moves, int[] palettes)
        {
            if (moves.Length != MachineCount || (palettes != null && palettes.Length != moves.Length))
                throw new InvalidOperationException("The number of machines changed since the editor opened. Reopen it to edit them.");
            // The move tables don't need item rows; only palettes are written there.
            for (int i = 0; palettes != null && i < moves.Length; i++)
                if (!ItemTable.Exists(MachineItemId(i))) throw new InvalidOperationException($"{MachineLabelFromIndex(i)} has no item row, so nothing was saved.");

            var writer = new ARM9.Writer(RomInfo.GetMachineMoveOffset());
            for (int i = 0; i < VanillaMachineCount; i++) writer.Write((ushort)moves[i]);
            writer.Close();

            var extra = PlatPatches.Tms();
            if (extra != null)
                PlatPatches.SetExtraTmMoves(Enumerable.Range(VanillaMachineCount, moves.Length - VanillaMachineCount)
                    .ToDictionary(i => i - VanillaMachineCount, i => (ushort)moves[i]));

            for (int i = 0; palettes != null && i < palettes.Length; i++)
            {
                int item = MachineItemId(i);
                var e = ItemTable.Read(item);
                if (e.itemPalette == palettes[i]) continue;
                e.itemPalette = (uint)palettes[i];
                ItemTable.Write(item, e);
            }
        }

        #endregion       
    }
}
