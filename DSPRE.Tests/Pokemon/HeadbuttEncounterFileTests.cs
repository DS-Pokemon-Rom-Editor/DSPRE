using System;
using System.IO;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests.Pokemon
{
    public class HeadbuttEncounterFileTests
    {
        private static byte[] RoundTrip(byte[] bytes)
        {
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(path, bytes);
                return new HeadbuttEncounterFile(path).ToByteArray();
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void AMapWithNoTreesStaysFourBytes()
        {
            Assert.Equal(new byte[4], RoundTrip(new byte[4]));
        }

        [Fact]
        public void SlotsOnAMapWithNoTreesSurviveARoundTrip()
        {
            var bytes = new byte[4 + 18 * 4];
            BitConverter.GetBytes((ushort)25).CopyTo(bytes, 4);
            bytes[6] = 10;
            bytes[7] = 12;
            BitConverter.GetBytes((ushort)151).CopyTo(bytes, 4 + 17 * 4);
            bytes[4 + 17 * 4 + 2] = 50;
            bytes[4 + 17 * 4 + 3] = 55;

            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(path, bytes);
                var file = new HeadbuttEncounterFile(path);
                Assert.Equal(25, file.normalEncounters[0].pokemonID);
                Assert.Equal(12, file.normalEncounters[0].maxLevel);
                Assert.Equal(151, file.specialEncounters[5].pokemonID);
                Assert.Equal(50, file.specialEncounters[5].minLevel);
                Assert.Equal(bytes, file.ToByteArray());
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void CountsAreSixteenBit()
        {
            // 256 regular groups: 4-byte counts, 18 slots of 4 bytes, then 6 trees of 4 bytes per group.
            var bytes = new byte[4 + 18 * 4 + 256 * 6 * 4];
            BitConverter.GetBytes((ushort)256).CopyTo(bytes, 0);
            bytes[4] = 1;
            bytes[^1] = 7;
            Assert.Equal(bytes, RoundTrip(bytes));
        }
    }
}
