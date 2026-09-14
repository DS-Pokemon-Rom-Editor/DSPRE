using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DSPRE.Avalonia.ViewModels.Graphics;

namespace DSPRE.Avalonia.Views.Graphics
{
    public partial class ParticleLibraryView : Window
    {
        private ParticleLibraryViewModel VM => DataContext as ParticleLibraryViewModel;

        public ParticleLibraryView() { AvaloniaXamlLoader.Load(this); }

        public ParticleLibraryView(ParticleLibraryViewModel vm) : this() { DataContext = vm; }

        private void Open_Click(object sender, RoutedEventArgs e) => Open();
        private void List_DoubleTapped(object sender, TappedEventArgs e) => Open();

        private void Open()
        {
            var pick = VM?.Selected;
            if (pick == null) return;
            AvaloniaEditorLauncher.OpenParticleEditor(pick.Source, pick.Index, pick.Name, null, pick.Orthographic);
        }
    }
}
