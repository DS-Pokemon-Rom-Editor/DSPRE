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
        public async Task SetupAsync(Window owner)
        {
            _owner = owner;
            IsBusy = true;
            IsReadOnly = true;
            StatusText = "Preparing Rotom project...";

            if (GameFamily == GameFamilies.DP && !HasNote)
            {
                AddNote("Diamond/Pearl scripts are a work in progress",
                    "Rotom handles Diamond and Pearl scripts less reliably than Platinum and HGSS, so a failed " +
                    "or wrong conversion may be Rotom's doing.");
            }

            try
            {
                if (!RotomTool.IsAvailable)
                    throw new FileNotFoundException("rotom wasn't found in DSPRE's Tools folder.", RotomTool.ExePath);

                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.Scripts });

                await EnsureRotomProjectAsync();

                RefreshScriptList();
                string undecompiled = await DecompileIfEmptyAsync();

                await UpgradeRotomProjectAsync();

                IsReadOnly = ScriptNames.Count == 0;

                if (ScriptNames.Count == 0)
                {
                    StatusText = "No Rotom source files found.";
                    return;
                }

                _suppress = true;
                _selectedIndex = InitialSelection();
                OnPropertyChanged(nameof(SelectedScriptIndex));
                _suppress = false;
                LoadSelectedFile();
                await StartLanguageServerAsync();
                if (undecompiled != null) StatusText = undecompiled;
            }
            catch (Exception ex) when (AppLogger.Noted("ScriptEditorViewModel.SetupAsync", ex))
            {
                IsReadOnly = true;
                StatusText = "Error: " + ex.Message;
                await DialogHelper.ShowErrorAsync("Couldn't prepare Rotom scripts:\n" + ex.Message, "Script editor");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task EnsureRotomProjectAsync()
        {
            if (!HasRotomProject)
            {
                RotomTool.EnsureSourceRoot();

                StatusText = "Initializing Rotom project...";
                RotomTool.Result init = await RotomTool.InitProjectAsync();
                if (!init.Success) throw new InvalidOperationException("rotom init --non-interactive failed:\n" + RotomTool.FormatDetails(init));
                RefreshRotomProjectState();
            }

            if (RotomTool.HasLegacyScripts())
            {
                // convert decompiles each binary and only backs up the .script, so unbuilt edits are built first.
                StatusText = "Building edited legacy scripts...";
                string buildFailure = await Task.Run(() => DSPRE.ROMFiles.ScriptFile.BuildRequiredBins(out string error) ? null : error);
                if (buildFailure != null)
                    throw new InvalidOperationException("These legacy scripts don't build, so they weren't converted:\n" + buildFailure);

                StatusText = "Converting legacy scripts to Rotom...";
                await RunRequiredRotomCommandAsync("convert", "--non-interactive");
                RefreshRotomProjectState();
                AppLogger.Info("rotom kept the replaced .script files under " + Path.Combine(RotomTool.ProjectRoot, ".rotom", "backups"));
            }
        }

        private async Task<string> DecompileIfEmptyAsync()
        {
            string undecompiled = null;
            if (_rotomFileCount == 0)
            {
                StatusText = "Decompiling binary scripts to Rotom...";
                RotomTool.Result decompiled = await RotomTool.RunAsync("decompile");
                RotomTool.SetAsideHgEngineOwnedSources();
                RefreshScriptList();
                // One file rotom can't read must not cost every other script; its binary stays as it is.
                if (!decompiled.Success)
                {
                    if (_rotomFileCount == 0)
                        throw new InvalidOperationException("rotom decompile failed:\n" + RotomTool.FormatDetails(decompiled));
                    undecompiled = UndecompiledScripts();
                    AppLogger.Warn("rotom decompile left files out:\n" + RotomTool.FormatDetails(decompiled));
                }
            }
            return undecompiled;
        }
    }
}
