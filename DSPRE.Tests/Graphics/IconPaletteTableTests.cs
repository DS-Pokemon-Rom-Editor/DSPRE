using System.IO;
using System.Linq;
using DSPRE;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>The party icon palette table address is set when a ROM loads, so it follows the open ROM.</summary>
    [Collection("rom")]
    public class IconPaletteTableTests
    {
        private readonly ITestOutputHelper _out;
        public IconPaletteTableTests(ITestOutputHelper o) => _out = o;

        private static readonly int[] DiamondFirstFifteen = { 1, 1, 1, 0, 0, 0, 0, 2, 2, 1, 1, 0, 1, 2, 2 };

        [SkippableFact]
        public void DiamondReadsItsOwnTableAfterPlatinumWasOpen()
        {
            Skip.IfNot(Directory.Exists(TestRoms.Platinum) && Directory.Exists(TestRoms.Diamond),
                       "Platinum and Diamond are both needed here");

            new RomInfo("CPUE", TestRoms.Platinum);
            uint platinumTable = RomInfo.monIconPalTableAddress;
            var platinum = Enumerable.Range(1, 493).Select(DSUtils.GetMonIconPaletteId).ToArray();

            new RomInfo("ADAE", TestRoms.Diamond);
            uint diamondTable = RomInfo.monIconPalTableAddress;
            var diamond = Enumerable.Range(1, 15).Select(DSUtils.GetMonIconPaletteId).ToArray();

            _out.WriteLine($"Platinum table at 0x{platinumTable:X8}, Diamond table at 0x{diamondTable:X8}");
            _out.WriteLine("Diamond 1-15: " + string.Join(",", diamond));

            Assert.NotEqual(0u, platinumTable);
            Assert.NotEqual(platinumTable, diamondTable);
            Assert.All(platinum, id => Assert.InRange(id, 0, 2));
            Assert.Equal(DiamondFirstFifteen, diamond);
        }
    }
}
