using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.World
{
    public partial class CameraEditorView : UserControl
    {
        private CameraEditorViewModel VM => DataContext as CameraEditorViewModel;
        private bool _setupDone;

        public CameraEditorView()
        {
            InitializeComponent();
            // Run setup once attached to the visual tree, regardless of whether the
            // VM arrives via the (vm) constructor or via DataContext binding when the
            // control is embedded as a tab in the Avalonia MainWindow.
            Loaded += OnLoadedSetup;
        }

        public CameraEditorView(CameraEditorViewModel vm) : this()
        {
            DataContext = vm;
        }

        private async void OnLoadedSetup(object sender, RoutedEventArgs e)
        {
            if (_setupDone) return;
            if (Design.IsDesignMode) return;

            var vm = VM;
            if (vm == null) return;
            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner == null) return;

            _setupDone = true;
            await vm.SetupAsync(owner);

            if (!vm.IsHgss)
                HideHgssColumns();
        }

        private void HideHgssColumns()
        {
            // Avalonia's compiled-XAML name generator does not emit fields for
            // DataGridColumn elements (they live outside the control name scope),
            // so the HGSS-only X/Y/Z offset columns are addressed by matching their
            // Header text instead of by x:Name.
            foreach (var col in CamerasGrid.Columns)
            {
                if (col.Header is string h &&
                    (h == "X Offset" || h == "Y Offset" || h == "Z Offset"))
                {
                    col.IsVisible = false;
                }
            }
        }

        // ── Toolbar handlers ─────────────────────────────────────────────────
        private async void SaveTable_Click(object sender, RoutedEventArgs e)
            => await RunSafe(() => VM?.SaveAsync());

        private async void ExportTable_Click(object sender, RoutedEventArgs e)
            => await RunSafe(() => VM?.ExportTableAsync());

        private async void ImportTable_Click(object sender, RoutedEventArgs e)
            => await RunSafe(() => VM?.ImportTableAsync());

        private static async Task RunSafe(System.Func<Task> action)
        {
            if (action == null) return;
            try { await action(); } catch { /* errors handled inside the VM */ }
        }
    }
}
