using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using DSPRE.Editors;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>
    /// The Special Encounters Editor: one tab per special encounter system the game has.
    ///   • DPPt : Honey Tree, Great Marsh, Trophy Garden
    ///   • HGSS : Headbutt, Bug Contest, Bug Contest Opponents, Safari Zone
    /// </summary>
    public class SpecialEncountersEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public const string Title = "Special Encounters Editor";

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public HoneyTreeEncounterViewModel HoneyTreeVM { get; }
        public GreatMarshEncounterViewModel GreatMarshVM { get; }
        public TrophyGardenEncounterViewModel TrophyGardenVM { get; }
        public HeadbuttEncounterViewModel HeadbuttVM { get; }
        public BugContestEncounterViewModel BugContestVM { get; }
        public BugContestTrainersViewModel BugContestTrainersVM { get; }
        public SafariZoneEncounterViewModel SafariZoneVM { get; }
        public SwarmsViewModel SwarmsVM { get; }

        public bool ShowHoneyTree { get; }
        public bool ShowGreatMarsh { get; }
        public bool ShowTrophyGarden { get; }
        public bool ShowHeadbutt { get; }
        public bool ShowBugContest { get; }
        public bool ShowBugContestOpponents => ShowBugContest && BetaEditors.Enabled;
        public bool ShowSafariZone { get; }
        public bool ShowSwarms { get; }

        /// <summary>Open on the Headbutt tab, as a "Go to Headbutt file" jump does.</summary>
        public bool StartOnHeadbutt { get; }

        private string _pendingNote = "";
        public string PendingNote { get => _pendingNote; private set { _pendingNote = value; OnPropertyChanged(); } }
        public bool HasPending => !string.IsNullOrEmpty(_pendingNote);

        private IEditorWithUnsavedChanges[] Children => new IEditorWithUnsavedChanges[]
        { HoneyTreeVM, GreatMarshVM, TrophyGardenVM, HeadbuttVM, BugContestVM, BugContestTrainersVM, SafariZoneVM, SwarmsVM };

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
                return parts.Count > 0 ? string.Join(", ", parts) : Title;
            }
        }
        public void SaveChanges() => _ = SaveChangesAsync();

        public async Task<bool> SaveChangesAsync()
        {
            foreach (var c in Children)
                if (c?.HasUnsavedChanges ?? false) await c.SaveChangesAsync();
            // A tab can refuse to save (the opponents do when the game would crash), so only a full save is announced.
            if (HasUnsavedChanges) return false;
            SaveNotice.Saved(Title);
            return true;
        }
        public void DiscardChanges()
        {
            foreach (var c in Children) c?.DiscardChanges();
        }

        // Design-time constructor.
        public SpecialEncountersEditorViewModel()
        {
            HoneyTreeVM = new HoneyTreeEncounterViewModel();
            GreatMarshVM = new GreatMarshEncounterViewModel();
            BugContestVM = new BugContestEncounterViewModel();
            BugContestTrainersVM = new BugContestTrainersViewModel();
            SafariZoneVM = new SafariZoneEncounterViewModel();
            ShowHoneyTree = true;
            ShowGreatMarsh = true;
        }

        /// <param name="headbuttOnly">A linked hg-engine project: only Headbutt reads its data safely.</param>
        /// <param name="headbuttFile">Headbutt file to open first, or -1 to open on the first tab.</param>
        public SpecialEncountersEditorViewModel(bool headbuttOnly, int headbuttFile = -1)
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

                TrophyGardenVM = new TrophyGardenEncounterViewModel();
                TrophyGardenVM.PropertyChanged += OnChildChanged;
                ShowTrophyGarden = true;
            }
            else if (hgss)
            {
                // The tab sets it up when first shown; the 3D view is heavy.
                HeadbuttVM = new HeadbuttEncounterViewModel(true) { InitialIndex = headbuttFile < 0 ? 0 : headbuttFile };
                HeadbuttVM.PropertyChanged += OnChildChanged;
                ShowHeadbutt = true;
                StartOnHeadbutt = headbuttFile >= 0 || headbuttOnly;

                if (!headbuttOnly)
                {
                    BugContestVM = new BugContestEncounterViewModel(true);
                    BugContestVM.PropertyChanged += OnChildChanged;
                    BugContestTrainersVM = new BugContestTrainersViewModel();
                    BugContestTrainersVM.PropertyChanged += OnChildChanged;
                    ShowBugContest = true;

                    SafariZoneVM = new SafariZoneEncounterViewModel(true);
                    SafariZoneVM.PropertyChanged += OnChildChanged;
                    ShowSafariZone = true;
                }
                else PendingNote = "hg-engine builds the other special encounters from its own source.";
            }
            else
            {
                PendingNote = "This ROM version has no special encounters.";
            }
            if ((dppt || (hgss && !headbuttOnly)) && BetaEditors.Enabled)
            {
                SwarmsVM = new SwarmsViewModel();
                SwarmsVM.PropertyChanged += OnChildChanged;
                ShowSwarms = true;
            }
            OnPropertyChanged(nameof(HasPending));
        }

        private void OnChildChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IEditorWithUnsavedChanges.HasUnsavedChanges))
                OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        public async Task SetupAsync(Window owner)
        {
            if (ShowHoneyTree && HoneyTreeVM != null)
                await HoneyTreeVM.SetupAsync(owner);
            if (ShowGreatMarsh && GreatMarshVM != null)
                await GreatMarshVM.SetupAsync(owner);
            if (ShowBugContest && BugContestVM != null)
                await BugContestVM.SetupAsync(owner);
            if (ShowBugContest && BugContestTrainersVM != null)
                BugContestTrainersVM.Setup();
            if (ShowSafariZone && SafariZoneVM != null)
                await SafariZoneVM.SetupAsync(owner);
            if (ShowSwarms && SwarmsVM != null)
                SwarmsVM.Setup();
        }
    }
}
