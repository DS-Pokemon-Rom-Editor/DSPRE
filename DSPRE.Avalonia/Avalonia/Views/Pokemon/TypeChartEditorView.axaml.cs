using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.ViewModels.Pokemon;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class TypeChartEditorView : UserControl
    {
        private TypeChartEditorViewModel VM => DataContext as TypeChartEditorViewModel;
        private Border[,] _cells;

        public TypeChartEditorView() { InitializeComponent(); }

        public TypeChartEditorView(TypeChartEditorViewModel vm) : this()
        {
            DataContext = vm;
            BuildGrid();
            vm.CellsChanged += (_, _) => Paint();
            vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(TypeChartEditorViewModel.SelectedAttacker)) Paint(); };
            ActualThemeVariantChanged += (_, _) => Paint();
            Cells.KeyDown += Cells_KeyDown;
            Host.SizeChanged += (_, _) => Fit();
            Cells.SizeChanged += (_, _) => Fit();
        }

        private const double PanelRoom = 256;   // the edit panel's width plus its margin
        private bool _minSet;

        // Scales the chart up to fill the window but never below natural size; a small window scrolls instead.
        private void Fit()
        {
            var natural = Cells.DesiredSize;
            var room = Host.Bounds.Size;
            if (natural.Width <= 0 || natural.Height <= 0 || room.Width <= 0) return;
            // The window can't shrink below the whole chart plus the panel.
            if (!_minSet && TopLevel.GetTopLevel(this) is Window win && win.ClientSize.Width > 0)
            {
                _minSet = true;
                // Room for the scroll bars too: once one appears it takes space and clips the last row or column.
                win.MinWidth = win.ClientSize.Width - room.Width + natural.Width + PanelRoom + 24;
                win.MinHeight = win.ClientSize.Height - room.Height + natural.Height + 24;
            }
            double wide = System.Math.Max(0, room.Width - PanelRoom);
            double s = System.Math.Max(1, System.Math.Min((wide - 2) / natural.Width, (room.Height - 2) / natural.Height));
            s = System.Math.Floor(s * 20) / 20;   // steps of 5% so a resize doesn't relayout on every pixel
            ChartScroll.MaxWidth = System.Math.Min(wide, natural.Width * s + 20);
            if (ChartScale.LayoutTransform is ScaleTransform t && t.ScaleX == s) return;
            ChartScale.LayoutTransform = new ScaleTransform(s, s);
        }

        // Arrows move the selection; Enter or Space steps the selected cell like a second click.
        private void Cells_KeyDown(object sender, KeyEventArgs e)
        {
            var vm = VM;
            if (vm == null || vm.SelectedAttacker < 0) return;
            int a = vm.SelectedAttacker, d = vm.SelectedDefender, n = vm.TypeCount;
            switch (e.Key)
            {
                case Key.Up: a = (a + n - 1) % n; break;
                case Key.Down: a = (a + 1) % n; break;
                case Key.Left: d = (d + n - 1) % n; break;
                case Key.Right: d = (d + 1) % n; break;
                case Key.Enter: case Key.Space: break;
                default: return;
            }
            vm.Click(a, d);
            _cells[a, d].BringIntoView();
            e.Handled = true;
        }

        private static string Short(string name) => string.IsNullOrEmpty(name) ? "?" : name.Length <= 4 ? name : name.Substring(0, 4);

        private void BuildGrid()
        {
            var vm = VM;
            int n = vm.TypeCount;
            const double size = 34;
            for (int i = 0; i <= n; i++)
            {
                Cells.ColumnDefinitions.Add(new ColumnDefinition(i == 0 ? GridLength.Auto : new GridLength(size)));
                Cells.RowDefinitions.Add(new RowDefinition(new GridLength(i == 0 ? 20 : size)));
            }
            _cells = new Border[n, n];
            var corner = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
            corner.Children.Add(new TextBlock { Text = "Atk", FontSize = 10, VerticalAlignment = VerticalAlignment.Center });
            corner.Children.Add(DSPRE.Avalonia.Controls.Icon.Image("arrowdown"));
            corner.Children.Add(new TextBlock { Text = "Def", FontSize = 10, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) });
            corner.Children.Add(DSPRE.Avalonia.Controls.Icon.Image("arrowright"));
            Cells.Children.Add(corner);
            for (int t = 0; t < n; t++)
            {
                var icon = TypeIcons.For(t);
                Control across = icon != null
                    ? new Image { Source = icon, Width = 32, Height = 16, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                    : new TextBlock { Text = Short(vm.NameOf(t)), FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center };
                ToolTip.SetTip(across, vm.NameOf(t));
                Control down = icon != null
                    ? new Image { Source = icon, Width = 32, Height = 16, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) }
                    : new TextBlock { Text = vm.NameOf(t), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
                ToolTip.SetTip(down, vm.NameOf(t));
                int type = t;
                // The type icons toggle a highlighted row (attacking) or column (defending).
                var acrossHit = Clickable(across, () => VM?.ToggleColumn(type));
                var downHit = Clickable(down, () => VM?.ToggleRow(type));
                Grid.SetRow(acrossHit, 0); Grid.SetColumn(acrossHit, t + 1);
                Grid.SetRow(downHit, t + 1); Grid.SetColumn(downHit, 0);
                Cells.Children.Add(acrossHit); Cells.Children.Add(downHit);
            }
            for (int a = 0; a < n; a++)
                for (int d = 0; d < n; d++)
                {
                    int attacker = a, defender = d;
                    // A Border, not a Button: the button theme repaints hovered and pressed cells over their colour.
                    var cell = new Border
                    {
                        Width = size - 2, Height = size - 2, CornerRadius = new CornerRadius(3), Cursor = new Cursor(StandardCursorType.Hand),
                        Child = new TextBlock { FontSize = 12, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
                    };
                    ToolTip.SetTip(cell, $"{vm.NameOf(a)} attacking {vm.NameOf(d)}");
                    cell.PointerPressed += (_, e) =>
                    {
                        if (!e.GetCurrentPoint(cell).Properties.IsLeftButtonPressed) return;
                        Cells.Focus();
                        VM?.Click(attacker, defender);
                        e.Handled = true;
                    };
                    cell.PointerEntered += (_, _) => { _hover = (attacker, defender); Paint(); };
                    cell.PointerExited += (_, _) => { if (_hover == (attacker, defender)) { _hover = (-1, -1); Paint(); } };
                    Grid.SetRow(cell, a + 1); Grid.SetColumn(cell, d + 1);
                    Cells.Children.Add(cell);
                    _cells[a, d] = cell;
                }
            // Highlights sit on the cells' edges, above them, and never take clicks.
            _rowLine = Overlay(new Thickness(0, 2, 0, 2), RowColour);
            _colLine = Overlay(new Thickness(2, 0, 2, 0), ColumnColour);
            _cross = Overlay(new Thickness(2), CrossColour);
            Grid.SetColumnSpan(_rowLine, n + 1);
            Grid.SetRowSpan(_colLine, n + 1);
            vm.HighlightChanged += (_, _) => Paint();
            Paint();
        }

        private static readonly Color RowColour = Color.FromRgb(0xF2, 0xC9, 0x4C);
        private static readonly Color ColumnColour = Color.FromRgb(0x4E, 0xA8, 0xFF);
        private static readonly Color CrossColour = Color.FromRgb(0xC0, 0x6B, 0xFF);
        private Border _rowLine, _colLine, _cross;

        private Border Overlay(Thickness sides, Color colour)
        {
            var b = new Border { BorderThickness = sides, BorderBrush = new SolidColorBrush(colour), CornerRadius = new CornerRadius(3),
                                 IsHitTestVisible = false, IsVisible = false, ZIndex = 10 };
            Cells.Children.Add(b);
            return b;
        }

        private static Control Clickable(Control inner, System.Action toggle)
        {
            var hit = new Border { Background = Brushes.Transparent, Cursor = new Cursor(StandardCursorType.Hand), Child = inner };
            ToolTip.SetTip(hit, ToolTip.GetTip(inner));
            ToolTip.SetTip(inner, null);
            hit.PointerPressed += (_, e) => { if (e.GetCurrentPoint(hit).Properties.IsLeftButtonPressed) { toggle(); e.Handled = true; } };
            return hit;
        }

        private (int, int) _hover = (-1, -1);

        private void Paint()
        {
            var vm = VM;
            if (vm == null || _cells == null) return;
            int n = vm.TypeCount;
            IBrush subtle = this.TryFindResource("Editor.Subtle", ActualThemeVariant, out var s) && s is IBrush sb ? sb : Brushes.Gray;
            IBrush fore = this.TryFindResource("Editor.Text", ActualThemeVariant, out var f) && f is IBrush fb ? fb : subtle;
            IBrush back = BackgroundBehind();
            for (int a = 0; a < n; a++)
                for (int d = 0; d < n; d++)
                {
                    int t = vm.TenthsAt(a, d);
                    var cell = _cells[a, d];
                    var text = (TextBlock)cell.Child;
                    text.Text = t switch { 10 => "", 20 => "2", 5 => "½", 0 => vm.ForesightAt(a, d) ? "0*" : "0", _ => (t / 10m).ToString("0.#") };
                    cell.Background = new SolidColorBrush(t switch
                    {
                        10 => Color.FromArgb(0x24, 0x90, 0x90, 0x90),   // a shade off the background, so empty cells still read as cells
                        20 => Color.FromRgb(0x2E, 0x8B, 0x3E),
                        5 => Color.FromRgb(0xA8, 0x3A, 0x32),
                        0 => Color.FromRgb(0x30, 0x30, 0x30),
                        _ => Color.FromRgb(0x6A, 0x4C, 0x9C),
                    });
                    text.Foreground = t == 10 ? subtle : Brushes.White;
                    bool selected = a == vm.SelectedAttacker && d == vm.SelectedDefender;
                    cell.BorderBrush = selected || _hover == (a, d) ? fore : back;
                    cell.BorderThickness = new Thickness(selected ? 2 : 1);
                }
            if (_rowLine == null) return;
            int row = vm.HighlightRow, col = vm.HighlightColumn;
            _rowLine.IsVisible = row >= 0;
            if (row >= 0) Grid.SetRow(_rowLine, row + 1);
            _colLine.IsVisible = col >= 0;
            if (col >= 0) Grid.SetColumn(_colLine, col + 1);
            _cross.IsVisible = row >= 0 && col >= 0;
            if (_cross.IsVisible) { Grid.SetRow(_cross, row + 1); Grid.SetColumn(_cross, col + 1); }
        }

        // The colour the chart sits on: the nearest ancestor that paints one.
        private IBrush BackgroundBehind()
        {
            for (var v = Cells.Parent; v != null; v = v.Parent)
            {
                IBrush b = v switch { Panel p => p.Background, Border bd => bd.Background, TemplatedControl t => t.Background, _ => null };
                if (b is ISolidColorBrush sc && sc.Color.A == 0) continue;
                if (b != null) return b;
            }
            return Brushes.Transparent;
        }

        private async void Save_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.SaveChangesAsync(); }
        private void Discard_Click(object sender, RoutedEventArgs e) => VM?.DiscardChanges();
        private async void MakeRoom_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.MakeRoomAsync(); }
    }
}
