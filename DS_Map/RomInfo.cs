using DSPRE.Resources;
using DSPRE.ROMFiles;
using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using static DSPRE.RomInfo;
using Path = System.IO.Path;

namespace DSPRE
{
    /// <summary>
    /// Class to store ROM data from GEN IV Pokémon games
    /// </summary>

    public class RomInfo
    {
        public const string folderSuffix = "_DSPRE_contents"; // changed back to public static string
        private static string dataFolderName;
        private static string customNarcFolderName;

        public static bool IsDsRomProject { get; internal set; }
        public static bool isHGE { get; private set; }

        // UI-agnostic warning surface. The host sets this (WinForms → MessageBox, Avalonia → dialog); the default
        // just logs. Keeps RomInfo free of a direct System.Windows.Forms dependency so Avalonia can own ROM loading.
        public static Action<string, string> ShowWarning = (msg, title) => AppLogger.Error(title + ": " + msg);
        public static bool hasRotomProject { get; private set; }
        public static void RefreshRotomProjectState() => hasRotomProject =
            !string.IsNullOrWhiteSpace(workDir) && File.Exists(Path.Combine(workDir, "rotom.toml"));
        public static string romID { get; private set; }
        public static string projectName { get; private set; }
        public static string workDir { get; private set; }
        public static string arm9Path { get; private set; }
        public static string arm7Path { get; private set; }
        public static string overlayTablePath { get; set; }
        public static string y7Path { get; set; }
        public static string dataPath { get; set; }
        public static string overlayPath { get; set; }
        public static string unpackedPath { get; set; }
        public static string bannerPath { get; set; }
        public static string headerPath { get; set; }

        public static GameLanguages gameLanguage { get; private set; }
        public static GameVersions gameVersion { get; private set; }
        public static GameFamilies gameFamily { get; private set; }

        public static uint synthOverlayLoadAddress = 0x023C8000;
        public static uint arm9spawnOffset { get; private set; }

        public static int initialMoneyOverlayNumber { get; private set; }
        public static uint initialMoneyOverlayOffset { get; private set; }

        public static int cameraTblOverlayNumber { get; private set; }
        public static uint[] cameraTblOffsetsToRAMaddress { get; private set; }

        public static uint headerTableOffset { get; private set; }

        // Pickup Table offsets (in overlay file)
        public static int pickupTableOverlayNumber { get; private set; }
        public static uint pickupCommonItemsOffset { get; private set; }
        public static uint pickupRareItemsOffset { get; private set; }
        public static uint pickupActivationDivisorOffset { get; private set; }
        public static uint pickupWeightTableOffset { get; private set; }

        // Starter Pokémon table. DP/Pt: a fixed overlay offset (species IDs are 4-byte words, held item lives
        // separately in a script file). HGSS: no fixed offset — species are found at runtime via
        // starterArm9SearchSuffix (a byte-pattern search over arm9.bin), so starterOverlayNumber there only
        // covers the starter-cries table (a separate table, still overlay-based on HGSS).
        public static int starterOverlayNumber { get; private set; } = -1;
        public static uint starterSpeciesOffset { get; private set; }
        public static byte[] starterArm9SearchSuffix { get; private set; }
        public static string starterGraphicsPrefix { get; private set; }        // DP/Pt only
        public static string starterGraphicsPrefixInner { get; private set; }   // DP/Pt only
        public static string starterCriesPrefix { get; private set; }           // HGSS only
        public static int starterHeldItemScriptFileID { get; private set; } = -1; // DP/Pt only
        public static uint starterHeldItemOffset { get; private set; }            // DP/Pt only
        public static int starterScreenTextNumber { get; private set; } = -1;
        public static int starterPokedexSpeciesTextNumber { get; private set; } = -1; // DP/Pt only

        // Item Table offset (in ARM9)
        public static uint itemTableOffset { get; private set; }

        public static uint conditionalMusicTableOffsetToRAMAddress { get; internal set; }
        public static uint encounterMusicTableOffsetToRAMAddress { get; internal set; }
        public static uint dungeonCutinTableOffsetToRAMAddress { get; internal set; }

        public static uint vsTrainerEntryTableOffsetToRAMAddress { get; internal set; }
        public static uint vsPokemonEntryTableOffsetToRAMAddress { get; internal set; }
        public static uint effectsComboTableOffsetToRAMAddress { get; internal set; }

        public static uint vsTrainerEntryTableOffsetToSizeLimiter { get; internal set; }
        public static uint vsPokemonEntryTableOffsetToSizeLimiter { get; internal set; }
        public static uint effectsComboTableOffsetToSizeLimiter { get; internal set; }

        public static uint OWTableOffset { get; internal set; }
        public static string OWtablePath { get; private set; }

        public static uint monIconPalTableAddress { get; private set; }

        public static int nullEncounterID { get; private set; }
        public static int abilityNamesTextNumber { get; private set; }
        public static int attackNamesTextNumber { get; private set; }
        public static int[] pokemonNamesTextNumbers { get; private set; }
        public static int itemNamesTextNumber { get; private set; }
        public static int itemDescriptionsTextNumber { get; private set; }
        public static int itemScriptFileNumber { get; internal set; }
        public static int trainerClassMessageNumber { get; private set; }
        /// <summary>Trainer-class description text archive. Only confirmed for Platinum
        /// non-Japanese (619 name / 620 description, per a verified community write-up on adding a
        /// new trainer class) — elsewhere this is the unverified "next archive after the name one"
        /// convention, not independently confirmed. Only rely on it where that's been checked (see
        /// TrainerClassTableExpansion's Platinum-English-only gate for "Add Trainer Class").</summary>
        public static int trainerClassDescriptionMessageNumber { get; private set; }
        public static int trainerNamesMessageNumber { get; private set; }
        public static int moveDescriptionsTextNumbers { get; private set; }
        public static int moveNamesTextNumbers { get; private set; }
        public static int locationNamesTextNumber { get; private set; }
        public static int trainerNameLenOffset { get; private set; }
        public static int trainerNameMaxLen => SetTrainerNameMaxLen();
        public static int trainerFunnyScriptNumber { get; private set; }
        /// <summary>US-version text archive numbers only; not yet confirmed for other localizations.</summary>
        public static int battleTowerTrainerNamesMessageNumber { get; private set; }
        public static int battleTowerTrainerMessagesNumber { get; private set; }

        public static int typesTextNumber { get; private set; }
        public static int trainerMessageTextNumber { get; private set; }

        public static string internalNamesLocation { get; private set; }
        public static readonly byte internalNameLength = 16;
        public static string internalNamesPath { get; private set; }

        public static int cameraSize { get; private set; }

        public Dictionary<List<uint>, (Color background, Color foreground)> MapCellsColorDictionary;
        public static SortedDictionary<uint, (uint spriteID, ushort properties)> OverworldTable { get; private set; }
        public static uint[] overworldTableKeys { get; private set; }
        public static Dictionary<uint, string> ow3DSpriteDict { get; private set; }

        public static Dictionary<ushort, string> ScriptCommandNamesDict { get; private set; }
        public static Dictionary<string, ushort> ScriptCommandNamesReverseDict { get; private set; }

        public static Dictionary<ushort, string> ScriptActionNamesDict { get; private set; }
        public static Dictionary<string, ushort> ScriptActionNamesReverseDict { get; private set; }

        public static Dictionary<ushort, byte[]> ScriptCommandParametersDict { get; private set; }

        public static Dictionary<ushort, string> ScriptComparisonOperatorsDict { get; private set; }
        public static Dictionary<string, ushort> ScriptComparisonOperatorsReverseDict { get; private set; }
        public static bool AIBackportEnabled { get; private set; }
        public static bool OutdatedAIBackportEnabled { get; private set; }

        public enum GameVersions : byte
        {
            Diamond, Pearl, Platinum,
            HeartGold, SoulSilver,
            Black, White,
            Black2, White2
        }

        public enum GameFamilies : byte
        {
            NULL,
            DP,
            Plat,
            HGSS,
            BW,
            BW2
        }

        public enum GameLanguages : byte
        {
            English,
            Japanese,

            Italian,
            Spanish,
            French,
            German
        }

        public enum DirNames : byte
        {
            personalPokeData,
            pokemonBattleSprites,
            otherPokemonBattleSprites,
            pokemonSpriteOffsets,   // combined per-mon record: HGSS /a/1/8/0 (89 B/mon) · Plat pl_poke_data.narc — last 3 bytes = sprite Y/shadow X/shadow size
            pokeYofs,               // DP /poketool/pokegra/poke_yofs.narc — signed front-sprite Y, 1 B/mon
            pokeShadowOfx,          // DP /poketool/pokegra/poke_shadow_ofx.narc — signed shadow X, 1 B/mon
            pokeShadow,             // DP /poketool/pokegra/poke_shadow.narc — shadow size, 1 B/mon
            pokeHeight,             // DP+Plat /poketool/pokegra/height.narc — 4 files/mon (F-back,M-back,F-front,M-front), heights
            pokeHeightForms,        // DP+Plat /poketool/pokegra/height_o.narc — 2 files/form (back, front; both genders)
            pokeAnim,               // DP /poketool/pokeanm/pokeanm.narc — 28 B/mon battle-animation table (POKE_ANM_DATA)
            pokeAnimDefs,           // DP /pokeanime/poke_anm.narc — the PAST program-animation scripts (referenced by prg_anm)

            wazaSeq,                // battle move sequence scripts (per move) — battle/skill/waza_seq.narc (HGSS a/0/0/0)
            subSeq,                 // shared move-sequence subroutines — battle/skill/sub_seq.narc (HGSS a/0/0/1)
            beSeq,                  // per-effect move sequence scripts — battle/skill/be_seq.narc (HGSS a/0/3/0)
            wazaEffectScripts,      // move VISUAL-effect (WEST) scripts, one per move — wazaeffect/we.arc (HGSS a/0/1/0)
            wazaEffectSub,          // WEST subroutines / continuous animations — wazaeffect/we_sub.narc (HGSS a/0/6/1)
            wazaEffectChar,         // effect cell graphics: NCGR char — wazaeffect/effectclact/wechar.narc (HGSS a/0/2/2)
            wazaEffectPltt,         // effect cell graphics: NCLR palette — wazaeffect/effectclact/wepltt.narc (HGSS a/0/2/3)
            wazaEffectCell,         // effect cell graphics: NCER cells — wazaeffect/effectclact/wecell.narc (HGSS a/0/2/4)
            wazaEffectCellAnm,      // effect cell graphics: NANR anims — wazaeffect/effectclact/wecellanm.narc (HGSS a/0/2/5)
            wazaParticle,           // effect SPA particle systems — wazaeffect/effectdata/waza_particle.narc (HGSS a/0/2/9)
            battleBg,               // battle backgrounds + move-effect HAIKEI scroll BGs — pl_batt_bg.narc (HGSS a/0/0/7 = ARC_BATT_BG)
            battleObj,              // battle OBJ cells incl. the terrain ground platforms — pl_batt_obj.narc (HGSS a/0/0/8 = ARC_BATT_OBJ)
            battleBgPlanm,          // HGSS-ONLY animated BG palette-anim data (WEST_HAIKEI_CHG_EX) — a/0/0/9 = ARC_BATT_BG_PLANM
            dungeonCutinGraphics,   // HGSS only. Dungeon cutin (location-preview splash) art, a/1/5/0.
            titleScreenGraphics,    // HGSS only. Main-menu title logo/palette/background, a/0/4/6.
            trainerCardGraphics,    // HGSS + Platinum only. Trainer card face/back + trainer-pose art.

            synthOverlay,
            dynamicHeaders,

            textArchives,
            matrices,

            maps,
            exteriorBuildingModels,
            buildingConfigFiles,
            buildingTextures,
            mapTextures,
            areaData,

            eventFiles,
            OWSprites,

            scripts,

            encounters,
            encounterExtended,
            headbutt,
            rockSmash,
            safariZone,
            battleTowerTrainers,
            battleTowerPokemon,

            trainerProperties,
            trainerParty,
            trainerGraphics,
            moveData,

            monIcons,

            interiorBuildingModels,
            learnsets,
            evolutions,

            itemData,
            itemIcons,

            tradeData,

            trainerTextOffset,
            trainerTextTable,

            eggMoves,
        };

        public static Dictionary<DirNames, (string packedDir, string unpackedDir)> gameDirs { get; private set; }

        #region Constructors (1)

        public RomInfo(string id, string romFolderName)
        {
            // These are only ever (re)populated lazily elsewhere, behind an "if (x == null)" check —
            // without resetting them here first, switching to a different ROM mid-session would leave
            // the FIRST-loaded ROM's overworld-sprite table/dict silently in effect forever.
            OverworldTable = null;
            overworldTableKeys = null;
            ow3DSpriteDict = null;

            string path = Path.GetFullPath(romFolderName);

            IsDsRomProject = DSUtils.GetFolderType(romFolderName) == 0;
            
            if (IsDsRomProject)
            {
                dataFolderName = "files";
                customNarcFolderName = "files/zcustom";
            }
            else
            {
                dataFolderName = "data";
                customNarcFolderName = "data/zcustom";
            }

            workDir = path + Path.DirectorySeparatorChar; // Trailing separator is load-bearing: callers concatenate onto workDir directly
            RefreshRotomProjectState();
            
            if (IsDsRomProject)
            {
                arm9Path = Path.Combine(workDir, "arm9", "arm9.bin");
                arm7Path = Path.Combine(workDir, "arm7", "arm7.bin");
                overlayTablePath = Path.Combine(workDir, "arm9_overlays", "overlays.yaml");
                y7Path = Path.Combine(workDir, "arm7_overlays", "overlays.yaml");
                dataPath = Path.Combine(workDir, @"files");
                overlayPath = Path.Combine(workDir, @"arm9_overlays");
                bannerPath = Path.Combine(workDir, @"banner");
                headerPath = Path.Combine(workDir, @"header.yaml");
            }
            else
            {
                arm9Path = Path.Combine(workDir, @"arm9.bin");
                arm7Path = Path.Combine(workDir, @"arm7.bin");
                overlayTablePath = Path.Combine(workDir, @"y9.bin");
                y7Path = Path.Combine(workDir, @"y7.bin");
                dataPath = Path.Combine(workDir, dataFolderName);
                overlayPath = Path.Combine(workDir, @"overlay");
                bannerPath = Path.Combine(workDir, @"banner.bin");
                headerPath = Path.Combine(workDir, @"header.bin");
            }
            unpackedPath = Path.Combine(workDir, @"unpacked");
            internalNamesPath = Path.Combine(dataPath, "fielddata", "maptable", "mapname.bin");

            try
            {
                gameVersion = PokeDatabase.System.versionsDict[id];
            }
            catch (KeyNotFoundException)
            {
                ShowWarning?.Invoke("The ROM you attempted to load is not supported.\nYou can only load Gen IV Pokémon ROMS, for now.", "Unsupported ROM");
                return;
            }

            romID = id;
            isHGE = false;
            if (gameVersion == GameVersions.HeartGold && gameLanguage == GameLanguages.English)
            {
                string ov129path = OverlayUtils.GetPath(129);
                if (File.Exists(ov129path))
                {
                    using (DSUtils.EasyReader br = new DSUtils.EasyReader(ov129path))
                    {
                        string gameCode = Encoding.UTF8.GetString(br.ReadBytes(16));
                        if (gameCode == "hg-engine rocks!")
                        {
                            isHGE = true;
                        }
                        else
                        {
                            isHGE = false;
                        }
                    }                    
                } else
                {
                    isHGE = false;
                }
            }
            // Get the folder name and strip the _DSPRE_contents suffix to get the ROM name
            string folderName = Path.GetFileName(romFolderName);
            if (folderName.EndsWith(folderSuffix))
            {
                projectName = folderName.Substring(0, folderName.Length - folderSuffix.Length);
            }
            else
            {
                projectName = folderName;
            }

            LoadGameFamily();
            LoadGameLanguage();

            SetNarcDirs();
            SetHeaderTableOffset();
            SetNullEncounterID();
            SetPickupTableOffsets();
            SetItemTableOffset();
            SetStarterOffsets();

            SetAbilityNamesTextNumber();
            SetAttackNamesTextNumber();
            SetPokemonNamesTextNumber();
            SetItemsTextNumber();
            SetItemScriptFileNumber();
            SetLocationNamesTextNumber();
            SetTrainerNamesMessageNumber();
            SetTrainerClassMessageNumber();
            SetTrainerFunnyScriptNumber();
            SetTrainerNameLenOffset();
            SetBattleTowerTextNumbers();
            SetMoveTextNumbers();
            SetTypesTextNumber();
            SetTrainerMessageTextNumber();

            InitScriptDBs();

            /* System */
            ScriptCommandParametersDict = BuildCommandParametersDatabase(gameFamily);

            ScriptCommandNamesDict = BuildCommandNamesDatabase(gameFamily);
            ScriptActionNamesDict = BuildActionNamesDatabase(gameFamily);
            ScriptComparisonOperatorsDict = BuildComparisonOperatorsDatabase(gameFamily);

            ScriptCommandNamesReverseDict = ScriptCommandNamesDict.Reverse();
            ScriptActionNamesReverseDict = ScriptActionNamesDict.Reverse();
            ScriptComparisonOperatorsReverseDict = ScriptComparisonOperatorsDict.Reverse();

        }

        #endregion Constructors (1)

        #region Methods (22)

        public static void InitScriptDBs()
        {
            ScriptDatabaseSetup.InitializeScriptDatabase(projectName, gameFamily, gameVersion);
        }

        public static void ReloadScriptCommandDictionaries()
        {
            ScriptCommandParametersDict = BuildCommandParametersDatabase(gameFamily);
            ScriptCommandNamesDict = BuildCommandNamesDatabase(gameFamily);
            ScriptActionNamesDict = BuildActionNamesDatabase(gameFamily);
            ScriptComparisonOperatorsDict = BuildComparisonOperatorsDatabase(gameFamily);
            ScriptCommandNamesReverseDict = ScriptCommandNamesDict.Reverse();
            ScriptActionNamesReverseDict = ScriptActionNamesDict.Reverse();
            ScriptComparisonOperatorsReverseDict = ScriptComparisonOperatorsDict.Reverse();
        }

        public static Dictionary<ushort, ScriptCommandInfo> GetScriptCommandInfoDict()
        {
            switch (gameFamily)
            {
                case GameFamilies.DP:
                    return ScriptDatabase.DPScrCmdInfo;
                case GameFamilies.Plat:
                    return ScriptDatabase.PlatScrCmdInfo;
                case GameFamilies.HGSS:
                    return ScriptDatabase.HGSSScrCmdInfo;
                default:
                    return new Dictionary<ushort, ScriptCommandInfo>();
            }
        }

        /// <summary>
        /// Builds the command names dictionary from ScriptCommandInfo objects.
        /// </summary>
        public static Dictionary<ushort, string> BuildCommandNamesDatabase(GameFamilies gameFam)
        {
            var cmdInfoDict = GetScriptCommandInfoDict();
            return cmdInfoDict.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Name);
        }

        /// <summary>
        /// Builds the command parameters dictionary from ScriptCommandInfo objects.
        /// </summary>
        public static Dictionary<ushort, byte[]> BuildCommandParametersDatabase(GameFamilies gameFam)
        {
            var cmdInfoDict = GetScriptCommandInfoDict();
            return cmdInfoDict.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ParameterSizes);
        }

        public static Dictionary<ushort, string> BuildActionNamesDatabase(GameFamilies gameFam)
        {
            return ScriptDatabase.movementsDict.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Name);
        }

        public static Dictionary<ushort, string> BuildComparisonOperatorsDatabase(GameFamilies gameFam)
        {
            switch (gameFam)
            {
                case GameFamilies.DP:
                case GameFamilies.Plat:
                case GameFamilies.HGSS:
                    return ScriptDatabase.comparisonOperatorsDict;

                default:
                    var commonDict = ScriptDatabase.comparisonOperatorsDict;
                    var appendixDict = ScriptDatabase.comparisonOperatorsGenVappendix;
                    return commonDict.Concat(appendixDict).ToLookup(x => x.Key, x => x.Value).ToDictionary(x => x.Key, g => g.First());
            }
        }

        public static void Set3DOverworldsDict()
        {
            ow3DSpriteDict = new Dictionary<uint, string>()
            {
                [91] = "brown_sign",
                [92] = "red_sign",
                [93] = "gray_sign",
                [94] = "route_sign",
                [95] = "blue_sign", //to fix this one (gym_sign)
                [96] = "blue_sign",
                [101] = "dawn_platinum", // depends on value of variable 0x4020
                //[174] = "dppt_suitcase",
            };

            // Special Objects whose sprites depend on a variable value (0x4021-0x402F)
            for (uint i = 102; i <= 116; i++)
            {
                ow3DSpriteDict[i] = "overworld";
            }

        }

        public static void SetHeaderTableOffset()
        {
            switch (gameFamily)
            {
                case GameFamilies.DP:
                    switch (gameLanguage)
                    {
                        case GameLanguages.English:
                            headerTableOffset = 0xEEDBC;
                            break;

                        case GameLanguages.Spanish:
                            headerTableOffset = 0xEEE08;
                            break;

                        case GameLanguages.Italian:
                            headerTableOffset = 0xEED70;
                            break;

                        case GameLanguages.French:
                            headerTableOffset = 0xEEDFC;
                            break;

                        case GameLanguages.German:
                            headerTableOffset = 0xEEDCC;
                            break;

                        case GameLanguages.Japanese:
                            headerTableOffset = gameVersion == GameVersions.Diamond ? (uint)0xF0D68 : 0xF0D6C;
                            break;
                    }
                    break;

                case GameFamilies.Plat:
                    switch (gameLanguage)
                    {
                        case GameLanguages.English:
                            headerTableOffset = 0xE601C;
                            break;

                        case GameLanguages.Spanish:
                            headerTableOffset = 0xE60B0;
                            break;

                        case GameLanguages.Italian:
                            headerTableOffset = 0xE6038;
                            break;

                        case GameLanguages.French:
                            headerTableOffset = 0xE60A4;
                            break;

                        case GameLanguages.German:
                            headerTableOffset = 0xE6074;
                            break;

                        case GameLanguages.Japanese:
                            headerTableOffset = 0xE56F0;
                            break;
                    }
                    break;

                case GameFamilies.HGSS:
                    switch (gameLanguage)
                    {
                        case GameLanguages.English:
                            headerTableOffset = 0xF6BE0;
                            break;

                        case GameLanguages.Spanish:
                            headerTableOffset = gameVersion == GameVersions.HeartGold ? 0xF6BC8 : (uint)0xF6BD0;
                            break;

                        case GameLanguages.Italian:
                            headerTableOffset = 0xF6B58;
                            break;

                        case GameLanguages.French:
                            headerTableOffset = 0xF6BC4;
                            break;

                        case GameLanguages.German:
                            headerTableOffset = 0xF6B94;
                            break;

                        case GameLanguages.Japanese:
                            headerTableOffset = 0xF6390;
                            break;
                    }
                    break;
            }
        }

        public static void SetupSpawnSettings()
        {
            switch (gameFamily)
            {
                case GameFamilies.DP:
                    initialMoneyOverlayNumber = 52;
                    initialMoneyOverlayOffset = 0x1E4;
                    switch (gameLanguage)
                    {
                        case GameLanguages.English:
                            arm9spawnOffset = 0xF2B9C;
                            break;

                        case GameLanguages.Spanish:
                            arm9spawnOffset = 0xF2BE8;
                            break;

                        case GameLanguages.Italian:
                            arm9spawnOffset = 0xF2B50;
                            break;

                        case GameLanguages.French:
                            arm9spawnOffset = 0xF2BDC;
                            break;

                        case GameLanguages.German:
                            arm9spawnOffset = 0xF2BAC;
                            break;

                        case GameLanguages.Japanese:
                            arm9spawnOffset = 0xF4B48;
                            break;
                    }
                    break;

                case GameFamilies.Plat:
                    initialMoneyOverlayNumber = 57;
                    initialMoneyOverlayOffset = 0x1EC;
                    switch (gameLanguage)
                    {
                        case GameLanguages.English:
                            arm9spawnOffset = 0xEA12C;
                            break;

                        case GameLanguages.Spanish:
                            arm9spawnOffset = 0xEA1C0;
                            break;

                        case GameLanguages.Italian:
                            arm9spawnOffset = 0xEA148;
                            break;

                        case GameLanguages.French:
                            arm9spawnOffset = 0xEA1B4;
                            break;

                        case GameLanguages.German:
                            arm9spawnOffset = 0xEA184;
                            break;

                        case GameLanguages.Japanese:
                            arm9spawnOffset = 0xE9800;
                            break;
                    }
                    break;

                case GameFamilies.HGSS:
                    initialMoneyOverlayNumber = 36;
                    initialMoneyOverlayOffset = 0x2FC;
                    switch (gameLanguage)
                    {
                        case GameLanguages.English:
                            arm9spawnOffset = 0xFA17C;
                            break;

                        case GameLanguages.Spanish:
                            arm9spawnOffset = gameVersion == GameVersions.HeartGold ? 0xFA164 : (uint)0xFA16C;
                            break;

                        case GameLanguages.Italian:
                            arm9spawnOffset = 0xFA0F4;
                            break;

                        case GameLanguages.French:
                            arm9spawnOffset = 0xFA160;
                            break;

                        case GameLanguages.German:
                            arm9spawnOffset = 0xFA130;
                            break;

                        case GameLanguages.Japanese:
                            arm9spawnOffset = 0xF992C;
                            break;
                    }
                    break;
            }
        }

        public static void SetPickupTableOffsets()
        {
            // Initialize to invalid values by default
            pickupTableOverlayNumber = -1;
            pickupCommonItemsOffset = 0;
            pickupRareItemsOffset = 0;
            pickupActivationDivisorOffset = 0;
            pickupWeightTableOffset = 0;

            switch (gameFamily)
            {
                case GameFamilies.DP:
                    pickupTableOverlayNumber = 11;
                    switch (gameLanguage)
                    {
                        case GameLanguages.English:
                            pickupCommonItemsOffset = 0x30764;
                            pickupRareItemsOffset = 0x30688;
                            pickupActivationDivisorOffset = 0xBCB2;
                            pickupWeightTableOffset = 0x30B90;
                            break;
                        case GameLanguages.Japanese:
                            pickupCommonItemsOffset = 0x32E64;
                            pickupRareItemsOffset = 0x32D9C;
                            pickupActivationDivisorOffset = 0xADE4;
                            pickupWeightTableOffset = 0x32D68;
                            break;
                        case GameLanguages.French:
                            pickupCommonItemsOffset = 0x308CC;
                            pickupRareItemsOffset = 0x307F0;
                            pickupActivationDivisorOffset = 0xBDBA;
                            pickupWeightTableOffset = 0x30CF8;
                            break;
                        case GameLanguages.German:
                            pickupCommonItemsOffset = 0x307E0;
                            pickupRareItemsOffset = 0x30704;
                            pickupActivationDivisorOffset = 0xBCCE;
                            pickupWeightTableOffset = 0x30C0C;
                            break;
                        case GameLanguages.Italian:
                            pickupCommonItemsOffset = 0x307E0;
                            pickupRareItemsOffset = 0x30704;
                            pickupActivationDivisorOffset = 0xBCCE;
                            pickupWeightTableOffset = 0x30C0C;
                            break;
                        case GameLanguages.Spanish:
                            pickupCommonItemsOffset = 0x308CC;
                            pickupRareItemsOffset = 0x307F0;
                            pickupActivationDivisorOffset = 0xBDBA;
                            pickupWeightTableOffset = 0x30CF8;
                            break;
                        default:
                            pickupCommonItemsOffset = 0x30764;
                            pickupRareItemsOffset = 0x30688;
                            pickupActivationDivisorOffset = 0xBCB2;
                            pickupWeightTableOffset = 0x30B90;
                            break;
                    }
                    break;

                case GameFamilies.Plat:
                    pickupTableOverlayNumber = 16;
                    switch (gameLanguage)
                    {
                        case GameLanguages.English:
                            pickupCommonItemsOffset = 0x3352C;
                            pickupRareItemsOffset = 0x33450;
                            pickupActivationDivisorOffset = 0xC62A;
                            pickupWeightTableOffset = 0x33968;
                            break;
                        case GameLanguages.Japanese:
                            pickupCommonItemsOffset = 0x33520;
                            pickupRareItemsOffset = 0x33444;
                            pickupActivationDivisorOffset = 0xC622;
                            pickupWeightTableOffset = 0x3395C;
                            break;
                        case GameLanguages.French:
                            pickupCommonItemsOffset = 0x33634;
                            pickupRareItemsOffset = 0x33558;
                            pickupActivationDivisorOffset = 0xC732;
                            pickupWeightTableOffset = 0x33A70;
                            break;
                        case GameLanguages.German:
                            pickupCommonItemsOffset = 0x33548;
                            pickupRareItemsOffset = 0x3346C;
                            pickupActivationDivisorOffset = 0xC646;
                            pickupWeightTableOffset = 0x33984;
                            break;
                        case GameLanguages.Italian:
                            pickupCommonItemsOffset = 0x33548;
                            pickupRareItemsOffset = 0x3346C;
                            pickupActivationDivisorOffset = 0xC646;
                            pickupWeightTableOffset = 0x33984;
                            break;
                        case GameLanguages.Spanish:
                            pickupCommonItemsOffset = 0x33634;
                            pickupRareItemsOffset = 0x33558;
                            pickupActivationDivisorOffset = 0xC732;
                            pickupWeightTableOffset = 0x33A70;
                            break;
                        default:
                            pickupCommonItemsOffset = 0x3352C;
                            pickupRareItemsOffset = 0x33450;
                            pickupActivationDivisorOffset = 0xC62A;
                            pickupWeightTableOffset = 0x33968;
                            break;
                    }
                    break;

                case GameFamilies.HGSS:
                    pickupTableOverlayNumber = 12;
                    switch (gameLanguage)
                    {
                        case GameLanguages.English:
                            pickupCommonItemsOffset = 0x34B44;
                            pickupRareItemsOffset = 0x34A4C;
                            pickupActivationDivisorOffset = 0xC852;
                            pickupWeightTableOffset = 0x3518C;
                            break;
                        case GameLanguages.Japanese:
                            pickupCommonItemsOffset = 0x34B00;
                            pickupRareItemsOffset = 0x34A08;
                            pickupActivationDivisorOffset = 0xC85A;
                            pickupWeightTableOffset = 0x35148;
                            break;
                        case GameLanguages.French:
                            pickupCommonItemsOffset = 0x34B44;
                            pickupRareItemsOffset = 0x34A4C;
                            pickupActivationDivisorOffset = 0xC852;
                            pickupWeightTableOffset = 0x3518C;
                            break;
                        case GameLanguages.German:
                            pickupCommonItemsOffset = 0x34B44;
                            pickupRareItemsOffset = 0x34A4C;
                            pickupActivationDivisorOffset = 0xC852;
                            pickupWeightTableOffset = 0x3518C;
                            break;
                        case GameLanguages.Italian:
                            pickupCommonItemsOffset = 0x34B44;
                            pickupRareItemsOffset = 0x34A4C;
                            pickupActivationDivisorOffset = 0xC852;
                            pickupWeightTableOffset = 0x3518C;
                            break;
                        case GameLanguages.Spanish:
                            pickupCommonItemsOffset = 0x34B44;
                            pickupRareItemsOffset = 0x34A4C;
                            pickupActivationDivisorOffset = 0xC852;
                            pickupWeightTableOffset = 0x3518C;
                            break;
                        default:
                            pickupCommonItemsOffset = 0x34B44;
                            pickupRareItemsOffset = 0x34A4C;
                            pickupActivationDivisorOffset = 0xC852;
                            pickupWeightTableOffset = 0x3518C;
                            break;
                    }
                    break;
            }
        }

        /// <summary>
        /// Offsets for the Starter Pokémon editor. Sourced from Universal Pokémon Randomizer FVX's
        /// gen4_offsets.ini (its "(E)"/"(G)"/"(F)"/"(S)"/"(I)" ROM entries all CopyFrom the "(U)" entry with
        /// zero Starter-key overrides, so English and every European language share identical offsets here;
        /// Japanese has its own confirmed offsets). HGSS species aren't offset-based at all — see
        /// <see cref="starterArm9SearchSuffix"/> — so only the cries-table overlay is set for that family.
        /// </summary>
        public static void SetStarterOffsets()
        {
            // Initialize to invalid values by default
            starterOverlayNumber = -1;
            starterSpeciesOffset = 0;
            starterArm9SearchSuffix = null;
            starterGraphicsPrefix = null;
            starterGraphicsPrefixInner = null;
            starterCriesPrefix = null;
            starterHeldItemScriptFileID = -1;
            starterHeldItemOffset = 0;
            starterScreenTextNumber = -1;
            starterPokedexSpeciesTextNumber = -1;

            switch (gameFamily)
            {
                case GameFamilies.DP:
                    starterOverlayNumber = 64;
                    starterGraphicsPrefix = "000222402104120C";
                    starterGraphicsPrefixInner = "0290039002200002";
                    starterHeldItemScriptFileID = 342;
                    starterHeldItemOffset = 0x2B4;
                    switch (gameLanguage)
                    {
                        case GameLanguages.Japanese:
                            starterSpeciesOffset = 0x30;
                            starterScreenTextNumber = 318;
                            starterPokedexSpeciesTextNumber = 607;
                            break;
                        default: // English + EFIGS share identical offsets
                            starterSpeciesOffset = 0x1B88;
                            starterScreenTextNumber = 320;
                            starterPokedexSpeciesTextNumber = 621;
                            break;
                    }
                    break;

                case GameFamilies.Plat:
                    starterOverlayNumber = 78;
                    starterGraphicsPrefix = "000222402104120C";
                    starterGraphicsPrefixInner = "0290039002200002";
                    starterHeldItemScriptFileID = 427;
                    starterHeldItemOffset = 0x460;
                    switch (gameLanguage)
                    {
                        case GameLanguages.Japanese:
                            starterSpeciesOffset = 0x1BAC;
                            starterScreenTextNumber = 359;
                            starterPokedexSpeciesTextNumber = 698;
                            break;
                        default: // English + EFIGS share identical offsets
                            starterSpeciesOffset = 0x1BC0;
                            starterScreenTextNumber = 360;
                            starterPokedexSpeciesTextNumber = 711;
                            break;
                    }
                    break;

                case GameFamilies.HGSS:
                    // Species IDs are read/written straight in arm9.bin via this byte-pattern search (species
                    // words start 13 bytes before the match) rather than a fixed offset — see StarterPokemonData.
                    starterArm9SearchSuffix = new byte[] { 0x03, 0x03, 0x1A, 0x12, 0x01, 0x23, 0x00, 0x00 };
                    starterOverlayNumber = 61; // starter-cries table only (species table is in ARM9, above)
                    starterCriesPrefix = "0004000C10BD0000000000000000000000E000000000000000E0000000000200";
                    switch (gameLanguage)
                    {
                        case GameLanguages.Japanese:
                            starterScreenTextNumber = 188;
                            break;
                        default: // English + EFIGS share identical offsets
                            starterScreenTextNumber = 190;
                            break;
                    }
                    break;
            }
        }

        public static void SetItemTableOffset()
        {
            switch (gameFamily)
            {
                case GameFamilies.DP:
                    switch (gameLanguage)
                    {
                        case GameLanguages.English:
                            itemTableOffset = 0xF85B4;
                            break;
                        case GameLanguages.Japanese:
                            itemTableOffset = 0xFA520;
                            break;
                        case GameLanguages.French:
                            itemTableOffset = 0xF85F8;
                            break;
                        case GameLanguages.German:
                            itemTableOffset = 0xF85C8;
                            break;
                        case GameLanguages.Italian:
                            itemTableOffset = 0xF856C;
                            break;
                        case GameLanguages.Spanish:
                            itemTableOffset = 0xF8604;
                            break;
                        default:
                            itemTableOffset = 0xF85B4;
                            break;
                    }
                    break;
                case GameFamilies.Plat:
                    switch (gameLanguage)
                    {
                        case GameLanguages.English:
                            itemTableOffset = 0xF0CC4;
                            break;
                        case GameLanguages.Japanese:
                            itemTableOffset = 0xF0354;
                            break;
                        case GameLanguages.French:
                            itemTableOffset = 0xF0D4C;
                            break;
                        case GameLanguages.German:
                            itemTableOffset = 0xF0D1C;
                            break;
                        case GameLanguages.Italian:
                            itemTableOffset = 0xF0CE0;
                            break;
                        case GameLanguages.Spanish:
                            itemTableOffset = 0xF0D58;
                            break;
                        default:
                            itemTableOffset = 0xF0CC4;
                            break;
                    }
                    break;
                case GameFamilies.HGSS:
                    switch (gameLanguage)
                    {
                        case GameLanguages.English:
                            itemTableOffset = 0x100194;
                            break;
                        case GameLanguages.Japanese:
                            itemTableOffset = 0xFF914;
                            break;
                        case GameLanguages.French:
                            itemTableOffset = 0x100178;
                            break;
                        case GameLanguages.German:
                            itemTableOffset = 0x100148;
                            break;
                        case GameLanguages.Italian:
                            itemTableOffset = 0x10010C;
                            break;
                        case GameLanguages.Spanish:
                            itemTableOffset = 0x10017C;
                            break;
                        default:
                            itemTableOffset = 0x100194;
                            break;
                    }
                    break;
                default:
                    AppLogger.Error("SetItemTableOffset: Unsupported game");
                    throw new NotSupportedException("Game not supported");
            }
        }

        public static void PrepareCameraData()
        {
            switch (gameFamily)
            {
                case GameFamilies.DP:
                    cameraTblOverlayNumber = 5;
                    cameraTblOffsetsToRAMaddress = gameLanguage.Equals(GameLanguages.Japanese) ? (new uint[] { 0x4C50 }) : (new uint[] { 0x4908 });
                    cameraSize = 24;
                    break;

                case GameFamilies.Plat:
                    cameraTblOverlayNumber = 5;
                    cameraTblOffsetsToRAMaddress = new uint[] { 0x4E24 };
                    cameraSize = 24;
                    break;

                case GameFamilies.HGSS:
                    cameraTblOverlayNumber = 1;
                    cameraSize = 36;
                    switch (gameLanguage)
                    {
                        case GameLanguages.English:
                        case GameLanguages.Spanish:
                        case GameLanguages.French:
                        case GameLanguages.German:
                        case GameLanguages.Italian:
                            cameraTblOffsetsToRAMaddress = new uint[] { 0x532C, 0x547C };
                            break;

                        case GameLanguages.Japanese:
                            cameraTblOffsetsToRAMaddress = new uint[] { 0x5324, 0x5474 };
                            break;
                    }
                    break;
            }
        }

        public static void SetOWtable()
        {
            switch (gameFamily)
            {
                case GameFamilies.DP:
                    OWtablePath = OverlayUtils.GetPath(5);
                    switch (gameLanguage)
                    { // Go to the beginning of the overworld table
                        case GameLanguages.English:
                            OWTableOffset = 0x22BCC;
                            break;

                        case GameLanguages.Japanese:
                            OWTableOffset = 0x23BB8;
                            break;

                        default:
                            OWTableOffset = 0x22B84;
                            break;
                    }
                    break;

                case GameFamilies.Plat:
                    OWtablePath = OverlayUtils.GetPath(5);
                    switch (gameLanguage)
                    { // Go to the beginning of the overworld table
                        case GameLanguages.Italian:
                            OWTableOffset = 0x2BC44;
                            break;

                        case GameLanguages.French:
                        case GameLanguages.Spanish:
                            OWTableOffset = 0x2BC3C;
                            break;

                        case GameLanguages.German:
                            OWTableOffset = 0x2BC50;
                            break;

                        case GameLanguages.Japanese:
                            OWTableOffset = 0x2BA24;
                            break;

                        default:
                            OWTableOffset = 0x2BC34;
                            break;
                    }
                    break;

                case GameFamilies.HGSS:
                    if (OverlayUtils.OverlayTable.IsDefaultCompressed(1))
                    {
                        if (OverlayUtils.IsCompressed(1))
                        {
                            if (OverlayUtils.Decompress(1) < 0)
                            {
                                ShowWarning?.Invoke("Overlay 1 couldn't be decompressed.\nOverworld sprites in the Event Editor will be " +
                                "displayed incorrectly or not displayed at all.", "Decompression error");
                            }
                        }
                    }

                    string ov1Path = OverlayUtils.GetPath(1);
                    uint ov1Address = OverlayUtils.OverlayTable.GetRAMAddress(1);

                    int ramAddrOfPointer;
                    switch (gameLanguage)
                    {
                        case GameLanguages.Italian:
                            ramAddrOfPointer = 0x021F929C;
                            break;

                        case GameLanguages.French:
                        case GameLanguages.Spanish:
                            ramAddrOfPointer = 0x021F931C;
                            break;

                        case GameLanguages.German:
                            ramAddrOfPointer = 0x021F92DC;
                            break;

                        case GameLanguages.Japanese:
                            ramAddrOfPointer = 0x021F86C4;
                            break;

                        default:
                            ramAddrOfPointer = 0x021F92FC;
                            break;
                    }

                    using (DSUtils.EasyReader bReader = new DSUtils.EasyReader(ov1Path, ramAddrOfPointer - ov1Address))
                    { // read the pointer at the specified ram address and adjust accordingly below
                        uint ramAddressOfTable = bReader.ReadUInt32();
                        if ((ramAddressOfTable >> 0x18) != 0x02)
                        {
                            ShowWarning?.Invoke("Something went wrong reading the Overworld configuration table.\nOverworld sprites in the Event Editor will be " +
                                "displayed incorrectly or not displayed at all.", "Decompression error");
                            return;
                        }

                        string ov131path = OverlayUtils.GetPath(131);
                        if (File.Exists(ov131path))
                        {
                            // if HGE field extension overlay exists
                            OWTableOffset = ramAddressOfTable - OverlayUtils.OverlayTable.GetRAMAddress(131);
                            OWtablePath = ov131path;
                        }
                        else if (ramAddressOfTable >= RomInfo.synthOverlayLoadAddress)
                        {
                            // if the pointer shows the table was moved to the synthetic overlay
                            OWTableOffset = ramAddressOfTable - RomInfo.synthOverlayLoadAddress;
                            OWtablePath = Filesystem.expArmPath;
                        }
                        else
                        {
                            OWTableOffset = ramAddressOfTable - ov1Address;
                            OWtablePath = ov1Path;
                        }
                    }
                    break;
            }
        }

        public static void SetConditionalMusicTableOffsetToRAMAddress()
        {
            switch (gameFamily)
            {
                case GameFamilies.HGSS:
                    switch (gameLanguage)
                    {
                        case GameLanguages.Spanish:
                            conditionalMusicTableOffsetToRAMAddress = gameVersion == GameVersions.HeartGold ? (uint)0x667D0 : 0x667D8;
                            break;

                        case GameLanguages.English:
                        case GameLanguages.Italian:
                        case GameLanguages.French:
                        case GameLanguages.German:
                            conditionalMusicTableOffsetToRAMAddress = 0x667D8;
                            break;

                        case GameLanguages.Japanese:
                            conditionalMusicTableOffsetToRAMAddress = 0x66238;
                            break;
                    }
                    break;
            }
        }

        public static void SetDungeonCutinTableOffsetToRAMAddress()
        {
            if (gameFamily != GameFamilies.HGSS) return;
            switch (gameLanguage)
            {
                case GameLanguages.Spanish:
                    dungeonCutinTableOffsetToRAMAddress = gameVersion == GameVersions.HeartGold ? (uint)0x6A37C : 0x6A384;
                    break;

                case GameLanguages.English:
                    dungeonCutinTableOffsetToRAMAddress = 0x6A384;
                    break;
            }
        }

        public static void SetBattleEffectsData()
        {
            switch (gameFamily)
            {
                case GameFamilies.HGSS:
                    switch (gameLanguage)
                    {
                        case GameLanguages.Spanish:
                            vsPokemonEntryTableOffsetToRAMAddress = gameVersion == GameVersions.HeartGold ? (uint)0x518CC : 0x518D4;
                            vsTrainerEntryTableOffsetToRAMAddress = gameVersion == GameVersions.HeartGold ? (uint)0x51888 : 0x51890;
                            effectsComboTableOffsetToRAMAddress = gameVersion == GameVersions.HeartGold ? (uint)0x517C0 : 0x517C8;
                            break;

                        case GameLanguages.English:
                        case GameLanguages.Italian:
                        case GameLanguages.French:
                        case GameLanguages.German:
                            vsPokemonEntryTableOffsetToRAMAddress = 0x518D4;
                            vsTrainerEntryTableOffsetToRAMAddress = 0x51890;
                            effectsComboTableOffsetToRAMAddress = 0x517C8;
                            break;

                        case GameLanguages.Japanese:
                            vsPokemonEntryTableOffsetToRAMAddress = 0x5136C;
                            vsTrainerEntryTableOffsetToRAMAddress = 0x51328;
                            effectsComboTableOffsetToRAMAddress = 0x51260;
                            break;
                    }
                    vsPokemonEntryTableOffsetToSizeLimiter = vsPokemonEntryTableOffsetToRAMAddress - 0xA;
                    vsTrainerEntryTableOffsetToSizeLimiter = vsTrainerEntryTableOffsetToRAMAddress - 0xA;
                    effectsComboTableOffsetToSizeLimiter = effectsComboTableOffsetToRAMAddress - 0x1E;
                    break;

                case GameFamilies.Plat:
                    switch (gameLanguage)
                    {
                        case GameLanguages.English:
                            effectsComboTableOffsetToRAMAddress = 0x51BE0;
                            break;

                        case GameLanguages.Italian:
                        case GameLanguages.French:
                        case GameLanguages.Spanish:
                        case GameLanguages.German:
                            effectsComboTableOffsetToRAMAddress = 0x51C84;
                            break;

                        case GameLanguages.Japanese:
                            effectsComboTableOffsetToRAMAddress = 0x514C0;
                            break;
                    }
                    break;
            }
        }

        public static void SetEncounterMusicTableOffsetToRAMAddress()
        {
            switch (gameFamily)
            {
                case GameFamilies.HGSS:
                    switch (gameLanguage)
                    {
                        case GameLanguages.Spanish:
                            encounterMusicTableOffsetToRAMAddress = gameVersion == GameVersions.HeartGold ? (uint)0x550D8 : 0x550E0;
                            break;

                        case GameLanguages.English:
                        case GameLanguages.Italian:
                        case GameLanguages.French:
                        case GameLanguages.German:
                            encounterMusicTableOffsetToRAMAddress = 0x550E0;
                            break;

                        case GameLanguages.Japanese:
                            encounterMusicTableOffsetToRAMAddress = 0x54B44;
                            break;
                    }
                    break;

                case GameFamilies.Plat:
                    switch (gameLanguage)
                    {
                        case GameLanguages.English:
                            encounterMusicTableOffsetToRAMAddress = 0x5563C;
                            break;

                        case GameLanguages.Italian:
                        case GameLanguages.French:
                        case GameLanguages.Spanish:
                        case GameLanguages.German:
                            encounterMusicTableOffsetToRAMAddress = 0x556E0;
                            break;

                        case GameLanguages.Japanese:
                            encounterMusicTableOffsetToRAMAddress = 0x54F04;
                            break;
                    }
                    break;

                case GameFamilies.DP:
                    switch (gameLanguage)
                    {
                        case GameLanguages.English:
                            encounterMusicTableOffsetToRAMAddress = 0x4AD3C;
                            break;

                        case GameLanguages.Italian:
                        case GameLanguages.French:
                        case GameLanguages.Spanish:
                        case GameLanguages.German:
                            encounterMusicTableOffsetToRAMAddress = 0x4ADAC;
                            break;

                        case GameLanguages.Japanese:
                            encounterMusicTableOffsetToRAMAddress = 0x4D9AC;
                            break;
                    }
                    break;
            }
        }

        public static void SetMonIconsPalTableAddress()
        {
            switch (RomInfo.gameFamily)
            {
                case GameFamilies.DP:
                    switch (gameLanguage)
                    {
                        case GameLanguages.English:
                            monIconPalTableAddress = BitConverter.ToUInt32(ARM9.ReadBytes(0x6B838, 4), 0);
                            break;

                        case GameLanguages.Italian:
                            monIconPalTableAddress = BitConverter.ToUInt32(ARM9.ReadBytes(0x6B874, 4), 0);
                            break;

                        case GameLanguages.German:
                        case GameLanguages.French:
                        case GameLanguages.Spanish:
                            monIconPalTableAddress = BitConverter.ToUInt32(ARM9.ReadBytes(0x6B894, 4), 0);
                            break;

                        case GameLanguages.Japanese:
                            monIconPalTableAddress = BitConverter.ToUInt32(ARM9.ReadBytes(0x6FDEC, 4), 0);
                            break;
                    }
                    break;

                case GameFamilies.Plat:
                    switch (gameLanguage)
                    {
                        case GameLanguages.English:
                            monIconPalTableAddress = BitConverter.ToUInt32(ARM9.ReadBytes(0x79F80, 4), 0);
                            break;

                        case GameLanguages.Italian:
                        case GameLanguages.German:
                        case GameLanguages.French:
                        case GameLanguages.Spanish:
                            monIconPalTableAddress = BitConverter.ToUInt32(ARM9.ReadBytes(0x7A020, 4), 0);
                            break;

                        case GameLanguages.Japanese:
                            monIconPalTableAddress = BitConverter.ToUInt32(ARM9.ReadBytes(0x79858, 4), 0);
                            break;
                    }
                    break;

                case GameFamilies.HGSS:
                default:
                    switch (gameLanguage)
                    {
                        case GameLanguages.English:
                        case GameLanguages.Italian:
                        case GameLanguages.French:
                        case GameLanguages.German:
                            monIconPalTableAddress = BitConverter.ToUInt32(ARM9.ReadBytes(0x74408, 4), 0);
                            break;

                        case GameLanguages.Spanish:
                            if (gameVersion == GameVersions.HeartGold)
                            {
                                monIconPalTableAddress = BitConverter.ToUInt32(ARM9.ReadBytes(0x74400, 4), 0);
                            }
                            else
                            {
                                monIconPalTableAddress = BitConverter.ToUInt32(ARM9.ReadBytes(0x74408, 4), 0);
                            }
                            break;

                        case GameLanguages.Japanese:
                            monIconPalTableAddress = BitConverter.ToUInt32(ARM9.ReadBytes(0x73EA0, 4), 0);
                            break;
                    }
                    break;
            }
        }

        private static void SetItemScriptFileNumber()
        {
            switch (gameFamily)
            {
                case GameFamilies.DP:
                    itemScriptFileNumber = 370;
                    break;

                case GameFamilies.Plat:
                    itemScriptFileNumber = 404;
                    break;

                default:
                    itemScriptFileNumber = 141;
                    break;
            }
        }

        private static void SetNullEncounterID()
        {
            switch (gameFamily)
            {
                case GameFamilies.DP:
                case GameFamilies.Plat:
                    nullEncounterID = ushort.MaxValue;
                    break;

                case GameFamilies.HGSS:
                    nullEncounterID = Byte.MaxValue;
                    break;
            }
        }

        private static void SetAbilityNamesTextNumber()
        {
            switch (gameFamily)
            {
                case GameFamilies.DP:
                    abilityNamesTextNumber = 552;
                    break;

                case GameFamilies.Plat:
                    abilityNamesTextNumber = gameLanguage == GameLanguages.Japanese ? 604 : 610;
                    break;

                case GameFamilies.HGSS:
                    abilityNamesTextNumber = 720;
                    break;

                default:
                    break;
            }
        }

        private static void SetAttackNamesTextNumber()
        {
            switch (gameFamily)
            {
                case GameFamilies.DP:
                    attackNamesTextNumber = gameLanguage == GameLanguages.Japanese ? 575 : 588;
                    break;

                case GameFamilies.Plat:
                    attackNamesTextNumber = gameLanguage == GameLanguages.Japanese ? 636 : 647;
                    break;

                default:
                    attackNamesTextNumber = gameLanguage == GameLanguages.Japanese ? 739 : 750;
                    break;
            }
        }

        private static void SetItemsTextNumber()
        {
            switch (gameFamily)
            {
                case GameFamilies.DP:
                    itemNamesTextNumber = gameLanguage == GameLanguages.Japanese ? 341 : 344;
                    itemDescriptionsTextNumber = 0;
                    break;

                case GameFamilies.Plat:
                    itemNamesTextNumber = gameLanguage == GameLanguages.Japanese ? 390 : 392;
                    itemDescriptionsTextNumber = 0;
                    break;

                default:
                    itemNamesTextNumber = gameLanguage == GameLanguages.Japanese ? 219 : 222;
                    itemDescriptionsTextNumber = 221;
                    break;
            }
        }

        private static void SetLocationNamesTextNumber()
        {
            switch (gameFamily)
            {
                case GameFamilies.DP:
                    locationNamesTextNumber = gameLanguage == GameLanguages.Japanese ? 374 : 382;
                    break;

                case GameFamilies.Plat:
                    locationNamesTextNumber = gameLanguage == GameLanguages.Japanese ? 427 : 433;
                    break;

                default:
                    locationNamesTextNumber = gameLanguage == GameLanguages.Japanese ? 272 : 279;
                    break;
            }
        }

        private static void SetPokemonNamesTextNumber()
        {
            switch (gameFamily)
            {
                case GameFamilies.DP:
                    pokemonNamesTextNumbers = new int[2] { 362, 363 };
                    break;

                case GameFamilies.Plat:
                    pokemonNamesTextNumbers = gameLanguage == GameLanguages.Japanese
                        ? new int[1] { 408 }
                        : new int[7] { 412, 413, 712, 713, 714, 715, 716 }; //413?
                    break;

                case GameFamilies.HGSS:
                    pokemonNamesTextNumbers = gameLanguage.Equals(GameLanguages.Japanese) ? new int[1] { 232 } : new int[7] { 237, 238, 817, 818, 819, 820, 821 }; //238?
                    break;
            }
        }

        private static void SetTrainerNamesMessageNumber()
        {
            switch (gameFamily)
            {
                case GameFamilies.DP:
                    trainerNamesMessageNumber = 559;
                    if (gameLanguage.Equals(GameLanguages.Japanese))
                    {
                        trainerNamesMessageNumber -= 9;
                    }
                    break;

                case GameFamilies.Plat:
                    trainerNamesMessageNumber = gameLanguage == GameLanguages.Japanese ? 611 : 618;
                    break;

                default:
                    trainerNamesMessageNumber = 729;
                    if (gameLanguage == GameLanguages.Japanese)
                    {
                        trainerNamesMessageNumber -= 10;
                    }
                    break;
            }
        }

        private static void SetTrainerClassMessageNumber()
        {
            switch (gameFamily)
            {
                case GameFamilies.DP:
                    trainerClassMessageNumber = 560;
                    if (gameLanguage.Equals(GameLanguages.Japanese))
                    {
                        trainerClassMessageNumber -= 9;
                    }
                    break;

                case GameFamilies.Plat:
                    // Other languages have a separate "a Jogger"-style article-prefixed variant elsewhere;
                    // Japanese doesn't use articles, so there's no equivalent bank to also branch on here.
                    trainerClassMessageNumber = gameLanguage == GameLanguages.Japanese ? 612 : 619;
                    break;

                default:
                    trainerClassMessageNumber = 730;
                    if (gameLanguage.Equals(GameLanguages.Japanese))
                    {
                        trainerClassMessageNumber -= 10;
                    }
                    break;
            }

            trainerClassDescriptionMessageNumber = trainerClassMessageNumber + 1;
        }
        // US-version text archive numbers only; not yet confirmed for other localizations.
        private static void SetBattleTowerTextNumbers()
        {
            switch (gameFamily)
            {
                case GameFamilies.DP:
                    battleTowerTrainerNamesMessageNumber = 16;
                    battleTowerTrainerMessagesNumber = 555;
                    break;

                case GameFamilies.Plat:
                    battleTowerTrainerNamesMessageNumber = 21;
                    battleTowerTrainerMessagesNumber = 614;
                    break;

                case GameFamilies.HGSS:
                    battleTowerTrainerNamesMessageNumber = 27;
                    battleTowerTrainerMessagesNumber = 724;
                    break;
            }
        }
        private static void SetMoveTextNumbers()
        {
            bool jp = gameLanguage == GameLanguages.Japanese;
            switch (gameFamily)
            {
                case GameFamilies.DP:
                    moveDescriptionsTextNumbers = jp ? 574 : 587;
                    moveNamesTextNumbers = jp ? 575 : 588;
                    break;
                case GameFamilies.Plat:
                    moveDescriptionsTextNumbers = jp ? 635 : 646;
                    moveNamesTextNumbers = jp ? 636 : 647;
                    break;

                case GameFamilies.HGSS:
                    moveDescriptionsTextNumbers = jp ? 738 : 749;
                    moveNamesTextNumbers = jp ? 739 : 750;
                    break;
            }
        }

        private static void SetTrainerFunnyScriptNumber()
        {
            switch (gameFamily)
            {
                case GameFamilies.DP:
                    trainerFunnyScriptNumber = 851;
                    break;

                case GameFamilies.Plat:
                    trainerFunnyScriptNumber = 929;
                    break;

                default: // HGSS
                    trainerFunnyScriptNumber = 740;
                    break;
            }
        }

        private static void SetTypesTextNumber()
        {
            bool jp = gameLanguage == GameLanguages.Japanese;
            switch (gameFamily)
            {
                case GameFamilies.DP:
                    typesTextNumber = jp ? 555 : 565;
                    break;
                case GameFamilies.Plat:
                    typesTextNumber = jp ? 616 : 624;
                    break;
                case GameFamilies.HGSS:
                    typesTextNumber = jp ? 724 : 735;
                    break;
            }
        }

        private static void SetTrainerMessageTextNumber()
        {
            switch (gameFamily)
            {
                case GameFamilies.DP:
                    trainerMessageTextNumber = gameLanguage == GameLanguages.Japanese ? 549 : 558;
                    break;
                case GameFamilies.Plat:
                    trainerMessageTextNumber = gameLanguage == GameLanguages.Japanese ? 610 : 617;
                    break;
                case GameFamilies.HGSS:
                    trainerMessageTextNumber = gameLanguage == GameLanguages.Japanese ? 718 : 728;
                    break;
            }
        }

        private static void SetTrainerNameLenOffset()
        {
            switch (RomInfo.gameFamily)
            {
                case GameFamilies.DP:
                    switch (RomInfo.gameLanguage)
                    {
                        case GameLanguages.English:
                            trainerNameLenOffset = 0x6AC32;
                            break;

                        case GameLanguages.Italian:
                            trainerNameLenOffset = 0x6AC6E;
                            break;

                        case GameLanguages.Spanish:
                        case GameLanguages.German:
                        case GameLanguages.French:
                            trainerNameLenOffset = 0x6AC8E;
                            break;

                        case GameLanguages.Japanese: //?
                        default:
                            trainerNameLenOffset = -1;
                            break;
                    }
                    break;

                case GameFamilies.Plat:
                    switch (RomInfo.gameLanguage)
                    {
                        case GameLanguages.English:
                            trainerNameLenOffset = 0x791DE;
                            break;

                        case GameLanguages.Spanish:
                        case GameLanguages.Italian:
                        case GameLanguages.German:
                        case GameLanguages.French:
                            trainerNameLenOffset = 0x7927E;
                            break;

                        case GameLanguages.Japanese:
                            trainerNameLenOffset = 0x78AB6;
                            break;

                        default:
                            trainerNameLenOffset = -1;
                            break;
                    }
                    break;

                case GameFamilies.HGSS:
                    if (RomInfo.gameLanguage.Equals(GameLanguages.Japanese))
                    {
                        //Jap HGSS
                        trainerNameLenOffset = 0x7342E;
                    }
                    else if (gameVersion.Equals(GameVersions.SoulSilver))
                    {
                        //All SS languages except Jap
                        trainerNameLenOffset = 0x72EC2;
                    }
                    else
                    {
                        //All HG languages except Jap
                        switch (RomInfo.gameLanguage)
                        {
                            case GameLanguages.English:
                            case GameLanguages.Italian:
                            case GameLanguages.German:
                            case GameLanguages.French:
                                trainerNameLenOffset = 0x7342E;
                                break;

                            case GameLanguages.Spanish:
                                trainerNameLenOffset = 0x73426;
                                break;
                        }
                    }
                    break;
            }
        }

        public static int GetMachineMoveOffset()
        {
            switch (RomInfo.gameFamily)
            {
                case RomInfo.GameFamilies.DP:
                    switch (RomInfo.gameLanguage)
                    {
                        case RomInfo.GameLanguages.English:
                            return 0xF84EC;
                        case RomInfo.GameLanguages.Japanese:
                            return 0xFA458;
                        case RomInfo.GameLanguages.French:
                            return 0xF8530;
                        case RomInfo.GameLanguages.German:
                            return 0xF8500;
                        case RomInfo.GameLanguages.Italian:
                            return 0xF84A4;
                        case RomInfo.GameLanguages.Spanish:
                            return 0xF853C;
                        default:
                            return 0xF84EC;
                    }
                case RomInfo.GameFamilies.Plat:
                    switch (RomInfo.gameLanguage)
                    {
                        case RomInfo.GameLanguages.English:
                            return 0xF0BFC;
                        case RomInfo.GameLanguages.Japanese:
                            return 0xF028C;
                        case RomInfo.GameLanguages.French:
                            return 0xF0C84;
                        case RomInfo.GameLanguages.German:
                            return 0xF0C54;
                        case RomInfo.GameLanguages.Italian:
                            return 0xF0C18;
                        case RomInfo.GameLanguages.Spanish:
                            return 0xF0C90;
                        default:
                            return 0xF0BFC;
                    }
                case RomInfo.GameFamilies.HGSS:
                    switch (RomInfo.gameLanguage)
                    {
                        case RomInfo.GameLanguages.English:
                            return 0x1000CC;
                        case RomInfo.GameLanguages.Japanese:
                            return 0xFF84C;
                        case RomInfo.GameLanguages.French:
                            return 0x1000B0;
                        case RomInfo.GameLanguages.German:
                            return 0x100080;
                        case RomInfo.GameLanguages.Italian:
                            return 0x100044;
                        case RomInfo.GameLanguages.Spanish:
                            return 0x1000B4;
                        default:
                            return 0x1000CC;
                    }
                default:
                    AppLogger.Error("GetMachineMoveOffset: Unsupported game family.");
                    throw new NotImplementedException();
            }
        }

        public static uint GetItemTableOffset()
        {
            switch (RomInfo.gameFamily)
            {
                case RomInfo.GameFamilies.DP:
                    switch (RomInfo.gameLanguage)
                    {
                        case RomInfo.GameLanguages.English:
                            return 0xF85B4;
                        case RomInfo.GameLanguages.Japanese:
                            return 0xFA520;
                        case RomInfo.GameLanguages.French:
                            return 0xF85F8;
                        case RomInfo.GameLanguages.German:
                            return 0xF85C8;
                        case RomInfo.GameLanguages.Italian:
                            return 0xF856C;
                        case RomInfo.GameLanguages.Spanish:
                            return 0xF8604;
                        default:
                            return 0xF85B4;
                    }
                case RomInfo.GameFamilies.Plat:
                    switch (RomInfo.gameLanguage)
                    {
                        case RomInfo.GameLanguages.English:
                            return 0xF0CC4;
                        case RomInfo.GameLanguages.Japanese:
                            return 0xF0354;
                        case RomInfo.GameLanguages.French:
                            return 0xF0D4C;
                        case RomInfo.GameLanguages.German:
                            return 0xF0D1C;
                        case RomInfo.GameLanguages.Italian:
                            return 0xF0CE0;
                        case RomInfo.GameLanguages.Spanish:
                            return 0xF0D58;
                        default:
                            return 0xF0CC4;
                    }
                case RomInfo.GameFamilies.HGSS:
                    switch (RomInfo.gameLanguage)
                    {
                        case RomInfo.GameLanguages.English:
                            return 0x100194;
                        case RomInfo.GameLanguages.Japanese:
                            return 0xFF914;
                        case RomInfo.GameLanguages.French:
                            return 0x100178;
                        case RomInfo.GameLanguages.German:
                            return 0x100148;
                        case RomInfo.GameLanguages.Italian:
                            return 0x10010C;
                        case RomInfo.GameLanguages.Spanish:
                            return 0x10017C;
                        default:
                            return 0x100194;
                    }
                default:
                    AppLogger.Error("GetNarcTableOffset: Unsupported game");
                    throw new NotSupportedException("Game not supported");
            }
        }

        public static int GetEggMoveTableOffset()
        {
            switch (RomInfo.gameFamily)
            {
                case RomInfo.GameFamilies.DP:
                    switch (RomInfo.gameLanguage)
                    {
                        case RomInfo.GameLanguages.English:
                            return 0x20668;
                        case RomInfo.GameLanguages.Japanese:
                            return 0x21654;
                        case RomInfo.GameLanguages.French:
                            return 0x20620;
                        case RomInfo.GameLanguages.German:
                            return 0x20620;
                        case RomInfo.GameLanguages.Italian:
                            return 0x20620;
                        case RomInfo.GameLanguages.Spanish:
                            return 0x20620;
                        default:
                            return 0x20668;
                    }
                case RomInfo.GameFamilies.Plat:
                    switch (RomInfo.gameLanguage)
                    {
                        case RomInfo.GameLanguages.English:
                            return 0x29222;
                        case RomInfo.GameLanguages.Japanese:
                            return 0x29012;
                        case RomInfo.GameLanguages.French:
                            return 0x2922A;
                        case RomInfo.GameLanguages.German:
                            return 0x2923E;
                        case RomInfo.GameLanguages.Italian:
                            return 0x29232;
                        case RomInfo.GameLanguages.Spanish:
                            return 0x2922A;
                        default:
                            return 0x29222;
                    }
                default:
                    AppLogger.Error("GetEggMoveOffset: Unsupported game.");
                    throw new NotSupportedException("Game not supported");
            }
        }

        public static int SetTrainerNameMaxLen()
        {
            int maxLength = TrainerFile.defaultNameLen;
            if (trainerNameLenOffset > 0)
            {
                using (ARM9.Reader ar = new ARM9.Reader(trainerNameLenOffset))
                {
                    maxLength = ar.ReadByte();
                }
                maxLength += ((maxLength - 4) / 2);
            }
            return maxLength;
        }

        public static void SetAIBackportEnabled()
        {
            if (gameFamily != GameFamilies.Plat)
            {
                AIBackportEnabled = false;
                return;
            }

            byte[] bytesAtOffset = ARM9.ReadBytes(0x0793B8, 4);
            // Vanilla Plat USA is F8 B5 9A B0
            // Backport by is F0 B5 93 B0
            // The tutorial is only for the USA version, but it might be better to differentiate the different languages here
            AIBackportEnabled = bytesAtOffset.SequenceEqual(new byte[] { 0xF0, 0xB5, 0x93, 0xB0 });


            bytesAtOffset = ARM9.ReadBytes(0x0795A2, 4);
            // Original Backport by Lhea is 1D 1C 0F 23
            // Fixed Backport by YakoSWG is 1E 00 0F 24
            OutdatedAIBackportEnabled = bytesAtOffset.SequenceEqual(new byte[] { 0x1D, 0x1C, 0x0F, 0x23 });
        }

        public string GetBuildingModelsDirPath(bool interior) => interior ? gameDirs[DirNames.interiorBuildingModels].unpackedDir : gameDirs[DirNames.exteriorBuildingModels].unpackedDir;

        public string GetRomNameFromWorkdir() => workDir.Substring(0, workDir.Length - folderSuffix.Length - 1);

        public static int GetHeaderCount() => (int)new FileInfo(internalNamesPath).Length / internalNameLength;

        public static List<string> GetLocationNames() => new TextArchive(locationNamesTextNumber).messages;

        public static string[] GetSimpleTrainerNames() => new TextArchive(trainerNamesMessageNumber).GetSimpleTrainerNames().ToArray();

        public static string[] GetTrainerClassNames() => new TextArchive(trainerClassMessageNumber).messages.ToArray();

        public static string[] GetItemNames()
        {
            string[] names = new TextArchive(itemNamesTextNumber).messages.ToArray();
            HgEngine.HgEngineCustomLabel.ApplyItemLabel(names);
            return names;
        }

        public static string[] GetItemNames(int startIndex = 0, int? count = null)
        {
            TextArchive itemNames = new TextArchive(itemNamesTextNumber);
            return itemNames.messages.GetRange(startIndex, count == null ? itemNames.messages.Count - 1 : (int)count).ToArray();
        }

        public static string[] GetPokemonNames()
        {
            string[] names = new TextArchive(pokemonNamesTextNumbers[0]).messages.ToArray();
            HgEngine.HgEngineFormNames.ApplyFallback(names);
            HgEngine.HgEngineCustomLabel.ApplySpeciesLabel(names);
            return names;
        }

        public static string[] GetAbilityNames() => new TextArchive(abilityNamesTextNumber).messages.ToArray();

        public static string[] GetAttackNames()
        {
            string[] names = new TextArchive(attackNamesTextNumber).messages.ToArray();
            HgEngine.HgEngineCustomLabel.ApplyMoveLabel(names);
            return names;
        }

        public static string[] GetTypeNames() => new TextArchive(typesTextNumber).messages.ToArray();

        public static int GetLearnsetFilesCount() => Directory.GetFiles(gameDirs[DirNames.learnsets].unpackedDir).Length;

        public static int GetPersonalFilesCount() => Directory.GetFiles(gameDirs[DirNames.personalPokeData].unpackedDir).Length;

        public static string[] GetEvolutionFilesList() => Directory.GetFiles(gameDirs[DirNames.evolutions].unpackedDir);

        public static int GetEvolutionFilesCount() => GetEvolutionFilesList().Length;
        public static string[] GetBattleEffectSequenceFiles() => Directory.GetFiles(gameDirs[DirNames.moveData].unpackedDir);
        public static int GetBattleEffectSequenceFilesCount() => GetBattleEffectSequenceFiles().Length;

        public int GetAreaDataCount() => Directory.GetFiles(gameDirs[DirNames.areaData].unpackedDir).Length;

        public int GetMapTexturesCount() => Directory.GetFiles(gameDirs[DirNames.mapTextures].unpackedDir).Length;

        public int GetBuildingTexturesCount() => Directory.GetFiles(gameDirs[DirNames.buildingTextures].unpackedDir).Length;

        public int GetMatrixCount() => Directory.GetFiles(gameDirs[DirNames.matrices].unpackedDir).Length;

        public int GetTextArchivesCount() => Directory.GetFiles(TextConverter.GetExpandedFolderPath()).Length;

        public int GetMapCount() => Directory.GetFiles(gameDirs[DirNames.maps].unpackedDir).Length;

        public int GetEventCount() => Directory.GetFiles(gameDirs[DirNames.eventFiles].unpackedDir).Length;

        public int GetScriptCount() => Directory.GetFiles(gameDirs[DirNames.scripts].unpackedDir).Length;

        public int GetBuildingCount(bool interior) => Directory.GetFiles(GetBuildingModelsDirPath(interior)).Length;

        public static int GetEventFileCount() => Directory.GetFiles(RomInfo.gameDirs[DirNames.eventFiles].unpackedDir).Length;

        #endregion Methods (22)

        #region System Methods

        private void LoadGameLanguage()
        {
            switch (romID)
            {
                case "ADAE":
                case "APAE":
                case "CPUE":
                case "IPKE":
                case "IPGE":
                    gameLanguage = GameLanguages.English;
                    break;

                case "ADAS":
                case "APAS":
                case "CPUS":
                case "IPKS":
                case "IPGS":
                case "LATA":
                    gameLanguage = GameLanguages.Spanish;
                    break;

                case "ADAI":
                case "APAI":
                case "CPUI":
                case "IPKI":
                case "IPGI":
                    gameLanguage = GameLanguages.Italian;
                    break;

                case "ADAF":
                case "APAF":
                case "CPUF":
                case "IPKF":
                case "IPGF":
                    gameLanguage = GameLanguages.French;
                    break;

                case "ADAD":
                case "APAD":
                case "CPUD":
                case "IPKD":
                case "IPGD":
                    gameLanguage = GameLanguages.German;
                    break;

                default:
                    gameLanguage = GameLanguages.Japanese;
                    break;
            }
        }

        private void LoadGameFamily()
        {
            switch (gameVersion)
            {
                case GameVersions.Diamond:
                case GameVersions.Pearl:
                    gameFamily = GameFamilies.DP;
                    break;

                case GameVersions.Platinum:
                    gameFamily = GameFamilies.Plat;
                    break;

                case GameVersions.HeartGold:
                case GameVersions.SoulSilver:
                    gameFamily = GameFamilies.HGSS;
                    break;
            }
        }

        private void SetNarcDirs()
        {
            Dictionary<DirNames, string> packedDirsDict = null;
            switch (gameFamily)
            {
                case GameFamilies.DP:
                    string suffix = "";
                    if (!gameLanguage.Equals(GameLanguages.Japanese))
                    {
                        suffix = "_release";
                    }

                    packedDirsDict = new Dictionary<DirNames, string>()
                    {
                        [DirNames.synthOverlay] = $@"{dataFolderName}\data\weather_sys.narc",
                        [DirNames.textArchives] = $@"{dataFolderName}\msgdata\msg.narc",

                        [DirNames.matrices] = $@"{dataFolderName}\fielddata\mapmatrix\map_matrix.narc",

                        [DirNames.maps] = $@"{dataFolderName}\fielddata\land_data\land_data" + suffix + ".narc",
                        [DirNames.exteriorBuildingModels] = $@"{dataFolderName}\fielddata\build_model\build_model.narc",
                        [DirNames.buildingConfigFiles] = $@"{dataFolderName}\fielddata\areadata\area_build_model\area_build.narc",
                        [DirNames.buildingTextures] = $@"{dataFolderName}\fielddata\areadata\area_build_model\areabm_texset.narc",
                        [DirNames.mapTextures] = $@"{dataFolderName}\fielddata\areadata\area_map_tex\map_tex_set.narc",
                        [DirNames.areaData] = $@"{dataFolderName}\fielddata\areadata\area_data.narc",

                        [DirNames.eventFiles] = $@"{dataFolderName}\fielddata\eventdata\zone_event" + suffix + ".narc",
                        [DirNames.OWSprites] = $@"{dataFolderName}\data\mmodel\mmodel.narc",

                        [DirNames.scripts] = $@"{dataFolderName}\fielddata\script\scr_seq" + suffix + ".narc",

                        [DirNames.trainerProperties] = $@"{dataFolderName}\poketool\trainer\trdata.narc",
                        [DirNames.trainerParty] = $@"{dataFolderName}\poketool\trainer\trpoke.narc",
                        [DirNames.trainerGraphics] = $@"{dataFolderName}\poketool\trgra\trfgra.narc",
                        [DirNames.moveData] = $@"{dataFolderName}\poketool\waza\waza_tbl.narc",

                        [DirNames.monIcons] = $@"{dataFolderName}\poketool\icongra\poke_icon.narc",

                        [DirNames.encounters] = $@"{dataFolderName}\fielddata\encountdata\" + char.ToLower(gameVersion.ToString()[0]) + '_' + "enc_data.narc",
                        [DirNames.encounterExtended] = $@"{dataFolderName}\arc\encdata_ex.narc",
                        [DirNames.learnsets] = $@"{dataFolderName}\poketool\personal\wotbl.narc",
                        [DirNames.evolutions] = $@"{dataFolderName}\poketool\personal\evo.narc",

                        [DirNames.battleTowerTrainers] = $@"{dataFolderName}\battle\b_tower\btdtr.narc",
                        [DirNames.battleTowerPokemon] = $@"{dataFolderName}\battle\b_tower\btdpm.narc",

                        [DirNames.pokemonBattleSprites] = $@"{dataFolderName}\poketool\pokegra\pokegra.narc",
                        [DirNames.otherPokemonBattleSprites] = $@"{dataFolderName}\poketool\pokegra\otherpoke.narc",

                        // DP keeps the battle-sprite offsets in separate NARCs.
                        [DirNames.pokeYofs] = $@"{dataFolderName}\poketool\pokegra\poke_yofs.narc",
                        [DirNames.pokeShadowOfx] = $@"{dataFolderName}\poketool\pokegra\poke_shadow_ofx.narc",
                        [DirNames.pokeShadow] = $@"{dataFolderName}\poketool\pokegra\poke_shadow.narc",
                        [DirNames.pokeHeight] = $@"{dataFolderName}\poketool\pokegra\height.narc",
                        [DirNames.pokeHeightForms] = $@"{dataFolderName}\poketool\pokegra\height_o.narc",
                        [DirNames.pokeAnim] = $@"{dataFolderName}\poketool\pokeanm\pokeanm.narc",
                        [DirNames.pokeAnimDefs] = $@"{dataFolderName}\pokeanime\poke_anm.narc",
                        [DirNames.wazaSeq] = $@"{dataFolderName}\battle\skill\waza_seq.narc",
                        [DirNames.subSeq] = $@"{dataFolderName}\battle\skill\sub_seq.narc",
                        [DirNames.beSeq] = $@"{dataFolderName}\battle\skill\be_seq.narc",
                        [DirNames.wazaEffectScripts] = $@"{dataFolderName}\wazaeffect\we.arc",
                        [DirNames.wazaEffectSub] = $@"{dataFolderName}\wazaeffect\we_sub.narc",
                        [DirNames.wazaEffectChar] = $@"{dataFolderName}\wazaeffect\effectclact\wechar.narc",
                        [DirNames.wazaEffectPltt] = $@"{dataFolderName}\wazaeffect\effectclact\wepltt.narc",
                        [DirNames.wazaEffectCell] = $@"{dataFolderName}\wazaeffect\effectclact\wecell.narc",
                        [DirNames.wazaEffectCellAnm] = $@"{dataFolderName}\wazaeffect\effectclact\wecellanm.narc",
                        [DirNames.wazaParticle] = $@"{dataFolderName}\wazaeffect\effectdata\waza_particle.narc",
                        [DirNames.battleBg] = $@"{dataFolderName}\battle\graphic\batt_bg.narc",

                        [DirNames.itemData] = $@"{dataFolderName}\itemtool\itemdata\item_data.narc",
                        [DirNames.itemIcons] = $@"{dataFolderName}\itemtool\itemdata\item_icon.narc",

                        [DirNames.tradeData] = $@"{dataFolderName}\fielddata\pokemon_trade\fld_trade.narc",

                        [DirNames.trainerTextOffset] = $@"{dataFolderName}\poketool\trmsg\trtblofs.narc",
                        [DirNames.trainerTextTable] = $@"{dataFolderName}\poketool\trmsg\trtbl.narc",

                        [DirNames.eggMoves] = $@"{customNarcFolderName}/egg_moves.narc",
                    };

                    //Personal Data archive is different for Pearl
                    string personal = $@"{dataFolderName}\poketool\personal";
                    if (gameVersion == GameVersions.Pearl)
                    {
                        personal += ("_" + gameVersion.ToString().ToLower());
                    }
                    personal += @"\personal.narc";
                    packedDirsDict[DirNames.personalPokeData] = personal;

                    if (gameLanguage != GameLanguages.Japanese && gameLanguage != GameLanguages.English)
                    {
                        packedDirsDict[DirNames.tradeData] = $@"{dataFolderName}\resource\{GetLangResFolderName()}\pokemon_trade\fld_trade.narc";
                    }

                    break;

                case GameFamilies.Plat:
                    suffix = gameVersion.ToString().Substring(0, 2).ToLower();

                    packedDirsDict = new Dictionary<DirNames, string>()
                    {
                        [DirNames.personalPokeData] = $@"{dataFolderName}\poketool\personal\pl_personal.narc",

                        [DirNames.pokemonBattleSprites] = $@"{dataFolderName}\poketool\pokegra\pl_pokegra.narc",
                        [DirNames.otherPokemonBattleSprites] = $@"{dataFolderName}\poketool\pokegra\pl_otherpoke.narc",
                        // Platinum bundles the battle-sprite offsets (last 3 bytes/record = sprite Y, shadow X, shadow size) here, HGSS-style.
                        [DirNames.pokemonSpriteOffsets] = $@"{dataFolderName}\poketool\poke_edit\pl_poke_data.narc",
                        // Per-gender sprite heights live in their own NARC on Platinum too (same as DP).
                        [DirNames.pokeHeight] = $@"{dataFolderName}\poketool\pokegra\height.narc",
                        [DirNames.pokeHeightForms] = $@"{dataFolderName}\poketool\pokegra\height_o.narc",
                        [DirNames.pokeAnim] = $@"{dataFolderName}\poketool\pokeanm\pl_pokeanm.narc",   // Platinum's animation table (pl_ prefix)
                        [DirNames.pokeAnimDefs] = $@"{dataFolderName}\pokeanime\pl_poke_anm.narc",   // Platinum's PAST program-animation scripts
                        // Platinum keeps the move-sequence + effect scripts under shared (non-pl_) names.
                        [DirNames.wazaSeq] = $@"{dataFolderName}\battle\skill\waza_seq.narc",
                        [DirNames.subSeq] = $@"{dataFolderName}\battle\skill\sub_seq.narc",
                        [DirNames.beSeq] = $@"{dataFolderName}\battle\skill\be_seq.narc",
                        [DirNames.wazaEffectScripts] = $@"{dataFolderName}\wazaeffect\we.arc",
                        [DirNames.wazaEffectSub] = $@"{dataFolderName}\wazaeffect\we_sub.narc",
                        [DirNames.wazaEffectChar] = $@"{dataFolderName}\wazaeffect\effectclact\wechar.narc",
                        [DirNames.wazaEffectPltt] = $@"{dataFolderName}\wazaeffect\effectclact\wepltt.narc",
                        [DirNames.wazaEffectCell] = $@"{dataFolderName}\wazaeffect\effectclact\wecell.narc",
                        [DirNames.wazaEffectCellAnm] = $@"{dataFolderName}\wazaeffect\effectclact\wecellanm.narc",
                        [DirNames.wazaParticle] = $@"{dataFolderName}\wazaeffect\effectdata\waza_particle.narc",
                        [DirNames.battleBg] = $@"{dataFolderName}\battle\graphic\pl_batt_bg.narc",
                        [DirNames.battleObj] = $@"{dataFolderName}\battle\graphic\pl_batt_obj.narc",
                        [DirNames.trainerCardGraphics] = $@"{dataFolderName}\graphic\trainer_case.narc",

                        [DirNames.synthOverlay] = $@"{dataFolderName}\data\weather_sys.narc",
                        [DirNames.dynamicHeaders] = $@"{dataFolderName}\debug\cb_edit\d_test.narc",

                        [DirNames.textArchives] = $@"{dataFolderName}\msgdata\" + suffix + '_' + "msg.narc",

                        [DirNames.matrices] = $@"{dataFolderName}\fielddata\mapmatrix\map_matrix.narc",

                        [DirNames.maps] = $@"{dataFolderName}\fielddata\land_data\land_data.narc",
                        [DirNames.exteriorBuildingModels] = $@"{dataFolderName}\fielddata\build_model\build_model.narc",
                        [DirNames.buildingConfigFiles] = $@"{dataFolderName}\fielddata\areadata\area_build_model\area_build.narc",
                        [DirNames.buildingTextures] = $@"{dataFolderName}\fielddata\areadata\area_build_model\areabm_texset.narc",
                        [DirNames.mapTextures] = $@"{dataFolderName}\fielddata\areadata\area_map_tex\map_tex_set.narc",
                        [DirNames.areaData] = $@"{dataFolderName}\fielddata\areadata\area_data.narc",

                        [DirNames.eventFiles] = $@"{dataFolderName}\fielddata\eventdata\zone_event.narc",
                        [DirNames.OWSprites] = $@"{dataFolderName}\data\mmodel\mmodel.narc",

                        [DirNames.scripts] = $@"{dataFolderName}\fielddata\script\scr_seq.narc",

                        [DirNames.trainerProperties] = $@"{dataFolderName}\poketool\trainer\trdata.narc",
                        [DirNames.trainerParty] = $@"{dataFolderName}\poketool\trainer\trpoke.narc",
                        [DirNames.trainerGraphics] = $@"{dataFolderName}\poketool\trgra\trfgra.narc",
                        [DirNames.moveData] = $@"{dataFolderName}\poketool\waza\pl_waza_tbl.narc",

                        [DirNames.monIcons] = $@"{dataFolderName}\poketool\icongra\pl_poke_icon.narc",

                        [DirNames.encounters] = $@"{dataFolderName}\fielddata\encountdata\" + suffix + '_' + "enc_data.narc",
                        [DirNames.encounterExtended] = $@"{dataFolderName}\arc\encdata_ex.narc",
                        [DirNames.learnsets] = $@"{dataFolderName}\poketool\personal\wotbl.narc",
                        [DirNames.evolutions] = $@"{dataFolderName}\poketool\personal\evo.narc",

                        [DirNames.battleTowerTrainers] = $@"{dataFolderName}\battle\b_pl_tower\pl_btdtr.narc",
                        [DirNames.battleTowerPokemon] = $@"{dataFolderName}\battle\b_pl_tower\pl_btdpm.narc",

                        [DirNames.itemData] = $@"{dataFolderName}\itemtool\itemdata\pl_item_data.narc",
                        [DirNames.itemIcons] = $@"{dataFolderName}\itemtool\itemdata\item_icon.narc",

                        [DirNames.tradeData] = $@"{dataFolderName}\fielddata\pokemon_trade\fld_trade.narc",

                        [DirNames.trainerTextOffset] = $@"{dataFolderName}\poketool\trmsg\trtblofs.narc",
                        [DirNames.trainerTextTable] = $@"{dataFolderName}\poketool\trmsg\trtbl.narc",

                        [DirNames.eggMoves] = $@"{customNarcFolderName}/egg_moves.narc",
                    };

                    if (gameLanguage != GameLanguages.Japanese && gameLanguage != GameLanguages.English)
                    {
                        packedDirsDict[DirNames.tradeData] = $@"{dataFolderName}\resource\{GetLangResFolderName()}\pokemon_trade\fld_trade.narc";
                    }

                    break;

                case GameFamilies.HGSS:
                    packedDirsDict = new Dictionary<DirNames, string>()
                    {
                        [DirNames.personalPokeData] = $@"{dataFolderName}\a\0\0\2",
                        [DirNames.pokemonBattleSprites] = $@"{dataFolderName}\a\0\0\4",
                        [DirNames.otherPokemonBattleSprites] = $@"{dataFolderName}\a\1\1\4",
                        [DirNames.pokemonSpriteOffsets] = $@"{dataFolderName}\a\1\8\0",
                        [DirNames.pokeHeight] = $@"{dataFolderName}\a\0\0\5",
                        [DirNames.pokeAnim] = $@"{dataFolderName}\a\1\1\1",   // Pokeanm.narc (28 B/mon POKE_ANM_DATA table)
                        [DirNames.pokeAnimDefs] = $@"{dataFolderName}\a\0\9\0",   // PAST program-animation scripts (poke_anm equivalent)
                        [DirNames.wazaSeq] = $@"{dataFolderName}\a\0\0\0",   // move-sequence scripts (waza_seq)
                        [DirNames.subSeq] = $@"{dataFolderName}\a\0\0\1",   // shared subroutines (sub_seq)
                        [DirNames.beSeq] = $@"{dataFolderName}\a\0\3\0",   // per-effect scripts (be_seq)
                        [DirNames.wazaEffectScripts] = $@"{dataFolderName}\a\0\1\0",   // move animation (WEST we.arc equivalent)
                        [DirNames.wazaEffectSub] = $@"{dataFolderName}\a\0\6\1",   // continuous animations (we_sub)
                        [DirNames.wazaEffectChar] = $@"{dataFolderName}\a\0\2\2",   // wechar (NCGR)
                        [DirNames.wazaEffectPltt] = $@"{dataFolderName}\a\0\2\3",   // wepltt (NCLR)
                        [DirNames.wazaEffectCell] = $@"{dataFolderName}\a\0\2\4",   // wecell (NCER)
                        [DirNames.wazaEffectCellAnm] = $@"{dataFolderName}\a\0\2\5",   // wecellanm (NANR)
                        [DirNames.wazaParticle] = $@"{dataFolderName}\a\0\2\9",   // waza_particle (SPA)
                        [DirNames.battleBg] = $@"{dataFolderName}\a\0\0\7",   // ARC_BATT_BG (battle backgrounds + HAIKEI scroll BGs)
                        [DirNames.battleObj] = $@"{dataFolderName}\a\0\0\8",   // ARC_BATT_OBJ (battle OBJ / terrain ground platforms)
                        [DirNames.battleBgPlanm] = $@"{dataFolderName}\a\0\0\9",   // ARC_BATT_BG_PLANM (HGSS-only WEST_HAIKEI_CHG_EX anim data)

                        [DirNames.battleTowerTrainers] = $@"{dataFolderName}\a\2\0\2",
                        [DirNames.battleTowerPokemon] = $@"{dataFolderName}\a\2\0\3",

                        [DirNames.synthOverlay] = $@"{dataFolderName}\a\0\2\8",
                        [DirNames.dynamicHeaders] = $@"{dataFolderName}\a\0\5\0",

                        [DirNames.textArchives] = $@"{dataFolderName}\a\0\2\7",

                        [DirNames.matrices] = $@"{dataFolderName}\a\0\4\1",

                        [DirNames.maps] = $@"{dataFolderName}\a\0\6\5",
                        [DirNames.exteriorBuildingModels] = $@"{dataFolderName}\a\0\4\0",
                        [DirNames.buildingConfigFiles] = $@"{dataFolderName}\a\0\4\3",
                        [DirNames.buildingTextures] = $@"{dataFolderName}\a\0\7\0",
                        [DirNames.mapTextures] = $@"{dataFolderName}\a\0\4\4",
                        [DirNames.areaData] = $@"{dataFolderName}\a\0\4\2",

                        [DirNames.eventFiles] = $@"{dataFolderName}\a\0\3\2",
                        [DirNames.OWSprites] = $@"{dataFolderName}\a\0\8\1",

                        [DirNames.scripts] = $@"{dataFolderName}\a\0\1\2",
                        //ENCOUNTERS FOLDER DEPENDS ON VERSION
                        [DirNames.trainerProperties] = $@"{dataFolderName}\a\0\5\5",
                        [DirNames.trainerParty] = $@"{dataFolderName}\a\0\5\6",
                        [DirNames.trainerGraphics] = $@"{dataFolderName}\a\0\5\8",
                        [DirNames.moveData] = $@"{dataFolderName}\a\0\1\1",

                        [DirNames.monIcons] = $@"{dataFolderName}\a\0\2\0",

                        [DirNames.interiorBuildingModels] = $@"{dataFolderName}\a\1\4\8",
                        [DirNames.learnsets] = $@"{dataFolderName}\a\0\3\3",
                        [DirNames.evolutions] = $@"{dataFolderName}\a\0\3\4",
                        [DirNames.itemData] = $@"{dataFolderName}\a\0\1\7",
                        [DirNames.itemIcons] = $@"{dataFolderName}\a\0\1\8",
                        [DirNames.tradeData] = $@"{dataFolderName}\a\1\1\2",

                        [DirNames.safariZone] = $@"{dataFolderName}\a\2\3\0",
                        [DirNames.headbutt] = $@"{dataFolderName}\a\2\5\2", //both versions use the same folder with different data
                        [DirNames.rockSmash] = $@"{dataFolderName}\a\2\5\3", //odds+table-type per header; both versions use the same folder with different data

                        [DirNames.trainerTextOffset] = $@"{dataFolderName}\a\1\3\1",
                        [DirNames.trainerTextTable] = $@"{dataFolderName}\a\0\5\7",

                        [DirNames.eggMoves] = $@"{dataFolderName}\a\2\2\9",

                        [DirNames.dungeonCutinGraphics] = $@"{dataFolderName}\a\1\5\0",
                        [DirNames.titleScreenGraphics] = $@"{dataFolderName}\a\0\4\6",
                        [DirNames.trainerCardGraphics] = $@"{dataFolderName}\a\0\4\9"
                    };

                    //Encounter archive is different for SS
                    packedDirsDict[DirNames.encounters] = gameVersion == GameVersions.HeartGold ? $@"{dataFolderName}\a\0\3\7" : $@"{dataFolderName}\a\1\3\6";
                    break;
            }

            gameDirs = new Dictionary<DirNames, (string packedDir, string unpackedDir)>();
            foreach (KeyValuePair<DirNames, string> kvp in packedDirsDict)
            {
                // The NARC path literals use '\' — normalize so they resolve on non-Windows too.
                string packedDir = Path.Combine(workDir, kvp.Value.Replace('\\', Path.DirectorySeparatorChar));
                string unpackedDir = Path.Combine(workDir, "unpacked", kvp.Key.ToString());
                gameDirs.Add(kvp.Key, (packedDir, unpackedDir));
            }
        }

        public static string GetLangResFolderName()
        {
            switch (gameLanguage)
            {
                case GameLanguages.English:
                    return "eng";
                case GameLanguages.Italian:
                    return "ita";
                case GameLanguages.French:
                    return "fra";
                case GameLanguages.German:
                    return "ger";
                case GameLanguages.Spanish:
                    return "spa";
                default:
                    return "";
            }
        }

        public void ResetMapCellsColorDictionary()
        {
            switch (gameFamily)
            {
                case GameFamilies.DP:
                case GameFamilies.Plat:
                    MapCellsColorDictionary = PokeDatabase.System.MatrixCellColors.DPPtmatrixColorsDict;
                    break;

                case GameFamilies.HGSS:
                    MapCellsColorDictionary = PokeDatabase.System.MatrixCellColors.HGSSmatrixColorsDict;
                    break;
            }
        }

        public static void ReadOWTable()
        {
            OverworldTable = new SortedDictionary<uint, (uint spriteID, ushort properties)>();
            switch (gameFamily)
            {
                case GameFamilies.Plat when OverworldSpriteTableExpansion.Detect():
                case GameFamilies.DP when OverworldSpriteTableExpansion.Detect():
                    // Expansion patch applied: the vanilla fixed-offset read below is stale, the game
                    // reads the relocated table instead, which also holds any custom entries added.
                    OverworldTable = OverworldSpriteTableExpansion.ReadTextureTable();
                    break;

                case GameFamilies.DP:
                case GameFamilies.Plat:
                    using (BinaryReader idReader = new BinaryReader(new FileStream(OWtablePath, FileMode.Open)))
                    {
                        idReader.BaseStream.Position = OWTableOffset;

                        uint entryID = idReader.ReadUInt32();
                        idReader.BaseStream.Position -= 4;
                        while ((entryID = idReader.ReadUInt32()) != 0xFFFF)
                        {
                            uint spriteID = idReader.ReadUInt32();
                            (uint spriteID, ushort properties) tup = (spriteID, 0x0000);
                            OverworldTable.Add(entryID, tup);
                        }
                    }
                    break;

                case GameFamilies.HGSS:
                    using (BinaryReader idReader = new BinaryReader(new FileStream(OWtablePath, FileMode.Open)))
                    {
                        idReader.BaseStream.Position = OWTableOffset;

                        ushort entryID = idReader.ReadUInt16();
                        idReader.BaseStream.Position -= 2;
                        while ((entryID = idReader.ReadUInt16()) != 0xFFFF)
                        {
                            uint spriteID = idReader.ReadUInt16();
                            ushort properties = idReader.ReadUInt16();
                            (uint spriteID, ushort properties) tup = (spriteID, properties);
                            OverworldTable.Add(entryID, tup);
                        }
                    }
                    break;
            }
            foreach (uint k in ow3DSpriteDict.Keys)
            {
                OverworldTable.Add(k, (0x3D3D, 0x3D3D)); //ADD 3D overworld data (spriteID and properties are dummy values)
            }
            overworldTableKeys = OverworldTable.Keys.ToArray();
        }

        /// <summary>
        /// Checks if the Item Table Editor is available for the current ROM version.
        /// Item Table Editor requires at least pickup table support, or (HGSS) the Rock Smash tab.
        /// </summary>
        /// <returns>True if the editor is available, false otherwise</returns>
        public static bool IsItemTableEditorAvailable()
        {
            return pickupTableOverlayNumber >= 0 || gameFamily == GameFamilies.HGSS;
        }

        /// <summary>
        /// Checks if Hidden Items editor is available for the current ROM version.
        /// Currently only HeartGold US is supported.
        /// </summary>
        /// <returns>True if hidden items editor is available, false otherwise</returns>
        public static bool IsHiddenItemsEditorAvailable()
        {
            // Hidden items is only available for HeartGold US
            return gameVersion == GameVersions.HeartGold && gameLanguage == GameLanguages.English;
        }

        /// <summary>
        /// Checks if the Rock Smash per-header odds/table editor is available. This is plain NARC data
        /// (data/a/2/5/3), so it works for HGSS regardless of language.
        /// </summary>
        public static bool IsRockSmashEditorAvailable()
        {
            return gameFamily == GameFamilies.HGSS;
        }

        /// <summary>
        /// Checks if the Rock Smash item-drop tables (the 3 hardcoded 8-slot tables in ov001.bin,
        /// offsets 0x23D04/0x23D14/0x23D24 per https://ds-pokemon-hacking.github.io/docs/generation-iv/guides/hgss-rock_smash/)
        /// are safe to edit. Only the English build's offsets are confirmed; gate to that until other
        /// languages are verified, same convention as <see cref="IsHiddenItemsEditorAvailable"/>.
        /// </summary>
        public static bool IsRockSmashItemTableAvailable()
        {
            return gameFamily == GameFamilies.HGSS && gameLanguage == GameLanguages.English;
        }

        /// <summary>
        /// HGSS only (Diamond/Pearl/Platinum have no equivalent system), English and Spanish only,
        /// the only revisions with confirmed offsets. Same gating convention as
        /// <see cref="IsHiddenItemsEditorAvailable"/> and <see cref="IsRockSmashItemTableAvailable"/>.
        /// </summary>
        public static bool IsDungeonCutinEditorAvailable()
        {
            return gameFamily == GameFamilies.HGSS &&
                (gameLanguage == GameLanguages.English || gameLanguage == GameLanguages.Spanish);
        }

        public static bool IsTitleScreenEditorAvailable() => gameFamily == GameFamilies.HGSS;

        /// <summary>
        /// Member indices of the title logo/palette/background inside a/0/4/6 for a specific game version.
        /// That archive carries both HeartGold's and SoulSilver's sets in the same file, so both are always
        /// editable regardless of which one the loaded ROM actually is. The logo and background are each
        /// backed by a real NSCR (confirmed against titledemo.naix): logoNscr is shared between HeartGold
        /// and SoulSilver, backgroundNscr is per-version like the NCGR/NCLR pair.
        /// </summary>
        public static (int logo, int palette, int background, int logoNscr, int backgroundNscr) TitleScreenMembersFor(GameVersions version) =>
            version == GameVersions.HeartGold ? (3, 4, 34, 0, 35) : (1, 2, 36, 0, 37);

        /// <summary>Same as <see cref="TitleScreenMembersFor"/>, defaulting to the currently loaded ROM's version.</summary>
        public static (int logo, int palette, int background, int logoNscr, int backgroundNscr) TitleScreenMembers =>
            TitleScreenMembersFor(gameVersion);

        /// <summary>
        /// Member indices of the title screen's copyright text strip inside a/0/4/6. Unlike the logo and
        /// background, there is only one copy in the archive (confirmed via titledemo.naix's single
        /// title_cpright_* entry), shared between HeartGold and SoulSilver, with its own dedicated palette.
        /// </summary>
        public static (int ncgr, int nclr, int nscr) TitleScreenCopyrightMembers => (15, 16, 17);

        public static bool IsTrainerCardEditorAvailable() =>
            gameFamily == GameFamilies.HGSS || gameFamily == GameFamilies.Plat;

        // Shared NCGR + front/back NSCR; rankPalettes are the 7 selectable NCLRs (Normal/Bronze/Kap/
        // Silver/Gold/Black/no-Pokédex). HGSS and Platinum are separate archives, not shared.
        public static (int ncgr, int facaNscr, int backNscr, int[] rankPalettes) TrainerCardMembers =>
            gameFamily == GameFamilies.Plat
                ? (27, 35, 36, new[] { 0, 1, 2, 3, 4, 5, 6 })
                : (41, 47, 48, new[] { 0, 1, 2, 3, 4, 5, 6 });

        public static readonly string[] TrainerCardRankNames =
            { "Normal", "Bronze", "Kap", "Silver", "Gold", "Black", "No Pokédex" };

        // Shared NCGR + one NSCR per gender; always uses rankPalettes[0] (Normal), matching the game.
        public static (int ncgr, int maleNscr, int femaleNscr) TrainerCardTrainerMembers =>
            gameFamily == GameFamilies.Plat ? (31, 40, 41) : (44, 54, 55);

        /// <summary>
        /// Checks if the Starter Pokémon editor is available for the current ROM. Offsets are known for
        /// DP/Plat/HGSS in English and every European language (all share the "(U)" offsets) plus Japanese.
        /// </summary>
        public static bool IsStarterEditorAvailable()
        {
            return gameFamily == GameFamilies.DP || gameFamily == GameFamilies.Plat || gameFamily == GameFamilies.HGSS;
        }

        /// <summary>
        /// Gets a display name for the current game version.
        /// </summary>
        /// <returns>Game display name (e.g., "Diamond (US)", "Platinum (US)", "HeartGold (US)")</returns>
        public static string GetGameDisplayName()
        {
            string gameName;
            switch (gameVersion)
            {
                case GameVersions.Diamond:
                    gameName = "Diamond";
                    break;
                case GameVersions.Pearl:
                    gameName = "Pearl";
                    break;
                case GameVersions.Platinum:
                    gameName = "Platinum";
                    break;
                case GameVersions.HeartGold:
                    gameName = "HeartGold";
                    break;
                case GameVersions.SoulSilver:
                    gameName = "SoulSilver";
                    break;
                default:
                    gameName = "Unknown";
                    break;
            }

            string languageName;
            switch (gameLanguage)
            {
                case GameLanguages.English:
                    languageName = "US";
                    break;
                case GameLanguages.Japanese:
                    languageName = "JP";
                    break;
                case GameLanguages.French:
                    languageName = "FR";
                    break;
                case GameLanguages.German:
                    languageName = "DE";
                    break;
                case GameLanguages.Italian:
                    languageName = "IT";
                    break;
                case GameLanguages.Spanish:
                    languageName = "ES";
                    break;
                default:
                    languageName = "??";
                    break;
            }

            return $"{gameName} ({languageName})";
        }

        #endregion System Methods
    }
}
