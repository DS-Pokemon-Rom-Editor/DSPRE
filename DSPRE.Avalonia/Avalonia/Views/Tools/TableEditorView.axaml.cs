using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Tools
{
    public partial class TableEditorView : Window
    {
        private TableEditorViewModel VM => DataContext as TableEditorViewModel;
        private bool _setupDone;

        public TableEditorView()
        {
            InitializeComponent();
            Loaded += OnLoadedSetup;
        }

        public TableEditorView(TableEditorViewModel vm) : this()
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
            await vm.SetupAsync();
            TabDefault.SelectFirstVisible(Tabs);
        }

        private async void Save_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.SaveAllAsync(); }
        private void Discard_Click(object sender, RoutedEventArgs e) => VM?.DiscardChanges();
        private void OpenVsIntros_Click(object sender, RoutedEventArgs e) => AvaloniaEditorLauncher.OpenVsIntroEditor();
        private async void AddVsPokemonRow_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.AddVsPokemonRowAsync(); }
        private async void RemoveVsPokemonRow_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.RemoveVsPokemonRowAsync(); }
        private void OpenWildIntros_Click(object sender, RoutedEventArgs e) => AvaloniaEditorLauncher.OpenWildIntroEditor();
    }
}
