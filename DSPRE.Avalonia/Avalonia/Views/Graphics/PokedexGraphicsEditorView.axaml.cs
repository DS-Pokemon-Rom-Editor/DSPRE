using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.ViewModels.Graphics;

namespace DSPRE.Avalonia.Views.Graphics
{
    public partial class PokedexGraphicsEditorView : Window
    {
        private PokedexGraphicsEditorViewModel VM => DataContext as PokedexGraphicsEditorViewModel;

        private static readonly FilePickerFileType Png = new("PNG image") { Patterns = new[] { "*.png" } };

        public PokedexGraphicsEditorView()
        {
            AvaloniaXamlLoader.Load(this);
            DataContext = new PokedexGraphicsEditorViewModel();
            EditorWindowChrome.Attach(this, VM);
            EditorWindowChrome.AttachUndoKeys(this, VM);
        }

        private void Screen_Pressed(object sender, PointerPressedEventArgs e)
        {
            if (VM == null || sender is not Image image || image.Bounds.Width <= 0) return;
            Point p = e.GetPosition(image);
            int x = (int)(p.X * DexPageComposer.Width / image.Bounds.Width);
            int y = (int)(p.Y * DexPageComposer.Height / image.Bounds.Height);
            VM.PickAt(image.Name == "TopScreen", x, y);
        }

        private void Paint_Click(object sender, RoutedEventArgs e)
        {
            DexPartRow part = VM?.SelectedPart;
            GraphicAssets.Archive archive = PokedexGraphicsEditorViewModel.ArchiveOf(part);
            if (archive == null) return;
            VM.Remember(part, "the painting of " + part.Name.ToLowerInvariant() + " " + part.Member);
            GraphicPainterView painter = new(new GraphicPainterViewModel(archive, part.Member));
            // The painter writes as it goes, so the page is put together again once it closes.
            painter.Closed += (_, _) => VM?.Reload();
            painter.ShowManaged();
        }

        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            DexPartRow part = VM?.SelectedPart;
            GraphicAssets.Archive archive = PokedexGraphicsEditorViewModel.ArchiveOf(part);
            if (archive == null) return;
            string path = await DialogHelper.SaveFile(this, "Save as a PNG", new[] { Png }, $"pokedex_{part.Member}.png");
            if (path == null) return;
            string trouble = GraphicAssets.ExportPng(archive, part.Member, path);
            if (trouble != null) await DialogHelper.ShowError(trouble, "Pokédex graphics");
            else VM.Say("Saved " + path);
        }

        private async void Import_Click(object sender, RoutedEventArgs e)
        {
            DexPartRow part = VM?.SelectedPart;
            GraphicAssets.Archive archive = PokedexGraphicsEditorViewModel.ArchiveOf(part);
            if (archive == null) return;
            string path = await DialogHelper.OpenFile(this, "Import PNG", new[] { Png });
            if (path == null) return;
            VM.Remember(part, "the PNG imported into " + part.Name.ToLowerInvariant() + " " + part.Member);
            string trouble = GraphicAssets.ImportPng(archive, part.Member, path, out string note);
            if (trouble != null) { VM.DropLastStepIfUnchanged(); await DialogHelper.ShowError(trouble, "Pokédex graphics"); return; }
            if (!string.IsNullOrEmpty(note)) await DialogHelper.ShowInfo(note, "Pokédex graphics");
            VM.Reload();
            VM.Say("Put " + System.IO.Path.GetFileName(path) + " in.");
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.SaveChanges();
        private void Discard_Click(object sender, RoutedEventArgs e) => VM?.DiscardChanges();
        private void Undo_Click(object sender, RoutedEventArgs e) => VM?.Undo();
        private void Redo_Click(object sender, RoutedEventArgs e) => VM?.Redo();
    }
}
