using System;
using System.Collections.Generic;

namespace DSPRE.Models
{
    /// <summary>Cells covered by lines, rectangles and ellipses, as PDSMS computes them.</summary>
    public static class TileShapes
    {
        public static List<(int x, int z)> Line((int x, int z) a, (int x, int z) b)
        {
            var cells = new List<(int x, int z)>();
            int x0 = a.x, z0 = a.z, x1 = b.x, z1 = b.z;
            int dx = Math.Abs(x1 - x0), dz = Math.Abs(z1 - z0);
            int sx = x0 < x1 ? 1 : -1, sz = z0 < z1 ? 1 : -1;
            int err = dx - dz;
            while (true)
            {
                cells.Add((x0, z0));
                if (x0 == x1 && z0 == z1) break;
                int e2 = 2 * err;
                if (e2 > -dz) { err -= dz; x0 += sx; }
                if (e2 < dx) { err += dx; z0 += sz; }
            }
            return cells;
        }

        public static List<(int x, int z)> EdgeToEdgeLine((int x, int z) a, (int x, int z) b)
        {
            var diagonal = Line(a, b);
            var cells = new List<(int x, int z)>();
            bool acrossFirst = Math.Abs(b.x - a.x) >= Math.Abs(b.z - a.z);
            cells.Add(diagonal[0]);
            for (int i = 1; i < diagonal.Count; i++)
            {
                var was = diagonal[i - 1];
                var next = diagonal[i];
                if (was.x != next.x && was.z != next.z)
                {
                    var between = acrossFirst ? (next.x, was.z) : (was.x, next.z);
                    if (cells[cells.Count - 1] != between) cells.Add(between);
                }
                if (cells[cells.Count - 1] != next) cells.Add(next);
            }
            return cells;
        }

        public static void Extend(List<(int x, int z)> stroke, (int x, int z) to)
        {
            if (stroke.Count == 0) { stroke.Add(to); return; }
            var from = stroke[stroke.Count - 1];
            if (from == to) return;
            var segment = Line(from, to);
            for (int i = 1; i < segment.Count; i++)
                if (stroke[stroke.Count - 1] != segment[i]) stroke.Add(segment[i]);
        }

        public static bool[,] Enclosed(bool[,] outline)
        {
            int w = outline.GetLength(0), h = outline.GetLength(1);
            var outside = new bool[w + 2, h + 2];
            var waiting = new Stack<(int x, int z)>();
            waiting.Push((0, 0));
            outside[0, 0] = true;
            while (waiting.Count > 0)
            {
                var (px, pz) = waiting.Pop();
                foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = px + dx, nz = pz + dz;
                    if (nx < 0 || nz < 0 || nx >= w + 2 || nz >= h + 2 || outside[nx, nz]) continue;
                    int mx = nx - 1, mz = nz - 1;
                    if (mx >= 0 && mz >= 0 && mx < w && mz < h && outline[mx, mz]) continue;
                    outside[nx, nz] = true;
                    waiting.Push((nx, nz));
                }
            }
            var inside = new bool[w, h];
            for (int x = 0; x < w; x++)
                for (int z = 0; z < h; z++)
                    inside[x, z] = outline[x, z] || !outside[x + 1, z + 1];
            return inside;
        }

        public static List<(int x, int z)> RectangleOutline((int x, int z) a, (int x, int z) b)
        {
            var cells = new List<(int x, int z)>();
            int minX = Math.Min(a.x, b.x), maxX = Math.Max(a.x, b.x);
            int minZ = Math.Min(a.z, b.z), maxZ = Math.Max(a.z, b.z);
            for (int x = minX; x <= maxX; x++)
            {
                cells.Add((x, minZ));
                if (maxZ != minZ) cells.Add((x, maxZ));
            }
            for (int z = minZ + 1; z < maxZ; z++)
            {
                cells.Add((minX, z));
                if (maxX != minX) cells.Add((maxX, z));
            }
            return cells;
        }

        public static List<(int x, int z)> RectangleFilled((int x, int z) a, (int x, int z) b)
        {
            var cells = new List<(int x, int z)>();
            int minX = Math.Min(a.x, b.x), maxX = Math.Max(a.x, b.x);
            int minZ = Math.Min(a.z, b.z), maxZ = Math.Max(a.z, b.z);
            for (int x = minX; x <= maxX; x++)
                for (int z = minZ; z <= maxZ; z++)
                    cells.Add((x, z));
            return cells;
        }

        public static List<(int x, int z)> EllipseFilled((int x, int z) a, (int x, int z) b)
        {
            var cells = new List<(int x, int z)>();
            foreach (var (x, z, inside) in Ellipse(a, b))
                if (inside) cells.Add((x, z));
            return cells;
        }

        public static List<(int x, int z)> EllipseOutline((int x, int z) a, (int x, int z) b)
        {
            int minX = Math.Min(a.x, b.x), maxX = Math.Max(a.x, b.x);
            int minZ = Math.Min(a.z, b.z), maxZ = Math.Max(a.z, b.z);
            int w = maxX - minX + 1, h = maxZ - minZ + 1;
            var fill = new bool[w, h];
            foreach (var (x, z, inside) in Ellipse(a, b)) fill[x - minX, z - minZ] = inside;

            var cells = new List<(int x, int z)>();
            for (int i = 0; i < w; i++)
                for (int j = 0; j < h; j++)
                {
                    if (!fill[i, j]) continue;
                    bool edge = i == 0 || j == 0 || i == w - 1 || j == h - 1
                             || !fill[i - 1, j] || !fill[i + 1, j] || !fill[i, j - 1] || !fill[i, j + 1];
                    if (edge) cells.Add((minX + i, minZ + j));
                }
            return cells;
        }

        private static IEnumerable<(int x, int z, bool inside)> Ellipse((int x, int z) a, (int x, int z) b)
        {
            int minX = Math.Min(a.x, b.x), maxX = Math.Max(a.x, b.x);
            int minZ = Math.Min(a.z, b.z), maxZ = Math.Max(a.z, b.z);
            double cx = (minX + maxX + 1) / 2.0, cz = (minZ + maxZ + 1) / 2.0;
            double rx = (maxX - minX + 1) / 2.0, rz = (maxZ - minZ + 1) / 2.0;
            for (int x = minX; x <= maxX; x++)
                for (int z = minZ; z <= maxZ; z++)
                {
                    double dx = (x + 0.5 - cx) / rx, dz = (z + 0.5 - cz) / rz;
                    yield return (x, z, dx * dx + dz * dz <= 1.0);
                }
        }
    }
}
