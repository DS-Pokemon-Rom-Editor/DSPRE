using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;
using DSPRE.Avalonia.ViewModels.Pokemon;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class GrowthCurveEditorView : UserControl
    {
        private GrowthCurveEditorViewModel VM => DataContext as GrowthCurveEditorViewModel;

        public GrowthCurveEditorView()
        {
            InitializeComponent();
            Chart.SizeChanged += (_, _) => DrawChart();
            Chart.PointerMoved += Chart_PointerMoved;
            Chart.PointerExited += (_, _) => { _hoverLevel = -1; DrawChart(); };
        }

        public GrowthCurveEditorView(GrowthCurveEditorViewModel vm) : this()
        {
            DataContext = vm;
            vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(GrowthCurveEditorViewModel.CurveForChart)) DrawChart(); };
        }

        private int _hoverLevel = -1;

        // Level runs along the bottom, total EXP up the side; hovering marks the nearest level.
        private void DrawChart()
        {
            Chart.Children.Clear();
            var curve = VM?.CurveForChart;
            double w = Chart.Bounds.Width, h = Chart.Bounds.Height;
            if (curve == null || curve.Count < 2 || w < 60 || h < 60) return;
            double max = System.Math.Max(1, curve.Max());
            const double left = 70, right = 14, top = 12, bottom = 26;
            double pw = w - left - right, ph = h - top - bottom;
            IBrush subtle = this.TryFindResource("Editor.Subtle", ActualThemeVariant, out var sb) && sb is IBrush s1 ? s1 : Brushes.Gray;
            IBrush grid = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128));
            IBrush stroke = this.TryFindResource("Editor.Good", ActualThemeVariant, out var good) && good is IBrush b ? b : Brushes.SteelBlue;
            double X(int i) => left + i * pw / (curve.Count - 1);
            double Y(double v) => top + ph - v / max * ph;

            void Label(string text, double x, double y, bool right_ = false)
            {
                var t = new TextBlock { Text = text, FontSize = 10, Foreground = subtle };
                t.Measure(Size.Infinity);
                Canvas.SetLeft(t, right_ ? x - t.DesiredSize.Width : x - t.DesiredSize.Width / 2);
                Canvas.SetTop(t, y);
                Chart.Children.Add(t);
            }

            for (int k = 0; k <= 4; k++)
            {
                double v = max * k / 4, y = Y(v);
                Chart.Children.Add(new Line { StartPoint = new Point(left, y), EndPoint = new Point(left + pw, y), Stroke = grid });
                Label(v.ToString("N0"), left - 6, y - 7, true);
            }
            foreach (int level in new[] { 1, 25, 50, 75, 100 })
            {
                int i = System.Math.Min(curve.Count - 1, level - 1);
                Chart.Children.Add(new Line { StartPoint = new Point(X(i), top), EndPoint = new Point(X(i), top + ph), Stroke = grid });
                Label("Lv " + level, X(i), top + ph + 6);
            }

            Chart.Children.Add(new Polyline
            {
                Stroke = stroke, StrokeThickness = 2,
                Points = new Points(curve.Select((v, i) => new Point(X(i), Y(v)))),
            });

            if (_hoverLevel >= 0 && _hoverLevel < curve.Count)
            {
                double x = X(_hoverLevel), y = Y(curve[_hoverLevel]);
                Chart.Children.Add(new Line { StartPoint = new Point(x, top), EndPoint = new Point(x, top + ph), Stroke = subtle, StrokeDashArray = new AvaloniaList<double> { 3, 3 } });
                var dot = new Ellipse { Width = 8, Height = 8, Fill = stroke };
                Canvas.SetLeft(dot, x - 4); Canvas.SetTop(dot, y - 4);
                Chart.Children.Add(dot);
                var tip = new Border
                {
                    Background = this.TryFindResource("Editor.ToolbarBg", ActualThemeVariant, out var bg) && bg is IBrush bb ? bb : Brushes.Black,
                    BorderBrush = grid, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Padding = new Thickness(6, 2),
                    Child = new TextBlock { Text = $"Lv {_hoverLevel + 1}: {curve[_hoverLevel]:N0} EXP", FontSize = 11 },
                };
                tip.Measure(Size.Infinity);
                double tx = System.Math.Min(x + 8, left + pw - tip.DesiredSize.Width);
                Canvas.SetLeft(tip, tx); Canvas.SetTop(tip, System.Math.Max(top, y - 28));
                Chart.Children.Add(tip);
            }
        }

        private void Chart_PointerMoved(object sender, global::Avalonia.Input.PointerEventArgs e)
        {
            var curve = VM?.CurveForChart;
            if (curve == null || curve.Count < 2) return;
            double pw = Chart.Bounds.Width - 70 - 14;
            int level = (int)System.Math.Round((e.GetPosition(Chart).X - 70) / pw * (curve.Count - 1));
            level = System.Math.Clamp(level, 0, curve.Count - 1);
            if (level == _hoverLevel) return;
            _hoverLevel = level;
            DrawChart();
        }

        private async void Save_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.SaveChangesAsync(); }
        private void Discard_Click(object sender, RoutedEventArgs e) => VM?.DiscardChanges();
    }
}
