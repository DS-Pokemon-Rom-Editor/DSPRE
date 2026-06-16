using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class HiddenItemsEditorView : Window
    {
        private HiddenItemsEditorViewModel VM => DataContext as HiddenItemsEditorViewModel;
        private bool _setupDone;

        public HiddenItemsEditorView()
        {
            InitializeComponent();
            Loaded += OnLoadedSetup;
        }

        public HiddenItemsEditorView(HiddenItemsEditorViewModel vm) : this() { DataContext = vm; EditorWindowChrome.Attach(this, vm); }

        private async void OnLoadedSetup(object sender, RoutedEventArgs e)
        {
            if (_setupDone || Design.IsDesignMode) return;
            var vm = VM;
            if (vm == null) return;
            _setupDone = true;
            await vm.SetupAsync(this);
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();
        private void Add_Click(object sender, RoutedEventArgs e) => VM?.AddEntry();
        private void Remove_Click(object sender, RoutedEventArgs e) => VM?.RemoveEntry();
    }
}
