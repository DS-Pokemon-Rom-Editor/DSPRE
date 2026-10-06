using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.ROMFiles
{
    public sealed class TrainerReference
    {
        public TrainerReference(string kind, string location)
        {
            Kind = kind;
            Location = location;
        }

        public string Kind { get; }
        public string Location { get; }
        /// <summary>The trainer the reference names, when a scan covered several.</summary>
        public int TrainerId { get; init; } = -1;
        /// <summary>The file it is in (event or script file), or -1.</summary>
        public int SourceId { get; init; } = -1;
        /// <summary>Its place there: overworld, script number, row or entry; -1 when there is none.</summary>
        public int Index { get; init; } = -1;
        /// <summary>For a script reference, whether it sits in a function rather than a numbered script.</summary>
        public bool InFunction { get; init; }
        public override string ToString() => $"{Kind}: {Location}";
    }

    /// <summary>Finds known game resources that directly reference main-roster trainer IDs.</summary>
    public static class TrainerReferenceScanner
    {
        private const int PhoneBookHeaderSize = PokegearPhoneBook.HeaderSize;
        private const int PhoneBookEntrySize = PokegearPhoneBook.EntrySize;
        private const int PhoneBookTrainerIdOffset = PokegearPhoneBook.TrainerIdOffset;

        private static readonly IReadOnlyDictionary<ushort, int[]> PlatinumScriptParameters =
            new Dictionary<ushort, int[]>
            {
                [0x0023] = new[] { 0 }, // SetTrainerFlag
                [0x0024] = new[] { 0 }, // ClearTrainerFlag
                [0x0025] = new[] { 0 }, // CheckTrainerFlag
                [0x00D8] = new[] { 1 }, // TextTrainer
                [0x00E5] = new[] { 0, 1 }, // TrainerBattle
                [0x00E6] = new[] { 0 }, // TrainerMessage
                [0x00EA] = new[] { 0 }, // TrainerMusic
                [0x0125] = new[] { 0 }, // FirstBattle
                [0x02A0] = new[] { 0, 1, 2 }, // Battle2vs2
            };

        private static readonly IReadOnlyDictionary<ushort, int[]> HeartGoldScriptParameters =
            new Dictionary<ushort, int[]>
            {
                [0x0024] = new[] { 0 }, // SetTrainerFlag
                [0x0025] = new[] { 0 }, // ClearTrainerFlag
                [0x0026] = new[] { 0 }, // CheckTrainerFlag
                [0x00D5] = new[] { 0, 1 }, // TrainerBattle
                [0x00D6] = new[] { 0 }, // TrainerMessage; currently typed Flex in the command database
                [0x00DA] = new[] { 0 }, // TrainerMusic
                [0x01CC] = new[] { 0 }, // LoadPhoneDat, which finds a phone contact by trainer
                [0x0232] = new[] { 0, 1, 2 }, // Battle2vs2
            };

        public static bool TryFindCurrentProjectReferences(int trainerId,
            out List<TrainerReference> references, out string error) =>
            TryFindCurrentProjectReferences(new HashSet<int> { trainerId }, out references, out error);

        /// <summary>One pass over every resource for all of <paramref name="trainerIds"/>.</summary>
        public static bool TryFindCurrentProjectReferences(ISet<int> trainerIds,
            out List<TrainerReference> references, out string error) =>
            TryFindCurrentProjectReferences(trainerIds, -1, out references, out error);

        /// <summary>Only the trainer battle-message table and, in HeartGold and SoulSilver, the phone book.</summary>
        public static bool TryFindTableReferences(int trainerId, out List<TrainerReference> references, out string error)
        {
            references = new List<TrainerReference>();
            HashSet<int> ids = new HashSet<int> { trainerId };
            if (TryScanBattleMessages(ids, references, out error) && TryScanHeartGoldPhoneBook(ids, references, out error)) return true;
            references.Clear();
            return false;
        }

        /// <summary>
        /// The same, and when <paramref name="trainerClass"/> is set, the script commands that print that class's name.
        /// </summary>
        public static bool TryFindCurrentProjectReferences(ISet<int> trainerIds, int trainerClass,
            out List<TrainerReference> references, out string error)
        {
            references = new List<TrainerReference>();
            error = null;
            if (trainerIds.Count == 0 && trainerClass < 0) return true;
            if (trainerIds.Count == 0)
            {
                if (TryScanScripts(trainerIds, trainerClass, references, out error)) return true;
                references.Clear();
                return false;
            }

            if (!TryScanEvents(trainerIds, references, out error) ||
                !TryScanScripts(trainerIds, trainerClass, references, out error) ||
                !TryScanBattleMessages(trainerIds, references, out error) ||
                !TryScanVsSeeker(trainerIds, references, out error) ||
                !TryScanPokegearRematch(trainerIds, references, out error) ||
                !TryScanHeartGoldPhoneBook(trainerIds, references, out error))
            {
                references.Clear();
                return false;
            }

            return true;
        }

        internal static void FindEventReferences(RomInfo.GameFamilies family, int eventFileId,
            EventFile events, int trainerId,
            ICollection<TrainerReference> references) =>
            FindEventReferences(family, eventFileId, events, new HashSet<int> { trainerId }, references);

        internal static void FindEventReferences(RomInfo.GameFamilies family, int eventFileId,
            EventFile events, ISet<int> trainerIds,
            ICollection<TrainerReference> references)
        {
            for (int i = 0; i < events.overworlds.Count; i++)
            {
                Overworld overworld = events.overworlds[i];
                if (OverworldEventTypes.Find(family, overworld.type)?.IsTrainer != true) continue;
                if (TrainerScripts.TrainerIdFor(overworld.scriptNumber) is not int trainerId) continue;
                if (!trainerIds.Contains(trainerId)) continue;

                references.Add(new TrainerReference("Event",
                    $"event file {eventFileId}, overworld {overworld.owID}")
                    { TrainerId = trainerId, SourceId = eventFileId, Index = i });
            }
        }

        internal static void FindScriptReferences(RomInfo.GameFamilies family, int scriptFileId,
            ScriptFile scriptFile, int trainerId, ICollection<TrainerReference> references) =>
            FindScriptReferences(family, scriptFileId, scriptFile, new HashSet<int> { trainerId }, references);

        internal static void FindScriptReferences(RomInfo.GameFamilies family, int scriptFileId,
            ScriptFile scriptFile, ISet<int> trainerIds, ICollection<TrainerReference> references)
        {
            IReadOnlyDictionary<ushort, int[]> parameterMap = family switch
            {
                RomInfo.GameFamilies.Plat => PlatinumScriptParameters,
                RomInfo.GameFamilies.HGSS => HeartGoldScriptParameters,
                _ => null,
            };
            if (parameterMap == null) return;

            FindScriptContainerReferences(scriptFileId, "script", scriptFile.allScripts,
                parameterMap, trainerIds, references);
            FindScriptContainerReferences(scriptFileId, "function", scriptFile.allFunctions,
                parameterMap, trainerIds, references);
        }

        internal static bool TryFindBattleMessageReferences(ReadOnlySpan<byte> table, int trainerId,
            ICollection<TrainerReference> references, out string error) =>
            TryFindBattleMessageReferences(table, new HashSet<int> { trainerId }, references, out error);

        internal static bool TryFindBattleMessageReferences(ReadOnlySpan<byte> table, ISet<int> trainerIds,
            ICollection<TrainerReference> references, out string error)
        {
            error = null;
            if (table.Length % 4 != 0)
            {
                error = "The trainer battle-message table is not a sequence of four-byte entries.";
                return false;
            }

            for (int offset = 0; offset < table.Length; offset += 4)
            {
                int trainerId = BinaryPrimitives.ReadUInt16LittleEndian(table.Slice(offset, 2));
                if (trainerIds.Contains(trainerId))
                {
                    ushort trigger = BinaryPrimitives.ReadUInt16LittleEndian(table.Slice(offset + 2, 2));
                    references.Add(new TrainerReference("Battle message",
                        $"entry {offset / 4}, trigger {trigger}") { TrainerId = trainerId, Index = offset / 4 });
                }
            }
            return true;
        }

        internal static bool TryFindPhoneBookReferences(ReadOnlySpan<byte> data, int trainerId,
            ICollection<TrainerReference> references, out string error) =>
            TryFindPhoneBookReferences(data, new HashSet<int> { trainerId }, references, out error);

        internal static bool TryFindPhoneBookReferences(ReadOnlySpan<byte> data, ISet<int> trainerIds,
            ICollection<TrainerReference> references, out string error)
        {
            error = null;
            if (data.Length < PhoneBookHeaderSize)
            {
                error = "The Pokégear phonebook header is truncated.";
                return false;
            }

            uint count = BinaryPrimitives.ReadUInt32LittleEndian(data);
            long requiredLength = PhoneBookHeaderSize + (long)count * PhoneBookEntrySize;
            if (requiredLength > data.Length)
            {
                error = "The Pokégear phonebook entries are truncated.";
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                int offset = PhoneBookHeaderSize + i * PhoneBookEntrySize;
                int trainerId = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset + PhoneBookTrainerIdOffset, 2));
                if (trainerIds.Contains(trainerId))
                {
                    references.Add(new TrainerReference("Pokégear phonebook", $"entry {i}") { TrainerId = trainerId, Index = i });
                }
            }
            return true;
        }

        private static bool TryScanEvents(ISet<int> trainerIds, ICollection<TrainerReference> references,
            out string error)
        {
            error = null;
            string directory = RomInfo.gameDirs[RomInfo.DirNames.eventFiles].unpackedDir;
            if (!Directory.Exists(directory))
            {
                error = "The event archive must be unpacked before removing a trainer.";
                return false;
            }

            try
            {
                foreach ((int id, string path) in NumberedFiles(directory))
                {
                    using var input = File.OpenRead(path);
                    FindEventReferences(RomInfo.gameFamily, id, new EventFile(input), trainerIds,
                        references);
                }
                return true;
            }
            catch (Exception ex)
            {
                error = $"The event files could not be checked for trainer references: {ex.Message}";
                return false;
            }
        }

        private static bool TryScanScripts(ISet<int> trainerIds, int trainerClass, ICollection<TrainerReference> references,
            out string error)
        {
            error = null;
            string directory = RomInfo.gameDirs[RomInfo.DirNames.scripts].unpackedDir;
            if (!Directory.Exists(directory))
            {
                error = "The script archive must be unpacked before removing a trainer.";
                return false;
            }

            try
            {
                foreach ((int id, _) in NumberedFiles(directory))
                {
                    var scriptFile = new ScriptFile(id, readFunctions: true, readActions: false);
                    if (scriptFile.parseFailedDueToInvalidCommand)
                    {
                        error = $"Script file {id} did not parse completely, so trainer removal was cancelled.";
                        return false;
                    }
                    FindScriptReferences(RomInfo.gameFamily, id, scriptFile, trainerIds, references);
                    if (trainerClass >= 0) FindClassNameReferences(RomInfo.gameFamily, id, scriptFile, trainerClass, references);
                }
                return true;
            }
            catch (Exception ex)
            {
                error = $"The scripts could not be checked for trainer references: {ex.Message}";
                return false;
            }
        }

        private static bool TryScanBattleMessages(ISet<int> trainerIds,
            ICollection<TrainerReference> references, out string error)
        {
            string path = Path.Combine(
                RomInfo.gameDirs[RomInfo.DirNames.trainerTextTable].unpackedDir, "0000");
            if (!File.Exists(path))
            {
                error = "The trainer battle-message table must be unpacked before removing a trainer.";
                return false;
            }
            return TryFindBattleMessageReferences(File.ReadAllBytes(path), trainerIds, references,
                out error);
        }

        private static bool TryScanVsSeeker(ISet<int> trainerIds, ICollection<TrainerReference> references,
            out string error)
        {
            error = null;
            if (!VsSeekerRematchTable.IsSupported) return true;

            List<RematchTable.Row> rows;
            try { rows = VsSeekerRematchTable.ReadAll(); }
            catch (Exception ex)
            {
                error = $"The Vs. Seeker rematch table could not be checked: {ex.Message}";
                return false;
            }
            if (rows.Count != VsSeekerRematchTable.RowCount)
            {
                error = $"The Vs. Seeker rematch table has {rows.Count} readable rows; expected " +
                    $"{VsSeekerRematchTable.RowCount}.";
                return false;
            }

            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                RematchTable.Row row = rows[rowIndex];
                if (trainerIds.Contains(row.BaseTrainerId))
                {
                    references.Add(new TrainerReference("Vs. Seeker", $"row {rowIndex}, encounter") { TrainerId = row.BaseTrainerId, Index = rowIndex });
                }
                for (int level = 0; level < RematchTable.RematchLevelCount; level++)
                {
                    if (trainerIds.Contains(row.Rematch(level)))
                    {
                        references.Add(new TrainerReference("Vs. Seeker",
                            $"row {rowIndex}, rematch {level + 1}") { TrainerId = row.Rematch(level), Index = rowIndex });
                    }
                }
            }
            return true;
        }

        private static bool TryScanPokegearRematch(ISet<int> trainerIds,
            ICollection<TrainerReference> references, out string error)
        {
            error = null;
            if (!PokegearRematchTable.IsSupported) return true;

            List<RematchTable.Row> rows;
            try { rows = PokegearRematchTable.ReadAll(out _, out error); }
            catch (Exception ex)
            {
                error = $"The Pokégear rematch table could not be checked: {ex.Message}";
                return false;
            }
            if (error != null) return false;
            if (rows.Count == 0)
            {
                error = "The Pokégear rematch table has no readable rows.";
                return false;
            }

            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                RematchTable.Row row = rows[rowIndex];
                if (trainerIds.Contains(row.BaseTrainerId))
                {
                    references.Add(new TrainerReference("Pokégear rematch", $"row {rowIndex}, base trainer") { TrainerId = row.BaseTrainerId, Index = rowIndex });
                }
                for (int level = 0; level < RematchTable.RematchLevelCount; level++)
                {
                    if (trainerIds.Contains(row.Rematch(level)))
                    {
                        references.Add(new TrainerReference("Pokégear rematch",
                            $"row {rowIndex}, rematch {level + 1}") { TrainerId = row.Rematch(level), Index = rowIndex });
                    }
                }
            }
            return true;
        }

        private static bool TryScanHeartGoldPhoneBook(ISet<int> trainerIds,
            ICollection<TrainerReference> references, out string error)
        {
            error = null;
            if (RomInfo.gameFamily != RomInfo.GameFamilies.HGSS) return true;

            string path = PokegearPhoneBook.FilePath;
            if (!File.Exists(path))
            {
                error = "The Pokégear phonebook is missing, so trainer removal was cancelled.";
                return false;
            }
            return TryFindPhoneBookReferences(File.ReadAllBytes(path), trainerIds, references,
                out error);
        }

        private static void FindScriptContainerReferences(int scriptFileId, string containerKind,
            IReadOnlyList<ScriptCommandContainer> containers,
            IReadOnlyDictionary<ushort, int[]> parameterMap, ISet<int> trainerIds,
            ICollection<TrainerReference> references)
        {
            if (containers == null) return;
            foreach (ScriptCommandContainer container in containers)
            {
                if (container?.commands == null) continue;
                for (int commandIndex = 0; commandIndex < container.commands.Count; commandIndex++)
                {
                    ScriptCommand command = container.commands[commandIndex];
                    if (!command.id.HasValue || command.cmdParams == null ||
                        !parameterMap.TryGetValue(command.id.Value, out int[] parameterIndexes))
                    {
                        continue;
                    }

                    foreach (int parameterIndex in parameterIndexes)
                    {
                        if (parameterIndex >= command.cmdParams.Count) continue;
                        byte[] parameter = command.cmdParams[parameterIndex];
                        uint value = parameter.Length switch
                        {
                            1 => parameter[0],
                            2 => BinaryPrimitives.ReadUInt16LittleEndian(parameter),
                            4 => BinaryPrimitives.ReadUInt32LittleEndian(parameter),
                            _ => uint.MaxValue,
                        };
                        if (value <= int.MaxValue && trainerIds.Contains((int)value))
                        {
                            references.Add(new TrainerReference("Script",
                                $"file {scriptFileId}, {containerKind} {container.manualUserID}, " +
                                $"command {commandIndex + 1} ({command.name}), parameter {parameterIndex + 1}")
                                {
                                    TrainerId = (int)value, SourceId = scriptFileId, Index = (int)container.manualUserID,
                                    InFunction = containerKind == "function",
                                });
                        }
                    }
                }
            }
        }

        // TextTrainerClass: a string buffer, then the class as a flex value (a number below 0x4000, else a variable).
        private static ushort? TextTrainerClassCommand(RomInfo.GameFamilies family) => family switch
        {
            RomInfo.GameFamilies.HGSS => 0x0351,
            RomInfo.GameFamilies.Plat => 0x0344,
            RomInfo.GameFamilies.DP => 0x02CC,
            _ => null,
        };

        internal static void FindClassNameReferences(RomInfo.GameFamilies family, int scriptFileId,
            ScriptFile scriptFile, int trainerClass, ICollection<TrainerReference> references)
        {
            if (TextTrainerClassCommand(family) is not ushort command) return;
            foreach ((string kind, List<ScriptCommandContainer> containers) in new[] { ("script", scriptFile.allScripts), ("function", scriptFile.allFunctions) })
            {
                if (containers == null) continue;
                foreach (ScriptCommandContainer container in containers)
                {
                    if (container?.commands == null) continue;
                    for (int i = 0; i < container.commands.Count; i++)
                    {
                        ScriptCommand c = container.commands[i];
                        if (c.id != command || c.cmdParams == null || c.cmdParams.Count < 2 || c.cmdParams[1].Length < 2) continue;
                        if (BinaryPrimitives.ReadUInt16LittleEndian(c.cmdParams[1]) != trainerClass) continue;
                        references.Add(new TrainerReference("Script",
                            $"file {scriptFileId}, {kind} {container.manualUserID}, command {i + 1} ({c.name}) prints the class name")
                            { SourceId = scriptFileId, Index = (int)container.manualUserID, InFunction = kind == "function" });
                    }
                }
            }
        }

        private static IEnumerable<(int id, string path)> NumberedFiles(string directory)
        {
            return Directory.GetFiles(directory)
                .Select(path => (path, name: Path.GetFileName(path)))
                .Where(item => int.TryParse(item.name, out _))
                .Select(item => (int.Parse(item.name), item.path))
                .OrderBy(item => item.Item1);
        }
    }
}
