using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia
{
    /// <summary>The tile count, merge slider and new-tile outline shown by every tile picture import.</summary>
    public sealed class TileFitPanel
    {
        private readonly TextBlock _count = new TextBlock(), _why = new TextBlock { TextWrapping = TextWrapping.Wrap }, _freed = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap };
        private readonly TextBlock _mergeValue = new TextBlock { Text = "Off", FontSize = 12 };
        private readonly ProgressBar _meter = new ProgressBar { Minimum = 0, Maximum = 100, Height = 12 };
        private readonly CheckBox _showNew = new CheckBox { Content = "Show new tiles" };
        private readonly CheckBox _showChanged = new CheckBox { Content = "Show changed colours" };
        private readonly TextBlock _colourNote = new TextBlock { TextWrapping = TextWrapping.Wrap };
        private readonly WrapPanel _swaps = new WrapPanel();
        private readonly HashSet<ushort> _keep = new();
        private bool _canKeep;
        // Only the most used colours are listed. A noisy picture can have thousands that don't fit.
        private const int ListedSwaps = 24;

        public int Merge { get; private set; }
        public bool ShowNew => _showNew.IsChecked == true;
        public bool ShowChanged => _showChanged.IsChecked == true;
        /// <summary>Colours the user wants given a place first when a row runs out.</summary>
        public IReadOnlyCollection<ushort> Keep => _keep;
        public event Action MergeChanged, ShowNewChanged, KeepChanged;
        public Control View { get; }
        /// <summary>The new colours, the colours that don't fit and what each shows as.</summary>
        public Control ColoursView { get; }

        public TileFitPanel()
        {
            StackPanel panel = new StackPanel { Spacing = 6 };
            StackPanel tiles = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            tiles.Children.Add(new TextBlock { Text = "Tiles", FontWeight = FontWeight.SemiBold });
            tiles.Children.Add(_count);
            panel.Children.Add(tiles);
            panel.Children.Add(_meter);
            panel.Children.Add(_why);
            _showNew.IsCheckedChanged += (_, _) => ShowNewChanged?.Invoke();
            panel.Children.Add(_showNew);
            StackPanel merge = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            merge.Children.Add(new TextBlock { Text = "Merge similar tiles" });
            merge.Children.Add(_mergeValue);
            panel.Children.Add(merge);
            Slider slider = new Slider { Minimum = 0, Maximum = 8, TickFrequency = 1, IsSnapToTickEnabled = true };
            slider.ValueChanged += (_, e) =>
            {
                Merge = (int)e.NewValue;
                _mergeValue.Text = Merge == 0 ? "Off" : Plural.Of(Merge, "pixel");
                MergeChanged?.Invoke();
            };
            panel.Children.Add(slider);
            panel.Children.Add(_freed);
            View = panel;

            StackPanel colours = new StackPanel { Spacing = 6 };
            colours.Children.Add(new TextBlock { Text = "Colours", FontWeight = FontWeight.SemiBold });
            colours.Children.Add(_colourNote);
            _showChanged.IsCheckedChanged += (_, _) => ShowNewChanged?.Invoke();
            colours.Children.Add(_showChanged);
            colours.Children.Add(_swaps);
            ColoursView = colours;
        }

        /// <param name="canKeep">Some row gave places to new colours, so Keep can move one.</param>
        public void ShowColours(int newColours, IReadOnlyList<(ushort Colour, ushort Shown, int Pixels)> swaps, bool canKeep)
        {
            _canKeep = canKeep;
            int over = swaps.Select(w => w.Colour).Distinct().Count();
            string then = canKeep ? " Keep puts a colour in the palette first." : " The palette is full.";
            _colourNote.Text = (newColours == 0 ? "No new colours." : Plural.Of(newColours, "new colour") + ".")
                + (over == 1 ? " 1 colour doesn't fit in the palette and shows as the nearest one." + then
                   : over > 1 ? $" {over} colours don't fit in the palette and show as the nearest ones." + then : "");
            _showChanged.IsVisible = over > 0;
            _swaps.Children.Clear();
            HashSet<ushort> listed = new();
            foreach ((ushort colour, ushort shown, int pixels) in swaps.OrderByDescending(w => w.Pixels))
                if (listed.Count < ListedSwaps && listed.Add(colour)) _swaps.Children.Add(SwapChip(colour, shown, pixels));
            if (over > ListedSwaps) _swaps.Children.Add(new TextBlock { Text = $"and {over - ListedSwaps} more", FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            foreach (ushort kept in _keep.Where(k => !listed.Contains(k)).ToList())
                _swaps.Children.Add(SwapChip(kept, kept, 0));
        }

        private Control SwapChip(ushort colour, ushort shown, int pixels)
        {
            StackPanel chip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new global::Avalonia.Thickness(0, 0, 10, 6) };
            chip.Children.Add(Swatch(colour));
            chip.Children.Add(Swatch(shown));
            ToggleButton keep = new ToggleButton { Content = "Keep", IsChecked = _keep.Contains(colour), FontSize = 12, Padding = new global::Avalonia.Thickness(6, 1), IsVisible = _canKeep || _keep.Contains(colour) };
            keep.IsCheckedChanged += (_, _) =>
            {
                if (keep.IsChecked == true) _keep.Add(colour);
                else _keep.Remove(colour);
                KeepChanged?.Invoke();
            };
            chip.Children.Add(keep);
            ToolTip.SetTip(chip, colour == shown ? $"{Name(colour)} is in the palette" : $"{Name(colour)} shows as {Name(shown)} on {Plural.Of(pixels, "pixel")}");
            return chip;
        }

        private static Border Swatch(ushort colour)
        {
            (byte r, byte g, byte b) = NitroBgCodec.Colour(colour);
            return new Border { Width = 18, Height = 18, BorderThickness = new global::Avalonia.Thickness(1), BorderBrush = Brushes.Gray, Background = new SolidColorBrush(Color.FromRgb(r, g, b)) };
        }

        private static string Name(ushort colour)
        {
            (byte r, byte g, byte b) = NitroBgCodec.Colour(colour);
            return $"#{r:X2}{g:X2}{b:X2}";
        }

        public void Show(int need, int room, int merged, string why)
        {
            _count.Text = $"{need} needed, {room} free";
            _meter.Value = room == 0 ? 100 : Math.Min(100, need * 100.0 / room);
            _meter.Foreground = need <= room ? null : StatusBrushes.Bad;
            _freed.Text = merged > 0 ? $"Frees {Plural.Of(merged, "tile")}, but the picture changes a little." : "";
            _why.Text = why;
        }

        /// <summary>A PNG as RGBA, or null after telling the user why it can't be read.</summary>
        public static async Task<(byte[] Rgba, int Width, int Height)?> ReadPictureAsync(string path, string title)
        {
            byte[] file;
            try { file = File.ReadAllBytes(path); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { await DialogHelper.ShowErrorAsync(ex.Message, title); return null; }
            if (AnyPng.TryReadRgba(file, out byte[] rgba, out int w, out int h, out string whynot)) return (rgba, w, h);
            await DialogHelper.ShowErrorAsync(whynot, title);
            return null;
        }

        /// <summary>The Before and After switch over a preview.</summary>
        public static ToggleButton BeforeAfter(Action changed)
        {
            ToggleButton toggle = new ToggleButton { Content = "After", IsChecked = true };
            toggle.IsCheckedChanged += (_, _) => { toggle.Content = toggle.IsChecked == true ? "After" : "Before"; changed(); };
            return toggle;
        }

        /// <summary>Draws a yellow square around one 8 by 8 square of a picture, or a magenta one for changed colours.</summary>
        public static void Outline(byte[] rgba, int width, int height, int left, int top, bool changedColour = false)
        {
            for (int i = 0; i < 8; i++)
                foreach ((int x, int y) in new[] { (left + i, top), (left + i, top + 7), (left, top + i), (left + 7, top + i) })
                {
                    if (y >= height || x >= width) continue;
                    int at = (y * width + x) * 4;
                    (rgba[at], rgba[at + 1], rgba[at + 2], rgba[at + 3]) = changedColour ? ((byte)255, (byte)0, (byte)255, (byte)255) : ((byte)255, (byte)212, (byte)0, (byte)255);
                }
        }
    }
}
