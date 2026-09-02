using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.World
{
    /// <summary>The shared header-tree sidebar (used by the Header editor and the Maps workspace).</summary>
    public partial class HeaderSidebarView : UserControl
    {
        private HeaderEditorViewModel VM => DataContext as HeaderEditorViewModel;

        public HeaderSidebarView() => InitializeComponent();

        private void ExpandAll_Click(object sender, RoutedEventArgs e) => VM?.ExpandAllFolders();
        private void CollapseAll_Click(object sender, RoutedEventArgs e) => VM?.CollapseAllFolders();

        // ── Leaf context menu: select the right-clicked header, then open the editor ──
        private void CtxOpen(object sender, System.Action<HeaderEditorViewModel> open)
        {
            var vm = VM;
            var leaf = (sender as MenuItem)?.DataContext as Models.HeaderTreeLeaf;
            if (vm == null || leaf == null) return;
            vm.SelectedTreeNode = leaf;   // loads the header; the workspace follows
            open(vm);
        }

        private void CtxOpenMap_Click(object sender, RoutedEventArgs e) => CtxOpen(sender, _ => AvaloniaEditorLauncher.OpenMapEditor());
        private void CtxOpenEvents_Click(object sender, RoutedEventArgs e) => CtxOpen(sender, vm => vm.OpenEvents());
        private void CtxOpenScripts_Click(object sender, RoutedEventArgs e) => CtxOpen(sender, vm => vm.OpenScripts());
        private void CtxOpenTexts_Click(object sender, RoutedEventArgs e) => CtxOpen(sender, vm => vm.OpenTexts());
        private void CtxOpenEncounters_Click(object sender, RoutedEventArgs e) => CtxOpen(sender, vm => vm.OpenEncounters());
        private void CtxOpenMatrix_Click(object sender, RoutedEventArgs e) => CtxOpen(sender, vm => vm.OpenMatrix());
    }
}
