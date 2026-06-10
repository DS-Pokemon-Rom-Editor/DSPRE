using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class WildEditorDPPtView : Window
    {
        private WildEditorDPPtViewModel ViewModel => (WildEditorDPPtViewModel)DataContext;

        public WildEditorDPPtView(WildEditorDPPtViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            Closing += OnWindowClosing;
        }

        private async void OnWindowClosing(object sender, WindowClosingEventArgs e)
        {
            if (!ViewModel.HasUnsavedChanges) return;
            e.Cancel = true;
            if (await ViewModel.ConfirmCloseAsync())
            {
                Closing -= OnWindowClosing;
                Close();
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
            => await ViewModel.SaveCommand();
    }
}
