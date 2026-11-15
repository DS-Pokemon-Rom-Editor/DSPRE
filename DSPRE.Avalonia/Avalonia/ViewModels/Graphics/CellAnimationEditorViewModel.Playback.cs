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
        private DispatcherTimer _timer;
        private readonly Stopwatch _clock = new();
        private int _frame, _ticksRun, _held;

        public bool Playing => _timer != null;
        public string PlayLabel => Playing ? "Stop" : "Play";

        public void TogglePlay()
        {
            if (Playing) Stop(); else Play();
        }

        private void Play()
        {
            if (_file == null || Frames.Count == 0 || _sequence < 0 || _sequence >= _file.Sequences.Count) return;

            uint mode = _file.Sequences[_sequence].PlayMode;
            _reverse = mode == 3 || mode == 4;
            _frame = _reverse ? Frames.Count - 1 : 0;
            _shown = _frame;
            _held = 0; _ticksRun = 0;
            _clock.Restart();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(5) };
            _timer.Tick += Beat;
            _timer.Start();
            OnPropertyChanged(nameof(Playing));
            OnPropertyChanged(nameof(PlayLabel));
        }

        public void Stop()
        {
            if (_timer == null) return;
            _timer.Stop();
            _timer.Tick -= Beat;
            _timer = null;
            _clock.Stop();
            OnPropertyChanged(nameof(Playing));
            OnPropertyChanged(nameof(PlayLabel));
        }

        // _shown is the frame on screen. A zero hold is stepped past without being shown.
        private bool _reverse;
        private int _shown;

        private void Beat(object sender, EventArgs e)
        {
            if (_file == null || Frames.Count == 0 || _sequence < 0 || _sequence >= _file.Sequences.Count)
            { Stop(); return; }

            int due = (int)(_clock.Elapsed.TotalSeconds * 60 * TicksPerFrame);
            // Clamped so a hitch does not fast-forward.
            if (due - _ticksRun > 4 * TicksPerFrame) _ticksRun = due - 1;

            NanrFile.Sequence s = _file.Sequences[_sequence];
            bool loops = s.PlayMode == 2 || s.PlayMode == 4;
            bool backwards = s.PlayMode == 3 || s.PlayMode == 4;
            int loopStart = Math.Clamp(s.LoopStartFrame, 0, Frames.Count - 1);
            int was = _shown;

            while (_ticksRun < due && Playing)
            {
                _ticksRun++;
                _held++;

                // The step count stops a sequence of nothing but zero holds from spinning here.
                for (int steps = 0; Playing && _held >= Frames[_frame].Delay && steps <= Frames.Count; steps++)
                {
                    _held = 0;
                    _frame += _reverse ? -1 : 1;

                    if (_frame >= Frames.Count || _frame < loopStart)
                    {
                        if (backwards)
                        {
                            bool atStart = _frame < loopStart;
                            _reverse = !_reverse;
                            _frame = Math.Clamp(_frame, loopStart, Frames.Count - 1);
                            if (atStart && !loops) { Stop(); break; }
                        }
                        else if (loops) _frame = loopStart;
                        else { _frame = Frames.Count - 1; Stop(); break; }
                    }

                    if (Frames[_frame].Delay > 0) { _shown = _frame; break; }
                }
            }
            if (_shown != was) Draw();
        }

        /// <summary>Animation frames per screen refresh. The Pokétch drives its own at two.</summary>
        public int TicksPerFrame { get; set; } = 1;

        public int PlayingFrame => _shown;
    }
}
