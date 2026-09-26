using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace DSPRE.Models
{
    /// <summary>
    /// Terrain plates a user kept on a map, stored beside the project because BDHC has no room for the flag.
    /// One plate per line: min x, min z, max x, max z, normal x, y, z, constant.
    /// </summary>
    public static class KeptPlatesFile
    {
        private static string PathFor(int mapIndex) =>
            RomInfo.workDir == null ? null : Path.Combine(RomInfo.workDir, "expanded", "terrain", $"{mapIndex:D4}.kept");

        public static List<BdhcBuild.Piece> Load(int mapIndex)
        {
            var plates = new List<BdhcBuild.Piece>();
            string path = PathFor(mapIndex);
            if (path == null || !File.Exists(path)) return plates;
            foreach (string line in File.ReadAllLines(path))
            {
                var v = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (v.Length != 8) continue;
                var f = v.Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
                var plate = new BdhcBuild.Piece { MinX = f[0], MinZ = f[1], MaxX = f[2], MaxZ = f[3], Nx = f[4], Ny = f[5], Nz = f[6], D = f[7], Mine = true };
                plate.AtY = plate.Height;
                plates.Add(plate);
            }
            return plates;
        }

        public static void Save(int mapIndex, IReadOnlyList<BdhcBuild.Piece> plates)
        {
            string path = PathFor(mapIndex);
            if (path == null) return;
            if (plates == null || plates.Count == 0)
            {
                if (File.Exists(path)) File.Delete(path);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllLines(path, plates.Select(p => string.Join(" ",
                new[] { p.MinX, p.MinZ, p.MaxX, p.MaxZ, p.Nx, p.Ny, p.Nz, p.D }.Select(x => x.ToString("R", CultureInfo.InvariantCulture)))));
        }
    }
}
