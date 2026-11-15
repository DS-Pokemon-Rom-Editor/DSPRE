using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia;
using DSPRE.Avalonia.Gl;
using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    public partial class HeadbuttEncounterViewModel
    {
        private int _selTree = -1;
        public int SelectedTreeIndex { get => _selTree; set { if (Set(ref _selTree, value)) RefreshTreeMarkers(); } }

        private bool _editMode3D;
        public bool EditMode3D { get => _editMode3D; set { if (Set(ref _editMode3D, value)) { OnPropertyChanged(nameof(EditMode3D)); EditModeChanged?.Invoke(this, EventArgs.Empty); } } }
        public event EventHandler EditModeChanged;
        public event EventHandler GizmoTargetChanged;
        public float ModelScale => Model3D?.Scale ?? 1f;

        public bool TrySelectedTreeAnchorNorm(out float nx, out float ny, out float nz)
        {
            nx = ny = nz = 0f;
            NsbmdRenderModel m = Model3D;
            if (m == null || _selTree < 0 || _selTree >= SelectedGroupTrees.Count) return false;
            HeadbuttTree t = SelectedGroupTrees[_selTree].Tree;
            if (t.IsUnused || !TreeRaw(m, t, out float rx, out float rz)) return false;
            (float a, float b, float c) = m.ToNormalized(rx, m.SurfaceY(rx, rz), rz);
            nx = a; ny = b; nz = c;
            return true;
        }

        public IEnumerable<(int index, float nx, float ny, float nz)> TreeAnchorsNorm()
        {
            NsbmdRenderModel m = Model3D;
            if (m == null) yield break;
            for (int i = 0; i < SelectedGroupTrees.Count; i++)
            {
                HeadbuttTree t = SelectedGroupTrees[i].Tree;
                if (t.IsUnused || !TreeRaw(m, t, out float rx, out float rz)) continue;
                (float a, float b, float c) = m.ToNormalized(rx, m.SurfaceY(rx, rz), rz);
                yield return (i, a, b, c);
            }
        }

        private float _dragAccumX, _dragAccumZ;
        public void BeginGizmoDrag() { _dragAccumX = 0f; _dragAccumZ = 0f; }
        public bool HasSelectedTree => _selTree >= 0 && _selTree < SelectedGroupTrees.Count;

        /// <summary>Moves the selected tree by tiles, into the neighbouring cell at a map edge.</summary>
        public void NudgeSelectedTreeTiles(int dx, int dz)
        {
            if (!HasSelectedTree) return;
            HeadbuttTree t = SelectedGroupTrees[_selTree].Tree;
            if (t.IsUnused) return;
            if (dx != 0)
            {
                int tile = t.MapX + dx, mat = t.MatrixX;
                while (tile < 0) { mat--; tile += MapTiles; }
                while (tile >= MapTiles) { mat++; tile -= MapTiles; }
                if (mat < 0) { mat = 0; tile = 0; }
                t.MapX = (ushort)tile; t.MatrixX = (ushort)mat;
            }
            if (dz != 0)
            {
                int tile = t.MapY + dz, mat = t.MatrixY;
                while (tile < 0) { mat--; tile += MapTiles; }
                while (tile >= MapTiles) { mat++; tile -= MapTiles; }
                if (mat < 0) { mat = 0; tile = 0; }
                t.MapY = (ushort)tile; t.MatrixY = (ushort)mat;
            }
            SelectedGroupTrees[_selTree].RaiseAll();
        }

        /// <summary>Moves the tree on X (0) or Z (2) by raw model units, in whole tiles.</summary>
        public void NudgeSelectedTreeRaw(int axis, float rawDelta)
        {
            NsbmdRenderModel m = Model3D;
            if (m == null || axis == 1 || rawDelta == 0f || _selTree < 0 || _selTree >= SelectedGroupTrees.Count) return;
            HeadbuttTreeRow row = SelectedGroupTrees[_selTree];
            HeadbuttTree t = row.Tree;
            if (t.IsUnused || !m.TryCellPlacement(t.MatrixX, t.MatrixY, out NsbmdRenderModel.CellPlacement p)) return;
            int step;
            if (axis == 0)
            {
                float per = p.Width / MapTiles; if (per <= 0) return;
                _dragAccumX += rawDelta / per; step = (int)_dragAccumX; if (step == 0) return; _dragAccumX -= step;
                int tile = t.MapX + step, mat = t.MatrixX;
                while (tile < 0) { mat--; tile += MapTiles; }
                while (tile >= MapTiles) { mat++; tile -= MapTiles; }
                if (mat < 0) { mat = 0; tile = 0; }
                t.MapX = (ushort)tile; t.MatrixX = (ushort)mat;
            }
            else
            {
                float per = p.Height / MapTiles; if (per <= 0) return;
                _dragAccumZ += rawDelta / per; step = (int)_dragAccumZ; if (step == 0) return; _dragAccumZ -= step;
                int tile = t.MapY + step, mat = t.MatrixY;
                while (tile < 0) { mat--; tile += MapTiles; }
                while (tile >= MapTiles) { mat++; tile -= MapTiles; }
                if (mat < 0) { mat = 0; tile = 0; }
                t.MapY = (ushort)tile; t.MatrixY = (ushort)mat;
            }
            row.RaiseAll();   // updates numeric fields + marks dirty + refreshes markers
        }
    }
}
