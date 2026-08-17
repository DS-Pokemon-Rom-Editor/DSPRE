using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class PokemonSpriteEditorView : UserControl
    {
        private PokemonSpriteEditorViewModel VM => (PokemonSpriteEditorViewModel)DataContext;

        public PokemonSpriteEditorView() => InitializeComponent();

        private Window OwnerWindow => TopLevel.GetTopLevel(this) as Window;

        // --- Import handlers ---------------------------------------------------------
        private async void ImportFemaleBackNormal_Click(object sender, RoutedEventArgs e)
            => await VM.ImportSprite(0, OwnerWindow);
        private async void ImportMaleBackNormal_Click(object sender, RoutedEventArgs e)
            => await VM.ImportSprite(1, OwnerWindow);
        private async void ImportFemaleFrontNormal_Click(object sender, RoutedEventArgs e)
            => await VM.ImportSprite(2, OwnerWindow);
        private async void ImportMaleFrontNormal_Click(object sender, RoutedEventArgs e)
            => await VM.ImportSprite(3, OwnerWindow);

        // --- Export handlers ---------------------------------------------------------
        private async void ExportFemaleBackNormal_Click(object sender, RoutedEventArgs e)
            => await VM.ExportSprite(0, OwnerWindow);
        private async void ExportMaleBackNormal_Click(object sender, RoutedEventArgs e)
            => await VM.ExportSprite(1, OwnerWindow);
        private async void ExportFemaleFrontNormal_Click(object sender, RoutedEventArgs e)
            => await VM.ExportSprite(2, OwnerWindow);
        private async void ExportMaleFrontNormal_Click(object sender, RoutedEventArgs e)
            => await VM.ExportSprite(3, OwnerWindow);

        // --- Alternate forms toggle --------------------------------------------------
        private void ToggleAlternateForms_Click(object sender, RoutedEventArgs e)
            => VM.ToggleAlternateFormsMode();

        // --- Mono-gender / genderless sprite gap -------------------------------------
        private async void AddOppositeGenderSprites_Click(object sender, RoutedEventArgs e)
            => await VM.AddOppositeGenderSprites(OwnerWindow);
    }
}
