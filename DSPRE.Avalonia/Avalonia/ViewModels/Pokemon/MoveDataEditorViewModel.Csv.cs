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
        public async Task ExportCommandAsync(Window owner)
        {
            string path = await DialogHelper.SaveFileAsync(owner, "Export move data to CSV",
                new[] { DialogHelper.CsvFilter, DialogHelper.AllFilter }, "MoveData.csv");
            if (path == null) return;

            try
            {
                string[] typeNames = GetTypeNames();
                // What is on disk, without this editor's unsaved moves.
                string[] names = RomInfo.GetAttackNames();
                SortedDictionary<int, MoveData> moves = new SortedDictionary<int, MoveData>();
                List<string> skipped = new List<string>();
                if (HgEngineProject.IsActive)
                {
                    if (!HgEngineMoveSource.TryLoadMany(Enumerable.Range(0, names.Length), MoveData.BuiltOrBlank, out moves, out skipped, out string loadError))
                    {
                        await DialogHelper.ShowErrorAsync($"Couldn't export:\n{loadError}", "Move data editor");
                        return;
                    }
                }
                else
                {
                    for (int i = 0; i < names.Length; i++) moves[i] = new MoveData(i);
                }

                using (StreamWriter writer = new StreamWriter(PlainFiles.Create(path)))
                    MoveDataCsv.Write(writer, names, typeNames, moves.Select(kv => (kv.Key, kv.Value)));

                SaveNotice.Exported(path);
                if (skipped.Count > 0)
                    await DialogHelper.ShowInfoAsync($"{skipped.Count} moves couldn't be read and were left out:\n" + string.Join("\n", skipped.Take(10))
                        + (skipped.Count > 10 ? $"\n...and {skipped.Count - 10} more" : ""), "Moves left out");
            }
            catch (Exception ex) when (AppLogger.Noted("MoveDataEditorViewModel.ExportCommandAsync", ex))
            {
                await DialogHelper.ShowErrorAsync($"Couldn't export:\n{ex.Message}", "Move data editor");
            }
        }

        public async Task ImportCommandAsync(Window owner)
        {
            if (_pendingMove != null)
            {
                await DialogHelper.ShowErrorAsync("Save or discard the new move first.", "Import CSV", owner);
                return;
            }
            // Compare against unsaved imports too, so a second import shows the right changes.
            MoveDataCsv importer = new MoveDataCsv(MoveNames.ToArray(), GetTypeNames(), id =>
            {
                if (id == _currentId && _currentFile != null) return (_currentFile, SourceLoadError);
                if (_pendingImports.TryGetValue(id, out MoveData staged)) return (staged, null);
                try
                {
                    MoveData move = LoadRecord(id, out string loadError);
                    return (move, loadError);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { return (null, ex.Message); }
            });
            CsvImportSession session = await CsvImportReviewView.ReviewAsync(owner, importer);
            if (session == null) return;
            List<MoveDataCsv.Entry> entries = session.Accepted.Select(r => (MoveDataCsv.Entry)r.Value).ToList();
            // A row that only renames leaves the move data as it is, so it stages nothing.
            List<MoveDataImportEntry> changed = entries.Select(e => e.Move).Where(DataDiffers).ToList();
            if (changed.Count > 0) await StageImportedDataAsync(changed);
            SaveNotice.Imported($"{entries.Count} moves", session.FileName);
            if (!HgEngineProject.IsActive) await OfferRenamesAsync(owner, entries.Where(e => e.NameDiffers).ToList());
        }

        private bool DataDiffers(MoveDataImportEntry e)
        {
            MoveData m;
            if (e.MoveID == _currentId && _currentFile != null) m = _currentFile;
            else if (!_pendingImports.TryGetValue(e.MoveID, out m))
            {
                try { m = LoadRecord(e.MoveID, out string loadError); if (loadError != null) return true; }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { return true; }
            }
            return m.Movetype != e.MoveType || m.Split != e.Split || m.Damage != e.Power || m.Accuracy != e.Accuracy || m.Priority != e.Priority
                || m.SideEffectProbability != e.SideEffectProbability || m.Pp != e.PP || m.Target != e.Range;
        }

        // Names live in a text archive, so a rename is written at once, as the Text editor would.
        private async Task OfferRenamesAsync(Window owner, List<MoveDataCsv.Entry> differing)
        {
            if (differing.Count == 0) return;
            (string Label, bool IsChecked)[] options = differing
                .Select(e => ($"{e.Move.MoveID}: {e.Move.MoveName} to {e.CsvName}", false))
                .Append(("Title Case (Water Gun, not WATER GUN)", true)).ToArray();
            (bool yes, bool[] ticked) = await DialogHelper.AskWithChecksAsync(
                "The file names these moves differently. Rename the ticked ones in the ROM? Names are saved now.",
                "Rename moves", "Rename", "Keep ROM names", options, owner);
            if (!yes) return;
            bool titleCase = ticked[^1];
            try
            {
                TextArchive names = new TextArchive(AttackNamesTextNumber);
                int renamed = 0;
                for (int i = 0; i < differing.Count; i++)
                {
                    int id = differing[i].Move.MoveID;
                    if (!ticked[i] || id < 0 || id >= names.Messages.Count) continue;
                    string name = differing[i].CsvName;
                    names.Messages[id] = titleCase ? System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name.ToLowerInvariant()) : name;
                    renamed++;
                }
                if (renamed == 0) return;
                names.SaveToExpandedDir(AttackNamesTextNumber, showSuccessMessage: false);
                SyncNames();
                AppEvents.RaiseNamesChanged();
                StatusText = renamed == 1 ? "Renamed 1 move." : $"Renamed {renamed} moves.";
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                await DialogHelper.ShowErrorAsync("Renaming failed:\n" + ex.Message, "Rename moves", owner);
            }
        }

        // Imported values wait until Save or Discard. The current move takes them and restarts its undo.
        private async Task StageImportedDataAsync(List<MoveDataImportEntry> entries)
        {
            int staged = 0;
            bool currentChanged = false;
            List<string> failures = new List<string>();
            foreach (MoveDataImportEntry e in entries)
            {
                MoveData move;
                if (e.MoveID == _currentId && _currentFile != null)
                {
                    if (SourceLoadError != null) { failures.Add($"Move {e.MoveID}: {SourceLoadError}"); continue; }
                    move = _currentFile;
                    currentChanged = true;
                }
                else if (!_pendingImports.TryGetValue(e.MoveID, out move))
                {
                    try
                    {
                        // Effect, flags and contest aren't in the CSV, so on hg-engine they come from Moves.c.
                        move = LoadRecord(e.MoveID, out string loadError);
                        if (loadError != null) { failures.Add($"Move {e.MoveID}: {loadError}"); continue; }
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error($"Couldn't read move {e.MoveID}: {ex.Message}");
                        failures.Add($"Move {e.MoveID}: {ex.Message}");
                        continue;
                    }
                }
                move.Movetype  = e.MoveType;
                move.Split     = e.Split;
                move.Damage    = e.Power;
                move.Accuracy  = e.Accuracy;
                move.Priority  = e.Priority;
                move.SideEffectProbability = e.SideEffectProbability;
                move.Pp        = e.PP;
                move.Target    = e.Range;
                if (!ReferenceEquals(move, _currentFile)) _pendingImports[e.MoveID] = move;
                staged++;
            }

            if (currentChanged)
            {
                _pendingImports[_currentId] = Copy(_currentFile);
                _loading = true;
                PopulateFromCurrentFile();
                _loading = false;
                _dirty = false;
                _history.Reset(Snapshot());
                _lastCaptureUtc = System.DateTime.MinValue;
                RaiseUndoState();
            }
            StatusText = _pendingImports.Count == 0 ? string.Empty
                : _pendingImports.Count == 1 ? "1 imported move has unsaved changes. Press Save to write them to disk." : $"{_pendingImports.Count} imported moves have unsaved changes. Press Save to write them to disk.";
            RefreshDirty();

            if (failures.Count == 0) return;
            string listed = string.Join("\n", failures.Take(10)) + (failures.Count > 10 ? $"\n...and {failures.Count - 10} more" : "");
            await DialogHelper.ShowErrorAsync($"{Plural.Of(staged, "move")} imported, {failures.Count} not:\n{listed}", "Move data editor");
        }
    }
}
