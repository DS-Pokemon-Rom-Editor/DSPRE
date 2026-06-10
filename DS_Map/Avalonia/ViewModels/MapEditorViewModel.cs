using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Platform.Storage;
using DSPRE.Avalonia;
using DSPRE.Avalonia.Gl;
using DSPRE.Editors;
using DSPRE.Resources;
using DSPRE.ROMFiles;
using LibNDSFormats.NSBMD;
using LibNDSFormats.NSBTX;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
{
    public sealed class PainterOption
    {
        public byte Value { get; }
        public string Name { get; }
        public PainterOption(byte v, string n) { Value = v; Name = n; }
        public override string ToString() => Name;
    }

    /// <summary>
    /// Avalonia port of the WinForms <c>MapEditor</c> — core scope: map-file selection,
    /// a textured-geometry 3D preview (via <see cref="NsbmdGlControl"/>), the two 32×32
    /// movement-permission grids (collision + type, painted via
    /// <see cref="PermissionGridControl"/>), a buildings list, and save / import /
    /// export of the map .bin. Building placement-by-picking and tileset texture binding
    /// for the preview are deferred.
    /// </summary>
    public class MapEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private Window _owner;
        private bool _suppress;
        private MapFile _map;
        private Dictionary<int, byte> _mapToArea;   // map index → areaDataID (for the correct tileset)

        /// <summary>Raised after a map is (re)loaded so the view can refresh the GL control + grids.</summary>
        public event EventHandler MapLoaded;

        public ObservableCollection<string> MapNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Buildings { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> MapTilesets { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> BuildingTilesets { get; } = new ObservableCollection<string>();

        // ── View mode: single map vs. full matrix (fly-around) ──────────────────────────
        public ObservableCollection<string> ViewModes { get; } = new ObservableCollection<string> { "Single map", "Full matrix" };
        public ObservableCollection<string> Matrices { get; } = new ObservableCollection<string>();

        private int _viewModeIndex;
        public int ViewModeIndex { get => _viewModeIndex; set { if (Set(ref _viewModeIndex, value)) { OnPropertyChanged(nameof(IsSingleMap)); OnPropertyChanged(nameof(IsMatrixView)); RefreshView(); } } }
        public bool IsSingleMap => _viewModeIndex == 0;
        public bool IsMatrixView => _viewModeIndex == 1;

        private int _selectedMatrix = -1;
        public int SelectedMatrixIndex { get => _selectedMatrix; set { if (Set(ref _selectedMatrix, value) && !_suppress && value >= 0 && IsMatrixView) BuildMatrixPreview(); } }

        private int _mapTilesetIndex = -1;
        public int MapTilesetIndex { get => _mapTilesetIndex; set { if (Set(ref _mapTilesetIndex, value) && !_suppress && _map != null) RebuildPreview(); } }

        private int _buildingTilesetIndex;
        public int BuildingTilesetIndex { get => _buildingTilesetIndex; set { if (Set(ref _buildingTilesetIndex, value) && !_suppress && _map != null) RebuildPreview(); } }
        public ObservableCollection<PainterOption> CollisionPainters { get; } = new ObservableCollection<PainterOption>();
        public ObservableCollection<PainterOption> TypePainters { get; } = new ObservableCollection<PainterOption>();

        public byte[,] Collisions => _map?.collisions;
        public byte[,] Types => _map?.types;
        public NsbmdRenderModel Model3D { get; private set; }

        private int _selectedMapIndex = -1;
        public int SelectedMapIndex
        {
            get => _selectedMapIndex;
            set { if (Set(ref _selectedMapIndex, value) && !_suppress && value >= 0) LoadMap(value); }
        }

        private int _collisionPainterIndex;
        public int CollisionPainterIndex
        {
            get => _collisionPainterIndex;
            set { if (Set(ref _collisionPainterIndex, value)) OnPropertyChanged(nameof(CollisionPaintValue)); }
        }
        public byte CollisionPaintValue =>
            _collisionPainterIndex >= 0 && _collisionPainterIndex < CollisionPainters.Count ? CollisionPainters[_collisionPainterIndex].Value : (byte)0;

        private int _typePainterIndex;
        public int TypePainterIndex
        {
            get => _typePainterIndex;
            set { if (Set(ref _typePainterIndex, value)) OnPropertyChanged(nameof(TypePaintValue)); }
        }
        public byte TypePaintValue =>
            _typePainterIndex >= 0 && _typePainterIndex < TypePainters.Count ? TypePainters[_typePainterIndex].Value : (byte)0;

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        // ── 3D permission overlay ──────────────────────────────────────────────────────
        public ObservableCollection<string> OverlayModes { get; } = new ObservableCollection<string> { "No overlay", "Collision", "Type" };
        private int _overlayModeIndex;
        public int OverlayModeIndex { get => _overlayModeIndex; set { if (Set(ref _overlayModeIndex, value)) RebuildOverlay(); } }

        public float[] OverlayMesh { get; private set; }
        public int OverlayVertexCount { get; private set; }
        public event EventHandler OverlayChanged;

        public void RebuildOverlay()
        {
            OverlayMesh = null; OverlayVertexCount = 0;
            if (_map != null && Model3D != null && _overlayModeIndex > 0)
            {
                bool collision = _overlayModeIndex == 1;
                byte[,] grid = collision ? _map.collisions : _map.types;
                int n = grid.GetLength(0);     // 32
                var m = Model3D;

                // Fit the overlay to the MAP model footprint (not the whole scene, which
                // includes buildings), and sit it just above the map's top surface.
                float minX = m.HasMapBounds ? m.MapMinX : m.RawMinX;
                float maxX = m.HasMapBounds ? m.MapMaxX : m.RawMaxX;
                float minZ = m.HasMapBounds ? m.MapMinZ : m.RawMinZ;
                float maxZ = m.HasMapBounds ? m.MapMaxZ : m.RawMaxZ;
                float topY = m.HasMapBounds ? m.MapMaxY : m.RawMaxY;
                float yEps = topY + (maxX - minX) * 0.0003f;   // sit right on the surface
                var v = new List<float>(n * n * 48);

                for (int row = 0; row < n; row++)
                    for (int col = 0; col < n; col++)
                    {
                        var (r, g, b) = DSPRE.Avalonia.Gl.PermissionColors.Rgb(grid[row, col], collision);
                        float x0 = Lerp(minX, maxX, col / (float)n), x1 = Lerp(minX, maxX, (col + 1) / (float)n);
                        float z0 = Lerp(minZ, maxZ, row / (float)n), z1 = Lerp(minZ, maxZ, (row + 1) / (float)n);
                        var a = m.ToNormalized(x0, yEps, z0);
                        var bb = m.ToNormalized(x1, yEps, z0);
                        var c = m.ToNormalized(x1, yEps, z1);
                        var d = m.ToNormalized(x0, yEps, z1);
                        AddQuad(v, a, bb, c, d, r, g, b);
                    }

                OverlayMesh = v.ToArray();
                OverlayVertexCount = v.Count / 8;
            }
            OverlayChanged?.Invoke(this, EventArgs.Empty);
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private static void AddQuad(List<float> v, (float x, float y, float z) a, (float x, float y, float z) b,
            (float x, float y, float z) c, (float x, float y, float z) d, float r, float g, float bl)
        {
            void Vtx((float x, float y, float z) p) { v.Add(p.x); v.Add(p.y); v.Add(p.z); v.Add(0); v.Add(0); v.Add(r); v.Add(g); v.Add(bl); }
            Vtx(a); Vtx(b); Vtx(c);
            Vtx(a); Vtx(c); Vtx(d);
        }

        // ── Building detail / add / remove ──────────────────────────────────────────────
        private int _selectedBuildingIndex = -1;
        public int SelectedBuildingIndex
        {
            get => _selectedBuildingIndex;
            set { if (Set(ref _selectedBuildingIndex, value)) LoadBuildingDetail(); }
        }

        public bool HasBuildingSelected => _selectedBuildingIndex >= 0 && _map?.buildings != null && _selectedBuildingIndex < _map.buildings.Count;

        private decimal _bModelId, _bx, _by, _bz, _bRotX, _bRotY, _bRotZ;
        public decimal BModelId { get => _bModelId; set { if (Set(ref _bModelId, value) && !_suppress) ApplyBuilding(reloadModel: true); } }
        public decimal BX { get => _bx; set { if (Set(ref _bx, value) && !_suppress) ApplyBuilding(); } }
        public decimal BY { get => _by; set { if (Set(ref _by, value) && !_suppress) ApplyBuilding(); } }
        public decimal BZ { get => _bz; set { if (Set(ref _bz, value) && !_suppress) ApplyBuilding(); } }
        public decimal BRotX { get => _bRotX; set { if (Set(ref _bRotX, value) && !_suppress) ApplyBuilding(); } }
        public decimal BRotY { get => _bRotY; set { if (Set(ref _bRotY, value) && !_suppress) ApplyBuilding(); } }
        public decimal BRotZ { get => _bRotZ; set { if (Set(ref _bRotZ, value) && !_suppress) ApplyBuilding(); } }

        private void LoadBuildingDetail()
        {
            OnPropertyChanged(nameof(HasBuildingSelected));
            if (!HasBuildingSelected) return;
            var b = _map.buildings[_selectedBuildingIndex];
            _suppress = true;
            BModelId = b.modelID; BX = b.xPosition; BY = b.yPosition; BZ = b.zPosition;
            BRotX = (decimal)Math.Round(Building.U16ToDeg(b.xRotation)); BRotY = (decimal)Math.Round(Building.U16ToDeg(b.yRotation)); BRotZ = (decimal)Math.Round(Building.U16ToDeg(b.zRotation));
            _suppress = false;
        }

        private static ushort DegToU16(decimal deg) => (ushort)(((double)deg % 360) / 360.0 * 65536.0);

        private void ApplyBuilding(bool reloadModel = false)
        {
            if (!HasBuildingSelected) return;
            var b = _map.buildings[_selectedBuildingIndex];
            b.modelID = (uint)_bModelId;
            b.xPosition = (short)_bx; b.yPosition = (short)_by; b.zPosition = (short)_bz;
            b.xRotation = DegToU16(_bRotX); b.yRotation = DegToU16(_bRotY); b.zRotation = DegToU16(_bRotZ);
            if (reloadModel) b.NSBMDFile = null;   // force reload of the (new) model in BuildPreview
            MarkDirty();
            // NOTE: deliberately do not touch the Buildings list here — replacing the selected
            // item would drop the ListBox selection. The model ID lives in the detail panel.
            RebuildPreview();
        }

        public void AddBuilding()
        {
            if (_map?.buildings == null) return;
            _map.buildings.Add(new Building());
            RefreshBuildings();
            MarkDirty();
            SelectedBuildingIndex = _map.buildings.Count - 1;
            RebuildPreview();
        }

        public void RemoveBuilding()
        {
            if (!HasBuildingSelected) return;
            _map.buildings.RemoveAt(_selectedBuildingIndex);
            RefreshBuildings();
            MarkDirty();
            SelectedBuildingIndex = _map.buildings.Count > 0 ? Math.Min(_selectedBuildingIndex, _map.buildings.Count - 1) : -1;
            RebuildPreview();
        }

        // ── Dirty tracking ───────────────────────────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => $"Map {_selectedMapIndex}";
        public void SaveChanges() => Save();
        public void DiscardChanges() { _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); if (_selectedMapIndex >= 0) LoadMap(_selectedMapIndex); }
        public void MarkDirty() { if (_dirty) return; _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetClean() { if (!_dirty) return; _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        // ── Constructors ────────────────────────────────────────────────────────────
        public MapEditorViewModel() { if (Design.IsDesignMode) MapNames.Add("Map 0"); }
        public MapEditorViewModel(bool _) { }

        // ── Setup ─────────────────────────────────────────────────────────────────────
        public async Task SetupAsync(Window owner)
        {
            _owner = owner;
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> {
                    DirNames.maps, DirNames.exteriorBuildingModels, DirNames.buildingTextures, DirNames.mapTextures,
                    DirNames.matrices, DirNames.areaData, DirNames.dynamicHeaders });
                _mapToArea = BuildMapAreaLookup();

                foreach (var kv in PokeDatabase.System.MapCollisionPainters) CollisionPainters.Add(new PainterOption(kv.Key, kv.Value));
                foreach (var kv in PokeDatabase.System.MapCollisionTypePainters) TypePainters.Add(new PainterOption(kv.Key, kv.Value));
                if (CollisionPainters.Count > 1) CollisionPainterIndex = 1;

                _suppress = true;
                int mapTexCount = Filesystem.GetMapTexturesCount();
                for (int i = 0; i < mapTexCount; i++) MapTilesets.Add("Map Tileset " + i);
                BuildingTilesets.Add("None");
                int bldTexCount = Filesystem.GetBuildingTexturesCount();
                for (int i = 0; i < bldTexCount; i++) BuildingTilesets.Add("Building Tileset " + i);
                // Default to the first tileset so the map shows textured out of the box. There is
                // no direct map→tileset link in the ROM (it goes through area data), so this is a
                // best-effort default the user can change.
                if (MapTilesets.Count > 0) MapTilesetIndex = 0;
                _suppress = false;

                int count = Filesystem.GetMapCount();
                for (int i = 0; i < count; i++) MapNames.Add("Map " + i);

                int matrixCount = Filesystem.GetMatrixCount();
                for (int i = 0; i < matrixCount; i++) Matrices.Add("Matrix " + i);
                _suppress = true; if (matrixCount > 0) { _selectedMatrix = 0; OnPropertyChanged(nameof(SelectedMatrixIndex)); } _suppress = false;

                StatusText = $"{count} maps.";
                if (count > 0) SelectedMapIndex = 0;
            }
            catch (Exception ex)
            {
                StatusText = "Error: " + ex.Message;
                await DialogHelper.ShowError($"Failed to set up Map Editor:\n{ex.Message}", "Map Editor");
            }
        }

        private void LoadMap(int index)
        {
            try
            {
                _map = new MapFile(index, gameFamily);
                ResolveTilesetForMap(index);
                RefreshBuildings();
                SetClean();
                StatusText = $"Loaded map {index}.";
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
            catch (Exception ex)
            {
                _ = DialogHelper.ShowError($"Failed to load map {index}:\n{ex.Message}", "Map Editor");
            }
        }

        /// <summary>Rebuild the 3D preview (+ overlay, which depends on the new normalization).</summary>
        private void RebuildPreview()
        {
            BuildPreview();
            RebuildOverlay();
            MapLoaded?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Switch between single-map and full-matrix renders.</summary>
        private void RefreshView()
        {
            if (IsMatrixView) BuildMatrixPreview();
            else if (_selectedMapIndex >= 0) LoadMap(_selectedMapIndex);
        }

        /// <summary>map index → area-data id via the reverse header/matrix lookup (or null).</summary>
        private byte? AreaForMap(int mapIndex)
            => _mapToArea != null && _mapToArea.TryGetValue(mapIndex, out byte a) ? a : (byte?)null;

        /// <summary>Renders every non-VOID map of the selected matrix, stitched into one scene.</summary>
        private void BuildMatrixPreview()
        {
            Model3D = null;
            try
            {
                if (_selectedMatrix < 0) { MapLoaded?.Invoke(this, EventArgs.Empty); return; }
                var matrix = new GameMatrix(_selectedMatrix);
                byte fallback = AreaForMap(matrix.maps[0, 0]) ?? 0;
                Model3D = MatrixSceneBuilder.Build(matrix, fallback, gameFamily, AreaForMap);
                StatusText = Model3D != null
                    ? $"Matrix {_selectedMatrix}: {matrix.width}×{matrix.height} stitched."
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

        /// <summary>
        /// Resolves the correct texture packs for a map via the real ROM linkage:
        /// map → (a header whose matrix uses it) → areaDataID → AreaData.mapTileset /
        /// buildingsTileset. Sets the tileset selectors so the map shows with its proper
        /// textures by default; the user can still override.
        /// </summary>
        private void ResolveTilesetForMap(int mapIndex)
        {
            if (_mapToArea == null || !_mapToArea.TryGetValue(mapIndex, out byte areaId)) return;
            try
            {
                var area = new AreaData(areaId);
                _suppress = true;
                if (MapTilesets.Count > 0)
                {
                    _mapTilesetIndex = Math.Min(area.mapTileset, MapTilesets.Count - 1);
                    OnPropertyChanged(nameof(MapTilesetIndex));
                }
                int bld = area.buildingsTileset + 1; // building combo has "None" at index 0
                _buildingTilesetIndex = bld >= 0 && bld < BuildingTilesets.Count ? bld : 0;
                OnPropertyChanged(nameof(BuildingTilesetIndex));
                _suppress = false;
            }
            catch (Exception ex) { _suppress = false; AppLogger.Error("Tileset resolve failed: " + ex.Message); }
        }

        private static Dictionary<int, byte> BuildMapAreaLookup()
        {
            var lookup = new Dictionary<int, byte>();
            var matrixCache = new Dictionary<int, GameMatrix>();
            try
            {
                int headerCount = GetHeaderCount();
                for (ushort h = 0; h < headerCount; h++)
                {
                    try
                    {
                        var header = MapHeader.GetMapHeader(h);
                        if (header == null) continue;
                        if (!matrixCache.TryGetValue(header.matrixID, out var mtx))
                        {
                            mtx = new GameMatrix(header.matrixID);
                            matrixCache[header.matrixID] = mtx;
                        }
                        for (int y = 0; y < mtx.height; y++)
                            for (int x = 0; x < mtx.width; x++)
                            {
                                int map = mtx.maps[y, x];
                                if (map != GameMatrix.EMPTY && !lookup.ContainsKey(map)) lookup[map] = header.areaDataID;
                            }
                    }
                    catch { /* skip bad header/matrix */ }
                }
            }
            catch (Exception ex) { AppLogger.Error("Map→area lookup failed: " + ex.Message); }
            return lookup;
        }

        private void BuildPreview()
        {
            Model3D = null;
            try
            {
                if (_map == null) return;

                // Bind the map tileset textures (if a pack is selected).
                if (_mapTilesetIndex >= 0 && _map.mapModel?.models != null && _map.mapModel.models.Length > 0)
                    BindNsbtx(_map.mapModel, gameDirs[DirNames.mapTextures].unpackedDir + "\\" + _mapTilesetIndex.ToString("D4"));

                // Load building models + (optionally) bind building tileset, then collect transforms.
                var buildings = new List<(NSBMDModel model, float[] transform)>();
                string bdir = gameDirs[DirNames.exteriorBuildingModels].unpackedDir;
                byte[] bldTex = null;
                if (_buildingTilesetIndex > 0)
                {
                    string tp = gameDirs[DirNames.buildingTextures].unpackedDir + "\\" + (_buildingTilesetIndex - 1).ToString("D4");
                    if (File.Exists(tp)) bldTex = File.ReadAllBytes(tp);
                }

                if (_map.buildings != null)
                    foreach (var b in _map.buildings)
                    {
                        if (b.NSBMDFile == null)
                        {
                            string mp = bdir + "\\" + b.modelID.ToString("D4");
                            if (!File.Exists(mp)) continue;
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
                            catch { /* pack doesn't match this building — leave untextured */ }
                        }

                        buildings.Add((b.NSBMDFile.models[0], MapGeometry.BuildingTransform(b)));
                    }

                Model3D = NsbmdGeometry.BuildScene(_map.mapModel?.models?.Length > 0 ? _map.mapModel.models[0] : null, buildings);
            }
            catch (Exception ex) { AppLogger.Error("Map preview build failed: " + ex.Message); }
        }

        private static void BindNsbtx(NSBMD container, string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                container.materials = NSBTXLoader.LoadNsbtx(new MemoryStream(File.ReadAllBytes(path)), out container.Textures, out container.Palettes);
                container.MatchTextures();
            }
            catch (Exception ex) { AppLogger.Error("Map tileset bind failed: " + ex.Message); }
        }

        private void RefreshBuildings()
        {
            Buildings.Clear();
            if (_map?.buildings == null) return;
            for (int i = 0; i < _map.buildings.Count; i++)
                Buildings.Add($"Building {i:D2}");
        }

        // ── Save / import / export ─────────────────────────────────────────────────────
        public void Save()
        {
            if (_map == null || _selectedMapIndex < 0) return;
            _map.SaveToFileDefaultDir(_selectedMapIndex, showSuccessMessage: false);
            SetClean();
            StatusText = $"Saved map {_selectedMapIndex}.";
        }

        public async Task ImportAsync()
        {
            if (_selectedMapIndex < 0) return;
            var filter = new FilePickerFileType("Map file") { Patterns = new[] { "*.bin", "*.*" } };
            string path = await DialogHelper.OpenFile(_owner, "Import map .bin", new[] { filter });
            if (path == null) return;
            try
            {
                _map = new MapFile(path, gameFamily, false);
                BuildPreview();
                RefreshBuildings();
                MarkDirty();
                OnPropertyChanged(nameof(Collisions));
                OnPropertyChanged(nameof(Types));
                MapLoaded?.Invoke(this, EventArgs.Empty);
                StatusText = "Imported map (unsaved).";
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowError($"Import failed:\n{ex.Message}", "Import Error");
            }
        }

        public async Task ExportAsync()
        {
            if (_map == null) return;
            var filter = new FilePickerFileType("Map file") { Patterns = new[] { "*.bin" } };
            string path = await DialogHelper.SaveFile(_owner, "Export map .bin", new[] { filter }, $"map_{_selectedMapIndex:D4}.bin");
            if (path == null) return;
            try
            {
                File.WriteAllBytes(path, _map.ToByteArray());
                StatusText = "Exported.";
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowError($"Export failed:\n{ex.Message}", "Export Error");
            }
        }
    }
}
