using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE
{
    /// <summary>
    /// Which editors are still being tried out. Anything listed here is switched off unless DSPRE is
    /// started with <c>--beta</c>, so a normal run only offers what is settled.
    ///
    /// This is the same shape as the hg-engine gating: the window's own CanUse property asks here as
    /// well as asking whatever else it depends on, and the menu says why it is greyed out.
    /// </summary>
    public static class BetaEditors
    {
        /// <summary>The switch that turns them on.</summary>
        public const string Switch = "--beta";

        /// <summary>
        /// Whether the editors being tried out are available in this run. A debug build always has
        /// them, since that is a build made for working on DSPRE.
        /// </summary>
        public static bool Enabled { get; private set; }
#if DEBUG
            = true;
#endif

        /// <summary>
        /// Every editor still in beta, by the name of its window class, with what to call it in a
        /// message. The window class is the key because there is exactly one per editor and it is easy
        /// to search for; adding or removing a line here is the whole job.
        /// </summary>
        private static readonly Dictionary<string, string> Testing =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // New in 3.0.
                ["BattleScreenEditorView"] = "Battle Screen editor",
                ["FontEditorView"] = "Font editor",
                ["TilesetBuilderView"] = "Picture to Background",
                ["BattleSceneBrowserView"] = "Battle scenes",
                ["BattleDisplayEditorView"] = "Battle Display editor",
                ["BattleMessageEditorView"] = "Battle message editor",
                ["BattleScriptEditorView"] = "Battle script editor",
                ["AudioEditorView"] = "Audio editor",
                ["BannerEditorView"] = "Game icon and banner editor",
                ["TitleScreenEditorView"] = "Title Screen editor",
                ["DungeonCutinEditorView"] = "Dungeon Cut-in editor",
                ["TrainerCardEditorView"] = "Trainer Card editor",
                ["TrainerSpriteEditorView"] = "Trainer Sprite editor",
                ["TrainerClassesView"] = "Trainer Classes",
                ["PaletteColorEditorView"] = "Palette colour editor",
                ["SpriteImportWizardView"] = "Sprite import wizard",
                ["SpriteExportWizardView"] = "Sprite export wizard",
                ["ProjectChecksView"] = "Project checks",
                ["ResearchHelperView"] = "Research helper",
                ["AddressHelperView"] = "Address helper",
                ["HeaderSearchView"] = "Advanced header search",
                ["CharMapManagerView"] = "Char Map manager",
                ["CustomScrcmdManagerView"] = "Custom script command manager",
                ["ScriptCommandGuideView"] = "Script command reference",
                ["PatchToolboxView"] = "ROM patch toolbox",
                ["CompileRomView"] = "Compile ROM",
                ["HgEngineLinkView"] = "hg-engine link",
                ["HgEngineFormEditorView"] = "Form editor",
                ["MonReorderView"] = "Reorder Pokemon",
                ["BulkLearnsetEditorView"] = "Bulk learnset editor",
                ["TmHmBulkEditorView"] = "TM and HM bulk editor",
                ["TrainerFlagBulkEditorView"] = "Trainer flag bulk editor",
                ["GroundItemScriptsView"] = "Ground item scripts",
                ["FlyEditorView"] = "Fly destination editor",
                ["VsSeekerRematchView"] = "Vs. Seeker rematch editor",
                ["TrophyGardenEditorView"] = "Trophy Garden editor",
                ["SafariZoneEncounterView"] = "Safari Zone editor",
                ["BugContestEncounterView"] = "Bug Contest editor",
                ["GreatMarshEncounterView"] = "Great Marsh editor",
                ["HoneyTreeEncounterView"] = "Honey Tree editor",
                ["HeadbuttEncounterView"] = "Headbutt editor",
                ["StarterEditorView"] = "Starter Pokemon editor",
                ["ItemTableEditorView"] = "Item tables",
                ["BattleTowerEditorView"] = "Battle Tower editor",
                ["CameraEditorView"] = "Camera editor",
                ["SpawnEditorView"] = "Spawn Point editor",
                ["OverlayEditorView"] = "Overlay editor",
            };

        /// <summary>Reads the switch off the command line. Call this once, before any window opens.</summary>
        public static void ReadFrom(IEnumerable<string> args)
        {
#if DEBUG
            Enabled = true;
#else
            Enabled = args != null && args.Any(
                a => string.Equals(a, Switch, StringComparison.OrdinalIgnoreCase));
#endif
        }

        /// <summary>Turns them on or off from code. Only for tests and for the settings screen.</summary>
        public static void Set(bool on) => Enabled = on;

        /// <summary>Whether this editor is one of the ones still being tried out.</summary>
        public static bool IsBeta(string window) =>
            !string.IsNullOrEmpty(window) && Testing.ContainsKey(window);

        /// <summary>Whether this editor may be opened at all in this run.</summary>
        public static bool Allows(string window) => Enabled || !IsBeta(window);

        /// <summary>What to say when it is greyed out, or null when it is not.</summary>
        public static string WhyNot(string window)
        {
            if (Allows(window)) return null;
            return $"{Named(window)} is not available yet.";
        }

        /// <summary>What this editor is called in a message.</summary>
        public static string Named(string window) =>
            Testing.TryGetValue(window ?? "", out string name) ? name : window;

        /// <summary>Every editor being tried out, for anything that wants to list them.</summary>
        public static IReadOnlyCollection<string> All => Testing.Keys;

        /// <summary>How many there are, for the line the main window shows when they are switched on.</summary>
        public static int Count => Testing.Count;
    }
}
