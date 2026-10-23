using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;
using DSPRE.Avalonia.Views.Controls;
using DSPRE.ROMFiles;

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

        private void PreviewNext_Click(object sender, RoutedEventArgs e) => VM?.NextPreviewStep();

        private async void OnLoadedSetup(object sender, RoutedEventArgs e)
        {
            if (_setupDone || Design.IsDesignMode) return;
            BattleMessageEditorViewModel vm = VM;
            if (vm == null) return;
            _setupDone = true;
            // The game's own letters and a border; the Text editor's chosen border is left alone when already loaded.
            FieldMessageBoxView.Font ??= FieldFont.LoadTalkFont();
            FieldMessageBoxView.Frame ??= DSPRE.Avalonia.Data.WindowFrames.Load(0);
            vm.MeasureText = FieldMessageBoxView.Measure;
            vm.RefreshPreview();
            await vm.SetupAsync(this);
        }

        private async void Save_Click(object sender, RoutedEventArgs e) => await DialogHelper.GuardAsync(() => VM?.SaveAsync() ?? Task.CompletedTask, "The trainer text wasn't saved.", ErrorTitle);
        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) VM?.DiscardChanges();
        }
        private void Add_Click(object sender, RoutedEventArgs e) => VM?.AddEntry();
        private void Delete_Click(object sender, RoutedEventArgs e) => VM?.DeleteEntry();
        private void EditTrigger_Click(object sender, RoutedEventArgs e) => VM?.EditTrigger();
        private void SaveMessage_Click(object sender, RoutedEventArgs e) => VM?.SaveMessage();

        private const string ErrorTitle = "Trainer battle messages";
    }
}
