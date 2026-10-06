using System;
using System.Collections.Generic;
using System.IO;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// One berry's record in the berry archive; the 64 berries follow item order from Cheri and the count is compiled in.
    /// </summary>
    public class BerryData
    {
        public const int Count = 64, Size = 12, FirstBerryItem = 149;

        public static readonly string[] Firmness = { "Very Soft", "Soft", "Hard", "Very Hard", "Super Hard" };
        public static readonly string[] Flavours = { "Spicy", "Dry", "Sweet", "Bitter", "Sour" };

        public ushort SizeMm;
        public byte FirmnessLevel;      // 1-5
        public byte Yield;
        public byte HoursPerStage;
        public byte Drain;
        public byte[] Flavour = new byte[5];
        public byte Smoothness;

        public BerryData(byte[] data)
        {
            if (data == null || data.Length < Size) throw new InvalidDataException($"A berry record is {Size} bytes.");
            SizeMm = BitConverter.ToUInt16(data, 0);
            FirmnessLevel = data[2];
            Yield = data[3];
            HoursPerStage = data[4];
            Drain = data[5];
            Array.Copy(data, 6, Flavour, 0, 5);
            Smoothness = data[11];
        }

        public byte[] ToBytes()
        {
            byte[] data = new byte[Size];
            BitConverter.GetBytes(SizeMm).CopyTo(data, 0);
            data[2] = FirmnessLevel; data[3] = Yield; data[4] = HoursPerStage; data[5] = Drain;
            Array.Copy(Flavour, 0, data, 6, 5);
            data[11] = Smoothness;
            return data;
        }

        /// <summary>Why the berry can't be saved, or null.</summary>
        public string Problem()
        {
            if (FirmnessLevel < 1 || FirmnessLevel > 5) return "Firmness must be one of the five the game names.";
            if (HoursPerStage < 1) return "Each growth stage must take at least an hour.";
            if (Yield < 1) return "A berry must yield at least one.";
            return null;
        }

        public static string WhyNot()
        {
            if (!gameDirs.ContainsKey(DirNames.berryData)) return "This game has no known berry data.";
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.berryData });
            for (int b = 0; b < Count; b++)
                if (!File.Exists(PathOf(b))) return $"Berry {b} is missing from this project.";
            return null;
        }

        private static string PathOf(int berry) => Path.Combine(gameDirs[DirNames.berryData].unpackedDir, berry.ToString("D4"));

        public static List<BerryData> LoadAll()
        {
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.berryData });
            List<BerryData> all = new List<BerryData>();
            for (int b = 0; b < Count; b++) all.Add(new BerryData(File.ReadAllBytes(PathOf(b))));
            return all;
        }

        /// <summary>Writes the record, keeping anything past its 12 bytes.</summary>
        public void Save(int berry)
        {
            if (Problem() is string p) throw new InvalidOperationException(p);
            string path = PathOf(berry);
            byte[] data = File.ReadAllBytes(path);
            if (data.Length < Size) data = new byte[Size];
            ToBytes().CopyTo(data, 0);
            File.WriteAllBytes(path, data);
        }

        /// <summary>HeartGold and SoulSilver read only growth time, soil drying and yield.</summary>
        public static bool GameReadsLooks => gameFamily != GameFamilies.HGSS;
    }
}
