using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Tools;

namespace DSPRE.Avalonia.Views.Tools
{
    public partial class BattleTestsView : Window
    {
        private BattleTestsViewModel VM => DataContext as BattleTestsViewModel;

        public BattleTestsView() : this(new BattleTestsViewModel()) { }

        public BattleTestsView(BattleTestsViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
            DSPRE.Avalonia.EditorWindowChrome.Attach(this, vm);
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.SaveChanges();
        private void Discard_Click(object sender, RoutedEventArgs e) => VM?.DiscardChanges();
        private void Add_Click(object sender, RoutedEventArgs e) => VM?.AddTest();

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (VM?.Selected == null) return;
            if (!await DialogHelper.AskYesNo($"Delete {VM.Selected} from the checkout?", "Delete test", this)) return;
            string error = VM.DeleteSelected();
            if (error != null) await DialogHelper.ShowError(error, "Delete test", this);
        }
    }
}
