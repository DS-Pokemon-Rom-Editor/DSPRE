using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class LabelEditorView : Window
    {
        private LabelEditorViewModel VM => DataContext as LabelEditorViewModel;
        private bool _closeConfirmed;

        public LabelEditorView()
        {
            InitializeComponent();
            DataContext = new LabelEditorViewModel();
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();
        private void Add_Click(object sender, RoutedEventArgs e) => VM?.AddEntry();
        private void Reset_Click(object sender, RoutedEventArgs e) => VM?.ResetCategory();

        protected override async void OnClosing(WindowClosingEventArgs e)
        {
            if (VM != null && VM.HasUnsavedChanges && !_closeConfirmed)
            {
                e.Cancel = true;
                bool discard = await DialogHelper.AskYesNo(
                    "Discard unsaved label changes?", "Unsaved Changes");
                if (discard) { _closeConfirmed = true; VM.Discard(); Close(); }
                return;
            }
            base.OnClosing(e);
        }
    }
}
