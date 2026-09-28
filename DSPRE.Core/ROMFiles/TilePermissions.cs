using System;
using System.Collections.Generic;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>One tile behaviour (the low byte of a land data permission) as a game defines it.</summary>
    public sealed class TileBehaviour
    {
        public TileBehaviour(byte value, string key, string name, bool surf)
        {
            Value = value; Key = key; Name = name; Surf = surf;
        }

        public byte Value { get; }

        /// <summary>Stable id of the meaning, shared by every game that has it, whatever its value there.</summary>
        public string Key { get; }

        public string Name { get; }

        /// <summary>Open water you surf on; walking onto it is not possible.</summary>
        public bool Surf { get; }

        public string Label => TilePermissions.LabelOf(Value, Name);
    }

    /// <summary>A collision byte (the high byte of a land data permission) offered by a painter.</summary>
    public sealed class TileCollision
    {
        public TileCollision(byte value, string name) { Value = value; Name = name; }
        public byte Value { get; }
        public string Name { get; }
        public string Label => TilePermissions.LabelOf(Value, Name);
    }

    /// <summary>
    /// Per game names for land data permissions. Each tile is a u16: the low byte is the tile behaviour, the
    /// high byte's top bit closes the tile off. Diamond, Pearl and Platinum read nothing else from the high byte;
    /// HeartGold and SoulSilver read its low 7 bits as the footstep sound (16 sounds, 14 and 15 unused).
    /// </summary>
    public static class TilePermissions
    {
        public const byte BlockedBit = 0x80;
        public const byte FootstepMask = 0x7F;

        private const int FootstepSounds = 16;

        public static string LabelOf(byte value, string name) => $"[{value:X2}] {name}";

        public static string Unknown(byte value) => $"Unknown (0x{value:X2})";

        private static GameFamilies Family(GameFamilies family) => family == GameFamilies.NULL ? GameFamilies.Plat : family;

        #region Behaviours

        private static readonly (byte value, string key, string name)[] DiamondPearl =
        {
            (0x00, "ground", "Ground"),
            (0x02, "tall_grass", "Tall grass"),
            (0x03, "very_tall_grass", "Very tall grass (no bike)"),
            (0x08, "cave_floor", "Cave floor, wild encounters"),
            (0x0A, "no_bike", "No bike (unused)"),
            (0x0B, "indoor_encounter_floor", "Indoor floor, wild encounters"),
            (0x0C, "rocky_ground", "Rocky ground (no effect)"),
            (0x10, "pond_water", "Pond water"),
            (0x13, "waterfall", "Waterfall"),
            (0x15, "sea_water", "Sea water"),
            (0x16, "puddle", "Puddle"),
            (0x17, "shallow_water", "Shallow water"),
            (0x20, "ice", "Ice"),
            (0x21, "sand", "Sand"),
            (0x30, "block_right", "Blocks moving right"),
            (0x31, "block_left", "Blocks moving left"),
            (0x32, "block_up", "Blocks moving up"),
            (0x33, "block_down", "Blocks moving down"),
            (0x34, "block_right_up", "Blocks moving right and up"),
            (0x35, "block_left_up", "Blocks moving left and up"),
            (0x36, "block_right_down", "Blocks moving right and down"),
            (0x37, "block_left_down", "Blocks moving left and down"),
            (0x38, "jump_right", "Jump right"),
            (0x39, "jump_left", "Jump left"),
            (0x3A, "jump_up", "Jump up (unused)"),
            (0x3B, "jump_down", "Jump down"),
            // 3C-3F are registered but nothing reads them; retail maps still carry some.
            (0x3C, "unread_3c", "No effect (3C)"),
            (0x3D, "unread_3d", "No effect (3D)"),
            (0x3E, "unread_3e", "No effect (3E)"),
            (0x3F, "unread_3f", "No effect (3F)"),
            (0x40, "slide_right", "Slide right"),
            (0x41, "slide_left", "Slide left"),
            (0x42, "slide_up", "Slide up"),
            (0x43, "slide_down", "Slide down"),
            (0x44, "push_right", "Push one step right (unused)"),
            (0x45, "push_left", "Push one step left (unused)"),
            (0x46, "push_up", "Push one step up (unused)"),
            (0x47, "push_down", "Push one step down (unused)"),
            (0x48, "fast_slide", "Fast slide (unused)"),
            (0x49, "block_up_down", "Blocks moving up and down"),
            (0x4A, "block_left_right", "Blocks moving left and right"),
            (0x4B, "rock_climb_vertical", "Rock Climb, up and down"),
            (0x4C, "rock_climb_horizontal", "Rock Climb, left and right"),
            (0x50, "current_right", "Water current right (unused)"),
            (0x51, "current_left", "Water current left (unused)"),
            (0x52, "current_up", "Water current up (unused)"),
            (0x53, "current_down", "Water current down (unused)"),
            (0x54, "unread_54", "No effect (54, unused)"),
            (0x55, "unread_55", "No effect (55, unused)"),
            (0x56, "water_gym_high", "Water gym floor, high"),
            (0x57, "water_gym_middle", "Water gym floor, middle"),
            (0x58, "water_gym_low", "Water gym floor, low"),
            (0x59, "water_gym_water", "Water gym water"),
            (0x5E, "stairs_right", "Stairs warp, right"),
            (0x5F, "stairs_left", "Stairs warp, left"),
            (0x60, "warp_step_down", "Warp, then step down (unused)"),
            (0x61, "warp_keep_facing", "Warp, keep facing (unused)"),
            (0x62, "warp_mat_right", "Warp mat, right"),
            (0x63, "warp_mat_left", "Warp mat, left"),
            (0x64, "warp_mat_up", "Warp mat, up"),
            (0x65, "warp_mat_down", "Warp mat, down"),
            (0x66, "pitfall_warp", "Pitfall warp (unused)"),
            (0x67, "warp_panel", "Warp panel"),
            (0x68, "sand_geyser_warp", "Sand geyser warp (unused)"),
            (0x69, "door", "Door"),
            (0x6A, "escalator_turn", "Escalator, turns you around"),
            (0x6B, "escalator", "Escalator"),
            (0x6C, "warp_right", "Warp right, no arrow"),
            (0x6D, "warp_left", "Warp left, no arrow"),
            (0x6E, "warp_up", "Warp up, no arrow"),
            (0x6F, "warp_down", "Warp down, no arrow"),
            (0x70, "bridge_end", "Bridge start or end"),
            (0x71, "bridge_ground", "Bridge over ground"),
            (0x72, "bridge_encounter", "Bridge over encounter ground"),
            (0x73, "bridge_water", "Bridge over water"),
            (0x74, "bridge_sand", "Bridge over sand"),
            (0x75, "bridge_snow", "Bridge over snow"),
            (0x76, "bike_bridge_v_ground", "Bike bridge up-down, over ground"),
            (0x77, "bike_bridge_v_encounter", "Bike bridge up-down, over encounter ground"),
            (0x78, "bike_bridge_v_water", "Bike bridge up-down, over water"),
            (0x79, "bike_bridge_v_sand", "Bike bridge up-down, over sand"),
            (0x7A, "bike_bridge_h_ground", "Bike bridge left-right, over ground"),
            (0x7B, "bike_bridge_h_encounter", "Bike bridge left-right, over encounter ground"),
            (0x7C, "bike_bridge_h_water", "Bike bridge left-right, over water"),
            (0x7D, "bike_bridge_h_sand", "Bike bridge left-right, over sand"),
            (0x80, "counter", "Counter (talk across)"),
            (0x83, "pc", "PC"),
            (0x84, "sign", "Sign (unused)"),
            (0x85, "town_map", "Town map"),
            (0x86, "tv", "TV"),
            (0x87, "sign_2", "Sign 2 (unused)"),
            (0x88, "shelf", "Shelf (no effect)"),
            (0x89, "slot_machine", "Slot machine (unused)"),
            (0x8A, "roulette", "Roulette (unused)"),
            (0x8B, "furniture", "Furniture (unused)"),
            (0x8C, "furniture_2", "Furniture 2 (unused)"),
            (0x8D, "fake_door", "Fake door (unused)"),
            (0x8E, "notebook", "Notebook (no effect)"),
            (0x8F, "survey", "Survey (no effect)"),
            (0x90, "secret_power", "Secret Power spot (unused)"),
            (0x91, "secret_base", "Secret base entrance (unused)"),
            (0x92, "secret_power_2", "Secret Power spot 2 (unused)"),
            (0xA0, "berry_soil", "Berry soil"),
            (0xA1, "snow", "Snow"),
            (0xA2, "deep_snow", "Deep snow"),
            (0xA3, "very_deep_snow", "Very deep snow"),
            (0xA4, "mud", "Mud"),
            (0xA5, "deep_mud", "Deep mud"),
            (0xA6, "mud_grass", "Mud with grass"),
            (0xA7, "deep_mud_grass", "Deep mud with grass"),
            (0xA8, "light_snow", "Light snow"),
            (0xD0, "cycling_road", "Cycling Road (unused)"),
            (0xD1, "cycling_road_2", "Cycling Road 2 (unused)"),
            (0xD7, "bike_ramp_right", "Bike ramp, right"),
            (0xD8, "bike_ramp_left", "Bike ramp, left"),
            (0xD9, "sand_slope_top", "Sand slope, top (fast bike)"),
            (0xDA, "sand_slope_bottom", "Sand slope, bottom (fast bike)"),
            (0xDB, "bike_stopper", "Bike stopper"),
            (0xE0, "small_bookshelf", "Small bookshelf"),
            (0xE1, "bookshelf", "Bookshelf"),
            (0xE2, "bookshelf_2", "Bookshelf 2"),
            (0xE3, "pot", "Pot (unused)"),
            (0xE4, "trash_can", "Trash can"),
            (0xE5, "shop_shelf", "Shop shelf"),
            (0xE6, "blueprint", "Blueprint (unused)"),
            (0xEA, "small_bookshelf_2", "Small bookshelf 2"),
            (0xEB, "shop_shelf_2", "Shop shelf 2"),
            (0xEC, "shop_shelf_3", "Shop shelf 3"),
            (0xFF, "none", "None"),
        };

        private static readonly (byte value, string key, string name)[] PlatinumAdds =
        {
            (0x1D, "puddle_still", "Puddle, no splash"),
            (0x2C, "mirror_floor", "Reflective floor"),
            (0x2D, "no_explorer_kit", "No Explorer Kit"),
            (0x5A, "jump_2_up", "Jump 2 tiles up"),
            (0x5B, "jump_2_down", "Jump 2 tiles down"),
            (0x5C, "jump_2_left", "Jump 2 tiles left"),
            (0x5D, "jump_2_right", "Jump 2 tiles right"),
            (0xA9, "shadow_snow", "Snow with shadows"),
        };

        private static readonly (byte value, string key, string name)[] HeartGoldSoulSilver =
        {
            (0x00, "ground", "Ground"),
            (0x02, "tall_grass", "Tall grass"),
            (0x03, "very_tall_grass", "Very tall grass (no bike)"),
            (0x06, "headbutt_tree", "Headbutt tree"),
            (0x08, "cave_floor", "Cave floor, wild encounters"),
            (0x0A, "no_bike", "No bike (unused)"),
            (0x0B, "indoor_encounter_floor", "Indoor floor, wild encounters"),
            (0x10, "pond_water", "Pond water"),
            (0x11, "whirlpool", "Whirlpool"),
            (0x13, "waterfall", "Waterfall"),
            (0x15, "sea_water", "Sea water"),
            (0x16, "puddle", "Puddle"),
            (0x17, "shallow_water", "Shallow water"),
            (0x1D, "puddle_still", "Puddle, no splash (unused)"),
            (0x20, "ice", "Ice"),
            (0x21, "sand", "Sand"),
            (0x22, "behind_waterfall", "Behind a waterfall"),
            (0x23, "safari_object", "Safari Zone object"),
            (0x24, "safari_no_objects", "Safari Zone, no objects"),
            (0x2C, "magma", "Magma"),
            (0x2D, "mirror_floor", "Reflective floor"),
            (0x2E, "no_follower_bubble", "No follower bubble"),
            (0x30, "block_right", "Blocks moving right"),
            (0x31, "block_left", "Blocks moving left"),
            (0x32, "block_up", "Blocks moving up"),
            (0x33, "block_down", "Blocks moving down"),
            (0x34, "block_right_up", "Blocks moving right and up"),
            (0x35, "block_left_up", "Blocks moving left and up"),
            (0x36, "block_right_down", "Blocks moving right and down"),
            (0x37, "block_left_down", "Blocks moving left and down"),
            (0x38, "jump_right", "Jump right"),
            (0x39, "jump_left", "Jump left"),
            (0x3A, "jump_up", "Jump up (unused)"),
            (0x3B, "jump_down", "Jump down"),
            (0x3C, "ladder_up", "Ladder up (walk up)"),
            (0x3D, "ladder_up_back", "Ladder up (walk down)"),
            (0x3E, "ladder_down", "Ladder down"),
            (0x40, "slide_right", "Slide right"),
            (0x41, "slide_left", "Slide left"),
            (0x42, "slide_up", "Slide up"),
            (0x43, "slide_down", "Slide down"),
            (0x49, "block_up_down", "Blocks moving up and down"),
            (0x4A, "block_left_right", "Blocks moving left and right"),
            (0x4B, "rock_climb_vertical", "Rock Climb, up and down"),
            (0x4C, "rock_climb_horizontal", "Rock Climb, left and right"),
            (0x4D, "stop_sliding", "Stop sliding"),
            (0x5E, "stairs_right", "Stairs warp, right"),
            (0x5F, "stairs_left", "Stairs warp, left"),
            (0x62, "warp_mat_right", "Warp mat, right"),
            (0x63, "warp_mat_left", "Warp mat, left"),
            (0x64, "warp_mat_up", "Warp mat, up"),
            (0x65, "warp_mat_down", "Warp mat, down"),
            (0x67, "warp_panel", "Warp panel"),
            (0x69, "door", "Door"),
            (0x6A, "escalator_turn", "Escalator, turns you around"),
            (0x6B, "escalator", "Escalator"),
            (0x6C, "warp_right", "Warp right, no arrow"),
            (0x6D, "warp_left", "Warp left, no arrow"),
            (0x6E, "warp_up", "Warp up, no arrow"),
            (0x6F, "warp_down", "Warp down, no arrow"),
            (0x70, "bridge_end", "Bridge start or end"),
            (0x71, "bridge_ground", "Bridge over ground"),
            (0x72, "bridge_encounter", "Bridge over encounter ground"),
            (0x73, "bridge_water", "Bridge over water"),
            (0x80, "counter", "Counter (talk across)"),
            (0x83, "pc", "PC"),
            (0x84, "sign", "Sign (unused)"),
            (0x85, "town_map", "Town map"),
            (0x86, "tv", "TV"),
            (0x87, "sign_2", "Sign 2 (unused)"),
            (0x88, "shelf", "Shelf (unused)"),
            (0x89, "slot_machine", "Slot machine (unused)"),
            (0x8A, "roulette", "Roulette (unused)"),
            (0x8B, "furniture", "Furniture (unused)"),
            (0x8C, "furniture_2", "Furniture 2 (unused)"),
            (0x8D, "fake_door", "Fake door (unused)"),
            (0x8E, "notebook", "Notebook (unused)"),
            (0x8F, "survey", "Survey (unused)"),
            (0xA4, "mud", "Mud"),
            (0xA8, "light_snow", "Light snow"),
            (0xA9, "shadow_snow", "Snow with shadows"),
            (0xD1, "cycling_road_2", "Cycling Road 2 (unused)"),
            (0xE0, "small_bookshelf", "Small bookshelf"),
            (0xE1, "bookshelf", "Bookshelf"),
            (0xE2, "bookshelf_2", "Bookshelf 2"),
            (0xE3, "pot", "Pot (unused)"),
            (0xE4, "trash_can", "Trash can"),
            (0xE5, "shop_shelf", "Shop shelf"),
            (0xE6, "blueprint", "Blueprint (unused)"),
            (0xEA, "small_bookshelf_2", "Small bookshelf 2"),
            (0xEB, "shop_shelf_2", "Shop shelf 2"),
            (0xEC, "shop_shelf_3", "Shop shelf 3"),
            (0xFF, "none", "None"),
        };

        // Bridges over water also carry the game's surf flag, but you walk on them, so only open water counts.
        private static readonly HashSet<string> SurfKeys = new HashSet<string>
        {
            "pond_water", "waterfall", "sea_water", "whirlpool",
            "current_right", "current_left", "current_up", "current_down",
        };

        private static readonly Dictionary<GameFamilies, IReadOnlyList<TileBehaviour>> Behaviours = new Dictionary<GameFamilies, IReadOnlyList<TileBehaviour>>
        {
            [GameFamilies.DP] = Build(DiamondPearl),
            [GameFamilies.Plat] = Build(DiamondPearl.Concat(PlatinumAdds)),
            [GameFamilies.HGSS] = Build(HeartGoldSoulSilver),
        };

        private static IReadOnlyList<TileBehaviour> Build(IEnumerable<(byte value, string key, string name)> rows)
            => rows.OrderBy(r => r.value).Select(r => new TileBehaviour(r.value, r.key, r.name, SurfKeys.Contains(r.key))).ToList();

        private static readonly Dictionary<GameFamilies, Dictionary<byte, TileBehaviour>> BehaviourByValue
            = Behaviours.ToDictionary(kv => kv.Key, kv => kv.Value.ToDictionary(b => b.Value));

        /// <summary>Every tile behaviour the game defines, in value order.</summary>
        public static IReadOnlyList<TileBehaviour> BehavioursFor(GameFamilies family)
            => Behaviours.TryGetValue(Family(family), out var list) ? list : Array.Empty<TileBehaviour>();

        public static TileBehaviour FindBehaviour(byte value, GameFamilies family)
            => BehaviourByValue.TryGetValue(Family(family), out var map) && map.TryGetValue(value, out var b) ? b : null;

        public static string BehaviourName(byte value, GameFamilies family) => FindBehaviour(value, family)?.Name ?? Unknown(value);

        public static string BehaviourLabel(byte value, GameFamilies family) => FindBehaviour(value, family)?.Label ?? Unknown(value);

        public static bool IsSurfWater(byte value, GameFamilies family) => FindBehaviour(value, family)?.Surf ?? false;

        #endregion

        #region Collision

        private static readonly string[] FootstepNames =
        {
            null, "running", "leaves", "twigs", "soft grass", "sand", "hard floor", "metal",
            "tall plants", "straw", "rocky cave", "hollow floor", "splashing", "wooden planks",
            "spare 1 (unused)", "spare 2 (unused)",
        };

        private static readonly IReadOnlyList<TileCollision> SimpleCollisions = new[]
        {
            new TileCollision(0x00, "Walkable"),
            new TileCollision(BlockedBit, "Blocked"),
        };

        // Every sound, walkable then blocked: retail maps use blocked tiles with sounds too.
        private static readonly IReadOnlyList<TileCollision> FootstepCollisions =
            Enumerable.Range(0, FootstepNames.Length).Select(i => (byte)i)
                .Concat(Enumerable.Range(0, FootstepNames.Length).Select(i => (byte)(BlockedBit | i)))
                .Select(v => new TileCollision(v, CollisionName(v, GameFamilies.HGSS)))
                .ToList();

        /// <summary>The collision values a painter offers for the game.</summary>
        public static IReadOnlyList<TileCollision> CollisionsFor(GameFamilies family)
            => Family(family) == GameFamilies.HGSS ? FootstepCollisions : SimpleCollisions;

        public static bool HasFootsteps(GameFamilies family) => Family(family) == GameFamilies.HGSS;

        public static bool IsBlocked(byte collision) => (collision & BlockedBit) != 0;

        public static string CollisionName(byte value, GameFamilies family)
        {
            string open = IsBlocked(value) ? "Blocked" : "Walkable";
            int sound = value & FootstepMask;
            if (sound == 0) return open;
            if (!HasFootsteps(family) || sound >= FootstepSounds) return Unknown(value);
            return sound < FootstepNames.Length ? $"{open}, footsteps: {FootstepNames[sound]}" : $"{open}, footsteps: {sound} (unused)";
        }

        public static string CollisionLabel(byte value, GameFamilies family)
        {
            string name = CollisionName(value, family);
            return name.StartsWith("Unknown", StringComparison.Ordinal) ? name : LabelOf(value, name);
        }

        #endregion

        #region Colours

        // Meanings that exist only outside Platinum and whose own value is taken there by another meaning.
        private static readonly Dictionary<string, int> SeedOverrides = new Dictionary<string, int>
        {
            ["magma"] = 0xF0,
        };

        private static readonly Dictionary<string, int> Seeds = BuildSeeds();

        private static Dictionary<string, int> BuildSeeds()
        {
            var seeds = new Dictionary<string, int>();
            foreach (var b in Behaviours[GameFamilies.Plat]) seeds[b.Key] = b.Value;
            foreach (var b in Behaviours[GameFamilies.HGSS])
                if (!seeds.ContainsKey(b.Key)) seeds[b.Key] = SeedOverrides.TryGetValue(b.Key, out int s) ? s : b.Value;
            return seeds;
        }

        /// <summary>
        /// Normalized RGB for a tile. Behaviours take the colour of their meaning, placed at the meaning's Platinum
        /// value, so a meaning looks the same in every game. Unknown values keep a colour of their own value.
        /// </summary>
        public static (float r, float g, float b) Colour(byte value, bool isCollision, GameFamilies family)
        {
            if (isCollision)
            {
                int sound = HasFootsteps(family) ? value & FootstepMask : 0;
                if (value == 0x00) return (60 / 255f, 160 / 255f, 70 / 255f);
                if (value == BlockedBit) return (180 / 255f, 60 / 255f, 60 / 255f);
                if (sound > 0 && sound < FootstepSounds)
                    return IsBlocked(value) ? Hsv((sound * 7) % 30, 0.6, 0.72) : Hsv(95 + (sound * 11) % 60, 0.6, 0.66);
                return Hsv((value * 47) % 360, 0.55, 0.85);
            }
            var behaviour = FindBehaviour(value, family);
            int seed = behaviour != null && Seeds.TryGetValue(behaviour.Key, out int s) ? s : value;
            return Hsv((seed * 47) % 360, 0.55, 0.85);
        }

        private static (float r, float g, float b) Hsv(double h, double s, double v)
        {
            double c = v * s;
            double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
            double m = v - c;
            double r = 0, g = 0, b = 0;
            if (h < 60) { r = c; g = x; }
            else if (h < 120) { r = x; g = c; }
            else if (h < 180) { g = c; b = x; }
            else if (h < 240) { g = x; b = c; }
            else if (h < 300) { r = x; b = c; }
            else { r = c; b = x; }
            return ((byte)((r + m) * 255) / 255f, (byte)((g + m) * 255) / 255f, (byte)((b + m) * 255) / 255f);
        }

        #endregion
    }
}
