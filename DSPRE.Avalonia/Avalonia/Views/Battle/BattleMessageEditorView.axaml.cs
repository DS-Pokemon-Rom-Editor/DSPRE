using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Battle
{
    public partial class BattleMessageEditorView : Window
    {
        private BattleMessageEditorViewModel VM => DataContext as BattleMessageEditorViewModel;
        private bool _setupDone;

        public BattleMessageEditorView()
        {
            InitializeComponent();
            Loaded += OnLoadedSetup;
        }

        public BattleMessageEditorView(BattleMessageEditorViewModel vm) : this()
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
        }

        private async void Save_Click(object sender, RoutedEventArgs e) => await Safe(VM?.SaveAsync());
        private void Add_Click(object sender, RoutedEventArgs e) => VM?.AddEntry();
        private void Delete_Click(object sender, RoutedEventArgs e) => VM?.DeleteEntry();
        private void EditTrigger_Click(object sender, RoutedEventArgs e) => VM?.EditTrigger();
        private void SaveMessage_Click(object sender, RoutedEventArgs e) => VM?.SaveMessage();

        private static async Task Safe(Task task)
        {
            if (task == null) return;
            try { await task; } catch { /* handled in VM */ }
        }
    }
}
