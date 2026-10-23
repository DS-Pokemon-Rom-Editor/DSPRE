using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Pokemon
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

        /// <summary>Hands the sprites shown to All graphics, which can paint them by hand.</summary>
        private void OpenInGraphics_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            (DSPRE.RomInfo.DirNames dir, int member) = VM.GraphicsTarget;
            AvaloniaEditorLauncher.OpenGraphicAt(dir, member);
        }

        private void OpenFollowerInOverworldEditor_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null || VM.FollowerSpriteEntry < 0) return;
            AvaloniaEditorLauncher.OpenOverworldEditor((uint)VM.FollowerSpriteEntry);
        }

        private async void OpenImportWizard_Click(object sender, RoutedEventArgs e)
        {
            SpriteImportWizardViewModel wizardVm = new SpriteImportWizardViewModel(VM, OwnerWindow);
            SpriteImportWizardView wizard = new SpriteImportWizardView(wizardVm);
            await wizard.ShowDialog(OwnerWindow);
        }

        private async void OpenExportWizard_Click(object sender, RoutedEventArgs e)
        {
            SpriteExportWizardViewModel wizardVm = new SpriteExportWizardViewModel(VM, OwnerWindow);
            SpriteExportWizardView wizard = new SpriteExportWizardView(wizardVm);
            await wizard.ShowDialog(OwnerWindow);
        }

        // Each pose and colour picks its own preview frame.
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

        private async void ImportFemaleBackNormal_Click(object sender, RoutedEventArgs e)
            => await VM.ImportSpriteAsync(0, OwnerWindow);
        private async void ImportMaleBackNormal_Click(object sender, RoutedEventArgs e)
            => await VM.ImportSpriteAsync(1, OwnerWindow);
        private async void ImportFemaleFrontNormal_Click(object sender, RoutedEventArgs e)
            => await VM.ImportSpriteAsync(2, OwnerWindow);
        private async void ImportMaleFrontNormal_Click(object sender, RoutedEventArgs e)
            => await VM.ImportSpriteAsync(3, OwnerWindow);

        private async void ExportFemaleBackNormal_Click(object sender, RoutedEventArgs e)
            => await VM.ExportSpriteAsync(0, OwnerWindow);
        private async void ExportMaleBackNormal_Click(object sender, RoutedEventArgs e)
            => await VM.ExportSpriteAsync(1, OwnerWindow);
        private async void ExportFemaleFrontNormal_Click(object sender, RoutedEventArgs e)
            => await VM.ExportSpriteAsync(2, OwnerWindow);
        private async void ExportMaleFrontNormal_Click(object sender, RoutedEventArgs e)
            => await VM.ExportSpriteAsync(3, OwnerWindow);

        // Shiny imports only take the palette; the artwork is shared with Normal.
        private async void ImportFemaleBackShiny_Click(object sender, RoutedEventArgs e)
            => await VM.ImportShinyPaletteAsync(0, OwnerWindow);
        private async void ImportMaleBackShiny_Click(object sender, RoutedEventArgs e)
            => await VM.ImportShinyPaletteAsync(1, OwnerWindow);
        private async void ImportFemaleFrontShiny_Click(object sender, RoutedEventArgs e)
            => await VM.ImportShinyPaletteAsync(2, OwnerWindow);
        private async void ImportMaleFrontShiny_Click(object sender, RoutedEventArgs e)
            => await VM.ImportShinyPaletteAsync(3, OwnerWindow);

        private async void ExportFemaleBackShiny_Click(object sender, RoutedEventArgs e)
            => await VM.ExportSpriteAsync(0, OwnerWindow, shiny: true);
        private async void ExportMaleBackShiny_Click(object sender, RoutedEventArgs e)
            => await VM.ExportSpriteAsync(1, OwnerWindow, shiny: true);
        private async void ExportFemaleFrontShiny_Click(object sender, RoutedEventArgs e)
            => await VM.ExportSpriteAsync(2, OwnerWindow, shiny: true);
        private async void ExportMaleFrontShiny_Click(object sender, RoutedEventArgs e)
            => await VM.ExportSpriteAsync(3, OwnerWindow, shiny: true);

        private async void AddOppositeGenderSprites_Click(object sender, RoutedEventArgs e)
            => await VM.AddOppositeGenderSpritesAsync(OwnerWindow);

        private async void ImportFemaleSheet_Click(object sender, RoutedEventArgs e)
            => await VM.ImportSpriteSheetAsync(OwnerWindow, female: true);
        private async void ImportMaleSheet_Click(object sender, RoutedEventArgs e)
            => await VM.ImportSpriteSheetAsync(OwnerWindow, female: false);
        private async void ExportFemaleSheet_Click(object sender, RoutedEventArgs e)
            => await VM.ExportSpriteSheetAsync(OwnerWindow, female: true);
        private async void ExportMaleSheet_Click(object sender, RoutedEventArgs e)
            => await VM.ExportSpriteSheetAsync(OwnerWindow, female: false);
        private async void ImportFemaleShinySheet_Click(object sender, RoutedEventArgs e)
            => await VM.ImportShinySpriteSheetAsync(OwnerWindow, female: true);
        private async void ImportMaleShinySheet_Click(object sender, RoutedEventArgs e)
            => await VM.ImportShinySpriteSheetAsync(OwnerWindow, female: false);
        private async void ExportFemaleShinySheet_Click(object sender, RoutedEventArgs e)
            => await VM.ExportSpriteSheetAsync(OwnerWindow, female: true, shiny: true);
        private async void ExportMaleShinySheet_Click(object sender, RoutedEventArgs e)
            => await VM.ExportSpriteSheetAsync(OwnerWindow, female: false, shiny: true);

        // The both-genders sheet only shows for species with separate male and female art.
        private async void ImportFullSheet_Click(object sender, RoutedEventArgs e)
            => await VM.ImportFullSheetAsync(OwnerWindow);
        private async void ExportFullSheet_Click(object sender, RoutedEventArgs e)
            => await VM.ExportFullSheetAsync(OwnerWindow);
        private async void ImportShinyFullSheet_Click(object sender, RoutedEventArgs e)
            => await VM.ImportShinyFullSheetAsync(OwnerWindow);
        private async void ExportShinyFullSheet_Click(object sender, RoutedEventArgs e)
            => await VM.ExportFullSheetAsync(OwnerWindow, shiny: true);

        private void PaletteSwatch_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not PokemonSpriteEditorViewModel.PaletteSwatch swatch) return;
            PaletteColorEditorViewModel vm = new PaletteColorEditorViewModel(VM, swatch.Shiny, swatch.Index);
            PaletteColorEditorView popup = new PaletteColorEditorView(vm);
            popup.Show(OwnerWindow);
        }
    }
}
