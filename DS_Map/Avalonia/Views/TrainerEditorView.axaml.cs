using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class TrainerEditorView : Window
    {
        private TrainerEditorViewModel VM => DataContext as TrainerEditorViewModel;
        private bool _setupDone;

        public TrainerEditorView()
        {
            InitializeComponent();
            Loaded += OnLoadedSetup;
        }

        public TrainerEditorView(TrainerEditorViewModel vm) : this()
        {
            DataContext = vm;
            EditorWindowChrome.Attach(this, vm, onClosed: vm.Detach);
        }

        private async void OnLoadedSetup(object sender, RoutedEventArgs e)
        {
            if (_setupDone || Design.IsDesignMode) return;
            var vm = VM;
            if (vm == null) return;
            _setupDone = true;
            await vm.SetupAsync(this);
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();

        private void Undo_Click(object sender, RoutedEventArgs e) => VM?.Undo();
        private void Redo_Click(object sender, RoutedEventArgs e) => VM?.Redo();

        private void BattleMessages_Click(object sender, RoutedEventArgs e)
        {
            int trainerId = VM?.SelectedTrainerIndex ?? 0;
            new BattleMessageEditorView(new BattleMessageEditorViewModel(trainerId)).Show();
        }

        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            var dlgVm = new TrainerSearchViewModel(VM.TrainerNames);
            var dlg = new TrainerSearchView(dlgVm);
            await dlg.ShowDialog(this);
            if (dlgVm.Confirmed) VM.GoToTrainer(dlgVm.ResultIndex);
        }

        private async void Reorder_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            var dlgVm = new MonReorderViewModel(VM.GetPartyForReorder());
            var dlg = new MonReorderView(dlgVm);
            await dlg.ShowDialog(this);
            if (dlgVm.Confirmed) VM.ReorderParty(dlgVm.ResultOrder);
        }

        private async void DVCalc_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            var input = VM.GetDVCalcInput();
            if (input.party.Count == 0) return;
            var dlgVm = new DVCalcViewModel(input.trainerId, input.trainerClass, input.party);
            var dlg = new DVCalcView(dlgVm);
            await dlg.ShowDialog(this);
            if (dlgVm.Confirmed)
            {
                var results = new System.Collections.Generic.List<(int dv, int gender, int ability)>();
                foreach (var s in dlgVm.Slots) results.Add(((int)s.DV, s.GenderIndex, s.AbilityIndex));
                VM.ApplyDVCalc(results);
            }
        }
    }
}
