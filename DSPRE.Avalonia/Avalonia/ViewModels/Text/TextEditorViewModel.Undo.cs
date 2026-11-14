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
        private ByteStateUndo _undo;
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() { _undo?.Undo(); SyncDirtyWithUndo(); }
        public void Redo() { _undo?.Redo(); SyncDirtyWithUndo(); }
        private void SyncDirtyWithUndo()
        {
            if (_undo == null || _undo.IsDirty == _dirty) return;
            _dirty = _undo.IsDirty; OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private byte[] MessagesState() => ByteStateUndo.Pack(w =>
        {
            foreach (string m in _current.Messages) w.Write(m ?? "");
        });

        private void RestoreMessages(byte[] state)
        {
            List<string> messages = new List<string>();
            ByteStateUndo.Unpack(state, r =>
            {
                while (r.BaseStream.Position < r.BaseStream.Length) messages.Add(r.ReadString());
            });
            _current.Messages.Clear();
            _current.Messages.AddRange(messages);
            ShowLines();
            SetDirty();
            RefreshPreview();
        }

        private void ShowLines()
        {
            bool wasLoading = _isLoading;
            int keep = _selectedLineIndex;
            _isLoading = true;
            try
            {
                foreach (TextLineVM l in Lines) l.PropertyChanged -= OnLineChanged;
                Lines.Clear();
                for (int i = 0; i < _current.Messages.Count; i++)
                {
                    TextLineVM line = new TextLineVM(i, _current.Messages[i]);
                    line.PropertyChanged += OnLineChanged;
                    Lines.Add(line);
                }
                RenumberLines();
            }
            finally { _isLoading = wasLoading; }
            // Refilling the list clears the selection, which would leave the preview empty.
            if (Lines.Count > 0) SelectedLineIndex = Math.Clamp(keep, 0, Lines.Count - 1);
        }

        private void LoadArchive(int id)
        {
            _isLoading = true;
            try
            {
                _current = _addedArchives.TryGetValue(id, out List<string> added)
                    ? new TextArchive(id, new List<string>(added))
                    : new TextArchive(id);
                HgEngineOwnedFile owned = HgEngineOwnedFiles.Get(TextArchivePath, id);
                _generatedSource = owned?.Ownership == HgEngineOwnership.Generated ? owned : null;
                _managedSource = owned?.Ownership == HgEngineOwnership.EditableSource ? owned : null;
                if (_managedSource != null)
                {
                    if (HgEngineOwnedFiles.TryReadLines(_managedSource, out List<string> sourceLines, out string readError))
                    {
                        _current.Messages.Clear();
                        _current.Messages.AddRange(sourceLines);
                    }
                    else
                    {
                        _managedSource = null;
                        StatusText = readError;
                    }
                }
                _fieldSource = _generatedSource != null ? HgEngineGeneratedText.For(id) : null;
                _fieldBefore = null;
                if (_fieldSource?.IsMain == true)
                {
                    if (HgEngineGeneratedText.TryReadLines(_fieldSource, _current.Messages.Count, out string[] fieldLines, out string fieldError))
                    {
                        for (int i = 0; i < fieldLines.Length; i++)
                            if (fieldLines[i] != null) _current.Messages[i] = fieldLines[i];
                        _fieldBefore = new List<string>(_current.Messages);
                    }
                    else
                    {
                        _fieldSource = null;
                        StatusText = fieldError;
                    }
                }
                OnPropertyChanged(nameof(IsManagedByHgEngine));
                OnPropertyChanged(nameof(IsGeneratedByHgEngine));
                OnPropertyChanged(nameof(HasHgEngineNote));
                OnPropertyChanged(nameof(ManagedNote));

                ShowLines();
                SetClean();
                _undo = new ByteStateUndo(MessagesState, RestoreMessages,
                    () => { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); });
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));
                RefreshPreview();
                StatusText = _managedSource != null
                    ? $"Loaded {_managedSource.RelPath} ({_current.Messages.Count} lines) from the hg-engine checkout."
                    : $"{_current.Messages.Count} lines";
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void OnLineChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_isLoading || e.PropertyName != nameof(TextLineVM.Text)) return;
            if (sender is TextLineVM line && _current != null && line.Index < _current.Messages.Count)
            {
                _current.Messages[line.Index] = line.Text ?? "";
                SetDirty();
                if (line.Index == _selectedLineIndex) RefreshPreview();
            }
        }

        private void RenumberLines()
        {
            for (int i = 0; i < Lines.Count; i++)
            {
                Lines[i].Index = i;
                Lines[i].Number = _hexNumbering ? "0x" + i.ToString("X") : i.ToString();
            }
        }
    }
}
