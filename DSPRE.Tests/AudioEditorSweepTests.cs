using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.ViewModels;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// The Audio Editor's four lists, checked against the whole of a real ROM's sound archive rather than
    /// a few rows of it.
    ///
    /// The lists are built from the archive's own sequence names, so the point of these is that the split
    /// covers everything the archive holds and that what each row claims to play actually plays. Anything
    /// that renders to silence is a row somebody would click and hear nothing from.
    /// </summary>
    [Collection("rom")]
    public class AudioEditorSweepTests
    {
        private const string Project = @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents";

        private static bool Ready()
        {
            if (!Directory.Exists(Project)) return false;
            try { new RomInfo("IPKE", Project); } catch { return false; }
            SoundArchive.Reset();
            return SoundArchive.Load() != null;
        }

        [Fact]
        public void EverySequenceInTheArchiveLandsOnExactlyOneTab()
        {
            if (!Ready()) return;
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

        [Fact]
        public void EveryRowOnEveryTabActuallyMakesASound()
        {
            if (!Ready()) return;
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

            // Two of these are silence on purpose, by their own names. The third is silent in the game
            // too: SEQ_SE_GS_N_SESERAGI asks for program 104 of BANK_SE_FIELD, and that bank's program 104
            // is an empty slot (record type 0, offset 0), so there is nothing for it to play. Checked by
            // reading all 128 of that bank's program records: fifteen are empty and 104 is one of them.
            var expectedSilent = new[] { "SEQ_SILENCE_", "SEQ_SE_GS_N_SESERAGI" };
            var unexpected = silent.Where(s => !expectedSilent.Any(s.Contains)).ToList();
            Assert.True(unexpected.Count == 0,
                $"{unexpected.Count} of {checkedRows} rows played nothing: {string.Join(", ", unexpected.Take(25))}");

            // And the ones that are meant to be silent really were all that was silent, so this cannot pass
            // by everything having gone quiet at once.
            Assert.Equal(3, silent.Count);
        }

        [Fact]
        public void PickingOnOneTabDoesNotWipeThePickOnAnother()
        {
            if (!Ready()) return;
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
