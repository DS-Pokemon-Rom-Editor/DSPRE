using System;
using System.Collections.Generic;

namespace DSPRE.ROMFiles
{
    /// <summary>The ARM9 table of headers whose music changes with a flag: header, flag and music, six bytes a row.</summary>
    public static class ConditionalMusicTable
    {
        public sealed class Row
        {
            public ushort Header, Flag, Music;
        }

        /// <summary>The table's ARM9 offset and its rows, as many as the count byte before the pointer says.</summary>
        public static (uint Start, List<Row> Rows) Read()
        {
            RomInfo.SetConditionalMusicTableOffsetToRAMAddress();
            uint pointer = RomInfo.conditionalMusicTableOffsetToRAMAddress;
            uint start = BitConverter.ToUInt32(ARM9.ReadBytes(pointer, 4), 0) - ARM9.address;
            byte count = ARM9.ReadByte(pointer - 8);
            var rows = new List<Row>(count);
            using (var r = new ARM9.Reader(start))
                for (int i = 0; i < count; i++)
                    rows.Add(new Row { Header = r.ReadUInt16(), Flag = r.ReadUInt16(), Music = r.ReadUInt16() });
            return (start, rows);
        }

        public static void Write(uint start, IReadOnlyList<Row> rows)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                ARM9.WriteBytes(BitConverter.GetBytes(rows[i].Header), (uint)(start + 6 * i));
                ARM9.WriteBytes(BitConverter.GetBytes(rows[i].Flag), (uint)(start + 6 * i + 2));
                ARM9.WriteBytes(BitConverter.GetBytes(rows[i].Music), (uint)(start + 6 * i + 4));
            }
        }
    }
}
