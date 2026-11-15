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
        private bool _everywhere;
        /// <summary>Whether changing a shared drawing changes it for every frame that shares it.</summary>
        public bool ChangeEverywhere { get => _everywhere; set => Set(ref _everywhere, value); }

        private void FrameEdited(CellFrameRow row)
        {
            if (_loading || _filling || _file == null) return;

            if (row.Cell >= _banks.Count && _banks.Count > 0)
            {
                StatusText = $"This layout holds {_banks.Count} drawings, numbered 0 to {_banks.Count - 1}.";
                Restore(row);
                return;
            }

            // A number box commits on every keystroke, so keystrokes on one frame collapse into one step.
            bool sameAsLast = _lastEditFrame == row.Number && _history.CanUndo;
            Step before = Now();

            _file.SetDelay(_sequence, row.Number, row.Delay);
            if (row.Cell != _file.CellOf(_sequence, row.Number))
                _file.SetCell(_sequence, row.Number, row.Cell, _everywhere);

            string trouble = null;
            if (row.CanTurn)
            {
                (double turn, double across, double down) = _file.TurnOf(_sequence, row.Number);
                if (Differs(turn, row.Turn) || Differs(across, row.StretchAcross)
                                            || Differs(down, row.StretchDown))
                    trouble = _file.SetTurn(_sequence, row.Number, row.Turn,
                                            row.StretchAcross, row.StretchDown, _everywhere);
            }
            if (trouble == null && row.CanShift)
            {
                (int across, int down) = _file.ShiftOf(_sequence, row.Number);
                if (across != row.ShiftAcross || down != row.ShiftDown)
                    trouble = _file.SetShift(_sequence, row.Number, row.ShiftAcross, row.ShiftDown,
                                             _everywhere);
            }
            if (trouble != null)
            {
                StatusText = trouble;
                Restore(row);
                return;
            }

            Remember(before, sameAsLast);
            _lastEditFrame = row.Number;
            _lastMoveCell = -1;
            _lastMovePiece = -1;

            Dirty = true;
            RefreshSharing();
            Draw();
            StatusText = _everywhere ? "Changed for every frame that shares it." : "Changed for this frame.";
        }

        // Turns are stored in hardware units, so they never match what was typed exactly.
        private static bool Differs(double a, double b) => Math.Abs(a - b) > 0.01;

        private void Fill(CellFrameRow row)
        {
            (int across, int down) = _file.ShiftOf(_sequence, row.Number);
            (double turn, double stretchAcross, double stretchDown) = _file.TurnOf(_sequence, row.Number);
            row.Quietly(_file.CellOf(_sequence, row.Number),
                        _file.Sequences[_sequence].Frames[row.Number].Delay,
                        turn, stretchAcross, stretchDown, across, down);
        }

        private void Restore(CellFrameRow row)
        {
            _filling = true;
            Fill(row);
            _filling = false;
        }

        private string ShareNote(int frame)
        {
            int shared = _file.SharedWith(_sequence, frame);
            if (shared <= 0) return null;
            string what = _file.Sequences[_sequence].ElementType switch
            {
                1 => "drawing, turn and position",
                2 => "drawing and position",
                _ => "drawing",
            };
            return $"This {what} is shared with {shared} other frame{(shared == 1 ? "" : "s")}";
        }

        // Splitting a shared result changes how many frames share it, so the notes have to follow.
        private void RefreshSharing()
        {
            if (_file == null) return;
            _filling = true;
            for (int i = 0; i < Frames.Count; i++)
            {
                Frames[i].SharedNote = ShareNote(i);
                Fill(Frames[i]);
            }
            _filling = false;
        }
    }
}
