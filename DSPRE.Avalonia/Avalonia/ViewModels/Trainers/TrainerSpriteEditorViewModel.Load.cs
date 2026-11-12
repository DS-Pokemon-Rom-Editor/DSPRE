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
        /// <summary>Loads the class. Returns null on success, otherwise the error.</summary>
        public string Load(int trClassID)
        {
            _trClassID = trClassID;
            _bankMap = null;
            if (_set.NamingScreen) return LoadNamingIcon(trClassID);
            try
            {
                int paletteFileID = TrainerGraphicsLayout.ColoursEntry(trClassID);
                _pal = ReadPaletteRows(Member(paletteFileID));

                int tilesFileID = TrainerGraphicsLayout.DrawingEntry(trClassID);
                _tilesPath = ArchiveMembers.PathOf(_set.Archive, tilesFileID);
                _tile = ReadDrawing(Member(tilesFileID));

                ReadCellsFor(trClassID);

                _parts.Clear();
                _parts.Add(new SpritePart { Entry = trClassID, Tile = _tile, Pal = _pal, Sprite = _sprite, TilesPath = _tilesPath, PalPath = ArchiveMembers.PathOf(_set.Archive, paletteFileID), PalEntry = paletteFileID });
                AddLinkedPart(trClassID);
                Activate(0);
                MapScanFrames();
                OnPropertyChanged(nameof(HasLinkedSets));
                OnPropertyChanged(nameof(SelectedClassIndex));

                ShowLoaded(trClassID);

                OnPropertyChanged(nameof(IsFlatSheetMode));
                OnPropertyChanged(nameof(FrameCount));
                OnPropertyChanged(nameof(CanUseSheets));
                OnPropertyChanged(nameof(CanUseAnimationSheets));
                HasUnsavedChanges = false;
                _paletteDirty = false;
                _palPath = ArchiveMembers.PathOf(_set.Archive, paletteFileID); _palEntry = paletteFileID;
                ResetSteps();
                StopAnimPreview();
                RebuildAnimCellChoices();
                LoadAnimJson(trClassID);
                return null;
            }
            catch (Exception ex)
            {
                _tile = null; _pal = null; _sprite = null;
                StatusText = "Couldn't load: " + ex.Message;
                AppLogger.Error("TrainerSpriteEditorViewModel.Load failed: " + ex.Message);
                return ex.Message;
            }
        }

        private void ReadCellsFor(int trClassID)
        {
            _sprite = null; _cellsFromSource = false;
            _sourcePngPath = null;
            if (!TrainerGraphicsLayout.HasCells) return;
            if (HgEngineProject.IsActive)
            {
                string stem = HgEngineTrainerGraphicsSource.Stem(_set.IsBack, trClassID);
                if (_pal.Length > 0 && TrainerSpriteSourcePng.TryApply(stem + ".png", _tile, _pal[0])) _sourcePngPath = stem + ".png";
                string cellPath = stem + "_cell.json";
                if (HgEngineTrainerGraphicsSource.Has(cellPath))
                {
                    if (HgEngineTrainerGraphicsSource.TryReadCells(cellPath, out NcerFile cells, out string cellError))
                    {
                        _sprite = cells;
                        _cellsFromSource = true;
                    }
                    else
                    {
                        AppLogger.Error("TrainerSpriteEditorViewModel: " + cellError);
                    }
                }
            }

            if (!_cellsFromSource)
                _sprite = ReadCells(Member(TrainerGraphicsLayout.CellsEntry(trClassID)));
        }

        private void AddLinkedPart(int trClassID)
        {
            int linked = _set.IsBack && !_cellsFromSource && !HgEngineProject.IsActive ? DSPRE.ROMFiles.TrainerBackSprites.LinkedSet(trClassID) : -1;
            if (linked < 0 || !ArchiveMembers.Has(_set.Archive, TrainerGraphicsLayout.CellsEntry(linked))) return;
            int lt = TrainerGraphicsLayout.DrawingEntry(linked), lp = TrainerGraphicsLayout.ColoursEntry(linked), lc = TrainerGraphicsLayout.CellsEntry(linked);
            _parts[0].Label = "Double battles";
            _parts.Add(new SpritePart
            {
                Entry = linked, Label = "Single battles",
                Tile = ReadDrawing(Member(lt)),
                Pal = ReadPaletteRows(Member(lp)),
                Sprite = ReadCells(Member(lc)),
                TilesPath = ArchiveMembers.PathOf(_set.Archive, lt), PalPath = ArchiveMembers.PathOf(_set.Archive, lp), PalEntry = lp,
            });
        }

        private void ShowLoaded(int trClassID)
        {
            if (BankCount > 0)
            {
                ZoomFactor = 4;
                BuildFrameThumbnails();
                _activePaletteBank = -1;
                // Makes the setter below see a change, so a reload on the same frame still rebuilds.
                _selectedStripFrame = -1;
                SelectedFrameIndex = 0; // triggers LoadFrame -> cells + canvas + swatches
                StatusText = $"{Frames(FrameCount)}, {_tile.BitsPerPixel}bpp";
                return;
            }
            // DP (or an NCER with no banks): flat tile-sheet fallback.
            _sprite = null;
            ZoomFactor = 12;
            FrameThumbnails.Clear();
            OnPropertyChanged(nameof(HasFrames));
            LoadFlatSheet();
            StatusText = $"{Capital(_set.Noun)} {trClassID}: {_flatWidth}×{_flatHeight} tile sheet, {_tile.BitsPerPixel}bpp";
        }

        private void BuildFrameThumbnails()
        {
            FrameThumbnails.Clear();
            int keep = _activePart;
            for (int p = 0; p < Math.Max(1, _parts.Count); p++)
            {
                if (_parts.Count > 0) Activate(p);
                for (int i = 0; i < BankCount; i++)
                {
                    global::Avalonia.Media.Imaging.Bitmap bmp = ImageConverter.ToAvaloniaBitmap(GetCompositedRawImage(i, 64, 64, null));
                    string caption = _parts.Count > 1 ? $"{_parts[p].Label} {i}" : null;
                    if (bmp != null) FrameThumbnails.Add(new FrameThumbnailViewModel(FrameThumbnails.Count, bmp, p, i, caption));
                }
            }
            if (_parts.Count > 0) Activate(keep);
            OnPropertyChanged(nameof(HasFrames));
        }

        private bool _thumbnailRefreshQueued;

        // Frames share tiles, so an edit to one pose can change others; one refresh per burst of strokes.
        private void QueueFrameThumbnailRefresh()
        {
            if (_thumbnailRefreshQueued || BankCount == 0) return;
            _thumbnailRefreshQueued = true;
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _thumbnailRefreshQueued = false;
                if (_tile == null || BankCount == 0) return;
                int keep = _activePart;
                foreach (FrameThumbnailViewModel thumb in FrameThumbnails)
                {
                    if (_parts.Count > 0) Activate(thumb.Part);
                    if (thumb.Local >= BankCount) continue;
                    global::Avalonia.Media.Imaging.Bitmap bmp = ImageConverter.ToAvaloniaBitmap(GetCompositedRawImage(thumb.Local, 64, 64, null));
                    if (bmp != null) thumb.Image = bmp;
                }
                if (_parts.Count > 0) Activate(keep);
            }, global::Avalonia.Threading.DispatcherPriority.Background);
        }
    }
}
