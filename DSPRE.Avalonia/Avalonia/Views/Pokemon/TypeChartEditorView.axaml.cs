using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using DSPRE.Avalonia.ViewModels.Pokemon;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class TypeChartEditorView : UserControl
    {
        private TypeChartEditorViewModel VM => DataContext as TypeChartEditorViewModel;
        private Button[,] _cells;

        public TypeChartEditorView() { InitializeComponent(); }

        public TypeChartEditorView(TypeChartEditorViewModel vm) : this()
        {
            DataContext = vm;
            BuildGrid();
            vm.CellsChanged += (_, _) => Paint();
            vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(TypeChartEditorViewModel.SelectedAttacker)) Paint(); };
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
            _cells = new Button[n, n];
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
                    var cell = new Button
                    {
                        Width = size - 2, Height = size - 2, Padding = new Thickness(0), MinWidth = 0,
                        HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
                        FontSize = 12, FontWeight = FontWeight.SemiBold,
                    };
                    ToolTip.SetTip(cell, $"{vm.NameOf(a)} attacking {vm.NameOf(d)}");
                    cell.Click += (_, _) => VM?.Click(attacker, defender);
                    Grid.SetRow(cell, a + 1); Grid.SetColumn(cell, d + 1);
                    Cells.Children.Add(cell);
                    _cells[a, d] = cell;
                }
            Paint();
        }

        private void Paint()
        {
            var vm = VM;
            if (vm == null || _cells == null) return;
            int n = vm.TypeCount;
            IBrush subtle = this.TryFindResource("Editor.Subtle", ActualThemeVariant, out var s) && s is IBrush sb ? sb : Brushes.Gray;
            for (int a = 0; a < n; a++)
                for (int d = 0; d < n; d++)
                {
                    int t = vm.TenthsAt(a, d);
                    var cell = _cells[a, d];
                    cell.Content = t switch { 10 => "", 20 => "2", 5 => "½", 0 => vm.ForesightAt(a, d) ? "0*" : "0", _ => (t / 10m).ToString("0.#") };
                    cell.Background = new SolidColorBrush(t switch
                    {
                        10 => Color.FromArgb(0, 0, 0, 0),
                        20 => Color.FromRgb(0x2E, 0x8B, 0x3E),
                        5 => Color.FromRgb(0xA8, 0x3A, 0x32),
                        0 => Color.FromRgb(0x30, 0x30, 0x30),
                        _ => Color.FromRgb(0x6A, 0x4C, 0x9C),
                    });
                    cell.Foreground = t == 10 ? subtle : Brushes.White;
                    cell.BorderBrush = a == vm.SelectedAttacker && d == vm.SelectedDefender ? Brushes.Gold : null;
                    cell.BorderThickness = new Thickness(a == vm.SelectedAttacker && d == vm.SelectedDefender ? 2 : 0);
                }
        }

        private async void Save_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.SaveChangesAsync(); }
        private void Discard_Click(object sender, RoutedEventArgs e) => VM?.DiscardChanges();
        private async void MakeRoom_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.MakeRoomAsync(); }
    }
}
