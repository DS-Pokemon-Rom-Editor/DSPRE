using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Graphics;

namespace DSPRE.Avalonia.Views.Graphics
{
    public partial class BannerLayoutView : UserControl
    {
        private BannerLayoutViewModel VM => DataContext as BannerLayoutViewModel;

        public BannerLayoutView() => InitializeComponent();

        public BannerLayoutView(BannerLayoutViewModel vm) : this() => DataContext = vm;

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.SaveChanges();
        private void Patch_Click(object sender, RoutedEventArgs e) => VM?.OpenPatch();
        private void MotionPatch_Click(object sender, RoutedEventArgs e) => VM?.OpenMotionPatch();
        private void Play_Click(object sender, RoutedEventArgs e) => VM?.Play();

        private Point? _grab;

        // Grabs the banner where it was pressed, so it moves by whole tiles from there.
        private void Preview_Pressed(object sender, PointerPressedEventArgs e)
        {
            if (VM is not BannerLayoutViewModel vm || !vm.Patched || sender is not Control image) return;
            Point p = e.GetPosition(image);
            (int x, int y, int w, int h) = vm.Box;
            if (p.X < x || p.Y < y || p.X >= x + w || p.Y >= y + h) return;
            _grab = new Point(p.X - x, p.Y - y);
            e.Pointer.Capture(image);
        }

        private void Preview_Moved(object sender, PointerEventArgs e)
        {
            if (_grab is not Point grab || VM is not BannerLayoutViewModel vm || sender is not Control image) return;
            Point p = e.GetPosition(image);
            vm.DragTo((int)System.Math.Round((p.X - grab.X) / 8), (int)System.Math.Round((p.Y - grab.Y) / 8));
        }

        private void Preview_Released(object sender, PointerReleasedEventArgs e)
        {
            if (_grab == null) return;
            _grab = null;
            e.Pointer.Capture(null);
            VM?.EndDrag();
        }
        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) VM?.DiscardChanges();
        }
    }
}
