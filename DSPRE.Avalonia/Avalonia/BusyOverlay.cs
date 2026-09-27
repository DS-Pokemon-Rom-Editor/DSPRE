using System;
using System.Threading.Tasks;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using DSPRE.Avalonia.ViewModels.Shell;
using DSPRE.Avalonia.Views.Shell;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Runs slow file work off the UI thread behind a "busy" card on the window that asked for it, so the
    /// wait shows where the click happened. The main window has its own card; any other window gets one in
    /// its overlay layer, and its content is disabled until the work is done.
    /// </summary>
    public static class BusyOverlay
    {
        public static async Task RunAsync(string text, string hint, Action work, Window owner = null)
        {
            owner ??= OwnerWindow.Current;
            if (owner is MainWindowView { DataContext: MainWindowViewModel vm })
            {
                vm.BusyText = text; vm.BusyHint = hint; vm.IsBusy = true;
                try { await Task.Run(work); }
                finally { vm.IsBusy = false; }
                return;
            }

            var layer = owner == null ? null : OverlayLayer.GetOverlayLayer(owner);
            if (layer == null) { await Task.Run(work); return; }

            var content = owner.Content as Control;
            bool wasEnabled = content?.IsEnabled ?? true;
            var cover = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xA0, 0, 0, 0)),
                Width = layer.Bounds.Width, Height = layer.Bounds.Height,
                Child = Card(owner, text, hint),
            };
            void Resize(object s, SizeChangedEventArgs e) { cover.Width = layer.Bounds.Width; cover.Height = layer.Bounds.Height; }
            owner.SizeChanged += Resize;
            layer.Children.Add(cover);
            if (content != null) content.IsEnabled = false;
            try { await Task.Run(work); }
            finally
            {
                owner.SizeChanged -= Resize;
                layer.Children.Remove(cover);
                if (content != null) content.IsEnabled = wasEnabled;
            }
        }

        /// <summary>The card itself, readable in both themes.</summary>
        public static Border Card(Control resources, string text, string hint)
        {
            IBrush Res(string key, IBrush fallback) =>
                resources.TryFindResource(key, resources.ActualThemeVariant, out var v) && v is IBrush b ? b : fallback;
            var stack = new StackPanel { Spacing = 12, Width = 320 };
            stack.Children.Add(new TextBlock { Text = text, FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
            stack.Children.Add(new ProgressBar { IsIndeterminate = true });
            if (!string.IsNullOrEmpty(hint))
                stack.Children.Add(new TextBlock { Text = hint, FontSize = 12, Opacity = 0.8, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
            return new Border
            {
                Background = Res("Editor.PanelBg", Brushes.Gray),
                BorderBrush = Res("Editor.Border", Brushes.DimGray),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(20, 16),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = stack,
            };
        }
    }
}
