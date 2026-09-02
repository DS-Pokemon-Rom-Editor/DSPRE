using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class SafariZoneGroupView : UserControl
    {
        private SafariZoneGroupViewModel VM => DataContext as SafariZoneGroupViewModel;

        public SafariZoneGroupView()
        {
            InitializeComponent();
        }

        private void AddObject_Click(object sender, RoutedEventArgs e) => VM?.AddObjectSlot();
        private void RemoveObject_Click(object sender, RoutedEventArgs e) => VM?.RemoveObjectSlot();
    }
}
