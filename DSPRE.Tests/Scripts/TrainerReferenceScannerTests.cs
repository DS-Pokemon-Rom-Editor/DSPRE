using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    public class TrainerReferenceScannerTests
    {
        [Fact]
        public void EventScanReportsOnlyTrainerEventsWithTheRequestedTrainerId()
        {
            var events = new EventFile();
            events.overworlds.Add(new Overworld(4, 0, 0)
            {
                type = (ushort)Overworld.OwType.TRAINER,
                scriptNumber = 3928,
            });
            events.overworlds.Add(new Overworld(5, 0, 0)
            {
                type = (ushort)Overworld.OwType.NORMAL,
                scriptNumber = 3928,
            });
            var references = new List<TrainerReference>();

            TrainerReferenceScanner.FindEventReferences(RomInfo.GameFamilies.Plat, 12,
                events, 929, references);

            TrainerReference reference = Assert.Single(references);
            Assert.Equal("Event", reference.Kind);
            Assert.Contains("event file 12, overworld 4", reference.Location);
        }

        [Theory]
        [InlineData(RomInfo.GameFamilies.Plat, 0x00E5, 0)]
        [InlineData(RomInfo.GameFamilies.Plat, 0x0125, 0)]
        [InlineData(RomInfo.GameFamilies.HGSS, 0x00D5, 1)]
        [InlineData(RomInfo.GameFamilies.HGSS, 0x00D6, 0)]
        public void ScriptScanFindsLiteralTrainerParametersIncludingHgssTrainerMessage(
            RomInfo.GameFamilies family, ushort commandId, int parameterIndex)
        {
            var parameters = new List<byte[]> { BitConverter.GetBytes((ushort)0) };
            while (parameters.Count <= parameterIndex) parameters.Add(BitConverter.GetBytes((ushort)0));
            parameters[parameterIndex] = BitConverter.GetBytes((ushort)739);
            var scripts = new List<ScriptCommandContainer>
            {
                new ScriptCommandContainer(1, ScriptFile.ContainerTypes.Script,
                    commandList: new List<ScriptCommand>
                    {
                        new ScriptCommand("Synthetic trainer command", parameters, commandId)
                    }),
            };
            var file = new ScriptFile(scripts, new List<ScriptCommandContainer>(),
                new List<ScriptActionContainer>());
            var references = new List<TrainerReference>();

            TrainerReferenceScanner.FindScriptReferences(family, 77, file, 739, references);

            TrainerReference reference = Assert.Single(references);
            Assert.Equal("Script", reference.Kind);
            Assert.Contains("file 77", reference.Location);
        }

        [Fact]
        public void BattleMessageScanFindsTrainerIdAndRejectsPartialEntries()
        {
            byte[] table = new byte[8];
            BinaryPrimitives.WriteUInt16LittleEndian(table.AsSpan(4, 2), 739);
            BinaryPrimitives.WriteUInt16LittleEndian(table.AsSpan(6, 2), 17);
            var references = new List<TrainerReference>();

            Assert.True(TrainerReferenceScanner.TryFindBattleMessageReferences(table, 739,
                references, out string error), error);
            Assert.Contains("entry 1, trigger 17", Assert.Single(references).Location);
            Assert.False(TrainerReferenceScanner.TryFindBattleMessageReferences(
                new byte[3], 739, references, out error));
            Assert.Contains("four-byte", error);
        }

        [Fact]
        public void PhoneBookScanUsesTheStoredTrainerIdAndRejectsTruncation()
        {
            byte[] data = new byte[4 + 20];
            BinaryPrimitives.WriteUInt32LittleEndian(data, 1);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(8, 2), 739);
            var references = new List<TrainerReference>();

            Assert.True(TrainerReferenceScanner.TryFindPhoneBookReferences(data, 739,
                references, out string error), error);
            Assert.Contains("entry 0", Assert.Single(references).Location);

            BinaryPrimitives.WriteUInt32LittleEndian(data, 2);
            Assert.False(TrainerReferenceScanner.TryFindPhoneBookReferences(data, 739,
                references, out error));
            Assert.Contains("truncated", error);
        }
    }
}
