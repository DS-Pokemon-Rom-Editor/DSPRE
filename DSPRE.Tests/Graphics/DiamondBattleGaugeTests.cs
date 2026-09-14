using System.Collections.Generic;
using System.IO;
using DSPRE;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>Diamond and Pearl's player bar is laid out a tile differently from Platinum's.</summary>
    [Collection("rom")]
    public class DiamondBattleGaugeTests
    {
        private readonly ITestOutputHelper _out;
        public DiamondBattleGaugeTests(ITestOutputHelper o) => _out = o;

        [SkippableFact]
        public void YourBarKeepsItsHpLabelAndSitsWhereDiamondPutsIt()
        {
            Skip.If(!Directory.Exists(TestRoms.Diamond), "Diamond not unpacked here");
            SettingsManager.Load();
            new RomInfo("ADAE", TestRoms.Diamond);
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.battleObj, RomInfo.DirNames.fonts });
            BattleGaugeText.Reset();
            Assert.True(BattleGaugeText.IsAvailable, BattleGaugeText.Unavailable);

            var written = BattleGaugeComposer.Build(BattleGaugeComposer.Kind.PlayerSingle, new BattleGaugeComposer.Showing
                { Name = "BULBASAUR", Level = 5, Gender = BattleGaugeText.Gender.Male, ShowHealthNumbers = true });
            var plain = new BattleGroundRenderer().BuildGauge(true);
            Assert.NotNull(written);
            Assert.NotNull(plain);

            Assert.Equal(198 - written.Width / 2, written.Left);
            Assert.Equal(written.Left, plain.Left);

            // The "HP" label area, which no writing may touch.
            int changed = 0, opaque = 0;
            for (int y = 114; y < 124; y++)
                for (int x = 176; x < 200; x++)
                {
                    int at = ((y - written.Top) * written.Width + (x - written.Left)) * 4;
                    if (plain.Rgba[at + 3] > 0) opaque++;
                    if (written.Rgba[at] != plain.Rgba[at] || written.Rgba[at + 1] != plain.Rgba[at + 1]
                        || written.Rgba[at + 2] != plain.Rgba[at + 2]) changed++;
                }
            _out.WriteLine($"{changed} of {opaque} label pixels changed");
            Assert.True(opaque > 100, "the label area was not on the bar");
            Assert.Equal(0, changed);
        }
    }
}
