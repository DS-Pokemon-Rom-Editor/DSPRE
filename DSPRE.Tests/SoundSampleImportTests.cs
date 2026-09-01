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
    /// <summary>
    /// Putting a WAV in over one of the sounds the game is built from, and getting it back out again.
    ///
    /// Cries were the only thing that could be replaced, on the grounds that everything else is
    /// written-out notes. That is true of the sequences and false of what they play: HeartGold keeps 608
    /// samples across 66 wave archives besides the cries and Platinum 380 across 27, holding the
    /// instruments the tunes use and the actual noises the sound effects are made of. These check that
    /// one of those can be replaced and that nothing else in its archive moves.
    ///
    /// Every test here writes to a copy of the sound archive and puts the original back afterwards, so
    /// the project on disk is left as it was found.
    /// </summary>
    [Collection("rom")]
    public class SoundSampleImportTests
    {
        private readonly ITestOutputHelper _out;
        public SoundSampleImportTests(ITestOutputHelper o) { _out = o; }

        private const string HeartGold = @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents";

        private static bool Ready()
        {
            if (!Directory.Exists(HeartGold)) return false;
            try { new RomInfo("IPKE", HeartGold); } catch { return false; }
            SoundArchive.Reset();
            return SoundArchive.Load() != null;
        }

        /// <summary>The sound archive file, so it can be put back exactly as it was.</summary>
        private static string ArchivePath()
        {
            foreach (string root in new[] { "files", "data" })
            {
                string p = Path.Combine(HeartGold, root, "data", "sound", "gs_sound_data.sdat");
                if (File.Exists(p)) return p;
            }
            return null;
        }

        private static byte[] MakeWav(short[] pcm, int rate) => CryFiles.WriteWav(pcm, rate);

        [Fact]
        public void ThereAreSoundsBesidesTheCries()
        {
            if (!Ready()) { _out.WriteLine("HeartGold not unpacked here"); return; }

            var sets = SoundArchive.SampleArchives();
            Assert.NotEmpty(sets);

            int total = sets.Sum(s => s.Count);
            _out.WriteLine($"{sets.Count} sets of sounds, {total} sounds in them");
            foreach (var s in sets.Take(6)) _out.WriteLine($"   {s.Arc} {s.Name}: {s.Count}");

            // A handful would mean the survey found something other than what it was looking for.
            Assert.True(total > 300, $"only {total} sounds found besides the cries");

            // None of these may be a cry archive, or replacing one would silently change a cry.
            var cryArcs = new HashSet<int>();
            var sdat = SoundArchive.Load();
            foreach (int b in SoundArchive.CryBanks())
                if (b >= 0 && b < sdat.Banks.Count && sdat.Banks[b] != null)
                    foreach (int w in sdat.Banks[b].WaveArcNo)
                        if (w != 0xffff && w >= 0) cryArcs.Add(w);
            Assert.Empty(sets.Where(s => cryArcs.Contains(s.Arc)));
        }

        [Fact]
        public void APutInSoundComesBackOutAsItWentIn()
        {
            if (!Ready()) { _out.WriteLine("HeartGold not unpacked here"); return; }
            string archive = ArchivePath();
            Assert.NotNull(archive);

            var sets = SoundArchive.SampleArchives();
            // A set with more than one sound in it, so the test can also prove the others are left alone.
            var set = sets.FirstOrDefault(s => s.Count >= 3);
            Assert.True(set.Count >= 3, "no set of sounds with three or more in it, the test would prove little");
            int arc = set.Arc, at = 1;

            var before = SoundArchive.Sample(arc, at);
            Assert.NotNull(before);
            var neighbours = new[] { 0, 2 }
                .Select(i => SoundArchive.Sample(arc, i)?.Pcm?.ToArray())
                .ToArray();
            Assert.All(neighbours, n => Assert.NotNull(n));

            // A shape nothing in the ROM has, so getting it back cannot be the old sound coming through.
            var pcm = new short[2048];
            for (int i = 0; i < pcm.Length; i++)
                pcm[i] = (short)(Math.Sin(i * 0.05) * 12000);
            string wav = Path.Combine(Path.GetTempPath(), "dspre_sound_test.wav");
            File.WriteAllBytes(wav, MakeWav(pcm, 22050));

            byte[] original = File.ReadAllBytes(archive);
            try
            {
                Assert.True(SoundArchive.ImportSample(arc, at, wav, out string problem), problem);

                SoundArchive.Reset();
                var after = SoundArchive.Sample(arc, at);
                Assert.NotNull(after);
                Assert.Equal(22050, after.SampleRate);

                // The games squeeze their sounds, so what comes back is close rather than identical. What
                // matters is that it is the new shape and not the old one.
                Assert.Equal(pcm.Length, after.Pcm.Length);
                double toNew = Rms(pcm, after.Pcm), toOld = Rms(before.Pcm, after.Pcm);
                _out.WriteLine($"set {arc} \"{set.Name}\" sound {at}: "
                             + $"{after.Pcm.Length} samples, off the new shape by {toNew:F0}, "
                             + $"off the old one by {toOld:F0}");
                Assert.True(toNew < toOld / 4,
                    $"what came back is not the sound that went in: {toNew:F0} against {toOld:F0}");

                // Everything else in the same archive has to be untouched, byte for byte.
                for (int k = 0; k < 2; k++)
                {
                    int i = k == 0 ? 0 : 2;
                    var now = SoundArchive.Sample(arc, i)?.Pcm;
                    Assert.NotNull(now);
                    Assert.Equal(neighbours[k].Length, now.Length);
                    Assert.True(neighbours[k].SequenceEqual(now),
                        $"sound {i} in the same set changed when sound {at} was replaced");
                }
            }
            finally
            {
                File.WriteAllBytes(archive, original);
                SoundArchive.Reset();
                try { File.Delete(wav); } catch { }
            }

            // And the file on disk is back to exactly what it was.
            Assert.True(original.SequenceEqual(File.ReadAllBytes(archive)));
        }

        /// <summary>How far apart two sounds are, so "it is the new one" can be measured rather than
        /// asserted.</summary>
        private static double Rms(short[] a, short[] b)
        {
            int n = Math.Min(a.Length, b.Length);
            if (n == 0) return double.MaxValue;
            double sum = 0;
            for (int i = 0; i < n; i++) { double d = a[i] - b[i]; sum += d * d; }
            return Math.Sqrt(sum / n);
        }

        [Fact]
        public void ReplacingASoundLeavesEveryOtherSetAlone()
        {
            if (!Ready()) { _out.WriteLine("HeartGold not unpacked here"); return; }
            string archive = ArchivePath();
            Assert.NotNull(archive);

            var sets = SoundArchive.SampleArchives();
            var set = sets.FirstOrDefault(s => s.Count >= 1);
            Assert.True(set.Count >= 1, "no sounds to work with");

            // A cry, because a cry sharing an archive with a sound would be the worst way to find out.
            var cryBefore = SoundArchive.CrySample(1)?.Pcm?.ToArray();
            var otherSet = sets.FirstOrDefault(s => s.Arc != set.Arc && s.Count >= 1);
            var otherBefore = otherSet.Count >= 1
                ? SoundArchive.Sample(otherSet.Arc, 0)?.Pcm?.ToArray() : null;

            var pcm = new short[512];
            for (int i = 0; i < pcm.Length; i++) pcm[i] = (short)(i % 97 * 300 - 14000);
            string wav = Path.Combine(Path.GetTempPath(), "dspre_sound_test2.wav");
            File.WriteAllBytes(wav, MakeWav(pcm, 16000));

            byte[] original = File.ReadAllBytes(archive);
            try
            {
                Assert.True(SoundArchive.ImportSample(set.Arc, 0, wav, out string problem), problem);
                SoundArchive.Reset();

                if (cryBefore != null)
                {
                    var cryNow = SoundArchive.CrySample(1)?.Pcm;
                    Assert.NotNull(cryNow);
                    Assert.True(cryBefore.SequenceEqual(cryNow), "a cry changed when a sound was replaced");
                    _out.WriteLine($"cry 1 unchanged, {cryBefore.Length} samples");
                }
                if (otherBefore != null)
                {
                    var otherNow = SoundArchive.Sample(otherSet.Arc, 0)?.Pcm;
                    Assert.NotNull(otherNow);
                    Assert.True(otherBefore.SequenceEqual(otherNow),
                        $"set {otherSet.Arc} changed when set {set.Arc} was replaced");
                    _out.WriteLine($"set {otherSet.Arc} unchanged, {otherBefore.Length} samples");
                }
                Assert.True(cryBefore != null || otherBefore != null,
                    "nothing was available to compare, so this proved nothing");
            }
            finally
            {
                File.WriteAllBytes(archive, original);
                SoundArchive.Reset();
                try { File.Delete(wav); } catch { }
            }
        }

        /// <summary>
        /// Reading a wave archive and writing it straight back must give the same bytes.
        ///
        /// This is the check that catches a rebuild quietly degrading everything it did not mean to touch,
        /// which is what re-squeezing every sample was doing. Swept over every wave archive in the game
        /// rather than one, because one archive passing says nothing about the other 560.
        /// </summary>
        [Fact]
        public void EveryWaveArchiveRebuildsToTheSameBytes()
        {
            if (!Ready()) { _out.WriteLine("HeartGold not unpacked here"); return; }
            var sdat = SoundArchive.Load();
            Assert.NotNull(sdat);

            int checked_ = 0, same = 0, sizeOnly = 0;
            var wrong = new List<string>();
            for (int i = 0; i < sdat.WaveArcs.Count; i++)
            {
                if (sdat.WaveArcs[i] == null) continue;
                byte[] original;
                List<SwavSample> waves;
                try
                {
                    original = sdat.GetFileBytes(sdat.WaveArcs[i].FileId);
                    waves = sdat.GetWaveArchive(i);
                }
                catch { continue; }
                if (original == null || waves == null || waves.Count == 0) continue;

                checked_++;
                var rebuilt = CryFiles.BuildArchive(waves);
                if (rebuilt.Length == original.Length && rebuilt.SequenceEqual(original)) { same++; continue; }
                if (rebuilt.Length != original.Length) sizeOnly++;
                if (wrong.Count < 5)
                    wrong.Add($"archive {i} ({waves.Count} sounds): {original.Length} bytes in, "
                            + $"{rebuilt.Length} out");
            }

            _out.WriteLine($"{checked_} wave archives rebuilt, {same} byte for byte, "
                         + $"{checked_ - same} not, {sizeOnly} of those a different size");
            foreach (var w in wrong) _out.WriteLine("   " + w);

            Assert.True(checked_ > 500, $"only {checked_} wave archives were read, the sweep proved little");
            Assert.Equal(checked_, same);
        }

        /// <summary>A sequence still cannot take a WAV, and the window has to say so rather than
        /// offering a button that does nothing.</summary>
        [Fact]
        public void ASequenceStillCannotTakeAWav()
        {
            if (!Ready()) { _out.WriteLine("HeartGold not unpacked here"); return; }

            var vm = new DSPRE.Avalonia.ViewModels.AudioEditorViewModel(null);
            Assert.NotEmpty(vm.Music);
            Assert.NotEmpty(vm.Sounds);

            vm.SelectedTab = 1;
            vm.SelectedMusic = vm.Music[0];
            Assert.False(vm.CanImport);
            Assert.Contains("Sounds tab", vm.SelectedDescription);

            vm.SelectedTab = 4;
            vm.SelectedSound = vm.Sounds[0];
            Assert.True(vm.CanImport);
            _out.WriteLine($"{vm.Sounds.Count} sounds listed; first is {vm.Sounds[0].Label}");
        }
    }
}
