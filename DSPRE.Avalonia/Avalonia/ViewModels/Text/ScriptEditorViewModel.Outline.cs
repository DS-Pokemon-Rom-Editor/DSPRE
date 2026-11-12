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
        /// <summary>The open file's scripts, functions and actions, as rotom-lsp lists them.</summary>
        public ObservableCollection<OutlineEntry> Outline { get; } = new();

        /// <summary>Code lenses and inlay hints for the open file, drawn inline by the view.</summary>
        internal IReadOnlyList<RotomLspInlineText> InlineTexts { get; private set; } = Array.Empty<RotomLspInlineText>();
        public event EventHandler InlineTextsChanged;

        private List<RotomLspInlineText> _lenses = new List<RotomLspInlineText>();
        private List<RotomLspInlineText> _hints = new List<RotomLspInlineText>();

        /// <summary>Whether the parameter names rotom-lsp puts before each value are drawn.</summary>
        public bool ShowInlineHints
        {
            get => SettingsManager.Settings?.ScriptEditorInlineHints ?? true;
            set
            {
                if (SettingsManager.Settings == null || SettingsManager.Settings.ScriptEditorInlineHints == value) return;
                SettingsManager.Settings.ScriptEditorInlineHints = value;
                SettingsManager.Save();
                OnPropertyChanged();
                PublishInlineTexts();
            }
        }

        private void PublishInlineTexts()
        {
            InlineTexts = ShowInlineHints ? _lenses.Concat(_hints).ToList() : _lenses.ToList();
            InlineTextsChanged?.Invoke(this, EventArgs.Empty);
        }

        private CancellationTokenSource _extrasCts;

        // Asked for half a second after typing settles, so the server is not queried on every key.
        private void ScheduleDocumentExtras()
        {
            _extrasCts?.Cancel();
            CancellationTokenSource cts = _extrasCts = new CancellationTokenSource();
            _ = Task.Delay(500, cts.Token).ContinueWith(t =>
            {
                if (!t.IsCanceled) Dispatcher.UIThread.Post(() => _ = RefreshDocumentExtrasAsync(cts.Token));
            }, TaskScheduler.Default);
        }

        private async Task RefreshDocumentExtrasAsync(CancellationToken token)
        {
            string path = _currentPath;
            if (_lsp == null || !_lsp.IsRunning || string.IsNullOrWhiteSpace(path) || ManagedSource != null) return;
            try
            {
                int lastLine = (ScriptText ?? "").Count(c => c == '\n') + 1;
                List<RotomLspSymbol> symbols = await _lsp.DocumentSymbolsAsync(path);
                List<RotomLspInlineText> lenses = await _lsp.CodeLensAsync(path);
                List<RotomLspInlineText> hints = await _lsp.InlayHintsAsync(path, lastLine);
                if (token.IsCancellationRequested || !ProjectPaths.Same(path, _currentPath)) return;

                Outline.Clear();
                List<OutlineEntry> entries = symbols.Select(s => new OutlineEntry(s.Name, s.Line, s.Depth, ScriptNumberOf(s))).ToList();
                foreach (OutlineEntry entry in entries.Where(e => e.Number != null).OrderBy(e => e.Number)) Outline.Add(entry);
                List<OutlineEntry> rest = entries.Where(e => e.Number == null).ToList();
                for (int i = 0; i < rest.Count; i++)
                {
                    bool wasHeading = entries.IndexOf(rest[i]) + 1 < entries.Count && entries[entries.IndexOf(rest[i]) + 1].Depth > rest[i].Depth;   // a heading whose rows all moved up has nothing left under it
                    bool keepsRows = i + 1 < rest.Count && rest[i + 1].Depth > rest[i].Depth;
                    if (wasHeading && !keepsRows) continue;
                    Outline.Add(rest[i]);
                }
                _lenses = lenses;
                _hints = hints;
                PublishInlineTexts();
            }
            catch (Exception ex) when (ex is TimeoutException || ex is InvalidOperationException || ex is System.IO.IOException)
            {
                AppLogger.Warn("rotom-lsp outline failed: " + ex.Message);
            }
        }
    }
}
