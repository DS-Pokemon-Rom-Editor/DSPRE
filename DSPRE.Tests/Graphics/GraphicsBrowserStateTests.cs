using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.ViewModels.Graphics;
using Xunit;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>The graphics browser keeps the visible row, preview and action target together.</summary>
    [Collection("rom")]
    public class GraphicsBrowserStateTests
    {
        [SkippableFact]
        public void PlatinumSelectionAndGroupedActionsStayOnTheVisiblePart() =>
            Check("CPUE", TestRoms.Platinum, "Platinum");

        [SkippableFact]
        public void HeartGoldSelectionAndGroupedActionsStayOnTheVisiblePart() =>
            Check("IPKE", TestRoms.HeartGold, "HeartGold");

        private static void Check(string code, string path, string game)
        {
            Skip.IfNot(Directory.Exists(path), $"The {game} test ROM project is not available.");
            new RomInfo(code, path);
            GraphicAssets.Forget();

            // The launcher scans off the UI thread. Nothing bound is published until that work ends.
            var vm = new GraphicsBrowserViewModel(loadImmediately: false);
            vm.Scan();
            Assert.Empty(vm.Tabs);
            Assert.Empty(vm.Shown);
            vm.Publish();

            Assert.NotEmpty(vm.Shown);
            Assert.NotNull(vm.Selected);
            Assert.Contains(vm.Selected, vm.Shown);
            Assert.NotEqual(vm.Selected.Archive.Title, vm.Selected.Name);
            Assert.NotEqual(0, vm.Selected.Index);
            Assert.NotEqual("Pick something on the left to see it.", vm.Details);
            Assert.True(vm.HasPicture || vm.HasNoPicture,
                $"{game}: the initial selection produced neither a picture nor an explanation");

            var places = vm.Tabs.First(t => t.Only == GraphicAssets.Group.Places);
            vm.SelectedTab = places;
            Assert.NotNull(vm.Selected);
            Assert.Contains(vm.Selected, vm.Shown);
            Assert.Equal(GraphicAssets.Group.Places, vm.Selected.In);

            vm.Search = "a result that cannot exist 8f395141";
            Assert.Empty(vm.Shown);
            Assert.Null(vm.Selected);
            Assert.False(vm.HasSelection);
            Assert.Equal("Pick something on the left to see it.", vm.Details);

            vm.Search = "";
            var effects = vm.Tabs.First(t => t.Only == GraphicAssets.Group.MoveEffects);
            vm.SelectedTab = effects;
            var grouped = vm.Shown.First(i => i.Unit.Parts.Any(p =>
                p.Archive?.Dir == DirNames.wazaEffectCell));
            vm.Selected = grouped;
            vm.PartIndex = vm.Parts.ToList().FindIndex(p => p.Archive?.Dir == DirNames.wazaEffectCell);

            Assert.Equal(DirNames.wazaEffectCell, vm.ShowingArchive.Dir);
            Assert.False(vm.CanReplace);
            Assert.Equal(vm.ShowingArchive.CannotImportBecause, vm.ReplaceHelp);
            Assert.StartsWith("move_effect_layouts_", vm.SuggestedFileName(".bin"));
        }
    }
}
