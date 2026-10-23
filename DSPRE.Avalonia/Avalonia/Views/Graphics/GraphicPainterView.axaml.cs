using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;
using DSPRE.Avalonia.Views.Controls;

namespace DSPRE.Avalonia.Views.Graphics
{
    public partial class GraphicPainterView : Window
    {
        private GraphicPainterViewModel ViewModel => (GraphicPainterViewModel)DataContext;

        public GraphicPainterView() : this(null) { }

        public GraphicPainterView(GraphicPainterViewModel vm)
        {
            InitializeComponent();
            if (vm != null) { DataContext = vm; EditorWindowChrome.Attach(this, vm, title: () => vm.Title); }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void Undo_Click(object sender, RoutedEventArgs e) => ViewModel?.Undo();

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            GraphicPainterViewModel vm = ViewModel;
            if (vm == null) return;
            string err = vm.Save();
            if (err != null)
            {
                vm.StatusText = err;
                await DialogHelper.ShowInfoAsync(err, "Save");
                return;
            }
            vm.StatusText = "Saved.";
        }


        /// <summary>Turns a click into the pixel underneath it. The picture is drawn at a whole number of
        /// screen pixels per drawing pixel, so this is a division and nothing more.</summary>
        private bool PixelUnder(PointerEventArgs e, out int x, out int y)
        {
            x = y = 0;
            GraphicPainterViewModel vm = ViewModel;
            Panel host = this.FindControl<Panel>("CanvasHost");
            if (vm == null || host == null || vm.Zoom <= 0) return false;

            Point p = e.GetPosition(host);
            x = (int)(p.X / vm.Zoom);
            y = (int)(p.Y / vm.Zoom);
            return x >= 0 && y >= 0 && x < vm.ViewWidth && y < vm.ViewHeight;
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e) => ViewModel?.ZoomIn();
        private void ZoomOut_Click(object sender, RoutedEventArgs e) => ViewModel?.ZoomOut();

        /// <summary>Ctrl and the wheel zooms, the way it does everywhere else. Without Ctrl the wheel is
        /// left alone so the view still scrolls when the drawing is bigger than the window.</summary>
        private void Canvas_PointerWheelChanged(object sender, PointerWheelEventArgs e)
        {
            GraphicPainterViewModel vm = ViewModel;
            if (vm == null || !e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
            if (e.Delta.Y > 0) vm.ZoomIn(); else if (e.Delta.Y < 0) vm.ZoomOut();
            e.Handled = true;
        }

        private void Canvas_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (PixelUnder(e, out int x, out int y)) ViewModel?.Paint(x, y, startOfStroke: true);
        }

        private void Canvas_PointerMoved(object sender, PointerEventArgs e)
        {
            bool over = PixelUnder(e, out int x, out int y);
            ShowPointer(over ? x : -1, over ? y : -1);
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            if (over) ViewModel?.Paint(x, y, startOfStroke: false);
        }

        private void Canvas_PointerExited(object sender, PointerEventArgs e) => ShowPointer(-1, -1);

        private void ShowPointer(int x, int y)
        {
            this.FindControl<PixelRuler>("TopRuler").Hot = x;
            this.FindControl<PixelRuler>("LeftRuler").Hot = y;
            if (ViewModel != null) ViewModel.Pointer = x < 0 ? "" : $"Column {x}, row {y}";
        }


        private void CanvasScroll_ScrollChanged(object sender, ScrollChangedEventArgs e) => PlaceRulers();
        private void CanvasHost_LayoutUpdated(object sender, EventArgs e) => PlaceRulers();

        /// <summary>Lines pixel 0 on each ruler up with the drawing, wherever scrolling has put it.</summary>
        private void PlaceRulers()
        {
            Panel host = this.FindControl<Panel>("CanvasHost");
            PixelRuler top = this.FindControl<PixelRuler>("TopRuler");
            PixelRuler left = this.FindControl<PixelRuler>("LeftRuler");
            if (host == null || top == null || left == null) return;
            Point? across = host.TranslatePoint(new Point(0, 0), top);
            Point? down = host.TranslatePoint(new Point(0, 0), left);
            if (across is Point a && top.Start != a.X) top.Start = a.X;
            if (down is Point d && left.Start != d.Y) left.Start = d.Y;
        }


        private async void ChangeColour_Click(object sender, RoutedEventArgs e)
        {
            GraphicPainterViewModel vm = ViewModel;
            if (vm?.SelectedSwatch == null)
            {
                await DialogHelper.ShowInfoAsync(vm?.ChangeColourHelp ?? "Pick a colour below first.",
                                            "Change this colour");
                return;
            }
            int number = vm.SelectedSwatch.Number;
            (byte R, byte G, byte B)? now = vm.SelectedSwatch.Fill is global::Avalonia.Media.ISolidColorBrush brush
                ? (brush.Color.R, brush.Color.G, brush.Color.B) : null;
            (byte R, byte G, byte B)? picked = await DialogHelper.PickColourAsync(this, "Change colour " + number, now);
            if (picked == null) return;
            vm.SetColour(number, picked.Value.R, picked.Value.G, picked.Value.B);
        }

        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) (DataContext as DSPRE.Editors.IEditorWithUnsavedChanges)?.DiscardChanges();
        }
    }
}
