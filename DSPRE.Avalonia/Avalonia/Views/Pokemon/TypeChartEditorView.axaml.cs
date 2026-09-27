using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
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
            var corner = new TextBlock { Text = "Atk ↓  Def →", FontSize = 10, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
            Cells.Children.Add(corner);
            for (int t = 0; t < n; t++)
            {
                var across = new TextBlock { Text = Short(vm.NameOf(t)), FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center };
                ToolTip.SetTip(across, vm.NameOf(t));
                Grid.SetRow(across, 0); Grid.SetColumn(across, t + 1);
                var down = new TextBlock { Text = vm.NameOf(t), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
                Grid.SetRow(down, t + 1); Grid.SetColumn(down, 0);
                Cells.Children.Add(across); Cells.Children.Add(down);
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
            Paint();
        }

        private (int, int) _hover = (-1, -1);

        private void Paint()
        {
            var vm = VM;
            if (vm == null || _cells == null) return;
            int n = vm.TypeCount;
            IBrush subtle = this.TryFindResource("Editor.Subtle", ActualThemeVariant, out var s) && s is IBrush sb ? sb : Brushes.Gray;
            IBrush fore = this.TryFindResource("Editor.Text", ActualThemeVariant, out var f) && f is IBrush fb ? fb : subtle;
            for (int a = 0; a < n; a++)
                for (int d = 0; d < n; d++)
                {
                    int t = vm.TenthsAt(a, d);
                    var cell = _cells[a, d];
                    var text = (TextBlock)cell.Child;
                    text.Text = t switch { 10 => "", 20 => "2", 5 => "½", 0 => vm.ForesightAt(a, d) ? "0*" : "0", _ => (t / 10m).ToString("0.#") };
                    cell.Background = new SolidColorBrush(t switch
                    {
                        10 => Color.FromArgb(0, 0, 0, 0),
                        20 => Color.FromRgb(0x2E, 0x8B, 0x3E),
                        5 => Color.FromRgb(0xA8, 0x3A, 0x32),
                        0 => Color.FromRgb(0x30, 0x30, 0x30),
                        _ => Color.FromRgb(0x6A, 0x4C, 0x9C),
                    });
                    text.Foreground = t == 10 ? subtle : Brushes.White;
                    bool selected = a == vm.SelectedAttacker && d == vm.SelectedDefender;
                    cell.BorderBrush = selected ? Brushes.Gold : _hover == (a, d) ? fore : null;
                    cell.BorderThickness = new Thickness(selected ? 2 : _hover == (a, d) ? 1 : 0);
                }
        }

        private async void Save_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.SaveChangesAsync(); }
        private void Discard_Click(object sender, RoutedEventArgs e) => VM?.DiscardChanges();
        private async void MakeRoom_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.MakeRoomAsync(); }
    }
}
