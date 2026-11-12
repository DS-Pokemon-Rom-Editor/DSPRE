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
        private async Task StartLanguageServerAsync()
        {
            if (!RotomTool.IsLspAvailable)
            {
                DiagnosticsStatusText = "Live diagnostics unavailable: "
                    + DSUtils.ToolAvailabilityError("rotom-lsp");
                return;
            }

            try
            {
                ShutdownLsp();
                _lsp = new RotomLanguageServerClient();
                _lsp.DiagnosticsPublished += OnLspDiagnosticsPublished;
                await _lsp.StartAsync();
                await OpenCurrentDocumentInLspAsync();
            }
            catch (Exception ex)
            {
                ShutdownLsp();
                AppLogger.Warn("Couldn't start rotom-lsp: " + ex.Message);
                DiagnosticsStatusText = "Live diagnostics unavailable: " + ex.Message;
            }
        }

        private void OnLspDiagnosticsPublished(object sender, RotomLspDiagnosticsEventArgs e)
            => Dispatcher.UIThread.Post(() => ApplyLspDiagnostics(e));

        // Replaces the current file's live diagnostics. Compile rows and their selection stay.
        private void ApplyLspDiagnostics(RotomLspDiagnosticsEventArgs e)
        {
            if (e == null || string.IsNullOrWhiteSpace(e.Path) || string.IsNullOrWhiteSpace(_currentPath))
                return;
            if (!ProjectPaths.Same(e.Path, _currentPath)) return;

            foreach (ScriptDiagnostic diagnostic in Diagnostics.Where(d => d.IsLive).ToList())
                Diagnostics.Remove(diagnostic);
            if (SelectedDiagnostic?.IsLive == true) SelectedDiagnostic = null;

            string source = ReadSourceForPath(e.Path);
            foreach (RotomLspDiagnostic diagnostic in e.Diagnostics)
            {
                string preview = LineText(source, diagnostic.Line);
                if (string.IsNullOrWhiteSpace(preview)) preview = diagnostic.Message;

                Diagnostics.Add(new ScriptDiagnostic(
                    diagnostic.Severity,
                    diagnostic.Kind,
                    e.Path,
                    diagnostic.Line,
                    diagnostic.Column,
                    diagnostic.SelectionLength,
                    diagnostic.Message,
                    DisplayPath(e.Path) + ":" + diagnostic.Line + ":" + diagnostic.Column,
                    preview.Trim(),
                    isLive: true));
            }

            int errors = Diagnostics.Count(d => d.Severity == "Error");
            int warnings = Diagnostics.Count(d => d.Severity == "Warning");
            DiagnosticsStatusText = Diagnostics.Count == 0
                ? "No diagnostics."
                : Plural.Of(errors, "error") + ", " + Plural.Of(warnings, "warning") + ".";
            if (Diagnostics.Count > 0) SelectedSidebarTabIndex = 1;
            OnPropertyChanged(nameof(HasDiagnostics));
        }

        private async Task OpenCurrentDocumentInLspAsync()
        {
            RotomLanguageServerClient lsp = _lsp;
            if (lsp == null || !lsp.IsRunning || string.IsNullOrWhiteSpace(_currentPath)) return;

            try
            {
                if (!string.IsNullOrWhiteSpace(_lspOpenPath))
                {
                    if (ProjectPaths.Same(_lspOpenPath, _currentPath))
                    {
                        _documentVersion++;
                        await lsp.DidChangeAsync(_currentPath, _documentVersion, ScriptText ?? "");
                        // Hints placed for the old text would land in the wrong spots.
                        ScheduleDocumentExtras();
                        return;
                    }

                    await lsp.DidCloseAsync(_lspOpenPath);
                }

                _documentVersion = 1;
                _lspOpenPath = _currentPath;
                await lsp.DidOpenAsync(_currentPath, LanguageIdForPath(_currentPath), _documentVersion, ScriptText ?? "");
                ScheduleDocumentExtras();
            }
            catch (Exception ex)
            {
                // Closing the editor shuts the server down under a request still in flight.
                if (!ReferenceEquals(lsp, _lsp)) return;
                AppLogger.Warn("rotom-lsp open failed: " + ex.Message);
            }
        }

        private async Task SendCurrentDocumentChangedToLspAsync()
        {
            if (_lsp == null || !_lsp.IsRunning || string.IsNullOrWhiteSpace(_currentPath)) return;

            try
            {
                if (string.IsNullOrWhiteSpace(_lspOpenPath) || !ProjectPaths.Same(_lspOpenPath, _currentPath))
                {
                    await OpenCurrentDocumentInLspAsync();
                    return;
                }

                _documentVersion++;
                await _lsp.DidChangeAsync(_currentPath, _documentVersion, ScriptText ?? "");
                ScheduleDocumentExtras();
            }
            catch (Exception ex)
            {
                AppLogger.Warn("rotom-lsp change failed: " + ex.Message);
            }
        }

        // Edits reach the server after a delay, so completion and signature help flush them first.
        private async Task FlushLspDocumentAsync()
        {
            if (_lspChangeDelay == null) return;
            CancelPendingDocumentChange();
            await SendCurrentDocumentChangedToLspAsync();
        }

        internal async Task<List<RotomLspCompletion>> CompletionAsync(int line, int column)
        {
            if (_lsp == null || !_lsp.IsRunning || string.IsNullOrWhiteSpace(_currentPath) || ManagedSource != null)
                return new List<RotomLspCompletion>();
            await FlushLspDocumentAsync();
            try { return await _lsp.CompletionAsync(_currentPath, line, column); }
            catch (Exception ex)
            {
                AppLogger.Warn("rotom-lsp completion failed: " + ex.Message);
                return new List<RotomLspCompletion>();
            }
        }

        internal async Task<RotomLspSignature> SignatureHelpAsync(int line, int column)
        {
            if (_lsp == null || !_lsp.IsRunning || string.IsNullOrWhiteSpace(_currentPath) || ManagedSource != null) return null;
            await FlushLspDocumentAsync();
            try { return await _lsp.SignatureHelpAsync(_currentPath, line, column); }
            catch (Exception ex)
            {
                AppLogger.Warn("rotom-lsp signature help failed: " + ex.Message);
                return null;
            }
        }

        private void ScheduleCurrentDocumentChangedToLsp()
        {
            if (ManagedSource != null) return;
            CancelPendingDocumentChange();
            if (_lsp == null || !_lsp.IsRunning || string.IsNullOrWhiteSpace(_currentPath)) return;

            CancellationTokenSource delay = new CancellationTokenSource();
            _lspChangeDelay = delay;
            _ = SendCurrentDocumentChangedToLspAfterDelayAsync(delay);
        }

        private async Task SendCurrentDocumentChangedToLspAfterDelayAsync(CancellationTokenSource delay)
        {
            try
            {
                await Task.Delay(LanguageServerChangeDelayMs, delay.Token);
                await SendCurrentDocumentChangedToLspAsync();
            }
            catch (OperationCanceledException) { /* cancelled on purpose: a newer change, a flush, a file switch or shutdown */ }
            finally
            {
                if (ReferenceEquals(_lspChangeDelay, delay)) _lspChangeDelay = null;
                delay.Dispose();
            }
        }

        private async Task FlushCurrentDocumentChangedToLspAsync()
        {
            CancelPendingDocumentChange();
            await SendCurrentDocumentChangedToLspAsync();
        }

        private void CancelPendingDocumentChange()
        {
            CancellationTokenSource delay = _lspChangeDelay;
            _lspChangeDelay = null;
            delay?.Cancel();
        }

        private async Task SendCurrentDocumentSavedToLspAsync()
        {
            if (_lsp == null || !_lsp.IsRunning || string.IsNullOrWhiteSpace(_currentPath)) return;

            try
            {
                await FlushCurrentDocumentChangedToLspAsync();
                await _lsp.DidSaveAsync(_currentPath);
            }
            catch (Exception ex)
            {
                AppLogger.Warn("rotom-lsp save failed: " + ex.Message);
            }
        }
    }
}
