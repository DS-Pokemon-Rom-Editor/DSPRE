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
        private ByteStateUndo _undo;
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();
        private void RaiseUndo() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private void ResetUndo()
        {
            if (_file == null) return;
            _undo = new ByteStateUndo(() => _file.ToByteArray(), ApplyState, RaiseUndo);
            RaiseUndo();
        }

        private void ApplyState(byte[] state)
        {
            int group = _selGroup;
            bool special = _specialGroupActive;
            _file = new HeadbuttEncounterFile((ushort)System.Math.Max(0, _selFile), state);
            BuildEncounterRows();
            RefreshGroups();
            _selGroup = group;
            _specialGroupActive = special;
            ShowGroupTrees();
            bool dirty = _undo.IsDirty;
            if (_dirty != dirty) { _dirty = dirty; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        }
    }
}
