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

        // ── Toolbar ──────────────────────────────────────────────────────
        private void Save_Click(object sender, RoutedEventArgs e) => VM?.SaveChanges();
        private void Discard_Click(object sender, RoutedEventArgs e) => VM?.DiscardChanges();

        private bool SetsTabActive => VM?.ActiveTabIndex == 1;

        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            TopLevel top = TopLevel.GetTopLevel(this);
            if (top == null || VM == null) return;
            bool sets = SetsTabActive;
            IStorageFile file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = sets ? "Export Battle Tower Pokémon Sets" : "Export Battle Tower Trainers",
                DefaultExtension = "bin",
                SuggestedFileName = sets ? "battle_tower_sets.bin" : "battle_tower_trainers.bin",
                FileTypeChoices = new List<FilePickerFileType> { new FilePickerFileType("Binary file") { Patterns = new[] { "*.bin" } } }
            });
            string path = file?.TryGetLocalPath();
            if (path == null) return;
            if (sets) VM.ExportSets(path); else VM.ExportTrainers(path);
        }

        private async void Import_Click(object sender, RoutedEventArgs e)
        {
            TopLevel top = TopLevel.GetTopLevel(this);
            if (top == null || VM == null) return;
            bool sets = SetsTabActive;
            IReadOnlyList<IStorageFile> files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = sets ? "Import Battle Tower Pokémon Sets" : "Import Battle Tower Trainers",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType> { new FilePickerFileType("Binary file") { Patterns = new[] { "*.bin" } } }
            });
            string path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
            if (path == null) return;
            if (sets) VM.ImportSets(path); else VM.ImportTrainers(path);
        }

        private void Locate_Click(object sender, RoutedEventArgs e) => VM?.LocateActive();

        // ── Trainers tab ─────────────────────────────────────────────────
        private void NewTrainer_Click(object sender, RoutedEventArgs e) => VM?.NewTrainer();
        private void AddSet_Click(object sender, RoutedEventArgs e) => VM?.AddSetToTrainer();
        private void RemoveSet_Click(object sender, RoutedEventArgs e) => VM?.RemoveSetFromTrainer();
        private void SetIdList_DoubleTapped(object sender, TappedEventArgs e) => VM?.NavigateToSetId();

        // ── Sets tab ─────────────────────────────────────────────────────
        private void NewSet_Click(object sender, RoutedEventArgs e) => VM?.NewSet();
    }
}
