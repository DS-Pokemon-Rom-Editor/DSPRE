using System;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.ViewModels.Audio;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// An imported cry or sound is held by the Audio Editor until Save, and Save writes the same file the
    /// immediate import wrote for the same WAV.
    /// </summary>
    [Collection("rom")]
    public class StagedSoundImportTests
    {
        private readonly ITestOutputHelper _out;
        public StagedSoundImportTests(ITestOutputHelper o) { _out = o; }

        private static readonly string HeartGold = TestRoms.HeartGold;

        private static bool Ready()
        {
            if (!Directory.Exists(HeartGold)) return false;
            try { new RomInfo("IPKE", HeartGold); } catch { return false; }
            SoundArchive.Reset();
            return SoundArchive.Load() != null && !SoundArchive.CriesGoToCheckout;
        }

        private static string ArchivePath()
        {
            foreach (string root in new[] { "files", "data" })
            {
                string p = Path.Combine(HeartGold, root, "data", "sound", "gs_sound_data.sdat");
                if (File.Exists(p)) return p;
            }
            return null;
        }

        private static string MakeWav(string tag, int length, int rate, double step)
        {
            var pcm = new short[length];
            for (int i = 0; i < pcm.Length; i++) pcm[i] = (short)(Math.Sin(i * step) * 11000);
            string path = Path.Combine(Path.GetTempPath(), $"dspre_staged_{tag}_{Guid.NewGuid():N}.wav");
            File.WriteAllBytes(path, CryFiles.WriteWav(pcm, rate));
            return path;
        }

        private static void Restore(string archive, byte[] original)
        {
            File.SetAttributes(archive, File.GetAttributes(archive) & ~FileAttributes.ReadOnly);
            File.WriteAllBytes(archive, original);
            SoundArchive.Reset();
        }

        private static AudioItem PickSound(AudioEditorViewModel vm, int arc, int index)
        {
            var item = vm.Sounds.First(s => s.WaveArc == arc && s.SampleIndex == index);
            vm.SelectedTab = 4;
            vm.SelectedSound = item;
            Assert.Same(item, vm.Selected);
            return item;
        }

        private static AudioItem PickCry(AudioEditorViewModel vm, int number)
        {
            var item = vm.Cries.First(c => c.Number == number);
            vm.SelectedTab = 0;
            vm.SelectedCry = item;
            Assert.Same(item, vm.Selected);
            return item;
        }

        [SkippableFact]
        public void AHeldSoundWritesNothingUntilSaveAndThenWhatTheImmediateImportWrote()
        {
            Skip.If(!Ready(), "HeartGold not unpacked here");
            string archive = ArchivePath();
            Assert.NotNull(archive);

            var set = SoundArchive.SampleArchives().FirstOrDefault(s => s.Count >= 2);
            Assert.True(set.Count >= 2, "no set with two or more sounds to replace one of");
            string wav = MakeWav("sound", 2048, 22050, 0.05);
            byte[] original = File.ReadAllBytes(archive);
            try
            {
                Assert.True(SoundArchive.ImportSample(set.Arc, 1, wav, out string problem), problem);
                byte[] expected = File.ReadAllBytes(archive);
                Restore(archive, original);
                Assert.False(expected.SequenceEqual(original), "the immediate import changed nothing");

                var vm = new AudioEditorViewModel(null);
                var item = PickSound(vm, set.Arc, 1);
                short[] before = vm.RenderSelected();
                Assert.NotNull(before);

                var held = SoundArchive.PrepareSample(set.Arc, 1, wav, out problem);
                Assert.True(held != null, problem);
                DateTime stamp = File.GetLastWriteTimeUtc(archive);
                vm.StageSample(item, held);

                Assert.True(vm.HasUnsavedChanges);
                Assert.EndsWith("(not saved)", item.Label);
                Assert.Equal("1 imported sound", vm.UnsavedChangesDescription);
                Assert.True(original.SequenceEqual(File.ReadAllBytes(archive)), "staging wrote the archive");
                Assert.Equal(stamp, File.GetLastWriteTimeUtc(archive));

                short[] heldSound = vm.RenderSelected();
                Assert.NotNull(heldSound);
                Assert.False(heldSound.SequenceEqual(before), "playing a held sound still played the old one");

                Assert.Null(vm.Save());
                Assert.False(vm.HasUnsavedChanges);
                Assert.DoesNotContain("(not saved)", item.Label);
                byte[] saved = File.ReadAllBytes(archive);
                _out.WriteLine($"set {set.Arc} sound 1: {saved.Length} bytes saved, {expected.Length} from the immediate import");
                Assert.True(expected.SequenceEqual(saved), "Save wrote different bytes from the immediate import");

                // What was heard before saving is what the saved archive plays.
                Assert.True(heldSound.SequenceEqual(vm.RenderSelected()), "the held preview differs from the saved sound");
            }
            finally
            {
                Restore(archive, original);
                try { File.Delete(wav); } catch { }
            }
            Assert.True(original.SequenceEqual(File.ReadAllBytes(archive)));
        }

        [SkippableFact]
        public void AHeldCryIsDroppedByDiscardAndSavesWhatTheImmediateImportWrote()
        {
            Skip.If(!Ready(), "HeartGold not unpacked here");
            string archive = ArchivePath();
            Assert.NotNull(archive);

            const int species = 25;
            string wav = MakeWav("cry", 3000, 16000, 0.11);
            byte[] original = File.ReadAllBytes(archive);
            try
            {
                Assert.True(SoundArchive.ImportCry(species, wav, out string problem), problem);
                byte[] expected = File.ReadAllBytes(archive);
                Restore(archive, original);
                Assert.False(expected.SequenceEqual(original), "the immediate import changed nothing");

                var vm = new AudioEditorViewModel(null);
                var item = PickCry(vm, species);
                short[] before = vm.RenderSelected();
                Assert.NotNull(before);

                var held = SoundArchive.PrepareCry(species, wav, out problem);
                Assert.True(held != null, problem);
                vm.StageSample(item, held);
                Assert.EndsWith("(not saved)", item.Label);
                Assert.Equal("1 imported cry", vm.UnsavedChangesDescription);
                Assert.False(vm.RenderSelected().SequenceEqual(before), "playing a held cry still played the old one");
                Assert.True(original.SequenceEqual(File.ReadAllBytes(archive)), "staging wrote the archive");

                vm.DiscardChanges();
                Assert.False(vm.HasUnsavedChanges);
                Assert.DoesNotContain("(not saved)", item.Label);
                Assert.True(before.SequenceEqual(vm.RenderSelected()), "Discard left the new cry playing");
                Assert.True(original.SequenceEqual(File.ReadAllBytes(archive)), "Discard wrote the archive");

                vm.StageSample(item, SoundArchive.PrepareCry(species, wav, out problem));
                Assert.Null(vm.Save());
                Assert.True(expected.SequenceEqual(File.ReadAllBytes(archive)), "Save wrote different bytes from the immediate import");
            }
            finally
            {
                Restore(archive, original);
                try { File.Delete(wav); } catch { }
            }
        }

        /// <summary>A cry and a sound in different wave archives are saved in one rewrite that matches the two
        /// immediate imports made one after the other.</summary>
        [SkippableFact]
        public void ACryAndASoundSaveTogetherAsTheTwoImmediateImportsDid()
        {
            Skip.If(!Ready(), "HeartGold not unpacked here");
            string archive = ArchivePath();
            Assert.NotNull(archive);

            const int species = 1;
            int cryArc = SoundArchive.CryWaveArchive(species);
            var set = SoundArchive.SampleArchives().FirstOrDefault(s => s.Count >= 1 && s.Arc != cryArc);
            Assert.True(cryArc >= 0 && set.Count >= 1, "no cry and separate sound to work with");

            string cryWav = MakeWav("cry2", 2500, 16000, 0.07);
            string soundWav = MakeWav("sound2", 1024, 32000, 0.2);
            byte[] original = File.ReadAllBytes(archive);
            try
            {
                Assert.True(SoundArchive.ImportCry(species, cryWav, out string problem), problem);
                SoundArchive.Reset();
                Assert.True(SoundArchive.ImportSample(set.Arc, 0, soundWav, out problem), problem);
                byte[] expected = File.ReadAllBytes(archive);
                Restore(archive, original);

                var vm = new AudioEditorViewModel(null);
                var cry = PickCry(vm, species);
                vm.StageSample(cry, SoundArchive.PrepareCry(species, cryWav, out problem));
                var sound = PickSound(vm, set.Arc, 0);
                vm.StageSample(sound, SoundArchive.PrepareSample(set.Arc, 0, soundWav, out problem));
                Assert.Equal("1 imported cry and 1 imported sound", vm.UnsavedChangesDescription);
                Assert.True(original.SequenceEqual(File.ReadAllBytes(archive)), "staging wrote the archive");

                Assert.Null(vm.Save());
                Assert.True(expected.SequenceEqual(File.ReadAllBytes(archive)), "Save wrote different bytes from the two immediate imports");
            }
            finally
            {
                Restore(archive, original);
                try { File.Delete(cryWav); } catch { }
                try { File.Delete(soundWav); } catch { }
            }
        }

        [SkippableFact]
        public void ASaveThatCannotWriteKeepsEverythingHeld()
        {
            Skip.If(!Ready(), "HeartGold not unpacked here");
            string archive = ArchivePath();
            Assert.NotNull(archive);

            var set = SoundArchive.SampleArchives().FirstOrDefault(s => s.Count >= 1);
            Assert.True(set.Count >= 1, "no sound to replace");
            string wav = MakeWav("fail", 800, 22050, 0.3);
            byte[] original = File.ReadAllBytes(archive);
            try
            {
                var vm = new AudioEditorViewModel(null);
                var item = PickSound(vm, set.Arc, 0);
                vm.StageSample(item, SoundArchive.PrepareSample(set.Arc, 0, wav, out string problem));
                Assert.True(vm.HasUnsavedChanges, problem);

                File.SetAttributes(archive, File.GetAttributes(archive) | FileAttributes.ReadOnly);
                string failure = vm.Save();
                _out.WriteLine("reported: " + failure);
                Assert.False(string.IsNullOrEmpty(failure));
                Assert.True(vm.HasUnsavedChanges);
                Assert.EndsWith("(not saved)", item.Label);
                Assert.True(original.SequenceEqual(File.ReadAllBytes(archive)));
            }
            finally
            {
                Restore(archive, original);
                try { File.Delete(wav); } catch { }
            }
        }

        [SkippableFact]
        public void ABadFileIsRefusedBeforeAnythingIsHeld()
        {
            Skip.If(!Ready(), "HeartGold not unpacked here");
            string junk = Path.Combine(Path.GetTempPath(), $"dspre_staged_junk_{Guid.NewGuid():N}.wav");
            File.WriteAllBytes(junk, Enumerable.Range(0, 64).Select(i => (byte)i).ToArray());
            try
            {
                var set = SoundArchive.SampleArchives().First();
                Assert.Null(SoundArchive.PrepareSample(set.Arc, 0, junk, out string sampleWhy));
                Assert.False(SoundArchive.ImportSample(set.Arc, 0, junk, out string importWhy));
                Assert.Equal(importWhy, sampleWhy);

                Assert.Null(SoundArchive.PrepareCry(25, junk, out string cryWhy));
                Assert.False(SoundArchive.ImportCry(25, junk, out string cryImportWhy));
                Assert.Equal(cryImportWhy, cryWhy);
                Assert.False(string.IsNullOrEmpty(cryWhy));
            }
            finally { try { File.Delete(junk); } catch { } }
        }
    }
}
