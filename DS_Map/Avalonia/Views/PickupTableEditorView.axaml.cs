using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class PickupTableEditorView : Window
    {
        private PickupTableEditorViewModel VM => DataContext as PickupTableEditorViewModel;
        private bool _setupDone;

        public PickupTableEditorView()
        {
            InitializeComponent();
            Loaded += OnLoadedSetup;
        }

        public PickupTableEditorView(PickupTableEditorViewModel vm) : this() { DataContext = vm; EditorWindowChrome.Attach(this, vm); }

        private async void OnLoadedSetup(object sender, RoutedEventArgs e)
        {
            if (_setupDone || Design.IsDesignMode) return;
            var vm = VM;
            if (vm == null) return;
            _setupDone = true;
            await vm.SetupAsync(this);
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();
    }
}
