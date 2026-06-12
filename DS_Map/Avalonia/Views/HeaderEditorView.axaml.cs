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
        private void Reset_Click(object sender, RoutedEventArgs e) => VM?.Reset();
        private void Copy_Click(object sender, RoutedEventArgs e) => VM?.Copy();
        private void Paste_Click(object sender, RoutedEventArgs e) => VM?.Paste();
        private async void Import_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ImportAsync());
        private async void Export_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ExportAsync());
        private void GoTo_Click(object sender, RoutedEventArgs e) => VM?.GoTo();
        private async void AddHeader_Click(object sender, RoutedEventArgs e) => await Safe(VM?.AddHeaderAsync());
        private async void RemoveHeader_Click(object sender, RoutedEventArgs e) => await Safe(VM?.RemoveHeaderAsync());

        private void OpenMatrix_Click(object sender, RoutedEventArgs e) => VM?.OpenMatrix();
        private void OpenEvents_Click(object sender, RoutedEventArgs e) => VM?.OpenEvents();
        private void OpenScripts_Click(object sender, RoutedEventArgs e) => VM?.OpenScripts();
        private void OpenLevelScripts_Click(object sender, RoutedEventArgs e) => VM?.OpenLevelScripts();
        private void OpenTexts_Click(object sender, RoutedEventArgs e) => VM?.OpenTexts();

        private static async Task Safe(Task task)
        {
            if (task == null) return;
            try { await task; } catch { /* handled in VM */ }
        }
    }
}
