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
    /// <summary>The pages of a book (the Pokédex, the summary screen) with their layers and files, editable in place.</summary>
    public partial class ScreenPagesView : UserControl
    {
        private ScreenPagesEditorViewModel VM => DataContext as ScreenPagesEditorViewModel;
        private Window Owner => TopLevel.GetTopLevel(this) as Window;

        private static readonly FilePickerFileType Png = new("PNG image") { Patterns = new[] { "*.png" } };

        public ScreenPagesView()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private string ErrorTitle => (VM?.Title ?? "Screen") + " error";

        private void Screen_Pressed(object sender, PointerPressedEventArgs e)
        {
            if (VM == null || sender is not Image image || image.Bounds.Width <= 0) return;
            Point p = e.GetPosition(image);
            int x = (int)(p.X * ScreenPageComposer.Width / image.Bounds.Width);
            int y = (int)(p.Y * ScreenPageComposer.Height / image.Bounds.Height);
            VM.PickAt(image.Name == "TopScreen", x, y);
        }

        private async void Paint_Click(object sender, RoutedEventArgs e)
        {
            ScreenPartRow part = VM?.SelectedPart;
            GraphicAssets.Archive archive = ScreenPagesEditorViewModel.ArchiveOf(part);
            if (archive == null) return;
            // The painter works on the saved file, so pending imports are settled first.
            if (!await RecordSwitchGuard.ConfirmLeaveAsync(VM, Owner, "graphic", "Save them before painting?")) return;
            // The painter saves on its own, so the page is put together again once it closes.
            AvaloniaEditorLauncher.OpenPainter(archive, part.Member, () => VM?.Reload());
        }

        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            ScreenPartRow part = VM?.SelectedPart;
            GraphicAssets.Archive archive = ScreenPagesEditorViewModel.ArchiveOf(part);
            if (archive == null) return;
            string name = VM.Title.ToLowerInvariant().Replace(' ', '_').Replace("é", "e");
            string path = await DialogHelper.SaveFile(Owner, "Save as a PNG", new[] { Png }, $"{name}_{part.Member}.png");
            if (path == null) return;
            string trouble;
            using (VM.Reading()) trouble = GraphicAssets.ExportPng(archive, part.Member, path);
            if (trouble != null) await DialogHelper.ShowError(trouble, ErrorTitle);
            else SaveNotice.Exported(path);
        }

        private async void Import_Click(object sender, RoutedEventArgs e)
        {
            ScreenPartRow part = VM?.SelectedPart;
            GraphicAssets.Archive archive = ScreenPagesEditorViewModel.ArchiveOf(part);
            if (archive == null) return;
            string path = await DialogHelper.OpenFile(Owner, "Import PNG", new[] { Png });
            if (path == null) return;
            (PngColourPrompt.Choice choice, GraphicAssets.PngColourCheck check) = await PngColourPrompt.AskAsync(Owner, archive, part.Member, path);
            if (choice == PngColourPrompt.Choice.Cancel) return;
            string note = null;
            string trouble = VM.Import(() => PngColourPrompt.Import(archive, part.Member, path, choice, check, out note));
            if (trouble != null) { await DialogHelper.ShowError(trouble, ErrorTitle); return; }
            if (!string.IsNullOrEmpty(note)) await DialogHelper.ShowInfo(note, VM.Title);
            SaveNotice.Imported(part.Name.ToLowerInvariant() + " " + part.Member, path);
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.SaveChanges();
        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscard(this)) VM?.DiscardChanges();
        }
        private void Undo_Click(object sender, RoutedEventArgs e) => VM?.Undo();
        private void Redo_Click(object sender, RoutedEventArgs e) => VM?.Redo();
    }
}
