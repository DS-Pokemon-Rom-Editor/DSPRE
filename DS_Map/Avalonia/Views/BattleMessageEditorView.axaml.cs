using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class BattleMessageEditorView : Window
    {
        private BattleMessageEditorViewModel VM => DataContext as BattleMessageEditorViewModel;
        private bool _setupDone;
        private bool _closeConfirmed;

        public BattleMessageEditorView()
        {
            InitializeComponent();
            Loaded += OnLoadedSetup;
        }

        public BattleMessageEditorView(BattleMessageEditorViewModel vm) : this()
        {
            DataContext = vm;
        }

        private async void OnLoadedSetup(object sender, RoutedEventArgs e)
        {
            if (_setupDone || Design.IsDesignMode) return;
            var vm = VM;
            if (vm == null) return;
            _setupDone = true;
            await vm.SetupAsync(this);
        }

        private async void Save_Click(object sender, RoutedEventArgs e) => await Safe(VM?.SaveAsync());
        private void Add_Click(object sender, RoutedEventArgs e) => VM?.AddEntry();
        private void Delete_Click(object sender, RoutedEventArgs e) => VM?.DeleteEntry();
        private void EditTrigger_Click(object sender, RoutedEventArgs e) => VM?.EditTrigger();
        private void SaveMessage_Click(object sender, RoutedEventArgs e) => VM?.SaveMessage();

        protected override async void OnClosing(WindowClosingEventArgs e)
        {
            if (VM != null && VM.HasUnsavedChanges && !_closeConfirmed)
            {
                e.Cancel = true;
                bool discard = await DialogHelper.AskYesNo(
                    "Discard unsaved trainer message changes?", "Unsaved Changes");
                if (discard) { _closeConfirmed = true; VM.DiscardChanges(); Close(); }
                return;
            }
            base.OnClosing(e);
        }

        private static async Task Safe(Task task)
        {
            if (task == null) return;
            try { await task; } catch { /* handled in VM */ }
        }
    }
}
