using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Graphics
{
    public partial class GraphicPainterView : Window
    {
        private GraphicPainterViewModel ViewModel => (GraphicPainterViewModel)DataContext;

        public GraphicPainterView() : this(null) { }

        public GraphicPainterView(GraphicPainterViewModel vm)
        {
            InitializeComponent();
            if (vm != null) DataContext = vm;
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void Undo_Click(object sender, RoutedEventArgs e) => ViewModel?.Undo();

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            if (vm == null) return;
            string err = vm.Save();
            if (err != null)
            {
                vm.Status = err;
                await DialogHelper.ShowInfo(err, "Save into the game");
                return;
            }
            vm.Status = "Saved into the game. Save the ROM to keep it.";
        }

        // ── painting ───────────────────────────────────────────────────────────────────────────────

        /// <summary>Turns a click into the pixel underneath it. The picture is drawn at a whole number of
        /// screen pixels per drawing pixel, so this is a division and nothing more.</summary>
        private bool PixelUnder(PointerEventArgs e, out int x, out int y)
        {
            x = y = 0;
            var vm = ViewModel;
            var host = this.FindControl<Panel>("CanvasHost");
            if (vm == null || host == null || vm.Zoom <= 0) return false;

            var p = e.GetPosition(host);
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
            var vm = ViewModel;
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
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            if (PixelUnder(e, out int x, out int y)) ViewModel?.Paint(x, y, startOfStroke: false);
        }

        // ── the colours ────────────────────────────────────────────────────────────────────────────

        private async void ChangeColour_Click(object sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            if (vm?.SelectedSwatch == null)
            {
                await DialogHelper.ShowInfo(vm?.ChangeColourHelp ?? "Pick a colour below first.",
                                            "Change this colour");
                return;
            }
            int number = vm.SelectedSwatch.Number;
            var picked = await DialogHelper.PickColour(this, "Change colour " + number);
            if (picked == null) return;
            vm.SetColour(number, picked.Value.R, picked.Value.G, picked.Value.B);
        }
    }
}
