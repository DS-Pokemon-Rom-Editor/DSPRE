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
        private void FillList()
        {
            FillRampFills();
            int keep = Brush;
            Tiles.Clear();
            if (_set == null) { Brush = -1; _tileOrder.Clear(); ShowTileList(); return; }

            int[] uses = TileUses();
            for (int i = 0; i < _set.Tiles.Count; i++)
            {
                MapTileset.Tile tile = _set.Tiles[i];
                int n = i;
                Tiles.Add(new TileRow
                {
                    Index = i,
                    Swatch = TileGridControl.Colour(i),
                    Picture = Thumbnail(i),
                    MakeBig = () => BigThumbnail(n),
                    Name = tile.Name,
                    Pictures = string.Join(", ", tile.Pictures.Where(p => !string.IsNullOrEmpty(p))),
                    Size = tile.Spreads && !(tile.Name ?? "").Contains($"{tile.Wide}x{tile.Deep}") ? $"  ·  {tile.Wide}x{tile.Deep} squares" : "",
                    Uses = uses[i],
                });
            }

            Brush = keep >= 0 && keep < Tiles.Count ? keep : Tiles.Count > 0 ? 0 : -1;
            SortTileList();
        }

        // Tiles on the map go first.
        private void SortTileList()
        {
            CountUses();
            _tileOrder = Tiles.OrderByDescending(r => r.Uses > 0).ThenBy(r => r.Index).Select(r => r.Index).ToList();
            ShowTileList();
        }

        // Fixed when the list is filled, so the list does not reshuffle under the pointer while painting.
        private List<int> _tileOrder = new List<int>();

        private void ShowTileList()
        {
            ShownTiles.Clear();
            string[] words = _tileSearch.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (int i in _tileOrder)
            {
                if (i >= Tiles.Count) continue;
                TileRow row = Tiles[i];
                if (words.All(w => row.Name.Contains(w, StringComparison.OrdinalIgnoreCase)
                                || row.Pictures.Contains(w, StringComparison.OrdinalIgnoreCase)))
                    ShownTiles.Add(row);
            }
            Raise(nameof(ShownTile));
        }

        // How many times each tile is placed on the grid, every layer counted.
        private int[] TileUses()
        {
            int[] uses = new int[_set?.Tiles.Count ?? 0];
            TileGrid grid = _painter.Grid;
            if (grid == null) return uses;
            for (int l = 0; l < TileGrid.Layers; l++)
                for (int z = 0; z < TileGrid.Across; z++)
                    for (int x = 0; x < TileGrid.Across; x++)
                    {
                        TileGrid.Square sq = grid.At(x, z, l);
                        if (sq.Tile >= 0 && sq.Tile < uses.Length && sq.FromX == x && sq.FromZ == z) uses[sq.Tile]++;
                    }
            return uses;
        }

        private void CountUses()
        {
            int[] uses = TileUses();
            for (int i = 0; i < Tiles.Count && i < uses.Length; i++) Tiles[i].Uses = uses[i];
        }

        private void FillSmart()
        {
            SmartDrawings.Clear();
            if (_set != null)
                for (int i = 0; i < _set.SmartDrawings.Count; i++)
                    SmartDrawings.Add(new SmartRow { Index = i, Picture = SmartPicture(_set.SmartDrawings[i]) });

            if (_painter.SmartIndex >= SmartDrawings.Count) _painter.SmartIndex = Math.Max(0, SmartDrawings.Count - 1);
            FillSlots();
            Raise(nameof(SmartIndex));
            Raise(nameof(HasSmartDrawing));
        }

        private void FillSlots()
        {
            Slots.Clear();
            SmartDrawing drawing = _painter.Drawing;
            if (drawing == null) return;
            for (int slot = 0; slot < SmartDrawing.Slots; slot++)
            {
                int tile = drawing[slot];
                Slots.Add(new SlotRow
                {
                    Slot = slot,
                    Picture = tile >= 0 ? Thumbnail(tile) : null,
                    Tip = tile >= 0 && tile < (_set?.Tiles.Count ?? 0) ? _set.Tiles[tile].Name : "empty",
                });
            }
        }

        public void SetSlot(int slot, bool empty)
        {
            SmartDrawing drawing = _painter.Drawing;
            if (drawing == null || slot < 0 || slot >= SmartDrawing.Slots) return;
            drawing[slot] = empty ? -1 : Brush;
            FillSlots();
            if (SmartIndex >= 0 && SmartIndex < SmartDrawings.Count)
                SmartDrawings[SmartIndex] = new SmartRow { Index = SmartIndex, Picture = SmartPicture(drawing) };
        }

        public void AddSmartDrawing()
        {
            if (_set == null) return;
            _set.SmartDrawings.Add(new SmartDrawing());
            FillSmart();
            SmartIndex = _set.SmartDrawings.Count - 1;
        }

        public void RemoveSmartDrawing()
        {
            if (_set == null || SmartIndex < 0 || SmartIndex >= _set.SmartDrawings.Count) return;
            _set.SmartDrawings.RemoveAt(SmartIndex);
            FillSmart();
        }
    }
}
