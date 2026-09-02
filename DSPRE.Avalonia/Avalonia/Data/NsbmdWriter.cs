using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Writes a model file the DS can read: the shape, its materials, the pictures painted on it, and
    /// the little program that says what order to draw them in.
    ///
    /// Every field written here comes from the model format's own header layout, rather than from
    /// reading a file and
    /// guessing: NNSG3dResFileHeader, NNSG3dResMdlSet, NNSG3dResMdl, NNSG3dResMdlInfo,
    /// NNSG3dResNodeInfo, NNSG3dResMat, NNSG3dResShp and NNSG3dResTex.
    /// </summary>
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

        /// <summary>
        /// What a material declares about itself, matching what HeartGold's own materials carry. The
        /// low three picture-placing bits say the picture is not scaled, turned or moved, and each of
        /// them being off would mean more numbers follow the record. The rest say the material gives
        /// its own colours, its own see-through setting and its own place in the colour list.
        /// </summary>
        private const int MaterialFlag = 0x1FCE;

        /// <summary>
        /// A material's picture settings: repeat across and down, and nothing else. Every material in
        /// HeartGold's own building models carries exactly this.
        /// </summary>
        private const int MaterialImageParam = (1 << 16) | (1 << 17);

        /// <summary>The biggest a model can be before it stops being worth putting in a ROM.</summary>
        public const int MostTriangles = 8000;

        public static Result Build(ObjMesh mesh, IReadOnlyList<DsTexture> textures)
        {
            var r = new Result();
            if (mesh == null) return Fail(r, "There is no mesh to write.");
            if (mesh.Faces.Count == 0) return Fail(r, "That mesh has no faces, so there is nothing to draw.");
            if (mesh.Triangles > MostTriangles)
                return Fail(r, $"That mesh is {mesh.Triangles} triangles. {MostTriangles} is as many as "
                             + "this writes, and far more than the DS draws in one go.");

            r.Notes.AddRange(mesh.Notes);
            foreach (var t in textures ?? Array.Empty<DsTexture>()) r.Notes.AddRange(t.Notes);

            // The hardware keeps a distance as a signed number with twelve bits after the point, which
            // reaches from -8 to a hair under 8. That is not the same either side, so the two ends are
            // measured apart: using the further of the two as if it were symmetric picks a scale twice
            // as coarse as it needs to be and loses precision for nothing.
            float lo = 0, hi = 0;
            foreach (var p in mesh.Positions)
            {
                lo = Math.Min(lo, Math.Min(p.X, Math.Min(p.Y, p.Z)));
                hi = Math.Max(hi, Math.Max(p.X, Math.Max(p.Y, p.Z)));
            }
            const float Lowest = -8f, Highest = 8f - 1f / 4096f;
            int shift = 0;
            while ((lo / (1 << shift) < Lowest || hi / (1 << shift) > Highest) && shift < 20) shift++;
            float posScale = 1 << shift;                 // multiply back up when drawing
            if (shift > 0)
                r.Notes.Add($"It runs from {lo:0.##} to {hi:0.##}, further than the DS keeps in one "
                          + $"number, so it was written {posScale:0} times smaller and scaled back up.");

            // A mesh with no normals cannot be lit: the hardware would light every corner off whatever
            // direction it last saw, which comes out black. Those are drawn at their own colour instead,
            // which is what the games do for the same sort of shape.
            bool lit = mesh.Normals.Count > 0;
            if (!lit)
                r.Notes.Add("No normals, so it is drawn at its own colour rather than lit.");

            // One shape per material, since a shape is drawn with one material.
            var used = mesh.Faces.Select(f => f.Material).Distinct().OrderBy(x => x).ToList();
            if (used.Count > 60)
                return Fail(r, $"That mesh uses {used.Count} materials. Sixty is as many as this writes; "
                             + "joining some of them together would bring it down.");

            var shapes = new List<(string Name, byte[] Dl, int Flags, int Triangles)>();
            var matNames = new List<string>();
            foreach (int m in used)
            {
                // The draw program puts the matrix in place before the shape is drawn, which is what
                // the games do, so the shape itself does not restore one.
                var dl = new GxDisplayList();
                int tris = WriteFaces(dl, mesh, m, posScale, textures, lit);
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
                byte[] mdl0 = BuildMdl0(mesh, used, matNames, shapes, posScale, textures, lit);
                byte[] tex0 = textures != null && textures.Count > 0 ? BuildTex0(textures) : null;
                r.Bytes = Envelope(tex0 == null ? new[] { mdl0 } : new[] { mdl0, tex0 });
            }
            catch (Exception ex) { return Fail(r, "That model could not be put together: " + ex.Message); }
            return r;
        }

        private static Result Fail(Result r, string why) { r.Whynot = why; return r; }

        private static string UniqueName(List<string> taken, string wanted, string fallback)
        {
            string clean = new string((wanted ?? "").Where(c => c > 32 && c < 127).ToArray());
            if (clean.Length == 0) clean = fallback;
            if (clean.Length > 15) clean = clean.Substring(0, 15);
            string name = clean;
            for (int n = 1; taken.Contains(name); n++)
                name = clean.Substring(0, Math.Min(clean.Length, 13)) + n;
            return name;
        }

        /// <summary>Writes every face drawn with one material, as triangles.</summary>
        private static int WriteFaces(GxDisplayList dl, ObjMesh mesh, int material, float posScale,
                                      IReadOnlyList<DsTexture> textures, bool lit)
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

            int tris = 0;
            if (!lit)
            {
                // One colour for the whole shape, so every corner has something to be drawn in.
                var c = mesh.Materials[material];
                dl.SetColour((int)Math.Round(Math.Clamp(c.Red, 0, 1) * 31),
                             (int)Math.Round(Math.Clamp(c.Green, 0, 1) * 31),
                             (int)Math.Round(Math.Clamp(c.Blue, 0, 1) * 31));
            }
            dl.Begin(GxDisplayList.Shape.Triangles);
            foreach (var f in mesh.Faces)
            {
                if (f.Material != material) continue;
                // Anything with more than three corners is fanned into triangles, which is what the
                // hardware would do anyway and keeps one path rather than several.
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

        private static string Short(string s)
        {
            s = new string((s ?? "").Where(c => c > 32 && c < 127).ToArray());
            return s.Length > 15 ? s.Substring(0, 15) : s;
        }

        // ── the model block ───────────────────────────────────────────────────────────────────────

        private static byte[] BuildMdl0(ObjMesh mesh, List<int> used, List<string> matNames,
            List<(string Name, byte[] Dl, int Flags, int Triangles)> shapes, float posScale,
            IReadOnlyList<DsTexture> textures, bool lit)
        {
            string modelName = Short(mesh.Name);
            if (modelName.Length == 0) modelName = "model";

            // One node, which everything hangs off. A mesh out of an OBJ has no skeleton.
            var nodeNames = new List<string> { "world_root" };
            byte[] nodeDict = NitroDictionary.Write(nodeNames,
                new List<byte[]> { Word(NitroDictionary.SizeFor(1, 4)) });
            byte[] nodeData = NodeData();

            byte[] sbc = Sbc(matNames.Count, shapes.Count);
            byte[] mat = BuildMat(matNames, mesh, used, textures, lit);
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
            Put32(m, 16, modelSize);                 // no envelope matrices, so this points at the end

            m[infoAt + 0] = 0;                       // the plain sort of draw program
            m[infoAt + 1] = 0;                       // sizes are kept as they are
            m[infoAt + 2] = 0;                       // pictures are placed the plain way
            m[infoAt + 3] = (byte)nodeNames.Count;
            m[infoAt + 4] = (byte)matNames.Count;
            m[infoAt + 5] = (byte)shapes.Count;
            m[infoAt + 6] = 1;                       // one matrix on the stack is used
            Put32(m, infoAt + 8, (int)Math.Round(posScale * 4096));
            Put32(m, infoAt + 12, (int)Math.Round(4096 / posScale));
            Put16(m, infoAt + 16, Math.Min(65535, shapes.Sum(s => s.Triangles) * 3));
            Put16(m, infoAt + 18, Math.Min(65535, shapes.Sum(s => s.Triangles)));
            Put16(m, infoAt + 20, Math.Min(65535, shapes.Sum(s => s.Triangles)));
            Put16(m, infoAt + 22, 0);                // everything was written as triangles

            // The box the model sits in, so anything that culls by distance gets it right.
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

            // The model set that holds it. Where the model lands depends only on how big the lookup
            // is, and that is known before writing it, so the offset goes in first time.
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

        /// <summary>One node that does nothing: no move, no turn, no resize.</summary>
        private static byte[] NodeData()
        {
            var d = new byte[8];
            Put16(d, 0, 0x0007);        // nothing moved, nothing turned, nothing resized
            Put16(d, 2, 0);
            return d;
        }

        /// <summary>
        /// The little program that draws the model: put the one matrix in place, then for each shape
        /// set its material and draw it.
        /// </summary>
        /// <summary>
        /// The draw program. Every command and how many bytes it takes comes from the interpreter in
        /// the draw program's own definition: NODEDESC under the 001 flag carries a
        /// fourth operand saying which matrix to store into, NODE takes a number and a visible flag,
        /// MAT and SHP take one number each, and POSSCALE scales by the model's own factor, with the
        /// 001 flag scaling by its reciprocal again afterwards.
        /// </summary>
        private static byte[] Sbc(int materials, int shapes)
        {
            var o = new List<byte>
            {
                0x26, 0x00, 0x00, 0x00, 0x00,   // NODEDESC node 0, parent 0, no flags, into matrix 0
                0x02, 0x00, 0x01,               // NODE 0, visible
                0x0b,                           // POSSCALE, so the model comes out its real size
            };
            for (int i = 0; i < shapes; i++)
            {
                o.Add(0x04); o.Add((byte)Math.Min(i, materials - 1));   // MAT
                o.Add(0x05); o.Add((byte)i);                            // SHP
            }
            o.Add(0x2b);                        // POSSCALE the other way, putting the size back
            o.Add(0x01);                        // RET
            while (o.Count % 4 != 0) o.Add(0x00);
            return o.ToArray();
        }

        private static byte[] BuildMat(List<string> names, ObjMesh mesh, List<int> used,
                                       IReadOnlyList<DsTexture> textures, bool lit)
        {
            const int MatDataSize = 44;
            int count = names.Count;
            int dictSize = NitroDictionary.SizeFor(count, 4);

            // Which picture each material is painted with, matched by name.
            var texNames = textures?.Select(t => t.Name).ToList() ?? new List<string>();
            var textureOf = new int[count];
            for (int i = 0; i < count; i++)
                textureOf[i] = texNames.FindIndex(n =>
                    string.Equals(n, Short(mesh.Materials[used[i]].Name), StringComparison.OrdinalIgnoreCase));

            // And the other way round: for each picture, the materials that use it. Both dictionaries
            // are always written, even with nothing in them, because a reader is entitled to expect
            // them where the two offsets at the top say they are.
            var usersOf = new List<List<byte>>();
            foreach (var _ in texNames) usersOf.Add(new List<byte>());
            for (int i = 0; i < count; i++)
                if (textureOf[i] >= 0) usersOf[textureOf[i]].Add((byte)i);

            int texToMat = 4 + dictSize;
            int texToMatSize = NitroDictionary.SizeFor(texNames.Count, 4);
            int plttToMat = texToMat + texToMatSize;
            int plttToMatSize = NitroDictionary.SizeFor(texNames.Count, 4);
            int listsAt = plttToMat + plttToMatSize;

            // The lists of material numbers each of those entries points at.
            var listAt = new int[texNames.Count];
            int listBytes = 0;
            for (int i = 0; i < texNames.Count; i++)
            { listAt[i] = listsAt + listBytes; listBytes += Math.Max(1, usersOf[i].Count); }

            int dataAt = Align4(listsAt + listBytes);
            var o = new byte[dataAt + count * MatDataSize];
            Put16(o, 0, texToMat);
            Put16(o, 2, plttToMat);

            var entries = new List<byte[]>();
            for (int i = 0; i < count; i++) entries.Add(Word(dataAt + i * MatDataSize));
            byte[] dict = NitroDictionary.Write(names, entries);
            Array.Copy(dict, 0, o, 4, dict.Length);

            var toMat = new List<byte[]>();
            for (int i = 0; i < texNames.Count; i++)
            {
                var e = new byte[4];
                Put16(e, 0, listAt[i]);              // where the list of material numbers sits
                e[2] = (byte)usersOf[i].Count;
                e[3] = 0;                            // not bound yet; the game sets this on load
                toMat.Add(e);
            }
            byte[] texDict = NitroDictionary.Write(texNames, toMat);
            byte[] plttDict = NitroDictionary.Write(texNames, toMat);
            Array.Copy(texDict, 0, o, texToMat, texDict.Length);
            Array.Copy(plttDict, 0, o, plttToMat, plttDict.Length);
            for (int i = 0; i < texNames.Count; i++)
                for (int k = 0; k < usersOf[i].Count; k++) o[listAt[i] + k] = usersOf[i][k];

            for (int i = 0; i < count; i++)
            {
                int a = dataAt + i * MatDataSize;
                var src = mesh.Materials[used[i]];
                var tex = textureOf[i] >= 0 ? textures[textureOf[i]] : null;

                Put16(o, a, 0);                              // the plain sort of material
                Put16(o, a + 2, MatDataSize);
                Put32(o, a + 4, DiffuseAmbient(src));
                Put32(o, a + 8, 0x0000_0000);                // no shine, nothing glowing
                Put32(o, a + 12, PolygonAttr(src, lit));
                Put32(o, a + 16, unchecked((int)0xFFFF_FFFF));
                // What the material says about its picture. HeartGold's own materials carry only the
                // two repeat bits here and leave the size, the shape and where it sits to be filled
                // in when the game binds the picture to the material by name. Writing those in
                // ourselves points the hardware at whatever happens to be in memory at that address,
                // which is how the model came out dark in a real game.
                Put32(o, a + 20, tex != null ? MaterialImageParam : 0);
                Put32(o, a + 24, tex != null ? unchecked((int)0xFFFF_FFFF) : 0);
                Put16(o, a + 28, 0);                         // where its colours sit, filled in on load
                // What this material says for itself. The three picture-placing bits have to say the
                // picture is not scaled, turned or moved, because each of them being off means another
                // few numbers follow this record, and there are none. Saying it uses a placing matrix
                // at all, which an earlier version did, makes readers look for a matrix that is not
                // there. This is the value HeartGold's own materials carry.
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
            return diffuse | (1 << 15) | (ambient << 16);   // the top bit says to take the colour as it is
        }

        private static int PolygonAttr(ObjMesh.Material m, bool lit)
        {
            int alpha = (int)Math.Round(Math.Clamp(m.Opacity, 0, 1) * 31);
            // Both sides drawn, and however see-through the material says. The first light is only
            // turned on when the corners say which way they face; without that it lights everything
            // off nothing and the shape comes out black.
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
                Put16(o, a, 0);                            // the plain sort of shape
                Put16(o, a + 2, ShpDataSize);
                Put32(o, a + 4, shapes[i].Flags);
                Put32(o, a + 8, dlAts[i] - a);             // the list, measured from this shape
                Put32(o, a + 12, shapes[i].Dl.Length);
                Array.Copy(shapes[i].Dl, 0, o, dlAts[i], shapes[i].Dl.Length);
            }
            return o;
        }

        // ── the picture block ─────────────────────────────────────────────────────────────────────

        private static byte[] BuildTex0(IReadOnlyList<DsTexture> textures)
        {
            var names = textures.Select(t => t.Name).ToList();
            int header = 8 + 16 + 20 + 16;                 // block header, then the three run-downs

            // The pictures and their colours are laid out one after another, eight-byte aligned, which
            // is what the offsets in this block are counted in.
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

            var palEntries = new List<byte[]>();
            for (int i = 0; i < textures.Count; i++)
            {
                var e = new byte[4];
                Put16(e, 0, palAt[i] >> 3);
                Put16(e, 2, textures[i].Format == DsTexture.Kind.SixteenColours ? 1 : 0);
                palEntries.Add(e);
            }
            byte[] palDict = NitroDictionary.Write(names, palEntries);

            int texDataAt = Align8(header + texDict.Length + palDict.Length);
            int palDataAt = Align8(texDataAt + texTotal);
            int size = Align4(palDataAt + palTotal);

            var o = new byte[size];
            o[0] = (byte)'T'; o[1] = (byte)'E'; o[2] = (byte)'X'; o[3] = (byte)'0';
            Put32(o, 4, size);

            // The run-down for the ordinary pictures.
            Put16(o, 8 + 4, texTotal >> 3);
            Put16(o, 8 + 6, header);
            Put32(o, 8 + 12, texDataAt);
            // Nothing is in the squeezed-up sort, so its run-down stays empty.
            // The colours.
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

        // ── the wrapper ───────────────────────────────────────────────────────────────────────────

        private static byte[] Envelope(IReadOnlyList<byte[]> blocks)
        {
            int header = Align4(16 + blocks.Count * 4);
            int total = header + blocks.Sum(b => b.Length);
            var o = new byte[total];
            o[0] = (byte)'B'; o[1] = (byte)'M'; o[2] = (byte)'D'; o[3] = (byte)'0';
            o[4] = 0xFF; o[5] = 0xFE;
            Put16(o, 6, 0x0002);
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

        // ── odds and ends ─────────────────────────────────────────────────────────────────────────

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
