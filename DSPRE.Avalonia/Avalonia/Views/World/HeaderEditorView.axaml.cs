using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Threading.Tasks;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.World
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
            HeaderEditorViewModel vm = VM;
            if (vm == null) return;
            Window owner = TopLevel.GetTopLevel(this) as Window;
            if (owner == null) return;
            _setupDone = true;
            await vm.SetupAsync(owner);
            // Location names live in a text archive editable in another editor window; refresh folder
            // labels when this window regains focus (no-op when nothing changed).
            owner.Activated += (_, _) => vm.ReloadLocationNames();
        }

    }
}
