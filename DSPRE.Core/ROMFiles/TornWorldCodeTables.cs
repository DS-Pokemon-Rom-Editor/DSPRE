using System;
using System.Collections.Generic;
using System.IO;

namespace DSPRE.ROMFiles
{
    /// <summary>Distortion World tables that live in code: moving platforms and static props.</summary>
    public static class TornWorldCodeTables
    {
        public const int OverlayNumber = 9;

        private static readonly uint[] MovingPlatformFloors = { 573, 574, 575, 576, 577, 579, 580, 581 };

        private static readonly uint[] PropFloors = { 573, 579, 582, 583 };

        private const int MovingPlatformSize = 24;
        private const int PropSize = 16;
        private const int PropKindCount = 25;
        private const int AnimationKindCount = 5;
        private const int HoverStepCount = 8;
        private const int ElevatorPathCount = 22;
        private const int ElevatorPathSize = 32;
        private const int FixedOne = 0x1000;

        private const float HoverStepPerFrame = 0x800 / (float)FixedOne;
        private const uint PropKindInvalid = PropKindCount;

        public class MovingPlatform
        {
            public int Index;
            public short TileX, TileY, TileZ;
            public int ElevatorPathIndex;
            public int ElevatorDirection;
            public int DestinationIndex;
            public int PropKind;
            public uint PersistedFlag;

            public bool IsElevator => ElevatorDirection == 0 || ElevatorDirection == 1;
        }

        public class ElevatorPath
        {
            public int Index;
            public int NextIndex;
            public short FinalX, FinalY, FinalZ;
            public short ChangeMapsX, ChangeMapsY, ChangeMapsZ;
            public float SpeedX, SpeedY, SpeedZ;
            public int FlagToSet, FlagToClear;

            public int Frames
            {
                get
                {
                    float steps = Math.Max(Math.Abs(Step(FinalX, SpeedX)),
                                  Math.Max(Math.Abs(Step(FinalY, SpeedY)), Math.Abs(Step(FinalZ, SpeedZ))));
                    return steps > 0 ? (int)Math.Ceiling(steps) : 0;
                }
            }

            private static float Step(short tiles, float speed) => speed == 0 ? 0 : tiles * 16f / speed;
        }

        public class Prop
        {
            public int PropKind;
            public short TileX, TileY, TileZ;
            public int FlagCondition;
            public int FlagConditionValue;
        }

        public class Tables
        {
            public Dictionary<uint, List<MovingPlatform>> MovingPlatforms = new Dictionary<uint, List<MovingPlatform>>();
            public Dictionary<uint, List<Prop>> Props = new Dictionary<uint, List<Prop>>();

            public int[] ModelByPropKind = Array.Empty<int>();

            public int[] AnimationByPropKind = Array.Empty<int>();

            public float[] HoverOffsets = Array.Empty<float>();

            public float HoverStep;

            public (float x, float y, float z)[] OffsetByPropKind = Array.Empty<(float, float, float)>();

            public (float x, float y, float z)[] ScaleByPropKind = Array.Empty<(float, float, float)>();

            public (float x, float y, float z) OffsetFor(int propKind)
                => propKind >= 0 && propKind < OffsetByPropKind.Length ? OffsetByPropKind[propKind] : (0f, 0f, 0f);

            /// <summary>sPropScaleByKind: sizes the culling box (IsPropInView), never the drawn model.</summary>
            public (float x, float y, float z) ScaleFor(int propKind)
                => propKind >= 0 && propKind < ScaleByPropKind.Length ? ScaleByPropKind[propKind] : (0f, 0f, 0f);

            public List<ElevatorPath> ElevatorPaths = new List<ElevatorPath>();

            public ElevatorPath PathAt(int index)
                => index >= 0 && index < ElevatorPaths.Count ? ElevatorPaths[index] : null;

            public int[] AnimationMembers = Array.Empty<int>();

            public int AnimationFor(int propKind)
            {
                if (propKind < 0 || propKind >= AnimationByPropKind.Length) return -1;
                int kind = AnimationByPropKind[propKind];
                return kind >= 0 && kind < AnimationMembers.Length ? AnimationMembers[kind] : -1;
            }

            public IReadOnlyList<MovingPlatform> PlatformsOn(uint headerId)
                => MovingPlatforms.TryGetValue(headerId, out var list) ? list : (IReadOnlyList<MovingPlatform>)Array.Empty<MovingPlatform>();

            public IReadOnlyList<Prop> PropsOn(uint headerId)
                => Props.TryGetValue(headerId, out var list) ? list : (IReadOnlyList<Prop>)Array.Empty<Prop>();

            public int ModelFor(int propKind)
                => propKind >= 0 && propKind < ModelByPropKind.Length ? ModelByPropKind[propKind] : -1;
        }

        private static Tables _cached;
        private static string _cachedFor;

        public static void Forget() { _cached = null; _cachedFor = null; }

        public static Tables Read(out string error)
        {
            error = null;
            string project = RomInfo.workDir ?? "";
            if (_cached != null && _cachedFor == project) return _cached;

            if (RomInfo.gameVersion != RomInfo.GameVersions.Platinum)
            {
                error = "Only Platinum has a Distortion World.";
                return null;
            }

            string path = OverlayUtils.GetPath(OverlayNumber);
            if (!File.Exists(path)) { error = $"Overlay {OverlayNumber} is missing from this project."; return null; }

            if (!RomInfo.IsDsRomProject &&
                OverlayUtils.OverlayTable.IsDefaultCompressed(OverlayNumber) &&
                OverlayUtils.IsCompressed(OverlayNumber))
            {
                error = $"Overlay {OverlayNumber} is still compressed. Convert this project to ds-rom format first.";
                return null;
            }

            byte[] data;
            try { data = File.ReadAllBytes(path); }
            catch (Exception ex) { error = $"Overlay {OverlayNumber} could not be read: {ex.Message}"; return null; }

            uint ramBase = OverlayUtils.OverlayTable.GetRAMAddress(OverlayNumber);
            if (ramBase == 0) { error = $"Overlay {OverlayNumber} has no load address in the overlay table."; return null; }

            var tables = new Tables();
            long movingAt = FindFloorList(data, ramBase, MovingPlatformFloors, exact: true);
            if (movingAt >= 0) ReadMovingPlatforms(data, ramBase, movingAt, tables);

            long propsAt = FindFloorList(data, ramBase, PropFloors, exact: false);
            if (propsAt >= 0) ReadProps(data, ramBase, propsAt, tables);

            tables.ModelByPropKind = FindModelTable(data);
            tables.AnimationByPropKind = FindAnimationKindTable(data);
            tables.AnimationMembers = FindAnimationMemberTable(data);
            tables.HoverOffsets = FindHoverOffsets(data);
            tables.HoverStep = HoverStepPerFrame;
            tables.ElevatorPaths = FindElevatorPaths(data);
            tables.OffsetByPropKind = FindVectorsByPropKind(data, offsets: true);
            tables.ScaleByPropKind = FindVectorsByPropKind(data, offsets: false);

            if (movingAt < 0 && propsAt < 0)
            {
                error = "Platform and prop tables not found in the Distortion World overlay.";
                return null;
            }

            _cached = tables;
            _cachedFor = project;
            return tables;
        }

        private static long FindFloorList(byte[] data, uint ramBase, uint[] floors, bool exact)
        {
            uint ramEnd = ramBase + (uint)data.Length;
            for (long at = 0; at + floors.Length * 8 <= data.Length; at += 4)
            {
                bool match = true;
                for (int i = 0; i < floors.Length && match; i++)
                {
                    uint header = Word(data, at + i * 8);
                    uint pointer = Word(data, at + i * 8 + 4);
                    match = header == floors[i] && pointer >= ramBase && pointer < ramEnd;
                }
                if (!match) continue;

                if (exact && at + floors.Length * 8 + 4 <= data.Length)
                {
                    uint next = Word(data, at + floors.Length * 8);
                    if (next >= 500 && next <= 600) continue;
                }
                return at;
            }
            return -1;
        }

        private static void ReadMovingPlatforms(byte[] data, uint ramBase, long at, Tables tables)
        {
            for (int i = 0; i < MovingPlatformFloors.Length; i++)
            {
                uint header = Word(data, at + i * 8);
                long list = Offset(Word(data, at + i * 8 + 4), ramBase, data.Length);
                if (list < 0) continue;

                var platforms = new List<MovingPlatform>();
                for (long p = list; p + 4 <= data.Length; p += 4)
                {
                    uint entry = Word(data, p);
                    if (entry == 0) break;
                    long record = Offset(entry, ramBase, data.Length);
                    if (record < 0 || record + MovingPlatformSize > data.Length) break;

                    platforms.Add(new MovingPlatform
                    {
                        Index = Half(data, record),
                        TileX = Signed(data, record + 2),
                        TileY = Signed(data, record + 4),
                        TileZ = Signed(data, record + 6),
                        ElevatorPathIndex = Half(data, record + 8),
                        ElevatorDirection = Half(data, record + 10),
                        DestinationIndex = (int)Word(data, record + 12),
                        PropKind = (int)Word(data, record + 16),
                        PersistedFlag = Word(data, record + 20),
                    });
                    if (platforms.Count > 64) break;
                }
                if (platforms.Count > 0) tables.MovingPlatforms[header] = platforms;
            }
        }

        private static void ReadProps(byte[] data, uint ramBase, long at, Tables tables)
        {
            for (int i = 0; i < PropFloors.Length; i++)
            {
                uint header = Word(data, at + i * 8);
                long list = Offset(Word(data, at + i * 8 + 4), ramBase, data.Length);
                if (list < 0) continue;

                var props = new List<Prop>();
                for (long p = list; p + PropSize <= data.Length; p += PropSize)
                {
                    uint kind = Half(data, p + 4);
                    if (kind >= PropKindInvalid) break;

                    props.Add(new Prop
                    {
                        PropKind = (int)kind,
                        TileX = Signed(data, p + 6),
                        TileY = Signed(data, p + 8),
                        TileZ = Signed(data, p + 10),
                        FlagCondition = Half(data, p + 12),
                        FlagConditionValue = Half(data, p + 14),
                    });
                    if (props.Count > 64) break;
                }
                if (props.Count > 0) tables.Props[header] = props;
            }
        }

        private static int[] FindModelTable(byte[] data)
        {
            for (long at = 0; at + PropKindCount * 4 <= data.Length; at += 4)
            {
                uint first = Word(data, at);
                if (first < 0x20 || first > 0x400) continue;

                bool run = true;
                for (int i = 1; i < PropKindCount && run; i++) run = Word(data, at + i * 4) == first + i;
                if (!run) continue;

                var members = new int[PropKindCount];
                for (int i = 0; i < PropKindCount; i++) members[i] = (int)(first + i);
                return members;
            }
            return Array.Empty<int>();
        }

        private static int[] FindAnimationKindTable(byte[] data)
        {
            for (long at = 0; at + PropKindCount * 8 <= data.Length; at += 4)
            {
                bool run = true;
                for (int i = 0; i < PropKindCount && run; i++) run = Half(data, at + i * 8) == i;
                if (!run) continue;

                var kinds = new int[PropKindCount];
                bool anyAnimated = false;
                for (int i = 0; i < PropKindCount; i++)
                {
                    int kind = Half(data, at + i * 8 + 2);
                    kinds[i] = kind >= AnimationKindCount ? -1 : kind;
                    anyAnimated |= kinds[i] >= 0;
                }
                if (anyAnimated) return kinds;
            }
            return Array.Empty<int>();
        }

        private static int[] FindAnimationMemberTable(byte[] data)
        {
            for (long at = 0; at + AnimationKindCount * 4 <= data.Length; at += 4)
            {
                var members = new int[AnimationKindCount];
                bool plausible = true;
                for (int i = 0; i < AnimationKindCount && plausible; i++)
                {
                    uint member = Word(data, at + i * 4);
                    plausible = member >= 0x95 && member <= 0x200;
                    members[i] = (int)member;
                }
                if (!plausible) continue;
                if (new HashSet<int>(members).Count != AnimationKindCount) continue;

                if (at + AnimationKindCount * 4 + 4 <= data.Length)
                {
                    uint next = Word(data, at + AnimationKindCount * 4);
                    if (next >= 0x95 && next <= 0x200) continue;
                }
                return members;
            }
            return Array.Empty<int>();
        }

        private static float[] FindHoverOffsets(byte[] data)
        {
            for (long at = 0; at + HoverStepCount * 4 <= data.Length; at += 4)
            {
                if (Word(data, at) != 0) continue;

                var drops = new float[HoverStepCount];
                bool sinking = true;
                int previous = 0;
                for (int i = 1; i < HoverStepCount && sinking; i++)
                {
                    int drop = (int)Word(data, at + i * 4);
                    sinking = drop < previous && drop > -0x10000;
                    drops[i] = drop / (float)FixedOne;
                    previous = drop;
                }
                if (sinking) return drops;
            }
            return Array.Empty<float>();
        }

        private static List<ElevatorPath> FindElevatorPaths(byte[] data)
        {
            for (long at = 0; at + ElevatorPathCount * ElevatorPathSize <= data.Length; at += 4)
            {
                bool run = true;
                for (int i = 0; i < ElevatorPathCount && run; i++)
                {
                    long record = at + i * ElevatorPathSize;
                    run = Half(data, record) == i && Half(data, record + 2) <= ElevatorPathCount;
                }
                if (!run) continue;

                var paths = new List<ElevatorPath>();
                for (int i = 0; i < ElevatorPathCount; i++)
                {
                    long record = at + i * ElevatorPathSize;
                    paths.Add(new ElevatorPath
                    {
                        Index = Half(data, record),
                        NextIndex = Half(data, record + 2),
                        FinalX = Signed(data, record + 4),
                        FinalY = Signed(data, record + 6),
                        FinalZ = Signed(data, record + 8),
                        ChangeMapsX = Signed(data, record + 10),
                        ChangeMapsY = Signed(data, record + 12),
                        ChangeMapsZ = Signed(data, record + 14),
                        SpeedX = (int)Word(data, record + 16) / (float)FixedOne,
                        SpeedY = (int)Word(data, record + 20) / (float)FixedOne,
                        SpeedZ = (int)Word(data, record + 24) / (float)FixedOne,
                        FlagToSet = Half(data, record + 28),
                        FlagToClear = Half(data, record + 30),
                    });
                }

                if (paths.Exists(p => p.SpeedX != 0 || p.SpeedY != 0 || p.SpeedZ != 0)) return paths;
            }
            return new List<ElevatorPath>();
        }

        private static (float x, float y, float z)[] FindVectorsByPropKind(byte[] data, bool offsets)
        {
            const int stride = 12;
            for (long at = 0; at + PropKindCount * stride <= data.Length; at += 4)
            {
                var first = (Signed32(data, at), Signed32(data, at + 4), Signed32(data, at + 8));
                if (offsets)
                {
                    if (first.Item1 != 0 || first.Item2 >= 0 || first.Item2 < -64 * FixedOne) continue;
                }
                else
                {
                    if (first.Item1 != first.Item2 || first.Item2 != first.Item3) continue;
                    if (first.Item1 < FixedOne / 2 || first.Item1 > FixedOne * 4) continue;
                }

                int same = 0;
                bool sane = true;
                for (int i = 0; i < PropKindCount && sane; i++)
                {
                    long record = at + i * stride;
                    var entry = (Signed32(data, record), Signed32(data, record + 4), Signed32(data, record + 8));
                    sane = Math.Abs(entry.Item1) <= 64 * FixedOne
                        && Math.Abs(entry.Item2) <= 64 * FixedOne
                        && Math.Abs(entry.Item3) <= 64 * FixedOne;
                    if (entry.Equals(first)) same++;
                }
                if (!sane || same < 15) continue;

                var found = new (float, float, float)[PropKindCount];
                for (int i = 0; i < PropKindCount; i++)
                {
                    long record = at + i * stride;
                    found[i] = (Signed32(data, record) / (float)FixedOne,
                                Signed32(data, record + 4) / (float)FixedOne,
                                Signed32(data, record + 8) / (float)FixedOne);
                }
                return found;
            }
            return Array.Empty<(float, float, float)>();
        }

        private static int Signed32(byte[] data, long at) => (int)Word(data, at);

        private static long Offset(uint pointer, uint ramBase, int length)
        {
            if (pointer < ramBase) return -1;
            long at = pointer - ramBase;
            return at >= 0 && at < length ? at : -1;
        }

        private static uint Word(byte[] data, long at)
            => (uint)(data[at] | (data[at + 1] << 8) | (data[at + 2] << 16) | (data[at + 3] << 24));

        private static ushort Half(byte[] data, long at) => (ushort)(data[at] | (data[at + 1] << 8));

        private static short Signed(byte[] data, long at) => (short)Half(data, at);
    }
}
