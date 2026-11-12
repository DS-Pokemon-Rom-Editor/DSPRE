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
        /// <summary>Imports a canvas- or sheet-sized PNG. Null on success, else the error.</summary>
        public string ImportPng(string filePath)
        {
            if (_tile == null) return "No sprite loaded.";
            try
            {
                DSPRE.RawImage import;
                import = ImageConverter.DecodeRawImageFile(filePath);
                if (import == null) return "Image couldn't be decoded.";

                return BankCount > 0 ? ImportPngComposited(import) : ImportPngFlat(import);
            }
            catch (Exception ex) when (AppLogger.Noted("TrainerSpriteEditorViewModel.ImportPng", ex))
            {
                return ex.Message;
            }
        }

        private string ImportPngComposited(DSPRE.RawImage import)
        {
            if (import.Width != CanvasSize || import.Height != CanvasSize)
                return $"The PNG is {import.Width}×{import.Height} but the canvas is {CanvasSize}×{CanvasSize}. Export first to get a picture of the right size.";

            Dictionary<int, Dictionary<int, int>> lookups = new Dictionary<int, Dictionary<int, int>>();
            Dictionary<int, int> LookupFor(int bank)
            {
                if (lookups.TryGetValue(bank, out Dictionary<int, int> d)) return d;
                d = new Dictionary<int, int>();
                System.Drawing.Color[] pal = _pal[bank];
                for (int i = 0; i < pal.Length; i++)
                {
                    int key = (pal[i].R << 16) | (pal[i].G << 8) | pal[i].B;
                    if (!d.ContainsKey(key)) d[key] = i;
                }
                lookups[bank] = d;
                return d;
            }

            Dictionary<EditCell, int[]> perCell = new Dictionary<EditCell, int[]>();
            for (int y = 0; y < CanvasSize; y++)
            {
                for (int x = 0; x < CanvasSize; x++)
                {
                    EditCell cell = HitTest(x, y);
                    if (cell == null) continue; // background area, no cell to write into, ignore

                    if (!perCell.TryGetValue(cell, out int[] idxArr))
                        idxArr = perCell[cell] = DecodeCell(cell);

                    int i = (y * CanvasSize + x) * 4;
                    int key = (import.Bgra[i + 2] << 16) | (import.Bgra[i + 1] << 8) | import.Bgra[i];
                    int idx = 0;
                    if (!IsTransparent(import, i) && !LookupFor(cell.PaletteBank).TryGetValue(key, out idx))
                        return $"Pixel ({x},{y}) isn't one of that area's {_pal[cell.PaletteBank].Length} palette colours (bank {cell.PaletteBank}). Match the colours exactly, or paint it with the pencil.";

                    CellLocal(cell, x, y, out int lx, out int ly);
                    idxArr[ly * cell.Width + lx] = idx;
                }
            }

            foreach (KeyValuePair<EditCell, int[]> kv in perCell) EncodeCell(kv.Key, kv.Value);
            RebuildCompositedCanvas();
            QueueFrameThumbnailRefresh();
            RebuildTopBar();
            HasUnsavedChanges = true;
            return null;
        }

        /// <summary>Paints the selected frame over the other set's same frame. Null on success.</summary>
        public string CopyFrameToOtherSet()
        {
            if (_parts.Count < 2 || _selectedFrameIndex < 0) return "There is no other set to copy to.";
            int local = _selectedFrameIndex, from = _activePart;
            int[] source = FrameIndices(local);
            bool[] covered = new bool[source.Length];
            Activate(1 - from);
            try
            {
                if (local >= BankCount) return $"The other set has no frame {local}.";
                foreach (EditCell c in CellsOf(local))
                {
                    int[] indices = new int[c.Width * c.Height];
                    for (int ly = 0; ly < c.Height; ly++)
                        for (int lx = 0; lx < c.Width; lx++)
                        {
                            int x = c.DstX + (c.FlipX ? c.Width - 1 - lx : lx);
                            int y = c.DstY + (c.FlipY ? c.Height - 1 - ly : ly);
                            if (x < 0 || x >= CanvasSize || y < 0 || y >= CanvasSize) continue;
                            indices[ly * c.Width + lx] = source[y * CanvasSize + x];
                            covered[y * CanvasSize + x] = true;
                        }
                    EncodeCell(c, indices);
                }
            }
            finally { Activate(from); }

            int left = 0;
            for (int i = 0; i < source.Length; i++) if (source[i] != 0 && !covered[i]) left++;
            QueueFrameThumbnailRefresh();
            HasUnsavedChanges = true;
            StatusText = left == 0
                ? $"Copied frame {local} to {_parts[1 - from].Label.ToLowerInvariant()}."
                : $"Copied frame {local} to {_parts[1 - from].Label.ToLowerInvariant()}; {left} pixels fall outside that pose and were left out.";
            return null;
        }

        // A see-through pixel from any editor is colour 0, whatever its RGB.
        private static bool IsTransparent(DSPRE.RawImage image, int i) => image.Bgra[i + 3] < 128;

        private string ImportPngFlat(DSPRE.RawImage import)
        {
            if (import.Width != _flatWidth || import.Height != _flatHeight)
                return $"The PNG is {import.Width}×{import.Height} but the sprite sheet is {_flatWidth}×{_flatHeight}.";

            System.Drawing.Color[] pal = _pal[0];
            Dictionary<int, int> lookup = new Dictionary<int, int>();
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
                    int idx = 0;
                    if (!IsTransparent(import, i) && !lookup.TryGetValue(key, out idx))
                        return $"Pixel ({x},{y}) isn't one of this sprite's {pal.Length} palette colours. " +
                               "Match the PNG to the palette exactly, or paint it with the pencil.";
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
                if (BankCount > 0)
                {
                    // The file keeps colour 0 as a real colour; only the editor shows it see-through.
                    raw = GetCompositedRawImage(_selectedFrameIndex, CanvasSize, CanvasSize, null, trans: false);
                    System.Drawing.Color bg = ColorAt(_pal[0], 0);
                    for (int y = 0; y < CanvasSize; y++)
                        for (int x = 0; x < CanvasSize; x++)
                            if (raw.Bgra[(y * CanvasSize + x) * 4 + 3] == 0) raw.SetPixel(x, y, bg.R, bg.G, bg.B, 255);
                }
                else
                {
                    if (_flatIndices == null) return false;
                    raw = new DSPRE.RawImage(_flatWidth, _flatHeight);
                    System.Drawing.Color[] pal = _pal[0];
                    for (int y = 0; y < _flatHeight; y++)
                        for (int x = 0; x < _flatWidth; x++)
                        {
                            System.Drawing.Color c = ColorAt(pal, _flatIndices[y * _flatWidth + x]);
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
    }
}
