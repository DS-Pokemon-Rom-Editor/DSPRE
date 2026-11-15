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
        public IReadOnlyList<string> PlayModes { get; } =
            new[] { "Once", "Loops", "Backwards", "Loops backwards" };

        private int _playMode;
        public int PlayModeIndex
        {
            get => _playMode;
            set
            {
                if (!Set(ref _playMode, value)) return;
                OnPropertyChanged(nameof(CanLoop));
                if (_loading || _filling || _file == null || value < 0 || value >= PlayModes.Count) return;

                Stop();
                Step before = Now();
                _file.SetPlayMode(_sequence, value + 1);
                Remember(before);
                Dirty = true;
                if (_sequence >= 0 && _sequence < Sequences.Count)
                    Sequences[_sequence].Freshen(Frames.Count, _file.Sequences[_sequence].PlayMode);
                StatusText = value switch
                {
                    1 => "This sequence now loops.",
                    2 => "This sequence now plays backwards.",
                    3 => "This sequence now loops backwards.",
                    _ => "This sequence now plays once.",
                };
            }
        }

        /// <summary>Anything but running once has a frame it comes back to.</summary>
        public bool CanLoop => _playMode > 0;

        private int _loopStart = 1;
        public int LoopStart
        {
            get => _loopStart;
            set
            {
                if (!Set(ref _loopStart, value)) return;
                if (_loading || _filling || _file == null) return;

                Stop();
                Step before = Now();
                _file.SetLoopStart(_sequence, value - 1);
                Remember(before);
                Dirty = true;
                StatusText = $"It now loops back to frame {value}.";
            }
        }

        public int FrameCount => Frames.Count;

        public IReadOnlyList<string> AnimationTypes { get; } = new[] { "Cell banks", "Multi-cell banks" };

        private int _animationType;
        public int AnimationTypeIndex
        {
            get => _animationType;
            set
            {
                if (!Set(ref _animationType, value)) return;
                OnPropertyChanged(nameof(AnimationTypeWarning));
                OnPropertyChanged(nameof(HasAnimationTypeWarning));
                if (_loading || _filling || _file == null || value < 0 || value >= AnimationTypes.Count) return;

                Stop();
                Step before = Now();
                string trouble = _file.SetAnimationType(_sequence, value + 1);
                if (trouble != null) { StatusText = trouble; return; }
                Remember(before);
                Dirty = true;
                StatusText = value == 1
                    ? "This sequence now draws from multi-cell banks."
                    : "This sequence now draws from cell banks.";
            }
        }

        public bool HasAnimationTypeWarning => _animationType == 1;

        /// <summary>Shown because a sequence set this way draws nothing without multi-cell data.</summary>
        public string AnimationTypeWarning => _animationType != 1 ? null
            : "The games use cell banks. Set this way, the sequence only draws if the game has multi-cell "
            + "data for it.";

        /// <summary>Adds a sequence at the end only, since games reach sequences by number.</summary>
        public void AddSequence()
        {
            if (_file == null) return;
            Stop();
            Step before = Now();
            string trouble = _file.AddSequence();
            if (trouble != null) { StatusText = trouble; return; }

            Remember(before);
            Dirty = true;
            FillSequences();
            SelectedSequence = _file.Sequences.Count - 1;
            OnPropertyChanged(nameof(FileNote));
            StatusText = $"Added sequence {_file.Sequences.Count - 1} on the end.";
        }

        public void RemoveLastSequence()
        {
            if (_file == null) return;
            Stop();
            Step before = Now();
            string trouble = _file.RemoveLastSequence();
            if (trouble != null) { StatusText = trouble; return; }

            Remember(before);
            Dirty = true;
            FillSequences();
            OnPropertyChanged(nameof(FileNote));
            StatusText = "Removed the last sequence.";
        }

        public void AddFrameAfter(int number)
        {
            if (_file == null || _sequence < 0) return;
            Stop();
            Step before = Now();
            string trouble = _file.AddFrame(_sequence, number);
            if (trouble != null) { StatusText = trouble; return; }

            Remember(before);
            Dirty = true;
            FillFrames();
            if (_sequence < Sequences.Count)
                Sequences[_sequence].Freshen(Frames.Count, _file.Sequences[_sequence].PlayMode);
            OnPropertyChanged(nameof(FileNote));
            StatusText = $"Added a frame after frame {number + 1}.";
        }

        public void RemoveFrameAt(int number)
        {
            if (_file == null || _sequence < 0) return;
            Stop();
            Step before = Now();
            string trouble = _file.RemoveFrame(_sequence, number);
            if (trouble != null) { StatusText = trouble; return; }

            Remember(before);
            Dirty = true;
            FillFrames();
            if (_sequence < Sequences.Count)
                Sequences[_sequence].Freshen(Frames.Count, _file.Sequences[_sequence].PlayMode);
            OnPropertyChanged(nameof(FileNote));
            StatusText = $"Removed frame {number + 1}.";
        }
    }
}
