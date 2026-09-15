using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Tools;

namespace DSPRE.Avalonia.Views.Tools
{
    public partial class HgEnginePatchesView : UserControl
    {
        private HgEnginePatchesViewModel VM => DataContext as HgEnginePatchesViewModel;

        public HgEnginePatchesView()
        {
            InitializeComponent();
        }

        public HgEnginePatchesView(HgEnginePatchesViewModel vm) : this()
        {
            DataContext = vm;
        }

        private async void Add_Click(object sender, RoutedEventArgs e)
        {
            string trouble = VM?.AddPatch();
            if (trouble != null) await DialogHelper.ShowError(trouble, "Add a patch");
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            string trouble = VM?.Save();
            if (trouble != null) await DialogHelper.ShowError(trouble, "hg-engine patches");
        }

        private void Discard_Click(object sender, RoutedEventArgs e) => VM?.DiscardChanges();

        private async void Reload_Click(object sender, RoutedEventArgs e)
        {
            var vm = VM;
            if (vm == null) return;
            // Reading the lists again drops patches that were added and not saved.
            if (vm.HasUnsavedChanges && !await DialogHelper.AskYesNo(
                    "Reloading drops the patches you have not saved. Reload anyway?",
                    "hg-engine patches", TopLevel.GetTopLevel(this) as Window))
                return;

            string trouble = vm.Reload();
            if (trouble != null) await DialogHelper.ShowError(trouble, "hg-engine patches");
        }
    }
}
