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
        // Offers the copy only when one gender's back and front are stubs and the other's are real.
        private void UpdateOppositeGenderGap(bool[] hasRealSprite)
        {
            bool femaleReal = hasRealSprite[0] && hasRealSprite[2];
            bool femaleMissing = !hasRealSprite[0] && !hasRealSprite[2];
            bool maleReal = hasRealSprite[1] && hasRealSprite[3];
            bool maleMissing = !hasRealSprite[1] && !hasRealSprite[3];

            if (maleReal && femaleMissing)
            {
                _missingGenderIsFemale = true;
                CanAddOppositeGenderSprites = true;
                AddOppositeGenderLabel = "Add female sprites (copied from male)";
            }
            else if (femaleReal && maleMissing)
            {
                _missingGenderIsFemale = false;
                CanAddOppositeGenderSprites = true;
                AddOppositeGenderLabel = "Add male sprites (copied from female)";
            }
            else
            {
                CanAddOppositeGenderSprites = false;
                AddOppositeGenderLabel = "";
            }
            _genderGapAtLoad = CanAddOppositeGenderSprites;
        }

        /// <summary>Copies the other gender's sprites in; hg-engine writes now, else Save does.</summary>
        public async Task AddOppositeGenderSpritesAsync(Window owner)
        {
            if (!CanAddOppositeGenderSprites || _currentId <= 0) return;

            string missingGender = _missingGenderIsFemale ? "female" : "male";
            string sourceGender = _missingGenderIsFemale ? "male" : "female";
            bool confirmed = await DialogHelper.AskYesNoAsync(
                $"This Pokémon has no {missingGender} sprites. " +
                $"Copy its {sourceGender} back and front sprites into the empty slots?\n\n" +
                "That way a gender ratio change won't leave it blank.",
                "Add opposite gender sprites", owner);
            if (!confirmed) return;

            if (HgEngineProject.IsActive) { AddOppositeGenderToHgEngineSource(missingGender, sourceGender); return; }

            try
            {
                DSPRE.DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.PokemonBattleSprites, DirNames.PokeHeight });
                (int srcBack, int srcFront, int dstBack, int dstFront) = GenderSlots();
                int baseOffset = _currentId * 6;

                const int FB = 0, MB = 1, FF = 2, MF = 3;   // heights are copied too, or the new gender draws at the wrong Y
                int heightBase = _currentId * 4;
                int srcBackH = heightBase + (_missingGenderIsFemale ? MB : FB);
                int srcFrontH = heightBase + (_missingGenderIsFemale ? MF : FF);
                int dstBackH = heightBase + (_missingGenderIsFemale ? FB : MB);
                int dstFrontH = heightBase + (_missingGenderIsFemale ? FF : MF);
                byte[] heightBack = _pending.ReadMember(DirNames.PokeHeight, srcBackH);
                byte[] heightFront = _pending.ReadMember(DirNames.PokeHeight, srcFrontH);

                _pending.Change(step =>
                {
                    step.Put(DirNames.PokemonBattleSprites, dstBack, _pending.ReadMember(DirNames.PokemonBattleSprites, srcBack));
                    step.Put(DirNames.PokemonBattleSprites, dstFront, _pending.ReadMember(DirNames.PokemonBattleSprites, srcFront));
                    if (heightBack != null) step.Put(DirNames.PokeHeight, dstBackH, heightBack);
                    if (heightFront != null) step.Put(DirNames.PokeHeight, dstFrontH, heightFront);
                });
                _rawSprites[dstBack - baseOffset] = (byte[])_rawSprites[srcBack - baseOffset]?.Clone();
                _rawSprites[dstFront - baseOffset] = (byte[])_rawSprites[srcFront - baseOffset]?.Clone();
                ShowGenderGap();
                ApplyPalettesAndPublish();
                Edited();
                OnPropertyChanged(nameof(HasUnsavedChanges));
                StatusText = $"Added {missingGender} sprites, copied from {sourceGender}.";
            }
            catch (Exception ex)
            {
                StatusText = $"Couldn't add {missingGender} sprites: {ex.Message}";
            }
        }

        // Archive member ids, not slots: the existing gender's back and front, then the missing one's.
        private (int srcBack, int srcFront, int dstBack, int dstFront) GenderSlots()
        {
            int baseOffset = _currentId * 6;
            return (baseOffset + (_missingGenderIsFemale ? 1 : 0), baseOffset + (_missingGenderIsFemale ? 3 : 2),
                    baseOffset + (_missingGenderIsFemale ? 0 : 1), baseOffset + (_missingGenderIsFemale ? 2 : 3));
        }

        private bool _genderGapAtLoad;   // a gender was missing at load; the button stays while no copy is pending

        private void ShowGenderGap()
        {
            bool pending = _genderGapAtLoad && _pending.Changes(DirNames.PokemonBattleSprites, GenderSlots().dstBack);
            CanAddOppositeGenderSprites = _genderGapAtLoad && !pending;
            AddOppositeGenderLabel = !CanAddOppositeGenderSprites ? ""
                : _missingGenderIsFemale ? "Add female sprites (copied from male)" : "Add male sprites (copied from female)";
            OnPropertyChanged(nameof(CanUseFullSheet));
        }

        private const string NoHgEngineSpritesNote =
            "Not saved: pokegra.mk has no sprites (scripts/reformat_sprite_data.py adds them).";

        // hg-engine keeps a missing gender as empty PNGs. Its HeightTable.c offsets are copied too.
        private void AddOppositeGenderToHgEngineSource(string missingGender, string sourceGender)
        {
            string[] poses = HgEnginePokemonBattleSprites.TryGetPosePaths(_currentId);
            if (poses == null) { StatusText = NoHgEngineSpritesNote; return; }
            int srcBack = _missingGenderIsFemale ? 1 : 0, dstBack = _missingGenderIsFemale ? 0 : 1;   // slots: FemaleBack, MaleBack, FemaleFront, MaleFront
            int srcFront = _missingGenderIsFemale ? 3 : 2, dstFront = _missingGenderIsFemale ? 2 : 3;
            try
            {
                ArchiveMembers.Write(poses[dstBack], DSUtils.ReadFromFile(poses[srcBack]));
                ArchiveMembers.Write(poses[dstFront], DSUtils.ReadFromFile(poses[srcFront]));
                HgEnginePokemonBattleSprites.EnsurePoseKey(poses, dstBack);
                HgEnginePokemonBattleSprites.EnsurePoseKey(poses, dstFront);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                StatusText = $"Couldn't add {missingGender} sprites: {ex.Message}";
                return;
            }

            string heightNote = "";
            if (HgEngineHeightTable.TryGet(_currentId, out int fb, out int mb, out int ff, out int mf))
            {
                if (_missingGenderIsFemale) { fb = mb; ff = mf; } else { mb = fb; mf = ff; }
                if (!HgEngineHeightTable.TrySet(_currentId, fb, mb, ff, mf, out string error)) heightNote = $" HeightTable.c wasn't updated: {error}";
            }
            StatusText = $"Added {missingGender} sprites, copied from {sourceGender}." + heightNote;
            LoadMon(_currentId);
        }
    }
}
