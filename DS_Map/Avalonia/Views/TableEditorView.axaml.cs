using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
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
        }

        private async void OnLoadedSetup(object sender, RoutedEventArgs e)
        {
            if (_setupDone || Design.IsDesignMode) return;
            var vm = VM;
            if (vm == null) return;
            _setupDone = true;
            await vm.SetupAsync();
        }

        private void SaveCondMusic_Click(object sender, RoutedEventArgs e) => VM?.SaveConditionalMusic();
        private void SaveEffectCombo_Click(object sender, RoutedEventArgs e) => VM?.SaveEffectCombo();
        private void SaveVsTrainer_Click(object sender, RoutedEventArgs e) => VM?.SaveVsTrainer();

        private async void CondMusicHelp_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ShowConditionalMusicHelp());
        private async void EffectComboHelp_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ShowEffectsComboHelp());
        private async void VsTrainerHelp_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ShowVsTrainerHelp());
        private async void VsPokemonHelp_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ShowVsPokemonHelp());

        // ── Unsaved-changes guard on close ───────────────────────────────────
        protected override async void OnClosing(WindowClosingEventArgs e)
        {
            if (VM != null && VM.HasUnsavedChanges && !_closeConfirmed)
            {
                e.Cancel = true;
                bool discard = await DialogHelper.AskYesNo(
                    "Discard unsaved changes to the Table Editor?", "Unsaved Changes");
                if (discard) { _closeConfirmed = true; VM.DiscardChanges(); Close(); }
                return;
            }
            base.OnClosing(e);
        }
        private bool _closeConfirmed;

        private static async Task Safe(Task task)
        {
            if (task == null) return;
            try { await task; } catch { /* handled in VM */ }
        }
    }
}
