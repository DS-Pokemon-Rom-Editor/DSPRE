using DSPRE.HgEngine;
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
    /// <summary>Class to store ROM data from GEN IV Pokémon games</summary>

    public partial class RomInfo
    {
        public const string FolderSuffix = "_DSPRE_contents"; // changed back to public static string
        private static string _dataFolderName;
        private static string _customNarcFolderName;

        public static bool IsDsRomProject { get; internal set; }

        /// <summary>
        /// The open project is an hg-engine checkout's own extracted tree rather than a DSPRE project.
        /// It is the flat ndstool shape with its own names, and the build owns packing it.
        /// </summary>
        public static bool IsHgEngineBaseProject { get; internal set; }

        /// <summary>The base/ tree of a checkout that builds through ds-rom; its header, banner and overlay
        /// metadata live in this base_dsrom/ folder beside it. Null otherwise.</summary>
        public static string HgEngineDsRomMetaDir { get; private set; }

        /// <summary>Overlays on disk are always flat and the build compresses them, so DSPRE never compresses or
        /// decompresses one itself.</summary>
        public static bool OverlaysStayFlat => IsDsRomProject || HgEngineDsRomMetaDir != null;

        public static bool IsHGE { get; private set; }

        // UI-agnostic warning surface. The host sets this (WinForms → MessageBox, Avalonia → dialog); the default
        // just logs. Keeps RomInfo free of a direct System.Windows.Forms dependency so Avalonia can own ROM loading.
        public static Action<string, string> ShowWarning = (msg, title) => AppLogger.Error(title + ": " + msg);
        public static bool HasRotomProject { get; private set; }
        public static void RefreshRotomProjectState() => HasRotomProject =
            !string.IsNullOrWhiteSpace(DspreDir) && File.Exists(Path.Combine(DspreDir, "rotom.toml"));

        /// <summary>The checkout's .dspre/ folder for its base/ tree, created with a .gitignore that ignores it.</summary>
        private static string HgEngineDspreDirFor(string basePath)
        {
            try
            {
                string checkout = Path.GetDirectoryName(basePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (checkout == null) return null;
                string dir = Path.Combine(checkout, HgEngineDspreFolder);
                Directory.CreateDirectory(dir);
                string ignore = Path.Combine(dir, ".gitignore");
                if (!File.Exists(ignore)) PlainFiles.WriteText(ignore, "*\n");
                return dir + Path.DirectorySeparatorChar;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                AppLogger.Error("RomInfo.HgEngineDspreDirFor: " + ex.Message);
                return null;
            }
        }
        public static string RomID { get; private set; }

        /// <summary>The cartridge revision (header byte 0x1E), or -1 when unknown.</summary>
        public static int RomRevision { get; private set; } = -1;
        public static string ProjectName { get; private set; }
        public static string WorkDir { get; private set; }

        /// <summary>
        /// Where DSPRE keeps its own working folders (unpacked members, expanded sources, the Rotom project,
        /// labels), with a trailing separator. The project folder itself, except for an hg-engine checkout, whose
        /// base/ tree is deleted by make clean and every re-extract: there it is the checkout's .dspre/ folder.
        /// </summary>
        public static string DspreDir { get; private set; }

        public const string HgEngineDspreFolder = ".dspre";
        public static string Arm9Path { get; private set; }
        public static string Arm7Path { get; private set; }
        public static string OverlayTablePath { get; set; }
        public static string Y7Path { get; set; }
        public static string DataPath { get; set; }

        /// <summary>The field weather graphics, a named file in all three games.</summary>
        public static string WeatherSysNarcPath => DataPath == null ? null : Path.Combine(DataPath, "data", "weather_sys.narc");
        public static string OverlayPath { get; set; }
        public static string UnpackedPath { get; set; }
        public static string BannerPath { get; set; }
        public static string HeaderPath { get; set; }

        public static GameLanguages GameLanguage { get; private set; }
        public static GameVersions GameVersion { get; private set; }
        public static GameFamilies GameFamily { get; private set; }

        public static uint SynthOverlayLoadAddress = 0x023C8000;
        /// <summary>How much of the synthetic overlay the game loads.</summary>
        public const int SynthOverlaySize = 0x16000;
        /// <summary>The game's archive id for the synthetic overlay's NARC (weather_sys.narc in DP and Pt, a/0/2/8 in HGSS).</summary>
        public static byte SynthOverlayArchiveId => GameFamily == GameFamilies.HGSS ? (byte)28 : (byte)65;

        /// <summary>
        /// The ARM9 expansion's sites as ARM9 file offsets: the boot pair (movs r0, #0; movs r1, #3) it replaces with a call,
        /// where its loader goes, and the RAM address of the game's file loader the loader calls.
        /// </summary>
        public sealed record Arm9ExpansionSites(uint Boot, uint Loader, uint FileLoader);

        public static uint Arm9spawnOffset { get; private set; }

        public static int InitialMoneyOverlayNumber { get; private set; }
        public static uint InitialMoneyOverlayOffset { get; private set; }

        public static int CameraTblOverlayNumber { get; private set; }
        public static uint[] CameraTblOffsetsToRAMaddress { get; private set; }

        public static uint HeaderTableOffset { get; private set; }

        // Pickup Table offsets (in overlay file)
        public static int PickupTableOverlayNumber { get; private set; }
        public static uint PickupCommonItemsOffset { get; private set; }
        public static uint PickupRareItemsOffset { get; private set; }
        public static uint PickupActivationDivisorOffset { get; private set; }
        public static uint PickupWeightTableOffset { get; private set; }

        // Starter Pokémon table. Where the species, scene code and cries are is RomLocator's StarterSpecies, StarterScene,
        // StarterSceneInner and StarterCries.
        /// <summary>The overlay the starter editor rewrites: the selection scene on DP and Pt, the cries on HGSS; -1 when none.</summary>
        public static int StarterOverlayNumber =>
            (RomLocator.Where(RomLocator.Thing.StarterSpecies) is RomLocator.Spot species && species.Overlay >= 0
                ? species : RomLocator.Where(RomLocator.Thing.StarterCries))?.Overlay ?? -1;
        /// <summary>The bytes just before the selection scene's code, checked before it is rewritten. DP/Pt only.</summary>
        public static string StarterGraphicsPrefix { get; private set; }
        public static int[] StarterSceneSpeciesLiterals { get; private set; }    // Japanese DP only
        public static int StarterHeldItemScriptFileID { get; private set; } = -1; // DP/Pt only
        public static uint StarterHeldItemOffset { get; private set; }            // DP/Pt only

        // Where the starter's GivePokemon sits in an untouched ROM. Knowing this, the editor reads one
        // script instead of searching all 575, which is the difference between under a millisecond and
        // about 45. -1 means "no known slot, go and look".
        public static int StarterCommandScriptNumber { get; private set; } = -1;   // Plat only, for now
        public static int StarterCommandIndex { get; private set; } = -1;          // Plat only, for now
        public static int StarterScreenTextNumber { get; private set; } = -1;
        public static int StarterPokedexSpeciesTextNumber { get; private set; } = -1; // DP/Pt only

        // Both table offsets are fallbacks; RematchTable reads the address from the overlay itself.
        public static int VsSeekerRematchOverlayNumber { get; private set; } = -1;
        public static uint VsSeekerRematchTableOffset { get; private set; }
        public static int[] VsSeekerRematchPointerOffsets { get; private set; } = System.Array.Empty<int>();
        public static int VsSeekerRematchRowCompareOffset { get; private set; } = -1;
        public static int PokegearRematchOverlayNumber { get; private set; } = -1;
        public static uint PokegearRematchFallbackTableOffset { get; private set; }
        /// <summary>Pokégear phone strings, whose messages from 38 on are the special contact titles. -1 when unknown.</summary>
        public static int PokegearPhoneMessageArchive { get; private set; } = -1;

        /// <summary>The shared menu entries a script's standard-text menus read from. -1 when unknown.</summary>
        public static int FieldMenuEntriesArchive { get; private set; } = -1;

        /// <summary>The yes and no entries in <see cref="FieldMenuEntriesArchive"/>; -1 when unknown.</summary>
        public static int FieldYesEntry { get; private set; } = -1;
        public static int FieldNoEntry { get; private set; } = -1;

        /// <summary>The battle message archive; -1 when unknown.</summary>
        public static int BattleStringsTextNumber { get; private set; } = -1;

        /// <summary>The four archives GetStdMsgNaix picks between, in order. Empty when unknown.</summary>
        public static int[] FieldSharedMessageArchives { get; private set; } = System.Array.Empty<int>();

        /// <summary>The system font's palette in the font archive, which menu windows fill and write with. -1 when unknown.</summary>
        public static int SystemFontPaletteEntry { get; private set; } = -1;

        /// <summary>HGSS text bank the touch menu's labels are in. -1 when unknown.</summary>
        public static int FieldTouchMenuTextArchive { get; private set; } = -1;

        // Item Table offset (in ARM9)
        public static uint ItemTableOffset { get; private set; }
        /// <summary>
        /// ARM9 offset of the game's item loader (LoadItemDataOrGfx, Pt Item_Load), which clamps any id above
        /// its item limit to 0. Zero when unknown. Retail limits: DP 464, Pt 467, HGSS 536.
        /// </summary>
        public static uint ItemLimitSite { get; private set; }
        /// <summary>The loader's opening bytes as hex, ?? for the limit's own bytes. Checked before the limit is read.</summary>
        public static string ItemLimitSignature { get; private set; }

        /// <summary>The hidden-item table in ARM9 (gHiddenItems, HGSS sHiddenItemParam). Zero when unknown.</summary>
        public static uint HiddenItemTableOffset { get; private set; }
        /// <summary>Rows the ARM9 reserves for the hidden-item table (NUM_HIDDEN_ITEMS).</summary>
        public static int HiddenItemTableCapacity { get; private set; }
        /// <summary>The immediate byte of each <c>cmp rN, #count</c> that bounds a scan of the hidden-item table.</summary>
        public static uint[] HiddenItemCountSites { get; private set; } = System.Array.Empty<uint>();
        /// <summary>Platinum: the literal-pool words holding the hidden-item count, which no <c>cmp</c> immediate can hold.</summary>
        public static uint[] HiddenItemCountLiterals { get; private set; } = System.Array.Empty<uint>();
        /// <summary>
        /// The highest hidden-item script field with its own found flag: DP and Pt number those flags from 0x2DA up to the
        /// trainer flags at 0x550. HGSS: 255.
        /// </summary>
        public static int HiddenItemScriptMax => GameFamily is GameFamilies.DP or GameFamilies.Plat ? 0x550 - 0x2DA - 1 : 255;
        /// <summary>Platinum's hidden items that FieldSystem_ClearDailyHiddenItemFlags puts back each day (Iron Island,
        /// Floaroma Meadow honey), by script field; the same in every Platinum dump.</summary>
        public static int[] HiddenItemDailyScripts => GameFamily == GameFamilies.Plat
            ? new[] { 52, 53, 54, 55, 58, 59, 219, 220, 221, 222 } : System.Array.Empty<int>();

        /// <summary>Pt overlay 9's Distortion World Griseous Orb item, which names its ground-item script directly. Zero when unknown.</summary>
        public static uint DistortionWorldItemScriptOffset { get; private set; }

        // Standard Poké Mart tables in ARM9. Zero count means this ROM has no verified layout.
        public static uint MartCommonCountOffset { get; private set; }
        public static uint MartCommonPointerOffset { get; private set; }
        public static uint MartSpecialtyPointerOffset { get; private set; }
        public static int MartSpecialtyShopCount { get; private set; }

        // Read from the locator whenever asked, so no reader has to set them up first.
        public static uint ConditionalMusicTableOffsetToRAMAddress => (uint)(RomLocator.Where(RomLocator.Thing.ConditionalMusicPointer)?.Offset ?? 0);
        public static uint EncounterMusicTableOffsetToRAMAddress => (uint)(RomLocator.Where(RomLocator.Thing.EncounterMusicPointer)?.Offset ?? 0);
        /// <summary>The row count's compare immediate, read before the table's literal; Japanese DP's loop sits four bytes further back.</summary>
        public static uint EncounterMusicCountSite => EncounterMusicTableOffsetToRAMAddress
            - (GameFamily == GameFamilies.HGSS ? 12u : GameFamily == GameFamilies.DP && GameLanguage == GameLanguages.Japanese ? 14u : 10u);
        public static uint DungeonCutinTableOffsetToRAMAddress => (uint)(RomLocator.Where(RomLocator.Thing.DungeonCutinPointer)?.Offset ?? 0);
        /// <summary>
        /// HGSS: the Ruins of Alph, default and Cliff Cave Rock Smash item tables. SoulSilver keeps HeartGold's offsets with
        /// its own exclusive items.
        /// </summary>
        public static uint RockSmashItemTablesOffset => (uint)(RomLocator.Where(RomLocator.Thing.RockSmashItemTables)?.Offset ?? 0);

        public static uint VsTrainerEntryTableOffsetToRAMAddress => (uint)(RomLocator.Where(RomLocator.Thing.VsTrainerEntriesPointer)?.Offset ?? 0);
        public static uint VsPokemonEntryTableOffsetToRAMAddress => (uint)(RomLocator.Where(RomLocator.Thing.VsPokemonEntriesPointer)?.Offset ?? 0);
        public static uint EffectsComboTableOffsetToRAMAddress => (uint)(RomLocator.Where(RomLocator.Thing.BattleEffectCombosPointer)?.Offset ?? 0);
        /// <summary>Diamond, Pearl and Platinum also point two bytes into the combo table from here; it has to agree before the table is trusted.</summary>
        public static uint EffectsComboTableSecondPointerOffset => (uint)(RomLocator.Where(RomLocator.Thing.BattleEffectCombosSecondPointer)?.Offset ?? 0);

        // HGSS only: the compare that bounds each table, just before its literal.
        public static uint VsTrainerEntryTableOffsetToSizeLimiter => GameFamily == GameFamilies.HGSS ? VsTrainerEntryTableOffsetToRAMAddress - 0xA : 0;
        public static uint VsPokemonEntryTableOffsetToSizeLimiter => GameFamily == GameFamilies.HGSS ? VsPokemonEntryTableOffsetToRAMAddress - 0xA : 0;
        public static uint EffectsComboTableOffsetToSizeLimiter => GameFamily == GameFamilies.HGSS ? EffectsComboTableOffsetToRAMAddress - 0x1E : 0;

        public static uint OWTableOffset { get; internal set; }

        /// <summary>
        /// Japanese Diamond and Pearl share one fly table offset, but their header tables sit 4 bytes apart,
        /// so Pearl's is likely elsewhere; unchecked without a Japanese Pearl ROM.
        /// </summary>
        public static bool FlyTableUnverified => GameVersion == GameVersions.Pearl && GameLanguage == GameLanguages.Japanese;

        /// <summary>Where arm9 keeps the spawn table: blackout, fly and warp-unlock points per town.</summary>
        public static uint FlyTableOffset => (uint)(RomLocator.Where(RomLocator.Thing.FlyTable)?.Offset ?? 0);

        public static int FlyTableRows => GameFamily switch
        {
            GameFamilies.DP   => 20,
            GameFamilies.Plat => 20,
            GameFamilies.HGSS => 30,
            _                 => 20,
        };

        /// <summary>
        /// In a move list's overlay: the literal holding the list's address, the <c>cmp rN, #count</c> that bounds the
        /// walk over it, and how many entries the vanilla list has room for (its zero padding included).
        /// </summary>
        public sealed record MoveListSites(int PointerSite, int CountCompare, int VanillaCapacity);

        /// <summary>
        /// The battle overlay's HP bar text routines: the ldr before each image proxy call and the BLX of each text copy onto the bar.
        /// TileHooks and TileCopies do the same for the gender symbol, "Lv" (Diamond and Pearl only) and the caught ball (Platinum and
        /// HeartGold), which are copied as whole tiles.
        /// </summary>
        public sealed record HpBarTextSites(int Overlay, int[] Hooks, int[] Copies, int[] TileHooks, int[] TileCopies);

        /// <summary>
        /// Battle Point exchange code sites: Platinum's corner list pointers, overlay 7 price literal (the next one is +2)
        /// and row-count compare, plus both games' exchange command table and its <c>movs r1, #firstTmRow</c>.
        /// </summary>
        public sealed record BpShopSites(int ListPointers, int PriceLiteral, int PriceCountCompare,
            int ExchangeTable, int ExchangeSplit);

        /// <summary>
        /// One encounter method's `cmp r0, #boundary` sites per slot boundary in the selector's overlay; a boundary
        /// may be compared in two places.
        /// </summary>
        /// <param name="LastSlotSite">The last-slot compare's distance from the first compare (land only); Japanese
        /// Diamond and Pearl compare each boundary once more, which moves it.</param>
        public sealed record SlotOddsMethod(string Name, int Overlay, int Slots, int[][] Boundaries, int LastSlotSite = LandLastSlotSite);

        /// <summary>Land's `cmp r0, #x` then branch for the last two slots, relative to the land selector's first compare.</summary>
        public const int LandLastSlotSite = 0x72;

        /// <summary>
        /// The swarm destination table. HGSS rows are u16 header + u16 method; DP/Pt rows are a u32 header. The
        /// literals point at the table (HGSS also has one at +2) and the `movs r1, #count` sites give the row count.
        /// </summary>
        public sealed record SwarmSites(int Overlay, int Table, int Rows, int RowSize, int[] Literals, int[] LiteralsPlus2, int[] CountSites);

        /// <summary>
        /// The overlay holding the move-effect background table and its rows: five u32 battle background archive members
        /// per background id (pokeplatinum sBgNarcIndices in the battle animation overlay, pokeheartgold ov07_02234F48).
        /// Its place inside the overlay is found from the code that reads it.
        /// </summary>
        public static (int Overlay, int Rows) MoveBackgroundTableSite => GameFamily switch
        {
            GameFamilies.DP => (8, 58),
            GameFamilies.Plat => (12, 58),
            GameFamilies.HGSS => (7, 59),
            _ => (-1, 0),
        };

        /// <summary>
        /// Special trainer battle intros, as file offsets in the named binary, -1 where the game has none.
        /// DP/Pt pick an intro per class with an arm9 jump table; every game picks the intro's routine from a
        /// task table, and the gym, league and HGSS Rocket executive routines each read one record. The member
        /// numbers are the art the routines load by immediate. Every site is checked by content before use.
        /// </summary>
        public sealed record VsIntroSites(
            int ClassJumpTable, int ClassJumpCount,
            int TaskOverlay, int TaskTable, int TaskCount,
            int RecordOverlay, int GymTable, int GymCount, int GymSize, int RivalRecord, int LeagueTable,
            int ExecutiveOverlay, int ExecutiveTable,
            int ComboMusicLiteral, int ComboMusicCount,
            int[] ParticleSites, int[] EmitterSites,
            int VsMark, int LeagueFrame, int PlayerFaceBoy, int PlayerFaceGirl, int NamePalette, int GymBanner, int ExecutiveBackdrop);

        /// <summary>
        /// Data tables in the intro code: the six emblems the Team Rocket or Galactic grunt intro flies in (eight s32
        /// each), the column order of the black block wipe (eight bytes), and the emblem's palette, tiles, cells and
        /// animation in the encounter effect archive (null where unconfirmed). LeagueParticles: the League intro's fire and bar
        /// particle files, or null where the game has none.
        /// </summary>
        public sealed record VsIntroMotionSites(int FlightOverlay, int FlightTable, int BlockOverlay, int BlockOrder, int[] EmblemMembers, int[] LeagueParticles);

        /// <summary>
        /// How many cells a loaded matrix may hold: the game keeps every matrix in one fixed buffer (MAP_MATRIX_MAX_SIZE,
        /// 900 in DP and Platinum, 799 in HGSS) and indexes past it unchecked. HGSS's matrix expansion patch doubles it.
        /// </summary>
        public static int MatrixMaxCells(bool expansionPatched) => GameFamily == GameFamilies.HGSS ? (expansionPatched ? 799 * 2 : 799) : 900;

        private static int ReadRomRevision(string dir)
        {
            try
            {
                string yaml = Path.Combine(dir, "header.yaml");
                if (!File.Exists(yaml) && HgEngineDsRomMetaDir != null) yaml = Path.Combine(HgEngineDsRomMetaDir, "header.yaml");
                if (File.Exists(yaml)) return YamlUtils.ReadGameCodeFromHeaderYaml(yaml)?.revision ?? -1;
                string bin = Path.Combine(dir, "header.bin");
                if (File.Exists(bin))
                {
                    using FileStream f = File.OpenRead(bin);
                    if (f.Length > 0x1E) { f.Position = 0x1E; return f.ReadByte(); }
                }
            }
            catch (IOException) { }   // no readable header byte means the version is unknown
            return -1;
        }

        /// <summary>DP/Pt: the class switch picking each trainer class's battle music, as (arm9 table, entries), or null.</summary>
        public static (int Table, int Count)? ClassJumpSite =>
            RomLocator.VsIntroSites is VsIntroSites s && s.ClassJumpTable >= 0 ? (s.ClassJumpTable, s.ClassJumpCount) : null;
        /// <summary>
        /// The trainer class gender table in arm9, one byte per class (1 female), or 0 when unknown.
        /// </summary>
        public static uint TrainerClassGenderTableOffset => (uint)(RomLocator.Where(RomLocator.Thing.TrainerClassGenders)?.Offset ?? 0);
        /// <summary>
        /// The Pickup routine's range clamp (cmp rN, #10; bcc; movs rN, #9, never reached for levels 1 to 100), the register
        /// holding the range, and the literals pointing at the common and rare item lists, all in the pickup overlay.
        /// </summary>
        public sealed record PickupSlotSites(uint Clamp, int Register, uint CommonLiteral, uint RareLiteral);


        /// <summary>
        /// The Underground wall code in the mining table's overlay: the treasure count's random range and minimum
        /// (rand(range) + minimum, a 3-bit immediate), the first dig's count, the rock placement tries, and in Diamond and
        /// Pearl the orb bonus: two (row id compare, bonus add) pairs in the weight getter. Orb sites are 0 in Platinum.
        /// </summary>
        public sealed record MiningCodeSites(uint Range, uint Minimum, uint FirstDig, uint Tries,
            uint OrbRowA, uint OrbBonusA, uint OrbRowB, uint OrbBonusB);



        /// <summary>
        /// HGSS arm9 file offsets of the follower tables read by SpeciesToOverworldModelIndexOffset,
        /// OverworldModelLookupFormCount and OverworldModelLookupHasFemaleForm.
        /// </summary>
        public sealed record FollowerTableSites(int Models, int FormCounts, int Female);

        /// <summary>The trainer class prize multipliers: overlay, pointer literal there (0 when none is known), table, length.</summary>
        public sealed record TrainerPrizeTable(int Overlay, uint Pointer, uint Table, int Count, bool Paired);

        /// <summary>The vanilla game's trainer class count: the gender table's length.</summary>
        public static int VanillaTrainerClassCount => GameFamily switch
        {
            GameFamilies.DP => 98,
            GameFamilies.Plat => 105,
            GameFamilies.HGSS => 129,
            _ => 0,
        };

        /// <summary>
        /// The trainer shiny patch's ARM9 sites: the call that frees the raw party records, the ability mask, and the six
        /// routines its payload calls.
        /// </summary>
        public sealed record TrainerShinySites(int Hook, int Mask, uint[] CallTargets);


        /// <summary>
        /// The building rotation patch's sites: the field overlay, the hook in it that passes each building's rotation,
        /// and the routine the payload calls to build the rotation matrix.
        /// </summary>
        public sealed record BuildingRotationSites(int Overlay, uint Hook, uint RotationRoutine);

        /// <summary>BDHCam's arm9 branch and its two jumps in the field overlay (file offsets).</summary>
        public sealed record BdhcamSites(uint Branch, int Overlay, uint Hook1, uint Hook2);

        /// <summary>One of the summary screen's window tables in arm9: eight bytes a window, the page it belongs to.</summary>
        public sealed record SummaryWindowTableSite(string Page, uint Offset, int Count);

        /// <summary>
        /// Window templates (8 bytes each) in arm9 (overlay -1) or an overlay. A read checks the first window's layer and first tile.
        /// </summary>
        public sealed record ScreenWindowTableSite(string Group, int Overlay, uint Offset, ScreenWindowName[] Windows, int FirstLayer, int FirstBaseTile);

        /// <summary>For a party Pokémon's window, which Pokémon (1 to 6) and part it is.</summary>
        public sealed record ScreenWindowName(string Name, int Pokemon = 0, string Part = null);

        /// <summary>
        /// Party tables in arm9: panels (12 bytes each), cursor stops (8), sprite templates with the status icons at entries 4 to 9 (40),
        /// and HGSS touch areas (4).
        /// </summary>
        public sealed record PartyPanelSites(uint Panels, uint[] Cursors, uint SpriteTemplates, uint[] Touch);

        /// <summary>The arm9 addresses of the party menu functions the panel size patch rewrites.</summary>
        public sealed record PartyPanelCodeSites(uint Draw, uint Empty, uint Loader, uint Capture, uint Ticks, uint Step, uint Redraw);

        /// <summary>
        /// The movs r2, #48 that sets the party HP bar length. The colour call's 48 can't change: it's shared with the draw state size.
        /// </summary>
        public sealed record PartyHpBarSites(uint Length);

        /// <summary>Party graphics members a panel resize rebuilds, and how many background rows sit above the message box.</summary>
        public sealed record PartyGraphicsMembers(int CursorCells, int CursorTiles, int PanelArt, int Background, int PanelRows);

        /// <summary>
        /// A summary screen bar's code: the Thumb instructions whose low byte is its tile column and row, how many
        /// tiles long it is, the background it is drawn into (main 0-3, sub 4-7) and the map entry of a full tile.
        /// </summary>
        public sealed record SummaryBarSite(uint X, uint Y, int Tiles, int Layer, int FullTile);

        /// <summary>
        /// The summary screen's window tables (always-there windows first), its HP and EXP bars, its message bank and the
        /// banks it reads ability descriptions and contest effects from (-1 where the game has none).
        /// </summary>
        public sealed record SummaryScreenSites(SummaryWindowTableSite[] Windows, SummaryBarSite Hp, SummaryBarSite Exp, int MessageBank,
                                                int AbilityDescriptionBank, int ContestEffectBank);


        /// <summary>
        /// The trainer class metadata patch's ARM9 veneers (gender and eye-contact music lookups) and the vanilla VS
        /// Pokemon table's length, which its detection compares against.
        /// </summary>
        public sealed record TrainerClassMetadataHooks(uint Gender, uint EyeContact, int VanillaVsPokemonCount);

        /// <summary>US HeartGold and SoulSilver only.</summary>
        public static TrainerClassMetadataHooks ClassMetadataHooks =>
            RomLocator.Where(RomLocator.Thing.TrainerClassMetadataGenderHook) is RomLocator.Spot gender
            && RomLocator.Where(RomLocator.Thing.TrainerClassMetadataEyeContactHook) is RomLocator.Spot eyeContact
                ? new TrainerClassMetadataHooks((uint)gender.Offset, (uint)eyeContact.Offset, 43) : null;

        /// <summary>hzla's PlatPatches: the RAM addresses of their item file and TM/HM hooks in the ARM9.</summary>
        public sealed record PlatPatchesHooks(uint ItemFileId, uint ItemIsTmHm);

        /// <summary>US Platinum only.</summary>
        public static PlatPatchesHooks PlatHooks =>
            RomLocator.Where(RomLocator.Thing.PlatPatchesItemFileIdHook) is RomLocator.Spot itemFile
            && RomLocator.Where(RomLocator.Thing.PlatPatchesItemIsTmHmHook) is RomLocator.Spot tmHm
                ? new PlatPatchesHooks(ARM9.Address + (uint)itemFile.Offset, ARM9.Address + (uint)tmHm.Offset) : null;

        /// <summary>Where PlatPatches keep their fixed blocks in the synthetic overlay, up to its end.</summary>
        public const int PlatPatchesFixedStart = 0x10000;

        /// <summary>
        /// The Distortion World's tables in code: the overlay, the floors whose headers the moving platform and prop lists
        /// are keyed by, and the counts the searches expect.
        /// </summary>
        public sealed record TornWorldCodeLayout(int Overlay, uint[] MovingPlatformFloors, uint[] PropFloors,
            int PropKinds, int AnimationKinds, int HoverSteps, int ElevatorPaths);

        /// <summary>Platinum only.</summary>
        public static TornWorldCodeLayout TornWorldCode => GameFamily != GameFamilies.Plat ? null
            : new TornWorldCodeLayout(9, new uint[] { 573, 574, 575, 576, 577, 579, 580, 581 }, new uint[] { 573, 579, 582, 583 }, 25, 5, 8, 22);

        /// <summary>HGSS: the overlay holding the Rock Smash item tables.</summary>
        public static int RockSmashItemTablesOverlayNumber => RomLocator.Where(RomLocator.Thing.RockSmashItemTables)?.Overlay ?? 1;

        /// <summary>The Battle Frontier Brains' VS records: overlay, file offset there, rows and row size.</summary>
        public sealed record FrontierBrainTable(int Overlay, uint Table, int Count, int RowSize);

        /// <summary>US HeartGold and SoulSilver only.</summary>
        public static FrontierBrainTable FrontierBrains =>
            RomLocator.Where(RomLocator.Thing.FrontierBrainTable) is RomLocator.Spot brains
                ? new FrontierBrainTable(brains.Overlay, (uint)brains.Offset, 6, 0x0C) : null;

        /// <summary>
        /// HGSS: species whose Pokéwalker picture is picked by form, each with the first of its form pictures in
        /// a/2/5/6, as overlay 112's lookup adds them (the form number is added to it). Measured on English HeartGold only;
        /// empty elsewhere.
        /// </summary>
        public static IReadOnlyList<(int Species, int FirstPicture)> PokewalkerFormPictures =>
            GameVersion == GameVersions.HeartGold && GameLanguage == GameLanguages.English
            ? new[] { (386, 586), (201, 590), (351, 618), (412, 622), (413, 625), (422, 628), (423, 630), (421, 632),
                      (493, 634), (492, 652), (479, 654), (487, 660), (172, 662) }
            : Array.Empty<(int, int)>();

        /// <summary>HGSS: a Pokéwalker icon (a/2/4/8) is the party icon (a/0/2/0) this many entries earlier; -1 elsewhere.</summary>
        public static int PokewalkerIconLeadIn => GameFamily == GameFamilies.HGSS ? 7 : -1;


        /// <summary>Text archive whose lines 78-87 name the ten Bug-Catching Contest opponents; -1 where unknown.</summary>
        public static int BugContestTextNumber =>
            GameFamily != GameFamilies.HGSS ? -1 : GameLanguage == GameLanguages.Japanese ? 239 : TextBank(246);
        public static string OWtablePath { get; private set; }

        public static uint MonIconPalTableAddress { get; private set; }

        public static int NullEncounterID { get; private set; }
        public static int AbilityNamesTextNumber { get; private set; }

        // The Pokédex's own lines and each species' entry, category, height and weight. -1 where unconfirmed.
        // Japanese ROMs number them differently and lack the last few message lines (the Japanese heading and the
        // dash placeholders).
        public static int PokedexMessagesTextNumber { get; private set; } = -1;
        public static int PokedexEntryTextNumber { get; private set; } = -1;
        public static int PokedexCategoryTextNumber { get; private set; } = -1;
        public static int PokedexHeightTextNumber { get; private set; } = -1;
        public static int PokedexWeightTextNumber { get; private set; } = -1;
        // A species' entry, name and category in the other languages the Pokédex can show, in the order French,
        // German, Italian, Spanish, Japanese; null where unconfirmed or where the game offers other languages
        // (Japanese ROMs show English to Spanish). DP keeps all languages in one bank each.
        public static int[] PokedexForeignEntryTextNumbers { get; private set; }
        public static int[] PokedexForeignNameTextNumbers { get; private set; }
        public static int[] PokedexForeignCategoryTextNumbers { get; private set; }
        // DP has foreign text for only the species in an ARM9 table (u16 each); a species' line in each bank is its
        // place in the table times six plus the language, Japanese first. -1 where unconfirmed (US Diamond only).
        public static int PokedexForeignSpeciesTableOffset { get; private set; } = -1;
        public const int PokedexForeignSpeciesCount = 14;
        // HGSS's two player characters' names, the boy's then the girl's (the game gives them to the player's friend).
        // -1 where unconfirmed.
        public static int PlayerCharacterNamesTextNumber { get; private set; } = -1;
        // HGSS's Pokédex area page names each place's zone through two tables in overlay 18, one for routes and
        // towns and one for caves, both ending in 0xFFFF. -1 where unconfirmed (English HeartGold only so far).
        public const int PokedexAreaZoneOverlay = 18;
        public static int PokedexFieldZoneTableOffset { get; private set; } = -1;
        public static int PokedexDungeonZoneTableOffset { get; private set; } = -1;
        /// <summary>The zones in the order the area page lists them (u32 each, <see cref="PokedexAreaSortTableCount"/> long).</summary>
        public static int PokedexAreaSortTableOffset { get; private set; } = -1;
        public static int PokedexAreaSortTableCount { get; private set; }
        public static int AttackNamesTextNumber { get; private set; }
        public static int[] PokemonNamesTextNumbers { get; private set; }
        public static int ItemNamesTextNumber { get; private set; }
        public static int ItemDescriptionsTextNumber { get; private set; }
        public static int ItemScriptFileNumber { get; internal set; }
        public static int TrainerClassMessageNumber { get; private set; }
        /// <summary>Trainer-class description text archive. </summary>
        public static int TrainerClassWithArticleMessageNumber { get; private set; }
        public static int TrainerNamesMessageNumber { get; private set; }
        public static int MoveDescriptionsTextNumbers { get; private set; }
        public static int MoveNamesTextNumbers { get; private set; }
        public static int LocationNamesTextNumber { get; private set; }
        public static int TrainerNameLenOffset { get; private set; }
        public static int TrainerNameMaxLen => SetTrainerNameMaxLen();
        /// <summary>The longest trainer name the editors accept.</summary>
        public static int TrainerNameMaxChars
        {
            get
            {
                int plain = TrainerNameMaxLen;
                return plain > TrainerFile.DefaultNameLen ? plain : TrainerFile.CompressedNameLen(plain + 1);
            }
        }
        public static int TrainerFunnyScriptNumber { get; private set; }
        /// <summary>US-version text archive numbers only; not yet confirmed for other localizations.</summary>
        public static int BattleTowerTrainerNamesMessageNumber { get; private set; }
        public static int BattleTowerTrainerMessagesNumber { get; private set; }

        public static int TypesTextNumber { get; private set; }
        public static int TrainerMessageTextNumber { get; private set; }

        public static string InternalNamesLocation { get; private set; }
        public static readonly byte InternalNameLength = 16;
        public static string InternalNamesPath { get; private set; }

        public static int CameraSize { get; private set; }
        /// <summary>Entries in the field camera table: 16 in DP, 17 in Pt and HGSS.</summary>
        public static int CameraCount { get; private set; }

        public Dictionary<List<uint>, (Color background, Color foreground)> MapCellsColorDictionary;
        public static SortedDictionary<uint, (uint spriteID, ushort properties)> OverworldTable { get; private set; }
        public static uint[] OverworldTableKeys { get; private set; }
        public static Dictionary<uint, string> Ow3DSpriteDict { get; private set; }

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
            German,
            Korean
        }

        public enum DirNames : byte
        {
            PersonalPokeData,
            GrowthTable,            // EXP per level for each growth curve
            BerryData,              // berry size, firmness, growth and flavours
            PokemonBattleSprites,
            OtherPokemonBattleSprites,
            PokemonSpriteOffsets,   // combined per-mon record: HGSS /a/1/8/0 (89 B/mon) · Plat pl_poke_data.narc, last 3 bytes = sprite Y/shadow X/shadow size
            PokeYofs,               // DP /poketool/pokegra/poke_yofs.narc, signed front-sprite Y, 1 B/mon
            PokeShadowOfx,          // DP /poketool/pokegra/poke_shadow_ofx.narc, signed shadow X, 1 B/mon
            PokeShadow,             // DP /poketool/pokegra/poke_shadow.narc, shadow size, 1 B/mon
            PokeHeight,             // DP+Plat /poketool/pokegra/height.narc, 4 files/mon (F-back,M-back,F-front,M-front), heights
            PokeHeightForms,        // /poketool/pokegra/height_o.narc, alt-form heights (record index matches the otherpoke sprite index)
            PokeAnim,               // DP /poketool/pokeanm/pokeanm.narc, 28 B/mon battle-animation table (POKE_ANM_DATA)
            PokeAnimDefs,           // DP /pokeanime/poke_anm.narc, the Pokémon animation scripts (referenced by prg_anm)

            WazaSeq,                // battle move sequence scripts (per move), battle/skill/waza_seq.narc (HGSS a/0/0/0)
            SubSeq,                 // shared move-sequence subroutines, battle/skill/sub_seq.narc (HGSS a/0/0/1)
            BeSeq,                  // per-effect move sequence scripts, battle/skill/be_seq.narc (HGSS a/0/3/0)
            WazaEffectScripts,      // move animation scripts, one per move, wazaeffect/we.arc (HGSS a/0/1/0)
            WazaEffectSub,          // move animation subroutines and continuous animations, wazaeffect/we_sub.narc (HGSS a/0/6/1)
            WazaEffectChar,         // effect cell graphics: NCGR char, wazaeffect/effectclact/wechar.narc (HGSS a/0/2/2)
            WazaEffectPltt,         // effect cell graphics: NCLR palette, wazaeffect/effectclact/wepltt.narc (HGSS a/0/2/3)
            WazaEffectCell,         // effect cell graphics: NCER cells, wazaeffect/effectclact/wecell.narc (HGSS a/0/2/4)
            WazaEffectCellAnm,      // effect cell graphics: NANR anims, wazaeffect/effectclact/wecellanm.narc (HGSS a/0/2/5)
            WazaParticle,           // effect SPA particle systems, wazaeffect/effectdata/waza_particle.narc (HGSS a/0/2/9)
            BattleBg,               // battle backgrounds + move-effect backgrounds, pl_batt_bg.narc (HGSS a/0/0/7)
            BattleObj,              // battle OBJ cells incl. the terrain ground platforms, pl_batt_obj.narc (HGSS a/0/0/8)
            BallParticles,          // Poke Ball open bursts, one SPA per ball, wazaeffect/effectdata/ball_particle.narc (HGSS a/0/9/5)
            TrainerBackGraphics,    // player-side trainer back sprites, poketool/trgra/trbgra.narc (HGSS a/0/0/6)
            BattleBgPlanm,          // HGSS-ONLY animated BG palette-anim data (SwitchBgEx), a/0/0/9
            DungeonCutinGraphics,   // HGSS only. Dungeon cutin (location-preview splash) art, a/1/5/0.
            TitleScreenGraphics,    // HGSS only. Main-menu title logo/palette/background, a/0/4/6.
            TrainerCardGraphics,    // Trainer card face/back + trainer-pose art.
            WeatherGraphics,        // HGSS only. DP and Platinum keep weather in synthOverlay.
            AreaWindowGraphics,     // Place-name banner frames. DP uses one loose file.
            SealGraphics,           // Ball Capsule editor screens and seal stickers.
            TrainerCapsules,        // Capsules on trainers' Pokemon. Not in DP.

            SynthOverlay,
            DynamicHeaders,

            TornWorld,              // Pt only: Distortion World map data.
            TornWorldAttributes,    // Pt only: platform collision grids.
            FieldEffectModels,

            TextArchives,
            Matrices,

            Maps,
            ExteriorBuildingModels,
            BuildingConfigFiles,
            BuildingTextures,
            MapTextures,
            AreaData,

            // Animation data the games play over the field.
            GroundAnimations,       // HGSS(USA) a/1/4/0  ground_anm.narc  (2 NSBTA)
            BuildingAnimations,     // HGSS(USA) a/1/0/6  bm_anime.narc   (NSBCA/NSBTA/NSBTP)
            BuildingAnimListOut,    // HGSS(USA) a/1/0/7  bm_info_out.narc
            BuildingAnimListIn,     // HGSS(USA) a/1/0/8  bm_info_in.narc

            EventFiles,
            OWSprites,

            Scripts,

            Encounters,
            EncounterExtended,
            Headbutt,
            RockSmash,
            FollowerParams, // HGSS only
            PokeathlonPerformance, // HGSS only
            SafariZone,
            BattleTowerTrainers,
            BattleTowerPokemon,

            TrainerProperties,
            TrainerParty,
            TrainerGraphics,
            MoveData,

            MonIcons,

            InteriorBuildingModels,
            Learnsets,
            Evolutions,

            ItemData,
            ItemIcons,

            TradeData,

            TrainerTextOffset,
            TrainerTextTable,

            EggMoves,

            Fonts,                  // DP graphic/font.narc, Pt graphic/pl_font.narc, HGSS a/0/1/6
            WindowFrames,           // the borders round a message box
            Poketch,                // Pt graphic/poketch.narc, the Pokétch on the bottom screen
            FieldTouchMenu,         // HGSS a/0/1/4, the touch menu panel on the bottom screen
            FieldTouchChoices,      // HGSS a/2/3/7, the Poké Ball screen and its touch buttons
            SummaryGraphics,        // DP graphic/pst_gra.narc, Pt graphic/pl_pst_gra.narc, HGSS a/1/6/2: the Pokémon summary screens
            SummarySprites,         // HGSS a/0/3/9: the summary screen's ball cells, markings and other sprites, numbered like Platinum's summary archive
            MoveRelearnerGraphics,  // DP/Pt graphic/waza_oshie_gra.narc, HGSS a/1/1/0
            BattlePartyGraphics,    // DP battle/graphic/b_plist_gra.narc, Pt battle/graphic/pl_b_plist_gra.narc, HGSS a/0/7/1
            BagGraphics,            // DP graphic/bag_gra.narc, Pt graphic/pl_bag_gra.narc, HGSS a/0/1/5
            FieldTextureAnimations, // Pt data/fldtanime.narc, HGSS a/1/3/9 (HGSS data/fldtanime.narc is unused): member 0 names textures, the rest hold frames
            EncounterEffectGraphics, // DP/Pt graphic/field_encounteffect.narc, HGSS a/1/0/9: special trainer battle intro art
            NameInputGraphics,      // DP/Pt data/namein.narc, HGSS a/0/3/1: the naming screen's backgrounds and icons
            StartMenuGraphics,      // Pt graphic/menu_gra.narc: the start menu's icons and cursor
            PokegearGraphics,       // HGSS a/1/4/3: the Pokégear frame and app buttons, six skins each
            PokegearMapGraphics,    // HGSS a/1/4/4: the Pokégear map
            PokegearPhoneGraphics,  // HGSS a/1/4/6: the Pokégear phone
            PokegearRadioGraphics,  // HGSS a/1/4/7: the Pokégear radio
            PartyMenuGraphics,      // Pt graphic/pl_plist_gra.narc, HGSS a/0/2/1: the party screen
            BoxGraphics,            // Pt graphic/box.narc, HGSS a/0/1/9: the PC boxes
            OptionsGraphics,        // Pt graphic/config_gra.narc, HGSS a/0/7/2: the options screen
            TrainerClassMetadata,   // HGSS a/1/5/5, only used by the external trainer class metadata patch
            PokedexData,            // DP/Pt application/zukanlist/zkn_data/zukan_data.narc, HGSS a/0/7/4: dex height, weight, body shape, size check and sort lists (Pt/HGSS with Origin Giratina)
            PokedexDataAltered,     // Pt zukan_data_gira.narc, HGSS a/2/1/4: the same with Altered Giratina
            PokedexAreas,           // DP zukan_enc_diamond/pearl.narc, Pt zukan_enc_platinum.narc, HGSS a/1/3/3: where the Pokédex says each species lives
            PokedexSearchSteps,     // HGSS a/0/7/5: member 0 holds the height and weight search sliders' steps (u16 height, u16 weight)
            RegionalDexNumbers,     // DP poketool/pokezukan.narc, Pt poketool/pl_pokezukan.narc, HGSS a/1/3/8: member 0 is each species' regional number (u16, 0 = not in it)
            RegionalDexSpecies,     // DP/Pt poketool/shinzukan.narc: member 0 is the species at each regional number (u16, entry 0 unused); HGSS has none
            FootprintGraphics,      // DP/Pt poketool/pokefoot/pokefoot.narc, HGSS a/0/6/9: one palette and layout, then a footprint per species
            PokedexGraphics,        // DP/Pt resource/eng/zukan/zukan.narc, HGSS a/0/6/8 (graphic/zukan_gra.narc): the Pokédex screens
            OpeningDemoGraphics,    // HGSS a/2/6/2 (demo/opening/gs_opening.narc): the opening movie
            PokewalkerSprites,      // HGSS a/2/5/6: the Pokéwalker's Pokemon pictures, 64x96 2bpp (PokewalkerImage)
            PokewalkerIcons,        // HGSS a/2/4/8: the Pokéwalker's small pictures, 32x48 2bpp
            UndergroundParts,       // DP/Pt data/ug_parts.narc: Underground treasure, rock and sphere art
        };

        public static Dictionary<DirNames, (string packedDir, string unpackedDir)> GameDirs { get; private set; }

        #region Constructors (1)

        public RomInfo(string id, string romFolderName)
        {
            // Refused before any static state changes, so the ROM already open stays intact.
            if (!IsSupportedGameCode(id))
            {
                ShowWarning?.Invoke("The ROM you attempted to load is not supported.\nYou can only load Gen IV Pokémon ROMS, for now.", "Unsupported ROM");
                return;
            }

            // These are only ever (re)populated lazily elsewhere, behind an "if (x == null)" check, without
            // resetting them here first, switching to a different ROM mid-session would leave the
            // FIRST-loaded ROM's overworld-sprite table/dict silently in effect forever.
            OverworldTable = null;
            OverworldTableKeys = null;
            Ow3DSpriteDict = null;
            OWtablePath = null;
            OWTableOffset = 0;
            OverlayUtils.ForgetOverlayTable();
            DVCalculator.TrainerClassGender.Invalidate();

            string path = Path.GetFullPath(romFolderName);

            int folderType = DSUtils.GetFolderType(romFolderName);
            IsDsRomProject = folderType == 0;
            IsHgEngineBaseProject = folderType == 2;
            HgEngineDsRomMetaDir = IsHgEngineBaseProject && !File.Exists(Path.Combine(romFolderName, "header.bin"))
                ? DSUtils.HgEngineDsRomMetaDir(romFolderName)
                : null;

            if (IsDsRomProject)
            {
                _dataFolderName = "files";
            }
            else
            {
                _dataFolderName = IsHgEngineBaseProject ? "root" : "data";
            }
            _customNarcFolderName = _dataFolderName + "/zcustom";

            WorkDir = path + Path.DirectorySeparatorChar; // Trailing separator is load-bearing: callers concatenate onto workDir directly
            DspreDir = IsHgEngineBaseProject ? HgEngineDspreDirFor(path) ?? WorkDir : WorkDir;
            RefreshRotomProjectState();
            
            if (IsDsRomProject)
            {
                Arm9Path = Path.Combine(WorkDir, "arm9", "arm9.bin");
                Arm7Path = Path.Combine(WorkDir, "arm7", "arm7.bin");
                OverlayTablePath = Path.Combine(WorkDir, "arm9_overlays", "overlays.yaml");
                Y7Path = Path.Combine(WorkDir, "arm7_overlays", "overlays.yaml");
                DataPath = Path.Combine(WorkDir, @"files");
                OverlayPath = Path.Combine(WorkDir, @"arm9_overlays");
                BannerPath = Path.Combine(WorkDir, @"banner");
                HeaderPath = Path.Combine(WorkDir, @"header.yaml");
            }
            else
            {
                Arm9Path = Path.Combine(WorkDir, @"arm9.bin");
                Arm7Path = Path.Combine(WorkDir, @"arm7.bin");
                OverlayTablePath = Path.Combine(WorkDir, IsHgEngineBaseProject ? "overarm9.bin" : "y9.bin");
                Y7Path = Path.Combine(WorkDir, IsHgEngineBaseProject ? "overarm7.bin" : "y7.bin");
                DataPath = Path.Combine(WorkDir, _dataFolderName);
                OverlayPath = Path.Combine(WorkDir, @"overlay");
                BannerPath = HgEngineDsRomMetaDir != null ? Path.Combine(HgEngineDsRomMetaDir, "banner") : Path.Combine(WorkDir, @"banner.bin");
                HeaderPath = HgEngineDsRomMetaDir != null ? Path.Combine(HgEngineDsRomMetaDir, "header.yaml") : Path.Combine(WorkDir, @"header.bin");
            }
            UnpackedPath = Path.Combine(DspreDir, @"unpacked");
            InternalNamesPath = Path.Combine(DataPath, "fielddata", "maptable", "mapname.bin");

            GameVersion = PokeDatabase.System.VersionsDict[id];

            RomID = id;
            RomRevision = ReadRomRevision(WorkDir);
            IsHGE = false;
            // Get the folder name and strip the _DSPRE_contents suffix to get the ROM name
            string folderName = Path.GetFileName(romFolderName);
            if (folderName.EndsWith(FolderSuffix))
            {
                ProjectName = folderName.Substring(0, folderName.Length - FolderSuffix.Length);
            }
            else
            {
                ProjectName = folderName;
            }
            // Every checkout's tree is called base, which would make them share one script database.
            if (IsHgEngineBaseProject) ProjectName = Path.GetFileName(Path.GetDirectoryName(path)) ?? ProjectName;

            LoadGameFamily();
            LoadGameLanguage();

            // The marker hg-engine writes is the only identifier, since a hack may have changed the
            // title, code or anything else DSPRE could otherwise have keyed on.
            string ov129path = OverlayUtils.GetPath(129);
            if (File.Exists(ov129path))
            {
                using (DSUtils.EasyReader br = new DSUtils.EasyReader(ov129path))
                {
                    IsHGE = Encoding.UTF8.GetString(br.ReadBytes(16)) == "hg-engine rocks!";
                }
            }

            SetNarcDirs();
            // The synthetic overlay's member (HGSS 0, DP/Pt 9) and the patch flags belong to this ROM, not the last one.
            RomPatchState.ResetFlags();
            SetHeaderTableOffset();
            // Every header reader trusts this flag, and only applying the patch would otherwise set it.
            if (GameFamily != GameFamilies.DP)
            {
                try { RomPatchState.FlagDynamicHeadersPatchApplied = PatchToolboxLogic.CheckFilesDynamicHeadersPatchApplied(); }
                catch (Exception ex) { AppLogger.Warn("Dynamic headers check failed: " + ex.Message); }
            }
            SetNullEncounterID();
            SetPickupTableOffsets();
            SetItemTableOffset();
            SetItemReferenceOffsets();
            SetMartOffsets();
            SetStarterOffsets();
            SetRematchTableOffsets();
            SetFieldScriptArchives();
            SetupSpawnSettings();
            SetMonIconsPalTableAddress();
            // The trainer editor and DV calculator read these; only the WinForms trainer editor used to set them.
            try { SetAIBackportEnabled(); }
            catch (Exception ex) { AppLogger.Warn("AI backport check failed: " + ex.Message); }

            SetAbilityNamesTextNumber();
            SetPokedexTextNumbers();
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
            if (GameLanguage == GameLanguages.Korean) SetKoreanTextNumbers();

            InitScriptDBs();

            /* System */
            ScriptCommandParametersDict = BuildCommandParametersDatabase(GameFamily);

            ScriptCommandNamesDict = BuildCommandNamesDatabase(GameFamily);
            ScriptActionNamesDict = BuildActionNamesDatabase(GameFamily);
            ScriptComparisonOperatorsDict = BuildComparisonOperatorsDatabase(GameFamily);

            ScriptCommandNamesReverseDict = BuildCommandNamesReverse();
            ScriptActionNamesReverseDict = ScriptActionNamesDict.Reverse();
            ScriptComparisonOperatorsReverseDict = ScriptComparisonOperatorsDict.Reverse();

            try { SetOWtable(); }
            catch (Exception ex) { AppLogger.Warn("Overworld table not located: " + ex.Message); }
        }

        /// <summary>Whether DSPRE can open a ROM with this game code.</summary>
        public static bool IsSupportedGameCode(string id) =>
            id != null && PokeDatabase.System.VersionsDict.ContainsKey(id);

        /// <summary>Where the ARM9 records the end of its compressed code; zero there means uncompressed.</summary>
        public static uint Arm9CompressionMarkOffset => GameFamily == GameFamilies.DP ? 0xB7Cu : 0xBB4u;

        #endregion Constructors (1)

        #region Methods (22)

        public static void InitScriptDBs()
        {
            ScriptDatabaseSetup.InitializeScriptDatabase(ProjectName, GameFamily, GameVersion);
        }

        public static void ReloadScriptCommandDictionaries()
        {
            ScriptCommandParametersDict = BuildCommandParametersDatabase(GameFamily);
            ScriptCommandNamesDict = BuildCommandNamesDatabase(GameFamily);
            ScriptActionNamesDict = BuildActionNamesDatabase(GameFamily);
            ScriptComparisonOperatorsDict = BuildComparisonOperatorsDatabase(GameFamily);
            ScriptCommandNamesReverseDict = BuildCommandNamesReverse();
            ScriptActionNamesReverseDict = ScriptActionNamesDict.Reverse();
            ScriptComparisonOperatorsReverseDict = ScriptComparisonOperatorsDict.Reverse();
        }

        public static Dictionary<ushort, ScriptCommandInfo> GetScriptCommandInfoDict()
        {
            switch (GameFamily)
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

        /// <summary>Builds the command names dictionary from ScriptCommandInfo objects.</summary>
        public static Dictionary<ushort, string> BuildCommandNamesDatabase(GameFamilies gameFam)
        {
            Dictionary<ushort, ScriptCommandInfo> cmdInfoDict = GetScriptCommandInfoDict();
            return cmdInfoDict.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Name);
        }

        /// <summary>Command name back to its number. </summary>
        public static Dictionary<string, ushort> BuildCommandNamesReverse()
        {
            Dictionary<ushort, ScriptCommandInfo> info = GetScriptCommandInfoDict();
            Dictionary<string, ushort> map = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
            if (info == null) return map;

            foreach (KeyValuePair<ushort, ScriptCommandInfo> kv in info)
                if (!string.IsNullOrEmpty(kv.Value?.Name)) map[kv.Value.Name] = kv.Key;
            foreach (KeyValuePair<ushort, ScriptCommandInfo> kv in info)
                if (!string.IsNullOrEmpty(kv.Value?.LegacyName) && !map.ContainsKey(kv.Value.LegacyName))
                    map[kv.Value.LegacyName] = kv.Key;
            return map;
        }

        /// <summary>Builds the command parameters dictionary from ScriptCommandInfo objects.</summary>
        public static Dictionary<ushort, byte[]> BuildCommandParametersDatabase(GameFamilies gameFam)
        {
            Dictionary<ushort, ScriptCommandInfo> cmdInfoDict = GetScriptCommandInfoDict();
            return cmdInfoDict.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ParameterSizes);
        }

        public static Dictionary<ushort, string> BuildActionNamesDatabase(GameFamilies gameFam)
        {
            return ScriptDatabase.MovementsDict.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Name);
        }

        public static Dictionary<ushort, string> BuildComparisonOperatorsDatabase(GameFamilies gameFam)
        {
            switch (gameFam)
            {
                case GameFamilies.DP:
                case GameFamilies.Plat:
                case GameFamilies.HGSS:
                    return ScriptDatabase.ComparisonOperatorsDict;

                default:
                    Dictionary<ushort, string> commonDict = ScriptDatabase.ComparisonOperatorsDict;
                    Dictionary<ushort, string> appendixDict = ScriptDatabase.ComparisonOperatorsGenVappendix;
                    return commonDict.Concat(appendixDict).ToLookup(x => x.Key, x => x.Value).ToDictionary(x => x.Key, g => g.First());
            }
        }

        private static readonly Dictionary<uint, string> SignpostImages = new Dictionary<uint, string>
        {
            [91] = "brown_sign", [92] = "red_sign", [93] = "gray_sign", [94] = "route_sign", [95] = "blue_sign", [96] = "blue_sign",
        };

        /// <summary>
        /// Overworld ids with no sprite of their own and a stand-in picture for each: DP and Platinum's 3D models,
        /// and ids 101 to 116, which take their picture from variables 0x4020 to 0x402F.
        /// </summary>
        public static void Set3DOverworldsDict()
        {
            Ow3DSpriteDict = new Dictionary<uint, string>();
            for (uint id = FirstVariableOverworld; id <= LastVariableOverworld; id++) Ow3DSpriteDict[id] = "overworld";
            if (GameFamily != GameFamilies.DP && GameFamily != GameFamilies.Plat) return;

            try
            {
                if (string.IsNullOrEmpty(OWtablePath)) SetOWtable();
                foreach ((uint id, OverworldSpriteTableExpansion.OwRenderState state) in OverworldSpriteTableExpansion.ReadRenderStates())
                    if (state.DrawType == 2)
                        Ow3DSpriteDict[id] = SignpostImages.TryGetValue(id, out string sign) ? sign : "overworld";
            }
            catch (Exception ex) { AppLogger.Error("The 3D overworld ids could not be read: " + ex.Message); }
        }

        public const uint FirstVariableOverworld = 101, LastVariableOverworld = 116;

        /// <summary>
        /// How many save variables follow 0x4000; an id past them reads outside the game's table. The
        /// decompilations give 0x120 for Diamond, Pearl and Platinum and 0x170 for HeartGold and SoulSilver.
        /// hg-engine's layout is not pinned down, so it is only held to the script-memory boundary.
        /// </summary>
        public static int SaveVariableCount =>
            IsHGE ? 0x4000 : GameFamily == GameFamilies.HGSS ? 0x170 : 0x120;

        /// <summary>Whether an overworld id takes its picture from a map variable rather than the sprite table.</summary>
        public static bool IsVariableOverworld(uint id) => id >= FirstVariableOverworld && id <= LastVariableOverworld;

        // pokeheartgold include/constants/sprites.h SPRITE_HEROINE. HGSS scripts put the friend's sprite in
        // 0x4020 (ScrCmd_GetFriendSprite), so id 101 is the friend, shown as her for a male player.
        private const uint HgssHeroineOverworld = 97;

        // pokeplatinum scripts_pokemon_center_daily_trainers.s: the Pokémon Centre daily trainers in 111 and 112
        // change with the day; Kinsey and Tevin, the reporter and cameraman, stand in for them.
        private const uint PlatDailyTrainerOne = 111, PlatReporterOverworld = 22, PlatCameramanOverworld = 23;

        /// <summary>
        /// The overworld drawn in an editor for this id: the friend for HGSS's 101, a reporter and cameraman for
        /// Platinum's 111 and 112, otherwise the id itself.
        /// </summary>
        public static uint VariableOverworldStandIn(uint id)
        {
            if (GameFamily == GameFamilies.HGSS && id == FirstVariableOverworld) return HgssHeroineOverworld;
            if (GameFamily == GameFamilies.Plat && id == PlatDailyTrainerOne) return PlatReporterOverworld;
            if (GameFamily == GameFamilies.Plat && id == PlatDailyTrainerOne + 1) return PlatCameramanOverworld;
            return id;
        }

        public static void SetHeaderTableOffset() =>
            HeaderTableOffset = (uint)(RomLocator.Where(RomLocator.Thing.MapHeaders)?.Offset ?? 0);

        public static void SetupSpawnSettings()
        {
            RomLocator.Spot money = RomLocator.Where(RomLocator.Thing.InitialMoney);
            InitialMoneyOverlayNumber = money?.Overlay ?? 0;
            InitialMoneyOverlayOffset = (uint)(money?.Offset ?? 0);
            Arm9spawnOffset = (uint)(RomLocator.Where(RomLocator.Thing.SpawnPoint)?.Offset ?? 0);
        }

        public static void SetPickupTableOffsets()
        {
            RomLocator.Spot divisor = RomLocator.Where(RomLocator.Thing.PickupDivisor);
            PickupTableOverlayNumber = divisor?.Overlay ?? -1;
            PickupActivationDivisorOffset = (uint)(divisor?.Offset ?? 0);
            PickupCommonItemsOffset = (uint)(RomLocator.Where(RomLocator.Thing.PickupCommonItems)?.Offset ?? 0);
            PickupRareItemsOffset = (uint)(RomLocator.Where(RomLocator.Thing.PickupRareItems)?.Offset ?? 0);
            PickupWeightTableOffset = (uint)(RomLocator.Where(RomLocator.Thing.PickupWeights)?.Offset ?? 0);
        }

        /// <summary>Offsets for the Starter Pokémon editor. </summary>
        public static void SetStarterOffsets()
        {
            // Initialize to invalid values by default
            StarterGraphicsPrefix = null;
            StarterSceneSpeciesLiterals = null;
            StarterHeldItemScriptFileID = -1;
            StarterHeldItemOffset = 0;
            StarterCommandScriptNumber = -1;
            StarterCommandIndex = -1;
            StarterScreenTextNumber = -1;
            StarterPokedexSpeciesTextNumber = -1;

            switch (GameFamily)
            {
                case GameFamilies.DP:
                    StarterGraphicsPrefix = "000222402104120C";
                    StarterHeldItemScriptFileID = 342;
                    StarterHeldItemOffset = 0x2B4;
                    switch (GameLanguage)
                    {
                        case GameLanguages.Japanese:
                            // The scene loads each sprite's species from a literal pool word, not from the
                            // immediates the US code builds, so the US rewrite has nothing to match.
                            StarterGraphicsPrefix = null;
                            StarterSceneSpeciesLiterals = new[] { 0x16A8, 0x16B0, 0x16B8 };
                            StarterScreenTextNumber = 318;
                            StarterPokedexSpeciesTextNumber = 607;
                            break;
                        default:
                            StarterScreenTextNumber = 320;
                            StarterPokedexSpeciesTextNumber = 621;
                            break;
                    }
                    break;

                case GameFamilies.Plat:
                    StarterGraphicsPrefix = "000222402104120C";
                    StarterHeldItemScriptFileID = 427;
                    StarterHeldItemOffset = 0x460;
                    StarterCommandScriptNumber = 13;   // GivePokemon 0x8000 0x5 ITEM_NONE 0x800C
                    StarterCommandIndex = 11;
                    switch (GameLanguage)
                    {
                        case GameLanguages.Japanese:
                            StarterScreenTextNumber = 359;
                            StarterPokedexSpeciesTextNumber = 698;
                            break;
                        default:
                            StarterScreenTextNumber = 360;
                            StarterPokedexSpeciesTextNumber = 711;
                            break;
                    }
                    break;

                case GameFamilies.HGSS:
                    switch (GameLanguage)
                    {
                        case GameLanguages.Japanese:
                            StarterScreenTextNumber = 188;
                            break;
                        default:
                            StarterScreenTextNumber = 190;
                            break;
                    }
                    break;
            }
        }

        /// <summary>Where each game keeps its trainer rematch table.</summary>
        public static void SetRematchTableOffsets()
        {
            RomLocator.Spot pokegear = RomLocator.Where(RomLocator.Thing.PokegearRematches);
            PokegearRematchOverlayNumber = pokegear?.Overlay ?? -1;
            PokegearRematchFallbackTableOffset = (uint)(pokegear?.Offset ?? 0);
            PokegearPhoneMessageArchive = GameFamily == GameFamilies.HGSS ? (GameLanguage == GameLanguages.Japanese ? 264 : 271) : -1;
            // Literal-pool words in the lookup code that hold the table address, and the row-count compare bounding its
            // loop (pokeplatinum overlay005/vs_seeker.c).
            RomLocator.Spot rematches = RomLocator.Where(RomLocator.Thing.VsSeekerRematches);
            VsSeekerRematchOverlayNumber = rematches?.Overlay ?? -1;
            VsSeekerRematchTableOffset = (uint)(rematches?.Offset ?? 0);
            VsSeekerRematchPointerOffsets = RomLocator.Where(RomLocator.Thing.VsSeekerRematchPointers)?.Offsets ?? Array.Empty<int>();
            VsSeekerRematchRowCompareOffset = RomLocator.Where(RomLocator.Thing.VsSeekerRematchRowCompare)?.Offset ?? -1;
        }

        /// <summary>The text archives field scripts read menus and shared messages from.</summary>
        public static void SetFieldScriptArchives()
        {
            FieldMenuEntriesArchive = -1;
            FieldYesEntry = FieldNoEntry = -1;
            BattleStringsTextNumber = -1;
            FieldSharedMessageArchives = System.Array.Empty<int>();
            SystemFontPaletteEntry = -1;
            FieldTouchMenuTextArchive = -1;
            // Japanese archives are numbered differently; Japanese Diamond and Pearl's are not known.
            bool japanese = GameLanguage == GameLanguages.Japanese;
            switch (GameFamily)
            {
                case GameFamilies.Plat:
                    FieldMenuEntriesArchive = japanese ? 360 : 361;          // TEXT_BANK_MENU_ENTRIES
                    if (!japanese) { FieldYesEntry = 41; FieldNoEntry = 42; } // MenuEntries_Text_Yes, _No
                    if (!japanese) BattleStringsTextNumber = 368;               // TEXT_BANK_BATTLE_STRINGS
                    SystemFontPaletteEntry = 6;
                    break;
                case GameFamilies.HGSS:
                    FieldMenuEntriesArchive = japanese ? 189 : 191;
                    if (!japanese) { FieldYesEntry = 42; FieldNoEntry = 43; } // CreateYesNoMenu
                    if (!japanese) BattleStringsTextNumber = 197;
                    FieldSharedMessageArchives = japanese ? new[] { 740, 209, 29, 428 } : new[] { 752, 211, 30, 435 };
                    SystemFontPaletteEntry = 7;
                    FieldTouchMenuTextArchive = japanese ? 194 : 196;
                    break;
            }
        }

        public static void SetItemTableOffset()
        {
            // push {r3, lr}; mov r3, r0; <limit into r0>; cmp r3, r0; bls; mov r3, #0; cmp r1, #0.
            // DP and HGSS build the limit with mov r0, #imm; lsl r0, #n, Pt loads it from the literal pool.
            ItemLimitSite = (uint)(RomLocator.Where(RomLocator.Thing.ItemLimit)?.Offset ?? 0);
            ItemLimitSignature = GameFamily == GameFamilies.Plat
                ? "08B5031C ??48 8342 00D9 0023 0029"
                : "08B5031C ??20 ???? 8342 00D9 0023 0029";
            ItemTableOffset = (uint)(RomLocator.Where(RomLocator.Thing.ItemTable)?.Offset
                                     ?? throw new NotSupportedException("Game not supported"));
            // Japanese DP opens the loader with push {lr}; sub sp, #4 and loads the limit from the pool; the site is the sub.
            if (GameFamily == GameFamilies.DP && GameLanguage == GameLanguages.Japanese)
                ItemLimitSignature = "81B0031C ??48 8342 00D9 0023 0029";
        }

        public static void SetItemReferenceOffsets()
        {
            RomLocator.Spot hidden = RomLocator.Where(RomLocator.Thing.HiddenItems);
            HiddenItemTableOffset = (uint)(hidden?.Offset ?? 0);
            // The vanilla tables have no spare rows: DP 229, Pt 257, HGSS 231.
            HiddenItemTableCapacity = hidden == null ? 0 : GameFamily switch { GameFamilies.DP => 229, GameFamilies.Plat => 257, _ => 231 };
            HiddenItemCountSites = RomLocator.Where(RomLocator.Thing.HiddenItemCounts)?.Offsets.Select(o => (uint)o).ToArray() ?? Array.Empty<uint>();
            HiddenItemCountLiterals = RomLocator.Where(RomLocator.Thing.HiddenItemCountLiterals)?.Offsets.Select(o => (uint)o).ToArray() ?? Array.Empty<uint>();
            // SCRIPT_ID(VISIBLE_ITEMS, 321) in sMapObjectEventTurnbackCaveRoom_GriseousOrbItem.
            DistortionWorldItemScriptOffset = (uint)(RomLocator.Where(RomLocator.Thing.DistortionWorldItemScript)?.Offset ?? 0);
        }

        public static void SetMartOffsets()
        {
            RomLocator.Spot count = RomLocator.Where(RomLocator.Thing.MartCommonCount);
            MartCommonCountOffset = (uint)(count?.Offset ?? 0);
            MartCommonPointerOffset = (uint)(RomLocator.Where(RomLocator.Thing.MartCommonPointer)?.Offset ?? 0);
            MartSpecialtyPointerOffset = (uint)(RomLocator.Where(RomLocator.Thing.MartSpecialtyPointer)?.Offset ?? 0);
            MartSpecialtyShopCount = count == null ? 0 : GameFamily switch { GameFamilies.DP => 19, GameFamilies.Plat => 20, _ => 30 };
        }

        public static void PrepareCameraData()
        {
            switch (GameFamily)
            {
                case GameFamilies.DP:
                    CameraTblOverlayNumber = 5;
                    CameraSize = 24;
                    CameraCount = 16;
                    break;

                case GameFamilies.Plat:
                    CameraTblOverlayNumber = 5;
                    CameraSize = 24;
                    CameraCount = 17;
                    break;

                case GameFamilies.HGSS:
                    CameraTblOverlayNumber = 1;
                    CameraSize = 36;
                    CameraCount = 17;
                    break;
            }
            CameraTblOffsetsToRAMaddress = RomLocator.Where(RomLocator.Thing.CameraTablePointers)?.Offsets.Select(o => (uint)o).ToArray() ?? Array.Empty<uint>();
        }

        public static void SetOWtable()
        {
            switch (GameFamily)
            {
                case GameFamilies.DP:
                case GameFamilies.Plat:
                    OWtablePath = OverlayUtils.GetPath(5);
                    OWTableOffset = (uint)(RomLocator.Where(RomLocator.Thing.OverworldTable)?.Offset ?? 0);
                    break;

                case GameFamilies.HGSS:
                    if (OverlayUtils.OverlayTable.IsDefaultCompressed(1))
                    {
                        if (OverlayUtils.IsCompressed(1))
                        {
                            if (OverlayUtils.Decompress(1) < 0)
                            {
                                ShowWarning?.Invoke("Overlay 1 couldn't be decompressed.\nOverworld sprites in the Event editor will be " +
                                "displayed incorrectly or not displayed at all.", "Decompression error");
                            }
                        }
                    }

                    string ov1Path = OverlayUtils.GetPath(1);
                    uint ov1Address = OverlayUtils.OverlayTable.GetRAMAddress(1);
                    RomLocator.Spot pointer = RomLocator.Where(RomLocator.Thing.OverworldTablePointer);
                    if (pointer == null) return;

                    using (DSUtils.EasyReader bReader = new DSUtils.EasyReader(ov1Path, pointer.Offset))
                    { // read the pointer at the specified ram address and adjust accordingly below
                        uint ramAddressOfTable = bReader.ReadUInt32();
                        if ((ramAddressOfTable >> 0x18) != 0x02)
                        {
                            ShowWarning?.Invoke("Something went wrong reading the Overworld configuration table.\nOverworld sprites in the Event editor will be " +
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
                        else if (ramAddressOfTable >= RomInfo.SynthOverlayLoadAddress)
                        {
                            // if the pointer shows the table was moved to the synthetic overlay
                            OWTableOffset = ramAddressOfTable - RomInfo.SynthOverlayLoadAddress;
                            OWtablePath = Filesystem.ExpArmPath;
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

        public static void SetMonIconsPalTableAddress()
        {
            RomLocator.Spot literal = RomLocator.Where(RomLocator.Thing.MonIconPalettePointer);
            MonIconPalTableAddress = literal == null ? 0 : BitConverter.ToUInt32(ARM9.ReadBytes((uint)literal.Offset, 4), 0);
        }

        private static void SetItemScriptFileNumber()
        {
            switch (GameFamily)
            {
                case GameFamilies.DP:
                    ItemScriptFileNumber = 370;
                    break;

                case GameFamilies.Plat:
                    ItemScriptFileNumber = 404;
                    break;

                default:
                    ItemScriptFileNumber = 141;
                    break;
            }
        }

        private static void SetNullEncounterID()
        {
            switch (GameFamily)
            {
                case GameFamilies.DP:
                case GameFamilies.Plat:
                    NullEncounterID = ushort.MaxValue;
                    break;

                case GameFamilies.HGSS:
                    NullEncounterID = Byte.MaxValue;
                    break;
            }
        }

        private static void SetAbilityNamesTextNumber()
        {
            switch (GameFamily)
            {
                case GameFamilies.DP:
                    AbilityNamesTextNumber = GameLanguage == GameLanguages.Japanese ? 544 : 552;
                    break;

                case GameFamilies.Plat:
                    AbilityNamesTextNumber = GameLanguage == GameLanguages.Japanese ? 604 : 610;
                    break;

                case GameFamilies.HGSS:
                    AbilityNamesTextNumber = GameLanguage == GameLanguages.Japanese ? 711 : 720;
                    break;

                default:
                    break;
            }
        }

        private static void SetPokedexTextNumbers()
        {
            PokedexMessagesTextNumber = PokedexEntryTextNumber = PokedexCategoryTextNumber = -1;
            PokedexHeightTextNumber = PokedexWeightTextNumber = -1;
            PokedexForeignEntryTextNumbers = PokedexForeignNameTextNumbers = PokedexForeignCategoryTextNumbers = null;
            PokedexForeignSpeciesTableOffset = -1;
            PlayerCharacterNamesTextNumber = -1;
            PokedexFieldZoneTableOffset = PokedexDungeonZoneTableOffset = PokedexAreaSortTableOffset = -1;
            PokedexAreaSortTableCount = 0;
            bool japanese = GameLanguage == GameLanguages.Japanese;
            switch (GameFamily)
            {
                case GameFamilies.DP:
                    if (japanese)
                    {
                        PokedexMessagesTextNumber = 600;
                        PokedexEntryTextNumber = GameVersion == GameVersions.Pearl ? 602 : 601;
                        PokedexWeightTextNumber = 605;
                        PokedexHeightTextNumber = 606;
                        PokedexCategoryTextNumber = 607;
                    }
                    else
                    {
                        PokedexCategoryTextNumber = 621;
                        PokedexMessagesTextNumber = 614;
                        PokedexEntryTextNumber = GameVersion == GameVersions.Pearl ? 616 : 615;
                        PokedexWeightTextNumber = 619;
                        PokedexHeightTextNumber = 620;
                    }
                    // One bank per kind holds every foreign language's lines, five per species; Korean's are its own
                    // numbers, so the Korean bank mapping leaves them alone.
                    PokedexForeignSpeciesTableOffset = RomLocator.Where(RomLocator.Thing.PokedexForeignSpecies)?.Offset ?? -1;
                    if (PokedexForeignSpeciesTableOffset >= 0)
                    {
                        (int entries, int names, int categories) = GameLanguage switch
                        {
                            GameLanguages.Japanese => (603, 608, 609),
                            GameLanguages.Korean => (605, 610, 611),
                            _ => (617, 622, 623),
                        };
                        PokedexForeignEntryTextNumbers = Enumerable.Repeat(entries, 5).ToArray();
                        PokedexForeignNameTextNumbers = Enumerable.Repeat(names, 5).ToArray();
                        PokedexForeignCategoryTextNumbers = Enumerable.Repeat(categories, 5).ToArray();
                    }
                    break;

                case GameFamilies.Plat:
                    if (japanese)
                    {
                        PokedexMessagesTextNumber = 685;
                        PokedexEntryTextNumber = 693;
                        PokedexWeightTextNumber = 694;
                        PokedexHeightTextNumber = 696;
                        PokedexCategoryTextNumber = 698;
                        // Japanese games list English, French, German, Italian and Spanish.
                        PokedexForeignEntryTextNumbers = new[] { 688, 689, 690, 691, 692 };
                        PokedexForeignNameTextNumbers = new[] { 699, 700, 701, 702, 703 };
                        PokedexForeignCategoryTextNumbers = new[] { 704, 705, 706, 707, 708 };
                        break;
                    }
                    PokedexCategoryTextNumber = 711;
                    PokedexMessagesTextNumber = 697;
                    PokedexEntryTextNumber = 706;
                    PokedexWeightTextNumber = 707;
                    PokedexHeightTextNumber = 709;
                    PokedexForeignEntryTextNumbers = new[] { 701, 702, 703, 704, 705 };
                    PokedexForeignNameTextNumbers = new[] { 713, 714, 715, 716, 717 };
                    PokedexForeignCategoryTextNumbers = new[] { 719, 720, 721, 722, 723 };
                    break;

                case GameFamilies.HGSS:
                    // Korean's is its own number, so the Korean bank mapping leaves it alone.
                    PlayerCharacterNamesTextNumber = japanese ? 438 : GameLanguage == GameLanguages.Korean ? 440 : 445;
                    RomLocator.Spot areaSort = RomLocator.Where(RomLocator.Thing.PokedexAreaSort);
                    if (areaSort != null)
                    {
                        PokedexFieldZoneTableOffset = RomLocator.Where(RomLocator.Thing.PokedexFieldZones).Offset;
                        PokedexDungeonZoneTableOffset = RomLocator.Where(RomLocator.Thing.PokedexDungeonZones).Offset;
                        PokedexAreaSortTableOffset = areaSort.Offset;
                        PokedexAreaSortTableCount = 142;
                    }
                    if (japanese)
                    {
                        PokedexMessagesTextNumber = 790;
                        PokedexEntryTextNumber = GameVersion == GameVersions.SoulSilver ? 792 : 791;
                        PokedexWeightTextNumber = 799;
                        PokedexHeightTextNumber = 801;
                        PokedexCategoryTextNumber = 803;
                        PokedexForeignEntryTextNumbers = new[] { 793, 794, 795, 796, 797 };
                        PokedexForeignNameTextNumbers = new[] { 804, 805, 806, 807, 808 };
                        PokedexForeignCategoryTextNumbers = new[] { 809, 810, 811, 812, 813 };
                        break;
                    }
                    PokedexMessagesTextNumber = 802;
                    PokedexEntryTextNumber = GameVersion == GameVersions.SoulSilver ? 804 : 803;
                    PokedexWeightTextNumber = 812;
                    PokedexHeightTextNumber = 814;
                    PokedexCategoryTextNumber = 816;
                    PokedexForeignEntryTextNumbers = new[] { 806, 807, 808, 809, 810 };
                    PokedexForeignNameTextNumbers = new[] { 818, 819, 820, 821, 822 };
                    PokedexForeignCategoryTextNumbers = new[] { 824, 825, 826, 827, 828 };
                    break;
            }
        }

        private static void SetAttackNamesTextNumber()
        {
            switch (GameFamily)
            {
                case GameFamilies.DP:
                    AttackNamesTextNumber = GameLanguage == GameLanguages.Japanese ? 575 : 588;
                    break;

                case GameFamilies.Plat:
                    AttackNamesTextNumber = GameLanguage == GameLanguages.Japanese ? 636 : 647;
                    break;

                default:
                    AttackNamesTextNumber = GameLanguage == GameLanguages.Japanese ? 739 : 750;
                    break;
            }
        }

        private static void SetItemsTextNumber()
        {
            switch (GameFamily)
            {
                case GameFamilies.DP:
                    ItemNamesTextNumber = GameLanguage == GameLanguages.Japanese ? 341 : 344;
                    // The bank before the names, in every language.
                    ItemDescriptionsTextNumber = GameLanguage == GameLanguages.Japanese ? 340 : 343;
                    break;

                case GameFamilies.Plat:
                    ItemNamesTextNumber = GameLanguage == GameLanguages.Japanese ? 390 : 392;
                    ItemDescriptionsTextNumber = GameLanguage == GameLanguages.Japanese ? 389 : 391;
                    break;

                default:
                    ItemNamesTextNumber = GameLanguage == GameLanguages.Japanese ? 219 : 222;
                    ItemDescriptionsTextNumber = GameLanguage == GameLanguages.Japanese ? 218 : 221;
                    break;
            }
        }

        private static void SetLocationNamesTextNumber()
        {
            switch (GameFamily)
            {
                case GameFamilies.DP:
                    LocationNamesTextNumber = GameLanguage == GameLanguages.Japanese ? 374 : 382;
                    break;

                case GameFamilies.Plat:
                    LocationNamesTextNumber = GameLanguage == GameLanguages.Japanese ? 427 : 433;
                    break;

                default:
                    LocationNamesTextNumber = GameLanguage == GameLanguages.Japanese ? 272 : 279;
                    break;
            }
        }

        private static void SetPokemonNamesTextNumber()
        {
            switch (GameFamily)
            {
                case GameFamilies.DP:
                    PokemonNamesTextNumbers = GameLanguage == GameLanguages.Japanese
                        ? new int[1] { 356 }
                        : new int[2] { 362, 363 };
                    break;

                case GameFamilies.Plat:
                    PokemonNamesTextNumbers = GameLanguage == GameLanguages.Japanese
                        ? new int[1] { 408 }
                        : new int[7] { 412, 413, 712, 713, 714, 715, 716 }; //413?
                    break;

                case GameFamilies.HGSS:
                    PokemonNamesTextNumbers = GameLanguage.Equals(GameLanguages.Japanese) ? new int[1] { 232 } : new int[7] { 237, 238, 817, 818, 819, 820, 821 }; //238?
                    break;
            }
        }

        private static void SetTrainerNamesMessageNumber()
        {
            switch (GameFamily)
            {
                case GameFamilies.DP:
                    TrainerNamesMessageNumber = 559;
                    if (GameLanguage.Equals(GameLanguages.Japanese))
                    {
                        TrainerNamesMessageNumber -= 9;
                    }
                    break;

                case GameFamilies.Plat:
                    TrainerNamesMessageNumber = GameLanguage == GameLanguages.Japanese ? 611 : 618;
                    break;

                default:
                    TrainerNamesMessageNumber = 729;
                    if (GameLanguage == GameLanguages.Japanese)
                    {
                        TrainerNamesMessageNumber -= 10;
                    }
                    break;
            }
        }

        private static void SetTrainerClassMessageNumber()
        {
            switch (GameFamily)
            {
                case GameFamilies.DP:
                    TrainerClassMessageNumber = 560;
                    if (GameLanguage.Equals(GameLanguages.Japanese))
                    {
                        TrainerClassMessageNumber -= 9;
                    }
                    break;

                case GameFamilies.Plat:
                    // Other languages have a separate "a Jogger"-style article-prefixed variant elsewhere;
                    // Japanese doesn't use articles, so there's no equivalent bank to also branch on here.
                    TrainerClassMessageNumber = GameLanguage == GameLanguages.Japanese ? 612 : 619;
                    break;

                default:
                    TrainerClassMessageNumber = 730;
                    if (GameLanguage.Equals(GameLanguages.Japanese))
                    {
                        TrainerClassMessageNumber -= 10;
                    }
                    break;
            }

            // Japanese has no article bank; the bank after the classes holds other text there.
            TrainerClassWithArticleMessageNumber = GameLanguage == GameLanguages.Japanese ? -1 : TrainerClassMessageNumber + 1;
        }
        // US-version text archive numbers only; not yet confirmed for other localizations.
        private static void SetBattleTowerTextNumbers()
        {
            bool jp = GameLanguage == GameLanguages.Japanese;
            switch (GameFamily)
            {
                case GameFamilies.DP:
                    BattleTowerTrainerNamesMessageNumber = jp ? 15 : 16;
                    BattleTowerTrainerMessagesNumber = jp ? 546 : 555;
                    break;

                case GameFamilies.Plat:
                    BattleTowerTrainerNamesMessageNumber = jp ? 20 : 21;
                    BattleTowerTrainerMessagesNumber = jp ? 607 : 614;
                    break;

                case GameFamilies.HGSS:
                    BattleTowerTrainerNamesMessageNumber = jp ? 26 : 27;
                    BattleTowerTrainerMessagesNumber = jp ? 714 : 724;
                    break;
            }
        }
        private static void SetMoveTextNumbers()
        {
            bool jp = GameLanguage == GameLanguages.Japanese;
            switch (GameFamily)
            {
                case GameFamilies.DP:
                    MoveDescriptionsTextNumbers = jp ? 574 : 587;
                    MoveNamesTextNumbers = jp ? 575 : 588;
                    break;
                case GameFamilies.Plat:
                    MoveDescriptionsTextNumbers = jp ? 635 : 646;
                    MoveNamesTextNumbers = jp ? 636 : 647;
                    break;

                case GameFamilies.HGSS:
                    MoveDescriptionsTextNumbers = jp ? 738 : 749;
                    MoveNamesTextNumbers = jp ? 739 : 750;
                    break;
            }
        }

        private static void SetTrainerFunnyScriptNumber()
        {
            switch (GameFamily)
            {
                case GameFamilies.DP:
                    TrainerFunnyScriptNumber = 851;
                    break;

                case GameFamilies.Plat:
                    TrainerFunnyScriptNumber = 929;
                    break;

                default: // HGSS
                    TrainerFunnyScriptNumber = GameLanguage == GameLanguages.Japanese ? 738 : 740;
                    break;
            }
        }

        private static void SetTypesTextNumber()
        {
            bool jp = GameLanguage == GameLanguages.Japanese;
            switch (GameFamily)
            {
                case GameFamilies.DP:
                    TypesTextNumber = jp ? 555 : 565;
                    break;
                case GameFamilies.Plat:
                    TypesTextNumber = jp ? 616 : 624;
                    break;
                case GameFamilies.HGSS:
                    TypesTextNumber = jp ? 724 : 735;
                    break;
            }
        }

        /// <summary>The text bank holding each in-game trade's nickname, then each trade's OT name.</summary>
        public static int TradeTextNumber => GameFamily switch
        {
            GameFamilies.DP => GameLanguage == GameLanguages.Japanese ? 324 : TextBank(326),
            GameFamilies.Plat => GameLanguage == GameLanguages.Japanese ? 369 : TextBank(370),
            GameFamilies.HGSS => GameLanguage == GameLanguages.Japanese ? 198 : TextBank(200),
            _ => -1,
        };

        /// <summary>The bank holding US text bank <paramref name="usBank"/> in this ROM's language, or -1 when Korean lacks it.</summary>
        // Korean numbers its text banks its own way. Each US bank DSPRE uses, and the Korean bank with the same text,
        // matched by message count and line shape and then read side by side in both languages.
        private static readonly Dictionary<GameFamilies, Dictionary<int, int>> KoreanTextBanks = new()
        {
            [GameFamilies.DP] = new() { [10] = 10, [16] = 15, [320] = 319, [326] = 325, [343] = 341, [344] = 342, [362] = 357, [382] = 376, [552] = 546, [555] = 548, [558] = 551, [559] = 552, [560] = 553, [565] = 557, [587] = 576, [588] = 577, [614] = 602, [615] = 603, [616] = 604, [617] = 605, [619] = 607, [620] = 608, [621] = 609, [622] = 610, [623] = 611 },
            [GameFamilies.Plat] = new() { [12] = 12, [21] = 20, [360] = 359, [361] = 360, [370] = 369, [391] = 389, [392] = 390, [412] = 408, [433] = 428, [610] = 605, [614] = 608, [617] = 611, [618] = 612, [619] = 613, [624] = 617, [646] = 636, [647] = 637, [697] = 687, [701] = 691, [702] = 692, [703] = 693, [704] = 694, [705] = 695, [706] = 696, [707] = 697, [709] = 699, [711] = 701, [712] = 702, [713] = 703, [714] = 704, [715] = 705, [716] = 706, [717] = 707, [719] = 709, [720] = 710, [721] = 711, [722] = 712, [723] = 713 },
            [GameFamilies.HGSS] = new() { [15] = 15, [27] = 26, [30] = 29, [190] = 189, [191] = 190, [196] = 195, [200] = 199, [211] = 210, [221] = 219, [222] = 220, [237] = 233, [246] = 241, [271] = 266, [279] = 274, [435] = 430, [445] = 440, [720] = 715, [724] = 718, [728] = 722, [729] = 723, [730] = 724, [735] = 728, [749] = 742, [750] = 743, [752] = 744, [802] = 795, [803] = 796, [804] = 797, [806] = 799, [807] = 800, [808] = 801, [809] = 802, [810] = 803, [812] = 805, [814] = 807, [816] = 809, [817] = 810, [818] = 811, [819] = 812, [820] = 813, [821] = 814, [822] = 815, [824] = 817, [825] = 818, [826] = 819, [827] = 820, [828] = 821 },
        };

        public static int TextBank(int usBank) =>
            GameLanguage != GameLanguages.Korean || usBank < 0 ? usBank
            : KoreanTextBanks.TryGetValue(GameFamily, out Dictionary<int, int> banks) && banks.TryGetValue(usBank, out int korean) ? korean : -1;

        private static int[] TextBanks(int[] usBanks) => usBanks?.Select(TextBank).Where(b => b >= 0).ToArray();

        // Korean takes the US branches above, so each text number holds its US bank here and is moved to the Korean one.
        // DP's foreign Pokédex banks and HGSS's player names were measured on the Korean dumps and are left as they are.
        private static void SetKoreanTextNumbers()
        {
            StarterScreenTextNumber = TextBank(StarterScreenTextNumber);
            StarterPokedexSpeciesTextNumber = TextBank(StarterPokedexSpeciesTextNumber);
            PokegearPhoneMessageArchive = TextBank(PokegearPhoneMessageArchive);
            FieldMenuEntriesArchive = TextBank(FieldMenuEntriesArchive);
            FieldSharedMessageArchives = TextBanks(FieldSharedMessageArchives);
            FieldTouchMenuTextArchive = TextBank(FieldTouchMenuTextArchive);
            BattleStringsTextNumber = TextBank(BattleStringsTextNumber);
            AbilityNamesTextNumber = TextBank(AbilityNamesTextNumber);
            PokedexMessagesTextNumber = TextBank(PokedexMessagesTextNumber);
            PokedexEntryTextNumber = TextBank(PokedexEntryTextNumber);
            PokedexCategoryTextNumber = TextBank(PokedexCategoryTextNumber);
            PokedexHeightTextNumber = TextBank(PokedexHeightTextNumber);
            PokedexWeightTextNumber = TextBank(PokedexWeightTextNumber);
            if (GameFamily != GameFamilies.DP)
            {
                PokedexForeignEntryTextNumbers = TextBanks(PokedexForeignEntryTextNumbers);
                PokedexForeignNameTextNumbers = TextBanks(PokedexForeignNameTextNumbers);
                PokedexForeignCategoryTextNumbers = TextBanks(PokedexForeignCategoryTextNumbers);
            }
            AttackNamesTextNumber = TextBank(AttackNamesTextNumber);
            PokemonNamesTextNumbers = TextBanks(PokemonNamesTextNumbers);
            ItemNamesTextNumber = TextBank(ItemNamesTextNumber);
            ItemDescriptionsTextNumber = TextBank(ItemDescriptionsTextNumber);
            LocationNamesTextNumber = TextBank(LocationNamesTextNumber);
            TrainerNamesMessageNumber = TextBank(TrainerNamesMessageNumber);
            TrainerClassMessageNumber = TextBank(TrainerClassMessageNumber);
            TrainerClassWithArticleMessageNumber = -1;
            BattleTowerTrainerNamesMessageNumber = TextBank(BattleTowerTrainerNamesMessageNumber);
            BattleTowerTrainerMessagesNumber = TextBank(BattleTowerTrainerMessagesNumber);
            MoveNamesTextNumbers = TextBank(MoveNamesTextNumbers);
            MoveDescriptionsTextNumbers = TextBank(MoveDescriptionsTextNumbers);
            TypesTextNumber = TextBank(TypesTextNumber);
            TrainerMessageTextNumber = TextBank(TrainerMessageTextNumber);
        }

        private static void SetTrainerMessageTextNumber()
        {
            switch (GameFamily)
            {
                case GameFamilies.DP:
                    TrainerMessageTextNumber = GameLanguage == GameLanguages.Japanese ? 549 : 558;
                    break;
                case GameFamilies.Plat:
                    TrainerMessageTextNumber = GameLanguage == GameLanguages.Japanese ? 610 : 617;
                    break;
                case GameFamilies.HGSS:
                    TrainerMessageTextNumber = GameLanguage == GameLanguages.Japanese ? 718 : 728;
                    break;
            }
        }

        private static void SetTrainerNameLenOffset() =>
            TrainerNameLenOffset = RomLocator.Where(RomLocator.Thing.TrainerNameLength)?.Offset ?? -1;

        public static int GetMachineMoveOffset() =>
            RomLocator.Where(RomLocator.Thing.MachineMoves)?.Offset ?? throw new NotSupportedException("Game not supported");

        /// <summary>Whether the egg move limit counts only up to the last Pokémon header rather than the whole table.</summary>
        public static bool EggMoveLimitCountsHeadersOnly => GameFamily == GameFamilies.HGSS;

        /// <summary>
        /// Egg move table limit in bytes. DP/Pt: the space the table has in overlay 5, end marker included.
        /// HGSS: LoadEggMoves (get_egg.c) searches only the first 2045 entries for a Pokémon's header, so
        /// every header must sit inside them; the moves after the last one may run past.
        /// </summary>
        public static int GetEggMoveTableMaxBytes() => GameFamily == GameFamilies.HGSS ? 2045 * 2 : 0xEEC;

        public static int GetEggMoveTableOffset() =>
            RomLocator.Where(RomLocator.Thing.EggMoves)?.Offset ?? throw new NotSupportedException("Game not supported");

        public static int SetTrainerNameMaxLen()
        {
            int maxLength = TrainerFile.DefaultNameLen;
            if (TrainerNameLenOffset > 0)
            {
                using (ARM9.Reader ar = new ARM9.Reader(TrainerNameLenOffset))
                {
                    // The byte is the battle's name buffer size, end mark included.
                    maxLength = ar.ReadByte() - 1;
                }
            }
            return maxLength;
        }

        public static void SetAIBackportEnabled()
        {
            RomLocator.Spot check = RomLocator.Where(RomLocator.Thing.AiBackportCheck);
            RomLocator.Spot outdated = RomLocator.Where(RomLocator.Thing.AiBackportOutdatedCheck);
            if (check == null || outdated == null)
            {
                AIBackportEnabled = OutdatedAIBackportEnabled = false;
                return;
            }

            byte[] bytesAtOffset = ARM9.ReadBytes((uint)check.Offset, 4);

            // Vanilla is F8 B5 9A B0; the backport starts F0 B5 93 B0.
            AIBackportEnabled = bytesAtOffset.SequenceEqual(new byte[] { 0xF0, 0xB5, 0x93, 0xB0 });

            bytesAtOffset = ARM9.ReadBytes((uint)outdated.Offset, 4);
            // Original Backport by Lhea is 1D 1C 0F 23
            // Fixed Backport by YakoSWG is 1E 00 0F 24
            OutdatedAIBackportEnabled = bytesAtOffset.SequenceEqual(new byte[] { 0x1D, 0x1C, 0x0F, 0x23 });
        }

        public string GetBuildingModelsDirPath(bool interior) => interior ? GameDirs[DirNames.InteriorBuildingModels].unpackedDir : GameDirs[DirNames.ExteriorBuildingModels].unpackedDir;

        public static int GetHeaderCount() => (int)new FileInfo(InternalNamesPath).Length / InternalNameLength;

        public static List<string> GetLocationNames() => new TextArchive(LocationNamesTextNumber).Messages;

        // hg-engine generates the trainer name archive from Trainers.c.
        public static string[] GetSimpleTrainerNames() => HgEngine.HgEngineProject.IsActive
            ? TrainerNames.HgEngineSimpleNames()
            : new TextArchive(TrainerNamesMessageNumber).GetSimpleTrainerNames().ToArray();

        public static string[] GetTrainerClassNames()
        {
            // hg-engine rebuilds this archive from its text source, which is newer than the ROM copy.
            HgEngine.HgEngineOwnedFile owned = HgEngine.HgEngineProject.IsActive
                ? HgEngine.HgEngineOwnedFiles.Get(HgEngine.HgEngineOwnedFiles.ArchiveOf(DirNames.TextArchives), TrainerClassMessageNumber) : null;
            if (owned?.Ownership == HgEngine.HgEngineOwnership.EditableSource && HgEngine.HgEngineOwnedFiles.TryReadLines(owned, out List<string> lines, out _))
                return lines.ToArray();
            return new TextArchive(TrainerClassMessageNumber).Messages.ToArray();
        }

        public static string[] GetItemNames()
        {
            string[] names = new TextArchive(ItemNamesTextNumber).Messages.ToArray();
            HgEngine.HgEngineCustomLabel.ApplyItemLabel(names);
            return names;
        }

        public static string[] GetItemNames(int startIndex = 0, int? count = null)
        {
            TextArchive itemNames = new TextArchive(ItemNamesTextNumber);
            return itemNames.Messages.GetRange(startIndex, count == null ? itemNames.Messages.Count - 1 : (int)count).ToArray();
        }

        /// <summary>The last real species id in the retail games; name entries after it are Egg, Bad Egg and forms.</summary>
        public const int LastVanillaSpecies = 493;

        public static string[] GetPokemonNames()
        {
            string[] names = new TextArchive(PokemonNamesTextNumbers[0]).Messages.ToArray();
            HgEngine.HgEngineFormNames.ApplyFallback(names);
            HgEngine.HgEngineCustomLabel.ApplySpeciesLabel(names);
            return names;
        }

        /// <summary>A species by name where the ROM knows one, else "Pokémon 25".</summary>
        public static string PokemonName(int species)
        {
            try
            {
                string[] names = GetPokemonNames();
                return names != null && species >= 0 && species < names.Length && !string.IsNullOrWhiteSpace(names[species])
                    ? names[species] : $"Pokémon {species}";
            }
            catch (Exception ex) { AppLogger.WarnOnce("RomInfo.PokemonName", ex); return $"Pokémon {species}"; }
        }

        public static string[] GetAbilityNames() => new TextArchive(AbilityNamesTextNumber).Messages.ToArray();

        public static string[] GetAttackNames()
        {
            string[] names = new TextArchive(AttackNamesTextNumber).Messages.ToArray();
            HgEngine.HgEngineCustomLabel.ApplyMoveLabel(names);
            return names;
        }

        public static string[] GetTypeNames() => new TextArchive(TypesTextNumber).Messages.ToArray();

        public static int GetLearnsetFilesCount() => NarcAPI.Narc.MemberCount(GameDirs[DirNames.Learnsets].unpackedDir);

        public static int GetPersonalFilesCount() => NarcAPI.Narc.MemberCount(GameDirs[DirNames.PersonalPokeData].unpackedDir);

        /// <summary>Species names padded with alt-form labels up to targetCount, dropping any that don't fit this ROM's real file count.</summary>
        public static string[] GetPokemonNamesWithForms(int targetCount)
        {
            List<string> names = new List<string>(targetCount);
            names.AddRange(GetPokemonNames());

            // Under hg-engine this range is real species (Mega/Gigantamax/etc), not personalExtraFiles pseudo-forms.
            if (HgEngine.HgEngineProject.IsActive)
            {
                HgEngineSymbolTable species = HgEngine.HgEngineSymbolTable.Load("include/constants/species.h");
                while (names.Count < targetCount)
                {
                    int id = names.Count;
                    names.Add(species != null && HgEngine.HgEngineFormNames.TryReadableNameFromConstant(id, species, out string readable)
                        ? readable : $"Extra entry {id}");
                }
                return names.ToArray();
            }

            foreach (PokeDatabase.PersonalData.PersonalExtraFiles extra in PokeDatabase.PersonalData.ExtraFiles)
            {
                if (names.Count >= targetCount) break;
                names.Add($"{names[extra.MonId]} - {extra.Description}");
            }

            while (names.Count < targetCount)
                names.Add($"Extra entry {names.Count}");

            return names.ToArray();
        }

        public static string[] GetEvolutionFilesList() => Directory.GetFiles(GameDirs[DirNames.Evolutions].unpackedDir);

        public static int GetEvolutionFilesCount() => GetEvolutionFilesList().Length;
        public static string[] GetBattleEffectSequenceFiles() => Directory.GetFiles(GameDirs[DirNames.MoveData].unpackedDir);
        public static int GetBattleEffectSequenceFilesCount() => GetBattleEffectSequenceFiles().Length;

        public int GetAreaDataCount() => NarcAPI.Narc.MemberCount(GameDirs[DirNames.AreaData].unpackedDir);

        public int GetMapTexturesCount() => NarcAPI.Narc.MemberCount(GameDirs[DirNames.MapTextures].unpackedDir);

        public int GetBuildingTexturesCount() => NarcAPI.Narc.MemberCount(GameDirs[DirNames.BuildingTextures].unpackedDir);

        public int GetMatrixCount() => NarcAPI.Narc.MemberCount(GameDirs[DirNames.Matrices].unpackedDir);

        public int GetTextArchivesCount() => NarcAPI.Narc.MemberCount(TextConverter.GetExpandedFolderPath());

        public int GetMapCount() => NarcAPI.Narc.MemberCount(GameDirs[DirNames.Maps].unpackedDir);

        public int GetEventCount() => NarcAPI.Narc.MemberCount(GameDirs[DirNames.EventFiles].unpackedDir);

        public int GetScriptCount() => NarcAPI.Narc.MemberCount(GameDirs[DirNames.Scripts].unpackedDir);

        public int GetBuildingCount(bool interior) => NarcAPI.Narc.MemberCount(GetBuildingModelsDirPath(interior));

        public static int GetEventFileCount() => NarcAPI.Narc.MemberCount(RomInfo.GameDirs[DirNames.EventFiles].unpackedDir);

        #endregion Methods (22)

        #region System Methods

        private void LoadGameLanguage()
        {
            switch (RomID)
            {
                case "ADAE":
                case "APAE":
                case "CPUE":
                case "IPKE":
                case "IPGE":
                    GameLanguage = GameLanguages.English;
                    break;

                case "ADAS":
                case "APAS":
                case "CPUS":
                case "IPKS":
                case "IPGS":
                case "LATA":
                    GameLanguage = GameLanguages.Spanish;
                    break;

                case "ADAI":
                case "APAI":
                case "CPUI":
                case "IPKI":
                case "IPGI":
                    GameLanguage = GameLanguages.Italian;
                    break;

                case "ADAF":
                case "APAF":
                case "CPUF":
                case "IPKF":
                case "IPGF":
                    GameLanguage = GameLanguages.French;
                    break;

                case "ADAD":
                case "APAD":
                case "CPUD":
                case "IPKD":
                case "IPGD":
                    GameLanguage = GameLanguages.German;
                    break;

                case "ADAK":
                case "APAK":
                case "CPUK":
                case "IPKK":
                case "IPGK":
                    GameLanguage = GameLanguages.Korean;
                    break;

                default:
                    GameLanguage = GameLanguages.Japanese;
                    break;
            }
        }

        private void LoadGameFamily()
        {
            switch (GameVersion)
            {
                case GameVersions.Diamond:
                case GameVersions.Pearl:
                    GameFamily = GameFamilies.DP;
                    break;

                case GameVersions.Platinum:
                    GameFamily = GameFamilies.Plat;
                    break;

                case GameVersions.HeartGold:
                case GameVersions.SoulSilver:
                    GameFamily = GameFamilies.HGSS;
                    break;
            }
        }

        private void SetNarcDirs()
        {
            Dictionary<DirNames, string> packedDirsDict = null;
            // French, German, Italian, Spanish and Korean DP and Platinum load their screen graphics with text in them from
            // resource/<language>/ (the game's RESOURCE_DIR); English and Japanese keep them in the plain folders.
            string Localized(string plain, string folder, string file)
            {
                string lang = GameLanguage switch
                {
                    GameLanguages.French => "fra", GameLanguages.German => "ger",
                    GameLanguages.Italian => "ita", GameLanguages.Spanish => "spa", GameLanguages.Korean => "kor", _ => null,
                };
                return lang == null ? plain : $@"{_dataFolderName}\resource\{lang}\{folder}\{file}";
            }
            switch (GameFamily)
            {
                case GameFamilies.DP:
                    string suffix = "";
                    if (!GameLanguage.Equals(GameLanguages.Japanese))
                    {
                        suffix = "_release";
                    }

                    packedDirsDict = new Dictionary<DirNames, string>()
                    {
                        [DirNames.SynthOverlay] = $@"{_dataFolderName}\data\weather_sys.narc",
                        [DirNames.TextArchives] = $@"{_dataFolderName}\msgdata\msg.narc",
                        [DirNames.Fonts] = $@"{_dataFolderName}\graphic\font.narc",
                        [DirNames.WindowFrames] = $@"{_dataFolderName}\graphic\winframe.narc",
                        [DirNames.SummaryGraphics] = Localized($@"{_dataFolderName}\graphic\pst_gra.narc", "p_status", "pst_gra.narc"),
                        [DirNames.MoveRelearnerGraphics] = $@"{_dataFolderName}\graphic\waza_oshie_gra.narc",
                        [DirNames.BattlePartyGraphics] = Localized($@"{_dataFolderName}\battle\graphic\b_plist_gra.narc", "b_plist", "b_plist_gra.narc"),
                        [DirNames.BagGraphics] = Localized($@"{_dataFolderName}\graphic\bag_gra.narc", "bag", "bag_gra.narc"),
                        [DirNames.FootprintGraphics] = $@"{_dataFolderName}\poketool\pokefoot\pokefoot.narc",

                        [DirNames.Matrices] = $@"{_dataFolderName}\fielddata\mapmatrix\map_matrix.narc",

                        [DirNames.Maps] = $@"{_dataFolderName}\fielddata\land_data\land_data" + suffix + ".narc",
                        [DirNames.ExteriorBuildingModels] = $@"{_dataFolderName}\fielddata\build_model\build_model.narc",
                        [DirNames.BuildingConfigFiles] = $@"{_dataFolderName}\fielddata\areadata\area_build_model\area_build.narc",
                        [DirNames.BuildingTextures] = $@"{_dataFolderName}\fielddata\areadata\area_build_model\areabm_texset.narc",
                        [DirNames.MapTextures] = $@"{_dataFolderName}\fielddata\areadata\area_map_tex\map_tex_set.narc",
                        [DirNames.AreaData] = $@"{_dataFolderName}\fielddata\areadata\area_data.narc",

                        // DP/Pt has no terrain animation (its area record uses that slot for the
                        // animated model set) and keeps one combined building-animation list.
                        [DirNames.BuildingAnimations] = $@"{_dataFolderName}\arc\bm_anime.narc",
                        [DirNames.BuildingAnimListOut] = $@"{_dataFolderName}\arc\bm_anime_list.narc",

                        [DirNames.EventFiles] = $@"{_dataFolderName}\fielddata\eventdata\zone_event" + suffix + ".narc",
                        [DirNames.OWSprites] = $@"{_dataFolderName}\data\mmodel\mmodel.narc",
                        [DirNames.FieldEffectModels] = $@"{_dataFolderName}\data\mmodel\fldeff.narc",

                        [DirNames.Scripts] = $@"{_dataFolderName}\fielddata\script\scr_seq" + suffix + ".narc",

                        [DirNames.TrainerProperties] = $@"{_dataFolderName}\poketool\trainer\trdata.narc",
                        [DirNames.TrainerParty] = $@"{_dataFolderName}\poketool\trainer\trpoke.narc",
                        [DirNames.TrainerGraphics] = $@"{_dataFolderName}\poketool\trgra\trfgra.narc",
                        [DirNames.TrainerBackGraphics] = $@"{_dataFolderName}\poketool\trgra\trbgra.narc",
                        [DirNames.Poketch] = Localized($@"{_dataFolderName}\graphic\poketch.narc", "poketch", "poketch.narc"),
                        [DirNames.UndergroundParts] = $@"{_dataFolderName}\data\ug_parts.narc",
                        [DirNames.TrainerCardGraphics] = Localized($@"{_dataFolderName}\graphic\trainer_case.narc", "trainer_case", "trainer_case.narc"),
                        [DirNames.EncounterEffectGraphics] = $@"{_dataFolderName}\graphic\field_encounteffect.narc",
                        [DirNames.NameInputGraphics] = Localized($@"{_dataFolderName}\data\namein.narc", "nameinput", "namein.narc"),
                        [DirNames.MoveData] = $@"{_dataFolderName}\poketool\waza\waza_tbl.narc",

                        [DirNames.MonIcons] = $@"{_dataFolderName}\poketool\icongra\poke_icon.narc",

                        [DirNames.Encounters] = $@"{_dataFolderName}\fielddata\encountdata\" + char.ToLower(GameVersion.ToString()[0]) + '_' + "enc_data.narc",
                        [DirNames.EncounterExtended] = $@"{_dataFolderName}\arc\encdata_ex.narc",
                        [DirNames.Learnsets] = $@"{_dataFolderName}\poketool\personal\wotbl.narc",
                        [DirNames.Evolutions] = $@"{_dataFolderName}\poketool\personal\evo.narc",
                        [DirNames.GrowthTable] = $@"{_dataFolderName}\poketool\personal\growtbl.narc",
                        [DirNames.BerryData] = $@"{_dataFolderName}\itemtool\itemdata\nuts_data.narc",

                        [DirNames.BattleTowerTrainers] = $@"{_dataFolderName}\battle\b_tower\btdtr.narc",
                        [DirNames.BattleTowerPokemon] = $@"{_dataFolderName}\battle\b_tower\btdpm.narc",

                        [DirNames.PokemonBattleSprites] = $@"{_dataFolderName}\poketool\pokegra\pokegra.narc",
                        [DirNames.OtherPokemonBattleSprites] = $@"{_dataFolderName}\poketool\pokegra\otherpoke.narc",

                        // DP keeps the battle-sprite offsets in separate NARCs.
                        [DirNames.PokeYofs] = $@"{_dataFolderName}\poketool\pokegra\poke_yofs.narc",
                        [DirNames.PokeShadowOfx] = $@"{_dataFolderName}\poketool\pokegra\poke_shadow_ofx.narc",
                        [DirNames.PokeShadow] = $@"{_dataFolderName}\poketool\pokegra\poke_shadow.narc",
                        [DirNames.PokeHeight] = $@"{_dataFolderName}\poketool\pokegra\height.narc",
                        [DirNames.PokeHeightForms] = $@"{_dataFolderName}\poketool\pokegra\height_o.narc",
                        [DirNames.PokeAnim] = $@"{_dataFolderName}\poketool\pokeanm\pokeanm.narc",
                        [DirNames.PokeAnimDefs] = $@"{_dataFolderName}\pokeanime\poke_anm.narc",
                        [DirNames.WazaSeq] = $@"{_dataFolderName}\battle\skill\waza_seq.narc",
                        [DirNames.SubSeq] = $@"{_dataFolderName}\battle\skill\sub_seq.narc",
                        [DirNames.BeSeq] = $@"{_dataFolderName}\battle\skill\be_seq.narc",
                        [DirNames.WazaEffectScripts] = $@"{_dataFolderName}\wazaeffect\we.arc",
                        [DirNames.WazaEffectSub] = $@"{_dataFolderName}\wazaeffect\we_sub.narc",
                        [DirNames.WazaEffectChar] = $@"{_dataFolderName}\wazaeffect\effectclact\wechar.narc",
                        [DirNames.WazaEffectPltt] = $@"{_dataFolderName}\wazaeffect\effectclact\wepltt.narc",
                        [DirNames.WazaEffectCell] = $@"{_dataFolderName}\wazaeffect\effectclact\wecell.narc",
                        [DirNames.WazaEffectCellAnm] = $@"{_dataFolderName}\wazaeffect\effectclact\wecellanm.narc",
                        [DirNames.WazaParticle] = $@"{_dataFolderName}\wazaeffect\effectdata\waza_particle.narc",
                        [DirNames.BattleBg] = $@"{_dataFolderName}\battle\graphic\batt_bg.narc",
                        [DirNames.BattleObj] = Localized($@"{_dataFolderName}\battle\graphic\batt_obj.narc", "battle_graphic", "batt_obj.narc"),

                        [DirNames.ItemData] = $@"{_dataFolderName}\itemtool\itemdata\item_data.narc",
                        [DirNames.ItemIcons] = $@"{_dataFolderName}\itemtool\itemdata\item_icon.narc",

                        [DirNames.TradeData] = $@"{_dataFolderName}\fielddata\pokemon_trade\fld_trade.narc",

                        [DirNames.TrainerTextOffset] = $@"{_dataFolderName}\poketool\trmsg\trtblofs.narc",
                        [DirNames.TrainerTextTable] = $@"{_dataFolderName}\poketool\trmsg\trtbl.narc",

                        [DirNames.EggMoves] = $@"{_customNarcFolderName}/egg_moves.narc",

                        [DirNames.BallParticles] = $@"{_dataFolderName}\wazaeffect\effectdata\ball_particle.narc",
                        [DirNames.SealGraphics] = $@"{_dataFolderName}\application\custom_ball\data\cb_data.narc",
                        [DirNames.PokedexData] = $@"{_dataFolderName}\application\zukanlist\zkn_data\zukan_data.narc",
                        [DirNames.RegionalDexNumbers] = $@"{_dataFolderName}\poketool\pokezukan.narc",
                        [DirNames.RegionalDexSpecies] = $@"{_dataFolderName}\poketool\shinzukan.narc",
                        [DirNames.PokedexGraphics] = GameLanguage == GameLanguages.Japanese
                            ? $@"{_dataFolderName}\graphic\zukan.narc"
                            : Localized($@"{_dataFolderName}\resource\eng\zukan\zukan.narc", "zukan", "zukan.narc"),
                        [DirNames.PokedexAreas] = GameVersion == GameVersions.Pearl
                            ? $@"{_dataFolderName}\application\zukanlist\zkn_data\zukan_enc_pearl.narc"
                            : $@"{_dataFolderName}\application\zukanlist\zkn_data\zukan_enc_diamond.narc",
                    };

                    //Personal Data archive is different for Pearl
                    string personal = $@"{_dataFolderName}\poketool\personal";
                    if (GameVersion == GameVersions.Pearl)
                    {
                        personal += ("_" + GameVersion.ToString().ToLower());
                    }
                    personal += @"\personal.narc";
                    packedDirsDict[DirNames.PersonalPokeData] = personal;

                    if (GameLanguage != GameLanguages.Japanese && GameLanguage != GameLanguages.English)
                    {
                        packedDirsDict[DirNames.TradeData] = $@"{_dataFolderName}\resource\{GetLangResFolderName()}\pokemon_trade\fld_trade.narc";
                    }

                    break;

                case GameFamilies.Plat:
                    suffix = GameVersion.ToString().Substring(0, 2).ToLower();

                    packedDirsDict = new Dictionary<DirNames, string>()
                    {
                        [DirNames.PersonalPokeData] = $@"{_dataFolderName}\poketool\personal\pl_personal.narc",

                        [DirNames.PokemonBattleSprites] = $@"{_dataFolderName}\poketool\pokegra\pl_pokegra.narc",
                        [DirNames.OtherPokemonBattleSprites] = $@"{_dataFolderName}\poketool\pokegra\pl_otherpoke.narc",
                        // Platinum bundles the battle-sprite offsets (last 3 bytes/record = sprite Y, shadow X, shadow size) here, HGSS-style.
                        [DirNames.PokemonSpriteOffsets] = $@"{_dataFolderName}\poketool\poke_edit\pl_poke_data.narc",
                        // Per-gender sprite heights live in their own NARC on Platinum too (same as DP).
                        [DirNames.PokeHeight] = $@"{_dataFolderName}\poketool\pokegra\height.narc",
                        [DirNames.PokeHeightForms] = $@"{_dataFolderName}\poketool\pokegra\height_o.narc",
                        [DirNames.PokeAnim] = $@"{_dataFolderName}\poketool\pokeanm\pl_pokeanm.narc",   // Platinum's animation table (pl_ prefix)
                        [DirNames.PokeAnimDefs] = $@"{_dataFolderName}\pokeanime\pl_poke_anm.narc",   // Platinum's Pokémon animation scripts
                        // Platinum keeps the move-sequence + effect scripts under shared (non-pl_) names.
                        [DirNames.WazaSeq] = $@"{_dataFolderName}\battle\skill\waza_seq.narc",
                        [DirNames.SubSeq] = $@"{_dataFolderName}\battle\skill\sub_seq.narc",
                        [DirNames.BeSeq] = $@"{_dataFolderName}\battle\skill\be_seq.narc",
                        [DirNames.WazaEffectScripts] = $@"{_dataFolderName}\wazaeffect\we.arc",
                        [DirNames.WazaEffectSub] = $@"{_dataFolderName}\wazaeffect\we_sub.narc",
                        [DirNames.WazaEffectChar] = $@"{_dataFolderName}\wazaeffect\effectclact\wechar.narc",
                        [DirNames.WazaEffectPltt] = $@"{_dataFolderName}\wazaeffect\effectclact\wepltt.narc",
                        [DirNames.WazaEffectCell] = $@"{_dataFolderName}\wazaeffect\effectclact\wecell.narc",
                        [DirNames.WazaEffectCellAnm] = $@"{_dataFolderName}\wazaeffect\effectclact\wecellanm.narc",
                        [DirNames.WazaParticle] = $@"{_dataFolderName}\wazaeffect\effectdata\waza_particle.narc",
                        [DirNames.BattleBg] = $@"{_dataFolderName}\battle\graphic\pl_batt_bg.narc",
                        [DirNames.BattleObj] = Localized($@"{_dataFolderName}\battle\graphic\pl_batt_obj.narc", "battle_graphic", "pl_batt_obj.narc"),
                        [DirNames.BallParticles] = $@"{_dataFolderName}\wazaeffect\effectdata\ball_particle.narc",
                        [DirNames.TrainerBackGraphics] = $@"{_dataFolderName}\poketool\trgra\trbgra.narc",
                        [DirNames.TrainerCardGraphics] = Localized($@"{_dataFolderName}\graphic\trainer_case.narc", "trainer_case", "trainer_case.narc"),
                        [DirNames.EncounterEffectGraphics] = $@"{_dataFolderName}\graphic\field_encounteffect.narc",
                        [DirNames.NameInputGraphics] = Localized($@"{_dataFolderName}\data\namein.narc", "nameinput", "namein.narc"),

                        [DirNames.SynthOverlay] = $@"{_dataFolderName}\data\weather_sys.narc",
                        [DirNames.DynamicHeaders] = $@"{_dataFolderName}\debug\cb_edit\d_test.narc",
                        [DirNames.AreaWindowGraphics] = $@"{_dataFolderName}\arc\area_win_gra.narc",
                        [DirNames.StartMenuGraphics] = $@"{_dataFolderName}\graphic\menu_gra.narc",
                        [DirNames.PartyMenuGraphics] = $@"{_dataFolderName}\graphic\pl_plist_gra.narc",
                        [DirNames.BoxGraphics] = $@"{_dataFolderName}\graphic\box.narc",
                        [DirNames.OptionsGraphics] = $@"{_dataFolderName}\graphic\config_gra.narc",
                        [DirNames.SealGraphics] = $@"{_dataFolderName}\application\custom_ball\data\cb_data.narc",
                        [DirNames.TrainerCapsules] = $@"{_dataFolderName}\application\custom_ball\edit\pl_cb_data.narc",
                        [DirNames.PokedexData] = $@"{_dataFolderName}\application\zukanlist\zkn_data\zukan_data.narc",
                        [DirNames.PokedexDataAltered] = $@"{_dataFolderName}\application\zukanlist\zkn_data\zukan_data_gira.narc",
                        [DirNames.PokedexAreas] = $@"{_dataFolderName}\application\zukanlist\zkn_data\zukan_enc_platinum.narc",
                        [DirNames.RegionalDexNumbers] = $@"{_dataFolderName}\poketool\pl_pokezukan.narc",
                        [DirNames.RegionalDexSpecies] = $@"{_dataFolderName}\poketool\shinzukan.narc",
                        [DirNames.PokedexGraphics] = GameLanguage == GameLanguages.Japanese
                            ? $@"{_dataFolderName}\graphic\zukan.narc"
                            : Localized($@"{_dataFolderName}\resource\eng\zukan\zukan.narc", "zukan", "zukan.narc"),

                        [DirNames.TornWorld] = $@"{_dataFolderName}\fielddata\tornworld\tw_arc.narc",
                        [DirNames.TornWorldAttributes] = $@"{_dataFolderName}\fielddata\tornworld\tw_arc_attr.narc",

                        [DirNames.TextArchives] = $@"{_dataFolderName}\msgdata\" + suffix + '_' + "msg.narc",
                        [DirNames.Fonts] = $@"{_dataFolderName}\graphic\pl_font.narc",
                        [DirNames.WindowFrames] = $@"{_dataFolderName}\graphic\pl_winframe.narc",
                        [DirNames.SummaryGraphics] = Localized($@"{_dataFolderName}\graphic\pl_pst_gra.narc", "p_status", "pl_pst_gra.narc"),
                        [DirNames.MoveRelearnerGraphics] = $@"{_dataFolderName}\graphic\waza_oshie_gra.narc",
                        [DirNames.BattlePartyGraphics] = Localized($@"{_dataFolderName}\battle\graphic\pl_b_plist_gra.narc", "b_plist", "pl_b_plist_gra.narc"),
                        [DirNames.BagGraphics] = Localized($@"{_dataFolderName}\graphic\pl_bag_gra.narc", "bag", "pl_bag_gra.narc"),
                        [DirNames.FootprintGraphics] = $@"{_dataFolderName}\poketool\pokefoot\pokefoot.narc",
                        [DirNames.Poketch] = Localized($@"{_dataFolderName}\graphic\poketch.narc", "poketch", "poketch.narc"),
                        [DirNames.UndergroundParts] = $@"{_dataFolderName}\data\ug_parts.narc",

                        [DirNames.Matrices] = $@"{_dataFolderName}\fielddata\mapmatrix\map_matrix.narc",

                        [DirNames.Maps] = $@"{_dataFolderName}\fielddata\land_data\land_data.narc",
                        [DirNames.ExteriorBuildingModels] = $@"{_dataFolderName}\fielddata\build_model\build_model.narc",
                        [DirNames.BuildingConfigFiles] = $@"{_dataFolderName}\fielddata\areadata\area_build_model\area_build.narc",
                        [DirNames.BuildingTextures] = $@"{_dataFolderName}\fielddata\areadata\area_build_model\areabm_texset.narc",
                        [DirNames.MapTextures] = $@"{_dataFolderName}\fielddata\areadata\area_map_tex\map_tex_set.narc",
                        [DirNames.FieldTextureAnimations] = $@"{_dataFolderName}\data\fldtanime.narc",
                        [DirNames.AreaData] = $@"{_dataFolderName}\fielddata\areadata\area_data.narc",

                        // DP/Pt has no terrain animation (its area record uses that slot for the
                        // animated model set) and keeps one combined building-animation list.
                        [DirNames.BuildingAnimations] = $@"{_dataFolderName}\arc\bm_anime.narc",
                        [DirNames.BuildingAnimListOut] = $@"{_dataFolderName}\arc\bm_anime_list.narc",

                        [DirNames.EventFiles] = $@"{_dataFolderName}\fielddata\eventdata\zone_event.narc",
                        [DirNames.OWSprites] = $@"{_dataFolderName}\data\mmodel\mmodel.narc",
                        [DirNames.FieldEffectModels] = $@"{_dataFolderName}\data\mmodel\fldeff.narc",

                        [DirNames.Scripts] = $@"{_dataFolderName}\fielddata\script\scr_seq.narc",

                        [DirNames.TrainerProperties] = $@"{_dataFolderName}\poketool\trainer\trdata.narc",
                        [DirNames.TrainerParty] = $@"{_dataFolderName}\poketool\trainer\trpoke.narc",
                        [DirNames.TrainerGraphics] = $@"{_dataFolderName}\poketool\trgra\trfgra.narc",
                        [DirNames.MoveData] = $@"{_dataFolderName}\poketool\waza\pl_waza_tbl.narc",

                        [DirNames.MonIcons] = $@"{_dataFolderName}\poketool\icongra\pl_poke_icon.narc",

                        [DirNames.Encounters] = $@"{_dataFolderName}\fielddata\encountdata\" + suffix + '_' + "enc_data.narc",
                        [DirNames.EncounterExtended] = $@"{_dataFolderName}\arc\encdata_ex.narc",
                        [DirNames.Learnsets] = $@"{_dataFolderName}\poketool\personal\wotbl.narc",
                        [DirNames.Evolutions] = $@"{_dataFolderName}\poketool\personal\evo.narc",
                        // Platinum reads only pl_growtbl; its growtbl.narc has no reader.
                        [DirNames.GrowthTable] = $@"{_dataFolderName}\poketool\personal\pl_growtbl.narc",
                        [DirNames.BerryData] = $@"{_dataFolderName}\itemtool\itemdata\nuts_data.narc",

                        [DirNames.BattleTowerTrainers] = $@"{_dataFolderName}\battle\b_pl_tower\pl_btdtr.narc",
                        [DirNames.BattleTowerPokemon] = $@"{_dataFolderName}\battle\b_pl_tower\pl_btdpm.narc",

                        [DirNames.ItemData] = $@"{_dataFolderName}\itemtool\itemdata\pl_item_data.narc",
                        [DirNames.ItemIcons] = $@"{_dataFolderName}\itemtool\itemdata\item_icon.narc",

                        [DirNames.TradeData] = $@"{_dataFolderName}\fielddata\pokemon_trade\fld_trade.narc",

                        [DirNames.TrainerTextOffset] = $@"{_dataFolderName}\poketool\trmsg\trtblofs.narc",
                        [DirNames.TrainerTextTable] = $@"{_dataFolderName}\poketool\trmsg\trtbl.narc",

                        [DirNames.EggMoves] = $@"{_customNarcFolderName}/egg_moves.narc",
                    };

                    if (GameLanguage != GameLanguages.Japanese && GameLanguage != GameLanguages.English)
                    {
                        packedDirsDict[DirNames.TradeData] = $@"{_dataFolderName}\resource\{GetLangResFolderName()}\pokemon_trade\fld_trade.narc";
                    }

                    break;

                case GameFamilies.HGSS:
                    packedDirsDict = new Dictionary<DirNames, string>()
                    {
                        [DirNames.PersonalPokeData] = $@"{_dataFolderName}\a\0\0\2",
                        [DirNames.PokemonBattleSprites] = $@"{_dataFolderName}\a\0\0\4",
                        [DirNames.OtherPokemonBattleSprites] = $@"{_dataFolderName}\a\1\1\4",
                        [DirNames.PokeHeightForms] = $@"{_dataFolderName}\a\1\1\7",
                        [DirNames.PokemonSpriteOffsets] = $@"{_dataFolderName}\a\1\8\0",
                        [DirNames.PokeHeight] = $@"{_dataFolderName}\a\0\0\5",
                        [DirNames.PokeAnim] = $@"{_dataFolderName}\a\1\1\1",   // Pokeanm.narc (28 B/mon POKE_ANM_DATA table)
                        [DirNames.PokeAnimDefs] = $@"{_dataFolderName}\a\0\9\0",   // Pokémon animation scripts (poke_anm equivalent)
                        [DirNames.WazaSeq] = $@"{_dataFolderName}\a\0\0\0",   // move-sequence scripts (waza_seq)
                        [DirNames.SubSeq] = $@"{_dataFolderName}\a\0\0\1",   // shared subroutines (sub_seq)
                        [DirNames.BeSeq] = $@"{_dataFolderName}\a\0\3\0",   // per-effect scripts (be_seq)
                        [DirNames.WazaEffectScripts] = $@"{_dataFolderName}\a\0\1\0",   // move animations (we.arc)
                        [DirNames.WazaEffectSub] = $@"{_dataFolderName}\a\0\6\1",   // continuous animations (we_sub)
                        [DirNames.WazaEffectChar] = $@"{_dataFolderName}\a\0\2\2",   // wechar (NCGR)
                        [DirNames.WazaEffectPltt] = $@"{_dataFolderName}\a\0\2\3",   // wepltt (NCLR)
                        [DirNames.WazaEffectCell] = $@"{_dataFolderName}\a\0\2\4",   // wecell (NCER)
                        [DirNames.WazaEffectCellAnm] = $@"{_dataFolderName}\a\0\2\5",   // wecellanm (NANR)
                        [DirNames.WazaParticle] = $@"{_dataFolderName}\a\0\2\9",   // waza_particle (SPA)
                        [DirNames.BattleBg] = $@"{_dataFolderName}\a\0\0\7",   // battle and move-effect backgrounds
                        [DirNames.BattleObj] = $@"{_dataFolderName}\a\0\0\8",   // battle OBJ / terrain ground platforms
                        [DirNames.BallParticles] = $@"{_dataFolderName}\a\0\9\5",
                        [DirNames.TrainerBackGraphics] = $@"{_dataFolderName}\a\0\0\6",
                        [DirNames.BattleBgPlanm] = $@"{_dataFolderName}\a\0\0\9",   // HGSS-only SwitchBgEx anim data

                        [DirNames.BattleTowerTrainers] = $@"{_dataFolderName}\a\2\0\2",
                        [DirNames.BattleTowerPokemon] = $@"{_dataFolderName}\a\2\0\3",

                        [DirNames.SynthOverlay] = $@"{_dataFolderName}\a\0\2\8",
                        [DirNames.FieldEffectModels] = $@"{_dataFolderName}\a\1\0\3",   // field effect models, Flash's light spot among them
                        [DirNames.DynamicHeaders] = $@"{_dataFolderName}\a\0\5\0",
                        // An empty placeholder in retail; the external trainer class metadata patch (PR #272) keeps its records here.
                        [DirNames.TrainerClassMetadata] = $@"{_dataFolderName}\a\1\5\5",
                        [DirNames.AreaWindowGraphics] = $@"{_dataFolderName}\a\1\6\3",
                        [DirNames.SealGraphics] = $@"{_dataFolderName}\a\0\8\7",
                        [DirNames.TrainerCapsules] = $@"{_dataFolderName}\a\1\8\5",
                        [DirNames.PokedexData] = $@"{_dataFolderName}\a\0\7\4",
                        [DirNames.PokedexDataAltered] = $@"{_dataFolderName}\a\2\1\4",
                        [DirNames.PokedexAreas] = $@"{_dataFolderName}\a\1\3\3",
                        [DirNames.RegionalDexNumbers] = $@"{_dataFolderName}\a\1\3\8",
                        [DirNames.PokedexSearchSteps] = $@"{_dataFolderName}\a\0\7\5",

                        [DirNames.TextArchives] = $@"{_dataFolderName}\a\0\2\7",
                        [DirNames.Fonts] = $@"{_dataFolderName}\a\0\1\6",
                        [DirNames.WindowFrames] = $@"{_dataFolderName}\a\0\3\8",
                        [DirNames.FieldTouchMenu] = $@"{_dataFolderName}\a\0\1\4",
                        [DirNames.PokegearGraphics] = $@"{_dataFolderName}\a\1\4\3",
                        [DirNames.PokegearMapGraphics] = $@"{_dataFolderName}\a\1\4\4",
                        [DirNames.PokegearPhoneGraphics] = $@"{_dataFolderName}\a\1\4\6",
                        [DirNames.PokegearRadioGraphics] = $@"{_dataFolderName}\a\1\4\7",
                        [DirNames.PartyMenuGraphics] = $@"{_dataFolderName}\a\0\2\1",
                        [DirNames.BoxGraphics] = $@"{_dataFolderName}\a\0\1\9",
                        [DirNames.OptionsGraphics] = $@"{_dataFolderName}\a\0\7\2",
                        [DirNames.FieldTouchChoices] = $@"{_dataFolderName}\a\2\3\7",
                        [DirNames.SummaryGraphics] = $@"{_dataFolderName}\a\1\6\2",
                        [DirNames.SummarySprites] = $@"{_dataFolderName}\a\0\3\9",
                        [DirNames.MoveRelearnerGraphics] = $@"{_dataFolderName}\a\1\1\0",
                        [DirNames.BattlePartyGraphics] = $@"{_dataFolderName}\a\0\7\1",
                        [DirNames.BagGraphics] = $@"{_dataFolderName}\a\0\1\5",
                        [DirNames.FootprintGraphics] = $@"{_dataFolderName}\a\0\6\9",
                        [DirNames.PokedexGraphics] = $@"{_dataFolderName}\a\0\6\8",
                        [DirNames.OpeningDemoGraphics] = $@"{_dataFolderName}\a\2\6\2",
                        [DirNames.PokewalkerSprites] = $@"{_dataFolderName}\a\2\5\6",
                        [DirNames.PokewalkerIcons] = $@"{_dataFolderName}\a\2\4\8",

                        [DirNames.Matrices] = $@"{_dataFolderName}\a\0\4\1",

                        [DirNames.Maps] = $@"{_dataFolderName}\a\0\6\5",
                        [DirNames.ExteriorBuildingModels] = $@"{_dataFolderName}\a\0\4\0",
                        [DirNames.BuildingConfigFiles] = $@"{_dataFolderName}\a\0\4\3",
                        [DirNames.BuildingTextures] = $@"{_dataFolderName}\a\0\7\0",
                        [DirNames.MapTextures] = $@"{_dataFolderName}\a\0\4\4",
                        [DirNames.FieldTextureAnimations] = $@"{_dataFolderName}\a\1\3\9",
                        [DirNames.AreaData] = $@"{_dataFolderName}\a\0\4\2",

                        [DirNames.GroundAnimations] = $@"{_dataFolderName}\a\1\4\0",
                        [DirNames.BuildingAnimations] = $@"{_dataFolderName}\a\1\0\6",
                        [DirNames.BuildingAnimListOut] = $@"{_dataFolderName}\a\1\0\7",
                        [DirNames.BuildingAnimListIn] = $@"{_dataFolderName}\a\1\0\8",

                        [DirNames.EventFiles] = $@"{_dataFolderName}\a\0\3\2",
                        [DirNames.OWSprites] = $@"{_dataFolderName}\a\0\8\1",

                        [DirNames.Scripts] = $@"{_dataFolderName}\a\0\1\2",
                        //ENCOUNTERS FOLDER DEPENDS ON VERSION
                        [DirNames.TrainerProperties] = $@"{_dataFolderName}\a\0\5\5",
                        [DirNames.TrainerParty] = $@"{_dataFolderName}\a\0\5\6",
                        [DirNames.TrainerGraphics] = $@"{_dataFolderName}\a\0\5\8",
                        [DirNames.MoveData] = $@"{_dataFolderName}\a\0\1\1",

                        [DirNames.MonIcons] = $@"{_dataFolderName}\a\0\2\0",

                        [DirNames.InteriorBuildingModels] = $@"{_dataFolderName}\a\1\4\8",
                        [DirNames.Learnsets] = $@"{_dataFolderName}\a\0\3\3",
                        [DirNames.Evolutions] = $@"{_dataFolderName}\a\0\3\4",
                        [DirNames.GrowthTable] = $@"{_dataFolderName}\a\0\0\3",
                        [DirNames.BerryData] = $@"{_dataFolderName}\a\0\6\6",
                        [DirNames.ItemData] = $@"{_dataFolderName}\a\0\1\7",
                        [DirNames.ItemIcons] = $@"{_dataFolderName}\a\0\1\8",
                        [DirNames.TradeData] = $@"{_dataFolderName}\a\1\1\2",

                        [DirNames.SafariZone] = $@"{_dataFolderName}\a\2\3\0",
                        [DirNames.Headbutt] = $@"{_dataFolderName}\a\2\5\2", //both versions use the same folder with different data
                        [DirNames.RockSmash] = $@"{_dataFolderName}\a\2\5\3", //odds+table-type per header; both versions use the same folder with different data
                        [DirNames.FollowerParams] = $@"{_dataFolderName}\a\1\4\1", // fielddata/tsurepoke/tp_param, 4 bytes per follower model
                        [DirNames.PokeathlonPerformance] = $@"{_dataFolderName}\a\1\6\9", // poketool/personal/performance, 20 bytes per species form

                        [DirNames.TrainerTextOffset] = $@"{_dataFolderName}\a\1\3\1",
                        [DirNames.TrainerTextTable] = $@"{_dataFolderName}\a\0\5\7",

                        [DirNames.EggMoves] = $@"{_dataFolderName}\a\2\2\9",

                        [DirNames.DungeonCutinGraphics] = $@"{_dataFolderName}\a\1\5\0",
                        [DirNames.TitleScreenGraphics] = $@"{_dataFolderName}\a\0\4\6",
                        [DirNames.TrainerCardGraphics] = $@"{_dataFolderName}\a\0\4\9",
                        [DirNames.EncounterEffectGraphics] = $@"{_dataFolderName}\a\1\0\9",
                        [DirNames.NameInputGraphics] = $@"{_dataFolderName}\a\0\3\1",
                        [DirNames.WeatherGraphics] = $@"{_dataFolderName}\a\0\6\3"
                    };

                    //Encounter archive is different for SS
                    packedDirsDict[DirNames.Encounters] = GameVersion == GameVersions.HeartGold ? $@"{_dataFolderName}\a\0\3\7" : $@"{_dataFolderName}\a\1\3\6";
                    break;
            }

            GameDirs = new Dictionary<DirNames, (string packedDir, string unpackedDir)>();
            foreach (KeyValuePair<DirNames, string> kvp in packedDirsDict)
            {
                // The NARC path literals use '\', normalize so they resolve on non-Windows too.
                string packedDir = Path.Combine(WorkDir, kvp.Value.Replace('\\', Path.DirectorySeparatorChar));
                string unpackedDir = Path.Combine(DspreDir, "unpacked", UnpackedFolderName(kvp.Key));
                KeepLegacyFolder(Path.Combine(DspreDir, "unpacked", kvp.Key.ToString()), unpackedDir);
                GameDirs.Add(kvp.Key, (packedDir, unpackedDir));
            }
        }

        /// <summary>
        /// The member name with a lowercase first letter (scripts), or as written when it starts with two capitals
        /// (OWSprites). Rotom and existing projects depend on these names, and Linux tells the cases apart.
        /// </summary>
        public static string UnpackedFolderName(DirNames dir)
        {
            string name = dir.ToString();
            return name.Length > 1 && char.IsUpper(name[1]) ? name : char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        // Only a case-sensitive disk can hold a folder named as the member is written beside the usual one.
        private static void KeepLegacyFolder(string written, string folder)
        {
            if (string.Equals(written, folder, StringComparison.Ordinal) || Directory.Exists(folder) || !Directory.Exists(written)) return;
            try { Directory.Move(written, folder); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { AppLogger.Warn($"Could not rename {written} to {folder}: {ex.Message}"); }
        }

        public static string GetLangResFolderName()
        {
            switch (GameLanguage)
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

        public static void ReadOWTable()
        {
            OverworldTable = new SortedDictionary<uint, (uint spriteID, ushort properties)>();
            // hg-engine repoints the table to src/field/overworld_table.c, which also holds its new NPCs and followers.
            List<HgEngine.HgEngineOverworlds.Entry> sourceRows = null;
            string sourceError = null;
            bool fromSource = GameFamily == GameFamilies.HGSS && HgEngine.HgEngineProject.IsActive
                && HgEngine.HgEngineOverworlds.TryReadTable(out sourceRows, out sourceError);
            if (sourceError != null) AppLogger.Error("ReadOWTable: " + sourceError);
            if (fromSource)
                foreach (HgEngineOverworlds.Entry row in sourceRows) OverworldTable[(uint)row.Tag] = ((uint)row.Gfx, (ushort)row.Properties);
            else
            switch (GameFamily)
            {
                case GameFamilies.Plat when OverworldSpriteTableExpansion.Detect():
                case GameFamilies.DP when OverworldSpriteTableExpansion.Detect():
                    // Expansion patch applied: the vanilla fixed-offset read below is stale, the game
                    // reads the relocated table instead, which also holds any custom entries added.
                    OverworldTable = OverworldSpriteTableExpansion.ReadTextureTable();
                    break;

                case GameFamilies.DP:
                case GameFamilies.Plat:
                    using (BinaryReader idReader = new BinaryReader(new FileStream(OWtablePath, FileMode.Open, FileAccess.Read)))
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
                    using (BinaryReader idReader = new BinaryReader(new FileStream(OWtablePath, FileMode.Open, FileAccess.Read)))
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
            if (Ow3DSpriteDict == null) Set3DOverworldsDict();
            // Ids with no sprite get a placeholder row so they can be picked; a patched table may already hold one.
            foreach (uint k in Ow3DSpriteDict.Keys)
                if (!OverworldTable.ContainsKey(k)) OverworldTable[k] = (0x3D3D, 0x3D3D);
            OverworldTableKeys = OverworldTable.Keys.ToArray();
        }

        /// <summary>Whether the Item tables editor works for this ROM.</summary>
        /// <returns>True if the editor is available, false otherwise</returns>
        public static bool IsItemTableEditorAvailable()
        {
            return PickupTableOverlayNumber >= 0 || GameFamily == GameFamilies.HGSS;
        }

        /// <summary>Whether the standard ARM9 mart layout has been verified for this ROM.</summary>
        public static bool IsMartEditorAvailable()
        {
            return (!IsHGE || HgEngine.HgEngineMarts.Enabled) && MartSpecialtyShopCount > 0;
        }

        /// <summary>Whether this ROM's hidden item table and its count checks are known.</summary>
        public static bool IsHiddenItemsEditorAvailable() => HiddenItemTableCapacity > 0;

        /// <summary>Checks if the Rock Smash per-header odds/table editor is available. </summary>
        public static bool IsRockSmashEditorAvailable()
        {
            return GameFamily == GameFamilies.HGSS;
        }

        /// <summary>
        /// Checks if the Rock Smash item-drop tables (the three fixed 8-slot tables in ov001.bin at
        /// <see cref="RockSmashItemTablesOffset"/>, see
        /// https://ds-pokemon-hacking.github.io/docs/generation-iv/guides/hgss-rock_smash/) are safe to
        /// edit.
        /// </summary>
        public static bool IsRockSmashItemTableAvailable()
        {
            return GameFamily == GameFamilies.HGSS;
        }

        /// <summary>
        /// HGSS only (Diamond/Pearl/Platinum have no equivalent system), wherever the table's literal is known.
        /// </summary>
        public static bool IsDungeonCutinEditorAvailable()
        {
            return DungeonCutinTableOffsetToRAMAddress != 0;
        }

        public static bool IsTitleScreenEditorAvailable() => GameFamily == GameFamilies.HGSS;

        /// <summary>HGSS have the touch menu and the Poké Ball question screen; Diamond, Pearl and Platinum have the Pokétch.</summary>
        public static bool IsBottomScreenEditorAvailable() =>
            GameFamily == GameFamilies.HGSS || GameFamily == GameFamilies.Plat || GameFamily == GameFamilies.DP;

        /// <summary>
        /// Member indices of the title logo/palette/background inside a/0/4/6 for a specific game version.
        /// </summary>
        public static (int logo, int palette, int background, int logoNscr, int backgroundNscr) TitleScreenMembersFor(GameVersions version) =>
            version == GameVersions.HeartGold ? (3, 4, 34, 0, 35) : (1, 2, 36, 0, 37);

        /// <summary>Same as <see cref="TitleScreenMembersFor"/>, defaulting to the currently loaded ROM's version.</summary>
        public static (int logo, int palette, int background, int logoNscr, int backgroundNscr) TitleScreenMembers =>
            TitleScreenMembersFor(GameVersion);

        /// <summary>Member indices of the title screen's copyright text strip inside a/0/4/6. </summary>
        public static (int ncgr, int nclr, int nscr) TitleScreenCopyrightMembers => (15, 16, 17);

        /// <summary>The text bank naming the Ball Capsule seals in the US games.</summary>
        public static int SealNamesTextNumber => TextBank(GameFamily switch
        {
            GameFamilies.DP => 10,
            GameFamilies.Plat => 12,
            _ => 15,
        });

        public static bool IsTrainerCardEditorAvailable() =>
            GameFamily == GameFamilies.HGSS || GameFamily == GameFamilies.Plat || GameFamily == GameFamilies.DP;

        // rankPalettes are the seven selectable card colours; each family numbers its archive differently.
        public static (int ncgr, int facaNscr, int backNscr, int[] rankPalettes) TrainerCardMembers =>
            GameFamily == GameFamilies.Plat ? (27, 35, 36, new[] { 0, 1, 2, 3, 4, 5, 6 })
            : GameFamily == GameFamilies.DP ? (23, 30, 31, new[] { 0, 1, 2, 3, 4, 5, 6 })
            : (41, 47, 48, new[] { 0, 1, 2, 3, 4, 5, 6 });

        // DP and Platinum use the pokeplatinum level names; HeartGold's are not named, so they go by colour.
        public static string[] TrainerCardRankNames => GameFamily == GameFamilies.HGSS
            ? new[] { "Normal", "Blue", "Green", "Purple", "Silver", "Black", "No Pokédex" }
            : new[] { "Normal", "Cobalt", "Bronze", "Silver", "Gold", "Black", "No Pokédex" };

        // Shared NCGR + one NSCR per gender; its colours sit in slots 65-95, of Normal's palette in DP and
        // Platinum and of every rank's in HeartGold.
        public static (int ncgr, int maleNscr, int femaleNscr) TrainerCardTrainerMembers =>
            GameFamily == GameFamilies.Plat ? (31, 40, 41) : GameFamily == GameFamilies.DP ? (27, 35, 36) : (44, 54, 55);

        /// <summary>Checks if the Starter Pokémon editor is available for the current ROM. </summary>
        public static bool IsStarterEditorAvailable()
        {
            return GameFamily == GameFamilies.DP || GameFamily == GameFamilies.Plat || GameFamily == GameFamilies.HGSS;
        }

        /// <summary>Gets a display name for the current game version.</summary>
        /// <returns>Game display name (e.g., "Diamond (US)", "Platinum (US)", "HeartGold (US)")</returns>
        public static string GetGameDisplayName()
        {
            string gameName;
            switch (GameVersion)
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
            switch (GameLanguage)
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
                case GameLanguages.Korean:
                    languageName = "KO";
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
