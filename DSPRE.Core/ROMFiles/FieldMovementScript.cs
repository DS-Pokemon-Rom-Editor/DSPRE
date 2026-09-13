using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DSPRE.ROMFiles
{
    /// <summary>What one step of a movement does.</summary>
    public enum FieldActionKind
    {
        /// <summary>Turns on the spot without going anywhere.</summary>
        Face,
        /// <summary>Walks, which may be on the spot when it covers no ground.</summary>
        Walk,
        /// <summary>Hops, which arcs up and comes back down.</summary>
        Jump,
        /// <summary>Stands still for a while.</summary>
        Delay,
        /// <summary>Appears or disappears.</summary>
        Appear,
        /// <summary>A mark pops up over its head.</summary>
        Emote,
        /// <summary>Something the preview has no picture for, held for a frame so the timing still adds up.</summary>
        Other,
    }

    /// <summary>One step of a movement, with how far it goes and how long it takes.</summary>
    public sealed class FieldMovementStep
    {
        public FieldActionKind Kind;
        public MoveFacing Facing;
        /// <summary>How many tiles it covers. Zero for turning, waiting and anything done on the spot.</summary>
        public int Tiles;
        /// <summary>How many frames it takes.</summary>
        public int Frames;
        /// <summary>Set by the two showing and hiding actions.</summary>
        public bool? Visible;
        /// <summary>A dash, which the hero draws from his running pictures.</summary>
        public bool Run;
        public string Name;

        public override string ToString() => $"{Name} ({Kind}, {Tiles} tiles, {Frames} frames)";
    }

    /// <summary>
    /// Reads a movement the way the games do, so the preview can play it out rather than just name it.
    /// Durations follow unk_020655F4.c in the Platinum decomp; HGSS uses the same action numbers.
    /// </summary>
    public static class FieldMovementScript
    {
        /// <summary>A dash covers a tile in four frames, from AC_DASH_x_4F.</summary>
        public const int RunFrames = 4;

        /// <summary>The exclamation mark: one frame to appear, seven bouncing and thirty held.</summary>
        public const int EmoteFrames = 38;

        /// <summary>How far the mark sits above the ground on each of its bouncing frames, in game units.</summary>
        public static readonly int[] EmoteBounce = { 6, 10, 12, 12, 10, 6, 0 };

        private static readonly MoveFacing[] Compass = { MoveFacing.Up, MoveFacing.Down, MoveFacing.Left, MoveFacing.Right };

        private static readonly Regex OldWalk =
            new Regex(@"^(Walk|Jump)(OnSpot)?(North|South|West|East)(\d+)$", RegexOptions.Compiled);
        private static readonly Regex NewWalk = new Regex(
            @"^Walk(OnSpot)?(Slower|Slow|Normal|Fastest|Faster|Fast|SlightlyFaster|SlightlyFast|EverSoSlightlyFast)(North|South|West|East)$",
            RegexOptions.Compiled);
        private static readonly Regex JumpOnSpot = new Regex(@"^JumpOnSpot(Slow|Fast)(North|South|West|East)$", RegexOptions.Compiled);
        private static readonly Regex JumpNear = new Regex(@"^JumpNear(Slow|Fast)(North|South|West|East)$", RegexOptions.Compiled);
        private static readonly Regex Face = new Regex(@"^Face(North|South|West|East)$", RegexOptions.Compiled);
        private static readonly Regex Run = new Regex(@"^Run(North|South|West|East)$", RegexOptions.Compiled);
        private static readonly Regex Far =
            new Regex(@"^Jump(Far|VeryFar|Farther)(North|South|West|East)$", RegexOptions.Compiled);
        private static readonly Regex Delay = new Regex(@"^Delay(\d+)$", RegexOptions.Compiled);

        private static MoveFacing Dir(string name)
        {
            switch (name)
            {
                case "North": return MoveFacing.Up;
                case "South": return MoveFacing.Down;
                case "West": return MoveFacing.Left;
                default: return MoveFacing.Right;
            }
        }

        private static int SpeedFrames(string speed)
        {
            switch (speed)
            {
                case "Slower": return 32;
                case "Slow": return 16;
                case "Normal": return 8;
                case "Fast": return 4;
                case "Faster": return 2;
                case "Fastest": return 1;
                case "SlightlyFast": return 6;
                case "SlightlyFaster": return 3;
                default: return 7;          // EverSoSlightlyFast
            }
        }

        /// <summary>Whether this action ends the movement.</summary>
        public static bool IsEnd(string name) =>
            !string.IsNullOrEmpty(name) && name.StartsWith("End", StringComparison.OrdinalIgnoreCase);

        private static FieldMovementStep Make(FieldActionKind kind, MoveFacing facing, int tiles, int frames, string name) =>
            new FieldMovementStep { Kind = kind, Facing = facing, Tiles = tiles, Frames = frames, Name = name };

        /// <summary>
        /// Reads one action by its number, which is the same whatever the database calls it. Null when the
        /// number is the end marker or one this table does not know.
        /// </summary>
        public static FieldMovementStep FromId(int id, string name = null)
        {
            MoveFacing D(int first) => Compass[(id - first) & 3];

            if (id == 0xFE) return null;
            if (id <= 3) return Make(FieldActionKind.Face, D(0), 0, 1, name);
            if (id <= 23) return Make(FieldActionKind.Walk, D(4), 1, 32 >> ((id - 4) / 4), name);
            // On-the-spot walks last one frame longer than the walk they mimic.
            if (id <= 43) return Make(FieldActionKind.Walk, D(24), 0, (32 >> ((id - 24) / 4)) + 1, name);
            if (id <= 47) return Make(FieldActionKind.Jump, D(44), 0, 16, name);
            if (id <= 51) return Make(FieldActionKind.Jump, D(48), 0, 8, name);
            if (id <= 55) return Make(FieldActionKind.Jump, D(52), 1, 8, name);
            if (id <= 59) return Make(FieldActionKind.Jump, D(56), 2, 16, name);
            if (id <= 66)
            {
                int[] delays = { 1, 2, 4, 8, 15, 16, 32 };
                return Make(FieldActionKind.Delay, MoveFacing.Down, 0, delays[id - 60] + 1, name);
            }
            if (id == 69 || id == 70)
            {
                var s = Make(FieldActionKind.Appear, MoveFacing.Down, 0, 1, name);
                s.Visible = id == 70;
                return s;
            }
            if (id == 75 || id == 103 || id == 0x99) return Make(FieldActionKind.Emote, MoveFacing.Down, 0, EmoteFrames, name);
            if (id >= 76 && id <= 79) return Make(FieldActionKind.Walk, D(76), 1, 6, name);
            if (id >= 80 && id <= 83) return Make(FieldActionKind.Walk, D(80), 1, 3, name);
            if (id >= 84 && id <= 87) return Make(FieldActionKind.Walk, D(84), 1, 1, name);
            if (id >= 88 && id <= 91) { var dash = Make(FieldActionKind.Walk, D(88), 1, RunFrames, name); dash.Run = true; return dash; }
            if (id == 92 || id == 93) return Make(FieldActionKind.Jump, id == 92 ? MoveFacing.Left : MoveFacing.Right, 1, 16, name);
            if (id == 94 || id == 95) return Make(FieldActionKind.Jump, id == 94 ? MoveFacing.Left : MoveFacing.Right, 3, 12, name);
            if (id >= 96 && id <= 99) return Make(FieldActionKind.Walk, D(96), 1, 7, name);
            return Make(FieldActionKind.Other, MoveFacing.Down, 0, 1, name);
        }

        /// <summary>Reads one action by name, old database or decomp spelling, or null for the end marker.</summary>
        public static FieldMovementStep ParseOne(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            name = name.Trim();
            if (IsEnd(name)) return null;

            var m = OldWalk.Match(name);
            if (m.Success)
            {
                bool onSpot = m.Groups[2].Success;
                bool jump = m.Groups[1].Value == "Jump";
                int frames = int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture);
                return Make(jump ? FieldActionKind.Jump : FieldActionKind.Walk, Dir(m.Groups[3].Value),
                            onSpot ? 0 : 1, onSpot && !jump ? frames + 1 : frames, name);
            }

            m = NewWalk.Match(name);
            if (m.Success)
            {
                bool onSpot = m.Groups[1].Success;
                int frames = SpeedFrames(m.Groups[2].Value);
                return Make(FieldActionKind.Walk, Dir(m.Groups[3].Value), onSpot ? 0 : 1, onSpot ? frames + 1 : frames, name);
            }

            m = JumpOnSpot.Match(name);
            if (m.Success)
                return Make(FieldActionKind.Jump, Dir(m.Groups[2].Value), 0, m.Groups[1].Value == "Slow" ? 16 : 8, name);

            m = JumpNear.Match(name);
            if (m.Success)
                return Make(FieldActionKind.Jump, Dir(m.Groups[2].Value), 1, m.Groups[1].Value == "Slow" ? 16 : 8, name);

            m = Far.Match(name);
            if (m.Success)
            {
                bool farther = m.Groups[1].Value != "Far";
                return Make(FieldActionKind.Jump, Dir(m.Groups[2].Value), farther ? 3 : 2, farther ? 12 : 16, name);
            }

            m = Run.Match(name);
            if (m.Success) { var dash = Make(FieldActionKind.Walk, Dir(m.Groups[1].Value), 1, RunFrames, name); dash.Run = true; return dash; }

            m = Face.Match(name);
            if (m.Success) return Make(FieldActionKind.Face, Dir(m.Groups[1].Value), 0, 1, name);

            m = Delay.Match(name);
            if (m.Success)
                return Make(FieldActionKind.Delay, MoveFacing.Down, 0,
                            int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) + 1, name);

            if (name == "SetVisible" || name == "SetInvisible")
            {
                var s = Make(FieldActionKind.Appear, MoveFacing.Down, 0, 1, name);
                s.Visible = name == "SetVisible";
                return s;
            }

            if (name.StartsWith("Emote", StringComparison.Ordinal))
                return Make(FieldActionKind.Emote, MoveFacing.Down, 0, EmoteFrames, name);

            // Everything else still takes a frame, so a movement's timing does not come out short.
            return Make(FieldActionKind.Other, MoveFacing.Down, 0, 1, name);
        }

        /// <summary>Reads a whole movement, repeating the actions that ask to be repeated.</summary>
        public static List<FieldMovementStep> Parse(IEnumerable<ScriptAction> actions)
        {
            var steps = new List<FieldMovementStep>();
            if (actions == null) return steps;

            foreach (var action in actions)
            {
                if (action == null) continue;
                string name = StripCount(action.name);
                if (action.id == 0xFE || (action.id == null && IsEnd(name))) break;

                // The number means the same thing whichever names the database gives the actions.
                var step = action.id != null ? FromId(action.id.Value, name) : ParseOne(name);
                if (step == null) break;

                int times = Math.Max(1, (int)(action.repetitionCount ?? 1));
                for (int i = 0; i < times && steps.Count < 512; i++)
                    steps.Add(new FieldMovementStep
                    {
                        Kind = step.Kind, Facing = step.Facing, Tiles = step.Tiles,
                        Frames = step.Frames, Visible = step.Visible, Name = step.Name, Run = step.Run,
                    });
            }
            return steps;
        }

        /// <summary>How long a whole movement takes, in frames.</summary>
        public static int TotalFrames(IEnumerable<FieldMovementStep> steps)
        {
            int total = 0;
            if (steps != null) foreach (var s in steps) total += Math.Max(1, s.Frames);
            return total;
        }

        // A read action carries its repeat count on the end of its name; the count is read separately.
        private static string StripCount(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            int at = name.IndexOf(" 0x", StringComparison.Ordinal);
            return at > 0 ? name.Substring(0, at) : name;
        }
    }
}
