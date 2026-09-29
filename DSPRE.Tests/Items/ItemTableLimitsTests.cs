using System;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests.Items
{
    /// <summary>The ARM9 item table's length on US Diamond v05, Platinum Rev 1 and HeartGold, read-only.
    /// Retail row counts: pokediamond sItemIndexMappings, pokeplatinum MAX_ITEMS, pokeheartgold ITEMS_COUNT.</summary>
    [Collection("rom")]
    public class ItemTableLimitsTests
    {
        private static void AssertRows(string game, int rows)
        {
            DSPRE.Tests.Pokemon.GameTablesTests.Open(game);
            ItemTable.Forget();

            var limits = ItemTable.Limits();

            Assert.Equal(rows - 1, limits.CodeLimit);
            Assert.Equal(-1, limits.FirstBadRow);
            Assert.Equal(rows, limits.Count);
            Assert.Equal(rows, ItemTable.VanillaCount);
            ItemTable.Read(rows - 1);
            if (RomInfo.gameFamily != RomInfo.GameFamilies.Plat || PlatPatches.Items() == null)
                Assert.Throws<InvalidOperationException>(() => ItemTable.Read(rows));
        }

        [SkippableFact]
        public void DiamondHas465Rows() => AssertRows("Diamond", 465);

        [SkippableFact]
        public void PlatinumHas468Rows() => AssertRows("Platinum", 468);

        [SkippableFact]
        public void HeartGoldHas537Rows() => AssertRows("HeartGold", 537);
    }
}
