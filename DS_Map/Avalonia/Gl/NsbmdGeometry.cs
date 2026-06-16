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

        // Debug gizmo lines (8 floats/vertex, normalized space): per-cell the expected 32×32 tile-cell
        // boundary (cyan) and the actual map-geometry extent (yellow), so the user can SEE whether a map
        // fills its 32×32 cell or is partial/misplaced.
        public float[] GizmoMesh;
        public int GizmoVertexCount;

        // For stitched matrix scenes: the authored footprint of a single map cell, in raw space.
        // Tile (tx,ty in 0..32) of matrix cell (cx,cy) is at raw
        //   x = CellBaseX + (cx + tx/32) * CellStrideX,  z = CellBaseZ + (cy + ty/32) * CellStrideZ.
        // (Kept as a representative average; prefer per-cell CellPlacements for exact placement.)
        public float CellBaseX, CellBaseZ, CellStrideX, CellStrideZ;
        public bool IsMatrix;

        // Per-matrix-cell placement: each map is laid out CONTINUOUSLY (cumulative column widths /
        // row heights) and min-aligned so its actual geometry box tiles edge-to-edge with neighbours.
        // OriginX/Z is the map's min-corner (its tile-(0,0)) in raw space; Width/Height is its real
        // footprint. Event tile (tx,ty) of cell (cx,cy) sits at OriginX + (tx/32)*Width, etc.
        public struct CellPlacement { public float OriginX, OriginZ, Width, Height; }
        public Dictionary<long, CellPlacement> CellPlacements;
        public static long CellKey(int cx, int cy) => ((long)cx << 32) | (uint)cy;
        public bool TryCellPlacement(int cx, int cy, out CellPlacement p)
        {
            p = default;
            return CellPlacements != null && CellPlacements.TryGetValue(CellKey(cx, cy), out p);
        }

        /// <summary>Inverse of cell placement: finds which matrix cell + tile (0..32) a raw-space point
        /// falls in (the cell containing it, else the nearest). Used to drag events across maps.</summary>
        public bool TryRawToTile(float rawX, float rawZ, out int matX, out int matY, out float tileFx, out float tileFy)
        {
            matX = matY = 0; tileFx = tileFy = 0f;
            if (CellPlacements == null || CellPlacements.Count == 0) return false;
            long bestKey = 0; float bestD = float.MaxValue; bool inside = false;
            foreach (var kv in CellPlacements)
            {
                var p = kv.Value;
                bool within = rawX >= p.OriginX && rawX <= p.OriginX + p.Width && rawZ >= p.OriginZ && rawZ <= p.OriginZ + p.Height;
                if (within) { bestKey = kv.Key; inside = true; break; }
                float ccx = p.OriginX + p.Width * 0.5f, ccz = p.OriginZ + p.Height * 0.5f;
                float d = (rawX - ccx) * (rawX - ccx) + (rawZ - ccz) * (rawZ - ccz);
                if (d < bestD) { bestD = d; bestKey = kv.Key; }
            }
            if (!inside && bestD == float.MaxValue) return false;
            var bp = CellPlacements[bestKey];
            matX = (int)(bestKey >> 32); matY = (int)(uint)bestKey;
            tileFx = bp.Width > 0 ? (rawX - bp.OriginX) / bp.Width * 32f : 0f;
            tileFy = bp.Height > 0 ? (rawZ - bp.OriginZ) / bp.Height * 32f : 0f;
            return true;
        }

        // Per-tile top-surface height of the MAP geometry (raw space), so events can sit on the
        // floor rather than the scene's lowest point. NaN cells fall back to DefaultSurfaceY.
        public float[] HeightGrid;
        public int HCols, HRows;
        public float HOriginX, HOriginZ, HTileX, HTileZ, DefaultSurfaceY;

        /// <summary>Top-of-floor Y (raw space) under a point, or the fallback when off-grid.</summary>
        public float SurfaceY(float x, float z)
        {
            if (HeightGrid == null || HTileX <= 0 || HTileZ <= 0) return DefaultSurfaceY;
            int c = (int)Math.Floor((x - HOriginX) / HTileX);
            int r = (int)Math.Floor((z - HOriginZ) / HTileZ);
            if (c < 0 || r < 0 || c >= HCols || r >= HRows) return DefaultSurfaceY;
            float v = HeightGrid[r * HCols + c];
            return float.IsNaN(v) ? DefaultSurfaceY : v;
        }

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

        // A map is a 32×32 tile grid. One tile is 256/1024 raw units in the map-model coordinate space
        // (the same factor the building-placement transform uses: sf*tf = (ms/1024)*(256/ms) = 0.25),
        // so a full map cell spans exactly MapStride. These are fixed — they do NOT depend on how much
        // geometry a given map has, which is what lets under-filled maps still sit in the right grid cell.
        public const int MapTiles = 32;
        public const float TileSize = 256f / 1024f;        // 0.25
        public const float MapStride = MapTiles * TileSize; // 8.0

        private sealed class CellBuild
        {
            public int CellX, CellY;
            public Dictionary<int, List<float>> MapMats;
            public Dictionary<int, List<float>> BldMats;
            public float MinX, MinZ, FpX, FpZ; public bool HasBounds;
            public float OffX, OffZ;        // geometry placement offset (added to raw verts)
            public float ColW, RowH;        // allotted cell width/height (max in column/row)
        }

        /// <summary>
        /// Builds a stitched scene from several matrix cells: every cell's map (and its buildings)
        /// is translated to its grid position so the whole matrix is visible at once. The per-cell
        /// stride is the MEDIAN map footprint (robust to maps whose decorative borders overrun the
        /// 32-tile play area, which otherwise leaves gaps). A per-tile top-surface height grid is
        /// captured so callers can drop events onto the floor. Centred/scaled once at the end.
        /// </summary>
        /// <summary>How matrix cells are laid out in the stitched scene.</summary>
        public enum MatrixStitchMode
        {
            /// <summary>Variable-size: each column/row sized to its widest/tallest map's geometry, maps min-aligned
            /// so neighbours' actual geometry touches (handles decorative overhang/underfill, but a strict grid
            /// can't make ALL cells touch when sizes vary within a column/row).</summary>
            Continuous,
            /// <summary>DS-true fixed 32-tile grid: every cell is exactly MapStride apart; underfilled maps show
            /// real gaps and overhang overlaps as authored. Tiles always map to the standard 0.25-unit size.</summary>
            Grid,
        }

        public static NsbmdRenderModel BuildMatrixScene(IReadOnlyList<MatrixCellGeometry> cells,
            MatrixStitchMode mode = MatrixStitchMode.Continuous)
        {
            bool grid = mode == MatrixStitchMode.Grid;
            var result = new NsbmdRenderModel { IsMatrix = true };
            int offset = 0;

            // Phase 1 — interpret each cell once into temp buffers; collect map footprints + mins.
            var stored = new List<CellBuild>();
            var fxList = new List<float>(); var fzList = new List<float>();
            int minCx = int.MaxValue, minCy = int.MaxValue, maxCx = int.MinValue, maxCy = int.MinValue;

            foreach (var cell in cells)
            {
                var mapMats = new Dictionary<int, List<float>>();
                float cMinX = 0, cMinZ = 0, cFpX = 0, cFpZ = 0; bool cHas = false;
                if (cell.Map != null)
                {
                    Accumulate(cell.Map, null, offset, result, mapMats);
                    offset += Math.Max(1, cell.Map.Materials.Count);
                    if (ComputeRawBounds(mapMats, out float mnx, out float mxx, out float _, out float _, out float mnz, out float mxz))
                    {
                        cFpX = mxx - mnx; cFpZ = mxz - mnz;
                        fxList.Add(cFpX); fzList.Add(cFpZ);
                        cMinX = mnx; cMinZ = mnz; cHas = true;
                    }
                }
                var bldMats = new Dictionary<int, List<float>>();
                if (cell.Buildings != null)
                    foreach (var b in cell.Buildings)
                    {
                        if (b.model == null) continue;
                        Accumulate(b.model, b.transform, offset, result, bldMats);
                        offset += Math.Max(1, b.model.Materials.Count);
                    }
                stored.Add(new CellBuild { CellX = cell.CellX, CellY = cell.CellY, MapMats = mapMats, BldMats = bldMats, MinX = cMinX, MinZ = cMinZ, FpX = cFpX, FpZ = cFpZ, HasBounds = cHas });
                minCx = Math.Min(minCx, cell.CellX); maxCx = Math.Max(maxCx, cell.CellX);
                minCy = Math.Min(minCy, cell.CellY); maxCy = Math.Max(maxCy, cell.CellY);
            }
            // Maps come in DIFFERENT real sizes (decorative overhang, under-filled play areas), so a single
            // uniform stride leaves them spread apart with gaps. Instead lay them out CONTINUOUSLY: each
            // matrix column gets the width of its widest map, each row the height of its tallest, and offsets
            // accumulate so consecutive maps' actual geometry boxes tile edge-to-edge (touch, never overlap).
            // Grid mode forces every column/row to the fixed 32-tile stride; Continuous sizes by actual geometry.
            float fallback = grid ? MapStride : Math.Max(Mode(fxList), Mode(fzList));
            if (fallback <= 0) fallback = MapStride;

            var colW = new Dictionary<int, float>();
            var rowH = new Dictionary<int, float>();
            if (!grid)
                foreach (var cb in stored)
                {
                    if (!cb.HasBounds) continue;
                    if (!colW.TryGetValue(cb.CellX, out float w) || cb.FpX > w) colW[cb.CellX] = cb.FpX;
                    if (!rowH.TryGetValue(cb.CellY, out float h) || cb.FpZ > h) rowH[cb.CellY] = cb.FpZ;
                }
            // (Grid mode leaves colW/rowH empty → the colX/rowZ accumulators below use fallback = MapStride.)
            // Cumulative column-start X / row-start Z (in matrix order), filling empty cols/rows with fallback.
            var colX = new Dictionary<int, float>();
            float accX = 0f;
            for (int cx = minCx; cx <= maxCx; cx++)
            {
                colX[cx] = accX;
                accX += colW.TryGetValue(cx, out float w) ? w : fallback;
            }
            var rowZ = new Dictionary<int, float>();
            float accZ = 0f;
            for (int cy = minCy; cy <= maxCy; cy++)
            {
                rowZ[cy] = accZ;
                accZ += rowH.TryGetValue(cy, out float h) ? h : fallback;
            }

            // Place each map MIN-ALIGNED to its column/row start so its geometry box's min corner (its
            // authored tile-(0,0)) lands exactly on the grid line shared with the previous neighbour.
            var byMat = new Dictionary<int, List<float>>();
            var placements = new Dictionary<long, NsbmdRenderModel.CellPlacement>();
            foreach (var cb in stored)
            {
                float ox = colX[cb.CellX], oz = rowZ[cb.CellY];
                cb.ColW = colW.TryGetValue(cb.CellX, out float cw) ? cw : fallback;
                cb.RowH = rowH.TryGetValue(cb.CellY, out float ch) ? ch : fallback;
                // GRID: place the map's AUTHORED ORIGIN (tile-(0,0) at raw 0) on the cell corner, so every
                // map's play area lands on the shared grid line and aligns with its events — regardless of
                // decorative overhang. (Min-aligning here would shove each map right/down by its overhang,
                // the "greedy" misalignment that grows with overhang size.)
                // CONTINUOUS: min-align so each map's actual geometry box tiles edge-to-edge with neighbours.
                cb.OffX = grid ? ox : (ox - cb.MinX);
                cb.OffZ = grid ? oz : (oz - cb.MinZ);
                MergeOffset(cb.MapMats, byMat, cb.OffX, cb.OffZ);
                MergeOffset(cb.BldMats, byMat, cb.OffX, cb.OffZ);
                // Events ALWAYS anchor at the map's actual geometry-min corner (its tile-(0,0) "start", which
                // can be at negative raw coords from decorative geometry) and span its real footprint — so they
                // sit on the map regardless of how the MAPS are stitched (grid vs continuous). The geometry-min
                // world position is OffX + cb.MinX (continuous min-aligns → that's ox; grid → ox + cb.MinX).
                placements[NsbmdRenderModel.CellKey(cb.CellX, cb.CellY)] = new NsbmdRenderModel.CellPlacement
                {
                    OriginX = cb.OffX + cb.MinX, OriginZ = cb.OffZ + cb.MinZ,
                    Width  = cb.HasBounds ? cb.FpX : cb.ColW,
                    Height = cb.HasBounds ? cb.FpZ : cb.RowH,
                };
            }
            result.CellPlacements = placements;

            if (ComputeRawBounds(byMat, out float wmnx, out float wmxx, out float wmny, out float wmxy, out float wmnz, out float wmxz))
            {
                result.MapMinX = wmnx; result.MapMaxX = wmxx; result.MapMinY = wmny;
                result.MapMaxY = wmxy; result.MapMinZ = wmnz; result.MapMaxZ = wmxz;
                result.HasMapBounds = true;
            }
            // Representative stride (average column/row size) kept for legacy callers; exact placement uses CellPlacements.
            float repStride = Math.Max(accX / Math.Max(1, maxCx - minCx + 1), accZ / Math.Max(1, maxCy - minCy + 1));
            result.CellBaseX = 0f; result.CellBaseZ = 0f;
            result.CellStrideX = repStride; result.CellStrideZ = repStride;

            BuildHeightGrid(result, stored, minCx, minCy, maxCx, maxCy);

            Finalize(result, byMat);
            NormalizePositions(result);     // sets Cx/Cy/Cz/Scale used by ToNormalized
            BuildGizmos(result, stored);
            return result;
        }

        /// <summary>Per-tile top-surface (max Y) of the MAP geometry, for dropping events on the floor.
        /// A spatial hash over the scene's actual world bounds (variable-size maps mean tiles no longer
        /// align to a uniform stride, but SurfaceY only needs a fine bucket grid to look up max-Y).</summary>
        private static void BuildHeightGrid(NsbmdRenderModel result, List<CellBuild> stored,
            int minCx, int minCy, int maxCx, int maxCy)
        {
            if (stored.Count == 0 || maxCx < minCx || !result.HasMapBounds) return;
            const int Per = 32;
            int cols = (maxCx - minCx + 1) * Per, rows = (maxCy - minCy + 1) * Per;
            if (cols <= 0 || rows <= 0) return;
            float spanX = result.MapMaxX - result.MapMinX, spanZ = result.MapMaxZ - result.MapMinZ;
            if (spanX <= 0 || spanZ <= 0) return;
            float tileX = spanX / cols, tileZ = spanZ / rows;
            float originX = result.MapMinX, originZ = result.MapMinZ;
            var grid = new float[cols * rows];
            for (int i = 0; i < grid.Length; i++) grid[i] = float.NaN;

            foreach (var cb in stored)
            {
                float ox = cb.OffX, oz = cb.OffZ;
                foreach (var list in cb.MapMats.Values)
                    for (int i = 0; i + 2 < list.Count; i += 8)
                    {
                        float x = list[i] + ox, y = list[i + 1], z = list[i + 2] + oz;
                        int c = (int)Math.Floor((x - originX) / tileX);
                        int r = (int)Math.Floor((z - originZ) / tileZ);
                        if (c < 0 || r < 0 || c >= cols || r >= rows) continue;
                        int idx = r * cols + c;
                        if (float.IsNaN(grid[idx]) || y > grid[idx]) grid[idx] = y;
                    }
            }

            // Representative floor (median of sampled tiles) as the fallback — NOT the global max,
            // which would launch events on un-sampled tiles up to the tallest geometry in the scene.
            var samples = new List<float>();
            foreach (var g in grid) if (!float.IsNaN(g)) samples.Add(g);
            float def = samples.Count > 0 ? Median(samples) : (result.HasMapBounds ? result.MapMinY : 0f);

            // Fill empty tiles (void/edge gaps) by spreading from sampled neighbours so events there
            // still land near the floor. Skipped for very large grids (the map editor doesn't query it).
            if ((long)cols * rows <= 200000)
                for (int pass = 0; pass < 24; pass++)
                {
                    bool changed = false;
                    var src = (float[])grid.Clone();
                    for (int r = 0; r < rows; r++)
                        for (int c = 0; c < cols; c++)
                        {
                            int idx = r * cols + c;
                            if (!float.IsNaN(src[idx])) continue;
                            float sum = 0; int n = 0;
                            if (c > 0 && !float.IsNaN(src[idx - 1])) { sum += src[idx - 1]; n++; }
                            if (c < cols - 1 && !float.IsNaN(src[idx + 1])) { sum += src[idx + 1]; n++; }
                            if (r > 0 && !float.IsNaN(src[idx - cols])) { sum += src[idx - cols]; n++; }
                            if (r < rows - 1 && !float.IsNaN(src[idx + cols])) { sum += src[idx + cols]; n++; }
                            if (n > 0) { grid[idx] = sum / n; changed = true; }
                        }
                    if (!changed) break;
                }

            result.HeightGrid = grid; result.HCols = cols; result.HRows = rows;
            result.HOriginX = originX; result.HOriginZ = originZ; result.HTileX = tileX; result.HTileZ = tileZ;
            result.DefaultSurfaceY = def;
        }

        /// <summary>Builds debug gizmo lines: per cell, the allotted cell boundary (cyan) and the actual
        /// map-geometry extent (yellow). Both share the min corner now that maps are corner-aligned, so a
        /// smaller yellow inside cyan = a genuinely under-filled map; matching = a full map. Normalized space.</summary>
        private static void BuildGizmos(NsbmdRenderModel m, List<CellBuild> stored)
        {
            if (stored.Count == 0) return;
            float ext = (m.HasMapBounds ? m.MapMaxX - m.MapMinX : m.RawMaxX - m.RawMinX);
            float y = (m.HasMapBounds ? m.MapMaxY : m.RawMaxY) + ext * 0.004f;
            float lw = ext * 0.0015f;               // line half-width in raw units
            var v = new List<float>(stored.Count * 96);

            foreach (var cb in stored)
            {
                // The min corner (column/row start) shared by both boxes.
                float x0 = cb.OffX + cb.MinX, z0 = cb.OffZ + cb.MinZ;
                // Allotted cell boundary (cyan): the continuous grid slot this map occupies.
                Rect(v, m, x0, z0, x0 + cb.ColW, z0 + cb.RowH, y, lw, 0.1f, 0.9f, 1.0f);
                // Actual geometry extent (yellow), if measured.
                if (cb.HasBounds)
                    Rect(v, m, x0, z0, x0 + cb.FpX, z0 + cb.FpZ, y, lw, 1.0f, 0.85f, 0.1f);
            }

            m.GizmoMesh = v.ToArray();
            m.GizmoVertexCount = v.Count / 8;
        }

        private static void Rect(List<float> v, NsbmdRenderModel m, float x0, float z0, float x1, float z1, float y, float w, float r, float g, float b)
        {
            Line(v, m, x0, z0, x1, z0, y, w, r, g, b);
            Line(v, m, x1, z0, x1, z1, y, w, r, g, b);
            Line(v, m, x1, z1, x0, z1, y, w, r, g, b);
            Line(v, m, x0, z1, x0, z0, y, w, r, g, b);
        }

        private static void Line(List<float> v, NsbmdRenderModel m, float x0, float z0, float x1, float z1, float y, float w, float r, float g, float b)
        {
            float dx = x1 - x0, dz = z1 - z0; float len = (float)Math.Sqrt(dx * dx + dz * dz); if (len < 1e-6f) return;
            float px = -dz / len * w, pz = dx / len * w;   // perpendicular, scaled to half-width
            var a = m.ToNormalized(x0 + px, y, z0 + pz);
            var bb = m.ToNormalized(x1 + px, y, z1 + pz);
            var c = m.ToNormalized(x1 - px, y, z1 - pz);
            var d = m.ToNormalized(x0 - px, y, z0 - pz);
            void Vtx((float x, float y, float z) p) { v.Add(p.x); v.Add(p.y); v.Add(p.z); v.Add(0); v.Add(0); v.Add(r); v.Add(g); v.Add(b); }
            Vtx(a); Vtx(bb); Vtx(c);
            Vtx(a); Vtx(c); Vtx(d);
        }

        private static float Median(List<float> values)
        {
            if (values == null || values.Count == 0) return 0f;
            var s = new List<float>(values); s.Sort();
            int n = s.Count;
            return (n & 1) == 1 ? s[n / 2] : (s[n / 2 - 1] + s[n / 2]) * 0.5f;
        }

        /// <summary>The most common value (within ±2% bins), returned as the average of that cluster.
        /// Robust to under-/over-sized outliers — the "standard" map footprint wins.</summary>
        private static float Mode(List<float> values)
        {
            if (values == null || values.Count == 0) return 0f;
            float best = values[0]; int bestCount = -1;
            foreach (var v in values)
            {
                if (v <= 0) continue;
                float lo = v * 0.98f, hi = v * 1.02f, sum = 0f; int count = 0;
                foreach (var u in values) if (u >= lo && u <= hi) { sum += u; count++; }
                if (count > bestCount) { bestCount = count; best = sum / count; }
            }
            return best;
        }

        /// <summary>The geometry min of the cell whose footprint best matches the stride (a standard,
        /// full map) — i.e. the authored tile-(0,0) origin shared by the maps.</summary>
        private static float OriginOfStandardCell(List<CellBuild> stored, float stride, bool xAxis)
        {
            float bestOrigin = 0f, bestDiff = float.MaxValue; bool found = false;
            foreach (var cb in stored)
            {
                if (!cb.HasBounds) continue;
                float fp = xAxis ? cb.FpX : cb.FpZ;
                float diff = Math.Abs(fp - stride);
                if (diff < bestDiff) { bestDiff = diff; bestOrigin = xAxis ? cb.MinX : cb.MinZ; found = true; }
            }
            return found ? bestOrigin : 0f;
        }

        private static void MergeOffset(Dictionary<int, List<float>> src, Dictionary<int, List<float>> dst, float ox, float oz)
        {
            foreach (var kv in src)
            {
                if (!dst.TryGetValue(kv.Key, out var list)) { list = new List<float>(kv.Value.Count); dst[kv.Key] = list; }
                var s = kv.Value;
                for (int i = 0; i + 7 < s.Count; i += 8)
                {
                    list.Add(s[i] + ox); list.Add(s[i + 1]); list.Add(s[i + 2] + oz);
                    list.Add(s[i + 3]); list.Add(s[i + 4]);
                    list.Add(s[i + 5]); list.Add(s[i + 6]); list.Add(s[i + 7]);
                }
            }
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

                // The "h_kage" material is a building drop-shadow plane — render it transparent (skip).
                if (mat != null && IsHiddenMaterial(mat)) continue;

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

        /// <summary>Materials that should not be drawn (rendered transparent). "h_kage" (影 = shadow)
        /// is the building drop-shadow plane.</summary>
        private static bool IsHiddenMaterial(NSBMDMaterial mat)
        {
            string n = mat.MaterialName;
            if (!string.IsNullOrEmpty(n) && n.IndexOf("h_kage", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            string t = mat.texname;
            return !string.IsNullOrEmpty(t) && t.IndexOf("h_kage", StringComparison.OrdinalIgnoreCase) >= 0;
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
