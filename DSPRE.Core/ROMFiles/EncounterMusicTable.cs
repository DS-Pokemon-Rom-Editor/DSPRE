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

        /// <summary>The RAM address the code's pointer holds: arm9, or the synthetic overlay once a patch moved the table.</summary>
        public static uint Address() => BitConverter.ToUInt32(ARM9.ReadBytes(RomInfo.EncounterMusicTableOffsetToRAMAddress, 4), 0);

        public static (Location Where, List<Row> Rows) Read()
        {
            bool hgss = RomInfo.GameFamily == RomInfo.GameFamilies.HGSS;
            uint ram = Address();
            Location where = new Location { Repointed = ram >= RomInfo.SynthOverlayLoadAddress };
            where.Start = ram - (where.Repointed ? RomInfo.SynthOverlayLoadAddress : ARM9.Address);
            where.Path = where.Repointed ? Filesystem.ExpArmPath : RomInfo.Arm9Path;

            byte count = ARM9.ReadByte(RomInfo.EncounterMusicCountSite);
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
