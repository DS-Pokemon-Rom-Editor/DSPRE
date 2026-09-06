using System.Collections.Generic;
using DSPRE.Avalonia.ViewModels.Shell;
using Xunit;

namespace DSPRE.Tests.Items
{
    public class MartEditorAvailabilityTests
    {
        [Fact]
        public void RomRefreshNotifiesMartAvailabilityBindings()
        {
            var viewModel = new MainWindowViewModel();
            var changed = new HashSet<string>();
            viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            viewModel.RefreshRomState();

            Assert.Contains(nameof(MainWindowViewModel.CanUseMartEditor), changed);
            Assert.Contains(nameof(MainWindowViewModel.MartEditorNote), changed);
        }
    }
}
