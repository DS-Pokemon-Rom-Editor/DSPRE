using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Platform.Storage;
using DSPRE.Avalonia;
using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;
using Avalonia.Threading;

namespace DSPRE.Avalonia.ViewModels.Trainers
{
    public partial class TrainerEditorViewModel
    {
        // Add and remove set the status. The trainer they select then loads and would replace it.
        private string _statusAfterLoad;

        /// <summary>Appends a trainer and grows every table sized by the count, all or nothing.</summary>
        public void AddTrainer()
        {
            if (HasUnsavedChanges)
            {
                _ = DialogHelper.ShowErrorAsync("Save or discard the current trainer's changes before adding another trainer.", "Trainer editor");
                return;
            }
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.Scripts });
                if (!TrainerRosterService.TryAddTrainer("Trainer", out int newIndex,
                    out string error))
                {
                    StatusText = error;
                    _ = DialogHelper.ShowErrorAsync(error, "Add trainer");
                    return;
                }

                AppEvents.RaiseNamesChanged();
                TrainerRosterAnalysis after = TrainerRosterService.AnalyzeCurrentProject();
                StatusText = _statusAfterLoad = after.CanAdd
                    ? $"Added trainer {newIndex}. {after.RemainingAdditions} more can be added safely."
                    : $"Added trainer {newIndex}. {after.RefusalReason}";
                SelectedTrainerIndex = newIndex;
            }
            catch (Exception ex) when (AppLogger.Noted("TrainerEditorViewModel.AddTrainer", ex)) { _ = DialogHelper.ShowErrorAsync($"Couldn't add trainer:\n{ex.Message}", "Trainer editor"); }
        }

        /// <summary>Removes the last added trainer once nothing refers to it.</summary>
        public async Task RemoveLastAddedTrainerAsync()
        {
            if (HasUnsavedChanges)
            {
                await DialogHelper.ShowErrorAsync(
                    "Save or discard the current trainer's changes before removing a trainer.",
                    "Trainer editor");
                return;
            }
            if (TrainerNames.Count == 0) return;

            int candidate = TrainerNames.Count - 1;
            bool confirmed = await DialogHelper.AskYesNoAsync(
                $"Remove trainer {candidate}? Only the final trainer can be removed. " +
                "DSPRE stops if an event, script, rematch table, battle message or phone book entry still uses it.",
                "Remove trainer?");
            if (!confirmed) return;

            StatusText = $"Checking references to trainer {candidate}…";
            PushLoading();
            try
            {
                bool removed = false;
                int removedId = -1;
                string error = null;
                await Task.Run(() =>
                {
                    DSUtils.TryUnpackNarcs(new List<DirNames>
                    {
                        DirNames.Scripts, DirNames.EventFiles, DirNames.TrainerTextTable,
                        DirNames.TrainerTextOffset
                    });
                    removed = TrainerRosterService.TryRemoveLastAddedTrainer(out removedId,
                        out error);
                });

                if (!removed)
                {
                    StatusText = error;
                    await DialogHelper.ShowErrorAsync(error, "Trainer editor");
                    return;
                }

                AppEvents.RaiseNamesChanged();
                TrainerRosterAnalysis after = TrainerRosterService.AnalyzeCurrentProject();
                StatusText = _statusAfterLoad = $"Removed trainer {removedId}. " +
                    $"{after.RemainingAdditions} trainer slots are available.";
                SelectedTrainerIndex = Math.Max(0, removedId - 1);
            }
            catch (Exception ex) when (AppLogger.Noted("TrainerEditorViewModel.RemoveLastAddedTrainerAsync", ex))
            {
                await DialogHelper.ShowErrorAsync($"Couldn't remove trainer:\n{ex.Message}",
                    "Trainer editor");
            }
            finally { PopLoading(); }
        }

        private static List<string> TrainerListEntries(string[] classNames) => new List<string>(DSPRE.TrainerNames.GetAll());

        private void RefreshTrainerListEntry(int id)
        {
            if (id < 0 || id >= TrainerNames.Count) return;
            string entry;
            if (IsHgeActive)
            {
                if (!HgEngineTrainerSource.TryLoad(id, out HgEngineSourceBlock block, out _)) return;
                entry = DSPRE.TrainerNames.HgEngineEntry(id, block, GetTrainerClassNames());
            }
            else
            {
                string[] classNames = GetTrainerClassNames();
                string className = _savedClass < classNames.Length ? classNames[_savedClass] : $"Class {_savedClass}";
                entry = "[" + id.ToString("D2") + "] " + className + " " + _savedName;
            }
            if (TrainerNames[id] == entry) return;
            // Replacing the selected row can clear the list's selection.
            bool wasSuppressed = _suppress;
            _suppress = true;
            try
            {
                TrainerNames[id] = entry;
                _selectedTrainerIndex = id;
                OnPropertyChanged(nameof(SelectedTrainerIndex));
            }
            finally { _suppress = wasSuppressed; }
        }

        // The error, or null when the name was written.
        private string UpdateTrainerName(string newName)
        {
            if (_trainer == null) return null;
            try
            {
                _trainer.Name = newName;
                TextArchive ta = new TextArchive(TrainerNamesMessageNumber);
                ta.SetSimpleTrainerName(_trainer.Trp.TrainerID, newName);
                ta.SaveToExpandedDir(TrainerNamesMessageNumber, showSuccessMessage: false);
                return null;
            }
            catch (Exception ex)
            {
                AppLogger.Error("Trainer name save failed: " + ex.Message);
                return ex.Message;
            }
        }
    }
}
