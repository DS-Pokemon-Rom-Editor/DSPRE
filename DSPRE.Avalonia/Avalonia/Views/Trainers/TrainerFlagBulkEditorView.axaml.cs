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
        }

        public TrainerFlagBulkEditorView(TrainerFlagBulkEditorViewModel vm) : this()
        {
            DataContext = vm;
        }

        private void ByTrainerMode_Click(object sender, RoutedEventArgs e) => VM?.SetMode(false);

        private void ByFlagMode_Click(object sender, RoutedEventArgs e) => VM?.SetMode(true);

        private void SaveAll_Click(object sender, RoutedEventArgs e) => VM?.SaveAllChanges();

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
