using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using DSPRE.Resources;
using DSPRE.Avalonia.Views.Shell;
using DSPRE.Csv;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    public partial class PersonalDataEditorViewModel
    {
        public async Task SaveCommandAsync()
        {
            if (_current == null) return;
            string sourceError = null;
            bool partlySaved = false;
            if (HgEngineProject.IsActive)
            {
                bool stop;
                (sourceError, partlySaved, stop) = await SaveHgSourceAsync();
                if (stop) return;
            }
            if (sourceError != null)
            {
                // Operations that did finish are no longer pending.
                if (partlySaved) _history.Capture(Snapshot(), coalesce: true);
                string lead = partlySaved ? $"Species {_currentId} was only partly saved:" : $"Species {_currentId} wasn't saved:";
                await DSPRE.Avalonia.DialogHelper.ShowErrorAsync($"{lead}\n{sourceError}", "Personal data editor");
                return;
            }
            if (!await SaveRetailTablesAsync()) return;
            if (!HgEngineProject.IsActive) FinishPending();
            // Side tables can change on their own, so the personal file is only written when its bytes did.
            if (_savedSnapshot?.Data == null || !_current.ToByteArray().AsSpan().SequenceEqual(_savedSnapshot.Data))
                _current.SaveToFileDefaultDir(_currentId, showSuccessMessage: false);
            AppEvents.RaisePersonalDataSaved(this, _currentId);
            // hg-engine rebuilds pms.narc from data/BabyMons.c, which the Baby Pokémon picker edits.
            if (!HgEngineProject.IsActive && (_savedSnapshot == null || _savedSnapshot.Hatch != HatchResultIndex)) HatchResultTable.Write(_currentId, HatchResultIndex);
            PersonalSnapshot saved = Snapshot();
            _history.Capture(saved, coalesce: true);
            _history.MarkSaved();
            _savedSnapshot = saved;
            _heldRecord = false;
            SetClean();
            SaveNotice.Saved(UnsavedChangesDescription);
            RaiseUndoState();
        }

        private async Task<(string sourceError, bool partlySaved, bool stop)> SaveHgSourceAsync()
        {
            string sourceError = null;
            bool partlySaved = false;
            (HgStaged _hgLoaded, int _hgLoadedBounceValue, bool _owPendingCreate, string _owPendingSpritePath, string _owPendingShinyPath) pending =
                (_hgLoaded, _hgLoadedBounceValue, _owPendingCreate, _owPendingSpritePath, _owPendingShinyPath);
            (bool committed, string commitError) = await DSPRE.Avalonia.HgEngineSave.RunAsync(() =>
            {
                sourceError = WritePendingSource();
                if (sourceError != null) return sourceError;
                sourceError = SaveHgEngineSource(out partlySaved);
                // Steps that did succeed are kept when a later one fails.
                return partlySaved ? null : sourceError;
            });
            if (committed) FinishPending();
            if (!committed)
            {
                // Nothing reached disk, so the save's own bookkeeping goes back to what is still pending.
                (_hgLoaded, _hgLoadedBounceValue, _owPendingCreate, _owPendingSpritePath, _owPendingShinyPath) = pending;
                bool wasLoading = _loading;
                _loading = true;
                LoadOwFollower();
                _loading = wasLoading;
                partlySaved = false;
                sourceError = commitError ?? sourceError;
                if (sourceError == null) return (null, false, true);
            }
            return (sourceError, partlySaved, false);
        }

        private async Task<bool> SaveRetailTablesAsync()
        {
            string athlonProblem = PokeathlonSaveProblem();
            if (athlonProblem != null)
            {
                await DSPRE.Avalonia.DialogHelper.ShowErrorAsync($"Species {_currentId} wasn't saved:\n{athlonProblem}", "Personal data editor");
                return false;
            }
            string followerError = SaveRetailFollower();
            if (followerError != null)
            {
                await DSPRE.Avalonia.DialogHelper.ShowErrorAsync($"The follower of species {_currentId} wasn't saved:\n{followerError}", "Personal data editor");
                return false;
            }
            string athlonError = SavePokeathlon();
            if (athlonError != null)
            {
                await DSPRE.Avalonia.DialogHelper.ShowErrorAsync($"The Pokéathlon stats of species {_currentId} weren't saved:\n{athlonError}", "Personal data editor");
                return false;
            }
            string maskError = SaveExtraMaskTms();
            if (maskError != null)
            {
                await DSPRE.Avalonia.DialogHelper.ShowErrorAsync($"The TM121+ compatibility of species {_currentId} wasn't saved:\n{maskError}", "Personal data editor");
                return false;
            }
            string machineError = SaveSourceMachineMoves();
            if (machineError != null)
            {
                await DSPRE.Avalonia.DialogHelper.ShowErrorAsync($"The machine moves of species {_currentId} weren't saved:\n{machineError}", "Personal data editor");
                return false;
            }
            return true;
        }

        // Writes Species.c, side tables, then follower files. Null on success. Not the machine moves.
        private string SaveHgEngineSource(out bool partlySaved)
        {
            partlySaved = false;
            if (!HgEngineProject.IsActive) return null;
            if (_hgLoadError != null) return _hgLoadError;

            HgStaged s = CaptureHg();
            HgStaged loaded = _hgLoaded ?? s;
            string invalid = ValidateHgStaged(s, loaded, _followerBounceValues.Count, _owSizeClassValues.Count, HgEngineSpeciesOwFormFemale.TryValidateRawExpression);
            if (invalid != null) return invalid;

            string templateProblem = OwTemplateProblem(s, out int? templateSpecies);
            if (templateProblem != null) return templateProblem;

            if (!HgEngineSpeciesPersonalFields.TryWriteSource(_currentId, _current, out string error))
            {
                AppLogger.Error($"hg-engine write failed for species {_currentId}: {error}");
                return error;
            }
            partlySaved = true;

            List<string> failures = new List<string>();
            WriteHgSideTables(s, ref loaded, failures);
            WriteOwFollowerFiles(s, templateSpecies, ref loaded, failures);
            _hgLoaded = loaded with { CreateOwEntry = false, OwSpritePath = null, OwShinySpritePath = null };
            HgOwFemaleFormError = OwFemaleFormProblem(_hgOwFemaleFormExpression);

            return failures.Count == 0 ? null : string.Join("\n", failures);
        }

        private string OwTemplateProblem(HgStaged s, out int? templateSpecies)
        {
            templateSpecies = null;
            if (s.OwSpritePath == null) return null;
            if (!PlainFiles.Exists(s.OwSpritePath)) return $"{Path.GetFileName(s.OwSpritePath)} wasn't found.";
            if (s.OwShinySpritePath != null && !PlainFiles.Exists(s.OwShinySpritePath)) return $"{Path.GetFileName(s.OwShinySpritePath)} wasn't found.";
            if (OwNeedsTemplate)
            {
                if (_owTemplateSpeciesIndex < 0 || _owTemplateSpeciesIndex >= PokemonNames.Count)
                    return "Pick a template species for the overworld sprite.";
                templateSpecies = _owTemplateSpeciesIndex;
            }
            return null;
        }

        private bool HgStep(bool changed, string what, Func<string> write, List<string> failures)
        {
            if (!changed) return false;
            string stepError = write();
            if (stepError == null) return true;
            AppLogger.Error($"hg-engine {what} write failed for species {_currentId}: {stepError}");
            failures.Add($"{what}: {stepError}");
            return false;
        }

        private void WriteHgSideTables(HgStaged s, ref HgStaged loaded, List<string> failures)
        {
            int id = _currentId;
            if (HgStep(s.HiddenAbility != loaded.HiddenAbility, "Hidden ability",
                    () => HgEngineHiddenAbility.TrySetAbilityId(id, s.HiddenAbility, out string e) ? null : e, failures))
                loaded = loaded with { HiddenAbility = s.HiddenAbility };
            if (HgStep(s.BaseExp != loaded.BaseExp, "Base exp",
                    () => HgEngineBaseExperience.TrySetBaseExp(id, s.BaseExp, out string e) ? null : e, failures))
                loaded = loaded with { BaseExp = s.BaseExp };
            if (HgStep(s.BabyMon != loaded.BabyMon, "Baby Pokémon",
                    () => HgEngineBabyMon.TrySetBabySpecies(id, s.BabyMon, out string e) ? null : e, failures))
                loaded = loaded with { BabyMon = s.BabyMon };
            if (HgStep(s.RegionalDex != loaded.RegionalDex, "Regional Dex number",
                    () => HgEngineRegionalDex.TrySetDexNumber(id, s.RegionalDex, out string e) ? null : e, failures))
                loaded = loaded with { RegionalDex = s.RegionalDex };
            // The Battle Display tab writes this table too, and also only when its own value changed.
            if (HgStep(s.IconPalette != loaded.IconPalette, "Icon palette",
                    () => HgEngineIconPalette.TrySetPaletteId(id, s.IconPalette, out string e) ? null : e, failures))
                loaded = loaded with { IconPalette = s.IconPalette };

            int bounceValue = s.FollowerBounceIndex == loaded.FollowerBounceIndex ? _hgLoadedBounceValue : _followerBounceValues[s.FollowerBounceIndex].Value;   // keeps the file's bounce when it isn't a named option
            if (HgStep(s.FollowerSize != loaded.FollowerSize || s.FollowerBounceIndex != loaded.FollowerBounceIndex, "Follower",
                    () => HgEngineFollowerProperties.TrySet(id, s.FollowerSize, bounceValue, out string e) ? null : e, failures))
            {
                loaded = loaded with { FollowerSize = s.FollowerSize, FollowerBounceIndex = s.FollowerBounceIndex };
                _hgLoadedBounceValue = bounceValue;
            }
            if (HgStep(s.OwFemaleForm != loaded.OwFemaleForm, "Female overworld form",
                    () => HgEngineSpeciesOwFormFemale.TrySetRawExpression(id, s.OwFemaleForm, out string e) ? null : e, failures))
                loaded = loaded with { OwFemaleForm = s.OwFemaleForm };
            if (HgStep(_owHasEntry && s.OwSizeClassIndex != loaded.OwSizeClassIndex, "Size Class",
                    () => HgEngineOverworldFollowerSprite.TrySetSizeClass(id, _owSizeClassValues[s.OwSizeClassIndex], out string e) ? null : e, failures))
                loaded = loaded with { OwSizeClassIndex = s.OwSizeClassIndex };
        }

        private void WriteOwFollowerFiles(HgStaged s, int? templateSpecies, ref HgStaged loaded, List<string> failures)
        {
            int id = _currentId;
            bool followerFilesChanged = false;
            if (HgStep(s.CreateOwEntry, "Overworld follower entry",
                    () => HgEngineOverworldFollowerSprite.TryEnsureEntry(id, out _, out string e) ? null : e, failures))
            { _owPendingCreate = false; followerFilesChanged = true; }
            string importWarning = null;
            if (HgStep(s.OwSpritePath != null, "Overworld sprite",
                    () => OverworldSourceFiles.ImportFollower(id, s.OwSpritePath, s.OwShinySpritePath, templateSpecies, out importWarning), failures))
            { _owPendingSpritePath = null; _owPendingShinyPath = null; followerFilesChanged = true; }

            if (followerFilesChanged)
            {
                bool wasLoading = _loading;
                _loading = true;
                LoadOwFollower();
                _loading = wasLoading;
                loaded = loaded with { OwSizeClassIndex = _owSizeClassIndex };
            }
            if (importWarning != null)
            {
                OwStatusText = importWarning;
                SaveNotice.Show(importWarning);
            }
        }
    }
}
