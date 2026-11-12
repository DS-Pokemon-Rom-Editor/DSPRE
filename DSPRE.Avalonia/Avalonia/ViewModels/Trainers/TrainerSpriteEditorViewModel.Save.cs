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
        // hg-engine only: drawing and colours are read from and saved to this PNG.
        private string _sourcePngPath;
        /// <summary>Saves the sprite. Null on success, else the error.</summary>
        public string Save()
        {
            if (_tile == null) return "No sprite loaded.";
            try
            {
                if (BankCount == 0)
                {
                    // Composited edits are already in _tile.Tiles. The flat sheet packs on save.
                    int bpp = _tile.BitsPerPixel;
                    byte[] rasterBytes = PackIndices(_flatIndices, _flatWidth, _flatHeight, bpp);
                    byte[] nativeBytes = _tile.Tiled ? TileOrder.ToTiles(rasterBytes, _flatWidth, _flatHeight, bpp, rasterBytes.Length) : rasterBytes;
                    if (_scrambleSeed.HasValue)
                        SpriteScrambling.Scramble(nativeBytes, 0, nativeBytes.Length, _scrambleSeed.Value);
                    _tile.SetTiles(nativeBytes);
                }

                if (_sourcePngPath != null)
                {
                    string sourceError = TrainerSpriteSourcePng.Write(_sourcePngPath, _tile, _pal[0]);
                    if (sourceError != null) { StatusText = "Couldn't save: " + sourceError; return sourceError; }
                }

                int keep = _activePart;
                try
                {
                    for (int p = 0; p < Math.Max(1, _parts.Count); p++)
                    {
                        if (_parts.Count > 0) Activate(p);
                        // The built archive is kept in step too, so other previews show the edit before a compile.
                        ArchiveMembers.Write(_tilesPath, _packedTilesPath != null
                            ? GraphicAssets.Squeeze(_tile.ToBytes(), _packedTilesMarker)
                            : _tile.ToBytes());
                        if (_parts.Count > 0) WriteStagedSheetFiles(_parts[p]);
                        if (BankCount > 0 && !_set.NamingScreen)
                        {
                            int entry = _parts.Count > 0 ? _parts[p].Entry : _trClassID;
                            string scanError = WriteScan(entry);
                            if (scanError == null && _sourcePngPath != null) scanError = WriteScanPng(entry, _sourcePngPath[..^4] + "_enc.png");
                            if (scanError != null) { StatusText = "Couldn't save: " + scanError; return scanError; }
                        }
                        if (_paletteDirty || _sourcePngPath != null)
                        {
                            byte[] nclr = Member(_palEntry);
                            uint[] colours = _pal.SelectMany(bank => bank.Select(c => 0xFF000000u | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B)).ToArray();
                            string error = GraphicAssets.PatchPalette(ref nclr, colours);
                            if (error != null) { StatusText = "Couldn't save: " + error; return error; }
                            ArchiveMembers.Write(_palPath, nclr);
                        }
                    }
                    _paletteDirty = false;
                }
                finally { if (_parts.Count > 0) Activate(keep); }

                if (BankCount > 0) BuildFrameThumbnails();

                HasUnsavedChanges = false;
                _savedState = _lastState = TakeState();
                StatusText = _sourcePngPath != null ? "Saved. Compile the ROM to apply it." : "Saved.";
                return null;
            }
            catch (Exception ex)
            {
                StatusText = "Couldn't save: " + ex.Message;
                AppLogger.Error("TrainerSpriteEditorViewModel.Save failed: " + ex.Message);
                return ex.Message;
            }
        }

        // A 16-colour byte holds the left pixel in its low half.
        private static int[] UnpackIndices(byte[] data, int width, int height, int bpp)
        {
            int count = width * height;
            int[] indices = new int[count];
            switch (bpp)
            {
                case 4:
                    for (int i = 0; i < count && i / 2 < data.Length; i++)
                        indices[i] = (i & 1) == 0 ? data[i / 2] & 0x0F : data[i / 2] >> 4;
                    break;
                case 8:
                    for (int i = 0; i < count && i < data.Length; i++)
                        indices[i] = data[i];
                    break;
                default:
                    throw new NotSupportedException($"Sprites with {bpp} bits per pixel can't be edited.");
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
                        int lo = indices[i] & 0xF;
                        int hi = (i + 1 < count ? indices[i + 1] : 0) & 0xF;
                        result[i / 2] = (byte)(lo | (hi << 4));
                    }
                    return result;
                }
                case 8:
                {
                    byte[] result = new byte[count];
                    for (int i = 0; i < count; i++) result[i] = (byte)(indices[i] & 0xFF);
                    return result;
                }
                default:
                    throw new NotSupportedException($"Sprites with {bpp} bits per pixel can't be edited.");
            }
        }

    }
}
