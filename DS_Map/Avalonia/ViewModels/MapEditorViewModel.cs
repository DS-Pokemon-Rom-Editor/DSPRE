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

        // Full-matrix stitch layout: false = Continuous (geometry-sized), true = Grid (DS-true fixed 32-tile).
        // Grid is the default — it's the DS-accurate layout: every block is a fixed BLOCK_GRID_W(32)-tile = MapStride
        // span, so events/buildings map at exactly TileSize per tile and decorative overhang overlaps neighbours as on
        // hardware. (Events now anchor at the map's tile-(0,0)=raw-0 corner, so both modes align; Grid is exact.)
        private bool _stitchGrid = true;
        public bool StitchGrid { get => _stitchGrid; set { if (Set(ref _stitchGrid, value) && IsMatrixView && _selectedMatrix >= 0) BuildMatrixPreview(); } }
        private NsbmdGeometry.MatrixStitchMode StitchMode => _stitchGrid ? NsbmdGeometry.MatrixStitchMode.Grid : NsbmdGeometry.MatrixStitchMode.Continuous;

        private int _selectedMatrix = -1;
        public int SelectedMatrixIndex { get => _selectedMatrix; set { if (Set(ref _selectedMatrix, value) && !_suppress && value >= 0 && IsMatrixView) BuildMatrixPreview(); } }

        private string _matrixInfo = "";
        public string MatrixInfo { get => _matrixInfo; set => Set(ref _matrixInfo, value); }

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
        public byte CollisionPaintValue => _useRawCollision ? (byte)_rawCollision :
            (_collisionPainterIndex >= 0 && _collisionPainterIndex < CollisionPainters.Count ? CollisionPainters[_collisionPainterIndex].Value : (byte)0);

        private int _typePainterIndex;
        public int TypePainterIndex
        {
            get => _typePainterIndex;
            set { if (Set(ref _typePainterIndex, value)) OnPropertyChanged(nameof(TypePaintValue)); }
        }
        public byte TypePaintValue => _useRawType ? (byte)_rawType :
            (_typePainterIndex >= 0 && _typePainterIndex < TypePainters.Count ? TypePainters[_typePainterIndex].Value : (byte)0);

        // Paint a raw value (WinForms "Value" radio) instead of a named type from the combo.
        private bool _useRawCollision; private decimal _rawCollision;
        public bool UseRawCollision { get => _useRawCollision; set { if (Set(ref _useRawCollision, value)) OnPropertyChanged(nameof(CollisionPaintValue)); } }
        public decimal RawCollision { get => _rawCollision; set { if (Set(ref _rawCollision, value)) OnPropertyChanged(nameof(CollisionPaintValue)); } }
        private bool _useRawType; private decimal _rawType;
        public bool UseRawType { get => _useRawType; set { if (Set(ref _useRawType, value)) OnPropertyChanged(nameof(TypePaintValue)); } }
        public decimal RawType { get => _rawType; set { if (Set(ref _rawType, value)) OnPropertyChanged(nameof(TypePaintValue)); } }

        // 3D preview options.
        private bool _showTextures = true;
        public bool ShowTextures { get => _showTextures; set { if (Set(ref _showTextures, value) && _map != null) RefreshView(); } }
        private bool _interiorBuildings;
        public bool InteriorBuildings { get => _interiorBuildings; set { if (Set(ref _interiorBuildings, value) && _map != null) RefreshView(); } }

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        // ── 3D permission overlay ──────────────────────────────────────────────────────
        public ObservableCollection<string> OverlayModes { get; } = new ObservableCollection<string> { "No overlay", "Collision", "Type" };
        private int _overlayModeIndex;
        public int OverlayModeIndex { get => _overlayModeIndex; set { if (Set(ref _overlayModeIndex, value)) { OnPropertyChanged(nameof(ShowOverlayHeight)); RebuildOverlay(); } } }

        // Mesh mode (default): the overlay is a re-coloured copy of the real ground mesh, conforming to the
        // surface. Plane mode: a flat tile grid the user can raise with OverlayHeight (for top-down editing).
        private bool _overlayAsMesh = true;
        public bool OverlayAsMesh { get => _overlayAsMesh; set { if (Set(ref _overlayAsMesh, value)) { OnPropertyChanged(nameof(ShowOverlayHeight)); RebuildOverlay(); } } }
        public bool ShowOverlayHeight => !_overlayAsMesh && _overlayModeIndex > 0;

        // Height (in tiles) to lift the flat PLANE overlay off the surface. Ignored in mesh mode, which
        // always matches the surface height.
        private double _overlayHeight;
        public double OverlayHeight { get => _overlayHeight; set { if (Set(ref _overlayHeight, value)) RebuildOverlay(); } }

        public float[] OverlayMesh { get; private set; }
        public int OverlayVertexCount { get; private set; }
        public event EventHandler OverlayChanged;

        // Per-tile texture tint (mesh mode): the map textures themselves are shaded by the collision colour in the
        // shader, so trees/lamps get the colour on their real pixels and transparent texels stay clear.
        public bool TintOn { get; private set; }
        public float TintStrength => 0.5f;
        public float TintOx { get; private set; }
        public float TintOz { get; private set; }
        public float TintSx { get; private set; }
        public float TintSz { get; private set; }
        public byte[] TintRgba { get; private set; }

        public void RebuildOverlay()
        {
            OverlayMesh = null; OverlayVertexCount = 0;
            TintOn = false;
            // The single map is built as a 1×1 cell, so it carries a real tile grid (CellPlacement 0,0): a FIXED
            // 32 tiles regardless of how much geometry the map has — that's what makes the tiles the right size on
            // smaller maps.
            if (_map != null && Model3D != null && _overlayModeIndex > 0 && Model3D.TryCellPlacement(0, 0, out var cell))
            {
                bool collision = _overlayModeIndex == 1;
                byte[,] grid = collision ? _map.collisions : _map.types;
                int n = grid.GetLength(0);     // 32
                var m = Model3D;
                float tsx = cell.Width / n, tsz = cell.Height / n;   // real tile size
                float ox = cell.OriginX, oz = cell.OriginZ;          // real tile-(0,0) corner

                if (_overlayAsMesh)
                {
                    // MESH: hand the shader a 32×32 collision-colour texture + the tile grid (in normalized space).
                    // It mixes the colour into each opaque map texel, so decorations tint on their own shape.
                    var rgba = new byte[32 * 32 * 4];
                    for (int row = 0; row < n && row < 32; row++)
                        for (int col = 0; col < n && col < 32; col++)
                        {
                            var (cr, cg, cb) = DSPRE.Avalonia.Gl.PermissionColors.Rgb(grid[row, col], collision);
                            int i = (row * 32 + col) * 4;
                            rgba[i] = (byte)(cr * 255f); rgba[i + 1] = (byte)(cg * 255f);
                            rgba[i + 2] = (byte)(cb * 255f); rgba[i + 3] = 255;
                        }
                    TintOx = (ox - m.Cx) * m.Scale; TintOz = (oz - m.Cz) * m.Scale;
                    TintSx = tsx * m.Scale;         TintSz = tsz * m.Scale;
                    TintRgba = rgba; TintOn = true;
                }
                else
                {
                    // PLANE: a flat 32×32 tile grid, raised off the surface by the Height slider (top-down editing).
                    float eps = cell.Width * 0.0006f;
                    float planeY = (m.HasMapBounds ? m.MapMaxY : m.RawMaxY) + eps + (float)_overlayHeight * tsx;
                    var v = new List<float>(n * n * 48);
                    for (int row = 0; row < n; row++)
                        for (int col = 0; col < n; col++)
                        {
                            var (cr, cg, cb) = DSPRE.Avalonia.Gl.PermissionColors.Rgb(grid[row, col], collision);
                            float x0 = ox + col * tsx, x1 = x0 + tsx, z0 = oz + row * tsz, z1 = z0 + tsz;
                            AddQuad(v, m.ToNormalized(x0, planeY, z0), m.ToNormalized(x1, planeY, z0),
                                       m.ToNormalized(x1, planeY, z1), m.ToNormalized(x0, planeY, z1), cr, cg, cb);
                        }
                    OverlayMesh = v.ToArray();
                    OverlayVertexCount = v.Count / 8;
                }
            }
            OverlayChanged?.Invoke(this, EventArgs.Empty);
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        // One flat-coloured quad (a→b→c→d) as two triangles.
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

        // The stored rotation values are always readable/writable, but the GAME only reads them once
        // the Building Rotation patch (Patch Toolbox) has been applied — gate the controls so it's not
        // implied that rotating a building here does anything in an unpatched ROM.
        private bool _buildingRotationEnabled = true;
        public bool BuildingRotationEnabled { get => _buildingRotationEnabled; private set => Set(ref _buildingRotationEnabled, value); }

        /// <summary>Re-check whether the Building Rotation patch is applied. Call after (re)opening the editor or applying the patch.</summary>
        public void RefreshBuildingRotationPatchState()
        {
            try
            {
                BuildingRotationEnabled = RomPatchState.flag_BuildingRotationPatchApplied || PatchToolboxLogic.CheckFilesBuildingRotationPatchApplied();
            }
            catch
            {
                BuildingRotationEnabled = false;
            }
        }

        private void LoadBuildingDetail()
        {
            OnPropertyChanged(nameof(HasBuildingSelected));
            if (!HasBuildingSelected) { GizmoTargetChanged?.Invoke(this, EventArgs.Empty); return; }
            var b = _map.buildings[_selectedBuildingIndex];
            _suppress = true;
            // Positions are shown as the FULL fractional tile coordinate (whole tile + fraction/65536), so
            // the input boxes can fine-tune sub-tile placement after a coarse snap-drag.
            BModelId = b.modelID; BX = Coord(b.xPosition, b.xFraction); BY = Coord(b.yPosition, b.yFraction); BZ = Coord(b.zPosition, b.zFraction);
            BRotX = (decimal)Math.Round(Building.U16ToDeg(b.xRotation)); BRotY = (decimal)Math.Round(Building.U16ToDeg(b.yRotation)); BRotZ = (decimal)Math.Round(Building.U16ToDeg(b.zRotation));
            _suppress = false;
            GizmoTargetChanged?.Invoke(this, EventArgs.Empty);
        }

        private static decimal Coord(short pos, ushort frac) => pos + (decimal)frac / 65536m;
        private static (short pos, ushort frac) SplitCoord(decimal v)
        {
            decimal fl = Math.Floor(v);
            int pos = Math.Max(short.MinValue, Math.Min(short.MaxValue, (int)fl));
            int frac = (int)Math.Round((double)((v - fl) * 65536m));
            if (frac >= 65536) { frac = 0; pos = Math.Min(short.MaxValue, pos + 1); }
            if (frac < 0) frac = 0;
            return ((short)pos, (ushort)frac);
        }

        private static ushort DegToU16(decimal deg) => (ushort)(((double)deg % 360) / 360.0 * 65536.0);

        private void ApplyBuilding(bool reloadModel = false)
        {
            if (!HasBuildingSelected) return;
            var b = _map.buildings[_selectedBuildingIndex];
            b.modelID = (uint)_bModelId;
            var (px, fx) = SplitCoord(_bx); var (py, fy) = SplitCoord(_by); var (pz, fz) = SplitCoord(_bz);
            b.xPosition = px; b.xFraction = fx; b.yPosition = py; b.yFraction = fy; b.zPosition = pz; b.zFraction = fz;
            b.xRotation = DegToU16(_bRotX); b.yRotation = DegToU16(_bRotY); b.zRotation = DegToU16(_bRotZ);
            if (reloadModel) b.NSBMDFile = null;   // force reload of the (new) model in BuildPreview
            MarkDirty();
            // NOTE: deliberately do not touch the Buildings list here — replacing the selected
            // item would drop the ListBox selection. The model ID lives in the detail panel.
            RebuildPreview();
            GizmoTargetChanged?.Invoke(this, EventArgs.Empty);
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

        // ── 3D edit mode (move buildings with the translate gizmo) ──────────────────────
        private bool _editMode3D;
        public bool EditMode3D
        {
            get => _editMode3D;
            set { if (Set(ref _editMode3D, value)) { OnPropertyChanged(nameof(EditMode3D)); EditModeChanged?.Invoke(this, EventArgs.Empty); } }
        }
        /// <summary>Raised when edit mode toggles or the selected building anchor moves, so the
        /// view can refresh the gizmo target.</summary>
        public event EventHandler EditModeChanged;
        public event EventHandler GizmoTargetChanged;

        // ── 3D paint mode (click/drag the map to paint collision/type onto tiles) ────────────
        private bool _paintMode;
        public bool PaintMode
        {
            get => _paintMode;
            set
            {
                if (!Set(ref _paintMode, value)) return;
                if (value)
                {
                    EditMode3D = false;                      // paint and move-building can't both own the click
                    if (_overlayModeIndex == 0) OverlayModeIndex = 1;   // show Collision so painting is visible
                }
                PaintModeChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        /// <summary>Raised when paint mode toggles, so the view can lock the camera to Top.</summary>
        public event EventHandler PaintModeChanged;
        /// <summary>Raised after a tile is painted, so the view can refresh the 2D permission grids.</summary>
        public event EventHandler PaintedTile;

        /// <summary>Finds the tile whose centre projects nearest to a screen point (for paint picking).
        /// <paramref name="project"/> maps a normalized-space point to (ok, screenX, screenY).</summary>
        public bool TryTileAtScreen(float px, float py, Func<float, float, float, (bool ok, float sx, float sy)> project, out int col, out int row)
        {
            col = row = -1;
            if (Model3D == null || !Model3D.TryCellPlacement(0, 0, out var cp)) return false;
            const int n = 32;
            float tsx = cp.Width / n, tsz = cp.Height / n;
            float best = float.MaxValue;
            for (int r = 0; r < n; r++)
                for (int c = 0; c < n; c++)
                {
                    float rx = cp.OriginX + (c + 0.5f) * tsx, rz = cp.OriginZ + (r + 0.5f) * tsz;
                    var (nx, ny, nz) = Model3D.ToNormalized(rx, Model3D.SurfaceY(rx, rz), rz);
                    var (ok, sx, sy) = project(nx, ny, nz);
                    if (!ok) continue;
                    float d = (sx - px) * (sx - px) + (sy - py) * (sy - py);
                    if (d < best) { best = d; col = c; row = r; }
                }
            return col >= 0;
        }

        /// <summary>Paints the current collision or type value (matching the visible overlay) onto one tile.</summary>
        public void PaintTile(int col, int row)
        {
            if (_map == null || _overlayModeIndex <= 0) return;
            if (col < 0 || col >= 32 || row < 0 || row >= 32) return;
            bool collision = _overlayModeIndex == 1;
            var grid = collision ? _map.collisions : _map.types;
            byte val = collision ? CollisionPaintValue : TypePaintValue;
            if (grid[row, col] == val) return;
            grid[row, col] = val;
            MarkDirty();
            RebuildOverlay();
            PaintedTile?.Invoke(this, EventArgs.Empty);
        }

        public int BuildingCount => _map?.buildings?.Count ?? 0;

        /// <summary>A building's anchor in normalized render space (raw world = 0.25 × position;
        /// ToNormalized then applies the scene's centre/scale).</summary>
        public bool TryBuildingAnchorNorm(int index, out float nx, out float ny, out float nz)
        {
            nx = ny = nz = 0f;
            if (Model3D == null || _map?.buildings == null || index < 0 || index >= _map.buildings.Count) return false;
            var b = _map.buildings[index];
            // The single map is placed as cell (0,0): its origin (and its buildings) sit at OriginX + MapStride/2 in
            // scene space. The gizmo anchor must include that same offset, or it lands NW of the actual building.
            float offX = 0f, offZ = 0f;
            if (Model3D.TryCellPlacement(0, 0, out var cp))
            {
                offX = cp.OriginX + NsbmdGeometry.MapStride * 0.5f;
                offZ = cp.OriginZ + NsbmdGeometry.MapStride * 0.5f;
            }
            float rx = 0.25f * (b.xPosition + b.xFraction / 65536f) + offX;
            float ry = 0.25f * (b.yPosition + b.yFraction / 65536f);
            float rz = 0.25f * (b.zPosition + b.zFraction / 65536f) + offZ;
            var (a, c, d) = Model3D.ToNormalized(rx, ry, rz);
            nx = a; ny = c; nz = d;
            return true;
        }

        public bool TrySelectedBuildingAnchorNorm(out float nx, out float ny, out float nz)
            => TryBuildingAnchorNorm(_selectedBuildingIndex, out nx, out ny, out nz);

        /// <summary>Normalized→raw scale of the current scene, so the view can convert a gizmo
        /// drag (normalized units) back into raw map units.</summary>
        public float ModelScale => Model3D?.Scale ?? 1f;

        /// <summary>Moves the selected building by a raw-space delta along one world axis
        /// (0=X,1=Y,2=Z), with sub-tile (fraction) precision, and refreshes the live preview.</summary>
        public void NudgeSelectedBuildingRaw(int axis, float rawDelta)
        {
            if (!HasBuildingSelected || rawDelta == 0f) return;
            var b = _map.buildings[_selectedBuildingIndex];
            double tileDelta = rawDelta / 0.25;   // 1 position unit = 0.25 raw units
            switch (axis)
            {
                case 0: { var (p, f) = AddTiles(b.xPosition, b.xFraction, tileDelta); b.xPosition = p; b.xFraction = f; } break;
                case 1: { var (p, f) = AddTiles(b.yPosition, b.yFraction, tileDelta); b.yPosition = p; b.yFraction = f; } break;
                case 2: { var (p, f) = AddTiles(b.zPosition, b.zFraction, tileDelta); b.zPosition = p; b.zFraction = f; } break;
            }
            if (_snapToTile) SnapAxis(b, axis);
            AfterBuildingMoved(b);
        }

        // ── Snap-to-tile + arrow-key nudging ──────────────────────────────────────────────
        private bool _snapToTile;
        public bool SnapToTile { get => _snapToTile; set => Set(ref _snapToTile, value); }

        /// <summary>Moves the selected building by whole tiles along world X / Z (for arrow keys). Always
        /// tile-aligned: clears the sub-tile fraction so it snaps onto the grid.</summary>
        public void NudgeSelectedBuildingTiles(int dx, int dz)
        {
            if (!HasBuildingSelected) return;
            var b = _map.buildings[_selectedBuildingIndex];
            if (dx != 0) { b.xPosition = (short)(b.xPosition + dx); b.xFraction = 0; }
            if (dz != 0) { b.zPosition = (short)(b.zPosition + dz); b.zFraction = 0; }
            AfterBuildingMoved(b);
        }

        private static void SnapAxis(Building b, int axis)
        {
            switch (axis)
            {
                case 0: b.xPosition = (short)Math.Round(b.xPosition + b.xFraction / 65536.0); b.xFraction = 0; break;
                case 1: b.yPosition = (short)Math.Round(b.yPosition + b.yFraction / 65536.0); b.yFraction = 0; break;
                case 2: b.zPosition = (short)Math.Round(b.zPosition + b.zFraction / 65536.0); b.zFraction = 0; break;
            }
        }
         
        private void AfterBuildingMoved(Building b)
        {
            _suppress = true;
            BX = Coord(b.xPosition, b.xFraction); BY = Coord(b.yPosition, b.yFraction); BZ = Coord(b.zPosition, b.zFraction);
            _suppress = false;
            MarkDirty();
            RebuildPreview();
            GizmoTargetChanged?.Invoke(this, EventArgs.Empty);
        }

        private static (short pos, ushort frac) AddTiles(short pos, ushort frac, double tileDelta)
        {
            double cur = pos + frac / 65536.0 + tileDelta;
            double fl = Math.Floor(cur);
            int ip = (int)fl;
            int f = (int)Math.Round((cur - fl) * 65536.0);
            if (f >= 65536) { f -= 65536; ip++; }
            if (f < 0) { f += 65536; ip--; }
            ip = Math.Max(short.MinValue, Math.Min(short.MaxValue, ip));
            return ((short)ip, (ushort)f);
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
                    DirNames.matrices, DirNames.areaData, DirNames.dynamicHeaders, DirNames.synthOverlay });
                if (gameFamily == GameFamilies.HGSS)
                    DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.interiorBuildingModels });
                _mapToArea = BuildMapAreaLookup();
                RefreshBuildingRotationPatchState();

                foreach (var kv in PokeDatabase.System.MapCollisionPainters) CollisionPainters.Add(new PainterOption(kv.Key, kv.Value));
                foreach (var kv in PokeDatabase.System.MapCollisionTypePainters) TypePainters.Add(new PainterOption(kv.Key, kv.Value));
                if (CollisionPainters.Count > 1) CollisionPainterIndex = 1;
                if (TypePainters.Count > 0) { _typePainterIndex = 0; OnPropertyChanged(nameof(TypePainterIndex)); OnPropertyChanged(nameof(TypePaintValue)); }

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
                int used = 0;
                for (int y = 0; y < matrix.height; y++)
                    for (int x = 0; x < matrix.width; x++)
                        if (matrix.maps[y, x] != GameMatrix.EMPTY) used++;
                MatrixInfo = $"{matrix.width}×{matrix.height}, {used} map(s)";
                byte fallback = AreaForMap(matrix.maps[0, 0]) ?? 0;
                Model3D = MatrixSceneBuilder.Build(matrix, fallback, gameFamily, AreaForMap, mode: StitchMode);
                StatusText = Model3D != null
                    ? $"Matrix {_selectedMatrix}: {matrix.width}×{matrix.height}, {used} map(s) stitched."
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

        /// <summary>
        /// Builds map index → area-data id with the same accuracy as the full-matrix view:
        /// for matrices that carry a per-cell header section, each map gets the area of the
        /// header that actually occupies its cell; for plain matrices, the area comes from a
        /// header that references the matrix. Header-section results take precedence on conflict,
        /// so a single map shows the same tileset as it does in the stitched matrix view.
        /// </summary>
        private static Dictionary<int, byte> BuildMapAreaLookup()
        {
            var lookup = new Dictionary<int, byte>();
            try
            {
                int headerCount = GetHeaderCount();
                int matrixCount = Filesystem.GetMatrixCount();

                // matrix id → area, from the first header that references it (for plain matrices).
                var matrixArea = new Dictionary<int, byte>();
                for (ushort h = 0; h < headerCount; h++)
                {
                    try
                    {
                        var header = MapHeader.GetMapHeader(h);
                        if (header != null && !matrixArea.ContainsKey(header.matrixID))
                            matrixArea[header.matrixID] = header.areaDataID;
                    }
                    catch { /* skip bad header */ }
                }

                // Pass 1: plain matrices (one area for the whole matrix).
                // Pass 2: header-section matrices (per-cell area) — overwrites pass 1 on overlap.
                for (int pass = 0; pass < 2; pass++)
                    for (int mid = 0; mid < matrixCount; mid++)
                    {
                        try
                        {
                            var mtx = new GameMatrix(mid);
                            bool section = mtx.hasHeadersSection;
                            if (section != (pass == 1)) continue;
                            if (!section && !matrixArea.TryGetValue(mid, out byte plainArea)) continue;

                            for (int y = 0; y < mtx.height; y++)
                                for (int x = 0; x < mtx.width; x++)
                                {
                                    int map = mtx.maps[y, x];
                                    if (map == GameMatrix.EMPTY) continue;
                                    byte area;
                                    if (section)
                                    {
                                        try { var hh = MapHeader.GetMapHeader(mtx.headers[y, x]); if (hh == null) continue; area = hh.areaDataID; }
                                        catch { continue; }
                                    }
                                    else area = matrixArea[mid];
                                    lookup[map] = area;
                                }
                        }
                        catch { /* skip bad matrix */ }
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

                // Bind the map tileset textures (if a pack is selected and textures are on).
                if (_showTextures && _mapTilesetIndex >= 0 && _map.mapModel?.models != null && _map.mapModel.models.Length > 0)
                    BindNsbtx(_map.mapModel, Path.Combine(gameDirs[DirNames.mapTextures].unpackedDir, _mapTilesetIndex.ToString("D4")));

                // Load building models + (optionally) bind building tileset, then collect transforms.
                var buildings = new List<(NSBMDModel model, float[] transform)>();
                bool interior = _interiorBuildings && gameFamily == GameFamilies.HGSS && gameDirs.ContainsKey(DirNames.interiorBuildingModels);
                string bdir = gameDirs[interior ? DirNames.interiorBuildingModels : DirNames.exteriorBuildingModels].unpackedDir;
                byte[] bldTex = null;
                if (_showTextures && _buildingTilesetIndex > 0)
                {
                    string tp = Path.Combine(gameDirs[DirNames.buildingTextures].unpackedDir, (_buildingTilesetIndex - 1).ToString("D4"));
                    if (File.Exists(tp)) bldTex = File.ReadAllBytes(tp);
                }

                if (_map.buildings != null)
                    foreach (var b in _map.buildings)
                    {
                        if (b.NSBMDFile == null)
                        {
                            string mp = Path.Combine(bdir, b.modelID.ToString("D4"));
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

        // ── Map-file add / remove ────────────────────────────────────────────────────────
        public void AddMapFile()
        {
            try
            {
                int newId = MapNames.Count;
                new MapFile(0, gameFamily, discardMoveperms: true).SaveToFileDefaultDir(newId);
                MapNames.Add("Map " + newId);
                SelectedMapIndex = newId;
                StatusText = $"Added map file {newId}.";
            }
            catch (Exception ex) { _ = DialogHelper.ShowError($"Couldn't add map file:\n{ex.Message}", "Map Editor"); }
        }

        public async Task RemoveLastMapFileAsync()
        {
            if (MapNames.Count == 0) return;
            int last = MapNames.Count - 1;
            if (!await DialogHelper.AskYesNo($"Delete the last map file ({last})?", "Confirm deletion")) return;
            try
            {
                File.Delete(Path.Combine(gameDirs[DirNames.maps].unpackedDir, last.ToString("D4")));
                if (_selectedMapIndex == last) SelectedMapIndex = last - 1;
                MapNames.RemoveAt(last);
                StatusText = $"Removed map file {last}.";
            }
            catch (Exception ex) { _ = DialogHelper.ShowError($"Couldn't remove map file:\n{ex.Message}", "Map Editor"); }
        }

        private byte[] MapTextureData()
        {
            if (_mapTilesetIndex < 0) return null;
            string tp = Path.Combine(gameDirs[DirNames.mapTextures].unpackedDir, _mapTilesetIndex.ToString("D4"));
            return File.Exists(tp) ? File.ReadAllBytes(tp) : null;
        }
        private string ModelName() => $"map_{_selectedMapIndex:D4}";

        // ── 3D model export (NSBMD / DAE / GLB) ──────────────────────────────────────────
        public async Task ExportNsbmdAsync()
        {
            if (_map == null) return;
            var filter = new FilePickerFileType("NSBMD model") { Patterns = new[] { "*.nsbmd" } };
            string path = await DialogHelper.SaveFile(_owner, "Export map model (NSBMD)", new[] { filter }, ModelName() + ".nsbmd");
            if (path == null) return;
            try { File.WriteAllBytes(path, _map.mapModelData); StatusText = "Exported map model (NSBMD)."; }
            catch (Exception ex) { await DialogHelper.ShowError($"Export failed:\n{ex.Message}", "Export Error"); }
        }
        public void ExportDae() { if (_map != null) try { ModelUtils.ModelToDAE(ModelName(), _map.mapModelData, MapTextureData()); StatusText = "Exported DAE."; } catch (Exception ex) { AppLogger.Error("DAE export: " + ex.Message); } }
        public void ExportGlb() { if (_map != null) try { ModelUtils.ModelToGLB(ModelName(), _map.mapModelData, MapTextureData()); StatusText = "Exported GLB."; } catch (Exception ex) { AppLogger.Error("GLB export: " + ex.Message); } }

        // ── Terrain (BDHC) ──────────────────────────────────────────────────────────────
        public async Task ImportTerrainAsync()
        {
            if (_map == null) return;
            string path = await DialogHelper.OpenFile(_owner, "Import terrain (BDHC)", new[] { new FilePickerFileType("BDHC") { Patterns = new[] { "*.bdhc", "*.bin", "*.*" } } });
            if (path == null) return;
            try { _map.ImportTerrain(File.ReadAllBytes(path)); MarkDirty(); StatusText = $"Imported terrain ({_map.bdhc.Length} B)."; }
            catch (Exception ex) { await DialogHelper.ShowError($"Import failed:\n{ex.Message}", "Import Error"); }
        }
        public async Task ExportTerrainAsync()
        {
            if (_map == null) return;
            string path = await DialogHelper.SaveFile(_owner, "Export terrain (BDHC)", new[] { new FilePickerFileType("BDHC") { Patterns = new[] { "*.bdhc" } } }, ModelName() + ".bdhc");
            if (path == null) return;
            try { File.WriteAllBytes(path, _map.bdhc); StatusText = "Exported terrain."; }
            catch (Exception ex) { await DialogHelper.ShowError($"Export failed:\n{ex.Message}", "Export Error"); }
        }

        // ── Sound plates (BGS) ──────────────────────────────────────────────────────────
        public async Task ImportSoundAsync()
        {
            if (_map == null) return;
            string path = await DialogHelper.OpenFile(_owner, "Import sound plates (BGS)", new[] { new FilePickerFileType("BGS") { Patterns = new[] { "*.bgs", "*.bin", "*.*" } } });
            if (path == null) return;
            try { _map.ImportSoundPlates(File.ReadAllBytes(path)); MarkDirty(); StatusText = $"Imported sound plates ({_map.bgs.Length} B)."; }
            catch (Exception ex) { await DialogHelper.ShowError($"Import failed:\n{ex.Message}", "Import Error"); }
        }
        public async Task ExportSoundAsync()
        {
            if (_map == null) return;
            string path = await DialogHelper.SaveFile(_owner, "Export sound plates (BGS)", new[] { new FilePickerFileType("BGS") { Patterns = new[] { "*.bgs" } } }, ModelName() + ".bgs");
            if (path == null) return;
            try { File.WriteAllBytes(path, _map.bgs); StatusText = "Exported sound plates."; }
            catch (Exception ex) { await DialogHelper.ShowError($"Export failed:\n{ex.Message}", "Export Error"); }
        }
        public void BlankSound() { if (_map != null) { _map.bgs = MapFile.blankBGS; MarkDirty(); StatusText = "Blanked sound plates (remember to save)."; } }

        // ── Movement permissions ─────────────────────────────────────────────────────────
        public async Task ImportPermissionsAsync()
        {
            if (_map == null) return;
            string path = await DialogHelper.OpenFile(_owner, "Import permissions", new[] { new FilePickerFileType("Permissions") { Patterns = new[] { "*.mp", "*.bin", "*.*" } } });
            if (path == null) return;
            try
            {
                _map.ImportPermissions(File.ReadAllBytes(path));
                MarkDirty();
                OnPropertyChanged(nameof(Collisions)); OnPropertyChanged(nameof(Types));
                RebuildOverlay();
                MapLoaded?.Invoke(this, EventArgs.Empty);
                StatusText = "Imported permissions.";
            }
            catch (Exception ex) { await DialogHelper.ShowError($"Import failed:\n{ex.Message}", "Import Error"); }
        }
        public async Task ExportPermissionsAsync()
        {
            if (_map == null) return;
            string path = await DialogHelper.SaveFile(_owner, "Export permissions", new[] { new FilePickerFileType("Permissions") { Patterns = new[] { "*.mp" } } }, ModelName() + ".mp");
            if (path == null) return;
            try { File.WriteAllBytes(path, _map.CollisionsToByteArray()); StatusText = "Exported permissions."; }
            catch (Exception ex) { await DialogHelper.ShowError($"Export failed:\n{ex.Message}", "Export Error"); }
        }

        // ── Buildings I/O + duplicate ────────────────────────────────────────────────────
        public async Task ImportBuildingsAsync()
        {
            if (_map == null) return;
            string path = await DialogHelper.OpenFile(_owner, "Import buildings", new[] { new FilePickerFileType("Buildings") { Patterns = new[] { "*.bld", "*.bin", "*.*" } } });
            if (path == null) return;
            try
            {
                _map.ImportBuildings(File.ReadAllBytes(path));
                RefreshBuildings(); RebuildPreview(); MarkDirty();
                StatusText = "Imported buildings.";
            }
            catch (Exception ex) { await DialogHelper.ShowError($"Import failed:\n{ex.Message}", "Import Error"); }
        }
        public async Task ExportBuildingsAsync()
        {
            if (_map == null) return;
            string path = await DialogHelper.SaveFile(_owner, "Export buildings", new[] { new FilePickerFileType("Buildings") { Patterns = new[] { "*.bld" } } }, ModelName() + ".bld");
            if (path == null) return;
            try { File.WriteAllBytes(path, _map.BuildingsToByteArray()); StatusText = "Exported buildings."; }
            catch (Exception ex) { await DialogHelper.ShowError($"Export failed:\n{ex.Message}", "Export Error"); }
        }
        public void DuplicateBuilding()
        {
            if (!HasBuildingSelected) return;
            _map.buildings.Add(new Building(_map.buildings[_selectedBuildingIndex]));
            RefreshBuildings(); MarkDirty();
            SelectedBuildingIndex = _map.buildings.Count - 1;
            RebuildPreview();
        }

        /// <summary>Scans every map .bin and returns the set of collision/movement-permission types
        /// actually used, as a comma-separated hex report.</summary>
        public string ScanUsedTypes()
        {
            var used = new SortedSet<byte>();
            int count = Filesystem.GetMapCount();
            for (int i = 0; i < count; i++)
            {
                try { used.UnionWith(new MapFile(i, gameFamily, discardMoveperms: false).GetUsedTypes()); }
                catch { /* skip unreadable map */ }
            }
            var parts = new List<string>();
            foreach (var b in used) parts.Add("0x" + b.ToString("X2"));
            StatusText = $"{used.Count} distinct type(s) used across all maps.";
            return string.Join(", ", parts);
        }
    }
}
