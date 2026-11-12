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
        public ObservableCollection<string> OverlayModes { get; } = new ObservableCollection<string> { "No overlay", "Collision", "Type" };
        private int _overlayModeIndex;
        public int OverlayModeIndex { get => _overlayModeIndex; set { if (Set(ref _overlayModeIndex, value)) { OnPropertyChanged(nameof(ShowOverlayHeight)); OnPropertyChanged(nameof(ShowOverlayOpacity)); OnPropertyChanged(nameof(MeshToggleEnabled)); RebuildOverlay(); } } }

        // Mesh recolours the real ground mesh. Plane is a flat grid raised by OverlayHeight.
        private bool _overlayAsMesh = true;
        public bool OverlayAsMesh { get => _overlayAsMesh; set { if (Set(ref _overlayAsMesh, value)) { OnPropertyChanged(nameof(ShowOverlayHeight)); RebuildOverlay(); } } }
        public bool ShowOverlayHeight => !_overlayAsMesh && _overlayModeIndex > 0;
        public bool ShowOverlayOpacity => _overlayModeIndex > 0;
        // Mesh needs an overlay and a single map, since the tint texture fits one map's grid.
        public bool MeshToggleEnabled => _overlayModeIndex > 0 && !IsStitchedView;

        private double _overlayHeight;
        public double OverlayHeight { get => _overlayHeight; set { if (Set(ref _overlayHeight, value)) RebuildOverlay(); } }

        public float[] OverlayMesh { get; private set; }
        public int OverlayVertexCount { get; private set; }
        public event EventHandler OverlayChanged;

        // The shader tints map textures by permission colour, so transparent texels stay clear.
        public bool TintOn { get; private set; }
        public float TintStrength => _overlayOpacity / 100f;

        private int _overlayOpacity = 90;
        public int OverlayOpacity
        {
            get => _overlayOpacity;
            set { if (Set(ref _overlayOpacity, Math.Clamp(value, 0, 100))) OverlayChanged?.Invoke(this, EventArgs.Empty); }
        }
        public float TintOx { get; private set; }
        public float TintOz { get; private set; }
        public float TintSx { get; private set; }
        public float TintSz { get; private set; }
        public byte[] TintRgba { get; private set; }

        // Flat top-down with no perspective, the way the old editor drew permissions.
        private bool _flat2D = SettingsManager.Settings?.MapEditorFlat2D ?? false;
        public bool Flat2D
        {
            get => _flat2D;
            set
            {
                if (!Set(ref _flat2D, value)) return;
                if (SettingsManager.Settings != null)
                {
                    SettingsManager.Settings.MapEditorFlat2D = value;
                    SettingsManager.Save();
                }
                ViewModeChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler ViewModeChanged;

        public void RebuildOverlay()
        {
            OverlayMesh = null; OverlayVertexCount = 0;
            TintOn = false;
            bool collision = _overlayModeIndex == 1;

            if (IsHeaderView && Model3D != null && _overlayModeIndex > 0)
            {
                // The tint texture fits one map, so header view draws a flat grid per cell above the scene.
                NsbmdRenderModel m = Model3D;
                float planeY = (m.HasMapBounds ? m.MapMaxY : m.RawMaxY) + (float)_overlayHeight * NsbmdGeometry.TileSize;
                List<float> v = new List<float>();
                foreach (HeaderMapCell cellData in _headerCells)
                {
                    if (!Model3D.TryCellPlacement(cellData.CellX, cellData.CellY, out NsbmdRenderModel.CellPlacement cp)) continue;
                    byte[,] grid = collision ? cellData.Map.Collisions : cellData.Map.Types;
                    AppendPlaneOverlay(v, cp, grid, collision, m, planeY + cp.Width * 0.0006f);
                    AppendCellOutline(v, m, cp, planeY + cp.Width * 0.0012f);
                }
                OverlayMesh = v.ToArray();
                OverlayVertexCount = v.Count / 8;
                OverlayChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            // A single map is cell (0,0), always 32 tiles, so small maps keep full-size tiles.
            if (_map != null && Model3D != null && _overlayModeIndex > 0 && Model3D.TryCellPlacement(0, 0, out NsbmdRenderModel.CellPlacement cell))
            {
                byte[,] grid = collision ? _map.Collisions : _map.Types;
                int n = grid.GetLength(0);     // 32
                NsbmdRenderModel m = Model3D;
                float tsx = cell.Width / n, tsz = cell.Height / n;   // real tile size
                float ox = cell.OriginX, oz = cell.OriginZ;          // real tile-(0,0) corner

                if (_overlayAsMesh)
                {
                    byte[] rgba = new byte[32 * 32 * 4];
                    for (int row = 0; row < n && row < 32; row++)
                        for (int col = 0; col < n && col < 32; col++)
                        {
                            (float cr, float cg, float cb) = DSPRE.Avalonia.Gl.PermissionColors.Rgb(grid[row, col], collision);
                            int i = (row * 32 + col) * 4;
                            rgba[i] = (byte)(cr * 255f); rgba[i + 1] = (byte)(cg * 255f);
                            rgba[i + 2] = (byte)(cb * 255f); rgba[i + 3] = 255;
                        }
                    TintOx = (ox - m.Cx) * m.Scale; TintOz = (oz - m.Cz) * m.Scale;
                    TintSx = tsx * m.Scale;         TintSz = tsz * m.Scale;
                    TintRgba = rgba; TintOn = true;

                    List<float> outline = new List<float>(48);
                    AppendCellOutline(outline, m, cell,
                        (m.HasMapBounds ? m.MapMaxY : m.RawMaxY) + cell.Width * 0.0012f);
                    OverlayMesh = outline.ToArray();
                    OverlayVertexCount = outline.Count / 8;
                }
                else
                {
                    float eps = cell.Width * 0.0006f;
                    float planeY = (m.HasMapBounds ? m.MapMaxY : m.RawMaxY) + eps + (float)_overlayHeight * tsx;
                    List<float> v = new List<float>(n * n * 48);
                    for (int row = 0; row < n; row++)
                        for (int col = 0; col < n; col++)
                        {
                            (float cr, float cg, float cb) = DSPRE.Avalonia.Gl.PermissionColors.Rgb(grid[row, col], collision);
                            float x0 = ox + col * tsx, x1 = x0 + tsx, z0 = oz + row * tsz, z1 = z0 + tsz;
                            AddQuad(v, m.ToNormalized(x0, planeY, z0), m.ToNormalized(x1, planeY, z0),
                                       m.ToNormalized(x1, planeY, z1), m.ToNormalized(x0, planeY, z1), cr, cg, cb);
                        }
                    AppendCellOutline(v, m, cell, planeY + eps);
                    OverlayMesh = v.ToArray();
                    OverlayVertexCount = v.Count / 8;
                }
            }
            OverlayChanged?.Invoke(this, EventArgs.Empty);
        }

        private static void AppendPlaneOverlay(List<float> v, NsbmdRenderModel.CellPlacement cp, byte[,] grid,
            bool collision, NsbmdRenderModel m, float planeY)
        {
            int n = grid.GetLength(0);
            float tsx = cp.Width / n, tsz = cp.Height / n;
            float ox = cp.OriginX, oz = cp.OriginZ;
            for (int row = 0; row < n; row++)
                for (int col = 0; col < n; col++)
                {
                    (float cr, float cg, float cb) = DSPRE.Avalonia.Gl.PermissionColors.Rgb(grid[row, col], collision);
                    float x0 = ox + col * tsx, x1 = x0 + tsx, z0 = oz + row * tsz, z1 = z0 + tsz;
                    AddQuad(v, m.ToNormalized(x0, planeY, z0), m.ToNormalized(x1, planeY, z0),
                               m.ToNormalized(x1, planeY, z1), m.ToNormalized(x0, planeY, z1), cr, cg, cb);
                }
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private static void AppendCellOutline(List<float> v, NsbmdRenderModel m,
            NsbmdRenderModel.CellPlacement cell, float y)
        {
            float w = cell.Width * 0.012f;      // thin enough to read as a line at any zoom
            float x0 = cell.OriginX, x1 = x0 + cell.Width;
            float z0 = cell.OriginZ, z1 = z0 + cell.Height;
            const float r = 1f, g = 1f, b = 1f;

            void Bar(float ax, float az, float bx, float bz)
                => AddQuad(v, m.ToNormalized(ax, y, az), m.ToNormalized(bx, y, az),
                              m.ToNormalized(bx, y, bz), m.ToNormalized(ax, y, bz), r, g, b);

            Bar(x0, z0, x1, z0 + w);        // top
            Bar(x0, z1 - w, x1, z1);        // bottom
            Bar(x0, z0, x0 + w, z1);        // left
            Bar(x1 - w, z0, x1, z1);        // right
        }

        private static void AddQuad(List<float> v, (float x, float y, float z) a, (float x, float y, float z) b,
            (float x, float y, float z) c, (float x, float y, float z) d, float r, float g, float bl)
        {
            void Vtx((float x, float y, float z) p) { v.Add(p.x); v.Add(p.y); v.Add(p.z); v.Add(0); v.Add(0); v.Add(r); v.Add(g); v.Add(bl); }
            Vtx(a); Vtx(b); Vtx(c);
            Vtx(a); Vtx(c); Vtx(d);
        }

        // This header view lists every map's buildings, so these map a list index to its map and cell.
        private (MapFile map, int cellX, int cellY, Building building)? ResolveBuildingAt(int flatIndex)
        {
            if (IsHeaderView)
            {
                if (flatIndex < 0 || flatIndex >= _headerBuildingIndex.Count) return null;
                (int ci, int bi) = _headerBuildingIndex[flatIndex];
                if (ci < 0 || ci >= _headerCells.Count) return null;
                HeaderMapCell cell = _headerCells[ci];
                if (cell.Map?.Buildings == null || bi < 0 || bi >= cell.Map.Buildings.Count) return null;
                return (cell.Map, cell.CellX, cell.CellY, cell.Map.Buildings[bi]);
            }
            if (flatIndex < 0 || _map?.Buildings == null || flatIndex >= _map.Buildings.Count) return null;
            return (_map, 0, 0, _map.Buildings[flatIndex]);
        }

        private (MapFile map, int cellX, int cellY, Building building)? ResolveSelectedBuilding() => ResolveBuildingAt(_selectedBuildingIndex);
    }
}
