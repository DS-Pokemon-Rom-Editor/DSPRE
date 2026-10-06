using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.Models
{
    /// <summary>Slopes and stairs laid over marked squares, each rising from its own ground to its higher neighbours.</summary>
    public static class TileRamps
    {
        public enum Kind { Slope, Stairs }

        public sealed class Run
        {
            public HashSet<(int x, int z)> Squares = new HashSet<(int x, int z)>();
            public Kind Kind = Kind.Slope;
            /// <summary>The face whose picture and look cover the run; null uses each square's own ground.</summary>
            public MapTileset.Face Fill;
            /// <summary>A slope tile whose own faces are laid over the run, turned uphill and fitted to it.</summary>
            public MapTileset.Tile Template;
        }

        public sealed class Report
        {
            public int Made;
            public List<string> Skipped = new List<string>();
            public List<BdhcBuild.Piece> Plates = new List<BdhcBuild.Piece>();
        }

        // Model units to terrain units: a square is 0.25 in the tileset and 16 in the terrain.
        private const float ToTerrain = 64f;
        // Steeper than this the game will not walk up a plate.
        private const float SteepestRise = 1.7f;

        private enum Shape { Straight, Inner, Outer }

        public static Report Lay(TileBake.Result r, TileGrid grid, IReadOnlyList<Run> runs,
                                 Func<int, int, (float height, MapTileset.Face skin)> groundOf)
        {
            Report report = new Report();
            if (r == null || grid == null || runs == null) return report;
            int n = TileGrid.Across;
            float tw = MapTileset.TileWidth, half = MapTileset.HalfMap;
            HashSet<(int, int)> anyRamp = new HashSet<(int, int)>(runs.SelectMany(run => run.Squares));

            foreach (Run run in runs)
            {
                if (run.Squares.Count == 0) continue;
                int bx0 = run.Squares.Min(s => s.x), bx1 = run.Squares.Max(s => s.x) + 1;
                int bz0 = run.Squares.Min(s => s.z), bz1 = run.Squares.Max(s => s.z) + 1;
                string where = $"ramp at {bx0},{bz0}";

                float lo = run.Squares.Min(s => groundOf(s.x, s.z).height);
                float step = TileGrid.Step / 2f;

                // The highest ground just outside the run on each side.
                Dictionary<(int dx, int dz), float> high = new Dictionary<(int dx, int dz), float>();
                foreach ((int x, int z) in run.Squares)
                    foreach ((int, int) d in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        int nx = x + d.Item1, nz = z + d.Item2;
                        if (nx < 0 || nz < 0 || nx >= n || nz >= n || anyRamp.Contains((nx, nz))) continue;
                        (float h, MapTileset.Face skin) = groundOf(nx, nz);
                        if (skin == null || h < lo + step) continue;
                        high[d] = high.TryGetValue(d, out float had) ? Math.Max(had, h) : h;
                    }

                Shape shape;
                (int dx, int dz) a = (0, 0), b = (0, 0);
                if (high.Count == 0)
                {
                    // Only a diagonal rises: the outer corner of a block.
                    (int, int)? diagonal = null;
                    float best = lo + step;
                    foreach ((int cx, int cz, int dx, int dz) in new[] { (bx1, bz0 - 1, 1, -1), (bx0 - 1, bz0 - 1, -1, -1), (bx1, bz1, 1, 1), (bx0 - 1, bz1, -1, 1) })
                    {
                        if (cx < 0 || cz < 0 || cx >= n || cz >= n || anyRamp.Contains((cx, cz))) continue;
                        (float h, MapTileset.Face skin) = groundOf(cx, cz);
                        if (skin != null && h >= best) { best = h; diagonal = (dx, dz); }
                    }
                    if (diagonal is not (int ddx, int ddz)) { report.Skipped.Add($"{where}: no higher ground beside it"); continue; }
                    shape = Shape.Outer;
                    a = (ddx, 0); b = (0, ddz);
                    high[(ddx, ddz)] = best;
                }
                else
                {
                    List<(int dx, int dz)> sides = high.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).ToList();
                    a = sides[0];
                    (int, int)? across = sides.Skip(1).Cast<(int, int)?>().FirstOrDefault(s => s.Value.Item1 != 0 && a.dx == 0 || s.Value.Item2 != 0 && a.dz == 0);
                    if (across is (int, int) other) { shape = Shape.Inner; b = other; }
                    else shape = Shape.Straight;
                }
                float hi = high.Values.Max();

                // Where a vertex lies along a direction, 0 at the low edge of the run and 1 at the high edge.
                float Along((int dx, int dz) d, float vx, float vz)
                    => d.dx > 0 ? (vx - bx0) / (bx1 - bx0) : d.dx < 0 ? (bx1 - vx) / (bx1 - bx0)
                     : d.dz > 0 ? (vz - bz0) / (bz1 - bz0) : (bz1 - vz) / (bz1 - bz0);
                float Height(float vx, float vz)
                {
                    float f = shape switch
                    {
                        Shape.Straight => Along(a, vx, vz),
                        Shape.Inner => Math.Max(Along(a, vx, vz), Along(b, vx, vz)),
                        _ => Math.Min(Along(a, vx, vz), Along(b, vx, vz)),
                    };
                    return lo + (hi - lo) * Math.Clamp(f, 0f, 1f);
                }

                // Rise per square along the steepest way up.
                float runLength = shape == Shape.Straight ? (a.dx != 0 ? bx1 - bx0 : bz1 - bz0) : Math.Min(bx1 - bx0, bz1 - bz0);
                float rise = (hi - lo) / tw / runLength;
                if (rise > SteepestRise)
                {
                    int needed = (int)Math.Ceiling((hi - lo) / tw / SteepestRise);
                    report.Skipped.Add($"{where}: too steep to walk; make it {needed} squares long");
                    continue;
                }

                bool fromTile = run.Template != null && shape == Shape.Straight;
                bool stairs = !fromTile && run.Kind == Kind.Stairs && shape == Shape.Straight;
                if (fromTile) Lay(r, grid, run.Template, a, bx0, bx1, bz0, bz1, lo, hi);
                // A tile brings its own sides when it has upright faces; otherwise the sides are closed as usual.
                bool ownSides = fromTile && run.Template.Faces.Any(f => f.Corners?.Length >= 3
                    && Math.Abs(NormalOf(f.Corners.Select(i => (run.Template.Corners[i].X, run.Template.Corners[i].Y, run.Template.Corners[i].Z)).ToArray()).y) < 0.2f);
                bool terraces = !fromTile && run.Kind == Kind.Stairs && shape != Shape.Straight;
                int steps = stairs || terraces ? Math.Max(2, (int)Math.Round((hi - lo) / (tw / 4f))) : 0;
                if (terraces)
                    Terraces(r, grid, run.Squares, bx0, bx1, bz0, bz1, steps, lo, hi,
                        (fa, fb) => shape == Shape.Inner ? Math.Max(fa, fb) : Math.Min(fa, fb),
                        (vx, vz) => (Along(a, vx, vz), Along(b, vx, vz)),
                        (x, z) => run.Fill ?? groundOf(x, z).skin,
                        (x, z) => x < 0 || z < 0 || x >= n || z >= n || groundOf(x, z).skin == null ? lo : groundOf(x, z).height);
                foreach ((int x, int z) in terraces ? Enumerable.Empty<(int, int)>() : run.Squares)
                {
                    MapTileset.Face skin = run.Fill ?? run.Template?.MainFace ?? groundOf(x, z).skin;
                    if (skin == null) continue;
                    if (fromTile) { }
                    else if (stairs) Stairs(r, grid, skin, x, z, a, bx0, bx1, bz0, bz1, lo, hi, steps);
                    else if (shape == Shape.Straight) Quad(r, grid, skin, x, z, x, z, x + 1, z + 1, Height);
                    else
                        for (int qz = 0; qz < 2; qz++)
                            for (int qx = 0; qx < 2; qx++)
                                Quad(r, grid, skin, x, z, x + qx * 0.5f, z + qz * 0.5f, x + (qx + 1) * 0.5f, z + (qz + 1) * 0.5f, Height);

                    // Where the ramp stands above the ground beside it, a side face closes the gap down to that ground.
                    foreach ((int dx, int dz) in ownSides ? Array.Empty<(int, int)>() : new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        int nx = x + dx, nz = z + dz;
                        if (anyRamp.Contains((nx, nz))) continue;
                        float beside = nx < 0 || nz < 0 || nx >= n || nz >= n || groundOf(nx, nz).skin == null ? lo : groundOf(nx, nz).height;
                        (float x, float z) p0 = dx > 0 ? (x + 1, z) : dx < 0 ? (x, z) : dz > 0 ? (x, z + 1) : (x, z);
                        (float x, float z) p1 = dx > 0 ? (x + 1, z + 1) : dx < 0 ? (x, z + 1) : dz > 0 ? (x + 1, z + 1) : (x + 1, z);
                        bool side = stairs && ((a.dx != 0) == (dx == 0));
                        if (side)
                        {
                            // Stairs: one face per step, flat-topped at that step's height.
                            bool alongX = a.dx != 0;
                            float start = alongX ? bx0 : bz0, length = alongX ? bx1 - bx0 : bz1 - bz0;
                            bool rising = alongX ? a.dx > 0 : a.dz > 0;
                            float from = alongX ? p0.x : p0.z, to = alongX ? p1.x : p1.z;
                            for (int i = 0; i < steps; i++)
                            {
                                float t0 = length * i / steps, t1 = length * (i + 1) / steps;
                                float c0 = rising ? start + t0 : start + length - t1, c1 = rising ? start + t1 : start + length - t0;
                                float e0 = Math.Max(c0, from), e1 = Math.Min(c1, to);
                                float top = lo + (hi - lo) * (i + 1) / steps;
                                if (e1 <= e0 + 1e-4f || top <= beside + 1e-4f) continue;
                                (float, float) q0 = alongX ? (e0, p0.z) : (p0.x, e0);
                                (float, float) q1 = alongX ? (e1, p0.z) : (p0.x, e1);
                                Skirt(r, grid, skin, q0, q1, beside, top, top);
                            }
                        }
                        else
                        {
                            float h0 = stairs ? StairTop(p0) : Height(p0.x, p0.z), h1 = stairs ? StairTop(p1) : Height(p1.x, p1.z);
                            if (fromTile && (dx != 0) == (a.dx != 0)) continue;
                            if (Math.Max(h0, h1) > beside + 1e-4f) Skirt(r, grid, skin, p0, p1, beside, Math.Max(h0, beside), Math.Max(h1, beside));
                        }
                    }
                }

                // The height of the stair a point stands on, for the ends of a flight.
                float StairTop((float x, float z) p)
                {
                    float f = Math.Clamp(Along(a, p.x, p.z), 0f, 1f);
                    int i = Math.Min(steps - 1, (int)Math.Floor(f * steps + 1e-4f));
                    return f <= 1e-4f ? lo : lo + (hi - lo) * (i + 1) / steps;
                }

                // The ground the game walks: one plate over a straight run, quarters over a corner.
                BdhcBuild.Piece Plate(float x0, float z0, float x1, float z1)
                {
                    BdhcBuild.Piece p = new BdhcBuild.Piece
                    {
                        MinX = (-half + x0 * tw) * ToTerrain, MaxX = (-half + x1 * tw) * ToTerrain,
                        MinZ = (-half + z0 * tw) * ToTerrain, MaxZ = (-half + z1 * tw) * ToTerrain,
                    };
                    float wide = x1 - x0, deep = z1 - z0, mx = (x0 + x1) / 2, mz = (z0 + z1) / 2;
                    float centre = (grid.BaseLift + Height(mx, mz)) * ToTerrain;
                    float slopeX = (Height(x1, mz) - Height(x0, mz)) / wide * ToTerrain;
                    float slopeZ = (Height(mx, z1) - Height(mx, z0)) / deep * ToTerrain;
                    p.Tilt(centre, slopeX, slopeZ);
                    return p;
                }
                if (shape == Shape.Straight) report.Plates.Add(Plate(bx0, bz0, bx1, bz1));
                else
                    foreach ((int x, int z) in run.Squares)
                        for (int qz = 0; qz < 2; qz++)
                            for (int qx = 0; qx < 2; qx++)
                                report.Plates.Add(Plate(x + qx * 0.5f, z + qz * 0.5f, x + (qx + 1) * 0.5f, z + (qz + 1) * 0.5f));
                report.Made++;
            }
            return report;
        }

        private static (float x, float y, float z) NormalOf((float x, float y, float z)[] p)
        {
            float ux = p[1].x - p[0].x, uy = p[1].y - p[0].y, uz = p[1].z - p[0].z;
            float vx = p[2].x - p[0].x, vy = p[2].y - p[0].y, vz = p[2].z - p[0].z;
            float nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
            float len = MathF.Sqrt(nx * nx + ny * ny + nz * nz);
            return len < 1e-6f ? (0f, 1f, 0f) : (nx / len, ny / len, nz / len);
        }

        /// <summary>Turns, stretches and raises a slope tile to fit a straight run.</summary>
        private static void Lay(TileBake.Result r, TileGrid grid, MapTileset.Tile t, (int dx, int dz) up,
                                int bx0, int bx1, int bz0, int bz1, float lo, float hi)
        {
            float tw = MapTileset.TileWidth;
            float W = t.Wide, D = t.Deep;

            // Uphill comes from the leaning faces' normals; corner heights mislead since stair side walls stand above the top step.
            float downX = 0, downZ = 0, slopeLow = float.MaxValue, slopeHigh = float.MinValue;
            foreach (MapTileset.Face face in t.Faces)
            {
                if (face.Corners == null || face.Corners.Length < 3) continue;
                (float X, float Y, float Z)[] p = face.Corners.Select(i => (t.Corners[i].X, t.Corners[i].Y, t.Corners[i].Z)).ToArray();
                (float nx, float ny, float nz) = NormalOf(p);
                if (ny < 0) { nx = -nx; ny = -ny; nz = -nz; }
                if (ny < 0.3f || ny > 0.97f) continue;
                float area = 0;
                for (int i = 0; i < p.Length; i++) { (float X, float Y, float Z) q = p[(i + 1) % p.Length]; area += p[i].Item1 * q.Item3 - q.Item1 * p[i].Item3; }
                area = Math.Abs(area) / 2f;
                downX += area * nx; downZ += area * nz;
                slopeLow = Math.Min(slopeLow, p.Min(c => c.Item2)); slopeHigh = Math.Max(slopeHigh, p.Max(c => c.Item2));
            }
            (int dx, int dz) tu = Math.Abs(downX) >= Math.Abs(downZ) ? (dx: downX > 0 ? -1 : 1, dz: 0) : (dx: 0, dz: downZ > 0 ? -1 : 1);
            (int x, int z) U = (tu.dx, tu.dz), A = (-U.z, U.x);
            float tLength = U.x != 0 ? W : D, tWidth = U.x != 0 ? D : W;
            float Up(float px, float pz) => U.x > 0 ? px : U.x < 0 ? W - px : U.z > 0 ? pz : D - pz;
            float Across(float px, float pz) => A.x > 0 ? px : A.x < 0 ? W - px : A.z > 0 ? pz : D - pz;

            (int x, int z) RA = (-up.dz, up.dx);
            float rLength = up.dx != 0 ? bx1 - bx0 : bz1 - bz0, rWidth = up.dx != 0 ? bz1 - bz0 : bx1 - bx0;
            (float x, float z) Place(float along, float across)
            {
                float x = up.dx > 0 ? bx0 + along : up.dx < 0 ? bx1 - along : RA.x > 0 ? bx0 + across : bx1 - across;
                float z = up.dz > 0 ? bz0 + along : up.dz < 0 ? bz1 - along : RA.z > 0 ? bz0 + across : bz1 - across;
                return (x, z);
            }
            // Half a square at each end keeps its size, so a stair's side walls stay walls.
            float margin = Math.Min(0.5f, Math.Min(tWidth, rWidth) / 2f);
            float Fit(float across)
            {
                if (across <= margin) return across;
                if (across >= tWidth - margin) return rWidth - (tWidth - across);
                float middle = tWidth - 2 * margin;
                return middle < 1e-4f ? across : margin + (across - margin) * (rWidth - 2 * margin) / middle;
            }
            // The sloped faces run from the low ground to the high; anything standing proud of them keeps its share.
            float yLow = slopeLow < slopeHigh ? slopeLow : t.Corners.Min(c => c.Y);
            float yHigh = slopeLow < slopeHigh ? slopeHigh : t.Corners.Max(c => c.Y);
            float Rise(float y) => yHigh - yLow < 1e-5f ? lo : lo + (y - yLow) / (yHigh - yLow) * (hi - lo);

            foreach (MapTileset.Face face in t.Faces)
            {
                if (face.Corners == null || face.Corners.Length < 3) continue;
                MapTileset.Corner[] at = face.Corners.Select(i => t.Corners[i]).ToArray();
                float[] along = at.Select(c => Up(c.X / tw, c.Z / tw)).ToArray();
                float[] across = at.Select(c => Across(c.X / tw, c.Z / tw)).ToArray();
                float[] fitted = across.Select(Fit).ToArray();

                // Picture units per square across the face, kept when the picture repeats across.
                int param = face.Look?.ImageParam ?? 0;
                bool repeatS = (param & (1 << 16)) != 0, repeatT = (param & (1 << 17)) != 0;
                (float gs, float gt) = AcrossGradient(across, along, at.Select(c => (c.S, c.T)).ToArray());
                (float, float)[] uv = at.Select((c, i) => (repeatS ? c.S + gs * (fitted[i] - across[i]) : c.S,
                                               repeatT ? c.T + gt * (fitted[i] - across[i]) : c.T)).ToArray();
                (float x, float y, float z)[] points = at.Select((c, i) =>
                {
                    (float x, float z) = Place(along[i] * rLength / tLength, fitted[i]);
                    return Point(grid, x, Rise(c.Y), z);
                }).ToArray();
                Add(r, face, points, uv, bothSides: false);
            }
        }

        // How a face's picture coordinates change per square across, fitted over its corners.
        private static (float s, float t) AcrossGradient(float[] across, float[] along, (float s, float t)[] uv)
        {
            int n = across.Length;
            if (across.Max() - across.Min() < 1e-3f) return (0f, 0f);
            // Least squares for value = k + g*across + h*along.
            double sa = 0, sb = 0, saa = 0, sab = 0, sbb = 0;
            for (int i = 0; i < n; i++) { sa += across[i]; sb += along[i]; saa += across[i] * across[i]; sab += across[i] * along[i]; sbb += along[i] * along[i]; }
            float Solve(Func<int, float> v)
            {
                double sv = 0, sav = 0, sbv = 0;
                for (int i = 0; i < n; i++) { sv += v(i); sav += across[i] * v(i); sbv += along[i] * v(i); }
                double[,] m = new double[3, 4] { { n, sa, sb, sv }, { sa, saa, sab, sav }, { sb, sab, sbb, sbv } };
                for (int c = 0; c < 3; c++)
                {
                    int p = c;
                    for (int k = c + 1; k < 3; k++) if (Math.Abs(m[k, c]) > Math.Abs(m[p, c])) p = k;
                    if (Math.Abs(m[p, c]) < 1e-9) continue;
                    for (int k = 0; k < 4; k++) (m[c, k], m[p, k]) = (m[p, k], m[c, k]);
                    for (int k = 0; k < 3; k++)
                    {
                        if (k == c) continue;
                        double f = m[k, c] / m[c, c];
                        for (int j = 0; j < 4; j++) m[k, j] -= f * m[c, j];
                    }
                }
                return Math.Abs(m[1, 1]) < 1e-9 ? 0f : (float)(m[1, 3] / m[1, 1]);
            }
            return (Solve(i => uv[i].s), Solve(i => uv[i].t));
        }

        private static (int w, int h) PictureSize(MapTileset.Face skin)
            => skin.Look == null ? (0, 0) : (BitConverter.ToUInt16(skin.Look.Record, 32), BitConverter.ToUInt16(skin.Look.Record, 34));

        // One quad of slope over part of square (sx, sz), its picture placed as the square's own.
        private static void Quad(TileBake.Result r, TileGrid grid, MapTileset.Face skin, int sx, int sz,
                                 float x0, float z0, float x1, float z1, Func<float, float, float> height)
        {
            (float, float)[] pts = new[] { (x0, z0), (x0, z1), (x1, z1), (x1, z0) };
            Add(r, skin, pts.Select(p => Point(grid, p.Item1, height(p.Item1, p.Item2), p.Item2)).ToArray(),
                pts.Select(p => Uv(skin, p.Item1 - sx, p.Item2 - sz)).ToArray());
        }

        // Treads and risers crossing square (sx, sz) of a straight run climbing along `up`.
        private static void Stairs(TileBake.Result r, TileGrid grid, MapTileset.Face skin, int sx, int sz, (int dx, int dz) up,
                                   int bx0, int bx1, int bz0, int bz1, float lo, float hi, int steps)
        {
            bool alongX = up.dx != 0;
            float start = alongX ? bx0 : bz0, length = alongX ? bx1 - bx0 : bz1 - bz0;
            float from = alongX ? sx : sz, to = from + 1;
            float acrossFrom = alongX ? sz : sx, acrossTo = acrossFrom + 1;
            bool rising = alongX ? up.dx > 0 : up.dz > 0;

            (float x, float z) At(float along, float across) => alongX ? (along, across) : (across, along);

            for (int i = 0; i < steps; i++)
            {
                float t0 = length * i / steps, t1 = length * (i + 1) / steps;
                float c0 = rising ? start + t0 : start + length - t1, c1 = rising ? start + t1 : start + length - t0;
                float a0 = Math.Max(c0, from), a1 = Math.Min(c1, to);
                float h = lo + (hi - lo) * (i + 1) / steps, below = lo + (hi - lo) * i / steps;
                if (a1 > a0 + 1e-4f)
                {
                    (float x, float z)[] tread = new[] { At(a0, acrossFrom), At(a0, acrossTo), At(a1, acrossTo), At(a1, acrossFrom) };
                    Add(r, skin, tread.Select(p => Point(grid, p.x, h, p.z)).ToArray(),
                        tread.Select(p => Uv(skin, p.x - sx, p.z - sz)).ToArray());
                }
                // The riser stands at the step's low edge.
                float edge = rising ? c0 : c1;
                // Each riser belongs to exactly one square: the one on its upper side.
                bool mine = rising ? edge >= from - 1e-4f && edge < to - 1e-4f : edge > from + 1e-4f && edge <= to + 1e-4f;
                if (mine)
                {
                    (float x, float z)[] foot = new[] { At(edge, acrossFrom), At(edge, acrossTo) };
                    (float x, float y, float z)[] riser = new[]
                    {
                        Point(grid, foot[0].x, below, foot[0].z), Point(grid, foot[1].x, below, foot[1].z),
                        Point(grid, foot[1].x, h, foot[1].z), Point(grid, foot[0].x, h, foot[0].z),
                    };
                    (int pw, int ph) = PictureSize(skin);
                    float tall = (h - below) / MapTileset.TileWidth;
                    Add(r, skin, riser, new[] { (0f, ph * tall), ((float)pw, ph * tall), ((float)pw, 0f), (0f, 0f) });
                }
            }
        }

        // Stairs turning a corner: steps x steps cells, each a flat tread at the level of its band,
        // with a riser wherever two cells or a cell and the ground beside differ.
        private static void Terraces(TileBake.Result r, TileGrid grid, HashSet<(int x, int z)> squares,
                                     int bx0, int bx1, int bz0, int bz1, int steps, float lo, float hi,
                                     Func<int, int, int> level, Func<float, float, (float a, float b)> along,
                                     Func<int, int, MapTileset.Face> skinOf, Func<int, int, float> groundOf)
        {
            float cw = (float)(bx1 - bx0) / steps, cd = (float)(bz1 - bz0) / steps;
            float X(int i) => bx0 + cw * i;
            float Z(int j) => bz0 + cd * j;
            (int x, int z) SquareOf(int i, int j) => ((int)Math.Floor(X(i) + cw / 2), (int)Math.Floor(Z(j) + cd / 2));
            bool Inside(int i, int j) => i >= 0 && j >= 0 && i < steps && j < steps && squares.Contains(SquareOf(i, j));
            float Top(int i, int j)
            {
                (float fa, float fb) = along(X(i) + cw / 2, Z(j) + cd / 2);
                int la = Math.Min(steps - 1, (int)Math.Floor(Math.Clamp(fa, 0f, 1f) * steps));
                int lb = Math.Min(steps - 1, (int)Math.Floor(Math.Clamp(fb, 0f, 1f) * steps));
                return lo + (hi - lo) * (level(la, lb) + 1) / steps;
            }

            for (int i = 0; i < steps; i++)
                for (int j = 0; j < steps; j++)
                {
                    if (!Inside(i, j)) continue;
                    (int sx, int sz) = SquareOf(i, j);
                    MapTileset.Face skin = skinOf(sx, sz);
                    if (skin == null) continue;
                    float h = Top(i, j);
                    (float, float)[] tread = new[] { (X(i), Z(j)), (X(i), Z(j + 1)), (X(i + 1), Z(j + 1)), (X(i + 1), Z(j)) };
                    Add(r, skin, tread.Select(p => Point(grid, p.Item1, h, p.Item2)).ToArray(),
                        tread.Select(p => Uv(skin, p.Item1 - sx, p.Item2 - sz)).ToArray());

                    foreach ((int di, int dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        float below;
                        if (Inside(i + di, j + dj)) below = Top(i + di, j + dj);
                        else
                        {
                            (int ox, int oz) = SquareOf(i + di, j + dj);
                            if (squares.Contains((ox, oz))) continue;
                            below = groundOf(ox, oz);
                        }
                        if (h <= below + 1e-4f) continue;
                        (float, float) p0 = di > 0 ? (X(i + 1), Z(j)) : di < 0 ? (X(i), Z(j)) : dj > 0 ? (X(i), Z(j + 1)) : (X(i), Z(j));
                        (float, float) p1 = di > 0 ? (X(i + 1), Z(j + 1)) : di < 0 ? (X(i), Z(j + 1)) : dj > 0 ? (X(i + 1), Z(j + 1)) : (X(i + 1), Z(j));
                        Skirt(r, grid, skin, p0, p1, below, h, h);
                    }
                }
        }

        // An upright face from the ground beside up to the ramp's edge, its picture laid like a riser's.
        private static void Skirt(TileBake.Result r, TileGrid grid, MapTileset.Face skin, (float x, float z) p0, (float x, float z) p1,
                                  float ground, float top0, float top1)
        {
            (int pw, int ph) = PictureSize(skin);
            float along = MathF.Sqrt((p1.x - p0.x) * (p1.x - p0.x) + (p1.z - p0.z) * (p1.z - p0.z));
            float t0 = (top0 - ground) / MapTileset.TileWidth, t1 = (top1 - ground) / MapTileset.TileWidth;
            Add(r, skin,
                new[] { Point(grid, p0.x, ground, p0.z), Point(grid, p1.x, ground, p1.z), Point(grid, p1.x, top1, p1.z), Point(grid, p0.x, top0, p0.z) },
                new[] { (0f, ph * t0), (pw * along, ph * t1), (pw * along, 0f), (0f, 0f) });
        }

        private static (float x, float y, float z) Point(TileGrid grid, float squareX, float height, float squareZ)
            => (-MapTileset.HalfMap + squareX * MapTileset.TileWidth, grid.BaseLift + height, -MapTileset.HalfMap + squareZ * MapTileset.TileWidth);

        // A square's picture spans the square: u across, v from the south edge up.
        private static (float s, float t) Uv(MapTileset.Face skin, float inX, float inZ)
        {
            (int pw, int ph) = PictureSize(skin);
            return (pw * inX, ph * (1f - inZ));
        }

        private static void Add(TileBake.Result r, MapTileset.Face skin, (float x, float y, float z)[] points, (float s, float t)[] uv,
                                bool bothSides = true)
        {
            int first = r.Corners.Count;
            foreach ((float x, float y, float z) p in points) r.Corners.Add(p);
            r.Faces.Add(Enumerable.Range(first, points.Length).ToArray());
            r.OnPicture.Add(uv);
            r.Picture.Add(skin.Picture);
            r.Palette.Add(skin.Palette);
            r.Look.Add(bothSides ? skin.Look?.With(bothSides: true) : skin.Look);

            (float ax, float ay, float az) = points[0]; (float bx, float by, float bz) = points[1]; (float cx, float cy, float cz) = points[2];
            float ux = bx - ax, uy = by - ay, uz = bz - az, vx = cx - ax, vy = cy - ay, vz = cz - az;
            float nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
            float len = MathF.Sqrt(nx * nx + ny * ny + nz * nz);
            if (len < 1e-6f) { nx = 0; ny = 1; nz = 0; len = 1; }
            if (ny < 0 || (Math.Abs(ny) < 1e-6f && nz < 0)) { nx = -nx; ny = -ny; nz = -nz; }
            MapTileset.Corner normal = new MapTileset.Corner { Faces = true, NX = nx / len, NY = ny / len, NZ = nz / len };
            r.Light.Add(Enumerable.Range(0, points.Length).Select(_ => normal.Copy()).ToArray());
        }
    }
}
