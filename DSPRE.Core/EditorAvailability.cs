using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using static DSPRE.RomInfo;

namespace DSPRE
{
    /// <summary>
    /// Whether an editor can open on the ROM that is loaded, and why not. The menus grey items out with it and the
    /// launcher refuses with the same reason, so the two can't disagree. Editors are keyed by their window class.
    /// </summary>
    public static class EditorAvailability
    {
        /// <summary>What an hg-engine ROM does to an editor.</summary>
        public enum HgEngine
        {
            /// <summary>No effect.</summary>
            Open,
            /// <summary>Closed until the checkout is linked: hg-engine changes formats the vanilla readers assume.</summary>
            NeedsLink,
            /// <summary>Closed until the checkout is linked, then edits hg-engine's source for this data.</summary>
            Source,
            /// <summary>Closed: hg-engine builds this data itself and overwrites it.</summary>
            Closed,
            /// <summary>Only a linked hg-engine checkout has it, whatever the ROM.</summary>
            LinkedOnly,
        }

        private sealed class Rule
        {
            public string Title;
            public HgEngine Hge;
            public bool NeedsRom = true;
            public string Beta;
            public Func<string> Unsupported;
            /// <summary>Why a Closed editor stays closed on hg-engine, when the general reason isn't it.</summary>
            public string HgeReason;
            public DirNames[] BuiltArchives = Array.Empty<DirNames>();
        }

        private const string LinkHint = "File > Open hg-engine folder";

        private static Func<string> Unless(Func<bool> supported, string why) => () => supported() ? null : why;
        private static bool Family(params GameFamilies[] families) => Array.IndexOf(families, GameFamily) >= 0;
        private static string NoSpot(RomLocator.Thing table) =>
            RomLocator.Where(table) == null ? "Works on " + DescribeDumps(RomLocator.WorksOnAll(table)) + "." : null;

        private static readonly Dictionary<string, Rule> Rules = new(StringComparer.OrdinalIgnoreCase)
        {
            ["PokemonEditorView"] = new Rule { Title = "The Pokémon editor", Hge = HgEngine.Source },
            ["MoveDataEditorView"] = new Rule { Title = "The Move data editor", Hge = HgEngine.Source },
            ["ItemEditorView"] = new Rule { Title = "The Item editor", Hge = HgEngine.Source },
            ["WildEditorView"] = new Rule { Title = "The Wild Pokémon editor", Hge = HgEngine.Source },
            ["TrainerEditorView"] = new Rule { Title = "The Trainer editor", Hge = HgEngine.Source },
            ["TrainerSpriteEditorView"] = new Rule { Title = "The Trainer sprite editor", Hge = HgEngine.Source },
            ["TrainerBackSpriteEditor"] = new Rule { Title = "The Trainer back sprite editor", Hge = HgEngine.Source },

            ["BattleMessageEditorView"] = new Rule { Title = "Trainer battle messages", Hge = HgEngine.Source },

            ["EggMoveEditorView"] = new Rule { Title = "The Egg move editor", Hge = HgEngine.Source },
            ["BulkLearnsetEditorView"] = new Rule { Title = "The Bulk learnset editor", Hge = HgEngine.Source },
            ["TmHmBulkEditorView"] = new Rule { Title = "The TM/HM bulk editor", Hge = HgEngine.Source },
            ["TrainerFlagBulkEditorView"] = new Rule { Title = "The Trainer flag bulk editor", Hge = HgEngine.Source },
            ["StarterEditorView"] = new Rule
            {
                Title = "The Starter Pokémon editor", Hge = HgEngine.Source,
                Unsupported = Unless(IsStarterEditorAvailable, "The Starter Pokémon editor doesn't support this ROM."),
            },
            ["MartEditorView"] = new Rule
            {
                Title = "The Mart editor", Hge = HgEngine.Source,
                Unsupported = Unless(IsMartEditorAvailable, "The Mart editor doesn't support this ROM."),
            },
            ["WildHeldItemOddsView"] = new Rule { Title = "The Wild held items editor", Hge = HgEngine.Open, Unsupported = () => NoSpot(RomLocator.Thing.WildHeldItemOdds) },
            ["GrowthCurveEditorView"] = new Rule { Title = "The Growth curve editor", Hge = HgEngine.Open },
            ["PokedexListsView"] = new Rule { Title = "The Pokédex lists editor", Hge = HgEngine.Closed },
            ["FriendshipChangesView"] = new Rule { Title = "The Friendship changes editor", Hge = HgEngine.Open, Unsupported = () => RomLocator.Where(RomLocator.Thing.FriendshipChanges) == null ? RomLocator.WhyNot(RomLocator.Thing.FriendshipChanges, 0) : null },
            ["EncounterSlotOddsView"] = new Rule
            {
                Title = "The Encounter slot odds editor", Hge = HgEngine.Source,
            },
            ["BreedingItemsView"] = new Rule { Title = "The Breeding items editor", Hge = HgEngine.Open, Unsupported = () => NoSpot(RomLocator.Thing.IncenseBabies) },
            ["BerryDataEditorView"] = new Rule { Title = "The Berry data editor", Hge = HgEngine.Open },
            ["TypeChartEditorView"] = new Rule { Title = "The Type chart editor", Hge = HgEngine.Source },
            ["MoveTutorEditorView"] = new Rule
            {
                Title = "The Move tutor editor", Hge = HgEngine.Source,
                Unsupported = () => Unless(() => Family(GameFamilies.Plat, GameFamilies.HGSS), "Diamond and Pearl have no move tutors.")() ?? NoSpot(RomLocator.Thing.TutorPool),
            },
            ["BpShopEditorView"] = new Rule
            {
                Title = "The Battle Point shop editor", Hge = HgEngine.Closed,
                Unsupported = Unless(() => Family(GameFamilies.DP, GameFamilies.Plat), "The Battle Point shop editor is for Diamond, Pearl and Platinum."),
            },
            ["UndergroundMiningView"] = new Rule
            {
                Title = "The Underground mining editor", Hge = HgEngine.Closed,
                Unsupported = () => Unless(() => Family(GameFamilies.DP, GameFamilies.Plat), "Only Diamond, Pearl and Platinum have the Underground.")() ?? NoSpot(RomLocator.Thing.MiningTreasures),
            },
            ["VsIntroEditorView"] = new Rule { Title = "The VS intro editor", Hge = HgEngine.Source },
            ["WildIntroEditorView"] = new Rule { Title = "The Wild Pokémon intro editor", Hge = HgEngine.Source },

            ["AudioEditorView"] = new Rule { Title = "The Audio editor", Hge = HgEngine.NeedsLink },
            ["TMEditorView"] = new Rule { Title = "The TM/HM editor", Hge = HgEngine.NeedsLink },
            ["BattleScriptEditorView"] = new Rule
            {
                Title = "The Move animations & battle scripts editor", Hge = HgEngine.NeedsLink, BuiltArchives = new[] { DirNames.WazaParticle },
                Unsupported = Unless(() => Family(GameFamilies.Plat, GameFamilies.HGSS), "The Move animations & battle scripts editor supports Platinum, HeartGold and SoulSilver."),
            },
            ["ItemTableEditorView"] = new Rule
            {
                Title = "Item tables", Hge = HgEngine.NeedsLink,
                Unsupported = Unless(IsItemTableEditorAvailable, "The Item tables editor doesn't support this ROM."),
            },
            ["TradeEditorView"] = new Rule { Title = "The Trade editor", Hge = HgEngine.NeedsLink },
            ["TableEditorView"] = new Rule
            {
                Title = "Music & battle tables", Hge = HgEngine.NeedsLink,
                Unsupported = Unless(() => GameFamily != GameFamilies.DP || BattleMusicTables.IsSupported, "Music & battle tables doesn't support this Diamond or Pearl ROM."),
            },
            ["BattleTowerEditorView"] = new Rule
            {
                Title = "The Battle Tower editor", Hge = HgEngine.NeedsLink,
                Unsupported = Unless(() => BattleTowerTrainerFile.IsAvailable() && BattleTowerPokemonSetFile.IsAvailable(), "Battle Tower data wasn't found for this game."),
            },
            ["CameraEditorView"] = new Rule { Title = "The Camera editor", Hge = HgEngine.NeedsLink },
            ["VsSeekerRematchView"] = new Rule
            {
                Title = "The Vs. Seeker rematch editor", Hge = HgEngine.NeedsLink,
                Unsupported = Unless(() => VsSeekerRematchTable.IsSupported, "The Vs. Seeker rematch editor only supports Diamond, Pearl and Platinum."),
            },
            ["PokegearRematchView"] = new Rule
            {
                Title = "The Pokégear rematch editor", Hge = HgEngine.NeedsLink,
                Unsupported = Unless(() => PokegearRematchTable.IsSupported, "The Pokégear rematch editor only supports HeartGold and SoulSilver."),
            },
            ["PokegearPhoneBookView"] = new Rule
            {
                Title = "The Pokégear phone book", Hge = HgEngine.NeedsLink,
                Unsupported = Unless(() => PokegearPhoneBook.IsSupported, "The Pokégear phone book only exists in HeartGold and SoulSilver."),
            },
            ["FlyEditorView"] = new Rule { Title = "The Fly / warp editor", Hge = HgEngine.NeedsLink },
            ["DungeonCutinEditorView"] = new Rule
            {
                Title = "The Dungeon cut-in editor", Hge = HgEngine.NeedsLink,
                Unsupported = Unless(IsDungeonCutinEditorAvailable, "The Dungeon cut-in editor is for HeartGold and SoulSilver."),
            },
            ["TitleScreenEditorView"] = new Rule
            {
                Title = "The Title screen editor", Hge = HgEngine.NeedsLink,
                Unsupported = Unless(IsTitleScreenEditorAvailable, "The Title screen editor is for HeartGold and SoulSilver."),
            },
            ["BottomScreenEditorView"] = new Rule
            {
                Title = "Bottom screen", Hge = HgEngine.NeedsLink,
                Unsupported = Unless(IsBottomScreenEditorAvailable, "The Bottom screen editor isn't available for this ROM."),
            },
            ["TrainerCardEditorView"] = new Rule
            {
                Title = "The Trainer card editor", Hge = HgEngine.NeedsLink,
                Unsupported = Unless(IsTrainerCardEditorAvailable, "The Trainer card editor isn't available for this ROM."),
            },
            ["CellAnimationEditorView"] = new Rule { Title = "Cell animations", Hge = HgEngine.NeedsLink },
            ["PokedexGraphicsEditorView"] = new Rule { Title = "Pokédex graphics", Hge = HgEngine.NeedsLink },
            ["ScreensEditorView"] = new Rule
            {
                Title = "Screens", Hge = HgEngine.NeedsLink,
                Unsupported = Unless(() => (RomInfo.GameFamily == RomInfo.GameFamilies.Plat || RomInfo.GameFamily == RomInfo.GameFamilies.HGSS)
                                           && RomInfo.GameLanguage != RomInfo.GameLanguages.Japanese,
                                     "The Screens editor works on Platinum, HeartGold and SoulSilver, except Japanese."),
            },
            ["SummaryScreenEditorView"] = new Rule
            {
                Title = "Summary screen", Hge = HgEngine.NeedsLink,
                Unsupported = Unless(() => ROMFiles.SummaryScreenLayout.SupportsCurrentRom,
                                     "The Summary screen editor works on English Platinum, HeartGold and SoulSilver."),
            },
            ["NamingScreenEditor"] = new Rule { Title = "The Naming screen editor", Hge = HgEngine.NeedsLink, Beta = "TrainerSpriteEditorView" },
            ["ParticleEditorView"] = new Rule { Title = "The Particle editor", Hge = HgEngine.NeedsLink, BuiltArchives = new[] { DirNames.WazaParticle } },
            ["ParticleLibraryView"] = new Rule { Title = "Particles", Hge = HgEngine.NeedsLink },
            ["BallCapsuleEditorView"] = new Rule { Title = "Ball capsules", Hge = HgEngine.NeedsLink },
            ["BannerEditorView"] = new Rule { Title = "Game icon & banner", Hge = HgEngine.NeedsLink },
            ["SpawnEditorView"] = new Rule { Title = "The Spawn point editor", Hge = HgEngine.NeedsLink },
            ["OverlayEditorView"] = new Rule { Title = "The Overlay editor", Hge = HgEngine.NeedsLink },
            ["BtxEditorView"] = new Rule { Title = "The Overworld editor", Hge = HgEngine.NeedsLink },
            ["ProjectChecksView"] = new Rule { Title = "Validation and where used", Hge = HgEngine.NeedsLink },
            ["PatchToolboxView"] = new Rule { Title = "The ROM patch toolbox", Hge = HgEngine.NeedsLink },
            ["DataExports"] = new Rule { Title = "Data exports", Hge = HgEngine.NeedsLink },
            ["ExportDocs"] = new Rule { Title = "Export docs", Hge = HgEngine.NeedsLink },
            ["TrainerUsageReport"] = new Rule { Title = "The trainer usage report", Hge = HgEngine.NeedsLink },
            // On an hg-engine ROM only its Headbutt tab opens, and only once the checkout is linked.
            ["SpecialEncountersEditorView"] = new Rule { Title = "The Special encounters editor", Hge = HgEngine.NeedsLink },
            ["BattleScreenEditorView"] = new Rule
            {
                Title = "Battle screen", Hge = HgEngine.NeedsLink,
                BuiltArchives = new[] { DirNames.BattleObj, DirNames.WindowFrames, DirNames.Fonts },
            },
            ["HpBarLayoutEditorView"] = new Rule
            {
                Title = "The HP bar layout editor", Hge = HgEngine.NeedsLink,
                BuiltArchives = new[] { DirNames.BattleObj, DirNames.Fonts },
                Unsupported = Unless(() => ROMFiles.BattleGaugePieces.Locate()?.HasSlots == true,
                                     "The HP bar tables weren't found in this ROM's battle code."),
            },

            // These open without a ROM, so only the hg-engine and beta gates apply.
            ["GraphicsBrowserView"] = new Rule { Title = "All graphics", Hge = HgEngine.NeedsLink, NeedsRom = false },
            ["ModelBrowserView"] = new Rule { Title = "All models and textures", Hge = HgEngine.NeedsLink, NeedsRom = false },
            ["TilesetBuilderView"] = new Rule { Title = "Picture to background", Hge = HgEngine.NeedsLink, NeedsRom = false },
            ["FontEditorView"] = new Rule { Title = "The Font editor", Hge = HgEngine.NeedsLink, NeedsRom = false, BuiltArchives = new[] { DirNames.Fonts } },
            ["BattleSceneBrowserView"] = new Rule { Title = "The battle scenes list", Hge = HgEngine.NeedsLink, NeedsRom = false },

            ["HgEngineFormEditorView"] = new Rule { Title = "The Form editor", Hge = HgEngine.LinkedOnly },
            ["AbilityFlagsEditorView"] = new Rule { Title = "Ability flags", Hge = HgEngine.LinkedOnly },
            ["MoveBackgroundEditorView"] = new Rule { Title = "Move backgrounds", Hge = HgEngine.Source },
            ["HgEngineSettingsView"] = new Rule { Title = "hg-engine settings", Hge = HgEngine.LinkedOnly },
            ["HgEnginePatchesView"] = new Rule { Title = "hg-engine patches", Hge = HgEngine.LinkedOnly },
            ["BattleTestsView"] = new Rule { Title = "Battle tests", Hge = HgEngine.LinkedOnly },
            ["HgeRomReviewView"] = new Rule
            {
                Title = "hg-engine ROM review",
                Unsupported = Unless(() => IsHGE, "This review reads hg-engine's own archives, and this ROM isn't an hg-engine build."),
            },
            ["DistortionWorldView"] = new Rule
            {
                Title = "The Distortion World editor",
                Unsupported = Unless(() => GameFamily == GameFamilies.Plat, "The Distortion World only exists in Platinum."),
            },
        };

        public const string NoRom = "Open a ROM first.";

        public static bool RomOpen => GameFamily != GameFamilies.NULL;

        /// <summary>Why the editor can't open now, or null when it can. Editors with no rule only need an open ROM.</summary>
        public static string WhyNot(string editor)
        {
            Rules.TryGetValue(editor, out Rule rule);
            if ((rule?.NeedsRom ?? true) && !RomOpen) return NoRom;
            if (rule == null) return BetaEditors.WhyNot(editor);

            string title = rule.Title;
            switch (rule.Hge)
            {
                case HgEngine.NeedsLink when IsHGE && !HgEngineProject.IsActive:
                    return $"An hg-engine ROM opened on its own can't use {title}. Open its hg-engine folder instead ({LinkHint}).";
                case HgEngine.Source when IsHGE && !HgEngineProject.IsActive:
                    return $"{title} is disabled for hg-engine ROMs: hg-engine manages this data itself and would overwrite any changes " +
                        $"made here on its next build. Open its hg-engine folder ({LinkHint}) to edit it from source instead.";
                case HgEngine.Closed when IsHGE:
                    return rule.HgeReason != null ? $"{title} is disabled for hg-engine ROMs: {rule.HgeReason}"
                        : $"{title} is disabled for hg-engine ROMs: hg-engine manages this data itself and would overwrite any changes made here on its next build.";
                case HgEngine.LinkedOnly when !HgEngineProject.IsActive:
                    return $"{title} needs an hg-engine folder project ({LinkHint}).";
            }

            if (rule.Unsupported?.Invoke() is string unsupported) return unsupported;
            if (BetaEditors.WhyNot(rule.Beta ?? editor) is string beta) return beta;
            foreach (DirNames dir in rule.BuiltArchives)
                if (HgEngineOwnedFiles.RefusalFor(dir) is string built) return $"{title} isn't available for this project. {built}";
            return null;
        }

        public static bool Allows(string editor) => WhyNot(editor) == null;

        /// <summary>The editor's name for a message title.</summary>
        public static string TitleOf(string editor) =>
            !Rules.TryGetValue(editor, out Rule rule) ? BetaEditors.Named(editor)
            : rule.Title.StartsWith("The ", StringComparison.Ordinal) ? rule.Title.Substring(4) : rule.Title;
    }
}
