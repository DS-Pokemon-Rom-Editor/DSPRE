using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia;
using DSPRE.Avalonia.Gl;
using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    public partial class HeadbuttEncounterViewModel
    {
        private static readonly global::Avalonia.Platform.Storage.FilePickerFileType[] HeadbuttFiles =
        {
            new global::Avalonia.Platform.Storage.FilePickerFileType("Headbutt file") { Patterns = new[] { "*.bin" } },
            new global::Avalonia.Platform.Storage.FilePickerFileType("All files") { Patterns = new[] { "*" } },
        };

        public async Task ExportAsync(Window owner)
        {
            if (_file == null || _selFile < 0) return;
            string path = await DialogHelper.SaveFileAsync(owner, "Export headbutt file", HeadbuttFiles, $"headbutt_{_selFile:D4}.bin");
            if (path == null) return;
            try { PlainFiles.WriteBytes(path, _file.ToByteArray()); SaveNotice.Exported(path); }
            catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException)
            {
                await DialogHelper.ShowErrorAsync($"Couldn't export:\n{ex.Message}", "Special encounters editor");
            }
        }

        /// <summary>Puts an exported file in place of the shown one, unsaved and undoable.</summary>
        public async Task ImportAsync(Window owner)
        {
            if (_file == null || _selFile < 0) return;
            string path = await DialogHelper.OpenFileAsync(owner, "Import headbutt file", HeadbuttFiles);
            if (path == null) return;
            HeadbuttEncounterFile read;
            try { read = new HeadbuttEncounterFile((ushort)_selFile, PlainFiles.ReadBytes(path)); }
            catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException || ex is System.IO.EndOfStreamException || ex is ArgumentException)
            {
                await DialogHelper.ShowErrorAsync($"That file couldn't be read as a headbutt file:\n{ex.Message}", "Special encounters editor");
                return;
            }
            _file = read;
            _selGroup = -1;
            _specialGroupActive = false;
            BuildEncounterRows();
            RefreshGroups();
            RedrawIfTreeCellsMoved();
            SelectFirstGroup();
            Dirty();
            SaveNotice.Imported($"headbutt file {_selFile}", path);
        }

        public void Save()
        {
            if (_file == null || _selFile < 0) return;
            if (HgEngineProject.IsActive) { DialogHelper.Watch(SaveHgEngineAsync()); return; }
            try
            {
                if (_file.SaveToFile(_selFile)) MarkSaved(_selFile);
                else StatusText = "Couldn't save, see the log.";
            }
            catch (Exception ex) when (AppLogger.Noted("HeadbuttEncounterViewModel.Save", ex)) { _ = DialogHelper.ShowErrorAsync($"Couldn't save:\n{ex.Message}", "Special encounters editor"); }
        }

        async Task<bool> IEditorWithUnsavedChanges.SaveChangesAsync()
        {
            if (HgEngineProject.IsActive) await SaveHgEngineAsync();
            else Save();
            return !HasUnsavedChanges;
        }

        // Set when Headbutt.c couldn't be read: saving the blank file shown would replace the entry.
        private string _hgeReadError;

        private async Task SaveHgEngineAsync()
        {
            if (_file == null || _selFile < 0) return;
            if (_hgeReadError != null)
            {
                await DialogHelper.ShowErrorAsync($"Headbutt file {_selFile} couldn't be read from Headbutt.c, so it wasn't saved:"
                    + Environment.NewLine + _hgeReadError, "Special encounters editor");
                return;
            }
            int fileId = _selFile;
            HeadbuttEncounterFile file = _file;
            (bool saved, string error) = await HgEngineSave.RunAsync(() => HgEngineHeadbutt.TrySave(fileId, file, out string err) ? null : err);
            if (saved)
            {
                if (fileId == _selFile) MarkSaved(fileId);
                return;
            }
            if (error == null) return;
            StatusText = "Couldn't save.";
            await DialogHelper.ShowErrorAsync($"Headbutt file {fileId} wasn't saved.\n{error}", "Special encounters editor");
        }

        private void MarkSaved(int fileId)
        {
            _undo?.MarkSaved();
            SetClean();
            StatusText = $"Saved headbutt file {fileId}.";
            SaveNotice.Saved(UnsavedChangesDescription);
        }
    }
}
