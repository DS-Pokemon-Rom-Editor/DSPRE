using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Battle
{
    public partial class BattleScreenEditorView : UserControl
    {
        private Window Owner => TopLevel.GetTopLevel(this) as Window;

        private BattleScreenEditorViewModel VM => DataContext as BattleScreenEditorViewModel;

        public BattleScreenEditorView()
        {
            AvaloniaXamlLoader.Load(this);
            DataContext = new BattleScreenEditorViewModel();
        }

        // The screens are drawn at twice size, so a click has to come back down to ROM pixels.
        private void Pick(object sender, PointerPressedEventArgs e, bool touch)
        {
            if (sender is not Control c) return;
            Point p = e.GetPosition(c);
            int x = (int)(p.X / 2), y = (int)(p.Y / 2);
            VM?.PickAt(touch, x, y);
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.SaveChanges();
        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) VM?.DiscardChanges();
        }

        private void TopScreen_Pressed(object sender, PointerPressedEventArgs e) => Pick(sender, e, false);
        private void TouchScreen_Pressed(object sender, PointerPressedEventArgs e) => Pick(sender, e, true);

        private GraphicAssets.Archive ArchiveOf(BattleScreenRenderer.Piece piece) =>
            GraphicAssets.All.FirstOrDefault(a => a.Dir == piece.Archive);

        private const string ErrorTitle = "Battle screen error";
        private async void Paint_Click(object sender, RoutedEventArgs e) => await DialogHelper.GuardAsync(PaintAsync, "The painter didn't open.", ErrorTitle, Owner);
        private async void Export_Click(object sender, RoutedEventArgs e) => await DialogHelper.GuardAsync(ExportAsync, "The PNG wasn't exported.", ErrorTitle, Owner);
        private async void Import_Click(object sender, RoutedEventArgs e) => await DialogHelper.GuardAsync(ImportAsync, "The PNG wasn't imported.", ErrorTitle, Owner);
        private async void ExportBarPieces_Click(object sender, RoutedEventArgs e) => await DialogHelper.GuardAsync(ExportBarPiecesAsync, "The bar pieces weren't exported.", ErrorTitle, Owner);
        private async void ImportBarPieces_Click(object sender, RoutedEventArgs e) => await DialogHelper.GuardAsync(ImportBarPiecesAsync, "The bar pieces weren't imported.", ErrorTitle, Owner);

        private async Task PaintAsync()
        {
            BattleScreenRenderer.Piece piece = VM?.Selected?.Piece;
            if (piece == null) return;
            GraphicAssets.Archive archive = ArchiveOf(piece);
            if (archive == null)
            {
                await DialogHelper.ShowErrorAsync("This piece's archive is not one the painter can open.", ErrorTitle);
                return;
            }
            if (!await WarnIfSharedAsync(piece)) return;
            // The painter works on the saved graphic, so pending imports are settled first.
            if (!await RecordSwitchGuard.ConfirmLeaveAsync(VM, Owner, "graphic", "Save them before painting?")) return;

            int at = BattleScreenEditorViewModel.PaintableEntry(piece);
            // The painter saves on its own, so the screens are read again once it is closed.
            AvaloniaEditorLauncher.OpenPainter(archive, at, () => VM?.Reload());
        }

        /// <summary>
        /// Hands the sheet this piece is drawn from to the Graphics window. This is the way in for the
        /// touch screen layers, which share one sheet and so cannot take a painted picture back.
        /// </summary>
        private async void HandOver_Click(object sender, RoutedEventArgs e)
        {
            BattleScreenRenderer.Piece piece = VM?.Selected?.Piece;
            if (piece == null || piece.Drawing < 0)
            {
                await DialogHelper.ShowInfoAsync("This piece is not drawn from a single file in an archive.",
                                            "Battle screen");
                return;
            }
            AvaloniaEditorLauncher.OpenGraphicAt(piece.Archive, piece.Drawing);
        }

        private async Task ExportAsync()
        {
            BattleScreenRenderer.Piece piece = VM?.Selected?.Piece;
            if (piece == null) return;
            GraphicAssets.Archive archive = ArchiveOf(piece);
            if (archive == null) return;

            string path = await DialogHelper.SaveFileAsync(Owner, "Save this piece as a PNG",
                new[] { new FilePickerFileType("PNG image") { Patterns = new[] { "*.png" } } },
                Safe(piece.Name) + ".png");
            if (path == null) return;

            int at = BattleScreenEditorViewModel.PaintableEntry(piece);
            string trouble;
            using (VM.Reading()) trouble = GraphicAssets.ExportPng(archive, at, path);
            if (trouble != null) await DialogHelper.ShowErrorAsync(trouble, ErrorTitle);
            else { VM?.Refresh(); SaveNotice.Exported(path); }
        }

        private async Task ImportAsync()
        {
            BattleScreenRenderer.Piece piece = VM?.Selected?.Piece;
            if (piece == null) return;
            GraphicAssets.Archive archive = ArchiveOf(piece);
            if (archive == null) return;
            if (!await WarnIfSharedAsync(piece)) return;

            string path = await DialogHelper.OpenFileAsync(Owner, "Import PNG",
                new[] { new FilePickerFileType("PNG image") { Patterns = new[] { "*.png" } } });
            if (path == null) return;

            int at = BattleScreenEditorViewModel.PaintableEntry(piece);
            BattleGaugeComposer.Kind? bar = piece.UsesBarPieces ? HpBarFit.KindOf(piece.Layout) : null;
            if (bar != null)
            {
                (HpBarImportPrompt.Choice barChoice, HpBarFit.Plan plan) = await HpBarImportPrompt.AskAsync(Owner, archive, piece.Layout, bar.Value, path);
                if (barChoice == HpBarImportPrompt.Choice.Cancel) return;
                if (barChoice == HpBarImportPrompt.Choice.Fit)
                {
                    string failed = VM.Import(() => HpBarImport.Apply(archive, piece.Layout, path, plan,
                        () => VM.Pending.Read(HpBarImport.SheetTarget), s => VM.StageBarPieces(s), out _));
                    if (failed != null) { await DialogHelper.ShowErrorAsync(failed, ErrorTitle); return; }
                    SaveNotice.Imported(piece.Name, path);
                    return;
                }
            }
            (PngColourPrompt.Choice choice, GraphicAssets.PngColourCheck check) = await PngColourPrompt.AskAsync(Owner, archive, at, path);
            if (choice == PngColourPrompt.Choice.Cancel) return;
            string note = null;
            string trouble = VM.Import(() => PngColourPrompt.Import(archive, at, path, choice, check, out note));
            if (trouble != null) { await DialogHelper.ShowErrorAsync(trouble, ErrorTitle); return; }
            if (!string.IsNullOrEmpty(note)) await DialogHelper.ShowInfoAsync(note, "Battle screen");
            SaveNotice.Imported(piece.Name, path);
        }

        private void BarLayout_Click(object sender, RoutedEventArgs e) => AvaloniaEditorLauncher.OpenHpBarLayoutEditor();
        private void TextBackgroundPatch_Click(object sender, RoutedEventArgs e) => VM?.OpenTextBackgroundPatch();

        private async Task ExportBarPiecesAsync()
        {
            string path = await DialogHelper.SaveFileAsync(Owner, "Save the bar pieces as a PNG",
                new[] { new FilePickerFileType("PNG image") { Patterns = new[] { "*.png" } } },
                "HP bar pieces.png");
            if (path == null) return;
            string trouble = HpBarPiecesSheet.Export(path);
            if (trouble != null) await DialogHelper.ShowErrorAsync(trouble, ErrorTitle);
            else SaveNotice.Exported(path);
        }

        private async Task ImportBarPiecesAsync()
        {
            if (VM == null) return;
            string path = await DialogHelper.OpenFileAsync(Owner, "Import PNG",
                new[] { new FilePickerFileType("PNG image") { Patterns = new[] { "*.png" } } });
            if (path == null) return;
            string trouble = VM.ImportBarPieces(path);
            if (trouble != null) await DialogHelper.ShowErrorAsync(trouble, ErrorTitle);
            else SaveNotice.Imported("the bar pieces", path);
        }

        /// <summary>
        /// Says what else a piece is used by before it is changed, the same way the background painter
        /// counts the squares that share a tile.
        /// </summary>
        private async Task<bool> WarnIfSharedAsync(BattleScreenRenderer.Piece piece)
        {
            if (string.IsNullOrEmpty(piece.SharedNote)) return true;
            return await DialogHelper.AskThreeWayAsync(
                piece.SharedNote + "\n\nChanging it changes all of them.",
                "This piece is shared", "Change it", "Leave it alone", "Cancel")
                == DialogHelper.MsgResult.Yes;
        }

        private static string Safe(string name) =>
            string.Join("_", (name ?? "piece").Split(System.IO.Path.GetInvalidFileNameChars()));
    }
}
