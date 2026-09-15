using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Trainers
{
    public partial class TrainerClassesView : UserControl
    {
        private TrainerClassesViewModel VM => DataContext as TrainerClassesViewModel;

        public TrainerClassesView()
        {
            InitializeComponent();
        }

        public TrainerClassesView(TrainerClassesViewModel vm) : this()
        {
            DataContext = vm;
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();

        private void Discard_Click(object sender, RoutedEventArgs e) => VM?.DiscardChanges();

        private void TogglePlay_Click(object sender, RoutedEventArgs e) => VM?.TogglePlay();

        // Adds the entry with music = 0/0 so the Main/Alt fields can be set before Save writes it.
        private void EnableMusic_Click(object sender, RoutedEventArgs e) => VM?.EnableMusic(0, 0);

        private void EditSprite_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null || VM.SelectedClassIndex < 0 || !VM.CanEditSprite) return;
            var classesVm = VM;
            var vm = new TrainerSpriteEditorViewModel(VM.SelectedClassIndex);
            var win = new TrainerSpriteEditorView(vm);
            win.Closed += (_, __) => classesVm.RefreshSpritePreview();
            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner != null) win.Show(owner);
            else win.Show();
        }

        private async void AddTrainerClass_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            if (!VM.CanAddClass)
            {
                await DialogHelper.ShowError("Save or discard the new trainer class first.", "Add Trainer Class");
                return;
            }
            // The new class is selected once it is added, so the loaded class is settled first.
            if (!await VM.ConfirmLeaveAsync()) return;

            var dlgVm = new AddTrainerClassViewModel();
            var dlg = new AddTrainerClassView(dlgVm);
            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner != null) await dlg.ShowDialog(owner);
            else dlg.Show();

            if (!dlgVm.Confirmed) return;

            string error = VM.AddTrainerClass(dlgVm.ClassName, dlgVm.Description, (byte)dlgVm.GenderIndex, (byte)dlgVm.PrizeMultiplier,
                dlgVm.AddMusic, (ushort)dlgVm.MusicMain, 0);
            if (error != null)
                await DialogHelper.ShowError(error, "Add Trainer Class");
        }
    }
}
