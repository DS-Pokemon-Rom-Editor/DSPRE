using System;
using System.IO;

namespace DSPRE.ROMFiles
{
    /// <summary>What each species' egg hatches into: pms.narc, 16 bits per species.</summary>
    public static class HatchResultTable
    {
        private static string FilePath => Path.Combine(RomInfo.DataPath, "poketool", "personal", "pms.narc");

        public static int Read(int species)
        {
            string path = FilePath;
            if (!Holds(path, species)) return 0;
            return BitConverter.ToUInt16(DSUtils.ReadFromFile(path, species * 2, 2), 0);
        }

        public static void Write(int species, int value)
        {
            string path = FilePath;
            if (!Holds(path, species)) return;
            DSUtils.WriteToFile(path, BitConverter.GetBytes((ushort)value), (uint)(species * 2), fmode: FileMode.Open);
        }

        private static bool Holds(string path, int species) =>
            species >= 0 && File.Exists(path) && species * 2 + 2 <= new FileInfo(path).Length;
    }
}
