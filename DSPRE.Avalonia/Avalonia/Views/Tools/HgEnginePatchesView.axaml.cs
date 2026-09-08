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

        private async void Reload_Click(object sender, RoutedEventArgs e)
        {
            string trouble = VM?.Reload();
            if (trouble != null) await DialogHelper.ShowError(trouble, "hg-engine patches");
        }
    }
}
