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
        public string Name;

        public override string ToString() => $"{Name} ({Kind}, {Tiles} tiles, {Frames} frames)";
    }

    /// <summary>
    /// Reads a movement the way the games do, so the preview can play it out rather than just name it.
    /// </summary>
    public static class FieldMovementScript
    {
        /// <summary>A dash covers a tile in four frames, from AC_DASH_x_4F.</summary>
        public const int RunFrames = 4;

        private static readonly Regex Walk =
            new Regex(@"^(Walk|Jump)(OnSpot)?(North|South|West|East)(\d+)$", RegexOptions.Compiled);
        private static readonly Regex Face = new Regex(@"^Face(North|South|West|East)$", RegexOptions.Compiled);
        private static readonly Regex Run = new Regex(@"^Run(North|South|West|East)$", RegexOptions.Compiled);
        private static readonly Regex Far =
            new Regex(@"^Jump(Far|VeryFar)(North|South|West|East)$", RegexOptions.Compiled);
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

        /// <summary>Whether this action ends the movement.</summary>
        public static bool IsEnd(string name) =>
            !string.IsNullOrEmpty(name) && name.StartsWith("End", StringComparison.OrdinalIgnoreCase);

        /// <summary>Reads one action, or null when it is the end marker.</summary>
        public static FieldMovementStep ParseOne(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            name = name.Trim();
            if (IsEnd(name)) return null;

            var m = Walk.Match(name);
            if (m.Success)
            {
                bool onSpot = m.Groups[2].Success;
                bool jump = m.Groups[1].Value == "Jump";
                return new FieldMovementStep
                {
                    Kind = jump ? FieldActionKind.Jump : FieldActionKind.Walk,
                    Facing = Dir(m.Groups[3].Value),
                    Tiles = onSpot ? 0 : 1,
                    Frames = int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture),
                    Name = name,
                };
            }

            m = Far.Match(name);
            if (m.Success)
            {
                bool very = m.Groups[1].Value == "VeryFar";
                return new FieldMovementStep
                {
                    Kind = FieldActionKind.Jump,
                    Facing = Dir(m.Groups[2].Value),
                    Tiles = very ? 3 : 2,
                    Frames = very ? 32 : 16,
                    Name = name,
                };
            }

            m = Run.Match(name);
            if (m.Success)
                return new FieldMovementStep
                {
                    Kind = FieldActionKind.Walk,
                    Facing = Dir(m.Groups[1].Value),
                    Tiles = 1,
                    Frames = RunFrames,
                    Name = name,
                };

            m = Face.Match(name);
            if (m.Success)
                return new FieldMovementStep
                {
                    Kind = FieldActionKind.Face,
                    Facing = Dir(m.Groups[1].Value),
                    Tiles = 0,
                    Frames = 1,
                    Name = name,
                };

            m = Delay.Match(name);
            if (m.Success)
                return new FieldMovementStep
                {
                    Kind = FieldActionKind.Delay,
                    Tiles = 0,
                    Frames = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                    Name = name,
                };

            if (name == "SetVisible" || name == "SetInvisible")
                return new FieldMovementStep
                {
                    Kind = FieldActionKind.Appear,
                    Visible = name == "SetVisible",
                    Tiles = 0,
                    Frames = 1,
                    Name = name,
                };

            // Everything else still takes a frame, so a movement's timing does not come out short.
            return new FieldMovementStep { Kind = FieldActionKind.Other, Tiles = 0, Frames = 1, Name = name };
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
                if (IsEnd(name)) break;

                var step = ParseOne(name);
                if (step == null) break;

                int times = Math.Max(1, (int)(action.repetitionCount ?? 1));
                for (int i = 0; i < times && steps.Count < 512; i++)
                    steps.Add(new FieldMovementStep
                    {
                        Kind = step.Kind, Facing = step.Facing, Tiles = step.Tiles,
                        Frames = step.Frames, Visible = step.Visible, Name = step.Name,
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
