using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>What happened when the player was told to go somewhere.</summary>
    public enum StepResult
    {
        Turned,      // wasn't facing that way, so it turned instead of moving
        Walked,      // moved one tile
        Blocked,     // faced that way already, but the tile ahead is closed off
        BlockedByEvent,   // someone is standing there
        Walking,     // still part way through the last step, so nothing new happened
    }

    /// <summary>
    /// The player walking a map in the preview. Movement is one tile at a time, and a tile is refused for
    /// the reasons the engine refuses one (FieldOBJ_MoveHitCheck): the map says it is closed off, or
    /// somebody is already standing on it.
    ///
    /// Pressing a direction the player isn't facing turns on the spot first, the way the games do, so a
    /// person can be turned towards without walking into them.
    /// </summary>
    public sealed class FieldPlayer
    {
        /// <summary>One tile of walking takes the same eight frames an overworld's does.</summary>
        public const int WalkFrames = OverworldAnimator.WalkFrames;

        private readonly MapCollisionGrid _collision;
        private readonly Func<int, int, bool> _occupied;

        // While a step is in progress: the tile it left, and how far through it is.
        private int _fromX, _fromZ, _stepFramesLeft;

        /// <summary>Whole-matrix tile the player is standing on.</summary>
        public int TileX { get; private set; }
        public int TileZ { get; private set; }
        public MoveFacing Facing { get; private set; }

        /// <summary>Where the player started, so the viewer can put them back.</summary>
        public int StartX { get; }
        public int StartZ { get; }
        public MoveFacing StartFacing { get; }

        /// <param name="occupied">Whether an event is standing on a tile. Null means nobody is.</param>
        public FieldPlayer(int tileX, int tileZ, MoveFacing facing,
                           MapCollisionGrid collision, Func<int, int, bool> occupied = null)
        {
            TileX = StartX = _fromX = tileX;
            TileZ = StartZ = _fromZ = tileZ;
            Facing = StartFacing = facing;
            _collision = collision;
            _occupied = occupied;
        }

        public void Reset()
        {
            TileX = _fromX = StartX;
            TileZ = _fromZ = StartZ;
            Facing = StartFacing;
            _stepFramesLeft = 0;
        }

        /// <summary>True while the player is part way between two tiles.</summary>
        public bool IsWalking => _stepFramesLeft > 0;

        /// <summary>
        /// The tile being left, which is the same as <see cref="TileX"/> unless a step is running. A
        /// walker holds both tiles until they arrive, the way the engine does
        /// (FieldOBJ_MoveHitCheckFellowMust, fieldobj_move.c:1577).
        /// </summary>
        public int FromX => _fromX;
        public int FromZ => _fromZ;

        /// <summary>
        /// How many frames have been spent walking, which is what picks the walking picture. It keeps
        /// counting across steps so the feet alternate from one tile to the next.
        /// </summary>
        public int AnimationCell => _animCell;
        private int _animCell;

        /// <summary>Where to draw the player, which is between tiles while a step is running.</summary>
        public float DrawX => Blend(_fromX, TileX);
        public float DrawZ => Blend(_fromZ, TileZ);

        private float Blend(int from, int to)
        {
            if (_stepFramesLeft <= 0) return to;
            float gone = (WalkFrames - _stepFramesLeft) / (float)WalkFrames;
            return from + (to - from) * gone;
        }

        /// <summary>Moves the walk along. Call once per rendered frame with how many frames have passed.</summary>
        public void Advance(int frames)
        {
            if (_stepFramesLeft <= 0 || frames <= 0) return;
            _animCell += Math.Min(frames, _stepFramesLeft);
            _stepFramesLeft = Math.Max(0, _stepFramesLeft - frames);
            if (_stepFramesLeft == 0) { _fromX = TileX; _fromZ = TileZ; }
        }

        public static (int dx, int dz) Step(MoveFacing f)
        {
            switch (f)
            {
                case MoveFacing.Up: return (0, -1);
                case MoveFacing.Down: return (0, 1);
                case MoveFacing.Left: return (-1, 0);
                default: return (1, 0);
            }
        }

        /// <summary>The tile the player is looking at, which is what an interaction reaches.</summary>
        public (int x, int z) TileAhead
        {
            get { var (dx, dz) = Step(Facing); return (TileX + dx, TileZ + dz); }
        }

        /// <summary>
        /// Presses a direction. Turns to face it if it isn't already, otherwise tries to walk one tile.
        /// </summary>
        public StepResult Go(MoveFacing dir)
        {
            // A step already running has to finish before another can start.
            if (_stepFramesLeft > 0) return StepResult.Walking;

            if (Facing != dir) { Facing = dir; return StepResult.Turned; }

            var (dx, dz) = Step(dir);
            int nx = TileX + dx, nz = TileZ + dz;

            if (_collision != null && !_collision.IsEmpty && _collision.IsBlocked(nx, nz))
                return StepResult.Blocked;
            if (_occupied != null && _occupied(nx, nz))
                return StepResult.BlockedByEvent;

            _fromX = TileX; _fromZ = TileZ;
            TileX = nx; TileZ = nz;
            _stepFramesLeft = WalkFrames;
            return StepResult.Walked;
        }

        /// <summary>Turns without trying to move, for looking at something next to you.</summary>
        public void Face(MoveFacing dir) => Facing = dir;
    }

    /// <summary>
    /// What a spawnable is. The engine reads the type to decide how it is reached: a hidden item only
    /// answers while it has not been picked up, everything else answers when you face it.
    /// </summary>
    public enum SpawnableKind { Normal = 0, Signboard = 1, HiddenItem = 2 }

    /// <summary>
    /// Which way you have to be standing to talk to a spawnable, as the games number it: the direction
    /// you approach it FROM (TalkBgDirCheck in sxy.c).
    /// </summary>
    public enum SpawnableApproach
    {
        FromBelow = 0, FromLeft = 1, FromRight = 2, FromAbove = 3,
        AnyWay = 4, FromTheSides = 5, FromAboveOrBelow = 6,
    }

    /// <summary>
    /// A trainer's number is a script id like any other event's. The games work the trainer out from it
    /// (GetTrainerIdByScriptId in script.c), so nothing should ever read the number as a trainer directly.
    /// </summary>
    public static class TrainerScripts
    {
        public const int SingleFirst = 3000;      // ID_TRAINER_OFFSET
        public const int SingleLast = 4999;       // ID_TRAINER_OFFSET_END
        public const int DoubleFirst = 5000;      // ID_TRAINER_2VS2_OFFSET
        public const int DoubleLast = 6999;       // ID_TRAINER_2VS2_OFFSET_END

        /// <summary>True when a script id is one of the ones that stands for a trainer battle.</summary>
        public static bool IsTrainerScript(int scriptId) =>
            (scriptId >= SingleFirst && scriptId <= SingleLast) ||
            (scriptId >= DoubleFirst && scriptId <= DoubleLast);

        /// <summary>Whether the script id is one of the two-against-two range.</summary>
        public static bool IsDouble(int scriptId) => scriptId >= DoubleFirst && scriptId <= DoubleLast;

        /// <summary>The trainer a script id stands for, or null when it isn't a trainer script at all.</summary>
        public static int? TrainerIdFor(int scriptId)
        {
            if (!IsTrainerScript(scriptId)) return null;
            return scriptId - (IsDouble(scriptId) ? DoubleFirst : SingleFirst) + 1;
        }
    }

    /// <summary>
    /// Finding what the player is interacting with, following the games' own checks: an overworld or a
    /// spawnable on the tile in front (TalkObjEventCheck and TalkBgEventCheck in sxy.c), a trigger under
    /// the player's feet (PosEventCheck), and a warp on the tile they stepped onto.
    ///
    /// Every one of these hands back a script id. Nothing here decides what a script does.
    /// </summary>
    public static class FieldInteraction
    {
        /// <summary>The whole-matrix tile an event was placed on.</summary>
        public static int TileX(Event e) => e.xMatrixPosition * MapFile.mapSize + e.xMapPosition;
        public static int TileZ(Event e) => e.yMatrixPosition * MapFile.mapSize + e.yMapPosition;

        /// <summary>
        /// Whether an overworld is on the map at all. The games only add one whose flag is clear, so a
        /// set flag means it is not there to talk to or bump into. FldOBJ_AddFileProc in fieldobj.c:1423
        /// adds it when CheckEventFlag comes back FALSE, and nothing puts it back afterwards.
        /// </summary>
        public static bool IsPresent(Overworld o, Func<ushort, bool> flagIsSet) =>
            o != null && (flagIsSet == null || !flagIsSet(o.flag));

        /// <summary>The overworld standing on a tile, or null.</summary>
        public static Overworld OverworldAt(EventFile events, int tileX, int tileZ,
                                            Func<ushort, bool> flagIsSet = null) =>
            events?.overworlds?.FirstOrDefault(
                o => TileX(o) == tileX && TileZ(o) == tileZ && IsPresent(o, flagIsSet));

        /// <summary>Every tile an overworld occupies, for the player to bump into.</summary>
        public static HashSet<(int x, int z)> OccupiedTiles(EventFile events, Func<ushort, bool> flagIsSet = null)
        {
            var set = new HashSet<(int, int)>();
            if (events?.overworlds == null) return set;
            foreach (var o in events.overworlds)
                if (IsPresent(o, flagIsSet)) set.Add((TileX(o), TileZ(o)));
            return set;
        }

        /// <summary>
        /// The tile a talk reaches. Normally the one in front, but a counter tile lets it carry one
        /// further so you can talk to whoever is behind the shop desk.
        /// </summary>
        public static (int x, int z) TalkTile(FieldPlayer player, MapCollisionGrid map)
        {
            var (x, z) = player.TileAhead;
            if (map != null && map.IsCounter(x, z))
            {
                var (dx, dz) = FieldPlayer.Step(player.Facing);
                return (x + dx, z + dz);
            }
            return (x, z);
        }

        /// <summary>
        /// The spawnable on a tile that answers to someone facing this way, or null. A hidden item only
        /// answers while its flag says it has not been taken, which the caller decides.
        /// </summary>
        public static Spawnable SpawnableAt(EventFile events, int tileX, int tileZ,
                                            MoveFacing playerFacing, Func<Spawnable, bool> hiddenStillThere = null)
        {
            if (events?.spawnables == null) return null;
            foreach (var s in events.spawnables)
            {
                if (TileX(s) != tileX || TileZ(s) != tileZ) continue;

                if ((SpawnableKind)s.type == SpawnableKind.HiddenItem)
                {
                    if (hiddenStillThere == null || hiddenStillThere(s)) return s;
                    continue;
                }
                if (CanTalkFrom(s.dir, playerFacing)) return s;
            }
            return null;
        }

        /// <summary>Whether a spawnable answers someone facing this way (TalkBgDirCheck).</summary>
        public static bool CanTalkFrom(int spawnableDir, MoveFacing playerFacing)
        {
            var approach = (SpawnableApproach)spawnableDir;
            if (approach == SpawnableApproach.AnyWay) return true;
            switch (playerFacing)
            {
                case MoveFacing.Up:
                    return approach == SpawnableApproach.FromBelow || approach == SpawnableApproach.FromAboveOrBelow;
                case MoveFacing.Down:
                    return approach == SpawnableApproach.FromAbove || approach == SpawnableApproach.FromAboveOrBelow;
                case MoveFacing.Left:
                    return approach == SpawnableApproach.FromRight || approach == SpawnableApproach.FromTheSides;
                default:
                    return approach == SpawnableApproach.FromLeft || approach == SpawnableApproach.FromTheSides;
            }
        }

        /// <summary>
        /// The trigger the player is standing in, or null. A trigger covers a rectangle from where it is
        /// placed, and only fires while its watched variable holds the value it is waiting for.
        /// </summary>
        public static Trigger TriggerAt(EventFile events, int tileX, int tileZ, Func<ushort, int> variableValue)
        {
            if (events?.triggers == null) return null;
            foreach (var t in events.triggers)
            {
                int x = TileX(t), z = TileZ(t);
                if (tileX < x || tileX >= x + Math.Max(1, (int)t.widthX)) continue;
                if (tileZ < z || tileZ >= z + Math.Max(1, (int)t.heightY)) continue;
                if (variableValue != null && variableValue(t.variableWatched) != t.expectedVarValue) continue;
                return t;
            }
            return null;
        }

        /// <summary>The warp on a tile, or null.</summary>
        public static Warp WarpAt(EventFile events, int tileX, int tileZ) =>
            events?.warps?.FirstOrDefault(w => TileX(w) == tileX && TileZ(w) == tileZ);
    }
}