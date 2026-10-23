using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.Models;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Trainers
{
    public partial class TrainerFlagBulkEditorView : UserControl
    {
        private TrainerFlagBulkEditorViewModel VM => DataContext as TrainerFlagBulkEditorViewModel;

        public TrainerFlagBulkEditorView()
        {
            InitializeComponent();
            NameRefresh.Bind(this, () => VM?.RefreshNames());
        }

        public TrainerFlagBulkEditorView(TrainerFlagBulkEditorViewModel vm) : this()
        {
            DataContext = vm;
        }

        private void ByTrainerMode_Click(object sender, RoutedEventArgs e) => VM?.SetMode(false);

        private void ByFlagMode_Click(object sender, RoutedEventArgs e) => VM?.SetMode(true);

        private async void SaveAll_Click(object sender, RoutedEventArgs e)
            => await DialogHelper.GuardAsync(() => { VM?.SaveAllChanges(); return System.Threading.Tasks.Task.CompletedTask; }, "The trainer flags weren't saved.", "Trainer flag bulk editor");

        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) VM?.DiscardChanges();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e) => VM?.SetAllVisibleLeavesChecked(true);

        private void SelectNone_Click(object sender, RoutedEventArgs e) => VM?.SetAllVisibleLeavesChecked(false);

        private void FlagCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            if ((sender as CheckBox)?.DataContext is FlagChecklistItem item)
                VM.ToggleFlagForSelection(item.Index);
        }
    }
}
