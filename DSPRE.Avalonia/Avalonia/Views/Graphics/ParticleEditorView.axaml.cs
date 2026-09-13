using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.ViewModels.Graphics;

namespace DSPRE.Avalonia.Views.Graphics
{
    public partial class ParticleEditorView : Window
    {
        private ParticleEditorViewModel VM => DataContext as ParticleEditorViewModel;

        private static readonly FilePickerFileType Png = new("PNG image") { Patterns = new[] { "*.png" } };

        public ParticleEditorView() { AvaloniaXamlLoader.Load(this); }

        public ParticleEditorView(ParticleEditorViewModel vm) : this()
        {
            DataContext = vm;
            Closed += (_, _) => vm.Stop();
        }

        private void Replay_Click(object sender, RoutedEventArgs e) => VM?.Replay();
        private void Save_Click(object sender, RoutedEventArgs e) => VM?.SaveChanges();
        private void Revert_Click(object sender, RoutedEventArgs e) => VM?.DiscardChanges();

        private async void Colour_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Control)?.DataContext is not ParticleFieldRow row) return;
            var picked = await DialogHelper.PickColour(this, row.Label);
            if (picked is { } c) row.SetColour(c.R, c.G, c.B);
        }

        private async void ReplaceTexture_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Control)?.DataContext is not ParticleTextureRow row || VM == null) return;
            string path = await DialogHelper.OpenFile(this, $"Replace texture {row.Index + 1}", new[] { Png });
            if (path != null) VM.ReplaceTexture(row.Index, path);
        }

        private async void ExportTexture_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Control)?.DataContext is not ParticleTextureRow row || VM == null) return;
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = $"Save texture {row.Index + 1}",
                SuggestedFileName = $"texture{row.Index + 1}.png",
                DefaultExtension = "png",
                FileTypeChoices = new[] { Png },
            });
            string path = file?.TryGetLocalPath();
            if (path != null) VM.ExportTexture(row.Index, path);
        }
    }
}
