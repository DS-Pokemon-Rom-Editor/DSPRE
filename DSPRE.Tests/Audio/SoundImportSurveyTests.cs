using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// What the sound archive actually holds, so a decision about what can be put back in is made against
    /// the whole archive rather than the cries alone.
    /// </summary>
    [Collection("rom")]
    public class SoundImportSurveyTests
    {
        private readonly ITestOutputHelper _out;
        public SoundImportSurveyTests(ITestOutputHelper o) { _out = o; }

        public static IEnumerable<object[]> Games => new List<object[]>
        {
            new object[] { "IPKE", TestRoms.HeartGold, "HeartGold" },
            new object[] { "CPUE", TestRoms.Platinum, "Platinum" },
        };

        /// <summary>
        /// Every tab holds what the game itself plays on that tab's player, in both games.
        /// </summary>
        [Theory]
        [MemberData(nameof(Games))]
        public void EachTabHoldsWhatThatPlayerPlays(string code, string path, string game)
        {
            if (!Directory.Exists(path)) { _out.WriteLine($"{game}: not unpacked here"); return; }
            new RomInfo(code, path);
            SoundArchive.Reset();
            var sdat = SoundArchive.Load();
            if (sdat == null) { _out.WriteLine($"{game}: no sound archive"); return; }

            int Player(int seq) => seq >= 0 && seq < sdat.Sequences.Count && sdat.Sequences[seq] != null
                ? sdat.Sequences[seq].PlayerNo : -1;

            var vm = new DSPRE.Avalonia.ViewModels.Audio.AudioEditorViewModel(null);

            // PLAYER_ME is 2 in both games, from snd_system.c and both .sadl files.
            int wantFanfares = sdat.SeqNames.Count(k => Player(k.Key) == 2
                                                     && k.Value != SoundArchive.CrySequenceName);
            int wantMusic = sdat.SeqNames.Count(k => (Player(k.Key) == 1 || Player(k.Key) == 7)
                                                  && k.Value != SoundArchive.CrySequenceName);
            int wantEffects = sdat.SeqNames.Count(k => Player(k.Key) >= 3 && Player(k.Key) <= 6
                                                    && k.Value != SoundArchive.CrySequenceName);

            _out.WriteLine($"{game}: fanfares {vm.Fanfares.Count}/{wantFanfares}, "
                         + $"music {vm.Music.Count}/{wantMusic}, effects {vm.Effects.Count}/{wantEffects}, "
                         + $"sounds {vm.Sounds.Count}");

            Assert.Equal(wantFanfares, vm.Fanfares.Count);
            Assert.Equal(wantMusic, vm.Music.Count);

            // The cry player's leftovers go in with the effects, so that tab is the others plus those.
            Assert.True(vm.Effects.Count >= wantEffects,
                $"{game}: {vm.Effects.Count} effects listed but {wantEffects} sit on an effect player");

            // No tab may be empty without a reason, and every game has fanfares.
            Assert.True(vm.Fanfares.Count > 0, $"{game}: no fanfares listed at all");
            Assert.True(vm.Sounds.Count > 0, $"{game}: no sounds listed at all");
        }

        /// <summary>The check above proves able to fail: splitting on SEQ_ME_ alone must not satisfy it.</summary>
        [Fact]
        public void TheTabCheckWouldCatchTheOldNameSplit()
        {
            string Platinum = TestRoms.Platinum;
            if (!Directory.Exists(Platinum)) { _out.WriteLine("Platinum not unpacked here"); return; }
            new RomInfo("CPUE", Platinum);
            SoundArchive.Reset();
            var sdat = SoundArchive.Load();
            if (sdat == null) { _out.WriteLine("no sound archive"); return; }

            int byName = sdat.SeqNames.Count(k => (k.Value ?? "").StartsWith("SEQ_ME_"));
            int byPlayer = sdat.SeqNames.Count(k => k.Key < sdat.Sequences.Count
                                                 && sdat.Sequences[k.Key] != null
                                                 && sdat.Sequences[k.Key].PlayerNo == 2);
            _out.WriteLine($"Platinum: SEQ_ME_ names {byName}, sequences on the fanfare player {byPlayer}");
            Assert.Equal(0, byName);
            Assert.True(byPlayer > 0, "Platinum has no fanfares by either measure, so the test proves nothing");
        }
    }
}
