using DSPRE.Avalonia.Gl;
using DSPRE.Models;
using DSPRE.ROMFiles;
using DSPRE.Resources;
using LibNDSFormats.NSBTX;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using static DSPRE.RomInfo;
using AvBitmap = global::Avalonia.Media.Imaging.Bitmap;
using LibNDSFormats.NSBMD;
using Avalonia.Platform;
using Avalonia.Media.Imaging;

namespace DSPRE.Avalonia.ViewModels.World
{
    public partial class MapTilesViewModel
    {
        // Ramps marked for the next Apply, each a run of squares with its own kind and fill.
        private readonly List<TileRamps.Run> _rampRuns = new List<TileRamps.Run>();
        // Every marked square, for the code that only needs to know a square is a ramp.
        private readonly HashSet<(int x, int z)> _ramps = new HashSet<(int, int)>();
        public int RampCount => _ramps.Count;

        public IReadOnlyList<string> RampKinds { get; } = new[] { "Slope", "Stairs" };
        private int _rampKindIndex;
        public int RampKindIndex { get => _rampKindIndex; set => Set(ref _rampKindIndex, Math.Max(0, value)); }

        // Ramp fills: ground picture, brush tile picture, brush tile fitted over, or a tileset picture.
        private const int FillGround = 0, FillBrushPicture = 1, FillBrushTile = 2, FirstPicture = 3;
        public ObservableCollection<string> RampFills { get; } = new ObservableCollection<string>
            { "Ground's own picture", "Selected tile's picture", "Selected tile fitted as the ramp" };
        private int _rampFillIndex;
        public int RampFillIndex { get => _rampFillIndex; set => Set(ref _rampFillIndex, Math.Max(0, value)); }

        private void FillRampFills()
        {
            while (RampFills.Count > FirstPicture) RampFills.RemoveAt(RampFills.Count - 1);
            if (_set != null)
                foreach (string p in _set.Tiles.SelectMany(t => t.Faces).Where(f => f.Look != null).Select(f => f.Picture)
                                               .Where(p => !string.IsNullOrEmpty(p)).Distinct().OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
                    RampFills.Add(p);
            if (_rampFillIndex >= RampFills.Count) RampFillIndex = FillGround;
        }

        private MapTileset.Tile RampTemplate()
            => _rampFillIndex == FillBrushTile && _set != null && Brush >= 0 && Brush < _set.Tiles.Count ? _set.Tiles[Brush] : null;

        private MapTileset.Face RampFill()
        {
            if (_set == null || _rampFillIndex == FillGround || _rampFillIndex == FillBrushTile) return null;
            if (_rampFillIndex == FillBrushPicture)
            {
                if (Brush < 0 || Brush >= _set.Tiles.Count) return null;
                MapTileset.Tile tile = _set.Tiles[Brush];
                return tile.Faces.FirstOrDefault(f => f.Look != null && f.Picture == tile.MainPicture) ?? tile.Faces.FirstOrDefault(f => f.Look != null);
            }
            string name = RampFills[_rampFillIndex];
            return _set.Tiles.SelectMany(t => t.Faces).FirstOrDefault(f => f.Look != null && f.Picture == name);
        }

        private List<(int x, int z)> SelectedSquares()
        {
            bool[,] mask = _painter.Selection;
            List<(int, int)> picked = new List<(int, int)>();
            if (mask == null) return picked;
            for (int z = 0; z < TileGrid.Across; z++)
                for (int x = 0; x < TileGrid.Across; x++)
                    if (mask[x, z]) picked.Add((x, z));
            return picked;
        }

        private void RampsChanged()
        {
            _rampRuns.RemoveAll(r => r.Squares.Count == 0);
            _ramps.Clear();
            foreach (TileRamps.Run run in _rampRuns) _ramps.UnionWith(run.Squares);
            Note = _rampRuns.Count == 0 ? "No ramps."
                 : $"{_rampRuns.Count} ramp{(_rampRuns.Count > 1 ? "s" : "")} ({_ramps.Count} squares), built on Apply.";
            Raise(nameof(RampCount));
            GridChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Marks the selection as one ramp run, replacing ramps already there.</summary>
        public void MarkRamp()
        {
            List<(int x, int z)> picked = SelectedSquares();
            if (picked.Count == 0) return;
            foreach (TileRamps.Run run in _rampRuns) run.Squares.ExceptWith(picked);
            MapTileset.Tile template = RampTemplate();
            MapTileset.Face fill = RampFill();
            if (_rampFillIndex == FillBrushTile && template == null) { Warning = "Select the slope tile to fit first."; return; }
            if (_rampFillIndex != FillGround && _rampFillIndex != FillBrushTile && fill == null) { Warning = "That picture can't be copied. Pick another fill."; return; }
            _rampRuns.Add(new TileRamps.Run
            {
                Squares = new HashSet<(int x, int z)>(picked),
                Kind = _rampKindIndex == 1 ? TileRamps.Kind.Stairs : TileRamps.Kind.Slope,
                Fill = fill,
                Template = template,
            });
            RampsChanged();
        }

        public void UnmarkRamp()
        {
            List<(int x, int z)> picked = SelectedSquares();
            foreach (TileRamps.Run run in _rampRuns) run.Squares.ExceptWith(picked);
            RampsChanged();
        }

        public IReadOnlyCollection<(int x, int z)> RampSquares => _ramps;

        // The layer each square was walked on when taken apart. Maps can hide surfaces under ground.
        private int[,] _walkedLayer;

        // How far above its tile's base each square was walked, kept while the square keeps that tile.
        private float[,] _walkedAbove;
        private int[,] _walkedTile;

        private float SurfaceOf(TileGrid grid, int x, int z, int l)
        {
            TileGrid.Square sq = grid.At(x, z, l);
            if (sq.Tile < 0 || sq.Tile >= _set.Tiles.Count) return sq.Lift;
            if (_walkedLayer != null && x >= 0 && z >= 0 && x < TileGrid.Across && z < TileGrid.Across
                && _walkedLayer[x, z] == l && _walkedTile[x, z] == sq.Tile)
                return sq.Lift + _walkedAbove[x, z];
            return sq.Lift + _set.Tiles[sq.Tile].SurfaceY;
        }

        private int WalkedLayer(TileGrid grid, int x, int z)
        {
            if (_walkedLayer == null || x < 0 || z < 0 || x >= TileGrid.Across || z >= TileGrid.Across) return -1;
            int l = _walkedLayer[x, z];
            return l >= 0 && grid.At(x, z, l).Tile >= 0 && grid.At(x, z, l).Tile < _set.Tiles.Count ? l : -1;
        }

        private void FindWalkedLayers(TileGrid grid)
        {
            _walkedLayer = null;
            _walkedAbove = null;
            _walkedTile = null;
            if (_map?.Bdhc == null || !BdhcFile.TryParse(_map.Bdhc, out BdhcFile terrain)) return;
            float toUnits = 16f / MapTileset.TileWidth;
            _walkedLayer = new int[TileGrid.Across, TileGrid.Across];
            _walkedAbove = new float[TileGrid.Across, TileGrid.Across];
            _walkedTile = new int[TileGrid.Across, TileGrid.Across];
            for (int z = 0; z < TileGrid.Across; z++)
                for (int x = 0; x < TileGrid.Across; x++)
                {
                    _walkedLayer[x, z] = -1;
                    _walkedTile[x, z] = -1;
                    if (!terrain.TryGetHeight((x + 0.5f) * 0.25f, (z + 0.5f) * 0.25f, 0f, out float walkY)) continue;
                    float best = float.MaxValue;
                    for (int l = 0; l < TileGrid.Layers; l++)
                    {
                        TileGrid.Square sq = grid.At(x, z, l);
                        if (sq.Tile < 0) continue;
                        MapTileset.Tile tile = _set.Tiles[sq.Tile];
                        float bottom = grid.BaseLift + sq.Lift, top = bottom + tile.Corners.DefaultIfEmpty().Max(c => c?.Y ?? 0f);
                        float walk = walkY * 64f / toUnits;
                        // The walked height must lie within the tile. Of those, the nearest ground wins.
                        float off = walk < bottom - 1e-3f || walk > top + 1e-3f ? float.MaxValue / 2 : Math.Abs(bottom + tile.SurfaceY - walk);
                        if (off < best)
                        {
                            best = off; _walkedLayer[x, z] = l; _walkedTile[x, z] = sq.Tile;
                            _walkedAbove[x, z] = walk - bottom;
                        }
                    }
                    if (best >= float.MaxValue / 2) { _walkedLayer[x, z] = -1; _walkedTile[x, z] = -1; }
                }
        }

        private int GroundLayer(TileGrid grid, int x, int z)
        {
            int walked = WalkedLayer(grid, x, z);
            if (walked >= 0) return walked;
            for (int l = 0; l < TileGrid.Layers; l++)
            {
                int t = grid.At(x, z, l).Tile;
                if (t >= 0 && t < _set.Tiles.Count) return l;
            }
            return -1;
        }

        private bool _alsoWalls = true;
        public bool AlsoWalls { get => _alsoWalls; set => Set(ref _alsoWalls, value); }

        private TileGrid _atLoad;

        private bool HeightChanged(int x, int z)
        {
            if (_atLoad == null) return true;
            int ground = Math.Max(0, GroundLayer(_painter.Grid, x, z));
            return _painter.Grid.At(x, z, ground).Lift != _atLoad.At(x, z, ground).Lift;
        }

        private bool _alsoMovement = true;
        public bool AlsoMovement { get => _alsoMovement; set => Set(ref _alsoMovement, value); }

        public bool SetHasMovement => _set != null && _set.Tiles.Any(t => t.CollisionDefaults.Count > 0);

        public bool Ready => _set != null && _set.Tiles.Count > 0;
        public bool CanUndo => _before.Count > 0;
        public bool CanUndoPaint => _painter.CanUndo;
        public bool CanRedoPaint => _painter.CanRedo;
        public bool HasCopy => _painter.HasCopy;

        public event EventHandler Changed;
        public event EventHandler GridChanged;
    }
}
