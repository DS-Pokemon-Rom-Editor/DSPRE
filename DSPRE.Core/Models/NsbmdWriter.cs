using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.Models
{
    /// <summary>NSBMD writer.</summary>
    public static class NsbmdWriter
    {
        public sealed class Result
        {
            public byte[] Bytes;
            public string Whynot;
            public int Materials, Shapes, Triangles, Textures;
            public int TextureBytes, PaletteBytes;
            public List<string> Notes = new();

            public string Summary => Whynot ?? $"{Triangles} triangles in {Many(Shapes, "shape")}, "
                + $"{Many(Materials, "material")}, {Many(Textures, "picture")}.";

            private static string Many(int n, string one) => n == 1 ? "1 " + one : $"{n} {one}s";
        }

        // Identity texture matrix, so no matrix data follows the record.
        private const int MaterialFlag = 0x1FCE;

        // Repeat S/T only; the game fills in address and size when it binds the texture.
        private const int MaterialImageParam = (1 << 16) | (1 << 17);

        public const int MostTriangles = 8000;

        public static Result Build(ObjMesh mesh, IReadOnlyList<DsTexture> textures,
                                   bool picturesAreElsewhere = false, float drawnAtScale = 0f)
        {
            var r = new Result();
            if (mesh == null) return Fail(r, "No mesh.");
            if (mesh.Faces.Count == 0) return Fail(r, "Mesh has no faces.");
            if (mesh.Triangles > MostTriangles)
                return Fail(r, $"{mesh.Triangles} triangles; the limit is {MostTriangles}.");

            r.Notes.AddRange(mesh.Notes);
            foreach (var t in textures ?? Array.Empty<DsTexture>()) r.Notes.AddRange(t.Notes);

            float lo = 0, hi = 0;
            foreach (var p in mesh.Positions)
            {
                lo = Math.Min(lo, Math.Min(p.X, Math.Min(p.Y, p.Z)));
                hi = Math.Max(hi, Math.Max(p.X, Math.Max(p.Y, p.Z)));
            }
            // Positions are signed 4.12 fixed point.
            const float Lowest = -8f, Highest = 8f - 1f / 4096f;
            int shift = 0;
            while ((lo / (1 << shift) < Lowest || hi / (1 << shift) > Highest) && shift < 20) shift++;
            float posScale = 1 << shift;
            if (shift > 0)
                r.Notes.Add($"Coordinates too large for DS fixed point; scaled by 1/{posScale:0}.");

            bool lit = mesh.Normals.Count > 0;
            if (!lit)
                r.Notes.Add("No normals, so the model is unlit.");

            var used = mesh.Faces.Select(f => f.Material).Distinct().OrderBy(x => x).ToList();
            if (used.Count > 60)
                return Fail(r, $"{used.Count} materials; the limit is 60.");

            var shapes = new List<(string Name, byte[] Dl, int Flags, int Triangles)>();
            var matNames = new List<string>();
            foreach (int m in used)
            {
                var dl = new GxDisplayList();
                int tris = WriteFaces(dl, mesh, m, posScale, textures, lit, picturesAreElsewhere);
                shapes.Add(($"polygon{shapes.Count}", dl.ToBytes(), dl.Flags(), tris));
                matNames.Add(UniqueName(matNames, mesh.Materials[m].Name, "material" + m));
                r.Triangles += tris;
            }

            r.Materials = matNames.Count;
            r.Shapes = shapes.Count;
            r.Textures = textures?.Count ?? 0;
            r.TextureBytes = textures?.Sum(t => t.Pixels.Length) ?? 0;
            r.PaletteBytes = textures?.Sum(t => t.PaletteBytes) ?? 0;

            try
            {
                byte[] mdl0 = BuildMdl0(mesh, used, matNames, shapes, posScale, textures, lit,
                                        picturesAreElsewhere,
                                        // Coordinates divided to fit 4.12 are multiplied back by the declared scale.
                                        drawnAtScale > 0f ? drawnAtScale * posScale : posScale);
                byte[] tex0 = textures != null && textures.Count > 0 ? BuildTex0(textures) : null;
                r.Bytes = Envelope(tex0 == null ? new[] { mdl0 } : new[] { mdl0, tex0 });
            }
            catch (Exception ex) { return Fail(r, "Model build failed: " + ex.Message); }
            return r;
        }

        private static Result Fail(Result r, string why) { r.Whynot = why; return r; }

        private static string UniqueName(List<string> taken, string wanted, string fallback)
        {
            string clean = new string((wanted ?? "").Where(c => c > 32 && c < 127).ToArray());
            if (clean.Length == 0) clean = fallback;
            clean = NitroDictionary.Fit(clean);
            string name = clean;
            for (int n = 1; taken.Contains(name); n++)
                name = clean.Substring(0, Math.Min(clean.Length, NitroDictionary.NameSize - n.ToString().Length)) + n;
            return name;
        }

        private static int WriteFaces(GxDisplayList dl, ObjMesh mesh, int material, float posScale,
                                      IReadOnlyList<DsTexture> textures, bool lit, bool picturesAreElsewhere)
        {
            var tex = textures?.FirstOrDefault(t => t.Name != null);
            int texW = tex?.Width ?? 0, texH = tex?.Height ?? 0;
            var m = mesh.Materials[material];
            if (textures != null)
            {
                var mine = textures.FirstOrDefault(t => string.Equals(t.Name, Short(m.Name),
                                                                      StringComparison.OrdinalIgnoreCase));
                if (mine != null) { texW = mine.Width; texH = mine.Height; }
            }

            if (picturesAreElsewhere) { texW = 1; texH = 1; }

            int tris = 0;
            var look = m.Look;
            if (look != null) return WriteLookedFaces(dl, mesh, material, posScale, texW, texH, look);
            if (!lit)
            {
                var c = mesh.Materials[material];
                dl.SetColour((int)Math.Round(Math.Clamp(c.Red, 0, 1) * 31),
                             (int)Math.Round(Math.Clamp(c.Green, 0, 1) * 31),
                             (int)Math.Round(Math.Clamp(c.Blue, 0, 1) * 31));
            }
            dl.Begin(GxDisplayList.Shape.Triangles);
            foreach (var f in mesh.Faces)
            {
                if (f.Material != material) continue;
                for (int i = 2; i < f.Corners.Count; i++)
                {
                    foreach (var c in new[] { f.Corners[0], f.Corners[i - 1], f.Corners[i] })
                    {
                        if (c.Normal >= 0 && c.Normal < mesh.Normals.Count)
                        {
                            var n = mesh.Normals[c.Normal];
                            dl.SetNormal(n.X, n.Y, n.Z);
                        }
                        if (texW > 0 && c.TexCoord >= 0 && c.TexCoord < mesh.TexCoords.Count)
                        {
                            var t = mesh.TexCoords[c.TexCoord];
                            dl.SetTexCoord(t.U, t.V, texW, texH);
                        }
                        var p = mesh.Positions[c.Position];
                        dl.AddVertex(p.X / posScale, p.Y / posScale, p.Z / posScale);
                    }
                    tris++;
                }
            }
            dl.End();
            return tris;
        }

        private static int WriteLookedFaces(GxDisplayList dl, ObjMesh mesh, int material, float posScale,
                                            int texW, int texH, MaterialLook look)
        {
            int baseColour = look.CornerColour;
            int colourNow = baseColour, normalNow = -1;
            bool colourWasLast = true;
            int tris = 0;

            uint placeNow = uint.MaxValue;
            void Corner(ObjMesh.Corner c)
            {
                int colour = c.Colour ?? baseColour;
                int normal = -1;
                if (c.Normal >= 0 && c.Normal < mesh.Normals.Count)
                {
                    var n = mesh.Normals[c.Normal];
                    normal = NormalWord(n.X, n.Y, n.Z);
                }

                if (c.ColourLast)
                {
                    if (normal >= 0 && normal != normalNow)
                    { dl.Command(GxDisplayList.Normal, (uint)normal); normalNow = normal; colourWasLast = false; }
                    if (colour >= 0 && (colour != colourNow || !colourWasLast))
                    { dl.Command(GxDisplayList.Color, (uint)colour); colourNow = colour; colourWasLast = true; }
                }
                else
                {
                    if (colour >= 0 && colour != colourNow)
                    { dl.Command(GxDisplayList.Color, (uint)colour); colourNow = colour; colourWasLast = true; }
                    if (normal >= 0 && (normal != normalNow || colourWasLast))
                    { dl.Command(GxDisplayList.Normal, (uint)normal); normalNow = normal; colourWasLast = false; }
                }

                if (texW > 0 && c.TexCoord >= 0 && c.TexCoord < mesh.TexCoords.Count)
                {
                    var t = mesh.TexCoords[c.TexCoord];
                    uint place = (uint)((GxDisplayList.Sixteenths(t.U * texW) & 0xFFFF)
                                      | ((GxDisplayList.Sixteenths(t.V * texH) & 0xFFFF) << 16));
                    if (place != placeNow) { dl.Command(GxDisplayList.TexCoord, place); placeNow = place; }
                }
                var p = mesh.Positions[c.Position];
                dl.AddVertexRaw(GxDisplayList.Fixed(p.X / posScale), GxDisplayList.Fixed(p.Y / posScale),
                                GxDisplayList.Fixed(p.Z / posScale));
            }

            var mine = mesh.Faces.Where(f => f.Material == material).ToList();
            var quads = mine.Where(f => f.Corners.Count == 4).ToList();
            var others = mine.Where(f => f.Corners.Count != 4).ToList();
            if (others.Count > 0)
            {
                dl.Begin(GxDisplayList.Shape.Triangles);
                foreach (var f in others)
                    for (int i = 2; i < f.Corners.Count; i++)
                    {
                        Corner(f.Corners[0]); Corner(f.Corners[i - 1]); Corner(f.Corners[i]);
                        tris++;
                    }
                dl.End();
            }
            if (quads.Count > 0)
            {
                dl.Begin(GxDisplayList.Shape.Quads);
                foreach (var f in quads)
                {
                    foreach (var c in f.Corners) Corner(c);
                    tris += 2;
                }
                dl.End();
            }
            return tris;
        }

        private static int NormalWord(float x, float y, float z)
            => (GxDisplayList.Ten(x) & 0x3FF) | ((GxDisplayList.Ten(y) & 0x3FF) << 10) | ((GxDisplayList.Ten(z) & 0x3FF) << 20);

        private static string Short(string s)
        {
            s = new string((s ?? "").Where(c => c > 32 && c < 127).ToArray());
            // Keep all 16 characters so names match the texture pack's dictionary.
            return NitroDictionary.Fit(s);
        }

        private static byte[] BuildMdl0(ObjMesh mesh, List<int> used, List<string> matNames,
            List<(string Name, byte[] Dl, int Flags, int Triangles)> shapes, float posScale,
            IReadOnlyList<DsTexture> textures, bool lit, bool picturesAreElsewhere, float declared)
        {
            string modelName = Short(mesh.Name);
            if (modelName.Length == 0) modelName = "model";

            var nodeNames = new List<string> { "world_root" };
            byte[] nodeDict = NitroDictionary.Write(nodeNames,
                new List<byte[]> { Word(NitroDictionary.SizeFor(1, 4)) });
            byte[] nodeData = NodeData();

            byte[] sbc = Sbc(matNames.Count, shapes.Count);
            byte[] mat = BuildMat(matNames, mesh, used, textures, lit, picturesAreElsewhere);
            byte[] shp = BuildShp(shapes);

            int infoAt = 20;
            int nodeInfoAt = infoAt + 44;
            int sbcAt = Align4(nodeInfoAt + nodeDict.Length + nodeData.Length);
            int matAt = Align4(sbcAt + sbc.Length);
            int shpAt = Align4(matAt + mat.Length);
            int modelSize = Align4(shpAt + shp.Length);

            var m = new byte[modelSize];
            Put32(m, 0, modelSize);
            Put32(m, 4, sbcAt);
            Put32(m, 8, matAt);
            Put32(m, 12, shpAt);
            Put32(m, 16, modelSize);

            m[infoAt + 0] = 0;
            m[infoAt + 1] = 0;
            m[infoAt + 2] = 0;
            m[infoAt + 3] = (byte)nodeNames.Count;
            m[infoAt + 4] = (byte)matNames.Count;
            m[infoAt + 5] = (byte)shapes.Count;
            m[infoAt + 6] = 1;
            Put32(m, infoAt + 8, (int)Math.Round(declared * 4096));
            Put32(m, infoAt + 12, (int)Math.Round(4096 / declared));
            Put16(m, infoAt + 16, Math.Min(65535, shapes.Sum(s => s.Triangles) * 3));
            Put16(m, infoAt + 18, Math.Min(65535, shapes.Sum(s => s.Triangles)));
            Put16(m, infoAt + 20, Math.Min(65535, shapes.Sum(s => s.Triangles)));
            Put16(m, infoAt + 22, 0);

            float lo = 0, hi = 0;
            foreach (var p in mesh.Positions)
            {
                lo = Math.Min(lo, Math.Min(p.X, Math.Min(p.Y, p.Z)));
                hi = Math.Max(hi, Math.Max(p.X, Math.Max(p.Y, p.Z)));
            }
            float boxScale = posScale;
            Put16(m, infoAt + 24, Clamp16(lo / boxScale * 4096));
            Put16(m, infoAt + 26, Clamp16(lo / boxScale * 4096));
            Put16(m, infoAt + 28, Clamp16(lo / boxScale * 4096));
            Put16(m, infoAt + 30, Clamp16((hi - lo) / boxScale * 4096));
            Put16(m, infoAt + 32, Clamp16((hi - lo) / boxScale * 4096));
            Put16(m, infoAt + 34, Clamp16((hi - lo) / boxScale * 4096));
            Put32(m, infoAt + 36, (int)Math.Round(boxScale * 4096));
            Put32(m, infoAt + 40, (int)Math.Round(4096 / boxScale));

            Array.Copy(nodeDict, 0, m, nodeInfoAt, nodeDict.Length);
            Array.Copy(nodeData, 0, m, nodeInfoAt + nodeDict.Length, nodeData.Length);
            Array.Copy(sbc, 0, m, sbcAt, sbc.Length);
            Array.Copy(mat, 0, m, matAt, mat.Length);
            Array.Copy(shp, 0, m, shpAt, shp.Length);

            int modelAt = 8 + NitroDictionary.SizeFor(1, 4);
            byte[] setDict = NitroDictionary.Write(new List<string> { modelName },
                new List<byte[]> { Word(modelAt) });
            var block = new byte[modelAt + m.Length];
            block[0] = (byte)'M'; block[1] = (byte)'D'; block[2] = (byte)'L'; block[3] = (byte)'0';
            Put32(block, 4, block.Length);
            Array.Copy(setDict, 0, block, 8, setDict.Length);
            Array.Copy(m, 0, block, modelAt, m.Length);
            return block;
        }

        private static byte[] NodeData()
        {
            var d = new byte[8];
            Put16(d, 0, 0x0007);
            Put16(d, 2, 0);
            return d;
        }

        private static byte[] Sbc(int materials, int shapes)
        {
            var o = new List<byte>
            {
                0x26, 0x00, 0x00, 0x00, 0x00,
                0x02, 0x00, 0x01,
                0x0b,
            };
            for (int i = 0; i < shapes; i++)
            {
                o.Add(0x04); o.Add((byte)Math.Min(i, materials - 1));
                o.Add(0x05); o.Add((byte)i);
            }
            o.Add(0x2b);
            o.Add(0x01);
            while (o.Count % 4 != 0) o.Add(0x00);
            return o.ToArray();
        }

        private static byte[] BuildMat(List<string> names, ObjMesh mesh, List<int> used,
                                       IReadOnlyList<DsTexture> textures, bool lit,
                                       bool picturesAreElsewhere = false)
        {
            const int MatDataSize = 44;
            int count = names.Count;
            int dictSize = NitroDictionary.SizeFor(count, 4);

            var texNames = picturesAreElsewhere
                ? used.Select(u => Short(mesh.Materials[u].Name)).Distinct().ToList()
                : textures?.Select(t => t.Name).ToList() ?? new List<string>();

            var textureOf = new int[count];
            for (int i = 0; i < count; i++)
                textureOf[i] = texNames.FindIndex(n =>
                    string.Equals(n, Short(mesh.Materials[used[i]].Name), StringComparison.OrdinalIgnoreCase));

            var usersOf = new List<List<byte>>();
            foreach (var _ in texNames) usersOf.Add(new List<byte>());
            for (int i = 0; i < count; i++)
                if (textureOf[i] >= 0) usersOf[textureOf[i]].Add((byte)i);

            var palNames = new List<string>();
            var palUsers = new List<List<byte>>();
            var paletteOf = new int[count];
            for (int i = 0; i < count; i++)
            {
                string asked = mesh.Materials[used[i]].PaletteName;
                string wants = Short(string.IsNullOrEmpty(asked) ? mesh.Materials[used[i]].Name : asked);

                int at = palNames.FindIndex(n => string.Equals(n, wants, StringComparison.OrdinalIgnoreCase));
                if (at < 0)
                {
                    at = palNames.Count;
                    palNames.Add(wants);
                    palUsers.Add(new List<byte>());
                }
                paletteOf[i] = at;
                palUsers[at].Add((byte)i);
            }

            int texToMat = 4 + dictSize;
            int texToMatSize = NitroDictionary.SizeFor(texNames.Count, 4);
            int plttToMat = texToMat + texToMatSize;
            int plttToMatSize = NitroDictionary.SizeFor(palNames.Count, 4);
            int listsAt = plttToMat + plttToMatSize;

            var listAt = new int[texNames.Count];
            int listBytes = 0;
            for (int i = 0; i < texNames.Count; i++)
            { listAt[i] = listsAt + listBytes; listBytes += Math.Max(1, usersOf[i].Count); }

            var palListAt = new int[palNames.Count];
            for (int i = 0; i < palNames.Count; i++)
            { palListAt[i] = listsAt + listBytes; listBytes += Math.Max(1, palUsers[i].Count); }

            int dataAt = Align4(listsAt + listBytes);
            var recordAt = new int[count];
            int recordBytes = 0;
            for (int i = 0; i < count; i++)
            {
                recordAt[i] = dataAt + recordBytes;
                recordBytes += Align4(mesh.Materials[used[i]].Look?.Record.Length ?? MatDataSize);
            }
            var o = new byte[dataAt + recordBytes];
            Put16(o, 0, texToMat);
            Put16(o, 2, plttToMat);

            var entries = new List<byte[]>();
            for (int i = 0; i < count; i++) entries.Add(Word(recordAt[i]));
            byte[] dict = NitroDictionary.Write(names, entries);
            Array.Copy(dict, 0, o, 4, dict.Length);

            var toMat = new List<byte[]>();
            for (int i = 0; i < texNames.Count; i++)
            {
                var e = new byte[4];
                Put16(e, 0, listAt[i]);
                e[2] = (byte)usersOf[i].Count;
                e[3] = 0;
                toMat.Add(e);
            }
            var toPal = new List<byte[]>();
            for (int i = 0; i < palNames.Count; i++)
            {
                var e = new byte[4];
                Put16(e, 0, palListAt[i]);
                e[2] = (byte)palUsers[i].Count;
                e[3] = 0;
                toPal.Add(e);
            }

            byte[] texDict = NitroDictionary.Write(texNames, toMat);
            byte[] plttDict = NitroDictionary.Write(palNames, toPal);
            Array.Copy(texDict, 0, o, texToMat, texDict.Length);
            Array.Copy(plttDict, 0, o, plttToMat, plttDict.Length);
            for (int i = 0; i < texNames.Count; i++)
                for (int k = 0; k < usersOf[i].Count; k++) o[listAt[i] + k] = usersOf[i][k];
            for (int i = 0; i < palNames.Count; i++)
                for (int k = 0; k < palUsers[i].Count; k++) o[palListAt[i] + k] = palUsers[i][k];

            for (int i = 0; i < count; i++)
            {
                int a = recordAt[i];
                var src = mesh.Materials[used[i]];
                if (src.Look != null)
                {
                    Array.Copy(src.Look.Record, 0, o, a, src.Look.Record.Length);
                    continue;
                }
                var tex = textures != null && textureOf[i] >= 0 && textureOf[i] < textures.Count
                    ? textures[textureOf[i]] : null;
                bool named = textureOf[i] >= 0;

                Put16(o, a, 0);
                Put16(o, a + 2, MatDataSize);
                Put32(o, a + 4, DiffuseAmbient(src));
                Put32(o, a + 8, 0x0000_0000);
                Put32(o, a + 12, PolygonAttr(src, lit));
                Put32(o, a + 16, unchecked((int)0xFFFF_FFFF));
                Put32(o, a + 20, named ? MaterialImageParam : 0);
                Put32(o, a + 24, named ? unchecked((int)0xFFFF_FFFF) : 0);
                Put16(o, a + 28, 0);
                Put16(o, a + 30, MaterialFlag);
                Put16(o, a + 32, tex?.Width ?? 0);
                Put16(o, a + 34, tex?.Height ?? 0);
                Put32(o, a + 36, 4096);
                Put32(o, a + 40, 4096);
            }
            return o;
        }

        private static int DiffuseAmbient(ObjMesh.Material m)
        {
            int r = (int)Math.Round(Math.Clamp(m.Red, 0, 1) * 31);
            int g = (int)Math.Round(Math.Clamp(m.Green, 0, 1) * 31);
            int b = (int)Math.Round(Math.Clamp(m.Blue, 0, 1) * 31);
            int diffuse = r | (g << 5) | (b << 10);
            int ambient = (r / 2) | ((g / 2) << 5) | ((b / 2) << 10);
            return diffuse | (1 << 15) | (ambient << 16);
        }

        private static int PolygonAttr(ObjMesh.Material m, bool lit)
        {
            int alpha = (int)Math.Round(Math.Clamp(m.Opacity, 0, 1) * 31);
            return (lit ? 0x0000_0001 : 0) | (3 << 6) | ((alpha & 31) << 16);
        }

        private static byte[] BuildShp(List<(string Name, byte[] Dl, int Flags, int Triangles)> shapes)
        {
            const int ShpDataSize = 16;
            int count = shapes.Count;
            int dictSize = NitroDictionary.SizeFor(count, 4);
            int dataAt = dictSize;
            int dlAt = Align4(dataAt + count * ShpDataSize);

            var entries = new List<byte[]>();
            for (int i = 0; i < count; i++) entries.Add(Word(dataAt + i * ShpDataSize));
            byte[] dict = NitroDictionary.Write(shapes.Select(s => s.Name).ToList(), entries);

            int total = dlAt;
            var dlAts = new int[count];
            for (int i = 0; i < count; i++) { dlAts[i] = total; total = Align4(total + shapes[i].Dl.Length); }

            var o = new byte[total];
            Array.Copy(dict, o, dict.Length);
            for (int i = 0; i < count; i++)
            {
                int a = dataAt + i * ShpDataSize;
                Put16(o, a, 0);
                Put16(o, a + 2, ShpDataSize);
                Put32(o, a + 4, shapes[i].Flags);
                Put32(o, a + 8, dlAts[i] - a);
                Put32(o, a + 12, shapes[i].Dl.Length);
                Array.Copy(shapes[i].Dl, 0, o, dlAts[i], shapes[i].Dl.Length);
            }
            return o;
        }

        internal static byte[] BuildTex0(IReadOnlyList<DsTexture> textures)
        {
            var names = textures.Select(t => t.Name).ToList();
            int header = 8 + 16 + 20 + 16;

            var texAt = new int[textures.Count];
            var palAt = new int[textures.Count];
            int texTotal = 0, palTotal = 0;
            for (int i = 0; i < textures.Count; i++)
            {
                texAt[i] = texTotal; texTotal = Align8(texTotal + textures[i].Pixels.Length);
                palAt[i] = palTotal; palTotal = Align8(palTotal + textures[i].PaletteBytes);
            }

            var texEntries = new List<byte[]>();
            for (int i = 0; i < textures.Count; i++)
            {
                var e = new byte[8];
                Put32(e, 0, (int)textures[i].ImageParam(texAt[i]));
                Put32(e, 4, (textures[i].Width & 0x7FF) | ((textures[i].Height & 0x7FF) << 11));
                texEntries.Add(e);
            }
            byte[] texDict = NitroDictionary.Write(names, texEntries);

            var palNames = new List<string>();
            var palEntries = new List<byte[]>();
            for (int i = 0; i < textures.Count; i++)
            {
                var called = textures[i].PaletteNames != null && textures[i].PaletteNames.Count > 0
                    ? textures[i].PaletteNames : new List<string> { textures[i].Name };
                foreach (string name in called)
                {
                    if (string.IsNullOrEmpty(name) || palNames.Contains(name)) continue;
                    var e = new byte[4];
                    Put16(e, 0, palAt[i] >> 3);
                    // The flag marks four-colour palettes, which the hardware addresses in 8-byte steps.
                    Put16(e, 2, 0);
                    palNames.Add(name);
                    palEntries.Add(e);
                }
            }
            byte[] palDict = NitroDictionary.Write(palNames, palEntries);

            int texDataAt = Align8(header + texDict.Length + palDict.Length);
            int palDataAt = Align8(texDataAt + texTotal);
            int size = Align4(palDataAt + palTotal);

            var o = new byte[size];
            o[0] = (byte)'T'; o[1] = (byte)'E'; o[2] = (byte)'X'; o[3] = (byte)'0';
            Put32(o, 4, size);

            Put16(o, 8 + 4, texTotal >> 3);
            Put16(o, 8 + 6, header);
            Put32(o, 8 + 12, texDataAt);
            Put16(o, 8 + 36 + 4, palTotal >> 3);
            Put16(o, 8 + 36 + 8, header + texDict.Length);
            Put32(o, 8 + 36 + 12, palDataAt);

            Array.Copy(texDict, 0, o, header, texDict.Length);
            Array.Copy(palDict, 0, o, header + texDict.Length, palDict.Length);
            for (int i = 0; i < textures.Count; i++)
            {
                Array.Copy(textures[i].Pixels, 0, o, texDataAt + texAt[i], textures[i].Pixels.Length);
                for (int c = 0; c < textures[i].Colours.Length; c++)
                    Put16(o, palDataAt + palAt[i] + c * 2, textures[i].Colours[c]);
            }
            return o;
        }

        internal static byte[] Envelope(IReadOnlyList<byte[]> blocks, string magic = "BMD0",
                                        int version = 2)
        {
            int header = Align4(16 + blocks.Count * 4);
            int total = header + blocks.Sum(b => b.Length);
            var o = new byte[total];
            for (int i = 0; i < 4; i++) o[i] = (byte)(i < magic.Length ? magic[i] : ' ');
            o[4] = 0xFF; o[5] = 0xFE;
            Put16(o, 6, version);
            Put32(o, 8, total);
            Put16(o, 12, 16);
            Put16(o, 14, blocks.Count);
            int at = header;
            for (int i = 0; i < blocks.Count; i++)
            {
                Put32(o, 16 + i * 4, at);
                Array.Copy(blocks[i], 0, o, at, blocks[i].Length);
                at += blocks[i].Length;
            }
            return o;
        }

        private static byte[] Word(int v) { var b = new byte[4]; Put32(b, 0, v); return b; }
        private static int Align4(int v) => (v + 3) & ~3;
        private static int Align8(int v) => (v + 7) & ~7;
        private static short Clamp16(float v) => (short)Math.Clamp(v, short.MinValue, short.MaxValue);

        private static void Put16(byte[] d, int at, int v)
        { d[at] = (byte)v; d[at + 1] = (byte)(v >> 8); }

        private static void Put32(byte[] d, int at, int v)
        {
            d[at] = (byte)v; d[at + 1] = (byte)(v >> 8);
            d[at + 2] = (byte)(v >> 16); d[at + 3] = (byte)(v >> 24);
        }
    }
}
