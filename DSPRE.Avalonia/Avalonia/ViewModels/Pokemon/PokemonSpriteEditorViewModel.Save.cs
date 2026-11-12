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

        public void Save()
        {
            if (!_dirty || _currentId <= 0) return;

            if (_isAlternateForms) { SaveAlternateForm(); return; }

            if (HgEngineProject.IsActive)
            {
                // hg-engine rebuilds a/0/0/4 from pokegra.mk, so a species it has no PNGs for can't be saved here.
                if (!SaveToHgEngineSource()) StatusText = NoHgEngineSpritesNote;
                return;
            }

            _pending.Save();   // a copied gender's members first, so its poses have full-size members to go into
            PokemonBattleSpriteArchive narc = PokemonBattleSpriteArchive.Open(DirNames.PokemonBattleSprites);
            if (narc == null)
            {
                StatusText = "The battle sprite archive is missing.";
                return;
            }

            int baseOffset = _currentId * 6;
            List<string> failed = new List<string>();
            try
            {
                for (int i = 0; i < 4; i++)
                {
                    if (_rawSprites[i] != null && !narc.WriteSprite(baseOffset + i, _rawSprites[i]))
                        failed.Add($"{SpriteLabels[i]}: {MissingSlotReason(i)}");
                }
                if (_normalPal != null && !narc.WritePalette(baseOffset + 4, _normalPal))
                    failed.Add("Normal palette: its file isn't a 16-colour palette.");
                if (_shinyPal != null && !narc.WritePalette(baseOffset + 5, _shinyPal))
                    failed.Add("Shiny palette: its file isn't a 16-colour palette.");
            }
            catch (Exception ex) when (AppLogger.Noted("PokemonSpriteEditorViewModel.Save", ex))
            {
                failed.Add(ex.Message);
            }

            FinishSave(failed);
        }

        private string MissingSlotReason(int slot)
        {
            bool female = slot == 0 || slot == 2;
            if (CanAddOppositeGenderSprites && female == _missingGenderIsFemale)
                return $"this Pokémon has no {(female ? "female" : "male")} sprite slot yet. Use {AddOppositeGenderLabel} first.";
            return "its file isn't a battle sprite.";
        }

        // Anything not written keeps the editor dirty, so unsaved work is never reported as saved.
        private void FinishSave(List<string> failed)
        {
            if (failed.Count == 0)
            {
                _undo?.MarkSaved();
                _dirty = false;
                SaveNotice.Saved(UnsavedChangesDescription);
                OnPropertyChanged(nameof(HasUnsavedChanges));
                StatusText = "Saved.";
                return;
            }
            AppLogger.Error($"Sprite save for Mon {_currentId} incomplete: {string.Join("; ", failed)}");
            StatusText = "Not saved:\n" + string.Join("\n", failed);
        }

        // Forms persist only Male Back and Front (_rawSprites[1] and [3]), as ShowFemaleFormImport shows.
        private void SaveAlternateForm()
        {
            if (_currentFormData == null || _selectedFormIndex < 0 || _selectedFormIndex >= _currentFormData.Length) return;
            FormSpriteData form = _currentFormData[_selectedFormIndex];

            PokemonBattleSpriteArchive narc = PokemonBattleSpriteArchive.Open(DirNames.OtherPokemonBattleSprites);
            if (narc == null)
            {
                StatusText = "The alternate form sprite archive is missing.";
                return;
            }

            List<string> failed = new List<string>();
            try
            {
                if (_rawSprites[1] != null && !narc.WriteSprite(form.BackSpriteIndex, _rawSprites[1]))
                    failed.Add("Back: this form has no battle sprite file.");
                if (_rawSprites[3] != null && !narc.WriteSprite(form.FrontSpriteIndex, _rawSprites[3]))
                    failed.Add("Front: this form has no battle sprite file.");
                if (_normalPal != null && !narc.WritePalette(form.NormalPaletteIndex, _normalPal))
                    failed.Add("Normal palette: this form has no palette file.");
                if (_shinyPal != null && !narc.WritePalette(form.ShinyPaletteIndex, _shinyPal))
                    failed.Add("Shiny palette: this form has no palette file.");

                // hg-engine rebuilds some of these palettes from JASC files on every build.
                foreach ((int index, uint[] colours) in new[] { (form.NormalPaletteIndex, _normalPal), (form.ShinyPaletteIndex, _shinyPal) })
                    if (colours != null && HgEngineSourceAssets.ConvertedPaletteFor(DirNames.OtherPokemonBattleSprites, index) is string pal)
                        HgEngineOverworlds.WriteJasc(pal, colours.Take(16).Select(c => (int)(c & 0xFFFFFF)).ToArray());
            }
            catch (Exception ex) when (AppLogger.Noted("PokemonSpriteEditorViewModel.SaveAlternateForm", ex))
            {
                failed.Add(ex.Message);
            }

            FinishSave(failed);
        }

        // Writes hg-engine's source PNGs (front Normal, back Shiny) so the edit survives the next make.
        private bool SaveToHgEngineSource()
        {
            string[] posePaths = HgEnginePokemonBattleSprites.TryGetPosePaths(_currentId);
            if (posePaths == null) return false;

            try
            {
                for (int i = 0; i < 4; i++)
                {
                    if (_rawSprites[i] == null) continue;
                    uint[] pal = (i == 0 || i == 1) ? _shinyPal : _normalPal;
                    if (pal == null) continue;
                    byte[] png = IndexedPng.Write(_rawSprites[i], pal, SpriteWidth, SpriteHeight);
                    PlainFiles.WriteBytes(posePaths[i], png);
                    HgEnginePokemonBattleSprites.EnsurePoseKey(posePaths, i);
                }
                _undo?.MarkSaved();
                _dirty = false;
                SaveNotice.Saved(UnsavedChangesDescription);
                OnPropertyChanged(nameof(HasUnsavedChanges));
                StatusText = "Saved to hg-engine source.";
            }
            catch (Exception ex)
            {
                StatusText = $"Couldn't save hg-engine sprites: {ex.Message}";
                AppLogger.Error("hg-engine sprite save failed: " + ex.Message);
            }
            return true;
        }
    }
}
