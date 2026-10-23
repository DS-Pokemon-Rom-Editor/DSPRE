using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE
{
    /// <summary>
    /// Where each table and code site DSPRE reads from arm9 or an overlay sits in the loaded ROM, or why it isn't
    /// known. The offsets are switches on the game, then the language, in RomLocator.Tables.cs.
    /// </summary>
    public static partial class RomLocator
    {
        public enum Thing
        {
            FriendshipChanges,
            TypeChart, TypeChartPointers, TypeChartPointersPlus1, TypeChartPointersPlus2, TypeChartCount, TypeChartModulus,
            WildHeldItemOdds, IncenseBabies, PoketchTypeChart, TutorPool, TutorCompatibility, MiningTreasures, MiningItems, BpShopPrices, BpShopItems, BpShopTms, HeldItemSameItemBranch, TypeIconBanks, MoveCategoryIconBanks, TypeIconMembers, MoveTypeButtonPalettes, PokewalkerSprites, PunchingMoves, SoundMoves, RegionalDexCompletion, RegionalDexRatingLast, RegionalDexLookupBound, EvolutionSlotBuffer, EvolutionSlotLoopLevel, EvolutionSlotLoopTrade, EvolutionSlotLoopItem,
            MapHeaders, SpawnPoint, InitialMoney, FlyTable, TrainerClassGenders, MachineMoves, EggMoves, TrainerNameLength, ItemTable, ItemLimit,
            PickupDivisor, PickupCommonItems, PickupRareItems, PickupWeights, MartCommonCount, MartCommonPointer, MartSpecialtyPointer, HiddenItems, HiddenItemCounts, HiddenItemCountLiterals, DistortionWorldItemScript, VsSeekerRematches, VsSeekerRematchPointers, VsSeekerRematchRowCompare, CameraTablePointers, EncounterMusicPointer, ConditionalMusicPointer, DungeonCutinPointer, BattleEffectCombosPointer, BattleEffectCombosSecondPointer, VsPokemonEntriesPointer, VsTrainerEntriesPointer,
            MonIconPalettePointer, OverworldTable, OverworldTablePointer, PokedexForeignSpecies, PokedexFieldZones, PokedexDungeonZones, PokedexAreaSort,
            StarterSpecies, StarterScene, StarterSceneInner, StarterCries, PokegearRematches,
            RockSmashItemTables, TrainerClassMetadataGenderHook, TrainerClassMetadataEyeContactHook, FrontierBrainTable, PlatPatchesItemFileIdHook, PlatPatchesItemIsTmHmHook, AiBackportCheck, AiBackportOutdatedCheck, DynamicHeadersInit, MatrixExpansionCode,
        }

        /// <summary>arm9 when <see cref="Overlay"/> is -1, otherwise that overlay; one or more file offsets.</summary>
        public sealed record Spot(int Overlay, int[] Offsets)
        {
            public int Offset => Offsets[0];
        }

        private static Spot Arm9(params int[] offsets) => new Spot(-1, offsets);
        private static Spot Ov(int overlay, params int[] offsets) => new Spot(overlay, offsets);

        private static readonly Dictionary<Thing, Func<GameVersions, GameLanguages, int, Spot>> Lookups = new()
        {
            [Thing.FriendshipChanges] = FriendshipChanges,
            [Thing.TypeChart] = TypeChart,
            [Thing.TypeChartPointers] = TypeChartPointers,
            [Thing.TypeChartPointersPlus1] = TypeChartPointersPlus1,
            [Thing.TypeChartPointersPlus2] = TypeChartPointersPlus2,
            [Thing.TypeChartCount] = TypeChartCount,
            [Thing.TypeChartModulus] = TypeChartModulus,
            [Thing.WildHeldItemOdds] = WildHeldItemOdds,
            [Thing.IncenseBabies] = IncenseBabies,
            [Thing.PoketchTypeChart] = PoketchTypeChart,
            [Thing.TutorPool] = TutorPool,
            [Thing.TutorCompatibility] = TutorCompatibility,
            [Thing.MiningTreasures] = MiningTreasures,
            [Thing.MiningItems] = MiningItems,
            [Thing.BpShopPrices] = BpShopPrices,
            [Thing.BpShopItems] = BpShopItems,
            [Thing.BpShopTms] = BpShopTms,
            [Thing.HeldItemSameItemBranch] = HeldItemSameItemBranch,
            [Thing.TypeIconBanks] = TypeIconBanks,
            [Thing.MoveCategoryIconBanks] = MoveCategoryIconBanks,
            [Thing.TypeIconMembers] = TypeIconMembers,
            [Thing.MoveTypeButtonPalettes] = MoveTypeButtonPalettes,
            [Thing.PokewalkerSprites] = PokewalkerSprites,
            [Thing.PunchingMoves] = PunchingMoves,
            [Thing.SoundMoves] = SoundMoves,
            [Thing.RegionalDexCompletion] = RegionalDexCompletion,
            [Thing.RegionalDexRatingLast] = RegionalDexRatingLast,
            [Thing.RegionalDexLookupBound] = RegionalDexLookupBound,
            [Thing.EvolutionSlotBuffer] = EvolutionSlotBuffer,
            [Thing.EvolutionSlotLoopLevel] = EvolutionSlotLoopLevel,
            [Thing.EvolutionSlotLoopTrade] = EvolutionSlotLoopTrade,
            [Thing.EvolutionSlotLoopItem] = EvolutionSlotLoopItem,
            [Thing.MapHeaders] = MapHeaders,
            [Thing.SpawnPoint] = SpawnPoint,
            [Thing.InitialMoney] = InitialMoney,
            [Thing.FlyTable] = FlyTable,
            [Thing.TrainerClassGenders] = TrainerClassGenders,
            [Thing.MachineMoves] = MachineMoves,
            [Thing.EggMoves] = EggMoves,
            [Thing.TrainerNameLength] = TrainerNameLength,
            [Thing.ItemTable] = ItemTable,
            [Thing.ItemLimit] = ItemLimit,
            [Thing.PickupDivisor] = PickupDivisor,
            [Thing.PickupCommonItems] = PickupCommonItems,
            [Thing.PickupRareItems] = PickupRareItems,
            [Thing.PickupWeights] = PickupWeights,
            [Thing.MartCommonCount] = MartCommonCount,
            [Thing.MartCommonPointer] = MartCommonPointer,
            [Thing.MartSpecialtyPointer] = MartSpecialtyPointer,
            [Thing.HiddenItems] = HiddenItems,
            [Thing.HiddenItemCounts] = HiddenItemCounts,
            [Thing.HiddenItemCountLiterals] = HiddenItemCountLiterals,
            [Thing.DistortionWorldItemScript] = DistortionWorldItemScript,
            [Thing.VsSeekerRematches] = VsSeekerRematches,
            [Thing.VsSeekerRematchPointers] = VsSeekerRematchPointers,
            [Thing.VsSeekerRematchRowCompare] = VsSeekerRematchRowCompare,
            [Thing.CameraTablePointers] = CameraTablePointers,
            [Thing.EncounterMusicPointer] = EncounterMusicPointer,
            [Thing.ConditionalMusicPointer] = ConditionalMusicPointer,
            [Thing.DungeonCutinPointer] = DungeonCutinPointer,
            [Thing.BattleEffectCombosPointer] = BattleEffectCombosPointer,
            [Thing.BattleEffectCombosSecondPointer] = BattleEffectCombosSecondPointer,
            [Thing.VsPokemonEntriesPointer] = VsPokemonEntriesPointer,
            [Thing.VsTrainerEntriesPointer] = VsTrainerEntriesPointer,
            [Thing.MonIconPalettePointer] = MonIconPalettePointer,
            [Thing.OverworldTable] = OverworldTable,
            [Thing.OverworldTablePointer] = OverworldTablePointer,
            [Thing.PokedexForeignSpecies] = PokedexForeignSpecies,
            [Thing.PokedexFieldZones] = PokedexFieldZones,
            [Thing.PokedexDungeonZones] = PokedexDungeonZones,
            [Thing.PokedexAreaSort] = PokedexAreaSort,
            [Thing.StarterSpecies] = StarterSpecies,
            [Thing.StarterScene] = StarterScene,
            [Thing.StarterSceneInner] = StarterSceneInner,
            [Thing.StarterCries] = StarterCries,
            [Thing.PokegearRematches] = PokegearRematches,
            [Thing.RockSmashItemTables] = RockSmashItemTables,
            [Thing.TrainerClassMetadataGenderHook] = TrainerClassMetadataGenderHook,
            [Thing.TrainerClassMetadataEyeContactHook] = TrainerClassMetadataEyeContactHook,
            [Thing.FrontierBrainTable] = FrontierBrainTable,
            [Thing.PlatPatchesItemFileIdHook] = PlatPatchesItemFileIdHook,
            [Thing.PlatPatchesItemIsTmHmHook] = PlatPatchesItemIsTmHmHook,
            [Thing.AiBackportCheck] = AiBackportCheck,
            [Thing.AiBackportOutdatedCheck] = AiBackportOutdatedCheck,
            [Thing.DynamicHeadersInit] = DynamicHeadersInit,
            [Thing.MatrixExpansionCode] = MatrixExpansionCode,
        };

        /// <summary>Where <paramref name="thing"/> is in a given dump, whether or not it is the loaded one.</summary>
        public static Spot WhereIn(Thing thing, GameVersions game, GameLanguages language, int revision) =>
            Lookups[thing](game, language, revision);

        public static Spot Where(Thing thing) => RomID == null ? null : WhereIn(thing, GameVersion, GameLanguage, RomRevision);

        public static string PathOf(Spot spot) => spot.Overlay < 0 ? Arm9Path : OverlayUtils.GetPath(spot.Overlay);

        /// <summary>The known dumps <paramref name="thing"/> has a place in, from the same switch <see cref="Where"/> reads.</summary>
        public static List<(string id, int rev)> WorksOn(Thing thing) =>
            KnownDumps.Where(d => GameOfCode(d.id) is GameVersions g && WhereIn(thing, g, LanguageOfCode(d.id), d.rev) != null).ToList();

        private static T Here<T>(System.Func<GameVersions, GameLanguages, int, T> lookup) where T : class =>
            RomID == null ? null : lookup(GameVersion, GameLanguage, RomRevision);

        /// <summary>The known dumps a site lookup has an answer for.</summary>
        public static List<(string id, int rev)> WorksOn<T>(System.Func<GameVersions, GameLanguages, int, T> lookup) where T : class =>
            KnownDumps.Where(d => GameOfCode(d.id) is GameVersions g && lookup(g, LanguageOfCode(d.id), d.rev) != null).ToList();

        private static System.Func<GameVersions, GameLanguages, int, MoveListSites> MoveListLookup(Thing table) =>
            table == Thing.PunchingMoves ? PunchingMovesSites : table == Thing.SoundMoves ? SoundMovesSites : null;

        public static MoveListSites MoveListSitesIn(Thing table, GameVersions game, GameLanguages language, int revision) =>
            MoveListLookup(table)?.Invoke(game, language, revision);

        public static MoveListSites MoveListSitesOf(Thing table) => MoveListLookup(table) is { } f ? Here(f) : null;
        public static List<(string id, int rev)> MoveListDumps(Thing table) =>
            MoveListLookup(table) is { } f ? WorksOn(f).Intersect(WorksOn(table)).ToList() : new List<(string id, int rev)>();

        public static SwarmSites SwarmSites => Here(SwarmSitesIn);
        public static List<(string id, int rev)> SwarmDumps => WorksOn(SwarmSitesIn);

        public static BpShopSites BpShopSites => Here(BpShopSitesIn);
        public static List<(string id, int rev)> BpShopDumps => WorksOn(BpShopSitesIn);

        public static HpBarTextSites HpBarTextSites => Here(HpBarTextSitesIn);
        public static List<(string id, int rev)> HpBarTextDumps => WorksOn(HpBarTextSitesIn);

        public static SlotOddsMethod[] SlotOddsMethods => Here(SlotOddsIn);
        public static List<(string id, int rev)> SlotOddsDumps => WorksOn(SlotOddsIn);

        public static VsIntroSites VsIntroSites => Here(VsIntroSitesIn);
        public static List<(string id, int rev)> VsIntroClassTableDumps => WorksOn(VsIntroSitesIn).Where(d => d.id.StartsWith("IP")).ToList();

        public static VsIntroMotionSites VsIntroMotionSites => Here(VsIntroMotionSitesIn);

        /// <summary>Null where ToolboxDB's per-language entry applies instead.</summary>
        public static Arm9ExpansionSites Arm9ExpansionSites => Here(Arm9ExpansionSitesIn);
        public static PickupSlotSites PickupSlotSites => Here(PickupSlotSitesIn);
        public static MiningCodeSites MiningCodeSites => Here(MiningCodeSitesIn);
        public static TrainerShinySites TrainerShinySites => Here(TrainerShinySitesIn);
        public static BuildingRotationSites BuildingRotationSites => Here(BuildingRotationSitesIn);
        public static BdhcamSites BdhcamSites => Here(BdhcamSitesIn);
        public static SummaryScreenSites SummaryScreenSites => Here(SummaryScreenSitesIn);
        public static List<(string id, int rev)> SummaryScreenDumps => WorksOn(SummaryScreenSitesIn);
        public static SummaryFontSites SummaryFontSites => Here(SummaryFontSitesIn);
        public static List<(string id, int rev)> SummaryFontDumps => WorksOn(SummaryFontSitesIn);
        public static TrainerPrizeTable TrainerPrizeTable => Here(TrainerPrizeTableIn);
        public static FollowerTableSites FollowerTables => Here(FollowerTablesIn);

        /// <summary>Platinum: the arm9 literal pointing at the trainer class gender table, or 0 where it isn't known.</summary>
        public static uint TrainerClassGenderPointer => RomID == null ? 0 : TrainerClassGenderPointerIn(GameVersion, GameLanguage, RomRevision) ?? 0;

        /// <summary>HGSS arm9 file offset of sPokeathlonPerformanceArcIdxs, the first Pokéathlon record of each species; -1 elsewhere.</summary>
        public static int PokeathlonMemberTable => RomID == null ? -1 : PokeathlonMemberTableIn(GameVersion, GameLanguage, RomRevision) ?? -1;

        // Each selector is the US one moved: every boundary keeps its distance from its method's first compare.
        private static SlotOddsMethod[] HgssSlotOdds(int walking, int surfing, int fishing, int rockSmash, int headbutt) => new[]
        {
            new SlotOddsMethod("Walking", 2, 12, LandSites(walking)),
            new SlotOddsMethod("Surfing", 2, 5, SurfSites(surfing)),
            new SlotOddsMethod("Fishing (all rods)", 2, 5, StepSites(fishing, 4)),
            new SlotOddsMethod("Rock Smash", 2, 2, StepSites(rockSmash, 1)),
            new SlotOddsMethod("Headbutt", 2, 6, StepSites(headbutt, 5)),
        };

        private static SlotOddsMethod[] DpPtSlotOdds(int walking, int surfing, int oldRod, int goodRod, int superRod) => new[]
        {
            new SlotOddsMethod("Walking", 6, 12, LandSites(walking)),
            new SlotOddsMethod("Surfing", 6, 5, SurfSites(surfing)),
            new SlotOddsMethod("Old Rod", 6, 5, StepSites(oldRod, 4)),
            new SlotOddsMethod("Good Rod", 6, 5, StepSites(goodRod, 4)),
            new SlotOddsMethod("Super Rod", 6, 5, StepSites(superRod, 4)),
        };

        private static int[][] LandSites(int b) => new[]
        {
            new[] { b }, new[] { b + 0x0A, b + 0x12 }, new[] { b + 0x16, b + 0x1E }, new[] { b + 0x22, b + 0x2A },
            new[] { b + 0x2E, b + 0x36 }, new[] { b + 0x3A, b + 0x42 }, new[] { b + 0x46, b + 0x4E },
            new[] { b + 0x52, b + 0x5A }, new[] { b + 0x5E, b + 0x66 }, new[] { b + 0x6A },
        };
        private static int[][] SurfSites(int b) => new[] { new[] { b }, new[] { b + 0x0A, b + 0x12 }, new[] { b + 0x16, b + 0x1E }, new[] { b + 0x22 } };
        private static int[][] StepSites(int b, int count) => Enumerable.Range(0, count).Select(i => new[] { b + 8 * i }).ToArray();

        /// <summary>The known dumps every one of <paramref name="things"/> has a place in.</summary>
        public static List<(string id, int rev)> WorksOnAll(params Thing[] things) =>
            things.Select(WorksOn).Aggregate((a, b) => a.Intersect(b).ToList());

        public static GameVersions? GameOfCode(string id) => id.Substring(0, 3) switch
        {
            "ADA" => GameVersions.Diamond, "APA" => GameVersions.Pearl, "CPU" => GameVersions.Platinum,
            "IPK" => GameVersions.HeartGold, "IPG" => GameVersions.SoulSilver, _ => null,
        };

        /// <summary>Why <paramref name="length"/> bytes at <paramref name="thing"/> can't be read here, or null.</summary>
        public static string WhyNot(Thing thing, int length)
        {
            Spot spot = Where(thing);
            if (spot == null) return "Works on " + DescribeDumps(WorksOn(thing)) + ".";
            string path = PathOf(spot);
            if (!File.Exists(path)) return $"{Path.GetFileName(path)} is missing from this project.";
            if (spot.Overlay < 0 && !IsDsRomProject && ARM9.CheckCompressionMark())
                return "arm9 is still compressed. Convert this project to ds-rom format first.";
            // A compressed overlay is shorter on disk than once it's decompressed for the read.
            long size = spot.Overlay >= 0 && OverlayUtils.IsStillCompressed(spot.Overlay)
                ? OverlayUtils.OverlayTable.GetUncompressedSize(spot.Overlay) : new FileInfo(path).Length;
            if (spot.Offsets.Any(o => size < o + length)) return $"{Path.GetFileName(path)} is too short for this.";
            return null;
        }

        /// <summary>The file holding <paramref name="thing"/>, decompressed first when it is an overlay.</summary>
        public static byte[] ReadFile(Thing thing)
        {
            Spot spot = Where(thing) ?? throw new InvalidOperationException(WhyNot(thing, 0));
            if (spot.Overlay >= 0 && OverlayUtils.IsCompressed(spot.Overlay)) OverlayUtils.Decompress(spot.Overlay);
            return File.ReadAllBytes(PathOf(spot));
        }

        public static byte[] Read(Thing thing, int length)
        {
            if (WhyNot(thing, length) is string why) throw new InvalidOperationException(why);
            Spot spot = Where(thing);
            if (spot.Overlay >= 0 && OverlayUtils.IsCompressed(spot.Overlay)) OverlayUtils.Decompress(spot.Overlay);
            return DSUtils.ReadFromFile(PathOf(spot), spot.Offset, length);
        }

        public static void Write(Thing thing, byte[] data)
        {
            if (WhyNot(thing, data.Length) is string why) throw new InvalidOperationException(why);
            Spot spot = Where(thing);
            if (spot.Overlay >= 0 && OverlayUtils.IsCompressed(spot.Overlay)) OverlayUtils.Decompress(spot.Overlay);
            DSUtils.WriteToFile(PathOf(spot), data, (uint)spot.Offset);
        }
    }
}
