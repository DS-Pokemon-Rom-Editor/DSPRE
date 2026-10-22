using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.ViewModels;
using System.Collections.Generic;

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
            EditorWindowChrome.Attach(this, vm, title: () => vm.Title);
            NameRefresh.Bind(this, vm.RefreshNames);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
            => ViewModel.SaveAll();

        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) ViewModel.DiscardChanges();
        }

        private async void AddSpecies_Click(object sender, RoutedEventArgs e)
            => await ViewModel.AddNewFakemonAsync(this);

        private async void RebuildDexSortLists_Click(object sender, RoutedEventArgs e)
            => await ViewModel.PokedexVM.RebuildSortListsAsync();

        private async void ImportPokewalker_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Control)?.Tag is not PokewalkerViewModel.Picture picture) return;
            IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import Pokéwalker picture",
                AllowMultiple = false,
                FileTypeFilter = new System.Collections.Generic.List<FilePickerFileType> { DialogHelper.PngFilter },
            });
            string path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
            if (path == null) return;
            string error = ViewModel.PokewalkerVM.Import(picture, path);
            if (error != null) await DialogHelper.ShowErrorAsync(error, "Pokémon editor error", this);
            else SaveNotice.Imported("the Pokéwalker picture", path);
        }

        private async void ExportPokewalker_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Control)?.Tag is not PokewalkerViewModel.Picture picture) return;
            IStorageFile file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Pokéwalker picture",
                DefaultExtension = "png",
                SuggestedFileName = $"pokewalker_{ViewModel.SelectedMonIndex:D3}_{picture.Title.ToLowerInvariant().Replace(' ', '_')}.png",
                FileTypeChoices = new System.Collections.Generic.List<FilePickerFileType> { DialogHelper.PngFilter },
            });
            string path = file?.TryGetLocalPath();
            if (path == null) return;
            string error = ViewModel.PokewalkerVM.Export(picture, path);
            if (error != null) await DialogHelper.ShowErrorAsync(error, "Pokémon editor error", this);
            else SaveNotice.Exported(path);
        }

        private async void ImportFootprint_Click(object sender, RoutedEventArgs e)
        {
            IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import footprint",
                AllowMultiple = false,
                FileTypeFilter = new System.Collections.Generic.List<FilePickerFileType> { DialogHelper.PngFilter },
            });
            string path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
            if (path == null) return;
            string error = ViewModel.PokedexVM.ImportFootprint(path);
            if (error != null) await DialogHelper.ShowErrorAsync(error, "Pokémon editor error", this);
            else SaveNotice.Imported("the footprint", path);
        }

        private async void ExportFootprint_Click(object sender, RoutedEventArgs e)
        {
            IStorageFile file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export footprint",
                DefaultExtension = "png",
                SuggestedFileName = $"footprint_{ViewModel.SelectedMonIndex:D3}.png",
                FileTypeChoices = new System.Collections.Generic.List<FilePickerFileType> { DialogHelper.PngFilter },
            });
            string path = file?.TryGetLocalPath();
            if (path == null) return;
            string error = ViewModel.PokedexVM.ExportFootprint(path);
            if (error != null) await DialogHelper.ShowErrorAsync(error, "Pokémon editor error", this);
            else SaveNotice.Exported(path);
        }

        /// <summary>Plays the chosen Pokemon's cry. </summary>
        private void PokedexLists_Click(object sender, RoutedEventArgs e) => AvaloniaEditorLauncher.OpenPokedexLists();

        private void OpenSizePage_Click(object sender, RoutedEventArgs e) => ViewModel.PokedexVM?.OpenSizePage();

        private void OpenEvolutionSlots_Click(object sender, RoutedEventArgs e) => ViewModel.EvolutionsVM?.OpenSlotsPatch();
        private async void Evolutions_Import_Click(object sender, RoutedEventArgs e) => await ViewModel.EvolutionsVM.ImportCsvAsync(this);
        private async void Evolutions_Export_Click(object sender, RoutedEventArgs e) => await ViewModel.EvolutionsVM.ExportCsvAsync(this);

        private void PlayCry_Click(object sender, RoutedEventArgs e)
        {
            int species = ViewModel?.SelectedMonIndex ?? 0;
            if (species <= 0) return;

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    short[] pcm = SoundArchive.RenderCry(species);
                    if (pcm != null && pcm.Length > 0) AudioOutput.Current.Play(pcm, 32000);
                }
                catch { /* an editor should not put up a dialog because a sound would not play */ }
            });
        }

        /// <summary>
        /// Opens this cry in the Audio editor, where saving it out and putting a new one in live alongside
        /// every other sound in the ROM rather than being duplicated here.
        /// </summary>
        private void EditCry_Click(object sender, RoutedEventArgs e)
        {
            int species = ViewModel?.SelectedMonIndex ?? 0;
            if (species <= 0) return;
            _ = AvaloniaEditorLauncher.OpenAudioEditorAsync(species);
        }

        // ─── Learnset button handlers ─────────────────────────────────────────────
        private void Learnset_Add_Click(object sender, RoutedEventArgs e)
            => ViewModel.LearnsetVM.AddEntry();

        private void Learnset_Replace_Click(object sender, RoutedEventArgs e)
            => ViewModel.LearnsetVM.ReplaceEntry();

        private void Learnset_Delete_Click(object sender, RoutedEventArgs e)
            => ViewModel.LearnsetVM.DeleteEntry();

        private void Learnset_MoveUp_Click(object sender, RoutedEventArgs e)
            => ViewModel.LearnsetVM.MoveEntryUp();

        private void Learnset_MoveDown_Click(object sender, RoutedEventArgs e)
            => ViewModel.LearnsetVM.MoveEntryDown();

        private void Learnset_BulkEdit_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenBulkLearnsetEditor();

        private async void Learnset_Import_Click(object sender, RoutedEventArgs e)
            => await ViewModel.LearnsetVM.ImportCsvAsync(this);

        private async void Learnset_Export_Click(object sender, RoutedEventArgs e)
        {
            LearnsetEditorViewModel vm = ViewModel.LearnsetVM;
            FilePickerFileType filter = new global::Avalonia.Platform.Storage.FilePickerFileType("CSV") { Patterns = new[] { "*.csv" } };
            string path = await DialogHelper.SaveFileAsync(this, "Export learnset (CSV)", new[] { filter }, $"learnset_{vm.CurrentId:D4}.csv");
            if (path == null) return;
            try { PlainFiles.WriteText(path, vm.BuildCsv()); SaveNotice.Exported(path); }
            catch (System.Exception ex) { await DialogHelper.ShowErrorAsync($"Export failed:\n{ex.Message}", "Pokémon editor error"); }
        }
    }
}
