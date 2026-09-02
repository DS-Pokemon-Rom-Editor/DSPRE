using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// A mesh read out of a Wavefront OBJ, with the materials its MTL names. Only what a DS model can
    /// carry is kept: where each corner sits, which way it faces, where it lands on its picture, and
    /// what colour and picture the face is drawn with.
    /// </summary>
    public sealed class ObjMesh
    {
        public struct Vec3 { public float X, Y, Z; }
        public struct Vec2 { public float U, V; }

        /// <summary>One corner of one face.</summary>
        public struct Corner
        {
            public int Position;    // into Positions
            public int Normal;      // into Normals, or -1
            public int TexCoord;    // into TexCoords, or -1
        }

        public sealed class Face
        {
            public List<Corner> Corners = new();
            public int Material;    // into Materials
        }

        public sealed class Material
        {
            public string Name = "";
            /// <summary>The picture this is painted with, as a path beside the OBJ. Null when none.</summary>
            public string TexturePath;
            /// <summary>The flat colour, when there is no picture. Nought to one each.</summary>
            public float Red = 1, Green = 1, Blue = 1;
            /// <summary>How see-through it is, nought to one, where one is solid.</summary>
            public float Opacity = 1;
        }

        public List<Vec3> Positions { get; } = new();
        public List<Vec3> Normals { get; } = new();
        public List<Vec2> TexCoords { get; } = new();
        public List<Face> Faces { get; } = new();
        public List<Material> Materials { get; } = new();

        /// <summary>What the OBJ called itself, which becomes the model's name.</summary>
        public string Name = "model";

        /// <summary>Anything worth saying about what was read, or quietly left out.</summary>
        public List<string> Notes { get; } = new();

        public int Triangles => Faces.Sum(f => Math.Max(0, f.Corners.Count - 2));

        /// <summary>Reads an OBJ and, when it names one, the MTL beside it. Returns why not, or null.</summary>
        public static ObjMesh Read(string path, out string whynot)
        {
            whynot = null;
            var m = new ObjMesh { Name = Path.GetFileNameWithoutExtension(path) };
            string[] lines;
            try { lines = File.ReadAllLines(path); }
            catch (Exception ex) { whynot = "That file could not be read: " + ex.Message; return null; }

            string dir = Path.GetDirectoryName(path) ?? ".";
            int material = -1, ignored = 0, badFaces = 0;
            var byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var bits = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                switch (bits[0])
                {
                    case "v":
                        if (bits.Length >= 4) m.Positions.Add(new Vec3
                        { X = F(bits[1]), Y = F(bits[2]), Z = F(bits[3]) });
                        break;
                    case "vn":
                        if (bits.Length >= 4) m.Normals.Add(new Vec3
                        { X = F(bits[1]), Y = F(bits[2]), Z = F(bits[3]) });
                        break;
                    case "vt":
                        // OBJ counts up the picture and the DS counts down it.
                        if (bits.Length >= 3) m.TexCoords.Add(new Vec2
                        { U = F(bits[1]), V = 1f - F(bits[2]) });
                        break;
                    case "f":
                    {
                        var face = new Face { Material = Math.Max(0, material) };
                        for (int i = 1; i < bits.Length; i++)
                        {
                            var c = ParseCorner(bits[i], m.Positions.Count, m.TexCoords.Count, m.Normals.Count);
                            if (c.Position < 0) { face = null; break; }
                            face.Corners.Add(c);
                        }
                        if (face == null || face.Corners.Count < 3) { badFaces++; break; }
                        m.Faces.Add(face);
                        break;
                    }
                    case "mtllib":
                        if (bits.Length >= 2) m.ReadMtl(Path.Combine(dir, string.Join(" ", bits.Skip(1))), byName);
                        break;
                    case "usemtl":
                        if (bits.Length >= 2)
                        {
                            string name = string.Join(" ", bits.Skip(1));
                            if (!byName.TryGetValue(name, out material))
                            {
                                material = m.Materials.Count;
                                byName[name] = material;
                                m.Materials.Add(new Material { Name = name });
                            }
                        }
                        break;
                    case "o":
                    case "g":
                        break;      // groups are not kept; the DS groups by material instead
                    default:
                        ignored++;
                        break;
                }
            }

            if (m.Materials.Count == 0)
            {
                m.Materials.Add(new Material { Name = "material" });
                m.Notes.Add("No materials named, so everything went into one white one.");
            }
            if (m.Faces.Count == 0)
            {
                whynot = "That OBJ has no faces in it, so there would be nothing to draw.";
                return null;
            }
            if (badFaces > 0)
                m.Notes.Add($"{badFaces} faces were left out: too few corners, or a corner that is "
                          + "not there.");
            if (m.TexCoords.Count == 0 && m.Materials.Any(x => x.TexturePath != null))
                m.Notes.Add("A picture is named but nothing says where it goes, so it is unused.");
            return m;
        }

        private void ReadMtl(string path, Dictionary<string, int> byName)
        {
            if (!File.Exists(path))
            {
                Notes.Add($"The materials file {Path.GetFileName(path)} is not beside the OBJ, so its "
                        + "colours and pictures could not be read.");
                return;
            }
            string dir = Path.GetDirectoryName(path) ?? ".";
            Material current = null;
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var bits = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                switch (bits[0].ToLowerInvariant())
                {
                    case "newmtl":
                        if (bits.Length < 2) break;
                        string name = string.Join(" ", bits.Skip(1));
                        if (byName.TryGetValue(name, out int at)) current = Materials[at];
                        else
                        {
                            current = new Material { Name = name };
                            byName[name] = Materials.Count;
                            Materials.Add(current);
                        }
                        break;
                    case "kd":
                        if (current != null && bits.Length >= 4)
                        { current.Red = F(bits[1]); current.Green = F(bits[2]); current.Blue = F(bits[3]); }
                        break;
                    case "d":
                        if (current != null && bits.Length >= 2) current.Opacity = F(bits[1]);
                        break;
                    case "tr":
                        if (current != null && bits.Length >= 2) current.Opacity = 1f - F(bits[1]);
                        break;
                    case "map_kd":
                    {
                        if (current == null || bits.Length < 2) break;
                        // Some writers put switches before the file name; the last word is the file.
                        string file = bits[bits.Length - 1];
                        string full = Path.IsPathRooted(file) ? file : Path.Combine(dir, file);
                        if (File.Exists(full)) current.TexturePath = full;
                        else Notes.Add($"{current.Name} names the picture {Path.GetFileName(file)}, which "
                                     + "is not beside the model, so that material has no picture.");
                        break;
                    }
                }
            }
        }

        private static Corner ParseCorner(string s, int positions, int texCoords, int normals)
        {
            var c = new Corner { Position = -1, Normal = -1, TexCoord = -1 };
            var parts = s.Split('/');
            c.Position = Index(parts.Length > 0 ? parts[0] : null, positions);
            c.TexCoord = Index(parts.Length > 1 ? parts[1] : null, texCoords);
            c.Normal = Index(parts.Length > 2 ? parts[2] : null, normals);
            return c;
        }

        /// <summary>OBJ counts from one, and counts backwards from the end when the number is negative.</summary>
        private static int Index(string s, int count)
        {
            if (string.IsNullOrEmpty(s) || !int.TryParse(s, out int v)) return -1;
            int at = v > 0 ? v - 1 : count + v;
            return at >= 0 && at < count ? at : -1;
        }

        private static float F(string s) =>
            float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;
    }
}
