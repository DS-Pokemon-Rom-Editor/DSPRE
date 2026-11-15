using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using DSPRE.Avalonia.Data;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    public sealed partial class CellAnimationEditorViewModel
    {
        private Step Now() => new(_file?.Write(), _layout?.Write());

        private void Remember(Step before, bool merge = false)
        {
            if (before.Animation == null && before.Layout == null) return;
            if (!merge) _lastEditFrame = _lastMoveCell = _lastMovePiece = -1;
            // The step starts from exactly what was there before this edit.
            if (!merge) _history.Capture(before, coalesce: true);
            _history.Capture(Now(), coalesce: merge);
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        }

        public void Undo() { if (_history.CanUndo) StepTo(_history.Undo()); }
        public void Redo() { if (_history.CanRedo) StepTo(_history.Redo()); }

        private void StepTo(Step step)
        {
            if (step.Animation != null) _file = NanrFile.Read(step.Animation);
            if (step.Layout != null)
            {
                _layout = NcerFile.Read(step.Layout);
                _banks = DsBgScreen.ReadCells(step.Layout);
            }

            Dirty = true;
            // Undoing an add or a removal changes the sequence count, so the list is rebuilt.
            if (Sequences.Count != _file.Sequences.Count)
            {
                int keep = Math.Clamp(_sequence, 0, _file.Sequences.Count - 1);
                FillSequences();
                SelectedSequence = keep;
            }
            FillFrames();
            RefreshSequenceRows();
            StatusText = "";
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(FileNote));
        }

        // Stepping back can reach any sequence, not just the chosen one, so every row is refreshed.
        private void RefreshSequenceRows()
        {
            if (_file == null) return;
            for (int i = 0; i < Sequences.Count && i < _file.Sequences.Count; i++)
                Sequences[i].Freshen(_file.Sequences[i].Frames.Count, _file.Sequences[i].PlayMode);
        }
    }
}
