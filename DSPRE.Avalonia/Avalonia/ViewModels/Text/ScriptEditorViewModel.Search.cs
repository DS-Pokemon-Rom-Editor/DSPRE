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
        public void SearchProject()
        {
            SearchResults.Clear();
            SelectedSearchResult = null;

            string needle = (SearchText ?? "").Trim();
            if (needle.Length == 0)
            {
                SearchStatusText = "Enter search text.";
                OnPropertyChanged(nameof(CanSearchProject));
                return;
            }

            int readErrors = 0;
            bool limited = false;

            foreach (string path in _sourceFiles)
            {
                string text;
                try
                {
                    text = _currentPath != null && ProjectPaths.Same(path, _currentPath)
                        ? ScriptText ?? ""
                        : RotomTool.ReadSource(path);
                }
                catch (Exception ex)
                {
                    AppLogger.WarnOnce("ScriptEditorViewModel.SearchProject", ex);
                    readErrors++;
                    continue;
                }

                string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].TrimEnd('\r');
                    int column;
                    if (SearchFuzzy)
                    {
                        if (!TryFuzzyMatch(line, needle, out column)) continue;
                    }
                    else
                    {
                        int index = line.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
                        if (index < 0) continue;
                        column = index + 1;
                    }

                    int lineNumber = i + 1;
                    string preview = line.Trim();
                    if (preview.Length == 0) preview = "(blank line)";
                    int remainingLineLength = Math.Max(1, line.Length - column + 1);
                    int selectionLength = Math.Min(needle.Length, remainingLineLength);
                    SearchResults.Add(new ScriptSearchResult(
                        path,
                        lineNumber,
                        column,
                        selectionLength,
                        DisplayPath(path) + ":" + lineNumber + ":" + column,
                        preview));

                    if (SearchResults.Count >= SearchResultLimit)
                    {
                        limited = true;
                        break;
                    }
                }

                if (limited) break;
            }

            string suffix = readErrors == 0 ? "" : " (" + Plural.Of(readErrors, "file") + " couldn't be read)";
            SearchStatusText = SearchResults.Count == 1
                ? "1 result" + suffix + "."
                : SearchResults.Count + " results" + suffix + (limited ? " (limited)." : ".");
            SelectedSidebarTabIndex = 0;
            OnPropertyChanged(nameof(CanSearchProject));
        }

        /// <summary>Lists a reference count's uses in the Search tab and returns them.</summary>
        internal List<ScriptSearchResult> ShowReferences(int line, IReadOnlyList<RotomLspLocation> uses)
        {
            List<ScriptSearchResult> results = new List<ScriptSearchResult>();
            Dictionary<string, string[]> files = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (RotomLspLocation use in uses)
            {
                if (!files.TryGetValue(use.Path, out string[] lines))
                {
                    if (ProjectPaths.Same(use.Path, _currentPath)) lines = (ScriptText ?? "").Replace("\r\n", "\n").Split('\n');
                    else
                    {
                        try { lines = RotomTool.ReadSource(use.Path).Replace("\r\n", "\n").Split('\n'); }
                        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { lines = Array.Empty<string>(); }
                    }
                    files[use.Path] = lines;
                }
                string preview = use.Line >= 1 && use.Line <= lines.Length ? lines[use.Line - 1].Trim() : "";
                results.Add(new ScriptSearchResult(use.Path, use.Line, use.Column, use.SelectionLength,
                    DisplayPath(use.Path) + ":" + use.Line + ":" + use.Column, preview.Length == 0 ? "(blank line)" : preview));
            }

            SearchResults.Clear();
            foreach (ScriptSearchResult r in results) SearchResults.Add(r);
            string what = LineText(line);
            SearchStatusText = (results.Count == 1 ? "1 use" : results.Count + " uses") + (what.Length > 0 ? " of " + what : "") + ".";
            SelectedSidebarTabIndex = 0;
            return results;
        }

        private string LineText(int line)
        {
            string[] lines = (ScriptText ?? "").Replace("\r\n", "\n").Split('\n');
            return line >= 1 && line <= lines.Length ? lines[line - 1].Trim().TrimEnd(':') : "";
        }

        public async Task<bool> OpenSearchResultAsync(ScriptSearchResult result)
        {
            if (result == null) return false;

            int index = _sourceFiles.FindIndex(path => ProjectPaths.Same(path, result.Path));
            if (index < 0)
            {
                SearchStatusText = "Result source no longer exists.";
                return false;
            }

            if (!await SelectScriptAsync(index)) return false;
            StatusText = "Opened " + result.Display + ".";
            return true;
        }

        public async Task<bool> OpenDiagnosticAsync(ScriptDiagnostic diagnostic)
        {
            if (diagnostic == null) return false;

            int index = _sourceFiles.FindIndex(path => ProjectPaths.Same(path, diagnostic.Path));
            if (index < 0)
            {
                DiagnosticsStatusText = "Diagnostic source no longer exists.";
                return false;
            }

            if (!await SelectScriptAsync(index)) return false;
            StatusText = "Opened " + diagnostic.Display + ".";
            return true;
        }

        /// <summary>Removes compile and live diagnostics and clears the selected row.</summary>
        public void ClearDiagnostics()
        {
            Diagnostics.Clear();
            SelectedDiagnostic = null;
            DiagnosticsStatusText = "No diagnostics.";
            OnPropertyChanged(nameof(HasDiagnostics));
        }

        public void ShutdownLsp()
        {
            CancelPendingDocumentChange();
            if (_lsp == null) return;
            _lsp.DiagnosticsPublished -= OnLspDiagnosticsPublished;
            _lsp.Dispose();
            _lsp = null;
            _lspOpenPath = null;
        }
    }
}
