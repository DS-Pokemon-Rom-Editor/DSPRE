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
        /// <summary>Open file's line for jump-table slot scriptNumber, per rotom-lsp, or null.</summary>
        public async Task<int?> ResolveScriptEntryLineAsync(int scriptNumber)
        {
            if (scriptNumber <= 0 || string.IsNullOrWhiteSpace(_currentPath)) return null;
            if (_lsp == null || !_lsp.IsRunning)
            {
                StatusText = "Couldn't jump to script #" + scriptNumber + ": the script checker isn't running.";
                return null;
            }

            try
            {
                if (Path.GetExtension(_currentPath).Equals(".rotom", StringComparison.OrdinalIgnoreCase))
                    await FlushCurrentDocumentChangedToLspAsync();

                int? line = FindScriptEntryLine(await _lsp.DocumentSymbolsAsync(_currentPath), scriptNumber);
                StatusText = line == null
                    ? "Script #" + scriptNumber + " wasn't found in " + DisplayPath(_currentPath) + "."
                    : "Opened script #" + scriptNumber + " in " + DisplayPath(_currentPath) + ".";
                return line;
            }
            catch (Exception ex)
            {
                AppLogger.Warn("rotom-lsp document symbols failed: " + ex.Message);
                StatusText = "Couldn't jump to script #" + scriptNumber + ": " + ex.Message;
                return null;
            }
        }

        private static int? ScriptNumberOf(RotomLspSymbol symbol) =>
            symbol.Detail != null && symbol.Detail.StartsWith('#') && int.TryParse(symbol.Detail.AsSpan(1), out int n) ? n : null;

        /// <summary>The slot's symbol line. "#N" is compared whole: #2 never matches #21.</summary>
        internal static int? FindScriptEntryLine(IReadOnlyList<RotomLspSymbol> symbols, int scriptNumber)
        {
            string slot = "#" + scriptNumber;
            foreach (RotomLspSymbol symbol in symbols)
            {
                if (string.Equals(symbol.Detail, slot, StringComparison.Ordinal))
                    return symbol.Line;
            }
            return null;
        }

        public async Task<ScriptNavigationTarget> GoToDefinitionAsync(int line, int column)
        {
            if (_lsp == null || !_lsp.IsRunning || string.IsNullOrWhiteSpace(_currentPath))
            {
                StatusText = "Go to definition needs the script checker running.";
                return null;
            }

            try
            {
                if (Path.GetExtension(_currentPath).Equals(".rotom", StringComparison.OrdinalIgnoreCase))
                    await FlushCurrentDocumentChangedToLspAsync();

                RotomLspLocation target = await _lsp.DefinitionAsync(_currentPath, line, column);
                if (target == null || string.IsNullOrWhiteSpace(target.Path))
                {
                    StatusText = "No definition found.";
                    return null;
                }

                int index = _sourceFiles.FindIndex(path => ProjectPaths.Same(path, target.Path));
                if (index < 0)
                {
                    RefreshScriptList();
                    index = _sourceFiles.FindIndex(path => ProjectPaths.Same(path, target.Path));
                }

                if (index < 0)
                {
                    StatusText = "Definition is outside the Rotom source list: " + DisplayPath(target.Path) + ".";
                    return null;
                }

                if (!await SelectScriptAsync(index)) return null;
                StatusText = "Opened definition at " + DisplayPath(target.Path) + ":" + target.Line + ":" + target.Column + ".";
                return new ScriptNavigationTarget(target.Line, target.Column, target.SelectionLength);
            }
            catch (Exception ex)
            {
                AppLogger.Warn("rotom-lsp definition failed: " + ex.Message);
                StatusText = "Go to definition failed: " + ex.Message;
                return null;
            }
        }

        public async Task<string> HoverAsync(int line, int column)
        {
            if (_lsp == null || !_lsp.IsRunning || string.IsNullOrWhiteSpace(_currentPath))
                return null;

            try
            {
                return await _lsp.HoverAsync(_currentPath, line, column);
            }
            catch (Exception ex)
            {
                AppLogger.Warn("rotom-lsp hover failed: " + ex.Message);
                return null;
            }
        }
    }
}
