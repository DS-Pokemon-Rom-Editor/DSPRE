using System.IO;
using System.Threading.Tasks;
using DSPRE.Avalonia.ViewModels.Pokemon;
using DSPRE.ROMFiles;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// A Safari Zone save that cannot write used to end one of two ways: the Save button ran a void
    /// handler with nothing catching, so the whole application went down, and Save As threw into a task
    /// the view swallowed, so nothing was written and nothing was said. These pin the reporting.
    /// </summary>
    [Collection("rom")]
    public class SafariZoneSaveTests
    {
        private readonly ITestOutputHelper _out;
        public SafariZoneSaveTests(ITestOutputHelper o) { _out = o; }

        [Fact]
        public void TheFileClassUsesTheSharedSaveRatherThanItsOwnCopy()
        {
            // Its own copy always returned true and never checked what ToByteArray gave back, so a
            // caller could not tell a written file from an unwritten one.
            Assert.True(typeof(SafariZoneEncounterFile).IsSubclassOf(typeof(RomFile)),
                        "SafariZoneEncounterFile should inherit RomFile's SaveToFile");
        }

        [SkippableFact]
        public async Task AFailedSaveIsNotMistakenForASavedEditor()
        {
            Skip.If(!Directory.Exists(TestRoms.HeartGold), "HeartGold not unpacked here");
            new RomInfo("IPKE", TestRoms.HeartGold);

            var vm = new SafariZoneEncounterViewModel(true);
            await vm.SetupAsync(null);
            Skip.If(vm.FileNames.Count == 0, "this project holds no Safari Zone areas");

            vm.SelectedFileIndex = 0;
            string path = Filesystem.GetSafariZonePath(0);
            Skip.If(!File.Exists(path), "Safari Zone area 0 is not unpacked here");

            // Edit something, so the editor has changes worth losing.
            vm.GrassVM.MorningIndex = 0;
            vm.GrassVM.MorningSpecies = 25;
            Assert.True(vm.HasUnsavedChanges, "editing a slot should mark the editor dirty");

            byte[] before = File.ReadAllBytes(path);
            try
            {
                // Hold the file open so the write cannot land.
                using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    vm.Save();      // must not throw: this ran straight off a void click handler
                    Assert.True(vm.HasUnsavedChanges,
                                "a save that could not write must leave the editor dirty");
                }

                // Control: with the file free again the same call works and clears the flag, so the
                // assertion above is about the failure and not about saving never working at all.
                vm.Save();
                Assert.False(vm.HasUnsavedChanges, "a save that wrote should clear the editor");
                Assert.NotEqual(before, File.ReadAllBytes(path));
            }
            finally
            {
                // This edits the real project the tests read, so put it back whatever happened.
                File.WriteAllBytes(path, before);
                _out.WriteLine("area 0 restored to what it was");
            }
        }
    }
}
