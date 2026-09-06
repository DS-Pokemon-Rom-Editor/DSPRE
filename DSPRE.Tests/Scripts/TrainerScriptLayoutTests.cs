using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    public class TrainerScriptLayoutTests
    {
        [Fact]
        public void AnalyzerIdentifiesGenericAliasesAndFinalSpecialEntry()
        {
            byte[] bytes = MakePointerTable(18, 18, 18, 20);

            Assert.True(TrainerScriptLayout.TryAnalyze(bytes, out TrainerScriptLayout layout,
                out string error), error);
            Assert.Equal(4, layout.EntryCount);
            Assert.Equal(3, layout.GenericEntryCount);
            Assert.Equal(3, layout.SpecialIndex);
            Assert.Equal(18, layout.GenericTarget);
            Assert.Equal(20, layout.SpecialTarget);
        }

        [Fact]
        public void AnalyzerRejectsAnOrdinaryEntryWithAnotherBody()
        {
            byte[] bytes = MakePointerTable(18, 19, 18, 20);

            Assert.False(TrainerScriptLayout.TryAnalyze(bytes, out _, out string error));
            Assert.Contains("entry 1", error);
        }

        [Fact]
        public void AnalyzerRejectsAFileWithoutATerminatedPointerTable()
        {
            Assert.False(TrainerScriptLayout.TryAnalyze(Array.Empty<byte>(), out _, out string error));
            Assert.Contains("terminator", error);
        }

        [Fact]
        public void InsertionRefusesUnexpectedAliasesWithoutChangingIds()
        {
            var scripts = new List<ScriptCommandContainer>
            {
                new ScriptCommandContainer(1, ScriptFile.ContainerTypes.Script,
                    commandList: new List<ScriptCommand>()),
                new ScriptCommandContainer(2, ScriptFile.ContainerTypes.Script, usedScriptID: 2),
                new ScriptCommandContainer(3, ScriptFile.ContainerTypes.Script,
                    commandList: new List<ScriptCommand>())
            };
            var file = new ScriptFile(scripts, new List<ScriptCommandContainer>(),
                new List<ScriptActionContainer>());

            Assert.False(TrainerScriptLayout.TryInsertGenericAliasBeforeSpecial(file, out string error));
            Assert.Contains("entry 1", error);
            Assert.Equal(new uint[] { 1, 2, 3 }, file.allScripts.ConvertAll(script => script.manualUserID));
        }

        [Fact]
        public void RemovingFinalAliasRestoresThePreviousSpecialEntry()
        {
            var genericCommands = new List<ScriptCommand>();
            var specialCommands = new List<ScriptCommand>();
            var scripts = new List<ScriptCommandContainer>
            {
                new ScriptCommandContainer(1, ScriptFile.ContainerTypes.Script,
                    commandList: genericCommands),
                new ScriptCommandContainer(2, ScriptFile.ContainerTypes.Script, usedScriptID: 1),
                new ScriptCommandContainer(3, ScriptFile.ContainerTypes.Script, usedScriptID: 1),
                new ScriptCommandContainer(4, ScriptFile.ContainerTypes.Script,
                    commandList: specialCommands),
            };
            var functions = new List<ScriptCommandContainer>
            {
                new ScriptCommandContainer(1, ScriptFile.ContainerTypes.Function, usedScriptID: 4),
            };
            var file = new ScriptFile(scripts, functions, new List<ScriptActionContainer>());

            Assert.True(TrainerScriptLayout.TryRemoveGenericAliasBeforeSpecial(file,
                out string error), error);
            Assert.Equal(new uint[] { 1, 2, 3 },
                file.allScripts.ConvertAll(script => script.manualUserID));
            Assert.Same(specialCommands, file.allScripts[^1].commands);
            Assert.Equal(3, file.allFunctions[0].usedScriptID);
        }

        [Fact]
        public void MappingRemainsDirectAndRejectsTheSpecialEntry()
        {
            byte[] bytes = MakePointerTable(18, 18, 18, 20);
            Assert.True(TrainerScriptLayout.TryAnalyze(bytes, out TrainerScriptLayout layout,
                out string error), error);

            Assert.Equal(3002, layout.GetScriptNumberForTrainer(3, trainerRecordCount: 4,
                doubleBattle: false));
            Assert.Equal(5002, layout.GetScriptNumberForTrainer(3, trainerRecordCount: 4,
                doubleBattle: true));
            Assert.True(layout.TryGetTrainerId(3002, trainerRecordCount: 4, out int singleId));
            Assert.Equal(3, singleId);
            Assert.True(layout.TryGetTrainerId(5002, trainerRecordCount: 4, out int doubleId));
            Assert.Equal(3, doubleId);
            Assert.False(layout.TryGetTrainerId(3003, trainerRecordCount: 4, out _));
        }

        [Theory]
        [InlineData(RomInfo.GameFamilies.DP, 1040, 850)]
        [InlineData(RomInfo.GameFamilies.Plat, 1114, 928)]
        [InlineData(RomInfo.GameFamilies.HGSS, 953, 739)]
        public void RetailDescriptorsCentralizeKnownSharedBanks(RomInfo.GameFamilies family,
            int archiveId, int specialIndex)
        {
            Assert.True(TrainerScriptDescriptor.TryFor(family, out TrainerScriptDescriptor descriptor));
            Assert.Equal(archiveId, descriptor.SharedScriptArchiveId);
            Assert.Equal(specialIndex, descriptor.RetailSpecialIndex);
        }

        private static byte[] MakePointerTable(params int[] targets)
        {
            int terminatorOffset = targets.Length * sizeof(uint);
            byte[] bytes = new byte[terminatorOffset + sizeof(ushort) + 4];
            for (int i = 0; i < targets.Length; i++)
            {
                int pointerPosition = i * sizeof(uint);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(pointerPosition, sizeof(uint)),
                    checked((uint)(targets[i] - pointerPosition - sizeof(uint))));
            }

            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(terminatorOffset, sizeof(ushort)),
                0xFD13);
            return bytes;
        }
    }
}
