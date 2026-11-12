using Avalonia.Controls;
using DSPRE.Avalonia;
using DSPRE.Editors;
using DSPRE.Editors.Utils;
using DSPRE.HgEngine;
using DSPRE.Resources;
using DSPRE.ROMFiles;
using NarcAPI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AvaBitmap = Avalonia.Media.Imaging.Bitmap;
using static DSPRE.RomInfo;
using FormSpriteData = DSPRE.Graphics.AlternateFormSprites.Form;
using Avalonia.Media;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    public partial class PokemonSpriteEditorViewModel
    {
        public async Task ImportSpriteAsync(int slot, Window owner)
        {
            if (slot < 0 || slot > 3) return;
            string path = await DialogHelper.OpenFileAsync(owner, $"Import PNG for {SpriteLabels[slot]}",
                new[] { DialogHelper.PngFilter, DialogHelper.AllFilter });
            if (string.IsNullOrEmpty(path)) return;
            StatusText = "";

            try
            {
                byte[] fileBytes = DSUtils.ReadFromFile(path);
                RawImage imported;
                using (MemoryStream ms = new MemoryStream(fileBytes))
                    imported = ImageConverter.DecodeRawImage(ms);
                if (imported == null)
                {
                    StatusText = "Image couldn't be decoded.";
                    return;
                }
                if (imported.Width != SpriteWidth || imported.Height != SpriteHeight)
                {
                    StatusText = $"Sprites are {SpriteWidth}×{SpriteHeight} (two 80×80 frames), not {imported.Width}×{imported.Height}.";
                    return;
                }

                if (TryRecolorOnly(slot, imported)) return;

                if (!TryReadIndexedOrQuantize(fileBytes, imported, out byte[] newIndices, out uint[] newPalette, out int usedCount))
                {
                    StatusText = "The image has more than 16 colours.";
                    return;
                }

                newIndices = await ResolveImportPaletteAsync(newIndices, newPalette, usedCount, slot, owner);
                if (newIndices == null)
                {
                    StatusText = "Import cancelled.";
                    return;
                }

                _rawSprites[slot] = newIndices;
                ApplyPalettesAndPublish();
                Edited();
                OnPropertyChanged(nameof(HasUnsavedChanges));
                SaveNotice.Imported(SpriteLabels[slot], path);
            }
            catch (Exception ex)
            {
                StatusText = $"Couldn't import: {ex.Message}";
            }
        }

        private bool TryRecolorOnly(int slot, RawImage imported)
        {
            if (_rawSprites[slot] == null || !TryDeriveRecolorPalette(_rawSprites[slot], imported, out uint[] recolorPal, out bool[] recolorUsed))
                return false;
            uint[] pal = _normalPal ??= new uint[16];
            for (int i = 0; i < 16; i++) if (recolorUsed[i]) { pal[i] = recolorPal[i]; _normalPalUsed[i] = true; }
            ApplyPalettesAndPublish();
            Edited();
            OnPropertyChanged(nameof(HasUnsavedChanges));
            StatusText = "Palette updated.";
            return true;
        }

        private async Task<byte[]> ResolveImportPaletteAsync(byte[] newIndices, uint[] newPalette, int usedCount, int slot, Window owner)
        {
            bool[] newUsed = MakeUsedMask(usedCount);

            if (_normalPal != null && !PaletteEqualsUpTo(_normalPal, newPalette, usedCount))
            {
                bool keepExisting = await DialogHelper.AskYesNoAsync(
                    $"{SpriteLabels[slot]}'s colours differ from the saved palette.\n\n" +
                    "Keep the saved palette and match the image to it? No replaces the palette for every sprite using it.",
                    "Palette mismatch", owner);
                if (keepExisting)
                {
                    if (!await ConfirmOverwriteIfNeededAsync(newPalette, usedCount, _normalPal, _normalPalUsed, owner)) return null;
                    newIndices = RemapToExistingPalette(newIndices, newPalette, usedCount, _normalPal, _normalPalUsed, out uint[] merged, out bool[] mergedUsed);
                    _normalPal = merged;
                    _normalPalUsed = mergedUsed;
                }
                else
                {
                    if (!await ConfirmFullPaletteReplaceAsync(owner)) return null;
                    _normalPal = newPalette;
                    _normalPalUsed = newUsed;
                }
            }
            else if (_normalPal == null)
            {
                _normalPal = newPalette;
                _normalPalUsed = newUsed;
            }
            return newIndices;
        }

        /// <summary>Shiny palette from a reference image.</summary>
        public Task ImportShinyPaletteAsync(int slot, Window owner) => ImportPaletteFromReferenceAsync(slot, owner, shiny: true);

        /// <summary>Normal palette from a reference image.</summary>
        public Task ImportNormalPaletteAsync(int slot, Window owner) => ImportPaletteFromReferenceAsync(slot, owner, shiny: false);

        private async Task ImportPaletteFromReferenceAsync(int slot, Window owner, bool shiny)
        {
            if (slot < 0 || slot > 3) return;
            string label = shiny ? "shiny" : "normal";
            if (_rawSprites[slot] == null)
            {
                StatusText = $"Needs {SpriteLabels[slot]}'s artwork first.";
                return;
            }
            string path = await DialogHelper.OpenFileAsync(owner, $"Import {label} reference for {SpriteLabels[slot]}",
                new[] { DialogHelper.PngFilter, DialogHelper.AllFilter });
            if (string.IsNullOrEmpty(path)) return;
            StatusText = "";

            try
            {
                byte[] fileBytes = DSUtils.ReadFromFile(path);
                RawImage imported;
                using (MemoryStream ms = new MemoryStream(fileBytes))
                    imported = ImageConverter.DecodeRawImage(ms);
                if (imported == null)
                {
                    StatusText = "Image couldn't be decoded.";
                    return;
                }
                if (imported.Width != SpriteWidth || imported.Height != SpriteHeight)
                {
                    StatusText = $"The image must be {SpriteWidth}×{SpriteHeight} like the artwork, not {imported.Width}×{imported.Height}.";
                    return;
                }
                if (!TryReadIndexedOrQuantize(fileBytes, imported, out byte[] childIndices, out uint[] childPalette, out _))
                {
                    StatusText = "The image has more than 16 colours.";
                    return;
                }

                uint[] derived = DeriveAlternatePalette(_rawSprites[slot], childIndices, childPalette, out bool[] derivedUsed);
                if (derived == null)
                {
                    StatusText = $"Couldn't get a {label} palette from this image.";
                    return;
                }

                // Fill only the slots this image uses, so other poses' colors survive.
                uint[] pal = shiny ? (_shinyPal ??= new uint[16]) : (_normalPal ??= new uint[16]);
                bool[] used = shiny ? _shinyPalUsed : _normalPalUsed;
                for (int i = 0; i < 16; i++)
                {
                    if (derivedUsed[i]) { pal[i] = derived[i]; used[i] = true; }
                }
                ApplyPalettesAndPublish();
                Edited();
                OnPropertyChanged(nameof(HasUnsavedChanges));
                SaveNotice.Imported($"the {label} palette from {SpriteLabels[slot]}'s reference", path);
            }
            catch (Exception ex)
            {
                StatusText = $"Couldn't import: {ex.Message}";
            }
        }

        public async Task ExportSpriteAsync(int slot, Window owner, bool shiny = false)
        {
            if (slot < 0 || slot > 3 || !HasSlot(slot)) return;

            bool[] used = shiny ? _shinyPalUsed : _normalPalUsed;
            int usedCount = 0;
            foreach (bool u in used) if (u) usedCount++;
            if (usedCount < 16)
            {
                bool fillBlack = await DialogHelper.AskYesNoAsync(
                    $"This image has a palette of {usedCount} colours, not the full 16.\n\n" +
                    "Fill in the blanks with black? Choosing No keeps it as a " + usedCount + "-colour palette.",
                    "Palette isn't full", owner);
                if (fillBlack)
                {
                    for (int i = 0; i < 16; i++) used[i] = true;
                    RefreshSwatches(shiny ? ShinySwatches : NormalSwatches, shiny ? _shinyPal : _normalPal, used, shiny);
                }
            }

            string suffix = shiny ? "Shiny" : "";
            string path = await DialogHelper.SaveFileAsync(owner,
                $"Export {SpriteLabels[slot]}{(shiny ? " (shiny)" : "")} as PNG",
                new[] { DialogHelper.PngFilter, DialogHelper.AllFilter },
                $"mon{_currentId:D3}_{SpriteLabels[slot].Replace(" ", "")}{suffix}.png");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                byte[] indices = _rawSprites[slot];
                uint[] pal = shiny ? _shinyPal : _normalPal;
                if (indices == null || pal == null) { StatusText = "There's nothing to export."; return; }
                PlainFiles.WriteBytes(path, IndexedPng.Write(indices, pal, SpriteWidth, SpriteHeight));
                SaveNotice.Exported(path);
            }
            catch (Exception ex) { StatusText = $"Couldn't export: {ex.Message}"; }
        }
    }
}
