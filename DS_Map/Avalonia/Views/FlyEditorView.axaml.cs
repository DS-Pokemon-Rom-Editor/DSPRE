using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class FlyEditorView : Window
    {
        private FlyEditorViewModel _vm;

        public FlyEditorView(System.Collections.Generic.List<string> headerNames)
        {
            AvaloniaXamlLoader.Load(this);
            _vm = new FlyEditorViewModel(headerNames);
            DataContext = _vm;
            Closing += OnWindowClosing;
        }

        private async void OnWindowClosing(object sender, WindowClosingEventArgs e)
        {
            e.Cancel = true;
            if (await _vm.ConfirmCloseAsync())
            {
                Closing -= OnWindowClosing;
                Close();
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
            => await _vm.SaveCommand();
    }
}
