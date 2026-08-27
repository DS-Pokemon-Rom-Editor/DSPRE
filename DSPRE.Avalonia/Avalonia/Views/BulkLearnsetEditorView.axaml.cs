using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class BulkLearnsetEditorView : Window
    {
        private BulkLearnsetEditorViewModel VM => DataContext as BulkLearnsetEditorViewModel;
        private bool _setupDone;

        public BulkLearnsetEditorView()
        {
            InitializeComponent();
            Loaded += OnLoadedSetup;
        }

        public BulkLearnsetEditorView(BulkLearnsetEditorViewModel vm) : this() { DataContext = vm; EditorWindowChrome.Attach(this, vm); }

        private async void OnLoadedSetup(object sender, RoutedEventArgs e)
        {
            if (_setupDone || Design.IsDesignMode) return;
            var vm = VM;
            if (vm == null) return;
            _setupDone = true;
            await vm.SetupAsync(this);
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.SaveAll();
        private void Add_Click(object sender, RoutedEventArgs e) => VM?.AddRow();
        private void Remove_Click(object sender, RoutedEventArgs e) => VM?.RemoveSelected();
    }
}
