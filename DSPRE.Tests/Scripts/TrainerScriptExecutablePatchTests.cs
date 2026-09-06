using System;
using System.Buffers.Binary;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    public class TrainerScriptExecutablePatchTests
    {
        [Fact]
        public void HeartGoldEnglishPatchesOnlyTheTwoArm9EyeMeetsConsumers()
        {
            Assert.True(TrainerScriptExecutableDescriptor.TryFor(RomInfo.GameVersions.HeartGold,
                RomInfo.GameLanguages.English, out TrainerScriptExecutableDescriptor descriptor,
                out string error), error);

            TrainerScriptPatchTarget target = Assert.Single(descriptor.Targets);
            Assert.Equal(TrainerScriptExecutableFile.Arm9, target.File);
            Assert.Equal(2, target.ExpectedThumbReferences);
        }

        [Fact]
        public void SignaturePatchChangesOnlyTheReferencedLiteral()
        {
            byte[] source = MakeThumbLiteralFixture(referenceCount: 2, literalOffset: 0x40,
                scriptNumber: 3928);

            Assert.True(TrainerScriptExecutableDescriptor.TryPatchTarget(source, 3928, 3929, 2,
                out byte[] patched, out TrainerScriptPatchSite site, out string error), error);
            Assert.Equal(0x40, site.LiteralOffset);
            Assert.Equal(2, site.ReferenceOffsets.Count);
            Assert.Equal((uint)3928, BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(0x40, 4)));
            Assert.Equal((uint)3929, BinaryPrimitives.ReadUInt32LittleEndian(patched.AsSpan(0x40, 4)));

            for (int i = 0; i < source.Length; i++)
            {
                if (i < 0x40 || i >= 0x44)
                {
                    Assert.Equal(source[i], patched[i]);
                }
            }
        }

        [Fact]
        public void SignaturePatchRejectsAnUnexpectedReferenceCount()
        {
            byte[] source = MakeThumbLiteralFixture(referenceCount: 1, literalOffset: 0x40,
                scriptNumber: 3928);

            Assert.False(TrainerScriptExecutableDescriptor.TryPatchTarget(source, 3928, 3929, 2,
                out _, out _, out string error));
            Assert.Contains("found 1", error);
        }

        [Fact]
        public void SignaturePatchRejectsDuplicateLiterals()
        {
            byte[] source = MakeThumbLiteralFixture(referenceCount: 1, literalOffset: 0x40,
                scriptNumber: 3928);
            BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(0x50, 4), 3928);

            Assert.False(TrainerScriptExecutableDescriptor.TryPatchTarget(source, 3928, 3929, 1,
                out _, out _, out string error));
            Assert.Contains("found 2", error);
        }

        private static byte[] MakeThumbLiteralFixture(int referenceCount, int literalOffset,
            ushort scriptNumber)
        {
            byte[] bytes = new byte[0x60];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(literalOffset, 4), scriptNumber);
            for (int i = 0; i < referenceCount; i++)
            {
                int instructionOffset = 0x10 + i * 4;
                int alignedPc = (instructionOffset + 4) & ~3;
                int immediateWords = (literalOffset - alignedPc) / 4;
                ushort instruction = checked((ushort)(0x4800 | (i << 8) | immediateWords));
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(instructionOffset, 2), instruction);
            }
            return bytes;
        }
    }
}
