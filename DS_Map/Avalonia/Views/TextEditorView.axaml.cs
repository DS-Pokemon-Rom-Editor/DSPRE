using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class TextEditorView : Window
    {
        private TextEditorViewModel VM => DataContext as TextEditorViewModel;
        private bool _setupDone;

        public TextEditorView()
        {
            InitializeComponent();
            Loaded += OnLoadedSetup;
        }

        public TextEditorView(TextEditorViewModel vm) : this()
        {
            DataContext = vm;
            EditorWindowChrome.Attach(this, vm, manageTitle: false);   // VM owns the bound Title (+ " *" marker)
        }

        private async void OnLoadedSetup(object sender, RoutedEventArgs e)
        {
            if (_setupDone || Design.IsDesignMode) return;
            var vm = VM;
            if (vm == null) return;
            _setupDone = true;
            await vm.SetupAsync(this);
        }

        // ── Archive toolbar ──────────────────────────────────────────────────
        private void AddArchive_Click(object sender, RoutedEventArgs e) => VM?.AddArchive();
        private async void RemoveArchive_Click(object sender, RoutedEventArgs e)
            => await RunSafe(() => VM?.RemoveArchiveAsync());
        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();
        private async void Import_Click(object sender, RoutedEventArgs e)
            => await RunSafe(() => VM?.ImportAsync());
        private async void Export_Click(object sender, RoutedEventArgs e)
            => await RunSafe(() => VM?.ExportAsync());

        // ── Line controls ────────────────────────────────────────────────────
        private void AddString_Click(object sender, RoutedEventArgs e) => VM?.AddString();
        private void RemoveString_Click(object sender, RoutedEventArgs e) => VM?.RemoveString();
        private void MoveUp_Click(object sender, RoutedEventArgs e) => VM?.MoveSelectedUp();
        private void MoveDown_Click(object sender, RoutedEventArgs e) => VM?.MoveSelectedDown();

        // ── Search / replace ─────────────────────────────────────────────────
        private void Search_Click(object sender, RoutedEventArgs e) => VM?.Search();
        private async void Replace_Click(object sender, RoutedEventArgs e)
            => await RunSafe(() => VM?.ReplaceAsync());

        private void Search_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) VM?.Search();
        }

        private void SearchResult_DoubleTapped(object sender, TappedEventArgs e)
        {
            if (SearchResultsList.SelectedItem is TextSearchResultVM r)
                VM?.GoToResult(r);
        }

        private static async Task RunSafe(System.Func<Task> action)
        {
            var task = action?.Invoke();
            if (task == null) return;
            try { await task; } catch { /* errors handled inside the VM */ }
        }
    }
}
