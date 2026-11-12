using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Platform.Storage;
using DSPRE.Avalonia;
using DSPRE.Avalonia.Gl;
using DSPRE.Editors;
using DSPRE.Resources;
using DSPRE.Models;
using DSPRE.ROMFiles;
using LibNDSFormats.NSBMD;
using LibNDSFormats.NSBTX;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.World
{
    public partial class MapEditorViewModel
    {
        private void LoadMap(int index)
        {
            ClearPendingEvents();
            try
            {
                _map = new MapFile(new MemoryStream(_archive.Read(index) ?? throw new FileNotFoundException($"Map {index} is missing.")), GameFamily);
                _map.KeptPlates = _platesToClear.Contains(index) ? new List<BdhcBuild.Piece>() : KeptPlatesFile.Load(index);
                ResolveTilesetForMap(index);
                RefreshBuildings();

                OpenModelEditor(index, AreaForMap(index) ?? (byte)_previewArea);

                SetClean();
                ResetUndo();
                StatusText = "";
                OnPropertyChanged(nameof(CanEditModel));
                OnPropertyChanged(nameof(FocusedMapIndex));
                OnPropertyChanged(nameof(Collisions));
                OnPropertyChanged(nameof(Types));
                OnPropertyChanged(nameof(UnsavedChangesDescription));
                if (IsSingleMap)
                {
                    BuildPreview();
                    RebuildOverlay();
                    MapLoaded?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex) when (AppLogger.Noted("MapEditorViewModel.LoadMap", ex))
            {
                _ = DialogHelper.ShowErrorAsync($"Couldn't load map {index}:\n{ex.Message}", "Map editor");
            }
        }

        private void RebuildPreview()
        {
            if (IsHeaderView) RebuildHeaderPreview();
            else BuildPreview();
            RebuildOverlay();
            MapLoaded?.Invoke(this, EventArgs.Empty);
        }

        private void RefreshView()
        {
            if (IsMatrixView) BuildMatrixPreview();
            else if (IsHeaderView) BuildHeaderPreview();
            else if (_selectedMapIndex >= 0) LoadMap(_selectedMapIndex);
        }

        private int _headerMapsOf = -1;

        private void ShowHeaderMaps()
        {
            int keep = _headerMapsOf == _headerId ? _selectedHeaderMap : 0;
            _headerMapsOf = _headerId;
            HeaderMapNames.Clear();
            foreach (HeaderMapCell c in _headerCells) HeaderMapNames.Add($"Map {c.MapIndex}");
            _selectedHeaderMap = -1;
            SelectedHeaderMapIndex = _headerCells.Count == 0 ? -1 : Math.Clamp(keep, 0, _headerCells.Count - 1);
            ResetUndo();
        }

        private void BuildHeaderPreview()
        {
            Model3D = null;
            _headerCells.Clear();
            SelectedBuildingIndex = -1;
            try
            {
                if (_headerId < 0) { StatusText = "No header selected."; RefreshBuildings(); MapLoaded?.Invoke(this, EventArgs.Empty); return; }
                MapHeader hdr;
                try { hdr = MapHeader.GetMapHeader((ushort)_headerId); } catch (Exception ex) { AppLogger.WarnOnce("MapEditorViewModel.BuildHeaderPreview", ex); hdr = null; }
                if (hdr == null) { StatusText = $"Header {_headerId}: not found."; RefreshBuildings(); MapLoaded?.Invoke(this, EventArgs.Empty); return; }
                if (_headerId == MapHeader.Everywhere)
                {
                    StatusText = "Header 0 has no maps.";
                    ShowHeaderMaps(); RefreshBuildings(); SetClean(); RebuildOverlay(); MapLoaded?.Invoke(this, EventArgs.Empty); return;
                }

                LoadArea(hdr.AreaDataID);
                SyncTilesetsToArea();
                GameMatrix matrix = new GameMatrix(hdr.MatrixID);
                for (int y = 0; y < matrix.Height; y++)
                    for (int x = 0; x < matrix.Width; x++)
                    {
                        if (matrix.Maps[y, x] == GameMatrix.EMPTY) continue;
                        if (matrix.HasHeadersSection && matrix.Headers[y, x] != _headerId) continue;
                        int mapIndex = matrix.Maps[y, x];

                        byte areaId = hdr.AreaDataID;
                        if (matrix.HasHeadersSection)
                        {
                            try { MapHeader hh = MapHeader.GetMapHeader(matrix.Headers[y, x]); if (hh != null) areaId = hh.AreaDataID; } catch { /* keep hdr's area */ }
                        }

                        float altitudeY = matrix.HasHeightsSection ? matrix.Altitudes[y, x] * (NsbmdGeometry.TileSize / 2f) : 0f;
                        MapFile map = new MapFile(mapIndex, GameFamily, discardMoveperms: false);
                        _headerCells.Add(new HeaderMapCell { CellX = x, CellY = y, MapIndex = mapIndex, AreaId = areaId, AltitudeY = altitudeY, Map = map });
                    }

                if (_headerCells.Count == 0)
                {
                    StatusText = $"Header {_headerId}: no maps found in its matrix.";
                    RefreshBuildings(); MapLoaded?.Invoke(this, EventArgs.Empty); return;
                }

                Model3D = MatrixSceneBuilder.BuildFromLoaded(GameFamily,
                    _headerCells.Select(c => (c.CellX, c.CellY, c.Map, c.AreaId, c.AltitudeY)), StitchMode);
                ShowHeaderMaps();
                RefreshBuildings();
                SetClean();
                StatusText = Model3D != null
                    ? $"Header {_headerId}: {Plural.Of(_headerCells.Count, "map")} stitched."
                    : $"Header {_headerId}: no renderable maps.";
            }
            catch (Exception ex)
            {
                AppLogger.Error("Header map preview failed: " + ex.Message);
                StatusText = "Header map render failed: " + ex.Message;
            }
            RebuildOverlay();
            MapLoaded?.Invoke(this, EventArgs.Empty);
        }

        // Re-stitches from memory, not disk, so unsaved painting and building edits show.
        private void RebuildHeaderPreview()
        {
            if (_headerCells.Count == 0) { Model3D = null; return; }
            try
            {
                Model3D = MatrixSceneBuilder.BuildFromLoaded(GameFamily,
                    _headerCells.Select(c => (c.CellX, c.CellY, c.Map, c.AreaId, c.AltitudeY)), StitchMode);
            }
            catch (Exception ex) { AppLogger.Error("Header preview rebuild failed: " + ex.Message); }
        }

        // Where the shown map is closed off, for the preview's people to walk against.
        public MapCollisionGrid Collision
        {
            get
            {
                MapCollisionGrid grid = new MapCollisionGrid();
                if (_map?.Collisions != null) grid.Add(0, 0, _map.Collisions);
                if (_map?.Types != null) grid.AddTypes(0, 0, _map.Types);
                return grid;
            }
        }

        public AreaData Area
        {
            get
            {
                try { return AreaForMap(_selectedMapIndex) is byte id ? new AreaData(id) : null; }
                catch (Exception ex) { AppLogger.WarnOnce("MapEditorViewModel.Area", ex); return null; }
            }
        }

        private byte? AreaForMap(int mapIndex)
            => _mapToArea != null && _mapToArea.TryGetValue(mapIndex, out byte a) ? a : (byte?)null;

        private void BuildMatrixPreview()
        {
            Model3D = null;
            try
            {
                if (_selectedMatrix < 0) { MapLoaded?.Invoke(this, EventArgs.Empty); return; }
                GameMatrix matrix = new GameMatrix(_selectedMatrix);
                int used = 0;
                for (int y = 0; y < matrix.Height; y++)
                    for (int x = 0; x < matrix.Width; x++)
                        if (matrix.Maps[y, x] != GameMatrix.EMPTY) used++;
                MatrixInfo = $"{matrix.Width}×{matrix.Height}, {Plural.Of(used, "map")}";
                byte fallback = AreaForMap(matrix.Maps[0, 0]) ?? 0;
                Model3D = MatrixSceneBuilder.Build(matrix, fallback, GameFamily, AreaForMap, mode: StitchMode);
                StatusText = Model3D != null
                    ? $"Matrix {_selectedMatrix}: {matrix.Width}×{matrix.Height}, {Plural.Of(used, "map")} stitched."
                    : $"Matrix {_selectedMatrix} has no renderable maps.";
            }
            catch (Exception ex)
            {
                AppLogger.Error("Matrix preview failed: " + ex.Message);
                StatusText = "Matrix render failed: " + ex.Message;
            }
            // No permission overlay / paint grids in matrix mode.
            OverlayMesh = null; OverlayVertexCount = 0; OverlayChanged?.Invoke(this, EventArgs.Empty);
            MapLoaded?.Invoke(this, EventArgs.Empty);
        }

        private void ResolveTilesetForMap(int mapIndex)
        {
            if (_mapToArea == null || !_mapToArea.TryGetValue(mapIndex, out byte areaId))
            {
                _area = null;
                RaiseArea();
                OnPropertyChanged(nameof(NoHeaderNote));
                bool was = _suppress;
                _suppress = true;
                PreviewArea = _previewAreas.TryGetValue(mapIndex, out int chosen) ? chosen : 0;
                _suppress = was;
                UsePreviewArea();
                return;
            }
            LoadArea(areaId);
            SyncTilesetsToArea();
            OnPropertyChanged(nameof(NoHeaderNote));
        }

        private void SyncTilesetsToArea()
        {
            if (_area == null) return;
            bool was = _suppress;
            _suppress = true;
            try
            {
                // Adding textures makes a new pack.
                int packs = Filesystem.GetMapTexturesCount();
                for (int i = MapTilesets.Count; i < packs; i++) MapTilesets.Add("Map textures " + i);
                if (MapTilesets.Count > 0)
                {
                    _mapTilesetIndex = Math.Min(_area.MapTileset, MapTilesets.Count - 1);
                    OnPropertyChanged(nameof(MapTilesetIndex));
                }
                int bld = _area.BuildingsTileset + 1; // building combo has "None" at index 0
                _buildingTilesetIndex = bld >= 0 && bld < BuildingTilesets.Count ? bld : 0;
                OnPropertyChanged(nameof(BuildingTilesetIndex));
            }
            catch (Exception ex) { AppLogger.Error("Tileset resolve failed: " + ex.Message); }
            finally { _suppress = was; }
        }

        // A header section gives each cell its header's area, otherwise the matrix's header does.
        private static Dictionary<int, byte> BuildMapAreaLookup()
        {
            Dictionary<int, byte> lookup = new Dictionary<int, byte>();
            try
            {
                int headerCount = GetHeaderCount();
                int matrixCount = Filesystem.GetMatrixCount();

                // DP and Pt put a Mystery Zone header first on two matrices, so a named location replaces it.
                Dictionary<int, byte> matrixArea = new Dictionary<int, byte>();
                HashSet<int> fromMysteryZone = new HashSet<int>();
                for (ushort h = 0; h < headerCount; h++)
                {
                    try
                    {
                        MapHeader header = MapHeader.GetMapHeader(h);
                        if (header == null) continue;
                        bool mystery = HeaderLabels.LocationOf(header) == HeaderLabels.MysteryZone;
                        if (matrixArea.ContainsKey(header.MatrixID) && (mystery || !fromMysteryZone.Contains(header.MatrixID))) continue;
                        matrixArea[header.MatrixID] = header.AreaDataID;
                        if (mystery) fromMysteryZone.Add(header.MatrixID); else fromMysteryZone.Remove(header.MatrixID);
                    }
                    catch { /* skip bad header */ }
                }

                // Plain matrices first, then header-section matrices, which win on overlap.
                for (int pass = 0; pass < 2; pass++)
                    for (int mid = 0; mid < matrixCount; mid++)
                    {
                        try
                        {
                            GameMatrix mtx = new GameMatrix(mid);
                            bool section = mtx.HasHeadersSection;
                            if (section != (pass == 1)) continue;
                            if (!section && !matrixArea.TryGetValue(mid, out byte plainArea)) continue;

                            for (int y = 0; y < mtx.Height; y++)
                                for (int x = 0; x < mtx.Width; x++)
                                {
                                    int map = mtx.Maps[y, x];
                                    if (map == GameMatrix.EMPTY) continue;
                                    byte area;
                                    if (section)
                                    {
                                        try { MapHeader hh = MapHeader.GetMapHeader(mtx.Headers[y, x]); if (hh == null) continue; area = hh.AreaDataID; }
                                        catch (Exception ex) { AppLogger.WarnOnce("MapEditorViewModel.BuildMapAreaLookup", ex); continue; }
                                    }
                                    else area = matrixArea[mid];
                                    lookup[map] = area;
                                }
                        }
                        catch { /* skip bad matrix */ }
                    }
            }
            catch (Exception ex) { AppLogger.Error("Map to area lookup failed: " + ex.Message); }
            return lookup;
        }

        private void BuildPreview()
        {
            Model3D = null;
            try
            {
                if (_map == null) return;

                // Textures are always bound; ShowTextures only hides them.
                if (_mapTilesetIndex >= 0 && _map.MapModel?.Models != null && _map.MapModel.Models.Length > 0)
                    BindNsbtx(_map.MapModel, ArchiveMembers.PathOf(DirNames.MapTextures, _mapTilesetIndex));

                // HGSS: the map's area data picks interior or exterior buildings, as in MatrixSceneBuilder.
                List<PlacedBuilding> buildings = new List<PlacedBuilding>();
                DirNames models = DirNames.ExteriorBuildingModels;
                if (GameFamily == GameFamilies.HGSS)
                    try { if (AreaForMap(_selectedMapIndex) is byte aid) models = new AreaData(aid).BuildingModels; }
                    catch { /* fall back to exterior */ }
                byte[] bldTex = null;
                if (_buildingTilesetIndex > 0)
                {
                    bldTex = ArchiveMembers.ReadOrNull(DirNames.BuildingTextures, _buildingTilesetIndex - 1);
                }

                if (_map.Buildings != null)
                    foreach (Building b in _map.Buildings)
                    {
                        if (b.NSBMDFile == null)
                        {
                            byte[] model = ArchiveMembers.ReadOrNull(models, (int)b.ModelID);
                            if (model == null) continue;
                            b.NSBMDFile = NSBMDLoader.LoadNSBMD(new MemoryStream(model));
                        }
                        if (b.NSBMDFile?.Models == null || b.NSBMDFile.Models.Length == 0) continue;

                        if (bldTex != null)
                        {
                            try
                            {
                                b.NSBMDFile.Materials = NSBTXLoader.LoadNsbtx(new MemoryStream(bldTex), out b.NSBMDFile.Textures, out b.NSBMDFile.Palettes);
                                b.NSBMDFile.MatchTextures();
                            }
                            catch { /* pack doesn't match this building, leave untextured */ }
                        }

                        buildings.Add(new PlacedBuilding
                        {
                            Model = b.NSBMDFile.Models[0],
                            Transform = MapGeometry.BuildingTransform(b),
                            ModelId = (int)b.ModelID,
                            TileX = b.XPosition,
                            TileZ = b.ZPosition,
                            Source = b,
                        });
                    }

                TextureSrtAnimation scroll = null;
                try { if (AreaForMap(_selectedMapIndex) is byte aid) scroll = GroundAnimationSet.ForArea(new AreaData(aid)); } catch (Exception ex) { AppLogger.Warn("Map editor: ground animation: " + ex.Message); }
                Model3D = NsbmdGeometry.BuildScene(_map.MapModel?.Models?.Length > 0 ? _map.MapModel.Models[0] : null, buildings,
                                                   MatrixSceneBuilder.FieldAnimationFrames(_map.MapModel), scroll);
            }
            catch (Exception ex) { AppLogger.Error("Map preview build failed: " + ex.Message); }
        }

        private static void BindNsbtx(NSBMD container, string path)
        {
            try
            {
                byte[] pack = ArchiveMembers.ReadOrNull(path);
                if (pack == null) return;
                container.Materials = NSBTXLoader.LoadNsbtx(new MemoryStream(pack), out container.Textures, out container.Palettes);
                container.MatchTextures();
            }
            catch (Exception ex) { AppLogger.Error("Map tileset bind failed: " + ex.Message); }
        }

        private void RefreshBuildings()
        {
            Buildings.Clear();
            _headerBuildingIndex.Clear();
            RaiseBuildingLimit();
            if (IsHeaderView)
            {
                for (int ci = 0; ci < _headerCells.Count; ci++)
                {
                    HeaderMapCell cell = _headerCells[ci];
                    if (cell.Map?.Buildings == null) continue;
                    for (int bi = 0; bi < cell.Map.Buildings.Count; bi++)
                    {
                        Buildings.Add($"Map {cell.MapIndex} · Building {bi:D2}");
                        _headerBuildingIndex.Add((ci, bi));
                    }
                }
                return;
            }
            if (_map?.Buildings == null) return;
            for (int i = 0; i < _map.Buildings.Count; i++)
                Buildings.Add($"Building {i:D2}");
        }

        // pokeplatinum MAX_LOADED_MAP_PROPS: more buildings than this on one map aren't loaded.
        public const int MaxLoadedBuildings = 32;

        // Which shown maps hold more buildings than the game loads, or empty.
        public string BuildingLimitWarning
        {
            get
            {
                IEnumerable<string> over = IsHeaderView
                    ? _headerCells.Where(c => (c.Map?.Buildings?.Count ?? 0) > MaxLoadedBuildings)
                                  .Select(c => $"Map {c.MapIndex}: {c.Map.Buildings.Count}")
                    : (_map?.Buildings?.Count ?? 0) > MaxLoadedBuildings ? new[] { $"{_map.Buildings.Count} buildings" } : Array.Empty<string>();
                return string.Join(", ", over);
            }
        }
        public bool HasBuildingLimitWarning => BuildingLimitWarning.Length > 0;

        private void RaiseBuildingLimit()
        {
            OnPropertyChanged(nameof(BuildingLimitWarning));
            OnPropertyChanged(nameof(HasBuildingLimitWarning));
        }
    }
}
