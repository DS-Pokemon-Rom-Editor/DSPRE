using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Tools
{
    public partial class LabelEditorView : Window
    {
        private LabelEditorViewModel VM => DataContext as LabelEditorViewModel;

        public LabelEditorView()
        {
            InitializeComponent();
            LabelEditorViewModel vm = new LabelEditorViewModel();
            DataContext = vm;
            EditorWindowChrome.Attach(this, vm);
            EditorWindowChrome.AttachUndoKeys(this, vm);
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();
        private void Add_Click(object sender, RoutedEventArgs e) => VM?.AddEntry();
        private void Reset_Click(object sender, RoutedEventArgs e) => VM?.ResetCategory();

        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) VM?.Discard();
        }
    }
}
