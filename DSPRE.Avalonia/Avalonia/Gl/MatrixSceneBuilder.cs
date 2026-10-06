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
        /// <summary>Unpacks every archive a matrix scene reads. A fresh extract has none of them unpacked.</summary>
        public static void EnsureUnpacked()
        {
            List<DirNames> dirs = new List<DirNames> {
                DirNames.matrices, DirNames.maps, DirNames.areaData, DirNames.mapTextures,
                DirNames.exteriorBuildingModels, DirNames.buildingTextures, DirNames.dynamicHeaders };
            if (RomInfo.gameFamily == GameFamilies.HGSS) dirs.Add(DirNames.interiorBuildingModels);
            DSUtils.TryUnpackNarcs(dirs);
        }

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
            List<NsbmdGeometry.MatrixCellGeometry> cells = new List<NsbmdGeometry.MatrixCellGeometry>();
            string mapTexDir = gameDirs[DirNames.mapTextures].unpackedDir;
            string extBldDir = gameDirs[DirNames.exteriorBuildingModels].unpackedDir;
            string intBldDir = gameDirs.ContainsKey(DirNames.interiorBuildingModels) ? gameDirs[DirNames.interiorBuildingModels].unpackedDir : null;
            string bldTexDir = gameDirs[DirNames.buildingTextures].unpackedDir;
            Dictionary<byte, AreaData> areaCache = new Dictionary<byte, AreaData>();

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
                        MapFile map = new MapFile(mapIndex, gameFamily, discardMoveperms: true);
                        NsbmdGeometry.MatrixCellGeometry geo = BuildCellGeometry(map, areaId, gameFamily, x, y, altitudeY,
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
            NsbmdGeometry.MatrixStitchMode mode = NsbmdGeometry.MatrixStitchMode.Grid,
            IEnumerable<(int cellX, int cellY, PlacedBuilding placed)> extras = null)
            => BuildFromPlaced(gameFamily,
                loadedCells.Select(c => (c.cellX, c.cellY, c.map, c.areaId, c.altitudeY, 0f, 0f)), mode, extras);

        public static NsbmdRenderModel BuildFromPlaced(
            GameFamilies gameFamily,
            IEnumerable<(int cellX, int cellY, MapFile map, byte areaId, float altitudeY, float shiftX, float shiftZ)> loadedCells,
            NsbmdGeometry.MatrixStitchMode mode = NsbmdGeometry.MatrixStitchMode.Grid,
            IEnumerable<(int cellX, int cellY, PlacedBuilding placed)> extras = null)
        {
            List<NsbmdGeometry.MatrixCellGeometry> cells = new List<NsbmdGeometry.MatrixCellGeometry>();
            string mapTexDir = gameDirs[DirNames.mapTextures].unpackedDir;
            string extBldDir = gameDirs[DirNames.exteriorBuildingModels].unpackedDir;
            string intBldDir = gameDirs.ContainsKey(DirNames.interiorBuildingModels) ? gameDirs[DirNames.interiorBuildingModels].unpackedDir : null;
            string bldTexDir = gameDirs[DirNames.buildingTextures].unpackedDir;
            Dictionary<byte, AreaData> areaCache = new Dictionary<byte, AreaData>();

            foreach ((int cellX, int cellY, MapFile map, byte areaId, float altitudeY, float shiftX, float shiftZ) in loadedCells)
            {
                try
                {
                    NsbmdGeometry.MatrixCellGeometry geo = BuildCellGeometry(map, areaId, gameFamily, cellX, cellY, altitudeY,
                        mapTexDir, extBldDir, intBldDir, bldTexDir, areaCache);
                    if (geo != null) { geo.ShiftX = shiftX; geo.ShiftZ = shiftZ; cells.Add(geo); }
                }
                catch (Exception ex) { AppLogger.Error($"Loaded cell ({cellX},{cellY}) failed: {ex.Message}"); }
            }

            if (extras != null)
                foreach (NsbmdGeometry.MatrixCellGeometry cell in cells)
                {
                    List<PlacedBuilding> standing = extras.Where(e => e.cellX == cell.CellX && e.cellY == cell.CellY)
                                         .Select(e => e.placed).ToList();
                    if (standing.Count == 0) continue;

                    List<PlacedBuilding> all = new List<PlacedBuilding>(cell.Buildings ?? (IReadOnlyList<PlacedBuilding>)Array.Empty<PlacedBuilding>());
                    all.AddRange(standing);
                    cell.Buildings = all;
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
            if (!areaCache.TryGetValue(areaId, out AreaData area)) { area = new AreaData(areaId); areaCache[areaId] = area; }

            // HGSS indoor areas use the interior building model set.
            bool interior = gameFamily == GameFamilies.HGSS && area.areaType == AreaData.TYPE_INDOOR;
            string bldDir = (interior && intBldDir != null) ? intBldDir : extBldDir;

            BdhcFile.TryParse(map.bdhc, out BdhcFile bdhc);

            if (map.mapModel?.models != null && map.mapModel.models.Length > 0)
                BindNsbtx(map.mapModel, Path.Combine(mapTexDir, area.mapTileset.ToString("D4")));
            Dictionary<int, (Dictionary<string, NsbmdTextureData> Frames, List<(string Swap, int Frames)> Sequence)> mapAnimations = FieldAnimationFrames(map.mapModel);

            List<PlacedBuilding> buildings = new List<PlacedBuilding>();
            Dictionary<int, Dictionary<string, NsbmdTextureData>> swappable = new Dictionary<int, Dictionary<string, NsbmdTextureData>>();
            string btexPath = Path.Combine(bldTexDir, area.buildingsTileset.ToString("D4"));
            byte[] bldTex = System.IO.File.Exists(btexPath) ? System.IO.File.ReadAllBytes(btexPath) : null;

            if (map.buildings != null)
                foreach (Building b in map.buildings)
                {
                    if (b.NSBMDFile == null)
                    {
                        string mp = Path.Combine(bldDir, b.modelID.ToString("D4"));
                        if (!System.IO.File.Exists(mp)) continue;
                        using FileStream fs = new FileStream(mp, FileMode.Open, FileAccess.Read);
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
                MapAnimations = mapAnimations,
                GroundScroll = GroundAnimationSet.ForArea(area),
                CellX = cellX,
                CellY = cellY,
                Bdhc = bdhc,
                AltitudeY = altitudeY,
            };
        }

        private static FieldTextureAnimations _fieldAnimations;
        private static string _fieldAnimationsDir;

        /// <summary>The field animation list, reread whenever the loaded game changes.</summary>
        public static FieldTextureAnimations FieldAnimations(bool reload = false)
        {
            if (!FieldTextureAnimations.Available) return null;
            string dir = gameDirs[DirNames.fieldTextureAnimations].unpackedDir;
            if (reload || dir != _fieldAnimationsDir) { _fieldAnimations = FieldTextureAnimations.Load(); _fieldAnimationsDir = dir; }
            return _fieldAnimations;
        }

        /// <summary>Decodes every frame of each animated map texture with that texture's own palette, as the game only copies texels.</summary>
        public static Dictionary<int, (Dictionary<string, NsbmdTextureData> Frames, List<(string Swap, int Frames)> Sequence)> FieldAnimationFrames(NSBMD container)
        {
            NSBMDModel model = container?.models?.Length > 0 ? container.models[0] : null;
            FieldTextureAnimations list = model == null ? null : FieldAnimations();
            if (list == null || list.Entries.Count == 0) return null;

            Dictionary<int, (Dictionary<string, NsbmdTextureData> Frames, List<(string Swap, int Frames)> Sequence)> found = new Dictionary<int, (Dictionary<string, NsbmdTextureData> Frames, List<(string Swap, int Frames)> Sequence)>();
            Dictionary<string, List<NSBMDTexture>> packs = new Dictionary<string, List<NSBMDTexture>>();
            for (int k = 0; k < model.Materials.Count; k++)
            {
                NSBMDMaterial mat = model.Materials[k];
                FieldTextureAnimations.Entry entry = list.For(mat.texname);
                if (entry?.FramePack == null || entry.Frames.Count == 0 || mat.texdata == null) continue;
                try
                {
                    if (!packs.TryGetValue(entry.Name, out List<NSBMDTexture> frames))
                    {
                        NSBTXLoader.LoadNsbtx(new MemoryStream(entry.FramePack), out frames, out _);
                        packs[entry.Name] = frames;
                    }
                    Dictionary<string, NsbmdTextureData> decoded = new Dictionary<string, NsbmdTextureData>();
                    List<(string, int)> sequence = new List<(string, int)>();
                    foreach ((byte frame, byte duration) in entry.Frames)
                    {
                        if (frames == null || frame >= frames.Count) continue;
                        string swap = "anim" + frame;
                        if (!decoded.ContainsKey(swap))
                        {
                            NSBMDMaterial stand_in = mat.Clone();
                            stand_in.texdata = frames[frame].texdata;
                            stand_in.texoffset = frames[frame].texoffset;
                            stand_in.texsize = frames[frame].texsize;
                            stand_in.spdata = frames[frame].spdata;
                            NsbmdTextureData data = NsbmdTextureDecoder.Decode(stand_in);
                            if (data == null) continue;
                            decoded[swap] = data;
                        }
                        sequence.Add((swap, Math.Max(1, (int)duration)));
                    }
                    if (sequence.Count > 0) found[k] = (decoded, sequence);
                }
                catch (Exception ex) { AppLogger.Error($"Field animation {entry.Name} failed: {ex.Message}"); }
            }
            return found.Count > 0 ? found : null;
        }

        private static byte ResolveAreaId(GameMatrix matrix, int x, int y, byte fallbackAreaId,
            int mapIndex, Func<int, byte?> areaForMap)
        {
            if (matrix.hasHeadersSection)
            {
                try
                {
                    ushort headerId = matrix.headers[y, x];
                    MapHeader h = MapHeader.GetMapHeader(headerId);
                    if (h != null) return h.areaDataID;
                }
                catch { /* fall through */ }
            }
            if (areaForMap != null)
            {
                byte? a = areaForMap(mapIndex);
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
        /// Decodes every texture a building's swapping animations can put on screen, keyed by model id then
        /// by texture name.
        /// </summary>
        private static void CollectSwappableTextures(NSBMD file, int modelId, bool indoor,
            Dictionary<int, Dictionary<string, NsbmdTextureData>> into)
        {
            if (file?.models == null || file.models.Length == 0 || into.ContainsKey(modelId)) return;

            IReadOnlyList<TexturePatternAnimation> patterns = BuildingAnimationSet.PatternsFor(modelId, indoor);
            if (patterns.Count == 0) return;

            NSBMDModel model = file.models[0];
            Dictionary<string, string> wanted = new Dictionary<string, string>();       // texture name → palette name
            foreach (TexturePatternAnimation anim in patterns)
                for (int m = 0; m < anim.MaterialNames.Count; m++)
                    foreach (TexturePatternAnimation.Swap swap in anim.AllSwaps(m))
                        if (swap.IsSet) wanted[swap.TextureName] = swap.PaletteName;
            if (wanted.Count == 0) return;

            Dictionary<string, NsbmdTextureData> decoded = new Dictionary<string, NsbmdTextureData>();
            foreach (KeyValuePair<string, string> kv in wanted)
            {
                try
                {
                    NSBMDTexture tex = file.Textures?.FirstOrDefault(t => t.texname == kv.Key);
                    if (tex == null) continue;
                    NSBMDPalette pal = file.Palettes?.FirstOrDefault(pp => pp.palname == kv.Value);

                    // Borrow one of the model's materials for its render flags, then point it at this
                    // texture and palette so the normal decoder can do the work.
                    NSBMDMaterial basis = model.Materials.Count > 0 ? model.Materials[0] : null;
                    if (basis == null) continue;
                    NSBMDMaterial stand_in = new NSBMDMaterial
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
                    NsbmdTextureData data = NsbmdTextureDecoder.Decode(stand_in);
                    if (data != null) decoded[kv.Key] = data;
                }
                catch (Exception ex) { AppLogger.Error($"Building {modelId} texture {kv.Key} failed: {ex.Message}"); }
            }
            if (decoded.Count > 0) into[modelId] = decoded;
        }
}
}
