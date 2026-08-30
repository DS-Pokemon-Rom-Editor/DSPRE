using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.ROMFiles
{
    public enum MoveFacing { Up, Down, Left, Right }

    public enum MoveKind
    {
        Static,        // never moves or turns
        Player,        // reserved for the player object
        TurnRandom,    // turns on the spot at random, never leaves its tile
        Wander,        // walks about at random
        FaceFixed,     // stands still facing one way
        Spin,          // turns on the spot in a fixed direction
        Route,         // walks a set path, turning back when it reaches the end
        Special,       // hiding, pairs, rematches and so on: nothing generic to animate
    }

    /// <summary>
    /// What an overworld's move_code makes it do, for previewing a map without running scripts.
    /// Values and names come from the games' fieldobj_code.h, and each code's behaviour from the
    /// handler the move table registers for it in fieldobj_movedata.c. The engine defines 0x00-0x38.
    /// </summary>
    public sealed class OverworldMovement
    {
        public byte Value;
        public string Name;
        public MoveKind Kind;
        /// <summary>Directions this movement uses, in order for a route and as a choice for the rest.</summary>
        public IReadOnlyList<MoveFacing> Facings = Array.Empty<MoveFacing>();
        public bool SpinClockwise;

        /// <summary>A route with no directions of its own walks the way the event is facing (MV_RT2).</summary>
        public bool RouteFollowsEventFacing => Kind == MoveKind.Route && Facings.Count == 0;

        public override string ToString() => $"[{Value:D2}]  {Name}";
    }

    public static class OverworldMovements
    {
        public const byte MaxDefined = 0x38;   // MV_CODE_MAX is 0x39, so 0x38 is the last real code
        public const byte NotSet = 0xFF;       // MV_CODE_NOT

        private static readonly Dictionary<char, MoveFacing> Letters = new Dictionary<char, MoveFacing>
        {
            ['U'] = MoveFacing.Up, ['D'] = MoveFacing.Down, ['L'] = MoveFacing.Left, ['R'] = MoveFacing.Right,
        };

        private static MoveFacing[] Parse(string letters) => letters.Select(c => Letters[c]).ToArray();

        private static string Spell(string letters) =>
            string.Join(", ", Parse(letters).Select(f => f.ToString().ToLowerInvariant()));

        private static readonly OverworldMovement[] Table = BuildTable();

        private static OverworldMovement[] BuildTable()
        {
            var list = new List<OverworldMovement>
            {
                new OverworldMovement { Value = 0x00, Name = "None",     Kind = MoveKind.Static },
                new OverworldMovement { Value = 0x01, Name = "Player",   Kind = MoveKind.Player },

                // MV_DIR_RND and the MV_RND_UL family all register a DirRnd handler, which switches the
                // move status off for good: they turn on the spot and never leave their tile.
                new OverworldMovement { Value = 0x02, Name = "Look around", Kind = MoveKind.TurnRandom,
                    Facings = Parse("UDLR") },

                // Only these three walk at random. They take no direction table beyond their axis.
                new OverworldMovement { Value = 0x03, Name = "Walk about",              Kind = MoveKind.Wander, Facings = Parse("UDLR") },
                new OverworldMovement { Value = 0x04, Name = "Walk up and down",        Kind = MoveKind.Wander, Facings = Parse("UD") },
                new OverworldMovement { Value = 0x05, Name = "Walk left and right",     Kind = MoveKind.Wander, Facings = Parse("LR") },
            };

            // 0x06-0x0d: look around, but only towards some of the directions.
            byte v = 0x06;
            foreach (string set in new[] { "UL", "UR", "DL", "DR", "UDL", "UDR", "ULR", "DLR" })
                list.Add(new OverworldMovement
                {
                    Value = v++,
                    Name = "Look around, " + Spell(set),
                    Kind = MoveKind.TurnRandom,
                    Facings = Parse(set),
                });

            foreach (var (val, set) in new (byte, string)[] { (0x0e, "U"), (0x0f, "D"), (0x10, "L"), (0x11, "R") })
                list.Add(new OverworldMovement
                {
                    Value = val,
                    Name = "Face " + Spell(set),
                    Kind = MoveKind.FaceFixed,
                    Facings = Parse(set),
                });

            list.Add(new OverworldMovement { Value = 0x12, Name = "Spin anticlockwise", Kind = MoveKind.Spin });
            list.Add(new OverworldMovement { Value = 0x13, Name = "Spin clockwise",     Kind = MoveKind.Spin, SpinClockwise = true });

            // MV_RT2 walks back and forth the way the event faces, turning round at the end of its range.
            list.Add(new OverworldMovement { Value = 0x14, Name = "Walk back and forth", Kind = MoveKind.Route });

            // 0x15-0x24 patrol four points, 0x25-0x2c patrol two; the letters after RT are the order.
            string[] routes =
            {
                "URLD", "RLDU", "DURL", "LDUR", "ULRD", "LRDU", "DULR", "RDUL",
                "LUDR", "UDRL", "RLUD", "DRLU", "RUDL", "UDLR", "LRUD", "DLRU",
                "UL", "DR", "LD", "RU", "UR", "DL", "LU", "RD",
            };
            v = 0x15;
            foreach (string route in routes)
                list.Add(new OverworldMovement
                {
                    Value = v++,
                    Name = "Walk " + Spell(route),
                    Kind = MoveKind.Route,
                    Facings = Parse(route),
                });

            // Two more look-around codes, added later than the 0x06 block but the same handler.
            list.Add(new OverworldMovement { Value = 0x2d, Name = "Look around, up, down",  Kind = MoveKind.TurnRandom, Facings = Parse("UD") });
            list.Add(new OverworldMovement { Value = 0x2e, Name = "Look around, left, right", Kind = MoveKind.TurnRandom, Facings = Parse("LR") });

            foreach (var (val, name) in new (byte, string)[]
            {
                (0x2f, "Berry tree"), (0x30, "Follows the player"), (0x31, "Rematch"),
                (0x32, "Trainer follows the player"),
                (0x33, "Hidden in snow"), (0x34, "Hidden in sand"), (0x35, "Hidden in ground"),
                (0x36, "Hidden in grass"),
                (0x37, "Follows the player, no delay"), (0x38, "Follows the player, copying its movement"),
            })
                list.Add(new OverworldMovement { Value = val, Name = name, Kind = MoveKind.Special });

            return list.ToArray();
        }

        public static IReadOnlyList<OverworldMovement> All => Table;

        public static OverworldMovement Find(byte value) => Table.FirstOrDefault(m => m.Value == value);

        /// <summary>True for a code no game defines, so the preview can leave it alone.</summary>
        public static bool IsDefined(byte value) => value <= MaxDefined && Find(value) != null;
    }
}
