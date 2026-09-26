using System;
using System.Collections.Generic;
using System.IO;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Total EXP per level, one curve per growth rate: 8 members of 101 u32 (levels 0-100), 6 and 7 unused.
    /// The game scans up from level 1, so level 1 must stay 0 and totals must keep rising.
    /// </summary>
    public class GrowthTable
    {
        public const int Curves = 8, Levels = 101;

        public static readonly string[] CurveNames =
            { "Medium Fast", "Erratic", "Fluctuating", "Medium Slow", "Fast", "Slow", "Unused 1", "Unused 2" };

        public uint[][] Totals { get; } = new uint[Curves][];

        public static string WhyNot()
        {
            if (!gameDirs.ContainsKey(DirNames.growthTable)) return "This game has no known growth table.";
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.growthTable });
            for (int c = 0; c < Curves; c++)
                if (!File.Exists(PathOf(c))) return $"Growth curve {c} is missing from this project.";
            return null;
        }

        private static string PathOf(int curve) => Path.Combine(gameDirs[DirNames.growthTable].unpackedDir, curve.ToString("D4"));

        public static GrowthTable Load()
        {
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.growthTable });
            var table = new GrowthTable();
            for (int c = 0; c < Curves; c++)
            {
                byte[] data = File.ReadAllBytes(PathOf(c));
                if (data.Length < Levels * 4) throw new InvalidDataException($"Growth curve {c} is {data.Length} bytes; it needs {Levels * 4}.");
                table.Totals[c] = new uint[Levels];
                for (int l = 0; l < Levels; l++) table.Totals[c][l] = BitConverter.ToUInt32(data, l * 4);
            }
            return table;
        }

        /// <summary>Why a curve can't be saved, or null.</summary>
        public string Problem(int curve)
        {
            var t = Totals[curve];
            if (t[1] != 0) return $"{CurveNames[curve]}: level 1 must need 0 EXP.";
            for (int l = 2; l < Levels; l++)
                if (t[l] <= t[l - 1]) return $"{CurveNames[curve]}: level {l} needs more EXP than level {l - 1}.";
            return null;
        }

        public void Save(int curve)
        {
            if (Problem(curve) is string p) throw new InvalidOperationException(p);
            string path = PathOf(curve);
            byte[] data = File.ReadAllBytes(path);
            for (int l = 0; l < Levels; l++) BitConverter.GetBytes(Totals[curve][l]).CopyTo(data, l * 4);
            File.WriteAllBytes(path, data);
        }

        /// <summary>A copy of <paramref name="curve"/>'s totals.</summary>
        public uint[] Copy(int curve) => (uint[])Totals[curve].Clone();
    }
}
