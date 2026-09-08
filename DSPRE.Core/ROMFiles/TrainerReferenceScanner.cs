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
        public override string ToString() => $"{Kind}: {Location}";
    }

    /// <summary>Finds known game resources that directly reference a main-roster trainer ID.</summary>
    internal static class TrainerReferenceScanner
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
                [0x0232] = new[] { 0, 1, 2 }, // Battle2vs2
            };

        public static bool TryFindCurrentProjectReferences(int trainerId,
            out List<TrainerReference> references, out string error)
        {
            references = new List<TrainerReference>();
            error = null;

            if (!TryScanEvents(trainerId, references, out error) ||
                !TryScanScripts(trainerId, references, out error) ||
                !TryScanBattleMessages(trainerId, references, out error) ||
                !TryScanVsSeeker(trainerId, references, out error) ||
                !TryScanPokegearRematch(trainerId, references, out error) ||
                !TryScanHeartGoldPhoneBook(trainerId, references, out error))
            {
                references.Clear();
                return false;
            }

            return true;
        }

        internal static void FindEventReferences(RomInfo.GameFamilies family, int eventFileId,
            EventFile events, int trainerId,
            ICollection<TrainerReference> references)
        {
            foreach (Overworld overworld in events.overworlds)
            {
                if (OverworldEventTypes.Find(family, overworld.type)?.IsTrainer != true ||
                    TrainerScripts.TrainerIdFor(overworld.scriptNumber) != trainerId)
                {
                    continue;
                }

                references.Add(new TrainerReference("Event",
                    $"event file {eventFileId}, overworld {overworld.owID}"));
            }
        }

        internal static void FindScriptReferences(RomInfo.GameFamilies family, int scriptFileId,
            ScriptFile scriptFile, int trainerId, ICollection<TrainerReference> references)
        {
            IReadOnlyDictionary<ushort, int[]> parameterMap = family switch
            {
                RomInfo.GameFamilies.Plat => PlatinumScriptParameters,
                RomInfo.GameFamilies.HGSS => HeartGoldScriptParameters,
                _ => null,
            };
            if (parameterMap == null) return;

            FindScriptContainerReferences(scriptFileId, "script", scriptFile.allScripts,
                parameterMap, trainerId, references);
            FindScriptContainerReferences(scriptFileId, "function", scriptFile.allFunctions,
                parameterMap, trainerId, references);
        }

        internal static bool TryFindBattleMessageReferences(ReadOnlySpan<byte> table, int trainerId,
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
                if (BinaryPrimitives.ReadUInt16LittleEndian(table.Slice(offset, 2)) == trainerId)
                {
                    ushort trigger = BinaryPrimitives.ReadUInt16LittleEndian(table.Slice(offset + 2, 2));
                    references.Add(new TrainerReference("Battle message",
                        $"entry {offset / 4}, trigger {trigger}"));
                }
            }
            return true;
        }

        internal static bool TryFindPhoneBookReferences(ReadOnlySpan<byte> data, int trainerId,
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
                if (BinaryPrimitives.ReadUInt16LittleEndian(
                    data.Slice(offset + PhoneBookTrainerIdOffset, 2)) == trainerId)
                {
                    references.Add(new TrainerReference("Pokégear phonebook", $"entry {i}"));
                }
            }
            return true;
        }

        private static bool TryScanEvents(int trainerId, ICollection<TrainerReference> references,
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
                    FindEventReferences(RomInfo.gameFamily, id, new EventFile(input), trainerId,
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

        private static bool TryScanScripts(int trainerId, ICollection<TrainerReference> references,
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
                    FindScriptReferences(RomInfo.gameFamily, id, scriptFile, trainerId, references);
                }
                return true;
            }
            catch (Exception ex)
            {
                error = $"The scripts could not be checked for trainer references: {ex.Message}";
                return false;
            }
        }

        private static bool TryScanBattleMessages(int trainerId,
            ICollection<TrainerReference> references, out string error)
        {
            string path = Path.Combine(
                RomInfo.gameDirs[RomInfo.DirNames.trainerTextTable].unpackedDir, "0000");
            if (!File.Exists(path))
            {
                error = "The trainer battle-message table must be unpacked before removing a trainer.";
                return false;
            }
            return TryFindBattleMessageReferences(File.ReadAllBytes(path), trainerId, references,
                out error);
        }

        private static bool TryScanVsSeeker(int trainerId, ICollection<TrainerReference> references,
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
                if (row.BaseTrainerId == trainerId)
                {
                    references.Add(new TrainerReference("Vs. Seeker", $"row {rowIndex}, encounter"));
                }
                for (int level = 0; level < RematchTable.RematchLevelCount; level++)
                {
                    if (row.Rematch(level) == trainerId)
                    {
                        references.Add(new TrainerReference("Vs. Seeker",
                            $"row {rowIndex}, rematch {level + 1}"));
                    }
                }
            }
            return true;
        }

        private static bool TryScanPokegearRematch(int trainerId,
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
                if (row.BaseTrainerId == trainerId)
                {
                    references.Add(new TrainerReference("Pokégear rematch", $"row {rowIndex}, base trainer"));
                }
                for (int level = 0; level < RematchTable.RematchLevelCount; level++)
                {
                    if (row.Rematch(level) == trainerId)
                    {
                        references.Add(new TrainerReference("Pokégear rematch",
                            $"row {rowIndex}, rematch {level + 1}"));
                    }
                }
            }
            return true;
        }

        private static bool TryScanHeartGoldPhoneBook(int trainerId,
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
            return TryFindPhoneBookReferences(File.ReadAllBytes(path), trainerId, references,
                out error);
        }

        private static void FindScriptContainerReferences(int scriptFileId, string containerKind,
            IReadOnlyList<ScriptCommandContainer> containers,
            IReadOnlyDictionary<ushort, int[]> parameterMap, int trainerId,
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
                        if (value == trainerId)
                        {
                            references.Add(new TrainerReference("Script",
                                $"file {scriptFileId}, {containerKind} {container.manualUserID}, " +
                                $"command {commandIndex + 1} ({command.name}), parameter {parameterIndex + 1}"));
                        }
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
