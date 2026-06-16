using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class AreaDataEditorView : Window
    {
        private AreaDataEditorViewModel VM => DataContext as AreaDataEditorViewModel;
        private bool _setupDone;

        public AreaDataEditorView()
        {
            InitializeComponent();
            Loaded += OnLoadedSetup;
        }

        public AreaDataEditorView(AreaDataEditorViewModel vm) : this() { DataContext = vm; EditorWindowChrome.Attach(this, vm); }

        private async void OnLoadedSetup(object sender, RoutedEventArgs e)
        {
            if (_setupDone || Design.IsDesignMode) return;
            var vm = VM;
            if (vm == null) return;
            _setupDone = true;
            await vm.SetupAsync(this);
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();

        private void Undo_Click(object sender, RoutedEventArgs e) => VM?.Undo();
        private void Redo_Click(object sender, RoutedEventArgs e) => VM?.Redo();
    }
}
