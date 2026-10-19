using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Graphics
{
    public partial class NsbtxEditorView : Window
    {
        private NsbtxEditorViewModel VM => DataContext as NsbtxEditorViewModel;
        private bool _setupDone;

        public NsbtxEditorView()
        {
            InitializeComponent();
            Loaded += OnLoadedSetup;
        }

        public NsbtxEditorView(NsbtxEditorViewModel vm) : this() { DataContext = vm; EditorWindowChrome.Attach(this, vm); }
        private void Save_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e) => (DataContext as NsbtxEditorViewModel)?.SaveChanges();
        private async void Discard_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) (DataContext as NsbtxEditorViewModel)?.DiscardChanges();
        }

        private async void OnLoadedSetup(object sender, RoutedEventArgs e)
        {
            if (_setupDone || Design.IsDesignMode) return;
            NsbtxEditorViewModel vm = VM;
            if (vm == null) return;
            _setupDone = true;
            await vm.SetupAsync(this);
        }

        private async void Import_Click(object sender, RoutedEventArgs e) => await SafeAsync(VM?.ImportAsync());
        private async void Export_Click(object sender, RoutedEventArgs e) => await SafeAsync(VM?.ExportAsync());
        private void ShowFile_Click(object sender, RoutedEventArgs e) => VM?.ShowFile();

        // ── Preview zoom: fitted until the wheel turns, then whole-pixel steps; dragging pans ─────────────
        private int _zoom;           // 0 fits the box
        private global::Avalonia.Point? _panFrom;

        private void Preview_Wheel(object sender, global::Avalonia.Input.PointerWheelEventArgs e)
        {
            if (PreviewImage.Source is not global::Avalonia.Media.Imaging.Bitmap bmp) return;
            int fitted = System.Math.Max(1, (int)System.Math.Floor(System.Math.Min(PreviewScroll.Bounds.Width / bmp.PixelSize.Width,
                                                              PreviewScroll.Bounds.Height / bmp.PixelSize.Height)));
            int from = _zoom == 0 ? fitted : _zoom;
            int to = System.Math.Clamp(from + (e.Delta.Y > 0 ? 1 : -1), 1, 32);
            SetZoom(to, bmp);
            e.Handled = true;
        }

        private void SetZoom(int zoom, global::Avalonia.Media.Imaging.Bitmap bmp)
        {
            _zoom = zoom;
            PreviewImage.Stretch = zoom == 0 ? global::Avalonia.Media.Stretch.Uniform : global::Avalonia.Media.Stretch.Fill;
            PreviewImage.Width = zoom == 0 || bmp == null ? double.NaN : bmp.PixelSize.Width * zoom;
            PreviewImage.Height = zoom == 0 || bmp == null ? double.NaN : bmp.PixelSize.Height * zoom;
            FitButton.IsVisible = zoom != 0;
            // Fitting needs the box's size; with scroll bars on, the image would be measured with no limit.
            global::Avalonia.Controls.Primitives.ScrollBarVisibility bars = zoom == 0
                ? global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled : global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
            PreviewScroll.HorizontalScrollBarVisibility = bars;
            PreviewScroll.VerticalScrollBarVisibility = bars;
        }

        private void Fit_Click(object sender, RoutedEventArgs e) => SetZoom(0, null);

        private void Preview_Pressed(object sender, global::Avalonia.Input.PointerPressedEventArgs e)
        {
            if (_zoom == 0) return;
            _panFrom = e.GetPosition(PreviewScroll);
            e.Pointer.Capture(PreviewScroll);
        }

        private void Preview_Moved(object sender, global::Avalonia.Input.PointerEventArgs e)
        {
            if (_panFrom is not global::Avalonia.Point from) return;
            global::Avalonia.Point now = e.GetPosition(PreviewScroll);
            PreviewScroll.Offset = new global::Avalonia.Vector(PreviewScroll.Offset.X - (now.X - from.X), PreviewScroll.Offset.Y - (now.Y - from.Y));
            _panFrom = now;
        }

        private void Preview_Released(object sender, global::Avalonia.Input.PointerReleasedEventArgs e)
        {
            _panFrom = null;
            e.Pointer.Capture(null);
        }
        private void AddPack_Click(object sender, RoutedEventArgs e) => VM?.AddPack();
        private async void RemPack_Click(object sender, RoutedEventArgs e) => await SafeAsync(VM?.RemoveLastPackAsync());

        private static async Task SafeAsync(Task task)
        {
            if (task == null) return;
            try { await task; }
            catch (System.Exception ex)
            {
                CrashReporter.LogHandled(ex);
                await DialogHelper.ShowErrorAsync(ex.Message);
            }
        }
    }
}
