using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace DSPRE.Models
{
    /// <summary>Wavefront OBJ mesh with its MTL materials.</summary>
    public sealed class ObjMesh
    {
        public struct Vec3 { public float X, Y, Z; }
        public struct Vec2 { public float U, V; }

        public struct Corner
        {
            public int Position;
            public int Normal;
            public int TexCoord;

            public int? Colour;

            public bool ColourLast;
        }

        public sealed class Face
        {
            public List<Corner> Corners = new();
            public int Material;

            public int[] Colours;
            public bool[] ColoursLast;
        }

        public sealed class Material
        {
            public string Name = "";

            public string PaletteName;
            public string TexturePath;
            public float Red = 1, Green = 1, Blue = 1;
            public float Opacity = 1;

            public MaterialLook Look;

            public string PictureName;
        }

        public List<Vec3> Positions { get; } = new();

        public List<int> PositionColours { get; } = new();

        public HashSet<int> ColourLastAt { get; } = new();
        public List<Vec3> Normals { get; } = new();
        public List<Vec2> TexCoords { get; } = new();
        public List<Face> Faces { get; } = new();

        public sealed class Group
        {
            public float[] Tileable;

            public int[] Footprint;
            public string Collision;

            public string Name = "";
            public int First;
            public int Count;

            public int Wide, Deep;
        }

        public List<Group> Groups { get; } = new();
        public List<Material> Materials { get; } = new();

        public string Name = "model";

        public List<string> Notes { get; } = new();

        public List<string> SmartDrawings { get; } = new();

        public int Triangles => Faces.Sum(f => Math.Max(0, f.Corners.Count - 2));

        public static ObjMesh Read(string path, out string whynot)
        {
            whynot = null;
            ObjMesh m = new ObjMesh { Name = Path.GetFileNameWithoutExtension(path) };
            string[] lines;
            try { lines = File.ReadAllLines(path); }
            catch (Exception ex) { whynot = "Could not read file: " + ex.Message; return null; }

            string dir = Path.GetDirectoryName(path) ?? ".";
            int material = -1, ignored = 0, badFaces = 0;
            Group group = null;
            int[] pendingColours = null;
            bool[] pendingLast = null;
            Dictionary<string, int> byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.StartsWith("# squares ", StringComparison.Ordinal))
                {
                    string[] said = line.Substring(10).Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    if (said.Length >= 2 && group != null
                        && int.TryParse(said[0], out int w) && int.TryParse(said[1], out int d))
                    { group.Wide = w; group.Deep = d; }
                    continue;
                }
                if (line.StartsWith("# colours ", StringComparison.Ordinal))
                {
                    string[] said = line.Substring(10).Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    pendingColours = said.Select(v => int.TryParse(v.TrimEnd('!'), out int c) ? c : -1).ToArray();
                    pendingLast = said.Select(v => v.EndsWith("!")).ToArray();
                    continue;
                }
                if (line.StartsWith("# footprint ", StringComparison.Ordinal))
                {
                    if (group != null)
                        group.Footprint = line.Substring(12).Split((char[])null, StringSplitOptions.RemoveEmptyEntries)
                                              .Select(v => int.TryParse(v, out int i) ? i : 0).ToArray();
                    continue;
                }
                if (line.StartsWith("# collision ", StringComparison.Ordinal))
                {
                    if (group != null) group.Collision = line.Substring(12).Trim();
                    continue;
                }
                if (line.StartsWith("# tileable ", StringComparison.Ordinal))
                {
                    if (group != null)
                        group.Tileable = line.Substring(11).Split((char[])null, StringSplitOptions.RemoveEmptyEntries)
                                             .Select(F).ToArray();
                    continue;
                }
                if (line.StartsWith("# smart ", StringComparison.Ordinal))
                {
                    m.SmartDrawings.Add(line.Substring(8));
                    continue;
                }
                if (line.StartsWith("# colourlast ", StringComparison.Ordinal))
                {
                    foreach (string n in line.Substring(13).Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
                        if (int.TryParse(n, out int at) && at > 0) m.ColourLastAt.Add(at - 1);
                    continue;
                }
                if (line.Length == 0 || line[0] == '#') continue;
                string[] bits = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                switch (bits[0])
                {
                    case "v":
                        if (bits.Length >= 4)
                        {
                            m.Positions.Add(new Vec3 { X = F(bits[1]), Y = F(bits[2]), Z = F(bits[3]) });
                            int colour = -1;
                            if (bits.Length >= 7)
                            {
                                int C(string v) => (int)Math.Round(Math.Clamp(F(v), 0f, 1f) * 31f);
                                colour = C(bits[4]) | (C(bits[5]) << 5) | (C(bits[6]) << 10);
                            }
                            m.PositionColours.Add(colour);
                        }
                        break;
                    case "vn":
                        if (bits.Length >= 4) m.Normals.Add(new Vec3
                        { X = F(bits[1]), Y = F(bits[2]), Z = F(bits[3]) });
                        break;
                    case "vt":
                        if (bits.Length >= 3) m.TexCoords.Add(new Vec2
                        { U = F(bits[1]), V = 1f - F(bits[2]) });
                        break;
                    case "f":
                    {
                            Face face = new Face { Material = Math.Max(0, material) };
                        for (int i = 1; i < bits.Length; i++)
                        {
                                Corner c = ParseCorner(bits[i], m.Positions.Count, m.TexCoords.Count, m.Normals.Count);
                            if (c.Position < 0) { face = null; break; }
                            face.Corners.Add(c);
                        }
                        if (face == null || face.Corners.Count < 3) { badFaces++; pendingColours = null; break; }
                        if (pendingColours != null && pendingColours.Length == face.Corners.Count)
                        { face.Colours = pendingColours; face.ColoursLast = pendingLast; }
                        pendingColours = null;
                        m.Faces.Add(face);
                        if (group != null) group.Count++;
                        break;
                    }
                    case "o":
                    case "g":
                    {
                        if (group != null && group.Count == 0) m.Groups.Remove(group);
                        group = new Group
                        {
                            Name = bits.Length >= 2 ? string.Join(" ", bits.Skip(1)) : $"part{m.Groups.Count}",
                            First = m.Faces.Count,
                        };
                        m.Groups.Add(group);
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
                    default:
                        ignored++;
                        break;
                }
            }

            if (m.Materials.Count == 0)
            {
                m.Materials.Add(new Material { Name = "material" });
                m.Notes.Add("No materials; using a single white one.");
            }
            if (m.Faces.Count == 0)
            {
                whynot = "No faces in OBJ.";
                return null;
            }
            if (group != null && group.Count == 0) m.Groups.Remove(group);

            if (badFaces > 0)
                m.Notes.Add($"{badFaces} invalid faces skipped.");
            if (m.TexCoords.Count == 0 && m.Materials.Any(x => x.TexturePath != null))
                m.Notes.Add("Texture named but the OBJ has no UVs.");
            return m;
        }

        private void ReadMtl(string path, Dictionary<string, int> byName)
        {
            if (!File.Exists(path))
            {
                Notes.Add($"MTL not found: {Path.GetFileName(path)}");
                return;
            }
            string dir = Path.GetDirectoryName(path) ?? ".";
            Material current = null;
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();

                if (line.StartsWith("# palette ", StringComparison.Ordinal))
                {
                    if (current != null) current.PaletteName = line.Substring(10).Trim();
                    continue;
                }

                if (line.StartsWith("# look ", StringComparison.Ordinal))
                {
                    if (current != null)
                        try { current.Look = MaterialLook.FromRecord(Convert.FromHexString(line.Substring(7).Trim())); }
                        catch (FormatException) { }
                    continue;
                }
                if (line.StartsWith("# picture ", StringComparison.Ordinal))
                {
                    if (current != null) current.PictureName = line.Substring(10).Trim();
                    continue;
                }
                if (line.Length == 0 || line[0] == '#') continue;
                string[] bits = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
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
                        string file = bits[bits.Length - 1];
                        string full = Path.IsPathRooted(file) ? file : Path.Combine(dir, file);
                        if (File.Exists(full)) current.TexturePath = full;
                        else Notes.Add($"{current.Name}: texture not found: {Path.GetFileName(file)}");
                        break;
                    }
                }
            }
        }

        private static Corner ParseCorner(string s, int positions, int texCoords, int normals)
        {
            Corner c = new Corner { Position = -1, Normal = -1, TexCoord = -1 };
            string[] parts = s.Split('/');
            c.Position = Index(parts.Length > 0 ? parts[0] : null, positions);
            c.TexCoord = Index(parts.Length > 1 ? parts[1] : null, texCoords);
            c.Normal = Index(parts.Length > 2 ? parts[2] : null, normals);
            return c;
        }

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
