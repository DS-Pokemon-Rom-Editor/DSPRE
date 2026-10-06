using System;
using System.Threading.Tasks;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using DSPRE.Avalonia.ViewModels.Shell;
using DSPRE.Avalonia.Views.Shell;
using Avalonia.Threading;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Runs slow file work off the UI thread behind a busy card on the window that asked for it, with that
    /// window's content disabled until the work is done.
    /// </summary>
    public static class BusyOverlay
    {
        private static readonly System.Collections.Generic.HashSet<Window> Busy = new();
        // One load at a time, so an editor opened mid-load doesn't race the first.
        private static readonly System.Threading.SemaphoreSlim OneAtATime = new(1, 1);

        /// <summary>Whether the window holding <paramref name="c"/> is behind a busy card right now.</summary>
        public static bool IsBusy(Control c)
        {
            TopLevel top = TopLevel.GetTopLevel(c);
            if (top is MainWindowView { DataContext: MainWindowViewModel vm } && vm.IsBusy) return true;
            return top is Window w && Busy.Contains(w);
        }

        public static async Task RunAsync(string text, string hint, Action work, Window owner = null)
        {
            await OneAtATime.WaitAsync();
            try { await RunOneAsync(text, hint, work, owner); }
            finally { OneAtATime.Release(); }
        }

        /// <summary>Whether file work is running behind any busy card, in any window.</summary>
        public static bool IsWorking => OneAtATime.CurrentCount == 0;

        /// <summary>
        /// Runs <paramref name="work"/> off the UI thread under the same lock as <see cref="RunAsync"/>, with
        /// no card of its own, so a ROM load or build never overlaps an editor's unpacking.
        /// </summary>
        public static async Task<T> RunLockedAsync<T>(Func<T> work)
        {
            await OneAtATime.WaitAsync();
            try { return await Task.Run(work); }
            finally { OneAtATime.Release(); }
        }

        private static async Task RunOneAsync(string text, string hint, Action work, Window owner)
        {
            owner ??= OwnerWindow.Current;
            if (owner is MainWindowView { DataContext: MainWindowViewModel vm })
            {
                vm.BusyText = text; vm.BusyHint = hint; vm.IsBusy = true;
                try { await Task.Run(work); }
                finally { vm.IsBusy = false; }
                return;
            }

            OverlayLayer layer = owner == null ? null : OverlayLayer.GetOverlayLayer(owner);
            if (layer == null) { await Task.Run(work); return; }

            Control content = owner.Content as Control;
            bool wasEnabled = content?.IsEnabled ?? true;
            Border cover = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xA0, 0, 0, 0)),
                Width = layer.Bounds.Width, Height = layer.Bounds.Height,
                Child = Card(owner, text, hint),
            };
            void Resize(object s, SizeChangedEventArgs e) { cover.Width = layer.Bounds.Width; cover.Height = layer.Bounds.Height; }
            owner.SizeChanged += Resize;
            layer.Children.Add(cover);
            if (content != null) content.IsEnabled = false;
            Busy.Add(owner);
            try { await Task.Run(work); }
            finally
            {
                Busy.Remove(owner);
                owner.SizeChanged -= Resize;
                layer.Children.Remove(cover);
                if (content != null) content.IsEnabled = wasEnabled;
            }
        }

        /// <summary>The card itself, readable in both themes.</summary>
        public static Border Card(Control resources, string text, string hint)
        {
            IBrush Res(string key, IBrush fallback) =>
                resources.TryFindResource(key, resources.ActualThemeVariant, out object v) && v is IBrush b ? b : fallback;
            StackPanel stack = new StackPanel { Spacing = 12, Width = 320 };
            stack.Children.Add(new TextBlock { Text = text, FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
            stack.Children.Add(new Panel { ClipToBounds = true, Children = { new DSPRE.Avalonia.Controls.LoadingWalker() } });
            stack.Children.Add(new ProgressBar { IsIndeterminate = true });
            if (!string.IsNullOrEmpty(hint))
                stack.Children.Add(new TextBlock { Text = hint, FontSize = 12, Opacity = 0.8, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
            TextBlock fact = new TextBlock { Text = PokeFacts.Next(), FontSize = 12, FontStyle = FontStyle.Italic, Opacity = 0.7,
                                       TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };
            fact.IsVisible = fact.Text.Length > 0;
            stack.Children.Add(fact);
            DispatcherTimer timer = new global::Avalonia.Threading.DispatcherTimer { Interval = PokeFacts.Interval };
            timer.Tick += (_, _) => fact.Text = PokeFacts.Next();
            fact.AttachedToVisualTree += (_, _) => timer.Start();
            fact.DetachedFromVisualTree += (_, _) => timer.Stop();
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
