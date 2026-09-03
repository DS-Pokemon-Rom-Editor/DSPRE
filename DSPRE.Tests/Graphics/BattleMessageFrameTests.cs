using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>The frames the battle screen draws its windows from.</summary>
    [Collection("rom")]
    public class BattleMessageFrameTests
    {
        private readonly ITestOutputHelper _out;
        public BattleMessageFrameTests(ITestOutputHelper o) => _out = o;

        private static readonly (string code, string path, string name)[] Games =
        {
            ("ADAE", TestRoms.Diamond, "Diamond"),
            ("CPUE", TestRoms.Platinum, "Platinum"),
            ("IPKE", TestRoms.HeartGold, "HeartGold"),
        };

        /// <summary>
        /// The frames carry no colours of their own, so without a rule for them they draw in whatever
        /// palette happens to be reached for. battle_input.c:2760 in HeartGold and :2623 in Platinum load
        /// BATTLE_WOBJ_NCLR for the screen the frames are on.
        /// </summary>
        [Fact]
        public void EveryMessageFrameIsPairedWithTheColoursTheGameLoads()
        {
            int checkedGames = 0, checkedFrames = 0;
            foreach (var (code, path, name) in Games)
            {
                if (!Directory.Exists(path)) { _out.WriteLine($"{name}: not unpacked here, skipped"); continue; }
                try { new RomInfo(code, path); }
                catch (Exception ex) { _out.WriteLine($"{name}: would not load ({ex.Message}), skipped"); continue; }
                DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.battleObj });
                checkedGames++;

                var names = BattleObjects.Names();
                int wobj = -1;
                for (int i = 0; i < names.Count; i++)
                    if (names[i] == "BATTLE_WOBJ_NCLR") { wobj = i; break; }
                Assert.True(wobj >= 0, name + " has no BATTLE_WOBJ_NCLR");

                int frames = 0;
                for (int i = 0; i < names.Count; i++)
                {
                    if (names[i] == null || !names[i].StartsWith("BATTLE_W_WAKU", StringComparison.Ordinal)) continue;
                    if (names[i].EndsWith("_NCLR", StringComparison.Ordinal)) continue;
                    frames++;
                    Assert.Equal(wobj, BattleObjects.ColoursFor(i));
                }
                Assert.True(frames >= 9, $"{name}: expected the three frames' nine files, found {frames}");
                checkedFrames += frames;
                _out.WriteLine($"{name}: {frames} frame files, all pointed at colours {wobj}");
            }
            Assert.True(checkedGames > 0, "no game was unpacked here, so nothing was checked");
            _out.WriteLine($"{checkedGames} games, {checkedFrames} frame files checked");
        }

        /// <summary>
        /// A name must not end with a word it has already used. The part is added on the end, so a
        /// thing whose own name already says "colours" came out as "Message frame colours, colours".
        /// "Two on two" repeats a word on purpose and is not this fault, which is why only the last
        /// piece is checked against what came before it.
        /// </summary>
        [Fact]
        public void NoBattleObjectSaysTheSameWordTwice()
        {
            string folder = TestRoms.HeartGold;
            if (!Directory.Exists(folder)) { _out.WriteLine("HeartGold not unpacked here"); return; }
            new RomInfo("IPKE", folder);
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.battleObj });

            var doubled = new List<string>();
            int looked = 0;
            var names = BattleObjects.Names();
            for (int i = 0; i < names.Count; i++)
            {
                string shown = BattleObjects.NameOf(i);
                if (string.IsNullOrEmpty(shown)) continue;
                looked++;
                var pieces = shown.ToLowerInvariant().Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (pieces.Length < 2) continue;
                string last = pieces[^1].Trim();
                bool saidBefore = pieces[..^1].Any(seg => seg
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Any(w => w == last));
                if (saidBefore) doubled.Add($"{i}: {shown}");
            }
            Assert.True(looked > 0, "no names were read, so nothing was checked");
            Assert.True(doubled.Count == 0,
                $"{doubled.Count} of {looked} names end with a word they already used: {string.Join(" | ", doubled.Take(6))}");
            _out.WriteLine($"{looked} names read, none end with a word they already used");
        }
        /// <summary>
        /// The gauge files are found by name, not by number. The numbers differ per game: Diamond keeps
        /// SINGLE_GAGE1 at 123/124 where Platinum and HeartGold keep it at 187/188, so a constant is right
        /// for two games and wrong for the third.
        /// </summary>
        [Fact]
        public void TheGaugeGraphicsAreFoundInEveryGame()
        {
            int checkedGames = 0;
            foreach (var (code, path, name) in Games)
            {
                if (!Directory.Exists(path)) { _out.WriteLine($"{name}: not unpacked here, skipped"); continue; }
                try { new RomInfo(code, path); }
                catch (Exception ex) { _out.WriteLine($"{name}: would not load ({ex.Message}), skipped"); continue; }
                DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.battleObj });
                checkedGames++;

                var r = new BattleGroundRenderer();
                if (!r.Available) { _out.WriteLine($"{name}: no battle furniture archive, skipped"); continue; }

                foreach (bool player in new[] { true, false })
                {
                    var g = r.BuildGauge(player);
                    Assert.True(g?.Rgba != null, $"{name}: the {(player ? "player" : "enemy")} gauge did not draw");
                    Assert.True(g.Rgba.Any(b => b != 0), $"{name}: the {(player ? "player" : "enemy")} gauge came out blank");
                }
                _out.WriteLine($"{name}: both gauges drew, from files "
                    + $"{BattleObjects.Find("SINGLE_GAGE2", "Drawing")} and {BattleObjects.Find("SINGLE_GAGE1", "Drawing")}");
            }
            Assert.True(checkedGames > 0, "no game was unpacked here, so nothing was checked");
            _out.WriteLine($"{checkedGames} games checked");
        }

    }
}
