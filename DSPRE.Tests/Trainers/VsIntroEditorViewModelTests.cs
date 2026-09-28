using System.Linq;
using DSPRE.Avalonia.ViewModels.Pokemon;
using DSPRE.Avalonia.ViewModels.Trainers;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>The intro editors' lists and fields, driven the way their views bind them. Nothing is saved.</summary>
    [Collection("rom")]
    public class VsIntroEditorViewModelTests
    {
        private static VsIntroEditorViewModel OpenTrainers(string game)
        {
            DSPRE.Tests.Pokemon.GameTablesTests.Open(game);
            var vm = new VsIntroEditorViewModel();
            vm.Load();
            vm.Ready();
            return vm;
        }

        [SkippableFact]
        public void HeartGoldListsEveryMugshotByTrainerAndEditsInMemory()
        {
            var vm = OpenTrainers("HeartGold");
            var items = vm.MugshotRows.Where(r => r.IsItem).ToList();
            Assert.Equal(16 + 1 + 5 + 5, items.Count);
            Assert.Equal("FALKNER", items[0].Title.ToUpperInvariant());
            Assert.Equal(new[] { "Gym Leaders", "Rival", "Elite Four and Champion", "Team Rocket" },
                         vm.MugshotRows.Where(r => r.IsHeader).Select(r => r.Title));

            Assert.Same(items[0], vm.SelectedMugshot);
            Assert.True(vm.ShowTrainer && vm.ShowFace && vm.ShowBanner && vm.ShowRivalName);
            Assert.False(vm.RivalName);
            Assert.StartsWith("Falkner", vm.FaceSetChoices[vm.FaceSetIndex], System.StringComparison.OrdinalIgnoreCase);
            Assert.Contains("(63)", vm.FaceSetChoices[vm.FaceSetIndex]);
            Assert.Contains("(21)", vm.BannerSetChoices[vm.BannerSetIndex]);   // the palette sits after the tiles here

            vm.TrainerIndex = 21;
            Assert.True(vm.HasUnsavedChanges);
            Assert.Equal("BUGSY", vm.SelectedMugshot.Title.ToUpperInvariant());
            vm.Undo();
            Assert.False(vm.HasUnsavedChanges);

            vm.SelectedMugshot = items.First(r => r.Record.Kind == ROMFiles.VsIntroTables.RecordKind.League);
            Assert.True(vm.ShowParticles);
            Assert.Equal(new[] { 151, 152 }, vm.Particles.Select(p => p.File));
            Assert.Contains(vm.Fixed, f => f.Label == "VS mark" && f.Value == "59, 60, 61, 62");
        }

        [SkippableFact]
        public void PlatinumClassesMoveBetweenIntros()
        {
            var vm = OpenTrainers("Platinum");
            vm.SelectedClass = vm.ClassRows.First(r => r.Class == 70);
            Assert.True(vm.ClassEditable);
            int roark = vm.ClassIntroChoices.ToList().FindIndex(c => c.StartsWith("Roark", System.StringComparison.OrdinalIgnoreCase));
            Assert.True(roark >= 0);
            vm.ClassIntroIndex = roark;
            Assert.True(vm.HasUnsavedChanges);
            Assert.StartsWith("Roark", vm.SelectedClass.Detail, System.StringComparison.OrdinalIgnoreCase);

            vm.SelectedClass = vm.ClassRows.First(r => r.Class == 5);
            Assert.False(vm.ClassEditable);

            var gym = vm.IntroRows.First(r => r.IsItem);
            vm.SelectedIntro = gym;
            Assert.True(vm.HasIntroMugshot);
            Assert.Equal(2, vm.IntroClasses.Count);                      // Roark's class, and the one just added
            vm.ShowMugshot();
            Assert.Equal(0, vm.SelectedTab);
            Assert.True(vm.ShowClass && vm.CanEditClassColours);
            vm.DiscardChanges();
            Assert.False(vm.HasUnsavedChanges);
        }

        [SkippableFact]
        public void DiamondShowsItsRecordsWithoutArt()
        {
            var vm = OpenTrainers("Diamond");
            Assert.Equal(8 + 5, vm.MugshotRows.Count(r => r.IsItem));
            Assert.False(vm.ShowFace);
            Assert.False(vm.HasPreview);
            Assert.True(vm.ShowEndX && vm.ShowFaceColumn && vm.ShowClass);
            Assert.Equal(176, (int)vm.EndX);
        }

        [SkippableTheory]
        [InlineData("HeartGold", 11)]
        [InlineData("Platinum", 19)]
        public void TheWildEditorListsItsPokemonAndMusic(string game, int species)
        {
            DSPRE.Tests.Pokemon.GameTablesTests.Open(game);
            var vm = new WildIntroEditorViewModel();
            vm.Load();
            vm.Ready();
            Assert.Equal(species, vm.SpeciesRows.Count);
            Assert.Equal(game == "HeartGold", vm.SpeciesEditable);
            Assert.NotEmpty(vm.ComboRows);
            vm.SelectedCombo = vm.ComboRows.First();
            Assert.True(vm.MusicEditable || vm.MusicElsewhere);
            if (vm.SpeciesEditable)
            {
                vm.SpeciesIndex = 150;
                Assert.True(vm.HasUnsavedChanges);
                vm.Undo();
                Assert.False(vm.HasUnsavedChanges);
            }
        }
    }
}
