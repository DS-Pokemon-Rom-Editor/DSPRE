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

        private static (int backSlot, int frontSlot) GenderSlots(bool female) => female ? (0, 2) : (1, 3);

        // A sheet is the given slots' raw arrays side by side, each already two frames and 160 wide.
        private byte[] BuildSheetIndices(params int[] slots)
        {
            foreach (int s in slots) if (_rawSprites[s] == null) return null;
            byte[] sheet = new byte[SpriteWidth * slots.Length * SpriteHeight];
            for (int y = 0; y < SpriteHeight; y++)
                for (int i = 0; i < slots.Length; i++)
                    Array.Copy(_rawSprites[slots[i]], y * SpriteWidth, sheet, y * SpriteWidth * slots.Length + i * SpriteWidth, SpriteWidth);
            return sheet;
        }

        private void WriteSheetIndices(byte[] sheetIndices, params int[] slots)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                byte[] arr = new byte[SpriteWidth * SpriteHeight];
                for (int y = 0; y < SpriteHeight; y++)
                    Array.Copy(sheetIndices, y * SpriteWidth * slots.Length + i * SpriteWidth, arr, y * SpriteWidth, SpriteWidth);
                _rawSprites[slots[i]] = arr;
            }
        }

        private byte[] BuildGenderSheetIndices(bool female) { (int b, int f) = GenderSlots(female); return BuildSheetIndices(b, f); }
        private void WriteGenderSheetIndices(bool female, byte[] sheetIndices) { (int b, int f) = GenderSlots(female); WriteSheetIndices(sheetIndices, b, f); }

        // Both genders in one sheet: Female Back, Male Back, Female Front, Male Front.
        private byte[] BuildFullSheetIndices() => BuildSheetIndices(0, 1, 2, 3);
        private void WriteFullSheetIndices(byte[] sheetIndices) => WriteSheetIndices(sheetIndices, 0, 1, 2, 3);

        /// <summary>True when both genders have real sprites and no alternate form is open.</summary>
        public bool CanUseFullSheet => _rawSprites[0] != null && _rawSprites[1] != null && _rawSprites[2] != null && _rawSprites[3] != null && !CanAddOppositeGenderSprites && !IsAlternateForms;

        public async Task ExportSpriteSheetAsync(Window owner, bool female, bool shiny = false)
        {
            uint[] pal = shiny ? _shinyPal : _normalPal;
            if (pal == null) { StatusText = "Load a Pokémon first."; return; }
            string genderLabel = female ? "Female" : "Male";
            string colorLabel = shiny ? "Shiny" : "Normal";
            string path = await DialogHelper.SaveFileAsync(owner, $"Export {genderLabel.ToLowerInvariant()} {colorLabel.ToLowerInvariant()} sprite sheet",
                new[] { DialogHelper.PngFilter, DialogHelper.AllFilter },
                $"mon{_currentId:D3}_{genderLabel}_{colorLabel}_sheet.png");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                byte[] indices = BuildGenderSheetIndices(female);
                if (indices == null) { StatusText = "There's nothing to export."; return; }
                PlainFiles.WriteBytes(path, IndexedPng.Write(indices, pal, SpriteWidth * 2, SpriteHeight));
                SaveNotice.Exported(path);
            }
            catch (Exception ex) { StatusText = $"Couldn't export: {ex.Message}"; }
        }

        /// <summary>Both genders' Back and Front in one sheet. Shown only when CanUseFullSheet.</summary>
        public async Task ExportFullSheetAsync(Window owner, bool shiny = false)
        {
            uint[] pal = shiny ? _shinyPal : _normalPal;
            if (pal == null) { StatusText = "Load a Pokémon first."; return; }
            string colorLabel = shiny ? "Shiny" : "Normal";
            string path = await DialogHelper.SaveFileAsync(owner, $"Export {colorLabel.ToLowerInvariant()} sprite sheet (both genders)",
                new[] { DialogHelper.PngFilter, DialogHelper.AllFilter },
                $"mon{_currentId:D3}_Both_{colorLabel}_sheet.png");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                byte[] indices = BuildFullSheetIndices();
                if (indices == null) { StatusText = "There's nothing to export."; return; }
                PlainFiles.WriteBytes(path, IndexedPng.Write(indices, pal, SpriteWidth * 4, SpriteHeight));
                SaveNotice.Exported(path);
            }
            catch (Exception ex) { StatusText = $"Couldn't export: {ex.Message}"; }
        }

        private async Task<(RawImage image, byte[] fileBytes)> OpenGenderSheetAsync(Window owner, string title) =>
            await OpenSheetFileAsync(owner, title, 2, "Back on the left, Front on the right, each with both animation frames");

        private async Task<(RawImage image, byte[] fileBytes)> OpenFullSheetAsync(Window owner, string title) =>
            await OpenSheetFileAsync(owner, title, 4, "Female back, male back, female front and male front, left to right, each with both animation frames");

        private async Task<(RawImage image, byte[] fileBytes)> OpenSheetFileAsync(Window owner, string title, int poseCount, string layoutHint)
        {
            string path = await DialogHelper.OpenFileAsync(owner, title, new[] { DialogHelper.PngFilter, DialogHelper.AllFilter });
            if (string.IsNullOrEmpty(path)) return (null, null);
            StatusText = "";
            byte[] fileBytes = DSUtils.ReadFromFile(path);
            RawImage imported;
            using (MemoryStream ms = new MemoryStream(fileBytes))
                imported = ImageConverter.DecodeRawImage(ms);
            if (imported == null) { StatusText = "Image couldn't be decoded."; return (null, null); }
            int expectedWidth = SpriteWidth * poseCount;
            if (imported.Width != expectedWidth || imported.Height != SpriteHeight)
            {
                StatusText = $"Sprite sheet must be {expectedWidth}×{SpriteHeight} pixels: {layoutHint} (got {imported.Width}×{imported.Height}).";
                return (null, null);
            }
            return (imported, fileBytes);
        }

        public async Task ImportSpriteSheetAsync(Window owner, bool female)
        {
            string genderLabel = female ? "Female" : "Male";
            try
            {
                (RawImage imported, byte[] fileBytes) = await OpenGenderSheetAsync(owner, $"Import {genderLabel.ToLowerInvariant()} sprite sheet");
                if (imported == null) return;

                byte[] oldSheetIndices = BuildGenderSheetIndices(female);
                if (oldSheetIndices != null && TryDeriveRecolorPalette(oldSheetIndices, imported, out uint[] recolorPal, out bool[] recolorUsed))
                {
                    uint[] pal = _normalPal ??= new uint[16];
                    for (int i = 0; i < 16; i++) if (recolorUsed[i]) { pal[i] = recolorPal[i]; _normalPalUsed[i] = true; }
                    ApplyPalettesAndPublish();
                    Edited();
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                    StatusText = "Palette updated.";
                    return;
                }

                if (!TryReadIndexedOrQuantize(fileBytes, imported, out byte[] newIndices, out uint[] newPalette, out int usedCount))
                {
                    StatusText = "The image has more than 16 colours.";
                    return;
                }

                bool[] newUsed = MakeUsedMask(usedCount);
                if (_normalPal != null && !PaletteEqualsUpTo(_normalPal, newPalette, usedCount))
                {
                    bool keepExisting = await DialogHelper.AskYesNoAsync(
                        "This sheet's colours differ from the saved palette.\n\n" +
                        "Keep the saved palette and match the sheet to it? No replaces the palette for every sprite using it.",
                        "Palette mismatch", owner);
                    if (keepExisting)
                    {
                        if (!await ConfirmOverwriteIfNeededAsync(newPalette, usedCount, _normalPal, _normalPalUsed, owner))
                        {
                            StatusText = "Import cancelled.";
                            return;
                        }
                        newIndices = RemapToExistingPalette(newIndices, newPalette, usedCount, _normalPal, _normalPalUsed, out uint[] merged, out bool[] mergedUsed);
                        _normalPal = merged;
                        _normalPalUsed = mergedUsed;
                    }
                    else
                    {
                        if (!await ConfirmFullPaletteReplaceAsync(owner))
                        {
                            StatusText = "Import cancelled.";
                            return;
                        }
                        _normalPal = newPalette;
                        _normalPalUsed = newUsed;
                    }
                }
                else if (_normalPal == null)
                {
                    _normalPal = newPalette;
                    _normalPalUsed = newUsed;
                }

                WriteGenderSheetIndices(female, newIndices);
                ApplyPalettesAndPublish();
                Edited();
                OnPropertyChanged(nameof(HasUnsavedChanges));
                SaveNotice.Imported($"{genderLabel}'s back and front", null);
            }
            catch (Exception ex) { StatusText = $"Couldn't import: {ex.Message}"; }
        }

        /// <summary>Shiny palette from a sheet.</summary>
        public async Task ImportShinySpriteSheetAsync(Window owner, bool female)
        {
            string genderLabel = female ? "Female" : "Male";
            try
            {
                byte[] parentIndices = BuildGenderSheetIndices(female);
                if (parentIndices == null)
                {
                    StatusText = $"Needs {genderLabel}'s back and front artwork first.";
                    return;
                }
                (RawImage imported, byte[] fileBytes) = await OpenGenderSheetAsync(owner, $"Import {genderLabel.ToLowerInvariant()} shiny reference sheet");
                if (imported == null) return;
                if (!TryReadIndexedOrQuantize(fileBytes, imported, out byte[] childIndices, out uint[] childPalette, out _))
                {
                    StatusText = "The image has more than 16 colours.";
                    return;
                }

                uint[] derived = DeriveAlternatePalette(parentIndices, childIndices, childPalette, out bool[] derivedUsed);
                if (derived == null) { StatusText = "Couldn't get a shiny palette from this image."; return; }

                uint[] pal = _shinyPal ??= new uint[16];
                for (int i = 0; i < 16; i++)
                {
                    if (derivedUsed[i]) { pal[i] = derived[i]; _shinyPalUsed[i] = true; }
                }
                ApplyPalettesAndPublish();
                Edited();
                OnPropertyChanged(nameof(HasUnsavedChanges));
                SaveNotice.Imported($"the shiny palette from the {genderLabel.ToLowerInvariant()} reference sheet", null);
            }
            catch (Exception ex) { StatusText = $"Couldn't import: {ex.Message}"; }
        }

        /// <summary>Imports both genders' artwork from one sheet.</summary>
        public async Task ImportFullSheetAsync(Window owner)
        {
            try
            {
                (RawImage imported, byte[] fileBytes) = await OpenFullSheetAsync(owner, "Import sprite sheet (both genders)");
                if (imported == null) return;

                byte[] oldSheetIndices = BuildFullSheetIndices();
                if (oldSheetIndices != null && TryDeriveRecolorPalette(oldSheetIndices, imported, out uint[] recolorPal, out bool[] recolorUsed))
                {
                    uint[] pal = _normalPal ??= new uint[16];
                    for (int i = 0; i < 16; i++) if (recolorUsed[i]) { pal[i] = recolorPal[i]; _normalPalUsed[i] = true; }
                    ApplyPalettesAndPublish();
                    Edited();
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                    StatusText = "Palette updated.";
                    return;
                }

                if (!TryReadIndexedOrQuantize(fileBytes, imported, out byte[] newIndices, out uint[] newPalette, out int usedCount))
                {
                    StatusText = "The image has more than 16 colours.";
                    return;
                }

                bool[] newUsed = MakeUsedMask(usedCount);
                if (_normalPal != null && !PaletteEqualsUpTo(_normalPal, newPalette, usedCount))
                {
                    bool keepExisting = await DialogHelper.AskYesNoAsync(
                        "This sheet's colours differ from the saved palette.\n\n" +
                        "Keep the saved palette and match the sheet to it? No replaces the palette for every sprite using it.",
                        "Palette mismatch", owner);
                    if (keepExisting)
                    {
                        if (!await ConfirmOverwriteIfNeededAsync(newPalette, usedCount, _normalPal, _normalPalUsed, owner))
                        {
                            StatusText = "Import cancelled.";
                            return;
                        }
                        newIndices = RemapToExistingPalette(newIndices, newPalette, usedCount, _normalPal, _normalPalUsed, out uint[] merged, out bool[] mergedUsed);
                        _normalPal = merged;
                        _normalPalUsed = mergedUsed;
                    }
                    else
                    {
                        if (!await ConfirmFullPaletteReplaceAsync(owner))
                        {
                            StatusText = "Import cancelled.";
                            return;
                        }
                        _normalPal = newPalette;
                        _normalPalUsed = newUsed;
                    }
                }
                else if (_normalPal == null)
                {
                    _normalPal = newPalette;
                    _normalPalUsed = newUsed;
                }

                WriteFullSheetIndices(newIndices);
                ApplyPalettesAndPublish();
                Edited();
                OnPropertyChanged(nameof(HasUnsavedChanges));
                SaveNotice.Imported("both genders' back and front", null);
            }
            catch (Exception ex) { StatusText = $"Couldn't import: {ex.Message}"; }
        }

        /// <summary>Shiny palette from a both-genders sheet.</summary>
        public async Task ImportShinyFullSheetAsync(Window owner)
        {
            try
            {
                byte[] parentIndices = BuildFullSheetIndices();
                if (parentIndices == null)
                {
                    StatusText = "Needs the back and front artwork of both genders first.";
                    return;
                }
                (RawImage imported, byte[] fileBytes) = await OpenFullSheetAsync(owner, "Import shiny reference sheet (both genders)");
                if (imported == null) return;
                if (!TryReadIndexedOrQuantize(fileBytes, imported, out byte[] childIndices, out uint[] childPalette, out _))
                {
                    StatusText = "The image has more than 16 colours.";
                    return;
                }

                uint[] derived = DeriveAlternatePalette(parentIndices, childIndices, childPalette, out bool[] derivedUsed);
                if (derived == null) { StatusText = "Couldn't get a shiny palette from this image."; return; }

                uint[] pal = _shinyPal ??= new uint[16];
                for (int i = 0; i < 16; i++)
                {
                    if (derivedUsed[i]) { pal[i] = derived[i]; _shinyPalUsed[i] = true; }
                }
                ApplyPalettesAndPublish();
                Edited();
                OnPropertyChanged(nameof(HasUnsavedChanges));
                SaveNotice.Imported("both genders' shiny palette from the reference sheet", null);
            }
            catch (Exception ex) { StatusText = $"Couldn't import: {ex.Message}"; }
        }
    }
}
