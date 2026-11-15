using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using DSPRE.Resources;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;
using static DSPRE.MoveData;
using static DSPRE.RomInfo;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.Views.Shell;
using DSPRE.Csv;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    public partial class MoveDataEditorViewModel
    {

        public async Task SaveCommandAsync()
        {
            if (_currentFile == null) return;
            if (HgEngineProject.IsActive && SourceLoadError != null)
            {
                await DialogHelper.ShowErrorAsync($"Move {_currentId} wasn't saved.\n{SourceLoadError}", "Move data editor");
                return;
            }
            int id = _currentId;
            MoveData move = _currentFile;
            HgEngineMoveExpansion.PendingMove pending = _pendingMove;
            // The shown record wins over an import staged for the same move.
            Dictionary<int, MoveData> records = new Dictionary<int, MoveData>(_pendingImports) { [id] = move };
            string subject = pending != null ? pending.DisplayName : records.Count > 1 ? $"{records.Count} moves" : $"Move {id}";

            // The next sync rebuilds from the source, so a save that can't write it fails.
            if (HgEngineProject.IsActive)
            {
                (bool saved, string error) = await HgEngineSave.RunAsync(() => pending != null
                    ? (HgEngineMoveExpansion.TryCommitMove(pending, move, out string addError) ? null : addError)
                    : (HgEngineMoveSource.TryWriteMany(records, out string writeError) ? WriteCategories() : writeError));
                if (!saved)
                {
                    if (error != null) await DialogHelper.ShowErrorAsync($"{subject} not saved.\n{error}", "Move data editor");
                    return;
                }
            }

            _pendingImports.Clear();
            StatusText = string.Empty;
            if (pending != null)
            {
                HgEngineMoveExpansion.CompleteAdd(pending);
                _pendingMove = null;
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.MoveData });
                move.SaveToFileDefaultDir(id, showSuccessMessage: false);
                SyncNames();
                AppEvents.RaiseNamesChanged();
                _selectedMoveIndex = id;
                OnPropertyChanged(nameof(SelectedMoveIndex));
                LoadMove(id);
            }
            else
            {
                foreach ((int importId, MoveData imported) in records)
                    if (importId != id) imported.SaveToFileDefaultDir(importId, showSuccessMessage: false);
                move.SaveToFileDefaultDir(id, showSuccessMessage: false);
                if (SaveTexts() is string textError)
                {
                    await DialogHelper.ShowErrorAsync($"The text of move {id} wasn't saved.\n{textError}", "Move data editor");
                    return;
                }
                if (!HgEngineProject.IsActive && WriteCategories() is string listError)
                {
                    await DialogHelper.ShowErrorAsync($"Move {id} was saved, but its punching or sound listing wasn't.\n{listError}", "Move data editor");
                    return;
                }
                MarkCategoriesSaved();
                _history.MarkSaved();   // the on-disk baseline now; undo can still go past it
            }
            SetClean();
            MoveInfo.Forget();
            SaveNotice.Saved(UnsavedChangesDescription);
            RaiseUndoState();
        }

        // On hg-engine the move comes from Moves.c. A field its entry lacks fails the save.
        private static MoveData LoadRecord(int id, out string error)
        {
            error = null;
            if (!HgEngineProject.IsActive) return new MoveData(id);
            MoveData move = MoveData.BuiltOrBlank(id);
            HgEngineMoveSource.TryLoad(id, move, out error);
            return move;
        }

        /// <summary>hg-engine only: makes a new move and opens it, unwritten until Save.</summary>
        public async Task AddNewMoveAsync(Window owner)
        {
            if (!HgEngineProject.IsActive) return;
            if (_pendingMove != null || _pendingImports.Count > 0)
            {
                string first = _pendingMove != null ? "Save or discard the new move first." : "Save or discard the imported moves first.";
                await DialogHelper.ShowErrorAsync(first, "Add new move", owner);
                return;
            }
            if (!await RecordSwitchGuard.ConfirmLeaveAsync(this, owner, "move", "Save them before adding a new move?")) return;

            string name = await DialogHelper.PromptTextAsync("New move's display name:", "Add new move", owner: owner);
            if (name == null) return;

            MoveData move = new MoveData(new MemoryStream(new byte[16]));
            if (!HgEngineMoveExpansion.TryPrepareMove(name, out HgEngineMoveExpansion.PendingMove pending, out string error)
                || !HgEngineMoveExpansion.TryReadTemplate(pending, move, out error))
            {
                await DialogHelper.ShowErrorAsync($"Couldn't add the move:\n{error}", "Add new move", owner);
                return;
            }

            _returnIndex = _selectedMoveIndex;
            _pendingMove = pending;
            SyncNames();

            _loading = true;
            _currentId = pending.Id;
            _currentFile = move;
            SourceLoadError = null;
            PopulateFromCurrentFile();
            _loading = false;
            _selectedMoveIndex = pending.Id;
            OnPropertyChanged(nameof(SelectedMoveIndex));

            _dirty = false;
            LoadTexts();
            _history.Reset(Snapshot());
            _lastCaptureUtc = System.DateTime.MinValue;
            RefreshDirty();
            RaiseUndoState();
        }
    }
}
