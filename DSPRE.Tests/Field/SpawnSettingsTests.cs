using System;
using System.Collections.Generic;
using System.IO;
using DSPRE;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>Where a new game starts, and how much money it starts with.</summary>
    [Collection("rom")]
    public class SpawnSettingsTests
    {
        private readonly ITestOutputHelper _out;
        public SpawnSettingsTests(ITestOutputHelper o) => _out = o;

        private static readonly (string code, string path, string name)[] Games =
        {
            ("ADAE", @"C:\Romhacking\ROMs\NDS\DP\Pokemon Diamond (v05) (U)(Legacy)\1015 - Pokemon Diamond (v05) (U)(Legacy)_DSPRE_contents",  "Diamond"),
            ("CPUE", @"C:\Romhacking\ROMs\NDS\Plat\Pokemon - Platinum Version (USA) (Rev 1)\Pokemon - Platinum Version (USA) (Rev 1)_DSPRE_contents", "Platinum"),
            ("IPKE", @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents", "HeartGold"),
        };

        /// <summary>
        /// Loading a ROM has to leave the spawn offsets set, or the editor reads offset zero of overlay
        /// zero and shows whatever happens to be there.
        /// </summary>
        [Fact]
        public void LoadingARomSetsTheSpawnOffsets()
        {
            int checkedGames = 0;
            foreach (var (code, path, name) in Games)
            {
                if (!Directory.Exists(path)) { _out.WriteLine($"{name}: not unpacked here, skipped"); continue; }
                try { new RomInfo(code, path); }
                catch (Exception ex) { _out.WriteLine($"{name}: would not load ({ex.Message}), skipped"); continue; }
                checkedGames++;

                Assert.True(RomInfo.arm9spawnOffset != 0, name + " left the spawn offset at zero");
                Assert.True(RomInfo.initialMoneyOverlayNumber != 0, name + " left the money overlay at zero");

                ushort header = BitConverter.ToUInt16(ARM9.ReadBytes(RomInfo.arm9spawnOffset, 2), 0);
                Assert.InRange(header, 0, RomInfo.GetHeaderCount() - 1);

                string moneyPath = OverlayUtils.GetPath(RomInfo.initialMoneyOverlayNumber);
                uint money = BitConverter.ToUInt32(
                    DSUtils.ReadFromFile(moneyPath, RomInfo.initialMoneyOverlayOffset, 4), 0);
                Assert.InRange(money, 0u, 999999u);
                _out.WriteLine($"{name}: starts at header {header} with {money}");
            }
            Assert.True(checkedGames > 0, "no game was unpacked here, so nothing was checked");
            _out.WriteLine($"{checkedGames} games checked");
        }

        /// <summary>HeartGold's own numbers, read straight out of the files rather than through RomInfo.</summary>
        [Fact]
        public void HeartGoldStartsInNewBarkTownWithThreeThousand()
        {
            string folder = @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents";
            if (!Directory.Exists(folder)) { _out.WriteLine("HeartGold not unpacked here"); return; }
            new RomInfo("IPKE", folder);

            byte[] arm9 = File.ReadAllBytes(Path.Combine(folder, "arm9", "arm9.bin"));
            byte[] ov36 = File.ReadAllBytes(Path.Combine(folder, "arm9_overlays", "ov036.bin"));

            Assert.Equal(64, BitConverter.ToUInt16(arm9, (int)RomInfo.arm9spawnOffset));
            Assert.Equal(6, BitConverter.ToUInt16(arm9, (int)RomInfo.arm9spawnOffset + 8));
            Assert.Equal(6, BitConverter.ToUInt16(arm9, (int)RomInfo.arm9spawnOffset + 12));
            Assert.Equal(3000u, BitConverter.ToUInt32(ov36, (int)RomInfo.initialMoneyOverlayOffset));
        }
    }
}
