using System;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>The names the games give the files in their battle background archive.</summary>
    [Collection("rom")]
    public class BattleBgNameTests
    {
        private readonly ITestOutputHelper _out;
        public BattleBgNameTests(ITestOutputHelper o) => _out = o;

        private static readonly (string code, string path, string name)[] Games =
        {
            ("ADAE", @"C:\Romhacking\ROMs\NDS\DP\Pokemon Diamond (v05) (U)(Legacy)\1015 - Pokemon Diamond (v05) (U)(Legacy)_DSPRE_contents", "Diamond"),
            ("CPUE", @"C:\Romhacking\ROMs\NDS\Plat\Pokemon - Platinum Version (USA) (Rev 1)\Pokemon - Platinum Version (USA) (Rev 1)_DSPRE_contents", "Platinum"),
            ("IPKE", @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents", "HeartGold"),
        };

        private static string PackedFor(RomInfo.GameFamilies family) => family switch
        {
            RomInfo.GameFamilies.HGSS => BattleBgNames.Johto,
            RomInfo.GameFamilies.Plat => BattleBgNames.Platinum,
            _ => BattleBgNames.DiamondPearl,
        };

        /// <summary>
        /// A name list one entry out of step mislabels everything after the gap, silently. Platinum's
        /// index list carries Diamond's 257 entries while Platinum's archive holds 342, so
        /// the count is the check that catches using the wrong source.
        /// </summary>
        [Fact]
        public void TheNameListIsExactlyAsLongAsTheArchive()
        {
            int checkedGames = 0;
            foreach (var (code, path, name) in Games)
            {
                if (!Directory.Exists(path)) { _out.WriteLine($"{name}: not unpacked here, skipped"); continue; }
                try { new RomInfo(code, path); }
                catch (Exception ex) { _out.WriteLine($"{name}: would not load ({ex.Message}), skipped"); continue; }
                DSUtils.TryUnpackNarcs(new System.Collections.Generic.List<RomInfo.DirNames> { RomInfo.DirNames.battleBg });
                checkedGames++;

                var files = RomFiles.Settled(RomInfo.gameDirs[RomInfo.DirNames.battleBg].unpackedDir);
                var names = PackedFor(RomInfo.gameFamily).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                Assert.Equal(files.Length, names.Length);
                _out.WriteLine($"{name}: {files.Length} files, {names.Length} names");
            }
            Assert.True(checkedGames > 0, "no game was unpacked here, so nothing was checked");
            _out.WriteLine($"{checkedGames} games checked");
        }

        /// <summary>
        /// The touch screen's command panel is built from these, by battle_input.c:204-212. They sit at
        /// different numbers in each game, which is the whole reason for looking them up by name.
        /// </summary>
        [Fact]
        public void TheTouchScreenPanelPiecesAreNamedInEveryGame()
        {
            string[] wanted =
            {
                "BATTLE_WBG0A_NCGR_BIN", "BATTLE_WBG0B_NSCR_BIN", "BATTLE_WBG1A_NSCR_BIN",
                "BATTLE_WBG1B_NSCR_BIN", "BATTLE_WBG1C_NSCR_BIN", "BATTLE_WBG1D_NSCR_BIN",
            };
            int checkedGames = 0;
            foreach (var (code, path, name) in Games)
            {
                if (!Directory.Exists(path)) { _out.WriteLine($"{name}: not unpacked here, skipped"); continue; }
                try { new RomInfo(code, path); } catch { continue; }
                checkedGames++;

                var names = PackedFor(RomInfo.gameFamily).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var at = wanted.Select(w => Array.IndexOf(names, w)).ToArray();
                Assert.All(at, i => Assert.True(i >= 0, name + " is missing one of the panel pieces"));
                _out.WriteLine($"{name}: panel pieces at {string.Join(", ", at)}");
            }
            Assert.True(checkedGames > 0, "no game was unpacked here, so nothing was checked");
        }
        /// <summary>
        /// The count matching is not enough. Platinum's build list holds exactly 342 entries, the same
        /// as its archive, but in a different order, and every name was wrong while the count test
        /// passed. BattleBgRenderer's tables were checked against the games, so if the names at those
        /// numbers read as one thing's tiles, colours and screen, the list is in step.
        /// </summary>
        [Fact]
        public void TheNamesLineUpWithFilesAlreadyKnownToBeRight()
        {
            var checks = new (string code, string path, string name, int chr, int pal, int scr)[]
            {
                ("CPUE", @"C:\Romhacking\ROMs\NDS\Plat\Pokemon - Platinum Version (USA) (Rev 1)\Pokemon - Platinum Version (USA) (Rev 1)_DSPRE_contents", "Platinum", 65, 291, 62),
                ("IPKE", @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents", "HeartGold", 59, 295, 56),
            };
            int checkedGames = 0;
            foreach (var (code, path, name, chr, pal, scr) in checks)
            {
                if (!Directory.Exists(path)) { _out.WriteLine($"{name}: not unpacked here, skipped"); continue; }
                try { new RomInfo(code, path); } catch { continue; }
                checkedGames++;

                var names = PackedFor(RomInfo.gameFamily).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                Assert.True(chr < names.Length && pal < names.Length && scr < names.Length,
                            name + ": the list is shorter than the numbers being checked");

                // One backdrop's three files must name one thing, each of its own kind.
                Assert.Contains("_NCGR", names[chr], StringComparison.Ordinal);
                Assert.Contains("_NCLR", names[pal], StringComparison.Ordinal);
                Assert.Contains("_NSCR", names[scr], StringComparison.Ordinal);
                string stem = names[chr].Split("_NCGR")[0];
                Assert.StartsWith(stem, names[pal], StringComparison.Ordinal);
                Assert.StartsWith(stem, names[scr], StringComparison.Ordinal);
                _out.WriteLine($"{name}: {chr}/{pal}/{scr} all name {stem}");
            }
            Assert.True(checkedGames > 0, "no game was unpacked here, so nothing was checked");
        }
    }
}
