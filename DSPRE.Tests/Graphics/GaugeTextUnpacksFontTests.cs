using System.IO;
using DSPRE;
using DSPRE.ROMFiles;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>Gauge text is read even when the font archive was never unpacked.</summary>
    [Collection("rom")]
    public class GaugeTextUnpacksFontTests
    {
        private readonly ITestOutputHelper _out;
        public GaugeTextUnpacksFontTests(ITestOutputHelper o) => _out = o;

        [SkippableFact]
        public void GaugeTextIsReadWhenTheFontArchiveWasNeverUnpacked()
        {
            Skip.If(!Directory.Exists(TestRoms.Diamond), "Diamond not unpacked here");
            SettingsManager.Load();
            new RomInfo("ADAE", TestRoms.Diamond);
            string dir = RomInfo.gameDirs[RomInfo.DirNames.fonts].unpackedDir;
            string aside = dir + ".testaside";
            bool existed = Directory.Exists(dir);
            if (existed) Directory.Move(dir, aside);
            try
            {
                BattleGaugeText.Reset();
                bool available = BattleGaugeText.IsAvailable;
                _out.WriteLine(available ? "gauge text read" : "not read: " + BattleGaugeText.Unavailable);
                Assert.True(available, BattleGaugeText.Unavailable);
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
                if (existed) Directory.Move(aside, dir);
                BattleGaugeText.Reset();
            }
        }
    }
}
