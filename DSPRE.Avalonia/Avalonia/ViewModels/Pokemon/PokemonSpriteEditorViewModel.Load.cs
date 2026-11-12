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
        public void LoadMon(int id)
        {
            // The last Pokémon's history must never apply to this one.
            _undo = null;
            _pending = new();
            RaiseUndo();
            ClearBitmaps();
            StatusText = "";
            IsAlternateForms = false;
            FormSharesBaseData = false;

            // Ids past the Pokédex are pseudo-species (Deoxys, Wormadam, Rotom formes): go to that form.
            (int baseId, string description)? pseudo = ResolvePseudoFormId(id);
            _currentId = pseudo?.baseId ?? id;
            // Also under hg-engine: its PokeFormDataTbl.c forms move, the rest (form 0 too) read otherpoke.
            HasAlternateForms = _currentId > 0 && GetAlternateFormsForCurrentSpecies().Length > 0;
            PopulateVariantList();
            LoadFollower(_currentId);

            if (pseudo.HasValue)
            {
                int formIdx = Array.FindIndex(_currentFormData, f => FormNameMatchesDescription(f.Name, pseudo.Value.description));
                if (formIdx >= 0) { SelectedVariantIndex = formIdx; return; }
                StatusText = "This form doesn't have its own sprite data.";
                return;
            }

            if (SelectOwnFormEntry()) return;

            if (id <= 0)
            {
                StatusText = "No Pokémon selected.";
                return;
            }

            if (HgEngineProject.IsActive && LoadMonFromHgEngineSource(id)) return;

            LoadMainSprites(id);
        }

        // Form-table species keep their default form there. True when that form's load took over.
        private bool SelectOwnFormEntry()
        {
            int selfIndex = -1;
            for (int i = 0; i < _currentFormData.Length; i++)
            {
                if (_currentFormData[i].HgEngineSpeciesId == _currentId) { selfIndex = i; break; }
                if (HgEngineProject.IsActive && ResolveHgEngineMigratedFormId(_currentFormFamilyBaseId, i) == _currentId) { selfIndex = i; break; }
            }
            if (selfIndex >= 0)
            {
                _selectedVariantIndex = selfIndex;
                OnPropertyChanged(nameof(SelectedVariantIndex));
            }
            else if (_currentFormData.Length > 0)
            {
                SelectedVariantIndex = 0;
                return true;
            }
            return false;
        }

        private void LoadMainSprites(int id)
        {
            try
            {
                PokemonBattleSpriteArchive narc = PokemonBattleSpriteArchive.Open(DirNames.PokemonBattleSprites);
                if (narc == null)
                {
                    StatusText = "The battle sprite archive is missing.";
                    return;
                }

                int baseOffset = id * 6;

                byte[][] rawBmps = new byte[4][];
                bool[] hasRealSprite = new bool[4];
                for (int i = 0; i < 4; i++)
                {
                    rawBmps[i] = narc.ReadSprite(baseOffset + i);
                    hasRealSprite[i] = rawBmps[i] != null;
                }
                UpdateOppositeGenderGap(hasRealSprite);

                uint[] normalPal = narc.ReadPalette(baseOffset + 4);
                uint[] shinyPal = narc.ReadPalette(baseOffset + 5);

                if (normalPal == null)
                {
                    StatusText = "Couldn't load palette for this Pokémon.";
                    return;
                }
                if (shinyPal == null) shinyPal = normalPal;

                _rawSprites = rawBmps;
                _normalPal  = normalPal;
                _shinyPal   = shinyPal;
                _normalPalUsed = AllUsed();
                _shinyPalUsed  = AllUsed();

                ApplyPalettesAndPublish();
                _dirty = false;
                ResetUndo();
                OnPropertyChanged(nameof(HasUnsavedChanges));
            }
            catch (Exception ex)
            {
                StatusText = $"Couldn't load sprites: {ex.Message}";
            }
        }


        // hg-engine's source PNGs: front has the Normal palette, back the Shiny. Placeholders are skipped.
        private bool LoadMonFromHgEngineSource(int id)
        {
            string[] posePaths = HgEnginePokemonBattleSprites.TryGetPosePaths(id);
            if (posePaths == null) return false;
            _loadedFromHgEngine = true;

            try
            {
                byte ratio = ReadGenderRatio(id);
                bool skipFemale = ratio == SpeciesFile.GenderRatioMale || ratio == SpeciesFile.GenderRatioGenderless;
                bool skipMale = ratio == SpeciesFile.GenderRatioFemale;
                int normalSlot = skipMale ? 2 : 3;   // Front: Female or Male
                int shinySlot  = skipMale ? 0 : 1;   // Back: Female or Male

                byte[][] rawBmps = new byte[4][];
                bool[] hasRealSprite = new bool[4];
                uint[] normalPal = null, shinyPal = null;
                for (int i = 0; i < 4; i++)
                {
                    bool isFemaleSlot = i == 0 || i == 2;
                    if ((isFemaleSlot && skipFemale) || (!isFemaleSlot && skipMale)) continue;

                    byte[] pngBytes = DSUtils.ReadFromFile(posePaths[i]);
                    if (!IndexedPng.TryRead(pngBytes, out byte[] indices, out uint[] pal, out int w, out int h) ||
                        w != SpriteWidth || h != SpriteHeight || pal.Length > 16)
                    {
                        StatusText = $"{SpriteLabels[i]}'s hg-engine sprite isn't a {SpriteWidth}×{SpriteHeight} 16-colour indexed PNG.";
                        return true;
                    }
                    rawBmps[i] = indices;
                    hasRealSprite[i] = true;
                    if (i == normalSlot) normalPal = Pad16(pal);
                    if (i == shinySlot) shinyPal = Pad16(pal);
                }

                UpdateOppositeGenderGap(hasRealSprite);
                _rawSprites = rawBmps;
                _normalPal = normalPal;
                _shinyPal = shinyPal ?? normalPal;
                _normalPalUsed = AllUsed();
                _shinyPalUsed = AllUsed();
                ApplyPalettesAndPublish();
                _dirty = false;
                ResetUndo();
                OnPropertyChanged(nameof(HasUnsavedChanges));
                StatusText = "Loaded from hg-engine source.";
            }
            catch (Exception ex)
            {
                StatusText = $"Couldn't load hg-engine sprites: {ex.Message}";
            }
            return true;
        }

        private static uint[] Pad16(uint[] palette)
        {
            if (palette.Length == 16) return palette;
            uint[] padded = new uint[16];
            Array.Copy(palette, padded, palette.Length);
            for (int i = palette.Length; i < 16; i++) padded[i] = 0xFF000000u;
            return padded;
        }
    }
}
