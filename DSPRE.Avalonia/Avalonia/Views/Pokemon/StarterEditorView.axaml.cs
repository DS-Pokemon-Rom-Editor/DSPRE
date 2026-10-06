using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using DSPRE.Avalonia.ViewModels;
using DSPRE.Avalonia.ViewModels.Pokemon;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class StarterEditorView : Window
    {
        private StarterEditorViewModel VM => (StarterEditorViewModel)DataContext;

        public StarterEditorView()
        {
            AvaloniaXamlLoader.Load(this);
            StarterEditorViewModel vm = new StarterEditorViewModel();
            DataContext = vm;
            EditorWindowChrome.Attach(this, vm, manageTitle: false, onClosed: vm.Detach);
        }

        private void Save_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e) => VM.SaveChanges();
        private void Discard_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e) => VM.DiscardChanges();

        private async void Manage_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            StarterCommandDialogView dialog = new StarterCommandDialogView(VM.NewCommandChoice());
            bool ok = await dialog.ShowDialog<bool>(this);
            if (ok) VM.ApplyCommandChoice((StarterCommandDialogViewModel)dialog.DataContext);
        }
    }
}
