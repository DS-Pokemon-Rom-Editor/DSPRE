using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Trainers
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

            // The Classes tab is a separate ViewModel (trainer classes aren't per-trainer data),
            // give it its own instance now that the ROM/trainer data is actually loaded, starting
            // on whichever class the current trainer uses.
            ClassesTabView.DataContext = new TrainerClassesViewModel(vm.TrainerClassIndex);
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();

        private void Undo_Click(object sender, RoutedEventArgs e) => VM?.Undo();
        private void Redo_Click(object sender, RoutedEventArgs e) => VM?.Redo();

        private void AddTrainer_Click(object sender, RoutedEventArgs e) => VM?.AddTrainer();

        private void BattleMessages_Click(object sender, RoutedEventArgs e)
        {
            int trainerId = VM?.SelectedTrainerIndex ?? 0;
            new BattleMessageEditorView(new BattleMessageEditorViewModel(trainerId)).ShowManaged();
        }

        private void TrainerClasses_Click(object sender, RoutedEventArgs e)
        {
            int classId = VM?.TrainerClassIndex ?? 0;
            if (ClassesTabView.DataContext is TrainerClassesViewModel classesVm)
                classesVm.SelectedClassIndex = classId;
            MainTabs.SelectedIndex = 1;
        }

        private async void ExportTrainer_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ExportTrainerAsync());
        private async void ImportTrainer_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ImportTrainerAsync());
        private async void ExportProperties_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ExportPropertiesAsync());
        private async void ImportProperties_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ReplacePropertiesAsync());
        private async void ExportParty_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ExportPartyAsync());
        private async void ImportParty_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ImportPartyAsync());

        private static async Task Safe(Task task)
        {
            if (task == null) return;
            try { await task; } catch { /* handled in VM */ }
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
