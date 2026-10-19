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
            // The animation runs on a timer, so it has to be let go of when the window closes.
            Closed += (_, _) => VM?.Stop();
        }

        private static GraphicAssets.Archive ArchiveOf(BottomScreenPiece piece) =>
            GraphicAssets.All.FirstOrDefault(a => a.Dir == piece.Archive);

        private static readonly FilePickerFileType Png =
            new("PNG image") { Patterns = new[] { "*.png" } };

        private async void Paint_Click(object sender, RoutedEventArgs e)
        {
            BottomScreenPiece piece = VM?.Selected;
            if (piece == null || piece.Drawing < 0) return;
            GraphicAssets.Archive archive = ArchiveOf(piece);
            if (archive == null)
            {
                await DialogHelper.ShowErrorAsync("This piece's archive is not one the painter can open.", "Bottom screen error");
                return;
            }

            // The painter works on the saved file, so pending changes are settled first.
            if (!await RecordSwitchGuard.ConfirmLeaveAsync(VM, this, "graphic", "Save them before painting?")) return;
            // The painter saves on its own, so the screen is read again once it is closed.
            AvaloniaEditorLauncher.OpenPainter(archive, piece.Drawing, () => VM?.ReloadAfterImport());
        }

        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            BottomScreenPiece piece = VM?.Selected;
            if (piece == null || piece.Drawing < 0) return;
            GraphicAssets.Archive archive = ArchiveOf(piece);
            if (archive == null) return;

            string path = await DialogHelper.SaveFileAsync(this, "Save this piece as a PNG",
                new[] { Png }, Safe(piece.Name) + ".png");
            if (path == null) return;

            string trouble;
            using (VM.Reading()) trouble = GraphicAssets.ExportPng(archive, piece.Drawing, path);
            if (trouble != null) await DialogHelper.ShowErrorAsync(trouble, "Bottom screen error");
            else SaveNotice.Exported(path);
        }

        private async void Import_Click(object sender, RoutedEventArgs e)
        {
            BottomScreenPiece piece = VM?.Selected;
            if (piece == null || piece.Drawing < 0) return;
            GraphicAssets.Archive archive = ArchiveOf(piece);
            if (archive == null) return;

            string path = await DialogHelper.OpenFileAsync(this, "Import PNG", new[] { Png });
            if (path == null) return;

            (PngColourPrompt.Choice choice, GraphicAssets.PngColourCheck check) = await PngColourPrompt.AskAsync(this, archive, piece.Drawing, path);
            if (choice == PngColourPrompt.Choice.Cancel) return;
            string note = null;
            string trouble = VM.Import(() => PngColourPrompt.Import(archive, piece.Drawing, path, choice, check, out note)
                                              ?? VM.WhyOverRoom(piece));
            if (trouble != null) { await DialogHelper.ShowErrorAsync(trouble, "Bottom screen error"); return; }
            if (!string.IsNullOrEmpty(note)) await DialogHelper.ShowInfoAsync(note, "Bottom screen");
            SaveNotice.Imported(piece.Name, path);
        }

        private void EditAnimation_Click(object sender, RoutedEventArgs e)
        {
            BottomScreenPiece piece = VM?.Selected;
            if (piece == null || piece.Animation < 0) return;
            // Named for the screen it belongs to, not just "Animation", so several open at once stay apart.
            string what = $"{VM.AppName} animation {piece.Animation}";
            AvaloniaEditorLauncher.OpenCellAnimationEditor(
                piece.Archive, piece.Animation, piece.Cells, piece.Sprites,
                piece.PaletteMember, piece.PaletteRow, what, piece.SharedSheet, VM.PoketchAppId);
        }

        private void Play_Click(object sender, RoutedEventArgs e) => VM?.TogglePlay();

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.SaveChanges();
        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) VM?.DiscardChanges();
        }
        private void Undo_Click(object sender, RoutedEventArgs e) => VM?.Undo();

        private void Redo_Click(object sender, RoutedEventArgs e) => VM?.Redo();

        private void Swatch_Pressed(object sender, PointerPressedEventArgs e)
        {
            if (VM == null || sender is not Control c || c.Tag is not int at) return;
            PaletteColorEditorViewModel editor = new PaletteColorEditorViewModel(
                VM.SelectedName + ", colour " + at, VM.ColourAt(at), (argb, group) => VM.SetColour(at, argb, group));

            // Opened away from the right-hand side, because the colours sit there and a window over them
            // swallows the click that picks the next one.
            PaletteColorEditorView view = new PaletteColorEditorView(editor)
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
