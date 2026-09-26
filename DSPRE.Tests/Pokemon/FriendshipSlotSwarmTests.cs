using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests.Pokemon
{
    /// <summary>Friendship changes, encounter slot odds and swarm destinations on the three fixture projects.
    /// Expected values are the retail ones in the decomps.</summary>
    [Collection("rom")]
    public class FriendshipSlotSwarmTests
    {
        private static void Restoring(IEnumerable<string> paths, Action body, Action alsoRestore = null)
        {
            var saved = paths.Distinct().ToDictionary(p => p, p => File.Exists(p) ? File.ReadAllBytes(p) : null);
            try { body(); }
            finally
            {
                alsoRestore?.Invoke();
                foreach (var (path, bytes) in saved)
                {
                    if (bytes != null) File.WriteAllBytes(path, bytes);
                    else if (File.Exists(path)) File.Delete(path);
                }
            }
        }

        private static string OverlayOrArm9(int ov) => ov < 0 ? RomInfo.arm9Path : OverlayUtils.GetPath(ov);

        // ---------------------------------------------------------------- friendship

        [SkippableTheory]
        [MemberData(nameof(GameTablesTests.Games), MemberType = typeof(GameTablesTests))]
        public void FriendshipChangesReadAsTheGameHasThem(string game)
        {
            GameTablesTests.Open(game);
            Assert.Null(FriendshipTable.WhyNot());
            var t = FriendshipTable.Load();
            Assert.Equal(new sbyte[] { 5, 3, 2 }, Enumerable.Range(0, 3).Select(b => t.Values[0, b]));
            Assert.Equal(new sbyte[] { -5, -5, -10 }, Enumerable.Range(0, 3).Select(b => t.Values[7, b]));
            Assert.Equal(new sbyte[] { 3, 2, 1 }, Enumerable.Range(0, 3).Select(b => t.Values[9, b]));
            Assert.Empty(t.Risky());
            Assert.Equal(GameTableFile.Read(RomInfo.GameTable.FriendshipChanges, FriendshipTable.Size), t.ToBytes());
        }

        [SkippableTheory]
        [MemberData(nameof(GameTablesTests.Games), MemberType = typeof(GameTablesTests))]
        public void FriendshipChangesSaveOnlyTheirOwnBytes(string game)
        {
            GameTablesTests.Open(game);
            var spot = RomInfo.SpotOf(RomInfo.GameTable.FriendshipChanges).Value;
            GameTablesTests.Restoring(RomInfo.GameTable.FriendshipChanges, () =>
            {
                byte[] before = File.ReadAllBytes(RomInfo.arm9Path);
                var t = FriendshipTable.Load();
                t.Values[5, 0] = 90;     // walking, low friendship
                t.Values[6, 2] = -128;
                t.Save();
                var again = FriendshipTable.Load();
                Assert.Equal(90, again.Values[5, 0]);
                Assert.Equal(-128, again.Values[6, 2]);
                Assert.Contains((5, 0), again.Risky());
                byte[] after = File.ReadAllBytes(RomInfo.arm9Path);
                var changed = Enumerable.Range(0, before.Length).Where(i => before[i] != after[i]).ToList();
                Assert.NotEmpty(changed);
                Assert.All(changed, i => Assert.InRange(i, spot.Offset, spot.Offset + FriendshipTable.Size - 1));
            });
        }

        // ---------------------------------------------------------------- slot odds

        [SkippableTheory]
        [MemberData(nameof(GameTablesTests.Games), MemberType = typeof(GameTablesTests))]
        public void SlotOddsReadAsTheGameHasThem(string game)
        {
            GameTablesTests.Open(game);
            Assert.Null(EncounterSlotOdds.WhyNot());
            var odds = EncounterSlotOdds.Load();
            var walk = odds.Methods.Single(m => m.Name == "Walking");
            Assert.Equal(new[] { 20, 20, 10, 10, 10, 10, 5, 5, 4, 4, 1, 1 }, walk.Percents);
            Assert.Equal(new[] { 60, 30, 5, 4, 1 }, odds.Methods.Single(m => m.Name == "Surfing").Percents);
            if (game == "HeartGold")
            {
                Assert.Equal(new[] { 40, 30, 15, 10, 5 }, odds.Methods.Single(m => m.Name == "Fishing (all rods)").Percents);
                Assert.Equal(new[] { 80, 20 }, odds.Methods.Single(m => m.Name == "Rock Smash").Percents);
                Assert.Equal(HeadbuttRules.SlotChance, odds.Methods.Single(m => m.Name == "Headbutt").Percents);
            }
            else
            {
                Assert.Equal(new[] { 60, 30, 5, 4, 1 }, odds.Methods.Single(m => m.Name == "Old Rod").Percents);
                Assert.Equal(new[] { 40, 40, 15, 4, 1 }, odds.Methods.Single(m => m.Name == "Good Rod").Percents);
                Assert.Equal(new[] { 40, 40, 15, 4, 1 }, odds.Methods.Single(m => m.Name == "Super Rod").Percents);
            }
            Assert.Null(odds.Problem());
        }

        [SkippableTheory]
        [MemberData(nameof(GameTablesTests.Games), MemberType = typeof(GameTablesTests))]
        public void SlotOddsSaveAndTheLastWalkingSlotCanWiden(string game)
        {
            GameTablesTests.Open(game);
            int ov = RomInfo.SlotOddsMethods[0].Overlay;
            string path = OverlayUtils.GetPath(ov);
            Restoring(new[] { path }, () =>
            {
                byte[] retail = File.ReadAllBytes(path);
                var odds = EncounterSlotOdds.Load();
                odds.Save();
                Assert.Equal(retail, File.ReadAllBytes(path));   // an unchanged save is byte-identical

                var walk = odds.Methods.Single(m => m.Name == "Walking");
                walk.Percents[0] = 10; walk.Percents[1] = 30;    // 20/20 -> 10/30
                walk.Percents[10] = 2; walk.Percents[11] = 0;    // eleventh slot two rolls, last slot none
                var surf = odds.Methods.Single(m => m.Name == "Surfing");
                surf.Percents[0] = 100; surf.Percents[1] = 0; surf.Percents[2] = 0; surf.Percents[3] = 0; surf.Percents[4] = 0;
                Assert.Null(odds.Problem());
                odds.Save();

                var again = EncounterSlotOdds.Load();
                Assert.Equal(walk.Percents, again.Methods.Single(m => m.Name == "Walking").Percents);
                Assert.Equal(surf.Percents, again.Methods.Single(m => m.Name == "Surfing").Percents);
                byte[] edited = File.ReadAllBytes(path);
                int tail = RomInfo.SlotOddsMethods[0].Boundaries[0][0] + RomInfo.LandLastSlotSite;
                Assert.Equal(0xD2, edited[tail + 3]);   // bcs
                Assert.Equal(100, edited[tail]);        // eleventh slot ends at 100

                // Back to retail odds gives back the retail bytes, bne form included.
                again.Methods.Single(m => m.Name == "Walking").Percents.AsSpan().Clear();
                new[] { 20, 20, 10, 10, 10, 10, 5, 5, 4, 4, 1, 1 }.CopyTo(again.Methods.Single(m => m.Name == "Walking").Percents, 0);
                new[] { 60, 30, 5, 4, 1 }.CopyTo(again.Methods.Single(m => m.Name == "Surfing").Percents, 0);
                again.Save();
                Assert.Equal(retail, File.ReadAllBytes(path));

                again.Methods[0].Percents[0] = 21;
                Assert.NotNull(again.Problem());
            });
        }

        [Theory]
        [InlineData("EXTRATMSV1", 0x608 + 4 * 1024)]
        [InlineData("ITEMEXPV2", 0x298 + 128 * 8)]
        public void PlatPatchesBlocksAreNotFreeSpace(string marker, int extent)
        {
            // The blocks keep pre-reserved zero rows after their marker, which a zero scan would take.
            byte[] data = new byte[0x4000];
            System.Text.Encoding.ASCII.GetBytes(marker).CopyTo(data, 0);
            int at = SyntheticOverlaySpace.FindFree(data, 0x100, 4, SyntheticOverlaySpace.PlatPatchesBlocks(data).ToList());
            Assert.Equal(extent, at);
        }

        // ---------------------------------------------------------------- swarms

        [SkippableTheory]
        [InlineData("HeartGold", 20, 9, 0)]
        [InlineData("Platinum", 22, 342, 0)]
        [InlineData("Diamond", 28, 342, 0)]
        public void SwarmsReadAsTheGameHasThem(string game, int rows, int firstHeader, int firstMethod)
        {
            GameTablesTests.Open(game);
            Assert.Null(SwarmTable.WhyNot());
            var t = SwarmTable.Load();
            Assert.Equal(rows, t.Rows.Count);
            Assert.Equal(rows, t.Capacity);
            Assert.Equal((ushort)firstHeader, t.Rows[0].Header);
            Assert.Equal((ushort)firstMethod, t.Rows[0].Method);
            Assert.Equal(game == "HeartGold", t.HasMethod);
            Assert.Equal("where the game keeps it", t.Where);
        }

        [SkippableTheory]
        [MemberData(nameof(GameTablesTests.Games), MemberType = typeof(GameTablesTests))]
        public void SwarmsSaveShrinkAndGrow(string game)
        {
            GameTablesTests.Open(game);
            var sites = RomInfo.SwarmCodeSites;
            string code = OverlayOrArm9(sites.Overlay);
            int headers = RomInfo.GetHeaderCount();
            bool flag = RomPatchState.flag_arm9Expanded;
            Restoring(new[] { code, Filesystem.expArmPath }, () =>
            {
                byte[] retail = File.ReadAllBytes(code);
                var t = SwarmTable.Load();
                t.Save(headers, null);
                Assert.Equal(retail, File.ReadAllBytes(code));   // unchanged save is byte-identical

                ushort first = t.Rows[1].Header;
                t.Rows[0].Header = first;
                t.Rows.RemoveAt(t.Rows.Count - 1);
                t.Save(headers, null);
                var shrunk = SwarmTable.Load();
                Assert.Equal(sites.Rows - 1, shrunk.Rows.Count);
                Assert.Equal(first, shrunk.Rows[0].Header);
                Assert.Equal(sites.Rows, shrunk.Capacity);
                Assert.All(sites.CountSites, o => Assert.Equal(sites.Rows - 1, File.ReadAllBytes(code)[o]));

                // Growing needs the expansion; stage what it leaves behind if the fixture lacks it.
                if (!SyntheticOverlaySpace.Available())
                {
                    RomPatchState.flag_arm9Expanded = true;
                    File.WriteAllBytes(Filesystem.expArmPath, new byte[0x16000]);
                }
                for (int i = 0; i < 6; i++) shrunk.Rows.Add(new SwarmTable.Row { Header = first, Method = 0 });
                Assert.False(shrunk.FitsWhereItIs);
                shrunk.Save(headers, null);
                var grown = SwarmTable.Load();
                Assert.Equal(sites.Rows + 5, grown.Rows.Count);
                Assert.Equal("in the expanded ARM9 area", grown.Where);
                Assert.Single(SyntheticOverlaySpace.Blocks(File.ReadAllBytes(Filesystem.expArmPath), SwarmTable.Marker));
                Assert.Equal(first, grown.Rows[^1].Header);

                Assert.NotNull(grown.Problem(headers, h => false));
                grown.Rows[0].Header = (ushort)headers;
                Assert.NotNull(grown.Problem(headers, null));
            }, () => RomPatchState.flag_arm9Expanded = flag);
        }
    }
}
