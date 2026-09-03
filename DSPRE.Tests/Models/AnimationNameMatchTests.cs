using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// Working out which movement goes with which model from the names the files carry, for the models
    /// the game keeps no table for.
    /// </summary>
    public class AnimationNameMatchTests
    {
        private readonly ITestOutputHelper _out;
        public AnimationNameMatchTests(ITestOutputHelper o) { _out = o; }

        private static readonly string Unpacked = TestRoms.HeartGold + @"\unpacked";

        // ── the rule itself ───────────────────────────────────────────────────────────────────────

        [Fact]
        public void TheSameNameIsTheBestMatchAndAStartSharedIsALesserOne()
        {
            Assert.Equal(2, ModelAssets.NameMatch("en_fs", "en_fs"));
            Assert.Equal(2, ModelAssets.NameMatch("en_fs", "EN_FS"));       // case is not the point
            Assert.Equal(2, ModelAssets.NameMatch(" en_fs ", "en_fs"));     // nor is stray space
            Assert.Equal(1, ModelAssets.NameMatch("gym01", "gym01_lift"));
            Assert.Equal(1, ModelAssets.NameMatch("wk_door1", "wk_door1_open"));
        }

        [Fact]
        public void NamesThatOnlyAgreeOnTheirFirstFewLettersDoNotCount()
        {
            // Nearly every building in these games is called en_something, so agreeing on that says
            // nothing at all and must not be read as a match.
            Assert.Equal(0, ModelAssets.NameMatch("en_fs", "en_pc"));
            Assert.Equal(0, ModelAssets.NameMatch("en_gate01", "en_gym"));
            Assert.Equal(0, ModelAssets.NameMatch("wk_h01", "wk_h02"));
            Assert.Equal(0, ModelAssets.NameMatch("en_", "en_fs"));   // too short a shared start
        }

        [Fact]
        public void NothingToGoOnMeansNoMatch()
        {
            Assert.Equal(0, ModelAssets.NameMatch(null, "en_fs"));
            Assert.Equal(0, ModelAssets.NameMatch("en_fs", null));
            Assert.Equal(0, ModelAssets.NameMatch("", ""));
            Assert.Equal(0, ModelAssets.NameMatch("   ", "en_fs"));
        }

        // ── against the ROM's own names ───────────────────────────────────────────────────────────

        [Fact]
        public void AcrossEveryModelAndMovementInTheRomTheMatchesAreFewAndEachModelGetsAtMostAHandful()
        {
            if (!Directory.Exists(Unpacked))
            { Assert.Fail($"{Unpacked} is not there, so this proved nothing."); return; }

            var models = Directory.GetFiles(Path.Combine(Unpacked, "exteriorBuildingModels")).OrderBy(x => x)
                .Select(f => (id: int.Parse(Path.GetFileName(f)),
                              name: ModelAssets.NameInFile(File.ReadAllBytes(f)))).ToList();

            var moves = new List<(int id, string name)>();
            foreach (string f in Directory.GetFiles(Path.Combine(Unpacked, "buildingAnimations")).OrderBy(x => x))
            {
                JointAnimation a = null;
                try { a = JointAnimation.Load(File.ReadAllBytes(f)); } catch { }
                moves.Add((int.Parse(Path.GetFileName(f)), a?.Name));
            }

            Assert.True(models.Count > 300, $"only {models.Count} models were read");
            Assert.True(moves.Count > 200, $"only {moves.Count} movements were read");

            int modelsWithAMatch = 0, mostForOneModel = 0, pairs = 0;
            string busiest = null;
            var notAPrefix = new List<string>();
            foreach (var m in models)
            {
                int here = 0;
                foreach (var v in moves)
                {
                    if (ModelAssets.NameMatch(m.name, v.name) == 0) continue;
                    here++;
                    // Every pair this claims has to be one name inside the other. Without that the rule
                    // is just "these two names look a bit alike", which pairs up half the game.
                    if (!v.name.StartsWith(m.name, StringComparison.OrdinalIgnoreCase)
                        && !m.name.StartsWith(v.name, StringComparison.OrdinalIgnoreCase))
                        notAPrefix.Add($"model {m.name} was matched to movement {v.name}");
                }
                if (here == 0) continue;
                modelsWithAMatch++;
                pairs += here;
                if (here > mostForOneModel) { mostForOneModel = here; busiest = m.name; }
            }

            _out.WriteLine($"{models.Count} models and {moves.Count} movements, "
                         + $"{moves.Count(v => v.name != null)} of the movements named.");
            _out.WriteLine($"{modelsWithAMatch} models are matched to something by name, {pairs} pairs "
                         + $"in all. The most any one model matches is {mostForOneModel} ({busiest}).");

            Assert.True(modelsWithAMatch > 0, "no model matched any movement by name, so this does nothing");
            Assert.True(notAPrefix.Count == 0, string.Join(Environment.NewLine, notAPrefix.Take(8)));

            // A rule that pairs up most of the game is no better than the flat list it replaces. The
            // right rule pairs 20 of 340 models; three times that would mean it had gone loose.
            Assert.True(modelsWithAMatch < 60,
                $"{modelsWithAMatch} of {models.Count} models matched something by name, which is far more "
                + "than the names in this ROM support and means the rule has gone loose");
            Assert.True(mostForOneModel <= 6,
                $"one model matched {mostForOneModel} movements by name, which is no better than showing "
                + "the whole list");
        }
    }
}
