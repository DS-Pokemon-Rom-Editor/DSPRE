using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DSPRE.Avalonia.ViewModels;
using System;

namespace DSPRE.Avalonia.Views.World
{
    public partial class FlyEditorView : Window
    {
        private FlyEditorViewModel _vm;

        public FlyEditorView(System.Collections.Generic.List<string> headerNames)
        {
            AvaloniaXamlLoader.Load(this);
            _vm = new FlyEditorViewModel(headerNames);
            DataContext = _vm;
            // VM owns the bound Title (+ "●" marker); chrome adds Ctrl+S + the close guard.
            EditorWindowChrome.Attach(this, _vm, title: () => _vm.Title);

            // A zero width still leaves a sliver with a live cell in it, so the other family's columns are hidden.
            DataGrid unlock = this.FindControl<DataGrid>("UnlockGrid");
            for (int i = 0; unlock != null && i < unlock.Columns.Count; i++)
                unlock.Columns[i].IsVisible = i < 3 ? _vm.IsDpOrPlat : _vm.IsHgss;
        }

        public FlyEditorView()
        {
            AvaloniaXamlLoader.Load(this);
            if (Design.IsDesignMode)
            {
                _vm = new FlyEditorViewModel();
                DataContext = _vm;
                return;
            }
            throw new InvalidOperationException("Parameterless constructor only for design time.");
        }

        private FlySpotRow _dragging;
        private global::Avalonia.Point _dragFrom;
        private (int X, int Y) _dragStart;

        private void Spot_PointerPressed(object sender, global::Avalonia.Input.PointerPressedEventArgs e)
        {
            if (sender is not global::Avalonia.Controls.Border { Tag: FlySpotRow spot } border) return;
            _dragging = spot;
            _dragFrom = e.GetPosition(this.FindControl<Panel>("TownMapPanel"));
            _dragStart = (spot.X, spot.Y);
            _vm.BeginStep();
            e.Pointer.Capture(border);
            e.Handled = true;
        }

        private void Spot_PointerMoved(object sender, global::Avalonia.Input.PointerEventArgs e)
        {
            if (_dragging == null) return;
            global::Avalonia.Point now = e.GetPosition(this.FindControl<Panel>("TownMapPanel"));
            double tile = 8 * FlySpotRow.Scale;
            int dx = (int)Math.Round((now.X - _dragFrom.X) / tile), dy = (int)Math.Round((now.Y - _dragFrom.Y) / tile);
            _vm.MoveSpot(_dragging, _dragStart.X + dx, _dragStart.Y + dy);
        }

        private void Spot_PointerReleased(object sender, global::Avalonia.Input.PointerReleasedEventArgs e)
        {
            _dragging = null;
            _vm.EndStep();
            e.Pointer.Capture(null);
        }

        private void FlyRow_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Recycled rows change the selection too, so only the focused box links.
            if (sender is ComboBox { Tag: FlySpotRow spot, IsKeyboardFocusWithin: true } box && box.SelectedIndex >= 0 && box.SelectedIndex != spot.FlyRow)
                _vm.LinkSpot(spot, box.SelectedIndex);
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
            => await _vm.SaveCommandAsync();

        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) _vm?.DiscardChanges();
        }
    }
}
