using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace DSPRE.Models
{
    /// <summary>Set of 3D tiles a map can be painted with.</summary>
    public sealed class MapTileset
    {
        public const float TileWidth = 0.25f;

        public const float HalfMap = TileWidth * 16f;

        public const int MostSquares = 6;

        public sealed class Corner
        {
            public float X, Y, Z;
            public float S, T;

            public int Colour = -1;

            public bool Faces;
            public float NX, NY, NZ;

            public bool ColourLast;

            public Corner Copy() => (Corner)MemberwiseClone();
        }

        public sealed class Face
        {
            public int[] Corners;
            public string Picture = "";

            public string Palette = "";

            public MaterialLook Look;
        }

        public sealed class Tile
        {
            public string Name = "";

            public int Wide = 1, Deep = 1;

            public List<Corner> Corners = new List<Corner>();
            public List<Face> Faces = new List<Face>();

            /// <summary>Ground rather than an object: a flat face covers the footprint and nothing rises more than a tile above it.</summary>
            public bool IsGround
            {
                get
                {
                    if (Corners.Count == 0) return false;
                    var flats = CoveringFlats().ToList();
                    return flats.Count > 0 && Corners.Max(c => c.Y) - flats.Max() <= TileWidth + 1e-3f;
                }
            }

            // Heights where flat faces together cover the footprint; ground is often laid as many small faces.
            private IEnumerable<float> CoveringFlats()
            {
                float whole = Wide * Deep * TileWidth * TileWidth;
                var byHeight = new Dictionary<int, (float y, float area)>();
                foreach (var face in Faces)
                {
                    if (face.Corners == null || face.Corners.Length < 3) continue;
                    var c = face.Corners.Select(i => Corners[i]).ToArray();
                    if (c.Any(q => Math.Abs(q.Y - c[0].Y) > 1e-3f)) continue;
                    float area = 0;
                    for (int i = 0; i < c.Length; i++)
                    {
                        var p = c[i]; var q = c[(i + 1) % c.Length];
                        area += p.X * q.Z - q.X * p.Z;
                    }
                    int key = (int)MathF.Round(c[0].Y * 1000f);
                    byHeight.TryGetValue(key, out var had);
                    byHeight[key] = (c[0].Y, had.area + Math.Abs(area) / 2f);
                }
                return byHeight.Values.Where(h => h.area >= whole * 0.9f).Select(h => h.y);
            }

            /// <summary>Height of the ground above the tile's base: the highest flat layer covering the footprint.</summary>
            public float SurfaceY => CoveringFlats().DefaultIfEmpty(0f).Max();

            public IEnumerable<string> Pictures => Faces.Select(f => f.Picture).Distinct();

            /// <summary>The picture covering the most surface, shadows only when there is nothing else.</summary>
            public string MainPicture
            {
                get
                {
                    var area = new Dictionary<string, float>();
                    foreach (var face in Faces)
                    {
                        if (string.IsNullOrEmpty(face.Picture) || face.Corners == null || face.Corners.Length < 3) continue;
                        var c = face.Corners.Select(i => Corners[i]).ToArray();
                        float sum = 0;
                        for (int i = 1; i + 1 < c.Length; i++)
                        {
                            float ax = c[i].X - c[0].X, ay = c[i].Y - c[0].Y, az = c[i].Z - c[0].Z;
                            float bx = c[i + 1].X - c[0].X, by = c[i + 1].Y - c[0].Y, bz = c[i + 1].Z - c[0].Z;
                            float cx = ay * bz - az * by, cy = az * bx - ax * bz, cz = ax * by - ay * bx;
                            sum += MathF.Sqrt(cx * cx + cy * cy + cz * cz) / 2f;
                        }
                        area[face.Picture] = area.TryGetValue(face.Picture, out float had) ? had + sum : sum;
                    }
                    bool Shadow(string p) => p.Contains("kage", StringComparison.OrdinalIgnoreCase)
                                          || p.Contains("shadow", StringComparison.OrdinalIgnoreCase);
                    var pick = area.Where(kv => !Shadow(kv.Key)).DefaultIfEmpty().MaxBy(kv => kv.Value);
                    return pick.Key ?? area.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).FirstOrDefault();
                }
            }

            /// <summary>The lowest flat layer covering the footprint near the top, so a see-through decal just above it is skipped.</summary>
            public Face GroundFace
            {
                get
                {
                    float top = SurfaceY;
                    var near = CoveringFlats().Where(y => y >= top - TileWidth / 2f).ToList();
                    if (near.Count == 0) return MainFace;
                    float y = near.Min();
                    float Area(Face f)
                    {
                        var c = f.Corners.Select(i => Corners[i]).ToArray();
                        float a = 0;
                        for (int i = 0; i < c.Length; i++) { var p = c[i]; var q = c[(i + 1) % c.Length]; a += p.X * q.Z - q.X * p.Z; }
                        return Math.Abs(a) / 2f;
                    }
                    return Faces.Where(f => f.Corners != null && f.Corners.Length >= 3
                                         && f.Corners.All(i => Math.Abs(Corners[i].Y - y) < 1e-3f))
                                .OrderByDescending(f => f.Look != null).ThenByDescending(Area)
                                .FirstOrDefault() ?? MainFace;
                }
            }

            /// <summary>A face wearing <see cref="MainPicture"/>, for things drawn in the tile's own look.</summary>
            public Face MainFace
            {
                get
                {
                    string main = MainPicture;
                    return Faces.FirstOrDefault(f => f.Look != null && f.Picture == main) ?? Faces.FirstOrDefault(f => f.Look != null)
                        ?? Faces.FirstOrDefault(f => f.Picture == main) ?? Faces.FirstOrDefault();
                }
            }

            public string Picture => Faces.Count == 0 ? ""
                : Faces.GroupBy(f => f.Picture).OrderByDescending(g => g.Count()).First().Key;

            public int Seen;

            public float Height => Corners.Count == 0 ? 0f : Corners.Max(c => c.Y) - Corners.Min(c => c.Y);

            public int FaceCount => Faces.Count;

            public bool Spreads => Wide > 1 || Deep > 1;

            public bool AcrossTileable, DownTileable, PictureRepeatsAcross, PictureRepeatsDown;

            public bool PictureAcrossTheMap;
            public float PictureScale = 1f;

            public float OffsetX, OffsetZ;

            public bool Merges => AcrossTileable || DownTileable;

            public Dictionary<int, int[,]> CollisionDefaults = new Dictionary<int, int[,]>();

            public int CollisionWide, CollisionDeep, CollisionAnchorX, CollisionAnchorY = -1;

            public int FootprintWide => CollisionWide > 0 ? CollisionWide : Wide;
            public int FootprintDeep => CollisionDeep > 0 ? CollisionDeep : Deep;
            public int FootprintAnchorX => Math.Clamp(CollisionAnchorX, 0, FootprintWide - 1);
            public int FootprintAnchorY => Math.Clamp(CollisionAnchorY >= 0 ? CollisionAnchorY : FootprintDeep - 1, 0, FootprintDeep - 1);

            public int[,] CollisionGrid(int layer)
            {
                if (CollisionDefaults.TryGetValue(layer, out var had)
                    && had.GetLength(0) == FootprintWide && had.GetLength(1) == FootprintDeep) return had;
                var grid = new int[FootprintWide, FootprintDeep];
                for (int x = 0; x < FootprintWide; x++)
                    for (int y = 0; y < FootprintDeep; y++)
                        grid[x, y] = had != null && x < had.GetLength(0) && y < had.GetLength(1) ? had[x, y] : -1;
                CollisionDefaults[layer] = grid;
                return grid;
            }

            public void SetWholeCollision(int layer, int value)
            {
                if (value < 0) { CollisionDefaults.Remove(layer); return; }
                var grid = CollisionGrid(layer);
                for (int x = 0; x < grid.GetLength(0); x++) for (int y = 0; y < grid.GetLength(1); y++) grid[x, y] = value & 0xff;
            }

            public int WholeCollision(int layer)
            {
                if (!CollisionDefaults.TryGetValue(layer, out var grid)) return -1;
                var values = grid.Cast<int>().Distinct().ToList();
                return values.Count == 1 ? values[0] : -2;
            }
        }

        public string Name = "tileset";
        public List<Tile> Tiles { get; } = new List<Tile>();

        public int[] TileOfListed;

        public List<SmartDrawing> SmartDrawings { get; } = new List<SmartDrawing>();

        public void RemoveTile(int tile, params TileGrid[] painted)
        {
            if (tile < 0 || tile >= Tiles.Count) return;
            Tiles.RemoveAt(tile);
            int To(int t) => t == tile ? -1 : t > tile ? t - 1 : t;
            foreach (var d in SmartDrawings) d.Renumber(To);
            foreach (var g in painted) g?.Renumber(To);
        }

        public void SwapTiles(int a, int b, params TileGrid[] painted)
        {
            if (a < 0 || b < 0 || a >= Tiles.Count || b >= Tiles.Count || a == b) return;
            (Tiles[a], Tiles[b]) = (Tiles[b], Tiles[a]);
            int To(int t) => t == a ? b : t == b ? a : t;
            foreach (var d in SmartDrawings) d.Renumber(To);
            foreach (var g in painted) g?.Renumber(To);
        }

        public int DuplicateTile(int tile)
        {
            if (tile < 0 || tile >= Tiles.Count) return -1;
            var was = Tiles[tile];
            var copy = new Tile
            {
                Name = was.Name + "_copy", Wide = was.Wide, Deep = was.Deep,
                AcrossTileable = was.AcrossTileable, DownTileable = was.DownTileable,
                PictureRepeatsAcross = was.PictureRepeatsAcross, PictureRepeatsDown = was.PictureRepeatsDown,
                PictureAcrossTheMap = was.PictureAcrossTheMap, PictureScale = was.PictureScale,
                OffsetX = was.OffsetX, OffsetZ = was.OffsetZ,
                CollisionDefaults = was.CollisionDefaults.ToDictionary(kv => kv.Key, kv => (int[,])kv.Value.Clone()),
                CollisionWide = was.CollisionWide, CollisionDeep = was.CollisionDeep,
                CollisionAnchorX = was.CollisionAnchorX, CollisionAnchorY = was.CollisionAnchorY,
                Corners = was.Corners.Select(c => c.Copy()).ToList(),
                Faces = was.Faces.Select(f => new Face { Corners = (int[])f.Corners.Clone(), Picture = f.Picture, Palette = f.Palette, Look = f.Look }).ToList(),
            };
            Tiles.Add(copy);
            return Tiles.Count - 1;
        }

        public int Append(MapTileset other)
        {
            int first = Tiles.Count;
            if (other == null) return first;
            foreach (var tile in other.Tiles)
            {
                if (Tiles.Any(t => t.Name == tile.Name)) tile.Name += $"_{Tiles.Count}";
                Tiles.Add(tile);
            }
            foreach (var d in other.SmartDrawings)
            {
                var copy = d.Clone();
                copy.Renumber(t => t + first);
                SmartDrawings.Add(copy);
            }
            foreach (var kv in other.PictureFiles) PictureFiles.TryAdd(kv.Key, kv.Value);
            return first;
        }

        public static void Nudge(Tile tile, float dx, float dy, float dz)
        {
            foreach (var c in tile.Corners) { c.X += dx; c.Y += dy; c.Z += dz; }
            tile.OffsetX += dx;
            tile.OffsetZ += dz;
        }

        public static void TurnShape(Tile tile)
        {
            float d = tile.Deep * TileWidth;
            foreach (var c in tile.Corners)
            {
                (c.X, c.Z) = (d - c.Z, c.X);
                if (c.Faces) (c.NX, c.NZ) = (-c.NZ, c.NX);
            }
            (tile.Wide, tile.Deep) = (tile.Deep, tile.Wide);
        }

        public static void Mirror(Tile tile)
        {
            float w = tile.Wide * TileWidth;
            foreach (var c in tile.Corners) { c.X = w - c.X; if (c.Faces) c.NX = -c.NX; }
            foreach (var f in tile.Faces) Array.Reverse(f.Corners);
        }

        public void ChangeLook(string picture, Func<MaterialLook, MaterialLook> change)
        {
            foreach (var tile in Tiles)
                foreach (var face in tile.Faces)
                    if (face.Picture == picture)
                        face.Look = change(face.Look ?? MaterialLook.Plain);
        }

        public bool PlacesArePartsOfThePicture;

        public Dictionary<string, string> PictureFiles { get; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public void PictureIsAt(string name, string folder, string file)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(folder)) return;
            if (PictureFiles.ContainsKey(name)) return;

            foreach (string guess in new[] { file, file + ".png", name, name + ".png" })
            {
                if (string.IsNullOrEmpty(guess)) continue;
                string path = Path.IsPathRooted(guess) ? guess : Path.Combine(folder, guess);
                if (File.Exists(path)) { PictureFiles[name] = path; return; }
            }
        }

        public void TurnPlacesIntoDots(Func<string, (int wide, int tall)> sizeOf)
        {
            if (!PlacesArePartsOfThePicture || sizeOf == null) return;

            foreach (var tile in Tiles)
            {
                var byCorner = new Dictionary<int, (int wide, int tall)>();
                foreach (var face in tile.Faces)
                {
                    var size = sizeOf(face.Picture);
                    if (size.wide <= 0 || size.tall <= 0) continue;
                    foreach (int c in face.Corners)
                        if (!byCorner.ContainsKey(c)) byCorner[c] = size;
                }

                for (int c = 0; c < tile.Corners.Count; c++)
                {
                    if (!byCorner.TryGetValue(c, out var size)) continue;
                    tile.Corners[c].S *= size.wide;
                    tile.Corners[c].T *= size.tall;
                }

                foreach (var face in tile.Faces)
                {
                    if (face.Look == null) continue;
                    var size = sizeOf(face.Picture);
                    if (size.wide > 0 && size.tall > 0) face.Look = face.Look.WithPictureSize(size.wide, size.tall);
                }
            }

            PlacesArePartsOfThePicture = false;
        }

        public static MapTileset FromMap(MapMesh mesh, Func<int, string> pictureOf, string name = null,
                                         Func<int, string> paletteOf = null)
        {
            var set = new MapTileset { Name = name ?? "from map" };
            if (mesh == null) return set;

            var already = new Dictionary<string, Tile>();
            foreach (var piece in PiecesOf(mesh))
            {
                var tile = piece.TileOf(mesh, pictureOf, paletteOf, out _);
                string print = Print(tile);
                if (already.TryGetValue(print, out var had)) { had.Seen++; continue; }

                tile.Seen = 1;
                tile.Name = $"tile{set.Tiles.Count:D3}";
                already[print] = tile;
                set.Tiles.Add(tile);
            }

            set.Tiles.Sort((a, b) => b.Seen.CompareTo(a.Seen));
            // Named after the main picture, with its size when wider than a square; repeats are numbered.
            var used = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var tile in set.Tiles)
            {
                string label = tile.MainPicture ?? "tile";
                if (tile.Spreads) label += $" {tile.Wide}x{tile.Deep}";
                used[label] = used.TryGetValue(label, out int n) ? n + 1 : 1;
                tile.Name = used[label] == 1 ? label : $"{label} {used[label]}";
            }
            return set;
        }

        /// <summary>Faces that make one tile: a square's own faces, or an object spanning several squares.</summary>
        public sealed class Piece
        {
            public int X, Z, Wide = 1, Deep = 1;
            public bool Spans;
            public List<MapMesh.Face> Faces = new List<MapMesh.Face>();

            public Tile TileOf(MapMesh mesh, Func<int, string> pictureOf, Func<int, string> paletteOf, out float baseY)
            {
                baseY = float.MaxValue;
                foreach (var face in Faces)
                    foreach (int c in face.Corners) baseY = Math.Min(baseY, mesh.Vertices[c].Y);
                var tile = TileOver(mesh, Faces, -HalfMap + X * TileWidth, -HalfMap + Z * TileWidth, baseY, pictureOf, paletteOf);
                tile.Wide = Wide;
                tile.Deep = Deep;
                return tile;
            }
        }

        /// <param name="cut">Collects the faces cut into squares because no tile is big enough for them.</param>
        /// <param name="split">Collects, for each face cut, the parts it was cut into.</param>
        public static List<Piece> PiecesOf(MapMesh mesh, List<MapMesh.Face> cut = null,
                                           Dictionary<MapMesh.Face, List<MapMesh.Face>> split = null)
        {
            const float Slack = 1e-3f;
            var bySquare = new Dictionary<(int x, int z), Piece>();
            var wide = new List<(MapMesh.Face face, float x0, float x1, float z0, float z1)>();

            void BySquare(MapMesh.Face face)
            {
                float mx = 0, mz = 0;
                foreach (int c in face.Corners) { mx += mesh.Vertices[c].X; mz += mesh.Vertices[c].Z; }
                var key = (Square(mx / face.Corners.Length), Square(mz / face.Corners.Length));
                if (!bySquare.TryGetValue(key, out var here)) bySquare[key] = here = new Piece { X = key.Item1, Z = key.Item2 };
                here.Faces.Add(face);
            }

            var pieces = new List<Piece>();
            var byFootprint = new Dictionary<(int x, int z, int wide, int deep), Piece>();

            // Parts over the same squares are one tile whatever their heights, so a cliff of stacked strips needs one layer.
            void Place(MapMesh.Face part)
            {
                var v = part.Corners.Select(c => mesh.Vertices[c]).ToArray();
                int Edge(float at) => (int)Math.Round((at + HalfMap) / TileWidth);
                int left = Math.Clamp(Edge(v.Min(q => q.X)), 0, 31), top = Math.Clamp(Edge(v.Min(q => q.Z)), 0, 31);
                int across = Math.Max(1, Math.Min(32 - left, Edge(v.Max(q => q.X)) - left));
                int down = Math.Max(1, Math.Min(32 - top, Edge(v.Max(q => q.Z)) - top));
                if (across == 1 && down == 1) { BySquare(part); return; }
                var key = (left, top, across, down);
                if (!byFootprint.TryGetValue(key, out var piece))
                {
                    byFootprint[key] = piece = new Piece { X = left, Z = top, Wide = across, Deep = down, Spans = true };
                    pieces.Add(piece);
                }
                piece.Faces.Add(part);
            }

            // Pictures repeating within two squares are cut per square, others into the largest tiles, so a long road gives few tiles.
            List<MapMesh.Face> Cut(MapMesh.Face face) => CutIntoSquares(mesh, face, CutsIntoFewTiles(mesh, face) ? 1 : MostSquares);

            foreach (var face in mesh.Faces)
            {
                if (face.Corners == null || face.Corners.Length < 3) continue;
                float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
                foreach (int c in face.Corners)
                {
                    var v = mesh.Vertices[c];
                    x0 = Math.Min(x0, v.X); x1 = Math.Max(x1, v.X); z0 = Math.Min(z0, v.Z); z1 = Math.Max(z1, v.Z);
                }
                if (x1 - x0 <= TileWidth + Slack && z1 - z0 <= TileWidth + Slack) BySquare(face);
                // A face bigger than any tile would pull every object it lies under into one group, so it is cut first.
                else if (x1 - x0 > MostSquares * TileWidth + Slack || z1 - z0 > MostSquares * TileWidth + Slack)
                {
                    var parts = Cut(face);
                    if (parts == null) BySquare(face);
                    else { cut?.Add(face); if (split != null) split[face] = parts; foreach (var part in parts) Place(part); }
                }
                else wide.Add((face, x0, x1, z0, z1));
            }

            // Evenly lit flat ground repeating within two squares is cut per square so each square can be painted alone.
            // A flat face under something standing, or stacked on another, stays with that object.
            bool Flat(MapMesh.Face f)
            {
                float y = mesh.Vertices[f.Corners[0]].Y;
                return f.Corners.All(c => Math.Abs(mesh.Vertices[c].Y - y) < Slack);
            }
            var standing = wide.Where(w => !Flat(w.face)).ToList();
            bool Under((MapMesh.Face face, float x0, float x1, float z0, float z1) w, (MapMesh.Face face, float x0, float x1, float z0, float z1) o)
                => Math.Max(0, Math.Min(o.x1, w.x1) - Math.Max(o.x0, w.x0)) * Math.Max(0, Math.Min(o.z1, w.z1) - Math.Max(o.z0, w.z0))
                   >= (w.x1 - w.x0) * (w.z1 - w.z0) * 0.9f;
            float Y(MapMesh.Face f) => mesh.Vertices[f.Corners[0]].Y;
            bool Stacked((MapMesh.Face face, float x0, float x1, float z0, float z1) w)
                => wide.Any(o => !ReferenceEquals(o.face, w.face) && Flat(o.face)
                              && Math.Abs(Y(o.face) - Y(w.face)) > TileWidth / 2f
                              && Math.Max(0, Math.Min(o.x1, w.x1) - Math.Max(o.x0, w.x0)) * Math.Max(0, Math.Min(o.z1, w.z1) - Math.Max(o.z0, w.z0))
                                 >= Math.Min((o.x1 - o.x0) * (o.z1 - o.z0), (w.x1 - w.x0) * (w.z1 - w.z0)) * 0.5f);
            for (int i = wide.Count - 1; i >= 0; i--)
            {
                var w = wide[i];
                if (!Flat(w.face) || !CutsIntoFewTiles(mesh, w.face) || standing.Any(o => Under(w, o)) || Stacked(w)) continue;
                var parts = CutIntoSquares(mesh, w.face);
                if (parts == null) continue;
                cut?.Add(w.face);
                if (split != null) split[w.face] = parts;
                foreach (var part in parts) BySquare(part);
                wide.RemoveAt(i);
            }

            // Stacked faces over the same ground (a tree's tiers) are one object.
            var root = Enumerable.Range(0, wide.Count).ToArray();
            int Find(int i) { while (root[i] != i) i = root[i] = root[root[i]]; return i; }
            const float Thin = 0.01f;
            for (int i = 0; i < wide.Count; i++)
                for (int j = i + 1; j < wide.Count; j++)
                {
                    var a = wide[i]; var b = wide[j];
                    // A flat patch that cannot be cut (a triangle, a slanted quad) is not part of an object beside it.
                    bool fa = Flat(a.face), fb = Flat(b.face);
                    if (fa != fb && !(fa ? Under(a, b) : Under(b, a))) continue;
                    float ox = Math.Min(a.x1, b.x1) - Math.Max(a.x0, b.x0) + Thin;
                    float oz = Math.Min(a.z1, b.z1) - Math.Max(a.z0, b.z0) + Thin;
                    if (ox <= 0 || oz <= 0) continue;
                    float smaller = Math.Min((a.x1 - a.x0 + Thin) * (a.z1 - a.z0 + Thin), (b.x1 - b.x0 + Thin) * (b.z1 - b.z0 + Thin));
                    if (ox * oz >= smaller * 0.5f) root[Find(i)] = Find(j);
                }

            foreach (var group in Enumerable.Range(0, wide.Count).GroupBy(Find))
            {
                var faces = group.Select(i => wide[i]).ToList();
                int Edge(float at) => (int)Math.Round((at + HalfMap) / TileWidth);
                int left = Math.Clamp(Edge(faces.Min(f => f.x0)), 0, 31), top = Math.Clamp(Edge(faces.Min(f => f.z0)), 0, 31);
                int across = Math.Max(1, Math.Min(32 - left, Edge(faces.Max(f => f.x1)) - left));
                int down = Math.Max(1, Math.Min(32 - top, Edge(faces.Max(f => f.z1)) - top));
                if (across > MostSquares || down > MostSquares)
                {
                    // Too big to be one tile: flat ground is cut into its squares, anything else stays whole.
                    foreach (var f in faces)
                    {
                        var parts = Cut(f.face);
                        if (parts == null) { BySquare(f.face); continue; }
                        cut?.Add(f.face);
                        if (split != null) split[f.face] = parts;
                        foreach (var part in parts) Place(part);
                    }
                    continue;
                }
                // Groups reaching past the map's edge anchor on the edge squares; those on the same squares are one tile.
                bool past = Edge(faces.Min(f => f.x0)) < 0 || Edge(faces.Min(f => f.z0)) < 0
                         || Edge(faces.Max(f => f.x1)) > 32 || Edge(faces.Max(f => f.z1)) > 32;
                if (past && byFootprint.TryGetValue((left, top, across, down), out var over))
                {
                    over.Faces.AddRange(faces.Select(f => f.face));
                    continue;
                }
                var made = new Piece { X = left, Z = top, Wide = across, Deep = down, Spans = true, Faces = faces.Select(f => f.face).ToList() };
                pieces.Add(made);
                if (past) byFootprint[(left, top, across, down)] = made;
            }

            return bySquare.Values.Concat(pieces).ToList();
        }

        /// <summary>Whether a face's picture repeats both ways within two squares and its corners are lit the same.</summary>
        private static bool CutsIntoFewTiles(MapMesh mesh, MapMesh.Face face)
        {
            if (face.Colour != null && face.Colour.Distinct().Count() > 1) return false;
            if (face.OnPicture == null || face.OnPicture.Length < 3) return true;
            var look = face.Material >= 0 ? mesh.LookOf(face.Material) : null;
            if (look == null) return true;
            int pw = BitConverter.ToUInt16(look.Record, 32), ph = BitConverter.ToUInt16(look.Record, 34);
            int param = look.ImageParam;
            if ((param & (1 << 16)) == 0 || (param & (1 << 17)) == 0 || pw == 0 || ph == 0) return false;
            float periodS = pw * ((param & (1 << 18)) != 0 ? 2 : 1), periodT = ph * ((param & (1 << 19)) != 0 ? 2 : 1);

            // Picture units a square spans, along each picture axis.
            var v = face.Corners.Select(c => mesh.Vertices[c]).ToArray();
            float across = Math.Max(1e-4f, (v.Max(q => q.X) - v.Min(q => q.X)) / TileWidth);
            float down = Math.Max(1e-4f, (v.Max(q => q.Z) - v.Min(q => q.Z)) / TileWidth);
            float spanS = face.OnPicture.Max(q => q.s) - face.OnPicture.Min(q => q.s);
            float spanT = face.OnPicture.Max(q => q.t) - face.OnPicture.Min(q => q.t);
            // The picture's axes may run along or across the map's; the pairing that gives square pixels is the one.
            float sx = spanS / across, tz = spanT / down, sz = spanS / down, tx = spanT / across;
            var (perS, perT) = Math.Abs(sx - tz) <= Math.Abs(sz - tx) ? (sx, tz) : (sz, tx);
            // Maps often inset a picture by half a texel, so a two-square repeat reads a little over two.
            return periodS / Math.Max(1e-4f, perS) <= 2.2f && periodT / Math.Max(1e-4f, perT) <= 2.2f;
        }

        /// <summary>Cuts a grid-aligned quad into one quad per square, adding the new corners to the mesh. Null when the face is not such a quad.</summary>
        private static List<MapMesh.Face> CutIntoSquares(MapMesh mesh, MapMesh.Face face, int every = 1)
        {
            const float Near = 1e-3f;
            if (face.Corners == null || face.Corners.Length != 4) return null;
            var v = face.Corners.Select(c => mesh.Vertices[c]).ToArray();
            float x0 = v.Min(q => q.X), x1 = v.Max(q => q.X), z0 = v.Min(q => q.Z), z1 = v.Max(q => q.Z);
            if (x1 - x0 < Near || z1 - z0 < Near) return null;

            // Each corner must sit on the rectangle's own corner; (u, w) says which one.
            var at = new (int u, int w)[4];
            for (int i = 0; i < 4; i++)
            {
                bool left = Math.Abs(v[i].X - x0) < Near, right = Math.Abs(v[i].X - x1) < Near;
                bool front = Math.Abs(v[i].Z - z0) < Near, back = Math.Abs(v[i].Z - z1) < Near;
                if (!(left || right) || !(front || back)) return null;
                at[i] = (right ? 1 : 0, back ? 1 : 0);
            }
            if (at.Distinct().Count() != 4) return null;
            int Of(int u, int w) => Array.FindIndex(at, a => a.u == u && a.w == w);
            int a00 = Of(0, 0), a10 = Of(1, 0), a11 = Of(1, 1), a01 = Of(0, 1);

            float Mix(float p00, float p10, float p11, float p01, float u, float w)
                => (1 - u) * (1 - w) * p00 + u * (1 - w) * p10 + u * w * p11 + (1 - u) * w * p01;
            (float s, float t) Uv(int i) => face.OnPicture != null && i < face.OnPicture.Length ? face.OnPicture[i] : (0f, 0f);
            int Channel(int colour, int shift) => (colour >> shift) & 31;
            int MixColour(float u, float w)
            {
                if (face.Colour == null || face.Colour.Length < 4) return -1;
                int c00 = face.Colour[a00], c10 = face.Colour[a10], c11 = face.Colour[a11], c01 = face.Colour[a01];
                if (c00 < 0 || c10 < 0 || c11 < 0 || c01 < 0) return c00;
                int mixed = 0;
                foreach (int shift in new[] { 0, 5, 10 })
                    mixed |= Math.Clamp((int)Math.Round(Mix(Channel(c00, shift), Channel(c10, shift), Channel(c11, shift), Channel(c01, shift), u, w)), 0, 31) << shift;
                return mixed;
            }

            // Pictures repeat, so each part's placement is moved back by whole repeats to keep its tile common.
            var look = face.Material >= 0 ? mesh.LookOf(face.Material) : null;
            int pw = look == null ? 0 : BitConverter.ToUInt16(look.Record, 32);
            int ph = look == null ? 0 : BitConverter.ToUInt16(look.Record, 34);
            int param = look?.ImageParam ?? 0;
            float periodS = pw * ((param & (1 << 18)) != 0 ? 2 : 1), periodT = ph * ((param & (1 << 19)) != 0 ? 2 : 1);
            bool repeatS = (param & (1 << 16)) != 0 && periodS > 0, repeatT = (param & (1 << 17)) != 0 && periodT > 0;

            var cutsX = new List<float> { x0 };
            var cutsZ = new List<float> { z0 };
            int firstX = (int)Math.Floor((x0 + HalfMap) / TileWidth + Near), firstZ = (int)Math.Floor((z0 + HalfMap) / TileWidth + Near);
            for (int k = firstX + every; -HalfMap + k * TileWidth < x1 - Near; k += every) cutsX.Add(-HalfMap + k * TileWidth);
            for (int k = firstZ + every; -HalfMap + k * TileWidth < z1 - Near; k += every) cutsZ.Add(-HalfMap + k * TileWidth);
            cutsX.Add(x1); cutsZ.Add(z1);

            var parts = new List<MapMesh.Face>();
            for (int j = 0; j + 1 < cutsZ.Count; j++)
                for (int i = 0; i + 1 < cutsX.Count; i++)
                {
                    var corners = new int[4];
                    var uv = new (float s, float t)[4];
                    var colour = face.Colour == null ? null : new int[4];
                    for (int k = 0; k < 4; k++)
                    {
                        float px = cutsX[i + at[k].u], pz = cutsZ[j + at[k].w];
                        float u = (px - x0) / (x1 - x0), w = (pz - z0) / (z1 - z0);
                        float y = Mix(v[a00].Y, v[a10].Y, v[a11].Y, v[a01].Y, u, w);
                        corners[k] = mesh.Vertices.Count;
                        mesh.Vertices.Add(new MapMesh.Vertex { X = px, Y = y, Z = pz });
                        uv[k] = (Mix(Uv(a00).s, Uv(a10).s, Uv(a11).s, Uv(a01).s, u, w), Mix(Uv(a00).t, Uv(a10).t, Uv(a11).t, Uv(a01).t, u, w));
                        if (colour != null) colour[k] = MixColour(u, w);
                    }
                    float shiftS = repeatS ? (float)Math.Floor(uv.Min(q => q.s) / periodS) * periodS : 0f;
                    float shiftT = repeatT ? (float)Math.Floor(uv.Min(q => q.t) / periodT) * periodT : 0f;
                    parts.Add(new MapMesh.Face
                    {
                        Shape = face.Shape, Run = face.Run, Material = face.Material,
                        Corners = corners,
                        OnPicture = uv.Select(q => (q.s - shiftS, q.t - shiftT)).ToArray(),
                        Colour = colour,
                        Normal = face.Normal == null ? null : (int[])face.Normal.Clone(),
                        ColourLast = face.ColourLast == null ? null : (bool[])face.ColourLast.Clone(),
                    });
                }
            return parts;
        }

        public static MapTileset FromObj(string path, out string whynot, float scale = 1f)
        {
            whynot = null;
            var mesh = ObjMesh.Read(path, out whynot);
            if (mesh == null) return null;
            if (mesh.Groups.Count == 0 && mesh.Faces.Count == 0)
            { whynot = "No faces in file."; return null; }

            var set = new MapTileset
            {
                Name = Path.GetFileNameWithoutExtension(path),
                PlacesArePartsOfThePicture = true,
            };

            var groups = mesh.Groups.Count > 0
                ? mesh.Groups
                : new List<ObjMesh.Group> { new ObjMesh.Group { Name = set.Name, First = 0, Count = mesh.Faces.Count } };

            foreach (var group in groups)
            {
                if (group.Count <= 0) continue;

                var faces = mesh.Faces.Skip(group.First).Take(group.Count).ToList();
                var tile = new Tile { Name = group.Name };

                bool alreadyPlaced = group.Wide > 0 && group.Deep > 0;
                float lowX = float.MaxValue, lowY = float.MaxValue, lowZ = float.MaxValue;
                foreach (var face in faces)
                    foreach (var corner in face.Corners)
                    {
                        var p = mesh.Positions[corner.Position];
                        lowX = Math.Min(lowX, p.X * scale);
                        lowY = Math.Min(lowY, p.Y * scale);
                        lowZ = Math.Min(lowZ, p.Z * scale);
                    }
                if (alreadyPlaced) { lowX = 0f; lowY = 0f; lowZ = 0f; }

                foreach (var face in faces)
                {
                    var corners = new int[face.Corners.Count];
                    for (int i = 0; i < face.Corners.Count; i++)
                    {
                        var c = face.Corners[i];
                        var p = mesh.Positions[c.Position];
                        var uv = c.TexCoord >= 0 && c.TexCoord < mesh.TexCoords.Count
                            ? mesh.TexCoords[c.TexCoord] : new ObjMesh.Vec2();

                        corners[i] = tile.Corners.Count;
                        var made = new Corner
                        {
                            X = p.X * scale - lowX,
                            Y = p.Y * scale - lowY,
                            Z = p.Z * scale - lowZ,
                            S = uv.U, T = uv.V,
                            Colour = c.Position < mesh.PositionColours.Count ? mesh.PositionColours[c.Position] : -1,
                            ColourLast = mesh.ColourLastAt.Contains(c.Position),
                        };
                        if (c.Normal >= 0 && c.Normal < mesh.Normals.Count)
                        {
                            var n = mesh.Normals[c.Normal];
                            made.Faces = true; made.NX = n.X; made.NY = n.Y; made.NZ = n.Z;
                        }
                        tile.Corners.Add(made);
                    }
                    var material = face.Material >= 0 && face.Material < mesh.Materials.Count
                        ? mesh.Materials[face.Material] : null;
                    string picture = material == null ? "" : material.PictureName ?? material.Name;
                    tile.Faces.Add(new Face
                    {
                        Corners = corners,
                        Picture = picture,
                        Palette = material?.PaletteName ?? "",
                        Look = material?.Look,
                    });

                    if (face.Material >= 0 && face.Material < mesh.Materials.Count)
                        set.PictureIsAt(picture, Path.GetDirectoryName(path) ?? ".",
                                        mesh.Materials[face.Material].TexturePath);
                }

                if (tile.Faces.Count == 0) continue;

                if (group.Wide > 0 && group.Deep > 0)
                {
                    tile.Wide = Math.Max(1, Math.Min(MostSquares, group.Wide));
                    tile.Deep = Math.Max(1, Math.Min(MostSquares, group.Deep));
                }
                else
                {
                    float reachX = tile.Corners.Count == 0 ? 0f : tile.Corners.Max(c => c.X);
                    float reachZ = tile.Corners.Count == 0 ? 0f : tile.Corners.Max(c => c.Z);
                    tile.Wide = Math.Max(1, Math.Min(MostSquares, (int)Math.Ceiling(reachX / TileWidth - 1e-3f)));
                    tile.Deep = Math.Max(1, Math.Min(MostSquares, (int)Math.Ceiling(reachZ / TileWidth - 1e-3f)));
                }

                if (group.Tileable is { Length: >= 8 } said)
                {
                    tile.AcrossTileable = said[0] != 0; tile.DownTileable = said[1] != 0;
                    tile.PictureRepeatsAcross = said[2] != 0; tile.PictureRepeatsDown = said[3] != 0;
                    tile.PictureAcrossTheMap = said[4] != 0; tile.PictureScale = said[5];
                    tile.OffsetX = said[6]; tile.OffsetZ = said[7];
                }

                if (group.Footprint is { Length: 4 } foot)
                {
                    tile.CollisionWide = foot[0]; tile.CollisionDeep = foot[1];
                    tile.CollisionAnchorX = foot[2]; tile.CollisionAnchorY = foot[3];
                }
                if (!string.IsNullOrEmpty(group.Collision))
                    foreach (string cell in group.Collision.Split(';'))
                    {
                        var parts = cell.Split(',');
                        if (parts.Length != 4 || !int.TryParse(parts[0], out int layer) || !int.TryParse(parts[1], out int cx)
                            || !int.TryParse(parts[2], out int cy)
                            || !int.TryParse(parts[3], NumberStyles.HexNumber, null, out int value)) continue;
                        var grid = tile.CollisionGrid(layer);
                        if (cx >= 0 && cy >= 0 && cx < grid.GetLength(0) && cy < grid.GetLength(1)) grid[cx, cy] = value;
                    }

                tile.Seen = 0;
                if (set.Tiles.Any(t => t.Name == tile.Name)) tile.Name += $"_{set.Tiles.Count}";
                set.Tiles.Add(tile);
            }

            if (set.Tiles.Count == 0) { whynot = "No tiles in file."; return null; }

            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < set.Tiles.Count; i++) index[Safe(set.Tiles[i].Name)] = i;
            foreach (string said in mesh.SmartDrawings)
            {
                var names = said.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                var drawing = new SmartDrawing();
                for (int i = 0; i < names.Length && i < SmartDrawing.Slots; i++)
                    drawing[i] = index.TryGetValue(names[i], out int t) ? t : -1;
                if (!drawing.IsEmpty) set.SmartDrawings.Add(drawing);
            }
            return set;
        }

        public string SaveObj(string path)
        {
            if (Tiles.Count == 0) return "No tiles to export.";

            var obj = new StringBuilder();
            var mtl = new StringBuilder();
            var inv = CultureInfo.InvariantCulture;
            string mtlPath = Path.ChangeExtension(path, ".mtl");

            obj.AppendLine("# DSPRE tileset: one object per tile.");
            obj.AppendLine($"mtllib {Path.GetFileName(mtlPath)}");

            foreach (var drawing in SmartDrawings)
                obj.AppendLine("# smart " + string.Join(" ", Enumerable.Range(0, SmartDrawing.Slots)
                    .Select(i => drawing[i] >= 0 && drawing[i] < Tiles.Count ? Safe(Tiles[drawing[i]].Name) : "-")));

            var materialName = new Dictionary<string, string>(StringComparer.Ordinal);
            var perPicture = new Dictionary<string, int>(StringComparer.Ordinal);
            string MaterialFor(Face face)
            {
                string key = face.Picture + "\n" + face.Palette + "\n" + face.Look?.Key;
                if (materialName.TryGetValue(key, out string name)) return name;

                string picture = string.IsNullOrEmpty(face.Picture) ? "nothing" : face.Picture;
                int seen = perPicture.TryGetValue(picture, out int n) ? n : 0;
                perPicture[picture] = seen + 1;
                name = seen == 0 ? picture : $"{picture}~{seen + 1}";
                materialName[key] = name;

                mtl.AppendLine($"newmtl {name}");
                mtl.AppendLine("Kd 1.000 1.000 1.000");
                if (seen > 0) mtl.AppendLine($"# picture {face.Picture}");

                if (!string.IsNullOrEmpty(face.Palette)) mtl.AppendLine($"# palette {face.Palette}");
                if (face.Look != null) mtl.AppendLine($"# look {face.Look.Key}");
                if (!string.IsNullOrEmpty(face.Picture)) mtl.AppendLine($"map_Kd {face.Picture}.png");
                mtl.AppendLine();
                return name;
            }

            int corner = 1, place = 1, facing = 1;
            foreach (var tile in Tiles)
            {
                obj.AppendLine($"o {Safe(tile.Name)}");
                obj.AppendLine($"# squares {tile.Wide} {tile.Deep}");
                if (tile.CollisionDefaults.Count > 0)
                {
                    obj.AppendLine($"# footprint {tile.CollisionWide} {tile.CollisionDeep} {tile.CollisionAnchorX} {tile.CollisionAnchorY}");
                    var cells = new List<string>();
                    foreach (var (layer, grid) in tile.CollisionDefaults)
                        for (int x = 0; x < grid.GetLength(0); x++)
                            for (int y = 0; y < grid.GetLength(1); y++)
                                if (grid[x, y] >= 0) cells.Add($"{layer},{x},{y},{grid[x, y]:X2}");
                    obj.AppendLine("# collision " + string.Join(";", cells));
                }
                if (tile.Merges || tile.PictureAcrossTheMap || tile.OffsetX != 0 || tile.OffsetZ != 0)
                    obj.AppendLine(FormattableString.Invariant(
                        $"# tileable {B(tile.AcrossTileable)} {B(tile.DownTileable)} {B(tile.PictureRepeatsAcross)} {B(tile.PictureRepeatsDown)} {B(tile.PictureAcrossTheMap)} {tile.PictureScale} {tile.OffsetX} {tile.OffsetZ}"));

                foreach (var c in tile.Corners)
                {
                    string at = $"v {c.X.ToString("0.######", inv)} {c.Y.ToString("0.######", inv)} "
                              + $"{c.Z.ToString("0.######", inv)}";
                    if (c.Colour >= 0)
                        at += $" {((c.Colour & 31) / 31f).ToString("0.####", inv)} "
                            + $"{(((c.Colour >> 5) & 31) / 31f).ToString("0.####", inv)} "
                            + $"{(((c.Colour >> 10) & 31) / 31f).ToString("0.####", inv)}";
                    obj.AppendLine(at);
                }
                var last = tile.Corners.Select((c, i) => (c, i)).Where(x => x.c.ColourLast).Select(x => corner + x.i).ToList();
                if (last.Count > 0) obj.AppendLine("# colourlast " + string.Join(" ", last));

                foreach (var c in tile.Corners)
                    obj.AppendLine($"vt {c.S.ToString("0.######", inv)} "
                                 + $"{(1f - c.T).ToString("0.######", inv)}");

                var facingOf = new int[tile.Corners.Count];
                int faced = 0;
                for (int i = 0; i < tile.Corners.Count; i++)
                {
                    var c = tile.Corners[i];
                    if (!c.Faces) { facingOf[i] = -1; continue; }
                    facingOf[i] = facing + faced++;
                    obj.AppendLine($"vn {c.NX.ToString("0.######", inv)} {c.NY.ToString("0.######", inv)} "
                                 + $"{c.NZ.ToString("0.######", inv)}");
                }

                string painting = null;
                foreach (var face in tile.Faces)
                {
                    string name = MaterialFor(face);
                    if (name != painting)
                    {
                        painting = name;
                        obj.AppendLine($"usemtl {name}");
                    }
                    obj.AppendLine("f " + string.Join(" ", face.Corners.Select(i =>
                        facingOf[i] > 0 ? $"{corner + i}/{place + i}/{facingOf[i]}" : $"{corner + i}/{place + i}")));
                }

                corner += tile.Corners.Count;
                place += tile.Corners.Count;
                facing += faced;
            }

            try
            {
                File.WriteAllText(path, obj.ToString());
                File.WriteAllText(mtlPath, mtl.ToString());
            }
            catch (Exception ex) { return ex.Message; }

            return null;
        }

        private static int B(bool b) => b ? 1 : 0;

        private static string Safe(string name)
        {
            name = new string((name ?? "").Select(c => c == ' ' ? '_' : c).Where(c => c > 32 && c < 127).ToArray());
            return name.Length == 0 ? "tile" : name;
        }

        public static int Square(float at)
        {
            int n = (int)Math.Floor((at + HalfMap) / TileWidth + 1e-4f);
            return Math.Max(0, Math.Min(31, n));
        }

        private static int Fine(float v) => (int)Math.Round(v * 4096f);

        public static Tile TileOver(MapMesh mesh, IEnumerable<MapMesh.Face> faces, float originX,
                                    float originZ, float baseY, Func<int, string> pictureOf,
                                    Func<int, string> paletteOf)
        {
            var tile = new Tile();
            var where = new Dictionary<(int, int, int, int, int, string), int>();

            foreach (var face in faces)
            {
                var corners = new int[face.Corners.Length];
                for (int i = 0; i < face.Corners.Length; i++)
                {
                    var v = mesh.Vertices[face.Corners[i]];
                    var (s, t) = face.OnPicture != null && i < face.OnPicture.Length
                        ? face.OnPicture[i] : (0f, 0f);

                    float x = v.X - originX, y = v.Y - baseY, z = v.Z - originZ;
                    var corner = new Corner { X = x, Y = y, Z = z, S = s, T = t };
                    CarryLight(corner, face, i);

                    var key = (Fine(x), Fine(y), Fine(z), Fine(s), Fine(t), LightKey(corner));
                    if (!where.TryGetValue(key, out int at))
                    {
                        at = tile.Corners.Count;
                        tile.Corners.Add(corner);
                        where[key] = at;
                    }
                    corners[i] = at;
                }
                tile.Faces.Add(new Face
                {
                    Corners = corners,
                    Picture = pictureOf?.Invoke(face.Material) ?? $"material{face.Material}",
                    Palette = paletteOf?.Invoke(face.Material) ?? "",
                    Look = face.Material >= 0 ? mesh.LookOf(face.Material) : null,
                });
            }
            return tile;
        }

        public static void CarryLight(Corner corner, MapMesh.Face face, int i)
        {
            if (face.Colour != null && i < face.Colour.Length) corner.Colour = face.Colour[i];
            if (face.ColourLast != null && i < face.ColourLast.Length) corner.ColourLast = face.ColourLast[i];
            if (face.Normal != null && i < face.Normal.Length && face.Normal[i] >= 0)
            {
                var (nx, ny, nz) = Unpack(face.Normal[i]);
                corner.Faces = true; corner.NX = nx; corner.NY = ny; corner.NZ = nz;
            }
        }

        public static (float x, float y, float z) Unpack(int word)
        {
            static float Ten(int v) => ((v & 0x3ff) ^ 0x200) - 0x200;
            return (Ten(word) / 512f, Ten(word >> 10) / 512f, Ten(word >> 20) / 512f);
        }

        private static string LightKey(Corner c)
            => c.Colour + (c.ColourLast ? "!" : ",")
             + (c.Faces ? $"{Fine(c.NX)},{Fine(c.NY)},{Fine(c.NZ)}" : "-");

        public static string Print(Tile tile)
        {
            var sb = new StringBuilder();
            foreach (var face in tile.Faces)
            {
                sb.Append(face.Picture).Append(':').Append(face.Palette).Append(':')
                  .Append(face.Look?.Key ?? "").Append('|');
                foreach (int i in face.Corners)
                {
                    var c = tile.Corners[i];
                    sb.Append(Fine(c.X)).Append(',').Append(Fine(c.Y)).Append(',').Append(Fine(c.Z))
                      .Append(',').Append(Fine(c.S)).Append(',').Append(Fine(c.T))
                      .Append(',').Append(LightKey(c)).Append(';');
                }
                sb.Append('/');
            }
            return sb.ToString();
        }
    }
}
