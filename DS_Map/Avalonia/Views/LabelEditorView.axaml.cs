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
                var r = await DialogHelper.AskYesNoCancel(
                    "You have unsaved changes. Do you want to save them before closing?", "Unsaved Changes");
                if (r == DialogHelper.MsgResult.Cancel) return;   // stay open
                if (r == DialogHelper.MsgResult.Yes) VM.Save(); else VM.Discard();
                _closeConfirmed = true; Close();
                return;
            }
            base.OnClosing(e);
        }
    }
}
