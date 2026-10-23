using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE
{
    /// <summary>
    /// Editors still being tried out. They stay hidden unless DSPRE starts with <c>--beta</c>; a debug build
    /// always shows them.
    /// </summary>
    public static class BetaEditors
    {
        private const string Switch = "--beta";

        public static bool Enabled { get; private set; }
#if DEBUG
            = true;
#endif

        /// <summary>A gated editor: its menu label and the menu it sits in.</summary>
        private sealed record Editor(string Name, string Menu);

        // Keyed by window class. Only editors new in 3.0 belong here: gating one that shipped in 2.x takes a
        // working feature away.
        private static readonly Dictionary<string, Editor> Testing = new(StringComparer.OrdinalIgnoreCase)
        {
            ["AbilityFlagsEditorView"] = new("Ability flags (hg-engine)", "Pokémon"),
            ["AudioEditorView"] = new("Audio editor", "Audio"),
            ["BallCapsuleEditorView"] = new("Ball capsules", "Graphics"),
            ["BannerEditorView"] = new("Game icon & banner", "Graphics"),
            ["BattleSceneBrowserView"] = new("Battle scenes", "Graphics"),
            ["BattleScreenEditorView"] = new("Battle screen", "Graphics"),
            ["BattleScriptEditorView"] = new("Move animations & battle scripts", "Pokémon"),
            ["BerryDataEditorView"] = new("Berry data", "Items"),
            ["BottomScreenEditorView"] = new("Bottom screen", "Graphics"),
            ["BpShopEditorView"] = new("Battle Point shop", "Items"),
            ["BreedingItemsView"] = new("Breeding items", "Pokémon"),
            ["CellAnimationEditorView"] = new("Cell animations", "Graphics"),
            ["DistortionWorldView"] = new("Distortion World", "World"),
            ["DungeonCutinEditorView"] = new("Dungeon cut-in editor", "Graphics"),
            ["EncounterSlotOddsView"] = new("Encounter slot odds", "Pokémon"),
            ["FontEditorView"] = new("Font editor", "Text"),
            ["FriendshipChangesView"] = new("Friendship changes", "Pokémon"),
            ["GrowthCurveEditorView"] = new("Growth curves", "Pokémon"),
            ["HgEngineFormEditorView"] = new("Form editor (hg-engine)", "Pokémon"),
            ["HpBarLayoutEditorView"] = new("HP bar layout", "Graphics"),
            ["HgeRomReviewView"] = new("hg-engine ROM review", "File"),
            ["MartEditorView"] = new("Mart editor", "Items"),
            ["MoveBackgroundEditorView"] = new("Move backgrounds", "Pokémon"),
            ["MoveTutorEditorView"] = new("Move tutors", "Pokémon"),
            ["ParticleEditorView"] = new("Particle editor", "Graphics"),
            ["ParticleLibraryView"] = new("Particles", "Graphics"),
            ["PokedexGraphicsEditorView"] = new("Pokédex graphics", "Graphics"),
            ["PokegearPhoneBookView"] = new("Pokégear phone book", "Trainers"),
            ["SummaryScreenEditorView"] = new("Summary screen", "Graphics"),
            ["ScreensEditorView"] = new("Screens", "Graphics"),
            ["ProjectChecksView"] = new("Validation and where used", "Tools"),
            ["TilesetBuilderView"] = new("Picture to background", "Graphics"),
            ["TitleScreenEditorView"] = new("Title screen editor", "Graphics"),
            ["TrainerCardEditorView"] = new("Trainer card editor", "Graphics"),
            ["TypeChartEditorView"] = new("Type chart", "Pokémon"),
            ["UndergroundMiningView"] = new("Underground mining", "Items"),
            ["VsIntroEditorView"] = new("VS intro editor", "Trainers"),
            ["WildHeldItemOddsView"] = new("Wild held items", "Pokémon"),
            ["WildIntroEditorView"] = new("Wild Pokémon intro editor", "Pokémon"),
        };

        /// <summary>Reads the switch off the command line, before any window opens.</summary>
        public static void ReadFrom(IEnumerable<string> args)
        {
#if !DEBUG
            Enabled = args != null && args.Any(a => string.Equals(a, Switch, StringComparison.OrdinalIgnoreCase));
#endif
        }

        // A warning that names a gated editor as the fix would otherwise point at a greyed-out menu, so a fault the
        // open ROM really has lets that one editor through until another ROM is loaded.
        private static readonly HashSet<string> Needed = new(StringComparer.OrdinalIgnoreCase);

        public static void AllowBecauseNeeded(string window)
        {
            if (!string.IsNullOrEmpty(window)) Needed.Add(window);
        }

        public static void ForgetWhatWasNeeded() => Needed.Clear();

        public static bool Allows(string window) =>
            Enabled || string.IsNullOrEmpty(window) || !Testing.ContainsKey(window) || Needed.Contains(window);

        /// <summary>The message for a greyed-out editor, or null when it is allowed.</summary>
        public static string WhyNot(string window) => Allows(window) ? null : $"{Named(window)} isn't available yet.";

        public static string Named(string window) => Testing.TryGetValue(window ?? "", out Editor e) ? e.Name : window;

        public static int Count => Testing.Count;

        /// <summary>An unfinished part of an otherwise finished editor; its controls check <see cref="Enabled"/>.</summary>
        public sealed class BetaFeature
        {
            public string Name { get; init; }
            public string Where { get; init; }
        }

        /// <summary>Listed so the welcome window and the tour can say what the switch turns on.</summary>
        public static IReadOnlyList<BetaFeature> Features { get; } = new List<BetaFeature>
        {
            new() { Name = "Walking the map", Where = "Event editor" },
            new() { Name = "Animated preview", Where = "Event editor and Map editor" },
            new() { Name = "Tile boundary overlay", Where = "Event editor and Map editor" },
            new() { Name = "Bug Contest opponents", Where = "Special encounters editor" },
            new() { Name = "Pokéathlon stats", Where = "Pokémon editor" },
            new() { Name = "Swarms", Where = "Special encounters editor" },
            new() { Name = "Roamers", Where = "Special encounters editor" },
        };

        /// <summary>Gated editors per menu, most first, for the summary line.</summary>
        public static IEnumerable<KeyValuePair<string, int>> CountByArea() =>
            Testing.Values.GroupBy(e => e.Menu)
                   .Select(g => new KeyValuePair<string, int>(g.Key, g.Count()))
                   .OrderByDescending(p => p.Value)
                   .ThenBy(p => p.Key, StringComparer.Ordinal);
    }
}
