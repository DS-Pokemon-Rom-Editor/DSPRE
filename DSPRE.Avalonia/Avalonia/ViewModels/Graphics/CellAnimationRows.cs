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
    /// <summary>One frame of a sequence, as a row that can be edited.</summary>
    public sealed class CellFrameRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        internal Action<CellFrameRow> Changed;

        public int Number { get; init; }
        public string Title => "Frame " + (Number + 1);

        /// <summary>How the sequence stores its frames, which decides what this frame can carry.</summary>
        public int Element { get; init; }
        public bool CanTurn => Element == 1;
        public bool CanShift => Element != 0;

        private int _cell;
        public int Cell
        {
            get => _cell;
            set { if (_cell == value) return; _cell = value; Raise(); Changed?.Invoke(this); }
        }

        private int _delay;
        public int Delay
        {
            get => _delay;
            set { if (_delay == value) return; _delay = Math.Clamp(value, 0, 9999); Raise(); Changed?.Invoke(this); }
        }

        private double _turn;
        public double Turn
        {
            get => _turn;
            set { if (_turn == value) return; _turn = value; Raise(); Changed?.Invoke(this); }
        }

        private double _stretchAcross = 1, _stretchDown = 1;
        public double StretchAcross
        {
            get => _stretchAcross;
            set { if (_stretchAcross == value) return; _stretchAcross = value; Raise(); Changed?.Invoke(this); }
        }
        public double StretchDown
        {
            get => _stretchDown;
            set { if (_stretchDown == value) return; _stretchDown = value; Raise(); Changed?.Invoke(this); }
        }

        private int _shiftAcross, _shiftDown;
        public int ShiftAcross
        {
            get => _shiftAcross;
            set { if (_shiftAcross == value) return; _shiftAcross = value; Raise(); Changed?.Invoke(this); }
        }
        public int ShiftDown
        {
            get => _shiftDown;
            set { if (_shiftDown == value) return; _shiftDown = value; Raise(); Changed?.Invoke(this); }
        }

        public string SharedNote { get; internal set; }
        public bool IsShared => !string.IsNullOrEmpty(SharedNote);

        internal void Quietly(int cell, int delay, double turn, double stretchAcross, double stretchDown,
                              int shiftAcross, int shiftDown)
        {
            _cell = cell; _delay = delay;
            _turn = turn; _stretchAcross = stretchAcross; _stretchDown = stretchDown;
            _shiftAcross = shiftAcross; _shiftDown = shiftDown;
            Raise(nameof(Cell)); Raise(nameof(Delay)); Raise(nameof(Turn));
            Raise(nameof(StretchAcross)); Raise(nameof(StretchDown));
            Raise(nameof(ShiftAcross)); Raise(nameof(ShiftDown));
            Raise(nameof(SharedNote)); Raise(nameof(IsShared));
        }
    }

    /// <summary>One piece of a drawing, as a row whose position can be edited.</summary>
    public sealed class CellPieceRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        internal Action<CellPieceRow> Changed;

        public int Number { get; init; }
        public string Size { get; init; } = "";
        public string Title => $"Piece {Number + 1}  ({Size})";

        private int _x;
        public int Across
        {
            get => _x;
            set { if (_x == value) return; _x = value; Raise(); Changed?.Invoke(this); }
        }

        private int _y;
        public int Down
        {
            get => _y;
            set { if (_y == value) return; _y = value; Raise(); Changed?.Invoke(this); }
        }

        internal void Quietly(int x, int y)
        {
            _x = x; _y = y;
            Raise(nameof(Across)); Raise(nameof(Down));
        }
    }

    /// <summary>One sequence in the file, as a row in the list.</summary>
    public sealed class CellSequenceRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public int Number { get; init; }
        public string Name { get; init; }
        public int Element { get; init; }

        private int _frames;
        public int Frames { get => _frames; init => _frames = value; }

        private uint _playMode;
        public uint PlayMode { get => _playMode; init => _playMode = value; }

        // The row is updated in place, since replacing the selected item would drop the selection.
        internal void Freshen(int frames, uint playMode)
        {
            _frames = frames; _playMode = playMode;
            Raise(nameof(Frames)); Raise(nameof(PlayMode)); Raise(nameof(Detail));
        }

        public string Title => $"{Number}  {(string.IsNullOrWhiteSpace(Name) ? "(no name)" : Name)}";

        public string Detail
        {
            get
            {
                string kind = Element switch
                {
                    1 => "turn and stretch",
                    2 => "shift",
                    _ => "frame order",
                };
                string mode = PlayMode switch
                {
                    2 => "loops",
                    3 => "backwards",
                    4 => "loops backwards",
                    _ => "once",
                };
                return $"{Frames} frame{(Frames == 1 ? "" : "s")}, {kind}, {mode}";
            }
        }
    }
}
