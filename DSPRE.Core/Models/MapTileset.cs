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
                    List<float> flats = CoveringFlats().ToList();
                    return flats.Count > 0 && Corners.Max(c => c.Y) - flats.Max() <= TileWidth + 1e-3f;
                }
            }

            // Heights where flat faces together cover the footprint; ground is often laid as many small faces.
            private IEnumerable<float> CoveringFlats()
            {
                float whole = Wide * Deep * TileWidth * TileWidth;
                Dictionary<int, (float y, float area)> byHeight = new Dictionary<int, (float y, float area)>();
                foreach (Face face in Faces)
                {
                    if (face.Corners == null || face.Corners.Length < 3) continue;
                    Corner[] c = face.Corners.Select(i => Corners[i]).ToArray();
                    if (c.Any(q => Math.Abs(q.Y - c[0].Y) > 1e-3f)) continue;
                    float area = 0;
                    for (int i = 0; i < c.Length; i++)
                    {
                        Corner p = c[i]; Corner q = c[(i + 1) % c.Length];
                        area += p.X * q.Z - q.X * p.Z;
                    }
                    int key = (int)MathF.Round(c[0].Y * 1000f);
                    byHeight.TryGetValue(key, out (float y, float area) had);
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
                    Dictionary<string, float> area = new Dictionary<string, float>();
                    foreach (Face face in Faces)
                    {
                        if (string.IsNullOrEmpty(face.Picture) || face.Corners == null || face.Corners.Length < 3) continue;
                        Corner[] c = face.Corners.Select(i => Corners[i]).ToArray();
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
                    KeyValuePair<string, float> pick = area.Where(kv => !Shadow(kv.Key)).DefaultIfEmpty().MaxBy(kv => kv.Value);
                    return pick.Key ?? area.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).FirstOrDefault();
                }
            }

            /// <summary>The lowest flat layer covering the footprint near the top, so a see-through decal just above it is skipped.</summary>
            public Face GroundFace
            {
                get
                {
                    float top = SurfaceY;
                    List<float> near = CoveringFlats().Where(y => y >= top - TileWidth / 2f).ToList();
                    if (near.Count == 0) return MainFace;
                    float y = near.Min();
                    float Area(Face f)
                    {
                        Corner[] c = f.Corners.Select(i => Corners[i]).ToArray();
                        float a = 0;
                        for (int i = 0; i < c.Length; i++) { Corner p = c[i]; Corner q = c[(i + 1) % c.Length]; a += p.X * q.Z - q.X * p.Z; }
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
                if (CollisionDefaults.TryGetValue(layer, out int[,] had)
                    && had.GetLength(0) == FootprintWide && had.GetLength(1) == FootprintDeep) return had;
                int[,] grid = new int[FootprintWide, FootprintDeep];
                for (int x = 0; x < FootprintWide; x++)
                    for (int y = 0; y < FootprintDeep; y++)
                        grid[x, y] = had != null && x < had.GetLength(0) && y < had.GetLength(1) ? had[x, y] : -1;
                CollisionDefaults[layer] = grid;
                return grid;
            }

            public void SetWholeCollision(int layer, int value)
            {
                if (value < 0) { CollisionDefaults.Remove(layer); return; }
                int[,] grid = CollisionGrid(layer);
                for (int x = 0; x < grid.GetLength(0); x++) for (int y = 0; y < grid.GetLength(1); y++) grid[x, y] = value & 0xff;
            }

            public int WholeCollision(int layer)
            {
                if (!CollisionDefaults.TryGetValue(layer, out int[,] grid)) return -1;
                List<int> values = grid.Cast<int>().Distinct().ToList();
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
            foreach (SmartDrawing d in SmartDrawings) d.Renumber(To);
            foreach (TileGrid g in painted) g?.Renumber(To);
        }

        public void SwapTiles(int a, int b, params TileGrid[] painted)
        {
            if (a < 0 || b < 0 || a >= Tiles.Count || b >= Tiles.Count || a == b) return;
            (Tiles[a], Tiles[b]) = (Tiles[b], Tiles[a]);
            int To(int t) => t == a ? b : t == b ? a : t;
            foreach (SmartDrawing d in SmartDrawings) d.Renumber(To);
            foreach (TileGrid g in painted) g?.Renumber(To);
        }

        public int DuplicateTile(int tile)
        {
            if (tile < 0 || tile >= Tiles.Count) return -1;
            Tile was = Tiles[tile];
            Tile copy = new Tile
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
            foreach (Tile tile in other.Tiles)
            {
                if (Tiles.Any(t => t.Name == tile.Name)) tile.Name += $"_{Tiles.Count}";
                Tiles.Add(tile);
            }
            foreach (SmartDrawing d in other.SmartDrawings)
            {
                SmartDrawing copy = d.Clone();
                copy.Renumber(t => t + first);
                SmartDrawings.Add(copy);
            }
            foreach (KeyValuePair<string, string> kv in other.PictureFiles) PictureFiles.TryAdd(kv.Key, kv.Value);
            return first;
        }

        public static void Nudge(Tile tile, float dx, float dy, float dz)
        {
            foreach (Corner c in tile.Corners) { c.X += dx; c.Y += dy; c.Z += dz; }
            tile.OffsetX += dx;
            tile.OffsetZ += dz;
        }

        public static void TurnShape(Tile tile)
        {
            float d = tile.Deep * TileWidth;
            foreach (Corner c in tile.Corners)
            {
                (c.X, c.Z) = (d - c.Z, c.X);
                if (c.Faces) (c.NX, c.NZ) = (-c.NZ, c.NX);
            }
            (tile.Wide, tile.Deep) = (tile.Deep, tile.Wide);
        }

        public static void Mirror(Tile tile)
        {
            float w = tile.Wide * TileWidth;
            foreach (Corner c in tile.Corners) { c.X = w - c.X; if (c.Faces) c.NX = -c.NX; }
            foreach (Face f in tile.Faces) Array.Reverse(f.Corners);
        }

        public void ChangeLook(string picture, Func<MaterialLook, MaterialLook> change)
        {
            foreach (Tile tile in Tiles)
                foreach (Face face in tile.Faces)
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

            foreach (Tile tile in Tiles)
            {
                Dictionary<int, (int wide, int tall)> byCorner = new Dictionary<int, (int wide, int tall)>();
                foreach (Face face in tile.Faces)
                {
                    (int wide, int tall) size = sizeOf(face.Picture);
                    if (size.wide <= 0 || size.tall <= 0) continue;
                    foreach (int c in face.Corners)
                        if (!byCorner.ContainsKey(c)) byCorner[c] = size;
                }

                for (int c = 0; c < tile.Corners.Count; c++)
                {
                    if (!byCorner.TryGetValue(c, out (int wide, int tall) size)) continue;
                    tile.Corners[c].S *= size.wide;
                    tile.Corners[c].T *= size.tall;
                }

                foreach (Face face in tile.Faces)
                {
                    if (face.Look == null) continue;
                    (int wide, int tall) size = sizeOf(face.Picture);
                    if (size.wide > 0 && size.tall > 0) face.Look = face.Look.WithPictureSize(size.wide, size.tall);
                }
            }

            PlacesArePartsOfThePicture = false;
        }

        public static MapTileset FromMap(MapMesh mesh, Func<int, string> pictureOf, string name = null,
                                         Func<int, string> paletteOf = null)
        {
            MapTileset set = new MapTileset { Name = name ?? "from map" };
            if (mesh == null) return set;

            Dictionary<string, Tile> already = new Dictionary<string, Tile>();
            foreach (Piece piece in PiecesOf(mesh))
            {
                Tile tile = piece.TileOf(mesh, pictureOf, paletteOf, out _);
                string print = Print(tile);
                if (already.TryGetValue(print, out Tile had)) { had.Seen++; continue; }

                tile.Seen = 1;
                tile.Name = $"tile{set.Tiles.Count:D3}";
                already[print] = tile;
                set.Tiles.Add(tile);
            }

            set.Tiles.Sort((a, b) => b.Seen.CompareTo(a.Seen));
            // Named after the main picture, with its size when wider than a square; repeats are numbered.
            Dictionary<string, int> used = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (Tile tile in set.Tiles)
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
                foreach (MapMesh.Face face in Faces)
                    foreach (int c in face.Corners) baseY = Math.Min(baseY, mesh.Vertices[c].Y);
                Tile tile = TileOver(mesh, Faces, -HalfMap + X * TileWidth, -HalfMap + Z * TileWidth, baseY, pictureOf, paletteOf);
                tile.Wide = Wide;
                tile.Deep = Deep;
                return tile;
            }
        }

        /// <param name="cut">Collects the faces cut into squares because no tile is big enough for them.</param>
        /// <param name="split">Collects, for each face cut, the parts it was cut into.</param>
        /// <param name="pieceOf">Collects the piece each face or part went into.</param>
        public static List<Piece> PiecesOf(MapMesh mesh, List<MapMesh.Face> cut = null,
                                           Dictionary<MapMesh.Face, List<MapMesh.Face>> split = null,
                                           Dictionary<MapMesh.Face, Piece> pieceOf = null)
        {
            List<Piece> all = PiecesOfMap(mesh, cut, split);
            if (pieceOf != null)
                foreach (Piece piece in all)
                    foreach (MapMesh.Face face in piece.Faces) pieceOf[face] = piece;
            return all;
        }

        private static List<Piece> PiecesOfMap(MapMesh mesh, List<MapMesh.Face> cut, Dictionary<MapMesh.Face, List<MapMesh.Face>> split)
        {
            const float Slack = 1e-3f;
            Dictionary<(int x, int z), Piece> bySquare = new Dictionary<(int x, int z), Piece>();
            List<(MapMesh.Face face, float x0, float x1, float z0, float z1)> wide = new List<(MapMesh.Face face, float x0, float x1, float z0, float z1)>();

            void BySquare(MapMesh.Face face)
            {
                float mx = 0, mz = 0;
                foreach (int c in face.Corners) { mx += mesh.Vertices[c].X; mz += mesh.Vertices[c].Z; }
                (int, int) key = (Square(mx / face.Corners.Length), Square(mz / face.Corners.Length));
                if (!bySquare.TryGetValue(key, out Piece here)) bySquare[key] = here = new Piece { X = key.Item1, Z = key.Item2 };
                here.Faces.Add(face);
            }

            List<Piece> pieces = new List<Piece>();
            Dictionary<(int x, int z, int wide, int deep), Piece> byFootprint = new Dictionary<(int x, int z, int wide, int deep), Piece>();

            // Parts over the same squares are one tile whatever their heights, so a cliff of stacked strips needs one layer.
            void Place(MapMesh.Face part)
            {
                (float x0, float x1, float z0, float z1) stand = StandBox(mesh, part);
                int Edge(float at) => (int)Math.Round((at + HalfMap) / TileWidth);
                int left = Math.Clamp(Edge(stand.x0), 0, 31), top = Math.Clamp(Edge(stand.z0), 0, 31);
                int across = Math.Max(1, Math.Min(32 - left, Edge(stand.x1) - left));
                int down = Math.Max(1, Math.Min(32 - top, Edge(stand.z1) - top));
                if (across == 1 && down == 1) { BySquare(part); return; }
                (int left, int top, int across, int down) key = (left, top, across, down);
                if (!byFootprint.TryGetValue(key, out Piece piece))
                {
                    byFootprint[key] = piece = new Piece { X = left, Z = top, Wide = across, Deep = down, Spans = true };
                    pieces.Add(piece);
                }
                piece.Faces.Add(part);
            }

            // A part cut again is replaced by its own parts in the map face's list, so only map faces are put back whole.
            Dictionary<MapMesh.Face, MapMesh.Face> partOf = new Dictionary<MapMesh.Face, MapMesh.Face>();
            void Record(MapMesh.Face face, List<MapMesh.Face> parts)
            {
                if (partOf.TryGetValue(face, out MapMesh.Face whole))
                {
                    if (split != null && split.TryGetValue(whole, out List<MapMesh.Face> list))
                    {
                        int at = list.IndexOf(face);
                        list.RemoveAt(at);
                        list.InsertRange(at, parts);
                    }
                }
                else
                {
                    whole = face;
                    cut?.Add(face);
                    if (split != null) split[face] = parts;
                }
                foreach (MapMesh.Face part in parts) partOf[part] = whole;
            }

            // Something standing is only ever cut along its length; gentle slopes are ground and cut like it.
            List<MapMesh.Face> Cut(MapMesh.Face face)
            {
                if (Strip(face) && CutStrip(mesh, face) is List<MapMesh.Face> strip) return strip;
                if (Steep(mesh, face) && !TooBig(face)) return null;
                (int x, int z) every = StripCuts(mesh, face);
                return CutIntoSquares(mesh, face, every.x, every.z);
            }

            bool TooBig(MapMesh.Face face)
            {
                (float x0, float x1, float z0, float z1) b = StandBox(mesh, face);
                return b.x1 - b.x0 > MostSquares * TileWidth + Slack || b.z1 - b.z0 > MostSquares * TileWidth + Slack;
            }

            HashSet<int> objectFloor = ObjectFloors(mesh);
            // Pictures worn by something standing; a flat band of one, as under a DP or Platinum tree row, is part of
            // that row and is cut with it.
            HashSet<int> standingPicture = new HashSet<int>(mesh.Faces.Where(f => Rises(mesh, f)).Select(f => f.Material));
            bool Strip(MapMesh.Face f) => !Flat(f) || standingPicture.Contains(f.Material);

            // A part of a long face joins the others standing on its squares, so a cut tree row's trunk, base and
            // canopy become one tree instead of three strips on three layers.
            void Queue(MapMesh.Face part)
            {
                float px0 = float.MaxValue, px1 = float.MinValue, pz0 = float.MaxValue, pz1 = float.MinValue;
                foreach (int c in part.Corners)
                {
                    MapMesh.Vertex v = mesh.Vertices[c];
                    px0 = Math.Min(px0, v.X); px1 = Math.Max(px1, v.X); pz0 = Math.Min(pz0, v.Z); pz1 = Math.Max(pz1, v.Z);
                }
                if (px1 - px0 <= TileWidth + Slack && pz1 - pz0 <= TileWidth + Slack) BySquare(part);
                else wide.Add((part, px0, px1, pz0, pz1));
            }

            foreach (MapMesh.Face face in mesh.Faces)
            {
                if (face.Corners == null || face.Corners.Length < 3) continue;
                float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
                foreach (int c in face.Corners)
                {
                    MapMesh.Vertex v = mesh.Vertices[c];
                    x0 = Math.Min(x0, v.X); x1 = Math.Max(x1, v.X); z0 = Math.Min(z0, v.Z); z1 = Math.Max(z1, v.Z);
                }
                if (x1 - x0 <= TileWidth + Slack && z1 - z0 <= TileWidth + Slack) BySquare(face);
                // A fence, wall or canopy row longer than one picture is cut along its length into single objects.
                else if (Strip(face) && CutStrip(mesh, face) is List<MapMesh.Face> strip)
                {
                    Record(face, strip);
                    foreach (MapMesh.Face part in strip) Queue(part);
                }
                // A face bigger than any tile would pull every object it lies under into one group, so it is cut first.
                else if (x1 - x0 > MostSquares * TileWidth + Slack || z1 - z0 > MostSquares * TileWidth + Slack)
                {
                    List<MapMesh.Face> parts = Cut(face);
                    if (parts == null) BySquare(face);
                    else { Record(face, parts); foreach (MapMesh.Face part in parts) Queue(part); }
                }
                else wide.Add((face, x0, x1, z0, z1));
            }

            // Evenly lit flat ground repeating within two squares is cut per square so each square can be painted alone.
            // A flat face stacked on another, or an object's own floor under it, stays with that object; plain ground
            // under a lamp or a rock is still ground.
            bool Flat(MapMesh.Face f)
            {
                float y = mesh.Vertices[f.Corners[0]].Y;
                return f.Corners.All(c => Math.Abs(mesh.Vertices[c].Y - y) < Slack);
            }
            List<(MapMesh.Face face, float x0, float x1, float z0, float z1)> standing = wide.Where(w => !Flat(w.face)).ToList();
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
                (MapMesh.Face face, float x0, float x1, float z0, float z1) w = wide[i];
                if (!Flat(w.face) || !CutsIntoFewTiles(mesh, w.face) || Stacked(w)
                    || (objectFloor.Contains(w.face.Material) && standing.Any(o => Under(w, o)))) continue;
                List<MapMesh.Face> parts = CutIntoSquares(mesh, w.face);
                if (parts == null) continue;
                Record(w.face, parts);
                foreach (MapMesh.Face part in parts) BySquare(part);
                wide.RemoveAt(i);
            }

            // Sloped faces of one picture that meet along an edge are one surface, such as the strips of a cliff's lip.
            // Only strips under a square across count, so neighbouring trees of a row stay apart.
            bool Narrow((MapMesh.Face face, float x0, float x1, float z0, float z1) w) => Math.Min(w.x1 - w.x0, w.z1 - w.z0) < TileWidth - Slack;
            bool Meet(MapMesh.Face a, MapMesh.Face b)
            {
                int shared = 0;
                foreach (int i in a.Corners)
                {
                    MapMesh.Vertex p = mesh.Vertices[i];
                    if (b.Corners.Any(j => { MapMesh.Vertex q = mesh.Vertices[j]; return Math.Abs(p.X - q.X) < Slack && Math.Abs(p.Y - q.Y) < Slack && Math.Abs(p.Z - q.Z) < Slack; }))
                        shared++;
                }
                return shared >= 2;
            }

            // Stacked faces over the same ground (a tree's tiers) are one object.
            int[] root = Enumerable.Range(0, wide.Count).ToArray();
            int Find(int i) { while (root[i] != i) i = root[i] = root[root[i]]; return i; }
            const float Thin = 0.01f;
            for (int i = 0; i < wide.Count; i++)
                for (int j = i + 1; j < wide.Count; j++)
                {
                    (MapMesh.Face face, float x0, float x1, float z0, float z1) a = wide[i]; (MapMesh.Face face, float x0, float x1, float z0, float z1) b = wide[j];
                    // A flat patch that cannot be cut (a triangle, a slanted quad) is not part of an object beside it,
                    // unless it wears the object's own picture, as the base of a tree row does.
                    bool fa = Flat(a.face), fb = Flat(b.face);
                    // Parts a strip was cut into are separate objects; they meet only because they were one face.
                    bool sameStrip = partOf.TryGetValue(a.face, out MapMesh.Face wa) && partOf.TryGetValue(b.face, out MapMesh.Face wb)
                                     && ReferenceEquals(wa, wb) && !Flat(wa);
                    if (sameStrip) continue;
                    if (!fa && !fb && Narrow(a) && Narrow(b) && a.face.Material == b.face.Material && Meet(a.face, b.face)) { root[Find(i)] = Find(j); continue; }
                    if (fa != fb && a.face.Material != b.face.Material
                        && !(fa ? Under(a, b) && objectFloor.Contains(a.face.Material) : Under(b, a) && objectFloor.Contains(b.face.Material))) continue;
                    float ox = Math.Min(a.x1, b.x1) - Math.Max(a.x0, b.x0) + Thin;
                    float oz = Math.Min(a.z1, b.z1) - Math.Max(a.z0, b.z0) + Thin;
                    if (ox <= 0 || oz <= 0) continue;
                    float smaller = Math.Min((a.x1 - a.x0 + Thin) * (a.z1 - a.z0 + Thin), (b.x1 - b.x0 + Thin) * (b.z1 - b.z0 + Thin));
                    if (ox * oz >= smaller * 0.5f) root[Find(i)] = Find(j);
                }

            foreach (IGrouping<int, int> group in Enumerable.Range(0, wide.Count).GroupBy(Find))
            {
                List<(MapMesh.Face face, float x0, float x1, float z0, float z1)> faces = group.Select(i => wide[i]).ToList();
                int Edge(float at) => (int)Math.Round((at + HalfMap) / TileWidth);
                // The footprint is where the group stands; what leans or hangs past it, such as a canopy, overhangs.
                List<(float x0, float x1, float z0, float z1)> stands = faces.Select(f => StandBox(mesh, f.face)).ToList();
                int left = Math.Clamp(Edge(stands.Min(f => f.x0)), 0, 31), top = Math.Clamp(Edge(stands.Min(f => f.z0)), 0, 31);
                int across = Math.Max(1, Math.Min(32 - left, Edge(stands.Max(f => f.x1)) - left));
                int down = Math.Max(1, Math.Min(32 - top, Edge(stands.Max(f => f.z1)) - top));
                if (across > MostSquares || down > MostSquares)
                {
                    // Too big to be one tile: flat ground is cut into its squares, anything else stays whole.
                    foreach ((MapMesh.Face face, float x0, float x1, float z0, float z1) f in faces)
                    {
                        List<MapMesh.Face> parts = Cut(f.face);
                        if (parts == null) { if (Steep(mesh, f.face)) Place(f.face); else BySquare(f.face); continue; }
                        Record(f.face, parts);
                        foreach (MapMesh.Face part in parts) Place(part);
                    }
                    continue;
                }
                // Groups reaching past the map's edge anchor on the edge squares; those on the same squares are one tile.
                bool past = Edge(faces.Min(f => f.x0)) < 0 || Edge(faces.Min(f => f.z0)) < 0
                         || Edge(faces.Max(f => f.x1)) > 32 || Edge(faces.Max(f => f.z1)) > 32;
                if (past && byFootprint.TryGetValue((left, top, across, down), out Piece over))
                {
                    over.Faces.AddRange(faces.Select(f => f.face));
                    continue;
                }
                Piece made = new Piece { X = left, Z = top, Wide = across, Deep = down, Spans = true, Faces = faces.Select(f => f.face).ToList() };
                pieces.Add(made);
                if (past) byFootprint[(left, top, across, down)] = made;
            }

            float Low(IEnumerable<MapMesh.Face> fs) => fs.SelectMany(f => f.Corners).Min(c => mesh.Vertices[c].Y);
            float High(IEnumerable<MapMesh.Face> fs) => fs.SelectMany(f => f.Corners).Max(c => mesh.Vertices[c].Y);

            // Neighbouring objects whose footprints share an edge column or row, as an HGSS tree whose 4-wide canopy
            // reaches over the trees beside it: the larger gives the shared squares up as overhang, as PDSMS's tree
            // stands on its middle 2x2, so a forest fits on one layer.
            List<Piece> objects = pieces.Where(p => p.Spans && p.Faces.Any(f => !Flat(f)) && High(p.Faces) - Low(p.Faces) > TileWidth * 1.5f).ToList();
            for (bool moved = true; moved;)
            {
                moved = false;
                for (int i = 0; i < objects.Count && !moved; i++)
                    for (int j = 0; j < objects.Count && !moved; j++)
                    {
                        Piece a = objects[i], b = objects[j];
                        if (i == j || a.Wide * a.Deep < b.Wide * b.Deep) continue;
                        int ox0 = Math.Max(a.X, b.X), ox1 = Math.Min(a.X + a.Wide, b.X + b.Wide);
                        int oz0 = Math.Max(a.Z, b.Z), oz1 = Math.Min(a.Z + a.Deep, b.Z + b.Deep);
                        if (ox0 >= ox1 || oz0 >= oz1) continue;
                        // Only a whole edge column or row of the larger one can become overhang.
                        if (oz0 == a.Z && oz1 == a.Z + a.Deep && ox1 - ox0 < a.Wide)
                        {
                            if (ox0 == a.X) { a.X = ox1; a.Wide -= ox1 - ox0; moved = true; }
                            else if (ox1 == a.X + a.Wide) { a.Wide -= ox1 - ox0; moved = true; }
                        }
                        else if (ox0 == a.X && ox1 == a.X + a.Wide && oz1 - oz0 < a.Deep)
                        {
                            if (oz0 == a.Z) { a.Z = oz1; a.Deep -= oz1 - oz0; moved = true; }
                            else if (oz1 == a.Z + a.Deep) { a.Deep -= oz1 - oz0; moved = true; }
                        }
                    }
            }

            // An object's own floor, cut into squares with the ground, goes back into the object standing on it, as a
            // PDSMS tree carries its shadow: erasing the tree then takes its shadow too.
            foreach (Piece obj in pieces.Where(p => p.Spans && p.Faces.Any(f => !Flat(f)) && High(p.Faces) - Low(p.Faces) > TileWidth * 1.5f))
            {
                HashSet<int> own = new HashSet<int>(obj.Faces.Select(f => f.Material));
                float foot = Low(obj.Faces);
                for (int z = obj.Z; z < obj.Z + obj.Deep; z++)
                    for (int x = obj.X; x < obj.X + obj.Wide; x++)
                    {
                        if (!bySquare.TryGetValue((x, z), out Piece here)) continue;
                        List<MapMesh.Face> floor = here.Faces.Where(f => Flat(f) && (objectFloor.Contains(f.Material) || own.Contains(f.Material))
                                                                    && Math.Abs(mesh.Vertices[f.Corners[0]].Y - foot) < TileWidth / 2f).ToList();
                        if (floor.Count == 0) continue;
                        obj.Faces.AddRange(floor);
                        here.Faces.RemoveAll(floor.Contains);
                        if (here.Faces.Count == 0) bySquare.Remove((x, z));
                    }
            }

            // A lamp or post standing on a square leaves the plain ground under it as ground; a cliff step, rising no
            // more than about a square, keeps its ground as PDSMS cliff tiles do.
            List<Piece> grounds = new List<Piece>();
            foreach (Piece here in bySquare.Values)
            {
                List<MapMesh.Face> standingHere = here.Faces.Where(f => Steep(mesh, f)).ToList();
                if (standingHere.Count == 0) continue;
                List<MapMesh.Face> flats = here.Faces.Where(Flat).ToList();
                if (flats.Count == 0) continue;
                float floorY = Low(flats);
                if (High(standingHere) - floorY <= TileWidth * 1.5f) continue;
                HashSet<int> own = new HashSet<int>(standingHere.Select(f => f.Material));
                List<MapMesh.Face> plain = flats.Where(f => Math.Abs(mesh.Vertices[f.Corners[0]].Y - floorY) < Slack
                                                            && !objectFloor.Contains(f.Material) && !own.Contains(f.Material)).ToList();
                if (plain.Count == 0 || plain.Count == here.Faces.Count) continue;
                here.Faces.RemoveAll(plain.Contains);
                grounds.Add(new Piece { X = here.X, Z = here.Z, Faces = plain });
            }

            return bySquare.Values.Concat(grounds).Concat(pieces).ToList();
        }

        /// <summary>Whether a face's picture repeats both ways within two squares and its corners are lit the same.</summary>
        private static bool CutsIntoFewTiles(MapMesh mesh, MapMesh.Face face)
        {
            if (face.Colour != null && face.Colour.Distinct().Count() > 1) return false;
            if (face.OnPicture == null || face.OnPicture.Length < 3) return true;
            MaterialLook look = face.Material >= 0 ? mesh.LookOf(face.Material) : null;
            if (look == null) return true;
            int pw = BitConverter.ToUInt16(look.Record, 32), ph = BitConverter.ToUInt16(look.Record, 34);
            int param = look.ImageParam;
            if ((param & (1 << 16)) == 0 || (param & (1 << 17)) == 0 || pw == 0 || ph == 0) return false;
            float periodS = pw * ((param & (1 << 18)) != 0 ? 2 : 1), periodT = ph * ((param & (1 << 19)) != 0 ? 2 : 1);

            // Picture units a square spans, along each picture axis.
            MapMesh.Vertex[] v = face.Corners.Select(c => mesh.Vertices[c]).ToArray();
            float across = Math.Max(1e-4f, (v.Max(q => q.X) - v.Min(q => q.X)) / TileWidth);
            float down = Math.Max(1e-4f, (v.Max(q => q.Z) - v.Min(q => q.Z)) / TileWidth);
            float spanS = face.OnPicture.Max(q => q.s) - face.OnPicture.Min(q => q.s);
            float spanT = face.OnPicture.Max(q => q.t) - face.OnPicture.Min(q => q.t);
            // The picture's axes may run along or across the map's; the pairing that gives square pixels is the one.
            float sx = spanS / across, tz = spanT / down, sz = spanS / down, tx = spanT / across;
            (float perS, float perT) = Math.Abs(sx - tz) <= Math.Abs(sz - tx) ? (sx, tz) : (sz, tx);
            // Maps often inset a picture by half a texel, so a two-square repeat reads a little over two.
            return periodS / Math.Max(1e-4f, perS) <= 2.2f && periodT / Math.Max(1e-4f, perT) <= 2.2f;
        }

        /// <summary>
        /// How a face that is not a strip is cut, in squares along X and Z (0 = left whole). Ground whose picture
        /// repeats within two squares goes square by square; anything longer than two squares is cut at its picture's
        /// repeat, at most two squares.
        /// </summary>
        private static (int x, int z) StripCuts(MapMesh mesh, MapMesh.Face face)
        {
            if (CutsIntoFewTiles(mesh, face)) return (1, 1);
            MapMesh.Vertex[] v = face.Corners.Select(c => mesh.Vertices[c]).ToArray();
            float acrossX = (v.Max(q => q.X) - v.Min(q => q.X)) / TileWidth, acrossZ = (v.Max(q => q.Z) - v.Min(q => q.Z)) / TileWidth;
            int Along(bool alongX)
            {
                if ((alongX ? acrossX : acrossZ) <= 2f + 1e-3f) return 0;
                int repeat = RepeatSquares(mesh, face, alongX);
                return repeat > 0 ? Math.Clamp(repeat, 1, 2) : MostSquares;
            }
            return (Along(true), Along(false));
        }

        /// <summary>Squares one repeat of the face's picture spans along X or Z, or 0 when the picture does not repeat that way.</summary>
        private static int RepeatSquares(MapMesh mesh, MapMesh.Face face, bool alongX)
        {
            const float Near = 1e-3f;
            if (face.OnPicture == null || face.OnPicture.Length != face.Corners.Length) return 0;
            MaterialLook look = face.Material >= 0 ? mesh.LookOf(face.Material) : null;
            if (look == null) return 0;
            int pw = BitConverter.ToUInt16(look.Record, 32), ph = BitConverter.ToUInt16(look.Record, 34);
            int param = look.ImageParam;
            // Two corners on the same line across the other axis give how the picture moves along this one.
            for (int i = 0; i < face.Corners.Length; i++)
                for (int j = 0; j < face.Corners.Length; j++)
                {
                    MapMesh.Vertex a = mesh.Vertices[face.Corners[i]], b = mesh.Vertices[face.Corners[j]];
                    float along = alongX ? b.X - a.X : b.Z - a.Z, other = alongX ? b.Z - a.Z : b.X - a.X;
                    if (along < Near || Math.Abs(other) > Near) continue;
                    float ds = Math.Abs(face.OnPicture[j].s - face.OnPicture[i].s), dt = Math.Abs(face.OnPicture[j].t - face.OnPicture[i].t);
                    bool onS = ds >= dt;
                    bool repeats = (param & (1 << (onS ? 16 : 17))) != 0;
                    float period = (onS ? pw : ph) * ((param & (1 << (onS ? 18 : 19))) != 0 ? 2 : 1);
                    float perSquare = (onS ? ds : dt) / (along / TileWidth);
                    if (!repeats || period <= 0 || perSquare <= 0) return 0;
                    return Math.Max(1, (int)Math.Round(period / perSquare));
                }
            return 0;
        }

        /// <summary>
        /// Cuts a grid-aligned quad on the grid lines every so many squares along X and Z (0 leaves that way whole),
        /// adding the new corners to the mesh. Null when the face is not such a quad.
        /// </summary>
        private static List<MapMesh.Face> CutIntoSquares(MapMesh mesh, MapMesh.Face face, int every = 1, int everyZ = -1)
        {
            if (everyZ < 0) everyZ = every;
            const float Near = 1e-3f;
            if (face.Corners == null || face.Corners.Length != 4) return null;
            MapMesh.Vertex[] v = face.Corners.Select(c => mesh.Vertices[c]).ToArray();
            float x0 = v.Min(q => q.X), x1 = v.Max(q => q.X), z0 = v.Min(q => q.Z), z1 = v.Max(q => q.Z);
            if (x1 - x0 < Near || z1 - z0 < Near) return null;

            // Each corner must sit on the rectangle's own corner; (u, w) says which one.
            (int u, int w)[] at = new (int u, int w)[4];
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
            MaterialLook look = face.Material >= 0 ? mesh.LookOf(face.Material) : null;
            int pw = look == null ? 0 : BitConverter.ToUInt16(look.Record, 32);
            int ph = look == null ? 0 : BitConverter.ToUInt16(look.Record, 34);
            int param = look?.ImageParam ?? 0;
            float periodS = pw * ((param & (1 << 18)) != 0 ? 2 : 1), periodT = ph * ((param & (1 << 19)) != 0 ? 2 : 1);
            bool repeatS = (param & (1 << 16)) != 0 && periodS > 0, repeatT = (param & (1 << 17)) != 0 && periodT > 0;

            List<float> cutsX = new List<float> { x0 };
            List<float> cutsZ = new List<float> { z0 };
            int firstX = (int)Math.Floor((x0 + HalfMap) / TileWidth + Near), firstZ = (int)Math.Floor((z0 + HalfMap) / TileWidth + Near);
            if (every > 0)
                for (int k = firstX + every; -HalfMap + k * TileWidth < x1 - Near; k += every) cutsX.Add(-HalfMap + k * TileWidth);
            if (everyZ > 0)
                for (int k = firstZ + everyZ; -HalfMap + k * TileWidth < z1 - Near; k += everyZ) cutsZ.Add(-HalfMap + k * TileWidth);
            cutsX.Add(x1); cutsZ.Add(z1);

            List<MapMesh.Face> parts = new List<MapMesh.Face>();
            for (int j = 0; j + 1 < cutsZ.Count; j++)
                for (int i = 0; i + 1 < cutsX.Count; i++)
                {
                    int[] corners = new int[4];
                    (float s, float t)[] uv = new (float s, float t)[4];
                    int[] colour = face.Colour == null ? null : new int[4];
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

        /// <summary>
        /// Cuts a strip such as a fence, a wall or a row of tree canopy on the grid lines across its level length, never
        /// across its slope, into parts of its picture's repeat, at most two squares as a PDSMS tileable tree. The strip
        /// may lie off the grid. Null when the face is no such strip or needs no cut.
        /// </summary>
        private static List<MapMesh.Face> CutStrip(MapMesh mesh, MapMesh.Face face)
        {
            const float Near = 1e-3f;
            if (face.Corners == null || face.Corners.Length != 4) return null;
            MapMesh.Vertex[] v = face.Corners.Select(c => mesh.Vertices[c]).ToArray();
            bool longerX = v.Max(q => q.X) - v.Min(q => q.X) >= v.Max(q => q.Z) - v.Min(q => q.Z);
            foreach (bool alongX in new[] { longerX, !longerX })
            {
                float A(MapMesh.Vertex q) => alongX ? q.X : q.Z;
                float a0 = v.Min(A), a1 = v.Max(A);
                if (a1 - a0 <= TileWidth + Near) continue;
                int[] start = Enumerable.Range(0, 4).Where(i => Math.Abs(A(v[i]) - a0) < Near).ToArray();
                int[] end = Enumerable.Range(0, 4).Where(i => Math.Abs(A(v[i]) - a1) < Near).ToArray();
                if (start.Length != 2 || end.Length != 2) continue;
                // Each start corner runs along an edge of the quad to the end corner beside it.
                int EndOf(int s) => end.Contains((s + 1) % 4) ? (s + 1) % 4 : end.Contains((s + 3) % 4) ? (s + 3) % 4 : -1;
                int[] to = { EndOf(start[0]), EndOf(start[1]) };
                if (to[0] < 0 || to[1] < 0 || to[0] == to[1]) continue;
                // The length runs level; a ramp climbing along it is no strip.
                if (Math.Abs(v[start[0]].Y - v[to[0]].Y) > TileWidth / 4 || Math.Abs(v[start[1]].Y - v[to[1]].Y) > TileWidth / 4) continue;

                int repeat = RepeatSquares(mesh, face, alongX);
                float squares = (a1 - a0) / TileWidth;
                // An upright picture one repeat long is one object, as an HGSS tree is; a leaning row such as a DP or
                // Platinum forest holds two trees per repeat and is cut every two squares like PDSMS's tileable tree.
                // A picture that does not repeat is stretched and is cut only when longer than any tile.
                bool onePicture = repeat <= 0 || (Steep(mesh, face) && squares <= repeat + 0.1f);
                if (onePicture && squares <= MostSquares + Near) continue;
                int every = onePicture ? MostSquares : Math.Clamp(repeat, 1, 2);
                if (squares <= every + 0.1f) continue;
                List<float> cuts = new List<float> { a0 };
                int first = (int)Math.Floor((a0 + HalfMap) / TileWidth + Near);
                for (int k = first + every; -HalfMap + k * TileWidth < a1 - Near; k += every) cuts.Add(-HalfMap + k * TileWidth);
                cuts.Add(a1);
                if (cuts.Count < 3) continue;

                // Each corner is the start or the end of one of the two long edges.
                (int edge, bool atEnd)[] role = new (int edge, bool atEnd)[4];
                for (int e = 0; e < 2; e++) { role[start[e]] = (e, false); role[to[e]] = (e, true); }
                MaterialLook look = face.Material >= 0 ? mesh.LookOf(face.Material) : null;
                int pw = look == null ? 0 : BitConverter.ToUInt16(look.Record, 32);
                int ph = look == null ? 0 : BitConverter.ToUInt16(look.Record, 34);
                int param = look?.ImageParam ?? 0;
                float periodS = pw * ((param & (1 << 18)) != 0 ? 2 : 1), periodT = ph * ((param & (1 << 19)) != 0 ? 2 : 1);
                bool repeatS = (param & (1 << 16)) != 0 && periodS > 0, repeatT = (param & (1 << 17)) != 0 && periodT > 0;
                (float s, float t) Uv(int i) => face.OnPicture != null && i < face.OnPicture.Length ? face.OnPicture[i] : (0f, 0f);
                int MixColour(int c0, int c1, float t)
                {
                    if (c0 < 0 || c1 < 0) return c0;
                    int mixed = 0;
                    foreach (int shift in new[] { 0, 5, 10 })
                        mixed |= Math.Clamp((int)Math.Round(((c0 >> shift) & 31) * (1 - t) + ((c1 >> shift) & 31) * t), 0, 31) << shift;
                    return mixed;
                }

                List<MapMesh.Face> parts = new List<MapMesh.Face>();
                for (int n = 0; n + 1 < cuts.Count; n++)
                {
                    int[] corners = new int[4];
                    (float s, float t)[] uv = new (float s, float t)[4];
                    int[] colour = face.Colour == null || face.Colour.Length < 4 ? null : new int[4];
                    for (int k = 0; k < 4; k++)
                    {
                        int s0 = start[role[k].edge], s1 = to[role[k].edge];
                        float at = role[k].atEnd ? cuts[n + 1] : cuts[n];
                        float t = (at - A(v[s0])) / (A(v[s1]) - A(v[s0]));
                        corners[k] = mesh.Vertices.Count;
                        mesh.Vertices.Add(new MapMesh.Vertex
                        {
                            X = v[s0].X + (v[s1].X - v[s0].X) * t,
                            Y = v[s0].Y + (v[s1].Y - v[s0].Y) * t,
                            Z = v[s0].Z + (v[s1].Z - v[s0].Z) * t,
                        });
                        uv[k] = (Uv(s0).s + (Uv(s1).s - Uv(s0).s) * t, Uv(s0).t + (Uv(s1).t - Uv(s0).t) * t);
                        if (colour != null) colour[k] = MixColour(face.Colour[s0], face.Colour[s1], t);
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
            return null;
        }

        /// <summary>
        /// Whether a face stands rather than lies: it rises at least as far as it leans, as a wall, fence or tree
        /// canopy does, where a hill or a ramp rises gently over its length.
        /// </summary>
        private static bool Steep(MapMesh mesh, MapMesh.Face face)
        {
            const float Near = 1e-3f;
            if (face.Corners == null || face.Corners.Length < 3) return false;
            MapMesh.Vertex[] v = face.Corners.Select(c => mesh.Vertices[c]).ToArray();
            float low = v.Min(q => q.Y), high = v.Max(q => q.Y);
            if (high - low < Near) return false;
            return high - low >= Run(v) * 0.9f;
        }

        // Standing or leaning at least half a square up, as any tree canopy does; flat ground never rises.
        private static bool Rises(MapMesh mesh, MapMesh.Face face)
        {
            if (face.Corners == null || face.Corners.Length < 3) return false;
            if (Steep(mesh, face)) return true;
            IEnumerable<float> y = face.Corners.Select(c => mesh.Vertices[c].Y);
            return y.Max() - y.Min() >= TileWidth / 2f;
        }

        // How far a face runs from its bottom to its top, seen from above.
        private static float Run(MapMesh.Vertex[] v)
        {
            const float Near = 1e-3f;
            float low = v.Min(q => q.Y), high = v.Max(q => q.Y);
            float run = float.MaxValue;
            foreach (MapMesh.Vertex a in v.Where(q => q.Y - low < Near))
                foreach (MapMesh.Vertex b in v.Where(q => high - q.Y < Near))
                    run = Math.Min(run, MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z)));
            return run;
        }

        /// <summary>
        /// Where a face stands, seen from above: for something standing, the part of it below half its height. A canopy
        /// leaning over the squares behind it stands only on its lower squares, as a PDSMS tree's footprint is its base,
        /// not its overhang. Ground, flat or sloped, stands on all of itself.
        /// </summary>
        private static (float x0, float x1, float z0, float z1) StandBox(MapMesh mesh, MapMesh.Face face)
        {
            MapMesh.Vertex[] v = face.Corners.Select(c => mesh.Vertices[c]).ToArray();
            float low = v.Min(q => q.Y), high = v.Max(q => q.Y);
            // A rock or fence leans within its own two squares; only a lean reaching further, as a canopy's, overhangs.
            if (!Steep(mesh, face) || Run(v) <= 2 * TileWidth + 1e-3f) return (v.Min(q => q.X), v.Max(q => q.X), v.Min(q => q.Z), v.Max(q => q.Z));
            float mid = (low + high) / 2f;
            List<(float x, float z)> below = new List<(float x, float z)>();
            for (int i = 0; i < v.Length; i++)
            {
                MapMesh.Vertex a = v[i], b = v[(i + 1) % v.Length];
                if (a.Y <= mid) below.Add((a.X, a.Z));
                if ((a.Y - mid) * (b.Y - mid) < 0)
                {
                    float t = (mid - a.Y) / (b.Y - a.Y);
                    below.Add((a.X + (b.X - a.X) * t, a.Z + (b.Z - a.Z) * t));
                }
            }
            return (below.Min(p => p.x), below.Max(p => p.x), below.Min(p => p.z), below.Max(p => p.z));
        }

        /// <summary>
        /// Materials laid flat only under standing things, such as a tree's shadow: nearly every square of them lies
        /// under something standing, and nothing standing wears them. They belong to those things, not to the ground.
        /// </summary>
        private static HashSet<int> ObjectFloors(MapMesh mesh)
        {
            const float Slack = 1e-3f;
            bool Flat(MapMesh.Face f) => f.Corners.All(c => Math.Abs(mesh.Vertices[c].Y - mesh.Vertices[f.Corners[0]].Y) < Slack);
            List<MapMesh.Face> faces = mesh.Faces.Where(f => f.Corners != null && f.Corners.Length >= 3).ToList();
            List<(float x0, float x1, float z0, float z1)> standing = faces.Where(f => Rises(mesh, f)).Select(f =>
            {
                MapMesh.Vertex[] v = f.Corners.Select(c => mesh.Vertices[c]).ToArray();
                return (v.Min(q => q.X), v.Max(q => q.X), v.Min(q => q.Z), v.Max(q => q.Z));
            }).Where(b => b.Item2 - b.Item1 > Slack && b.Item4 - b.Item3 > Slack).ToList();
            HashSet<int> worn = new HashSet<int>(faces.Where(f => !Flat(f)).Select(f => f.Material));
            Dictionary<int, (int under, int all)> count = new Dictionary<int, (int under, int all)>();
            foreach (MapMesh.Face f in faces.Where(Flat))
            {
                if (worn.Contains(f.Material)) continue;
                MapMesh.Vertex[] v = f.Corners.Select(c => mesh.Vertices[c]).ToArray();
                float x0 = v.Min(q => q.X), x1 = v.Max(q => q.X), z0 = v.Min(q => q.Z), z1 = v.Max(q => q.Z);
                (int under, int all) n = count.TryGetValue(f.Material, out (int, int) had) ? had : (0, 0);
                // The middles of the squares the face covers, or its own middle when it covers none.
                List<(float x, float z)> points = new List<(float x, float z)>();
                for (int gz = Square(z0); gz <= Square(z1); gz++)
                    for (int gx = Square(x0); gx <= Square(x1); gx++)
                    {
                        float cx = -HalfMap + (gx + 0.5f) * TileWidth, cz = -HalfMap + (gz + 0.5f) * TileWidth;
                        if (cx > x0 && cx < x1 && cz > z0 && cz < z1) points.Add((cx, cz));
                    }
                if (points.Count == 0) points.Add(((x0 + x1) / 2, (z0 + z1) / 2));
                foreach ((float x, float z) in points)
                {
                    n.all++;
                    if (standing.Any(b => x > b.x0 - Slack && x < b.x1 + Slack && z > b.z0 - Slack && z < b.z1 + Slack)) n.under++;
                }
                count[f.Material] = n;
            }
            return new HashSet<int>(count.Where(kv => kv.Value.all > 0 && kv.Value.under >= kv.Value.all * 0.9f).Select(kv => kv.Key));
        }

        public static MapTileset FromObj(string path, out string whynot, float scale = 1f)
        {
            whynot = null;
            ObjMesh mesh = ObjMesh.Read(path, out whynot);
            if (mesh == null) return null;
            if (mesh.Groups.Count == 0 && mesh.Faces.Count == 0)
            { whynot = "No faces in file."; return null; }

            MapTileset set = new MapTileset
            {
                Name = Path.GetFileNameWithoutExtension(path),
                PlacesArePartsOfThePicture = true,
            };

            List<ObjMesh.Group> groups = mesh.Groups.Count > 0
                ? mesh.Groups
                : new List<ObjMesh.Group> { new ObjMesh.Group { Name = set.Name, First = 0, Count = mesh.Faces.Count } };

            foreach (ObjMesh.Group group in groups)
            {
                if (group.Count <= 0) continue;

                List<ObjMesh.Face> faces = mesh.Faces.Skip(group.First).Take(group.Count).ToList();
                Tile tile = new Tile { Name = group.Name };

                bool alreadyPlaced = group.Wide > 0 && group.Deep > 0;
                float lowX = float.MaxValue, lowY = float.MaxValue, lowZ = float.MaxValue;
                foreach (ObjMesh.Face face in faces)
                    foreach (ObjMesh.Corner corner in face.Corners)
                    {
                        ObjMesh.Vec3 p = mesh.Positions[corner.Position];
                        lowX = Math.Min(lowX, p.X * scale);
                        lowY = Math.Min(lowY, p.Y * scale);
                        lowZ = Math.Min(lowZ, p.Z * scale);
                    }
                if (alreadyPlaced) { lowX = 0f; lowY = 0f; lowZ = 0f; }

                foreach (ObjMesh.Face face in faces)
                {
                    int[] corners = new int[face.Corners.Count];
                    for (int i = 0; i < face.Corners.Count; i++)
                    {
                        ObjMesh.Corner c = face.Corners[i];
                        ObjMesh.Vec3 p = mesh.Positions[c.Position];
                        ObjMesh.Vec2 uv = c.TexCoord >= 0 && c.TexCoord < mesh.TexCoords.Count
                            ? mesh.TexCoords[c.TexCoord] : new ObjMesh.Vec2();

                        corners[i] = tile.Corners.Count;
                        Corner made = new Corner
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
                            ObjMesh.Vec3 n = mesh.Normals[c.Normal];
                            made.Faces = true; made.NX = n.X; made.NY = n.Y; made.NZ = n.Z;
                        }
                        tile.Corners.Add(made);
                    }
                    ObjMesh.Material material = face.Material >= 0 && face.Material < mesh.Materials.Count
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
                        string[] parts = cell.Split(',');
                        if (parts.Length != 4 || !int.TryParse(parts[0], out int layer) || !int.TryParse(parts[1], out int cx)
                            || !int.TryParse(parts[2], out int cy)
                            || !int.TryParse(parts[3], NumberStyles.HexNumber, null, out int value)) continue;
                        int[,] grid = tile.CollisionGrid(layer);
                        if (cx >= 0 && cy >= 0 && cx < grid.GetLength(0) && cy < grid.GetLength(1)) grid[cx, cy] = value;
                    }

                tile.Seen = 0;
                if (set.Tiles.Any(t => t.Name == tile.Name)) tile.Name += $"_{set.Tiles.Count}";
                set.Tiles.Add(tile);
            }

            if (set.Tiles.Count == 0) { whynot = "No tiles in file."; return null; }

            Dictionary<string, int> index = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < set.Tiles.Count; i++) index[Safe(set.Tiles[i].Name)] = i;
            foreach (string said in mesh.SmartDrawings)
            {
                string[] names = said.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                SmartDrawing drawing = new SmartDrawing();
                for (int i = 0; i < names.Length && i < SmartDrawing.Slots; i++)
                    drawing[i] = index.TryGetValue(names[i], out int t) ? t : -1;
                if (!drawing.IsEmpty) set.SmartDrawings.Add(drawing);
            }
            return set;
        }

        public string SaveObj(string path)
        {
            if (Tiles.Count == 0) return "No tiles to export.";

            StringBuilder obj = new StringBuilder();
            StringBuilder mtl = new StringBuilder();
            CultureInfo inv = CultureInfo.InvariantCulture;
            string mtlPath = Path.ChangeExtension(path, ".mtl");

            obj.AppendLine("# DSPRE tileset: one object per tile.");
            obj.AppendLine($"mtllib {Path.GetFileName(mtlPath)}");

            foreach (SmartDrawing drawing in SmartDrawings)
                obj.AppendLine("# smart " + string.Join(" ", Enumerable.Range(0, SmartDrawing.Slots)
                    .Select(i => drawing[i] >= 0 && drawing[i] < Tiles.Count ? Safe(Tiles[drawing[i]].Name) : "-")));

            Dictionary<string, string> materialName = new Dictionary<string, string>(StringComparer.Ordinal);
            Dictionary<string, int> perPicture = new Dictionary<string, int>(StringComparer.Ordinal);
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
            foreach (Tile tile in Tiles)
            {
                obj.AppendLine($"o {Safe(tile.Name)}");
                obj.AppendLine($"# squares {tile.Wide} {tile.Deep}");
                if (tile.CollisionDefaults.Count > 0)
                {
                    obj.AppendLine($"# footprint {tile.CollisionWide} {tile.CollisionDeep} {tile.CollisionAnchorX} {tile.CollisionAnchorY}");
                    List<string> cells = new List<string>();
                    foreach ((int layer, int[,] grid) in tile.CollisionDefaults)
                        for (int x = 0; x < grid.GetLength(0); x++)
                            for (int y = 0; y < grid.GetLength(1); y++)
                                if (grid[x, y] >= 0) cells.Add($"{layer},{x},{y},{grid[x, y]:X2}");
                    obj.AppendLine("# collision " + string.Join(";", cells));
                }
                if (tile.Merges || tile.PictureAcrossTheMap || tile.OffsetX != 0 || tile.OffsetZ != 0)
                    obj.AppendLine(FormattableString.Invariant(
                        $"# tileable {B(tile.AcrossTileable)} {B(tile.DownTileable)} {B(tile.PictureRepeatsAcross)} {B(tile.PictureRepeatsDown)} {B(tile.PictureAcrossTheMap)} {tile.PictureScale} {tile.OffsetX} {tile.OffsetZ}"));

                foreach (Corner c in tile.Corners)
                {
                    string at = $"v {c.X.ToString("0.######", inv)} {c.Y.ToString("0.######", inv)} "
                              + $"{c.Z.ToString("0.######", inv)}";
                    if (c.Colour >= 0)
                        at += $" {((c.Colour & 31) / 31f).ToString("0.####", inv)} "
                            + $"{(((c.Colour >> 5) & 31) / 31f).ToString("0.####", inv)} "
                            + $"{(((c.Colour >> 10) & 31) / 31f).ToString("0.####", inv)}";
                    obj.AppendLine(at);
                }
                List<int> last = tile.Corners.Select((c, i) => (c, i)).Where(x => x.c.ColourLast).Select(x => corner + x.i).ToList();
                if (last.Count > 0) obj.AppendLine("# colourlast " + string.Join(" ", last));

                foreach (Corner c in tile.Corners)
                    obj.AppendLine($"vt {c.S.ToString("0.######", inv)} "
                                 + $"{(1f - c.T).ToString("0.######", inv)}");

                int[] facingOf = new int[tile.Corners.Count];
                int faced = 0;
                for (int i = 0; i < tile.Corners.Count; i++)
                {
                    Corner c = tile.Corners[i];
                    if (!c.Faces) { facingOf[i] = -1; continue; }
                    facingOf[i] = facing + faced++;
                    obj.AppendLine($"vn {c.NX.ToString("0.######", inv)} {c.NY.ToString("0.######", inv)} "
                                 + $"{c.NZ.ToString("0.######", inv)}");
                }

                string painting = null;
                foreach (Face face in tile.Faces)
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
                PlainFiles.WriteText(path, obj.ToString());
                PlainFiles.WriteText(mtlPath, mtl.ToString());
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
            Tile tile = new Tile();
            Dictionary<(int, int, int, int, int, string), int> where = new Dictionary<(int, int, int, int, int, string), int>();

            foreach (MapMesh.Face face in faces)
            {
                int[] corners = new int[face.Corners.Length];
                for (int i = 0; i < face.Corners.Length; i++)
                {
                    MapMesh.Vertex v = mesh.Vertices[face.Corners[i]];
                    (float s, float t) = face.OnPicture != null && i < face.OnPicture.Length
                        ? face.OnPicture[i] : (0f, 0f);

                    float x = v.X - originX, y = v.Y - baseY, z = v.Z - originZ;
                    Corner corner = new Corner { X = x, Y = y, Z = z, S = s, T = t };
                    CarryLight(corner, face, i);

                    (int, int, int, int, int, string) key = (Fine(x), Fine(y), Fine(z), Fine(s), Fine(t), LightKey(corner));
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
                (float nx, float ny, float nz) = Unpack(face.Normal[i]);
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
            StringBuilder sb = new StringBuilder();
            foreach (Face face in tile.Faces)
            {
                sb.Append(face.Picture).Append(':').Append(face.Palette).Append(':')
                  .Append(face.Look?.Key ?? "").Append('|');
                foreach (int i in face.Corners)
                {
                    Corner c = tile.Corners[i];
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
