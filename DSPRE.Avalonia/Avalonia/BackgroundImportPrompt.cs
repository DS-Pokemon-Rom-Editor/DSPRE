using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia
{
    /// <summary>Fits a picture to a whole background and shows it before anything is written, as the button import does.</summary>
    public sealed class BackgroundImportPrompt
    {
        private readonly ScreenLayerFit _fit;
        private readonly byte[] _rgba;
        private readonly int _width, _height;
        private readonly byte[] _before;
        private readonly TileFitPanel _fitPanel = new TileFitPanel();
        private readonly Image _main = new Image { Stretch = Stretch.Uniform };
        private readonly ToggleButton _showAfter;
        private readonly Button _import = new Button { Content = "Import" };
        private readonly DispatcherTimer _redraw = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        private readonly Func<ScreenLayerFit.Plan, IReadOnlyList<(string Name, byte[] Before, byte[] After)>> _pagesOf;
        private readonly WrapPanel _cards = new WrapPanel();
        private IReadOnlyList<(string Name, byte[] Before, byte[] After)> _pages = Array.Empty<(string, byte[], byte[])>();
        private ScreenLayerFit.Plan _plan;
        private byte[] _after;
        private const int ScreenWidth = 256, ScreenHeight = 192;

        private BackgroundImportPrompt(ScreenLayerFit fit, byte[] rgba, int width, int height, Func<ScreenLayerFit.Plan, IReadOnlyList<(string, byte[], byte[])>> pagesOf)
        {
            (_fit, _rgba, _width, _height, _pagesOf) = (fit, rgba, width, height, pagesOf);
            _showAfter = TileFitPanel.BeforeAfter(ShowMain);
            _before = fit.Picture();
            RenderOptions.SetBitmapInterpolationMode(_main, BitmapInterpolationMode.None);
        }

        /// <summary>Returns the plan to write, or null when cancelled.</summary>
        /// <param name="pagesOf">Every page the background reaches, drawn now and with a plan.</param>
        public static async Task<ScreenLayerFit.Plan> AskAsync(Window owner, ScreenLayerFit fit, string path, string title,
                                                               Func<ScreenLayerFit.Plan, IReadOnlyList<(string, byte[], byte[])>> pagesOf)
        {
            if (await TileFitPanel.ReadPictureAsync(path, title) is not (byte[] rgba, int w, int h)) return null;
            if (fit.SizeProblem(w, h) is string size)
            {
                await DialogHelper.ShowErrorAsync(size, title);
                return null;
            }
            return await new BackgroundImportPrompt(fit, rgba, w, h, pagesOf).ShowAsync(owner, title);
        }

        private async Task<ScreenLayerFit.Plan> ShowAsync(Window owner, string title)
        {
            bool imported = false;
            Window win = new Window { Title = title, Width = 1180, Height = 760, WindowStartupLocation = WindowStartupLocation.CenterOwner, MinWidth = 900, MinHeight = 500 };
            _import.Click += (_, _) => { imported = true; win.Close(); };
            Button cancel = new Button { Content = "Cancel", IsCancel = true };
            cancel.Click += (_, _) => win.Close();
            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 6, Margin = new global::Avalonia.Thickness(0, 10, 0, 0) };
            buttons.Children.Add(_import);
            buttons.Children.Add(cancel);

            StackPanel left = new StackPanel { Spacing = 6 };
            left.Children.Add(new Viewbox { Stretch = Stretch.Uniform, MaxWidth = _fit.Width * 2, Child = _main });
            _fitPanel.ShowNewChanged += ShowMain;
            _fitPanel.MergeChanged += Refit;
            _fitPanel.KeepChanged += Refit;
            left.Children.Add(_fitPanel.View);

            StackPanel right = new StackPanel { Spacing = 6 };
            right.Children.Add(new TextBlock { Text = "Picture", FontWeight = FontWeight.SemiBold });
            right.Children.Add(_showAfter);
            right.Children.Add(_fitPanel.ColoursView);

            Grid top = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 16 };
            Grid.SetColumn(right, 1);
            top.Children.Add(left);
            top.Children.Add(right);
            StackPanel all = new StackPanel { Spacing = 12 };
            all.Children.Add(top);
            all.Children.Add(new TextBlock { Text = "Affected screens", FontWeight = FontWeight.SemiBold });
            all.Children.Add(_cards);
            ScrollViewer scroll = new ScrollViewer { Content = all };
            DockPanel body = DialogSizing.Body(scroll, buttons);
            body.Margin = new global::Avalonia.Thickness(16);
            win.Content = body;
            DialogSizing.Grow(win, scroll);

            _redraw.Tick += (_, _) =>
            {
                _redraw.Stop();
                _after = _plan?.Fits == true ? _fit.Picture(_plan) : null;
                _pages = _pagesOf?.Invoke(_plan?.Fits == true ? _plan : null) ?? Array.Empty<(string, byte[], byte[])>();
                ShowMain();
            };
            Refit();
            await win.ShowDialog(owner);
            _redraw.Stop();
            return imported && _plan?.Fits == true ? _plan : null;
        }

        private void Refit()
        {
            _plan = _fit.Make(_rgba, _width, _height, _fitPanel.Merge, _fitPanel.Keep);
            string why = _plan.Need > _plan.Room
                ? $"{Plural.Of(_plan.Need - _plan.Room, "tile")} too many. Merge similar tiles or simplify the picture."
                : $"Fits, with {Plural.Of(_plan.Room - _plan.Need, "tile")} to spare.";
            _fitPanel.Show(_plan.Need, _plan.Room, _plan.Merged, why);
            _fitPanel.ShowColours(_plan.NewColours, _plan.Swaps, _plan.CanKeep);
            _import.IsEnabled = _plan.Fits;
            ToolTip.SetTip(_import, _plan.Fits ? null : why);
            _redraw.Stop();
            _redraw.Start();
        }

        private void ShowMain()
        {
            bool after = _showAfter.IsChecked == true && _after != null;
            byte[] rgba = after ? _after : _before;
            if (rgba == null) return;
            if (after && _plan != null && (_fitPanel.ShowNew || _fitPanel.ShowChanged))
            {
                rgba = (byte[])rgba.Clone();
                int cols = _fit.Width / 8;
                if (_fitPanel.ShowNew)
                    foreach (int square in _plan.NewSquares) TileFitPanel.Outline(rgba, _fit.Width, _fit.ShownHeight, square % cols * 8, square / cols * 8);
                if (_fitPanel.ShowChanged)
                    foreach (int square in _plan.ChangedSquares) TileFitPanel.Outline(rgba, _fit.Width, _fit.ShownHeight, square % cols * 8, square / cols * 8, changedColour: true);
            }
            _main.Source = ImageConverter.FromRgba(rgba, _fit.Width, _fit.ShownHeight);
            ShowCards();
        }

        private void ShowCards()
        {
            bool after = _showAfter.IsChecked == true;
            _cards.Children.Clear();
            foreach ((string name, byte[] before, byte[] drawn) in _pages)
            {
                byte[] shown = after && drawn != null ? drawn : before;
                if (shown == null) continue;
                Image image = new Image { Width = ScreenWidth, Height = ScreenHeight, Stretch = Stretch.Uniform, Source = ImageConverter.FromRgba(shown, ScreenWidth, ScreenHeight) };
                RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.None);
                StackPanel card = new StackPanel { Spacing = 2, Margin = new global::Avalonia.Thickness(0, 0, 8, 8) };
                card.Children.Add(image);
                card.Children.Add(new TextBlock { Text = name, FontWeight = FontWeight.SemiBold });
                bool changes = drawn != null && before != null && !drawn.AsSpan().SequenceEqual(before);
                card.Children.Add(new TextBlock { Text = !after ? "Current" : _plan?.Fits != true ? "Doesn't fit" : changes ? "Changes" : "No change", FontSize = 12 });
                _cards.Children.Add(card);
            }
        }
    }
}
