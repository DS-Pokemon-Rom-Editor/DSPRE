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
        public void AddString()
        {
            // A field-backed archive has exactly one line per entry.
            if (_current == null || _fieldSource != null) return;
            _current.Messages.Add("");
            TextLineVM line = new TextLineVM(Lines.Count, "");
            line.PropertyChanged += OnLineChanged;
            Lines.Add(line);
            RenumberLines();
            SetDirty();
        }

        public void RemoveString()
        {
            if (_current == null || _current.Messages.Count == 0 || _fieldSource != null) return;
            _current.Messages.RemoveAt(_current.Messages.Count - 1);
            TextLineVM last = Lines[Lines.Count - 1];
            last.PropertyChanged -= OnLineChanged;
            Lines.RemoveAt(Lines.Count - 1);
            RenumberLines();
            SetDirty();
        }

        public void MoveSelectedUp()
        {
            int i = _selectedLineIndex;
            if (i <= 0 || i >= Lines.Count) return;
            (Lines[i].Text, Lines[i - 1].Text) = (Lines[i - 1].Text, Lines[i].Text);
            SelectedLineIndex = i - 1;
        }

        public void MoveSelectedDown()
        {
            int i = _selectedLineIndex;
            if (i < 0 || i >= Lines.Count - 1) return;
            (Lines[i].Text, Lines[i + 1].Text) = (Lines[i + 1].Text, Lines[i].Text);
            SelectedLineIndex = i + 1;
        }

        public void AddArchive()
        {
            int newId = ArchiveCount;
            _removedArchives.Remove(newId);
            _addedArchives[newId] = new List<string> { "New text." };
            ArchiveNames.Add("Text archive " + newId);
            SelectedArchiveIndex = newId;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        public async Task RemoveArchiveAsync()
        {
            if (ArchiveNames.Count == 0) return;
            if (!await DialogHelper.AskYesNoAsync("Delete the last text archive? It is removed on Save.", "Delete text archive?"))
                return;

            int lastIndex = ArchiveNames.Count - 1;
            if (!_addedArchives.Remove(lastIndex)) _removedArchives.Add(lastIndex);

            if (SelectedArchiveIndex == lastIndex)
            {
                SetClean();
                _isLoading = true;
                SelectedArchiveIndex = lastIndex - 1;
                _isLoading = false;
                if (lastIndex - 1 >= 0) LoadArchive(lastIndex - 1);
            }
            ArchiveNames.RemoveAt(lastIndex);
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }
}
