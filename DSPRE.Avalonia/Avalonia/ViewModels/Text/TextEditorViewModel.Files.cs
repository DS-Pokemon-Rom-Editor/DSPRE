using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Platform.Storage;
using DSPRE.Avalonia;
using DSPRE.CharMaps;
using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Text
{
    public partial class TextEditorViewModel
    {
        public async Task ImportAsync()
        {
            if (_current == null) return;
            if (await RefusedForManagedArchiveAsync()) return;
            FilePickerFileType[] filters = new[]
            {
                new FilePickerFileType("JSON text archive") { Patterns = new[] { "*.json" } },
                new FilePickerFileType("Binary text archive") { Patterns = new[] { "*.msg", "*.bin" } },
            };
            string path = await DialogHelper.OpenFileAsync(_owner, "Import text archive", filters);
            if (path == null) return;
            if (_current == null) return;

            try
            {
                // Counts as an edit: Save keeps it and Undo takes it back.
                List<string> lines = TextArchive.ReadMessagesFrom(path);
                if (lines.SequenceEqual(_current.Messages))
                {
                    StatusText = $"That file already matches text archive {_current.ID}.";
                    return;
                }
                _current.Messages.Clear();
                _current.Messages.AddRange(lines);
                ShowLines();
                RefreshPreview();
                SetDirty();
                SaveNotice.Imported($"{lines.Count} lines into text archive {_current.ID}", path);
            }
            catch (Exception ex) when (AppLogger.Noted("TextEditorViewModel.ImportAsync", ex))
            {
                await DialogHelper.ShowErrorAsync($"Couldn't import:\n{ex.Message}", "Text editor");
            }
        }

        public async Task ExportAsync()
        {
            if (_current == null) return;
            if (await RefusedForManagedArchiveAsync()) return;
            int id = _current.ID;
            FilePickerFileType[] filters = new[]
            {
                new FilePickerFileType("JSON text archive") { Patterns = new[] { "*.json" } },
                new FilePickerFileType("Binary text archive") { Patterns = new[] { "*.msg" } },
            };
            string path = await DialogHelper.SaveFileAsync(_owner, "Export text archive", filters, "Text archive " + id);
            if (path == null) return;

            string ext = Path.GetExtension(path).ToLowerInvariant();
            try
            {
                // Exports what the editor shows, unsaved edits and added archives included.
                byte[] json = _current.ToExpandedJsonBytes(id);
                if (ext == ".json")
                {
                    PlainFiles.WriteBytes(path, json);
                }
                else
                {
                    TextConverter.JsonBytesToBin(json, path, CharMapManager.GetCharMapPath());
                }
                SaveNotice.Exported(path);
            }
            catch (Exception ex) when (AppLogger.Noted("TextEditorViewModel.ExportAsync", ex))
            {
                await DialogHelper.ShowErrorAsync($"Couldn't export:\n{ex.Message}", "Text editor");
            }
        }
    }
}
