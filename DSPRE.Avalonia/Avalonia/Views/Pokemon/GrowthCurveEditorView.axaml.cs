using System.ComponentModel;
using System.Linq;
using Avalonia;
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
        }

        public GrowthCurveEditorView(GrowthCurveEditorViewModel vm) : this()
        {
            DataContext = vm;
            vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(GrowthCurveEditorViewModel.CurveForChart)) DrawChart(); };
        }

        private void DrawChart()
        {
            Chart.Children.Clear();
            var curve = VM?.CurveForChart;
            double w = Chart.Bounds.Width, h = Chart.Bounds.Height;
            if (curve == null || curve.Count < 2 || w < 20 || h < 20) return;
            double max = System.Math.Max(1, curve.Max());
            const double pad = 12;
            var line = new Polyline
            {
                Stroke = this.TryFindResource("Editor.Good", ActualThemeVariant, out var good) && good is IBrush b ? b : Brushes.SteelBlue,
                StrokeThickness = 2,
                Points = new Points(curve.Select((v, i) => new Point(
                    pad + i * (w - 2 * pad) / (curve.Count - 1),
                    h - pad - v / max * (h - 2 * pad)))),
            };
            Chart.Children.Add(line);
        }

        private async void Save_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.SaveChangesAsync(); }
        private void Discard_Click(object sender, RoutedEventArgs e) => VM?.DiscardChanges();
    }
}
