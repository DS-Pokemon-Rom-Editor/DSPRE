using System;
using System.IO;
using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests.Pokemon
{
    /// <summary>Fixed game tables edited in place, checked on US HeartGold, Platinum Rev 1 and Diamond v05.
    /// Expected values are the retail ones in the pokeheartgold, pokeplatinum and pokediamond decomps.</summary>
    [Collection("rom")]
    public class GameTablesTests
    {
        public static readonly TheoryData<string> Games = new() { "HeartGold", "Platinum", "Diamond" };

        internal static void Open(string game)
        {
            var (id, path) = game switch
            {
                "HeartGold" => ("IPKE", TestRoms.HeartGold),
                "Platinum" => ("CPUE", TestRoms.Platinum),
                _ => ("ADAE", TestRoms.Diamond),
            };
            Skip.If(!Directory.Exists(path), $"{game} test project not configured");
            new RomInfo(id, path);
        }

        /// <summary>Runs <paramref name="body"/> and then puts the file holding <paramref name="table"/> back as it was.</summary>
        internal static void Restoring(RomInfo.GameTable table, Action body)
        {
            string path = GameTableFile.PathOf(RomInfo.SpotOf(table).Value);
            byte[] original = File.ReadAllBytes(path);
            try { body(); }
            finally { File.WriteAllBytes(path, original); }
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void WildHeldItemOddsReadAsTheGameHasThem(string game)
        {
            Open(game);
            Assert.Null(WildHeldItemOdds.WhyNot());
            var odds = WildHeldItemOdds.Load();
            Assert.Equal((45, 95), (odds.Normal.NoneBelow, odds.Normal.RareFrom));
            Assert.Equal((20, 80), (odds.CompoundEyes.NoneBelow, odds.CompoundEyes.RareFrom));
            Assert.Equal((45, 50, 5), (odds.Normal.NonePercent, odds.Normal.CommonPercent, odds.Normal.RarePercent));
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void WildHeldItemOddsSaveOnlyTheirOwnBytes(string game)
        {
            Open(game);
            var spot = RomInfo.SpotOf(RomInfo.GameTable.WildHeldItemOdds).Value;
            string path = GameTableFile.PathOf(spot);
            Restoring(RomInfo.GameTable.WildHeldItemOdds, () =>
            {
                byte[] before = File.ReadAllBytes(path);
                var odds = WildHeldItemOdds.Load();
                odds.Normal.NoneBelow = 30; odds.Normal.RareFrom = 90;
                odds.Save();

                var again = WildHeldItemOdds.Load();
                Assert.Equal((30, 90), (again.Normal.NoneBelow, again.Normal.RareFrom));
                Assert.Equal((20, 80), (again.CompoundEyes.NoneBelow, again.CompoundEyes.RareFrom));
                byte[] after = File.ReadAllBytes(path);
                var changed = Enumerable.Range(0, before.Length).Where(i => before[i] != after[i]).ToList();
                Assert.All(changed, i => Assert.InRange(i, spot.Offset, spot.Offset + WildHeldItemOdds.Size - 1));
                Assert.NotEmpty(changed);
            });
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void GrowthCurvesReadAsTheGameHasThem(string game)
        {
            Open(game);
            Assert.Null(GrowthTable.WhyNot());
            var table = GrowthTable.Load();
            Assert.Equal(1000u, table.Totals[0][10]);          // Medium Fast
            Assert.Equal(1_000_000u, table.Totals[0][100]);
            Assert.Equal(1_640_000u, table.Totals[2][100]);    // Fluctuating
            Assert.Equal(600_000u, table.Totals[1][100]);      // Erratic
            Assert.Equal(table.Totals[0], table.Totals[6]);
            Assert.Equal(table.Totals[0], table.Totals[7]);
            for (int c = 0; c < GrowthTable.Curves; c++) Assert.Null(table.Problem(c));
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void AGrowthCurveSavesOnlyItself(string game)
        {
            Open(game);
            string dir = RomInfo.gameDirs[RomInfo.DirNames.growthTable].unpackedDir;
            var before = Enumerable.Range(0, GrowthTable.Curves).Select(c => File.ReadAllBytes(Path.Combine(dir, c.ToString("D4")))).ToArray();
            try
            {
                var table = GrowthTable.Load();
                table.Totals[4][50] += 1;
                table.Save(4);
                var again = GrowthTable.Load();
                Assert.Equal(table.Totals[4][50], again.Totals[4][50]);
                for (int c = 0; c < GrowthTable.Curves; c++)
                    if (c != 4) Assert.Equal(before[c], File.ReadAllBytes(Path.Combine(dir, c.ToString("D4"))));

                table.Totals[4][50] = table.Totals[4][49];
                Assert.NotNull(table.Problem(4));
                Assert.Throws<InvalidOperationException>(() => table.Save(4));
            }
            finally
            {
                for (int c = 0; c < GrowthTable.Curves; c++) File.WriteAllBytes(Path.Combine(dir, c.ToString("D4")), before[c]);
            }
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void IncenseBabiesReadAndSaveInPlace(string game)
        {
            Open(game);
            Assert.Null(IncenseBreedingTable.WhyNot());
            var table = IncenseBreedingTable.Load();
            Assert.Equal((360, 255, 202), ((int)table.Rows[0].Baby, (int)table.Rows[0].Item, (int)table.Rows[0].Fallback));   // Wynaut, Lax Incense, Wobbuffet
            Assert.Null(table.Problem(RomInfo.GetPokemonNames().Length, RomInfo.GetItemNames().Length));

            var spot = RomInfo.SpotOf(RomInfo.GameTable.IncenseBabies).Value;
            string path = GameTableFile.PathOf(spot);
            Restoring(RomInfo.GameTable.IncenseBabies, () =>
            {
                byte[] before = File.ReadAllBytes(path);
                table.Rows[0].Fallback = 25;
                table.Save(RomInfo.GetPokemonNames().Length, RomInfo.GetItemNames().Length);
                Assert.Equal(25, IncenseBreedingTable.Load().Rows[0].Fallback);
                byte[] after = File.ReadAllBytes(path);
                var changed = Enumerable.Range(0, before.Length).Where(i => before[i] != after[i]).ToList();
                Assert.All(changed, i => Assert.InRange(i, spot.Offset, spot.Offset + IncenseBreedingTable.Size - 1));

                table.Rows[1].Baby = table.Rows[0].Baby;
                Assert.Contains("already has a row", table.Problem(RomInfo.GetPokemonNames().Length, RomInfo.GetItemNames().Length));
            });
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void BerriesReadAsTheGameHasThem(string game)
        {
            Open(game);
            Assert.Null(BerryData.WhyNot());
            var all = BerryData.LoadAll();
            Assert.Equal(BerryData.Count, all.Count);
            var cheri = all[0];
            Assert.Equal((20, 2, 1, 3, 15), ((int)cheri.SizeMm, (int)cheri.FirmnessLevel, (int)cheri.Yield, (int)cheri.HoursPerStage, (int)cheri.Drain));
            Assert.Equal(new byte[] { 10, 0, 0, 0, 0 }, cheri.Flavour);
            Assert.Equal(25, cheri.Smoothness);
            Assert.All(all, b => Assert.Null(b.Problem()));
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void ABerrySavesOnlyItself(string game)
        {
            Open(game);
            string dir = RomInfo.gameDirs[RomInfo.DirNames.berryData].unpackedDir;
            var before = Enumerable.Range(0, BerryData.Count).Select(b => File.ReadAllBytes(Path.Combine(dir, b.ToString("D4")))).ToArray();
            try
            {
                var all = BerryData.LoadAll();
                all[5].HoursPerStage = 1;
                all[5].Save(5);
                Assert.Equal(1, BerryData.LoadAll()[5].HoursPerStage);
                for (int b = 0; b < BerryData.Count; b++)
                    if (b != 5) Assert.Equal(before[b], File.ReadAllBytes(Path.Combine(dir, b.ToString("D4"))));
                all[5].FirmnessLevel = 6;
                Assert.Throws<InvalidOperationException>(() => all[5].Save(5));
            }
            finally
            {
                for (int b = 0; b < BerryData.Count; b++) File.WriteAllBytes(Path.Combine(dir, b.ToString("D4")), before[b]);
            }
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void TheSameHeldItemPatchFlipsOnlyItsBranch(string game)
        {
            Open(game);
            Assert.Equal(PatchToolboxLogic.PatchState.Available, PatchToolboxLogic.SameHeldItemOddsState());
            var spot = RomInfo.SpotOf(RomInfo.GameTable.HeldItemSameItemBranch).Value;
            string path = GameTableFile.PathOf(spot);
            var confirm = PatchToolboxLogic.ConfirmYesNo;
            var info = PatchToolboxLogic.ShowInfo;
            Restoring(RomInfo.GameTable.HeldItemSameItemBranch, () =>
            {
                try
                {
                    PatchToolboxLogic.ConfirmYesNo = (_, _) => true;
                    PatchToolboxLogic.ShowInfo = (_, _) => { };
                    byte[] before = File.ReadAllBytes(path);
                    Assert.True(PatchToolboxLogic.ApplySameHeldItemOddsPatch());
                    Assert.Equal(PatchToolboxLogic.PatchState.Applied, PatchToolboxLogic.SameHeldItemOddsState());
                    byte[] after = File.ReadAllBytes(path);
                    var changed = Enumerable.Range(0, before.Length).Where(i => before[i] != after[i]).ToList();
                    Assert.Equal(new[] { spot.Offset + 1 }, changed);   // D1 (bne) becomes E0 (b)
                    Assert.False(PatchToolboxLogic.ApplySameHeldItemOddsPatch());
                }
                finally
                {
                    PatchToolboxLogic.ConfirmYesNo = confirm;
                    PatchToolboxLogic.ShowInfo = info;
                }
            });
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void TheTypeChartReadsAsTheGameHasIt(string game)
        {
            Open(game);
            Assert.Null(TypeChart.WhyNot());
            var chart = TypeChart.Load();
            Assert.Equal(112, chart.Capacity);
            Assert.Equal(110, chart.Matchups.Count);
            Assert.Equal(new[] { (0, 7), (1, 7) },
                chart.Matchups.Where(m => m.ForesightRemovable).Select(m => ((int)m.Attacker, (int)m.Defender)));
            Assert.Equal(5, chart.Find(10, 11).Tenths);    // Fire into Water
            Assert.Equal(0, chart.Find(4, 2).Tenths);      // Ground into Flying

            var spot = RomInfo.SpotOf(RomInfo.GameTable.TypeChart).Value;
            byte[] onDisk = DSUtils.ReadFromFile(GameTableFile.PathOf(spot), spot.Offset, 112 * TypeChart.RecordSize);
            Assert.Equal(onDisk, chart.ToBytes());
            if (RomInfo.SpotOf(RomInfo.GameTable.PoketchTypeChart) != null)
                Assert.Equal(GameTableFile.Read(RomInfo.GameTable.PoketchTypeChart, 324), TypeChart.PoketchGrid(chart.Matchups));
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void AnEditedTypeChartKeepsItsPoketchCopyInStep(string game)
        {
            Open(game);
            bool poketch = RomInfo.SpotOf(RomInfo.GameTable.PoketchTypeChart) != null;
            string poketchPath = poketch ? GameTableFile.PathOf(RomInfo.SpotOf(RomInfo.GameTable.PoketchTypeChart).Value) : null;
            byte[] poketchBefore = poketch ? File.ReadAllBytes(poketchPath) : null;
            Restoring(RomInfo.GameTable.TypeChart, () =>
            {
                try
                {
                    var chart = TypeChart.Load();
                    chart.Set(11, 15, 5, false);     // Ice resists Water
                    chart.Set(10, 11, 10, false);    // Fire into Water becomes neutral
                    chart.Save();
                    var again = TypeChart.Load();
                    Assert.Equal(5, again.Find(11, 15).Tenths);
                    Assert.Null(again.Find(10, 11));
                    Assert.Equal(110, again.Matchups.Count);
                    if (poketch)
                        Assert.Equal(TypeChart.PoketchGrid(again.Matchups), GameTableFile.Read(RomInfo.GameTable.PoketchTypeChart, 324));

                    for (int i = 0; i < 3; i++) again.Set(i, 16, 5, false);
                    Assert.NotNull(again.Problem());   // 113 matchups can't fit in 110
                }
                finally
                {
                    if (poketch) File.WriteAllBytes(poketchPath, poketchBefore);
                }
            });
        }

        [SkippableFact]
        public void PlatinumTutorsReadAsTheGameHasThem()
        {
            Open("Platinum");
            Assert.Null(MoveTutorData.WhyNot());
            var data = MoveTutorData.Load();
            Assert.Equal(38, data.Pool.Count);
            Assert.Equal(291, data.Pool[0].Move);                       // Dive
            Assert.Equal(new byte[] { 2, 4, 2, 0 }, data.Pool[0].Costs);
            Assert.Equal(0, data.Pool[0].Where);                        // Route 212
            Assert.Equal((253, 2), ((int)data.Pool[37].Move, data.Pool[37].Where));
            Assert.Equal(new byte[] { 0x06, 0x20, 0x10, 0x08, 0x04 }, data.MaskBytes().Take(5));   // Bulbasaur
            Assert.Equal(GameTableFile.Read(RomInfo.GameTable.TutorPool, 38 * 12), data.PoolBytes());
            Assert.Equal(GameTableFile.Read(RomInfo.GameTable.TutorCompatibility, 505 * 5), data.MaskBytes());
            Assert.Null(data.Problem(RomInfo.GetAttackNames().Length));
        }

        [SkippableFact]
        public void HeartGoldTutorsReadAsTheGameHasThem()
        {
            Open("HeartGold");
            Assert.Null(MoveTutorData.WhyNot());
            var data = MoveTutorData.Load();
            Assert.Equal(52, data.Pool.Count);
            Assert.Equal((291, 40, 0), ((int)data.Pool[0].Move, (int)data.Pool[0].Costs[0], data.Pool[0].Where));   // Dive, 40 BP
            Assert.Equal((29, 0, 3), ((int)data.Pool[51].Move, (int)data.Pool[51].Costs[0], data.Pool[51].Where));  // Headbutt tutor
            Assert.Equal(GameTableFile.Read(RomInfo.GameTable.TutorPool, 52 * 4), data.PoolBytes());
            Assert.Equal(File.ReadAllBytes(Path.Combine(RomInfo.dataPath, "fielddata", "wazaoshie", "waza_oshie.bin")), data.MaskBytes());
            Assert.Null(data.Problem(RomInfo.GetAttackNames().Length));
        }

        [SkippableTheory]
        [InlineData("Platinum")]
        [InlineData("HeartGold")]
        public void TutorEditsSaveAndDuplicatesAreRefused(string game)
        {
            Open(game);
            string maskPath = game == "Platinum" ? null : Path.Combine(RomInfo.dataPath, "fielddata", "wazaoshie", "waza_oshie.bin");
            byte[] maskBefore = maskPath == null ? null : File.ReadAllBytes(maskPath);
            Restoring(RomInfo.GameTable.TutorPool, () =>
            {
                try
                {
                    int moves = RomInfo.GetAttackNames().Length;
                    var data = MoveTutorData.Load();
                    data.Pool[0].Costs[0] = 9;
                    data.SetLearns(MoveTutorData.RowOf(25), 0, true);   // Pikachu learns tutor move 1
                    data.Save(moves);
                    var again = MoveTutorData.Load();
                    Assert.Equal(9, again.Pool[0].Costs[0]);
                    Assert.True(again.Learns(MoveTutorData.RowOf(25), 0));

                    again.Pool[1].Move = again.Pool[0].Move;
                    Assert.Contains("repeats", again.Problem(moves));
                }
                finally
                {
                    if (maskPath != null) File.WriteAllBytes(maskPath, maskBefore);
                }
            });
        }

        [SkippableTheory]
        [InlineData("Platinum", 167, 194, 110, 164)]
        [InlineData("Diamond", 166, 192, 122, 150)]
        public void MiningOddsReadAsTheGameHasThem(string game, int a, int b, int c, int d)
        {
            Open(game);
            Assert.Null(MiningTable.WhyNot());
            var table = MiningTable.Load();
            Assert.Equal(71, table.Treasures.Count);
            var red = table.Treasures.First(t => t.MiningId == 3);   // Small Red Sphere
            Assert.Equal(new ushort[] { (ushort)a, (ushort)b, (ushort)c, (ushort)d }, red.Weights);
            Assert.Equal("Small Red Sphere", table.NameOf(red, RomInfo.GetItemNames()));
            Assert.Equal(110, table.Treasures.First(t => t.MiningId == 11).BagItem);   // Oval Stone
            Assert.Equal(GameTableFile.Read(RomInfo.GameTable.MiningTreasures, MiningTable.Size), table.ToBytes());
            Assert.Null(table.Problem());
        }

        [SkippableTheory]
        [InlineData("Platinum")]
        [InlineData("Diamond")]
        public void MiningWeightsSaveOnlyWeights(string game)
        {
            Open(game);
            var spot = RomInfo.SpotOf(RomInfo.GameTable.MiningTreasures).Value;
            string path = GameTableFile.PathOf(spot);
            Restoring(RomInfo.GameTable.MiningTreasures, () =>
            {
                byte[] before = File.ReadAllBytes(path);
                var table = MiningTable.Load();
                table.Treasures[0].Weights[2] = 999;
                table.Save();
                Assert.Equal(999, MiningTable.Load().Treasures[0].Weights[2]);
                byte[] after = File.ReadAllBytes(path);
                var changed = Enumerable.Range(0, before.Length).Where(i => before[i] != after[i]).ToList();
                Assert.All(changed, i => Assert.InRange(i, spot.Offset + 4, spot.Offset + 11));

                foreach (var t in table.Treasures) t.Weights[1] = 0;
                Assert.NotNull(table.Problem());
            });
        }

        [Fact]
        public void TutorRowsFollowThePersonalFiles()
        {
            Assert.Equal(0, MoveTutorData.RowOf(1));
            Assert.Equal(492, MoveTutorData.RowOf(493));
            Assert.Equal(-1, MoveTutorData.RowOf(494));
            Assert.Equal(493, MoveTutorData.RowOf(496));   // Deoxys Attack
            Assert.Equal(504, MoveTutorData.RowOf(507));   // Rotom Mow
        }

        [Fact]
        public void HeldItemOddsOutOfOrderAreRefused()
        {
            var odds = new WildHeldItemOdds(new byte[] { 45, 0, 95, 0, 20, 0, 80, 0 });
            Assert.Null(odds.Problem());
            odds.Normal.NoneBelow = 96;
            Assert.NotNull(odds.Problem());
            odds.Normal.NoneBelow = 0; odds.Normal.RareFrom = 101;
            Assert.NotNull(odds.Problem());
        }
    }
}
