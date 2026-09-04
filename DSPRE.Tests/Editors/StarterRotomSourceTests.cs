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
    /// Finding the starter in a project's decompiled script sources. The game names it: a GivePokemon
    /// reading the variable a GetPlayerStarterSpecies just wrote. Neither command identifies it alone,
    /// which is the whole point of the pair.
    /// </summary>
    [Collection("rom")]
    public class StarterRotomSourceTests
    {
        private readonly ITestOutputHelper _out;
        public StarterRotomSourceTests(ITestOutputHelper o) => _out = o;

        private static readonly string Platinum = TestRoms.Platinum;

        private static bool Open(string project)
        {
            if (!Directory.Exists(project)) return false;
            SettingsManager.Load();
            new RomInfo("CPUE", project);
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.scripts });
            return true;
        }

        [SkippableFact]
        public void TheGameNamesItsOwnStarterAndOnlyOnce()
        {
            Skip.If(!Open(Platinum), "Platinum not unpacked here");
            Skip.If(!StarterRotomSource.IsAvailable, "this project has no decompiled sources");

            var all = StarterRotomSource.FindAll();
            foreach (var m in all)
                _out.WriteLine($"  {m.Where}: {m.Summary}  starter={m.NamedAsStarter}");

            Assert.True(all.Count >= 2, $"only {all.Count} give commands found, so this proved nothing");

            var named = all.Where(m => m.NamedAsStarter).ToList();
            Assert.Single(named);

            var starter = named[0];
            Assert.Equal(427, starter.FileId);
            Assert.True(IsStarterVariable(starter.SpeciesArgument),
                        $"species argument was {starter.SpeciesArgument}");
            Assert.Equal(5, starter.Level);
            Assert.True(starter.HeldItemArgument == "ITEM_NONE" || starter.HeldItemArgument == "0"
                     || int.TryParse(starter.HeldItemArgument, out _),
                        $"held item argument was {starter.HeldItemArgument}");
            Assert.Equal(starter.Key, StarterRotomSource.FindStarter().Key);
        }

        /// <summary>
        /// The rule earns its keep by rejecting things: the other give commands are not the starter,
        /// and GetPlayerStarterSpecies on its own is all over the game asking which one you picked.
        /// </summary>
        [SkippableFact]
        public void TheOtherGiveCommandsAreNotMistakenForIt()
        {
            Skip.If(!Open(Platinum), "Platinum not unpacked here");
            Skip.If(!StarterRotomSource.IsAvailable, "this project has no decompiled sources");

            var all = StarterRotomSource.FindAll();
            foreach (var m in all.Where(x => !x.NamedAsStarter))
                Assert.NotEqual(427, m.FileId);

            // One of the rejected ones takes its species from a variable too, which is why the older
            // "species is a variable" rule was not good enough.
            Assert.Contains(all, m => !m.NamedAsStarter
                                   && m.SpeciesArgument.StartsWith("VAR_", StringComparison.Ordinal));

            int lookups = Directory.GetFiles(Path.Combine(RomInfo.workDir, "expanded", "scripts"), "*.rotom")
                .Sum(f => File.ReadAllLines(f).Count(l => l.TrimStart().StartsWith("GetPlayerStarterSpecies")));
            _out.WriteLine($"GetPlayerStarterSpecies appears {lookups} times in the game");
            Assert.True(lookups > 5, "if this command were rare, the pair rule would not be needed");
        }

        /// <summary>
        /// The sources and the binaries have to point at the same command, because the editor finds it
        /// in the text and then writes it in the binary. If those two ever disagree, the edit lands
        /// somewhere the user was not shown.
        /// </summary>
        [SkippableFact]
        public void TheSourceAndTheBinaryAgreeOnWhereTheStarterIs()
        {
            Skip.If(!Open(Platinum), "Platinum not unpacked here");
            Skip.If(!StarterRotomSource.IsAvailable, "this project has no decompiled sources");

            RomInfo.InitScriptDBs();
            RomInfo.ReloadScriptCommandDictionaries();

            var fromText = StarterRotomSource.FindStarter();
            var fromBinary = StarterScriptLocator.ReadVanillaSlot();
            Assert.NotNull(fromText);
            Assert.NotNull(fromBinary);

            _out.WriteLine($"source: {fromText.Where}, level {fromText.Level}, item {fromText.HeldItemArgument}");
            _out.WriteLine($"binary: {fromBinary.Where}, level {fromBinary.Level}, item {fromBinary.HeldItem}");

            Assert.Equal(fromBinary.FileId, fromText.FileId);
            Assert.Equal(fromBinary.Level, fromText.Level);
            Assert.Equal(fromBinary.HeldItem == 0, fromText.HeldItemArgument == "ITEM_NONE");
        }
        /// <summary>
        /// Changing the line and saving, which is exactly what the Script Editor does: write the
        /// source, compile the project. Runs on a COPY, never the real project. The check that
        /// matters is the BINARY changing, because that is what the ROM is built from.
        /// </summary>
        [SkippableFact]
        public void ChangingTheLineAndSavingUpdatesTheBinary()
        {
            Skip.If(!Directory.Exists(Platinum), "Platinum not unpacked here");

            string copy = Path.Combine(Path.GetTempPath(), "dspre_save_" + Guid.NewGuid().ToString("N"));
            try
            {
                CopyTree(Platinum, copy);
                Skip.If(!Open(copy), "copy would not open");
                Skip.If(!StarterRotomSource.IsAvailable, "this project has no decompiled sources");

                var starter = StarterRotomSource.FindStarter();
                Assert.NotNull(starter);
                _out.WriteLine($"before: {starter.Line.Trim()}");

                string binPath = Filesystem.GetScriptPath(starter.FileId);
                string binBefore = Convert.ToBase64String(File.ReadAllBytes(binPath));

                string failure = StarterRotomSource.SaveAsync(starter, 20, "ITEM_ORAN_BERRY")
                                                   .GetAwaiter().GetResult();
                Assert.True(failure == null, "saving reported: " + failure);

                var again = StarterRotomSource.FindStarter();
                Assert.NotNull(again);
                _out.WriteLine($"after:  {again.Line.Trim()}");
                Assert.Equal(20, again.Level);
                Assert.Equal("ITEM_ORAN_BERRY", again.HeldItemArgument);
                Assert.True(IsStarterVariable(again.SpeciesArgument),
                            $"species argument was {again.SpeciesArgument}");

                // The compile ran: the file the ROM is built from is different now.
                string binAfter = Convert.ToBase64String(File.ReadAllBytes(binPath));
                Assert.NotEqual(binBefore, binAfter);

                // And the binary says the same thing the source does.
                RomInfo.InitScriptDBs();
                RomInfo.ReloadScriptCommandDictionaries();
                var slot = StarterScriptLocator.ReadVanillaSlot();
                Assert.NotNull(slot);
                _out.WriteLine($"binary now reads level {slot.Level}, item {slot.HeldItem}");
                Assert.Equal(20, slot.Level);
                Assert.NotEqual(0, slot.HeldItem);
            }
            finally
            {
                // Put the shared RomInfo back on the real project: the tests after this one in the
                // collection would otherwise be pointed at a folder that is about to be deleted.
                try { new RomInfo("CPUE", Platinum); } catch { }
                try { Directory.Delete(copy, true); } catch { }
            }
        }
        /// <summary>
        /// Saving must not flatten the file. rotom's single-file decompile turns every symbol in a
        /// file into a raw number (SPECIES_EEVEE to 133, ITEM_NONE to 0), and the starter save used to
        /// run it over every script it touched. Editing the text and saving touches no decompiler at
        /// all, so the names have to survive.
        /// </summary>
        [SkippableFact]
        public void SavingKeepsTheReadableNamesInTheFile()
        {
            Skip.If(!Directory.Exists(Platinum), "Platinum not unpacked here");

            string copy = Path.Combine(Path.GetTempPath(), "dspre_names_" + Guid.NewGuid().ToString("N"));
            try
            {
                CopyTree(Platinum, copy);
                Skip.If(!Open(copy), "copy would not open");
                Skip.If(!StarterRotomSource.IsAvailable, "this project has no decompiled sources");

                var starter = StarterRotomSource.FindStarter();
                Assert.NotNull(starter);

                string scripts = Path.Combine(copy, "expanded", "scripts");
                int before = Directory.GetFiles(scripts, "*.rotom").Sum(CountNames);
                _out.WriteLine($"{before} readable names across the project before saving");
                Assert.True(before > 1000, "the project should be full of names, so this proved nothing");

                string failure = StarterRotomSource.SaveAsync(starter, 20, "ITEM_ORAN_BERRY")
                                                   .GetAwaiter().GetResult();
                Assert.True(failure == null, "saving reported: " + failure);

                int after = Directory.GetFiles(scripts, "*.rotom").Sum(CountNames);
                _out.WriteLine($"{after} readable names across the project after saving");
                Assert.True(after >= before, $"saving lost {before - after} names");
            }
            finally
            {
                // Put the shared RomInfo back on the real project: the tests after this one in the
                // collection would otherwise be pointed at a folder that is about to be deleted.
                try { new RomInfo("CPUE", Platinum); } catch { }
                try { Directory.Delete(copy, true); } catch { }
            }
        }

        /// <summary>The starter's species slot, however this project happens to spell it.</summary>
        private static bool IsStarterVariable(string argument) =>
            argument == "VAR_0x8000" || argument == "0x8000"
            || argument.StartsWith("VAR_", StringComparison.Ordinal);
        /// <summary>SPECIES_, ITEM_ and VAR_ tokens: what a flattening decompile destroys.</summary>
        private static int CountNames(string path)
        {
            int n = 0;
            foreach (string line in File.ReadAllLines(path))
            {
                foreach (var token in new[] { "SPECIES_", "ITEM_", "VAR_", "FLAG_" })
                {
                    int at = 0;
                    while ((at = line.IndexOf(token, at, StringComparison.Ordinal)) >= 0) { n++; at += token.Length; }
                }
            }
            return n;
        }
        private static void CopyTree(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (string d in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(d.Replace(from, to));
            foreach (string f in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
                File.Copy(f, f.Replace(from, to), true);
        }
    }
}
