using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Trainers;

namespace DSPRE.Avalonia.Views.Trainers
{
    public partial class VsIntroEditorView : UserControl
    {
        private VsIntroEditorViewModel VM => DataContext as VsIntroEditorViewModel;

        public VsIntroEditorView() { InitializeComponent(); }

        public VsIntroEditorView(VsIntroEditorViewModel vm) : this()
        {
            DataContext = vm;
            DetachedFromVisualTree += (_, _) => vm.StopAnimation();
        }

        private async void Save_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.SaveChangesAsync(); }
        private void Discard_Click(object sender, RoutedEventArgs e) => VM?.DiscardChanges();

        private void ClassesAndMusic_Click(object sender, RoutedEventArgs e) => VM?.ShowClassesAndMusic();
        private async void MakeClassRoom_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.MakeClassRoomAsync(); }
        private void ClassColours_Click(object sender, RoutedEventArgs e) => VM?.EditClassColours();
        private void PaintFace_Click(object sender, RoutedEventArgs e) => VM?.PaintFace();
        private void AnimateFace_Click(object sender, RoutedEventArgs e) => VM?.AnimateFace();
        private void PaintBanner_Click(object sender, RoutedEventArgs e) => VM?.PaintBanner();
        private void BannerGraphics_Click(object sender, RoutedEventArgs e) => VM?.ShowBannerInGraphics();
        private void Animate_Click(object sender, RoutedEventArgs e) => VM?.ToggleAnimation();
        private void ShowMugshot_Click(object sender, RoutedEventArgs e) => VM?.ShowMugshot();

        private void Particle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Control { Tag: ParticleArt p }) VM?.OpenParticles(p);
        }

        private void Fixed_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Control { Tag: FixedArt f }) f.Open?.Invoke();
        }

        private void IntroClass_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox list && list.SelectedIndex >= 0) VM?.GoToClass(list.SelectedIndex);
        }
    }
}
