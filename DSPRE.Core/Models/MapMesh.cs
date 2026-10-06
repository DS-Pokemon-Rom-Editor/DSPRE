using LibNDSFormats.NSBMD;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.Models
{
    /// <summary>Editable view of a map model; untouched shapes are written back byte for byte.</summary>
    public sealed class MapMesh
    {
        public sealed class Vertex
        {
            public float X, Y, Z;

            public List<(int shape, int corner)> Corners = new List<(int shape, int corner)>();

            public bool Moved;
        }

        public sealed class Face
        {
            public int Shape;
            public int Run;

            public int Material;

            public int[] Corners;

            public (float s, float t)[] OnPicture;

            public int[] Colour;
            public int[] Normal;
            public bool[] ColourLast;
        }

        public List<Vertex> Vertices { get; } = new List<Vertex>();
        public List<Face> Faces { get; } = new List<Face>();

        public float ModelScale { get; private set; }

        public IReadOnlyList<NsbmdFile.Shape> Shapes => _file.Shapes;

        private NsbmdFile _file;
        private readonly List<byte[]> _lists = new List<byte[]>();

        private readonly Dictionary<int, string> _repainted = new Dictionary<int, string>();
        private readonly Dictionary<int, string> _recoloured = new Dictionary<int, string>();

        public IReadOnlyList<NsbmdFile.Named> Pictures => _file.Pictures;
        public IReadOnlyList<NsbmdFile.Named> Colours => _file.Colours;

        public int PictureFor(int material) => Which(_file.Pictures, material);
        public int ColoursFor(int material) => Which(_file.Colours, material);

        private readonly Dictionary<int, MaterialLook> _looks = new Dictionary<int, MaterialLook>();

        public MaterialLook LookOf(int material)
        {
            if (!_looks.TryGetValue(material, out MaterialLook look))
                _looks[material] = look = MaterialLook.FromRecord(_file.MaterialRecord(material));
            return look;
        }

        private static int Which(IReadOnlyList<NsbmdFile.Named> among, int material)
        {
            for (int i = 0; i < among.Count; i++)
                if (among[i].Materials.Contains(material)) return i;
            return -1;
        }

        public string NameOfPicture(int picture)
            => picture < 0 || picture >= _file.Pictures.Count ? null
             : _repainted.TryGetValue(picture, out string now) ? now : _file.Pictures[picture].Name;

        public string NameOfColours(int colours)
            => colours < 0 || colours >= _file.Colours.Count ? null
             : _recoloured.TryGetValue(colours, out string now) ? now : _file.Colours[colours].Name;

        public void Repaint(int picture, string name)
        {
            if (picture < 0 || picture >= _file.Pictures.Count) return;
            if (_file.Pictures[picture].Name == name) { _repainted.Remove(picture); return; }

            _repainted[picture] = name;
        }

        public void Recolour(int colours, string name)
        {
            if (colours < 0 || colours >= _file.Colours.Count) return;
            if (_file.Colours[colours].Name == name) { _recoloured.Remove(colours); return; }
            _recoloured[colours] = name;
        }

        private MapMesh() { }

        public static MapMesh Read(byte[] model, out string whynot)
        {
            NsbmdFile file = NsbmdFile.Read(model, out whynot);
            if (file == null) return null;

            MapMesh mesh = new MapMesh { _file = file, ModelScale = file.ModelScale };

            int[] material = new int[file.Shapes.Count];
            int[] stack = new int[file.Shapes.Count];
            for (int i = 0; i < material.Length; i++) { material[i] = -1; stack[i] = 0; }
            try
            {
                using MemoryStream stream = new MemoryStream(model);
                NSBMD known = NSBMDLoader.LoadNSBMD(stream);
                List<NSBMDPolygon> polygons = known?.models?.FirstOrDefault()?.Polygons;
                if (polygons != null)
                {
                    List<NSBMDPolygon> withData = polygons.Where(p => p.PolyData != null).ToList();
                    for (int i = 0; i < material.Length && i < withData.Count; i++)
                    {
                        material[i] = withData[i].MatId;
                        stack[i] = withData[i].StackID;
                    }
                }
            }
            catch { }

            Dictionary<(int stack, int x, int y, int z), int> joined = new Dictionary<(int stack, int x, int y, int z), int>();

            for (int shape = 0; shape < file.Shapes.Count; shape++)
            {
                byte[] dl = file.DisplayList(shape);
                mesh._lists.Add(dl);

                DisplayListWalk walk = DisplayListWalk.Read(dl, out string why);
                if (walk == null) { whynot = $"Shape {shape}: {why}"; return null; }

                for (int r = 0; r < walk.Runs.Count; r++)
                {
                    DisplayListWalk.Run run = walk.Runs[r];
                    int[] here = new int[run.Corners.Count];

                    for (int c = 0; c < run.Corners.Count; c++)
                    {
                        DisplayListWalk.Corner corner = run.Corners[c];
                        // Weld only corners on the same spot and matrix.
                        (int, int RawX, int RawY, int RawZ) key = (stack[shape], corner.RawX, corner.RawY, corner.RawZ);
                        if (!joined.TryGetValue(key, out int at))
                        {
                            at = mesh.Vertices.Count;
                            mesh.Vertices.Add(new Vertex { X = corner.X, Y = corner.Y, Z = corner.Z });
                            joined[key] = at;
                        }
                        mesh.Vertices[at].Corners.Add((shape, corner.Index));
                        here[c] = at;
                    }

                    foreach (int[] face in DisplayListWalk.Faces(run))
                    {
                        mesh.Faces.Add(new Face
                        {
                            Shape = shape,
                            Run = r,
                            Material = material[shape],
                            Corners = face.Select(i => here[i]).ToArray(),
                            OnPicture = face.Select(i => (run.Corners[i].S, run.Corners[i].T)).ToArray(),
                            Colour = face.Select(i => run.Corners[i].Colour).ToArray(),
                            Normal = face.Select(i => run.Corners[i].Normal).ToArray(),
                            ColourLast = face.Select(i => run.Corners[i].ColourLast).ToArray(),
                        });
                    }
                }
            }

            return mesh;
        }

        public void Move(int vertex, float x, float y, float z)
        {
            if (vertex < 0 || vertex >= Vertices.Count) return;
            Vertex v = Vertices[vertex];
            if (v.X == x && v.Y == y && v.Z == z) return;
            v.X = x; v.Y = y; v.Z = z; v.Moved = true;
        }

        public bool AnythingMoved => Vertices.Any(v => v.Moved);

        public bool AnythingRepainted => _repainted.Count > 0 || _recoloured.Count > 0;

        public bool AnythingChanged => AnythingMoved || AnythingRepainted || _rebuilt.Count > 0;

        public IEnumerable<int> TouchedShapes => Vertices
            .Where(v => v.Moved)
            .SelectMany(v => v.Corners.Select(c => c.shape))
            .Concat(_rebuilt)
            .Distinct()
            .OrderBy(x => x);

        private readonly HashSet<int> _rebuilt = new HashSet<int>();

        public bool DeleteFace(int face)
        {
            if (face < 0 || face >= Faces.Count) return false;
            _rebuilt.Add(Faces[face].Shape);
            Faces.RemoveAt(face);
            return true;
        }

        public int AddFace(int[] corners, int like, int pictureWide = 0, int pictureTall = 0)
        {
            if (corners == null || corners.Length < 3 || corners.Length > 4) return -1;
            if (corners.Any(c => c < 0 || c >= Vertices.Count) || corners.Distinct().Count() != corners.Length) return -1;
            if (like < 0 || like >= Faces.Count) return -1;
            Face model = Faces[like];

            Vertex[] p = corners.Select(c => Vertices[c]).ToArray();
            float ux = p[1].X - p[0].X, uy = p[1].Y - p[0].Y, uz = p[1].Z - p[0].Z;
            float vx = p[2].X - p[0].X, vy = p[2].Y - p[0].Y, vz = p[2].Z - p[0].Z;
            float nx = Math.Abs(uy * vz - uz * vy), ny = Math.Abs(uz * vx - ux * vz), nz = Math.Abs(ux * vy - uy * vx);
            int w = pictureWide > 0 ? pictureWide : 32, h = pictureTall > 0 ? pictureTall : 32;
            (float s, float t) Place(Vertex v)
            {
                float a, b;
                if (ny >= nx && ny >= nz) { a = v.X; b = v.Z; }
                else if (nx >= nz) { a = v.Z; b = -v.Y; }
                else { a = v.X; b = -v.Y; }
                return (a / MapTileset.TileWidth * w, b / MapTileset.TileWidth * h);
            }

            Faces.Add(new Face
            {
                Shape = model.Shape,
                Run = -1,
                Material = model.Material,
                Corners = (int[])corners.Clone(),
                OnPicture = p.Select(Place).ToArray(),
                Colour = corners.Select(_ => model.Colour?.FirstOrDefault() ?? -1).ToArray(),
                Normal = corners.Select(_ => model.Normal?.FirstOrDefault() ?? -1).ToArray(),
                ColourLast = corners.Select(_ => model.ColourLast?.FirstOrDefault() ?? false).ToArray(),
            });
            _rebuilt.Add(model.Shape);
            return Faces.Count - 1;
        }

        private byte[] Emit(int shape)
        {
            GxDisplayList dl = new GxDisplayList();
            foreach (int restore in Restores(_lists[shape])) dl.RestoreMatrix(restore);

            MaterialLook look = Faces.Where(f => f.Shape == shape).Select(f => f.Material).FirstOrDefault(m => m >= 0) is int mat && mat >= 0
                ? LookOf(mat) : null;
            int colourNow = look?.CornerColour ?? -1, normalNow = -1;
            bool colourWasLast = true;
            uint placeNow = uint.MaxValue;

            foreach (int count in new[] { 3, 4 })
            {
                List<Face> faces = Faces.Where(f => f.Shape == shape && f.Corners.Length == count).ToList();
                if (faces.Count == 0) continue;
                dl.Begin(count == 3 ? GxDisplayList.Shape.Triangles : GxDisplayList.Shape.Quads);
                foreach (Face f in faces)
                    for (int i = 0; i < count; i++)
                    {
                        int colour = f.Colour != null && f.Colour[i] >= 0 ? f.Colour[i] : look?.CornerColour ?? -1;
                        int normal = f.Normal?[i] ?? -1;
                        bool last = f.ColourLast?[i] ?? false;
                        if (last)
                        {
                            if (normal >= 0 && normal != normalNow) { dl.Command(GxDisplayList.Normal, (uint)normal); normalNow = normal; colourWasLast = false; }
                            if (colour >= 0 && (colour != colourNow || !colourWasLast)) { dl.Command(GxDisplayList.Color, (uint)colour); colourNow = colour; colourWasLast = true; }
                        }
                        else
                        {
                            if (colour >= 0 && colour != colourNow) { dl.Command(GxDisplayList.Color, (uint)colour); colourNow = colour; colourWasLast = true; }
                            if (normal >= 0 && (normal != normalNow || colourWasLast)) { dl.Command(GxDisplayList.Normal, (uint)normal); normalNow = normal; colourWasLast = false; }
                        }

                        (float ps, float pt) = f.OnPicture != null && i < f.OnPicture.Length ? f.OnPicture[i] : (0f, 0f);
                        int s16 = (int)Math.Round(ps * 16f), t16 = (int)Math.Round(pt * 16f);
                        uint place = (uint)((s16 & 0xffff) | ((t16 & 0xffff) << 16));
                        if (place != placeNow) { dl.Command(GxDisplayList.TexCoord, place); placeNow = place; }

                        Vertex v = Vertices[f.Corners[i]];
                        dl.AddVertexRaw(Raw(v.X), Raw(v.Y), Raw(v.Z));
                    }
                dl.End();
            }
            return dl.ToBytes();
        }

        private static int Raw(float v) => Math.Clamp((int)Math.Round(v * 4096f), short.MinValue, short.MaxValue);

        private static List<int> Restores(byte[] dl)
        {
            List<int> found = new List<int>();
            int at = 0;
            while (at + 4 <= dl.Length)
            {
                byte[] ops = new[] { dl[at], dl[at + 1], dl[at + 2], dl[at + 3] };
                at += 4;
                foreach (byte op in ops)
                {
                    int words = GxDisplayList.TryParamWords(op);
                    if (words < 0 || op == GxDisplayList.BeginVtxs) return found;
                    if (op == GxDisplayList.MtxRestore && at + 4 <= dl.Length) found.Add(BitConverter.ToInt32(dl, at) & 31);
                    at += words * 4;
                }
            }
            return found;
        }

        private bool _rescaled;

        /// <summary>Converts positions to another model scale. The mesh can no longer be saved, since its display lists keep the old values.</summary>
        public void ScaleTo(float scale)
        {
            if (scale <= 0f || ModelScale <= 0f || ModelScale == scale) return;
            float k = ModelScale / scale;
            foreach (Vertex v in Vertices) { v.X *= k; v.Y *= k; v.Z *= k; }
            ModelScale = scale;
            _rescaled = true;
        }

        public byte[] Save(out string whynot)
        {
            whynot = null;
            if (_rescaled) { whynot = "This model was rescaled and cannot be saved."; return null; }
            Dictionary<int, byte[]> replaced = new Dictionary<int, byte[]>();

            foreach (int shape in TouchedShapes)
            {
                if (_rebuilt.Contains(shape)) { replaced[shape] = Emit(shape); continue; }
                Dictionary<int, (float x, float y, float z)> moved = new Dictionary<int, (float x, float y, float z)>();
                foreach (Vertex v in Vertices)
                    foreach ((int s, int corner) in v.Corners)
                        if (s == shape) moved[corner] = (v.X, v.Y, v.Z);

                byte[] written = ShapeRewrite.WithCornersAt(_lists[shape], moved, out string why);
                if (written == null)
                {
                    whynot = $"{_file.Shapes[shape].Name}: {why}";
                    return null;
                }
                replaced[shape] = written;
            }

            byte[] made = _file.With(replaced, out whynot);
            if (made == null) return null;

            if (!_file.ApplyNamesTo(made, _repainted, _recoloured, out whynot)) return null;
            return made;
        }
    }
}
