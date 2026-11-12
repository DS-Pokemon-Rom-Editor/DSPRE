using DSPRE.Avalonia;
using DSPRE.Avalonia.Gl;
using DSPRE.Editors;
using DSPRE.HgEngine;
using AvaloniaEdit.Document;
using global::Avalonia.Controls;
using global::Avalonia.Platform.Storage;
using global::Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static DSPRE.RomInfo;
using static MKDS_Course_Editor.NSBTP.NSBTP.NSBTP_File;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.ViewModels.Text
{
    public partial class ScriptEditorViewModel
    {
        /// <summary>Removes the last script file, source and binary, unless something needs it.</summary>
        public async Task RemoveLastScriptFileAsync()
        {
            int id = NextScriptId() - 1;
            if (id < 0) return;
            if (HasUnsavedChanges)
            {
                await DialogHelper.ShowInfoAsync("Save or discard the open script first.", "Script editor");
                return;
            }
            // The check also counts headers loading the file as their level script.
            if (NewProjectFiles.WhyKeepLastScriptFile(id) is string keep) { await DialogHelper.ShowInfoAsync(keep, "Script editor"); return; }
            if (!await DialogHelper.AskYesNoAsync($"Delete script file {id}, its source and its compiled copy?", "Delete script file?")) return;
            try
            {
                string source = _sourceFiles.FirstOrDefault(f => _scriptIdByPath.TryGetValue(f, out int x) && x == id);
                NewProjectFiles.DeleteScriptFile(id);
                bool wasShown = SelectedScriptIndex >= 0 && SelectedScriptIndex < _sourceFiles.Count && ProjectPaths.Same(_sourceFiles[SelectedScriptIndex], source);
                RefreshScriptList();
                if (wasShown || SelectedScriptIndex >= _sourceFiles.Count) SelectedScriptIndex = _sourceFiles.Count - 1;
                StatusText = $"Removed script file {id}.";
                OnPropertyChanged(nameof(CanRemoveLast));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                await DialogHelper.ShowErrorAsync("Couldn't remove the script file:" + Environment.NewLine + ex.Message, "Script editor");
            }
        }
        public bool CanSearchProject => !IsBusy && _sourceFiles.Count > 0 && !string.IsNullOrWhiteSpace(SearchText);
        public bool HasDiagnostics => Diagnostics.Count > 0;
        public bool HasUnsavedChanges => _dirty;
        public string SelectedScriptPath => _currentPath;
        public string UnsavedChangesDescription =>
            ManagedSource != null ? "hg-engine script " + ManagedSource.RelPath
            : _currentPath == null ? "Rotom script"
            : "Rotom script " + DisplayPath(_currentPath);

        /// <summary>Set while the selected entry is a script hg-engine assembles from source.</summary>
        public HgEngineOwnedFile ManagedSource =>
            _currentPath != null && _managedByPath.TryGetValue(_currentPath, out HgEngineOwnedFile f) ? f : null;

        public bool IsManagedByHgEngine => ManagedSource != null;

        public string ManagedNote => ManagedSource == null
            ? ""
            : $"hg-engine assembles script {ManagedSource.Id} from {ManagedSource.RelPath}. " +
              "Editing it here edits that file, and Rotom doesn't read it.";

        private async Task ShowManagedSaveNoticeAsync(HgEngineOwnedFile source)
        {
            if (HgEngineProject.SuppressManagedFileSaveNotice) return;

            bool never = await DialogHelper.ShowNoticeWithOptOutAsync(
                $"{source.RelPath} is saved. hg-engine builds this file, so compile the ROM to see the change in game.",
                "Managed by hg-engine");
            if (never) HgEngineProject.SuppressManagedFileSaveNoticeForProject();
        }

        public string StatusText
        {
            get => _statusText;
            set => Set(ref _statusText, value);
        }

        // Notes that need no answer, shown by the status line and not in a dialog.
        private string _noteText = "", _noteDetail = "";
        public string NoteText { get => _noteText; private set { if (Set(ref _noteText, value)) OnPropertyChanged(nameof(HasNote)); } }
        public string NoteDetail { get => _noteDetail; private set => Set(ref _noteDetail, value); }
        public bool HasNote => NoteText.Length > 0;

        private void AddNote(string shortText, string detail)
        {
            NoteText = NoteText.Length == 0 ? shortText : NoteText + "  ·  " + shortText;
            NoteDetail = NoteDetail.Length == 0 ? detail : NoteDetail + "\n\n" + detail;
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (!Set(ref _searchText, value)) return;
                OnPropertyChanged(nameof(CanSearchProject));
            }
        }

        public bool SearchFuzzy
        {
            get => _searchFuzzy;
            set => Set(ref _searchFuzzy, value);
        }

        public string SearchStatusText
        {
            get => _searchStatusText;
            set => Set(ref _searchStatusText, value);
        }

        public ScriptSearchResult SelectedSearchResult
        {
            get => _selectedSearchResult;
            set => Set(ref _selectedSearchResult, value);
        }

        public string DiagnosticsStatusText
        {
            get => _diagnosticsStatusText;
            set => Set(ref _diagnosticsStatusText, value);
        }

        public ScriptDiagnostic SelectedDiagnostic
        {
            get => _selectedDiagnostic;
            set => Set(ref _selectedDiagnostic, value);
        }

        public int SelectedSidebarTabIndex
        {
            get => _selectedSidebarTabIndex;
            set => Set(ref _selectedSidebarTabIndex, value);
        }

        public void AddScriptFile()
        {
            try
            {
                (int _, string path) = NewProjectFiles.AddScript();
                RefreshScriptList();
                SelectedScriptIndex = _sourceFiles.FindIndex(p => ProjectPaths.Same(p, path));
                StatusText = "Added " + DisplayPath(path) + ".";
            }
            catch (Exception ex) when (AppLogger.Noted("ScriptEditorViewModel.AddScriptFile", ex))
            {
                _ = DialogHelper.ShowErrorAsync("Couldn't add Rotom script:\n" + ex.Message, "Script editor");
            }
        }

        public async Task ImportAsync()
        {
            if (!IsEditable) return;

            FilePickerFileType filter = new FilePickerFileType("Rotom source") { Patterns = new[] { "*.rotom", "*.json", "*.*" } };
            string path = await DialogHelper.OpenFileAsync(_owner, "Import Rotom source", new[] { filter });
            if (path == null) return;

            try
            {
                // Stays in the editor until Save, so Discard can still take it back.
                ScriptText = RotomTool.ReadSource(path);
                SaveNotice.Imported(ManagedSource?.RelPath ?? DisplayPath(_currentPath), path);
            }
            catch (Exception ex) when (AppLogger.Noted("ScriptEditorViewModel.ImportAsync", ex))
            {
                await DialogHelper.ShowErrorAsync("Couldn't import:\n" + ex.Message, "Script editor");
            }
        }

        public async Task ExportAsync()
        {
            if (_currentPath == null) return;

            string extension = Path.GetExtension(_currentPath);
            FilePickerFileType filter = new FilePickerFileType("Rotom source") { Patterns = new[] { "*.rotom", "*.json", "*.*" } };
            string path = await DialogHelper.SaveFileAsync(_owner, "Export Rotom source", new[] { filter },
                Path.GetFileNameWithoutExtension(_currentPath) + extension);
            if (path == null) return;

            try
            {
                PlainFiles.WriteText(path, ScriptText ?? "");
                SaveNotice.Exported(path);
            }
            catch (Exception ex) when (AppLogger.Noted("ScriptEditorViewModel.ExportAsync", ex))
            {
                await DialogHelper.ShowErrorAsync("Couldn't export:\n" + ex.Message, "Script editor");
            }
        }

        public void SaveChanges() => DialogHelper.Watch(SaveAsync());

        async Task<bool> IEditorWithUnsavedChanges.SaveChangesAsync()
        {
            // A failed compile marks the editor dirty again, so a true result alone is not enough.
            bool ok = await SaveAsync();
            return ok && !HasUnsavedChanges;
        }

        public void DiscardChanges()
        {
            SetClean();
            LoadSelectedFile();
        }

        // A project last built by another rotom is rebuilt in full, stale sources regenerated first.
        private async Task UpgradeRotomProjectAsync()
        {
            RotomProjectUpgrade.Assessment assessment = await DSPRE.ROMFiles.RotomProjectUpgrade.AssessAsync();
            if (assessment == null) return;

            bool keepBinaries = true;
            if (assessment.BothChanged.Count > 0)
            {
                string files = string.Join(", ", assessment.BothChanged.Select(i => i.ToString("D4")));
                keepBinaries = await DialogHelper.AskTwoWayAsync(
                    $"These scripts changed both in the game data and in their source since they were last compiled: {files}. Which should win?",
                    "Update Rotom project", "Game data", "Source");
            }
            StatusText = "Updating the Rotom project...";
            (string problem, List<int> kept) = await DSPRE.ROMFiles.RotomProjectUpgrade.ApplyAsync(assessment, keepBinaries);
            if (problem != null) await DialogHelper.ShowErrorAsync(problem, "Update Rotom project");
            else if (kept.Count > 0) AddNote(KeptBinariesNote(kept), KeptBinariesMessage(kept));
            RefreshScriptList();
        }

        private static string KeptBinariesNote(List<int> kept)
            => (kept.Count == 1 ? "Script " : "Scripts ") + string.Join(", ", kept.Select(i => i.ToString("D4"))) + " keep the game's bytes";

        private static string OwnedEditsMessage(List<int> ids)
        {
            string list = string.Join(", ", ids.Select(i => i.ToString("D4")));
            return ids.Count == 1
                ? $"hg-engine builds script {list} from its own source, so the edit to it wasn't compiled in. Edit it in the hg-engine checkout."
                : $"hg-engine builds scripts {list} from its own source, so the edits to them weren't compiled in. Edit them in the hg-engine checkout.";
        }

        private static string KeptBinariesMessage(List<int> kept)
        {
            string ids = string.Join(", ", kept.Select(i => i.ToString("D4")));
            return kept.Count == 1
                ? $"Rotom builds script {ids} differently from the game, although nobody edited it, so the game's version was kept. " +
                  "Its source doesn't build back to those bytes, so editing it here would change more than the edit."
                : $"Rotom builds scripts {ids} differently from the game, although nobody edited them, so the game's versions were kept. " +
                  "Their sources don't build back to those bytes, so editing them here would change more than the edit.";
        }

        // Binaries with no source after a decompile are the ones rotom could not read.
        private string UndecompiledScripts()
        {
            List<int> left = RotomTool.BinariesWithoutSource(new HashSet<int>(_scriptIdByPath.Values));
            if (left.Count == 0) return null;
            if (left.Count == 1) return $"Script {left[0]} couldn't be decompiled and keeps its binary.";
            string ids = string.Join(", ", left.Take(10)) + (left.Count > 10 ? $" and {left.Count - 10} more" : "");
            return $"Scripts {ids} couldn't be decompiled and keep their binaries.";
        }

        private async Task RunRequiredRotomCommandAsync(params string[] args)
        {
            RotomTool.Result result = await RotomTool.RunAsync(args);
            if (!result.Success)
                throw new InvalidOperationException("rotom " + string.Join(" ", args) + " failed:\n" + RotomTool.FormatDetails(result));
        }
    }
}
