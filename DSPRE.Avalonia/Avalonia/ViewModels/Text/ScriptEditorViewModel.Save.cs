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
        public async Task<bool> SaveAsync()
        {
            if (_saving || !IsEditable) return false;
            _saving = true;
            try
            {
                if (ManagedSource != null)
                {
                    SaveSourceOnly(true);
                    await ShowManagedSaveNoticeAsync(ManagedSource);
                    return true;
                }

                SaveSourceOnly(true);
                bool compiled = await CompileAsync(false);
                if (!compiled) Dirty();   // source is on disk, binary isn't, so stay dirty
                return compiled;
            }
            catch (Exception ex) when (AppLogger.Noted("ScriptEditorViewModel.SaveAsync", ex))
            {
                await DialogHelper.ShowErrorAsync("Couldn't save:\n" + ex.Message, "Script editor");
                return false;
            }
            finally
            {
                _saving = false;
            }
        }

        public async Task<bool> CompileAsync(bool saveCurrentFile = true)
        {
            if (IsBusy || IsReadOnly) return false;
            if (ManagedSource != null) return false;

            try
            {
                if (saveCurrentFile && _dirty) SaveSourceOnly(false);
                IsBusy = true;
                StatusText = "Compiling Rotom project...";

                RotomTool.Result result = await RotomTool.CompileProjectAsync();
                string summary = RotomTool.FormatResult(result);
                int diagnostics = UpdateCompileDiagnostics(result);

                if (result.Success)
                {
                    StatusText = diagnostics == 0
                        ? "Compile successful: " + summary
                        : "Compile successful with " + Plural.Of(diagnostics, "warning") + ": " + summary;
                    if (result.KeptBinaries.Count > 0)
                        await DialogHelper.ShowInfoAsync(KeptBinariesMessage(result.KeptBinaries), "Compile");
                    if (result.HgEngineOwnedEdits.Count > 0)
                        await DialogHelper.ShowInfoAsync(OwnedEditsMessage(result.HgEngineOwnedEdits), "Compile");
                }
                else
                {
                    StatusText = "Compile failed: " + summary;
                }

                if (!result.Success && diagnostics == 0)
                    await DialogHelper.ShowErrorAsync("rotom compile failed:\n" + RotomTool.FormatDetails(result), "Script editor");
                return result.Success;
            }
            catch (Exception ex) when (AppLogger.Noted("ScriptEditorViewModel.CompileAsync", ex))
            {
                StatusText = "Compile failed: " + ex.Message;
                DiagnosticsStatusText = "Compile failed before diagnostics were available.";
                await DialogHelper.ShowErrorAsync("Compile failed:\n" + ex.Message, "Script editor");
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        // Replaces compile diagnostics and returns their count. Live rows and their selection stay.
        private int UpdateCompileDiagnostics(RotomTool.Result result)
        {
            foreach (ScriptDiagnostic diagnostic in Diagnostics.Where(d => !d.IsLive).ToList())
                Diagnostics.Remove(diagnostic);
            if (SelectedDiagnostic?.IsLive != true) SelectedDiagnostic = null;

            if (result == null || string.IsNullOrWhiteSpace(result.Stdout))
            {
                DiagnosticsStatusText = "No compile diagnostics.";
                OnPropertyChanged(nameof(HasDiagnostics));
                return 0;
            }

            try
            {
                using JsonDocument doc = JsonDocument.Parse(result.Stdout);
                JsonElement root = doc.RootElement;

                if (root.TryGetProperty("failures", out JsonElement failures) && failures.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement failure in failures.EnumerateArray())
                    {
                        string path = ResolveProjectPath(failure.ReadString("path"));
                        JsonElement error = failure.TryGetProperty("error", out JsonElement err) ? err : default;
                        string kind = error.ReadString("type") ?? "Error";
                        JsonElement details = error.ValueKind == JsonValueKind.Object && error.TryGetProperty("details", out JsonElement d) ? d : default;
                        string message = details.ReadString("message") ?? kind;
                        TryReadSpan(details, out int start, out int end);
                        AddDiagnostic("Error", kind, path, start, end, message);
                    }
                }

                if (root.TryGetProperty("successes", out JsonElement successes) && successes.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement success in successes.EnumerateArray())
                    {
                        string path = ResolveProjectPath(success.ReadString("input"));
                        if (!success.TryGetProperty("warnings", out JsonElement warnings) || warnings.ValueKind != JsonValueKind.Array)
                            continue;

                        foreach (JsonElement warning in warnings.EnumerateArray())
                        {
                            string kind = warning.ReadString("type") ?? "Warning";
                            JsonElement details = warning.ValueKind == JsonValueKind.Object && warning.TryGetProperty("details", out JsonElement d) ? d : default;
                            TryReadSpan(details, out int start, out int end);
                            AddDiagnostic("Warning", kind, path, start, end, WarningMessage(kind, details));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Couldn't parse rotom compile diagnostics: " + ex.Message);
                DiagnosticsStatusText = "Couldn't parse compile diagnostics.";
                OnPropertyChanged(nameof(HasDiagnostics));
                return 0;
            }

            int errors = Diagnostics.Count(d => d.Severity == "Error");
            int warningsCount = Diagnostics.Count(d => d.Severity == "Warning");
            DiagnosticsStatusText = errors == 0 && warningsCount == 0
                ? "No diagnostics."
                : Plural.Of(errors, "error") + ", " + Plural.Of(warningsCount, "warning") + ".";
            if (Diagnostics.Count > 0) SelectedSidebarTabIndex = 1;
            OnPropertyChanged(nameof(HasDiagnostics));
            return Diagnostics.Count(d => !d.IsLive);
        }

        private void AddDiagnostic(string severity, string kind, string path, int byteStart, int byteEnd, string message)
        {
            string source = ReadSourceForPath(path);
            LineColumnForByteOffset(source ?? "", byteStart, out int line, out int column);
            string preview = LineText(source, line);
            if (string.IsNullOrWhiteSpace(preview)) preview = message;

            int selectionLength = 1;
            if (!string.IsNullOrEmpty(source) && byteEnd > byteStart)
            {
                int startIndex = CharIndexFromUtf8Offset(source, byteStart);
                int endIndex = CharIndexFromUtf8Offset(source, byteEnd);
                selectionLength = Math.Max(1, endIndex - startIndex);
            }

            Diagnostics.Add(new ScriptDiagnostic(
                severity,
                kind,
                path,
                line,
                column,
                selectionLength,
                message,
                DisplayPath(path) + ":" + line + ":" + column,
                preview.Trim()));
        }
    }
}
