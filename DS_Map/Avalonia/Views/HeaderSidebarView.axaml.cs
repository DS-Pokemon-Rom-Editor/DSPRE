using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    /// <summary>The shared header-tree sidebar (used by the Header editor and the Maps workspace).</summary>
    public partial class HeaderSidebarView : UserControl
    {
        private HeaderEditorViewModel VM => DataContext as HeaderEditorViewModel;

        public HeaderSidebarView() => InitializeComponent();

        private void ExpandAll_Click(object sender, RoutedEventArgs e) => VM?.ExpandAllFolders();
        private void CollapseAll_Click(object sender, RoutedEventArgs e) => VM?.CollapseAllFolders();
    }
}
