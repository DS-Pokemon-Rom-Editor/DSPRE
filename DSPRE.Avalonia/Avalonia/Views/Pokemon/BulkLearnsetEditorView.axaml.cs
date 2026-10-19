using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class BulkLearnsetEditorView : Window
    {
        private BulkLearnsetEditorViewModel VM => DataContext as BulkLearnsetEditorViewModel;
        private bool _setupDone;

        public BulkLearnsetEditorView()
        {
            InitializeComponent();
            Loaded += OnLoadedSetup;
            NameRefresh.Bind(this, () => VM?.RefreshNames());
        }

        public BulkLearnsetEditorView(BulkLearnsetEditorViewModel vm) : this() { DataContext = vm; EditorWindowChrome.Attach(this, vm); }

        private async void OnLoadedSetup(object sender, RoutedEventArgs e)
        {
            if (_setupDone || Design.IsDesignMode) return;
            BulkLearnsetEditorViewModel vm = VM;
            if (vm == null) return;
            _setupDone = true;
            await vm.SetupAsync(this);
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.SaveAll();
        private void Add_Click(object sender, RoutedEventArgs e) => VM?.AddRow();
        private void Remove_Click(object sender, RoutedEventArgs e) => VM?.RemoveSelected();
        private async void Import_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.ImportAsync(this); }
        private async void Export_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.ExportAsync(this); }
        private async void ExportJson_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.ExportJsonAsync(this); }

        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscard(this)) (DataContext as DSPRE.Editors.IEditorWithUnsavedChanges)?.DiscardChanges();
        }
    }
}
