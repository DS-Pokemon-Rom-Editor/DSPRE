using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Trainers
{
    public partial class VsSeekerRematchView : UserControl
    {
        private VsSeekerRematchViewModel VM => DataContext as VsSeekerRematchViewModel;

        public VsSeekerRematchView()
        {
            InitializeComponent();
        }

        public VsSeekerRematchView(VsSeekerRematchViewModel vm) : this()
        {
            DataContext = vm;
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
            => await DialogHelper.GuardAsync(() => { VM?.SaveAll(); return System.Threading.Tasks.Task.CompletedTask; }, "The rematches weren't saved.", "Vs. Seeker rematch editor", TopLevel.GetTopLevel(this) as Window);

        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) VM?.DiscardChanges();
        }
    }
}
