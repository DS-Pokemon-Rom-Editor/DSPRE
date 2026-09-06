using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// Characterizes the retail shared trainer-script bank before issue 215 changes it. These tests
    /// deliberately read the raw pointer header: parsing the command bodies is a separate concern and
    /// would hide the aliasing and special final entry that roster expansion must preserve.
    /// </summary>
    [Collection("rom")]
    public class TrainerScriptBoundaryTests
    {
        [SkippableFact]
        public void DiamondRetailTableSeparatesTheEyeMeetsEntry()
            => CheckRetailBoundary("ADAE", TestRoms.Diamond, RomInfo.GameFamilies.DP,
                expectedFunnyNumber: 851, sharedScriptArchiveId: 1040,
                expectedTrainerCount: 850, expectedNonRosterGenericEntries: 1);

        [SkippableFact]
        public void PlatinumRetailTableSeparatesTheEyeMeetsEntry()
            => CheckRetailBoundary("CPUE", TestRoms.Platinum, RomInfo.GameFamilies.Plat,
                expectedFunnyNumber: 929, sharedScriptArchiveId: 1114,
                expectedTrainerCount: 928, expectedNonRosterGenericEntries: 1);

        [SkippableFact]
        public void HeartGoldRetailTableSeparatesTheEyeMeetsEntry()
            => CheckRetailBoundary("IPKE", TestRoms.HeartGold, RomInfo.GameFamilies.HGSS,
                expectedFunnyNumber: 740, sharedScriptArchiveId: 953,
                expectedTrainerCount: 738, expectedNonRosterGenericEntries: 2);

        private static void CheckRetailBoundary(string gameCode, string project,
            RomInfo.GameFamilies expectedFamily, int expectedFunnyNumber, int sharedScriptArchiveId,
            int expectedTrainerCount, int expectedNonRosterGenericEntries)
        {
            Skip.IfNot(Directory.Exists(project), $"The {expectedFamily} test ROM project is not available.");

            SettingsManager.Load();
            new RomInfo(gameCode, project);

            Assert.Equal(expectedFamily, RomInfo.gameFamily);
            Assert.Equal(expectedFunnyNumber, RomInfo.trainerFunnyScriptNumber);
            Assert.True(TrainerScriptDescriptor.TryFor(expectedFamily,
                out TrainerScriptDescriptor descriptor));
            Assert.Equal(sharedScriptArchiveId, descriptor.SharedScriptArchiveId);
            Assert.Equal(expectedFunnyNumber - 1, descriptor.RetailSpecialIndex);

            if (expectedFamily == RomInfo.GameFamilies.Plat)
            {
                AssertEnglishExecutableConsumers(expectedFunnyNumber - 1, expectedTargetCount: 2);
                TrainerRosterAnalysis analysis = TrainerRosterService.AnalyzeCurrentProject();
                Assert.True(analysis.CanAdd, analysis.RefusalReason);
                Assert.Equal(expectedTrainerCount, analysis.TrainerRecordCount);
                Assert.Equal(expectedTrainerCount, analysis.PartyRecordCount);
                Assert.Equal(expectedTrainerCount, analysis.TrainerNameCount);
                Assert.Equal(expectedFunnyNumber - 1, analysis.SpecialScriptIndex);
                Assert.Equal(71, analysis.RemainingAdditions);
            }
            else if (expectedFamily == RomInfo.GameFamilies.HGSS)
            {
                Assert.Equal(RomInfo.GameVersions.HeartGold, RomInfo.gameVersion);
                AssertEnglishExecutableConsumers(expectedFunnyNumber - 1, expectedTargetCount: 1);
                TrainerRosterAnalysis analysis = TrainerRosterService.AnalyzeCurrentProject();
                Assert.True(analysis.CanAdd, analysis.RefusalReason);
                Assert.Equal(expectedTrainerCount, analysis.TrainerRecordCount);
                Assert.Equal(expectedTrainerCount, analysis.PartyRecordCount);
                Assert.Equal(expectedTrainerCount, analysis.TrainerNameCount);
                Assert.Equal(expectedFunnyNumber - 1, analysis.SpecialScriptIndex);
                Assert.Equal(2, analysis.RemainingAdditions);
            }

            string trainerPropertiesDir = RomInfo.gameDirs[RomInfo.DirNames.trainerProperties].unpackedDir;
            string scriptsDir = RomInfo.gameDirs[RomInfo.DirNames.scripts].unpackedDir;
            Skip.IfNot(Directory.Exists(trainerPropertiesDir),
                $"The {expectedFamily} trainer-properties archive is not unpacked.");
            Skip.IfNot(Directory.Exists(scriptsDir),
                $"The {expectedFamily} script archive is not unpacked.");

            int trainerPropertiesCount = RomFiles.Settled(trainerPropertiesDir).Length;
            Assert.True(trainerPropertiesCount > 0,
                $"The {expectedFamily} trainer-properties archive contained no records.");
            Assert.Equal(expectedTrainerCount, trainerPropertiesCount);

            string trainerPartyNarc = RomInfo.gameDirs[RomInfo.DirNames.trainerParty].packedDir;
            Assert.True(File.Exists(trainerPartyNarc),
                $"The {expectedFamily} trainer-party NARC was not found.");
            Assert.Equal(trainerPropertiesCount, ReadNarcElementCount(trainerPartyNarc));

            string trainerNames = Path.Combine(
                RomInfo.gameDirs[RomInfo.DirNames.textArchives].unpackedDir,
                RomInfo.trainerNamesMessageNumber.ToString("D4"));
            Assert.True(File.Exists(trainerNames),
                $"The {expectedFamily} trainer-name archive was not found.");
            Assert.Equal(trainerPropertiesCount, ReadMessageCount(trainerNames));

            string sharedScriptPath = Path.Combine(scriptsDir, sharedScriptArchiveId.ToString("D4"));
            Assert.True(File.Exists(sharedScriptPath),
                $"The {expectedFamily} shared trainer-script file was not found.");

            byte[] scriptBytes = File.ReadAllBytes(sharedScriptPath);
            IReadOnlyList<int> targets = ReadScriptPointerTargets(scriptBytes);
            Assert.True(targets.Count > 0,
                $"The {expectedFamily} shared trainer-script table contained no pointers.");
            Assert.Equal(expectedFunnyNumber, targets.Count);

            int genericTrainerTarget = targets[0];
            Assert.All(targets.Take(targets.Count - 1), target => Assert.Equal(genericTrainerTarget, target));
            Assert.NotEqual(genericTrainerTarget, targets[^1]);
            Assert.Equal(2, targets.Distinct().Count());
            Assert.Equal(targets.Count - 1, targets.Count(target => target == genericTrainerTarget));

            Assert.True(TrainerScriptLayout.TryAnalyze(scriptBytes, out TrainerScriptLayout layout,
                out string layoutError), layoutError);
            Assert.Equal(targets.Count, layout.EntryCount);
            Assert.Equal(targets.Count - 1, layout.GenericEntryCount);
            Assert.Equal(targets.Count - 1, layout.SpecialIndex);
            Assert.Equal(genericTrainerTarget, layout.GenericTarget);
            Assert.Equal(targets[^1], layout.SpecialTarget);

            ScriptFile.SetSuppressInvalidCommandErrors(true);
            try
            {
                using var input = new MemoryStream(scriptBytes, writable: false);
                var parsed = new ScriptFile(input, readFunctions: true, readActions: true,
                    fileID: sharedScriptArchiveId);
                Assert.False(parsed.parseFailedDueToInvalidCommand);
                string genericBody = CommandSignature(parsed.allScripts[0]);
                string specialBody = CommandSignature(parsed.allScripts[^1]);
                Assert.Equal(targets.Count, parsed.allScripts.Count);
                Assert.Equal(2, parsed.allScripts.Count(script => script.usedScriptID == -1));
                Assert.Equal(targets.Count - 2,
                    parsed.allScripts.Count(script => script.usedScriptID != -1));

                byte[] roundTripped = parsed.ToByteArray();
                Assert.NotNull(roundTripped);
                IReadOnlyList<int> roundTrippedTargets = ReadScriptPointerTargets(roundTripped);
                Assert.Equal(targets.Count, roundTrippedTargets.Count);
                Assert.All(roundTrippedTargets.Take(roundTrippedTargets.Count - 1),
                    target => Assert.Equal(roundTrippedTargets[0], target));
                Assert.NotEqual(roundTrippedTargets[0], roundTrippedTargets[^1]);

                Assert.True(TrainerScriptLayout.TryInsertGenericAliasBeforeSpecial(parsed,
                    out string firstInsertError), firstInsertError);
                byte[] firstExpansion = parsed.ToByteArray();
                Assert.NotNull(firstExpansion);
                Assert.True(TrainerScriptLayout.TryAnalyze(firstExpansion,
                    out TrainerScriptLayout firstLayout, out string firstLayoutError), firstLayoutError);
                Assert.Equal(layout.EntryCount + 1, firstLayout.EntryCount);
                Assert.Equal(layout.GenericEntryCount + 1, firstLayout.GenericEntryCount);
                Assert.Equal(layout.SpecialIndex + 1, firstLayout.SpecialIndex);
                Assert.Equal(3000 + expectedTrainerCount - 1,
                    firstLayout.GetScriptNumberForTrainer(expectedTrainerCount,
                        expectedTrainerCount + 1, doubleBattle: false));

                using var firstInput = new MemoryStream(firstExpansion, writable: false);
                var firstParsed = new ScriptFile(firstInput, readFunctions: true, readActions: true,
                    fileID: sharedScriptArchiveId);
                Assert.False(firstParsed.parseFailedDueToInvalidCommand);
                Assert.Equal(genericBody, CommandSignature(firstParsed.allScripts[0]));
                Assert.Equal(specialBody, CommandSignature(firstParsed.allScripts[^1]));
                Assert.Equal(1, firstParsed.allScripts[^2].usedScriptID);
                Assert.True(TrainerScriptLayout.TryInsertGenericAliasBeforeSpecial(firstParsed,
                    out string secondInsertError), secondInsertError);
                byte[] secondExpansion = firstParsed.ToByteArray();
                Assert.NotNull(secondExpansion);
                Assert.True(TrainerScriptLayout.TryAnalyze(secondExpansion,
                    out TrainerScriptLayout secondLayout, out string secondLayoutError), secondLayoutError);
                Assert.Equal(layout.EntryCount + 2, secondLayout.EntryCount);
                Assert.Equal(layout.GenericEntryCount + 2, secondLayout.GenericEntryCount);
                Assert.Equal(layout.SpecialIndex + 2, secondLayout.SpecialIndex);
                Assert.Equal(3000 + expectedTrainerCount,
                    secondLayout.GetScriptNumberForTrainer(expectedTrainerCount + 1,
                        expectedTrainerCount + 2, doubleBattle: false));

                using var secondInput = new MemoryStream(secondExpansion, writable: false);
                var secondParsed = new ScriptFile(secondInput, readFunctions: true, readActions: true,
                    fileID: sharedScriptArchiveId);
                Assert.False(secondParsed.parseFailedDueToInvalidCommand);
                Assert.Equal(genericBody, CommandSignature(secondParsed.allScripts[0]));
                Assert.Equal(specialBody, CommandSignature(secondParsed.allScripts[^1]));
                Assert.Equal(1, secondParsed.allScripts[^2].usedScriptID);
                Assert.Equal(layout.EntryCount + 2, secondParsed.allScripts.Count);
                Assert.Equal(2, secondParsed.allScripts.Count(script => script.usedScriptID == -1));
                Assert.Equal(layout.EntryCount,
                    secondParsed.allScripts.Count(script => script.usedScriptID != -1));
            }
            finally
            {
                ScriptFile.SetSuppressInvalidCommandErrors(false);
            }

            // Entry zero represents trainer ID 1, so exclude trainer-properties record zero when
            // comparing generic shared entries with the current real trainer roster. The test only
            // measures the excess entries; it does not assume they are supported expansion capacity.
            int ordinarySharedEntries = targets.Count - 1;
            int currentTrainerIds = trainerPropertiesCount - 1;
            Assert.Equal(expectedNonRosterGenericEntries, ordinarySharedEntries - currentTrainerIds);
        }

        private static int ReadNarcElementCount(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            Assert.True(bytes.Length >= 0x1C, "The trainer-party NARC header is truncated.");
            Assert.Equal((byte)'N', bytes[0]);
            Assert.Equal((byte)'A', bytes[1]);
            Assert.Equal((byte)'R', bytes[2]);
            Assert.Equal((byte)'C', bytes[3]);
            return checked((int)BitConverter.ToUInt32(bytes, 0x18));
        }

        private static void AssertEnglishExecutableConsumers(int specialIndex, int expectedTargetCount)
        {
            Assert.Equal(RomInfo.GameLanguages.English, RomInfo.gameLanguage);
            Assert.True(TrainerScriptExecutableDescriptor.TryFor(RomInfo.gameVersion,
                RomInfo.gameLanguage, out TrainerScriptExecutableDescriptor descriptor,
                out string descriptorError), descriptorError);
            Assert.Equal(expectedTargetCount, descriptor.Targets.Count);

            ushort specialScriptNumber = checked((ushort)(3000 + specialIndex));
            foreach (TrainerScriptPatchTarget target in descriptor.Targets)
            {
                string path = target.File == TrainerScriptExecutableFile.Arm9
                    ? RomInfo.arm9Path
                    : OverlayUtils.GetPath(target.OverlayNumber);
                Assert.True(File.Exists(path), $"The {target.File} patch target was not found.");
                byte[] bytes = File.ReadAllBytes(path);
                Assert.True(TrainerScriptExecutableDescriptor.TryLocateTarget(bytes,
                    specialScriptNumber, target.ExpectedThumbReferences, out TrainerScriptPatchSite site,
                    out string error), error);
                Assert.True(site.LiteralOffset >= 0);
                Assert.Equal(target.ExpectedThumbReferences, site.ReferenceOffsets.Count);
            }
        }

        private static string CommandSignature(ScriptCommandContainer container)
        {
            return string.Join("|", container.commands.Select(command =>
                $"{command.id}:{string.Join(",", command.cmdParams.SelectMany(bytes => bytes).Select(value => value.ToString("X2")))}"));
        }

        private static int ReadMessageCount(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            Assert.True(bytes.Length >= 2, "The trainer-name archive header is truncated.");
            return BitConverter.ToUInt16(bytes, 0);
        }

        private static IReadOnlyList<int> ReadScriptPointerTargets(byte[] bytes)
        {
            var targets = new List<int>();
            for (int position = 0; ; position += sizeof(uint))
            {
                if (position + sizeof(ushort) > bytes.Length)
                {
                    throw new InvalidDataException("The shared trainer-script table has no FD13 terminator.");
                }

                if (BitConverter.ToUInt16(bytes, position) == 0xFD13)
                {
                    return targets;
                }

                if (position + sizeof(uint) > bytes.Length)
                {
                    throw new InvalidDataException("The shared trainer-script pointer table is truncated.");
                }

                uint relativeOffset = BitConverter.ToUInt32(bytes, position);
                long target = (long)position + sizeof(uint) + relativeOffset;
                if (target < 0 || target >= bytes.Length)
                {
                    throw new InvalidDataException(
                        $"Shared trainer-script pointer {targets.Count} targets 0x{target:X} outside the file.");
                }

                targets.Add(checked((int)target));
            }
        }
    }
}
