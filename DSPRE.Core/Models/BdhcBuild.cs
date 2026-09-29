using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.ROMFiles;

namespace DSPRE.Models
{
    /// <summary>Builds BDHC terrain from a map model's faces.</summary>
    public static class BdhcBuild
    {
        private const float Fx32One = 4096f;
        private const float UnitsPerRaw = 64f;

        public sealed class Piece
        {
            public float MinX, MaxX, MinZ, MaxZ;

            public float Nx, Ny, Nz, D;

            public float AtY;

            // Made or edited by the user; proposals leave its squares alone.
            public bool Mine;

            public float CentreX => (MinX + MaxX) / 2f;
            public float CentreZ => (MinZ + MaxZ) / 2f;
            public float Height => HeightOf(this, CentreX, CentreZ);
            public float SlopeX => Math.Abs(Ny) < 1e-4f ? 0f : -Nx / Ny * SquareSize;
            public float SlopeZ => Math.Abs(Ny) < 1e-4f ? 0f : -Nz / Ny * SquareSize;

            public float HeightAtOrZero(float x, float z) => HeightOf(this, x, z);

            public bool Covers(float x, float z) => x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;

            /// <summary>Sets the plane from the height at the centre and the rise per square along x and z.</summary>
            public void Tilt(float height, float slopeX, float slopeZ)
            {
                float nx = -slopeX / SquareSize, nz = -slopeZ / SquareSize;
                float len = (float)Math.Sqrt(nx * nx + 1f + nz * nz);
                Nx = nx / len; Ny = 1f / len; Nz = nz / len;
                D = -(Nx * CentreX + Ny * height + Nz * CentreZ);
                AtY = height;
            }

            public Piece Copy() => (Piece)MemberwiseClone();
        }

        public static List<Piece> Read(byte[] terrain)
        {
            var plates = new List<Piece>();
            if (terrain == null || !BdhcFile.TryParse(terrain, out var file)) return plates;
            foreach (var p in file.Plates())
            {
                var piece = new Piece { MinX = p.MinX, MaxX = p.MaxX, MinZ = p.MinZ, MaxZ = p.MaxZ, Nx = p.Nx, Ny = p.Ny, Nz = p.Nz, D = p.D };
                piece.AtY = piece.Height;
                plates.Add(piece);
            }
            return plates;
        }

        /// <summary>Plates for a map: the user's own kept as they are, the rest proposed from the model.</summary>
        /// <param name="want">Height in map units a square should be walked at, where the caller knows it; wins over the current terrain.</param>
        public static List<Piece> Propose(MapMesh mesh, byte[,] collisions, byte[] terrainNow, IReadOnlyList<Piece> mine = null,
                                          Func<int, int, bool> changed = null, Func<int, int, float?> want = null)
        {
            // Plates lying only over unchanged squares stay exactly as the map has them.
            var keep = new List<Piece>(mine ?? Array.Empty<Piece>());
            if (changed != null)
                foreach (var p in Read(terrainNow))
                {
                    if (keep.Any(k => Same(k, p))) continue;
                    var under = SquaresUnder(p).ToList();
                    if (!under.Any(s => changed(s.c, s.r))) { keep.Add(p); continue; }
                    // The plate stays over the squares nothing changed, cut into rectangles on its own plane.
                    foreach (var (c, r, w, d) in Rectangles(under.Where(s => !changed(s.c, s.r))))
                    {
                        var part = p.Copy();
                        part.MinX = Math.Max(p.MinX, -HalfMap + c * SquareSize);
                        part.MaxX = Math.Min(p.MaxX, -HalfMap + (c + w) * SquareSize);
                        part.MinZ = Math.Max(p.MinZ, -HalfMap + r * SquareSize);
                        part.MaxZ = Math.Min(p.MaxZ, -HalfMap + (r + d) * SquareSize);
                        keep.Add(part);
                    }
                }
            mine = keep;
            Func<int, int, bool> blocked = collisions == null ? null : (c, r) => (collisions[r, c] & 0x80) != 0;
            Func<int, int, float?> guide = null;
            if (terrainNow != null && BdhcFile.TryParse(terrainNow, out var now))
                guide = (c, r) => now.TryGetHeight((c + 0.5f) * 0.25f, (r + 0.5f) * 0.25f, 0f, out float y) ? y * UnitsPerRaw : null;
            if (want != null)
            {
                var was = guide;
                guide = (c, r) => want(c, r) ?? was?.Invoke(c, r);
            }
            return Squares(PiecesOf(mesh, mesh?.ModelScale ?? 0f), blocked, guide, mine);
        }

        public static List<Piece> PiecesOf(MapMesh mesh, float modelScale, float steepest = 0.5f)
        {
            var pieces = new List<Piece>();
            if (mesh == null) return pieces;

            float toRaw = (modelScale == 0f ? 1f : modelScale) / 64f;

            foreach (var face in mesh.Faces)
            {
                if (face.Corners == null || face.Corners.Length < 3) continue;

                var a = At(mesh, face.Corners[0], toRaw);
                var b = At(mesh, face.Corners[1], toRaw);
                var c = At(mesh, face.Corners[2], toRaw);

                float ux = b.x - a.x, uy = b.y - a.y, uz = b.z - a.z;
                float vx = c.x - a.x, vy = c.y - a.y, vz = c.z - a.z;
                float nx = uy * vz - uz * vy;
                float ny = uz * vx - ux * vz;
                float nz = ux * vy - uy * vx;

                float len = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (len < 1e-6f) continue;
                nx /= len; ny /= len; nz /= len;

                if (ny < 0f) { nx = -nx; ny = -ny; nz = -nz; }
                if (ny < steepest) continue;

                float minX = float.MaxValue, maxX = float.MinValue;
                float minZ = float.MaxValue, maxZ = float.MinValue, lowest = float.MaxValue;
                foreach (int corner in face.Corners)
                {
                    var p = At(mesh, corner, toRaw);
                    minX = Math.Min(minX, p.x); maxX = Math.Max(maxX, p.x);
                    minZ = Math.Min(minZ, p.z); maxZ = Math.Max(maxZ, p.z);
                    lowest = Math.Min(lowest, p.y);
                }

                if (maxX - minX < 1e-4f || maxZ - minZ < 1e-4f) continue;

                pieces.Add(new Piece
                {
                    MinX = minX, MaxX = maxX, MinZ = minZ, MaxZ = maxZ,
                    Nx = nx, Ny = ny, Nz = nz,
                    D = -(nx * a.x + ny * a.y + nz * a.z),
                    AtY = lowest,
                });
            }

            return Merged(pieces);
        }

        private static List<Piece> Merged(List<Piece> pieces)
        {
            var done = new List<Piece>();

            foreach (var group in pieces.GroupBy(p => (Fx(p.Nx), Fx(p.Ny), Fx(p.Nz), Fx(p.D))))
            {
                var here = group.ToList();

                for (int pass = 0; pass < 3; pass++)
                {
                    int was = here.Count;
                    here = Join(here, alongX: pass % 2 == 0);
                    if (here.Count == was && pass > 0) break;
                }

                done.AddRange(here);
            }

            return done;
        }

        private static List<Piece> Join(List<Piece> pieces, bool alongX)
        {
            var order = pieces
                .OrderBy(p => alongX ? Fx(p.MinZ) : Fx(p.MinX))
                .ThenBy(p => alongX ? Fx(p.MaxZ) : Fx(p.MaxX))
                .ThenBy(p => alongX ? Fx(p.MinX) : Fx(p.MinZ))
                .ToList();

            var done = new List<Piece>();
            Piece open = null;

            foreach (var piece in order)
            {
                bool joins = open != null
                    && (alongX
                        ? Fx(open.MinZ) == Fx(piece.MinZ) && Fx(open.MaxZ) == Fx(piece.MaxZ)
                          && Fx(open.MaxX) == Fx(piece.MinX)
                        : Fx(open.MinX) == Fx(piece.MinX) && Fx(open.MaxX) == Fx(piece.MaxX)
                          && Fx(open.MaxZ) == Fx(piece.MinZ));

                if (joins)
                {
                    if (alongX) open.MaxX = piece.MaxX; else open.MaxZ = piece.MaxZ;
                    open.AtY = Math.Min(open.AtY, piece.AtY);
                    continue;
                }

                if (open != null) done.Add(open);
                open = new Piece
                {
                    MinX = piece.MinX, MaxX = piece.MaxX, MinZ = piece.MinZ, MaxZ = piece.MaxZ,
                    Nx = piece.Nx, Ny = piece.Ny, Nz = piece.Nz, D = piece.D, AtY = piece.AtY,
                };
            }

            if (open != null) done.Add(open);
            return done;
        }

        public static byte[] ForMap(MapMesh mesh, byte[,] collisions, byte[] terrainNow, out string whynot,
                                    IReadOnlyList<Piece> mine = null, Func<int, int, bool> changed = null,
                                    Func<int, int, float?> want = null)
            => From(Propose(mesh, collisions, terrainNow, mine, changed, want), out whynot);

        /// <summary>Blocks walkable squares no plate covers, since the game won't let anyone stand there.</summary>
        public static int BlockUngrounded(byte[] terrain, byte[,] collisions)
        {
            if (terrain == null || collisions == null) return 0;
            var plates = Read(terrain);
            int blocked = 0;
            for (int r = 0; r < Across; r++)
                for (int c = 0; c < Across; c++)
                {
                    if ((collisions[r, c] & 0x80) != 0) continue;
                    float x = -HalfMap + (c + 0.5f) * SquareSize, z = -HalfMap + (r + 0.5f) * SquareSize;
                    if (plates.Any(p => p.Covers(x, z))) continue;
                    collisions[r, c] |= 0x80;
                    blocked++;
                }
            return blocked;
        }

        /// <summary>How many of the map's current plates a proposal would drop.</summary>
        public static int Replaced(byte[] terrainNow, IReadOnlyList<Piece> proposal) =>
            Read(terrainNow).Count(p => !proposal.Any(q => Same(p, q)));

        public static bool Same(Piece a, Piece b) =>
            Math.Abs(a.MinX - b.MinX) < 0.01f && Math.Abs(a.MaxX - b.MaxX) < 0.01f && Math.Abs(a.MinZ - b.MinZ) < 0.01f
            && Math.Abs(a.MaxZ - b.MaxZ) < 0.01f && Math.Abs(a.Height - b.Height) < 0.05f
            && Math.Abs(a.SlopeX - b.SlopeX) < 0.05f && Math.Abs(a.SlopeZ - b.SlopeZ) < 0.05f;

        /// <summary>Covers a set of squares with as few rectangles as a greedy sweep finds.</summary>
        private static List<(int c, int r, int w, int d)> Rectangles(IEnumerable<(int c, int r)> squares)
        {
            var left = new HashSet<(int c, int r)>(squares);
            var found = new List<(int, int, int, int)>();
            for (int r = 0; r < Across; r++)
                for (int c = 0; c < Across; c++)
                {
                    if (!left.Contains((c, r))) continue;
                    int w = 1;
                    while (left.Contains((c + w, r))) w++;
                    int d = 1;
                    while (Enumerable.Range(c, w).All(x => left.Contains((x, r + d)))) d++;
                    for (int y = r; y < r + d; y++)
                        for (int x = c; x < c + w; x++) left.Remove((x, y));
                    found.Add((c, r, w, d));
                }
            return found;
        }

        public static IEnumerable<(int c, int r)> SquaresUnder(Piece p)
        {
            for (int r = 0; r < Across; r++)
                for (int c = 0; c < Across; c++)
                    if (p.Covers(-HalfMap + (c + 0.5f) * SquareSize, -HalfMap + (r + 0.5f) * SquareSize)) yield return (c, r);
        }

        private const int Across = 32;
        private const float SquareSize = 16f, HalfMap = 256f;

        /// <summary>Plates the way hand-made terrain is laid: square-aligned rectangles, one ground per square.</summary>
        /// <param name="guide">Height the square is walked at now; its nearest surface becomes the ground.</param>
        public static List<Piece> Squares(List<Piece> pieces, Func<int, int, bool> blocked = null,
                                          Func<int, int, float?> guide = null, IReadOnlyList<Piece> mine = null)
        {
            blocked ??= (_, _) => false;
            mine ??= Array.Empty<Piece>();
            bool Kept(int c, int r)
            {
                float x = -HalfMap + (c + 0.5f) * SquareSize, z = -HalfMap + (r + 0.5f) * SquareSize;
                return mine.Any(p => p.Covers(x, z));
            }
            var ground = new Piece[Across, Across];
            var groundY = new float[Across, Across];
            var upper = new Dictionary<Piece, List<(int c, int r)>>();

            for (int r = 0; r < Across; r++)
                for (int c = 0; c < Across; c++)
                {
                    float x = -HalfMap + (c + 0.5f) * SquareSize, z = -HalfMap + (r + 0.5f) * SquareSize;
                    if (Kept(c, r)) continue;
                    var here = pieces.Where(p => x >= p.MinX && x <= p.MaxX && z >= p.MinZ && z <= p.MaxZ)
                                     .Select(p => (p, y: HeightOf(p, x, z)))
                                     .OrderBy(h => h.y).ToList();
                    if (here.Count == 0) continue;

                    var chosen = guide?.Invoke(c, r) is float want
                        ? here.OrderBy(h => Math.Abs(h.y - want)).First() : here[0];
                    ground[c, r] = chosen.p;
                    groundY[c, r] = chosen.y;
                    if (blocked(c, r)) continue;

                    float kept = chosen.y;
                    foreach (var (p, y) in here.Where(h => h.y > chosen.y))
                    {
                        if (p.Ny < 0.99f || y - kept < SquareSize) continue;
                        if (!upper.TryGetValue(p, out var at)) upper[p] = at = new List<(int, int)>();
                        at.Add((c, r));
                        kept = y;
                    }
                }

            var done = new List<Piece>();
            var taken = new bool[Across, Across];
            for (int r = 0; r < Across; r++)
                for (int c = 0; c < Across; c++) taken[c, r] = Kept(c, r);
            // Walkable squares seed plates first; blocked squares join any plate that passes close to their own ground.
            foreach (bool seedBlocked in new[] { false, true })
                for (int r = 0; r < Across; r++)
                    for (int c = 0; c < Across; c++)
                    {
                        if (taken[c, r] || ground[c, r] == null || blocked(c, r) != seedBlocked) continue;
                        var seed = ground[c, r];
                        var plane = Key(seed);
                        bool Fits(int cc, int rr)
                        {
                            if (taken[cc, rr]) return false;
                            if (ground[cc, rr] != null && Key(ground[cc, rr]) == plane) return true;
                            if (!blocked(cc, rr)) return false;
                            if (ground[cc, rr] == null) return true;
                            float x = -HalfMap + (cc + 0.5f) * SquareSize, z = -HalfMap + (rr + 0.5f) * SquareSize;
                            return Math.Abs(HeightOf(seed, x, z) - groundY[cc, rr]) < 2f;
                        }

                        int run = 1;
                        while (c + run < Across && Fits(c + run, r)) run++;
                        int w = 1, d = 1, depth = Across - r;
                        for (int k = 1; k <= run; k++)
                        {
                            int dk = 1;
                            while (dk < depth && r + dk < Across && Fits(c + k - 1, r + dk)) dk++;
                            depth = dk;
                            if (k * dk > w * d) { w = k; d = dk; }
                        }

                        for (int rr = r; rr < r + d; rr++)
                            for (int cc = c; cc < c + w; cc++) taken[cc, rr] = true;
                        done.Add(Over(seed, c, r, w, d));
                    }

            done = Merged(done);

            var levels = new List<Piece>();
            foreach (var (p, at) in upper)
            {
                if (at.Count < 3) continue;
                levels.AddRange(at.Select(s => Over(p, s.c, s.r, 1, 1)));
            }
            done.AddRange(Merged(levels));
            done.AddRange(mine.Select(p => p.Copy()));
            return done;
        }

        private static float HeightOf(Piece p, float x, float z)
            => Math.Abs(p.Ny) < 1e-4f ? p.AtY : -(p.Nx * x + p.Nz * z + p.D) / p.Ny;

        private static (long, long, long, long) Key(Piece p)
            => p == null ? (0, 0, 0, long.MinValue)
                         : ((long)Math.Round(p.Nx * 1024), (long)Math.Round(p.Ny * 1024),
                            (long)Math.Round(p.Nz * 1024), (long)Math.Round(p.D * 16));

        private static Piece Over(Piece plane, int c, int r, int w, int d) => new Piece
        {
            MinX = -HalfMap + c * SquareSize, MaxX = -HalfMap + (c + w) * SquareSize,
            MinZ = -HalfMap + r * SquareSize, MaxZ = -HalfMap + (r + d) * SquareSize,
            Nx = plane.Nx, Ny = plane.Ny, Nz = plane.Nz, D = plane.D, AtY = plane.AtY,
        };

        private static (float x, float y, float z) At(MapMesh mesh, int corner, float toRaw)
        {
            var v = mesh.Vertices[corner];
            return (v.X * toRaw * UnitsPerRaw, v.Y * toRaw * UnitsPerRaw, v.Z * toRaw * UnitsPerRaw);
        }

        /// <summary>Terrain in the loaded game's own layout: Diamond and Pearl walk on triangles, later games on plates.</summary>
        public static byte[] From(IReadOnlyList<Piece> pieces, out string whynot)
            => RomInfo.gameFamily == RomInfo.GameFamilies.DP ? Triangles(pieces, out whynot) : Plates(pieces, out whynot);

        /// <summary>Platinum and HeartGold/SoulSilver terrain: plates, each a rectangle on its own plane, found by Z strips.</summary>
        public static byte[] Plates(IReadOnlyList<Piece> pieces, out string whynot)
        {
            whynot = null;
            if (pieces == null || pieces.Count == 0)
            { whynot = "No walkable faces."; return null; }

            var points = new Shared<(int x, int z)>();
            var normals = new Shared<(int x, int y, int z)>();
            var constants = new Shared<int>();

            var plates = new List<(ushort a, ushort b, ushort n, ushort d)>();
            foreach (var piece in pieces)
            {
                ushort a = (ushort)points.Of((Fx(piece.MinX), Fx(piece.MinZ)));
                ushort b = (ushort)points.Of((Fx(piece.MaxX), Fx(piece.MaxZ)));
                ushort n = (ushort)normals.Of((Fx(piece.Nx), Fx(piece.Ny), Fx(piece.Nz)));
                ushort d = (ushort)constants.Of(Fx(piece.D));
                plates.Add((a, b, n, d));
            }

            const int Bands = 32;

            float lowZ = pieces.Min(p => p.MinZ), highZ = pieces.Max(p => p.MaxZ);
            if (highZ <= lowZ) highZ = lowZ + 1f;
            float step = (highZ - lowZ) / Bands;

            var strips = new List<(int scanline, ushort count, ushort start)>();
            var access = new List<ushort>();

            for (int i = 0; i < Bands; i++)
            {
                float from = i == 0 ? float.MinValue : lowZ + step * i;
                float to = i == Bands - 1 ? float.MaxValue : lowZ + step * (i + 1);

                int start = access.Count;
                for (int p = 0; p < pieces.Count; p++)
                {
                    var piece = pieces[p];
                    if (piece.MaxZ < from || piece.MinZ > to) continue;
                    access.Add((ushort)p);
                }

                float line = i == Bands - 1 ? highZ + step : lowZ + step * (i + 1);
                strips.Add((Fx(line), (ushort)(access.Count - start), (ushort)start));
            }

            if (points.Count > ushort.MaxValue || normals.Count > ushort.MaxValue
                || constants.Count > ushort.MaxValue || plates.Count > ushort.MaxValue
                || strips.Count > ushort.MaxValue || access.Count > ushort.MaxValue)
            {
                whynot = $"Too many plates ({plates.Count}) or strips ({strips.Count}) for a BDHC file.";
                return null;
            }

            using var stream = new MemoryStream();
            using var write = new BinaryWriter(stream);
            write.Write(new[] { (byte)'B', (byte)'D', (byte)'H', (byte)'C' });
            write.Write((ushort)points.Count);
            write.Write((ushort)normals.Count);
            write.Write((ushort)constants.Count);
            write.Write((ushort)plates.Count);
            write.Write((ushort)strips.Count);
            write.Write((ushort)access.Count);

            foreach (var (x, z) in points.InOrder) { write.Write(x); write.Write(z); }
            foreach (var (x, y, z) in normals.InOrder) { write.Write(x); write.Write(y); write.Write(z); }
            foreach (int c in constants.InOrder) write.Write(c);
            foreach (var (a, b, n, d) in plates) { write.Write(a); write.Write(b); write.Write(n); write.Write(d); }
            foreach (var (scanline, count, start) in strips)
            { write.Write(scanline); write.Write(count); write.Write(start); }
            foreach (ushort a in access) write.Write(a);

            write.Flush();
            return stream.ToArray();
        }

        private static int Fx(float v) => (int)Math.Round(v * Fx32One);

        // What the field keeps for one map's terrain, less the 40-byte header it reads separately.
        private const int DpTerrainRoom = 0x9000 - 0x10;

        /// <summary>
        /// Diamond and Pearl terrain: each plate becomes two triangles on its plane. The map is split into an N by N grid;
        /// each cell holds Z bands sorted for the game's binary search, each band listing the triangles crossing it.
        /// </summary>
        public static byte[] Triangles(IReadOnlyList<Piece> pieces, out string whynot)
        {
            whynot = null;
            if (pieces == null || pieces.Count == 0)
            { whynot = "No walkable faces."; return null; }

            var vertices = new Shared<(int x, int y, int z)>();
            var normals = new Shared<(int x, int y, int z)>();
            var triangles = new List<(ushort a, ushort b, ushort c, ushort n, int d, float x0, float x1, float z0, float z1)>();
            foreach (var p in pieces)
            {
                (int, int, int) At(float x, float z) => (Fx(x), Fx(HeightOf(p, x, z)), Fx(z));
                var corners = new[] { At(p.MinX, p.MaxZ), At(p.MaxX, p.MaxZ), At(p.MaxX, p.MinZ), At(p.MinX, p.MinZ) };
                ushort n = (ushort)normals.Of((Fx(p.Nx), Fx(p.Ny), Fx(p.Nz)));
                int d = Fx(p.D);
                // The same turn as the game's own triangles when seen from above.
                foreach (var (i, j, k) in new[] { (0, 1, 2), (0, 2, 3) })
                    triangles.Add(((ushort)vertices.Of(corners[i]), (ushort)vertices.Of(corners[j]), (ushort)vertices.Of(corners[k]),
                                   n, d, p.MinX, p.MaxX, p.MinZ, p.MaxZ));
            }

            // Grid sizes and bounds as the game's own maps have them.
            int cells = triangles.Count <= 24 ? 1 : triangles.Count <= 160 ? 4 : 8;
            float start = -257f, end = 256f + cells / 2f, size = (end - start) / cells;
            var grid = new List<(ushort lines, ushort first)>();
            var lines = new List<(ushort count, int z, int offset)>();
            var ids = new List<ushort>();
            int mostLines = 0, mostInLine = 0;
            for (int gz = 0; gz < cells; gz++)
                for (int gx = 0; gx < cells; gx++)
                {
                    float cx0 = start + gx * size, cx1 = cx0 + size, cz0 = start + gz * size, cz1 = cz0 + size;
                    // A point clamped into an edge cell can lie beyond it, so edge cells reach to the map's end.
                    if (gx == 0) cx0 = float.MinValue; if (gx == cells - 1) cx1 = float.MaxValue;
                    if (gz == 0) cz0 = float.MinValue; if (gz == cells - 1) cz1 = float.MaxValue;
                    var here = Enumerable.Range(0, triangles.Count)
                        .Where(t => triangles[t].x1 >= cx0 && triangles[t].x0 <= cx1 && triangles[t].z1 >= cz0 && triangles[t].z0 <= cz1)
                        .ToList();

                    // Band edges: every triangle's Z bounds inside the cell, so each band is crossed cleanly.
                    float lo = Math.Max(start + gz * size, -256f), hi = Math.Min(start + (gz + 1) * size, 256f);
                    var edges = here.SelectMany(t => new[] { triangles[t].z0, triangles[t].z1 })
                                    .Where(z => z > lo + 1e-3f && z < hi - 1e-3f).Select(z => (float)Math.Round(z, 3))
                                    .Distinct().OrderBy(z => z).ToList();
                    edges.Add(hi);
                    int first = lines.Count;
                    float from = float.MinValue;
                    for (int e = 0; e < edges.Count; e++)
                    {
                        // The game takes the first band whose edge lies past the point; the last reaches past the map.
                        float to = e == edges.Count - 1 ? float.MaxValue : edges[e];
                        var inBand = here.Where(t => triangles[t].z1 >= from && triangles[t].z0 <= to).ToList();
                        lines.Add(((ushort)inBand.Count, e == edges.Count - 1 ? int.MaxValue : Fx(edges[e]), ids.Count));
                        foreach (int t in inBand) ids.Add((ushort)t);
                        mostInLine = Math.Max(mostInLine, inBand.Count);
                        from = to;
                    }
                    grid.Add(((ushort)(lines.Count - first), (ushort)first));
                    mostLines = Math.Max(mostLines, lines.Count - first);
                }

            if (vertices.Count > ushort.MaxValue || triangles.Count > ushort.MaxValue || lines.Count > ushort.MaxValue || ids.Count > ushort.MaxValue)
            { whynot = $"Too many triangles ({triangles.Count}) for Diamond and Pearl terrain."; return null; }

            using var stream = new MemoryStream();
            using var write = new BinaryWriter(stream);
            write.Write(new[] { (byte)'B', (byte)'D', (byte)'H', (byte)'C' });
            write.Write(32);
            write.Write((ushort)12); write.Write((ushort)vertices.Count);
            write.Write((ushort)12); write.Write((ushort)normals.Count);
            write.Write((ushort)12); write.Write((ushort)triangles.Count);
            write.Write((ushort)28); write.Write((ushort)2); write.Write((ushort)grid.Count);
            write.Write((ushort)10); write.Write((ushort)mostLines);
            write.Write((ushort)mostInLine);
            write.Write(lines.Count * 10);
            write.Write(ids.Count * 2);

            write.Write((ushort)cells); write.Write((ushort)cells);
            write.Write(Fx(start)); write.Write(Fx(start)); write.Write(Fx(end)); write.Write(Fx(end));
            write.Write(Fx(size)); write.Write(Fx(size));
            foreach (var (x, y, z) in vertices.InOrder) { write.Write(x); write.Write(y); write.Write(z); }
            foreach (var (x, y, z) in normals.InOrder) { write.Write(x); write.Write(y); write.Write(z); }
            foreach (var t in triangles) { write.Write(t.a); write.Write(t.b); write.Write(t.c); write.Write(t.n); write.Write(t.d); }
            foreach (var (count, first) in grid) { write.Write(count); write.Write(first); }
            foreach (var (count, z, offset) in lines) { write.Write(count); write.Write(z); write.Write(offset); }
            foreach (ushort id in ids) write.Write(id);
            write.Flush();

            if (stream.Length - 40 > DpTerrainRoom)
            { whynot = $"Terrain of {stream.Length:n0} bytes is more than Diamond and Pearl keep for a map ({DpTerrainRoom + 40:n0})."; return null; }
            return stream.ToArray();
        }

        /// <summary>
        /// Reads Diamond and Pearl terrain back as plates: triangle pairs on one plane become the rectangle they cover,
        /// a lone triangle the rectangle around it.
        /// </summary>
        public static List<Piece> FromTriangles(byte[] data)
        {
            var pieces = new List<Piece>();
            using var read = new BinaryReader(new MemoryStream(data));
            read.BaseStream.Position = 8;
            var counts = Enumerable.Range(0, 12).Select(_ => read.ReadUInt16()).ToArray();
            int vn = counts[1], nn = counts[3], pn = counts[5];
            read.BaseStream.Position = 40 + 28;
            var v = Enumerable.Range(0, vn).Select(_ => (x: read.ReadInt32() / Fx32One, y: read.ReadInt32() / Fx32One, z: read.ReadInt32() / Fx32One)).ToArray();
            var n = Enumerable.Range(0, nn).Select(_ => (x: read.ReadInt32() / Fx32One, y: read.ReadInt32() / Fx32One, z: read.ReadInt32() / Fx32One)).ToArray();
            var t = Enumerable.Range(0, pn).Select(_ => (a: read.ReadUInt16(), b: read.ReadUInt16(), c: read.ReadUInt16(), n: read.ReadUInt16(), d: read.ReadInt32() / Fx32One)).ToArray();
            for (int i = 0; i < t.Length; i++)
            {
                var corners = new List<(float x, float y, float z)> { v[t[i].a], v[t[i].b], v[t[i].c] };
                if (i + 1 < t.Length && t[i + 1].n == t[i].n && Math.Abs(t[i + 1].d - t[i].d) < 1e-4f)
                {
                    corners.AddRange(new[] { v[t[i + 1].a], v[t[i + 1].b], v[t[i + 1].c] });
                    i++;
                }
                var nrm = n[t[i].n];
                var piece = new Piece
                {
                    MinX = corners.Min(c => c.x), MaxX = corners.Max(c => c.x), MinZ = corners.Min(c => c.z), MaxZ = corners.Max(c => c.z),
                    Nx = nrm.x, Ny = nrm.y, Nz = nrm.z, D = t[i].d,
                };
                piece.AtY = piece.Height;
                pieces.Add(piece);
            }
            return pieces;
        }

        /// <summary>Whether terrain bytes are in the Diamond and Pearl layout.</summary>
        public static bool IsTriangles(byte[] data)
            => data != null && data.Length >= 68 && BitConverter.ToUInt32(data, 4) == 32
               && BitConverter.ToUInt16(data, 8) == 12 && BitConverter.ToUInt16(data, 12) == 12 && BitConverter.ToUInt16(data, 16) == 12;

        private sealed class Shared<T>
        {
            private readonly Dictionary<T, int> _where = new Dictionary<T, int>();
            private readonly List<T> _order = new List<T>();

            public int Count => _order.Count;
            public IEnumerable<T> InOrder => _order;

            public int Of(T what)
            {
                if (_where.TryGetValue(what, out int at)) return at;
                at = _order.Count;
                _order.Add(what);
                _where[what] = at;
                return at;
            }
        }
    }
}
