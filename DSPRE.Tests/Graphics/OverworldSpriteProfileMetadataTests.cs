using System;
using System.IO;
using System.Linq;
using DSPRE;
using Xunit;

namespace DSPRE.Tests
{
    [Collection("rom")]
    public class OverworldSpriteProfileMetadataTests
    {
        [Fact]
        public void PlatinumPatch_ClonesOnlyAnimationPayloadAndCanRollback()
        {
            string path = MakeTempFile(96);
            try
            {
                byte[] data = File.ReadAllBytes(path);
                WriteU32(data, 0, 1); WriteU32(data, 4, 10);
                WriteU32(data, 8, 2); WriteU32(data, 12, 11);
                WriteU32(data, 16, 0xFFFF);
                WriteU32(data, 24, 1); Fill(data, 28, 12, 0x11);
                WriteU32(data, 40, 2); Fill(data, 44, 12, 0x22);
                WriteU32(data, 56, 0xFFFF);
                File.WriteAllBytes(path, data);

                Assert.True(OverworldSpriteProfileMetadata.TryCreateDppPatch(path, 0, 1, 2, out var patch, out string error), error);
                Assert.True(patch.TryApply(out error), error);

                byte[] applied = File.ReadAllBytes(path);
                Assert.Equal(1u, BitConverter.ToUInt32(applied, 24));
                Assert.Equal(new byte[12].Select((_, i) => (byte)0x22), applied[28..40]);
                Assert.True(patch.TryRollback(out error), error);
                Assert.Equal(new byte[12].Select((_, i) => (byte)0x11), File.ReadAllBytes(path)[28..40]);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void HgssPatch_ClonesOnlyPropertyWord()
        {
            string path = MakeTempFile(24);
            try
            {
                byte[] data = File.ReadAllBytes(path);
                WriteU16(data, 0, 1); WriteU16(data, 2, 20); WriteU16(data, 4, 0x1234);
                WriteU16(data, 6, 2); WriteU16(data, 8, 21); WriteU16(data, 10, 0x4E25);
                WriteU16(data, 12, 0xFFFF);
                File.WriteAllBytes(path, data);

                Assert.True(OverworldSpriteProfileMetadata.TryCreateHgssPatch(path, 0, 1, 2, out var patch, out string error), error);
                Assert.True(patch.TryApply(out error), error);

                byte[] applied = File.ReadAllBytes(path);
                Assert.Equal((ushort)1, BitConverter.ToUInt16(applied, 0));
                Assert.Equal((ushort)20, BitConverter.ToUInt16(applied, 2));
                Assert.Equal((ushort)0x4E25, BitConverter.ToUInt16(applied, 4));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Apply_RefusesToOverwriteMetadataChangedAfterStaging()
        {
            string path = MakeTempFile(24);
            try
            {
                byte[] data = File.ReadAllBytes(path);
                WriteU16(data, 0, 1); WriteU16(data, 4, 0x1111);
                WriteU16(data, 6, 2); WriteU16(data, 10, 0x2222);
                WriteU16(data, 12, 0xFFFF);
                File.WriteAllBytes(path, data);

                Assert.True(OverworldSpriteProfileMetadata.TryCreateHgssPatch(path, 0, 1, 2, out var patch, out string error), error);
                data[4] = 0x33;
                File.WriteAllBytes(path, data);

                Assert.False(patch.TryApply(out error));
                Assert.Contains("changed", error, StringComparison.OrdinalIgnoreCase);
                Assert.Equal((byte)0x33, File.ReadAllBytes(path)[4]);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [SkippableFact]
        public void FindsVanillaPlatinumAnimationPayloadInConfiguredRom()
        {
            Skip.IfNot(Directory.Exists(TestRoms.Platinum), "The Platinum test ROM project is not available.");
            new RomInfo("CPUE", TestRoms.Platinum);
            RomInfo.Set3DOverworldsDict();
            RomInfo.SetOWtable();
            RomInfo.ReadOWTable();

            Assert.True(OverworldSpriteProfileMetadata.TryCreatePatch(1, 252, out var patch, out string error), error);
            Assert.NotNull(patch);
        }

        [SkippableFact]
        public void FindsHeartGoldPropertyWordInConfiguredRom()
        {
            Skip.IfNot(Directory.Exists(TestRoms.HeartGold), "The HeartGold test ROM project is not available.");
            new RomInfo("IPKE", TestRoms.HeartGold);
            RomInfo.Set3DOverworldsDict();
            RomInfo.SetOWtable();
            RomInfo.ReadOWTable();

            Assert.True(OverworldSpriteProfileMetadata.TryCreatePatch(1, 415, out var patch, out string error), error);
            Assert.NotNull(patch);
        }

        private static string MakeTempFile(int length)
        {
            string path = Path.Combine(Path.GetTempPath(), "dspre-profile-" + Guid.NewGuid().ToString("N"));
            File.WriteAllBytes(path, new byte[length]);
            return path;
        }

        private static void Fill(byte[] data, int offset, int length, byte value) =>
            Array.Fill(data, value, offset, length);

        private static void WriteU16(byte[] data, int offset, int value) =>
            BitConverter.GetBytes((ushort)value).CopyTo(data, offset);

        private static void WriteU32(byte[] data, int offset, uint value) =>
            BitConverter.GetBytes(value).CopyTo(data, offset);
    }
}
