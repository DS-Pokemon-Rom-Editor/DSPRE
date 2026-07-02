using DSPRE.ROMFiles;
using LibNDSFormats.NSBMD;
using LibNDSFormats.NSBTX;
using NSMBe4.DSFileSystem;
using System;
using System.Collections.Generic;
using System.IO;
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
                        if (!areaCache.TryGetValue(areaId, out var area)) { area = new AreaData(areaId); areaCache[areaId] = area; }

                        // HGSS indoor areas use the interior building model set.
                        bool interior = gameFamily == GameFamilies.HGSS && area.areaType == AreaData.TYPE_INDOOR;
                        string bldDir = (interior && intBldDir != null) ? intBldDir : extBldDir;

                        var map = new MapFile(mapIndex, gameFamily, discardMoveperms: true);
                        BdhcFile.TryParse(map.bdhc, out var bdhc);
                        float altitudeY = matrix.hasHeightsSection ? matrix.altitudes[y, x] * (NsbmdGeometry.TileSize / 2f) : 0f;

                        if (map.mapModel?.models != null && map.mapModel.models.Length > 0)
                            BindNsbtx(map.mapModel, mapTexDir + "\\" + area.mapTileset.ToString("D4"));

                        var buildings = new List<(NSBMDModel, float[])>();
                        string btexPath = bldTexDir + "\\" + area.buildingsTileset.ToString("D4");
                        byte[] bldTex = System.IO.File.Exists(btexPath) ? System.IO.File.ReadAllBytes(btexPath) : null;

                        if (map.buildings != null)
                            foreach (var b in map.buildings)
                            {
                                string mp = bldDir + "\\" + b.modelID.ToString("D4");
                                if (!System.IO.File.Exists(mp)) continue;
                                using (var fs = new FileStream(mp, FileMode.Open, FileAccess.Read))
                                    b.NSBMDFile = NSBMDLoader.LoadNSBMD(fs);
                                if (b.NSBMDFile?.models == null || b.NSBMDFile.models.Length == 0) continue;
                                if (bldTex != null)
                                {
                                    try
                                    {
                                        b.NSBMDFile.materials = NSBTXLoader.LoadNsbtx(new MemoryStream(bldTex), out b.NSBMDFile.Textures, out b.NSBMDFile.Palettes);
                                        b.NSBMDFile.MatchTextures();
                                    }
                                    catch { /* pack mismatch — leave untextured */ }
                                }
                                buildings.Add((b.NSBMDFile.models[0], MapGeometry.BuildingTransform(b)));
                            }

                        cells.Add(new NsbmdGeometry.MatrixCellGeometry
                        {
                            Map = map.mapModel?.models?.Length > 0 ? map.mapModel.models[0] : null,
                            Buildings = buildings,
                            CellX = x,
                            CellY = y,
                            Bdhc = bdhc,
                            AltitudeY = altitudeY,
                        });
                    }
                    catch (Exception ex) { AppLogger.Error($"Matrix cell ({x},{y}) map {mapIndex} failed: {ex.Message}"); }
                }

            return cells.Count > 0 ? NsbmdGeometry.BuildMatrixScene(cells, mode) : null;
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
    }
}
