using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace DSPRE.Models
{
    /// <summary>Exports a map model as OBJ + MTL.</summary>
    public static class ObjWrite
    {
        public sealed class Paint
        {
            public string Name;
            public int Width = 1, Height = 1;

            public string Palette;

            public string Picture;
        }

        public sealed class Result
        {
            public string Obj;
            public string Mtl;
            public int Faces, Corners, Materials;
            public string Whynot;
            public List<string> Notes = new List<string>();
        }

        public static Result To(string objPath, MapMesh mesh, IReadOnlyDictionary<int, Paint> paints)
        {
            var r = new Result();
            if (mesh == null) { r.Whynot = "No map."; return r; }
            if (mesh.Faces.Count == 0) { r.Whynot = "Map has no faces."; return r; }

            string mtlPath = Path.ChangeExtension(objPath, ".mtl");
            var obj = new StringBuilder();
            var mtl = new StringBuilder();
            var inv = CultureInfo.InvariantCulture;

            obj.AppendLine("# Written by DSPRE. Keep material names: they match the area's textures.");
            obj.AppendLine($"mtllib {Path.GetFileName(mtlPath)}");

            foreach (var v in mesh.Vertices)
                obj.AppendLine($"v {v.X.ToString("0.######", inv)} {v.Y.ToString("0.######", inv)} "
                             + $"{v.Z.ToString("0.######", inv)}");

            var facing = new Dictionary<int, int>();
            foreach (var face in mesh.Faces)
                if (face.Normal != null)
                    foreach (int word in face.Normal)
                        if (word >= 0 && !facing.ContainsKey(word))
                        {
                            facing[word] = facing.Count + 1;
                            var (nx, ny, nz) = MapTileset.Unpack(word);
                            obj.AppendLine($"vn {nx.ToString("0.######", inv)} {ny.ToString("0.######", inv)} {nz.ToString("0.######", inv)}");
                        }

            int texture = 0;
            var uvOf = new Dictionary<(int face, int corner), int>();
            foreach (var face in mesh.Faces)
            {
                var paint = Paint0(paints, face.Material);
                float w = Math.Max(1, paint.Width), h = Math.Max(1, paint.Height);
                for (int c = 0; c < face.Corners.Length; c++)
                {
                    var (s, t) = face.OnPicture != null && c < face.OnPicture.Length
                        ? face.OnPicture[c] : (0f, 0f);

                    float u = s / w, vv = 1f - t / h;
                    obj.AppendLine($"vt {u.ToString("0.######", inv)} {vv.ToString("0.######", inv)}");
                    uvOf[(r.Faces, c)] = ++texture;
                }
                r.Faces++;
                r.Corners += face.Corners.Length;
            }

            var nameOf = new Dictionary<int, string>();
            var taken = new HashSet<string>(StringComparer.Ordinal);
            string MaterialName(int material)
            {
                if (nameOf.TryGetValue(material, out string had)) return had;
                var paint = Paint0(paints, material);
                string name = paint.Name;
                for (int n = 2; !taken.Add(name); n++) name = $"{paint.Name}~{n}";
                nameOf[material] = name;

                mtl.AppendLine($"newmtl {name}");
                mtl.AppendLine("Kd 1.000 1.000 1.000");
                if (name != paint.Name) mtl.AppendLine($"# picture {paint.Name}");
                if (!string.IsNullOrEmpty(paint.Palette)) mtl.AppendLine($"# palette {paint.Palette}");
                var look = material >= 0 ? mesh.LookOf(material) : null;
                if (look != null) mtl.AppendLine($"# look {look.Key}");
                if (!string.IsNullOrEmpty(paint.Picture)) mtl.AppendLine($"map_Kd {paint.Picture}");
                mtl.AppendLine();
                r.Materials++;
                return name;
            }

            int current = int.MinValue;
            int at = 0;
            foreach (var face in mesh.Faces)
            {
                if (face.Material != current)
                {
                    current = face.Material;
                    obj.AppendLine($"usemtl {MaterialName(face.Material)}");
                }

                var corners = new List<string>();
                for (int c = 0; c < face.Corners.Length; c++)
                {
                    int word = face.Normal != null && c < face.Normal.Length ? face.Normal[c] : -1;
                    corners.Add(word >= 0 ? $"{face.Corners[c] + 1}/{uvOf[(at, c)]}/{facing[word]}"
                                          : $"{face.Corners[c] + 1}/{uvOf[(at, c)]}");
                }
                if (face.Colour != null && face.Colour.Any(v => v >= 0))
                    obj.AppendLine("# colours " + string.Join(" ", face.Colour.Select((v, i) =>
                        v.ToString(inv) + (face.ColourLast != null && face.ColourLast[i] ? "!" : ""))));
                obj.AppendLine("f " + string.Join(" ", corners));
                at++;
            }

            try
            {
                File.WriteAllText(objPath, obj.ToString());
                File.WriteAllText(mtlPath, mtl.ToString());
            }
            catch (Exception ex) { r.Whynot = ex.Message; return r; }

            r.Obj = objPath;
            r.Mtl = mtlPath;
            return r;
        }

        private static Paint Paint0(IReadOnlyDictionary<int, Paint> paints, int material)
        {
            if (paints != null && paints.TryGetValue(material, out var paint) && paint != null) return paint;
            return new Paint { Name = material >= 0 ? $"material{material}" : "nothing" };
        }
    }
}
