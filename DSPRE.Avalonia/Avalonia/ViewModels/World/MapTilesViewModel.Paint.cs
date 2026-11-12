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
        public IReadOnlyList<TileGridControl.Placed> PlacedTiles()
        {
            List<(int layer, int x, int z)> hidden = new List<(int layer, int x, int z)>();
            if (_set != null) TileBake.Laid(_painter.Grid, _set, hidden);
            HashSet<(int, int, int)> skip = new HashSet<(int, int, int)>(hidden);
            return _painter.Grid.Placed()
                .Where(p => !skip.Contains((p.square.Layer, p.x, p.z)))
                // Drawn from the lowest top to the highest, so a tree is never hidden under a flat decal.
                .OrderBy(p => p.square.Lift + (p.square.Tile >= 0 && p.square.Tile < _set.Tiles.Count && _set.Tiles[p.square.Tile].Corners.Count > 0
                                                ? _set.Tiles[p.square.Tile].Corners.Max(c => c.Y) : 0f))
                .ThenBy(p => p.square.Layer)
                .ThenByDescending(p => FromAbove(p.square.Tile, p.square.Turn) is TileGridControl.Look l ? l.Across * l.Down : 1)
                .Select(p => new TileGridControl.Placed(p.x, p.z - p.square.PastNorth, p.square.Tile, p.square.Turn,
                                                         p.square.FullWide, p.square.FullDeep, p.square.Layer))
                .ToList();
        }

        public bool[] VisibleLayers() => Layers.Select(l => l.Visible).ToArray();

        public float[,] ActiveHeights()
        {
            float[,] h = new float[TileGrid.Across, TileGrid.Across];
            for (int z = 0; z < TileGrid.Across; z++)
                for (int x = 0; x < TileGrid.Across; x++)
                {
                    // Only where this layer has a tile, at its walked top.
                    TileGrid.Square sq = _painter.Grid.At(x, z, _painter.Layer);
                    h[z, x] = sq.Tile < 0 ? float.NaN : SurfaceOf(_painter.Grid, x, z, _painter.Layer) / TileGrid.Step;
                }
            return h;
        }

        public IReadOnlyList<(int x, int z)> Pending
            => _painter.Pasting && _hover is (int hx, int hz) ? _painter.PasteCells(hx, hz) : _painter.Pending;

        public bool[,] Selection => _painter.Selection;
        private (int x, int z)? _hover;

        public bool HasSelection => _painter.Selection != null;
        public bool HasClipboard => _painter.HasClipboard;

        public void CopySelection() { _painter.CopySelection(); Raise(nameof(HasClipboard)); }
        public void CutSelection() { if (_painter.CutSelection()) Edited(); Raise(nameof(HasClipboard)); }
        public void DeleteSelection() { if (_painter.DeleteSelection()) Edited(); }
        public void FillSelection() { if (_painter.FillSelection()) Edited(); }
        public void RotateSelection() { if (_painter.TransformSelection(TilePainter.Transform.Rotate)) { Edited(); SelectionShown(); } }
        public void FlipSelectionAcross() { if (_painter.TransformSelection(TilePainter.Transform.FlipAcross)) { Edited(); SelectionShown(); } }
        public void FlipSelectionDown() { if (_painter.TransformSelection(TilePainter.Transform.FlipDown)) { Edited(); SelectionShown(); } }
        public void SelectAll() { _painter.SelectAll(); SelectionShown(); }
        public void SelectNone() { _painter.SelectNone(); _painter.CancelPaste(); SelectionShown(); }

        public void Paste()
        {
            if (!_painter.HasClipboard) return;
            _painter.StartPaste();
            Note = "Click to paste, right-click to cancel.";
            PendingChanged?.Invoke(this, EventArgs.Empty);
        }

        private void SelectionShown()
        {
            Raise(nameof(HasSelection));
            PendingChanged?.Invoke(this, EventArgs.Empty);
        }

        public (int x, int z, int across, int down)? Ghost { get; private set; }

        private void CountLayers()
        {
            for (int l = 0; l < TileGrid.Layers; l++) Layers[l].Painted = _painter.Grid.PaintedOn(l);
        }

        public void Press(int x, int z, TilePainter.Button button, bool shift = false, bool ctrl = false)
        {
            if (_set == null) return;
            int brush = Brush, turn = Turn, height = HeightBrush;
            bool changed = _painter.Press(x, z, button, shift, ctrl);
            Warning = _painter.Warning;
            if (Brush != brush || Turn != turn) { Raise(nameof(Brush)); Raise(nameof(Turn)); Describe(); ShowTile(); }
            if (HeightBrush != height) { Raise(nameof(HeightBrush)); Raise(nameof(HeightIndex)); }
            if (changed) GridChanged?.Invoke(this, EventArgs.Empty);
            Raise(nameof(HasSelection));
            PendingChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Drag(int x, int z)
        {
            bool changed = _painter.Drag(x, z);
            if (changed) GridChanged?.Invoke(this, EventArgs.Empty);
            PendingChanged?.Invoke(this, EventArgs.Empty);
            Over(x, z);
        }

        public void Release(int x, int z)
        {
            bool changed = _painter.Release(x, z);
            Warning = _painter.Warning ?? Warning;
            Raise(nameof(HasSelection));
            PendingChanged?.Invoke(this, EventArgs.Empty);
            if (changed) Edited();
        }

        private void Edited()
        {
            Dirty = true;
            CountLayers();
            CountUses();
            if (_set != null && (Warning == null || Warning.StartsWith("Missing textures", StringComparison.Ordinal)))
                Warning = MissingPictures(_set);
            Raise(nameof(CanUndoPaint));
            Raise(nameof(CanRedoPaint));
            Raise(nameof(HasCopy));
            GridChanged?.Invoke(this, EventArgs.Empty);
            Preview();
        }

        public void UndoPaint() { if (_painter.Undo()) Edited(); }
        public void RedoPaint() { if (_painter.Redo()) Edited(); }

        public void ShiftLayer(int dx, int dz) { if (_painter.ShiftLayer(dx, dz)) Edited(); }
        public void RaiseLayer(int steps) { if (_painter.RaiseLayer(steps)) Edited(); }
        public void ClearLayer() { if (_painter.ClearLayer()) Edited(); }
        public void CopyLayer() { _painter.CopyLayer(); Raise(nameof(HasCopy)); Note = $"Copied layer {ActiveLayer + 1}"; }
        public void PasteLayer() { if (_painter.PasteLayer()) Edited(); }

        public void Over(int? x, int? z)
        {
            (int, int, int, int)? ghost = (ValueTuple<int, int, int, int>?)null;
            _hover = x != null && z != null ? (x.Value, z.Value) : null;
            if (_painter.Pasting) PendingChanged?.Invoke(this, EventArgs.Empty);
            if (_set == null || x == null || z == null) { Under = ""; SetGhost(null); return; }

            if (!ShowHeights && _painter.Current == TilePainter.Tool.Paint && Brush >= 0 && Brush < _set.Tiles.Count)
            {
                MapTileset.Tile tile = _set.Tiles[Brush];
                (int across, int down) = TileGrid.Footprint(tile.Wide, tile.Deep, (byte)Turn);
                (int ax, int az) = _painter.AnchorFor(x.Value, z.Value, Brush);
                ghost = (ax, az, across, down);
            }
            SetGhost(ghost);

            // Everything on the square, layer by layer, so it is clear what an edit will touch.
            string Said(int layer)
            {
                TileGrid.Square q = _painter.Grid.At(x.Value, z.Value, layer);
                if (q.Tile < 0 || q.Tile >= _set.Tiles.Count) return null;
                MapTileset.Tile t = _set.Tiles[q.Tile];
                string what = string.IsNullOrEmpty(t.Name) ? t.Picture : t.Name;
                string size = t.Spreads && !what.Contains($"{t.Wide}x{t.Deep}") ? $" {t.Wide}x{t.Deep}" : "";
                return $"{layer + 1}: {what}{size}{(q.Turn > 0 ? $" {q.Turn * 90}°" : "")}"
                     + $" at {(int)Math.Round(SurfaceOf(_painter.Grid, x.Value, z.Value, layer) / TileGrid.Step)}";
            }
            List<string> here = Enumerable.Range(0, TileGrid.Layers).Select(Said).Where(t => t != null).ToList();
            string active = Said(ActiveLayer) ?? $"{ActiveLayer + 1}: empty";
            List<string> others = here.Where(t => !t.StartsWith($"{ActiveLayer + 1}:")).ToList();
            Under = $"{x},{z}  ·  layer {active}" + (others.Count > 0 ? $"  ·  also {string.Join(", ", others)}" : "")
                  + (here.Count == 0 ? "  ·  nothing here, a hole" : "");
        }

        private void SetGhost((int, int, int, int)? ghost)
        {
            if (Ghost == ghost) return;
            Ghost = ghost;
            PendingChanged?.Invoke(this, EventArgs.Empty);
        }

        private MapTileset.Tile Chosen => _set != null && Brush >= 0 && Brush < _set.Tiles.Count ? _set.Tiles[Brush] : null;

        private int _animationStep = 8;
        public int AnimationStep { get => _animationStep; set => Set(ref _animationStep, Math.Clamp(value, 1, 255)); }

        public bool CanAnimate => FieldTextureAnimations.Available && !string.IsNullOrEmpty(_lookPicture);
        public bool IsAnimated => CanAnimate && MatrixSceneBuilder.FieldAnimations()?.For(_lookPicture) != null;

        public string AnimationNote
        {
            get
            {
                if (!CanAnimate) return "";
                FieldTextureAnimations.Entry entry = MatrixSceneBuilder.FieldAnimations()?.For(_lookPicture);
                return entry == null ? $"{_lookPicture} isn't animated."
                    : $"{_lookPicture}: {entry.Frames.Count} frames, {string.Join(" ", entry.Frames.Select(f => f.Duration))} steps.";
            }
        }
    }
}
