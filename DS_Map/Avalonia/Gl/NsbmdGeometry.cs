using System;
using System.Collections.Generic;
using System.Drawing;
using LibNDSFormats.NSBMD;

namespace DSPRE.Avalonia.Gl
{
    /// <summary>One drawable submesh: all triangles that share a material.</summary>
    public sealed class NsbmdMeshPart
    {
        public int MaterialIndex;     // -1 = no material
        public float[] Vertices;      // interleaved pos.xyz, uv.st, col.rgb (8 floats/vertex)
        public int VertexCount;
    }

    /// <summary>A model ready for GL: per-material triangle parts + decoded textures.</summary>
    public sealed class NsbmdRenderModel
    {
        public List<NsbmdMeshPart> Parts = new List<NsbmdMeshPart>();
        public Dictionary<int, NsbmdTextureData> Textures = new Dictionary<int, NsbmdTextureData>();
        public int TotalVertices;

        // Normalization applied to fit the camera: normalized = (raw - Center) * Scale.
        // Exposed so callers (e.g. the permission overlay) can place geometry in the same space.
        public float Cx, Cy, Cz, Scale = 1f;
        public float RawMinX, RawMaxX, RawMinY, RawMaxY, RawMinZ, RawMaxZ;

        // Raw bounds of the MAP model only (excludes buildings) — for fitting the tile-grid overlay.
        public float MapMinX, MapMaxX, MapMinY, MapMaxY, MapMinZ, MapMaxZ;
        public bool HasMapBounds;

        // For stitched matrix scenes: the authored footprint of a single map cell, in raw space.
        // Tile (tx,ty in 0..32) of matrix cell (cx,cy) is at raw
        //   x = CellBaseX + (cx + tx/32) * CellStrideX,  z = CellBaseZ + (cy + ty/32) * CellStrideZ.
        public float CellBaseX, CellBaseZ, CellStrideX, CellStrideZ;
        public bool IsMatrix;

        /// <summary>Maps a raw-space point into the normalized render space.</summary>
        public (float x, float y, float z) ToNormalized(float x, float y, float z)
            => ((x - Cx) * Scale, (y - Cy) * Scale, (z - Cz) * Scale);
    }

    /// <summary>
    /// Self-contained NDS geometry-engine display-list interpreter. Walks an
    /// <see cref="NSBMDModel"/>'s joint matrices, decodes each polygon's GE command
    /// stream (positions, texcoords, the matrix ops and BEGIN/END_VTXS), tessellates
    /// tris/quads/strips into triangles, groups them by material, and pairs each group
    /// with its decoded texture (<see cref="NsbmdTextureDecoder"/>). Positions are
    /// centred and scaled to ~unit size for the orbit camera.
    /// </summary>
    public static class NsbmdGeometry
    {
        /// <summary>Builds a single model, centred/scaled to fit (for the standalone viewer).</summary>
        public static NsbmdRenderModel BuildModel(NSBMDModel model)
        {
            var result = new NsbmdRenderModel();
            var byMat = new Dictionary<int, List<float>>();
            Accumulate(model, null, 0, result, byMat);
            Finalize(result, byMat);
            NormalizePositions(result);
            return result;
        }

        /// <summary>
        /// Builds a combined scene: the map model plus each building transformed into map
        /// space, with unique material keys per source model. Centred/scaled once at the end.
        /// </summary>
        public static NsbmdRenderModel BuildScene(NSBMDModel map, IReadOnlyList<(NSBMDModel model, float[] transform)> buildings)
        {
            var result = new NsbmdRenderModel();
            var byMat = new Dictionary<int, List<float>>();
            int offset = 0;

            if (map != null)
            {
                Accumulate(map, null, offset, result, byMat);
                offset += Math.Max(1, map.Materials.Count);
                // Capture the map-only raw bounds (before buildings are added) for the overlay.
                if (ComputeRawBounds(byMat, out float mnx, out float mxx, out float mny, out float mxy, out float mnz, out float mxz))
                {
                    result.MapMinX = mnx; result.MapMaxX = mxx; result.MapMinY = mny;
                    result.MapMaxY = mxy; result.MapMinZ = mnz; result.MapMaxZ = mxz;
                    result.HasMapBounds = true;
                }
            }
            if (buildings != null)
                foreach (var b in buildings)
                {
                    if (b.model == null) continue;
                    Accumulate(b.model, b.transform, offset, result, byMat);
                    offset += Math.Max(1, b.model.Materials.Count);
                }

            Finalize(result, byMat);
            NormalizePositions(result);
            return result;
        }

        /// <summary>One matrix cell's geometry: its map model, its buildings (already transformed
        /// into the map's local space) and its position in the matrix grid.</summary>
        public sealed class MatrixCellGeometry
        {
            public NSBMDModel Map;
            public IReadOnlyList<(NSBMDModel model, float[] transform)> Buildings;
            public int CellX, CellY;
        }

        /// <summary>
        /// Builds a stitched scene from several matrix cells: every cell's map (and its buildings)
        /// is translated to its grid position so the whole matrix is visible at once, then the
        /// combined model is centred/scaled once for the orbit camera. The single-map footprint
        /// (stride) is measured from the first cell that has a map and assumed uniform.
        /// </summary>
        public static NsbmdRenderModel BuildMatrixScene(IReadOnlyList<MatrixCellGeometry> cells)
        {
            var result = new NsbmdRenderModel { IsMatrix = true };
            var byMat = new Dictionary<int, List<float>>();
            int offset = 0;

            // Measure a single map's footprint to use as the per-cell stride.
            float strideX = 0, strideZ = 0, baseX = 0, baseZ = 0;
            foreach (var cell in cells)
            {
                if (cell.Map == null) continue;
                var probe = new Dictionary<int, List<float>>();
                var scratch = new NsbmdRenderModel();
                Accumulate(cell.Map, null, 0, scratch, probe);
                if (ComputeRawBounds(probe, out float mnx, out float mxx, out float _, out float _, out float mnz, out float mxz))
                {
                    strideX = mxx - mnx; strideZ = mxz - mnz;
                    baseX = mnx; baseZ = mnz;   // authored min of a cell, before grid offset
                }
                break;
            }
            if (strideX <= 0) strideX = 1f;
            if (strideZ <= 0) strideZ = 1f;

            // Accumulate every cell at its grid offset.
            foreach (var cell in cells)
            {
                float ox = cell.CellX * strideX, oz = cell.CellY * strideZ;
                var cellOffset = Mat4.Translate(ox, 0f, oz);

                if (cell.Map != null)
                {
                    Accumulate(cell.Map, cellOffset, offset, result, byMat);
                    offset += Math.Max(1, cell.Map.Materials.Count);
                }
                if (cell.Buildings != null)
                    foreach (var b in cell.Buildings)
                    {
                        if (b.model == null) continue;
                        var t = b.transform != null ? Mat4.Multiply(cellOffset, b.transform) : cellOffset;
                        Accumulate(b.model, t, offset, result, byMat);
                        offset += Math.Max(1, b.model.Materials.Count);
                    }
            }

            // Whole stitched footprint becomes the "map bounds" (for overlays); record cell stride.
            if (ComputeRawBounds(byMat, out float wmnx, out float wmxx, out float wmny, out float wmxy, out float wmnz, out float wmxz))
            {
                result.MapMinX = wmnx; result.MapMaxX = wmxx; result.MapMinY = wmny;
                result.MapMaxY = wmxy; result.MapMinZ = wmnz; result.MapMaxZ = wmxz;
                result.HasMapBounds = true;
            }
            result.CellBaseX = baseX; result.CellBaseZ = baseZ;
            result.CellStrideX = strideX; result.CellStrideZ = strideZ;

            Finalize(result, byMat);
            NormalizePositions(result);
            return result;
        }

        private static void Accumulate(NSBMDModel model, float[] sceneTransform, int matOffset,
            NsbmdRenderModel target, Dictionary<int, List<float>> byMat)
        {
            if (model == null || model.Polygons.Count == 0) return;

            var stack = new MTX44[32];
            for (int i = 0; i < stack.Length; i++) { stack[i] = new MTX44(); stack[i].LoadIdentity(); }
            var running = new MTX44(); running.LoadIdentity();
            foreach (var obj in model.Objects)
            {
                if (obj.RestoreID != -1) running = stack[obj.RestoreID].Clone();
                if (obj.StackID != -1)
                {
                    if (obj.visible)
                    {
                        var b = new MTX44(); b.SetValues(obj.materix);
                        running = running.MultMatrix(b);
                    }
                    else { running = running.Clone(); running.Zero(); }
                    stack[obj.StackID & 0x1f] = running.Clone();
                }
            }

            foreach (var poly in model.Polygons)
            {
                int matId = poly.MatId;
                NSBMDMaterial mat = (matId >= 0 && matId < model.Materials.Count) ? model.Materials[matId] : null;
                int key = matOffset + matId;

                Color c = mat?.DiffuseColor ?? Color.LightGray;
                float r = c.R / 255f, g = c.G / 255f, b = c.B / 255f;
                if (r + g + b < 0.05f) { r = g = b = 0.85f; }

                if (!byMat.TryGetValue(key, out var list)) { list = new List<float>(); byMat[key] = list; }
                InterpretPolyData(poly.PolyData, poly.StackID, stack, model.modelScale, mat, sceneTransform, list, r, g, b);

                if (mat != null && !target.Textures.ContainsKey(key))
                {
                    var tex = NsbmdTextureDecoder.Decode(mat);
                    if (tex != null) target.Textures[key] = tex;
                }
            }
        }

        private static bool ComputeRawBounds(Dictionary<int, List<float>> byMat,
            out float minX, out float maxX, out float minY, out float maxY, out float minZ, out float maxZ)
        {
            minX = minY = minZ = float.MaxValue; maxX = maxY = maxZ = float.MinValue;
            bool any = false;
            foreach (var list in byMat.Values)
                for (int i = 0; i + 2 < list.Count; i += 8)
                {
                    any = true;
                    minX = Math.Min(minX, list[i]); maxX = Math.Max(maxX, list[i]);
                    minY = Math.Min(minY, list[i + 1]); maxY = Math.Max(maxY, list[i + 1]);
                    minZ = Math.Min(minZ, list[i + 2]); maxZ = Math.Max(maxZ, list[i + 2]);
                }
            return any;
        }

        private static void Finalize(NsbmdRenderModel result, Dictionary<int, List<float>> byMat)
        {
            foreach (var kv in byMat)
            {
                if (kv.Value.Count == 0) continue;
                result.Parts.Add(new NsbmdMeshPart { MaterialIndex = kv.Key, Vertices = kv.Value.ToArray(), VertexCount = kv.Value.Count / 8 });
                result.TotalVertices += kv.Value.Count / 8;
            }
        }

        private static int S32(byte[] d, ref int i) { int v = BitConverter.ToInt32(d, i); i += 4; return v; }

        private static void InterpretPolyData(byte[] poly, int polyStackId, MTX44[] stack, float modelScale,
            NSBMDMaterial mat, float[] sceneTransform, List<float> outVerts, float cr, float cg, float cb)
        {
            if (poly == null || poly.Length == 0) return;

            float texW = mat != null && mat.width > 0 ? mat.width : 1f;
            float texH = mat != null && mat.height > 0 ? mat.height : 1f;
            float scaleS = mat?.scaleS ?? 1f, scaleT = mat?.scaleT ?? 1f;
            int flipS = mat?.flipS ?? 0, flipT = mat?.flipT ?? 0;

            var cur = new MTX44(); cur.LoadIdentity();
            int stackId = polyStackId;
            if (stackId >= 0 && stackId < stack.Length) stack[stackId & 0x1f].CopyValuesTo(cur);

            var v = new float[3];
            float u = 0f, w = 0f;                 // current texcoord
            var prim = new List<float[]>();       // each entry: pos.xyz + uv.st (5 floats)
            int primType = -1;

            int idx = 0, len = poly.Length;
            while (idx < len)
            {
                var cmds = new int[4];
                for (int k = 0; k < 4; k++) cmds[k] = idx < len ? poly[idx++] : 0xff;

                for (int k = 0; k < 4 && idx < len; k++)
                {
                    switch (cmds[k])
                    {
                        case 0x14: stackId = S32(poly, ref idx) & 0x1f; stack[stackId].CopyValuesTo(cur); break;
                        case 0x15: cur.LoadIdentity(); break;
                        case 0x16: for (int n = 0; n < 16; n++) cur[n] = S32(poly, ref idx) / 4096f; break;
                        case 0x17: LoadOrMult(poly, ref idx, cur, 12, true); break;
                        case 0x18: LoadOrMult(poly, ref idx, cur, 16, false); break;
                        case 0x19: LoadOrMult(poly, ref idx, cur, 12, false); break;
                        case 0x1a: LoadOrMult(poly, ref idx, cur, 9, false); break;
                        case 0x1b:
                        {
                            float sx = S32(poly, ref idx) / 4096f / modelScale;
                            float sy = S32(poly, ref idx) / 4096f / modelScale;
                            float sz = S32(poly, ref idx) / 4096f / modelScale;
                            cur.Scale(sx, sy, sz); break;
                        }
                        case 0x1c:
                        {
                            float tx = NSBMDGlRenderer.Sign(S32(poly, ref idx), 0x20) / 4096f / modelScale;
                            float ty = NSBMDGlRenderer.Sign(S32(poly, ref idx), 0x20) / 4096f / modelScale;
                            float tz = NSBMDGlRenderer.Sign(S32(poly, ref idx), 0x20) / 4096f / modelScale;
                            cur.translate(tx, ty, tz); break;
                        }
                        case 0x20: idx += 4; break;   // COLOR (per-vertex colour ignored)
                        case 0x21: idx += 4; break;   // NORMAL (lighting deferred)
                        case 0x22:                    // TEXCOORD
                        {
                            int p = S32(poly, ref idx);
                            int s = NSBMDGlRenderer.Sign(p & 0xffff, 0x10);
                            int tt = NSBMDGlRenderer.Sign((p >> 16) & 0xffff, 0x10);
                            u = (scaleS / texW) * (s / 16f) / (flipS + 1);
                            // GL samples textures bottom-up vs the DS top-down convention, so V is flipped.
                            w = (scaleT / texH) * (tt / 16f) / (flipT + 1);
                            break;
                        }
                        case 0x23:
                        {
                            int p0 = S32(poly, ref idx), p1 = S32(poly, ref idx);
                            v[0] = NSBMDGlRenderer.Sign(p0 & 0xffff, 0x10) / 4096f;
                            v[1] = NSBMDGlRenderer.Sign((p0 >> 16) & 0xffff, 0x10) / 4096f;
                            v[2] = NSBMDGlRenderer.Sign(p1 & 0xffff, 0x10) / 4096f;
                            Emit(cur, stackId, v, u, w, sceneTransform, prim); break;
                        }
                        case 0x24:
                        {
                            int p = S32(poly, ref idx);
                            v[0] = NSBMDGlRenderer.Sign(p & 0x3ff, 10) / 64f;
                            v[1] = NSBMDGlRenderer.Sign((p >> 10) & 0x3ff, 10) / 64f;
                            v[2] = NSBMDGlRenderer.Sign((p >> 20) & 0x3ff, 10) / 64f;
                            Emit(cur, stackId, v, u, w, sceneTransform, prim); break;
                        }
                        case 0x25:
                        {
                            int p = S32(poly, ref idx);
                            v[0] = NSBMDGlRenderer.Sign(p & 0xffff, 0x10) / 4096f;
                            v[1] = NSBMDGlRenderer.Sign((p >> 16) & 0xffff, 0x10) / 4096f;
                            Emit(cur, stackId, v, u, w, sceneTransform, prim); break;
                        }
                        case 0x26:
                        {
                            int p = S32(poly, ref idx);
                            v[0] = NSBMDGlRenderer.Sign(p & 0xffff, 0x10) / 4096f;
                            v[2] = NSBMDGlRenderer.Sign((p >> 16) & 0xffff, 0x10) / 4096f;
                            Emit(cur, stackId, v, u, w, sceneTransform, prim); break;
                        }
                        case 0x27:
                        {
                            int p = S32(poly, ref idx);
                            v[1] = NSBMDGlRenderer.Sign(p & 0xffff, 0x10) / 4096f;
                            v[2] = NSBMDGlRenderer.Sign((p >> 16) & 0xffff, 0x10) / 4096f;
                            Emit(cur, stackId, v, u, w, sceneTransform, prim); break;
                        }
                        case 0x28:
                        {
                            int p = S32(poly, ref idx);
                            v[0] += NSBMDGlRenderer.Sign(p & 0x3ff, 10) / 4096f;
                            v[1] += NSBMDGlRenderer.Sign((p >> 10) & 0x3ff, 10) / 4096f;
                            v[2] += NSBMDGlRenderer.Sign((p >> 20) & 0x3ff, 10) / 4096f;
                            Emit(cur, stackId, v, u, w, sceneTransform, prim); break;
                        }
                        case 0x40: primType = S32(poly, ref idx); prim.Clear(); break;
                        case 0x41: Tessellate(prim, primType, outVerts, cr, cg, cb); prim.Clear(); break;

                        case 0x10: case 0x12: case 0x13: idx += 4; break;
                        case 0x29: case 0x2a: case 0x2b: idx += 4; break;
                        case 0x30: case 0x31: case 0x32: case 0x33: idx += 4; break;
                        case 0x34: idx += 0x80; break;
                        case 0x50: case 0x60: idx += 4; break;
                        case 0x70: idx += 12; break;
                        case 0x71: idx += 8; break;
                        case 0x72: idx += 4; break;
                        default: break;
                    }
                }
            }
        }

        private static void LoadOrMult(byte[] poly, ref int idx, MTX44 cur, int count, bool load)
        {
            var m = new MTX44(); m.LoadIdentity();
            if (count == 16) for (int n = 0; n < 16; n++) m[n] = S32(poly, ref idx) / 4096f;
            else if (count == 12) for (int col = 0; col < 4; col++) for (int row = 0; row < 3; row++) m[col * 4 + row] = S32(poly, ref idx) / 4096f;
            else for (int col = 0; col < 3; col++) for (int row = 0; row < 3; row++) m[col * 4 + row] = S32(poly, ref idx) / 4096f;
            if (load) m.CopyValuesTo(cur);
            else cur.MultMatrix(m).CopyValuesTo(cur);
        }

        private static void Emit(MTX44 cur, int stackId, float[] v, float u, float w, float[] sceneTransform, List<float[]> prim)
        {
            float x = v[0], y = v[1], z = v[2];
            if (stackId >= 0) { var t = cur.MultVector(v); x = t[0]; y = t[1]; z = t[2]; }
            if (sceneTransform != null) Mat4.TransformPoint(sceneTransform, ref x, ref y, ref z);
            prim.Add(new[] { x, y, z, u, w });
        }

        private static void Tessellate(List<float[]> p, int type, List<float> outv, float r, float g, float b)
        {
            void Vtx(float[] a) { outv.Add(a[0]); outv.Add(a[1]); outv.Add(a[2]); outv.Add(a[3]); outv.Add(a[4]); outv.Add(r); outv.Add(g); outv.Add(b); }
            void Tri(float[] a, float[] bb, float[] c) { Vtx(a); Vtx(bb); Vtx(c); }

            switch (type)
            {
                case 0:
                    for (int i = 0; i + 2 < p.Count; i += 3) Tri(p[i], p[i + 1], p[i + 2]);
                    break;
                case 1:
                    for (int i = 0; i + 3 < p.Count; i += 4) { Tri(p[i], p[i + 1], p[i + 2]); Tri(p[i], p[i + 2], p[i + 3]); }
                    break;
                case 2:
                    for (int i = 2; i < p.Count; i++)
                    {
                        if ((i & 1) == 0) Tri(p[i - 2], p[i - 1], p[i]);
                        else Tri(p[i - 1], p[i - 2], p[i]);
                    }
                    break;
                case 3:
                    for (int i = 0; i + 3 < p.Count; i += 2) { Tri(p[i], p[i + 1], p[i + 3]); Tri(p[i], p[i + 3], p[i + 2]); }
                    break;
            }
        }

        private static void NormalizePositions(NsbmdRenderModel model)
        {
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
            foreach (var part in model.Parts)
                for (int i = 0; i < part.Vertices.Length; i += 8)
                {
                    minX = Math.Min(minX, part.Vertices[i]); maxX = Math.Max(maxX, part.Vertices[i]);
                    minY = Math.Min(minY, part.Vertices[i + 1]); maxY = Math.Max(maxY, part.Vertices[i + 1]);
                    minZ = Math.Min(minZ, part.Vertices[i + 2]); maxZ = Math.Max(maxZ, part.Vertices[i + 2]);
                }
            if (minX > maxX) return;

            float cx = (minX + maxX) / 2, cy = (minY + maxY) / 2, cz = (minZ + maxZ) / 2;
            float extent = Math.Max(maxX - minX, Math.Max(maxY - minY, maxZ - minZ));
            if (extent < 1e-6f) extent = 1f;
            float scale = 2f / extent;

            model.Cx = cx; model.Cy = cy; model.Cz = cz; model.Scale = scale;
            model.RawMinX = minX; model.RawMaxX = maxX;
            model.RawMinY = minY; model.RawMaxY = maxY;
            model.RawMinZ = minZ; model.RawMaxZ = maxZ;

            foreach (var part in model.Parts)
                for (int i = 0; i < part.Vertices.Length; i += 8)
                {
                    part.Vertices[i] = (part.Vertices[i] - cx) * scale;
                    part.Vertices[i + 1] = (part.Vertices[i + 1] - cy) * scale;
                    part.Vertices[i + 2] = (part.Vertices[i + 2] - cz) * scale;
                }
        }
    }
}
