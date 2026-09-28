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
        Special,       // hiding, following, berry patches and so on: nothing generic to animate
    }

    /// <summary>
    /// What an overworld's movement type makes it do, for previewing a map without running scripts.
    /// </summary>
    public sealed class OverworldMovement
    {
        public byte Value;
        public string Name;
        public MoveKind Kind;
        /// <summary>Directions this movement uses, in order for a route and as a choice for the rest.</summary>
        public IReadOnlyList<MoveFacing> Facings = Array.Empty<MoveFacing>();
        public bool SpinClockwise;

        /// <summary>A route with no directions of its own walks the way the event is facing (type 20).</summary>
        public bool RouteFollowsEventFacing => Kind == MoveKind.Route && Facings.Count == 0;

        public override string ToString() => $"[{Value:D2}]  {Name}";
    }

    /// <summary>
    /// The movement types each game defines. 0-46 and 48-54 mean the same in all three families;
    /// Diamond/Pearl stops at 54, HeartGold/SoulSilver has no berry patch at 47 and adds 55-56, and
    /// Platinum adds 55-67.
    /// </summary>
    public static class OverworldMovements
    {
        public const byte BerryPatch = 47;

        private static readonly Dictionary<char, MoveFacing> Letters = new Dictionary<char, MoveFacing>
        {
            ['U'] = MoveFacing.Up, ['D'] = MoveFacing.Down, ['L'] = MoveFacing.Left, ['R'] = MoveFacing.Right,
        };

        private static MoveFacing[] Parse(string letters) => letters.Select(c => Letters[c]).ToArray();

        private static string Spell(string letters) =>
            string.Join(", ", Parse(letters).Select(f => f.ToString().ToLowerInvariant()));

        private static readonly OverworldMovement[] Common = BuildCommon();
        private static readonly OverworldMovement[] DpTable = Common.ToArray();
        private static readonly OverworldMovement[] PtTable = Common.Concat(PlatinumExtras()).ToArray();
        private static readonly OverworldMovement[] HgssTable =
            Common.Where(m => m.Value != BerryPatch).Concat(HgssExtras()).ToArray();

        private static OverworldMovement[] BuildCommon()
        {
            var list = new List<OverworldMovement>
            {
                new OverworldMovement { Value = 0, Name = "None",   Kind = MoveKind.Static },
                new OverworldMovement { Value = 1, Name = "Player", Kind = MoveKind.Player },

                // The look-around types turn on the spot and never leave their tile.
                new OverworldMovement { Value = 2, Name = "Look around", Kind = MoveKind.TurnRandom,
                    Facings = Parse("UDLR") },

                // Only these three walk at random.
                new OverworldMovement { Value = 3, Name = "Walk about",          Kind = MoveKind.Wander, Facings = Parse("UDLR") },
                new OverworldMovement { Value = 4, Name = "Walk up and down",    Kind = MoveKind.Wander, Facings = Parse("UD") },
                new OverworldMovement { Value = 5, Name = "Walk left and right", Kind = MoveKind.Wander, Facings = Parse("LR") },
            };

            byte v = 6;
            foreach (string set in new[] { "UL", "UR", "DL", "DR", "UDL", "UDR", "ULR", "DLR" })
                list.Add(new OverworldMovement
                {
                    Value = v++,
                    Name = "Look around, " + Spell(set),
                    Kind = MoveKind.TurnRandom,
                    Facings = Parse(set),
                });

            foreach (var (val, set) in new (byte, string)[] { (14, "U"), (15, "D"), (16, "L"), (17, "R") })
                list.Add(new OverworldMovement
                {
                    Value = val,
                    Name = "Face " + Spell(set),
                    Kind = MoveKind.FaceFixed,
                    Facings = Parse(set),
                });

            list.Add(new OverworldMovement { Value = 18, Name = "Spin anticlockwise", Kind = MoveKind.Spin });
            list.Add(new OverworldMovement { Value = 19, Name = "Spin clockwise",     Kind = MoveKind.Spin, SpinClockwise = true });

            // Walks the way the event faces, turning round at the end of its range.
            list.Add(new OverworldMovement { Value = 20, Name = "Walk back and forth", Kind = MoveKind.Route });

            // 21-44 each walk a route of four legs, moving to the next leg at the edge of the range.
            // The legs are the game's own direction lists; 25 and 26 really are the same route.
            string[] routes =
            {
                "URLD", "RLDU", "DURL", "LDUR", "LRDU", "LRDU", "DULR", "RDUL",
                "LUDR", "UDRL", "RLUD", "DRLU", "RUDL", "UDLR", "LRUD", "DLRU",
                "ULDR", "DRUL", "LDRU", "RULD", "URDL", "DLUR", "LURD", "RDLU",
            };
            v = 21;
            foreach (string route in routes)
                list.Add(new OverworldMovement
                {
                    Value = v++,
                    Name = "Walk " + Spell(route),
                    Kind = MoveKind.Route,
                    Facings = Parse(route),
                });

            list.Add(new OverworldMovement { Value = 45, Name = "Look around, up, down",    Kind = MoveKind.TurnRandom, Facings = Parse("UD") });
            list.Add(new OverworldMovement { Value = 46, Name = "Look around, left, right", Kind = MoveKind.TurnRandom, Facings = Parse("LR") });

            foreach (var (val, name) in new (byte, string)[]
            {
                (BerryPatch, "Berry patch"), (48, "Follow the player"), (49, "Spin, ready for a rematch"),
                (50, "Follow partner trainer"),
                (51, "Hidden in snow"), (52, "Hidden in sand"), (53, "Hidden in rock"), (54, "Hidden in grass"),
            })
                list.Add(new OverworldMovement { Value = val, Name = name, Kind = MoveKind.Special });

            return list.ToArray();
        }

        private static IEnumerable<OverworldMovement> HgssExtras() => new[]
        {
            // The walking Pokémon is switched to these after a warp or an item use, so it keeps up at once.
            new OverworldMovement { Value = 55, Name = "Follow the player closely",                   Kind = MoveKind.Special },
            new OverworldMovement { Value = 56, Name = "Follow the player closely, copying its moves", Kind = MoveKind.Special },
        };

        // From the handlers in pokeplatinum src/unk_02069BE0.c and src/unk_0206450C.c; the decomp leaves them unnamed.
        private static IEnumerable<OverworldMovement> PlatinumExtras()
        {
            // 55-58 share one handler; 59-62 also refuse any step out of very tall grass.
            for (int v = 55; v <= 58; v++)
                yield return new OverworldMovement { Value = (byte)v, Name = "Copy the player's steps", Kind = MoveKind.Special };
            for (int v = 59; v <= 62; v++)
                yield return new OverworldMovement { Value = (byte)v, Name = "Copy the player's steps in tall grass", Kind = MoveKind.Special };

            yield return new OverworldMovement { Value = 63, Name = "Follow a wall on the left",  Kind = MoveKind.Special };
            yield return new OverworldMovement { Value = 64, Name = "Follow a wall on the right", Kind = MoveKind.Special };
            yield return new OverworldMovement { Value = 65, Name = "Follow a wall on the left, turning back at the range edge",  Kind = MoveKind.Special };
            yield return new OverworldMovement { Value = 66, Name = "Follow a wall on the right, turning back at the range edge", Kind = MoveKind.Special };

            // Only the range stops it, so it walks through walls and other objects.
            yield return new OverworldMovement { Value = 67, Name = "Walk left and right through walls",
                Kind = MoveKind.Wander, Facings = Parse("LR") };
        }

        /// <summary>The movement types this game family defines, in value order.</summary>
        public static IReadOnlyList<OverworldMovement> For(RomInfo.GameFamilies family)
        {
            switch (family)
            {
                case RomInfo.GameFamilies.DP: return DpTable;
                case RomInfo.GameFamilies.Plat: return PtTable;
                case RomInfo.GameFamilies.HGSS: return HgssTable;
                default: return Common.Where(m => m.Value != BerryPatch).ToArray();
            }
        }

        public static OverworldMovement Find(RomInfo.GameFamilies family, int value) =>
            For(family).FirstOrDefault(m => m.Value == value);

        /// <summary>A type every game gives the same meaning (0-46 and 48-54), or null.</summary>
        public static OverworldMovement Find(int value) =>
            value == BerryPatch ? null : Common.FirstOrDefault(m => m.Value == value);

        /// <summary>False for a value this game has no handler for, so the editor keeps it as read.</summary>
        public static bool IsDefined(RomInfo.GameFamilies family, int value) => Find(family, value) != null;
    }
}
