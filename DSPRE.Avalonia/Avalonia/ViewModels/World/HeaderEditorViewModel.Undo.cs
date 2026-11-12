using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using global::Avalonia.Collections;
using global::Avalonia.Controls;
using global::Avalonia.Threading;
using global::Avalonia.Media;
using global::Avalonia.Media.Imaging;
using DSPRE.Avalonia;
using DSPRE.Avalonia.Models;
using DSPRE.Editors;
using DSPRE.Resources;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;
using DSPRE.Avalonia.Data;
using Avalonia.Platform.Storage;

namespace DSPRE.Avalonia.ViewModels.World
{
    public partial class HeaderEditorViewModel
    {
        private readonly UndoHistory<byte[]> _history = new();
        private DateTime _lastCaptureUtc = DateTime.MinValue;
        private const int CoalesceMs = 500;

        public bool CanUndo => _history.CanUndo || _removedHeaders.Count > 0;
        public bool CanRedo => _history.CanRedo;
        public void Undo()
        {
            if (_history.CanUndo) ApplyState(_history.Undo());
            else if (_removedHeaders.Count > 0) RestoreRemovedHeader();
        }
        public void Redo() { if (_history.CanRedo) ApplyState(_history.Redo()); }
        private void RaiseUndoState() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private void ApplyState(byte[] bytes)
        {
            if (bytes == null || _header == null) return;
            int headerLength = _header.ToByteArray().Length;
            _header = MapHeader.LoadFromByteArray(bytes.AsSpan(0, headerLength).ToArray(), _header.ID);
            PopulateFromHeader();   // manages _suppress itself
            _suppress = true;
            try { InternalName = System.Text.Encoding.UTF8.GetString(bytes, headerLength, bytes.Length - headerLength); }
            finally { _suppress = false; }
            _dirty = _history.IsDirty;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            RaiseUndoState();
        }

        // The internal name is in another file, so it rides along after the header bytes.
        private byte[] UndoState() => _header.ToByteArray().Concat(System.Text.Encoding.UTF8.GetBytes(_internalName ?? "")).ToArray();

        private void RecordUndoSnapshot()
        {
            if (_suppress || _header == null) return;
            bool coalesce = (DateTime.UtcNow - _lastCaptureUtc).TotalMilliseconds < CoalesceMs;
            _history.Capture(UndoState(), coalesce);
            _lastCaptureUtc = DateTime.UtcNow;
            RaiseUndoState();
        }
    }
}
