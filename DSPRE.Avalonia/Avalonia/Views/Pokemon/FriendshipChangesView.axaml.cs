using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Pokemon;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class FriendshipChangesView : UserControl
    {
        private FriendshipChangesViewModel VM => DataContext as FriendshipChangesViewModel;

        public FriendshipChangesView() { InitializeComponent(); }

        public FriendshipChangesView(FriendshipChangesViewModel vm) : this() { DataContext = vm; }

        private async void Save_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.SaveChangesAsync(); }
        private void Discard_Click(object sender, RoutedEventArgs e) => VM?.DiscardChanges();
    }
}
