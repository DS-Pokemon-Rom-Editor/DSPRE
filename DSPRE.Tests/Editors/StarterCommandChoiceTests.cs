using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.ROMFiles;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// Pointing the starter editor at a command by hand, for a romhack that moved the starter somewhere
    /// the pair rule cannot see. The check has to say yes, say "this one picks its own species", and
    /// refuse, and each of those has to happen for a real place in the game rather than in the abstract.
    /// </summary>
    [Collection("rom")]
    public class StarterCommandChoiceTests
    {
        private readonly ITestOutputHelper _out;
        public StarterCommandChoiceTests(ITestOutputHelper o) => _out = o;

        private static readonly string Platinum = TestRoms.Platinum;

        private static bool Open()
        {
            if (!Directory.Exists(Platinum)) return false;
            SettingsManager.Load();
            new RomInfo("CPUE", Platinum);
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.scripts });
            return StarterRotomSource.IsAvailable;
        }

        /// <summary>
        /// A project nobody has moved the starter in reads ONE script source, not all 575. The editor
        /// knows from RomInfo which file and which script the game keeps it in, so there is nothing to
        /// search for until that stops being true.
        /// </summary>
        [Fact]
        public void TheKnownSlotIsReadWithoutSearchingTheWholeGame()
        {
            if (!Open()) { _out.WriteLine("Platinum sources not here, skipped"); return; }

            var vanilla = StarterRotomSource.FindVanilla();
            Assert.NotNull(vanilla);
            _out.WriteLine($"RomInfo says file {RomInfo.starterHeldItemScriptFileID}, script "
                         + $"{RomInfo.starterCommandScriptNumber}; found {vanilla.Where}");
            Assert.Equal(RomInfo.starterHeldItemScriptFileID, vanilla.FileId);
            Assert.Equal(RomInfo.starterCommandScriptNumber, vanilla.ContainerNumber);

            // The same command the full search settles on, so the shortcut is not a different answer.
            Assert.Equal(StarterRotomSource.FindStarter().Key, vanilla.Key);

            // And it really is one file: the whole game holds give commands in other files too.
            var everywhere = StarterRotomSource.FindAll();
            Assert.True(everywhere.Count > 1, "one give command in the game proves nothing here");
            Assert.All(StarterRotomSource.ReadFile(vanilla.FileId),
                       m => Assert.Equal(vanilla.FileId, m.FileId));
            _out.WriteLine($"{everywhere.Count} give commands in the game, "
                         + $"{StarterRotomSource.ReadFile(vanilla.FileId).Count} in the one file read");

            // A remembered choice is followed the same way, from its own file alone.
            var byKey = StarterRotomSource.FindByKey(vanilla.Key);
            Assert.NotNull(byKey);
            Assert.Equal(vanilla.Key, byKey.Key);
            Assert.Null(StarterRotomSource.FindByKey("rotom:427:script_13:99"));
        }

        /// <summary>
        /// Opening the editor on a project nobody has changed reads ONE script source. It used to read
        /// all 575 every time, which is work for nothing and would have had the editor deciding afresh
        /// each open whether the starter had moved.
        /// </summary>
        [Fact]
        public void OpeningTheEditorReadsOneScriptSourceNotTheWholeGame()
        {
            if (!Open()) { _out.WriteLine("Platinum sources not here, skipped"); return; }

            int inTheGame = Directory.GetFiles(
                Path.Combine(RomInfo.workDir, "expanded", "scripts"), "*.rotom").Length;
            Assert.True(inTheGame > 100, $"only {inTheGame} sources here, so this would prove nothing");

            StarterRotomSource.ForgetFilesRead();
            var vm = new DSPRE.Avalonia.ViewModels.Pokemon.StarterEditorViewModel();
            int read = StarterRotomSource.FilesRead;

            _out.WriteLine($"opening the editor read {read} of the {inTheGame} script sources");
            Assert.True(vm.HasCommandLocation, "it should still have found the starter");
            Assert.Equal(1, read);
        }

        /// <summary>Every give command is placed in a named script, or the picker cannot tell two apart.</summary>
        [Fact]
        public void EveryCommandKnowsWhichScriptItIsIn()
        {
            if (!Open()) { _out.WriteLine("Platinum sources not here, skipped"); return; }

            var all = StarterRotomSource.FindAll();
            Assert.True(all.Count >= 2, $"only {all.Count} found, so this proved nothing");
            foreach (var m in all)
                Assert.False(string.IsNullOrEmpty(m.Container), $"{m.Where} has no script around it");

            var starter = StarterRotomSource.FindStarter();
            _out.WriteLine($"the starter is in {starter.Container} of file {starter.FileId}");
            Assert.Equal("script_13", starter.Container);
            Assert.Contains("script_13", starter.Where, StringComparison.Ordinal);
        }

        /// <summary>The three answers, each from a real place in Platinum.</summary>
        [Fact]
        public void CheckingAPlaceSaysYesSaysItPicksItsOwnOrRefuses()
        {
            if (!Open()) { _out.WriteLine("Platinum sources not here, skipped"); return; }

            var starter = StarterRotomSource.FindStarter();
            var yes = StarterRotomSource.Verify(starter.FileId, starter.Container);
            _out.WriteLine($"the starter's own script: {yes.Verdict} - {yes.Message}");
            Assert.Equal(StarterRotomSource.Verdict.Usable, yes.Verdict);
            Assert.Equal(starter.Key, yes.Found.Key);

            // A give command that names the species outright, which the editor cannot drive.
            var literal = StarterRotomSource.FindAll().FirstOrDefault(m => !m.SpeciesFromVariable);
            Assert.True(literal != null, "expected a give command with the species written in");
            var own = StarterRotomSource.Verify(literal.FileId, literal.Container);
            _out.WriteLine($"{literal.Where}: {own.Verdict} - {own.Message}");
            Assert.Equal(StarterRotomSource.Verdict.SpeciesIsItsOwn, own.Verdict);
            Assert.Contains("manage species on its own", own.Message, StringComparison.Ordinal);

            // A script that gives nothing at all.
            var no = StarterRotomSource.Verify(starter.FileId, "script_1");
            _out.WriteLine($"script_1 of the same file: {no.Verdict} - {no.Message}");
            Assert.Equal(StarterRotomSource.Verdict.NotAGiveCommand, no.Verdict);
            Assert.Null(no.Found);
        }

        /// <summary>The scripts in a file are listed, so the box can be filled in rather than guessed at.</summary>
        [Fact]
        public void TheScriptsInAFileCanBeListed()
        {
            if (!Open()) { _out.WriteLine("Platinum sources not here, skipped"); return; }

            var starter = StarterRotomSource.FindStarter();
            var names = StarterRotomSource.ContainersIn(starter.FileId);
            _out.WriteLine($"file {starter.FileId} holds {names.Count} scripts: {string.Join(", ", names.Take(6))}");
            Assert.Contains(starter.Container, names);
            Assert.True(names.Count > 1, "a file with one script proves nothing about listing them");
        }

        /// <summary>
        /// The dialog hands back what was chosen, and says when the species is out of our hands. Without
        /// that flag the editor would keep offering species dropdowns that change nothing.
        /// </summary>
        [Fact]
        public void TheDialogReportsWhatWasChosenAndWhetherSpeciesIsOurs()
        {
            if (!Open()) { _out.WriteLine("Platinum sources not here, skipped"); return; }

            var starter = StarterRotomSource.FindStarter();
            var vm = new DSPRE.Avalonia.ViewModels.Pokemon.StarterCommandDialogViewModel(starter);
            Assert.True(vm.Candidates.Count >= 2, "the picker should have something to pick between");
            Assert.True(vm.SelectedIndex >= 0, "the current one should be picked out already");

            vm.Verify();
            vm.Accept();
            Assert.Equal(starter.Key, vm.Chosen.Key);
            Assert.False(vm.SpeciesIsOutOfOurHands);

            var literal = StarterRotomSource.FindAll().First(m => !m.SpeciesFromVariable);
            var other = new DSPRE.Avalonia.ViewModels.Pokemon.StarterCommandDialogViewModel(starter)
            { FileId = literal.FileId, ContainerName = literal.Container };
            other.Verify();
            other.Accept();
            _out.WriteLine($"choosing {literal.Where} gives speciesIsOutOfOurHands={other.SpeciesIsOutOfOurHands}");
            Assert.True(other.SpeciesIsOutOfOurHands);
            Assert.Equal(literal.Key, other.Chosen.Key);
        }

        /// <summary>
        /// The editor offers the level, starts on what the script says, and remembers a chosen command
        /// for this project so the next open does not ask again.
        /// </summary>
        [Fact]
        public void TheEditorOffersTheLevelAndRemembersTheChosenCommand()
        {
            if (!Open()) { _out.WriteLine("Platinum sources not here, skipped"); return; }

            var starter = StarterRotomSource.FindStarter();
            var vm = new DSPRE.Avalonia.ViewModels.Pokemon.StarterEditorViewModel();
            _out.WriteLine($"level control shown={vm.IsLevelSupported}, at {vm.StarterLevel}, {vm.CommandLocation}");
            Assert.True(vm.IsLevelSupported, "Platinum should offer the level");
            Assert.Equal(starter.Level, vm.StarterLevel);
            Assert.Equal(starter.Where, vm.CommandLocation);
            Assert.True(vm.SpeciesEditable);

            var other = StarterRotomSource.FindAll().First(m => m.Key != starter.Key);
            string before = Remembered();
            string fingerprintBefore = Fingerprint();
            try
            {
                vm.ChooseCommand(other);
                Assert.Equal(other.Where, vm.CommandLocation);
                Assert.Equal(other.Key, Remembered());

                // A new editor on the same project picks the remembered one back up.
                var again = new DSPRE.Avalonia.ViewModels.Pokemon.StarterEditorViewModel();
                _out.WriteLine($"reopened on {again.CommandLocation}");
                Assert.Equal(other.Where, again.CommandLocation);
            }
            finally
            {
                // The editor writes both of these, so this test puts the user's settings back as it found them.
                if (before == null) SettingsManager.Settings.starterCommandChoice.Remove(RomInfo.workDir ?? "");
                else SettingsManager.Settings.starterCommandChoice[RomInfo.workDir ?? ""] = before;
                if (fingerprintBefore == null) SettingsManager.Settings.starterCommandFingerprint.Remove(RomInfo.workDir ?? "");
                else SettingsManager.Settings.starterCommandFingerprint[RomInfo.workDir ?? ""] = fingerprintBefore;
                SettingsManager.Save();
            }
        }

        /// <summary>
        /// Changing the held item and the level is the ordinary thing to do in this editor, and it must
        /// not make the editor ask which command the starter is next time it opens. The whole save runs
        /// here, compile and resync included, on a COPY.
        /// </summary>
        [Fact]
        public void ChangingTheItemAndLevelDoesNotMakeTheEditorAskAgain()
        {
            if (!Directory.Exists(Platinum)) { _out.WriteLine("Platinum not unpacked here, skipped"); return; }

            string copy = Path.Combine(Path.GetTempPath(), "dspre_ask_" + Guid.NewGuid().ToString("N"));
            try
            {
                foreach (string d in Directory.GetDirectories(Platinum, "*", SearchOption.AllDirectories))
                    Directory.CreateDirectory(d.Replace(Platinum, copy));
                foreach (string f in Directory.GetFiles(Platinum, "*", SearchOption.AllDirectories))
                    File.Copy(f, f.Replace(Platinum, copy), true);

                SettingsManager.Load();
                new RomInfo("CPUE", copy);
                DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.scripts });
                if (!StarterRotomSource.IsAvailable)
                { _out.WriteLine("this project has no decompiled sources, skipped"); return; }

                var opened = new DSPRE.Avalonia.ViewModels.Pokemon.StarterEditorViewModel();
                Assert.True(opened.HasCommandLocation, "the starter should have been found");
                Assert.False(opened.CommandsHaveChanged, "a project nobody has touched should say nothing");
                string before = opened.CommandLocation;
                _out.WriteLine($"first open: {before}, level {opened.StarterLevel}");

                // Somebody sets a held item and a level, and presses Save.
                var starter = StarterRotomSource.FindStarter();
                string failure = StarterRotomSource.SaveAsync(starter, 12, "ITEM_ORAN_BERRY", 155)
                                                   .GetAwaiter().GetResult();
                Assert.True(failure == null, "saving reported: " + failure);
                StarterPokemonData.RefreshRotomSourcesAsync(new List<int> { starter.FileId })
                                  .GetAwaiter().GetResult();
                _out.WriteLine("after saving: " + StarterRotomSource.FindStarter()?.Line.Trim());

                // Open it again. Nothing about which command is the starter has changed.
                var reopened = new DSPRE.Avalonia.ViewModels.Pokemon.StarterEditorViewModel();
                _out.WriteLine($"second open: {reopened.CommandLocation}, level {reopened.StarterLevel}, "
                             + $"asks again={reopened.CommandsHaveChanged}");
                Assert.False(reopened.CommandsHaveChanged,
                             "changing the item and level made the editor ask which command the starter is");
                Assert.Equal(before, reopened.CommandLocation);
                Assert.Equal(12, reopened.StarterLevel);
            }
            finally
            {
                try { new RomInfo("CPUE", Platinum); } catch { }
                try { Directory.Delete(copy, true); } catch { }
            }
        }

        /// A remembe
        /// A remembered choice has to survive an edit somewhere else in the same file. Keyed by line
        /// number it would not: adding a line above the starter would point the editor at nothing.
        /// Done on a COPY, never the real project.
        /// </summary>
        [Fact]
        public void TheRememberedChoiceSurvivesAnEditAboveIt()
        {
            if (!Directory.Exists(Platinum)) { _out.WriteLine("Platinum not unpacked here, skipped"); return; }

            string copy = Path.Combine(Path.GetTempPath(), "dspre_key_" + Guid.NewGuid().ToString("N"));
            try
            {
                foreach (string d in Directory.GetDirectories(Platinum, "*", SearchOption.AllDirectories))
                    Directory.CreateDirectory(d.Replace(Platinum, copy));
                foreach (string f in Directory.GetFiles(Platinum, "*", SearchOption.AllDirectories))
                    File.Copy(f, f.Replace(Platinum, copy), true);

                SettingsManager.Load();
                new RomInfo("CPUE", copy);
                DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.scripts });
                if (!StarterRotomSource.IsAvailable)
                { _out.WriteLine("this project has no decompiled sources, skipped"); return; }

                var before = StarterRotomSource.FindStarter();
                Assert.NotNull(before);

                // A comment put in near the top, which pushes everything below it down a line.
                string path = StarterRotomSource.PathFor(before.FileId);
                var lines = File.ReadAllLines(path).ToList();
                lines.Insert(0, "// somebody left a note here");
                File.WriteAllLines(path, lines);

                var after = StarterRotomSource.FindStarter();
                Assert.NotNull(after);
                _out.WriteLine($"line {before.LineNumber} became {after.LineNumber}, key {after.Key}");
                Assert.Equal(before.LineNumber + 1, after.LineNumber);
                Assert.Equal(before.Key, after.Key);
            }
            finally
            {
                try { new RomInfo("CPUE", Platinum); } catch { }
                try { Directory.Delete(copy, true); } catch { }
            }
        }

        private static string Fingerprint()
        {
            var map = SettingsManager.Settings?.starterCommandFingerprint;
            return map != null && map.TryGetValue(RomInfo.workDir ?? "", out string f) ? f : null;
        }

        private static string Remembered()
        {
            var map = SettingsManager.Settings?.starterCommandChoice;
            return map != null && map.TryGetValue(RomInfo.workDir ?? "", out string k) ? k : null;
        }
    }
}
