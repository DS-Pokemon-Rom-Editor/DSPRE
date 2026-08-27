using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    /// <summary>The header detail form (fields). Shared by the Header editor and the Maps workspace.</summary>
    public partial class HeaderFieldsView : UserControl
    {
        private HeaderEditorViewModel VM => DataContext as HeaderEditorViewModel;

        public HeaderFieldsView() => InitializeComponent();

        private void OpenMatrix_Click(object sender, RoutedEventArgs e) => VM?.OpenMatrix();
        private void OpenAreaData_Click(object sender, RoutedEventArgs e) => VM?.OpenAreaData();
        private void OpenScripts_Click(object sender, RoutedEventArgs e) => VM?.OpenScripts();
        private void OpenLevelScripts_Click(object sender, RoutedEventArgs e) => VM?.OpenLevelScripts();
        private void OpenEvents_Click(object sender, RoutedEventArgs e) => VM?.OpenEvents();
        private void OpenTexts_Click(object sender, RoutedEventArgs e) => VM?.OpenTexts();
        private void OpenEncounters_Click(object sender, RoutedEventArgs e) => VM?.OpenEncounters();
    }
}
