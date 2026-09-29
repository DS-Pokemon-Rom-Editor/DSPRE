using System;
using System.IO;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// Saving a map writes its buildings section back as it was read, including the bytes DSPRE does not
    /// edit and a section that does not end on a whole record.
    /// </summary>
    public class BuildingRecordRoundTripTests
    {
        private static MapFile MapWithBuildings(byte[] section)
        {
            var data = new MemoryStream();
            using (var w = new BinaryWriter(data, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                w.Write(2048);
                w.Write(section.Length);
                w.Write(4);
                w.Write(0);
                w.Write(new byte[2048]);
                w.Write(section);
                w.Write(0);   // not a model; the map loads without one
            }
            data.Position = 0;
            return new MapFile(data, RomInfo.GameFamilies.Plat, discardMoveperms: false, showMessages: false);
        }

        private static byte[] Record(byte seed)
        {
            var r = new byte[MapFile.buildingHeaderSize];
            for (int i = 0; i < r.Length; i++) r[i] = (byte)(seed + i);
            // Rotation high halves are written as zero, so keep them zero in the fixture.
            r[0x12] = r[0x13] = r[0x16] = r[0x17] = r[0x1A] = r[0x1B] = 0;
            r[0x28] = 1;
            return r;
        }

        [Fact]
        public void UneditedBytesAndATrailingPartialRecordSurvive()
        {
            byte[] a = Record(3), b = Record(90), tail = { 0xAB, 0xCD };
            var section = new byte[a.Length + b.Length + tail.Length];
            Buffer.BlockCopy(a, 0, section, 0, a.Length);
            Buffer.BlockCopy(b, 0, section, a.Length, b.Length);
            Buffer.BlockCopy(tail, 0, section, a.Length + b.Length, tail.Length);

            var map = MapWithBuildings(section);

            Assert.Equal(2, map.buildings.Count);
            Assert.Equal(section, map.BuildingsToByteArray());
        }

        [Fact]
        public void AnEditKeepsTheRestOfTheRecord()
        {
            byte[] a = Record(7);
            var map = MapWithBuildings(a);
            map.buildings[0].xPosition = -5;

            byte[] saved = map.BuildingsToByteArray();

            Assert.Equal((short)-5, BitConverter.ToInt16(saved, 0x06));
            Assert.Equal(1, saved[0x28]);
            Assert.Equal(a[0x1C], saved[0x1C]);
            Assert.Equal(a[0x2F], saved[0x2F]);
        }
    }
}
