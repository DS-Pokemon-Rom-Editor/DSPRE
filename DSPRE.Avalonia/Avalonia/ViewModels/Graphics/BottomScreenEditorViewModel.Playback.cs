using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using DSPRE.Avalonia.Data;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    public sealed partial class BottomScreenEditorViewModel
    {
        private NanrFile _animation;
        private int _sequence;
        private PoketchScreen.Motion _motion = PoketchScreen.Motion.Still;

        private DispatcherTimer _timer;
        private readonly System.Diagnostics.Stopwatch _clock = new();
        private int _frame, _shown, _ticksRun, _held;
        private bool _reverse;

        // The Pokétch hands its animations two frames of time per screen refresh.
        private const int TicksPerRefresh = 2;

        public bool Playing => _timer != null;
        public string PlayLabel => Playing ? "Stop" : "Play";

        public bool CanPlay => _animation != null && _animation.Sequences.Count > 0;

        /// <summary>Which sequence to run. Several files hold one sequence per pose or angle.</summary>
        public int PlaySequence
        {
            get => _sequence;
            set
            {
                if (!Set(ref _sequence, value)) return;
                OnPropertyChanged(nameof(PlayNote));
                if (Playing) { Stop(); Play(); }
            }
        }

        public int PlaySequenceCount => _animation?.Sequences.Count ?? 0;

        public string PlayNote
        {
            get
            {
                if (_animation == null) return null;
                if (_sequence < 0 || _sequence >= _animation.Sequences.Count) return null;
                NanrFile.Sequence s = _animation.Sequences[_sequence];
                string mode = s.PlayMode switch
                {
                    2 => "loops",
                    3 => "backwards",
                    4 => "loops backwards",
                    _ => "once",
                };
                return $"Sequence {_sequence} of {_animation.Sequences.Count}, "
                     + $"{s.Frames.Count} frame{(s.Frames.Count == 1 ? "" : "s")}, {mode}";
            }
        }

        private void LoadAnimation()
        {
            _animation = null;
            _sequence = 0;
            _motion = PoketchScreen.Motion.Still;
            try
            {
                PoketchApps.App app = _app >= 1 && _app <= PoketchApps.All.Length ? PoketchApps.All[_app - 1] : null;
                if (app == null || app.Animation < 0) return;
                using (Pending.Reading())
                    _animation = NanrFile.Read(NitroBgCodec.Inflate(new ScriptNarc(DirNames.Poketch).Get(app.Animation)));
            }
            catch (Exception ex) { AppLogger.Error("Poketch animation: " + ex.Message); }

            OnPropertyChanged(nameof(CanPlay));
            OnPropertyChanged(nameof(PlaySequence));
            OnPropertyChanged(nameof(PlaySequenceCount));
            OnPropertyChanged(nameof(PlayNote));
        }

        public void TogglePlay()
        {
            if (Playing) Stop(); else Play();
        }

        private void Play()
        {
            if (!CanPlay) return;
            if (_sequence < 0 || _sequence >= _animation.Sequences.Count) return;
            if (_animation.Sequences[_sequence].Frames.Count == 0) return;

            uint mode = _animation.Sequences[_sequence].PlayMode;
            _reverse = mode == 3 || mode == 4;
            _frame = _reverse ? _animation.Sequences[_sequence].Frames.Count - 1 : 0;
            _shown = _frame;
            _held = 0; _ticksRun = 0;
            _clock.Restart();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(5) };
            _timer.Tick += Beat;
            _timer.Start();
            OnPropertyChanged(nameof(Playing));
            OnPropertyChanged(nameof(PlayLabel));
            ShowFrame();
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

        // As the animation editor plays: zero holds skipped, loops return, backwards modes turn round.
        private void Beat(object sender, EventArgs e)
        {
            if (!CanPlay || _sequence < 0 || _sequence >= _animation.Sequences.Count) { Stop(); return; }
            NanrFile.Sequence s = _animation.Sequences[_sequence];
            if (s.Frames.Count == 0) { Stop(); return; }

            int due = (int)(_clock.Elapsed.TotalSeconds * 60 * TicksPerRefresh);
            if (due - _ticksRun > 4 * TicksPerRefresh) _ticksRun = due - 1;

            bool loops = s.PlayMode == 2 || s.PlayMode == 4;
            bool backwards = s.PlayMode == 3 || s.PlayMode == 4;
            int loopStart = Math.Clamp(s.LoopStartFrame, 0, s.Frames.Count - 1);
            int was = _shown;

            while (_ticksRun < due && Playing)
            {
                _ticksRun++;
                _held++;

                for (int steps = 0; Playing && _held >= s.Frames[_frame].Delay && steps <= s.Frames.Count; steps++)
                {
                    _held = 0;
                    _frame += _reverse ? -1 : 1;

                    if (_frame >= s.Frames.Count || _frame < loopStart)
                    {
                        if (backwards)
                        {
                            bool atStart = _frame < loopStart;
                            _reverse = !_reverse;
                            _frame = Math.Clamp(_frame, loopStart, s.Frames.Count - 1);
                            if (atStart && !loops) { Stop(); break; }
                        }
                        else if (loops) _frame = loopStart;
                        else { _frame = s.Frames.Count - 1; Stop(); break; }
                    }

                    if (s.Frames[_frame].Delay > 0) { _shown = _frame; break; }
                }
            }
            if (_shown != was) ShowFrame();
        }

        // The drawing and its pose both come from the animation, not the last bank stepped to.
        private void ShowFrame()
        {
            if (_animation == null) return;
            (double turn, double across, double down) = _animation.TurnOf(_sequence, _shown);
            (int shiftX, int shiftY) = _animation.ShiftOf(_sequence, _shown);
            _motion = new PoketchScreen.Motion(turn, across, down, shiftX, shiftY);

            int cell = _animation.CellOf(_sequence, _shown);
            if (!Set(ref _bank, Math.Max(0, cell))) Draw();
            else { OnPropertyChanged(nameof(BankNote)); Draw(); }
        }

        // Stand-in wording to fill the boxes. A script supplies the real words.
        private static string[] SampleLabels(int count, bool yesNo) =>
            yesNo ? new[] { "YES", "NO" }
                  : Enumerable.Range(1, count).Select(n => "Answer " + n).ToArray();

        // A picked layout wins over the toolbar, so "List of 5" shows five boxes.
        private (string[] Labels, bool YesNo) ChoiceShown()
        {
            int count = ChoiceCount;
            bool yesNo = YesNo;

            BottomScreenPiece p = Selected;
            if (p != null && p.Arrangement >= 0)
            {
                if (p.Arrangement == YesNoMap) { yesNo = true; count = 2; }
                else if (p.Arrangement >= 2 && p.Arrangement <= 8) { yesNo = false; count = p.Arrangement; }
                else if (p.Arrangement == PokeBallMap) return (null, false);
            }

            return (SampleLabels(count, yesNo), yesNo);
        }
    }
}
