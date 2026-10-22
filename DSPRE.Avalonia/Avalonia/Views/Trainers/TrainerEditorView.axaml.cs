using System.Linq;
using System.Collections.Generic;
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

        private void DesignCapsule_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Control)?.DataContext is ViewModels.Trainers.TrainerPartyMonViewModel mon)
                if (!mon.CustomPokeballsEnabled) AvaloniaEditorLauncher.OpenBallCapsuleEditorAt((int)mon.BallSeals);
        }

        public TrainerEditorView()
        {
            InitializeComponent();
            Loaded += OnLoadedSetup;
        }

        public TrainerEditorView(TrainerEditorViewModel vm) : this()
        {
            DataContext = vm;
            EditorWindowChrome.Attach(this, vm);
            vm.TrainerList.KeepInView(TrainerList);
            // Another trainer starts at its top, not wherever the last one was scrolled to.
            vm.PropertyChanged += (_, e) =>
            {
                // After the cards relayout, or their growth scrolls it back down.
                if (e.PropertyName == nameof(TrainerEditorViewModel.SelectedTrainerIndex))
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() => DetailScroll.Offset = default, global::Avalonia.Threading.DispatcherPriority.Background);
            };
        }

        private async void AiNotes_Click(object sender, RoutedEventArgs e)
            => await Launcher.LaunchUriAsync(new System.Uri(TrainerEditorViewModel.AiNotesUrl));

        private async void OnLoadedSetup(object sender, RoutedEventArgs e)
        {
            if (_setupDone || Design.IsDesignMode) return;
            TrainerEditorViewModel vm = VM;
            if (vm == null) return;
            _setupDone = true;
            await vm.SetupAsync(this);

            // The Classes tab is a separate ViewModel (trainer classes aren't per-trainer data),
            // give it its own instance now that the ROM/trainer data is actually loaded, starting
            // on whichever class the current trainer uses.
            ClassesTabView.DataContext = vm.Classes = new TrainerClassesViewModel(vm.TrainerClassIndex);
            vm.Classes.OpenTrainer = id => { MainTabs.SelectedIndex = 0; vm.GoToTrainer(id); };
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
            => await DialogHelper.GuardAsync(() => { VM?.Save(); return System.Threading.Tasks.Task.CompletedTask; }, "The trainer wasn't saved.", ErrorTitle, this);
        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) VM?.DiscardTrainer();
        }

        private void Undo_Click(object sender, RoutedEventArgs e) => VM?.Undo();
        private void Redo_Click(object sender, RoutedEventArgs e) => VM?.Redo();

        private void AddTrainer_Click(object sender, RoutedEventArgs e) => VM?.AddTrainer();
        private async void RemoveTrainer_Click(object sender, RoutedEventArgs e)
            => await DialogHelper.GuardAsync(() => VM?.RemoveLastAddedTrainerAsync() ?? Task.CompletedTask, "The trainer wasn't removed.", ErrorTitle);

        private void BattleMessages_Click(object sender, RoutedEventArgs e)
        {
            AvaloniaEditorLauncher.OpenBattleMessageEditor(VM?.SelectedTrainerIndex ?? 0);
        }

        private void TrainerClasses_Click(object sender, RoutedEventArgs e)
        {
            int classId = VM?.TrainerClassIndex ?? 0;
            if (ClassesTabView.DataContext is TrainerClassesViewModel classesVm)
                classesVm.SelectedClassIndex = classId;
            MainTabs.SelectedIndex = 1;
        }

        private async void ExportTrainer_Click(object sender, RoutedEventArgs e) => await DialogHelper.GuardAsync(() => VM?.ExportTrainerAsync() ?? Task.CompletedTask, "The trainer wasn't exported.", ErrorTitle);
        private async void ImportTrainer_Click(object sender, RoutedEventArgs e) => await DialogHelper.GuardAsync(() => VM?.ImportTrainerAsync() ?? Task.CompletedTask, "The trainer wasn't imported.", ErrorTitle);
        private async void ExportProperties_Click(object sender, RoutedEventArgs e) => await DialogHelper.GuardAsync(() => VM?.ExportPropertiesAsync() ?? Task.CompletedTask, "The trainer properties weren't exported.", ErrorTitle);
        private async void ImportProperties_Click(object sender, RoutedEventArgs e) => await DialogHelper.GuardAsync(() => VM?.ReplacePropertiesAsync() ?? Task.CompletedTask, "The trainer properties weren't imported.", ErrorTitle);
        private async void ExportParty_Click(object sender, RoutedEventArgs e) => await DialogHelper.GuardAsync(() => VM?.ExportPartyAsync() ?? Task.CompletedTask, "The party wasn't exported.", ErrorTitle);
        private async void ImportParty_Click(object sender, RoutedEventArgs e) => await DialogHelper.GuardAsync(() => VM?.ImportPartyAsync() ?? Task.CompletedTask, "The party wasn't imported.", ErrorTitle);

        private const string ErrorTitle = "Trainer editor error";

        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            TrainerSearchViewModel dlgVm = new TrainerSearchViewModel(VM.TrainerNames, VM.TrainerClassItems, VM.AiFlags.Select(f => f.Label),
                VM.PokemonNames, VM.ItemNames);
            TrainerSearchView dlg = new TrainerSearchView(dlgVm);
            await dlg.ShowDialog(this);
            if (dlgVm.Confirmed) VM.GoToTrainer(dlgVm.ResultIndex);
        }

        private void PlaySprite_Click(object sender, RoutedEventArgs e) => VM?.TogglePlay();
        private async void FindUses_Click(object sender, RoutedEventArgs e) => await DialogHelper.GuardAsync(() => VM?.FindUsesAsync() ?? Task.CompletedTask, "The search for uses didn't finish.", ErrorTitle);

        private void Use_DoubleTapped(object sender, global::Avalonia.Input.TappedEventArgs e)
        {
            if ((sender as Control)?.DataContext is TrainerEditorViewModel.TrainerUse use) VM?.OpenUse(use);
        }

        private void MonUp_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Control)?.DataContext is ViewModels.Trainers.TrainerPartyMonViewModel mon) VM?.MoveMon(mon, -1);
        }

        private async void AllNatures_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null || (sender as Control)?.DataContext is not ViewModels.Trainers.TrainerPartyMonViewModel mon) return;
            ViewModels.Pokemon.DVCalcNatureViewerViewModel picker = new ViewModels.Pokemon.DVCalcNatureViewerViewModel(VM.AllNatureOptions(mon));
            await new Views.Pokemon.DVCalcNatureViewerView(picker).ShowDialog(this);
            if (picker.SelectedDV >= 0) mon.Difficulty = picker.SelectedDV;
        }

        private void MonDown_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Control)?.DataContext is ViewModels.Trainers.TrainerPartyMonViewModel mon) VM?.MoveMon(mon, 1);
        }
    }
}
