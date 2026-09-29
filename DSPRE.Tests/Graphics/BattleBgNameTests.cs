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
            ("ADAE", TestRoms.Diamond, "Diamond"),
            ("CPUE", TestRoms.Platinum, "Platinum"),
            ("IPKE", TestRoms.HeartGold, "HeartGold"),
        };

        private static string PackedFor(RomInfo.GameFamilies family) => family switch
        {
            RomInfo.GameFamilies.HGSS => BattleBgNames.Johto,
            RomInfo.GameFamilies.Plat => BattleBgNames.Platinum,
            _ => BattleBgNames.DiamondPearl,
        };

        /// <summary>A name list one entry out of step mislabels everything after the gap, silently.</summary>
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
        /// The touch screen's battle menus are built from these. They sit at
        /// different numbers in each game, which is the whole reason for looking them up by name.
        /// </summary>
        [Fact]
        public void TheTouchScreenPanelPiecesAreNamedInEveryGame()
        {
            string[] wanted =
            {
                "TouchScreen.Layer.Unused:Drawing", "TouchScreen.Layer.Background:Screen",
                "TouchScreen.Layer.CommandButtons:Screen", "TouchScreen.Layer.MoveButtons:Screen",
                "TouchScreen.Layer.TargetButtons:Screen", "TouchScreen.Layer.YesNoButtons:Screen",
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
        /// The count matching is not enough: a list in the wrong order passes it. BattleBgRenderer's tables
        /// were checked against the games, so if the names at those numbers read as one thing's tiles,
        /// colours and screen, the list is in step.
        /// </summary>
        [Fact]
        public void TheNamesLineUpWithFilesAlreadyKnownToBeRight()
        {
            var checks = new (string code, string path, string name, int chr, int pal, int scr)[]
            {
                ("ADAE", TestRoms.Diamond, "Diamond", 53, 208, 50),
                ("CPUE", TestRoms.Platinum, "Platinum", 65, 291, 62),
                ("IPKE", TestRoms.HeartGold, "HeartGold", 59, 295, 56),
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
                Assert.EndsWith(":Drawing", names[chr], StringComparison.Ordinal);
                Assert.EndsWith(":Colours", names[pal], StringComparison.Ordinal);
                Assert.EndsWith(":Screen", names[scr], StringComparison.Ordinal);
                string stem = names[chr].Split(':')[0];
                Assert.Equal(stem, names[pal].Split(':')[0]);
                Assert.StartsWith(stem, names[scr].Split(':')[0], StringComparison.Ordinal);
                _out.WriteLine($"{name}: {chr}/{pal}/{scr} all name {stem}");
            }
            Assert.True(checkedGames > 0, "no game was unpacked here, so nothing was checked");
        }

        /// <summary>Every backdrop's drawing and daytime colours are the files named for that backdrop.</summary>
        [Fact]
        public void EveryBackdropReadsItsOwnDrawingAndColours()
        {
            int checkedGames = 0;
            foreach (var (code, path, name) in Games)
            {
                if (!Directory.Exists(path)) { _out.WriteLine($"{name}: not unpacked here, skipped"); continue; }
                try { new RomInfo(code, path); } catch { continue; }
                checkedGames++;

                var names = BattleBgNames.Names();
                for (int bg = 0; bg < BattleBgRenderer.BackdropCount; bg++)
                {
                    var files = BattleBgRenderer.BackdropFiles(bg);
                    Assert.True(files.Drawing < names.Length && files.PaletteDay < names.Length,
                                $"{name}: backdrop {bg} points past the name list");
                    Assert.True(names[files.Drawing] == $"Backdrop.{bg}:Drawing",
                                $"{name}: backdrop {bg}'s drawing is {names[files.Drawing]}");
                    Assert.True(names[files.PaletteDay] == $"Backdrop.{bg}.Day:Colours",
                                $"{name}: backdrop {bg}'s day colours are {names[files.PaletteDay]}");
                }
                _out.WriteLine($"{name}: {BattleBgRenderer.BackdropCount} backdrops read their own files");
            }
            Assert.True(checkedGames > 0, "no game was unpacked here, so nothing was checked");
        }
    }
}
