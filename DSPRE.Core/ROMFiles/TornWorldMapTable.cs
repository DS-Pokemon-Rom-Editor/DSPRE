using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>tw_arc member 0: floor list and world placement.</summary>
    public class TornWorldMapTable : RomFile
    {
        public const int EntrySize = 12;

        public class Floor
        {
            public long HeaderId;
            public int FileIndex;
            public short OffsetX, OffsetAltitude, OffsetZ;

            public int DataMember => FileIndex + 1;
        }

        public List<Floor> Floors { get; } = new List<Floor>();

        public TornWorldMapTable() { }

        public TornWorldMapTable(byte[] data)
        {
            if (data == null || data.Length < sizeof(int))
                throw new ArgumentException("The Distortion World map table is shorter than four bytes.");

            using (BinaryReader reader = new BinaryReader(new MemoryStream(data)))
            {
                int count = reader.ReadInt32();
                if (sizeof(int) + count * EntrySize > data.Length)
                    throw new ArgumentException("The Distortion World map table lists more floors than it holds.");

                for (int i = 0; i < count; i++)
                {
                    Floors.Add(new Floor
                    {
                        HeaderId = reader.ReadUInt32(),
                        FileIndex = reader.ReadUInt16(),
                        OffsetX = reader.ReadInt16(),
                        OffsetAltitude = reader.ReadInt16(),
                        OffsetZ = reader.ReadInt16(),
                    });
                }
            }
        }

        public Floor ForHeader(int headerId) => Floors.FirstOrDefault(f => f.HeaderId == headerId);

        public bool IsDistortionWorld(int headerId) => ForHeader(headerId) != null;

        public override byte[] ToByteArray()
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(Floors.Count);
                foreach (Floor floor in Floors)
                {
                    writer.Write((uint)floor.HeaderId);
                    writer.Write((ushort)floor.FileIndex);
                    writer.Write(floor.OffsetX);
                    writer.Write(floor.OffsetAltitude);
                    writer.Write(floor.OffsetZ);
                }
                return stream.ToArray();
            }
        }
    }
}
