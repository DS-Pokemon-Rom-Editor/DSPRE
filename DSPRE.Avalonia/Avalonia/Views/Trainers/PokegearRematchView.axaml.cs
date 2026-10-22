using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Trainers;

namespace DSPRE.Avalonia.Views.Trainers
{
    public partial class PokegearRematchView : UserControl
    {
        private PokegearRematchViewModel VM => DataContext as PokegearRematchViewModel;

        public PokegearRematchView()
        {
            InitializeComponent();
        }

        public PokegearRematchView(PokegearRematchViewModel vm) : this()
        {
            DataContext = vm;
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
            => await DialogHelper.GuardAsync(() => { VM?.SaveAll(); return System.Threading.Tasks.Task.CompletedTask; }, "The rematches weren't saved.", "Pokégear rematch editor error");

        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) VM?.DiscardChanges();
        }

        private void OpenPhoneBook_Click(object sender, RoutedEventArgs e)
        {
            if (VM?.PhoneEntry >= 0) AvaloniaEditorLauncher.OpenPokegearPhoneBook(VM.PhoneEntry);
        }
    }
}
