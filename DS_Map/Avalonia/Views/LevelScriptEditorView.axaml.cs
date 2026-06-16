using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class LevelScriptEditorView : Window
    {
        private LevelScriptEditorViewModel VM => DataContext as LevelScriptEditorViewModel;
        private bool _setupDone;

        public LevelScriptEditorView()
        {
            InitializeComponent();
            Loaded += OnLoadedSetup;
        }

        public LevelScriptEditorView(LevelScriptEditorViewModel vm) : this() { DataContext = vm; EditorWindowChrome.Attach(this, vm); }

        private async void OnLoadedSetup(object sender, RoutedEventArgs e)
        {
            if (_setupDone || Design.IsDesignMode) return;
            var vm = VM;
            if (vm == null) return;
            _setupDone = true;
            await vm.SetupAsync(this);
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();
        private async void Import_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ImportAsync());
        private async void Export_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ExportAsync());
        private void Add_Click(object sender, RoutedEventArgs e) => VM?.AddTrigger();
        private void Remove_Click(object sender, RoutedEventArgs e) => VM?.RemoveTrigger();

        private static async Task Safe(Task task)
        {
            if (task == null) return;
            try { await task; } catch { }
        }
    }
}
