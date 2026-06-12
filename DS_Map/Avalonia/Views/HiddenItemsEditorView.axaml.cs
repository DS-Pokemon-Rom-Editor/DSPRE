using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class HiddenItemsEditorView : Window
    {
        private HiddenItemsEditorViewModel VM => DataContext as HiddenItemsEditorViewModel;
        private bool _setupDone;
        private bool _closeConfirmed;

        public HiddenItemsEditorView()
        {
            InitializeComponent();
            Loaded += OnLoadedSetup;
        }

        public HiddenItemsEditorView(HiddenItemsEditorViewModel vm) : this() { DataContext = vm; }

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

        protected override async void OnClosing(WindowClosingEventArgs e)
        {
            if (VM != null && VM.HasUnsavedChanges && !_closeConfirmed)
            {
                e.Cancel = true;
                bool discard = await DialogHelper.AskYesNo($"Discard unsaved changes to {VM.UnsavedChangesDescription}?", "Unsaved Changes");
                if (discard) { _closeConfirmed = true; VM.DiscardChanges(); Close(); }
                return;
            }
            base.OnClosing(e);
        }
    }
}
