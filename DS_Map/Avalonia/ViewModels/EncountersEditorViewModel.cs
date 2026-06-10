using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using DSPRE.Editors;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>
    /// Parent ViewModel for the Avalonia Encounters editor — a composite that hosts
    /// the special-encounter sub-editors as tabs, gated by game family:
    ///   • DPPt : Honey Tree, Great Marsh
    ///   • HGSS : Headbutt, Safari Zone, Bug Contest
    ///
    /// Mirrors the WinForms <c>EncountersEditor</c> container. Sub-editors are ported
    /// incrementally; <see cref="PendingNote"/> lists those not yet migrated.
    /// </summary>
    public class EncountersEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        // ── Ported sub-editors ─────────────────────────────────────────────────────
        public HoneyTreeEncounterViewModel HoneyTreeVM { get; }
        public GreatMarshEncounterViewModel GreatMarshVM { get; }
        public BugContestEncounterViewModel BugContestVM { get; }
        public SafariZoneEncounterViewModel SafariZoneVM { get; }

        // ── Tab visibility (by family) ──────────────────────────────────────────────
        public bool ShowHoneyTree { get; }
        public bool ShowGreatMarsh { get; }
        public bool ShowBugContest { get; }
        public bool ShowSafariZone { get; }

        private string _pendingNote = "";
        public string PendingNote { get => _pendingNote; private set { _pendingNote = value; OnPropertyChanged(); } }
        public bool HasPending => !string.IsNullOrEmpty(_pendingNote);

        // ── Dirty aggregation ───────────────────────────────────────────────────────
        private IEditorWithUnsavedChanges[] Children => new IEditorWithUnsavedChanges[]
        { HoneyTreeVM, GreatMarshVM, BugContestVM, SafariZoneVM };

        public bool HasUnsavedChanges
        {
            get { foreach (var c in Children) if (c?.HasUnsavedChanges ?? false) return true; return false; }
        }
        public string UnsavedChangesDescription
        {
            get
            {
                var parts = new List<string>();
                foreach (var c in Children)
                    if (c?.HasUnsavedChanges ?? false) parts.Add(c.UnsavedChangesDescription);
                return parts.Count > 0 ? string.Join(", ", parts) : "Encounters Editor";
            }
        }
        public void SaveChanges()
        {
            foreach (var c in Children) if (c?.HasUnsavedChanges ?? false) c.SaveChanges();
        }
        public void DiscardChanges()
        {
            foreach (var c in Children) c?.DiscardChanges();
        }

        // ── Design-time constructor ─────────────────────────────────────────────────
        public EncountersEditorViewModel()
        {
            HoneyTreeVM = new HoneyTreeEncounterViewModel();
            GreatMarshVM = new GreatMarshEncounterViewModel();
            BugContestVM = new BugContestEncounterViewModel();
            SafariZoneVM = new SafariZoneEncounterViewModel();
            ShowHoneyTree = true;
            ShowGreatMarsh = true;
        }

        // ── Runtime constructor ─────────────────────────────────────────────────────
        public EncountersEditorViewModel(bool _)
        {
            bool dppt = gameFamily == GameFamilies.DP || gameFamily == GameFamilies.Plat;
            bool hgss = gameFamily == GameFamilies.HGSS;

            if (dppt)
            {
                HoneyTreeVM = new HoneyTreeEncounterViewModel(true);
                HoneyTreeVM.PropertyChanged += OnChildChanged;
                ShowHoneyTree = true;

                GreatMarshVM = new GreatMarshEncounterViewModel(true);
                GreatMarshVM.PropertyChanged += OnChildChanged;
                ShowGreatMarsh = true;
            }
            else if (hgss)
            {
                BugContestVM = new BugContestEncounterViewModel(true);
                BugContestVM.PropertyChanged += OnChildChanged;
                ShowBugContest = true;

                SafariZoneVM = new SafariZoneEncounterViewModel(true);
                SafariZoneVM.PropertyChanged += OnChildChanged;
                ShowSafariZone = true;

                PendingNote = "Headbutt encounter editor: not yet ported (needs the 3D map view — tracked with the Map/Matrix/NSBTX editors).";
            }
            else
            {
                PendingNote = "This ROM version has no special encounter editors.";
            }
            OnPropertyChanged(nameof(HasPending));
        }

        private void OnChildChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IEditorWithUnsavedChanges.HasUnsavedChanges))
                OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        // ── Setup ────────────────────────────────────────────────────────────────────
        public async Task SetupAsync(Window owner)
        {
            if (ShowHoneyTree && HoneyTreeVM != null)
                await HoneyTreeVM.SetupAsync(owner);
            if (ShowGreatMarsh && GreatMarshVM != null)
                await GreatMarshVM.SetupAsync(owner);
            if (ShowBugContest && BugContestVM != null)
                await BugContestVM.SetupAsync(owner);
            if (ShowSafariZone && SafariZoneVM != null)
                await SafariZoneVM.SetupAsync(owner);
        }
    }
}
