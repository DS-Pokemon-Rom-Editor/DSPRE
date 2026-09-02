using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Trainers
{
    public partial class BattleTowerEditorView : UserControl
    {
        private BattleTowerEditorViewModel VM => DataContext as BattleTowerEditorViewModel;

        public BattleTowerEditorView()
        {
            InitializeComponent();
        }

        public BattleTowerEditorView(BattleTowerEditorViewModel vm) : this()
        {
            DataContext = vm;
        }

        // ── Trainers tab ─────────────────────────────────────────────────
        private void NewTrainer_Click(object sender, RoutedEventArgs e) => VM?.NewTrainer();
        private void AddSet_Click(object sender, RoutedEventArgs e) => VM?.AddSetToTrainer();
        private void RemoveSet_Click(object sender, RoutedEventArgs e) => VM?.RemoveSetFromTrainer();
        private void SetIdList_DoubleTapped(object sender, TappedEventArgs e) => VM?.NavigateToSetId();
        private void SaveTrainers_Click(object sender, RoutedEventArgs e) => VM?.SaveTrainers();

        private async void ExportTrainers_Click(object sender, RoutedEventArgs e)
        {
            var top = TopLevel.GetTopLevel(this);
            if (top == null || VM == null) return;
            var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Battle Tower Trainers",
                DefaultExtension = "bin",
                SuggestedFileName = "battle_tower_trainers.bin",
                FileTypeChoices = new List<FilePickerFileType> { new FilePickerFileType("Binary file") { Patterns = new[] { "*.bin" } } }
            });
            string path = file?.TryGetLocalPath();
            if (path != null) VM.ExportTrainers(path);
        }

        private async void ImportTrainers_Click(object sender, RoutedEventArgs e)
        {
            var top = TopLevel.GetTopLevel(this);
            if (top == null || VM == null) return;
            var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import Battle Tower Trainers",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType> { new FilePickerFileType("Binary file") { Patterns = new[] { "*.bin" } } }
            });
            string path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
            if (path != null) VM.ImportTrainers(path);
        }

        private void LocateTrainers_Click(object sender, RoutedEventArgs e) => VM?.LocateTrainers();

        // ── Sets tab ─────────────────────────────────────────────────────
        private void NewSet_Click(object sender, RoutedEventArgs e) => VM?.NewSet();
        private void SaveSets_Click(object sender, RoutedEventArgs e) => VM?.SaveSets();

        private async void ExportSets_Click(object sender, RoutedEventArgs e)
        {
            var top = TopLevel.GetTopLevel(this);
            if (top == null || VM == null) return;
            var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Battle Tower Pokémon Sets",
                DefaultExtension = "bin",
                SuggestedFileName = "battle_tower_sets.bin",
                FileTypeChoices = new List<FilePickerFileType> { new FilePickerFileType("Binary file") { Patterns = new[] { "*.bin" } } }
            });
            string path = file?.TryGetLocalPath();
            if (path != null) VM.ExportSets(path);
        }

        private async void ImportSets_Click(object sender, RoutedEventArgs e)
        {
            var top = TopLevel.GetTopLevel(this);
            if (top == null || VM == null) return;
            var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import Battle Tower Pokémon Sets",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType> { new FilePickerFileType("Binary file") { Patterns = new[] { "*.bin" } } }
            });
            string path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
            if (path != null) VM.ImportSets(path);
        }

        private void LocateSets_Click(object sender, RoutedEventArgs e) => VM?.LocateSets();
    }
}
