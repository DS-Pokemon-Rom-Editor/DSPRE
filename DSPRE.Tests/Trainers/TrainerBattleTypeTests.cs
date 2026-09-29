using System;
using System.IO;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>The game ORs the whole battle type in, so saving a trainer keeps bits DSPRE has no box for.</summary>
    public class TrainerBattleTypeTests
    {
        private static byte[] Record(uint battleType)
        {
            var bytes = new byte[20];
            bytes[3] = 1;
            BitConverter.GetBytes(1u).CopyTo(bytes, 12);
            BitConverter.GetBytes(battleType).CopyTo(bytes, 16);
            return bytes;
        }

        [Fact]
        public void OtherBitsSurviveASave()
        {
            var trp = new TrainerProperties(1, new MemoryStream(Record(0x42)));
            Assert.True(trp.doubleBattle);
            Assert.Equal(Record(0x42), trp.ToByteArray());
        }

        [Fact]
        public void TogglingDoubleOnlyTouchesItsBit()
        {
            var trp = new TrainerProperties(1, new MemoryStream(Record(0x40))) { doubleBattle = true };
            Assert.Equal(0x42u, BitConverter.ToUInt32(trp.ToByteArray(), 16));
            trp.doubleBattle = false;
            Assert.Equal(0x40u, BitConverter.ToUInt32(trp.ToByteArray(), 16));
        }
    }
}
