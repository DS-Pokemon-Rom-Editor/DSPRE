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

        /// <summary>The patches grouped by what they change; a patch with no group lands in Other.</summary>
        public ObservableCollection<PatchTabViewModel> Tabs { get; } = new ObservableCollection<PatchTabViewModel>();

        private static readonly string[] TabNames = { "Foundation", "Trainers", "Text", "Items and Pokémon", "Moves", "Maps and graphics", "External", "Other" };

        private static readonly Dictionary<string, string> TabOf = new Dictionary<string, string>
        {
            ["arm9"] = "Foundation", ["matrix"] = "Foundation", ["dynamicHeaders"] = "Foundation", ["scrcmdRepoint"] = "Foundation",
            ["trainerClassMetadata"] = "Trainers", ["trainerClassTablesExpanded"] = "Trainers", ["trainerEncounterBgmRepointed"] = "Trainers", ["trainerShiny"] = "Trainers",
            ["sentenceCase"] = "Text", ["itemSentenceCase"] = "Text", ["trainerNames"] = "Text",
            ["sameHeldItemOdds"] = "Items and Pokémon", ["itemStandardize"] = "Items and Pokémon",
            ["punchingMovesExpanded"] = "Moves", ["soundMovesExpanded"] = "Moves",
            ["bdhcam"] = "Maps and graphics", ["buildingRotation"] = "Maps and graphics", ["disableTextures"] = "Maps and graphics",
            ["platPatches"] = "External",
        };

        // Words a search should hit beyond the title and description.
        private static readonly Dictionary<string, string> KeywordsOf = new Dictionary<string, string>
        {
            ["arm9"] = "synthetic overlay memory expansion expand required", ["matrix"] = "matrix 0 map size expand", ["dynamicHeaders"] = "header narc allocate headers",
            ["scrcmdRepoint"] = "script command table custom commands", ["trainerClassMetadata"] = "gender prize eye contact music vs intro class",
            ["trainerClassTablesExpanded"] = "class tables expand", ["trainerEncounterBgmRepointed"] = "encounter music bgm table", ["trainerShiny"] = "shiny trainer party",
            ["sentenceCase"] = "names capital case pokemon", ["itemSentenceCase"] = "names capital case items", ["trainerNames"] = "trainer name length text",
            ["sameHeldItemOdds"] = "held item odds wild", ["itemStandardize"] = "item numbers scripts ground items order",
            ["punchingMovesExpanded"] = "punch punching iron fist move list table expand repoint", ["soundMovesExpanded"] = "sound soundproof move list table expand repoint ai",
            ["bdhcam"] = "camera cameras dynamic", ["buildingRotation"] = "building rotation map editor", ["disableTextures"] = "textures dynamic disable",
            ["platPatches"] = "external platinum patches link",
        };

        private string _search = "";
        /// <summary>Narrows every tab to the patches whose title, description, author or keywords contain the text.</summary>
        public string Search
        {
            get => _search;
            set { _search = value ?? ""; OnPropertyChanged(); foreach (PatchTabViewModel tab in Tabs) tab.Filter(_search); }
        }

        private int _selectedTab;
        public int SelectedTab { get => _selectedTab; set { _selectedTab = value; OnPropertyChanged(); } }

        private int _columns = 1;
        /// <summary>Cards per row, decided by the window from its width.</summary>
        public void SetColumns(int columns)
        {
            if (columns == _columns) return;
            _columns = columns;
            foreach (PatchTabViewModel tab in Tabs) tab.Columns = columns;
        }

        /// <summary>Switches to the tab a patch's requirement lives on.</summary>
        public void GoToTab(string name)
        {
            int at = Tabs.ToList().FindIndex(t => t.Name == name);
            if (at >= 0) SelectedTab = at;
        }

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
            foreach (PatchToolboxLogic.PatchInfo p in _statuses)
                Patches.Add(new PatchRowViewModel(p, KeywordsOf.TryGetValue(p.Key, out string words) ? words : ""));
            BuildTabs();
        }

        private void BuildTabs()
        {
            int keep = _selectedTab;
            Tabs.Clear();
            foreach (string name in TabNames)
            {
                List<PatchRowViewModel> rows = Patches.Where(r => (TabOf.TryGetValue(r.Key, out string t) ? t : "Other") == name).ToList();
                if (rows.Count == 0) continue;
                foreach (PatchRowViewModel row in rows) row.PlacedOn(name);
                PatchTabViewModel tab = new PatchTabViewModel(name, rows) { Columns = _columns };
                tab.Filter(_search);
                Tabs.Add(tab);
            }
            SelectedTab = keep >= 0 && keep < Tabs.Count ? keep : 0;
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

    /// <summary>One tab of the toolbox: the patches of one area, narrowed by the search box.</summary>
    public class PatchTabViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public string Name { get; }
        private readonly List<PatchRowViewModel> _all;
        public ObservableCollection<PatchRowViewModel> Rows { get; } = new ObservableCollection<PatchRowViewModel>();
        public string Header => Rows.Count == _all.Count ? Name : $"{Name} ({Rows.Count})";

        public PatchTabViewModel(string name, List<PatchRowViewModel> rows) { Name = name; _all = rows; }

        private int _columns = 1;
        public int Columns
        {
            get => _columns;
            set { if (_columns == value) return; _columns = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Columns))); }
        }

        public void Filter(string search)
        {
            Rows.Clear();
            foreach (PatchRowViewModel row in _all)
                if (string.IsNullOrWhiteSpace(search) || row.Matches(search)) Rows.Add(row);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Header)));
        }
    }

    /// <summary>One row in the toolbox: a patch's title, description and current state.</summary>
    public class PatchRowViewModel
    {
        private readonly string _keywords;
        public bool Matches(string search) =>
            SearchMatch.Contains(Title, search) || SearchMatch.Contains(Description, search)
            || SearchMatch.Contains(AuthorText, search) || SearchMatch.Contains(_keywords, search) || SearchMatch.Contains(Key, search);

        /// <summary>The tab holding the patch this one needs first, shown as a link in place of the status.</summary>
        public string RequiresTab { get; private set; }
        public bool HasRequires => RequiresTab != null;
        public bool ShowStatusText => RequiresTab == null;
        public string RequiresText => StatusText + ". See " + RequiresTab + ".";
        /// <summary>A link to the tab the row already sits on says nothing, so it becomes the plain status.</summary>
        internal void PlacedOn(string tab) { if (RequiresTab == tab) RequiresTab = null; }
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
        /// <summary>Further reading on what the patch changes, shown as a Guide button.</summary>
        public string Guide { get; }
        public bool HasGuide => Guide != null;
        public bool ShowApply => Link == null;
        public List<PatchPartViewModel> Parts { get; } = new List<PatchPartViewModel>();
        public bool HasParts => Parts.Count > 0;
        /// <summary>The patch's notes, for the Notes button.</summary>
        public string Notes { get; }

        public PatchRowViewModel(DSPRE.PatchToolboxLogic.PatchInfo p, string keywords = "")
        {
            _keywords = keywords;
            Notes = DSPRE.PatchNotes.For(p);
            Key = p.Key;
            Title = p.Title;
            Description = p.Description;
            AuthorText = string.IsNullOrEmpty(p.Author) ? null : "by " + p.Author;
            Link = p.Link;
            Guide = p.Guide;
            if (p.Parts != null)
                foreach (PatchToolboxLogic.PatchPart part in p.Parts)
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
            if (p.State == DSPRE.PatchToolboxLogic.PatchState.Unsupported && (p.Reason ?? "").StartsWith("Requires ARM9", System.StringComparison.Ordinal))
                RequiresTab = "Foundation";
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
