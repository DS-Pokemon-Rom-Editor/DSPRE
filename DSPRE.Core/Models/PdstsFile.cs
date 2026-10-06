using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DSPRE.Models
{
    /// <summary>Reads PDSMS .pdsts tilesets.</summary>
    public static class PdstsFile
    {
        private const byte TileAt = 0, ImageName = 1, PaletteNameForModel = 2, PictureNameForModel = 3;
        private const byte MaterialName = 4, MaterialStart = 9, MaterialEnd = 10;
        private const byte Wide = 11, Deep = 12, XTileable = 13, YTileable = 14;
        private const byte Corners = 15, Places = 16, FacesQuad = 17, FacesTri = 18;
        private const byte WhichMaterials = 19, ObjectName = 21, Normals = 22, ZOffset = 23;
        private const byte QuadStarts = 24, TriStarts = 25, SmartGrid = 26;
        private const byte GlobalMapping = 27, GlobalScale = 28;
        private const byte Fog = 30, BothFaces = 31, NormalOrient = 32, Alpha = 33, TexGen = 34;
        private const byte IncludeInModel = 35, UTileable = 36, VTileable = 37;
        private const byte XOffset = 38, YOffset = 39, TilingU = 40, TilingV = 41, ColourFormat = 42;
        private const byte Light0 = 43, Light1 = 44, Light2 = 45, Light3 = 46;
        private const byte RenderBorder = 47, VertexColours = 48, Colours = 49;
        private const byte FacesQuadExtended = 50, FacesTriExtended = 51;
        private const byte Diffuse = 53, Ambient = 54, Specular = 55, Emission = 56;

        private sealed class Material
        {
            public string Picture = "", Palette = "", Name = "";

            public string Image = "";

            public bool Fog = true, BothSides, Outlined, VertexColours;
            public bool[] Lights = { true, false, false, false };
            public int Alpha = 31, PlacesFrom, TilingAcross, TilingDown;
            public (int, int, int) Diffuse = (25, 25, 25), Ambient = (31, 31, 31), Specular, Emission;

            private MaterialLook _look;
            public MaterialLook Look => _look ??= MaterialLook.Made(Lights, BothSides, Alpha, Fog, Outlined,
                (MaterialLook.Tiling)TilingAcross, (MaterialLook.Tiling)TilingDown, PlacesFrom,
                Diffuse, Ambient, Specular, Emission);

            public bool Lit => Lights.Any(l => l);
        }

        private sealed class Piece
        {
            public int Wide = 1, Deep = 1;
            public string Name = "";
            public float[] Corners = Array.Empty<float>();
            public float[] Places = Array.Empty<float>();
            public List<int[]> Quads = new List<int[]>();
            public List<int[]> Tris = new List<int[]>();
            public int[] Materials = Array.Empty<int>();
            public int[] QuadStarts = Array.Empty<int>();
            public int[] TriStarts = Array.Empty<int>();
            public float XOffset, YOffset, ZOffset;
            public bool XTileable, YTileable, UTileable, VTileable, GlobalMapping;
            public float GlobalScale = 1f;
            public float[] Normals = Array.Empty<float>();
            public float[] Colours = Array.Empty<float>();
        }

        public static MapTileset Read(string path, out string whynot, float squareSize = 0f)
        {
            whynot = null;
            byte[] bytes;
            try { bytes = File.ReadAllBytes(path); }
            catch (Exception ex) { whynot = "Could not read file: " + ex.Message; return null; }

            float perSquare = squareSize > 0f ? squareSize : MapTileset.TileWidth;

            List<Material> materials = new List<Material>();
            List<Piece> pieces = new List<Piece>();
            List<List<int[]>> smart = new List<List<int[]>>();
            Material material = null;
            Piece piece = null;

            int at = 0;
            while (at < bytes.Length)
            {
                byte tag = bytes[at++];
                if (at + 4 > bytes.Length) break;
                int count = Int(bytes, ref at);
                if (count < 0) { whynot = $"Invalid element count {count}."; return null; }

                switch (tag)
                {
                    case MaterialStart:
                        Skip(bytes, ref at, count * 4);
                        material = new Material();
                        materials.Add(material);
                        break;

                    case MaterialEnd:
                        Skip(bytes, ref at, count * 4);
                        material = null;
                        break;

                    case TileAt:
                        Skip(bytes, ref at, count * 4);
                        piece = new Piece();
                        pieces.Add(piece);
                        break;

                    case ImageName:
                        {
                            string s = Text(bytes, ref at, count);
                            if (material != null) { material.Image = s; material.Picture = s; }
                            break;
                        }
                    case PictureNameForModel:
                        { string s = Text(bytes, ref at, count); if (material != null && s.Length > 0) material.Picture = s; break; }
                    case PaletteNameForModel:
                        { string s = Text(bytes, ref at, count); if (material != null) material.Palette = s; break; }
                    case MaterialName:
                        { string s = Text(bytes, ref at, count); if (material != null) material.Name = s; break; }
                    case ObjectName:
                        { string s = Text(bytes, ref at, count); if (piece != null) piece.Name = s; break; }

                    case Wide:
                        { int[] v = Ints(bytes, ref at, count); if (piece != null && v.Length > 0) piece.Wide = v[0]; break; }
                    case Deep:
                        { int[] v = Ints(bytes, ref at, count); if (piece != null && v.Length > 0) piece.Deep = v[0]; break; }

                    case Corners:
                        { float[] v = Floats(bytes, ref at, count); if (piece != null) piece.Corners = v; break; }
                    case Places:
                        { float[] v = Floats(bytes, ref at, count); if (piece != null) piece.Places = v; break; }

                    case WhichMaterials:
                        { int[] v = Ints(bytes, ref at, count); if (piece != null) piece.Materials = v; break; }
                    case QuadStarts:
                        { int[] v = Ints(bytes, ref at, count); if (piece != null) piece.QuadStarts = v; break; }
                    case TriStarts:
                        { int[] v = Ints(bytes, ref at, count); if (piece != null) piece.TriStarts = v; break; }

                    case XOffset:
                        { float[] v = Floats(bytes, ref at, count); if (piece != null && v.Length > 0) piece.XOffset = v[0]; break; }
                    case YOffset:
                        { float[] v = Floats(bytes, ref at, count); if (piece != null && v.Length > 0) piece.YOffset = v[0]; break; }
                    case ZOffset:
                        { float[] v = Floats(bytes, ref at, count); if (piece != null && v.Length > 0) piece.ZOffset = v[0]; break; }

                    case FacesQuad:
                        { List<int[]> f = Faces(bytes, ref at, count, 4, false); if (piece != null) piece.Quads = f; break; }
                    case FacesTri:
                        { List<int[]> f = Faces(bytes, ref at, count, 3, false); if (piece != null) piece.Tris = f; break; }
                    case FacesQuadExtended:
                        { List<int[]> f = Faces(bytes, ref at, count, 4, true); if (piece != null) piece.Quads = f; break; }
                    case FacesTriExtended:
                        { List<int[]> f = Faces(bytes, ref at, count, 3, true); if (piece != null) piece.Tris = f; break; }

                    case SmartGrid:
                        {
                            List<int[]> grid = new List<int[]>();
                            for (int i = 0; i < count && at + 4 <= bytes.Length; i++)
                            {
                                int inner = Int(bytes, ref at);
                                if (inner < 0 || at + inner * 4 > bytes.Length) { at = bytes.Length + 1; break; }
                                int[] column = new int[inner];
                                for (int j = 0; j < inner; j++) column[j] = Int(bytes, ref at);
                                grid.Add(column);
                            }
                            smart.Add(grid);
                            break;
                        }

                    case Diffuse: case Ambient: case Specular: case Emission:
                        {
                            int from = at;
                            Skip(bytes, ref at, count);
                            if (material == null || count < 3 || at > bytes.Length) break;
                            (int, int, int) rgb = (bytes[from] & 31, bytes[from + 1] & 31, bytes[from + 2] & 31);
                            if (tag == Diffuse) material.Diffuse = rgb;
                            else if (tag == Ambient) material.Ambient = rgb;
                            else if (tag == Specular) material.Specular = rgb;
                            else material.Emission = rgb;
                            break;
                        }

                    case Normals:
                        { float[] v = Floats(bytes, ref at, count); if (piece != null) piece.Normals = v; break; }
                    case Colours:
                        { float[] v = Floats(bytes, ref at, count); if (piece != null) piece.Colours = v; break; }

                    case Fog: case BothFaces: case Alpha: case TexGen: case TilingU: case TilingV:
                    case Light0: case Light1: case Light2: case Light3: case RenderBorder: case VertexColours:
                        {
                            int[] v = Ints(bytes, ref at, count);
                            if (material == null || v.Length == 0) break;
                            int n = v[0];
                            switch (tag)
                            {
                                case Fog: material.Fog = n != 0; break;
                                case BothFaces: material.BothSides = n != 0; break;
                                case Alpha: material.Alpha = n; break;
                                case TexGen: material.PlacesFrom = n; break;
                                case TilingU: material.TilingAcross = n; break;
                                case TilingV: material.TilingDown = n; break;
                                case Light0: material.Lights[0] = n != 0; break;
                                case Light1: material.Lights[1] = n != 0; break;
                                case Light2: material.Lights[2] = n != 0; break;
                                case Light3: material.Lights[3] = n != 0; break;
                                case RenderBorder: material.Outlined = n != 0; break;
                                case VertexColours: material.VertexColours = n != 0; break;
                            }
                            break;
                        }

                    case XTileable: case YTileable: case UTileable: case VTileable: case GlobalMapping:
                        {
                            int[] v = Ints(bytes, ref at, count);
                            if (piece == null || v.Length == 0) break;
                            bool on = v[0] != 0;
                            if (tag == XTileable) piece.XTileable = on;
                            else if (tag == YTileable) piece.YTileable = on;
                            else if (tag == UTileable) piece.UTileable = on;
                            else if (tag == VTileable) piece.VTileable = on;
                            else piece.GlobalMapping = on;
                            break;
                        }
                    case GlobalScale:
                        { float[] v = Floats(bytes, ref at, count); if (piece != null && v.Length > 0) piece.GlobalScale = v[0]; break; }

                    case NormalOrient: case IncludeInModel: case ColourFormat:
                        Skip(bytes, ref at, count * 4);
                        break;

                    default:
                        whynot = $"Unknown tag 0x{tag:X2}.";
                        return null;
                }

                if (at > bytes.Length) { whynot = "Unexpected end of file."; return null; }
            }

            if (pieces.Count == 0) { whynot = "No tiles in file."; return null; }

            MapTileset set = new MapTileset
            {
                Name = Path.GetFileNameWithoutExtension(path),
                PlacesArePartsOfThePicture = true,
            };

            string folder = Path.GetDirectoryName(path) ?? ".";
            foreach (Material m in materials)
                set.PictureIsAt(string.IsNullOrEmpty(m.Picture) ? m.Name : m.Picture, folder, m.Image);

            int[] tileOfPiece = new int[pieces.Count];
            for (int i = 0; i < pieces.Count; i++)
            {
                MapTileset.Tile made = Build(pieces[i], materials, perSquare);
                tileOfPiece[i] = made == null ? -1 : set.Tiles.Count;
                if (made == null) continue;
                if (set.Tiles.Any(t => t.Name == made.Name)) made.Name += $"_{set.Tiles.Count}";
                set.Tiles.Add(made);
            }

            if (set.Tiles.Count == 0) { whynot = "No usable tiles in file."; return null; }
            if (set.Tiles.Count != pieces.Count) set.TileOfListed = tileOfPiece;

            foreach (List<int[]> grid in smart)
            {
                SmartDrawing drawing = new SmartDrawing();
                for (int across = 0; across < grid.Count && across < SmartDrawing.Wide; across++)
                    for (int down = 0; down < grid[across].Length && down < SmartDrawing.Tall; down++)
                    {
                        int listed = grid[across][down];
                        drawing[across, down] = listed >= 0 && listed < tileOfPiece.Length ? tileOfPiece[listed] : -1;
                    }
                if (!drawing.IsEmpty) set.SmartDrawings.Add(drawing);
            }
            return set;
        }

        private static MapTileset.Tile Build(Piece piece, List<Material> materials, float perSquare)
        {
            if (piece.Corners.Length < 9) return null;

            MapTileset.Tile tile = new MapTileset.Tile
            {
                Name = string.IsNullOrEmpty(piece.Name) ? "tile" : piece.Name,
                Wide = Math.Max(1, Math.Min(MapTileset.MostSquares, piece.Wide)),
                Deep = Math.Max(1, Math.Min(MapTileset.MostSquares, piece.Deep)),
                AcrossTileable = piece.XTileable, DownTileable = piece.YTileable,
                PictureRepeatsAcross = piece.UTileable, PictureRepeatsDown = piece.VTileable,
                PictureAcrossTheMap = piece.GlobalMapping, PictureScale = piece.GlobalScale,
                OffsetX = piece.XOffset * perSquare,
                OffsetZ = -piece.YOffset * perSquare,
            };

            Dictionary<(int corner, int place, int facing, int colour), int> where = new Dictionary<(int corner, int place, int facing, int colour), int>();

            void Add(List<int[]> faces, int[] starts, int perFace)
            {
                for (int f = 0; f < faces.Count; f++)
                {
                    int[] face = faces[f];
                    Material material = MaterialFor(materials, piece.Materials, starts, f);

                    int[] corners = new int[perFace];
                    for (int i = 0; i < perFace; i++)
                    {
                        // Face indices are 1-based.
                        int corner = face[i] - 1;
                        int place = face[perFace + i] - 1;
                        int facing = face.Length > perFace * 2 + i ? face[perFace * 2 + i] - 1 : -1;
                        int colour = face.Length > perFace * 3 + i ? face[perFace * 3 + i] - 1 : 0;
                        (int colour, bool faces, float nx, float ny, float nz) light = Light(piece, material, facing, colour);
                        if (!where.TryGetValue((corner, place, facing, colour), out int index))
                        {
                            index = tile.Corners.Count;
                            tile.Corners.Add(new MapTileset.Corner
                            {
                                // PDSMS tiles are Z-up with +Y north.
                                X = (At(piece.Corners, corner, 0) + piece.XOffset) * perSquare,
                                Y = (At(piece.Corners, corner, 2) + piece.ZOffset) * perSquare,
                                Z = (tile.Deep - (At(piece.Corners, corner, 1) + piece.YOffset)) * perSquare,
                                S = piece.Places.Length > place * 2 + 1 ? piece.Places[place * 2] : 0f,

                                T = piece.Places.Length > place * 2 + 1 ? 1f - piece.Places[place * 2 + 1] : 0f,
                                Colour = light.colour,
                                ColourLast = light.colour >= 0 && !light.faces,
                                Faces = light.faces, NX = light.nx, NY = light.ny, NZ = light.nz,
                            });
                            where[(corner, place, facing, colour)] = index;
                        }
                        corners[i] = index;
                    }

                    tile.Faces.Add(new MapTileset.Face
                    {
                        Corners = corners,
                        Picture = material?.Picture ?? "",
                        Palette = material?.Palette ?? "",
                        Look = material?.Look,
                    });
                }
            }

            Add(piece.Quads, piece.QuadStarts, 4);
            Add(piece.Tris, piece.TriStarts, 3);
            if (tile.Faces.Count == 0) return null;

            return tile;
        }

        private static Material MaterialFor(List<Material> materials, int[] which, int[] starts, int face)
        {
            int group = -1;
            for (int i = 0; i < starts.Length; i++)
                if (starts[i] <= face) group = i;

            if (group < 0 || group >= which.Length) return materials.FirstOrDefault();
            int at = which[group];
            return at >= 0 && at < materials.Count ? materials[at] : materials.FirstOrDefault();
        }

        private static (int colour, bool faces, float nx, float ny, float nz) Light(Piece piece, Material material,
                                                                                    int facing, int colour)
        {
            int ownColour = -1;
            if (material != null && material.VertexColours && colour >= 0 && piece.Colours.Length >= colour * 3 + 3)
            {
                int R(float v) => (int)Math.Round(Math.Clamp(v, 0f, 1f) * 31f);
                ownColour = R(piece.Colours[colour * 3]) | (R(piece.Colours[colour * 3 + 1]) << 5)
                          | (R(piece.Colours[colour * 3 + 2]) << 10);
            }

            if (material == null || !material.Lit || facing < 0 || piece.Normals.Length < facing * 3 + 3)
                return (ownColour, false, 0, 0, 0);

            float x = piece.Normals[facing * 3], y = piece.Normals[facing * 3 + 1], z = piece.Normals[facing * 3 + 2];
            return (ownColour, true, x, z, -y);
        }

        private static float At(float[] corners, int corner, int axis)
            => corners.Length > corner * 3 + 2 ? corners[corner * 3 + axis] : 0f;

        private static List<int[]> Faces(byte[] b, ref int at, int count, int perFace, bool extended)
        {
            int runs = extended ? 4 : 3;
            List<int[]> faces = new List<int[]>();
            for (int i = 0; i < count; i++)
            {
                int[] face = new int[perFace * runs];
                for (int k = 0; k < face.Length; k++)
                    face[k] = at + 4 <= b.Length ? Int(b, ref at) : 0;
                faces.Add(face);
            }
            return faces;
        }

        private static int Int(byte[] b, ref int at)
        {
            if (at + 4 > b.Length) { at = b.Length + 1; return 0; }
            int v = (b[at] << 24) | (b[at + 1] << 16) | (b[at + 2] << 8) | b[at + 3];
            at += 4;
            return v;
        }

        private static int[] Ints(byte[] b, ref int at, int count)
        {
            int[] v = new int[Math.Max(0, count)];
            for (int i = 0; i < v.Length; i++) v[i] = Int(b, ref at);
            return v;
        }

        private static float[] Floats(byte[] b, ref int at, int count)
        {
            float[] v = new float[Math.Max(0, count)];
            for (int i = 0; i < v.Length; i++) v[i] = BitConverter.Int32BitsToSingle(Int(b, ref at));
            return v;
        }

        private static string Text(byte[] b, ref int at, int count)
        {
            if (count < 0 || at + count > b.Length) { at = b.Length + 1; return ""; }
            string s = Encoding.ASCII.GetString(b, at, count);
            at += count;
            return s;
        }

        private static void Skip(byte[] b, ref int at, int bytes) => at += Math.Max(0, bytes);
    }
}
