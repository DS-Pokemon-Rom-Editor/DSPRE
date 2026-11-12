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
        private void RefreshScriptList()
        {
            _sourceFiles.Clear();
            ScriptNames.Clear();

            _sourceFiles.AddRange(RotomTool.SourceFiles());

            _sourceFiles.Sort(StringComparer.OrdinalIgnoreCase);
            _rotomFileCount = _sourceFiles.Count;

            _managedByPath.Clear();
            _scriptIdByPath.Clear();
            foreach (string file in _sourceFiles)
            {
                if (int.TryParse(Path.GetFileNameWithoutExtension(file), out int rotomId))
                    _scriptIdByPath[file] = rotomId;
            }

            IOrderedEnumerable<HgEngineOwnedFile> ownedScripts = HgEngineOwnedFiles
                .FilesIn(HgEngineOwnedFiles.ArchiveOf(DirNames.Scripts)).Values
                .Where(f => f.Ownership == HgEngineOwnership.EditableSource)
                .OrderBy(f => f.Id);
            foreach (HgEngineOwnedFile owned in ownedScripts)
            {
                // hg-engine builds this script from its source, so the source takes the decompiled copy's place.
                int at = _sourceFiles.FindIndex(path =>
                    _scriptIdByPath.TryGetValue(path, out int id) && id == owned.Id);
                _managedByPath[owned.FullPath] = owned;
                _scriptIdByPath[owned.FullPath] = owned.Id;
                if (at >= 0) { _sourceFiles[at] = owned.FullPath; continue; }

                int after = _sourceFiles.FindIndex(path =>
                    _scriptIdByPath.TryGetValue(path, out int id) && id > owned.Id);
                if (after < 0) _sourceFiles.Add(owned.FullPath);
                else _sourceFiles.Insert(after, owned.FullPath);
            }

            foreach (string file in _sourceFiles)
            {
                ScriptNames.Add(_managedByPath.TryGetValue(file, out HgEngineOwnedFile owned)
                    ? $"{owned.RelPath}   [hg-engine]"
                    : DisplayPath(file));
            }

            // Clearing the names resets the list's selection, and an added file can shift the rest.
            if (_currentPath != null)
            {
                int current = _sourceFiles.FindIndex(p => ProjectPaths.Same(p, _currentPath));
                if (current >= 0 && current != _selectedIndex)
                {
                    _selectedIndex = current;
                    OnPropertyChanged(nameof(SelectedScriptIndex));
                    OnEditorStateChanged();
                }
            }

            SearchResults.Clear();
            SelectedSearchResult = null;
            OnPropertyChanged(nameof(CanSearchProject));
        }

        private int InitialSelection()
        {
            int exact = IndexOfScriptFile(InitialIndex);
            if (exact >= 0) return exact;
            return Math.Min(Math.Max(0, InitialIndex), _sourceFiles.Count - 1);
        }

        // Sorted by path with hg-engine sources slotted in by id, so a position is not a file number.
        private int IndexOfScriptFile(int fileId) => _sourceFiles.FindIndex(path =>
            _scriptIdByPath.TryGetValue(path, out int id) && id == fileId);

        /// <summary>Whether script file <paramref name="fileId"/> is the one showing.</summary>
        public bool IsShowingScriptFile(int fileId) => _selectedIndex >= 0 && IndexOfScriptFile(fileId) == _selectedIndex;

        /// <summary>The script file number showing, or -1.</summary>
        public int ShowingScriptFileId =>
            _selectedIndex >= 0 && _selectedIndex < _sourceFiles.Count && _scriptIdByPath.TryGetValue(_sourceFiles[_selectedIndex], out int id) ? id : -1;

        public void SelectScriptFile(int fileId)
        {
            InitialIndex = fileId;
            int at = IndexOfScriptFile(fileId);
            if (at >= 0) SelectedScriptIndex = at;
        }

        private int NextScriptId()
        {
            int max = -1;
            foreach (string file in _sourceFiles)
            {
                if (_scriptIdByPath.TryGetValue(file, out int id)) max = Math.Max(max, id);
            }
            return max + 1;
        }

        // Loads the selected file. A file switch drops the previous file's live diagnostics.
        private void LoadSelectedFile()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _sourceFiles.Count) return;

            CancelPendingDocumentChange();
            string oldPath = _currentPath;
            _currentPath = _sourceFiles[_selectedIndex];
            _suppress = true;
            ScriptText = RotomTool.ReadSource(_currentPath);
            _suppress = false;
            SetClean();
            if (string.IsNullOrWhiteSpace(oldPath) || !ProjectPaths.Same(oldPath, _currentPath))
            {
                foreach (ScriptDiagnostic diagnostic in Diagnostics.Where(d => d.IsLive).ToList())
                    Diagnostics.Remove(diagnostic);
                if (SelectedDiagnostic?.IsLive == true) SelectedDiagnostic = null;
                int errors = Diagnostics.Count(d => d.Severity == "Error");
                int warnings = Diagnostics.Count(d => d.Severity == "Warning");
                DiagnosticsStatusText = Diagnostics.Count == 0
                    ? "Waiting for live diagnostics."
                    : Plural.Of(errors, "error") + ", " + Plural.Of(warnings, "warning") + ".";
                OnPropertyChanged(nameof(HasDiagnostics));
            }
            IsReadOnly = false;
            StatusText = ManagedSource != null
                ? "Loaded " + ManagedSource.RelPath + " from the hg-engine checkout."
                : "";
            OnPropertyChanged(nameof(SelectedScriptPath));
            OnPropertyChanged(nameof(UnsavedChangesDescription));
            OnPropertyChanged(nameof(IsManagedByHgEngine));
            OnPropertyChanged(nameof(ManagedNote));
            if (ManagedSource != null)
            {
                if (Diagnostics.Count == 0) DiagnosticsStatusText = "hg-engine source, not checked by Rotom.";
                OnPropertyChanged(nameof(HasDiagnostics));
                return;
            }
            _ = OpenCurrentDocumentInLspAsync();
        }

        private void SaveSourceOnly(bool showStatus)
        {
            if (_currentPath == null) return;
            HgEngineOwnedFile managed = ManagedSource;
            if (managed != null)
            {
                if (!HgEngineOwnedFiles.TryWriteText(managed, ScriptText ?? "", out string error))   // keeps line endings and the source cache right
                    throw new IOException(error);
            }
            else
            {
                DSPRE.ROMFiles.ProjectSourceWatcher.Expect(_currentPath);
                PlainFiles.WriteText(_currentPath, ScriptText ?? "");
            }
            SetClean();
            AppEvents.RaiseScriptSourceSaved(this, _currentPath);
            if (showStatus) StatusText = "Saved " + (managed?.RelPath ?? DisplayPath(_currentPath)) + ".";
            if (managed == null) _ = SendCurrentDocumentSavedToLspAsync();   // rotom-lsp never opens hg-engine's sources
        }
    }
}
