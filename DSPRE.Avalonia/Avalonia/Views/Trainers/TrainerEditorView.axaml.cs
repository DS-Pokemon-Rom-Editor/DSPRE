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
                AvaloniaEditorLauncher.OpenBallCapsuleEditorAt((int)mon.BallSeals);
        }

        public TrainerEditorView()
        {
            InitializeComponent();
            Loaded += OnLoadedSetup;
        }

        public TrainerEditorView(TrainerEditorViewModel vm) : this()
        {
            DataContext = vm;
            EditorWindowChrome.Attach(this, vm, onClosed: vm.Detach);
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

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();
        private void Discard_Click(object sender, RoutedEventArgs e) => VM?.DiscardTrainer();

        private void Undo_Click(object sender, RoutedEventArgs e) => VM?.Undo();
        private void Redo_Click(object sender, RoutedEventArgs e) => VM?.Redo();

        private void AddTrainer_Click(object sender, RoutedEventArgs e) => VM?.AddTrainer();
        private async void RemoveTrainer_Click(object sender, RoutedEventArgs e)
            => await Safe(VM?.RemoveLastAddedTrainerAsync());

        private void BattleMessages_Click(object sender, RoutedEventArgs e)
        {
            // hg-engine builds the message table from Trainers.c, which only a linked checkout can edit.
            if (AvaloniaEditorLauncher.Refused("BattleMessageEditorView")) return;

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
            TrainerSearchViewModel dlgVm = new TrainerSearchViewModel(VM.TrainerNames, VM.TrainerClassItems, VM.AiFlags.Select(f => f.Label),
                VM.PokemonNames, VM.ItemNames);
            TrainerSearchView dlg = new TrainerSearchView(dlgVm);
            await dlg.ShowDialog(this);
            if (dlgVm.Confirmed) VM.GoToTrainer(dlgVm.ResultIndex);
        }

        private void PlaySprite_Click(object sender, RoutedEventArgs e) => VM?.TogglePlay();
        private async void FindUses_Click(object sender, RoutedEventArgs e) => await Safe(VM?.FindUsesAsync());

        private void Use_DoubleTapped(object sender, global::Avalonia.Input.TappedEventArgs e)
        {
            if ((sender as Control)?.DataContext is TrainerEditorViewModel.TrainerUse use) VM?.OpenUse(use);
        }

        private void MonUp_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Control)?.DataContext is ViewModels.Trainers.TrainerPartyMonViewModel mon) VM?.MoveMon(mon, -1);
        }

        private void MonDown_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Control)?.DataContext is ViewModels.Trainers.TrainerPartyMonViewModel mon) VM?.MoveMon(mon, 1);
        }
    }
}
