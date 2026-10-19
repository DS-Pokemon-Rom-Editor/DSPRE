using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using static DSPRE.RomInfo;

namespace DSPRE.Tests.Core
{
    /// <summary>
    /// Reads every offset RomLocator gives for every retail dump listed in testroms.json, straight from the .nds: a
    /// data table must hold the same bytes as the US dump of that game, and a pointer must point at the table.
    /// </summary>
    public class RomLocatorTests
    {
        private static readonly Dictionary<GameVersions, (string Id, int Rev)> Us = new()
        {
            [GameVersions.Diamond] = ("ADAE", 5), [GameVersions.Pearl] = ("APAE", 5), [GameVersions.Platinum] = ("CPUE", 1),
            [GameVersions.HeartGold] = ("IPKE", 0), [GameVersions.SoulSilver] = ("IPGE", 0),
        };

        private static readonly HashSet<RomLocator.Thing> Immediates = new()
        {
            RomLocator.Thing.RegionalDexCompletion, RomLocator.Thing.RegionalDexRatingLast, RomLocator.Thing.RegionalDexLookupBound,
            RomLocator.Thing.EvolutionSlotBuffer, RomLocator.Thing.EvolutionSlotLoopLevel, RomLocator.Thing.EvolutionSlotLoopTrade,
            RomLocator.Thing.EvolutionSlotLoopItem, RomLocator.Thing.TypeChartCount, RomLocator.Thing.TypeChartModulus,
            RomLocator.Thing.PickupDivisor, RomLocator.Thing.MartCommonCount, RomLocator.Thing.VsSeekerRematchRowCompare,
            RomLocator.Thing.HiddenItemCounts,
        };

        // Literals holding a table's RAM address, and how much of that table must hold the US bytes. The specialty marts'
        // table is itself RAM addresses, so only where it points is checked.
        private static readonly Dictionary<RomLocator.Thing, int> Pointers = new()
        {
            [RomLocator.Thing.MartCommonPointer] = 16, [RomLocator.Thing.MartSpecialtyPointer] = 0,
            [RomLocator.Thing.VsSeekerRematchPointers] = 32, [RomLocator.Thing.CameraTablePointers] = 48,
            [RomLocator.Thing.EncounterMusicPointer] = 16, [RomLocator.Thing.ConditionalMusicPointer] = 18,
            [RomLocator.Thing.DungeonCutinPointer] = 16, [RomLocator.Thing.BattleEffectCombosPointer] = 16,
            [RomLocator.Thing.BattleEffectCombosSecondPointer] = 16, [RomLocator.Thing.VsPokemonEntriesPointer] = 16,
            [RomLocator.Thing.VsTrainerEntriesPointer] = 16, [RomLocator.Thing.MonIconPalettePointer] = 32,
            [RomLocator.Thing.OverworldTablePointer] = 64,
        };

        // What the editor relies on: an immediate keeps its value and opcode though the compiler may pick another register,
        // a branch stays the same branch though its distance can change, and a mining row keeps everything but the pointer
        // to its shape, which is a RAM address and so moves with the overlay.
        private static bool Matches(RomLocator.Thing thing, GameVersions game, byte[] got, byte[] wanted, uint ram, int fileLength)
        {
            if (Immediates.Contains(thing)) return got[0] == wanted[0] && (got[1] & 0xF8) == (wanted[1] & 0xF8);
            if (thing == RomLocator.Thing.HeldItemSameItemBranch) return got[1] == wanted[1];
            // Sorted by place name, so each language orders the same zones its own way.
            if (thing == RomLocator.Thing.PokedexAreaSort)
                return Enumerable.Range(0, got.Length / 4).Select(i => BitConverter.ToInt32(got, i * 4)).OrderBy(z => z)
                    .SequenceEqual(Enumerable.Range(0, wanted.Length / 4).Select(i => BitConverter.ToInt32(wanted, i * 4)).OrderBy(z => z));
            // Japanese and Korean headers number their own text banks and some scripts; each header's area data and matrix match.
            if (thing == RomLocator.Thing.MapHeaders)
            {
                bool hgss = game is GameVersions.HeartGold or GameVersions.SoulSilver;
                int area = hgss ? 1 : 0, matrix = hgss ? 4 : 2;
                for (int at = 0; at < got.Length; at += 24)
                    if (got[at + area] != wanted[at + area] || got[at + matrix] != wanted[at + matrix] || got[at + matrix + 1] != wanted[at + matrix + 1]) return false;
                return true;
            }
            if (thing == RomLocator.Thing.MiningTreasures)
            {
                for (int at = 0; at < got.Length; at += 20)
                {
                    if (!got.AsSpan(at + 4, 16).SequenceEqual(wanted.AsSpan(at + 4, 16))) return false;
                    uint shape = BitConverter.ToUInt32(got, at);
                    if (shape != 0 && (shape < ram || shape - ram >= fileLength)) return false;
                }
                return true;
            }
            return got.AsSpan().SequenceEqual(wanted);
        }

        // How many bytes of each thing to compare: the table, or the instruction holding an immediate.
        private static int LengthOf(RomLocator.Thing thing, GameVersions game) => thing switch
        {
            RomLocator.Thing.FriendshipChanges => 30,
            RomLocator.Thing.TypeChart => 112 * 3,
            RomLocator.Thing.WildHeldItemOdds => 8,
            RomLocator.Thing.IncenseBabies => 9 * 6,
            RomLocator.Thing.PoketchTypeChart => 18 * 18,
            RomLocator.Thing.TutorPool => game == GameVersions.Platinum ? 38 * 12 : 52 * 4,
            RomLocator.Thing.TutorCompatibility => 505 * 5,
            RomLocator.Thing.MiningTreasures => 85 * 20,
            RomLocator.Thing.MiningItems => 49 * 2,
            RomLocator.Thing.BpShopPrices => 41 * 4,
            RomLocator.Thing.BpShopItems or RomLocator.Thing.BpShopTms => 8,
            RomLocator.Thing.TypeIconBanks => 23,
            RomLocator.Thing.MoveCategoryIconBanks => 3,
            RomLocator.Thing.TypeIconMembers => 23 * 4,
            RomLocator.Thing.PokewalkerSprites => 494 * 4,
            RomLocator.Thing.PunchingMoves => 15 * 2,
            RomLocator.Thing.SoundMoves => 12 * 2,
            RomLocator.Thing.MapHeaders => 24 * 20,
            RomLocator.Thing.SpawnPoint => 20,
            RomLocator.Thing.InitialMoney => 4,
            RomLocator.Thing.FlyTable => 12 * 4,
            RomLocator.Thing.TrainerClassGenders => game switch { GameVersions.Platinum => 105, GameVersions.HeartGold or GameVersions.SoulSilver => 129, _ => 98 },
            RomLocator.Thing.MachineMoves => 100 * 2,
            RomLocator.Thing.EggMoves => 64,
            RomLocator.Thing.ItemTable => 8 * 16,
            RomLocator.Thing.ItemLimit => 2,
            RomLocator.Thing.PickupCommonItems => 18 * 2,
            RomLocator.Thing.PickupRareItems => 11 * 2,
            RomLocator.Thing.PickupWeights => 9,
            RomLocator.Thing.HiddenItems => 8 * 20,
            RomLocator.Thing.HiddenItemCountLiterals => 4,
            RomLocator.Thing.VsSeekerRematches => 32,
            RomLocator.Thing.OverworldTable => 64,
            RomLocator.Thing.PokedexForeignSpecies => 32,
            RomLocator.Thing.PokedexFieldZones or RomLocator.Thing.PokedexDungeonZones => 32,
            RomLocator.Thing.PokedexAreaSort => 142 * 4,
            _ => 2,
        };

        [SkippableFact]
        public void EveryOffsetMatchesItsDump()
        {
            List<(string Id, int Revision, string Rom)> dumps = TestRoms.Dumps();
            Skip.If(dumps.Count == 0, "No retail dumps are listed under \"dumps\" in testroms.json.");

            Dictionary<string, Dump> cache = new Dictionary<string, Dump>();
            Dump Open(string rom) => cache.TryGetValue(rom, out Dump d) ? d : cache[rom] = new Dump(rom);
            List<string> failures = new List<string>();
            int checkedCount = 0;

            foreach ((string id, int rev, string rom) in dumps)
            {
                GameVersions game = RomLocator.GameOfCode(id).Value;
                GameLanguages language = LanguageOfCode(id);
                (string usId, int usRev) = Us[game];
                (string Id, int Revision, string Rom) us = dumps.FirstOrDefault(d => d.Id == usId && d.Revision == usRev);
                Dump here = Open(rom);
                string name = $"{id} rev {rev}";

                foreach (RomLocator.Thing thing in Enum.GetValues<RomLocator.Thing>())
                {
                    RomLocator.Spot spot = RomLocator.WhereIn(thing, game, language, rev);
                    if (spot == null) continue;
                    checkedCount++;
                    byte[] file = here.File(spot.Overlay);
                    if (Pointers.TryGetValue(thing, out int pointee))
                    {
                        (string Id, int Revision, string Rom) usP = us;
                        RomLocator.Spot usPointerSpot = usP.Rom == null ? null : RomLocator.WhereIn(thing, game, LanguageOfCode(usP.Id), usP.Revision);
                        for (int i = 0; i < spot.Offsets.Length; i++)
                        {
                            (byte[] f, int at) = here.Follow(spot.Overlay, spot.Offsets[i]);
                            if (f == null) { failures.Add($"{name}: {thing} at 0x{spot.Offsets[i]:X} doesn't point into arm9 or its overlay"); continue; }
                            if (pointee == 0)
                            {
                                if (BitConverter.ToUInt32(f, at) >> 24 != 0x02) failures.Add($"{name}: {thing} at 0x{spot.Offsets[i]:X} doesn't point at RAM addresses");
                                continue;
                            }
                            if (usPointerSpot == null) continue;
                            (byte[] usF, int usAt) = Open(usP.Rom).Follow(usPointerSpot.Overlay, usPointerSpot.Offsets[Math.Min(i, usPointerSpot.Offsets.Length - 1)]);
                            if (usF == null || usAt + pointee > usF.Length) { failures.Add($"{name}: {thing}'s US literal doesn't point at a table"); continue; }
                            if (at + pointee > f.Length || !f.AsSpan(at, pointee).SequenceEqual(usF.AsSpan(usAt, pointee)))
                                failures.Add($"{name}: {thing} at 0x{spot.Offsets[i]:X} points at a table that doesn't hold the US bytes");
                        }
                        continue;
                    }
                    if (thing is RomLocator.Thing.TypeChartPointers or RomLocator.Thing.TypeChartPointersPlus1 or RomLocator.Thing.TypeChartPointersPlus2)
                    {
                        RomLocator.Spot chart = RomLocator.WhereIn(RomLocator.Thing.TypeChart, game, language, rev);
                        uint plus = thing == RomLocator.Thing.TypeChartPointersPlus1 ? 1u : thing == RomLocator.Thing.TypeChartPointersPlus2 ? 2u : 0u;
                        uint want = here.RamOf(chart.Overlay) + (uint)chart.Offset + plus;
                        foreach (int o in spot.Offsets)
                            if (o + 4 > file.Length || BitConverter.ToUInt32(file, o) != want)
                                failures.Add($"{name}: {thing} at 0x{o:X} doesn't point at the chart");
                        continue;
                    }
                    if (thing == RomLocator.Thing.MoveTypeButtonPalettes)
                    {
                        // RAM pointers, so they differ between dumps; each must at least be an address in main RAM.
                        for (int i = 0; i < 18; i++)
                            if (spot.Offset + 4 * i + 4 > file.Length || BitConverter.ToUInt32(file, spot.Offset + 4 * i) >> 24 != 0x02)
                                failures.Add($"{name}: {thing} entry {i} isn't a RAM address");
                        continue;
                    }
                    if (us.Rom == null) { checkedCount--; continue; }
                    int length = LengthOf(thing, game);
                    RomLocator.Spot usSpot = RomLocator.WhereIn(thing, game, LanguageOfCode(us.Id), us.Revision);
                    byte[] wanted = Open(us.Rom).File(usSpot.Overlay).AsSpan(usSpot.Offset, length).ToArray();
                    if (spot.Offset + length > file.Length) { failures.Add($"{name}: {thing} at 0x{spot.Offset:X} runs past its file"); continue; }
                    if (thing == RomLocator.Thing.ItemLimit)
                    {
                        // Japanese Diamond and Pearl load the limit from the literal pool; the others build it from an immediate.
                        int limit = ItemLimitAt(file, spot.Offset), usLimit = ItemLimitAt(Open(us.Rom).File(usSpot.Overlay), usSpot.Offset);
                        if (limit < 0 || limit != usLimit) failures.Add($"{name}: {thing} at 0x{spot.Offset:X} gives {limit}, not the US {usLimit}");
                        continue;
                    }
                    byte[] got = file.AsSpan(spot.Offset, length).ToArray();
                    if (!Matches(thing, game, got, wanted, here.RamOf(spot.Overlay), file.Length))
                        failures.Add($"{name}: {thing} at 0x{spot.Offset:X} doesn't hold the US bytes (first difference at +{Enumerable.Range(0, length).First(i => got[i] != wanted[i])})");
                }
            }

            Assert.True(checkedCount > 0, "No offsets were checked.");
            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        }

        // The code sites editors patch: each literal must point where the record says, each count must be the instruction
        // the editor rewrites, and each hook must be the instruction the patch replaces.
        [SkippableFact]
        public void EverySiteRecordMatchesItsDump()
        {
            List<(string Id, int Revision, string Rom)> dumps = TestRoms.Dumps();
            Skip.If(dumps.Count == 0, "No retail dumps are listed under \"dumps\" in testroms.json.");

            List<string> failures = new List<string>();
            int checkedCount = 0;

            foreach ((string id, int rev, string rom) in dumps)
            {
                GameVersions game = RomLocator.GameOfCode(id).Value;
                GameLanguages language = LanguageOfCode(id);
                (string usId, int usRev) = Us[game];
                (string Id, int Revision, string Rom) us = dumps.FirstOrDefault(d => d.Id == usId && d.Revision == usRev);
                Dump here = new Dump(rom);
                Dump usDump = us.Rom == null ? null : us.Rom == rom ? here : new Dump(us.Rom);
                string name = $"{id} rev {rev}";
                void Fail(string what) => failures.Add($"{name}: {what}");
                ushort U16(byte[] f, int at) => at + 2 <= f.Length ? BitConverter.ToUInt16(f, at) : (ushort)0;
                uint U32(byte[] f, int at) => at + 4 <= f.Length ? BitConverter.ToUInt32(f, at) : 0;
                GameLanguages usLanguage = usDump == null ? language : LanguageOfCode(us.Id);
                byte[] arm9Here = here.File(-1), usArm9 = usDump?.File(-1);
                void SameAsUs(string what, byte[] f, int at, byte[] usF, int usAt, int length)
                {
                    if (usF == null) return;
                    if (at < 0 || usAt < 0 || at + length > f.Length || !f.AsSpan(at, length).SequenceEqual(usF.AsSpan(usAt, length))) Fail($"{what} doesn't hold the US bytes");
                }

                foreach (RomLocator.Thing list in new[] { RomLocator.Thing.PunchingMoves, RomLocator.Thing.SoundMoves })
                {
                    MoveListSites sites = RomLocator.MoveListSitesIn(list, game, language, rev);
                    RomLocator.Spot spot = RomLocator.WhereIn(list, game, language, rev);
                    if (sites == null) continue;
                    checkedCount++;
                    if (spot == null) { Fail($"{list} has code sites but no list"); continue; }
                    byte[] ov = here.File(spot.Overlay);
                    if (U32(ov, sites.PointerSite) != here.RamOf(spot.Overlay) + (uint)spot.Offset) Fail($"{list} pointer at 0x{sites.PointerSite:X} doesn't point at the list");
                    if ((U16(ov, sites.CountCompare) >> 8 & 0xF8) != 0x28) Fail($"{list} count at 0x{sites.CountCompare:X} isn't a cmp");
                    if (usDump != null)
                    {
                        MoveListSites usSites = RomLocator.MoveListSitesIn(list, game, LanguageOfCode(us.Id), us.Revision);
                        RomLocator.Spot usSpot = RomLocator.WhereIn(list, game, LanguageOfCode(us.Id), us.Revision);
                        if (ov[sites.CountCompare] != usDump.File(usSpot.Overlay)[usSites.CountCompare]) Fail($"{list} count differs from the US one");
                    }
                }

                SwarmSites swarm = RomLocator.SwarmSitesIn(game, language, rev);
                if (swarm != null)
                {
                    checkedCount++;
                    byte[] code = here.File(swarm.Overlay);
                    uint table = here.RamOf(swarm.Overlay) + (uint)swarm.Table;
                    foreach (int o in swarm.Literals) if (U32(code, o) != table) Fail($"swarm literal at 0x{o:X} doesn't point at the table");
                    foreach (int o in swarm.LiteralsPlus2) if (U32(code, o) != table + 2) Fail($"swarm literal at 0x{o:X} doesn't point two past the table");
                    foreach (int o in swarm.CountSites) if (U16(code, o) != (0x2100 | swarm.Rows)) Fail($"swarm count at 0x{o:X} isn't movs r1, #{swarm.Rows}");
                    if (usDump != null)
                    {
                        SwarmSites usSwarm = RomLocator.SwarmSitesIn(game, LanguageOfCode(us.Id), us.Revision);
                        int length = swarm.Rows * swarm.RowSize;
                        if (!code.AsSpan(swarm.Table, length).SequenceEqual(usDump.File(usSwarm.Overlay).AsSpan(usSwarm.Table, length)))
                            Fail("swarm table doesn't hold the US rows");
                    }
                }

                BpShopSites shop = RomLocator.BpShopSitesIn(game, language, rev);
                if (shop != null)
                {
                    checkedCount++;
                    byte[] arm9 = here.File(-1);
                    if (U16(arm9, shop.ExchangeSplit) >> 8 != 0x21) Fail($"BP shop split at 0x{shop.ExchangeSplit:X} isn't movs r1");
                    if (shop.ListPointers >= 0)
                    {
                        if (U32(arm9, shop.ListPointers) >> 24 != 0x02 || U32(arm9, shop.ListPointers + 4) >> 24 != 0x02) Fail("BP shop list pointers aren't RAM addresses");
                        byte[] ov7 = here.File(7);
                        if (U32(ov7, shop.PriceLiteral + 4) != U32(ov7, shop.PriceLiteral) + 2) Fail("BP shop price literals don't agree");
                        if (U16(ov7, shop.PriceCountCompare) >> 8 != 0x2A) Fail($"BP shop price count at 0x{shop.PriceCountCompare:X} isn't cmp r2");
                    }
                    if (usDump != null)
                    {
                        BpShopSites usShop = RomLocator.BpShopSitesIn(game, LanguageOfCode(us.Id), us.Revision);
                        int length = DSPRE.ROMFiles.BpShopData.ExchangeRows * 4;
                        if (!arm9.AsSpan(shop.ExchangeTable, length).SequenceEqual(usArm9.AsSpan(usShop.ExchangeTable, length))) Fail("BP shop exchange table doesn't hold the US rows");
                        if (arm9[shop.ExchangeSplit] != usArm9[usShop.ExchangeSplit]) Fail("BP shop split differs from the US one");
                    }
                }

                SlotOddsMethod[] slots = RomLocator.SlotOddsIn(game, language, rev);
                if (slots != null)
                {
                    checkedCount++;
                    SlotOddsMethod[] usSlots = usDump == null ? null : RomLocator.SlotOddsIn(game, LanguageOfCode(us.Id), us.Revision);
                    for (int m = 0; m < slots.Length; m++)
                    {
                        byte[] ov = here.File(slots[m].Overlay);
                        byte[] usOv = usSlots == null ? null : usDump.File(usSlots[m].Overlay);
                        List<(int Site, int UsSite)> compares = slots[m].Boundaries.Select((b, i) => (b[0], usSlots?[m].Boundaries[i][0] ?? -1)).ToList();
                        foreach (int[] boundary in slots[m].Boundaries) compares.AddRange(boundary.Skip(1).Select(o => (o, -1)));
                        if (m == 0) compares.Add((slots[0].Boundaries[0][0] + slots[0].LastSlotSite, usSlots == null ? -1 : usSlots[0].Boundaries[0][0] + usSlots[0].LastSlotSite));
                        foreach ((int site, int usSite) in compares)
                        {
                            if ((U16(ov, site) >> 8 & 0xF8) != 0x28) Fail($"{slots[m].Name} odds at 0x{site:X} isn't a cmp");
                            else if (usSite >= 0 && ov[site] != usOv[usSite]) Fail($"{slots[m].Name} odds at 0x{site:X} differ from the US ones");
                        }
                    }
                }

                VsIntroSites vs = RomLocator.VsIntroSitesIn(game, language, rev);
                if (vs != null)
                {
                    checkedCount++;
                    VsIntroSites usVs = usDump == null ? null : RomLocator.VsIntroSitesIn(game, LanguageOfCode(us.Id), us.Revision);
                    byte[] records = here.File(vs.RecordOverlay), task = here.File(vs.TaskOverlay);
                    uint taskRam = here.RamOf(vs.TaskOverlay);
                    for (int i = 0; i < vs.TaskCount; i++)
                        if ((U32(task, vs.TaskTable + 4 * i) & 1) == 0 || (U32(task, vs.TaskTable + 4 * i) >> 24) != 0x02) Fail($"VS intro task {i} isn't a Thumb routine");
                    foreach (int o in vs.ParticleSites.Concat(vs.EmitterSites)) if ((U16(records, o) >> 8 & 0xF8) != 0x20) Fail($"VS intro particle at 0x{o:X} isn't a movs");
                    if (vs.ComboMusicLiteral >= 0 && U32(here.File(-1), vs.ComboMusicLiteral) >> 24 != 0x02) Fail("VS intro combo music literal isn't a RAM address");
                    if (usVs != null)
                    {
                        byte[] usRecords = usDump.File(usVs.RecordOverlay);
                        void Same(string what, byte[] f, int at, byte[] usF, int usAt, int length)
                        {
                            if (at < 0 && usAt < 0) return;
                            if (at < 0 || usAt < 0 || at + length > f.Length || !f.AsSpan(at, length).SequenceEqual(usF.AsSpan(usAt, length))) Fail($"VS intro {what} doesn't hold the US bytes");
                        }
                        Same("gym records", records, vs.GymTable, usRecords, usVs.GymTable, vs.GymCount * vs.GymSize);
                        Same("rival record", records, vs.RivalRecord, usRecords, usVs.RivalRecord, vs.GymSize);
                        Same("league records", records, vs.LeagueTable, usRecords, usVs.LeagueTable, 5 * 8);
                        if (vs.ExecutiveOverlay >= 0) Same("executive records", here.File(vs.ExecutiveOverlay), vs.ExecutiveTable, usDump.File(usVs.ExecutiveOverlay), usVs.ExecutiveTable, 5 * 8);
                        Same("class switch", here.File(-1), vs.ClassJumpTable, usDump.File(-1), usVs.ClassJumpTable, 2 * vs.ClassJumpCount);
                        Same("combo count", here.File(-1), vs.ComboMusicCount, usDump.File(-1), usVs.ComboMusicCount, 1);
                        for (int i = 0; i < vs.ParticleSites.Length; i++) Same("particle", records, vs.ParticleSites[i], usRecords, usVs.ParticleSites[i], 1);
                        for (int i = 0; i < vs.EmitterSites.Length; i++) Same("emitter", records, vs.EmitterSites[i], usRecords, usVs.EmitterSites[i], 1);
                    }
                }

                VsIntroMotionSites motion = RomLocator.VsIntroMotionSitesIn(game, language, rev);
                if (motion != null && usDump != null)
                {
                    checkedCount++;
                    VsIntroMotionSites usMotion = RomLocator.VsIntroMotionSitesIn(game, LanguageOfCode(us.Id), us.Revision);
                    int flight = DSPRE.ROMFiles.VsIntroMotion.FlightCount * 32, block = DSPRE.ROMFiles.VsIntroMotion.BlockColumns;
                    if (!here.File(motion.FlightOverlay).AsSpan(motion.FlightTable, flight).SequenceEqual(usDump.File(usMotion.FlightOverlay).AsSpan(usMotion.FlightTable, flight)))
                        Fail("VS intro emblem flight doesn't hold the US bytes");
                    if (!here.File(motion.BlockOverlay).AsSpan(motion.BlockOrder, block).SequenceEqual(usDump.File(usMotion.BlockOverlay).AsSpan(usMotion.BlockOrder, block)))
                        Fail("VS intro block order doesn't hold the US bytes");
                }

                Arm9ExpansionSites expansion = RomLocator.Arm9ExpansionSitesIn(game, language, rev);
                if (expansion != null)
                {
                    checkedCount++;
                    if (U16(arm9Here, (int)expansion.Boot) != 0x2000 || U16(arm9Here, (int)expansion.Boot + 2) != 0x2103) Fail("ARM9 expansion boot pair isn't movs r0, #0; movs r1, #3");
                    if (expansion.FileLoader >> 24 != 0x02) Fail("ARM9 expansion file loader isn't a RAM address");
                }

                PickupSlotSites pickup = RomLocator.PickupSlotSitesIn(game, language, rev);
                if (pickup != null)
                {
                    checkedCount++;
                    int pickupOverlay = game switch { GameVersions.Diamond or GameVersions.Pearl => 11, GameVersions.Platinum => 16, _ => 12 };
                    byte[] ov = here.File(pickupOverlay);
                    uint ram = here.RamOf(pickupOverlay);
                    if (U16(ov, (int)pickup.Clamp) != (0x2800 | pickup.Register << 8 | 10)) Fail($"Pickup clamp at 0x{pickup.Clamp:X} isn't cmp r{pickup.Register}, #10");
                    foreach (uint literal in new[] { pickup.CommonLiteral, pickup.RareLiteral })
                        if (U32(ov, (int)literal) - ram >= (uint)ov.Length) Fail($"Pickup literal at 0x{literal:X} doesn't point into its overlay");
                    if (usDump != null)
                    {
                        PickupSlotSites usPickup = RomLocator.PickupSlotSitesIn(game, usLanguage, us.Revision);
                        byte[] usOv = usDump.File(pickupOverlay);
                        uint usRam = usDump.RamOf(pickupOverlay);
                        SameAsUs("Pickup common items", ov, (int)(U32(ov, (int)pickup.CommonLiteral) - ram), usOv, (int)(U32(usOv, (int)usPickup.CommonLiteral) - usRam), 18 * 2);
                        SameAsUs("Pickup rare items", ov, (int)(U32(ov, (int)pickup.RareLiteral) - ram), usOv, (int)(U32(usOv, (int)usPickup.RareLiteral) - usRam), 11 * 2);
                    }
                }

                MiningCodeSites mining = RomLocator.MiningCodeSitesIn(game, language, rev);
                if (mining != null)
                {
                    checkedCount++;
                    RomLocator.Spot treasures = RomLocator.WhereIn(RomLocator.Thing.MiningTreasures, game, language, rev);
                    byte[] ov = here.File(treasures.Overlay);
                    MiningCodeSites usMining = usDump == null ? null : RomLocator.MiningCodeSitesIn(game, usLanguage, us.Revision);
                    byte[] usOv = usDump?.File(treasures.Overlay);
                    uint[] sites = { mining.Range, mining.Minimum, mining.FirstDig, mining.Tries, mining.OrbRowA, mining.OrbBonusA, mining.OrbRowB, mining.OrbBonusB };
                    uint[] usSites = usMining == null ? null : new[] { usMining.Range, usMining.Minimum, usMining.FirstDig, usMining.Tries, usMining.OrbRowA, usMining.OrbBonusA, usMining.OrbRowB, usMining.OrbBonusB };
                    for (int i = 0; i < sites.Length; i++)
                    {
                        if (sites[i] == 0) continue;
                        if (usSites != null && (ov[sites[i]] != usOv[usSites[i]] || (ov[sites[i] + 1] & 0xF8) != (usOv[usSites[i] + 1] & 0xF8)))
                            Fail($"mining code site {i} at 0x{sites[i]:X} isn't the US instruction");
                    }
                }

                TrainerShinySites shiny = RomLocator.TrainerShinySitesIn(game, language, rev);
                if (shiny != null)
                {
                    checkedCount++;
                    if ((U16(arm9Here, shiny.Hook) & 0xF800) != 0xF000 || (U16(arm9Here, shiny.Hook + 2) & 0xE800) != 0xE800) Fail($"trainer shiny hook at 0x{shiny.Hook:X} isn't a call");
                    if (shiny.CallTargets.Any(t => t >> 24 != 0x02)) Fail("trainer shiny call targets aren't RAM addresses");
                    TrainerShinySites usShiny = usDump == null ? null : RomLocator.TrainerShinySitesIn(game, usLanguage, us.Revision);
                    if (usShiny != null) SameAsUs("trainer shiny ability mask", arm9Here, shiny.Mask, usArm9, usShiny.Mask, 2);
                }

                BuildingRotationSites rotation = RomLocator.BuildingRotationSitesIn(game, language, rev);
                if (rotation != null)
                {
                    checkedCount++;
                    byte[] ov = here.File(rotation.Overlay);
                    if (rotation.Hook + 4 > ov.Length) Fail($"building rotation hook at 0x{rotation.Hook:X} runs past its overlay");
                    if (rotation.RotationRoutine >> 24 != 0x02) Fail("building rotation routine isn't a RAM address");
                }

                TrainerPrizeTable prize = RomLocator.TrainerPrizeTableIn(game, language, rev);
                if (prize != null)
                {
                    checkedCount++;
                    byte[] ov = here.File(prize.Overlay);
                    if (prize.Pointer != 0 && U32(ov, (int)prize.Pointer) != here.RamOf(prize.Overlay) + prize.Table) Fail("prize table pointer doesn't point at the table");
                    TrainerPrizeTable usPrize = usDump == null ? null : RomLocator.TrainerPrizeTableIn(game, usLanguage, us.Revision);
                    if (usPrize != null) SameAsUs("prize table", ov, (int)prize.Table, usDump.File(usPrize.Overlay), (int)usPrize.Table, prize.Paired ? prize.Count * 4 : prize.Count);
                }

                uint? genderPointer = RomLocator.TrainerClassGenderPointerIn(game, language, rev);
                if (genderPointer is uint gp)
                {
                    checkedCount++;
                    uint? usGp = usDump == null ? null : RomLocator.TrainerClassGenderPointerIn(game, usLanguage, us.Revision);
                    if (usGp is uint u)
                        SameAsUs("trainer class gender table", arm9Here, (int)(U32(arm9Here, (int)gp) - here.RamOf(-1)), usArm9, (int)(U32(usArm9, (int)u) - usDump.RamOf(-1)), 105);
                }

                FollowerTableSites followers = RomLocator.FollowerTablesIn(game, language, rev);
                int? pokeathlon = RomLocator.PokeathlonMemberTableIn(game, language, rev);
                if (followers != null && usDump != null)
                {
                    checkedCount++;
                    FollowerTableSites usFollowers = RomLocator.FollowerTablesIn(game, usLanguage, us.Revision);
                    SameAsUs("follower models", arm9Here, followers.Models, usArm9, usFollowers.Models, 494 * 2);
                    SameAsUs("follower form counts", arm9Here, followers.FormCounts, usArm9, usFollowers.FormCounts, 493 * 2);
                    SameAsUs("follower female models", arm9Here, followers.Female, usArm9, usFollowers.Female, 493 * 2);
                }
                if (pokeathlon is int p && usDump != null)
                {
                    checkedCount++;
                    SameAsUs("Pokéathlon members", arm9Here, p, usArm9, RomLocator.PokeathlonMemberTableIn(game, usLanguage, us.Revision).Value, 494 * 2);
                }

                HpBarTextSites hp = RomLocator.HpBarTextSitesIn(game, language, rev);
                if (hp != null)
                {
                    checkedCount++;
                    byte[] ov = here.File(hp.Overlay);
                    foreach (int o in hp.Hooks)
                        if (U16(ov, o) != 0x6800 || (U16(ov, o + 2) & 0xF800) != 0xF000 || (U16(ov, o + 4) & 0xF800) != 0xF800) Fail($"HP bar hook at 0x{o:X} isn't ldr r0, [r0] then bl");
                    foreach (int o in hp.Copies)
                        if ((U16(ov, o) & 0xF800) != 0xF000 || (U16(ov, o + 2) & 0xF800) != 0xE800) Fail($"HP bar copy at 0x{o:X} isn't a blx");
                }
            }

            Assert.True(checkedCount > 0, "No site records were checked.");
            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        }

        private static int ItemLimitAt(byte[] arm9, int site)
        {
            if (site + 8 > arm9.Length) return -1;
            ushort load = BitConverter.ToUInt16(arm9, site + 4);
            if ((load & 0xFF00) == 0x2000) return (load & 0xFF) << (BitConverter.ToUInt16(arm9, site + 6) >> 6 & 0x1F);
            if ((load & 0xFF00) != 0x4800) return -1;
            int literal = ((site + 8) & ~3) + (load & 0xFF) * 4;
            return literal + 4 <= arm9.Length ? (int)BitConverter.ToUInt32(arm9, literal) : -1;
        }

        /// <summary>arm9 and the arm9 overlays of a .nds, decompressed.</summary>
        private sealed class Dump
        {
            private readonly byte[] _rom;
            private readonly Dictionary<int, byte[]> _files = new Dictionary<int, byte[]>();
            private readonly Dictionary<int, uint> _ram = new Dictionary<int, uint>();

            public Dump(string path) { _rom = System.IO.File.ReadAllBytes(path); }

            private uint U32(int at) => BitConverter.ToUInt32(_rom, at);

            /// <summary>The file and offset the literal at <paramref name="at"/> points into: arm9, or the literal's own overlay.</summary>
            public (byte[] File, int At) Follow(int overlay, int at)
            {
                byte[] here = File(overlay);
                if (at + 4 > here.Length) return (null, 0);
                uint value = BitConverter.ToUInt32(here, at);
                byte[] arm9 = File(-1);
                if (value >= RamOf(-1) && value - RamOf(-1) < arm9.Length) return (arm9, (int)(value - RamOf(-1)));
                if (overlay >= 0 && value >= RamOf(overlay) && value - RamOf(overlay) < here.Length) return (here, (int)(value - RamOf(overlay)));
                return (null, 0);
            }

            public uint RamOf(int overlay)
            {
                if (overlay < 0) return U32(0x28);
                File(overlay);
                return _ram[overlay];
            }

            public byte[] File(int overlay)
            {
                if (_files.TryGetValue(overlay, out byte[] f)) return f;
                if (overlay < 0)
                {
                    byte[] arm9 = _rom.AsSpan((int)U32(0x20), (int)U32(0x2C)).ToArray();
                    uint ram = U32(0x28);
                    int magic = IndexOf(arm9, BitConverter.GetBytes(0xDEC00621u));
                    uint compressedEnd = magic >= 0x1C ? BitConverter.ToUInt32(arm9, magic - 0x1C + 0x14) : 0;
                    if (compressedEnd != 0)
                    {
                        int end = (int)(compressedEnd - ram);
                        arm9 = Blz(arm9.AsSpan(0, end).ToArray()).Concat(arm9.Skip(end)).ToArray();
                    }
                    return _files[overlay] = arm9;
                }
                int table = (int)U32(0x50), fat = (int)U32(0x48);
                for (int at = table; at < table + (int)U32(0x54); at += 32)
                {
                    if (U32(at) != (uint)overlay) continue;
                    uint fileId = U32(at + 0x18), flags = U32(at + 0x1C);
                    int start = (int)U32(fat + (int)fileId * 8), stop = (int)U32(fat + (int)fileId * 8 + 4);
                    byte[] raw = _rom.AsSpan(start, stop - start).ToArray();
                    _ram[overlay] = U32(at + 4);
                    return _files[overlay] = (flags >> 24 & 1) != 0 ? Blz(raw) : raw;
                }
                throw new InvalidDataException($"Overlay {overlay} isn't in this ROM.");
            }

            private static int IndexOf(byte[] data, byte[] what)
            {
                for (int i = 0; i + what.Length <= data.Length; i += 4)
                    if (data.AsSpan(i, what.Length).SequenceEqual(what)) return i;
                return -1;
            }

            // The DS's backward LZ, as overlays and arm9 are stored.
            private static byte[] Blz(byte[] data)
            {
                uint enc = BitConverter.ToUInt32(data, data.Length - 8);
                int extra = (int)BitConverter.ToUInt32(data, data.Length - 4);
                int header = (int)(enc >> 24), encoded = (int)(enc & 0xFFFFFF);
                byte[] output = new byte[data.Length + extra];
                Array.Copy(data, output, data.Length);
                int src = data.Length - header, dst = output.Length, end = data.Length - encoded;
                while (src > end)
                {
                    byte flags = data[--src];
                    for (int bit = 0; bit < 8 && src > end; bit++, flags <<= 1)
                    {
                        if ((flags & 0x80) != 0)
                        {
                            src -= 2;
                            int v = data[src] | data[src + 1] << 8, n = (v >> 12) + 3, disp = (v & 0xFFF) + 3;
                            for (int i = 0; i < n; i++) { dst--; output[dst] = output[dst + disp]; }
                        }
                        else output[--dst] = data[--src];
                    }
                }
                return output;
            }
        }
    }
}
