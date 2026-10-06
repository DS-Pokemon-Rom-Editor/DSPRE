using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Graphics
{
    public partial class NsbtxEditorView : Window
    {
        private NsbtxEditorViewModel VM => DataContext as NsbtxEditorViewModel;
        private bool _setupDone;

        public NsbtxEditorView()
        {
            InitializeComponent();
            Loaded += OnLoadedSetup;
        }

        public NsbtxEditorView(NsbtxEditorViewModel vm) : this() { DataContext = vm; EditorWindowChrome.Attach(this, vm); }
        private void Save_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e) => (DataContext as NsbtxEditorViewModel)?.SaveChanges();
        private void Discard_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e) => (DataContext as NsbtxEditorViewModel)?.DiscardChanges();

        private async void OnLoadedSetup(object sender, RoutedEventArgs e)
        {
            if (_setupDone || Design.IsDesignMode) return;
            NsbtxEditorViewModel vm = VM;
            if (vm == null) return;
            _setupDone = true;
            await vm.SetupAsync(this);
        }

        private async void Import_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ImportAsync());
        private async void Export_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ExportAsync());
        private void AddPack_Click(object sender, RoutedEventArgs e) => VM?.AddPack();
        private async void RemPack_Click(object sender, RoutedEventArgs e) => await Safe(VM?.RemoveLastPackAsync());

        private static async Task Safe(Task task)
        {
            if (task == null) return;
            try { await task; } catch { }
        }
    }
}
