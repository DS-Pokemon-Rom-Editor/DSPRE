using System;
using System.Collections.Generic;
using System.IO;
using DSPRE;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// The overlay table belongs to one ROM. It used to be read once and kept for the rest of the
    /// session, so opening a second ROM left every overlay address and size pointing at the first one.
    /// On HeartGold that made the overworld sprite table read from the wrong offset and come back
    /// empty, which takes every person out of the Event Editor and the animated preview.
    /// </summary>
    [Collection("rom")]
    public class RomSwitchOverlayTableTests
    {
        private readonly ITestOutputHelper _out;
        public RomSwitchOverlayTableTests(ITestOutputHelper o) => _out = o;

        private static readonly string Platinum = TestRoms.Platinum;
        private static readonly string HeartGold = TestRoms.HeartGold;

        [Fact]
        public void OpeningASecondRomReadsThatRomsOverlays()
        {
            if (!Directory.Exists(Platinum) || !Directory.Exists(HeartGold))
            { Assert.Fail("both projects are needed here, and one is missing, so this would prove nothing"); }

            SettingsManager.Load();

            new RomInfo("CPUE", Platinum);
            uint plat = OverlayUtils.OverlayTable.GetRAMAddress(1);

            new RomInfo("IPKE", HeartGold);
            uint hg = OverlayUtils.OverlayTable.GetRAMAddress(1);

            _out.WriteLine($"overlay 1 loads at 0x{plat:X} in Platinum and 0x{hg:X} in HeartGold");
            Assert.NotEqual(plat, hg);

            // The symptom that gave this away: the table the overworld sprites come from.
            RomInfo.Set3DOverworldsDict();
            RomInfo.SetOWtable();
            RomInfo.ReadOWTable();
            _out.WriteLine($"HeartGold's overworld table holds {RomInfo.OverworldTable.Count} entries");
            Assert.True(RomInfo.OverworldTable.Count > 100,
                $"the overworld table came back with {RomInfo.OverworldTable.Count} entries, so nobody would be drawn");
        }
    }
}
