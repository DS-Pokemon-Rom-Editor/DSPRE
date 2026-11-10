using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using DSPRE.Avalonia.ViewModels.Shell;

namespace DSPRE.Avalonia.Views.Shell
{
    public partial class HgEngineHooksView : UserControl
    {
        private HgEngineHooksViewModel VM => DataContext as HgEngineHooksViewModel;

        public HgEngineHooksView()
        {
            InitializeComponent();
        }

        public HgEngineHooksView(HgEngineHooksViewModel vm) : this()
        {
            DataContext = vm;
            // Opened at a row from another editor: show it, since the table is long.
            Loaded += (_, _) => { if (VM?.SelectedRow is HookRow row) CentreOn(row); };
        }

        // ScrollIntoView leaves a row at the bottom edge, so scrolling to the row half a screen below centres this one.
        private void CentreOn(HookRow row)
        {
            EntryGrid.ScrollIntoView(row, null);
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (VM == null) return;
                double rowHeight = EntryGrid.GetVisualDescendants().OfType<DataGridRow>().Select(r => r.Bounds.Height).FirstOrDefault(h => h > 0);
                if (rowHeight <= 0) return;
                int at = VM.Rows.IndexOf(row), half = (int)(EntryGrid.Bounds.Height / rowHeight / 2);
                if (at >= 0) EntryGrid.ScrollIntoView(VM.Rows[Math.Min(at + half, VM.Rows.Count - 1)], null);
            }, global::Avalonia.Threading.DispatcherPriority.Background);
        }

        private async void Add_Click(object sender, RoutedEventArgs e)
        {
            string trouble = VM?.AddEntry();
            if (trouble != null) await DialogHelper.ShowErrorAsync(trouble, "Add an entry");
        }

        private async void Change_Click(object sender, RoutedEventArgs e)
        {
            string trouble = VM?.ChangeSelected();
            if (trouble != null) await DialogHelper.ShowErrorAsync(trouble, "Change an entry");
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            string trouble = VM?.DeleteSelected();
            if (trouble != null) await DialogHelper.ShowErrorAsync(trouble, "hg-engine hooks and byte replacements");
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            string trouble = VM?.Save();
            if (trouble != null) await DialogHelper.ShowErrorAsync(trouble, "hg-engine hooks and byte replacements");
        }

        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) VM?.DiscardChanges();
        }

        private async void Reload_Click(object sender, RoutedEventArgs e)
        {
            HgEngineHooksViewModel vm = VM;
            if (vm == null) return;
            // Reading the lists again drops entries that were added and not saved.
            if (vm.HasUnsavedChanges && !await DialogHelper.AskYesNoAsync(
                    "Reloading drops unsaved entries. Reload anyway?",
                    "hg-engine hooks and byte replacements", TopLevel.GetTopLevel(this) as Window))
                return;

            string trouble = vm.Reload();
            if (trouble != null) await DialogHelper.ShowErrorAsync(trouble, "hg-engine hooks and byte replacements");
        }
    }
}
