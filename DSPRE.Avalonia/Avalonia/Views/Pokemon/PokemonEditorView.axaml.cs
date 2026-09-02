using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class PokemonEditorView : Window
    {
        private PokemonEditorViewModel ViewModel => (PokemonEditorViewModel)DataContext;

        // Design-time constructor
        public PokemonEditorView()
        {
            InitializeComponent();
            DataContext = new PokemonEditorViewModel();
        }

        // Runtime constructor
        public PokemonEditorView(PokemonEditorViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            vm.SetOwner(this);
            // VM owns the bound Title (+ marker); chrome adds Ctrl+S + the close guard (Detach on close).
            EditorWindowChrome.Attach(this, vm, manageTitle: false, onClosed: vm.Detach);
        }

        private void SaveAll_Click(object sender, RoutedEventArgs e)
            => ViewModel.SaveAll();

        private void Close_Click(object sender, RoutedEventArgs e)
            => Close();

        private async void AddSpecies_Click(object sender, RoutedEventArgs e)
            => await ViewModel.AddNewFakemonAsync(this);

        /// <summary>Plays the chosen Pokemon's cry. </summary>
        private void PlayCry_Click(object sender, RoutedEventArgs e)
        {
            int species = ViewModel?.SelectedMonIndex ?? 0;
            if (species <= 0) return;

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var pcm = SoundArchive.RenderCry(species);
                    if (pcm != null && pcm.Length > 0) AudioOutput.Current.Play(pcm, 32000);
                }
                catch { /* an editor should not put up a dialog because a sound would not play */ }
            });
        }

        /// <summary>
        /// Opens this cry in the Audio Editor, where saving it out and putting a new one in live alongside
        /// every other sound in the ROM rather than being duplicated here.
        /// </summary>
        private void EditCry_Click(object sender, RoutedEventArgs e)
        {
            int species = ViewModel?.SelectedMonIndex ?? 0;
            if (species <= 0) return;
            _ = AvaloniaEditorLauncher.OpenAudioEditorAsync(species);
        }

        private void Undo_Click(object sender, RoutedEventArgs e) => ViewModel.Undo();
        private void Redo_Click(object sender, RoutedEventArgs e) => ViewModel.Redo();

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
