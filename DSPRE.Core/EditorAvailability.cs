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
            public DirNames[] BuiltArchives = Array.Empty<DirNames>();
        }

        private const string LinkHint = "File > hg-engine > Link hg-engine checkout";

        private static Func<string> Unless(Func<bool> supported, string why) => () => supported() ? null : why;
        private static bool Family(params GameFamilies[] families) => Array.IndexOf(families, gameFamily) >= 0;

        private static readonly Dictionary<string, Rule> Rules = new(StringComparer.OrdinalIgnoreCase)
        {
            ["PokemonEditorView"] = new Rule { Title = "The Pokémon Editor", Hge = HgEngine.Source },
            ["MoveDataEditorView"] = new Rule { Title = "The Move Data Editor", Hge = HgEngine.Source },
            ["ItemEditorView"] = new Rule { Title = "The Item Editor", Hge = HgEngine.Source },
            ["WildEditorView"] = new Rule { Title = "The Wild Pokémon Editor", Hge = HgEngine.Source },
            ["TrainerEditorView"] = new Rule { Title = "The Trainer Editor", Hge = HgEngine.Source },
            ["TrainerSpriteEditorView"] = new Rule { Title = "The Trainer Sprite Editor", Hge = HgEngine.Source },
            ["TrainerBackSpriteEditor"] = new Rule { Title = "The Trainer Back Sprite Editor", Hge = HgEngine.Source },

            ["BattleMessageEditorView"] = new Rule { Title = "The Battle Message Editor", Hge = HgEngine.Source },

            ["EggMoveEditorView"] = new Rule { Title = "The Egg Move Editor", Hge = HgEngine.Closed },
            ["BulkLearnsetEditorView"] = new Rule { Title = "The Bulk Learnset Editor", Hge = HgEngine.Closed },
            ["TmHmBulkEditorView"] = new Rule { Title = "The TM/HM Bulk Editor", Hge = HgEngine.Closed },
            ["TrainerFlagBulkEditorView"] = new Rule { Title = "The Trainer Flag Bulk Editor", Hge = HgEngine.Closed },
            ["StarterEditorView"] = new Rule
            {
                Title = "The Starter Pokémon Editor", Hge = HgEngine.Closed,
                Unsupported = Unless(IsStarterEditorAvailable, "The Starter Pokémon Editor does not support this ROM."),
            },
            ["MartEditorView"] = new Rule
            {
                Title = "The Mart Editor", Hge = HgEngine.Closed,
                Unsupported = Unless(IsMartEditorAvailable, "The Mart Editor currently supports English Diamond, Pearl, Platinum, HeartGold and SoulSilver ROMs."),
            },
            ["WildHeldItemOddsView"] = new Rule { Title = "The Wild held items editor", Hge = HgEngine.Closed },
            ["GrowthCurveEditorView"] = new Rule { Title = "The Growth curve editor", Hge = HgEngine.Closed },
            ["FriendshipChangesView"] = new Rule { Title = "The Friendship changes editor", Hge = HgEngine.Closed },
            ["EncounterSlotOddsView"] = new Rule { Title = "The Encounter slot odds editor", Hge = HgEngine.Closed },
            ["BreedingItemsView"] = new Rule { Title = "The Breeding items editor", Hge = HgEngine.Closed },
            ["BerryDataEditorView"] = new Rule { Title = "The Berry data editor", Hge = HgEngine.Closed },
            ["TypeChartEditorView"] = new Rule { Title = "The Type chart editor", Hge = HgEngine.Closed },
            ["MoveTutorEditorView"] = new Rule
            {
                Title = "The Move tutor editor", Hge = HgEngine.Closed,
                Unsupported = Unless(() => Family(GameFamilies.Plat, GameFamilies.HGSS), "Diamond and Pearl have no move tutors."),
            },
            ["BpShopEditorView"] = new Rule
            {
                Title = "The Battle Point shop editor", Hge = HgEngine.Closed,
                Unsupported = Unless(() => Family(GameFamilies.DP, GameFamilies.Plat), "The Battle Point shop editor is for Diamond, Pearl and Platinum."),
            },
            ["UndergroundMiningView"] = new Rule
            {
                Title = "The Underground mining editor", Hge = HgEngine.Closed,
                Unsupported = Unless(() => Family(GameFamilies.DP, GameFamilies.Plat), "Only Diamond, Pearl and Platinum have the Underground."),
            },
            ["VsIntroEditorView"] = new Rule { Title = "The VS Intro Editor", Hge = HgEngine.Closed },
            ["WildIntroEditorView"] = new Rule { Title = "The Wild Pokémon Intro Editor", Hge = HgEngine.Closed },

            ["AudioEditorView"] = new Rule { Title = "The Audio Editor", Hge = HgEngine.NeedsLink },
            ["TMEditorView"] = new Rule { Title = "The TM Editor", Hge = HgEngine.NeedsLink },
            ["BattleScriptEditorView"] = new Rule { Title = "The Battle Script Editor", Hge = HgEngine.NeedsLink, BuiltArchives = new[] { DirNames.wazaParticle } },
            ["ItemTableEditorView"] = new Rule
            {
                Title = "The Item Tables editor", Hge = HgEngine.NeedsLink,
                Unsupported = Unless(IsItemTableEditorAvailable, "The Item Tables editor does not support this ROM."),
            },
            ["TradeEditorView"] = new Rule { Title = "The Trade Editor", Hge = HgEngine.NeedsLink },
            ["TableEditorView"] = new Rule
            {
                Title = "Music & Battle Tables", Hge = HgEngine.NeedsLink,
                Unsupported = Unless(() => gameFamily != GameFamilies.DP || BattleMusicTables.IsSupported, "Music & Battle Tables does not support this Diamond or Pearl ROM."),
            },
            ["BattleTowerEditorView"] = new Rule
            {
                Title = "The Battle Tower Editor", Hge = HgEngine.NeedsLink,
                Unsupported = Unless(() => BattleTowerTrainerFile.IsAvailable() && BattleTowerPokemonSetFile.IsAvailable(), "Battle Tower data was not found for this game."),
            },
            ["CameraEditorView"] = new Rule { Title = "The Camera Editor", Hge = HgEngine.NeedsLink },
            ["VsSeekerRematchView"] = new Rule
            {
                Title = "The Vs. Seeker Rematch Editor", Hge = HgEngine.NeedsLink,
                Unsupported = Unless(() => VsSeekerRematchTable.IsSupported, "The Vs. Seeker Rematch Editor only supports Diamond, Pearl and Platinum (English)."),
            },
            ["PokegearRematchView"] = new Rule
            {
                Title = "The Pokégear Rematch Editor", Hge = HgEngine.NeedsLink,
                Unsupported = Unless(() => PokegearRematchTable.IsSupported, "The Pokégear Rematch Editor only supports HeartGold and SoulSilver."),
            },
            ["PokegearPhoneBookView"] = new Rule
            {
                Title = "The Pokégear Phone Book", Hge = HgEngine.NeedsLink,
                Unsupported = Unless(() => PokegearPhoneBook.IsSupported, "The Pokégear Phone Book only exists in HeartGold and SoulSilver."),
            },
            ["FlyEditorView"] = new Rule { Title = "The Fly / Warp Editor", Hge = HgEngine.NeedsLink },
            ["DungeonCutinEditorView"] = new Rule
            {
                Title = "The Dungeon Cut-in Editor", Hge = HgEngine.NeedsLink,
                Unsupported = Unless(IsDungeonCutinEditorAvailable, "The Dungeon Cut-in editor is available for English and Spanish HeartGold and SoulSilver ROMs."),
            },
            ["TitleScreenEditorView"] = new Rule
            {
                Title = "The Title Screen Editor", Hge = HgEngine.NeedsLink,
                Unsupported = Unless(IsTitleScreenEditorAvailable, "The Title Screen editor is available for HeartGold and SoulSilver ROMs."),
            },
            ["BottomScreenEditorView"] = new Rule
            {
                Title = "The Bottom Screen Editor", Hge = HgEngine.NeedsLink,
                Unsupported = Unless(IsBottomScreenEditorAvailable, "The Bottom Screen editor is not available for this ROM."),
            },
            ["TrainerCardEditorView"] = new Rule
            {
                Title = "The Trainer Card Editor", Hge = HgEngine.NeedsLink,
                Unsupported = Unless(IsTrainerCardEditorAvailable, "The Trainer Card editor is not available for this ROM."),
            },
            ["CellAnimationEditorView"] = new Rule { Title = "The Cell Animation Editor", Hge = HgEngine.NeedsLink },
            ["NamingScreenEditor"] = new Rule { Title = "The Naming Screen Editor", Hge = HgEngine.NeedsLink, Beta = "TrainerSpriteEditorView" },
            ["ParticleEditorView"] = new Rule { Title = "The Particle Editor", Hge = HgEngine.NeedsLink },
            ["ParticleLibraryView"] = new Rule { Title = "The Particle Library", Hge = HgEngine.NeedsLink },
            ["BallCapsuleEditorView"] = new Rule { Title = "The Ball Capsule Editor", Hge = HgEngine.NeedsLink },
            ["BannerEditorView"] = new Rule { Title = "The Game Icon & Banner editor", Hge = HgEngine.NeedsLink },
            ["SpawnEditorView"] = new Rule { Title = "The Spawn Point Editor", Hge = HgEngine.NeedsLink },
            ["OverlayEditorView"] = new Rule { Title = "The Overlay Editor", Hge = HgEngine.NeedsLink },
            ["BtxEditorView"] = new Rule { Title = "The Overworld Editor", Hge = HgEngine.NeedsLink },
            ["ProjectChecksView"] = new Rule { Title = "Validation & Where-Used", Hge = HgEngine.NeedsLink },
            ["PatchToolboxView"] = new Rule { Title = "The ROM Patch Toolbox", Hge = HgEngine.NeedsLink },
            ["DataExports"] = new Rule { Title = "Data exports", Hge = HgEngine.NeedsLink },
            ["ExportDocs"] = new Rule { Title = "Export Docs", Hge = HgEngine.NeedsLink },
            ["TrainerUsageReport"] = new Rule { Title = "The trainer usage report", Hge = HgEngine.NeedsLink },
            // On an hg-engine ROM only its Headbutt tab opens, and only once the checkout is linked.
            ["SpecialEncountersEditorView"] = new Rule { Title = "The Special Encounters editor", Hge = HgEngine.NeedsLink },
            ["BattleScreenEditorView"] = new Rule
            {
                Title = "The Battle Screen Editor", Hge = HgEngine.NeedsLink,
                BuiltArchives = new[] { DirNames.battleObj, DirNames.windowFrames, DirNames.fonts },
            },

            // These open without a ROM, so only the hg-engine and beta gates apply.
            ["GraphicsBrowserView"] = new Rule { Title = "The graphics list", Hge = HgEngine.NeedsLink, NeedsRom = false },
            ["ModelBrowserView"] = new Rule { Title = "The models list", Hge = HgEngine.NeedsLink, NeedsRom = false },
            ["TilesetBuilderView"] = new Rule { Title = "Picture to Background", Hge = HgEngine.NeedsLink, NeedsRom = false },
            ["FontEditorView"] = new Rule { Title = "The Font Editor", Hge = HgEngine.NeedsLink, NeedsRom = false, BuiltArchives = new[] { DirNames.fonts } },
            ["BattleSceneBrowserView"] = new Rule { Title = "The battle scenes list", Hge = HgEngine.NeedsLink, NeedsRom = false },

            ["HgEngineFormEditorView"] = new Rule { Title = "The Form Editor", Hge = HgEngine.LinkedOnly },
            ["HgEnginePatchesView"] = new Rule { Title = "hg-engine patches", Hge = HgEngine.LinkedOnly },
            ["HgeRomReviewView"] = new Rule
            {
                Title = "The hg-engine ROM Review",
                Unsupported = Unless(() => isHGE, "This review reads hg-engine's own archives, and this ROM is not an hg-engine build."),
            },
            ["DistortionWorldView"] = new Rule
            {
                Title = "The Distortion World editor",
                Unsupported = Unless(() => gameFamily == GameFamilies.Plat, "The Distortion World only exists in Platinum."),
            },
        };

        public const string NoRom = "Open a ROM first.";

        public static bool RomOpen => gameFamily != GameFamilies.NULL;

        /// <summary>Why the editor can't open now, or null when it can. Editors with no rule only need an open ROM.</summary>
        public static string WhyNot(string editor)
        {
            Rules.TryGetValue(editor, out Rule rule);
            if ((rule?.NeedsRom ?? true) && !RomOpen) return NoRom;
            if (rule == null) return BetaEditors.WhyNot(editor);

            string title = rule.Title;
            switch (rule.Hge)
            {
                case HgEngine.NeedsLink when isHGE && !HgEngineProject.IsActive:
                    return $"{title} is not available for this hg-engine project until its checkout is linked ({LinkHint}).";
                case HgEngine.Source when isHGE && !HgEngineProject.IsActive:
                    return $"{title} is disabled for hg-engine ROMs: hg-engine manages this data itself and would overwrite any changes " +
                        $"made here on its next build. Link your hg-engine checkout ({LinkHint}) to edit it from source instead.";
                case HgEngine.Closed when isHGE:
                    return $"{title} is disabled for hg-engine ROMs: hg-engine manages this data itself and would overwrite any changes made here on its next build.";
                case HgEngine.LinkedOnly when !HgEngineProject.IsActive:
                    return $"{title} needs a linked hg-engine checkout ({LinkHint}).";
            }

            if (rule.Unsupported?.Invoke() is string unsupported) return unsupported;
            if (BetaEditors.WhyNot(rule.Beta ?? editor) is string beta) return beta;
            foreach (DirNames dir in rule.BuiltArchives)
                if (HgEngineOwnedFiles.RefusalFor(dir) is string built) return $"{title} is not available for this project. {built}";
            return null;
        }

        public static bool Allows(string editor) => WhyNot(editor) == null;

        /// <summary>The editor's name for a message title.</summary>
        public static string TitleOf(string editor) =>
            !Rules.TryGetValue(editor, out Rule rule) ? BetaEditors.Named(editor)
            : rule.Title.StartsWith("The ", StringComparison.Ordinal) ? rule.Title.Substring(4) : rule.Title;
    }
}
