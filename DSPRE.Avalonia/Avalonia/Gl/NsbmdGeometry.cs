using System;
using System.Collections.Generic;
using DSPRE.ROMFiles;
using System.Drawing;
using LibNDSFormats.NSBMD;

namespace DSPRE.Avalonia.Gl
{
    /// <summary>One drawable submesh: all triangles that share a material.</summary>
    public sealed class NsbmdMeshPart
    {
        public int MaterialIndex;     // -1 = no material
        /// <summary>The NSBMD node that owns this shape. Kept separate from the material because two
        /// nodes can use the same material and NSBVA visibility is defined per node.</summary>
        public int NodeIndex = -1;
        public float[] Vertices;      // interleaved pos.xyz, uv.st, col.rgb (8 floats/vertex)
        public int VertexCount;
        public float Alpha = 1f;      // material alpha (0-1); < 1 needs GL_BLEND when drawn

        /// <summary>
        /// Which faces the DS would draw, out of the material's polygon attribute: 0 draws neither, 1 drops
        /// the front, 2 drops the back, 3 draws both.
        /// </summary>
        public int CullMode = NsbmdCull.None;

        /// <summary>The material lets fog fall on it, polygon attribute bit 15.</summary>
        public bool Fog;

        /// <summary>The material's resting texture matrix (its own scale, rotation and translation, built the way
        /// the model's tool builds them); a texture animation replaces it while it plays. Null is identity.</summary>
        public float[] TexMatrix;
    }

    /// <summary>Which faces of a polygon the hardware draws, straight out of GXCull.</summary>
    public static class NsbmdCull
    {
        public const int Nothing = 0;   // GX_CULL_ALL
        public const int Front = 1;     // GX_CULL_FRONT: the front face is dropped
        public const int Back = 2;      // GX_CULL_BACK: the back face is dropped
        public const int None = 3;      // GX_CULL_NONE: both sides drawn

        /// <summary>Reads the cull mode out of a material's polygon attribute. </summary>
        public static int FromPolyAttrib(uint attr, uint mask) =>
            (mask & 0xC0) == 0 ? None : (int)((attr >> 6) & 3);
    }

    /// <summary>A model ready for GL: per-material triangle parts + decoded textures.</summary>
    /// <summary>One building put into a scene: its model, where it goes, and which tile it stands on.</summary>
    public sealed class PlacedBuilding
    {
        public NSBMDModel Model;
        public float[] Transform;
        public int ModelId;
        public int TileX, TileZ;
    }

    public sealed class NsbmdRenderModel
    {
        public List<NsbmdMeshPart> Parts = new List<NsbmdMeshPart>();
        public Dictionary<int, NsbmdTextureData> Textures = new Dictionary<int, NsbmdTextureData>();
        public Dictionary<int, float> MaterialAlphaByKey = new Dictionary<int, float>();
        /// <summary>Which faces each material draws, so a backdrop does not show its inside.</summary>
        public Dictionary<int, int> MaterialCullByKey = new Dictionary<int, int>();
        public Dictionary<int, bool> MaterialFogByKey = new Dictionary<int, bool>();
        public Dictionary<int, float[]> MaterialTexMatrixByKey = new Dictionary<int, float[]>();
        // Material names, so a terrain animation (which targets materials by name, e.g. "river")
        // can be matched to the parts it should move.
        public Dictionary<int, string> MaterialNameByKey = new Dictionary<int, string>();

        /// <summary>Which material keys came from which building, so a building's own animation can be
        /// matched to just its parts rather than to every material in the scene with the same name.</summary>
        public sealed class BuildingMaterials
        {
            public int ModelId; public int FirstKey, Count;
            /// <summary>Where it stands, in tiles across the whole matrix.</summary>
            public int TileX, TileZ;
            // Kept so a joint animation can rebuild this building with its parts moved.
            public NSBMDModel Model;
            public float[] Transform;
            public float OffsetX, OffsetY, OffsetZ;
        }
        public List<BuildingMaterials> Buildings = new List<BuildingMaterials>();

        /// <summary>Extra textures a material can be switched to by a texture-swapping animation, by
        /// the name the animation uses. Only filled for materials some animation actually swaps.</summary>
        public Dictionary<int, Dictionary<string, NsbmdTextureData>> SwappableTextures
            = new Dictionary<int, Dictionary<string, NsbmdTextureData>>();

        /// <summary>HGSS terrain scrolls, by the map material they move and its place in the animation.</summary>
        public List<(int MaterialKey, DSPRE.ROMFiles.TextureSrtAnimation Anim, int Index)> GroundScrolls
            = new List<(int, DSPRE.ROMFiles.TextureSrtAnimation, int)>();

        /// <summary>Map materials the field animates (fldtanime): swap names in order, each held for its frames.</summary>
        public List<(int MaterialKey, List<(string Swap, int Frames)> Sequence)> FieldAnimations
            = new List<(int, List<(string, int)>)>();

        public int TotalVertices;

        // Normalization applied to fit the camera: normalized = (raw - Center) * Scale.
        // Exposed so callers (e.g. the permission overlay) can place geometry in the same space.
        public float Cx, Cy, Cz, Scale = 1f;
        public float RawMinX, RawMaxX, RawMinY, RawMaxY, RawMinZ, RawMaxZ;

        // Raw bounds of the MAP model only (excludes buildings), for fitting the tile-grid overlay.
        public float MapMinX, MapMaxX, MapMinY, MapMaxY, MapMinZ, MapMaxZ;
        public bool HasMapBounds;

        // Raw MAP surface triangles (x,y,z per vertex, post-offset, pre-normalization). The permission overlay
        // tints only the near-horizontal (floor) triangles from this, so vertical trees/walls stay untinted.
        public float[] MapSurface;


        public float[] GizmoMesh;
        public int GizmoVertexCount;

        public float CellBaseX, CellBaseZ, CellStrideX, CellStrideZ;
        public bool IsMatrix;

        public struct CellPlacement { public float OriginX, OriginZ, Width, Height; }
        public Dictionary<long, CellPlacement> CellPlacements;
        public Dictionary<long, BdhcFile> CellBdhc;
        public Dictionary<long, float> CellAltitudeY;
        public static long CellKey(int cx, int cy) => ((long)cx << 32) | (uint)cy;
        public bool TryCellPlacement(int cx, int cy, out CellPlacement p)
        {
            p = default;
            return CellPlacements != null && CellPlacements.TryGetValue(CellKey(cx, cy), out p);
        }

        public float[] CellToScene(int cx, int cy, float modelScale)
        {
            float s = modelScale == 0f ? 1f : modelScale;
            float[] m = Mat4.Scale(s / 64f, s / 64f, s / 64f);

            float ox = 0f, oy = 0f, oz = 0f;
            if (TryCellPlacement(cx, cy, out CellPlacement placement))
            {
                ox = placement.OriginX + placement.Width / 2f;
                oz = placement.OriginZ + placement.Height / 2f;
            }
            if (CellAltitudeY != null && CellAltitudeY.TryGetValue(CellKey(cx, cy), out float alt)) oy = alt;

            m = Mat4.Multiply(Mat4.Translate(ox, oy, oz), m);

            float[] normalize = Mat4.Scale(Scale, Scale, Scale);
            normalize[12] = -Cx * Scale;
            normalize[13] = -Cy * Scale;
            normalize[14] = -Cz * Scale;
            return Mat4.Multiply(normalize, m);
        }

        public bool TryBdhcSurfaceY(int cx, int cy, float rawX, float rawZ, float preferredY, out float y)
        {
            y = 0f;
            long key = CellKey(cx, cy);
            if (CellBdhc == null || !CellBdhc.TryGetValue(key, out BdhcFile bdhc) || bdhc == null) return false;
            if (!TryCellPlacement(cx, cy, out CellPlacement p)) return false;
            float altitude = CellAltitudeY != null && CellAltitudeY.TryGetValue(key, out float ay) ? ay : 0f;
            float localX = rawX - p.OriginX;
            float localZ = rawZ - p.OriginZ;
            if (!bdhc.TryGetHeight(localX, localZ, preferredY - altitude, out float localY)) return false;
            y = localY + altitude;
            return true;
        }

        /// <summary>Inverse of cell placement: finds which matrix cell + tile (0..32) a raw-space point
        /// falls in (the cell containing it, else the nearest). Used to drag events across maps.</summary>
        public bool TryRawToTile(float rawX, float rawZ, out int matX, out int matY, out float tileFx, out float tileFy)
        {
            matX = matY = 0; tileFx = tileFy = 0f;
            if (CellPlacements == null || CellPlacements.Count == 0) return false;
            long bestKey = 0; float bestD = float.MaxValue; bool inside = false;
            foreach (KeyValuePair<long, CellPlacement> kv in CellPlacements)
            {
                CellPlacement p = kv.Value;
                bool within = rawX >= p.OriginX && rawX <= p.OriginX + p.Width && rawZ >= p.OriginZ && rawZ <= p.OriginZ + p.Height;
                if (within) { bestKey = kv.Key; inside = true; break; }
                float ccx = p.OriginX + p.Width * 0.5f, ccz = p.OriginZ + p.Height * 0.5f;
                float d = (rawX - ccx) * (rawX - ccx) + (rawZ - ccz) * (rawZ - ccz);
                if (d < bestD) { bestD = d; bestKey = kv.Key; }
            }
            if (!inside && bestD == float.MaxValue) return false;
            CellPlacement bp = CellPlacements[bestKey];
            matX = (int)(bestKey >> 32); matY = (int)(uint)bestKey;
            tileFx = bp.Width > 0 ? (rawX - bp.OriginX) / bp.Width * 32f : 0f;
            tileFy = bp.Height > 0 ? (rawZ - bp.OriginZ) / bp.Height * 32f : 0f;
            return true;
        }

        public float[] HeightGrid;
        public Dictionary<int, int>[] HeightBuckets;
        public int HCols, HRows;
        public float HOriginX, HOriginZ, HTileX, HTileZ, DefaultSurfaceY;
        public const float HeightSnap = 4f;

        /// <summary>Estimated floor Y (raw space) at a point, or the fallback when off-grid.</summary>
        public float SurfaceY(float x, float z)
        {
            if (HeightGrid == null || HTileX <= 0 || HTileZ <= 0) return DefaultSurfaceY;
            int c = (int)Math.Floor((x - HOriginX) / HTileX);
            int r = (int)Math.Floor((z - HOriginZ) / HTileZ);
            if (c < 0 || r < 0 || c >= HCols || r >= HRows) return DefaultSurfaceY;
            float v = HeightGrid[r * HCols + c];
            return float.IsNaN(v) ? DefaultSurfaceY : v;
        }

        /// <summary>Estimated floor Y, preferring sampled candidates near a current/expected Y.</summary>
        public float SurfaceY(float x, float z, float preferredY)
        {
            if (HeightGrid == null || HTileX <= 0 || HTileZ <= 0) return DefaultSurfaceY;
            int c = (int)Math.Floor((x - HOriginX) / HTileX);
            int r = (int)Math.Floor((z - HOriginZ) / HTileZ);
            if (c < 0 || r < 0 || c >= HCols || r >= HRows) return DefaultSurfaceY;
            int idx = r * HCols + c;
            Dictionary<int, int> bucket = HeightBuckets != null && idx < HeightBuckets.Length ? HeightBuckets[idx] : null;
            if (bucket != null && bucket.Count > 0)
            {
                float modal = HeightGrid[idx];
                int modalKey = (int)Math.Round((float.IsNaN(modal) ? DefaultSurfaceY : modal) * HeightSnap);
                int preferredKey = (int)Math.Round(preferredY * HeightSnap);
                int bestKey = modalKey, bestDist = int.MaxValue, bestModalDist = int.MaxValue, bestCount = -1;
                foreach (KeyValuePair<int, int> kv in bucket)
                {
                    int dist = Math.Abs(kv.Key - preferredKey);
                    int modalDist = Math.Abs(kv.Key - modalKey);
                    if (dist < bestDist ||
                        (dist == bestDist && (kv.Value > bestCount ||
                        (kv.Value == bestCount && (modalDist < bestModalDist ||
                        (modalDist == bestModalDist && kv.Key < bestKey))))))
                    {
                        bestKey = kv.Key;
                        bestDist = dist;
                        bestModalDist = modalDist;
                        bestCount = kv.Value;
                    }
                }
                return bestKey / HeightSnap;
            }
            float v = HeightGrid[idx];
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
        // WinForms applied this scale before rendering map models; buildings already include it.
        private static float[] MapVertexScale(NSBMDModel model)
        {
            float ms = model?.modelScale ?? 0f;
            if (ms == 0f) ms = 1f;
            float s = ms / 64f;
            return Mat4.Scale(s, s, s);
        }

        /// <summary>Builds a single model, centred/scaled to fit (for the standalone viewer).</summary>
        /// <summary>Builds one model on its own. A joint animation can be handed in to replace the parts'
        /// own matrices for a frame, which is what makes a door swing or a windmill turn.</summary>
        public static NsbmdRenderModel BuildModel(NSBMDModel model,
            Func<int, NSBMDObject, float[]> jointMatrix = null, NsbmdRenderModel placeLike = null)
        {
            NsbmdRenderModel result = new NsbmdRenderModel();
            Dictionary<(int Material, int Node), List<float>> byNodeAndMat = new Dictionary<(int Material, int Node), List<float>>();
            Accumulate(model, null, 0, result, null, jointMatrix, byNodeAndMat);
            Finalize(result, byNodeAndMat);
            NormalizePositions(result, placeLike);
            return result;
        }

        /// <summary>
        /// Builds a combined scene: the map model plus each building transformed into map
        /// space, with unique material keys per source model. Centred/scaled once at the end.
        /// </summary>
        public static NsbmdRenderModel BuildScene(NSBMDModel map, IReadOnlyList<PlacedBuilding> buildings,
            Dictionary<int, (Dictionary<string, NsbmdTextureData> Frames, List<(string Swap, int Frames)> Sequence)> mapAnimations = null,
            DSPRE.ROMFiles.TextureSrtAnimation groundScroll = null)
        {
            // Build the single map as a 1×1 matrix cell so it gets the SAME fixed 32-tile CellPlacement and
            // per-tile height grid the matrix/event editor uses. That gives the permission overlay a real tile
            // grid (fixes oversized tiles on maps that don't fill all 32 tiles) and per-tile surface heights.
            NsbmdRenderModel scene = BuildMatrixScene(new[]
            {
                new MatrixCellGeometry { Map = map, Buildings = buildings, MapAnimations = mapAnimations, GroundScroll = groundScroll, CellX = 0, CellY = 0 }
            }, MatrixStitchMode.Grid);
            scene.IsMatrix = false;   // single-map view, nothing reads this, but keep it honest
            return scene;
        }

        /// <summary>One matrix cell's geometry: its map model, its buildings (already transformed
        /// into the map's local space) and its position in the matrix grid.</summary>
        public sealed class MatrixCellGeometry
        {
            public NSBMDModel Map;
            public IReadOnlyList<PlacedBuilding> Buildings;
            /// <summary>Textures a building's swapping animation can show, by model id then name.</summary>
            public Dictionary<int, Dictionary<string, NsbmdTextureData>> SwappableTextures;
            /// <summary>Field texture animation frames for the map's own materials, by material index.</summary>
            public Dictionary<int, (Dictionary<string, NsbmdTextureData> Frames, List<(string Swap, int Frames)> Sequence)> MapAnimations;
            /// <summary>The area's terrain scroll animation, which moves map materials by name.</summary>
            public DSPRE.ROMFiles.TextureSrtAnimation GroundScroll;
            public int CellX, CellY;
            public BdhcFile Bdhc;
            public float AltitudeY;

            public float ShiftX, ShiftZ;
        }

        public const int MapTiles = 32;
        public const float TileSize = 256f / 1024f;
        public const float MapStride = MapTiles * TileSize;

        private sealed class CellBuild
        {
            public int CellX, CellY;
            public Dictionary<int, List<float>> MapMats;
            public Dictionary<int, List<float>> BldMats;
            public float MinX, MinZ, FpX, FpZ; public bool HasBounds;
            public float OffX, OffY, OffZ;
            public float ColW, RowH;
            public BdhcFile Bdhc;
            public float AltitudeY;
            public float ShiftX, ShiftZ;
        }

        /// <summary>How matrix cells are laid out in the stitched scene.</summary>
        public enum MatrixStitchMode
        {
            Continuous,
            Grid,
        }

        public static NsbmdRenderModel BuildMatrixScene(IReadOnlyList<MatrixCellGeometry> cells,
            MatrixStitchMode mode = MatrixStitchMode.Continuous)
        {
            NsbmdRenderModel result = new NsbmdRenderModel { IsMatrix = true };
            int offset = 0;
            Dictionary<int, Dictionary<string, NsbmdTextureData>> cellSwaps = new Dictionary<int, Dictionary<string, NsbmdTextureData>>();

            List<CellBuild> stored = new List<CellBuild>();
            int minCx = int.MaxValue, minCy = int.MaxValue, maxCx = int.MinValue, maxCy = int.MinValue;

            foreach (MatrixCellGeometry cell in cells)
            {
                Dictionary<int, List<float>> mapMats = new Dictionary<int, List<float>>();
                float cMinX = 0, cMinZ = 0, cFpX = 0, cFpZ = 0; bool cHas = false;
                if (cell.Map != null)
                {
                    if (cell.MapAnimations != null)
                        foreach (KeyValuePair<int, (Dictionary<string, NsbmdTextureData> Frames, List<(string Swap, int Frames)> Sequence)> kv in cell.MapAnimations)
                        {
                            result.SwappableTextures[offset + kv.Key] = kv.Value.Frames;
                            result.FieldAnimations.Add((offset + kv.Key, kv.Value.Sequence));
                        }
                    if (cell.GroundScroll != null)
                        for (int k = 0; k < cell.Map.Materials.Count; k++)
                        {
                            int index = cell.GroundScroll.IndexOf(cell.Map.Materials[k].MaterialName);
                            if (index >= 0 && !cell.GroundScroll.IsStatic(index)) result.GroundScrolls.Add((offset + k, cell.GroundScroll, index));
                        }
                    Accumulate(cell.Map, MapVertexScale(cell.Map), offset, result, mapMats);
                    offset += Math.Max(1, cell.Map.Materials.Count);
                    if (ComputeRawBounds(mapMats, out float mnx, out float mxx, out float _, out float _, out float mnz, out float mxz))
                    {
                        cFpX = mxx - mnx; cFpZ = mxz - mnz;
                        cMinX = mnx; cMinZ = mnz; cHas = true;
                    }
                }
                Dictionary<int, List<float>> bldMats = new Dictionary<int, List<float>>();
                if (cell.SwappableTextures != null)
                    foreach (KeyValuePair<int, Dictionary<string, NsbmdTextureData>> kv in cell.SwappableTextures) cellSwaps[kv.Key] = kv.Value;
                if (cell.Buildings != null)
                    foreach (PlacedBuilding b in cell.Buildings)
                    {
                        if (b.Model == null) continue;
                        Accumulate(b.Model, b.Transform, offset, result, bldMats);
                        result.Buildings.Add(new NsbmdRenderModel.BuildingMaterials
                        {
                            ModelId = b.ModelId, FirstKey = offset, Count = b.Model.Materials.Count,
                            Model = b.Model, Transform = b.Transform,
                            TileX = b.TileX, TileZ = b.TileZ,
                        });
                        if (cellSwaps.TryGetValue(b.ModelId, out Dictionary<string, NsbmdTextureData> swaps))
                            for (int k = 0; k < b.Model.Materials.Count; k++)
                                result.SwappableTextures[offset + k] = swaps;
                        offset += Math.Max(1, b.Model.Materials.Count);
                    }
                stored.Add(new CellBuild { CellX = cell.CellX, CellY = cell.CellY, MapMats = mapMats, BldMats = bldMats, MinX = cMinX, MinZ = cMinZ, FpX = cFpX, FpZ = cFpZ, HasBounds = cHas, Bdhc = cell.Bdhc, AltitudeY = cell.AltitudeY, ShiftX = cell.ShiftX, ShiftZ = cell.ShiftZ });
                minCx = Math.Min(minCx, cell.CellX); maxCx = Math.Max(maxCx, cell.CellX);
                minCy = Math.Min(minCy, cell.CellY); maxCy = Math.Max(maxCy, cell.CellY);
            }
            Dictionary<int, float> colX = new Dictionary<int, float>();
            for (int cx = minCx; cx <= maxCx; cx++) colX[cx] = (cx - minCx) * MapStride;
            Dictionary<int, float> rowZ = new Dictionary<int, float>();
            for (int cy = minCy; cy <= maxCy; cy++) rowZ[cy] = (cy - minCy) * MapStride;

            Dictionary<int, List<float>> byMat = new Dictionary<int, List<float>>();
            Dictionary<long, NsbmdRenderModel.CellPlacement> placements = new Dictionary<long, NsbmdRenderModel.CellPlacement>();
            Dictionary<long, BdhcFile> cellBdhc = new Dictionary<long, BdhcFile>();
            Dictionary<long, float> cellAltitude = new Dictionary<long, float>();
            List<float> mapSurf = new List<float>();   // map-only triangles (post-offset) for the permission overlay
            foreach (CellBuild cb in stored)
            {
                float ox = colX[cb.CellX] + cb.ShiftX, oz = rowZ[cb.CellY] + cb.ShiftZ;
                cb.ColW = MapStride; cb.RowH = MapStride;
                cb.OffX = ox + MapStride / 2f; cb.OffY = cb.AltitudeY; cb.OffZ = oz + MapStride / 2f;
                MergeOffset(cb.MapMats, byMat, cb.OffX, cb.OffY, cb.OffZ);
                MergeOffset(cb.BldMats, byMat, cb.OffX, cb.OffY, cb.OffZ);
                foreach (NsbmdRenderModel.BuildingMaterials bm in result.Buildings)
                    if (cb.BldMats.ContainsKey(bm.FirstKey))
                    { bm.OffsetX = cb.OffX; bm.OffsetY = cb.OffY; bm.OffsetZ = cb.OffZ; }
                foreach (List<float> list in cb.MapMats.Values)
                    for (int i = 0; i + 7 < list.Count; i += 8)
                    { mapSurf.Add(list[i] + cb.OffX); mapSurf.Add(list[i + 1] + cb.OffY); mapSurf.Add(list[i + 2] + cb.OffZ); }
                long key = NsbmdRenderModel.CellKey(cb.CellX, cb.CellY);
                placements[key] = new NsbmdRenderModel.CellPlacement
                {
                    OriginX = ox,
                    OriginZ = oz,
                    Width = MapStride,
                    Height = MapStride,
                };
                if (cb.Bdhc != null) cellBdhc[key] = cb.Bdhc;
                if (cb.AltitudeY != 0f) cellAltitude[key] = cb.AltitudeY;
            }
            result.CellPlacements = placements;
            result.CellBdhc = cellBdhc;
            result.CellAltitudeY = cellAltitude;
            result.MapSurface = mapSurf.ToArray();

            if (ComputeRawBounds(byMat, out float wmnx, out float wmxx, out float wmny, out float wmxy, out float wmnz, out float wmxz))
            {
                result.MapMinX = wmnx; result.MapMaxX = wmxx; result.MapMinY = wmny;
                result.MapMaxY = wmxy; result.MapMinZ = wmnz; result.MapMaxZ = wmxz;
                result.HasMapBounds = true;
            }
            result.CellBaseX = 0f; result.CellBaseZ = 0f;
            result.CellStrideX = MapStride; result.CellStrideZ = MapStride;

            BuildHeightGrid(result, stored, minCx, minCy, maxCx, maxCy);

            Finalize(result, byMat);
            NormalizePositions(result);
            BuildGizmos(result, stored);
            return result;
        }

        /// <summary>Per-tile modal Y of the map geometry, snapped to one tile.</summary>
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
            float[] grid = new float[cols * rows];
            for (int i = 0; i < grid.Length; i++) grid[i] = float.NaN;
            Dictionary<int, int>[] buckets = new Dictionary<int, int>[grid.Length];
            Dictionary<int, int> globalCounts = new Dictionary<int, int>();

            const float snap = NsbmdRenderModel.HeightSnap;
            foreach (CellBuild cb in stored)
            {
                float ox = cb.OffX, oz = cb.OffZ;
                foreach (List<float> list in cb.MapMats.Values)
                    for (int i = 0; i + 2 < list.Count; i += 8)
                    {
                        float x = list[i] + ox, y = list[i + 1] + cb.OffY, z = list[i + 2] + oz;
                        int c = (int)Math.Floor((x - originX) / tileX);
                        int r = (int)Math.Floor((z - originZ) / tileZ);
                        if (c < 0 || r < 0 || c >= cols || r >= rows) continue;
                        int idx = r * cols + c;
                        int key = (int)Math.Round(y * snap);
                        if (buckets[idx] == null) buckets[idx] = new Dictionary<int, int>();
                        buckets[idx][key] = buckets[idx].TryGetValue(key, out int n) ? n + 1 : 1;
                        globalCounts[key] = globalCounts.TryGetValue(key, out n) ? n + 1 : 1;
                    }
            }

            int defaultKey = (int)Math.Round((result.HasMapBounds ? result.MapMinY : 0f) * snap);
            int defaultCount = -1;
            foreach (KeyValuePair<int, int> kv in globalCounts)
            {
                if (kv.Value > defaultCount || (kv.Value == defaultCount && kv.Key < defaultKey))
                {
                    defaultKey = kv.Key;
                    defaultCount = kv.Value;
                }
            }
            float def = defaultKey / snap;

            for (int i = 0; i < buckets.Length; i++)
            {
                Dictionary<int, int> bucket = buckets[i];
                if (bucket == null || bucket.Count == 0) continue;
                int bestKey = defaultKey, bestCount = -1, bestDist = int.MaxValue;
                foreach (KeyValuePair<int, int> kv in bucket)
                {
                    int dist = Math.Abs(kv.Key - defaultKey);
                    if (kv.Value > bestCount ||
                        (kv.Value == bestCount && (dist < bestDist || (dist == bestDist && kv.Key < bestKey))))
                    {
                        bestKey = kv.Key;
                        bestCount = kv.Value;
                        bestDist = dist;
                    }
                }
                grid[i] = bestKey / snap;
            }

            if ((long)cols * rows <= 200000)
                for (int pass = 0; pass < 12; pass++)
                {
                    bool changed = false;
                    float[] src = (float[])grid.Clone();
                    for (int r = 0; r < rows; r++)
                        for (int c = 0; c < cols; c++)
                        {
                            int idx = r * cols + c;
                            if (!float.IsNaN(src[idx])) continue;
                            Dictionary<int, int> seen = new Dictionary<int, int>(4);
                            if (c > 0 && !float.IsNaN(src[idx - 1]))
                            {
                                int key = (int)Math.Round(src[idx - 1] * snap);
                                seen[key] = seen.TryGetValue(key, out int n) ? n + 1 : 1;
                            }
                            if (c < cols - 1 && !float.IsNaN(src[idx + 1]))
                            {
                                int key = (int)Math.Round(src[idx + 1] * snap);
                                seen[key] = seen.TryGetValue(key, out int n) ? n + 1 : 1;
                            }
                            if (r > 0 && !float.IsNaN(src[idx - cols]))
                            {
                                int key = (int)Math.Round(src[idx - cols] * snap);
                                seen[key] = seen.TryGetValue(key, out int n) ? n + 1 : 1;
                            }
                            if (r < rows - 1 && !float.IsNaN(src[idx + cols]))
                            {
                                int key = (int)Math.Round(src[idx + cols] * snap);
                                seen[key] = seen.TryGetValue(key, out int n) ? n + 1 : 1;
                            }
                            if (seen.Count > 0)
                            {
                                int bestKey = defaultKey, bestCount = -1, bestDist = int.MaxValue;
                                foreach (KeyValuePair<int, int> kv in seen)
                                {
                                    int dist = Math.Abs(kv.Key - defaultKey);
                                    if (kv.Value > bestCount ||
                                        (kv.Value == bestCount && (dist < bestDist || (dist == bestDist && kv.Key < bestKey))))
                                    {
                                        bestKey = kv.Key;
                                        bestCount = kv.Value;
                                        bestDist = dist;
                                    }
                                }
                                grid[idx] = bestKey / snap;
                                changed = true;
                            }
                        }
                    if (!changed) break;
                }

            result.HeightGrid = grid; result.HeightBuckets = buckets; result.HCols = cols; result.HRows = rows;
            result.HOriginX = originX; result.HOriginZ = originZ; result.HTileX = tileX; result.HTileZ = tileZ;
            result.DefaultSurfaceY = def;
        }

        /// <summary>Builds debug gizmo lines: per cell, the 32-tile cell boundary (cyan) and the actual
        /// map-geometry extent (yellow). Both share the min corner (block corner = model geometry-min),
        /// so a smaller yellow inside cyan = a genuinely under-filled map; matching = a full map.</summary>
        private static void BuildGizmos(NsbmdRenderModel m, List<CellBuild> stored)
        {
            if (stored.Count == 0) return;
            float ext = (m.HasMapBounds ? m.MapMaxX - m.MapMinX : m.RawMaxX - m.RawMinX);
            float y = (m.HasMapBounds ? m.MapMaxY : m.RawMaxY) + ext * 0.004f;
            float lw = ext * 0.0015f;               // line half-width in raw units
            List<float> v = new List<float>(stored.Count * 96);

            foreach (CellBuild cb in stored)
            {
                // Cell boundary (cyan): block corner → corner (OffX is the block CENTER).
                float cx0 = cb.OffX - MapStride / 2f, cz0 = cb.OffZ - MapStride / 2f;
                Rect(v, m, cx0, cz0, cx0 + cb.ColW, cz0 + cb.RowH, y, lw, 0.1f, 0.9f, 1.0f);
                // Actual geometry extent (yellow): model-space min/max offset by the block center.
                if (cb.HasBounds)
                {
                    float gx0 = cb.OffX + cb.MinX, gz0 = cb.OffZ + cb.MinZ;
                    Rect(v, m, gx0, gz0, gx0 + cb.FpX, gz0 + cb.FpZ, y, lw, 1.0f, 0.85f, 0.1f);
                }
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
            (float x, float y, float z) a = m.ToNormalized(x0 + px, y, z0 + pz);
            (float x, float y, float z) bb = m.ToNormalized(x1 + px, y, z1 + pz);
            (float x, float y, float z) c = m.ToNormalized(x1 - px, y, z1 - pz);
            (float x, float y, float z) d = m.ToNormalized(x0 - px, y, z0 - pz);
            void Vtx((float x, float y, float z) p) { v.Add(p.x); v.Add(p.y); v.Add(p.z); v.Add(0); v.Add(0); v.Add(r); v.Add(g); v.Add(b); }
            Vtx(a); Vtx(bb); Vtx(c);
            Vtx(a); Vtx(c); Vtx(d);
        }

        private static void MergeOffset(Dictionary<int, List<float>> src, Dictionary<int, List<float>> dst, float ox, float oy, float oz)
        {
            foreach (KeyValuePair<int, List<float>> kv in src)
            {
                if (!dst.TryGetValue(kv.Key, out List<float> list)) { list = new List<float>(kv.Value.Count); dst[kv.Key] = list; }
                List<float> s = kv.Value;
                for (int i = 0; i + 7 < s.Count; i += 8)
                {
                    list.Add(s[i] + ox); list.Add(s[i + 1] + oy); list.Add(s[i + 2] + oz);
                    list.Add(s[i + 3]); list.Add(s[i + 4]);
                    list.Add(s[i + 5]); list.Add(s[i + 6]); list.Add(s[i + 7]);
                }
            }
        }

        /// <summary>
        /// Rebuilds one building's triangles with its parts moved to where a joint animation puts them on
        /// this frame, in the same space the rest of the scene already sits in.
        /// </summary>
        public static Dictionary<int, float[]> RebuildBuilding(NsbmdRenderModel scene,
            NsbmdRenderModel.BuildingMaterials building, Func<int, NSBMDObject, float[]> jointMatrix,
            float[] transform = null)
        {
            Dictionary<int, float[]> result = new Dictionary<int, float[]>();
            if (scene == null || building?.Model == null) return result;

            Dictionary<int, List<float>> raw = new Dictionary<int, List<float>>();
            Accumulate(building.Model, transform ?? building.Transform, building.FirstKey, scene, raw, jointMatrix);

            foreach (KeyValuePair<int, List<float>> kv in raw)
            {
                List<float> src = kv.Value;
                float[] dst = new float[src.Count];
                for (int i = 0; i + 7 < src.Count; i += 8)
                {
                    (float nx, float ny, float nz) = scene.ToNormalized(src[i] + building.OffsetX,
                                                          src[i + 1] + building.OffsetY,
                                                          src[i + 2] + building.OffsetZ);
                    dst[i] = nx; dst[i + 1] = ny; dst[i + 2] = nz;
                    dst[i + 3] = src[i + 3]; dst[i + 4] = src[i + 4];
                    dst[i + 5] = src[i + 5]; dst[i + 6] = src[i + 6]; dst[i + 7] = src[i + 7];
                }
                result[kv.Key] = dst;
            }
            return result;
        }

        private static void Accumulate(NSBMDModel model, float[] sceneTransform, int matOffset,
            NsbmdRenderModel target, Dictionary<int, List<float>> byMat,
            Func<int, NSBMDObject, float[]> jointMatrix = null,
            Dictionary<(int Material, int Node), List<float>> byNodeAndMat = null)
        {
            if (model == null || model.Polygons.Count == 0) return;

            MTX44[] stack = new MTX44[32];
            for (int i = 0; i < stack.Length; i++) { stack[i] = new MTX44(); stack[i].LoadIdentity(); }
            MTX44 running = new MTX44(); running.LoadIdentity();
            foreach (NSBMDObject obj in model.Objects)
            {
                if (obj.RestoreID != -1) running = stack[obj.RestoreID].Clone();
                if (obj.StackID != -1)
                {
                    if (obj.visible)
                    {
                        // A joint animation replaces a part's own matrix for this frame, keeping
                        // whatever the model says about anything the animation does not touch.
                        float[] mtx = jointMatrix?.Invoke(model.Objects.IndexOf(obj), obj) ?? obj.materix;
                        MTX44 b = new MTX44(); b.SetValues(mtx);
                        running = running.MultMatrix(b);
                    }
                    else { running = running.Clone(); running.Zero(); }
                    stack[obj.StackID & 0x1f] = running.Clone();
                }
            }

            foreach (NSBMDPolygon poly in model.Polygons)
            {
                int matId = poly.MatId;
                NSBMDMaterial mat = (matId >= 0 && matId < model.Materials.Count) ? model.Materials[matId] : null;
                int key = matOffset + matId;

                Color c = mat?.DiffuseColor ?? Color.LightGray;
                float r = c.R / 255f, g = c.G / 255f, b = c.B / 255f;
                if (r + g + b < 0.05f) { r = g = b = 0.85f; }

                List<float> list;
                if (byNodeAndMat != null)
                {
                    (int key, int JointID) part = (key, poly.JointID);
                    if (!byNodeAndMat.TryGetValue(part, out list))
                    {
                        list = new List<float>();
                        byNodeAndMat[part] = list;
                    }
                }
                else if (!byMat.TryGetValue(key, out list))
                {
                    list = new List<float>();
                    byMat[key] = list;
                }
                InterpretPolyData(poly.PolyData, poly.StackID, stack, model.modelScale, mat, sceneTransform, list, r, g, b);

                if (mat != null)
                {
                    // Real per-material translucency (ported from WinForms PR #209) instead of hiding
                    // materials outright: "h_kage" (影 = shadow) drop-shadow planes and puddle/window
                    // overlays now render with their actual NSBMD alpha via GL_BLEND (NsbmdGlControl),
                    // rather than being skipped and not drawn at all.
                    if (!target.MaterialAlphaByKey.ContainsKey(key))
                        target.MaterialAlphaByKey[key] = MaterialAlpha(mat);

                    if (!target.MaterialTexMatrixByKey.ContainsKey(key))
                        target.MaterialTexMatrixByKey[key] = MaterialTexMatrix(mat, model.texMtxMode);

                    if (!target.MaterialNameByKey.ContainsKey(key) && !string.IsNullOrEmpty(mat.MaterialName))
                        target.MaterialNameByKey[key] = mat.MaterialName;

                    if (!target.MaterialCullByKey.ContainsKey(key))
                        target.MaterialCullByKey[key] = NsbmdCull.FromPolyAttrib(mat.PolyAttrib, mat.PolyAttribMask);

                    if (!target.MaterialFogByKey.ContainsKey(key))
                        target.MaterialFogByKey[key] = (mat.PolyAttribMask & 0x8000) != 0 && (mat.PolyAttrib & 0x8000) != 0;

                    if (!target.Textures.ContainsKey(key))
                    {
                        NsbmdTextureData tex = NsbmdTextureDecoder.Decode(mat);
                        if (tex != null) target.Textures[key] = tex;
                        else if (mat.missingExternalTexture) target.Textures[key] = MissingTexture();
                    }
                }
            }
        }

        private static NsbmdTextureData MissingTexture()
        {
            const int size = 8;
            byte[] rgba = new byte[size * size * 4];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool bright = ((x / 2) + (y / 2)) % 2 == 0;
                    int at = (y * size + x) * 4;
                    rgba[at] = bright ? (byte)255 : (byte)40;
                    rgba[at + 1] = 0;
                    rgba[at + 2] = bright ? (byte)255 : (byte)40;
                    rgba[at + 3] = 255;
                }
            return new NsbmdTextureData { Rgba = rgba, Width = size, Height = size, WrapS = 1, WrapT = 1 };
        }

        /// <summary>Converts the NSBMD 0-31 material alpha into a 0-1 GL alpha (31 = fully opaque).
        /// Mirrors the WinForms fix in PR #209 (NSBMDGlRenderer.MaterialAlpha).</summary>
        private static float MaterialAlpha(NSBMDMaterial mat) => mat.Alpha >= 31 ? 1f : mat.Alpha / 31f;

        private static bool ComputeRawBounds(Dictionary<int, List<float>> byMat,
            out float minX, out float maxX, out float minY, out float maxY, out float minZ, out float maxZ)
        {
            minX = minY = minZ = float.MaxValue; maxX = maxY = maxZ = float.MinValue;
            bool any = false;
            foreach (List<float> list in byMat.Values)
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
            foreach (KeyValuePair<int, List<float>> kv in byMat)
            {
                if (kv.Value.Count == 0) continue;
                float alpha = result.MaterialAlphaByKey.TryGetValue(kv.Key, out float a) ? a : 1f;
                int cull = result.MaterialCullByKey.TryGetValue(kv.Key, out int c) ? c : NsbmdCull.None;
                bool fog = result.MaterialFogByKey.TryGetValue(kv.Key, out bool f) && f;
                result.MaterialTexMatrixByKey.TryGetValue(kv.Key, out float[] texMtx);
                result.Parts.Add(new NsbmdMeshPart { MaterialIndex = kv.Key, Vertices = kv.Value.ToArray(), VertexCount = kv.Value.Count / 8, Alpha = alpha, CullMode = cull, Fog = fog, TexMatrix = texMtx });
                result.TotalVertices += kv.Value.Count / 8;
            }
        }

        /// <summary>One parameter word. Reads nought past the end rather than throwing, so a list
        /// that stops mid-command is simply cut short.</summary>
        private static int S32(byte[] d, ref int i)
        {
            int v = i + 4 <= d.Length ? BitConverter.ToInt32(d, i) : 0;
            i += 4;
            return v;
        }

        /// <summary>A material's resting texture matrix, or null when it leaves coordinates alone.</summary>
        private static float[] MaterialTexMatrix(NSBMDMaterial mat, byte mode)
        {
            // The loader's trailing placeholder material carries no parsed fields (all zero).
            if (mat == null || (mat.scaleS == 0f && mat.scaleT == 0f)) return null;
            if (mat.scaleS == 1f && mat.scaleT == 1f && mat.rot == 0f && mat.transS == 0f && mat.transT == 0f) return null;
            TextureSrtAnimation.Srt srt = new DSPRE.ROMFiles.TextureSrtAnimation.Srt
            {
                ScaleS = mat.scaleS, ScaleT = mat.scaleT, SinRotation = mat.rot, CosRotation = mat.rotCos,
                TranslateS = mat.transS, TranslateT = mat.transT,
            };
            float hw = mat.width > 0 && mat.height > 0 ? (float)mat.height / mat.width : 1f;
            return srt.ToMatrix3(mode, hw);
        }

        private static void InterpretPolyData(byte[] poly, int polyStackId, MTX44[] stack, float modelScale,
            NSBMDMaterial mat, float[] sceneTransform, List<float> outVerts, float cr, float cg, float cb)
        {
            if (poly == null || poly.Length == 0) return;

            float texW = mat != null && mat.width > 0 ? mat.width : 1f;
            float texH = mat != null && mat.height > 0 ? mat.height : 1f;
            // The material's scale is part of its texture matrix (NsbmdMeshPart.TexMatrix), applied when drawn.
            int flipS = mat?.flipS ?? 0, flipT = mat?.flipT ?? 0;

            MTX44 cur = new MTX44(); cur.LoadIdentity();
            int stackId = polyStackId;
            if (stackId >= 0 && stackId < stack.Length) stack[stackId & 0x1f].CopyValuesTo(cur);

            float[] v = new float[3];
            float u = 0f, w = 0f;                 // current texcoord
            float vr = cr, vg = cg, vb = cb;
            List<float[]> prim = new List<float[]>();       // each entry: pos.xyz + uv.st + colour.rgb
            int primType = -1;

            int idx = 0, len = poly.Length;
            while (idx < len)
            {
                int[] cmds = new int[4];
                for (int k = 0; k < 4; k++) cmds[k] = idx < len ? poly[idx++] : 0xff;

                // Every command in the word is run, not only the ones with a parameter still to come.
                // Stopping at the end of the parameters drops whatever sits after the last one, and the
                // command that ends a run of corners takes no parameter, so a shape written as a single
                // run lost all of it and a shape written as several lost its last one.
                for (int k = 0; k < 4; k++)
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
                        case 0x20:
                            {
                                int colour = S32(poly, ref idx);
                                vr = (colour & 0x1f) / 31f;
                                vg = ((colour >> 5) & 0x1f) / 31f;
                                vb = ((colour >> 10) & 0x1f) / 31f;
                                break;
                            }
                        case 0x21: idx += 4; break;   // NORMAL (lighting deferred)
                        case 0x22:                    // TEXCOORD
                            {
                                int p = S32(poly, ref idx);
                                int s = NSBMDGlRenderer.Sign(p & 0xffff, 0x10);
                                int tt = NSBMDGlRenderer.Sign((p >> 16) & 0xffff, 0x10);
                                u = (s / 16f) / texW / (flipS + 1);
                                w = (tt / 16f) / texH / (flipT + 1);
                                break;
                            }
                        case 0x23:
                            {
                                int p0 = S32(poly, ref idx), p1 = S32(poly, ref idx);
                                v[0] = NSBMDGlRenderer.Sign(p0 & 0xffff, 0x10) / 4096f;
                                v[1] = NSBMDGlRenderer.Sign((p0 >> 16) & 0xffff, 0x10) / 4096f;
                                v[2] = NSBMDGlRenderer.Sign(p1 & 0xffff, 0x10) / 4096f;
                                Emit(cur, stackId, v, u, w, vr, vg, vb, sceneTransform, prim); break;
                            }
                        case 0x24:
                            {
                                int p = S32(poly, ref idx);
                                v[0] = NSBMDGlRenderer.Sign(p & 0x3ff, 10) / 64f;
                                v[1] = NSBMDGlRenderer.Sign((p >> 10) & 0x3ff, 10) / 64f;
                                v[2] = NSBMDGlRenderer.Sign((p >> 20) & 0x3ff, 10) / 64f;
                                Emit(cur, stackId, v, u, w, vr, vg, vb, sceneTransform, prim); break;
                            }
                        case 0x25:
                            {
                                int p = S32(poly, ref idx);
                                v[0] = NSBMDGlRenderer.Sign(p & 0xffff, 0x10) / 4096f;
                                v[1] = NSBMDGlRenderer.Sign((p >> 16) & 0xffff, 0x10) / 4096f;
                                Emit(cur, stackId, v, u, w, vr, vg, vb, sceneTransform, prim); break;
                            }
                        case 0x26:
                            {
                                int p = S32(poly, ref idx);
                                v[0] = NSBMDGlRenderer.Sign(p & 0xffff, 0x10) / 4096f;
                                v[2] = NSBMDGlRenderer.Sign((p >> 16) & 0xffff, 0x10) / 4096f;
                                Emit(cur, stackId, v, u, w, vr, vg, vb, sceneTransform, prim); break;
                            }
                        case 0x27:
                            {
                                int p = S32(poly, ref idx);
                                v[1] = NSBMDGlRenderer.Sign(p & 0xffff, 0x10) / 4096f;
                                v[2] = NSBMDGlRenderer.Sign((p >> 16) & 0xffff, 0x10) / 4096f;
                                Emit(cur, stackId, v, u, w, vr, vg, vb, sceneTransform, prim); break;
                            }
                        case 0x28:
                            {
                                int p = S32(poly, ref idx);
                                v[0] += NSBMDGlRenderer.Sign(p & 0x3ff, 10) / 4096f;
                                v[1] += NSBMDGlRenderer.Sign((p >> 10) & 0x3ff, 10) / 4096f;
                                v[2] += NSBMDGlRenderer.Sign((p >> 20) & 0x3ff, 10) / 4096f;
                                Emit(cur, stackId, v, u, w, vr, vg, vb, sceneTransform, prim); break;
                            }
                        case 0x40: primType = S32(poly, ref idx); prim.Clear(); break;
                        case 0x41: Tessellate(prim, primType, outVerts); prim.Clear(); break;

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
            MTX44 m = new MTX44(); m.LoadIdentity();
            if (count == 16) for (int n = 0; n < 16; n++) m[n] = S32(poly, ref idx) / 4096f;
            else if (count == 12) for (int col = 0; col < 4; col++) for (int row = 0; row < 3; row++) m[col * 4 + row] = S32(poly, ref idx) / 4096f;
            else for (int col = 0; col < 3; col++) for (int row = 0; row < 3; row++) m[col * 4 + row] = S32(poly, ref idx) / 4096f;
            if (load) m.CopyValuesTo(cur);
            else cur.MultMatrix(m).CopyValuesTo(cur);
        }

        private static void Emit(MTX44 cur, int stackId, float[] v, float u, float w,
            float r, float g, float b, float[] sceneTransform, List<float[]> prim)
        {
            float x = v[0], y = v[1], z = v[2];
            if (stackId >= 0) { float[] t = cur.MultVector(v); x = t[0]; y = t[1]; z = t[2]; }
            if (sceneTransform != null) Mat4.TransformPoint(sceneTransform, ref x, ref y, ref z);
            prim.Add(new[] { x, y, z, u, w, r, g, b });
        }

        /// <summary>Finalizes a standalone model without folding shapes from different nodes together.
        /// Materials still share texture and colour state; node identity is only for visibility tracks.</summary>
        private static void Finalize(NsbmdRenderModel result,
            Dictionary<(int Material, int Node), List<float>> byNodeAndMat)
        {
            foreach (KeyValuePair<(int Material, int Node), List<float>> kv in byNodeAndMat)
            {
                if (kv.Value.Count == 0) continue;
                int material = kv.Key.Material;
                float alpha = result.MaterialAlphaByKey.TryGetValue(material, out float a) ? a : 1f;
                int cull = result.MaterialCullByKey.TryGetValue(material, out int c) ? c : NsbmdCull.None;
                bool fog = result.MaterialFogByKey.TryGetValue(material, out bool f) && f;
                result.MaterialTexMatrixByKey.TryGetValue(material, out float[] texMtx);
                result.Parts.Add(new NsbmdMeshPart
                {
                    TexMatrix = texMtx,
                    Fog = fog,
                    MaterialIndex = material,
                    NodeIndex = kv.Key.Node,
                    Vertices = kv.Value.ToArray(),
                    VertexCount = kv.Value.Count / 8,
                    Alpha = alpha,
                    CullMode = cull,
                });
                result.TotalVertices += kv.Value.Count / 8;
            }
        }

        private static void Tessellate(List<float[]> p, int type, List<float> outv)
        {
            void Vtx(float[] a) { for (int i = 0; i < 8; i++) outv.Add(a[i]); }
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

        /// <summary>Centres a model in view and scales it to fit.</summary>
        private static void NormalizePositions(NsbmdRenderModel model, NsbmdRenderModel like = null)
        {
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
            foreach (NsbmdMeshPart part in model.Parts)
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

            if (like != null && like.Scale > 0)
            {
                cx = like.Cx; cy = like.Cy; cz = like.Cz; scale = like.Scale;
            }

            model.Cx = cx; model.Cy = cy; model.Cz = cz; model.Scale = scale;
            model.RawMinX = minX; model.RawMaxX = maxX;
            model.RawMinY = minY; model.RawMaxY = maxY;
            model.RawMinZ = minZ; model.RawMaxZ = maxZ;

            foreach (NsbmdMeshPart part in model.Parts)
                for (int i = 0; i < part.Vertices.Length; i += 8)
                {
                    part.Vertices[i] = (part.Vertices[i] - cx) * scale;
                    part.Vertices[i + 1] = (part.Vertices[i + 1] - cy) * scale;
                    part.Vertices[i + 2] = (part.Vertices[i + 2] - cz) * scale;
                }
        }
    }
}
