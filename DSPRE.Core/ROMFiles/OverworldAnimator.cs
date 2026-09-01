using System;
using System.Collections.Generic;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Drives one overworld's idle motion for a preview, following what the games do rather than inventing
    /// something.
    /// </summary>
    public sealed class OverworldAnimator
    {
        public const int SpinIntervalFrames = 24;                            // MV_SPIN_WAIT_FRAME
        public static readonly int[] RandomWaits = { 16, 32, 48, 64 };       // DATA_MvDirRndWaitTbl

        /// <summary>How long one tile of ordinary walking takes: AC_WALK_U_8F, eight frames.</summary>
        public const int WalkFrames = 8;

        /// <summary>A movement range of -1 means the engine doesn't fence the event in at all.</summary>
        public const int NoMoveLimit = -1;

        private readonly OverworldMovement _move;
        private readonly int _rangeX, _rangeZ;
        private readonly int _interval;        // 0 = use the movement's own timing
        private readonly Random _rng;
        private readonly Func<int, int, bool> _blocked;
        private int _routeStep;
        private int _framesUntilNext;
        private bool _routeReversed;

        // While a step is in progress: where it came from, and how far through it is.
        private int _fromX, _fromZ;
        private int _stepFramesLeft;

        /// <summary>Which way the sprite currently faces.</summary>
        public MoveFacing Facing { get; private set; }

        /// <summary>The tile it belongs to, in tiles from where it was placed. </summary>
        public int OffsetX { get; private set; }
        public int OffsetZ { get; private set; }

        /// <summary>
        /// The tile it is coming from, which is the same as <see cref="OffsetX"/> unless it is mid-step.
        /// </summary>
        public int FromOffsetX => _scripted != null ? _scriptFromX : _fromX;
        public int FromOffsetZ => _scripted != null ? _scriptFromZ : _fromZ;

        /// <summary>True while it is part way between two tiles.</summary>
        public bool IsWalking => _stepFramesLeft > 0 || (_scripted != null && _scriptFramesLeft > 0);

        /// <summary>Where to draw it, in tiles from where it was placed. </summary>
        public float DrawOffsetX =>
            _scripted != null ? ScriptBlend(_scriptFromX, _scriptToX) : Blend(_fromX, OffsetX);
        public float DrawOffsetZ =>
            _scripted != null ? ScriptBlend(_scriptFromZ, _scriptToZ) : Blend(_fromZ, OffsetZ);

        private float Blend(int from, int to)
        {
            if (_stepFramesLeft <= 0) return to;
            float gone = (WalkFrames - _stepFramesLeft) / (float)WalkFrames;
            return from + (to - from) * gone;
        }

        /// <param name="rangeX">move_limit_x, or <see cref="NoMoveLimit"/> for no limit on that axis.</param>
        /// <param name="intervalOverride">param1 for the glance/spin trainer types, which set their own pace.</param>
        /// <param name="blocked">Whether the tile this many tiles from the start is blocked. Null means
        /// nothing is, which is what a preview with no permissions to hand should assume.</param>
        public OverworldAnimator(OverworldMovement move, MoveFacing initialFacing,
                                 int rangeX = 0, int rangeZ = 0, int intervalOverride = 0, int seed = 0,
                                 Func<int, int, bool> blocked = null)
        {
            _move = move;
            Facing = initialFacing;
            _rangeX = rangeX == NoMoveLimit ? NoMoveLimit : Math.Max(0, rangeX);
            _rangeZ = rangeZ == NoMoveLimit ? NoMoveLimit : Math.Max(0, rangeZ);
            _interval = Math.Max(0, intervalOverride);
            _rng = new Random(seed);
            _blocked = blocked;
            _framesUntilNext = NextWait();
        }

        private int NextWait()
        {
            if (_interval > 0) return _interval;
            switch (_move?.Kind)
            {
                case MoveKind.Spin: return SpinIntervalFrames;
                // A route keeps walking, pausing only as long as one step takes.
                case MoveKind.Route: return 1;
                default: return RandomWaits[_rng.Next(RandomWaits.Length)];
            }
        }

        // ── being told what to do by a script ─────────────────────────────
        private List<FieldMovementStep> _scripted;
        private int _scriptStep;
        private int _scriptFramesLeft;
        private int _scriptFromX, _scriptFromZ, _scriptToX, _scriptToZ;
        private float _hop;

        /// <summary>True while a script is telling this overworld where to go.</summary>
        public bool IsScripted => _scripted != null;

        /// <summary>Whether it is on show. A movement can hide and show it.</summary>
        public bool Visible { get; private set; } = true;

        /// <summary>How high off the ground it is, in tiles, while it is mid-hop.</summary>
        public float HopHeight => _hop;

        /// <summary>
        /// How many frames it has spent moving, which is what picks the walking picture.
        /// </summary>
        public int AnimationCell => _animCell;
        private int _animCell;

        /// <summary>Which step of the movement it is on, for showing progress.</summary>
        public int ScriptStepIndex => _scriptStep;

        /// <summary>How many steps the movement has.</summary>
        public int ScriptStepCount => _scripted?.Count ?? 0;

        /// <summary>The step it is playing now, or null when no script is running.</summary>
        public FieldMovementStep CurrentScriptStep =>
            _scripted != null && _scriptStep < _scripted.Count ? _scripted[_scriptStep] : null;

        /// <summary>Hands this overworld a movement to play out. </summary>
        public void PlayScript(List<FieldMovementStep> steps)
        {
            StopScript();
            if (steps == null || steps.Count == 0) return;
            _scripted = steps;
            _scriptStep = -1;
            BeginScriptStep();
        }

        /// <summary>Drops the movement and goes back to idling.</summary>
        public void StopScript()
        {
            _scripted = null;
            _scriptStep = 0;
            _scriptFramesLeft = 0;
            _hop = 0f;
            _fromX = OffsetX; _fromZ = OffsetZ;
            _stepFramesLeft = 0;
            _framesUntilNext = NextWait();
        }

        private void BeginScriptStep()
        {
            _hop = 0f;
            while (true)
            {
                _scriptStep++;
                if (_scripted == null || _scriptStep >= _scripted.Count) { StopScript(); return; }

                var step = _scripted[_scriptStep];
                _scriptFramesLeft = Math.Max(1, step.Frames);
                _scriptFromX = OffsetX; _scriptFromZ = OffsetZ;
                _scriptToX = OffsetX; _scriptToZ = OffsetZ;

                if (step.Kind == FieldActionKind.Appear) { Visible = step.Visible ?? true; continue; }
                if (step.Kind != FieldActionKind.Delay) Facing = step.Facing;

                if (step.Tiles > 0)
                {
                    var (dx, dz) = Step(step.Facing);
                    _scriptToX = OffsetX + dx * step.Tiles;
                    _scriptToZ = OffsetZ + dz * step.Tiles;
                }
                return;
            }
        }

        private void AdvanceScript()
        {
            var step = CurrentScriptStep;
            if (step == null) { StopScript(); return; }

            _scriptFramesLeft--;
            int total = Math.Max(1, step.Frames);
            float gone = (total - _scriptFramesLeft) / (float)total;

            // Only steps that actually go somewhere drive the walking pictures; turning on the spot
            // and waiting leave the sprite standing.
            if (step.Kind == FieldActionKind.Walk || step.Kind == FieldActionKind.Jump) _animCell++;

            // A hop rises and falls over the step, which is what tells a jump apart from a walk.
            _hop = step.Kind == FieldActionKind.Jump ? (float)Math.Sin(gone * Math.PI) * 0.5f : 0f;

            if (_scriptFramesLeft <= 0)
            {
                OffsetX = _scriptToX; OffsetZ = _scriptToZ;
                _fromX = OffsetX; _fromZ = OffsetZ;
                BeginScriptStep();
            }
        }

        /// <summary>Where a scripted step has got to, between the tile it left and the one it is heading for.</summary>
        private float ScriptBlend(int from, int to)
        {
            var step = CurrentScriptStep;
            if (step == null) return to;
            int total = Math.Max(1, step.Frames);
            float gone = (total - _scriptFramesLeft) / (float)total;
            return from + (to - from) * gone;
        }

        /// <summary>Advance the clock. Call once per rendered frame with how many frames have passed.</summary>
        public void Advance(int frames)
        {
            if (frames <= 0) return;

            // A movement from a script overrides whatever the event would be doing on its own.
            if (_scripted != null)
            {
                for (int i = 0; i < frames && _scripted != null; i++) AdvanceScript();
                return;
            }

            if (_move == null) return;
            switch (_move.Kind)
            {
                case MoveKind.Static:
                case MoveKind.Player:
                case MoveKind.Special:
                    return;
                case MoveKind.FaceFixed:
                    if (_move.Facings.Count > 0) Facing = _move.Facings[0];
                    return;
            }

            for (int i = 0; i < frames; i++)
            {
                // A step in progress has to finish before anything else happens.
                if (_stepFramesLeft > 0)
                {
                    _animCell++;
                    _stepFramesLeft--;
                    if (_stepFramesLeft == 0) { _fromX = OffsetX; _fromZ = OffsetZ; _framesUntilNext = NextWait(); }
                    continue;
                }

                if (--_framesUntilNext > 0) continue;
                Act();
                if (_stepFramesLeft == 0) _framesUntilNext = NextWait();
            }
        }

        private static readonly MoveFacing[] Clockwise =
            { MoveFacing.Up, MoveFacing.Right, MoveFacing.Down, MoveFacing.Left };

        private static MoveFacing Flip(MoveFacing f)
        {
            switch (f)
            {
                case MoveFacing.Up: return MoveFacing.Down;
                case MoveFacing.Down: return MoveFacing.Up;
                case MoveFacing.Left: return MoveFacing.Right;
                default: return MoveFacing.Left;
            }
        }

        private static (int dx, int dz) Step(MoveFacing f)
        {
            switch (f)
            {
                case MoveFacing.Up: return (0, -1);
                case MoveFacing.Down: return (0, 1);
                case MoveFacing.Left: return (-1, 0);
                default: return (1, 0);
            }
        }

        private void Act()
        {
            switch (_move.Kind)
            {
                case MoveKind.TurnRandom:
                    Facing = Pick();
                    break;

                case MoveKind.Spin:
                {
                    int at = Array.IndexOf(Clockwise, Facing);
                    if (at < 0) at = 0;
                    int step = _move.SpinClockwise ? 1 : Clockwise.Length - 1;
                    Facing = Clockwise[(at + step) % Clockwise.Length];
                    break;
                }

                case MoveKind.Wander:
                    Facing = Pick();
                    BeginStep(Facing);
                    break;

                case MoveKind.Route:
                    WalkRoute();
                    break;
            }
        }

        /// <summary>
        /// A route walks the way it faces until it can go no further, then turns back the way it came.
        /// </summary>
        private void WalkRoute()
        {
            if (_move.Facings.Count > 0)
            {
                var want = _move.Facings[_routeStep % _move.Facings.Count];
                Facing = _routeReversed ? Flip(want) : want;
            }

            if (BeginStep(Facing)) return;

            // Blocked or at the end of its range: turn round, and take the next leg next time.
            Facing = Flip(Facing);
            if (_move.Facings.Count > 0)
            {
                _routeReversed = !_routeReversed;
                if (!_routeReversed) _routeStep++;
            }
            BeginStep(Facing);
        }

        /// <summary>Starts walking one tile if the engine would allow it. </summary>
        private bool BeginStep(MoveFacing dir)
        {
            var (dx, dz) = Step(dir);
            int nx = OffsetX + dx, nz = OffsetZ + dz;
            if (!WithinRange(nx, nz)) return false;
            if (_blocked != null && _blocked(nx, nz)) return false;

            _fromX = OffsetX; _fromZ = OffsetZ;
            OffsetX = nx; OffsetZ = nz;
            _stepFramesLeft = WalkFrames;
            return true;
        }

        /// <summary>
        /// FieldOBJ_MoveHitCheckLimit: each axis is fenced to the spawn tile plus or minus its own limit,
        /// and an axis whose limit is -1 isn't fenced at all.
        /// </summary>
        private bool WithinRange(int x, int z)
        {
            if (_rangeX != NoMoveLimit && Math.Abs(x) > _rangeX) return false;
            if (_rangeZ != NoMoveLimit && Math.Abs(z) > _rangeZ) return false;
            return true;
        }

        private MoveFacing Pick()
        {
            IReadOnlyList<MoveFacing> choices = _move.Facings.Count > 0 ? _move.Facings : Clockwise;
            return choices[_rng.Next(choices.Count)];
        }
    }
}
