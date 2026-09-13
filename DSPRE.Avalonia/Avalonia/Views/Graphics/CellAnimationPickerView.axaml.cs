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
                pick.Source, pick.Animation, pick.Cells, pick.Sprites, pick.Palette, pick.PaletteRow,
                pick.Label == null ? $"{pick.ArchiveName} animation {pick.Animation}" : $"{pick.Label}, {pick.ArchiveName} animation {pick.Animation}");
        }

        // Some archives already have an editor that knows far more about them than this window does.
        // Routing on the name the graphics census records keeps this from guessing at archives.
        private void DeepEditor_Click(object sender, RoutedEventArgs e)
        {
            var pick = VM?.Selected;
            if (pick?.DeepEditor == null) return;

            // The animation's number says which class or Pokemon; files per trainer class vary by game.
            switch (pick.DeepEditor)
            {
                case "Trainer Sprite Editor":
                    AvaloniaEditorLauncher.OpenTrainerSpriteEditor(TrainerGraphicsLayout.ClassOf(pick.Animation));
                    break;
                case "Trainer Back Sprite Editor":
                    AvaloniaEditorLauncher.OpenTrainerBackSpriteEditor(TrainerGraphicsLayout.ClassOf(pick.Animation));
                    break;
                case "Pokemon Sprite Editor":
                case "Pokemon Editor":
                    _ = AvaloniaEditorLauncher.OpenPokemonEditorAsync(pick.Animation / 6);
                    break;
                case "Bottom Screen Editor":
                    AvaloniaEditorLauncher.OpenBottomScreenEditor();
                    break;
            }
        }
    }
}
