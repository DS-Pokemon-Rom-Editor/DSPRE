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
            // Only HeartGold and SoulSilver's Headbutt and Bug Contest tables need the full width.
            if (!vm.ShowHeadbutt && !vm.ShowBugContest) { Width = 1040; MinWidth = 820; }
        }

        private async void OnLoadedSetup(object sender, RoutedEventArgs e)
        {
            if (_setupDone || Design.IsDesignMode) return;
            SpecialEncountersEditorViewModel vm = VM;
            if (vm == null) return;
            _setupDone = true;
            await vm.SetupAsync(this);

            if (vm.StartOnHeadbutt) Tabs.SelectedItem = HeadbuttTab;
            TabDefault.SelectFirstVisible(Tabs);
            Tabs.SelectionChanged += (_, _) => TellActiveTab();
            TellActiveTab();
        }

        private void TellActiveTab()
        {
            if (VM != null) VM.ActiveTab = (Tabs.SelectedItem as TabItem)?.Content is Control c ? c.DataContext : null;
        }
    }
}
