using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Graphics;

namespace DSPRE.Avalonia.Views.Graphics
{
    public partial class BattlePressView : UserControl
    {
        private BattlePressViewModel VM => DataContext as BattlePressViewModel;

        public BattlePressView() => InitializeComponent();

        public BattlePressView(BattlePressViewModel vm) : this() => DataContext = vm;

        private void Frame_Pressed(object sender, PointerPressedEventArgs e)
        {
            if (VM == null || sender is not Control { DataContext: PressFrameRow frame } image) return;
            Point p = e.GetPosition(image);
            VM.SquareClick(frame, p.X, p.Y, e.GetCurrentPoint(image).Properties.IsRightButtonPressed);
        }

        private void MatchRest_Click(object sender, RoutedEventArgs e) => ((sender as Control)?.DataContext as PressFrameRow)?.MatchRest();

        private void GameLook_Click(object sender, RoutedEventArgs e) => ((sender as Control)?.DataContext as PressFrameRow)?.GameLook();


        private void Save_Click(object sender, RoutedEventArgs e) => VM?.SaveChanges();

        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) VM?.DiscardChanges();
        }
    }
}
