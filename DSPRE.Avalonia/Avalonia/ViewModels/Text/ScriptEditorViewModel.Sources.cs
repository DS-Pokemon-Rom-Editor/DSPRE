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
        /// <summary>For a standalone window closing. The Maps workspace keeps its instance.</summary>
        public void Detach()
        {
            AppEvents.ScriptSourceSaved -= OnSourceSavedElsewhere;
            DSPRE.ROMFiles.ScriptSourceSync.SourcesRefreshed -= OnSourcesRefreshed;
            DSPRE.ROMFiles.ProjectSourceWatcher.ScriptsCompiled -= OnCompiledAfterOutsideEdit;
        }

        private void OnCompiledAfterOutsideEdit(RotomTool.Result result, IReadOnlyList<string> paths)
        {
            Dispatcher.UIThread.Post(async () =>
            {
                if (_lsp != null) _ = _lsp.DidChangeWatchedFilesAsync(paths);
                bool current = _currentPath != null && paths.Any(p => ProjectPaths.Same(p, _currentPath));
                if (current) await ReloadSavedElsewhereAsync(_currentPath);
                if (!current || result.Success) return;
                UpdateCompileDiagnostics(result);
                List<string> failures = RotomTool.FailureLines(result);
                StatusText = "Edited outside DSPRE and didn't compile: " + failures[0] + (failures.Count > 1 ? $" (and {failures.Count - 1} more)" : "");
            });
        }

        private bool _askingAboutSavedElsewhere;

        private void OnSourceSavedElsewhere(object sender, string path)
        {
            if (ReferenceEquals(sender, this)) return;
            Dispatcher.UIThread.Post(() => _ = ReloadSavedElsewhereAsync(path));
        }

        private void OnSourcesRefreshed(IReadOnlyCollection<int> fileIds)
        {
            string dir = System.IO.Path.Combine(RotomTool.ProjectRoot, "expanded", "scripts");
            List<string> paths = fileIds.SelectMany(id => new[] { System.IO.Path.Combine(dir, id.ToString("D4") + ".rotom"), System.IO.Path.Combine(dir, id.ToString("D4") + ".json") })
                               .Where(System.IO.File.Exists).ToList();
            Dispatcher.UIThread.Post(() =>
            {
                if (paths.Count > 0 && _lsp != null) _ = _lsp.DidChangeWatchedFilesAsync(paths);
                foreach (string path in paths) _ = ReloadSavedElsewhereAsync(path);
            });
        }

        // Saving old text would compile it over the write made elsewhere, so this reloads or asks.
        private async Task ReloadSavedElsewhereAsync(string path)
        {
            if (_askingAboutSavedElsewhere || _saving) return;
            if (path == null && _currentPath != null && ManagedSource == null) RefreshScriptList();
            if (_currentPath == null || _selectedIndex < 0) return;
            if (path == null ? ManagedSource != null : !ProjectPaths.Same(path, _currentPath)) return;

            string onDisk;
            try { onDisk = RotomTool.ReadSource(_currentPath); }
            catch (Exception ex) { AppLogger.WarnOnce("ScriptEditorViewModel.ReloadSavedElsewhereAsync", ex); return; }
            if (string.Equals(onDisk, ScriptText ?? "", StringComparison.Ordinal))
            {
                if (_dirty) SetClean();
                return;
            }

            if (_dirty)
            {
                _askingAboutSavedElsewhere = true;
                try
                {
                    if (!await RecordSwitchGuard.TakeSavedVersionAsync(UnsavedChangesDescription)) return;
                }
                finally { _askingAboutSavedElsewhere = false; }
            }

            SetClean();
            LoadSelectedFile();
            StatusText = "";
        }
    }
}
