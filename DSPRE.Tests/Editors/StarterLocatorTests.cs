using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.ROMFiles;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// Finding the command that hands over the starter. The old code wrote to a byte offset, which is
    /// wrong the moment anything in the script moves, so this finds it by shape instead.
    /// </summary>
    [Collection("rom")]
    public class StarterLocatorTests
    {
        private readonly ITestOutputHelper _out;
        public StarterLocatorTests(ITestOutputHelper o) => _out = o;

        private static readonly string Platinum = TestRoms.Platinum;

        private static bool OpenPlatinum()
        {
            if (!Directory.Exists(Platinum)) return false;
            SettingsManager.Load();
            new RomInfo("CPUE", Platinum);
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.scripts });
            RomInfo.InitScriptDBs();
            RomInfo.ReloadScriptCommandDictionaries();
            return true;
        }

        /// <summary>
        /// An untouched project reads one file and stops. If this ever starts scanning, opening the
        /// editor gets slower for everybody who has changed nothing.
        /// </summary>
        [Fact]
        public void AnUntouchedPlatinumIsFoundWithoutScanning()
        {
            if (!OpenPlatinum()) { _out.WriteLine("Platinum not unpacked here, skipped"); return; }

            var sw = Stopwatch.StartNew();
            var r = StarterScriptLocator.Locate(null, null);
            sw.Stop();

            _out.WriteLine($"{r.Outcome} in {sw.ElapsedMilliseconds} ms: {r.Chosen?.Where} {r.Chosen?.Summary}");
            Assert.Equal(StarterScriptLocator.Outcome.Vanilla, r.Outcome);
            Assert.NotNull(r.Chosen);
            Assert.Equal(427, r.Chosen.FileId);
            Assert.False(r.Chosen.InFunction);
            Assert.Equal(13, r.Chosen.ContainerId);
            Assert.Equal(11, r.Chosen.CommandIndex);
            Assert.True(r.Chosen.FromScriptSlot, "the starter's species should come from the script's own slot");
            // The level and item are whatever this project has been set to; what matters here is
            // that the slot resolves without a scan.
            Assert.True(r.Chosen.Level > 0, "the starter should be given at some level");

            // One file parsed, not eleven hundred.
            Assert.Single(r.Candidates);
        }

        /// <summary>
        /// The reason the editor cannot just take the only match: an untouched Platinum already has two
        /// give commands whose species comes from a variable, in different files.
        /// </summary>
        [Fact]
        public void VanillaPlatinumAlreadyHasMoreThanOneCandidate()
        {
            if (!OpenPlatinum()) { _out.WriteLine("Platinum not unpacked here, skipped"); return; }

            var all = StarterScriptLocator.FindCandidates();
            foreach (var c in all) _out.WriteLine($"  {c.Where}, index {c.CommandIndex}: {c.Summary}");

            Assert.True(all.Count >= 2, $"only {all.Count} give commands found, so this proved nothing");
            Assert.True(all.Count(c => c.FromVariable) >= 2,
                "the picker exists because more than one takes its species from a variable");

            // Best guess first, and it is the real one.
            Assert.Equal(427, all[0].FileId);
            Assert.Equal(13, all[0].ContainerId);
            Assert.True(all[0].FromScriptSlot);

            // Eggs are a different command and must not be offered.
            Assert.DoesNotContain(all, c => c.CommandName.IndexOf("Egg", StringComparison.OrdinalIgnoreCase) >= 0);
        }
        /// <summary>A remembered choice is honoured, and a stale one asks again instead of writing blind.</summary>
        [Fact]
        public void ARememberedChoiceIsKeptAndAStaleOneAsksAgain()
        {
            if (!OpenPlatinum()) { _out.WriteLine("Platinum not unpacked here, skipped"); return; }

            var all = StarterScriptLocator.FindCandidates();
            var other = all.FirstOrDefault(c => c.FileId != 427);
            Assert.True(other != null, "expected a second candidate to choose instead");

            var kept = StarterScriptLocator.Locate(other.Key, null);
            Assert.Equal(StarterScriptLocator.Outcome.Remembered, kept.Outcome);
            Assert.Equal(other.Key, kept.Chosen.Key);

            var stale = StarterScriptLocator.Locate("999/S1/0", null);
            _out.WriteLine($"a location that no longer exists gives {stale.Outcome}");
            Assert.Equal(StarterScriptLocator.Outcome.NeedsChoosing, stale.Outcome);
        }

        /// <summary>A candidate appearing since last time is reported rather than silently ignored.</summary>
        [Fact]
        public void ANewCandidateSinceLastTimeIsReported()
        {
            if (!OpenPlatinum()) { _out.WriteLine("Platinum not unpacked here, skipped"); return; }

            var all = StarterScriptLocator.FindCandidates();
            var chosen = all[0];

            // A fingerprint from before the game grew whatever it has now.
            var r = StarterScriptLocator.Locate(chosen.Key, "something/S0/0");
            _out.WriteLine($"with an out of date fingerprint: {r.Outcome}");
            Assert.Equal(StarterScriptLocator.Outcome.NewOneAppeared, r.Outcome);
            Assert.Equal(chosen.Key, r.Chosen.Key);
        }
    }
}
