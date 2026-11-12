using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;
using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Trainers
{
    public partial class TrainerSpriteEditorViewModel
    {
        private List<EditCell> CellsOf(int frameIndex)
        {
            List<EditCell> cells = new List<EditCell>();
            NcerFile.Cell bank = GetBank(frameIndex);
            int bpp = _tile.BitsPerPixel;
            // Laid in the order they are drawn, so a later cell is the one on top.
            foreach (int piece in CellRenderer.DrawOrder(bank))
            {
                NcerFile.Piece oam = bank.Pieces[piece];
                if (oam.Width == 0 || oam.Height == 0) continue;

                int byteStart = _sprite.CharacterOf(bank, oam) * 0x20;
                int byteLen = oam.Width * oam.Height * bpp / 8;
                if (byteStart < 0 || byteLen <= 0 || byteStart + byteLen > _tile.Tiles.Length)
                    continue; // malformed cell: skip so unrelated bytes aren't overwritten

                int bank_ = oam.Palette;
                if (bank_ >= _pal.Length) bank_ = 0; // same clamp as CellRenderer

                cells.Add(new EditCell
                {
                    Width = oam.Width,
                    Height = oam.Height,
                    DstX = CanvasSize / 2 + oam.X,
                    DstY = CanvasSize / 2 + oam.Y,
                    FlipX = oam.FlipH,
                    FlipY = oam.FlipV,
                    PaletteBank = bank_,
                    ByteStart = byteStart,
                    ByteLen = byteLen,
                });
            }

            return cells;
        }

        // A frame as palette indices on the canvas: cells in draw order, 0 left see-through.
        private int[] FrameIndices(int frameIndex)
        {
            int[] canvas = new int[CanvasSize * CanvasSize];
            foreach (EditCell c in CellsOf(frameIndex))
            {
                int[] idx = DecodeCell(c);
                for (int ly = 0; ly < c.Height; ly++)
                    for (int lx = 0; lx < c.Width; lx++)
                    {
                        int v = idx[ly * c.Width + lx];
                        if (v == 0) continue;
                        int x = c.DstX + (c.FlipX ? c.Width - 1 - lx : lx);
                        int y = c.DstY + (c.FlipY ? c.Height - 1 - ly : ly);
                        if (x >= 0 && x < CanvasSize && y >= 0 && y < CanvasSize) canvas[y * CanvasSize + x] = v;
                    }
            }
            return canvas;
        }

        private void LoadFrame(int frameIndex)
        {
            if (BankCount == 0 || frameIndex < 0 || frameIndex >= BankCount) return;

            _cells.Clear();
            _cells.AddRange(CellsOf(frameIndex));

            RebuildCompositedCanvas();

            // The palette strip starts on the first cell's bank, so it is never empty.
            int firstBank = _cells.Count > 0 ? _cells[0].PaletteBank : 0;
            if (firstBank != _activePaletteBank)
                BuildPaletteSwatches(firstBank);
        }

        private void RebuildCompositedCanvas()
        {
            if (BankCount == 0 || _tile == null || _pal == null) return;
            RawImage raw = GetCompositedRawImage(_selectedFrameIndex, CanvasSize, CanvasSize, null);
            CanvasBitmap = ImageConverter.ToAvaloniaBitmap(ZoomRaw(raw, ZoomFactor));
        }

        private static DSPRE.RawImage ZoomRaw(DSPRE.RawImage src, int zoom)
        {
            if (zoom <= 1) return src;
            RawImage dst = new DSPRE.RawImage(src.Width * zoom, src.Height * zoom);
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

        // Topmost opaque cell wins, else any cell around it, so blank areas take paint.
        private EditCell HitTest(int x, int y)
        {
            for (int i = _cells.Count - 1; i >= 0; i--)
            {
                EditCell c = _cells[i];
                if (x < c.DstX || x >= c.DstX + c.Width || y < c.DstY || y >= c.DstY + c.Height) continue;
                CellLocal(c, x, y, out int lx, out int ly);
                if (ReadCellIndex(c, lx, ly) != 0) return c;
            }
            for (int i = _cells.Count - 1; i >= 0; i--)
            {
                EditCell c = _cells[i];
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
            byte[] raster = _tile.Tiled ? TileOrder.ToRows(slice, c.Width, c.Height, _tile.BitsPerPixel, slice.Length) : slice;
            return UnpackIndices(raster, c.Width, c.Height, _tile.BitsPerPixel);
        }

        private void EncodeCell(EditCell c, int[] indices)
        {
            byte[] raster = PackIndices(indices, c.Width, c.Height, _tile.BitsPerPixel);
            byte[] native = _tile.Tiled ? TileOrder.ToTiles(raster, c.Width, c.Height, _tile.BitsPerPixel, raster.Length) : raster;
            Array.Copy(native, 0, _tile.Tiles, c.ByteStart, c.ByteLen);
        }

        private int ReadCellIndex(EditCell c, int lx, int ly)
        {
            if (lx < 0 || lx >= c.Width || ly < 0 || ly >= c.Height) return 0;
            return DecodeCell(c)[ly * c.Width + lx];
        }

        private static string Capital(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
        private static string Frames(int n) => n == 1 ? "1 frame" : $"{n} frames";

        private string _palPath;
        private int _palEntry;
        private bool _paletteDirty;

        /// <summary>The swatches' bank: last cell clicked, else the frame's first cell.</summary>
        public int ActivePaletteBank => Math.Max(0, _activePaletteBank);

        public string PaletteTitle(int bank, int index) => $"{Capital(_set.Noun)} {_trClassID}, palette {bank}, colour {index}";

        public uint SwatchColor(int bank, int index)
        {
            System.Drawing.Color[] pal = _pal != null && bank >= 0 && bank < _pal.Length ? _pal[bank] : null;
            if (pal == null || index < 0 || index >= pal.Length) return 0xFF000000u;
            return 0xFF000000u | ((uint)pal[index].R << 16) | ((uint)pal[index].G << 8) | pal[index].B;
        }

        /// <summary>Sets a colour, cut to 5 bits a channel. Same group, same undo step.</summary>
        public void SetSwatchColor(int bank, int index, uint argb, object group = null)
        {
            System.Drawing.Color[] pal = _pal != null && bank >= 0 && bank < _pal.Length ? _pal[bank] : null;
            if (pal == null || index < 0 || index >= pal.Length) return;
            int Channel(int shift) => (int)((argb >> shift) & 0xF8);
            System.Drawing.Color color = System.Drawing.Color.FromArgb(Channel(16), Channel(8), Channel(0));
            if (pal[index].ToArgb() == color.ToArgb()) return;
            pal[index] = color;
            foreach (SpritePart part in _parts)
                if (part.Pal != _pal && bank < part.Pal.Length && index < part.Pal[bank].Length)
                    part.Pal[bank][index] = color;
            _paletteDirty = true;
            _editGroup = group;
            HasUnsavedChanges = true;
            if (bank == ActivePaletteBank) BuildPaletteSwatches(bank);
            if (BankCount > 0) { RebuildCompositedCanvas(); BuildFrameThumbnails(); RebuildTopBar(); } else RebuildFlatCanvas();
        }

        private void BuildPaletteSwatches(int bankIndex)
        {
            _activePaletteBank = bankIndex;
            PaletteSwatches.Clear();
            System.Drawing.Color[] pal = _pal[bankIndex];
            int keep = SelectedSwatchIndex;
            for (int i = 0; i < pal.Length; i++)
                PaletteSwatches.Add(new PaletteSwatchViewModel(i, pal[i]));
            SelectedSwatchIndex = keep >= 0 && keep < pal.Length ? keep : 0;
        }

        private void LoadFlatSheet()
        {
            _flatWidth = _tile.Width;
            _flatHeight = _tile.Height;

            _scrambleSeed = null;
            if (TrainerGraphicsLayout.PixelsAreScrambled && _tile.Tiles != null)
            {
                byte[] pixels = (byte[])_tile.Tiles.Clone();
                _scrambleSeed = SpriteScrambling.Seed(pixels, 0, pixels.Length);
                SpriteScrambling.Unscramble(pixels, 0, pixels.Length);
                _tile.SetTiles(pixels);
            }

            byte[] rasterBytes = _tile.Tiled ? TileOrder.ToRows(_tile.Tiles, _flatWidth, _flatHeight, _tile.BitsPerPixel, _tile.Tiles.Length) : _tile.Tiles;
            _flatIndices = UnpackIndices(rasterBytes, _flatWidth, _flatHeight, _tile.BitsPerPixel);

            BuildPaletteSwatches(0);
            RebuildFlatCanvas();
        }

        private void RebuildFlatCanvas()
        {
            RawImage raw = new DSPRE.RawImage(_flatWidth, _flatHeight);
            System.Drawing.Color[] pal = _pal[0];
            for (int y = 0; y < _flatHeight; y++)
                for (int x = 0; x < _flatWidth; x++)
                {
                    // Colour 0 is the background: kept in the file, shown see-through like the cell sprites.
                    int index = _flatIndices[y * _flatWidth + x];
                    System.Drawing.Color c = ColorAt(pal, index);
                    raw.SetPixel(x, y, c.R, c.G, c.B, index == 0 ? (byte)0 : (byte)255);
                }
            CanvasBitmap = ImageConverter.ToAvaloniaBitmap(ZoomRaw(raw, ZoomFactor));
        }

        private static System.Drawing.Color ColorAt(System.Drawing.Color[] pal, int index) =>
            index >= 0 && index < pal.Length ? pal[index] : System.Drawing.Color.Black;

        // Canvas coordinates, already un-zoomed by the view.
        public void HandlePointer(int x, int y)
        {
            if (BankCount > 0) HandlePointerComposited(x, y);
            else HandlePointerFlat(x, y);
        }

        private void HandlePointerComposited(int x, int y)
        {
            if (x < 0 || x >= CanvasSize || y < 0 || y >= CanvasSize) return;
            EditCell cell = HitTest(x, y);
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

            int[] indices = DecodeCell(cell);
            int pos = ly * cell.Width + lx;
            if (indices[pos] == SelectedSwatchIndex) return;
            indices[pos] = SelectedSwatchIndex;
            EncodeCell(cell, indices);

            RebuildCompositedCanvas();
            QueueFrameThumbnailRefresh();
            RebuildTopBar();
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
    }
}
