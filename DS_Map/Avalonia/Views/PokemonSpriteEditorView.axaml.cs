using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class PokemonSpriteEditorView : UserControl
    {
        private PokemonSpriteEditorViewModel VM => (PokemonSpriteEditorViewModel)DataContext;

        public PokemonSpriteEditorView()
        {
            InitializeComponent();
            Unloaded += (_, __) => VM?.StopFrameAnimation();
        }

        private Window OwnerWindow => TopLevel.GetTopLevel(this) as Window;

        // --- Import Wizard -------------------------------------------------------------
        private async void OpenImportWizard_Click(object sender, RoutedEventArgs e)
        {
            var wizardVm = new SpriteImportWizardViewModel(VM, OwnerWindow);
            var wizard = new SpriteImportWizardView(wizardVm);
            await wizard.ShowDialog(OwnerWindow);
        }

        // --- Frame preview controls: each pose/color picks its frame independently ---------------
        private void FemaleBackNormalFrame1_Click(object sender, RoutedEventArgs e) => VM.FemaleBackNormalFrame.Frame = 0;
        private void FemaleBackNormalFrame2_Click(object sender, RoutedEventArgs e) => VM.FemaleBackNormalFrame.Frame = 1;
        private void MaleBackNormalFrame1_Click(object sender, RoutedEventArgs e) => VM.MaleBackNormalFrame.Frame = 0;
        private void MaleBackNormalFrame2_Click(object sender, RoutedEventArgs e) => VM.MaleBackNormalFrame.Frame = 1;
        private void FemaleFrontNormalFrame1_Click(object sender, RoutedEventArgs e) => VM.FemaleFrontNormalFrame.Frame = 0;
        private void FemaleFrontNormalFrame2_Click(object sender, RoutedEventArgs e) => VM.FemaleFrontNormalFrame.Frame = 1;
        private void MaleFrontNormalFrame1_Click(object sender, RoutedEventArgs e) => VM.MaleFrontNormalFrame.Frame = 0;
        private void MaleFrontNormalFrame2_Click(object sender, RoutedEventArgs e) => VM.MaleFrontNormalFrame.Frame = 1;
        private void FemaleBackShinyFrame1_Click(object sender, RoutedEventArgs e) => VM.FemaleBackShinyFrame.Frame = 0;
        private void FemaleBackShinyFrame2_Click(object sender, RoutedEventArgs e) => VM.FemaleBackShinyFrame.Frame = 1;
        private void MaleBackShinyFrame1_Click(object sender, RoutedEventArgs e) => VM.MaleBackShinyFrame.Frame = 0;
        private void MaleBackShinyFrame2_Click(object sender, RoutedEventArgs e) => VM.MaleBackShinyFrame.Frame = 1;
        private void FemaleFrontShinyFrame1_Click(object sender, RoutedEventArgs e) => VM.FemaleFrontShinyFrame.Frame = 0;
        private void FemaleFrontShinyFrame2_Click(object sender, RoutedEventArgs e) => VM.FemaleFrontShinyFrame.Frame = 1;
        private void MaleFrontShinyFrame1_Click(object sender, RoutedEventArgs e) => VM.MaleFrontShinyFrame.Frame = 0;
        private void MaleFrontShinyFrame2_Click(object sender, RoutedEventArgs e) => VM.MaleFrontShinyFrame.Frame = 1;
        private void AnimateFrames_Click(object sender, RoutedEventArgs e) => VM.AnimateFrames = !VM.AnimateFrames;

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

        // --- Shiny import handlers (derive palette only; artwork is shared with Normal) ---------
        private async void ImportFemaleBackShiny_Click(object sender, RoutedEventArgs e)
            => await VM.ImportShinyPalette(0, OwnerWindow);
        private async void ImportMaleBackShiny_Click(object sender, RoutedEventArgs e)
            => await VM.ImportShinyPalette(1, OwnerWindow);
        private async void ImportFemaleFrontShiny_Click(object sender, RoutedEventArgs e)
            => await VM.ImportShinyPalette(2, OwnerWindow);
        private async void ImportMaleFrontShiny_Click(object sender, RoutedEventArgs e)
            => await VM.ImportShinyPalette(3, OwnerWindow);

        // --- Shiny export handlers ----------------------------------------------------
        private async void ExportFemaleBackShiny_Click(object sender, RoutedEventArgs e)
            => await VM.ExportSprite(0, OwnerWindow, shiny: true);
        private async void ExportMaleBackShiny_Click(object sender, RoutedEventArgs e)
            => await VM.ExportSprite(1, OwnerWindow, shiny: true);
        private async void ExportFemaleFrontShiny_Click(object sender, RoutedEventArgs e)
            => await VM.ExportSprite(2, OwnerWindow, shiny: true);
        private async void ExportMaleFrontShiny_Click(object sender, RoutedEventArgs e)
            => await VM.ExportSprite(3, OwnerWindow, shiny: true);

        // --- Alternate forms toggle --------------------------------------------------
        private void ToggleAlternateForms_Click(object sender, RoutedEventArgs e)
            => VM.ToggleAlternateFormsMode();

        // --- Mono-gender / genderless sprite gap -------------------------------------
        private async void AddOppositeGenderSprites_Click(object sender, RoutedEventArgs e)
            => await VM.AddOppositeGenderSprites(OwnerWindow);
    }
}
