using System;
using System.Collections.Generic;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Plays a script's steps out on the field's own clock instead of reporting them all at once, so a
    /// movement can be watched happening and a wait actually waits.
    ///
    /// Everything is counted in frames at the field's thirty a second. A step that starts something with
    /// a length, a movement or a shake, holds the script until it has run its course, which is what the
    /// games' own wait commands do.
    /// </summary>
    public sealed class FieldScriptRunner
    {
        /// <summary>What the runner wants done. The preview supplies these.</summary>
        public sealed class Hooks
        {
            /// <summary>Start a movement on an overworld. Return how many frames it will take, or 0.</summary>
            public Func<int, int, int> StartMovement;
            /// <summary>Play a sound. The kind says which of the three it is.</summary>
            public Action<ScriptEffectKind, int> PlaySound;
            /// <summary>Start the view shaking: across, down, how many times, frames each.</summary>
            public Action<int, int, int, int> ShakeCamera;
            /// <summary>Move the view to one of the alternative settings. Returns how long it takes.</summary>
            public Func<int, int> MoveCamera;
            /// <summary>
            /// Show a line of dialogue. Returns whether a box actually opened: the script only holds
            /// when one did, because otherwise there is nothing for the reader to press on and the
            /// script would sit there forever.
            /// </summary>
            public Func<string, bool> ShowMessage;
            /// <summary>Anything else, reported so the panel can list it.</summary>
            public Action<ScriptStep> Report;
        }

        private readonly List<ScriptStep> _steps = new List<ScriptStep>();
        private readonly Hooks _hooks;
        private int _at;
        private int _holdFrames;
        private bool _waitingOnReader;

        public FieldScriptRunner(Hooks hooks) { _hooks = hooks ?? new Hooks(); }

        /// <summary>Whether there is still something to play.</summary>
        public bool Running => _at < _steps.Count || _holdFrames > 0 || _waitingOnReader;

        /// <summary>Which step it is on, for showing progress.</summary>
        public int StepIndex => Math.Min(_at, _steps.Count);
        public int StepCount => _steps.Count;

        /// <summary>How many frames it is still holding for, so a caller can say what it is waiting on.</summary>
        public int HoldingFrames => _holdFrames;

        /// <summary>True while a message is up and the reader has not moved on.</summary>
        public bool WaitingOnReader => _waitingOnReader;

        /// <summary>Starts playing a set of steps from the beginning.</summary>
        public void Play(IEnumerable<ScriptStep> steps)
        {
            _steps.Clear();
            if (steps != null) _steps.AddRange(steps);
            _at = 0;
            _holdFrames = 0;
            _waitingOnReader = false;
        }

        public void Stop()
        {
            _steps.Clear();
            _at = 0;
            _holdFrames = 0;
            _waitingOnReader = false;
        }

        /// <summary>The reader has pressed on, so a message stops holding the script up.</summary>
        public void ReaderMovedOn() => _waitingOnReader = false;

        /// <summary>Moves the clock on. Call once a frame.</summary>
        public void Advance(int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                if (_waitingOnReader) return;
                if (_holdFrames > 0) { _holdFrames--; continue; }
                if (_at >= _steps.Count) return;
                DoOne(_steps[_at++]);
            }
        }

        // Holds for a run of frames counting the one it started on.
        private void Hold(int frames) => _holdFrames = Math.Max(0, frames - 1);

        private void DoOne(ScriptStep step)
        {
            var effect = step.Effect;
            if (effect == null)
            {
                _hooks.Report?.Invoke(step);
                if (step.Kind == ScriptStepKind.Message)
                    _waitingOnReader = _hooks.ShowMessage?.Invoke(step.Text) ?? false;
                return;
            }

            _hooks.Report?.Invoke(step);
            switch (effect.Kind)
            {
                case ScriptEffectKind.Movement:
                    // The frame it starts on is the first of its frames, so it holds for one less.
                    Hold(_hooks.StartMovement?.Invoke(effect.A, effect.B) ?? 0);
                    break;

                case ScriptEffectKind.SoundEffect:
                case ScriptEffectKind.Fanfare:
                case ScriptEffectKind.Music:
                case ScriptEffectKind.Cry:
                case ScriptEffectKind.MusicStop:
                    _hooks.PlaySound?.Invoke(effect.Kind, effect.A);
                    break;

                case ScriptEffectKind.CameraShake:
                    _hooks.ShakeCamera?.Invoke(effect.A, effect.B, effect.C, effect.D);
                    // The shake holds the script up for as long as it runs: count times, frames each.
                    Hold(Math.Max(0, effect.C) * Math.Max(1, effect.D));
                    break;

                case ScriptEffectKind.CameraChange:
                    Hold(_hooks.MoveCamera?.Invoke(effect.A) ?? 0);
                    break;

                case ScriptEffectKind.Wait:
                    // Whatever it is waiting on has already been counted, so there is nothing to add.
                    break;
            }
        }
    }

    /// <summary>
    /// Moves the view to one of the alternative camera settings, the way EvCmdMoveSeamlessCamera does.
    ///
    /// SMLS_CamCnt_Request picks a row of SmlsParam (field_camera.c:330) and SMLS_CamCnt_Main eases into
    /// it: ChangeCamAngle walks the downward tilt from where it was to where it is going, and ShiftCamPos
    /// slides the view across, both over the row's own number of frames. This build has one row, which
    /// tilts to -0x1a9e and shifts back by 0x6c000 over twenty four frames.
    ///
    /// Worth knowing: no script in the retail game asks for this, so it is here for hacks that do.
    /// </summary>
    public sealed class FieldCameraMove
    {
        /// <summary>The rows SmlsParam holds. Row numbers in a script count from one.</summary>
        public static readonly (int RawPitch, int ShiftX, int ShiftY, int ShiftZ, int Frames)[] Settings =
        {
            (-0x1a9e, 0, 0, -0x6c000, 24),
        };

        private const float FixedPointOne = 4096f;
        private const float TurnDegrees = 360f / 65536f;

        private readonly float _toPitch, _shiftX, _shiftY, _shiftZ;
        private readonly int _frames;
        private readonly float _fromPitch;
        private int _at;

        /// <summary>Whether a script's row number picks a real setting.</summary>
        public static bool Exists(int row) => row >= 1 && row <= Settings.Length;

        public FieldCameraMove(int row, float fromPitchDegrees)
        {
            var set = Settings[Math.Min(Math.Max(row, 1), Settings.Length) - 1];
            _fromPitch = fromPitchDegrees;
            _toPitch = -set.RawPitch * TurnDegrees;
            _shiftX = set.ShiftX / FixedPointOne / FieldCameraEntry.GameUnitsPerTile;
            _shiftY = set.ShiftY / FixedPointOne / FieldCameraEntry.GameUnitsPerTile;
            _shiftZ = set.ShiftZ / FixedPointOne / FieldCameraEntry.GameUnitsPerTile;
            _frames = Math.Max(1, set.Frames);
        }

        public bool Running => _at < _frames;

        /// <summary>How long the whole move takes, so a script can wait for it.</summary>
        public int TotalFrames => _frames;

        /// <summary>How far down the camera is looking right now, in degrees.</summary>
        public float PitchDegrees => _fromPitch + (_toPitch - _fromPitch) * Progress;

        /// <summary>How far the view has slid, in tiles.</summary>
        public float ShiftXInTiles => _shiftX * Progress;
        public float ShiftYInTiles => _shiftY * Progress;
        public float ShiftZInTiles => _shiftZ * Progress;

        private float Progress => Math.Min(1f, _at / (float)_frames);

        public void Advance(int frames) => _at = Math.Min(_frames, _at + Math.Max(0, frames));
    }

    /// <summary>
    /// Shakes the view the way EventCmd_ZishinEffect does. FDemoShake_Main in field_demo.c:1057 turns a
    /// full circle of sine over each pass, moving what the camera looks at by the given distance across
    /// and down, then snaps back and goes round again.
    /// </summary>
    public sealed class FieldCameraShake
    {
        private readonly float _width, _height;
        private readonly int _framesPerPass;
        private int _passesLeft;
        private int _frame;

        public FieldCameraShake(int width, int height, int count, int framesPerPass)
        {
            _width = width; _height = height;
            _passesLeft = Math.Max(0, count);
            _framesPerPass = Math.Max(1, framesPerPass);
        }

        public bool Running => _passesLeft > 0;

        /// <summary>How far the view is pushed this frame, across and down.</summary>
        public float OffsetX { get; private set; }
        public float OffsetY { get; private set; }

        public void Advance(int frames)
        {
            for (int i = 0; i < frames; i++) Step();
        }

        private void Step()
        {
            if (_passesLeft <= 0) { OffsetX = OffsetY = 0f; return; }

            // akey = 360 / wait degrees a frame, so one whole turn of sine over each pass.
            double degrees = 360.0 / _framesPerPass * _frame;
            float r = (float)Math.Sin(degrees * Math.PI / 180.0);
            OffsetX = r * _width;
            OffsetY = r * _height;

            _frame++;
            if (_frame < _framesPerPass) return;

            OffsetX = OffsetY = 0f;
            _frame = 0;
            _passesLeft--;
        }
    }
}
