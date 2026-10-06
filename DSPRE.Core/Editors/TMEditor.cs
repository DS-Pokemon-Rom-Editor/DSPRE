using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE
{
    /// <summary>Machine moves and disc colours. Indices 0-99 are TM01-HM08 and PlatPatches' extra TMs start at 100,
    /// so HM indices never shift.</summary>
    public static class TMEditor
    {
        /// <summary>TM01-TM92 and HM01-HM08, at indices 0-99.</summary>
        public static readonly int VanillaMachineCount = PokemonPersonalData.tmsCount + PokemonPersonalData.hmsCount;
        private const int FirstMachineItem = 328;

        /// <summary>All machines, including PlatPatches' TM93 onwards at indices 100 and up.</summary>
        public static int MachineCount => SourceMachines() is List<HgEngine.HgEngineMachineMoves.Machine> hge ? hge.Count
            : VanillaMachineCount + (PlatPatches.Tms()?.Count ?? 0);

        /// <summary>
        /// An hg-engine folder's machines, which its own src/item.c defines and its code reads instead of the ARM9
        /// table; null on any other project. An unreadable table is an empty list, so nothing falls back to ARM9.
        /// </summary>
        private static System.Collections.Generic.List<HgEngine.HgEngineMachineMoves.Machine> SourceMachines()
        {
            if (!HgEngine.HgEngineProject.IsActive) return null;
            if (HgEngine.HgEngineMachineMoves.TryRead(out List<HgEngineMachineMoves.Machine> machines, out string error)) return machines;
            AppLogger.Error("TM Editor: " + error);
            return new System.Collections.Generic.List<HgEngine.HgEngineMachineMoves.Machine>();
        }

        /// <summary>Machine indices in the order people read them: every TM by number, then the HMs.</summary>
        public static int[] DisplayOrder()
        {
            if (SourceMachines() is List<HgEngine.HgEngineMachineMoves.Machine> hge)
                return Enumerable.Range(0, hge.Count).OrderBy(i => hge[i].Label.StartsWith("HM") ? 1 : 0).ToArray();
            int total = MachineCount;
            return Enumerable.Range(0, PokemonPersonalData.tmsCount)
                .Concat(Enumerable.Range(VanillaMachineCount, total - VanillaMachineCount))
                .Concat(Enumerable.Range(PokemonPersonalData.tmsCount, PokemonPersonalData.hmsCount))
                .ToArray();
        }

        /// <summary>A machine's item id; extra TMs use the ids PlatPatches gave them.</summary>
        public static int MachineItemId(int index)
        {
            if (SourceMachines() is List<HgEngine.HgEngineMachineMoves.Machine> hge)
                return index >= 0 && index < hge.Count ? HgEngine.HgEngineMachineIcons.ItemIdFor(hge[index].Label) : -1;
            if (index < VanillaMachineCount) return FirstMachineItem + index;
            PlatPatches.ExtraTms t = PlatPatches.Tms();
            return t == null ? -1 : t.ItemIds[index - VanillaMachineCount];
        }

        /// <summary>Parses "TM05", "HM03" or "TM93"; -1 when the label isn't a machine this ROM has.</summary>
        public static int MachineIndexFromLabel(string label)
        {
            label = label.Split('-')[0].Trim();
            if (SourceMachines() is List<HgEngine.HgEngineMachineMoves.Machine> hge)
                return hge.FindIndex(m => string.Equals(m.Label, label, StringComparison.OrdinalIgnoreCase));
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
            if (SourceMachines() is List<HgEngine.HgEngineMachineMoves.Machine> hge) return hge.Select(m => m.Move).ToArray();
            int[] moves = new int[MachineCount];

            try
            {
                ARM9.Reader reader = new ARM9.Reader(RomInfo.GetMachineMoveOffset());
                
                for (int i = 0; i < VanillaMachineCount; i++)
                {
                    moves[i] = reader.ReadUInt16();
                }

                reader.Close();

                PlatPatches.ExtraTms extra = PlatPatches.Tms();
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
            if (SourceMachines() is List<HgEngine.HgEngineMachineMoves.Machine> hge) return index >= 0 && index < hge.Count ? hge[index].Label : $"Machine {index}";
            if (index >= VanillaMachineCount) return PlatPatches.ExtraTms.Label(index - VanillaMachineCount);
            return (index < PokemonPersonalData.tmsCount) ? $"TM{index + 1:00}" : $"HM{index - PokemonPersonalData.tmsCount + 1:00}";
        }

        /// <summary>Every machine's disc palette, or null when an item row can't be read (saving then leaves palettes alone).</summary>
        public static int[] ReadMachinePalettes()
        {
            if (SourceMachines() is List<HgEngine.HgEngineMachineMoves.Machine> hge) return ReadSourceDiscTypes(hge, out _);
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

        /// <summary>Writes every machine's move and disc colour, checking everything first; palettes may be null.</summary>
        public static void WriteMachines(int[] moves, int[] palettes)
        {
            if (moves.Length != MachineCount || (palettes != null && palettes.Length != moves.Length))
                throw new InvalidOperationException("The number of machines changed since the editor opened. Reopen it to edit them.");
            if (SourceMachines() is List<HgEngine.HgEngineMachineMoves.Machine> hge)
            {
                // Colours first: they are read against the moves still on disk, which is what they were shown for.
                if (palettes != null) WriteSourceDiscTypes(hge, palettes);
                if (!HgEngine.HgEngineMachineMoves.TryWrite(moves, out string error)) throw new InvalidOperationException(error);
                return;
            }
            // The move tables don't need item rows; only palettes are written there.
            for (int i = 0; palettes != null && i < moves.Length; i++)
                if (!ItemTable.Exists(MachineItemId(i))) throw new InvalidOperationException($"{MachineLabelFromIndex(i)} has no item row, so nothing was saved.");

            ARM9.Writer writer = new ARM9.Writer(RomInfo.GetMachineMoveOffset());
            for (int i = 0; i < VanillaMachineCount; i++) writer.Write((ushort)moves[i]);
            writer.Close();

            PlatPatches.ExtraTms extra = PlatPatches.Tms();
            if (extra != null)
                PlatPatches.SetExtraTmMoves(Enumerable.Range(VanillaMachineCount, moves.Length - VanillaMachineCount)
                    .ToDictionary(i => i - VanillaMachineCount, i => (ushort)moves[i]));

            for (int i = 0; palettes != null && i < palettes.Length; i++)
            {
                int item = MachineItemId(i);
                ItemNarcTableEntry e = ItemTable.Read(item);
                if (e.itemPalette == palettes[i]) continue;
                e.itemPalette = (uint)palettes[i];
                ItemTable.Write(item, e);
            }
        }

        #endregion

        // hg-engine has no shared disc palettes, so its machines carry a type instead, kept apart from the base games'
        // palette ids. Fairy is one of those types, which no base-game palette shows.
        private const int SourceTypeBase = 0x10000;

        /// <summary>The type a disc palette shows: a base-game palette id, or a type on an hg-engine folder.</summary>
        public static int PaletteToTypeIndex(int palette) => palette >= SourceTypeBase ? palette - SourceTypeBase : palette switch
        {
            398 => 1,  // Fighting
            399 => 16, // Dragon
            400 => 11, // Water
            401 => 14, // Psychic
            402 => 0,  // Normal
            403 => 3,  // Poison
            404 => 15, // Ice
            405 => 12, // Grass
            406 => 10, // Fire
            407 => 17, // Dark
            408 => 8,  // Steel
            409 => 13, // Electric
            410 => 4,  // Ground
            411 => 7,  // Ghost
            412 => 5,  // Rock
            413 => 2,  // Flying
            610 => 6,  // Bug
            _   => 0,  // Fallback Normal
        };

        public static int TypeIndexToPalette(int typeIndex) => HgEngine.HgEngineProject.IsActive ? SourceTypeBase + typeIndex : typeIndex switch
        {
            0  => 402, // Normal
            1  => 398, // Fighting
            2  => 413, // Flying
            3  => 403, // Poison
            4  => 410, // Ground
            5  => 412, // Rock
            6  => 610, // Bug
            7  => 411, // Ghost
            8  => 408, // Steel
            10 => 406, // Fire
            11 => 400, // Water
            12 => 405, // Grass
            13 => 409, // Electric
            14 => 401, // Psychic
            15 => 404, // Ice
            16 => 399, // Dragon
            17 => 407, // Dark
            _  => 402, // Fallback Normal
        };

        /// <summary>
        /// Each hg-engine machine's disc type, read from its icon's palette, and per type the disc most of its machines
        /// use, which is what a machine given that type copies. Null when an icon can't be read, so nothing is written.
        /// </summary>
        private static int[] ReadSourceDiscTypes(List<HgEngine.HgEngineMachineMoves.Machine> machines, out Dictionary<int, string> discByType)
        {
            discByType = new Dictionary<int, string>();
            string[] palettes = new string[machines.Count];
            Dictionary<int, Dictionary<string, (int Count, string Png)>> votes = new Dictionary<int, Dictionary<string, (int Count, string Png)>>();
            try
            {
                for (int i = 0; i < machines.Count; i++)
                {
                    string icon = HgEngine.HgEngineMachineIcons.IconPathFor(HgEngine.HgEngineMachineIcons.ItemIdFor(machines[i].Label));
                    if (icon == null) { AppLogger.Error($"TM Editor: no icon for {machines[i].Label}."); return null; }
                    byte[] palette = HgEngine.HgEngineMachineIcons.ReadPalette(icon);
                    if (palette == null) { AppLogger.Error($"TM Editor: {icon} has no palette."); return null; }
                    palettes[i] = Convert.ToBase64String(palette);
                    int type = (int)new MoveData(machines[i].Move).movetype;
                    if (!votes.TryGetValue(type, out Dictionary<string, (int Count, string Png)> forType)) votes[type] = forType = new Dictionary<string, (int, string)>();
                    forType[palettes[i]] = forType.TryGetValue(palettes[i], out (int Count, string Png) v) ? (v.Count + 1, v.Png) : (1, icon);
                }
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException)
            {
                AppLogger.Error("TM Editor: " + ex.Message);
                return null;
            }

            // A palette shows the type most of its machines teach; a few discs use a variant palette of their type.
            Dictionary<string, (int Type, int Count)> typeByPalette = new Dictionary<string, (int Type, int Count)>();
            foreach ((int type, Dictionary<string, (int Count, string Png)> forType) in votes)
            {
                discByType[type] = forType.OrderByDescending(kv => kv.Value.Count).First().Value.Png;
                foreach ((string palette, (int Count, string Png) vote) in forType)
                    if (!typeByPalette.TryGetValue(palette, out (int Type, int Count) held) || vote.Count > held.Count)
                        typeByPalette[palette] = (type, vote.Count);
            }

            return palettes.Select(p => SourceTypeBase + typeByPalette[p].Type).ToArray();
        }

        private static void WriteSourceDiscTypes(List<HgEngine.HgEngineMachineMoves.Machine> machines, int[] palettes)
        {
            int[] before = ReadSourceDiscTypes(machines, out Dictionary<int, string> discByType)
                ?? throw new InvalidOperationException("The disc colours could not be read from the checkout, so nothing was saved.");
            for (int i = 0; i < machines.Count; i++)
            {
                int type = PaletteToTypeIndex(palettes[i]);
                if (SourceTypeBase + type == before[i]) continue;
                if (!discByType.TryGetValue(type, out string disc))
                    throw new InvalidOperationException($"No machine teaches a move of that type, so there is no disc colour to give {MachineLabelFromIndex(i)}.");
                HgEngine.HgEngineMachineIcons.CopyColours(disc,
                    HgEngine.HgEngineMachineIcons.IconPathFor(HgEngine.HgEngineMachineIcons.ItemIdFor(machines[i].Label)));
            }
        }
    }
}
