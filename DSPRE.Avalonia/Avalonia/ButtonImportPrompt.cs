using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia
{
    /// <summary>Fits a picture to the battle buttons and shows every screen it reaches before anything is written.</summary>
    public sealed class ButtonImportPrompt
    {
        public sealed record Result(TouchButtonFit Fit, TouchButtonFit.Plan Plan);

        private const int ScreenWidth = BattleScreenRenderer.ScreenWidth, ScreenHeight = BattleScreenRenderer.ScreenHeight;
        private const string GameFlash = "Game's flash", LighterArt = "Lighter art";

        private sealed record Card(string Name, int Page, bool Frontier, BattlePressFrames.Rect Press, int Frame, int[] Buttons)
        {
            public Image Image;
            public TextBlock Status;
            public byte[] Before;
        }

        private readonly TouchButtonFit _fit;
        private readonly byte[][] _whole = new byte[TouchButtonFit.ButtonCount][];
        private readonly TouchButtonFit.Settings _settings = new TouchButtonFit.Settings();
        private readonly bool[] _on = { true, true, true, true };
        private readonly List<Card> _cards = new List<Card>();
        private readonly Func<int, int, IReadOnlyList<(int X, int Y)>> _textOf;
        private FightIconView _icon;
        private readonly TextBlock[] _tiles = new TextBlock[TouchButtonFit.ButtonCount], _notes = new TextBlock[TouchButtonFit.ButtonCount];
        private readonly Image _main = NewImage();
        private readonly TextBlock _count = new TextBlock(), _why = new TextBlock { TextWrapping = TextWrapping.Wrap }, _freed = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap };
        private readonly TextBlock _mergeValue = new TextBlock { Text = "Off", FontSize = 12 };
        private readonly ProgressBar _meter = new ProgressBar { Minimum = 0, Maximum = 100, Height = 12 };
        private readonly CheckBox _showNew = new CheckBox { Content = "Show new tiles" };
        private readonly Button _import = new Button { Content = "Import" };
        private readonly ComboBox _flashFor = Choices(TouchButtonFit.Names);
        private readonly TextBlock _flashNote = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap };
        private readonly string[] _flashNames = { "Automatic", "Automatic", "Automatic", "Automatic" };
        private readonly ToggleButton _showAfter = new ToggleButton { Content = "After", IsChecked = true };
        private readonly DispatcherTimer _redraw = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        private readonly Dictionary<Card, byte[]> _after = new();
        private TouchButtonFit.Plan _plan;
        private Window _win;
        private bool _ready;
        private byte[] _mainAfter;

        private ButtonImportPrompt(TouchButtonFit fit, byte[] rgba, int width)
        {
            _fit = fit;
            _textOf = LabelSpots();
            for (int b = 0; b < TouchButtonFit.ButtonCount; b++)
            {
                _whole[b] = TouchButtonFit.CutButton(rgba, width, b);
                _settings.Pictures[b] = _whole[b];
            }
            _settings.Whole = rgba;
            _settings.WholeWidth = width;
        }

        private Func<int, int, IReadOnlyList<(int X, int Y)>> LabelSpots()
        {
            if (RomLocator.BattleCommandSites == null) return null;
            BattleCommandButtons buttons = BattleCommandButtons.Read();
            IReadOnlyList<(int X, int Y)>[][] text = Enumerable.Range(0, buttons.PageNames.Count)
                .Select(p => Enumerable.Range(0, buttons.ButtonsOf(p).Length).Select(b => buttons.TextOf(p, b)).ToArray()).ToArray();
            _icon = buttons.IconView;
            return (p, b) => text[p][b];
        }

        /// <summary>Returns what to import, or null when cancelled.</summary>
        public static async Task<Result> AskAsync(Window owner, TouchButtonFit fit, string path)
        {
            byte[] file;
            try { file = File.ReadAllBytes(path); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { await DialogHelper.ShowErrorAsync(ex.Message, "Import buttons"); return null; }
            if (!AnyPng.TryReadRgba(file, out byte[] rgba, out int w, out int h, out string whynot))
            {
                await DialogHelper.ShowErrorAsync(whynot, "Import buttons");
                return null;
            }
            if (w != ScreenWidth || (h != ScreenHeight && h != ScreenWidth))
            {
                await DialogHelper.ShowErrorAsync($"{Path.GetFileName(path)} needs to be 256 by 192 or 256 by 256.", "Import buttons");
                return null;
            }
            ButtonImportPrompt prompt = new ButtonImportPrompt(fit, rgba, w);
            return await prompt.ShowAsync(owner);
        }

        private async Task<Result> ShowAsync(Window owner)
        {
            bool imported = false;
            _win = new Window { Title = "Import buttons", Width = 1180, Height = 900, WindowStartupLocation = WindowStartupLocation.CenterOwner, MinWidth = 900, MinHeight = 600 };
            _import.Click += (_, _) => { imported = true; _win.Close(); };
            Button cancel = new Button { Content = "Cancel", IsCancel = true };
            cancel.Click += (_, _) => _win.Close();
            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 6, Margin = new global::Avalonia.Thickness(0, 10, 0, 0) };
            buttons.Children.Add(_import);
            buttons.Children.Add(cancel);

            Grid top = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 16 };
            Control left = Left(), right = Right();
            Grid.SetColumn(right, 1);
            top.Children.Add(left);
            top.Children.Add(right);

            StackPanel all = new StackPanel { Spacing = 12 };
            all.Children.Add(top);
            all.Children.Add(Screens());

            ScrollViewer scroll = new ScrollViewer { Content = all };
            DockPanel body = DialogSizing.Body(scroll, buttons);
            body.Margin = new global::Avalonia.Thickness(16);
            _win.Content = body;
            DialogSizing.Grow(_win, scroll);

            foreach (Card c in _cards) c.Before = Draw(c);
            _redraw.Tick += (_, _) => { _redraw.Stop(); DrawCards(); };
            _ready = true;
            Refit();
            DrawCards();
            await _win.ShowDialog(owner);
            _redraw.Stop();
            return imported && _plan?.Fits == true ? new Result(_fit, _plan) : null;
        }

        private static Image NewImage()
        {
            Image image = new Image { Width = ScreenWidth * 2, Height = ScreenHeight * 2, Stretch = Stretch.Uniform };
            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.None);
            return image;
        }

        private static ComboBox Choices(params string[] items) =>
            new ComboBox { ItemsSource = items, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };

        private static TextBlock Heading(string text) => new TextBlock { Text = text, FontWeight = FontWeight.SemiBold };

        private Control Left()
        {
            StackPanel left = new StackPanel { Spacing = 6 };
            left.Children.Add(new Viewbox { Stretch = Stretch.Uniform, MaxWidth = ScreenWidth * 2, Child = _main });
            StackPanel tiles = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            tiles.Children.Add(Heading("Tiles"));
            tiles.Children.Add(_count);
            left.Children.Add(tiles);
            left.Children.Add(_meter);
            left.Children.Add(_why);
            _showNew.IsCheckedChanged += (_, _) => ShowMain();
            left.Children.Add(_showNew);
            StackPanel merge = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            merge.Children.Add(new TextBlock { Text = "Merge similar tiles" });
            merge.Children.Add(_mergeValue);
            left.Children.Add(merge);
            Slider slider = new Slider { Minimum = 0, Maximum = 8, TickFrequency = 1, IsSnapToTickEnabled = true };
            slider.ValueChanged += (_, e) =>
            {
                _settings.Merge = (int)e.NewValue;
                _mergeValue.Text = _settings.Merge == 0 ? "Off" : Plural.Of(_settings.Merge, "pixel");
                Refit();
            };
            left.Children.Add(slider);
            left.Children.Add(_freed);
            return left;
        }

        private Control Right()
        {
            StackPanel right = new StackPanel { Spacing = 6 };
            right.Children.Add(Heading("Buttons"));
            for (int b = 0; b < TouchButtonFit.ButtonCount; b++) right.Children.Add(ButtonRow(b));

            right.Children.Add(Heading("Flash colours"));
            right.Children.Add(_flashFor);
            _flashFor.SelectionChanged += (_, _) => ShowFlashNote();
            WrapPanel looks = new WrapPanel();
            looks.Children.Add(FlashChoice(GameFlash, () => SetFlash(TouchButtonFit.FlashKind.Game, null, GameFlash)));
            looks.Children.Add(FlashChoice(LighterArt, () => SetFlash(TouchButtonFit.FlashKind.Lighter, null, LighterArt)));
            Button any = new Button { Content = "Any colour…", Margin = new global::Avalonia.Thickness(0, 0, 4, 4) };
            any.Click += async (_, _) =>
            {
                (byte R, byte G, byte B)? c = await DialogHelper.PickColourAsync(_win, "Flash colour");
                if (c is (byte r, byte g, byte b)) SetFlash(TouchButtonFit.FlashKind.Colour, _fit.FromColour(NitroBgCodec.ToBgr555(r, g, b)), $"#{r:X2}{g:X2}{b:X2}");
            };
            looks.Children.Add(any);
            right.Children.Add(looks);
            WrapPanel swatches = new WrapPanel();
            foreach (ushort colour in _fit.ButtonColours()) swatches.Children.Add(Swatch(colour));
            right.Children.Add(swatches);
            right.Children.Add(_flashNote);
            ShowFlashNote();
            return right;
        }

        private static Button FlashChoice(string text, Action picked)
        {
            Button b = new Button { Content = text, Margin = new global::Avalonia.Thickness(0, 0, 4, 4) };
            b.Click += (_, _) => picked();
            return b;
        }

        private Button Swatch(ushort colour)
        {
            (byte r, byte g, byte b) = NitroBgCodec.Colour(colour);
            Button swatch = new Button { Width = 22, Height = 22, Padding = new global::Avalonia.Thickness(0), Margin = new global::Avalonia.Thickness(0, 0, 3, 3), Background = new SolidColorBrush(Color.FromRgb(r, g, b)) };
            string name = $"#{r:X2}{g:X2}{b:X2}";
            ToolTip.SetTip(swatch, name);
            swatch.Click += (_, _) => SetFlash(TouchButtonFit.FlashKind.Colour, _fit.FromColour(colour), name);
            return swatch;
        }

        private void SetFlash(TouchButtonFit.FlashKind kind, ushort[] colours, string name)
        {
            int which = _flashFor.SelectedIndex;
            _flashNames[which] = name;
            _settings.Kind[which] = kind;
            _settings.Flash[which] = colours;
            ShowFlashNote();
            Refit();
        }

        private void ShowFlashNote()
        {
            int which = _flashFor.SelectedIndex;
            _flashNote.Text = $"{TouchButtonFit.Names[which]}: {_flashNames[which]}";
        }

        private Control ButtonRow(int b)
        {
            (_, _, int w, int h) = TouchButtonFit.PixelRect(b);
            Grid row = new Grid { ColumnDefinitions = new ColumnDefinitions("110,70,*,Auto"), ColumnSpacing = 6, RowDefinitions = new RowDefinitions("Auto,Auto") };
            CheckBox use = new CheckBox { Content = TouchButtonFit.Names[b], IsChecked = true };
            use.IsCheckedChanged += (_, _) => { _on[b] = use.IsChecked == true; _settings.Pictures[b] = _on[b] ? _whole[b] : null; Refit(); };
            _tiles[b] = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
            Button png = new Button { Content = "PNG…" };
            ToolTip.SetTip(png, $"{w} by {h}");
            png.Click += async (_, _) => await PickButtonPngAsync(b, use);
            _notes[b] = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap };
            Grid.SetColumn(_tiles[b], 1);
            Grid.SetColumn(png, 3);
            Grid.SetRow(_notes[b], 1);
            Grid.SetColumnSpan(_notes[b], 4);
            row.Children.Add(use);
            row.Children.Add(_tiles[b]);
            row.Children.Add(png);
            row.Children.Add(_notes[b]);
            return row;
        }

        private async Task PickButtonPngAsync(int b, CheckBox use)
        {
            string path = await DialogHelper.OpenFileAsync(_win, "Import PNG",
                new[] { new global::Avalonia.Platform.Storage.FilePickerFileType("PNG image") { Patterns = new[] { "*.png" } } });
            if (path == null) return;
            (_, _, int w, int h) = TouchButtonFit.PixelRect(b);
            byte[] file;
            try { file = File.ReadAllBytes(path); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { await DialogHelper.ShowErrorAsync(ex.Message, "Import buttons"); return; }
            if (!AnyPng.TryReadRgba(file, out byte[] rgba, out int pw, out int ph, out string whynot)) { await DialogHelper.ShowErrorAsync(whynot, "Import buttons"); return; }
            if (pw != w || ph != h)
            {
                await DialogHelper.ShowErrorAsync($"{Path.GetFileName(path)} is {pw} by {ph}. {TouchButtonFit.Names[b]} needs {w} by {h}.", "Import buttons");
                return;
            }
            _whole[b] = rgba;
            use.IsChecked = true;
            _settings.Pictures[b] = rgba;
            Refit();
        }

        private void Refit()
        {
            if (!_ready) return;
            _plan = _fit.Make(_settings);
            for (int b = 0; b < TouchButtonFit.ButtonCount; b++)
            {
                _tiles[b].Text = _on[b] ? Plural.Of(_plan.Tiles[b], "tile") : "";
                _notes[b].Text = !_on[b] ? "No change."
                    : _plan.TooManyColours[b] == 1 ? "1 colour doesn't fit."
                    : _plan.TooManyColours[b] > 1 ? $"{_plan.TooManyColours[b]} colours don't fit."
                    : _plan.NewColours[b] > 0 ? $"{Changing(_plan.Squares[b])} {Plural.Of(_plan.NewColours[b], "new colour")}."
                    : Changing(_plan.Squares[b]);
            }
            _count.Text = $"{_plan.Need} needed, {_plan.Room} free";
            _meter.Value = _plan.Room == 0 ? 100 : Math.Min(100, _plan.Need * 100.0 / _plan.Room);
            _meter.Foreground = _plan.Need <= _plan.Room ? null : StatusBrushes.Bad;
            _freed.Text = _plan.Merged > 0 ? $"Frees {Plural.Of(_plan.Merged, "tile")}, but the picture changes a little." : "";
            _why.Text = Why();
            _import.IsEnabled = _plan.Fits && _on.Any(x => x);
            ToolTip.SetTip(_import, !_on.Any(x => x) ? "Pick a button" : _plan.Fits ? null : _why.Text);
            _redraw.Stop();
            _redraw.Start();
        }

        private static string Changing(int squares) => squares == 1 ? "1 square changes." : $"{squares} squares change.";

        private string Why()
        {
            if (!_on.Any(x => x)) return "";
            string[] over = Enumerable.Range(0, TouchButtonFit.ButtonCount).Where(b => _plan.TooManyColours[b] > 0).Select(b => TouchButtonFit.Names[b]).ToArray();
            if (over.Length > 0)
            {
                string names = string.Join(", ", over);
                return $"Too many colours on {names}. Each button can have 15 in total.";
            }
            if (_plan.Need > _plan.Room)
            {
                string most = TouchButtonFit.Names[Array.IndexOf(_plan.Tiles, _plan.Tiles.Max())];
                return $"{Plural.Of(_plan.Need - _plan.Room, "tile")} over. {most} uses the most. Try merging tiles.";
            }
            return $"Fits, with {Plural.Of(_plan.Room - _plan.Need, "tile")} to spare.";
        }

        private Control Screens()
        {
            StackPanel all = new StackPanel { Spacing = 6 };
            StackPanel head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            head.Children.Add(Heading("Screens affected"));
            _showAfter.IsCheckedChanged += (_, _) => { _showAfter.Content = _showAfter.IsChecked == true ? "After" : "Before"; ShowCards(); };
            head.Children.Add(_showAfter);
            all.Children.Add(head);

            int[] everyButton = { 0, 1, 2, 3 };
            _cards.Add(new Card("Command", 0, false, null, 0, everyButton));
            _cards.Add(new Card("First battle", 4, false, null, 0, new[] { 0 }));
            _cards.Add(new Card("Pal Park", 5, false, null, 0, new[] { 0, 3 }));
            _cards.Add(new Card("Battle Frontier", 0, true, null, 0, everyButton));
            for (int b = 0; b < TouchButtonFit.ButtonCount; b++)
            {
                BattlePressFrames.Rect rect = BattlePressFrames.CommandRects[b];
                _cards.Add(new Card($"{TouchButtonFit.Names[b]} press, first frame", 0, false, rect, BattlePressFrames.WhitePage, new[] { b }));
                _cards.Add(new Card($"{TouchButtonFit.Names[b]} press, second frame", 0, false, rect, BattlePressFrames.PalePage, new[] { b }));
            }
            _cards.Add(new Card("Frontier Fight press, first frame", 0, true, BattlePressFrames.CommandRects[0], BattlePressFrames.WhitePage, new[] { 0 }));
            _cards.Add(new Card("Frontier Fight press, second frame", 0, true, BattlePressFrames.CommandRects[0], BattlePressFrames.PalePage, new[] { 0 }));

            UniformGrid grid = new UniformGrid { Columns = 4 };
            foreach (Card c in _cards)
            {
                c.Image = NewImage();
                c.Image.Width = ScreenWidth;
                c.Image.Height = ScreenHeight;
                c.Status = new TextBlock { FontSize = 12 };
                StackPanel one = new StackPanel { Spacing = 2, Margin = new global::Avalonia.Thickness(0, 0, 8, 8) };
                one.Children.Add(new Viewbox { Stretch = Stretch.Uniform, Child = c.Image });
                one.Children.Add(new TextBlock { Text = c.Name, FontWeight = FontWeight.SemiBold });
                one.Children.Add(c.Status);
                grid.Children.Add(one);
            }
            all.Children.Add(grid);
            return all;
        }

        private byte[] Draw(Card c) =>
            TouchTabPictures.Battle(c.Page, _textOf, c.Press == null ? null : (c.Press, c.Frame), c.Frontier, _icon);

        private void DrawCards()
        {
            if (_plan == null || !_plan.Fits) { ShowMain(); ShowCards(); return; }
            PendingArchiveWrites dry = new PendingArchiveWrites();
            string trouble = dry.Import(() => _fit.Apply(_plan, compress: false));
            if (trouble != null) return;
            using (dry.Reading())
                foreach (Card c in _cards) _after[c] = Draw(c);
            _mainAfter = _after[_cards[0]];
            ShowMain();
            ShowCards();
        }

        private void ShowMain()
        {
            byte[] rgba = _mainAfter ?? _cards[0].Before;
            if (rgba == null) return;
            if (_showNew.IsChecked == true && _plan != null)
            {
                rgba = (byte[])rgba.Clone();
                foreach (int square in _plan.NewSquares) Outline(rgba, square % 32 * 8, square / 32 * 8);
            }
            _main.Source = ImageConverter.FromRgba(rgba, ScreenWidth, ScreenHeight);
        }

        private static void Outline(byte[] rgba, int left, int top)
        {
            for (int i = 0; i < 8; i++)
                foreach ((int x, int y) in new[] { (left + i, top), (left + i, top + 7), (left, top + i), (left + 7, top + i) })
                {
                    if (y >= ScreenHeight) continue;
                    int at = (y * ScreenWidth + x) * 4;
                    rgba[at] = 255; rgba[at + 1] = 212; rgba[at + 2] = 0; rgba[at + 3] = 255;
                }
        }

        private string StatusOf(Card c, bool after)
        {
            if (!after) return "Current";
            return c.Buttons.Any(b => _on[b]) ? "Changes" : "No change";
        }

        private void ShowCards()
        {
            bool after = _showAfter.IsChecked == true;
            foreach (Card c in _cards)
            {
                byte[] rgba = after && _after.TryGetValue(c, out byte[] drawn) ? drawn : c.Before;
                if (rgba != null) c.Image.Source = ImageConverter.FromRgba(rgba, ScreenWidth, ScreenHeight);
                c.Status.Text = StatusOf(c, after);
            }
        }
    }
}
