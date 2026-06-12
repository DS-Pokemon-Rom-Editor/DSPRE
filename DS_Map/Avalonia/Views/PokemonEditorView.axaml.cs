using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class PokemonEditorView : Window
    {
        private PokemonEditorViewModel ViewModel => (PokemonEditorViewModel)DataContext;

        // Design-time constructor
        public PokemonEditorView()
        {
            InitializeComponent();
            DataContext = new PokemonEditorViewModel();
            Closing += OnWindowClosing;
        }

        // Runtime constructor
        public PokemonEditorView(PokemonEditorViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            Closing += OnWindowClosing;
        }

        private async void OnWindowClosing(object sender, WindowClosingEventArgs e)
        {
            if (!ViewModel.HasUnsavedChanges) return;
            e.Cancel = true;
            bool confirmed = await DialogHelper.AskYesNo(
                "There are unsaved changes. Close and discard them?", "Unsaved Changes");
            if (confirmed)
            {
                Closing -= OnWindowClosing;
                Close();
            }
        }

        private void SaveAll_Click(object sender, RoutedEventArgs e)
            => ViewModel.SaveAll();

        private void Close_Click(object sender, RoutedEventArgs e)
            => Close();

        // ─── Learnset button handlers ─────────────────────────────────────────────
        private void Learnset_Add_Click(object sender, RoutedEventArgs e)
            => ViewModel.LearnsetVM.AddEntry();

        private void Learnset_Delete_Click(object sender, RoutedEventArgs e)
            => ViewModel.LearnsetVM.DeleteEntry();

        private void Learnset_MoveUp_Click(object sender, RoutedEventArgs e)
            => ViewModel.LearnsetVM.MoveEntryUp();

        private void Learnset_MoveDown_Click(object sender, RoutedEventArgs e)
            => ViewModel.LearnsetVM.MoveEntryDown();

        private void Learnset_BulkEdit_Click(object sender, RoutedEventArgs e)
            => new BulkLearnsetEditorView(new BulkLearnsetEditorViewModel(true)).Show();

        private async void Learnset_Export_Click(object sender, RoutedEventArgs e)
        {
            var vm = ViewModel.LearnsetVM;
            var filter = new global::Avalonia.Platform.Storage.FilePickerFileType("CSV") { Patterns = new[] { "*.csv" } };
            string path = await DialogHelper.SaveFile(this, "Export learnset (CSV)", new[] { filter }, $"learnset_{vm.CurrentId:D4}.csv");
            if (path == null) return;
            try { System.IO.File.WriteAllText(path, vm.BuildCsv()); }
            catch (System.Exception ex) { await DialogHelper.ShowError($"Export failed:\n{ex.Message}", "Export Error"); }
        }

        // ─── Evolutions button handler ────────────────────────────────────────────
        private void SaveEvolutions_Click(object sender, RoutedEventArgs e)
            => ViewModel.EvolutionsVM.Save();
    }
}
