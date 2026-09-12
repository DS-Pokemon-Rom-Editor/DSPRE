using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DSPRE.Avalonia.ViewModels.Graphics;

namespace DSPRE.Avalonia.Views.Graphics
{
    public partial class CellAnimationPickerView : Window
    {
        private CellAnimationPickerViewModel VM => DataContext as CellAnimationPickerViewModel;

        public CellAnimationPickerView()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public CellAnimationPickerView(CellAnimationPickerViewModel vm) : this()
        {
            DataContext = vm;
        }

        private void Open_Click(object sender, RoutedEventArgs e) => Open();

        private void List_DoubleTapped(object sender, TappedEventArgs e) => Open();

        private void Open()
        {
            var pick = VM?.Selected;
            if (pick == null) return;
            AvaloniaEditorLauncher.OpenCellAnimationEditor(
                pick.Archive, pick.Animation, pick.Cells, pick.Sprites, pick.Palette, 0,
                $"{pick.ArchiveName} animation {pick.Animation}");
        }
    }
}
