using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests.Pokemon
{
    /// <summary>A type chart moved from where the game shipped it, by another patch or by DSPRE's "Make room".</summary>
    [Collection("rom")]
    public class TypeChartMoveTests
    {
        private static void WithExpansion(Action body)
        {
            var spot = RomInfo.SpotOf(RomInfo.GameTable.TypeChart).Value;
            var files = new List<string> { GameTableFile.PathOf(spot), Filesystem.expArmPath };
            if (RomInfo.SpotOf(RomInfo.GameTable.PoketchTypeChart) is RomInfo.TableSpot p) files.Add(GameTableFile.PathOf(p));
            var saved = files.Distinct().ToDictionary(f => f, f => File.Exists(f) ? File.ReadAllBytes(f) : null);
            bool flag = RomPatchState.flag_arm9Expanded;
            try
            {
                if (!SyntheticOverlaySpace.Available())
                {
                    // What the ARM9 expansion leaves behind: its flag and an empty synthetic overlay.
                    RomPatchState.flag_arm9Expanded = true;
                    File.WriteAllBytes(Filesystem.expArmPath, new byte[0x16000]);
                }
                Assert.True(SyntheticOverlaySpace.Available());
                body();
            }
            finally
            {
                RomPatchState.flag_arm9Expanded = flag;
                foreach (var (f, bytes) in saved)
                {
                    if (bytes != null) File.WriteAllBytes(f, bytes);
                    else if (File.Exists(f)) File.Delete(f);
                }
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GameTablesTests.Games), MemberType = typeof(GameTablesTests))]
        public void AChartAnotherPatchMovedIsFollowedAndKeptClear(string game)
        {
            GameTablesTests.Open(game);
            WithExpansion(() =>
            {
                var sites = RomInfo.TypeChartPointerSites.Value;
                var spot = RomInfo.SpotOf(RomInfo.GameTable.TypeChart).Value;
                string ovPath = GameTableFile.PathOf(spot);
                var vanilla = TypeChart.Load();
                byte[] chart = vanilla.ToBytes();

                // Copy the chart to 0x1000 of the expanded area with no marker and point every literal there.
                const int at = 0x1000;
                byte[] synth = File.ReadAllBytes(Filesystem.expArmPath);
                chart.CopyTo(synth, at);
                File.WriteAllBytes(Filesystem.expArmPath, synth);
                byte[] ov = File.ReadAllBytes(ovPath);
                uint ram = RomInfo.synthOverlayLoadAddress + at;
                foreach (int o in sites.col0) BitConverter.GetBytes(ram).CopyTo(ov, o);
                foreach (int o in sites.col1) BitConverter.GetBytes(ram + 1).CopyTo(ov, o);
                foreach (int o in sites.col2) BitConverter.GetBytes(ram + 2).CopyTo(ov, o);
                // Blank the old copy so reading it by mistake can't pass.
                Array.Clear(ov, spot.Offset, chart.Length);
                File.WriteAllBytes(ovPath, ov);

                var moved = TypeChart.Load();
                Assert.Equal("in the expanded ARM9 area", moved.Where);
                Assert.False(moved.InExpansion);
                Assert.Equal(chart, moved.ToBytes());
                Assert.Equal(vanilla.Matchups.Count, moved.Matchups.Count);

                // Allocators must not treat it as free space.
                var reserved = SyntheticOverlaySpace.Reserved(File.ReadAllBytes(Filesystem.expArmPath));
                Assert.Contains(reserved, r => r.Start == at && r.End == at + chart.Length);

                // An edit saves where the chart now is.
                moved.Set(0, 10, 20, false);   // Normal hits Fire 2x
                moved.Set(0, 8, 10, false);    // Normal hits Steel 1x, keeping the count at 110
                moved.Save();
                Assert.Equal(20, TypeChart.Load().Find(0, 10).Tenths);
                Assert.Equal(new byte[chart.Length], File.ReadAllBytes(ovPath).Skip(spot.Offset).Take(chart.Length).ToArray());
            });
        }

        [SkippableTheory]
        [MemberData(nameof(GameTablesTests.Games), MemberType = typeof(GameTablesTests))]
        public void MakeRoomMovesTheChartAndLetsItGrow(string game)
        {
            GameTablesTests.Open(game);
            WithExpansion(() =>
            {
                var chart = TypeChart.Load();
                Assert.Equal(110, chart.MaxMatchups);
                chart.Set(0, 10, 20, false);   // an unsaved edit rides along
                chart.MoveToExpansion();

                var moved = TypeChart.Load();
                Assert.True(moved.InExpansion);
                Assert.Equal(TypeChart.ExpandedCapacity, moved.Capacity);
                // Conversion 2's random record pick has to reach the new records too.
                var sites = RomInfo.TypeChartPointerSites.Value;
                byte[] ov = File.ReadAllBytes(GameTableFile.PathOf(RomInfo.SpotOf(RomInfo.GameTable.TypeChart).Value));
                Assert.Equal(TypeChart.ExpandedCapacity, ov[sites.countModulus]);
                Assert.Equal(0x21, ov[sites.countModulus + 1]);
                Assert.Equal(20, moved.Find(0, 10).Tenths);
                Assert.Equal(111, moved.Matchups.Count);
                Assert.Single(SyntheticOverlaySpace.Blocks(File.ReadAllBytes(Filesystem.expArmPath), TypeChart.Marker));

                // Past the old limit now.
                for (int d = 11; d < 17; d++) if (moved.Find(1, d) == null) moved.Set(1, d, 20, false);
                Assert.Null(moved.Problem());
                moved.Save();
                var again = TypeChart.Load();
                Assert.True(again.Matchups.Count > 111);
                Assert.Equal(moved.ToBytes(), again.ToBytes());

                // A second Make room is a no-op.
                again.MoveToExpansion();
                Assert.Single(SyntheticOverlaySpace.Blocks(File.ReadAllBytes(Filesystem.expArmPath), TypeChart.Marker));
            });
        }
    }
}
