using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.ViewModels.Audio;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.ViewModels;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// The Audio Editor's four lists, checked against the whole of a real ROM's sound archive rather than a
    /// few rows of it.
    /// </summary>
    [Collection("rom")]
    public class AudioEditorSweepTests
    {
        private static readonly string Project = TestRoms.HeartGold;

        private static bool Ready()
        {
            if (!Directory.Exists(Project)) return false;
            try { new RomInfo("IPKE", Project); } catch { return false; }
            SoundArchive.Reset();
            return SoundArchive.Load() != null;
        }

        [SkippableFact]
        public void EverySequenceInTheArchiveLandsOnExactlyOneTab()
        {
            Skip.If(!Ready(), "the extracted game project these tests read is not on this machine");
            var sdat = SoundArchive.Load();
            Assert.NotNull(sdat);

            var vm = new AudioEditorViewModel(null);
            var listed = new HashSet<int>();
            foreach (var i in vm.Music) Assert.True(listed.Add(i.Number));
            foreach (var i in vm.Fanfares) Assert.True(listed.Add(i.Number));
            foreach (var i in vm.Effects) Assert.True(listed.Add(i.Number));

            // The only named sequence left out is the shared cry sequence, which the Cries tab stands for.
            var named = sdat.SeqNames.Where(k => !string.IsNullOrEmpty(k.Value)).Select(k => k.Key).ToList();
            var missing = named.Where(n => !listed.Contains(n))
                               .Select(n => sdat.SeqNames[n]).ToList();
            Assert.Equal(new[] { SoundArchive.CrySequenceName }, missing);

            Assert.True(vm.Music.Count > 0 && vm.Fanfares.Count > 0 && vm.Effects.Count > 0,
                $"tabs were {vm.Music.Count}/{vm.Fanfares.Count}/{vm.Effects.Count}");

            // Only the banks the archive itself names as cries are listed as cries, so none of the music
            // or sound-effect banks can turn up on the Cries tab wearing a Pokemon's name.
            Assert.Equal(SoundArchive.CryBanks().Count, vm.Cries.Count);
            foreach (var cry in vm.Cries)
                Assert.StartsWith(SoundArchive.CryBankPrefix, sdat.BankNames[cry.Number]);
        }

        [SkippableFact]
        public void EveryRowOnEveryTabActuallyMakesASound()
        {
            Skip.If(!Ready(), "the extracted game project these tests read is not on this machine");
            var sdat = SoundArchive.Load();
            var vm = new AudioEditorViewModel(null);

            var silent = new List<string>();
            int checkedRows = 0;

            void Sweep(IEnumerable<AudioItem> rows)
            {
                foreach (var row in rows)
                {
                    checkedRows++;
                    var pcm = row.IsCry
                        ? SoundArchive.RenderCry(row.Number)
                        : SseqPlayer.Render(sdat, row.Number, maxSeconds: 2.0);
                    int peak = 0;
                    if (pcm != null)
                        foreach (var s in pcm) { int a = s < 0 ? -s : s; if (a > peak) peak = a; }
                    if (peak == 0) silent.Add($"{row.Number} {row.Name}");
                }
            }

            Sweep(vm.Cries);
            Sweep(vm.Music);
            Sweep(vm.Fanfares);
            Sweep(vm.Effects);

            Assert.True(checkedRows > 1800, $"only {checkedRows} rows were swept");

            // SEQ_AIF_ takes its instrument from a variable the game sets first; the *_END entries have volume 0.
            var expectedSilent = new[] { "SEQ_SILENCE_", "SEQ_SE_GS_N_SESERAGI", "SEQ_AIF_", "SEQ_PV_END", "SEQ_BGM_END", "SEQ_SE_END" };
            var unexpected = silent.Where(s => !expectedSilent.Any(s.Contains)).ToList();
            Assert.True(unexpected.Count == 0,
                $"{unexpected.Count} of {checkedRows} rows played nothing: {string.Join(", ", unexpected.Take(25))}");

            // And the ones that are meant to be silent really were all that was silent, so this cannot pass
            // by everything having gone quiet at once.
            Assert.Equal(9, silent.Count);
        }

        /// <summary>
        /// A sound is one channel in the ROM, but playing, drawing and saving all work in interleaved stereo.
        /// Handing them a single channel played it an octave high and half as long.
        /// </summary>
        [SkippableFact]
        public void ASoundComesBackInBothEarsAtItsOwnLength()
        {
            Skip.If(!Ready(), "the extracted game project these tests read is not on this machine");
            var vm = new AudioEditorViewModel(null);
            Assert.True(vm.Sounds.Count > 0);

            var sound = vm.Sounds.First(s => s.IsSample);
            vm.SelectedSound = sound;
            for (int tab = 0; tab < 8 && !ReferenceEquals(vm.Selected, sound); tab++) vm.SelectedTab = tab;
            Assert.Same(sound, vm.Selected);

            const int rate = 32000;
            short[] pcm = vm.RenderSelected(rate);
            Assert.NotNull(pcm);
            Assert.Equal(0, pcm.Length % 2);
            for (int i = 0; i < pcm.Length; i += 2) Assert.Equal(pcm[i], pcm[i + 1]);

            var source = SoundArchive.Sample(sound.WaveArc, sound.SampleIndex);
            double wanted = source.Pcm.Length / (double)source.SampleRate;
            double got = pcm.Length / 2.0 / rate;
            Assert.True(System.Math.Abs(got - wanted) < wanted * 0.01 + 0.01,
                        $"the sound lasts {got:0.000}s where the sample is {wanted:0.000}s");
        }

        [SkippableFact]
        public void PickingOnOneTabDoesNotWipeThePickOnAnother()
        {
            Skip.If(!Ready(), "the extracted game project these tests read is not on this machine");
            var vm = new AudioEditorViewModel(null);
            Assert.True(vm.Cries.Count > 0 && vm.Music.Count > 0);

            vm.SelectedCry = vm.Cries[0];
            vm.SelectedTab = 1;
            vm.SelectedMusic = vm.Music[0];

            Assert.Same(vm.Music[0], vm.Selected);
            Assert.False(vm.CanImport);       // a tune has no sample of its own

            vm.SelectedTab = 0;
            Assert.Same(vm.Cries[0], vm.Selected);
            Assert.True(vm.CanImport);
        }
    }
}
