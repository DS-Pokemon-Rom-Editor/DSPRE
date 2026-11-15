using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using DSPRE.Resources;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;
using static DSPRE.MoveData;
using static DSPRE.RomInfo;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.Views.Shell;
using DSPRE.Csv;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    public partial class MoveDataEditorViewModel
    {
        // A snapshot holds everything Undo restores. Edits within CoalesceMs merge into one step.
        private readonly UndoHistory<byte[]> _history = new();
        private System.DateTime _lastCaptureUtc = System.DateTime.MinValue;
        private const int CoalesceMs = 500;

        public bool CanUndo => _history.CanUndo;
        public bool CanRedo => _history.CanRedo;
        public void Undo() { if (_history.CanUndo) ApplyState(_history.Undo()); }
        public void Redo() { if (_history.CanRedo) ApplyState(_history.Redo()); }

        private void RaiseUndoState() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private void RecordUndoSnapshot()
        {
            if (_currentFile == null) return;
            bool coalesce = (System.DateTime.UtcNow - _lastCaptureUtc).TotalMilliseconds < CoalesceMs;
            _history.Capture(Snapshot(), coalesce);
            _lastCaptureUtc = System.DateTime.UtcNow;
            RaiseUndoState();
        }

        private void ApplyState(byte[] bytes)
        {
            if (bytes == null) return;
            UndoState state = DSPRE.Avalonia.UndoJson.Read<UndoState>(bytes);
            _loading = true;
            _currentFile = new MoveData(new MemoryStream(state.Move));
            PopulateFromCurrentFile();
            Description = state.Description;
            BagText = state.Bag;
            _punching?.Set(_currentId, state.Punching);
            _sound?.Set(_currentId, state.Sound);
            for (int i = 0; i < ExtraLists.Count && state.Extra != null && i < state.Extra.Length; i++)
            {
                ushort id = (ushort)_currentId;
                if (state.Extra[i] && !ExtraLists[i].Moves.Contains(id)) ExtraLists[i].Moves.Add(id);
                else if (!state.Extra[i]) ExtraLists[i].Moves.Remove(id);
            }
            _loading = false;
            RaiseCategories();

            _dirty = _history.IsDirty;
            RefreshDirty();
            RaiseUndoState();
        }
    }
}
