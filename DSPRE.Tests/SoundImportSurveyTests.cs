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
            new object[] { "IPKE", @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents", "HeartGold" },
            new object[] { "CPUE", @"C:\Romhacking\ROMs\NDS\Plat\Pokemon - Platinum Version (USA) (Rev 1)\Pokemon - Platinum Version (USA) (Rev 1)_DSPRE_contents", "Platinum" },
        };

        /// <summary>
        /// Every tab holds what the game itself plays on that tab's player, in both games.
        ///
        /// The split used to be on name prefixes, which worked in HeartGold and failed in Platinum: nothing
        /// there is named SEQ_ME_, so the Fanfares tab was empty and twenty of them were unreachable. The
        /// player numbers come from the archive's own records and agree with the names exactly where the
        /// names work, so this asserts against the records rather than against the code's own idea.
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

            var vm = new DSPRE.Avalonia.ViewModels.AudioEditorViewModel(null);

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
            const string Platinum = @"C:\Romhacking\ROMs\NDS\Plat\Pokemon - Platinum Version (USA) (Rev 1)\Pokemon - Platinum Version (USA) (Rev 1)_DSPRE_contents";
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

        /// <summary>
        /// How the sequences split by the player the game hands them to, against how they split by name.
        ///
        /// snd_system.c and both .sadl files agree on the numbering in HeartGold and Platinum:
        /// PLAYER_PV 0, PLAYER_FIELD 1, PLAYER_ME 2, PLAYER_SE_1 to _4 3 to 6, PLAYER_BGM 7.
        /// </summary>
        [Theory]
        [MemberData(nameof(Games))]
        public void ReportPlayerSplitAgainstNameSplit(string code, string path, string game)
        {
            if (!Directory.Exists(path)) { _out.WriteLine($"{game}: not unpacked here"); return; }
            new RomInfo(code, path);
            SoundArchive.Reset();
            var sdat = SoundArchive.Load();
            if (sdat == null) { _out.WriteLine($"{game}: no sound archive"); return; }

            var byPlayer = new Dictionary<int, int>();
            int named = 0;
            for (int i = 0; i < sdat.Sequences.Count; i++)
            {
                var info = sdat.Sequences[i];
                if (info == null) continue;
                if (!sdat.SeqNames.TryGetValue(i, out var n) || string.IsNullOrEmpty(n)) continue;
                named++;
                byPlayer[info.PlayerNo] = byPlayer.TryGetValue(info.PlayerNo, out int c) ? c + 1 : 1;
            }

            int byNameMe = sdat.SeqNames.Count(k => (k.Value ?? "").StartsWith("SEQ_ME_"));
            int byNameSe = sdat.SeqNames.Count(k => (k.Value ?? "").StartsWith("SEQ_SE_"));

            _out.WriteLine($"{game}: {named} named sequences with a record");
            foreach (var kv in byPlayer.OrderBy(k => k.Key))
                _out.WriteLine($"   player {kv.Key}: {kv.Value}");
            _out.WriteLine($"   by name: SEQ_ME_ {byNameMe}, SEQ_SE_ {byNameSe}");
        }

        /// <summary>What the sequence names actually start with in each game, since the tabs are split on
        /// those prefixes and Platinum comes out with no fanfares at all.</summary>
        [Theory]
        [MemberData(nameof(Games))]
        public void ReportSequenceNamePrefixes(string code, string path, string game)
        {
            if (!Directory.Exists(path)) { _out.WriteLine($"{game}: not unpacked here"); return; }
            new RomInfo(code, path);
            SoundArchive.Reset();
            var sdat = SoundArchive.Load();
            if (sdat == null) { _out.WriteLine($"{game}: no sound archive"); return; }

            var counts = new Dictionary<string, int>();
            foreach (var kv in sdat.SeqNames)
            {
                string n = kv.Value ?? "";
                if (n.Length == 0) continue;
                var bits = n.Split('_');
                string head = bits.Length >= 2 ? bits[0] + "_" + bits[1] + "_" : n;
                counts[head] = counts.TryGetValue(head, out int c) ? c + 1 : 1;
            }
            _out.WriteLine($"{game}: {sdat.SeqNames.Count} named sequences");
            foreach (var kv in counts.OrderByDescending(k => k.Value).Take(12))
                _out.WriteLine($"   {kv.Key}  {kv.Value}");
        }

        [Theory]
        [MemberData(nameof(Games))]
        public void ReportWhatTheSoundArchiveHolds(string code, string path, string game)
        {
            if (!Directory.Exists(path)) { _out.WriteLine($"{game}: not unpacked here"); return; }
            new RomInfo(code, path);
            SoundArchive.Reset();
            var sdat = SoundArchive.Load();
            if (sdat == null) { _out.WriteLine($"{game}: no sound archive"); return; }

            var cryBanks = SoundArchive.CryBanks();
            var cryArcs = new HashSet<int>();
            foreach (int b in cryBanks)
            {
                if (b < 0 || b >= sdat.Banks.Count || sdat.Banks[b] == null) continue;
                foreach (int w in sdat.Banks[b].WaveArcNo) if (w >= 0 && w < sdat.WaveArcs.Count) cryArcs.Add(w);
            }

            int live = sdat.WaveArcs.Count(w => w != null);
            int samplesInCryArcs = 0, samplesElsewhere = 0, otherArcs = 0;
            for (int i = 0; i < sdat.WaveArcs.Count; i++)
            {
                if (sdat.WaveArcs[i] == null) continue;
                int n = 0;
                try { n = sdat.GetWaveArchive(i)?.Count ?? 0; } catch { }
                if (cryArcs.Contains(i)) samplesInCryArcs += n;
                else { samplesElsewhere += n; if (n > 0) otherArcs++; }
            }

            _out.WriteLine($"{game}: {sdat.Sequences.Count} sequences, {sdat.Banks.Count} banks, "
                         + $"{live} wave archives with data");
            _out.WriteLine($"   cry banks {cryBanks.Count}, cry wave archives {cryArcs.Count}, "
                         + $"{samplesInCryArcs} samples in them");
            _out.WriteLine($"   other wave archives {otherArcs}, {samplesElsewhere} samples in them");

            var named = sdat.WaveArcNames.Where(k => !string.IsNullOrEmpty(k.Value))
                                         .Where(k => !cryArcs.Contains(k.Key))
                                         .OrderBy(k => k.Key).Take(12).ToList();
            foreach (var kv in named)
            {
                int n = 0; try { n = sdat.GetWaveArchive(kv.Key)?.Count ?? 0; } catch { }
                _out.WriteLine($"   wave archive {kv.Key} \"{kv.Value}\": {n} samples");
            }
        }
    }
}
