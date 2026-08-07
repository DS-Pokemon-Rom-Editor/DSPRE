using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using DSPRE.Editors;
using Ekona.Images;
using Images;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
{
    public enum SpriteEditTool { Pencil, Eyedropper }

    /// <summary>One swatch in the palette strip, a fixed existing palette color, not editable in v1.</summary>
    public class PaletteSwatchViewModel
    {
        public int Index { get; }
        public IBrush Brush { get; }
        public PaletteSwatchViewModel(int index, System.Drawing.Color color)
        {
            Index = index;
            Brush = new SolidColorBrush(global::Avalonia.Media.Color.FromRgb(color.R, color.G, color.B));
        }
    }

    /// <summary>One clickable frame thumbnail in the strip.</summary>
    public class FrameThumbnailViewModel
    {
        public int Index { get; }
        public Bitmap Image { get; }
        public FrameThumbnailViewModel(int index, Bitmap image) { Index = index; Image = image; }
    }

    /// <summary>
    /// Pixel-level editor for a trainer class's sprite.
    ///
    /// Plat/HGSS trainer classes have per-frame OAM cells (NCER) compositing pieces of a shared NCGR
    /// tile sheet into the character you actually see. The flat sheet itself is a jumbled tile atlas,
    /// not a coherent picture (tiles referenced by an OAM aren't laid out to visually resemble the
    /// final sprite). So editing happens directly on the composited "as it looks" preview: every paint
    /// stroke is hit-tested against the current frame's OAM cells (same geometry
    /// <see cref="Ekona.Images.Actions.Get_RawImage(Bank, uint, ImageBase, PaletteBase, int, int, bool, int, int, int[])"/>
    /// itself uses) to find which cell owns that pixel, and therefore which bytes of the shared tile
    /// sheet and which palette bank, then decodes/edits/re-encodes just that cell's tile bytes in
    /// place. Because cells from different frames can reference the same underlying tiles, an edit
    /// naturally propagates to every frame that reuses them; frames that don't share tiles can be
    /// edited independently by switching the frame selector.
    ///
    /// DP trainer classes have no NCER at all (no per-class animation), so there's no cell
    /// geometry to hit-test against, and editing falls back to the flat NCGR tile sheet directly
    /// (<see cref="_flatIndices"/> path), same as this editor's original v1 implementation.
    /// </summary>
    public class TrainerSpriteEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (EqualityComparer<T>.Default.Equals(f, v)) return false;
            f = v; OnPropertyChanged(n); return true;
        }

        // Composited-canvas fixed logical size (OAM offsets are relative to its center), generous
        // enough to fit any trainer-class sprite without clipping, matching the size convention
        // TrainerEditorViewModel already renders class sprites at (96) with a little headroom.
        private const int CanvasSize = 128;

        private PaletteBase _pal;
        private ImageBase _tile;
        private SpriteBase _sprite; // null on DP (no NCER), flat-sheet fallback mode
        private string _tilesPath;

        // ── Mode A: composited cell editing (Plat/HGSS) ─────────────────────────
        private sealed class EditCell
        {
            public int Width, Height;
            public int DstX, DstY;
            public bool FlipX, FlipY;
            public int PaletteBank;
            public int ByteStart, ByteLen;
        }
        private readonly List<EditCell> _cells = new();
        private int _selectedFrameIndex = -1;
        private int _activePaletteBank = -1;

        // ── Mode B: flat tile-sheet editing (DP fallback) ───────────────────────
        private int[] _flatIndices;
        private int _flatWidth, _flatHeight;

        public bool IsFlatSheetMode => _sprite == null;

        public int ZoomFactor { get; private set; } = 4;

        public int FrameCount => _sprite?.Banks.Length ?? 0;
        public int SelectedFrameIndex
        {
            get => _selectedFrameIndex;
            set { if (Set(ref _selectedFrameIndex, value)) LoadFrame(value); }
        }

        public ObservableCollection<FrameThumbnailViewModel> FrameThumbnails { get; } = new();
        public bool HasFrames => FrameThumbnails.Count > 0;

        public ObservableCollection<string> ClassNames { get; } = new();
        public int SelectedClassIndex
        {
            get => _trClassID;
            set { if (Set(ref _trClassID, value)) Load(value); }
        }

        public ObservableCollection<PaletteSwatchViewModel> PaletteSwatches { get; } = new();

        private int _selectedSwatchIndex;
        public int SelectedSwatchIndex { get => _selectedSwatchIndex; set => Set(ref _selectedSwatchIndex, value); }

        private SpriteEditTool _selectedTool = SpriteEditTool.Pencil;
        public SpriteEditTool SelectedTool { get => _selectedTool; set => Set(ref _selectedTool, value); }

        private Bitmap _canvasBitmap;
        public Bitmap CanvasBitmap { get => _canvasBitmap; private set => Set(ref _canvasBitmap, value); }

        private string _statusText = "";
        public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }

        private bool _dirty;
        public bool HasUnsavedChanges { get => _dirty; private set => Set(ref _dirty, value); }
        public string UnsavedChangesDescription => $"Trainer Class Sprite Editor (class {_trClassID})";
        public void SaveChanges() => Save();

        /// <summary>Edits are applied straight into the in-memory <see cref="_tile"/>/<see cref="_flatIndices"/>
        /// buffers as you paint (there's no separate undo buffer), so discarding just means throwing all of
        /// that away and re-reading the class fresh from disk.</summary>
        public void DiscardChanges() => Load(_trClassID);

        private int _trClassID;
        public bool Loaded => _tile != null;

        // ── Design-time constructor ────────────────────────────────────────────
        public TrainerSpriteEditorViewModel()
        {
            if (!Design.IsDesignMode) return;
            StatusText = "Design preview";
        }

        public TrainerSpriteEditorViewModel(int trClassID)
        {
            string[] names = GetTrainerClassNames();
            for (int i = 0; i < names.Length; i++) ClassNames.Add($"[{i:D3}] {names[i]}");
            Load(trClassID);
        }

        // ── Load ───────────────────────────────────────────────────────────────
        /// Returns null on success, error message on failure.
        public string Load(int trClassID)
        {
            _trClassID = trClassID;
            try
            {
                string dir = RomInfo.gameDirs[DirNames.trainerGraphics].unpackedDir;

                int paletteFileID = trClassID * 5 + 1;
                string paletteFilename = paletteFileID.ToString("D4");
                _pal = new NCLR(Path.Combine(dir, paletteFilename), paletteFileID, paletteFilename);

                int tilesFileID = trClassID * 5;
                string tilesFilename = tilesFileID.ToString("D4");
                _tilesPath = Path.Combine(dir, tilesFilename);
                _tile = new NCGR(_tilesPath, tilesFileID, tilesFilename);

                _sprite = null;
                if (RomInfo.gameFamily != GameFamilies.DP)
                {
                    int spriteFileID = trClassID * 5 + 2;
                    string spriteFilename = spriteFileID.ToString("D4");
                    _sprite = new NCER(Path.Combine(dir, spriteFilename), spriteFileID, spriteFilename);
                }

                if (_sprite != null && _sprite.Banks.Length > 0)
                {
                    ZoomFactor = 4;
                    BuildFrameThumbnails();
                    _activePaletteBank = -1;
                    // Force the property setter below to detect a change (and so actually rebuild
                    // cells/canvas/swatches) even on a reload where the frame index doesn't move,
                    // e.g. a discard while already on frame 0.
                    _selectedFrameIndex = -1;
                    SelectedFrameIndex = 0; // triggers LoadFrame -> cells + canvas + swatches
                    StatusText = $"Class {trClassID}: {FrameCount} frame(s), {_tile.BPP}bpp";
                }
                else
                {
                    // DP (or an NCER with no banks): flat tile-sheet fallback.
                    _sprite = null;
                    ZoomFactor = 12;
                    FrameThumbnails.Clear();
                    OnPropertyChanged(nameof(HasFrames));
                    LoadFlatSheet();
                    StatusText = $"Class {trClassID}: {_flatWidth}×{_flatHeight} tile sheet (no per-class animation on this game), {_tile.BPP}bpp";
                }

                OnPropertyChanged(nameof(IsFlatSheetMode));
                OnPropertyChanged(nameof(FrameCount));
                HasUnsavedChanges = false;
                return null;
            }
            catch (Exception ex)
            {
                _tile = null; _pal = null; _sprite = null;
                StatusText = "Load failed: " + ex.Message;
                AppLogger.Error("TrainerSpriteEditorViewModel.Load failed: " + ex.Message);
                return ex.Message;
            }
        }

        private void BuildFrameThumbnails()
        {
            FrameThumbnails.Clear();
            for (int i = 0; i < _sprite.Banks.Length; i++)
            {
                var raw = _sprite.Get_RawImage(_tile, _pal, i, 64, 64, trans: true, currOAM: -1, draw_index: null);
                var bmp = ImageConverter.ToAvaloniaBitmap(raw);
                if (bmp != null) FrameThumbnails.Add(new FrameThumbnailViewModel(i, bmp));
            }
            OnPropertyChanged(nameof(HasFrames));
        }

        // ── Mode A: per-frame cell geometry + composited canvas ────────────────
        private void LoadFrame(int frameIndex)
        {
            if (_sprite == null || frameIndex < 0 || frameIndex >= _sprite.Banks.Length) return;

            _cells.Clear();
            var bank = _sprite.Banks[frameIndex];
            int bpp = _tile.BPP;
            foreach (var oam in bank.oams)
            {
                if (oam.width == 0 || oam.height == 0) continue;

                uint tileOffset = oam.obj2.tileOffset;
                tileOffset <<= (byte)_sprite.BlockSize;
                int byteStart = (int)(tileOffset * 0x20) + (int)bank.data_offset;
                int byteLen = oam.width * oam.height * bpp / 8;
                if (byteStart < 0 || byteLen <= 0 || byteStart + byteLen > _tile.Tiles.Length)
                    continue; // malformed/out-of-range cell, skip rather than risk corrupting unrelated bytes

                int bank_ = oam.obj2.index_palette;
                if (bank_ >= _pal.Palette.Length) bank_ = 0; // matches Actions.Get_RawImage(Bank...)'s own clamp

                _cells.Add(new EditCell
                {
                    Width = oam.width,
                    Height = oam.height,
                    DstX = CanvasSize / 2 + (int)oam.obj1.xOffset,
                    DstY = CanvasSize / 2 + (int)oam.obj0.yOffset,
                    FlipX = oam.obj1.flipX == 1,
                    FlipY = oam.obj1.flipY == 1,
                    PaletteBank = bank_,
                    ByteStart = byteStart,
                    ByteLen = byteLen,
                });
            }

            RebuildCompositedCanvas();

            // Default the palette strip to the first cell's bank so it's never empty, even before
            // the user has hovered/clicked anywhere.
            int firstBank = _cells.Count > 0 ? _cells[0].PaletteBank : 0;
            if (firstBank != _activePaletteBank)
                BuildPaletteSwatches(firstBank);
        }

        private void RebuildCompositedCanvas()
        {
            if (_sprite == null || _tile == null || _pal == null) return;
            var raw = _sprite.Get_RawImage(_tile, _pal, _selectedFrameIndex, CanvasSize, CanvasSize, trans: true, currOAM: -1, draw_index: null);
            CanvasBitmap = ImageConverter.ToAvaloniaBitmap(ZoomRaw(raw, ZoomFactor));
        }

        private static DSPRE.RawImage ZoomRaw(DSPRE.RawImage src, int zoom)
        {
            if (zoom <= 1) return src;
            var dst = new DSPRE.RawImage(src.Width * zoom, src.Height * zoom);
            for (int y = 0; y < src.Height; y++)
            {
                for (int x = 0; x < src.Width; x++)
                {
                    int si = (y * src.Width + x) * 4;
                    byte b = src.Bgra[si], g = src.Bgra[si + 1], r = src.Bgra[si + 2], a = src.Bgra[si + 3];
                    for (int dy = 0; dy < zoom; dy++)
                    {
                        int drow = (y * zoom + dy) * dst.Width;
                        for (int dx = 0; dx < zoom; dx++)
                        {
                            int di = (drow + x * zoom + dx) * 4;
                            dst.Bgra[di] = b; dst.Bgra[di + 1] = g; dst.Bgra[di + 2] = r; dst.Bgra[di + 3] = a;
                        }
                    }
                }
            }
            return dst;
        }

        /// Finds which cell owns composited-canvas pixel (x,y). Topmost drawn (last in draw order)
        /// non-transparent hit wins, matching what's visually on top; falls back to any cell whose
        /// bounds contain the point (even if transparent there) so painting into empty regions works.
        private EditCell HitTest(int x, int y)
        {
            for (int i = _cells.Count - 1; i >= 0; i--)
            {
                var c = _cells[i];
                if (x < c.DstX || x >= c.DstX + c.Width || y < c.DstY || y >= c.DstY + c.Height) continue;
                CellLocal(c, x, y, out int lx, out int ly);
                if (ReadCellIndex(c, lx, ly) != 0) return c;
            }
            for (int i = _cells.Count - 1; i >= 0; i--)
            {
                var c = _cells[i];
                if (x >= c.DstX && x < c.DstX + c.Width && y >= c.DstY && y < c.DstY + c.Height) return c;
            }
            return null;
        }

        private static void CellLocal(EditCell c, int x, int y, out int lx, out int ly)
        {
            int rawX = x - c.DstX, rawY = y - c.DstY;
            lx = c.FlipX ? c.Width - 1 - rawX : rawX;
            ly = c.FlipY ? c.Height - 1 - rawY : rawY;
        }

        private int[] DecodeCell(EditCell c)
        {
            byte[] slice = new byte[c.ByteLen];
            Array.Copy(_tile.Tiles, c.ByteStart, slice, 0, c.ByteLen);
            byte[] raster = _tile.FormTile == TileForm.Horizontal
                ? Actions.LinealToHorizontal(slice, c.Width, c.Height, _tile.BPP, _tile.TileSize)
                : slice;
            return UnpackIndices(raster, c.Width, c.Height, _tile.BPP);
        }

        private void EncodeCell(EditCell c, int[] indices)
        {
            byte[] raster = PackIndices(indices, c.Width, c.Height, _tile.BPP);
            byte[] native = _tile.FormTile == TileForm.Horizontal
                ? Actions.HorizontalToLineal(raster, c.Width, c.Height, _tile.BPP, _tile.TileSize)
                : raster;
            Array.Copy(native, 0, _tile.Tiles, c.ByteStart, c.ByteLen);
        }

        private int ReadCellIndex(EditCell c, int lx, int ly)
        {
            if (lx < 0 || lx >= c.Width || ly < 0 || ly >= c.Height) return 0;
            return DecodeCell(c)[ly * c.Width + lx];
        }

        private void BuildPaletteSwatches(int bankIndex)
        {
            _activePaletteBank = bankIndex;
            PaletteSwatches.Clear();
            var pal = _pal.Palette[bankIndex];
            int keep = SelectedSwatchIndex;
            for (int i = 0; i < pal.Length; i++)
                PaletteSwatches.Add(new PaletteSwatchViewModel(i, pal[i]));
            SelectedSwatchIndex = keep >= 0 && keep < pal.Length ? keep : 0;
        }

        // ── Mode B: flat tile-sheet fallback (DP, no NCER) ─────────────────────
        private void LoadFlatSheet()
        {
            _flatWidth = _tile.Width;
            _flatHeight = _tile.Height;

            byte[] rasterBytes = _tile.FormTile == TileForm.Horizontal
                ? Actions.LinealToHorizontal(_tile.Tiles, _flatWidth, _flatHeight, _tile.BPP, _tile.TileSize)
                : _tile.Tiles;
            _flatIndices = UnpackIndices(rasterBytes, _flatWidth, _flatHeight, _tile.BPP);

            BuildPaletteSwatches(0);
            RebuildFlatCanvas();
        }

        private void RebuildFlatCanvas()
        {
            var raw = new DSPRE.RawImage(_flatWidth, _flatHeight);
            var pal = _pal.Palette[0];
            for (int y = 0; y < _flatHeight; y++)
                for (int x = 0; x < _flatWidth; x++)
                {
                    var c = ColorAt(pal, _flatIndices[y * _flatWidth + x]);
                    raw.SetPixel(x, y, c.R, c.G, c.B, 255);
                }
            CanvasBitmap = ImageConverter.ToAvaloniaBitmap(ZoomRaw(raw, ZoomFactor));
        }

        private static System.Drawing.Color ColorAt(System.Drawing.Color[] pal, int index) =>
            index >= 0 && index < pal.Length ? pal[index] : System.Drawing.Color.Black;

        // ── Pointer interaction (canvas coordinates, already un-zoomed by the view) ────────────────
        public void HandlePointer(int x, int y)
        {
            if (_sprite != null) HandlePointerComposited(x, y);
            else HandlePointerFlat(x, y);
        }

        private void HandlePointerComposited(int x, int y)
        {
            if (x < 0 || x >= CanvasSize || y < 0 || y >= CanvasSize) return;
            var cell = HitTest(x, y);
            if (cell == null) return;

            if (cell.PaletteBank != _activePaletteBank)
                BuildPaletteSwatches(cell.PaletteBank);

            CellLocal(cell, x, y, out int lx, out int ly);
            if (lx < 0 || lx >= cell.Width || ly < 0 || ly >= cell.Height) return;

            if (SelectedTool == SpriteEditTool.Eyedropper)
            {
                SelectedSwatchIndex = DecodeCell(cell)[ly * cell.Width + lx];
                SelectedTool = SpriteEditTool.Pencil;
                return;
            }

            var indices = DecodeCell(cell);
            int pos = ly * cell.Width + lx;
            if (indices[pos] == SelectedSwatchIndex) return;
            indices[pos] = SelectedSwatchIndex;
            EncodeCell(cell, indices);

            RebuildCompositedCanvas();
            HasUnsavedChanges = true;
        }

        private void HandlePointerFlat(int x, int y)
        {
            if (_flatIndices == null || x < 0 || x >= _flatWidth || y < 0 || y >= _flatHeight) return;

            if (SelectedTool == SpriteEditTool.Eyedropper)
            {
                SelectedSwatchIndex = _flatIndices[y * _flatWidth + x];
                SelectedTool = SpriteEditTool.Pencil;
                return;
            }

            int pos = y * _flatWidth + x;
            if (_flatIndices[pos] == SelectedSwatchIndex) return;
            _flatIndices[pos] = SelectedSwatchIndex;
            RebuildFlatCanvas();
            HasUnsavedChanges = true;
        }

        // ── Import / Export PNG ────────────────────────────────────────────────
        /// Returns null on success, error message on failure. In composited mode, the PNG must match
        /// the fixed canvas size (export first to get a correctly-sized/aligned template). Each pixel
        /// is re-hit-tested the same way a click would be, and validated against whichever cell (and
        /// therefore palette bank) owns it.
        public string ImportPng(string filePath)
        {
            if (_tile == null) return "No sprite loaded.";
            try
            {
                DSPRE.RawImage import;
                using (var fs = File.OpenRead(filePath))
                    import = ImageConverter.DecodeRawImage(fs);
                if (import == null) return "Image could not be decoded.";

                return _sprite != null ? ImportPngComposited(import) : ImportPngFlat(import);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private string ImportPngComposited(DSPRE.RawImage import)
        {
            if (import.Width != CanvasSize || import.Height != CanvasSize)
                return $"Size mismatch. This editor's canvas is {CanvasSize}×{CanvasSize} (fixed), PNG: {import.Width}×{import.Height}. Export first to get a correctly-sized template.";

            var lookups = new Dictionary<int, Dictionary<int, int>>();
            Dictionary<int, int> LookupFor(int bank)
            {
                if (lookups.TryGetValue(bank, out var d)) return d;
                d = new Dictionary<int, int>();
                var pal = _pal.Palette[bank];
                for (int i = 0; i < pal.Length; i++)
                {
                    int key = (pal[i].R << 16) | (pal[i].G << 8) | pal[i].B;
                    if (!d.ContainsKey(key)) d[key] = i;
                }
                lookups[bank] = d;
                return d;
            }

            var perCell = new Dictionary<EditCell, int[]>();
            for (int y = 0; y < CanvasSize; y++)
            {
                for (int x = 0; x < CanvasSize; x++)
                {
                    var cell = HitTest(x, y);
                    if (cell == null) continue; // background area, no cell to write into, ignore

                    if (!perCell.TryGetValue(cell, out int[] idxArr))
                        idxArr = perCell[cell] = DecodeCell(cell);

                    int i = (y * CanvasSize + x) * 4;
                    int key = (import.Bgra[i + 2] << 16) | (import.Bgra[i + 1] << 8) | import.Bgra[i];
                    if (!LookupFor(cell.PaletteBank).TryGetValue(key, out int idx))
                        return $"Pixel ({x},{y}) isn't one of that area's {_pal.Palette[cell.PaletteBank].Length} palette colors (bank {cell.PaletteBank}). Recolor to match exactly, or use the pencil tool instead.";

                    CellLocal(cell, x, y, out int lx, out int ly);
                    idxArr[ly * cell.Width + lx] = idx;
                }
            }

            foreach (var kv in perCell) EncodeCell(kv.Key, kv.Value);
            RebuildCompositedCanvas();
            HasUnsavedChanges = true;
            return null;
        }

        private string ImportPngFlat(DSPRE.RawImage import)
        {
            if (import.Width != _flatWidth || import.Height != _flatHeight)
                return $"Size mismatch. Sprite sheet: {_flatWidth}×{_flatHeight}, PNG: {import.Width}×{import.Height}";

            var pal = _pal.Palette[0];
            var lookup = new Dictionary<int, int>();
            for (int i = 0; i < pal.Length; i++)
            {
                int key = (pal[i].R << 16) | (pal[i].G << 8) | pal[i].B;
                if (!lookup.ContainsKey(key)) lookup[key] = i;
            }

            int[] newIndices = new int[_flatWidth * _flatHeight];
            for (int y = 0; y < _flatHeight; y++)
            {
                for (int x = 0; x < _flatWidth; x++)
                {
                    int i = (y * _flatWidth + x) * 4;
                    int key = (import.Bgra[i + 2] << 16) | (import.Bgra[i + 1] << 8) | import.Bgra[i];
                    if (!lookup.TryGetValue(key, out int idx))
                        return $"Pixel ({x},{y}) isn't one of this sprite's {pal.Length} palette colors. " +
                               "Recolor the PNG to match the current palette exactly, or use the pencil tool instead.";
                    newIndices[y * _flatWidth + x] = idx;
                }
            }

            _flatIndices = newIndices;
            RebuildFlatCanvas();
            HasUnsavedChanges = true;
            return null;
        }

        public bool ExportPng(string filePath)
        {
            try
            {
                DSPRE.RawImage raw;
                if (_sprite != null)
                {
                    raw = _sprite.Get_RawImage(_tile, _pal, _selectedFrameIndex, CanvasSize, CanvasSize, trans: true, currOAM: -1, draw_index: null);
                }
                else
                {
                    if (_flatIndices == null) return false;
                    raw = new DSPRE.RawImage(_flatWidth, _flatHeight);
                    var pal = _pal.Palette[0];
                    for (int y = 0; y < _flatHeight; y++)
                        for (int x = 0; x < _flatWidth; x++)
                        {
                            var c = ColorAt(pal, _flatIndices[y * _flatWidth + x]);
                            raw.SetPixel(x, y, c.R, c.G, c.B, 255);
                        }
                }
                ImageConverter.ToAvaloniaBitmap(raw).Save(filePath, PngBitmapEncoderOptions.Default);
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error("TrainerSpriteEditorViewModel.ExportPng failed: " + ex.Message);
                return false;
            }
        }

        // ── Save (write back to the unpacked trainerGraphics NARC member) ──────
        /// Returns null on success, error message on failure.
        public string Save()
        {
            if (_tile == null) return "No sprite loaded.";
            try
            {
                if (_sprite == null)
                {
                    // Composited-mode edits are already written in place into _tile.Tiles by
                    // EncodeCell as they happen; the flat-sheet fallback packs on save instead.
                    int bpp = _tile.BPP;
                    byte[] rasterBytes = PackIndices(_flatIndices, _flatWidth, _flatHeight, bpp);
                    byte[] nativeBytes = _tile.FormTile == TileForm.Horizontal
                        ? Actions.HorizontalToLineal(rasterBytes, _flatWidth, _flatHeight, bpp, _tile.TileSize)
                        : rasterBytes;
                    _tile.Set_Tiles(nativeBytes);
                }

                _tile.Write(_tilesPath, _pal);

                if (_sprite != null) BuildFrameThumbnails();

                HasUnsavedChanges = false;
                StatusText = "Saved.";
                return null;
            }
            catch (Exception ex)
            {
                StatusText = "Save failed: " + ex.Message;
                AppLogger.Error("TrainerSpriteEditorViewModel.Save failed: " + ex.Message);
                return ex.Message;
            }
        }

        // ── Palette-index <-> packed byte helpers ──────────────────────────────
        // Mirror Ekona.Images.Actions.Get_Color's bit layout exactly (see Ekona/Images/Actions.cs and
        // Ekona/Helper/BitsConverter.cs) so packing is the true inverse of how the format is read.
        private static int[] UnpackIndices(byte[] data, int width, int height, int bpp)
        {
            int count = width * height;
            int[] indices = new int[count];
            switch (bpp)
            {
                case 4:
                    for (int i = 0; i < count && i / 2 < data.Length; i++)
                        indices[i] = Ekona.Helper.BitsConverter.ByteToBit4(data[i / 2])[i % 2];
                    break;
                case 8:
                    for (int i = 0; i < count && i < data.Length; i++)
                        indices[i] = data[i];
                    break;
                case 2:
                    for (int i = 0; i < count && i / 4 < data.Length; i++)
                        indices[i] = Ekona.Helper.BitsConverter.ByteToBit2(data[i / 4])[i % 4];
                    break;
                case 1:
                    for (int i = 0; i < count && i / 8 < data.Length; i++)
                        indices[i] = Ekona.Helper.BitsConverter.ByteToBits(data[i / 8])[i % 8];
                    break;
                default:
                    throw new NotSupportedException($"Unsupported color depth ({bpp} bpp) for sprite editing.");
            }
            return indices;
        }

        private static byte[] PackIndices(int[] indices, int width, int height, int bpp)
        {
            int count = width * height;
            switch (bpp)
            {
                case 4:
                {
                    byte[] result = new byte[(count + 1) / 2];
                    for (int i = 0; i < count; i += 2)
                    {
                        byte lo = (byte)(indices[i] & 0xF);
                        byte hi = (byte)((i + 1 < count ? indices[i + 1] : 0) & 0xF);
                        result[i / 2] = Ekona.Helper.BitsConverter.Bit4ToByte(lo, hi);
                    }
                    return result;
                }
                case 8:
                {
                    byte[] result = new byte[count];
                    for (int i = 0; i < count; i++) result[i] = (byte)(indices[i] & 0xFF);
                    return result;
                }
                case 2:
                {
                    byte[] result = new byte[(count + 3) / 4];
                    for (int i = 0; i < count; i += 4)
                    {
                        int b = 0;
                        for (int j = 0; j < 4; j++)
                        {
                            int idx = i + j < count ? indices[i + j] : 0;
                            b |= (idx & 0x3) << (j * 2);
                        }
                        result[i / 4] = (byte)b;
                    }
                    return result;
                }
                case 1:
                {
                    int padded = count % 8 == 0 ? count : count + (8 - count % 8);
                    byte[] bits = new byte[padded];
                    for (int i = 0; i < count; i++) bits[i] = (byte)(indices[i] & 0x1);
                    return Ekona.Helper.BitsConverter.BitsToBytes(bits);
                }
                default:
                    throw new NotSupportedException($"Unsupported color depth ({bpp} bpp) for sprite editing.");
            }
        }
    }
}
