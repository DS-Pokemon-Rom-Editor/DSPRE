using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.ViewModels.Graphics;

namespace DSPRE.Avalonia.Views.Graphics
{
    public partial class BottomScreenEditorView : Window
    {
        private BottomScreenEditorViewModel VM => DataContext as BottomScreenEditorViewModel;

        public BottomScreenEditorView()
        {
            AvaloniaXamlLoader.Load(this);
            DataContext = new BottomScreenEditorViewModel();
            EditorWindowChrome.Attach(this, VM);
        }

        private static GraphicAssets.Archive ArchiveOf(BottomScreenPiece piece) =>
            GraphicAssets.All.FirstOrDefault(a => a.Dir == piece.Archive);

        private static readonly FilePickerFileType Png =
            new("PNG image") { Patterns = new[] { "*.png" } };

        private async void Paint_Click(object sender, RoutedEventArgs e)
        {
            var piece = VM?.Selected;
            if (piece == null || piece.Drawing < 0) return;
            var archive = ArchiveOf(piece);
            if (archive == null)
            {
                await DialogHelper.ShowError("This piece's archive is not one the painter can open.", "Bottom Screen");
                return;
            }

            VM.Remember(piece.Archive, piece.Drawing, $"the painting of {piece.Name}");
            var painter = new GraphicPainterView(new GraphicPainterViewModel(archive, piece.Drawing));
            // The painter writes as it goes, so the screen is read again once it is closed.
            painter.Closed += (_, _) => VM?.ReloadAfterImport();
            painter.ShowManaged();
        }

        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            var piece = VM?.Selected;
            if (piece == null || piece.Drawing < 0) return;
            var archive = ArchiveOf(piece);
            if (archive == null) return;

            string path = await DialogHelper.SaveFile(this, "Save this piece as a PNG",
                new[] { Png }, Safe(piece.Name) + ".png");
            if (path == null) return;

            string trouble = GraphicAssets.ExportPng(archive, piece.Drawing, path);
            if (trouble != null) await DialogHelper.ShowError(trouble, "Bottom Screen");
            else VM?.Say("Saved " + path);
        }

        private async void Import_Click(object sender, RoutedEventArgs e)
        {
            var piece = VM?.Selected;
            if (piece == null || piece.Drawing < 0) return;
            var archive = ArchiveOf(piece);
            if (archive == null) return;

            string path = await DialogHelper.OpenFile(this, "Open a PNG to put in", new[] { Png });
            if (path == null) return;

            VM.Remember(piece.Archive, piece.Drawing, $"the PNG put into {piece.Name}");
            string trouble = GraphicAssets.ImportPng(archive, piece.Drawing, path, out string note);
            if (trouble != null) { await DialogHelper.ShowError(trouble, "Bottom Screen"); return; }
            if (!string.IsNullOrEmpty(note)) await DialogHelper.ShowInfo(note, "Bottom Screen");
            VM?.ReloadAfterImport();
            VM?.Say("Put " + System.IO.Path.GetFileName(path) + " in.");
        }

        private void EditAnimation_Click(object sender, RoutedEventArgs e)
        {
            var piece = VM?.Selected;
            if (piece == null || piece.Animation < 0) return;
            // Named for the screen it belongs to, not just "Animation", so several open at once stay apart.
            string what = $"{VM.AppName} animation {piece.Animation}";
            AvaloniaEditorLauncher.OpenCellAnimationEditor(
                piece.Archive, piece.Animation, piece.Cells, piece.Sprites,
                piece.PaletteMember, piece.PaletteRow, what, piece.SharedSheet);
        }

        private void Undo_Click(object sender, RoutedEventArgs e) => VM?.Undo();

        private void Redo_Click(object sender, RoutedEventArgs e) => VM?.Redo();

        private void Swatch_Pressed(object sender, PointerPressedEventArgs e)
        {
            if (VM == null || sender is not Control c || c.Tag is not int at) return;
            var editor = new PaletteColorEditorViewModel(
                VM.SelectedName + ", colour " + at, VM.ColourAt(at), argb => VM.SetColour(at, argb));

            // Opened away from the right-hand side, because the colours sit there and a window over them
            // swallows the click that picks the next one.
            var view = new PaletteColorEditorView(editor)
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
            };
            view.Position = new global::Avalonia.PixelPoint(Position.X + 48, Position.Y + 140);
            view.Show(this);
        }

        private static string Safe(string name) =>
            string.Join("_", (name ?? "piece").Split(System.IO.Path.GetInvalidFileNameChars()));
    }
}
