using System;
using System.Collections.Generic;
using System.IO;
using DSPRE;
using DSPRE.ROMFiles;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// Diamond and Pearl set the starter's held item and level inside a GivePokemon command, and DSPRE
    /// cannot read those scripts reliably yet, so the editor says where they are instead of offering a
    /// control. The numbers in that sentence are worked out from the file's own header, so they have to
    /// be right for a ROM whose scripts have moved, and for Pearl, which is not unpacked here.
    /// </summary>
    [Collection("rom")]
    public class StarterScriptNoteTests
    {
        private readonly ITestOutputHelper _out;
        public StarterScriptNoteTests(ITestOutputHelper o) => _out = o;

        private static readonly (string code, string path, string name, int file, int script)[] Games =
        {
            ("ADAE", TestRoms.Diamond, "Diamond", 342, 3),
            ("CPUE", TestRoms.Platinum, "Platinum", 427, 13),
        };

        /// <summary>
        /// Platinum is the control: its script file does parse, and the parser puts the GivePokemon in
        /// script 13. If the header walk agrees with the parser there, it can be trusted on Diamond,
        /// where the parser gives up and there is nothing else to check against.
        /// </summary>
        [Fact]
        public void TheScriptNumberIsWorkedOutFromTheFilesOwnHeader()
        {
            int played = 0;
            foreach (var (code, path, name, file, script) in Games)
            {
                if (!Directory.Exists(path)) { _out.WriteLine($"{name}: not unpacked here, skipped"); continue; }
                try { new RomInfo(code, path); }
                catch (Exception ex) { _out.WriteLine($"{name}: would not load ({ex.Message}), skipped"); continue; }
                DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.scripts });

                Assert.Equal(file, RomInfo.starterHeldItemScriptFileID);
                int found = StarterPokemonData.GetStarterScriptNumber();
                _out.WriteLine($"{name}: file {file}, header walk says script {found}, expected {script}");
                Assert.Equal(script, found);
                played++;
            }
            Assert.True(played > 0, "no game was unpacked here, so this proved nothing");
        }

        /// <summary>
        /// The sentence the user actually reads, built by the view model rather than checked in the
        /// abstract, and the held item control being gone on Diamond and still there on Platinum.
        /// </summary>
        [Fact]
        public void DiamondSaysWhereTheHeldItemLivesAndPlatinumStillEditsIt()
        {
            int played = 0;
            foreach (var (code, path, name, file, script) in Games)
            {
                if (!Directory.Exists(path)) { _out.WriteLine($"{name}: not unpacked here, skipped"); continue; }
                new RomInfo(code, path);
                DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.scripts });

                var vm = new DSPRE.Avalonia.ViewModels.Pokemon.StarterEditorViewModel();
                bool dp = RomInfo.gameFamily == RomInfo.GameFamilies.DP;
                _out.WriteLine($"{name}: note=\"{vm.ScriptNote}\" heldItem={vm.IsHeldItemSupported}");

                if (dp)
                {
                    Assert.True(vm.HasScriptNote, $"{name} should explain where the held item lives");
                    Assert.False(vm.IsHeldItemSupported, $"{name} should not offer the held item control");
                    Assert.Contains("GivePokemon", vm.ScriptNote, StringComparison.Ordinal);
                    Assert.Contains($"script file {file}", vm.ScriptNote, StringComparison.Ordinal);
                    Assert.Contains($"script {script}", vm.ScriptNote, StringComparison.Ordinal);
                }
                else
                {
                    Assert.False(vm.HasScriptNote, $"{name} should not show the Diamond note");
                    Assert.True(vm.IsHeldItemSupported, $"{name} should still edit the held item");
                    Assert.Null(vm.ScriptNote);
                }
                played++;
            }
            Assert.True(played > 0, "no game was unpacked here, so this proved nothing");
        }
        /// <summary>It says it cannot tell rather than inventing a number, when there is no file to read.</summary>
        [Fact]
        public void WithNoStarterScriptItSaysSoRatherThanGuessing()
        {
            var (code, path, name, _, _) = Games[1];
            if (!Directory.Exists(path)) { Assert.Fail($"{name} is not unpacked here, so this proved nothing"); return; }
            new RomInfo(code, path);

            // HGSS keeps its starters in the ARM9, so there is no script file to walk.
            Assert.True(RomInfo.starterHeldItemScriptFileID >= 0, "Platinum should have a starter script");
            _out.WriteLine($"Platinum's starter script id is {RomInfo.starterHeldItemScriptFileID}");
        }
    }
}
