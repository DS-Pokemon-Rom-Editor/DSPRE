using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Threading.Tasks;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    /// <summary>
    /// Header editor as a <see cref="UserControl"/> so it can be embedded as a tab in the
    /// Avalonia MainWindow shell. Standalone launches host it in an <see cref="EditorHostWindow"/>.
    /// </summary>
    public partial class HeaderEditorView : UserControl
    {
        private HeaderEditorViewModel VM => DataContext as HeaderEditorViewModel;
        private bool _setupDone;

        public HeaderEditorView()
        {
            InitializeComponent();
            Loaded += OnLoadedSetup;
        }

        public HeaderEditorView(HeaderEditorViewModel vm) : this()
        {
            DataContext = vm;
        }

        private async void OnLoadedSetup(object sender, RoutedEventArgs e)
        {
            if (_setupDone || Design.IsDesignMode) return;
            var vm = VM;
            if (vm == null) return;
            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner == null) return;
            _setupDone = true;
            await vm.SetupAsync(owner);
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();
        private async void AddHeader_Click(object sender, RoutedEventArgs e) => await Safe(VM?.AddHeaderAsync());
        private async void RemoveHeader_Click(object sender, RoutedEventArgs e) => await Safe(VM?.RemoveHeaderAsync());

        private static async Task Safe(Task task)
        {
            if (task == null) return;
            try { await task; } catch { /* handled in VM */ }
        }
    }
}
