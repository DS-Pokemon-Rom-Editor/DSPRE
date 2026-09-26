using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class SpecialEncountersEditorView : Window
    {
        private SpecialEncountersEditorViewModel VM => DataContext as SpecialEncountersEditorViewModel;
        private bool _setupDone;

        public SpecialEncountersEditorView()
        {
            InitializeComponent();
            Loaded += OnLoadedSetup;
        }

        public SpecialEncountersEditorView(SpecialEncountersEditorViewModel vm) : this()
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

            if (vm.StartOnHeadbutt) Tabs.SelectedItem = HeadbuttTab;
            TabDefault.SelectFirstVisible(Tabs);
        }
    }
}
