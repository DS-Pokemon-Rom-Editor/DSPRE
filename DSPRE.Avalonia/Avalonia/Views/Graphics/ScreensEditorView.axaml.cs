using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DSPRE.Avalonia.ViewModels.Graphics;

namespace DSPRE.Avalonia.Views.Graphics
{
    public partial class ScreensEditorView : UserControl
    {
        public ScreensEditorView()
        {
            InitializeComponent();
        }

        public ScreensEditorView(ScreensEditorViewModel vm) : this() => DataContext = vm;

        private static ScreenTabViewModel PageOf(object sender) => (sender as Control)?.DataContext as ScreenTabViewModel;

        private void NextStop_Click(object sender, RoutedEventArgs e) => PageOf(sender)?.NextStop();
        private void EditText_Click(object sender, RoutedEventArgs e) => PageOf(sender)?.EditText();
        private void OpenPiece_Click(object sender, RoutedEventArgs e) => ((sender as Control)?.DataContext as ScreenPieceRow)?.Open();

        // The picture is drawn at twice the screen's size, inside a one-pixel border.
        private void Picture_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (sender is not Control picture || PageOf(sender) is not ScreenTabViewModel page) return;
            Point at = e.GetPosition(picture);
            page.PickAt((int)(at.X / 2), (int)(at.Y / 2));
        }

        private static ScreenTabViewModel ListPageOf(object sender) =>
            (sender as Control)?.FindAncestorOfType<ItemsControl>()?.DataContext as ScreenTabViewModel;

        private void Piece_PointerEntered(object sender, PointerEventArgs e) =>
            ListPageOf(sender)?.PointAt((sender as Control)?.DataContext as ScreenPieceRow);

        private void Piece_PointerExited(object sender, PointerEventArgs e) => ListPageOf(sender)?.PointAt(null);

        // Scrolls the pieces list to the first lit piece, once its rows are laid out.
        private void PieceList_Attached(object sender, VisualTreeAttachmentEventArgs e)
        {
            if (sender is not ItemsControl list || list.Tag != null) return;
            ScreenTabViewModel page = null;
            void Lit(ScreenPieceRow row) =>
                Dispatcher.UIThread.Post(() => list.ContainerFromItem(row)?.BringIntoView(), DispatcherPriority.Background);
            void Follow()
            {
                if (page != null) page.Lit -= Lit;
                page = list.IsAttachedToVisualTree() ? list.DataContext as ScreenTabViewModel : null;
                if (page != null) page.Lit += Lit;
            }
            list.Tag = true;
            list.DataContextChanged += (_, _) => Follow();
            list.AttachedToVisualTree += (_, _) => Follow();
            list.DetachedFromVisualTree += (_, _) => Follow();
            Follow();
        }
        private void OpenApart_Click(object sender, RoutedEventArgs e) => ((sender as Control)?.DataContext as ScreenEditorView)?.OpenApart();
    }
}
