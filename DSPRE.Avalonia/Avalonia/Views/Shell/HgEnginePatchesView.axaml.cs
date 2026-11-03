using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using DSPRE.Avalonia.ViewModels.Shell;

namespace DSPRE.Avalonia.Views.Shell
{
    public partial class HgEnginePatchesView : UserControl
    {
        private HgEnginePatchesViewModel VM => DataContext as HgEnginePatchesViewModel;

        public HgEnginePatchesView()
        {
            InitializeComponent();
        }

        public HgEnginePatchesView(HgEnginePatchesViewModel vm) : this()
        {
            DataContext = vm;
            // Opened at a row from another editor: show it, since the table is long.
            Loaded += (_, _) => { if (VM?.SelectedRow is PatchRow row) CentreOn(row); };
        }

        // ScrollIntoView leaves a row at the bottom edge, so scrolling to the row half a screen below centres this one.
        private void CentreOn(PatchRow row)
        {
            PatchGrid.ScrollIntoView(row, null);
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (VM == null) return;
                double rowHeight = PatchGrid.GetVisualDescendants().OfType<DataGridRow>().Select(r => r.Bounds.Height).FirstOrDefault(h => h > 0);
                if (rowHeight <= 0) return;
                int at = VM.Rows.IndexOf(row), half = (int)(PatchGrid.Bounds.Height / rowHeight / 2);
                if (at >= 0) PatchGrid.ScrollIntoView(VM.Rows[Math.Min(at + half, VM.Rows.Count - 1)], null);
            }, global::Avalonia.Threading.DispatcherPriority.Background);
        }

        private async void Add_Click(object sender, RoutedEventArgs e)
        {
            string trouble = VM?.AddPatch();
            if (trouble != null) await DialogHelper.ShowErrorAsync(trouble, "Add a patch");
        }

        private async void Change_Click(object sender, RoutedEventArgs e)
        {
            string trouble = VM?.ChangeSelected();
            if (trouble != null) await DialogHelper.ShowErrorAsync(trouble, "Change a patch");
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            string trouble = VM?.DeleteSelected();
            if (trouble != null) await DialogHelper.ShowErrorAsync(trouble, "hg-engine patches error");
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            string trouble = VM?.Save();
            if (trouble != null) await DialogHelper.ShowErrorAsync(trouble, "hg-engine patches error");
        }

        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) VM?.DiscardChanges();
        }

        private async void Reload_Click(object sender, RoutedEventArgs e)
        {
            HgEnginePatchesViewModel vm = VM;
            if (vm == null) return;
            // Reading the lists again drops patches that were added and not saved.
            if (vm.HasUnsavedChanges && !await DialogHelper.AskYesNoAsync(
                    "Reloading drops the patches you haven't saved. Reload anyway?",
                    "hg-engine patches", TopLevel.GetTopLevel(this) as Window))
                return;

            string trouble = vm.Reload();
            if (trouble != null) await DialogHelper.ShowErrorAsync(trouble, "hg-engine patches error");
        }
    }
}
