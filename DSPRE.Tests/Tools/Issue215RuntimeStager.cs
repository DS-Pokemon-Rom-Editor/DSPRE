using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DSPRE;
using DSPRE.ROMFiles;
using NarcAPI;
using Xunit;
using Xunit.Abstractions;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>
    /// Builds disposable ROMs for the two live checks used while developing Issue 215.
    ///
    /// Set DSPRE_ISSUE215_STAGE to "platinum", "platinum-add", "heartgold", "heartgold-add",
    /// or "remove" and point
    /// DSPRE_ISSUE215_OUTPUT at a folder for the resulting ROM and evidence manifest. Source
    /// projects are copied to a uniquely named temporary directory and are never written to.
    /// </summary>
    [Collection("rom")]
    public class Issue215RuntimeStager
    {
        private readonly ITestOutputHelper _out;

        public Issue215RuntimeStager(ITestOutputHelper output)
        {
            _out = output;
        }

        private const int PlatinumRoute201Script = 427;
        private const int PlatinumTwinleafTownHeader = 411;
        private const int PlatinumPlayerHouseHeader = 414;
        private const ushort PlatinumRivalTurtwig = 851;
        private const ushort PlatinumYoungsterTristan = 1;

        private const int HeartGoldRoute29Events = 30;
        private const int HeartGoldLeagueEntranceHeader = 300;
        private const int HeartGoldLeagueEntranceEvents = 271;
        private const ushort HeartGoldYoungsterJoey = 8;
        private const ushort TrainerScriptBase = 2999;

        [SkippableFact]
        public void BuildPlatinumRivalRedirect()
        {
            Skip.If(!ModeIs("platinum"), "DSPRE_ISSUE215_STAGE is not platinum");
            string output = RequireOutputDirectory();
            string work = CreateTemporaryProjectCopy(TestRoms.Platinum, "platinum");

            try
            {
                SettingsManager.Load();
                new RomInfo("CPUE", work);
                var script = new ScriptFile(PlatinumRoute201Script);
                Assert.False(script.parseFailedDueToInvalidCommand,
                    "Route 201 did not parse completely, so it is unsafe to rewrite");

                List<ScriptCommand> firstBattles = Commands(script)
                    .Where(IsFirstBattle)
                    .ToList();
                ushort[] before = firstBattles.Select(TrainerArgument).ToArray();

                Assert.Contains((ushort)850, before);
                Assert.Contains((ushort)851, before);
                Assert.Contains((ushort)852, before);
                ScriptCommand target = Assert.Single(firstBattles,
                    command => TrainerArgument(command) == PlatinumRivalTurtwig);

                byte[] original = script.ToByteArray();
                Assert.NotNull(original);
                target.cmdParams[0] = BitConverter.GetBytes(PlatinumYoungsterTristan);
                byte[] staged = script.ToByteArray();
                Assert.NotNull(staged);
                int[] changedOffsets = Enumerable.Range(0, original.Length)
                    .Where(offset => original[offset] != staged[offset])
                    .ToArray();
                Assert.Equal(new[] { changedOffsets[0], changedOffsets[0] + 1 }, changedOffsets);
                File.WriteAllBytes(Filesystem.GetScriptPath(PlatinumRoute201Script), staged);

                var reopened = new ScriptFile(
                    new MemoryStream(File.ReadAllBytes(Filesystem.GetScriptPath(PlatinumRoute201Script))),
                    fileID: PlatinumRoute201Script);
                ushort[] after = Commands(reopened)
                    .Where(IsFirstBattle)
                    .Select(TrainerArgument)
                    .ToArray();
                Assert.Contains(PlatinumYoungsterTristan, after);
                Assert.DoesNotContain(PlatinumRivalTurtwig, after);
                Assert.Equal(before.Length, after.Length);

                RepackArchive(DirNames.scripts);
                string rom = Path.Combine(output, "issue215-platinum-rival-redirect.nds");
                Assert.True(DSUtils.RepackROM(rom) && File.Exists(rom), "Platinum ROM repack failed");

                string manifest = Path.Combine(output, "platinum-rival-redirect.txt");
                File.WriteAllLines(manifest, new[]
                {
                    "Issue 215 Platinum runtime stage",
                    $"Script archive: {PlatinumRoute201Script}",
                    $"StartFirstBattle before: {string.Join(", ", before)}",
                    $"StartFirstBattle after: {string.Join(", ", after)}",
                    $"Changed trainer: {PlatinumRivalTurtwig} -> {PlatinumYoungsterTristan}",
                    $"Changed script byte offsets: {string.Join(", ", changedOffsets)}",
                    $"Original context: {HexContext(original, changedOffsets[0])}",
                    $"Staged context: {HexContext(staged, changedOffsets[0])}",
                    $"ROM: {Path.GetFileName(rom)}",
                });

                _out.WriteLine($"Route 201 StartFirstBattle: {string.Join(", ", before)} -> {string.Join(", ", after)}");
                _out.WriteLine($"Built {Path.GetFileName(rom)}");
            }
            finally
            {
                DeleteTemporaryProject(work);
            }
        }

        [SkippableFact]
        public void BuildPlatinumAddedTrainerBattle()
        {
            Skip.If(!ModeIs("platinum-add"), "DSPRE_ISSUE215_STAGE is not platinum-add");
            string output = RequireOutputDirectory();
            string work = CreateTemporaryProjectCopy(TestRoms.Platinum, "platinum-add");

            try
            {
                SettingsManager.Load();
                new RomInfo("CPUE", work);
                DSUtils.TryUnpackNarcs(new List<DirNames>
                {
                    DirNames.trainerProperties, DirNames.trainerParty, DirNames.scripts,
                    DirNames.textArchives, DirNames.eventFiles, DirNames.matrices, DirNames.maps
                });

                TrainerRosterAnalysis before = TrainerRosterService.AnalyzeCurrentProject();
                Assert.True(before.CanAdd, before.RefusalReason);
                Assert.True(TrainerRosterService.TryAddTrainer("Added", out int addedId,
                    out string addError), addError);
                Assert.Equal(928, addedId);

                string addedIndex = addedId.ToString("D4");
                string propertiesDir = gameDirs[DirNames.trainerProperties].unpackedDir;
                string partyDir = gameDirs[DirNames.trainerParty].unpackedDir;
                Assert.True(File.Exists(Path.Combine(propertiesDir, addedIndex)));
                Assert.True(File.Exists(Path.Combine(partyDir, addedIndex)));

                TrainerRosterAnalysis afterAdd = TrainerRosterService.AnalyzeCurrentProject();
                Assert.True(afterAdd.CanAdd, afterAdd.RefusalReason);
                Assert.Equal(929, afterAdd.TrainerRecordCount);
                Assert.Equal(929, afterAdd.PartyRecordCount);
                Assert.Equal(929, afterAdd.TrainerNameCount);
                Assert.Equal(before.RemainingAdditions - 1, afterAdd.RemainingAdditions);

                // Give the runtime fixture trainer 1's known-valid properties and party. The add
                // transaction itself produced the new files; this fixture-only copy makes the new
                // trainer safe to battle immediately without choosing a party through the UI.
                File.Copy(Path.Combine(propertiesDir, "0001"),
                    Path.Combine(propertiesDir, addedIndex), overwrite: true);
                File.Copy(Path.Combine(partyDir, "0001"),
                    Path.Combine(partyDir, addedIndex), overwrite: true);

                var names = new TextArchive(trainerNamesMessageNumber);
                Assert.Equal("Added", names.GetSimpleTrainerNames()[addedId]);

                Assert.True(TrainerScriptDescriptor.TryFor(gameFamily,
                    out TrainerScriptDescriptor scriptDescriptor));
                byte[] sharedBytes = File.ReadAllBytes(Filesystem.GetScriptPath(
                    scriptDescriptor.SharedScriptArchiveId));
                Assert.True(TrainerScriptLayout.TryAnalyze(sharedBytes,
                    out TrainerScriptLayout expandedLayout, out string layoutError), layoutError);
                Assert.Equal(before.SpecialScriptIndex + 1, expandedLayout.SpecialIndex);

                Assert.True(TrainerScriptExecutableDescriptor.TryFor(gameVersion, gameLanguage,
                    out TrainerScriptExecutableDescriptor executableDescriptor, out string descriptorError),
                    descriptorError);
                ushort movedSpecial = checked((ushort)(3000 + expandedLayout.SpecialIndex));
                foreach (TrainerScriptPatchTarget target in executableDescriptor.Targets)
                {
                    string path = target.File == TrainerScriptExecutableFile.Arm9
                        ? arm9Path
                        : OverlayUtils.GetPath(target.OverlayNumber);
                    Assert.True(TrainerScriptExecutableDescriptor.TryLocateTarget(File.ReadAllBytes(path),
                        movedSpecial, target.ExpectedThumbReferences, out _, out string patchError), patchError);
                }

                var route201 = new ScriptFile(PlatinumRoute201Script);
                ScriptCommand battle = Assert.Single(Commands(route201).Where(IsFirstBattle),
                    command => TrainerArgument(command) == PlatinumRivalTurtwig);
                battle.cmdParams[0] = BitConverter.GetBytes(checked((ushort)addedId));
                battle.name = new ScriptCommand(battle.id.Value, battle.cmdParams).name;
                byte[] redirectedRouteBytes = route201.ToByteArray();
                var inMemoryRoute = new ScriptFile(new MemoryStream(redirectedRouteBytes));
                Assert.Contains(checked((ushort)addedId), Commands(inMemoryRoute)
                    .Where(IsFirstBattle).Select(TrainerArgument));
                var routePaths = ScriptFile.GetFilePaths(PlatinumRoute201Script);
                Assert.True(TrainerRosterFileTransaction.TryCommit(new[]
                {
                    new TrainerRosterFileMutation(routePaths.binPath, redirectedRouteBytes),
                    new TrainerRosterFileMutation(routePaths.txtPath,
                        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
                            .GetBytes(route201.ToPlainText(includeActions: true)))
                }, out string routeSaveError), routeSaveError);
                ScriptFile.ClearPlaintextCache();

                var reopenedRoute = new ScriptFile(PlatinumRoute201Script);
                Assert.Contains(checked((ushort)addedId), Commands(reopenedRoute)
                    .Where(IsFirstBattle).Select(TrainerArgument));

                MapHeader townHeader = MapHeader.GetMapHeader(PlatinumTwinleafTownHeader);
                Assert.NotNull(townHeader);
                int townEventId = townHeader.eventFileID;

                MapHeader houseHeader = MapHeader.GetMapHeader(PlatinumPlayerHouseHeader);
                Assert.NotNull(houseHeader);
                var houseMatrix = new GameMatrix(houseHeader.matrixID);
                Assert.Equal(1, houseMatrix.width);
                Assert.Equal(1, houseMatrix.height);
                ushort houseMapId = houseMatrix.maps[0, 0];
                var houseMap = new MapFile(houseMapId, gameFamily);
                var houseEvents = new EventFile(houseHeader.eventFileID);
                var permissionReport = new List<string>
                {
                    $"Header {PlatinumPlayerHouseHeader}, matrix {houseHeader.matrixID}, map {houseMapId}",
                    "Collision grid: . = walkable, # = blocked"
                };
                for (int row = 0; row < MapFile.mapSize; row++)
                {
                    var line = new StringBuilder(MapFile.mapSize);
                    for (int column = 0; column < MapFile.mapSize; column++)
                    {
                        line.Append(houseMap.collisions[row, column] == 0 ? '.' : '#');
                    }
                    permissionReport.Add($"{row:D2} {line}");
                }
                permissionReport.AddRange(houseEvents.warps.Select(warp =>
                    $"Warp ({warp.xMapPosition},{warp.yMapPosition}) -> header {warp.header}, anchor {warp.anchor}"));
                permissionReport.AddRange(houseEvents.overworlds.Select(overworld =>
                    $"Overworld {overworld.owID} ({overworld.xMapPosition},{overworld.yMapPosition})"));
                File.WriteAllLines(Path.Combine(output, "platinum-player-house-permissions.txt"),
                    permissionReport);

                int houseScriptId = houseHeader.scriptFileID;
                var houseScript = new ScriptFile(houseScriptId);
                Assert.True(houseScript.allScripts.Count > 2);
                Assert.Equal("LockAll", houseScript.allScripts[2].commands[0].name);
                houseScript.allScripts[2].commands = new List<ScriptCommand>
                {
                    new ScriptCommand("LockAll"),
                    new ScriptCommand("ReleaseAll"),
                    new ScriptCommand("End")
                };
                byte[] stagedHouseScript = houseScript.ToByteArray();
                var houseScriptPaths = ScriptFile.GetFilePaths(houseScriptId);
                Assert.True(TrainerRosterFileTransaction.TryCommit(new[]
                {
                    new TrainerRosterFileMutation(houseScriptPaths.binPath, stagedHouseScript),
                    new TrainerRosterFileMutation(houseScriptPaths.txtPath,
                        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
                            .GetBytes(houseScript.ToPlainText(includeActions: true)))
                }, out string houseScriptSaveError), houseScriptSaveError);
                ScriptFile.ClearPlaintextCache();
                var reopenedHouseScript = new ScriptFile(houseScriptId);
                Assert.Equal(new[] { "LockAll", "ReleaseAll", "End" },
                    reopenedHouseScript.allScripts[2].commands.Select(command => command.name));

                ushort addedTrainerScript = checked((ushort)expandedLayout.GetScriptNumberForTrainer(
                    addedId, addedId + 1, doubleBattle: false));
                var townEvents = new EventFile(townEventId);
                Overworld template = townEvents.overworlds[0];
                ushort originalTemplateType = template.type;
                ushort originalTemplateScript = template.scriptNumber;
                ushort testOverworldId = checked((ushort)(townEvents.overworlds.Max(x => x.owID) + 1));
                const int trainerGlobalX = 116;
                const int trainerGlobalY = 889;
                var testTrainer = new Overworld(template)
                {
                    owID = testOverworldId,
                    type = (ushort)Overworld.OwType.TRAINER,
                    flag = 0,
                    scriptNumber = addedTrainerScript,
                    movement = 0x0E,
                    orientation = 0,
                    sightRange = 4,
                    xRange = 0,
                    yRange = 0,
                    xMatrixPosition = (ushort)(trainerGlobalX / MapFile.mapSize),
                    yMatrixPosition = (ushort)(trainerGlobalY / MapFile.mapSize),
                    xMapPosition = (short)(trainerGlobalX % MapFile.mapSize),
                    yMapPosition = (short)(trainerGlobalY % MapFile.mapSize),
                    zPosition = 0,
                };
                townEvents.overworlds.Add(testTrainer);
                File.WriteAllBytes(Filesystem.GetEventPath(townEventId), townEvents.ToByteArray());
                var reopenedTown = new EventFile(new MemoryStream(File.ReadAllBytes(
                    Filesystem.GetEventPath(townEventId))));
                Overworld reopenedTrainer = Assert.Single(reopenedTown.overworlds,
                    overworld => overworld.owID == testOverworldId);
                Assert.Equal(addedTrainerScript, reopenedTrainer.scriptNumber);
                Assert.Equal((ushort)Overworld.OwType.TRAINER, reopenedTrainer.type);
                Assert.Equal(trainerGlobalX, reopenedTrainer.xMapPosition +
                    MapFile.mapSize * reopenedTrainer.xMatrixPosition);
                Assert.Equal(trainerGlobalY, reopenedTrainer.yMapPosition +
                    MapFile.mapSize * reopenedTrainer.yMatrixPosition);
                Overworld reopenedTemplate = Assert.Single(reopenedTown.overworlds,
                    overworld => overworld.owID == template.owID);
                Assert.Equal(originalTemplateScript, reopenedTemplate.scriptNumber);
                Assert.Equal(originalTemplateType, reopenedTemplate.type);

                Assert.True(TextArchive.BuildRequiredBins(), "Trainer-name binary rebuild failed");
                byte[] rebuiltTrainerNames = File.ReadAllBytes(
                    TextArchive.GetFilePaths(trainerNamesMessageNumber).binPath);
                Assert.Equal(addedId + 1,
                    BinaryPrimitives.ReadUInt16LittleEndian(rebuiltTrainerNames));
                RepackArchive(DirNames.trainerProperties);
                RepackArchive(DirNames.trainerParty);
                RepackArchive(DirNames.scripts);
                RepackArchive(DirNames.textArchives);
                RepackArchive(DirNames.eventFiles);

                Narc packedProperties = Narc.Open(gameDirs[DirNames.trainerProperties].packedDir);
                Narc packedParties = Narc.Open(gameDirs[DirNames.trainerParty].packedDir);
                Assert.NotNull(packedProperties);
                Assert.NotNull(packedParties);
                Assert.Equal(File.ReadAllBytes(Path.Combine(propertiesDir, addedIndex)),
                    packedProperties.GetElementBytes(addedId));
                Assert.Equal(File.ReadAllBytes(Path.Combine(partyDir, addedIndex)),
                    packedParties.GetElementBytes(addedId));

                Narc packedEvents = Narc.Open(gameDirs[DirNames.eventFiles].packedDir);
                Narc packedScripts = Narc.Open(gameDirs[DirNames.scripts].packedDir);
                Assert.NotNull(packedEvents);
                Assert.NotNull(packedScripts);
                var packedHouseScript = new ScriptFile(new MemoryStream(
                    packedScripts.GetElementBytes(houseScriptId)));
                Assert.Equal(new[] { "LockAll", "ReleaseAll", "End" },
                    packedHouseScript.allScripts[2].commands.Select(command => command.name));
                var packedTown = new EventFile(new MemoryStream(
                    packedEvents.GetElementBytes(townEventId)));
                Overworld packedTrainer = Assert.Single(packedTown.overworlds,
                    overworld => overworld.owID == testOverworldId);
                Assert.Equal(addedTrainerScript, packedTrainer.scriptNumber);
                Assert.Equal((ushort)Overworld.OwType.TRAINER, packedTrainer.type);

                string rom = Path.Combine(output, "issue215-platinum-added-trainer.nds");
                Assert.True(DSUtils.RepackROM(rom) && File.Exists(rom),
                    "Platinum added-trainer ROM repack failed");

                string manifest = Path.Combine(output, "platinum-added-trainer.txt");
                File.WriteAllLines(manifest, new[]
                {
                    "Issue 215 Platinum added-trainer runtime stage",
                    $"Trainer records: {before.TrainerRecordCount} -> {addedId + 1}",
                    $"Added trainer ID: {addedId}",
                    $"Special shared-script index: {before.SpecialScriptIndex} -> {expandedLayout.SpecialIndex}",
                    $"Moved special script number: {movedSpecial}",
                    $"Route 201 battle trainer: {PlatinumRivalTurtwig} -> {addedId}",
                    $"Twinleaf Town test overworld: {testOverworldId}, script {reopenedTrainer.scriptNumber}",
                    $"Twinleaf Town position: ({trainerGlobalX}, {trainerGlobalY})",
                    $"Runtime fixture replaces Player House script {houseScriptId} entry 3 with LockAll/ReleaseAll/End",
                    $"ROM: {Path.GetFileName(rom)}"
                });

                _out.WriteLine($"Added trainer {addedId}; special entry moved to {movedSpecial}.");
                _out.WriteLine($"Built {Path.GetFileName(rom)}");
            }
            finally
            {
                DeleteTemporaryProject(work);
            }
        }

        [SkippableFact]
        public void BuildHeartGoldRoute29Trainer()
        {
            Skip.If(!ModeIs("heartgold"), "DSPRE_ISSUE215_STAGE is not heartgold");
            string output = RequireOutputDirectory();
            string work = CreateTemporaryProjectCopy(TestRoms.HeartGold, "heartgold");

            try
            {
                new RomInfo("IPKE", work);
                var events = new EventFile(HeartGoldRoute29Events);
                int beforeCount = events.overworlds.Count;
                ushort id = 0;
                while (events.overworlds.Any(entry => entry.owID == id)) id++;

                // The representative state is at (652, 400). Three tiles south keeps the object on
                // the same grass patch; facing north with range four makes the encounter immediate
                // after the map is reloaded. These are global field coordinates, split the same way
                // EventFile does for every other object.
                const int globalX = 652;
                const int globalY = 403;
                var trainer = new Overworld(id, globalX / MapFile.mapSize, globalY / MapFile.mapSize)
                {
                    overlayTableEntry = 317, // HGSS SPRITE_GSBOY2
                    movement = 0x0E,         // fixed, facing up
                    type = (ushort)Overworld.OwType.TRAINER,
                    flag = 0,
                    scriptNumber = TrainerScriptBase + HeartGoldYoungsterJoey,
                    orientation = 0,
                    sightRange = 4,
                    param1 = 0,
                    param2 = 0,
                    xRange = 0,
                    yRange = 0,
                    xMapPosition = (short)(globalX % MapFile.mapSize),
                    yMapPosition = (short)(globalY % MapFile.mapSize),
                    zPosition = 0,
                };
                events.overworlds.Add(trainer);
                File.WriteAllBytes(Filesystem.GetEventPath(HeartGoldRoute29Events), events.ToByteArray());

                var reopened = new EventFile(
                    new MemoryStream(File.ReadAllBytes(Filesystem.GetEventPath(HeartGoldRoute29Events))));
                Assert.Equal(beforeCount + 1, reopened.overworlds.Count);
                Overworld staged = Assert.Single(reopened.overworlds, entry => entry.owID == id);
                Assert.Equal(globalX, staged.xMapPosition + MapFile.mapSize * staged.xMatrixPosition);
                Assert.Equal(globalY, staged.yMapPosition + MapFile.mapSize * staged.yMatrixPosition);
                Assert.Equal((ushort)(TrainerScriptBase + HeartGoldYoungsterJoey), staged.scriptNumber);
                Assert.Equal((ushort)Overworld.OwType.TRAINER, staged.type);
                Assert.Equal((ushort)4, staged.sightRange);

                RepackArchive(DirNames.eventFiles);
                string rom = Path.Combine(output, "issue215-heartgold-route29-trainer.nds");
                Assert.True(DSUtils.RepackROM(rom) && File.Exists(rom), "HeartGold ROM repack failed");

                string manifest = Path.Combine(output, "heartgold-route29-trainer.txt");
                File.WriteAllLines(manifest, new[]
                {
                    "Issue 215 HeartGold runtime stage",
                    $"Event archive: {HeartGoldRoute29Events}",
                    $"Overworld count: {beforeCount} -> {reopened.overworlds.Count}",
                    $"New overworld ID: {id}",
                    $"Position: ({globalX}, {globalY})",
                    $"Trainer ID: {HeartGoldYoungsterJoey}",
                    $"Encoded script number: {staged.scriptNumber}",
                    $"ROM: {Path.GetFileName(rom)}",
                });

                _out.WriteLine($"Route 29 overworlds: {beforeCount} -> {reopened.overworlds.Count}");
                _out.WriteLine($"Added trainer {HeartGoldYoungsterJoey} as script {staged.scriptNumber} at ({globalX}, {globalY})");
                _out.WriteLine($"Built {Path.GetFileName(rom)}");
            }
            finally
            {
                DeleteTemporaryProject(work);
            }
        }

        [SkippableFact]
        public void BuildHeartGoldAddedTrainerBattle()
        {
            Skip.If(!ModeIs("heartgold-add"), "DSPRE_ISSUE215_STAGE is not heartgold-add");
            string output = RequireOutputDirectory();
            string work = CreateTemporaryProjectCopy(TestRoms.HeartGold, "heartgold-add");

            try
            {
                SettingsManager.Load();
                new RomInfo("IPKE", work);
                Assert.False(TrainerRosterService.TryAddTrainer(
                    new string('X', trainerNameMaxLen + 1), out _, out string longNameError));
                Assert.Contains("limited", longNameError);
                DSUtils.TryUnpackNarcs(new List<DirNames>
                {
                    DirNames.trainerProperties, DirNames.trainerParty, DirNames.scripts,
                    DirNames.textArchives, DirNames.eventFiles
                });

                TrainerRosterAnalysis before = TrainerRosterService.AnalyzeCurrentProject();
                Assert.True(before.CanAdd, before.RefusalReason);
                Assert.Equal(2, before.RemainingAdditions);

                Assert.True(TrainerRosterService.TryAddTrainer("AddedOne", out int firstId,
                    out string firstError), firstError);
                Assert.Equal(738, firstId);
                TrainerRosterAnalysis afterFirst = TrainerRosterService.AnalyzeCurrentProject();
                Assert.True(afterFirst.CanAdd, afterFirst.RefusalReason);
                Assert.Equal(1, afterFirst.RemainingAdditions);

                Assert.True(TrainerRosterService.TryAddTrainer("AddedTwo", out int secondId,
                    out string secondError), secondError);
                Assert.Equal(739, secondId);
                TrainerRosterAnalysis afterSecond = TrainerRosterService.AnalyzeCurrentProject();
                Assert.False(afterSecond.CanAdd);
                Assert.Equal(0, afterSecond.RemainingAdditions);
                Assert.Contains("at most two", afterSecond.RefusalReason);
                Assert.False(TrainerRosterService.TryAddTrainer("Too Far", out _,
                    out string thirdError));
                Assert.Contains("at most two", thirdError);

                string propertiesDir = gameDirs[DirNames.trainerProperties].unpackedDir;
                string partyDir = gameDirs[DirNames.trainerParty].unpackedDir;
                foreach (int addedId in new[] { firstId, secondId })
                {
                    string addedIndex = addedId.ToString("D4");
                    File.Copy(Path.Combine(propertiesDir, HeartGoldYoungsterJoey.ToString("D4")),
                        Path.Combine(propertiesDir, addedIndex), overwrite: true);
                    File.Copy(Path.Combine(partyDir, HeartGoldYoungsterJoey.ToString("D4")),
                        Path.Combine(partyDir, addedIndex), overwrite: true);
                }

                var names = new TextArchive(trainerNamesMessageNumber);
                List<string> simpleNames = names.GetSimpleTrainerNames();
                Assert.Equal("AddedOne", simpleNames[firstId]);
                Assert.Equal("AddedTwo", simpleNames[secondId]);

                Assert.True(TrainerScriptDescriptor.TryFor(gameFamily,
                    out TrainerScriptDescriptor scriptDescriptor));
                byte[] sharedBytes = File.ReadAllBytes(Filesystem.GetScriptPath(
                    scriptDescriptor.SharedScriptArchiveId));
                Assert.True(TrainerScriptLayout.TryAnalyze(sharedBytes,
                    out TrainerScriptLayout expandedLayout, out string layoutError), layoutError);
                Assert.Equal(before.SpecialScriptIndex + 2, expandedLayout.SpecialIndex);
                ushort movedSpecial = checked((ushort)(3000 + expandedLayout.SpecialIndex));

                Assert.True(TrainerScriptExecutableDescriptor.TryFor(gameVersion, gameLanguage,
                    out TrainerScriptExecutableDescriptor executableDescriptor,
                    out string descriptorError), descriptorError);
                TrainerScriptPatchTarget arm9Target = Assert.Single(executableDescriptor.Targets);
                Assert.True(TrainerScriptExecutableDescriptor.TryLocateTarget(
                    File.ReadAllBytes(arm9Path), movedSpecial, arm9Target.ExpectedThumbReferences,
                    out _, out string patchError), patchError);

                var events = new EventFile(HeartGoldRoute29Events);
                int beforeEventCount = events.overworlds.Count;
                ushort overworldId = 0;
                while (events.overworlds.Any(entry => entry.owID == overworldId)) overworldId++;
                const int globalX = 652;
                const int globalY = 403;
                ushort addedTrainerScript = checked((ushort)expandedLayout.GetScriptNumberForTrainer(
                    secondId, secondId + 1, doubleBattle: false));
                events.overworlds.Add(new Overworld(overworldId,
                    globalX / MapFile.mapSize, globalY / MapFile.mapSize)
                {
                    overlayTableEntry = 317,
                    movement = 0x0E,
                    type = (ushort)Overworld.OwType.TRAINER,
                    flag = 0,
                    scriptNumber = addedTrainerScript,
                    orientation = 0,
                    sightRange = 4,
                    param1 = 0,
                    param2 = 0,
                    xRange = 0,
                    yRange = 0,
                    xMapPosition = (short)(globalX % MapFile.mapSize),
                    yMapPosition = (short)(globalY % MapFile.mapSize),
                    zPosition = 0,
                });
                File.WriteAllBytes(Filesystem.GetEventPath(HeartGoldRoute29Events),
                    events.ToByteArray());
                var reopenedEvents = new EventFile(new MemoryStream(File.ReadAllBytes(
                    Filesystem.GetEventPath(HeartGoldRoute29Events))));
                Assert.Equal(beforeEventCount + 1, reopenedEvents.overworlds.Count);
                Overworld stagedTrainer = Assert.Single(reopenedEvents.overworlds,
                    entry => entry.owID == overworldId);
                Assert.Equal(addedTrainerScript, stagedTrainer.scriptNumber);

                // The configured cold-boot save is outside the Pokémon League rather than on Route
                // 29. Keep the Route 29 scenario above and add the same trainer to the occupied
                // entrance event file so walking through the south door loads a fresh object list.
                MapHeader leagueHeader = MapHeader.GetMapHeader(HeartGoldLeagueEntranceHeader);
                Assert.Equal(HeartGoldLeagueEntranceEvents, leagueHeader.eventFileID);
                var leagueEvents = new EventFile(leagueHeader.eventFileID);
                int beforeLeagueCount = leagueEvents.overworlds.Count;
                const ushort leagueOverworldId = 1;
                const int leagueX = 11;
                const int leagueY = 20;
                Overworld leagueTrainer = Assert.Single(leagueEvents.overworlds,
                    entry => entry.owID == leagueOverworldId);
                leagueTrainer.movement = 0x0F;
                leagueTrainer.type = (ushort)Overworld.OwType.TRAINER;
                leagueTrainer.flag = 0;
                leagueTrainer.scriptNumber = addedTrainerScript;
                leagueTrainer.orientation = 1;
                leagueTrainer.sightRange = 4;
                leagueTrainer.xMapPosition = leagueX;
                leagueTrainer.yMapPosition = leagueY;
                leagueTrainer.xMatrixPosition = 0;
                leagueTrainer.yMatrixPosition = 0;
                File.WriteAllBytes(Filesystem.GetEventPath(HeartGoldLeagueEntranceEvents),
                    leagueEvents.ToByteArray());
                var reopenedLeagueEvents = new EventFile(new MemoryStream(File.ReadAllBytes(
                    Filesystem.GetEventPath(HeartGoldLeagueEntranceEvents))));
                Assert.Equal(beforeLeagueCount, reopenedLeagueEvents.overworlds.Count);
                Assert.Equal(addedTrainerScript, Assert.Single(reopenedLeagueEvents.overworlds,
                    entry => entry.owID == leagueOverworldId).scriptNumber);

                Assert.True(TextArchive.BuildRequiredBins(), "Trainer-name binary rebuild failed");
                byte[] rebuiltTrainerNames = File.ReadAllBytes(
                    TextArchive.GetFilePaths(trainerNamesMessageNumber).binPath);
                Assert.Equal(secondId + 1,
                    BinaryPrimitives.ReadUInt16LittleEndian(rebuiltTrainerNames));
                RepackArchive(DirNames.trainerProperties);
                RepackArchive(DirNames.trainerParty);
                RepackArchive(DirNames.scripts);
                RepackArchive(DirNames.textArchives);
                RepackArchive(DirNames.eventFiles);

                Narc packedProperties = Narc.Open(gameDirs[DirNames.trainerProperties].packedDir);
                Narc packedParties = Narc.Open(gameDirs[DirNames.trainerParty].packedDir);
                Narc packedEvents = Narc.Open(gameDirs[DirNames.eventFiles].packedDir);
                Assert.Equal(File.ReadAllBytes(Path.Combine(propertiesDir, secondId.ToString("D4"))),
                    packedProperties.GetElementBytes(secondId));
                Assert.Equal(File.ReadAllBytes(Path.Combine(partyDir, secondId.ToString("D4"))),
                    packedParties.GetElementBytes(secondId));
                var packedRoute29 = new EventFile(new MemoryStream(
                    packedEvents.GetElementBytes(HeartGoldRoute29Events)));
                Assert.Equal(addedTrainerScript, Assert.Single(packedRoute29.overworlds,
                    entry => entry.owID == overworldId).scriptNumber);
                var packedLeague = new EventFile(new MemoryStream(
                    packedEvents.GetElementBytes(HeartGoldLeagueEntranceEvents)));
                Assert.Equal(addedTrainerScript, Assert.Single(packedLeague.overworlds,
                    entry => entry.owID == leagueOverworldId).scriptNumber);

                string rom = Path.Combine(output, "issue215-heartgold-added-trainers.nds");
                Assert.True(DSUtils.RepackROM(rom) && File.Exists(rom),
                    "HeartGold added-trainer ROM repack failed");

                File.WriteAllLines(Path.Combine(output, "heartgold-added-trainers.txt"), new[]
                {
                    "Issue 215 HeartGold added-trainer runtime stage",
                    $"Trainer records: {before.TrainerRecordCount} -> {secondId + 1}",
                    $"Added trainer IDs: {firstId}, {secondId}",
                    $"Special shared-script index: {before.SpecialScriptIndex} -> {expandedLayout.SpecialIndex}",
                    $"Moved special script number: {movedSpecial}",
                    $"Route 29 trainer ID: {secondId}",
                    $"Pokémon League entrance runtime trainer ID: {secondId}",
                    $"Pokémon League entrance position: ({leagueX}, {leagueY})",
                    $"Encoded script number: {addedTrainerScript}",
                    $"ROM: {Path.GetFileName(rom)}"
                });

                _out.WriteLine($"Added trainers {firstId} and {secondId}; special entry moved to {movedSpecial}.");
                _out.WriteLine($"Built {Path.GetFileName(rom)}");
            }
            finally
            {
                DeleteTemporaryProject(work);
            }
        }

        [SkippableFact]
        public void PlatinumAddedTrainerCannotBeRemovedWhileAnEventUsesIt()
        {
            Skip.If(!ModeIs("remove"), "DSPRE_ISSUE215_STAGE is not remove");
            VerifySafeRemoval("CPUE", TestRoms.Platinum, "platinum-remove");
        }

        [SkippableFact]
        public void HeartGoldAddedTrainerCannotBeRemovedWhileAnEventUsesIt()
        {
            Skip.If(!ModeIs("remove"), "DSPRE_ISSUE215_STAGE is not remove");
            VerifySafeRemoval("IPKE", TestRoms.HeartGold, "heartgold-remove");
        }

        private void VerifySafeRemoval(string gameCode, string source, string label)
        {
            string work = CreateTemporaryProjectCopy(source, label);

            try
            {
                SettingsManager.Load();
                new RomInfo(gameCode, work);
                DSUtils.TryUnpackNarcs(new List<DirNames>
                {
                    DirNames.trainerProperties, DirNames.trainerParty, DirNames.scripts,
                    DirNames.textArchives, DirNames.eventFiles, DirNames.trainerTextTable
                });

                TrainerRosterAnalysis before = TrainerRosterService.AnalyzeCurrentProject();
                Assert.True(before.CanAdd, before.RefusalReason);
                Assert.True(TrainerRosterService.TryAddTrainer("RemoveMe", out int addedId,
                    out string addError), addError);
                Assert.Equal(before.TrainerRecordCount, addedId);

                string eventDirectory = gameDirs[DirNames.eventFiles].unpackedDir;
                string eventPath = null;
                EventFile eventFile = null;
                foreach (string candidate in RomFiles.Settled(eventDirectory))
                {
                    var parsed = new EventFile(new MemoryStream(File.ReadAllBytes(candidate)));
                    if (parsed.overworlds.Count == 0) continue;
                    eventPath = candidate;
                    eventFile = parsed;
                    break;
                }

                Assert.NotNull(eventPath);
                Assert.NotNull(eventFile);
                byte[] originalEvent = File.ReadAllBytes(eventPath);

                Assert.True(TrainerScriptDescriptor.TryFor(gameFamily,
                    out TrainerScriptDescriptor descriptor));
                byte[] sharedBytes = File.ReadAllBytes(Filesystem.GetScriptPath(
                    descriptor.SharedScriptArchiveId));
                Assert.True(TrainerScriptLayout.TryAnalyze(sharedBytes,
                    out TrainerScriptLayout expandedLayout, out string layoutError), layoutError);

                Overworld reference = eventFile.overworlds[0];
                reference.type = (ushort)Overworld.OwType.TRAINER;
                reference.scriptNumber = checked((ushort)expandedLayout.GetScriptNumberForTrainer(
                    addedId, addedId + 1, doubleBattle: false));
                File.WriteAllBytes(eventPath, eventFile.ToByteArray());

                Assert.False(TrainerRosterService.TryRemoveLastAddedTrainer(
                    out _, out string blockedError));
                Assert.Contains("Event: event file", blockedError);
                Assert.True(File.Exists(Path.Combine(
                    gameDirs[DirNames.trainerProperties].unpackedDir, addedId.ToString("D4"))));

                File.WriteAllBytes(eventPath, originalEvent);
                Assert.True(TrainerRosterService.TryRemoveLastAddedTrainer(
                    out int removedId, out string removeError), removeError);
                Assert.Equal(addedId, removedId);

                TrainerRosterAnalysis after = TrainerRosterService.AnalyzeCurrentProject();
                Assert.True(after.IsConsistent, after.RefusalReason);
                Assert.Equal(before.TrainerRecordCount, after.TrainerRecordCount);
                Assert.Equal(before.PartyRecordCount, after.PartyRecordCount);
                Assert.Equal(before.TrainerNameCount, after.TrainerNameCount);
                Assert.Equal(before.SpecialScriptIndex, after.SpecialScriptIndex);
                Assert.Equal(before.RemainingAdditions, after.RemainingAdditions);
                Assert.False(File.Exists(Path.Combine(
                    gameDirs[DirNames.trainerProperties].unpackedDir, addedId.ToString("D4"))));
                Assert.False(File.Exists(Path.Combine(
                    gameDirs[DirNames.trainerParty].unpackedDir, addedId.ToString("D4"))));

                AssertExecutableSpecialScript(after.SpecialScriptIndex,
                    gameFamily == GameFamilies.Plat ? 2 : 1);
                Assert.False(TrainerRosterService.TryRemoveLastAddedTrainer(
                    out _, out string retailError));
                Assert.Contains("Retail trainers cannot be removed", retailError);
                _out.WriteLine($"{gameFamily}: trainer {addedId} was blocked by an event reference, then removed after the reference was cleared.");
            }
            finally
            {
                DeleteTemporaryProject(work);
            }
        }

        private static void AssertExecutableSpecialScript(int specialIndex, int expectedTargetCount)
        {
            Assert.True(TrainerScriptExecutableDescriptor.TryFor(gameVersion, gameLanguage,
                out TrainerScriptExecutableDescriptor descriptor, out string descriptorError),
                descriptorError);
            Assert.Equal(expectedTargetCount, descriptor.Targets.Count);

            ushort scriptNumber = checked((ushort)(TrainerScriptBase + 1 + specialIndex));
            foreach (TrainerScriptPatchTarget target in descriptor.Targets)
            {
                string path = target.File == TrainerScriptExecutableFile.Arm9
                    ? arm9Path
                    : OverlayUtils.GetPath(target.OverlayNumber);
                Assert.True(TrainerScriptExecutableDescriptor.TryLocateTarget(
                    File.ReadAllBytes(path), scriptNumber, target.ExpectedThumbReferences,
                    out _, out string error), error);
            }
        }

        private static bool ModeIs(string expected) =>
            string.Equals(Environment.GetEnvironmentVariable("DSPRE_ISSUE215_STAGE"), expected,
                StringComparison.OrdinalIgnoreCase);

        private static string RequireOutputDirectory()
        {
            string output = Environment.GetEnvironmentVariable("DSPRE_ISSUE215_OUTPUT");
            Assert.False(string.IsNullOrWhiteSpace(output), "DSPRE_ISSUE215_OUTPUT is required");
            output = Path.GetFullPath(output);
            Directory.CreateDirectory(output);
            return output;
        }

        private static string CreateTemporaryProjectCopy(string source, string game)
        {
            Assert.True(Directory.Exists(source), $"the {game} test project is not available");
            string root = Path.Combine(Path.GetTempPath(), "DSPRE", "Issue215");
            string work = Path.Combine(root, $"{game}-{Guid.NewGuid():N}");
            CopyTree(source, work);
            return work;
        }

        private static void CopyTree(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(source, directory);
                Directory.CreateDirectory(Path.Combine(destination, relative));
            }

            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(source, file);
                File.Copy(file, Path.Combine(destination, relative));
            }
        }

        private static void DeleteTemporaryProject(string work)
        {
            string fullWork = Path.GetFullPath(work);
            string safeRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "DSPRE", "Issue215"))
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.StartsWith(safeRoot, fullWork, StringComparison.OrdinalIgnoreCase);
            if (Directory.Exists(fullWork)) Directory.Delete(fullWork, recursive: true);
        }

        private static IEnumerable<ScriptCommand> Commands(ScriptFile file) =>
            file.allScripts.Concat(file.allFunctions)
                .Where(container => container.commands != null)
                .SelectMany(container => container.commands);

        private static bool IsFirstBattle(ScriptCommand command) =>
            command.name != null
            && command.name.StartsWith("FirstBattle", StringComparison.OrdinalIgnoreCase)
            && command.cmdParams?.Count > 0
            && command.cmdParams[0].Length == sizeof(ushort);

        private static ushort TrainerArgument(ScriptCommand command) =>
            BitConverter.ToUInt16(command.cmdParams[0], 0);

        private static string HexContext(byte[] data, int offset)
        {
            int start = Math.Max(0, offset - 16);
            int length = Math.Min(data.Length - start, 34);
            return $"0x{start:X4}: {Convert.ToHexString(data, start, length)}";
        }

        private static void RepackArchive(DirNames archive)
        {
            var directory = gameDirs[archive];
            Narc.FromFolder(directory.unpackedDir).Save(directory.packedDir);
        }
    }
}
