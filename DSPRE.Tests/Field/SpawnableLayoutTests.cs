using System.IO;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// The spawnable record is the engine's BG_TALK_DATA: id, type, int gx, int gz, int height, dir,
    /// padding.
    /// </summary>
    public class SpawnableLayoutTests
    {
        private static byte[] Record(ushort id, ushort type, int gx, int gz, int height, ushort dir)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(id); w.Write(type); w.Write(gx); w.Write(gz); w.Write(height); w.Write(dir); w.Write((ushort)0);
            return ms.ToArray();
        }

        [Fact]
        public void ReadsEachFieldFromTheEngineOffsets()
        {
            var s = new Spawnable(new MemoryStream(Record(14, 1, 682, 393, 7, 4)));

            Assert.Equal(14, s.scriptNumber);
            Assert.Equal(1, s.type);
            Assert.Equal(682 % 32, s.xMapPosition);
            Assert.Equal(682 / 32, s.xMatrixPosition);
            Assert.Equal(393 % 32, s.yMapPosition);
            Assert.Equal(393 / 32, s.yMatrixPosition);
            Assert.Equal(7, s.zPosition);      // the real height field, not gz's top half
            Assert.Equal(4, s.dir);
        }

        [Fact]
        public void HeightWriteDoesNotDisturbTheGridCoordinates()
        {
            var s = new Spawnable(new MemoryStream(Record(14, 1, 682, 393, 0, 4)));
            s.zPosition = 7;
            byte[] outBytes = s.ToByteArray();

            Assert.Equal(0x14, outBytes.Length);
            Assert.Equal(682, System.BitConverter.ToInt32(outBytes, 4));    // gx untouched
            Assert.Equal(393, System.BitConverter.ToInt32(outBytes, 8));    // gz untouched
            Assert.Equal(7, System.BitConverter.ToInt32(outBytes, 12));     // height written here
        }

        [Fact]
        public void RoundTripsUnchanged()
        {
            byte[] original = Record(8201, 2, 107, 893, 3, 4);
            byte[] again = new Spawnable(new MemoryStream(original)).ToByteArray();
            Assert.Equal(original, again);
        }
    }
}
