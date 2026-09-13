using System;
using System.Collections.Generic;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Plays a script out on the field's own clock the way the script VM does: commands run one after
    /// another within a frame until one of them has to wait, and a satisfied wait lets the next command
    /// run on the following frame.
    /// </summary>
    public sealed class FieldScriptRunner
    {
        /// <summary>What the runner wants done. The preview supplies these.</summary>
        public sealed class Hooks
        {
            /// <summary>Start a movement on an overworld. Return how many frames it will take, or 0.</summary>
            public Func<int, int, int> StartMovement;
            /// <summary>Whether any movement a script started is still playing. Null counts the frames instead.</summary>
            public Func<bool> MovementsRunning;
            /// <summary>Play a sound. The kind says which of the three it is.</summary>
            public Action<ScriptEffectKind, int> PlaySound;
            /// <summary>Whether a sound of this kind is still playing, for the commands that wait on one.</summary>
            public Func<ScriptEffectKind, bool> SoundPlaying;
            /// <summary>Start the view shaking: across, down, how many times, frames each.</summary>
            public Action<int, int, int, int> ShakeCamera;
            /// <summary>Move the view to one of the alternative settings. Returns how long it takes.</summary>
            public Func<int, int> MoveCamera;
            /// <summary>Start printing a message. Return false when there is no box to print it in.</summary>
            public Func<ScriptEffect, bool> ShowMessage;
            /// <summary>Whether the message is still printing or waiting on a press part way through.</summary>
            public Func<bool> MessagePrinting;
            /// <summary>Open the box empty.</summary>
            public Action OpenMessage;
            /// <summary>Close the box. True leaves the words on screen.</summary>
            public Action<bool> CloseMessage;
            /// <summary>The script stopped on something only the watcher can answer.</summary>
            public Action<ScriptQuestion> Ask;
            /// <summary>Turning, locking and showing or hiding people.</summary>
            public Action<ScriptEffect> Apply;
            /// <summary>Every step, reported so the panel can list it.</summary>
            public Action<ScriptStep> Report;
        }

        /// <summary>What the script is waiting on.</summary>
        public enum WaitKind { None, Yield, Frames, Movement, Message, Button, Question, Sound }

        /// <summary>
        /// A WaitMovement finds its count at zero three frames after the last step lands: one to mark the
        /// list ended, one for the watcher to count it off, one for the script to look.
        /// </summary>
        public const int MovementEndLatency = 3;

        /// <summary>How many commands can run in one frame before the runner assumes a loop.</summary>
        public const int CommandsPerFrame = 512;

        /// <summary>
        /// HGSS swapping its touch screen fades it out over two frames, builds the new one and fades back in, and
        /// the script waits for all of it.
        /// </summary>
        public const int TouchScreenSwapFrames = 6;

        private readonly Hooks _hooks;
        private readonly List<ScriptStep> _fixed = new List<ScriptStep>();
        private ScriptWalker _walker;
        private int _at;
        private bool _started, _ended;
        private int _holdFrames;
        private int _movementUntil;
        private int _frame;
        private ScriptEffectKind _soundKind;
        private bool _pressed, _padPressed;
        private bool _buttonTakesPad, _buttonTurns;

        public FieldScriptRunner(Hooks hooks) { _hooks = hooks ?? new Hooks(); }

        /// <summary>What it is waiting on now.</summary>
        public WaitKind Waiting { get; private set; }

        /// <summary>Whether there is still something to play.</summary>
        public bool Running => _started && !_ended;

        /// <summary>Which step it is on, for showing progress.</summary>
        public int StepIndex => _at;
        public int StepCount => _walker?.Steps.Count ?? _fixed.Count;

        /// <summary>How many frames it is still holding for, so a caller can say what it is waiting on.</summary>
        public int HoldingFrames => Waiting == WaitKind.Frames ? _holdFrames : 0;

        /// <summary>True while a message is printing or a button wait is up.</summary>
        public bool WaitingOnReader => Waiting == WaitKind.Message || Waiting == WaitKind.Button;

        /// <summary>Whether a button wait also ends on the d-pad.</summary>
        public bool ButtonTakesPad => Waiting == WaitKind.Button && _buttonTakesPad;

        /// <summary>Whether that d-pad press also turns the player, the way WaitButton does.</summary>
        public bool ButtonTurnsPlayer => ButtonTakesPad && _buttonTurns;

        /// <summary>Plays a script as the walker runs it, a command at a time.</summary>
        public void Play(ScriptWalker walker)
        {
            Reset();
            _walker = walker;
            _started = walker != null;
        }

        /// <summary>Plays a fixed list of steps from the beginning.</summary>
        public void Play(IEnumerable<ScriptStep> steps)
        {
            Reset();
            if (steps != null) _fixed.AddRange(steps);
            _started = true;
        }

        public void Stop()
        {
            Reset();
            _started = false;
        }

        private void Reset()
        {
            _fixed.Clear();
            _walker = null;
            _at = 0;
            _holdFrames = 0;
            _movementUntil = 0;
            _frame = 0;
            _ended = false;
            _pressed = _padPressed = false;
            Waiting = WaitKind.None;
        }

        /// <summary>The player pressed A or B, or the d-pad when <paramref name="pad"/> is set.</summary>
        public void Pressed(bool pad = false)
        {
            if (pad) _padPressed = true;
            else _pressed = true;
        }

        /// <summary>Moves the clock on. Call once a frame.</summary>
        public void Advance(int frames)
        {
            for (int i = 0; i < frames && Running; i++)
            {
                _frame++;
                bool ran = Waiting == WaitKind.None;
                if (Waiting == WaitKind.Yield)
                {
                    // A command that yields without pausing hands the next command the very next frame.
                    Waiting = WaitKind.None;
                    ran = true;
                }
                else if (Waiting != WaitKind.None && Satisfied())
                {
                    // The check that finds a wait over only switches the script back on.
                    Waiting = WaitKind.None;
                }

                if (ran) RunCommands();
                _pressed = _padPressed = false;
            }
        }

        private bool Satisfied()
        {
            switch (Waiting)
            {
                case WaitKind.Frames:
                    return --_holdFrames <= 0;
                case WaitKind.Movement:
                    // Movement runs after the script in a frame, so moving now can still land this frame.
                    if (_hooks.MovementsRunning?.Invoke() == true)
                    {
                        _movementUntil = Math.Max(_movementUntil, _frame);
                        return false;
                    }
                    return _frame >= _movementUntil + MovementEndLatency;
                case WaitKind.Message:
                    return !(_hooks.MessagePrinting?.Invoke() ?? false);
                case WaitKind.Button:
                    return _pressed || (_buttonTakesPad && _padPressed);
                case WaitKind.Question:
                    return _walker == null || _walker.Pending == null;
                case WaitKind.Sound:
                    return !(_hooks.SoundPlaying?.Invoke(_soundKind) ?? false);
                default:
                    return true;
            }
        }

        private void RunCommands()
        {
            for (int budget = CommandsPerFrame; budget > 0 && Waiting == WaitKind.None && Running; budget--)
            {
                var step = NextStep();
                if (step == null) return;
                DoOne(step);
            }
        }

        private ScriptStep NextStep()
        {
            if (_walker == null)
            {
                if (_at < _fixed.Count) return _fixed[_at++];
                _ended = true;
                return null;
            }

            while (_at >= _walker.Steps.Count)
            {
                if (_walker.Pending != null)
                {
                    Waiting = WaitKind.Question;
                    _hooks.Ask?.Invoke(_walker.Pending);
                    return null;
                }
                if (_walker.Finished) { _ended = true; return null; }
                _walker.Next();
            }
            return _walker.Steps[_at++];
        }

        private void DoOne(ScriptStep step)
        {
            _hooks.Report?.Invoke(step);
            var effect = step.Effect;
            if (effect == null) return;

            switch (effect.Kind)
            {
                case ScriptEffectKind.Movement:
                {
                    int frames = _hooks.StartMovement?.Invoke(effect.A, effect.B) ?? 0;
                    // The first step moves on the frame the command runs.
                    _movementUntil = Math.Max(_movementUntil, _frame + Math.Max(0, frames - 1));
                    break;
                }

                case ScriptEffectKind.WaitMovement:
                    Waiting = WaitKind.Movement;
                    break;

                case ScriptEffectKind.SoundEffect:
                case ScriptEffectKind.Fanfare:
                case ScriptEffectKind.Music:
                case ScriptEffectKind.Cry:
                case ScriptEffectKind.MusicStop:
                    _hooks.PlaySound?.Invoke(effect.Kind, effect.A);
                    break;

                case ScriptEffectKind.Wait:
                    if (_hooks.SoundPlaying == null) break;
                    _soundKind = (ScriptEffectKind)effect.A;
                    Waiting = WaitKind.Sound;
                    break;

                case ScriptEffectKind.CameraShake:
                    _hooks.ShakeCamera?.Invoke(effect.A, effect.B, effect.C, effect.D);
                    Hold(Math.Max(0, effect.C) * Math.Max(1, effect.D));
                    break;

                case ScriptEffectKind.CameraChange:
                    Hold(_hooks.MoveCamera?.Invoke(effect.A) ?? 0);
                    break;

                case ScriptEffectKind.Message:
                {
                    bool shown = _hooks.ShowMessage?.Invoke(effect) ?? false;
                    // An instant message is drawn and the script carries straight on.
                    if (shown && effect.A != 1) Waiting = WaitKind.Message;
                    break;
                }

                case ScriptEffectKind.OpenMessage:
                    _hooks.OpenMessage?.Invoke();
                    break;

                case ScriptEffectKind.CloseMessage:
                    _hooks.CloseMessage?.Invoke(effect.A == 1);
                    break;

                case ScriptEffectKind.WaitButton:
                    _buttonTakesPad = effect.A != 0;
                    _buttonTurns = effect.A == 2;
                    _pressed = _padPressed = false;
                    Waiting = WaitKind.Button;
                    break;

                case ScriptEffectKind.WaitFrames:
                    // Wait 0 counts down from 65536, which is what the games do with it too.
                    _holdFrames = effect.A == 0 ? 65536 : effect.A;
                    Waiting = WaitKind.Frames;
                    break;

                case ScriptEffectKind.Lock:
                case ScriptEffectKind.Release:
                    _hooks.Apply?.Invoke(effect);
                    // LockAll and ReleaseAll give up the rest of the frame; the single-object ones do not.
                    if (effect.A < 0) Waiting = WaitKind.Yield;
                    break;

                case ScriptEffectKind.TouchScreen:
                    _hooks.Apply?.Invoke(effect);
                    Hold(TouchScreenSwapFrames);
                    break;

                case ScriptEffectKind.FacePlayer:
                case ScriptEffectKind.ShowObject:
                case ScriptEffectKind.CameraObject:
                    _hooks.Apply?.Invoke(effect);
                    break;
            }
        }

        private void Hold(int frames)
        {
            if (frames <= 0) return;
            _holdFrames = frames;
            Waiting = WaitKind.Frames;
        }
    }

    /// <summary>
    /// Moves the view to one of the alternative camera settings, the way EvCmdMoveSeamlessCamera does.
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

    /// <summary>Shakes the view the way EventCmd_ZishinEffect does. </summary>
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
