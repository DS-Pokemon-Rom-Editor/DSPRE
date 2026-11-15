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
        public NsbmdRenderModel Model3D { get; private set; }
        public float[] MarkerMesh { get; private set; }
        public int MarkerVertexCount { get; private set; }
        public event EventHandler MapLoaded;
        public event EventHandler MarkersChanged;
        public string MapInfo { get => _mapInfo; private set => Set(ref _mapInfo, value); }
        private string _mapInfo = "";

        private GameMatrix _matrix;
        private int _matrixId = -1;
        private byte _areaDataId;
        private const int MapTiles = 32;

        private int _headerId = -1;

        // The headbutt file number is the header number, which gives the matrix and area.
        private void ResolveMatrix()
        {
            _matrix = null; _matrixId = -1; _areaDataId = 0; _headerId = -1;
            try
            {
                MapHeader hdr = MapHeader.GetMapHeader((ushort)_selFile);
                if (hdr != null)
                {
                    _headerId = hdr.ID;
                    _matrixId = hdr.MatrixID;
                    _areaDataId = hdr.AreaDataID;
                    _matrix = new GameMatrix(hdr.MatrixID);
                }
            }
            catch (Exception ex) { AppLogger.Error("Headbutt matrix resolve failed (file " + _selFile + "): " + ex.Message); }
        }

        // Renders the header's cells and any cell with a tree, once per file.
        private void DisplayMap()
        {
            Model3D = null;
            try
            {
                if (_matrix == null) { MapInfo = "No map for this file."; MapLoaded?.Invoke(this, EventArgs.Empty); RefreshTreeMarkers(); return; }

                HashSet<(int x, int y)> include = HeaderCells();
                if (include.Count == 0 && _headerId == MapHeader.Everywhere)
                {
                    MapInfo = "Header 0 has no map.";
                    MapLoaded?.Invoke(this, EventArgs.Empty);
                    RefreshTreeMarkers();
                    return;
                }
                Model3D = MatrixSceneBuilder.Build(_matrix, _areaDataId, GameFamily, areaForMap: null, includeCells: include);
                IReadOnlyList<string> headers = HeaderLabels.Friendly();
                string place = _headerId < headers.Count ? headers[_headerId] : $"Header {_headerId}";
                MapInfo = Model3D != null ? place : place + ": no map to show.";
            }
            catch (Exception ex) { MapInfo = "Map render failed: " + ex.Message; AppLogger.Error("Headbutt map render failed: " + ex.Message); }
            MapLoaded?.Invoke(this, EventArgs.Empty);
            RefreshTreeMarkers();
        }

        // The header's cells (every filled cell if the matrix has no headers) plus cells with a tree.
        private HashSet<(int x, int y)> HeaderCells()
        {
            HashSet<(int x, int y)> set = new HashSet<(int x, int y)>();
            if (_matrix == null) return set;
            // The catch-all header has no place, only the cells its trees stand on.
            if (_headerId != MapHeader.Everywhere)
                for (int y = 0; y < _matrix.Height; y++)
                    for (int x = 0; x < _matrix.Width; x++)
                    {
                        if (_matrix.Maps[y, x] == GameMatrix.EMPTY) continue;
                        if (_matrix.HasHeadersSection && _matrix.Headers[y, x] != _headerId) continue;
                        set.Add((x, y));
                    }
            if (_file != null)
            {
                void NoteTrees(HeadbuttTreeGroup g)
                {
                    foreach (HeadbuttTree t in g.Trees)
                    {
                        if (t.IsUnused || t.MatrixX >= _matrix.Width || t.MatrixY >= _matrix.Height) continue;
                        if (_matrix.Maps[t.MatrixY, t.MatrixX] != GameMatrix.EMPTY) set.Add((t.MatrixX, t.MatrixY));
                    }
                }
                foreach (HeadbuttTreeGroup g in _file.NormalTreeGroups) NoteTrees(g);
                foreach (HeadbuttTreeGroup g in _file.SpecialTreeGroups) NoteTrees(g);
            }
            return set;
        }

        public void RefreshTreeMarkers()
        {
            MarkerMesh = null; MarkerVertexCount = 0;
            NsbmdRenderModel m = Model3D;
            // Only the tile being edited gets a square; the trees themselves are tinted below.
            if (m != null && m.CellStrideX != 0 && _selTree >= 0 && _selTree < SelectedGroupTrees.Count)
            {
                float tile = (m.CellStrideX / MapTiles + m.CellStrideZ / MapTiles) * 0.5f;
                HeadbuttTree t = SelectedGroupTrees[_selTree].Tree;
                if (!t.IsUnused && TreeRaw(m, t, out float rx, out float rz))
                {
                    List<float> v = new List<float>(48);
                    AddMarkerQuad(v, m, rx, m.SurfaceY(rx, rz) + tile * 0.06f, rz, 0.5f * tile, (1f, 1f, 1f));
                    MarkerMesh = v.ToArray();
                    MarkerVertexCount = v.Count / 8;
                }
            }
            BuildTreeHighlight(m);
            MarkersChanged?.Invoke(this, EventArgs.Empty);
            GizmoTargetChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
