using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Graphics
{
    public partial class FontEditorView : Window
    {
        private FontEditorViewModel VM => DataContext as FontEditorViewModel;

        public FontEditorView()
        {
            AvaloniaXamlLoader.Load(this);
            DataContext = new FontEditorViewModel();
            EditorWindowChrome.Attach(this, VM);
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.SaveChanges();
    }
}
