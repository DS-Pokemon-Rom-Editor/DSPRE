using DSPRE.ROMFiles;
using LibNDSFormats.NSBMD;
using LibNDSFormats.NSBTX;
using NSMBe4.DSFileSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Gl
{
    /// <summary>
    /// Loads every non-VOID map of a <see cref="GameMatrix"/>, resolves each cell's texture
    /// packs through the real ROM linkage (per-cell header section when present, else a
    /// supplied map→area lookup or a fallback area), binds map + building textures, and
    /// stitches the whole matrix into one <see cref="NsbmdRenderModel"/> positioned by the
    /// matrix grid. Shared by the Event editor (show all maps an event spans) and the Map
    /// editor (full-matrix fly-around view).
    /// </summary>
    public static class MatrixSceneBuilder
    {
        /// <summary>
        /// Builds the stitched matrix scene. <paramref name="areaForMap"/> resolves a map index
        /// to its area-data id (used when the matrix has no per-cell header section); when it is
        /// null or returns no value, <paramref name="fallbackAreaId"/> is used.
        /// <paramref name="includeCells"/>, when supplied, limits the build to those matrix cells
        /// (e.g. only the cells an event file's events occupy) instead of the whole matrix.
        /// </summary>
        public static NsbmdRenderModel Build(GameMatrix matrix, byte fallbackAreaId,
            GameFamilies gameFamily, Func<int, byte?> areaForMap = null,
            ISet<(int x, int y)> includeCells = null,
            NsbmdGeometry.MatrixStitchMode mode = NsbmdGeometry.MatrixStitchMode.Continuous)
        {
            if (matrix == null) return null;
            var cells = new List<NsbmdGeometry.MatrixCellGeometry>();
            string mapTexDir = gameDirs[DirNames.mapTextures].unpackedDir;
            string extBldDir = gameDirs[DirNames.exteriorBuildingModels].unpackedDir;
            string intBldDir = gameDirs.ContainsKey(DirNames.interiorBuildingModels) ? gameDirs[DirNames.interiorBuildingModels].unpackedDir : null;
            string bldTexDir = gameDirs[DirNames.buildingTextures].unpackedDir;
            var areaCache = new Dictionary<byte, AreaData>();

            for (int y = 0; y < matrix.height; y++)
                for (int x = 0; x < matrix.width; x++)
                {
                    if (includeCells != null && !includeCells.Contains((x, y))) continue;
                    int mapIndex = matrix.maps[y, x];
                    if (mapIndex == GameMatrix.EMPTY) continue;

                    try
                    {
                        byte areaId = ResolveAreaId(matrix, x, y, fallbackAreaId, mapIndex, areaForMap);
                        float altitudeY = matrix.hasHeightsSection ? matrix.altitudes[y, x] * (NsbmdGeometry.TileSize / 2f) : 0f;
                        var map = new MapFile(mapIndex, gameFamily, discardMoveperms: true);
                        var geo = BuildCellGeometry(map, areaId, gameFamily, x, y, altitudeY,
                            mapTexDir, extBldDir, intBldDir, bldTexDir, areaCache);
                        if (geo != null) cells.Add(geo);
                    }
                    catch (Exception ex) { AppLogger.Error($"Matrix cell ({x},{y}) map {mapIndex} failed: {ex.Message}"); }
                }

            return cells.Count > 0 ? NsbmdGeometry.BuildMatrixScene(cells, mode) : null;
        }

        /// <summary>
        /// Like <see cref="Build"/>, but stitches ALREADY-LOADED <see cref="MapFile"/> instances instead
        /// of reading fresh copies from disk, used to re-render a scene that has in-memory, not-yet-saved
        /// edits (e.g. the Map editor's "This header" view after painting or moving a building).
        /// </summary>
        public static NsbmdRenderModel BuildFromLoaded(
            GameFamilies gameFamily,
            IEnumerable<(int cellX, int cellY, MapFile map, byte areaId, float altitudeY)> loadedCells,
            NsbmdGeometry.MatrixStitchMode mode = NsbmdGeometry.MatrixStitchMode.Grid)
        {
            var cells = new List<NsbmdGeometry.MatrixCellGeometry>();
            string mapTexDir = gameDirs[DirNames.mapTextures].unpackedDir;
            string extBldDir = gameDirs[DirNames.exteriorBuildingModels].unpackedDir;
            string intBldDir = gameDirs.ContainsKey(DirNames.interiorBuildingModels) ? gameDirs[DirNames.interiorBuildingModels].unpackedDir : null;
            string bldTexDir = gameDirs[DirNames.buildingTextures].unpackedDir;
            var areaCache = new Dictionary<byte, AreaData>();

            foreach (var (cellX, cellY, map, areaId, altitudeY) in loadedCells)
            {
                try
                {
                    var geo = BuildCellGeometry(map, areaId, gameFamily, cellX, cellY, altitudeY,
                        mapTexDir, extBldDir, intBldDir, bldTexDir, areaCache);
                    if (geo != null) cells.Add(geo);
                }
                catch (Exception ex) { AppLogger.Error($"Loaded cell ({cellX},{cellY}) failed: {ex.Message}"); }
            }
            return cells.Count > 0 ? NsbmdGeometry.BuildMatrixScene(cells, mode) : null;
        }

        /// <summary>Binds textures + building models for one already-loaded map and packs it into
        /// stitchable cell geometry. Mutates the map's building NSBMD/material bindings in place
        /// (same as the rest of this class), so callers that keep the <see cref="MapFile"/> around for
        /// editing (rather than throwing it away after one render) see it come back textured too.</summary>
        private static NsbmdGeometry.MatrixCellGeometry BuildCellGeometry(MapFile map, byte areaId,
            GameFamilies gameFamily, int cellX, int cellY, float altitudeY,
            string mapTexDir, string extBldDir, string intBldDir, string bldTexDir,
            Dictionary<byte, AreaData> areaCache)
        {
            if (!areaCache.TryGetValue(areaId, out var area)) { area = new AreaData(areaId); areaCache[areaId] = area; }

            // HGSS indoor areas use the interior building model set.
            bool interior = gameFamily == GameFamilies.HGSS && area.areaType == AreaData.TYPE_INDOOR;
            string bldDir = (interior && intBldDir != null) ? intBldDir : extBldDir;

            BdhcFile.TryParse(map.bdhc, out var bdhc);

            if (map.mapModel?.models != null && map.mapModel.models.Length > 0)
                BindNsbtx(map.mapModel, Path.Combine(mapTexDir, area.mapTileset.ToString("D4")));

            var buildings = new List<PlacedBuilding>();
            var swappable = new Dictionary<int, Dictionary<string, NsbmdTextureData>>();
            string btexPath = Path.Combine(bldTexDir, area.buildingsTileset.ToString("D4"));
            byte[] bldTex = System.IO.File.Exists(btexPath) ? System.IO.File.ReadAllBytes(btexPath) : null;

            if (map.buildings != null)
                foreach (var b in map.buildings)
                {
                    if (b.NSBMDFile == null)
                    {
                        string mp = Path.Combine(bldDir, b.modelID.ToString("D4"));
                        if (!System.IO.File.Exists(mp)) continue;
                        using var fs = new FileStream(mp, FileMode.Open, FileAccess.Read);
                        b.NSBMDFile = NSBMDLoader.LoadNSBMD(fs);
                    }
                    if (b.NSBMDFile?.models == null || b.NSBMDFile.models.Length == 0) continue;
                    if (bldTex != null)
                    {
                        try
                        {
                            b.NSBMDFile.materials = NSBTXLoader.LoadNsbtx(new MemoryStream(bldTex), out b.NSBMDFile.Textures, out b.NSBMDFile.Palettes);
                            b.NSBMDFile.MatchTextures();
                        }
                        catch { /* pack mismatch, leave untextured */ }
                    }
                    buildings.Add(new PlacedBuilding
                    {
                        Model = b.NSBMDFile.models[0],
                        Transform = MapGeometry.BuildingTransform(b),
                        ModelId = (int)b.modelID,
                        TileX = cellX * MapFile.mapSize + b.xPosition,
                        TileZ = cellY * MapFile.mapSize + b.zPosition,
                    });
                    CollectSwappableTextures(b.NSBMDFile, (int)b.modelID, interior, swappable);
                }

            return new NsbmdGeometry.MatrixCellGeometry
            {
                Map = map.mapModel?.models?.Length > 0 ? map.mapModel.models[0] : null,
                Buildings = buildings,
                SwappableTextures = swappable,
                CellX = cellX,
                CellY = cellY,
                Bdhc = bdhc,
                AltitudeY = altitudeY,
            };
        }

        private static byte ResolveAreaId(GameMatrix matrix, int x, int y, byte fallbackAreaId,
            int mapIndex, Func<int, byte?> areaForMap)
        {
            if (matrix.hasHeadersSection)
            {
                try
                {
                    ushort headerId = matrix.headers[y, x];
                    var h = MapHeader.GetMapHeader(headerId);
                    if (h != null) return h.areaDataID;
                }
                catch { /* fall through */ }
            }
            if (areaForMap != null)
            {
                var a = areaForMap(mapIndex);
                if (a.HasValue) return a.Value;
            }
            return fallbackAreaId;
        }

        private static void BindNsbtx(NSBMD container, string path)
        {
            try
            {
                if (!System.IO.File.Exists(path)) return;
                container.materials = NSBTXLoader.LoadNsbtx(new MemoryStream(System.IO.File.ReadAllBytes(path)), out container.Textures, out container.Palettes);
                container.MatchTextures();
            }
            catch (Exception ex) { AppLogger.Error("Matrix tileset bind failed: " + ex.Message); }
        }
    
        /// <summary>
        /// Decodes every texture a building's swapping animations can put on screen, keyed by model id
        /// then by texture name. Done once at build time so nothing has to decode mid-animation.
        /// </summary>
        private static void CollectSwappableTextures(NSBMD file, int modelId, bool indoor,
            Dictionary<int, Dictionary<string, NsbmdTextureData>> into)
        {
            if (file?.models == null || file.models.Length == 0 || into.ContainsKey(modelId)) return;

            var patterns = BuildingAnimationSet.PatternsFor(modelId, indoor);
            if (patterns.Count == 0) return;

            var model = file.models[0];
            var wanted = new Dictionary<string, string>();       // texture name → palette name
            foreach (var anim in patterns)
                for (int m = 0; m < anim.MaterialNames.Count; m++)
                    foreach (var swap in anim.AllSwaps(m))
                        if (swap.IsSet) wanted[swap.TextureName] = swap.PaletteName;
            if (wanted.Count == 0) return;

            var decoded = new Dictionary<string, NsbmdTextureData>();
            foreach (var kv in wanted)
            {
                try
                {
                    var tex = file.Textures?.FirstOrDefault(t => t.texname == kv.Key);
                    if (tex == null) continue;
                    var pal = file.Palettes?.FirstOrDefault(pp => pp.palname == kv.Value);

                    // Borrow one of the model's materials for its render flags, then point it at this
                    // texture and palette so the normal decoder can do the work.
                    var basis = model.Materials.Count > 0 ? model.Materials[0] : null;
                    if (basis == null) continue;
                    var stand_in = new NSBMDMaterial
                    {
                        texdata = tex.texdata, spdata = tex.spdata, texname = tex.texname,
                        texoffset = tex.texoffset, texsize = tex.texsize,
                        width = tex.width, height = tex.height, format = tex.format, color0 = tex.color0,
                        repeatS = basis.repeatS, repeatT = basis.repeatT,
                        flipS = basis.flipS, flipT = basis.flipT,
                    };
                    if (pal != null)
                    {
                        stand_in.paldata = pal.paldata; stand_in.palname = pal.palname;
                        stand_in.paloffset = pal.paloffset; stand_in.palsize = pal.palsize;
                    }
                    var data = NsbmdTextureDecoder.Decode(stand_in);
                    if (data != null) decoded[kv.Key] = data;
                }
                catch (Exception ex) { AppLogger.Error($"Building {modelId} texture {kv.Key} failed: {ex.Message}"); }
            }
            if (decoded.Count > 0) into[modelId] = decoded;
        }
}
}
