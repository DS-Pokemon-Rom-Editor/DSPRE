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
        /// <summary>Raised after a tile is painted, so the 2D permission grids refresh.</summary>
        public event EventHandler PaintedTile;

        // Cell (0,0) for a single map, every map of the header in This header view.
        private IEnumerable<(int cellIndex, int cellX, int cellY)> PaintableCells()
        {
            if (IsHeaderView)
            {
                for (int i = 0; i < _headerCells.Count; i++)
                    yield return (i, _headerCells[i].CellX, _headerCells[i].CellY);
            }
            else if (_map != null) yield return (0, 0, 0);
        }

        // project maps a normalized point to (ok, screenX, screenY).
        public bool TryTileAtScreen(float px, float py, Func<float, float, float, (bool ok, float sx, float sy)> project,
            out int cellIndex, out int col, out int row)
        {
            cellIndex = -1; col = row = -1;
            if (Model3D == null) return false;
            const int n = 32;
            float best = float.MaxValue;
            foreach ((int ci, int cx, int cy) in PaintableCells())
            {
                if (!Model3D.TryCellPlacement(cx, cy, out NsbmdRenderModel.CellPlacement cp)) continue;
                float tsx = cp.Width / n, tsz = cp.Height / n;
                for (int r = 0; r < n; r++)
                    for (int c = 0; c < n; c++)
                    {
                        float rx = cp.OriginX + (c + 0.5f) * tsx, rz = cp.OriginZ + (r + 0.5f) * tsz;
                        (float nx, float ny, float nz) = Model3D.ToNormalized(rx, Model3D.SurfaceY(rx, rz), rz);
                        (bool ok, float sx, float sy) = project(nx, ny, nz);
                        if (!ok) continue;
                        float d = (sx - px) * (sx - px) + (sy - py) * (sy - py);
                        if (d < best) { best = d; cellIndex = ci; col = c; row = r; }
                    }
            }
            return col >= 0;
        }

        public void PaintTile(int cellIndex, int col, int row)
        {
            if (Model3D == null || _overlayModeIndex <= 0) return;
            if (col < 0 || col >= 32 || row < 0 || row >= 32) return;
            bool collision = _overlayModeIndex == 1;

            byte[,] grid;
            Action markDirty;
            if (IsHeaderView)
            {
                if (cellIndex < 0 || cellIndex >= _headerCells.Count) return;
                HeaderMapCell cell = _headerCells[cellIndex];
                SelectedHeaderMapIndex = cellIndex;
                grid = collision ? cell.Map.Collisions : cell.Map.Types;
                markDirty = () => { cell.Dirty = true; MarkDirty(); };
            }
            else
            {
                if (_map == null) return;
                grid = collision ? _map.Collisions : _map.Types;
                markDirty = MarkDirty;
            }

            byte val = collision ? CollisionPaintValue : TypePaintValue;
            if (grid[row, col] == val) return;
            grid[row, col] = val;
            markDirty();
            RebuildOverlay();
            PaintedTile?.Invoke(this, EventArgs.Empty);
        }

        public int BuildingCount => IsHeaderView ? _headerBuildingIndex.Count : (_map?.Buildings?.Count ?? 0);
    }
}
