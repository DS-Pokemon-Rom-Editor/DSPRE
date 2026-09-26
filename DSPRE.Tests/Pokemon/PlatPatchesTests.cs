using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests.Pokemon
{
    /// <summary>
    /// PlatPatches Item Expansion and Extra TMs on the DSPRE_TEST_PLATPATCHES project (30 Extra TM rows, Turtwig
    /// learns rows 0, 5 and 29, plus Modern Held Items); the plain Platinum fixture must show neither patch.
    /// </summary>
    [Collection("rom")]
    public class PlatPatchesTests
    {
        private const int Turtwig = 387, Bulbasaur = 1;

        private static void OpenPatched()
        {
            string path = Environment.GetEnvironmentVariable("DSPRE_TEST_PLATPATCHES");
            Skip.If(string.IsNullOrWhiteSpace(path) || !Directory.Exists(path), "DSPRE_TEST_PLATPATCHES is not set to an extracted PlatPatches project");
            new RomInfo("CPUE", path.TrimEnd('\\', '/'));
            PlatPatches.Forget();
        }

        [SkippableFact]
        public void RetailPlatinumHasNeitherPatch()
        {
            GameTablesTests.Open("Platinum");
            PlatPatches.Forget();
            Assert.Null(PlatPatches.Items());
            Assert.Null(PlatPatches.Tms());
            Assert.False(ItemTable.Exists(468));
            Assert.True(ItemTable.Exists(467));
        }

        [SkippableFact]
        public void ExpandedItemsResolveThroughTheOverflowTable()
        {
            OpenPatched();
            var items = PlatPatches.Items();
            Assert.NotNull(items);
            Assert.Equal(468, items.FirstItem);
            Assert.Equal(64, items.Count);             // TM93-TM152 and four held items
            Assert.Equal(128, items.Capacity);
            Assert.True(ItemTable.Exists(468 + 63));
            Assert.False(ItemTable.Exists(468 + 64));

            // TM93 clones TM01's data and icon; Eviolite borrows the Everstone's icon.
            var tm93 = ItemTable.Read(468);
            var tm01 = ItemTable.Read(328);
            Assert.Equal(tm01.itemData, tm93.itemData);
            Assert.Equal(tm01.itemIcon, tm93.itemIcon);
            Assert.Equal(ItemTable.Read(229).itemIcon, ItemTable.Read(0x210).itemIcon);
            Assert.Contains(328, ItemTable.SharingData(468, 532));
        }

        [SkippableFact]
        public void ExtraTmsReadTheirMovesAndBothCompatibilityTables()
        {
            OpenPatched();
            var t = PlatPatches.Tms();
            Assert.NotNull(t);
            Assert.Equal(60, t.Count);
            Assert.Equal(468, t.ItemIds[0]);
            Assert.Equal(527, t.ItemIds[59]);
            Assert.Equal(2, t.MoveIds[0]);      // Karate Chop
            Assert.Equal(10, t.MoveIds[5]);     // Scratch
            Assert.Equal(38, t.MoveIds[29]);    // Double-Edge
            foreach (int row in new[] { 0, 5, 29 }) Assert.True(PlatPatches.CanLearn(t, Turtwig, row), $"row {row}");
            Assert.False(PlatPatches.CanLearn(t, Turtwig, 1));
            Assert.False(PlatPatches.CanLearn(t, Turtwig, 28));
            Assert.True(PlatPatches.CanLearn(t, Bulbasaur, 1));
            Assert.Equal("TM93", PlatPatches.ExtraTms.Label(0));
        }

        [SkippableFact]
        public void ExtraTmEditsKeepEveryOtherBit()
        {
            OpenPatched();
            var t = PlatPatches.Tms();
            PlatPatches.CanLearn(t, Turtwig, 0);   // unpacks the personal archive
            string personal = Path.Combine(RomInfo.gameDirs[RomInfo.DirNames.personalPokeData].unpackedDir, Turtwig.ToString("D4"));
            var files = new[] { personal, Filesystem.expArmPath };
            var saved = files.ToDictionary(f => f, File.ReadAllBytes);
            try
            {
                byte[] personalBefore = File.ReadAllBytes(personal);
                byte[] synthBefore = File.ReadAllBytes(Filesystem.expArmPath);

                PlatPatches.SetExtraTmMove(3, 94);                                            // TM96 -> Psychic
                PlatPatches.SetCanLearn(t, 3, new Dictionary<int, bool> { [Turtwig] = true });   // personal word
                PlatPatches.SetCanLearn(t, 40, new Dictionary<int, bool> { [Turtwig] = true });  // synthetic mask
                PlatPatches.SetCanLearn(t, 0, new Dictionary<int, bool> { [Turtwig] = false });

                var again = PlatPatches.Tms();
                Assert.Equal(94, again.MoveIds[3]);
                Assert.True(PlatPatches.CanLearn(again, Turtwig, 3));
                Assert.True(PlatPatches.CanLearn(again, Turtwig, 40));
                Assert.False(PlatPatches.CanLearn(again, Turtwig, 0));
                Assert.True(PlatPatches.CanLearn(again, Turtwig, 5));

                // Only bits 4 (row 0) and 7 (row 3) of the fourth TM word change; HM05-HM08 stay.
                byte[] personalAfter = File.ReadAllBytes(personal);
                uint before = BitConverter.ToUInt32(personalBefore, 0x28), after = BitConverter.ToUInt32(personalAfter, 0x28);
                Assert.Equal(before ^ ((1u << 4) | (1u << 7)), after);
                Assert.Equal(personalBefore.Take(0x28), personalAfter.Take(0x28));
                Assert.Equal(personalBefore.Skip(0x2C), personalAfter.Skip(0x2C));

                // In the synthetic overlay only the move and Turtwig's mask changed.
                byte[] synthAfter = File.ReadAllBytes(Filesystem.expArmPath);
                var changed = Enumerable.Range(0, synthBefore.Length).Where(i => synthBefore[i] != synthAfter[i]).ToList();
                int move = t.Marker + 0x590 + 3 * 2, mask = t.Marker + 0x608 + Turtwig * 4;
                Assert.All(changed, i => Assert.True((i >= move && i < move + 2) || (i >= mask && i < mask + 4), $"byte {i:X}"));
            }
            finally
            {
                foreach (var (f, b) in saved) File.WriteAllBytes(f, b);
                PlatPatches.Forget();
            }
        }

        [SkippableFact]
        public void ExpandedItemRowsSave()
        {
            OpenPatched();
            var files = new[] { Filesystem.expArmPath, RomInfo.arm9Path };
            var saved = files.ToDictionary(f => f, File.ReadAllBytes);
            try
            {
                byte[] arm9 = File.ReadAllBytes(RomInfo.arm9Path);
                var e = ItemTable.Read(0x210);
                e.itemPalette = 11;
                ItemTable.Write(0x210, e);
                Assert.Equal(11u, ItemTable.Read(0x210).itemPalette);
                Assert.Equal(arm9, File.ReadAllBytes(RomInfo.arm9Path));   // expanded rows never touch arm9
                Assert.Throws<InvalidOperationException>(() => ItemTable.Write(468 + 64, e));
            }
            finally
            {
                foreach (var (f, b) in saved) File.WriteAllBytes(f, b);
                PlatPatches.Forget();
            }
        }
    }
}
