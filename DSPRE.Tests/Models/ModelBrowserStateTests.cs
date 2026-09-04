using System.IO;
using Avalonia;
using DSPRE;
using DSPRE.AvaloniaShell;
using DSPRE.Avalonia.ViewModels.Graphics;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>The model browser never previews or enables actions for a row hidden by its filter.</summary>
    [Collection("rom")]
    public class ModelBrowserStateTests
    {
        private static readonly object AppLock = new();
        private static bool _appReady;

        [SkippableFact]
        public void PlatinumFilterKeepsSelectionVisible() =>
            Check("CPUE", TestRoms.Platinum, "Platinum");

        [SkippableFact]
        public void HeartGoldFilterKeepsSelectionVisible() =>
            Check("IPKE", TestRoms.HeartGold, "HeartGold");

        private static void Check(string code, string path, string game)
        {
            Skip.IfNot(Directory.Exists(path), $"The {game} test ROM project is not available.");
            new RomInfo(code, path);
            EnsureAvalonia();

            var vm = new ModelBrowserViewModel();
            vm.Scan();
            Assert.Empty(vm.Tabs);
            Assert.Empty(vm.Shown);
            vm.Publish();

            Assert.NotEmpty(vm.Shown);
            Assert.NotNull(vm.Selected);
            Assert.Contains(vm.Selected, vm.Shown);
            Assert.NotEqual("Pick something on the left to see it.", vm.Details);
            Assert.True(vm.HasModel || vm.HasTexturePreview || vm.HasNoModel,
                $"{game}: the initial selection produced neither a preview nor an explanation");

            var category = vm.Tabs[1];
            vm.SelectedTab = category;
            Assert.NotNull(vm.Selected);
            Assert.Contains(vm.Selected, vm.Shown);
            Assert.Equal(category.Only, vm.Selected.In);

            vm.Search = "a result that cannot exist 8f395141";
            Assert.Empty(vm.Shown);
            Assert.Null(vm.Selected);
            Assert.False(vm.HasSelection);
            Assert.False(vm.HasModel);
            Assert.False(vm.HasTexturePreview);
            Assert.Equal("Pick something on the left to see it.", vm.Details);

            vm.Search = "";
            Assert.NotNull(vm.Selected);
            Assert.Contains(vm.Selected, vm.Shown);
        }

        private static void EnsureAvalonia()
        {
            lock (AppLock)
            {
                if (_appReady) return;
                Program.BuildAvaloniaApp().SetupWithoutStarting();
                _appReady = true;
            }
        }
    }
}
