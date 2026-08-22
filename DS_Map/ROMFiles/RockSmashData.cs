using System;
using System.IO;

namespace DSPRE.ROMFiles
{
    // HGSS Rock Smash per-header metadata (data/a/2/5/3): item-drop odds and which of the three
    // hardcoded item tables (Default / Ruins of Alph / Cliff Cave) a header rolls from when a Rock
    // Smash encounter fails to produce a wild Pokemon. One 4-byte file per Map Header ID.
    // https://ds-pokemon-hacking.github.io/docs/generation-iv/guides/hgss-rock_smash/
    public class RockSmashData
    {
        public enum TableType : ushort { Default = 0, RuinsOfAlph = 1, CliffCave = 2 }

        public ushort ID;
        public ushort Odds;                        // 0-100; 0 disables item drops for this header
        public TableType Type = TableType.Default;

        // False if a/2/5/3 had no file for this header (a modified ROM missing NARC entries, or a
        // header added without a matching Rock Smash file). SaveToFile creates it.
        public bool Existed;

        public RockSmashData(ushort id)
        {
            ID = id;
            Parse(Filesystem.GetRockSmashPath(id));
        }

        public RockSmashData(ushort id, string path)
        {
            ID = id;
            Parse(path);
        }

        private void Parse(string path)
        {
            if (!File.Exists(path))
            {
                Odds = 0;
                Type = TableType.Default;
                Existed = false;
                return;
            }

            byte[] data = File.ReadAllBytes(path);
            Odds = data.Length >= 2 ? BitConverter.ToUInt16(data, 0) : (ushort)0;
            ushort rawType = data.Length >= 4 ? BitConverter.ToUInt16(data, 2) : (ushort)0;
            Type = Enum.IsDefined(typeof(TableType), rawType) ? (TableType)rawType : TableType.Default;
            Existed = true;
        }

        public byte[] ToByteArray()
        {
            byte[] data = new byte[4];
            BitConverter.GetBytes(Odds).CopyTo(data, 0);
            BitConverter.GetBytes((ushort)Type).CopyTo(data, 2);
            return data;
        }

        public bool SaveToFile() => SaveToFile(Filesystem.GetRockSmashPath(ID));

        public bool SaveToFile(string path)
        {
            File.WriteAllBytes(path, ToByteArray());
            Existed = true;
            return true;
        }
    }
}
