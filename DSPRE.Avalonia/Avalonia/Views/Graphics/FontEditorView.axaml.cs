using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Graphics
{
    public partial class FontEditorView : Window
    {
        private FontEditorViewModel VM => DataContext as FontEditorViewModel;

        public FontEditorView()
        {
            AvaloniaXamlLoader.Load(this);
            DataContext = new FontEditorViewModel();
            EditorWindowChrome.Attach(this, VM);
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.SaveChanges();

        private void Undo_Click(object sender, RoutedEventArgs e) => VM?.Undo();
        private void Redo_Click(object sender, RoutedEventArgs e) => VM?.Redo();

        private static readonly global::Avalonia.Platform.Storage.FilePickerFileType PngOnly =
            new("PNG image") { Patterns = new[] { "*.png" } };

        private async void ExportPng_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            bool whole = VM.WholeFontForPictures;
            string path = await DialogHelper.SaveFileAsync(this,
                whole ? "Save the whole font as a PNG" : "Save this letter as a PNG",
                new[] { PngOnly },
                whole ? "font.png" : $"letter_{VM.SelectedGlyphIndex}.png");
            if (path == null) return;

            string trouble = VM.ExportPng(path, whole);
            if (trouble != null) await DialogHelper.ShowErrorAsync(trouble, "Font editor error");
            else SaveNotice.Exported(path);
        }

        private async void ImportPng_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            bool whole = VM.WholeFontForPictures;
            string path = await DialogHelper.OpenFileAsync(this,
                whole ? "Read a whole font from a PNG" : "Read this letter from a PNG",
                new[] { PngOnly });
            if (path == null) return;

            string trouble = VM.ImportPng(path, whole);
            if (trouble != null) await DialogHelper.ShowErrorAsync(trouble, "Font editor error");
            else SaveNotice.Imported(whole ? "the font" : $"letter {VM.SelectedGlyphIndex}", path);
        }

        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) (DataContext as DSPRE.Editors.IEditorWithUnsavedChanges)?.DiscardChanges();
        }
    }
}
