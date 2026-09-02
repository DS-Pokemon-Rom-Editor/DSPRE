using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class EncountersEditorView : Window
    {
        private EncountersEditorViewModel VM => DataContext as EncountersEditorViewModel;
        private bool _setupDone;

        public EncountersEditorView()
        {
            InitializeComponent();
            Loaded += OnLoadedSetup;
        }

        public EncountersEditorView(EncountersEditorViewModel vm) : this()
        {
            DataContext = vm;
            EditorWindowChrome.Attach(this, vm);
        }

        private async void OnLoadedSetup(object sender, RoutedEventArgs e)
        {
            if (_setupDone || Design.IsDesignMode) return;
            var vm = VM;
            if (vm == null) return;
            _setupDone = true;
            await vm.SetupAsync(this);

            TabDefault.SelectFirstVisible(Tabs);
        }
    }
}
