using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.ViewModels.Graphics;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Views.Graphics
{
    public partial class HpBarLayoutEditorView : Window
    {
        private HpBarLayoutEditorViewModel VM => DataContext as HpBarLayoutEditorViewModel;

        private HpBarPartRow _dragging;
        private (int X, int Y) _grabbedAt;

        public HpBarLayoutEditorView()
        {
            AvaloniaXamlLoader.Load(this);
            DataContext = new HpBarLayoutEditorViewModel();
            EditorWindowChrome.Attach(this, VM);
            DrawGrid();
        }

        // The squares, with the middle of the bar marked: a part that has to stay in one run cannot cross it.
        private void DrawGrid()
        {
            Canvas grid = this.FindControl<Canvas>("Grid");
            if (grid == null) return;
            double s = HpBarLayoutEditorViewModel.Square;
            IBrush thin = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)), middle = new SolidColorBrush(Color.FromArgb(200, 255, 200, 0));
            for (int x = 0; x <= BattleGaugeLayout.Columns; x++)
                grid.Children.Add(new Line
                {
                    StartPoint = new Point(x * s, 0), EndPoint = new Point(x * s, HpBarLayoutEditorViewModel.CanvasHeight),
                    Stroke = x == BattleGaugeLayout.Half ? middle : thin, StrokeThickness = x == BattleGaugeLayout.Half ? 2 : 1,
                });
            for (int y = 0; y <= BattleGaugeLayout.Rows; y++)
                grid.Children.Add(new Line
                {
                    StartPoint = new Point(0, y * s), EndPoint = new Point(HpBarLayoutEditorViewModel.CanvasWidth, y * s),
                    Stroke = thin, StrokeThickness = 1,
                });
        }

        private static (int X, int Y) SquareAt(Point p) =>
            ((int)(p.X / HpBarLayoutEditorViewModel.Square), (int)(p.Y / HpBarLayoutEditorViewModel.Square));

        private void Board_Pressed(object sender, PointerPressedEventArgs e)
        {
            if (VM == null || sender is not Control board) return;
            (int x, int y) = SquareAt(e.GetPosition(board));
            // The smallest box under the pointer, so parts that share squares in a double battle can both be picked.
            _dragging = VM.Parts.Where(p => x >= p.X && x < p.X + p.W && y >= p.Y && y < p.Y + p.H)
                                .OrderBy(p => p.W * p.H).FirstOrDefault();
            if (_dragging == null) return;
            _grabbedAt = (x - _dragging.X, y - _dragging.Y);
            e.Pointer.Capture(board);
        }

        private void Board_Moved(object sender, PointerEventArgs e)
        {
            if (_dragging == null || sender is not Control board) return;
            (int x, int y) = SquareAt(e.GetPosition(board));
            VM?.Move(_dragging, x - _grabbedAt.X, y - _grabbedAt.Y);
        }

        private void Board_Released(object sender, PointerReleasedEventArgs e)
        {
            if (_dragging == null) return;
            _dragging = null;
            e.Pointer.Capture(null);
            VM?.Commit();
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.SaveChanges();
        private void TextBackgroundPatch_Click(object sender, RoutedEventArgs e) => VM?.OpenTextBackgroundPatch();
        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscard(this)) VM?.DiscardChanges();
        }

        private async void Import_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            string path = await DialogHelper.OpenFile(this, "Import PNG",
                new[] { new FilePickerFileType("PNG image") { Patterns = new[] { "*.png" } } });
            if (path == null) return;
            GraphicAssets.Archive archive = GraphicAssets.All.FirstOrDefault(a => a.Dir == RomInfo.DirNames.battleObj);
            HpBarImportPrompt.Choice choice;
            HpBarFit.Plan plan;
            // The preview fits the picture to the layout as it stands here, saved or not.
            using (VM.Staging())
                (choice, plan) = await HpBarImportPrompt.AskAsync(this, archive, VM.LayoutIndex, VM.Kind, path);
            if (choice != HpBarImportPrompt.Choice.Fit) return;
            string trouble = VM.ImportFitted(path, plan);
            if (trouble != null) await DialogHelper.ShowError(trouble, "HP bar layout error");
            else SaveNotice.Imported("the bar picture", path);
        }
    }
}
