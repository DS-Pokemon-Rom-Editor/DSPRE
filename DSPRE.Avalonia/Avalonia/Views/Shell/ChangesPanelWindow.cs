using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using DSPRE.Avalonia.ViewModels.Shell;

namespace DSPRE.Avalonia.Views.Shell
{
    /// <summary>The changes panel held against the main window's edge, so it never takes width from the editors.</summary>
    public sealed class ChangesPanelWindow : Window
    {
        private const double PanelWidth = 322;

        private readonly Window _main;
        private readonly ChangesViewModel _vm;
        private readonly Control _overlay;

        public static void Attach(Window main, ChangesViewModel vm, Control overlay)
        {
            if (main == null || vm == null) return;
            ChangesPanelWindow window = new ChangesPanelWindow(main, vm, overlay);
            vm.PropertyChanged += window.OnVmChanged;
            main.PositionChanged += (_, _) => window.Place();
            main.Resized += (_, _) => window.Place();
            main.PropertyChanged += (_, e) => { if (e.Property == WindowStateProperty) window.Place(); };
            main.Closing += (_, _) => window.Close();
        }

        private ChangesPanelWindow(Window main, ChangesViewModel vm, Control overlay)
        {
            _main = main;
            _vm = vm;
            _overlay = overlay;
            Title = "Changes";
            WindowDecorations = WindowDecorations.None;
            ShowInTaskbar = false;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Width = PanelWidth;
            Content = new ChangesPanel { Open = true, DataContext = vm };
        }

        private void OnVmChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(ChangesViewModel.PanelOpen) or nameof(ChangesViewModel.IsTracking) or null) Place();
        }

        // When the main window is maximized or there's no room on either side, the panel covers the editors.
        private void Place()
        {
            bool want = _vm.PanelOpen && _vm.IsTracking && _main.IsVisible && _main.WindowState != WindowState.Minimized;
            PixelPoint? spot = want ? Beside() : null;
            _overlay.IsVisible = want && spot == null;
            if (spot == null)
            {
                if (IsVisible) Hide();
                return;
            }
            Height = _main.FrameSize?.Height ?? _main.Bounds.Height;
            Position = spot.Value;
            if (!IsVisible) Show(_main);
        }

        /// <summary>Right of the main window, else left of it; null when it fits on neither side.</summary>
        private PixelPoint? Beside()
        {
            if (_main.WindowState != WindowState.Normal) return null;
            Screen screen = _main.Screens?.ScreenFromWindow(_main);
            if (screen == null) return null;
            double scale = _main.RenderScaling;
            Size frame = _main.FrameSize ?? _main.Bounds.Size;
            int width = (int)Math.Ceiling(PanelWidth * scale);
            int left = _main.Position.X, right = left + (int)Math.Ceiling(frame.Width * scale);
            PixelRect area = screen.WorkingArea;
            if (right + width <= area.Right) return new PixelPoint(right, _main.Position.Y);
            if (left - width >= area.X) return new PixelPoint(left - width, _main.Position.Y);
            return null;
        }
    }
}
