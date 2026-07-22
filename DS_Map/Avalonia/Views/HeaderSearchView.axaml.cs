using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class HeaderSearchView : Window
    {
        private HeaderSearchViewModel VM => DataContext as HeaderSearchViewModel;

        public HeaderSearchView()
        {
            InitializeComponent();
            Closed += (_, _) => VM?.Dispose();
        }

        public HeaderSearchView(HeaderSearchViewModel vm) : this()
        {
            DataContext = vm;
        }

        private void Search_Click(object sender, RoutedEventArgs e) => VM?.Search();

        private void Result_DoubleTapped(object sender, TappedEventArgs e)
        {
            int id = VM?.SelectedHeaderId() ?? -1;
            if (id >= 0) AvaloniaEditorLauncher.OpenHeaderEditor(id);
        }
    }
}
