using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using global::Avalonia.Media;
using DSPRE.Avalonia;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Tools
{
    /// <summary>
    /// ViewModel for the native Avalonia ROM Patch Toolbox. It renders the patch catalogue and
    /// applied/supported state via the shared, UI-agnostic <see cref="DSPRE.PatchToolboxLogic"/>
    /// logic, so applying a patch here runs byte-for-byte the same ROM code as the WinForms dialog.
    /// </summary>
    public class PatchToolboxViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public ObservableCollection<PatchRowViewModel> Patches { get; } = new ObservableCollection<PatchRowViewModel>();

        private string _headerNote;
        public string HeaderNote
        {
            get => _headerNote;
            private set { _headerNote = value; OnPropertyChanged(); }
        }

        // Design-time
        public PatchToolboxViewModel() { Refresh(); }

        /// <summary>Re-query every patch's status (a single patch can enable another, e.g. ARM9 → BDHCam).</summary>
        public void Refresh()
        {
            Patches.Clear();

            if (!AvaloniaEditorLauncher.IsRomLoaded)
            {
                HeaderNote = "No ROM is loaded.";
                return;
            }

            HeaderNote = "These patches modify the ROM binary (ARM9 / overlays / NARCs). Back up your project first; some are irreversible.";
            foreach (var p in DSPRE.PatchToolboxLogic.GetPatchStatuses())
                Patches.Add(new PatchRowViewModel(p));
        }

        /// <summary>Apply the patch for <paramref name="row"/>, then refresh all statuses.</summary>
        public void Apply(PatchRowViewModel row)
        {
            if (row == null || !row.CanApply) return;
            DSPRE.PatchToolboxLogic.ApplyByKey(row.Key);   // shows its own confirm/result prompts
            AppEvents.RaiseRomPatchStateChanged();         // any open editor gating on a patch flag re-checks
            Refresh();
        }
    }

    /// <summary>One row in the toolbox: a patch's title, description and current state.</summary>
    public class PatchRowViewModel
    {
        public string Key { get; }
        public string Title { get; }
        public string Description { get; }
        public string StatusText { get; }
        public bool CanApply { get; }
        public string ButtonText { get; }
        public IBrush StatusBrush { get; }

        public PatchRowViewModel(DSPRE.PatchToolboxLogic.PatchInfo p)
        {
            Key = p.Key;
            Title = p.Title;
            Description = p.Description;

            switch (p.State)
            {
                case DSPRE.PatchToolboxLogic.PatchState.Applied:
                    StatusText = "✔ Applied";
                    CanApply = false;
                    ButtonText = "Applied";
                    StatusBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
                    break;
                case DSPRE.PatchToolboxLogic.PatchState.Unsupported:
                    StatusText = p.Reason ?? "Unsupported";
                    CanApply = false;
                    ButtonText = "N/A";
                    StatusBrush = new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E));
                    break;
                default:
                    StatusText = "Available";
                    CanApply = true;
                    ButtonText = string.IsNullOrEmpty(p.ActionLabel) ? "Apply" : p.ActionLabel;
                    StatusBrush = new SolidColorBrush(Color.FromRgb(0x15, 0x65, 0xC0));
                    break;
            }
        }
    }
}
