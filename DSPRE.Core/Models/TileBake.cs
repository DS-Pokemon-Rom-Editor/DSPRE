using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.Models
{
    /// <summary>Builds a map model from a painted tile grid.</summary>
    public static class TileBake
    {
        public sealed class Result
        {
            public List<(float x, float y, float z)> Corners = new List<(float, float, float)>();

            public List<int[]> Faces = new List<int[]>();

            public List<(float s, float t)[]> OnPicture = new List<(float, float)[]>();

            public List<string> Picture = new List<string>();

            public List<string> Palette = new List<string>();

            public List<MaterialLook> Look = new List<MaterialLook>();

            public List<MapTileset.Corner[]> Light = new List<MapTileset.Corner[]>();

            public string Whynot;

            public int Triangles => Faces.Sum(f => Math.Max(0, f.Length - 2));
            public IEnumerable<string> Pictures => Picture.Distinct();
        }

        public const int MostJoined = 8;

        /// <summary>A ground face cut into squares for editing, kept so it can go back in whole.</summary>
        public sealed class WholeFace
        {
            public (float x, float y, float z)[] Corners;
            public (float s, float t)[] OnPicture;
            public string Picture, Palette;
            public MaterialLook Look;
            public MapTileset.Corner[] Light;
            public float X0, X1, Z0, Z1;
            public List<(int x, int z)> Squares = new List<(int x, int z)>();
            /// <summary>Where each part the face was cut into lies, so only those parts make way for it.</summary>
            public List<(float x, float y, float z)[]> Parts;

            // Corners are the rectangle's own, so the surface over a point is their bilinear mix.
            public float HeightAt(float x, float z)
            {
                float u = (x - X0) / (X1 - X0), w = (z - Z0) / (Z1 - Z0);
                float Y(bool right, bool back) => Corners.First(c => (Math.Abs(c.x - X1) < Math.Abs(c.x - X0)) == right
                                                               && (Math.Abs(c.z - Z1) < Math.Abs(c.z - Z0)) == back).y;
                return (1 - u) * (1 - w) * Y(false, false) + u * (1 - w) * Y(true, false) + u * w * Y(true, true) + (1 - u) * w * Y(false, true);
            }
        }

        public static List<WholeFace> WholeFaces(MapMesh mesh, IEnumerable<MapMesh.Face> faces,
                                                 Func<int, string> pictureOf, Func<int, string> paletteOf,
                                                 Dictionary<MapMesh.Face, List<MapMesh.Face>> parts = null)
        {
            var whole = new List<WholeFace>();
            float tw = MapTileset.TileWidth, half = MapTileset.HalfMap;
            foreach (var face in faces)
            {
                var v = face.Corners.Select(c => mesh.Vertices[c]).ToArray();
                var w = new WholeFace
                {
                    Corners = v.Select(q => (q.X, q.Y, q.Z)).ToArray(),
                    OnPicture = Enumerable.Range(0, v.Length).Select(i => face.OnPicture != null && i < face.OnPicture.Length ? face.OnPicture[i] : (0f, 0f)).ToArray(),
                    Picture = pictureOf?.Invoke(face.Material) ?? $"material{face.Material}",
                    Palette = paletteOf?.Invoke(face.Material) ?? "",
                    Look = face.Material >= 0 ? mesh.LookOf(face.Material) : null,
                    Light = Enumerable.Range(0, v.Length).Select(i => { var c = new MapTileset.Corner(); MapTileset.CarryLight(c, face, i); return c; }).ToArray(),
                    X0 = v.Min(q => q.X), X1 = v.Max(q => q.X), Z0 = v.Min(q => q.Z), Z1 = v.Max(q => q.Z),
                    Parts = parts != null && parts.TryGetValue(face, out var cutInto)
                        ? cutInto.Select(p => p.Corners.Select(c => (mesh.Vertices[c].X, mesh.Vertices[c].Y, mesh.Vertices[c].Z)).ToArray()).ToList()
                        : null,
                };
                for (int z = 0; z < TileGrid.Across; z++)
                    for (int x = 0; x < TileGrid.Across; x++)
                    {
                        float cx = -half + (x + 0.5f) * tw, cz = -half + (z + 0.5f) * tw;
                        if (cx > w.X0 && cx < w.X1 && cz > w.Z0 && cz < w.Z1) w.Squares.Add((x, z));
                    }
                // An upright or sliver face covers no square's middle; it belongs to the square it stands in.
                if (w.Squares.Count == 0)
                    w.Squares.Add((Math.Clamp((int)Math.Floor(((w.X0 + w.X1) / 2 + half) / tw), 0, TileGrid.Across - 1),
                                   Math.Clamp((int)Math.Floor(((w.Z0 + w.Z1) / 2 + half) / tw), 0, TileGrid.Across - 1)));
                whole.Add(w);
            }
            return whole;
        }

        private readonly record struct SquareQuad(int Face, string Key, float S0, float T0, float Ds, float Dt, float Es, float Et,
                                                  float PeriodS, float PeriodT, (int u, int w)[] At);

        /// <summary>Joins neighbouring one-square flat quads into rectangles where the picture carries on unbroken.</summary>
        public static int JoinFlat(Result r)
        {
            const float Near = 1e-3f;
            float tw = MapTileset.TileWidth, half = MapTileset.HalfMap;
            int n = TileGrid.Across;

            var cell = new Dictionary<(int x, int z), List<SquareQuad>>();
            for (int i = 0; i < r.Faces.Count; i++)
            {
                var f = r.Faces[i];
                if (f.Length != 4) continue;
                var c = f.Select(k => r.Corners[k]).ToArray();
                float x0 = c.Min(q => q.x), x1 = c.Max(q => q.x), z0 = c.Min(q => q.z), z1 = c.Max(q => q.z);
                if (Math.Abs(x1 - x0 - tw) > Near || Math.Abs(z1 - z0 - tw) > Near) continue;
                if (c.Any(q => Math.Abs(q.y - c[0].y) > Near)) continue;
                int gx = (int)Math.Round((x0 + half) / tw), gz = (int)Math.Round((z0 + half) / tw);
                if (gx < 0 || gz < 0 || gx >= n || gz >= n || Math.Abs(-half + gx * tw - x0) > Near || Math.Abs(-half + gz * tw - z0) > Near) continue;
                var at = new (int u, int w)[4];
                bool ok = true;
                for (int k = 0; k < 4; k++)
                {
                    bool right = Math.Abs(c[k].x - x1) < Near, back = Math.Abs(c[k].z - z1) < Near;
                    if ((!right && Math.Abs(c[k].x - x0) > Near) || (!back && Math.Abs(c[k].z - z0) > Near)) { ok = false; break; }
                    at[k] = (right ? 1 : 0, back ? 1 : 0);
                }
                if (!ok || at.Distinct().Count() != 4) continue;
                var light = r.Light[i];
                if (light == null || light.Length != 4) continue;
                var l0 = light[0];
                if (light.Any(l => l.Colour != l0.Colour || l.ColourLast != l0.ColourLast || l.Faces != l0.Faces
                                   || Math.Abs(l.NX - l0.NX) > Near || Math.Abs(l.NY - l0.NY) > Near || Math.Abs(l.NZ - l0.NZ) > Near)) continue;
                var uv = r.OnPicture[i];
                if (uv == null || uv.Length != 4) continue;
                (float s, float t) Uv(int u, int w) => uv[Array.FindIndex(at, a => a.u == u && a.w == w)];
                var p00 = Uv(0, 0); var p10 = Uv(1, 0); var p01 = Uv(0, 1); var p11 = Uv(1, 1);
                // Only a picture laid straight across the square can carry on into the next one.
                if (Math.Abs(p11.s - p01.s - (p10.s - p00.s)) > Near || Math.Abs(p11.t - p01.t - (p10.t - p00.t)) > Near) continue;
                var look = r.Look[i];
                int pw = look == null ? 0 : BitConverter.ToUInt16(look.Record, 32), ph = look == null ? 0 : BitConverter.ToUInt16(look.Record, 34);
                int param = look?.ImageParam ?? 0;
                float periodS = (param & (1 << 16)) == 0 ? 0 : pw * ((param & (1 << 18)) != 0 ? 2 : 1);
                float periodT = (param & (1 << 17)) == 0 ? 0 : ph * ((param & (1 << 19)) != 0 ? 2 : 1);
                string key = string.Join("|", r.Picture[i], r.Palette[i], look?.Key ?? "", c[0].y.ToString("0.####"),
                                         l0.Colour, l0.ColourLast, l0.Faces, l0.NX.ToString("0.##"), l0.NY.ToString("0.##"), l0.NZ.ToString("0.##"),
                                         string.Join(",", at.Select(a => a.u * 2 + a.w)),
                                         (p10.s - p00.s).ToString("0.###"), (p10.t - p00.t).ToString("0.###"),
                                         (p01.s - p00.s).ToString("0.###"), (p01.t - p00.t).ToString("0.###"));
                if (!cell.TryGetValue((gx, gz), out var here)) cell[(gx, gz)] = here = new List<SquareQuad>();
                here.Add(new SquareQuad(i, key, p00.s, p00.t, p10.s - p00.s, p10.t - p00.t, p01.s - p00.s, p01.t - p00.t, periodS, periodT, at));
            }

            // A picture that repeats may carry on shifted by whole repeats.
            static bool Carries(float got, float want, float period)
            {
                float gap = got - want;
                if (Math.Abs(gap) < 1e-3f) return true;
                if (period <= 0) return false;
                float turns = gap / period;
                return Math.Abs(turns - MathF.Round(turns)) < 1e-3f;
            }
            var taken = new HashSet<int>();
            SquareQuad? Match(int x, int z, SquareQuad a, int k, int d)
            {
                if (!cell.TryGetValue((x, z), out var list)) return null;
                float s = a.S0 + a.Ds * k + a.Es * d, t = a.T0 + a.Dt * k + a.Et * d;
                foreach (var q in list)
                    if (!taken.Contains(q.Face) && q.Key == a.Key && Carries(q.S0, s, a.PeriodS) && Carries(q.T0, t, a.PeriodT)) return q;
                return null;
            }

            var remove = new List<int>();
            var add = new List<(SquareQuad a, int x, int z, int w, int d, float y)>();
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    if (!cell.TryGetValue((x, z), out var list)) continue;
                    foreach (var a in list)
                    {
                        if (taken.Contains(a.Face)) continue;
                        taken.Add(a.Face);
                        var members = new List<int> { a.Face };
                        int w = 1;
                        while (x + w < n && Match(x + w, z, a, w, 0) is SquareQuad m) { taken.Add(m.Face); members.Add(m.Face); w++; }
                        int d = 1;
                        while (z + d < n)
                        {
                            var next = new List<int>();
                            for (int k = 0; k < w; k++)
                            {
                                if (Match(x + k, z + d, a, k, d) is not SquareQuad m) break;
                                next.Add(m.Face);
                                taken.Add(m.Face);
                            }
                            if (next.Count < w) { foreach (int f in next) taken.Remove(f); break; }
                            members.AddRange(next);
                            d++;
                        }
                        if (members.Count == 1) continue;
                        remove.AddRange(members);
                        add.Add((a, x, z, w, d, r.Corners[r.Faces[a.Face][0]].y));
                    }
                }
            if (add.Count == 0) return 0;

            foreach (var (a, x, z, w, d, y) in add)
            {
                float x0 = -half + x * tw, z0 = -half + z * tw, x1 = x0 + w * tw, z1 = z0 + d * tw;
                int first = r.Corners.Count;
                var face = new int[4];
                var uv = new (float s, float t)[4];
                for (int k = 0; k < 4; k++)
                {
                    var (u, v) = a.At[k];
                    r.Corners.Add((u == 1 ? x1 : x0, y, v == 1 ? z1 : z0));
                    face[k] = first + k;
                    uv[k] = (a.S0 + u * a.Ds * w + v * a.Es * d, a.T0 + u * a.Dt * w + v * a.Et * d);
                }
                r.Faces.Add(face);
                r.OnPicture.Add(uv);
                r.Picture.Add(r.Picture[a.Face]);
                r.Palette.Add(r.Palette[a.Face]);
                r.Look.Add(r.Look[a.Face]);
                r.Light.Add(r.Light[a.Face].Select(l => l.Copy()).ToArray());
            }
            foreach (int i in remove.OrderByDescending(i => i))
            {
                r.Faces.RemoveAt(i); r.OnPicture.RemoveAt(i); r.Picture.RemoveAt(i);
                r.Palette.RemoveAt(i); r.Look.RemoveAt(i); r.Light.RemoveAt(i);
            }
            return remove.Count - add.Count;
        }

        /// <summary>Adds faces back exactly as the map had them, replacing nothing.</summary>
        public static int PutBackAsTheyWere(Result r, IEnumerable<WholeFace> faces)
        {
            int put = 0;
            foreach (var w in faces)
            {
                int first = r.Corners.Count;
                r.Corners.AddRange(w.Corners);
                r.Faces.Add(Enumerable.Range(first, w.Corners.Length).ToArray());
                r.OnPicture.Add(w.OnPicture);
                r.Picture.Add(w.Picture);
                r.Palette.Add(w.Palette);
                r.Look.Add(w.Look);
                r.Light.Add(w.Light.Select(c => c.Copy()).ToArray());
                put++;
            }
            return put;
        }

        private static (long, long, long) Key((float x, float y, float z) p)
            => ((long)Math.Round(p.x * 512), (long)Math.Round(p.y * 512), (long)Math.Round(p.z * 512));

        /// <summary>Swaps the squares cut from each face back for the face itself.</summary>
        public static int PutBackWhole(Result r, IEnumerable<WholeFace> faces)
        {
            const float Near = 1e-3f;
            int put = 0;
            foreach (var w in faces)
            {
                // Each known part takes one baked face by corner position, so a map face on the same spot stays.
                var partsAt = w.Parts?.Select(p => new HashSet<(long, long, long)>(p.Select(Key))).ToList();
                for (int i = r.Faces.Count - 1; i >= 0; i--)
                {
                    if (r.Picture[i] != w.Picture) continue;
                    int match = partsAt?.FindIndex(p => p.SetEquals(r.Faces[i].Select(k => Key(r.Corners[k])))) ?? -1;
                    if (match >= 0) partsAt.RemoveAt(match);
                    bool part = partsAt != null
                        ? match >= 0
                        : r.Faces[i].All(k =>
                        {
                            var (x, y, z) = r.Corners[k];
                            return x > w.X0 - Near && x < w.X1 + Near && z > w.Z0 - Near && z < w.Z1 + Near
                                   && Math.Abs(y - w.HeightAt(x, z)) < Near;
                        });
                    if (!part) continue;
                    r.Faces.RemoveAt(i); r.OnPicture.RemoveAt(i); r.Picture.RemoveAt(i);
                    r.Palette.RemoveAt(i); r.Look.RemoveAt(i); r.Light.RemoveAt(i);
                }
                int first = r.Corners.Count;
                r.Corners.AddRange(w.Corners);
                r.Faces.Add(Enumerable.Range(first, w.Corners.Length).ToArray());
                r.OnPicture.Add(w.OnPicture);
                r.Picture.Add(w.Picture);
                r.Palette.Add(w.Palette);
                r.Look.Add(w.Look);
                r.Light.Add(w.Light.Select(c => c.Copy()).ToArray());
                put++;
            }
            return put;
        }

        public static Result Of(TileGrid grid, MapTileset set)
        {
            var r = new Result();
            if (grid == null || set == null) { r.Whynot = "Nothing painted."; return r; }

            foreach (var lay in Laid(grid, set)) Lay(r, set, lay, grid.BaseLift);

            if (r.Faces.Count == 0) r.Whynot = "Nothing painted.";
            return r;
        }

        /// <summary>Adds a wall along each edge where the first layer's height steps, for edges <paramref name="at"/> accepts.</summary>
        public static int AddWalls(Result r, TileGrid grid, MapTileset set, Func<int, int, bool> at = null)
            => AddWalls(r, grid, set, at, null, null);

        public static int AddWalls(Result r, TileGrid grid, MapTileset set, Func<int, int, bool> at,
                                   ISet<(int x, int z)> ramps, ICollection<(int x, int z)> below)
            => AddWalls(r, grid, set, at, ramps, below, null, null);

        /// <param name="ramps">Squares that slope up instead; no wall is built on the edge they climb.</param>
        /// <param name="below">Collects the lower square beside each wall.</param>
        /// <param name="groundOf">The layer a square is walked on, or -1 to use its lowest occupied layer.</param>
        /// <param name="surfaceOf">Where a square's ground lies on a layer, when the caller knows better than the tile.</param>
        public static int AddWalls(Result r, TileGrid grid, MapTileset set, Func<int, int, bool> at,
                                   ISet<(int x, int z)> ramps, ICollection<(int x, int z)> below, Func<int, int, int> groundOf,
                                   Func<int, int, int, float> surfaceOf = null)
        {
            if (r == null || grid == null || set == null) return 0;
            int n = TileGrid.Across, added = 0;
            float tw = MapTileset.TileWidth, half = MapTileset.HalfMap;

            // Without groundOf, a square's ground is its lowest occupied layer.
            int GroundLayer(int x, int z)
            {
                int walked = groundOf?.Invoke(x, z) ?? -1;
                if (walked >= 0) return walked;
                for (int l = 0; l < TileGrid.Layers; l++)
                {
                    int t = grid.At(x, z, l).Tile;
                    if (t >= 0 && t < set.Tiles.Count) return l;
                }
                return -1;
            }
            // Where a square's ground is drawn: a spanning tile's anchor height plus its ground's place in the tile.
            float Surface(int x, int z, int l)
            {
                if (surfaceOf != null) return surfaceOf(x, z, l);
                var sq = grid.At(x, z, l);
                return sq.Lift + (sq.Tile >= 0 && sq.Tile < set.Tiles.Count ? set.Tiles[sq.Tile].SurfaceY : 0f);
            }
            MapTileset.Face Skin(int x, int z)
            {
                int l = GroundLayer(x, z);
                // The first face can be a shadow or a snow patch; the wall wears the ground under them.
                return l < 0 ? null : set.Tiles[grid.At(x, z, l).Tile].GroundFace;
            }

            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                    foreach (var (nx, nz) in new[] { (x + 1, z), (x, z + 1) })
                    {
                        if (nx >= n || nz >= n) continue;
                        int la = GroundLayer(x, z), lb = GroundLayer(nx, nz);
                        if (la < 0 || lb < 0) continue;
                        if (at != null && !at(x, z) && !at(nx, nz)) continue;
                        // A tile spanning squares is drawn at its anchor's height, so that is what the wall meets.
                        float a = Surface(x, z, la), b = Surface(nx, nz, lb);
                        // Decals sit a sliver above the ground; only a real step makes a wall.
                        if (Math.Abs(a - b) < TileGrid.Step / 2f) continue;

                        bool firstHigher = a > b;
                        var lowSide = firstHigher ? (nx, nz) : (x, z);
                        if (ramps != null && ramps.Contains(lowSide)) continue;
                        below?.Add(lowSide);
                        var skin = firstHigher ? Skin(x, z) : Skin(nx, nz);
                        if (skin == null) continue;
                        float low = grid.BaseLift + Math.Min(a, b), high = grid.BaseLift + Math.Max(a, b);

                        (float x, float z) p0, p1;
                        if (nx != x) { float ex = -half + nx * tw; p0 = (ex, -half + z * tw); p1 = (ex, -half + (z + 1) * tw); }
                        else { float ez = -half + nz * tw; p0 = (-half + x * tw, ez); p1 = (-half + (x + 1) * tw, ez); }

                        int first = r.Corners.Count;
                        r.Corners.Add((p0.x, low, p0.z)); r.Corners.Add((p1.x, low, p1.z));
                        r.Corners.Add((p1.x, high, p1.z)); r.Corners.Add((p0.x, high, p0.z));
                        r.Faces.Add(new[] { first, first + 1, first + 2, first + 3 });

                        int pw = skin.Look == null ? 0 : BitConverter.ToUInt16(skin.Look.Record, 32);
                        int ph = skin.Look == null ? 0 : BitConverter.ToUInt16(skin.Look.Record, 34);
                        r.OnPicture.Add(new[] { (0f, (float)ph), ((float)pw, (float)ph), ((float)pw, 0f), (0f, 0f) });
                        r.Picture.Add(skin.Picture);
                        r.Palette.Add(skin.Palette);
                        r.Look.Add(skin.Look?.With(bothSides: true));

                        float fx = nx != x ? (firstHigher ? 1f : -1f) : 0f, fz = nz != z ? (firstHigher ? 1f : -1f) : 0f;
                        r.Light.Add(Enumerable.Range(0, 4).Select(_ => new MapTileset.Corner { Faces = true, NX = fx, NZ = fz }).ToArray());
                        added++;
                    }
            return added;
        }

        public readonly record struct Laying(int X, int Z, TileGrid.Square Square, int Across, int Down, int Row, int Column, int Layer);

        public static List<Laying> Laid(TileGrid grid, MapTileset set) => Laid(grid, set, null);

        public static List<Laying> Laid(TileGrid grid, MapTileset set, List<(int layer, int x, int z)> hidden)
        {
            var laid = new List<Laying>();
            int n = TileGrid.Across;
            for (int layer = 0; layer < TileGrid.Layers; layer++)
            {
                var at = new TileGrid.Square?[n, n];
                var height = new float[n, n];
                foreach (var (x, z, square) in grid.Placed())
                {
                    if (square.Layer != layer || square.Tile < 0 || square.Tile >= set.Tiles.Count) continue;
                    int row = n - 1 - (z + square.Deep - 1);
                    if (row < 0) continue;
                    at[x, row] = square;
                }
                for (int c = 0; c < n; c++)
                    for (int row = 0; row < n; row++)
                        height[c, row] = grid.HeightAt(c, n - 1 - row, layer);

                // Tiles hanging over an edge are never merged.
                static bool Whole(TileGrid.Square q) => q.PastNorth == 0 && q.Wide == q.FullWide && q.Deep == q.FullDeep;
                bool Same(int c, int row, int c2, int row2)
                    => at[c2, row2] is TileGrid.Square o && at[c, row] is TileGrid.Square s
                       && o.Tile == s.Tile && o.Turn == 0 && s.Turn == 0 && Whole(o) && Whole(s)
                       && height[c, row] == height[c2, row2];

                var written = new bool[n, n];
                var owner = new int[n, n];
                for (int c = 0; c < n; c++)
                    for (int row = 0; row < n; row++)
                    {
                        if (at[c, row] is not TileGrid.Square square) continue;
                        if (written[c, row])
                        {
                            var by = laid[owner[c, row]];
                            bool joined = by.Square.Tile == square.Tile && (by.Across > 1 || by.Down > 1);
                            if (!joined) hidden?.Add((layer, c, n - 1 - (row + square.Deep - 1)));
                            continue;
                        }
                        var tile = set.Tiles[square.Tile];
                        int w = Math.Max(1, (int)square.Wide), d = Math.Max(1, (int)square.Deep);

                        int xs = 1, ys = 1;
                        if (square.Turn == 0 && tile.Merges)
                        {
                            int RunX()
                            {
                                int k = 1;
                                for (int i = w; i < n - c && k < MostJoined; i += w)
                                    if (Same(c, row, c + i, row) && !written[c + i, row]) k++; else break;
                                return k;
                            }
                            int RunY()
                            {
                                int k = 1;
                                for (int i = d; i < n - row && k < MostJoined; i += d)
                                    if (Same(c, row, c, row + i) && !written[c, row + i]) k++; else break;
                                return k;
                            }
                            int GrowY(int across)
                            {
                                int k = 1;
                                for (int i = d; i < n - row && k < MostJoined; i += d)
                                {
                                    for (int j = 0; j < across * w; j += w)
                                        if (!(Same(c, row, c + j, row + i) && !written[c + j, row + i])) return k;
                                    k++;
                                }
                                return k;
                            }
                            int GrowX(int down)
                            {
                                int k = 1;
                                for (int i = w; i < n - c && k < MostJoined; i += w)
                                {
                                    for (int j = 0; j < down * d; j += d)
                                        if (!(Same(c, row, c + i, row + j) && !written[c + i, row + j])) return k;
                                    k++;
                                }
                                return k;
                            }

                            if (tile.AcrossTileable && tile.DownTileable)
                            {
                                int runX = RunX(), runY = RunY();
                                if (runX == 1 && runY == 1) { }
                                else if (runX > runY) { xs = runX; ys = GrowY(runX); }
                                else { ys = runY; xs = GrowX(runY); }
                            }
                            else if (tile.AcrossTileable) xs = RunX();
                            else ys = RunY();
                        }

                        // PDSMS marks only the anchor square of a tile that never joins.
                        bool joins = tile.AcrossTileable || tile.DownTileable;
                        for (int i = 0; i < (joins ? Math.Min(xs * w, n - c) : 1); i++)
                            for (int j = 0; j < (joins ? Math.Min(ys * d, n - row) : 1); j++)
                            {
                                written[c + i, row + j] = true;
                                owner[c + i, row + j] = laid.Count;
                            }

                        int northRow = row + ys * d - 1;
                        laid.Add(new Laying(c, n - 1 - northRow, square, xs, ys, row, c, layer));
                    }
            }
            return laid;
        }

        private static void Lay(Result r, MapTileset set, Laying lay, float baseLift = 0f)
        {
            var square = lay.Square;
            var tile = set.Tiles[square.Tile];
            float originX = -MapTileset.HalfMap + lay.X * MapTileset.TileWidth;
            float originZ = -MapTileset.HalfMap + (lay.Z - square.PastNorth) * MapTileset.TileWidth;
            int mx = lay.Across, my = lay.Down;

            int mu = mx, mv = my;
            if (!(tile.AcrossTileable && tile.DownTileable))
            {
                if (tile.AcrossTileable) { if (tile.PictureRepeatsDown) { mv = mu; mu = 1; } }
                else if (tile.DownTileable) { if (tile.PictureRepeatsAcross) { mu = mv; mv = 1; } }
            }
            if (!tile.PictureRepeatsAcross && !tile.PictureRepeatsDown) { mu = 1; mv = 1; }

            float tw = MapTileset.TileWidth;
            int first = r.Corners.Count;
            var placed = new (float x, float z)[tile.Corners.Count];
            for (int i = 0; i < tile.Corners.Count; i++)
            {
                var corner = tile.Corners[i];
                float cx = corner.X, cz = corner.Z;
                if (mx != 1) cx = mx * (cx - tile.OffsetX) + tile.OffsetX;
                if (my != 1) cz = my * (cz - tile.OffsetZ) + tile.OffsetZ;
                placed[i] = (cx, cz);
                int d = Math.Max(1, square.FullDeep) * my, w = Math.Max(1, square.FullWide) * mx;
                var (tx, tz) = Turned(cx, cz, square.Turn, square.Turn % 2 == 0 ? w : d, square.Turn % 2 == 0 ? d : w);
                r.Corners.Add((originX + tx, baseLift + square.Lift + corner.Y, originZ + tz));
            }

            foreach (var face in tile.Faces)
            {
                r.Faces.Add(face.Corners.Select(i => first + i).ToArray());
                int pw = face.Look == null ? 0 : BitConverter.ToUInt16(face.Look.Record, 32);
                int ph = face.Look == null ? 0 : BitConverter.ToUInt16(face.Look.Record, 34);
                r.OnPicture.Add(face.Corners.Select(i =>
                {
                    var c = tile.Corners[i];
                    if (tile.PictureAcrossTheMap && pw > 0 && ph > 0)
                    {
                        float xt = (placed[i].x - tile.OffsetX) / tw;
                        float yt = square.FullDeep * my - (placed[i].z - tile.OffsetZ) / tw;
                        float u = (xt + lay.Column) * tile.PictureScale, v = (yt + lay.Row) * tile.PictureScale;
                        return (u * pw, (1f - v) * ph);
                    }
                    float s = c.S * mu;
                    float t = mv == 1 ? c.T : ph > 0 ? ph - mv * (ph - c.T) : c.T * mv;
                    return (s, t);
                }).ToArray());
                r.Picture.Add(face.Picture);
                r.Palette.Add(face.Palette);
                r.Look.Add(face.Look);
                r.Light.Add(face.Corners.Select(i => TurnedLight(tile.Corners[i], square.Turn)).ToArray());
            }
        }

        private static (float x, float z) Turned(float x, float z, int quarters, int wide, int deep)
        {
            float w = Math.Max(1, wide) * MapTileset.TileWidth;
            float d = Math.Max(1, deep) * MapTileset.TileWidth;
            switch (quarters & 3)
            {
                case 1: return (d - z, x);
                case 2: return (w - x, d - z);
                case 3: return (z, w - x);
                default: return (x, z);
            }
        }

        private static MapTileset.Corner TurnedLight(MapTileset.Corner corner, int quarters)
        {
            var c = corner.Copy();
            if (!c.Faces) return c;
            (c.NX, c.NZ) = (quarters & 3) switch
            {
                1 => (-corner.NZ, corner.NX),
                2 => (-corner.NX, -corner.NZ),
                3 => (corner.NZ, -corner.NX),
                _ => (corner.NX, corner.NZ),
            };
            return c;
        }

        public static byte[] ToModel(Result baked, out string whynot, float drawnAtScale = 64f)
        {
            whynot = null;
            if (baked == null || baked.Faces.Count == 0)
            { whynot = "Nothing to build."; return null; }

            var mesh = new ObjMesh { Name = "map" };
            foreach (var (x, y, z) in baked.Corners)
                mesh.Positions.Add(new ObjMesh.Vec3 { X = x, Y = y, Z = z });

            var materialOf = new Dictionary<string, int>();
            var materialFor = new int[baked.Faces.Count];
            for (int f = 0; f < baked.Faces.Count; f++)
            {
                string picture = baked.Picture[f];
                string palette = f < baked.Palette.Count && !string.IsNullOrEmpty(baked.Palette[f]) ? baked.Palette[f] : null;
                var look = f < baked.Look.Count ? baked.Look[f] : null;
                string key = picture + "\n" + palette + "\n" + look?.Key;
                if (!materialOf.TryGetValue(key, out int m))
                {
                    materialOf[key] = m = mesh.Materials.Count;
                    mesh.Materials.Add(new ObjMesh.Material { Name = picture, PaletteName = palette, Look = look });
                }
                materialFor[f] = m;
            }

            for (int f = 0; f < baked.Faces.Count; f++)
            {
                var face = new ObjMesh.Face { Material = materialFor[f] };
                var light = f < baked.Light.Count ? baked.Light[f] : null;
                for (int i = 0; i < baked.Faces[f].Length; i++)
                {
                    var (s, t) = baked.OnPicture[f][i];
                    int uv = mesh.TexCoords.Count;
                    mesh.TexCoords.Add(new ObjMesh.Vec2 { U = s, V = t });

                    var corner = new ObjMesh.Corner { Position = baked.Faces[f][i], Normal = -1, TexCoord = uv };
                    var l = light != null && i < light.Length ? light[i] : null;
                    if (l != null)
                    {
                        if (l.Colour >= 0) corner.Colour = l.Colour;
                        corner.ColourLast = l.ColourLast;
                        if (l.Faces)
                        {
                            corner.Normal = mesh.Normals.Count;
                            mesh.Normals.Add(new ObjMesh.Vec3 { X = l.NX, Y = l.NY, Z = l.NZ });
                        }
                    }
                    face.Corners.Add(corner);
                }
                mesh.Faces.Add(face);
            }

            var made = NsbmdWriter.Build(mesh, null, picturesAreElsewhere: true,
                                         drawnAtScale: drawnAtScale);
            if (made.Whynot != null) { whynot = made.Whynot; return null; }
            return made.Bytes;
        }
    }
}
