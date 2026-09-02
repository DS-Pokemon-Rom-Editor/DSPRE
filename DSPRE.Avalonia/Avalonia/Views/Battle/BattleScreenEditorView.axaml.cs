using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Battle
{
    public partial class BattleScreenEditorView : Window
    {
        private BattleScreenEditorViewModel VM => DataContext as BattleScreenEditorViewModel;

        public BattleScreenEditorView()
        {
            AvaloniaXamlLoader.Load(this);
            DataContext = new BattleScreenEditorViewModel();
            EditorWindowChrome.Attach(this, VM);
        }

        // The screens are drawn at twice size, so a click has to come back down to ROM pixels.
        private void Pick(object sender, PointerPressedEventArgs e, bool touch)
        {
            if (sender is not Control c) return;
            var p = e.GetPosition(c);
            int x = (int)(p.X / 2), y = (int)(p.Y / 2);
            VM?.PickAt(touch, x, y);
        }

        private void TopScreen_Pressed(object sender, PointerPressedEventArgs e) => Pick(sender, e, false);
        private void TouchScreen_Pressed(object sender, PointerPressedEventArgs e) => Pick(sender, e, true);

        private GraphicAssets.Archive ArchiveOf(BattleScreenRenderer.Piece piece) =>
            GraphicAssets.All.FirstOrDefault(a => a.Dir == piece.Archive);

        private async void Paint_Click(object sender, RoutedEventArgs e)
        {
            var piece = VM?.Selected?.Piece;
            if (piece == null) return;
            var archive = ArchiveOf(piece);
            if (archive == null)
            {
                await DialogHelper.ShowError("This piece's archive is not one the painter can open.", "Battle Screen");
                return;
            }
            if (!await WarnIfShared(piece)) return;

            int at = PaintableEntry(piece);
            new GraphicPainterView(new GraphicPainterViewModel(archive, at)).ShowManaged();
        }

        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            var piece = VM?.Selected?.Piece;
            if (piece == null) return;
            var archive = ArchiveOf(piece);
            if (archive == null) return;

            string path = await DialogHelper.SaveFile(this, "Save this piece as a PNG",
                new[] { new FilePickerFileType("PNG image") { Patterns = new[] { "*.png" } } },
                Safe(piece.Name) + ".png");
            if (path == null) return;

            int at = PaintableEntry(piece);
            string trouble = GraphicAssets.ExportPng(archive, at, path);
            if (trouble != null) await DialogHelper.ShowError(trouble, "Battle Screen");
            else VM?.Refresh();
        }

        private async void Import_Click(object sender, RoutedEventArgs e)
        {
            var piece = VM?.Selected?.Piece;
            if (piece == null) return;
            var archive = ArchiveOf(piece);
            if (archive == null) return;
            if (!await WarnIfShared(piece)) return;

            string path = await DialogHelper.OpenFile(this, "Open a PNG to put in",
                new[] { new FilePickerFileType("PNG image") { Patterns = new[] { "*.png" } } });
            if (path == null) return;

            int at = PaintableEntry(piece);
            string trouble = GraphicAssets.ImportPng(archive, at, path, out string note);
            if (trouble != null) { await DialogHelper.ShowError(trouble, "Battle Screen"); return; }
            if (!string.IsNullOrEmpty(note)) await DialogHelper.ShowInfo(note, "Battle Screen");
            VM?.Refresh();
        }

        /// <summary>
        /// Says what else a piece is used by before it is changed, the same way the background painter
        /// counts the squares that share a tile.
        /// </summary>
        private async Task<bool> WarnIfShared(BattleScreenRenderer.Piece piece)
        {
            if (string.IsNullOrEmpty(piece.SharedNote)) return true;
            return await DialogHelper.AskThreeWay(
                piece.SharedNote + "\n\nChanging it changes all of them.",
                "This piece is shared", "Change it", "Leave it alone", "Cancel")
                == DialogHelper.MsgResult.Yes;
        }

        /// <summary>
        /// Which file the painter is handed. A sprite is painted as the picture its layout makes; a
        /// background as its drawing, which is where the arrangement hangs off in this codebase.
        /// </summary>
        private static int PaintableEntry(BattleScreenRenderer.Piece piece) =>
            piece.Layout >= 0 ? piece.Layout : piece.Drawing;

        private static string Safe(string name) =>
            string.Join("_", (name ?? "piece").Split(System.IO.Path.GetInvalidFileNameChars()));
    }
}
