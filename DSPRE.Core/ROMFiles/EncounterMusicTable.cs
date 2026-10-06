using System;
using System.Collections.Generic;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The eye-contact music table: one row per trainer class that has eye-contact music, class (u16) then the music,
    /// with a second, night-time music in HeartGold and SoulSilver. It may be repointed into the synthetic overlay.
    /// </summary>
    public static class EncounterMusicTable
    {
        public sealed class Row
        {
            /// <summary>Where the row starts in <see cref="Location.Path"/>.</summary>
            public uint Offset;
            public ushort Class, Music;
            public ushort? NightMusic;
        }

        public sealed class Location
        {
            public string Path;
            public uint Start;
            public bool Repointed;
        }

        public static (Location Where, List<Row> Rows) Read()
        {
            RomInfo.SetEncounterMusicTableOffsetToRAMAddress();
            bool hgss = RomInfo.gameFamily == RomInfo.GameFamilies.HGSS;
            uint ram = BitConverter.ToUInt32(ARM9.ReadBytes(RomInfo.encounterMusicTableOffsetToRAMAddress, 4), 0);
            Location where = new Location { Repointed = ram >= RomInfo.synthOverlayLoadAddress };
            where.Start = ram - (where.Repointed ? RomInfo.synthOverlayLoadAddress : ARM9.address);
            where.Path = where.Repointed ? Filesystem.expArmPath : RomInfo.arm9Path;

            byte count = ARM9.ReadByte(RomInfo.encounterMusicTableOffsetToRAMAddress - (hgss ? 12u : 10u));
            List<Row> rows = new List<Row>(count);
            using DSUtils.EasyReader reader = new DSUtils.EasyReader(where.Path, where.Start);
            for (int i = 0; i < count; i++)
            {
                Row row = new Row { Offset = (uint)reader.BaseStream.Position, Class = reader.ReadUInt16(), Music = reader.ReadUInt16() };
                if (hgss) row.NightMusic = reader.ReadUInt16();
                rows.Add(row);
            }
            return (where, rows);
        }

        /// <summary>Writes a row's music (and night music in HeartGold and SoulSilver) back in place.</summary>
        public static void WriteMusic(Location where, Row row)
        {
            DSUtils.WriteToFile(where.Path, BitConverter.GetBytes(row.Music), row.Offset + 2);
            if (row.NightMusic is ushort night) DSUtils.WriteToFile(where.Path, BitConverter.GetBytes(night), row.Offset + 4);
        }
    }
}
