using DSPRE.Avalonia;
using DSPRE.Avalonia.Gl;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using global::Avalonia.Controls;
using global::Avalonia.Platform.Storage;
using LibNDSFormats.NSBMD;
using LibNDSFormats.NSBTX;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static DSPRE.RomInfo;
using DSPRE.Avalonia.Data;
using System.Text;

namespace DSPRE.Avalonia.ViewModels.World
{
    public partial class EventEditorViewModel
    {
        private bool _editMode3D;
        public bool EditMode3D
        {
            get => _editMode3D;
            set { if (Set(ref _editMode3D, value)) { OnPropertyChanged(nameof(EditMode3D)); EditModeChanged?.Invoke(this, EventArgs.Empty); } }
        }
        public event EventHandler EditModeChanged;
        public event EventHandler GizmoTargetChanged;
        public float ModelScale => Model3D?.Scale ?? 1f;

        private (int x, int z)? _walkTile;

        public (int x, int z)? WalkTile
        {
            get => _walkTile;
            set
            {
                if (_walkTile == value) return;
                _walkTile = value;
                BuildWalkTint();
                WalkTileChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler WalkTileChanged;

        /// <summary>Grid and colours the ground is tinted with. Off when nothing is hovered.</summary>
        public bool WalkTintOn { get; private set; }
        public float WalkTintStrength => 0.55f;
        public float WalkTintOx { get; private set; }
        public float WalkTintOz { get; private set; }
        public float WalkTintSx { get; private set; }
        public float WalkTintSz { get; private set; }
        public byte[] WalkTintRgba { get; private set; }

        // A tile grid for the hovered tile's map, clear except that tile.
        private void BuildWalkTint()
        {
            WalkTintOn = false;
            NsbmdRenderModel m = Model3D;
            if (_walkTile == null || m == null || m.CellStrideX == 0) return;

            int n = MapFile.MapSize;
            int gcx = FloorDiv(_walkTile.Value.x, n), gcy = FloorDiv(_walkTile.Value.z, n);
            if (!m.TryCellPlacement(gcx, gcy, out NsbmdRenderModel.CellPlacement cp)) return;

            int tx = _walkTile.Value.x - gcx * n, ty = _walkTile.Value.z - gcy * n;
            if (tx < 0 || ty < 0 || tx >= 32 || ty >= 32) return;

            byte[] rgba = new byte[32 * 32 * 4];        // all clear to start, so nothing else is touched
            int i = (ty * 32 + tx) * 4;
            rgba[i] = 255; rgba[i + 1] = 214; rgba[i + 2] = 79; rgba[i + 3] = 255;

            float tsx = cp.Width / n, tsz = cp.Height / n;
            WalkTintOx = (cp.OriginX - m.Cx) * m.Scale;
            WalkTintOz = (cp.OriginZ - m.Cz) * m.Scale;
            WalkTintSx = tsx * m.Scale;
            WalkTintSz = tsz * m.Scale;
            WalkTintRgba = rgba;
            WalkTintOn = true;
        }

        public (int x, int z)? SelectedEventTile => _current == null
            ? ((int, int)?)null
            : (FieldInteraction.TileX(_current), FieldInteraction.TileZ(_current));

        public bool TrySelectedEventAnchorNorm(out float nx, out float ny, out float nz)
            => EventAnchorNorm(_current, out nx, out ny, out nz);

        private bool EventAnchorNorm(Event e, out float nx, out float ny, out float nz) => TileAnchorNorm(e, 0, 0, out nx, out ny, out nz);

        private bool TileAnchorNorm(Event e, int dx, int dz, out float nx, out float ny, out float nz)
        {
            nx = ny = nz = 0f;
            NsbmdRenderModel m = Model3D;
            if (m == null || e == null) return false;
            (float rx, float rz) = TileRaw(m, e, dx, dz);
            float ry = EventSurfaceY(m, rx, rz, e);
            (float a, float b, float c) = m.ToNormalized(rx, ry, rz);
            nx = a; ny = b; nz = c;
            return true;
        }

        /// <summary>Visible event anchors, normalized. Type numbers match the tabs.</summary>
        public IEnumerable<(int type, int index, float nx, float ny, float nz)> EventAnchorsNorm()
        {
            if (_file == null || Model3D == null) yield break;
            if (_showOw) for (int i = 0; i < _file.Overworlds.Count; i++) if (EventAnchorNorm(_file.Overworlds[i], out float x, out float y, out float z)) yield return (0, i, x, y, z);
            if (_showWarp) for (int i = 0; i < _file.Warps.Count; i++) if (EventAnchorNorm(_file.Warps[i], out float x, out float y, out float z)) yield return (1, i, x, y, z);
            if (_showTrig)
                for (int i = 0; i < _file.Triggers.Count; i++)
                    foreach ((int dx, int dz) in TriggerTiles(_file.Triggers[i]))
                        if (TileAnchorNorm(_file.Triggers[i], dx, dz, out float x, out float y, out float z)) yield return (2, i, x, y, z);
            if (_showSpawn) for (int i = 0; i < _file.Spawnables.Count; i++) if (EventAnchorNorm(_file.Spawnables[i], out float x, out float y, out float z)) yield return (3, i, x, y, z);
        }

        public void SelectEvent(int type, int index)
        {
            switch (type)
            {
                case 0: SelectedOverworldIndex = index; break;
                case 1: SelectedWarpIndex = index; break;
                case 2: SelectedTriggerIndex = index; break;
                case 3: SelectedSpawnableIndex = index; break;
            }
            SelectedTabIndex = type;
        }

        // Positions are whole tiles, so a drag collects the fraction and steps on whole tiles.
        private float _dragAccumX, _dragAccumY, _dragAccumZ;
        public void BeginGizmoDrag() { _dragAccumX = 0f; _dragAccumY = 0f; _dragAccumZ = 0f; }

        /// <summary>Moves the selected event on one axis (0 X, 1 Y, 2 Z). Y changes the height.</summary>
        public void NudgeSelectedEventRaw(int axis, float rawDelta)
        {
            NsbmdRenderModel m = Model3D;
            if (m == null || _current == null || rawDelta == 0f) return;
            if (axis == 1)
            {
                if (ZIsFixedPoint)
                {
                    long nz = _current.ZPosition + (long)Math.Round(rawDelta * 262144f);
                    _current.ZPosition = (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, nz));
                }
                else
                {
                    // Only overworlds store a fraction, so collect the drag until it makes a whole unit.
                    _dragAccumY += rawDelta * 4f;
                    int step = (int)_dragAccumY;
                    if (step == 0) return;
                    _dragAccumY -= step;
                    long nz = _current.ZPosition + step;
                    _current.ZPosition = _current is Trigger
                        ? (int)Math.Clamp(nz, ushort.MinValue, ushort.MaxValue)
                        : (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, nz));
                }
            }
            else if (m.TryCellPlacement(_current.XMatrixPosition, _current.YMatrixPosition, out NsbmdRenderModel.CellPlacement p))
            {
                if (axis == 0)
                {
                    float per = p.Width / MapTiles; if (per <= 0) return;
                    _dragAccumX += rawDelta / per;
                    int step = (int)_dragAccumX;
                    if (step != 0) { _dragAccumX -= step; StepEventTileAcrossMaps(ref step, isX: true); }
                }
                else
                {
                    float per = p.Height / MapTiles; if (per <= 0) return;
                    _dragAccumZ += rawDelta / per;
                    int step = (int)_dragAccumZ;
                    if (step != 0) { _dragAccumZ -= step; StepEventTileAcrossMaps(ref step, isX: false); }
                }
            }
            _suppress = true;
            XMap = _current.XMapPosition; YMap = _current.YMapPosition; ZPos = ToDisplayZ(_current.ZPosition);
            OnPropertyChanged(nameof(ZPosNote));
            XMatrix = _current.XMatrixPosition; YMatrix = _current.YMatrixPosition;
            _suppress = false;
            Dirty();
            RefreshMarkers();   // also raises GizmoTargetChanged
        }

        public void NudgeSelectedEventTiles(int dx, int dz)
        {
            if (_current == null) return;
            if (dx != 0) { int s = dx; StepEventTileAcrossMaps(ref s, isX: true); }
            if (dz != 0) { int s = dz; StepEventTileAcrossMaps(ref s, isX: false); }
            _suppress = true;
            XMap = _current.XMapPosition; YMap = _current.YMapPosition;
            XMatrix = _current.XMatrixPosition; YMatrix = _current.YMatrixPosition;
            _suppress = false;
            Dirty();
            RefreshMarkers();
        }

        public bool HasSelectedEvent => _current != null;

        private void StepEventTileAcrossMaps(ref int step, bool isX)
        {
            int tile = isX ? _current.XMapPosition : _current.YMapPosition;
            int mat = isX ? _current.XMatrixPosition : _current.YMatrixPosition;
            tile += step;
            while (tile < 0) { mat--; tile += MapTiles; }
            while (tile >= MapTiles) { mat++; tile -= MapTiles; }
            if (mat < 0) { mat = 0; tile = 0; }
            if (isX) { _current.XMapPosition = (short)tile; _current.XMatrixPosition = (ushort)mat; }
            else { _current.YMapPosition = (short)tile; _current.YMatrixPosition = (ushort)mat; }
        }

        private static void AddMarker(List<float> v, NsbmdRenderModel m, float cx, float cy, float cz,
            float halfX, float halfZ, (float r, float g, float b) col, (float u0, float v0, float u1, float v1) uv)
            => AddFlatQuad(v, m, cx - halfX, cz - halfZ, cx + halfX, cz + halfZ, cy, col, uv);

        private static int FloorDiv(int a, int b) => a >= 0 ? a / b : -(((-a) + b - 1) / b);

        // North is the picture's top row, so icons read upright from the default camera.
        private static void AddFlatQuad(List<float> v, NsbmdRenderModel m, float x0, float z0, float x1, float z1, float y,
            (float r, float g, float b) col, (float u0, float v0, float u1, float v1) uv)
        {
            (float x, float y, float z) a = m.ToNormalized(x0, y, z0);
            (float x, float y, float z) b = m.ToNormalized(x1, y, z0);
            (float x, float y, float z) c = m.ToNormalized(x1, y, z1);
            (float x, float y, float z) d = m.ToNormalized(x0, y, z1);
            void Vtx((float x, float y, float z) p, float tu, float tv) { v.Add(p.x); v.Add(p.y); v.Add(p.z); v.Add(tu); v.Add(tv); v.Add(col.r); v.Add(col.g); v.Add(col.b); }
            Vtx(a, uv.u0, uv.v0); Vtx(b, uv.u1, uv.v0); Vtx(c, uv.u1, uv.v1);
            Vtx(a, uv.u0, uv.v0); Vtx(c, uv.u1, uv.v1); Vtx(d, uv.u0, uv.v1);
        }
    }
}
