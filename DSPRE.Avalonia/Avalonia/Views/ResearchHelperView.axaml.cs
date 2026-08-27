using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Views
{
    public partial class ResearchHelperView : Window
    {
        private ResearchHelperViewModel VM => (ResearchHelperViewModel)DataContext;

        public ResearchHelperView(ResearchHelperViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
            Opened += async (_, _) => await vm.LoadAllDataAsync();
        }

        // ── Variable Watcher ──────────────────────────────────────────────────
        private void VarSearch_Click(object sender, RoutedEventArgs e) => VM?.SearchVariableUsage();
        private void VarClear_Click(object sender, RoutedEventArgs e)  => VM?.ClearVariableResults();

        private void VariableGrid_DoubleTapped(object sender, TappedEventArgs e)
        {
            if (sender is DataGrid dg && dg.SelectedItem is VariableUsageResult r)
            {
                var clip = TopLevel.GetTopLevel(this)?.Clipboard;
                clip?.SetTextAsync($"{r.FileType} {r.FileID}");
                if (VM != null) VM.StatusText = $"{r.FileType} {r.FileID} copied to clipboard";
            }
        }

        // ── Flag Watcher ──────────────────────────────────────────────────────
        private void FlagSearch_Click(object sender, RoutedEventArgs e) => VM?.SearchFlagUsage();
        private void FlagClear_Click(object sender, RoutedEventArgs e)  => VM?.ClearFlagResults();

        private void FlagGrid_DoubleTapped(object sender, TappedEventArgs e)
        {
            if (sender is DataGrid dg && dg.SelectedItem is FlagUsageResult r)
            {
                var clip = TopLevel.GetTopLevel(this)?.Clipboard;
                clip?.SetTextAsync($"{r.FileType} {r.FileID}");
                if (VM != null) VM.StatusText = $"{r.FileType} {r.FileID} copied to clipboard";
            }
        }

        // ── Overworld Watcher ────────────────────────────────────────────────────
        private void OwSearch_Click(object sender, RoutedEventArgs e) => VM?.SearchOverworldEntryUsage();
        private void OwClear_Click(object sender, RoutedEventArgs e)  => VM?.ClearOwResults();

        private void OwGrid_DoubleTapped(object sender, TappedEventArgs e)
        {
            if (sender is DataGrid dg && dg.SelectedItem is OwEntryUsageResult r)
                VM?.NavigateToOwResult(r);
        }

        // ── Trainer Watcher ──────────────────────────────────────────────────────
        private void TrainerWatchSearch_Click(object sender, RoutedEventArgs e) => VM?.SearchTrainerUsage();
        private void TrainerWatchClear_Click(object sender, RoutedEventArgs e)  => VM?.ClearTrainerResults();

        private void TrainerWatchGrid_DoubleTapped(object sender, TappedEventArgs e)
        {
            if (sender is DataGrid dg && dg.SelectedItem is TrainerUsageResult r)
                VM?.NavigateToTrainerResult(r);
        }

        // ── File Watcher ──────────────────────────────────────────────────────
        private void FileWatcherSearch_Click(object sender, RoutedEventArgs e)
            => VM?.SearchScriptFileReferences();

        private void FileWatcherGrid_DoubleTapped(object sender, TappedEventArgs e)
        {
            if (sender is DataGrid dg && dg.SelectedItem is ScriptFileReferenceResult r)
            {
                var clip = TopLevel.GetTopLevel(this)?.Clipboard;
                clip?.SetTextAsync($"{r.ReferenceType} {r.ReferenceID}");
                if (VM != null) VM.StatusText = $"{r.ReferenceType} {r.ReferenceID} copied to clipboard";
            }
        }

        // ── ID Watcher ────────────────────────────────────────────────────────
        private void IdWatcherSearch_Click(object sender, RoutedEventArgs e)
            => VM?.SearchScriptIdUsage();

        private void IdWatcherGrid_DoubleTapped(object sender, TappedEventArgs e)
        {
            if (sender is DataGrid dg && dg.SelectedItem is ScriptIdUsageResult r)
            {
                var clip = TopLevel.GetTopLevel(this)?.Clipboard;
                clip?.SetTextAsync($"Event {r.EventFileID} {r.EventType}[{r.EventIndex}]");
                if (VM != null) VM.StatusText = $"Event {r.EventFileID} {r.EventType}[{r.EventIndex}] copied to clipboard";
            }
        }

        // ── Header Watcher ────────────────────────────────────────────────────
        private void HeaderSearch_Click(object sender, RoutedEventArgs e)
            => VM?.SearchHeaderInfo();

        private void HeaderPropsGrid_DoubleTapped(object sender, TappedEventArgs e)
        {
            if (sender is DataGrid dg && dg.SelectedItem is HeaderProperty p)
            {
                var clip = TopLevel.GetTopLevel(this)?.Clipboard;
                clip?.SetTextAsync(p.Value ?? "");
                if (VM != null) VM.StatusText = $"{p.Name} value copied to clipboard";
            }
        }

        private void IncomingWarpsGrid_DoubleTapped(object sender, TappedEventArgs e)
        {
            if (sender is DataGrid dg && dg.SelectedItem is HeaderWarpResult r && VM != null)
            {
                // Find the header that owns this event file, then navigate to it
                int headerCount = RomInfo.GetHeaderCount();
                for (int i = 0; i < headerCount; i++)
                {
                    try
                    {
                        var h = MapHeader.GetMapHeader((ushort)i);
                        if (h != null && h.eventFileID == r.EventFileID)
                        {
                            VM.NavigateToHeader(i);
                            return;
                        }
                    }
                    catch { }
                }
                VM.StatusText = $"Could not find header for event file {r.EventFileID}";
            }
        }

        private void OutgoingWarpsGrid_DoubleTapped(object sender, TappedEventArgs e)
        {
            if (sender is DataGrid dg && dg.SelectedItem is HeaderOutgoingWarpResult r && VM != null)
                VM.NavigateToHeader(r.DestHeader);
        }
    }
}
