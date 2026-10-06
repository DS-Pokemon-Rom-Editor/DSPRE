using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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

        private bool _canGenerateCredits;
        public bool CanGenerateCredits
        {
            get => _canGenerateCredits;
            private set { _canGenerateCredits = value; OnPropertyChanged(); }
        }

        // Design-time
        public PatchToolboxViewModel() { Refresh(); }

        /// <summary>Re-query every patch's status (a single patch can enable another, e.g. ARM9 → BDHCam).</summary>
        public void Refresh()
        {
            Patches.Clear();
            CanGenerateCredits = AvaloniaEditorLauncher.IsRomLoaded;

            if (!AvaloniaEditorLauncher.IsRomLoaded)
            {
                HeaderNote = "No ROM is loaded.";
                return;
            }

            HeaderNote = "Back up your project first. Some patches cannot be undone.";
            _statuses = DSPRE.PatchToolboxLogic.GetPatchStatuses();
            foreach (var p in _statuses)
                Patches.Add(new PatchRowViewModel(p));
        }

        private List<DSPRE.PatchToolboxLogic.PatchInfo> _statuses = new();

        /// <summary>Credits for the patches this ROM has, ready to paste.</summary>
        public string CreditsText()
        {
            DSPRE.PatchToolboxLogic.MarkCreditsHandled();
            return DSPRE.PatchToolboxLogic.CreditsText(DSPRE.PatchToolboxLogic.AppliedCreditKeys(_statuses));
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
        public string AuthorText { get; }
        public bool HasAuthor => AuthorText != null;
        public string StatusText { get; }
        public bool CanApply { get; }
        public string ButtonText { get; }
        public IBrush StatusBrush { get; }
        public string Link { get; }
        public bool HasLink => Link != null;
        public bool ShowApply => Link == null;
        public List<PatchPartViewModel> Parts { get; } = new List<PatchPartViewModel>();
        public bool HasParts => Parts.Count > 0;
        /// <summary>The patch's notes, for the Notes button.</summary>
        public string Notes { get; }

        public PatchRowViewModel(DSPRE.PatchToolboxLogic.PatchInfo p)
        {
            Notes = DSPRE.PatchNotes.For(p);
            Key = p.Key;
            Title = p.Title;
            Description = p.Description;
            AuthorText = string.IsNullOrEmpty(p.Author) ? null : "by " + p.Author;
            Link = p.Link;
            if (p.Parts != null)
                foreach (var part in p.Parts)
                    Parts.Add(new PatchPartViewModel(part));

            switch (p.State)
            {
                case DSPRE.PatchToolboxLogic.PatchState.Applied when HasParts:
                    StatusText = $"{Parts.Count(part => part.Applied)} of {Parts.Count} found";
                    StatusBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
                    break;
                case DSPRE.PatchToolboxLogic.PatchState.Unsupported when HasParts:
                    StatusText = "None found";
                    StatusBrush = new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E));
                    break;
                case DSPRE.PatchToolboxLogic.PatchState.Applied:
                    StatusText = "Applied";
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

    /// <summary>One patch inside a group another tool applies, found or not.</summary>
    public class PatchPartViewModel
    {
        public string Key { get; }
        public string Title { get; }
        public bool Applied { get; }
        public string StatusText { get; }
        public IBrush StatusBrush { get; }

        public PatchPartViewModel(DSPRE.PatchToolboxLogic.PatchPart part)
        {
            Key = part.Key;
            Title = part.Title;
            Applied = part.Applied;
            StatusText = !part.Applied ? "Not found" : string.IsNullOrEmpty(part.Note) ? "Found" : "Found, " + part.Note;
            StatusBrush = new SolidColorBrush(part.Applied ? Color.FromRgb(0x2E, 0x7D, 0x32) : Color.FromRgb(0x9E, 0x9E, 0x9E));
        }
    }
}
