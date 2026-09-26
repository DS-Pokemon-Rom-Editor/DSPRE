using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests.Pokemon
{
    /// <summary>The Battle Point exchange counters on Platinum Rev 1 and Diamond v05, against retail values.</summary>
    [Collection("rom")]
    public class BpShopDataTests
    {
        private const ushort Protein = 46, RareCandy = 50, Tm06 = 333, Tm26 = 353, Tm01 = 328;

        /// <summary>Runs <paramref name="body"/> and puts every listed file back, deleting ones it created.</summary>
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

        private static List<string> TouchedFiles()
        {
            var files = new List<string> { RomInfo.arm9Path, Filesystem.expArmPath };
            var sites = RomInfo.BpShopCodeSites;
            if (sites.ListPointers >= 0) files.Add(OverlayUtils.GetPath(7));
            return files;
        }

        [SkippableTheory]
        [InlineData("Platinum")]
        [InlineData("Diamond")]
        public void CountersReadAsTheGameHasThem(string game)
        {
            GameTablesTests.Open(game);
            Assert.Null(BpShopData.WhyNot());
            var shop = BpShopData.Load();
            Assert.Equal(BpShopData.VanillaLeft, shop.Left.Count);
            Assert.Equal(BpShopData.VanillaRight, shop.Right.Count);
            Assert.Equal((Protein, (ushort)1), (shop.Left[0].Item, shop.Left[0].Price));
            Assert.Equal((RareCandy, (ushort)48), (shop.Left[^1].Item, shop.Left[^1].Price));
            Assert.Equal((Tm06, (ushort)32), (shop.Right[0].Item, shop.Right[0].Price));
            Assert.Equal((Tm26, (ushort)80), (shop.Right[^1].Item, shop.Right[^1].Price));
            Assert.Null(shop.Problem(RomInfo.GetItemNames().Length));
            Assert.False(shop.RightHasNonTm);
        }

        [SkippableFact]
        public void HeartGoldIsLeftToScripts()
        {
            GameTablesTests.Open("HeartGold");
            Assert.NotNull(BpShopData.WhyNot());
        }

        [SkippableTheory]
        [InlineData("Platinum")]
        [InlineData("Diamond")]
        public void PriceChangesSaveInPlace(string game)
        {
            GameTablesTests.Open(game);
            Restoring(TouchedFiles(), () =>
            {
                byte[] arm9Before = File.ReadAllBytes(RomInfo.arm9Path);
                var shop = BpShopData.Load();
                shop.Left[0].Price = 7;
                shop.Right[^1].Price = 99;
                shop.Save(RomInfo.GetItemNames().Length);

                var again = BpShopData.Load();
                Assert.Equal(7, again.Left[0].Price);
                Assert.Equal(99, again.Right[^1].Price);
                Assert.Equal(shop.Snapshot(), again.Snapshot());

                // Only the exchange rows (and on Platinum the lists) change in arm9; no pointer moves.
                byte[] arm9After = File.ReadAllBytes(RomInfo.arm9Path);
                var sites = RomInfo.BpShopCodeSites;
                var changed = Enumerable.Range(0, arm9Before.Length).Where(i => arm9Before[i] != arm9After[i]).ToList();
                Assert.NotEmpty(changed);
                Assert.All(changed, i => Assert.InRange(i, sites.ExchangeTable, sites.ExchangeTable + BpShopData.ExchangeRows * 4 - 1));
            });
        }

        [SkippableFact]
        public void DiamondKeepsItsCounterSizes()
        {
            GameTablesTests.Open("Diamond");
            var shop = BpShopData.Load();
            Assert.False(shop.CanResize);
            shop.Left.RemoveAt(shop.Left.Count - 1);
            shop.Right.Add(new BpShopData.Entry { Item = Tm01, Price = 50 });
            Assert.NotNull(shop.Problem(RomInfo.GetItemNames().Length));
            Assert.Throws<InvalidOperationException>(() => shop.Save(RomInfo.GetItemNames().Length));
        }

        [SkippableFact]
        public void PlatinumCountersGrowIntoTheExpansion()
        {
            GameTablesTests.Open("Platinum");
            bool flag = RomPatchState.flag_arm9Expanded;
            Restoring(TouchedFiles(), () =>
            {
                var shop = BpShopData.Load();
                shop.Left.Add(new BpShopData.Entry { Item = 1, Price = 99 });   // Master Ball
                Assert.False(shop.FitsInPlace);
                if (!SyntheticOverlaySpace.Available())
                {
                    var e = Assert.Throws<InvalidOperationException>(() => shop.Save(RomInfo.GetItemNames().Length));
                    Assert.Contains("ARM9 expansion", e.Message);
                    // Stage what the ARM9 expansion leaves behind: its flag and an empty synthetic overlay.
                    RomPatchState.flag_arm9Expanded = true;
                    File.WriteAllBytes(Filesystem.expArmPath, new byte[0x16000]);
                    Assert.True(SyntheticOverlaySpace.Available());
                }
                byte[] synthBefore = File.ReadAllBytes(Filesystem.expArmPath);
                var reservedBefore = SyntheticOverlaySpace.Reserved(synthBefore);
                shop.Save(RomInfo.GetItemNames().Length);

                var again = BpShopData.Load();
                Assert.Equal(27, again.Left.Count);
                Assert.Equal(((ushort)1, (ushort)99), (again.Left[^1].Item, again.Left[^1].Price));
                Assert.Equal((Tm26, (ushort)80), (again.Right[^1].Item, again.Right[^1].Price));
                Assert.Equal("moved to the expanded ARM9 area", again.Where);

                byte[] synthAfter = File.ReadAllBytes(Filesystem.expArmPath);
                var block = Assert.Single(SyntheticOverlaySpace.Blocks(synthAfter, BpShopData.Marker));
                Assert.DoesNotContain(reservedBefore, r => block.Start < r.End && r.Start < block.End);
                var changed = Enumerable.Range(0, synthBefore.Length).Where(i => synthBefore[i] != synthAfter[i]).ToList();
                Assert.All(changed, i => Assert.InRange(i, block.Start, block.End - 1));

                // A second save that still fits reuses the block.
                again.Left[^1].Price = 98;
                again.Save(RomInfo.GetItemNames().Length);
                Assert.Equal(block, Assert.Single(SyntheticOverlaySpace.Blocks(File.ReadAllBytes(Filesystem.expArmPath), BpShopData.Marker)));
                Assert.Equal(98, BpShopData.Load().Left[^1].Price);
            }, () => RomPatchState.flag_arm9Expanded = flag);
        }

        [Fact]
        public void BlockHoldsListsThenPrices()
        {
            var left = new List<BpShopData.Entry> { new() { Item = 46, Price = 1 }, new() { Item = 49, Price = 2 } };
            var right = new List<BpShopData.Entry> { new() { Item = 333, Price = 32 } };
            var rows = left.Concat(right).ToList();
            byte[] block = BpShopData.BuildBlock(left, right, rows, out int l, out int r, out int p);
            Assert.Equal(BpShopData.Marker, System.Text.Encoding.ASCII.GetString(block, 0, 12));
            Assert.Equal((uint)block.Length, BitConverter.ToUInt32(block, 0x10));
            Assert.Equal(new ushort[] { 46, 49, 0xFFFF }, Enumerable.Range(0, 3).Select(i => BitConverter.ToUInt16(block, l + i * 2)));
            Assert.Equal(new ushort[] { 333, 0xFFFF }, Enumerable.Range(0, 2).Select(i => BitConverter.ToUInt16(block, r + i * 2)));
            Assert.Equal(0, p % 4);
            Assert.Equal((ushort)333, BitConverter.ToUInt16(block, p + 8));
            Assert.Equal((ushort)32, BitConverter.ToUInt16(block, p + 10));
        }

        [Fact]
        public void MartExpansionSkipsTheShopBlock()
        {
            byte[] data = new byte[0x400];
            var block = BpShopData.BuildBlock(new() { new() { Item = 46, Price = 1 } }, new() { new() { Item = 333, Price = 32 } },
                new() { new() { Item = 46, Price = 1 }, new() { Item = 333, Price = 32 } }, out _, out _, out _);
            // Zero the middle of the block so only the marker keeps it from reading as free.
            block.CopyTo(data, 0);
            Array.Clear(data, 0x20, block.Length - 0x20);
            var reserved = SyntheticOverlaySpace.Blocks(data);
            Assert.Single(reserved);
            int at = SyntheticOverlaySpace.FindFree(data, 0x10, 4, reserved);
            Assert.True(at >= block.Length);
        }
    }
}
