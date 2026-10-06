using System;
using System.Collections.Generic;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// What the Distortion World does while the player walks it: jump points, fading prop groups,
    /// moving platform flags and elevator rides. Follows pret pokeplatinum src/overlay009/ov9_02249960.c.
    /// </summary>
    public sealed class TornWorldRuntime
    {
        // Order of enum DistWorldPlatformFlag in include/overlay009/ov9_02249960.h.
        public enum PlatformFlag
        {
            B1F_1 = 0, B2F_1 = 1, B3F_1 = 2, B4F_1 = 3, B4F_2 = 4, B5F_3 = 5,
            B3F_2 = 6, B5F_1 = 7, B4F_3 = 8, B6F_1 = 9, B7F_1 = 10, Invalid = 11,
        }

        public const int NoPath = 22;
        public const uint B7FHeader = 581;

        // sDistWorldMapConnectionList: each floor's previous and next are its neighbours here.
        public static readonly uint[] FloorChain = { 573, 574, 575, 576, 577, 579, 580, 581 };

        public static uint? NextFloor(uint header)
        {
            int at = Array.IndexOf(FloorChain, header);
            return at >= 0 && at + 1 < FloorChain.Length ? FloorChain[at + 1] : (uint?)null;
        }

        public static uint? PreviousFloor(uint header)
        {
            int at = Array.IndexOf(FloorChain, header);
            return at > 0 ? FloorChain[at - 1] : (uint?)null;
        }

        // sFloatingPlatformJumpOffsets, in world units (16 to a tile).
        public static readonly int[] JumpOffsets = { 4, 6, 8, 10, 11, 12, 12, 12, 11, 10, 9, 8, 6, 4, 0, 0 };

        public uint PlatformFlags { get; private set; }

        public uint VisibleGroups { get; private set; }

        public TornWorldRuntime(uint startHeader = 0) => ResetFlags(startHeader);

        /// <summary>InitPersistedData: the set a warp in starts with.</summary>
        public void ResetFlags(uint startHeader)
        {
            PlatformFlags = startHeader == B7FHeader
                ? Bits(PlatformFlag.B1F_1, PlatformFlag.B2F_1, PlatformFlag.B3F_1, PlatformFlag.B4F_1,
                       PlatformFlag.B4F_2, PlatformFlag.B5F_1, PlatformFlag.B6F_1, PlatformFlag.B7F_1)
                : Bits(PlatformFlag.B4F_1, PlatformFlag.B5F_1);
        }

        private static uint Bits(params PlatformFlag[] flags)
        {
            uint bits = 0;
            foreach (PlatformFlag flag in flags) bits |= 1u << (int)flag;
            return bits;
        }

        public bool Has(PlatformFlag flag) => flag < PlatformFlag.Invalid && (PlatformFlags & (1u << (int)flag)) != 0;

        public void Set(int flag) { if (flag >= 0 && flag < (int)PlatformFlag.Invalid) PlatformFlags |= 1u << flag; }

        public void Clear(int flag) { if (flag >= 0 && flag < (int)PlatformFlag.Invalid) PlatformFlags &= ~(1u << flag); }

        /// <summary>A moving platform is created when its flag is 11 or set.</summary>
        public bool IsPresent(TornWorldCodeTables.MovingPlatform platform)
            => platform != null && (platform.PersistedFlag >= (uint)PlatformFlag.Invalid || Has((PlatformFlag)platform.PersistedFlag));

        // ── Fading props ─────────────────────────────────────────────────────────────
        public const int GhostGroupCount = 24;

        public void EnterFloor(TornWorldFile file)
            => VisibleGroups = (uint)(file?.DefaultVisibleGroups ?? 0) & ((1u << GhostGroupCount) - 1);

        public bool IsGroupVisible(long group)
            => group >= 0 && group < GhostGroupCount && (VisibleGroups & (1u << (int)group)) != 0;

        /// <summary>HandleGhostPropTriggerAt, run only after a real step. True when a group changed.</summary>
        public bool StepOn(TornWorldFile file, int x, int y, int z, int facing)
        {
            if (file == null) return false;
            uint before = VisibleGroups;
            foreach (TornWorldFile.GhostTrigger trigger in file.GhostTriggers)
            {
                if (trigger.PlayerDirection != facing || !trigger.Bounds.Contains(x, y, z)) continue;
                if (trigger.GroupId < 0 || trigger.GroupId >= GhostGroupCount) continue;
                uint bit = 1u << (int)trigger.GroupId;
                VisibleGroups = trigger.ShowProp != 0 ? VisibleGroups | bit : VisibleGroups & ~bit;
            }
            return before != VisibleGroups;
        }

        // ── Jump points ──────────────────────────────────────────────────────────────

        /// <summary>FindFloatingPlatformJumpPointAt: the first one whose facing and box both match.</summary>
        public static TornWorldFile.JumpPoint JumpPointAt(TornWorldFile file, int x, int y, int z, int facing)
        {
            if (file == null) return null;
            foreach (TornWorldFile.JumpPoint point in file.JumpPoints)
                if (point.PlayerDirection == facing && point.Bounds.Contains(x, y, z)) return point;
            return null;
        }

        /// <summary>The sprite's hop after step 1..steps, in world units (TickJumpOnFloatingPlatformMovementAnimation).</summary>
        public static (float x, float y, float z) HopOffset(TornWorldFile.JumpPoint point, int step)
        {
            int steps = point == null ? 0 : point.MovementSteps;
            if (steps <= 0 || step <= 0 || step >= steps) return (0f, 0f, 0f);

            int index = step * 16 / steps;
            float lift = index < JumpOffsets.Length ? JumpOffsets[index] : 0;
            if (point.InvertedJump == 1) lift = -lift;

            switch (point.JumpAxis)
            {
                case 0: return (lift, 0f, 0f);
                case 2: return (0f, 0f, lift);
                default: return (0f, lift, 0f);
            }
        }

        /// <summary>How far the sprite has turned after a number of steps, in degrees (RotateMapObject).</summary>
        public static float TurnAfter(TornWorldFile.JumpPoint point, int step)
        {
            int steps = point == null ? 0 : point.MovementSteps;
            if (steps <= 0) return point?.SpriteRotationAngle ?? 0;
            return point.SpriteRotationAngle * Math.Min(Math.Max(step, 0), steps) / (float)steps;
        }

        // ── Elevators ────────────────────────────────────────────────────────────────

        public enum RideEvent { None, ChangedFloor, Arrived }

        /// <summary>One elevator ride, frame by frame, the way the DistWorldElevatorPlatform task runs it.</summary>
        public sealed class ElevatorRide
        {
            private const int FxOne = 0x1000;
            private const int TileFx = 16 * FxOne;
            private const int FrameLimit = 4096;

            private enum State { Begin, Vibrate, FirstHalf, SecondHalf, Done }

            private readonly TornWorldRuntime _runtime;
            private readonly TornWorldCodeTables.Tables _tables;
            private readonly bool _down;
            private State _state = State.Begin;
            private bool _vibrated;
            private int _vibrationDelta, _vibrationSteps, _shownVibration;
            private int _frames;

            private TornWorldCodeTables.ElevatorPath _path;
            private int _nextPath;
            private int _currX, _currY, _currZ, _finalX, _finalY, _finalZ, _changeX, _changeY, _changeZ;
            private int _deltaX, _deltaY, _deltaZ;
            private int _pathStartX, _pathStartY, _pathStartZ;

            public TornWorldCodeTables.MovingPlatform Platform { get; private set; }

            /// <summary>The floor whose template the platform is, which changes on the last path.</summary>
            public uint PlatformHeader { get; private set; }

            /// <summary>The floor the ride is on now.</summary>
            public uint Header { get; private set; }

            public int StartX { get; }
            public int StartY { get; }
            public int StartZ { get; }

            /// <summary>Where the platform is, in tiles, including the shake before it sets off.</summary>
            public float X => _pathStartX + _currX / (float)TileFx;
            public float Y => _pathStartY + (_currY + _shownVibration) / (float)TileFx;
            public float Z => _pathStartZ + _currZ / (float)TileFx;

            public int PathStartX => _pathStartX;
            public int PathStartY => _pathStartY;
            public int PathStartZ => _pathStartZ;

            public int EndX { get; private set; }
            public int EndY { get; private set; }
            public int EndZ { get; private set; }

            public bool Done => _state == State.Done;

            public ElevatorRide(TornWorldRuntime runtime, TornWorldCodeTables.Tables tables, uint header,
                TornWorldCodeTables.MovingPlatform platform)
            {
                _runtime = runtime ?? new TornWorldRuntime();
                _tables = tables;
                Platform = platform;
                Header = PlatformHeader = header;
                _down = platform.ElevatorDirection == 1;
                _nextPath = platform.ElevatorPathIndex;
                StartX = EndX = _pathStartX = platform.TileX;
                StartY = EndY = _pathStartY = platform.TileY;
                StartZ = EndZ = _pathStartZ = platform.TileZ;
            }

            public RideEvent Tick()
            {
                if (_state == State.Done) return RideEvent.None;
                if (++_frames > FrameLimit) { Finish(); return RideEvent.Arrived; }

                RideEvent result = RideEvent.None;
                switch (_state)
                {
                    case State.Begin:
                        if (!Begin()) { Finish(); return RideEvent.Arrived; }
                        break;
                    case State.Vibrate:
                        Vibrate();
                        break;
                    case State.FirstHalf:
                        Step();
                        if (_currX == _changeX && _currY == _changeY && _currZ == _changeZ)
                        {
                            ChangeMaps();
                            result = RideEvent.ChangedFloor;
                            _state = State.SecondHalf;
                        }
                        break;
                    case State.SecondHalf:
                        Step();
                        if (_currX == _finalX && _currY == _finalY && _currZ == _finalZ)
                        {
                            EndX = _pathStartX + _finalX / TileFx;
                            EndY = _pathStartY + _finalY / TileFx;
                            EndZ = _pathStartZ + _finalZ / TileFx;

                            if (_path.NextIndex == NoPath)
                            {
                                if (_down && _path.Index == 9) _runtime.Set((int)PlatformFlag.B5F_1);
                                Finish();
                                return RideEvent.Arrived;
                            }

                            _pathStartX = EndX; _pathStartY = EndY; _pathStartZ = EndZ;
                            _nextPath = _path.NextIndex;
                            if (!Begin()) { Finish(); return RideEvent.Arrived; }
                        }
                        break;
                }
                return result;
            }

            private void Finish()
            {
                _state = State.Done;
                _currX = (EndX - _pathStartX) * TileFx;
                _currY = (EndY - _pathStartY) * TileFx;
                _currZ = (EndZ - _pathStartZ) * TileFx;
            }

            private bool Begin()
            {
                _path = _tables?.PathAt(_nextPath);
                if (_path == null) return false;

                if (!_down)
                    switch (_path.Index)
                    {
                        case 13:
                            _runtime.Set((int)PlatformFlag.B4F_1);
                            _runtime.Clear((int)PlatformFlag.B4F_2);
                            break;
                        case 10:
                            _runtime.Clear((int)PlatformFlag.B5F_3);
                            break;
                        case 11:
                            _runtime.Set((int)PlatformFlag.B3F_2);
                            _runtime.Clear((int)PlatformFlag.B4F_1);
                            _runtime.Clear((int)PlatformFlag.B5F_3);
                            break;
                    }

                _currX = _currY = _currZ = 0;
                _finalX = _path.FinalX * TileFx; _finalY = _path.FinalY * TileFx; _finalZ = _path.FinalZ * TileFx;
                _changeX = _path.ChangeMapsX * TileFx; _changeY = _path.ChangeMapsY * TileFx; _changeZ = _path.ChangeMapsZ * TileFx;
                _deltaX = (int)Math.Round(_path.SpeedX * FxOne);
                _deltaY = (int)Math.Round(_path.SpeedY * FxOne);
                _deltaZ = (int)Math.Round(_path.SpeedZ * FxOne);

                if (!_vibrated)
                {
                    _vibrated = true;
                    _vibrationDelta = 6 * FxOne;
                    _vibrationSteps = 0;
                    _state = State.Vibrate;
                }
                else _state = State.FirstHalf;
                return true;
            }

            // DistWorldElevatorPlatform_Vibrate: the shake shrinks until it has settled.
            private void Vibrate()
            {
                _shownVibration = _vibrationDelta;
                _vibrationDelta = -_vibrationDelta;
                if (_vibrationDelta < 0) return;

                if (_vibrationDelta >= FxOne * 4) _vibrationDelta -= FxOne * 2;
                else if (_vibrationDelta > FxOne) _vibrationDelta -= FxOne;
                else if (++_vibrationSteps >= 8) _vibrationDelta -= FxOne;

                if (_vibrationDelta <= 0)
                {
                    _shownVibration = 0;
                    _state = State.FirstHalf;
                }
            }

            private void Step()
            {
                if (_currX != _finalX) _currX += _deltaX;
                if (_currY != _finalY) _currY += _deltaY;
                if (_currZ != _finalZ) _currZ += _deltaZ;
            }

            private void ChangeMaps()
            {
                uint? destination = _down ? NextFloor(Header) : PreviousFloor(Header);

                if (_path.NextIndex == NoPath)
                {
                    _runtime.Set(_path.FlagToSet);
                    _runtime.Clear(_path.FlagToClear);

                    if (destination != null && _tables != null)
                    {
                        IReadOnlyList<TornWorldCodeTables.MovingPlatform> list = _tables.PlatformsOn(destination.Value);
                        if (Platform.DestinationIndex >= 0 && Platform.DestinationIndex < list.Count)
                        {
                            Platform = list[Platform.DestinationIndex];
                            PlatformHeader = destination.Value;
                        }
                    }
                }

                if (destination != null) Header = destination.Value;
            }
        }
    }
}
