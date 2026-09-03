using Avalonia.Controls;
using DSPRE;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DSPRE.Avalonia.ViewModels.Shell
{
    /// <summary>
    /// The Welcome &amp; Tutorial window: quick-open actions + recent projects on the left, a short
    /// paged getting-started guide on the right. Shown at startup (toggleable, and relaunchable
    /// from Tools → Welcome &amp; Tutorial or from Settings).
    /// </summary>
    public class WelcomeViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public sealed class TutorialPage
        {
            public string Title { get; init; }
            public string Body { get; init; }
        }

        // The pages everybody sees. BuildPages() adds the beta one on top when it applies.
        private static readonly TutorialPage[] StandardPages =
        {
            new()
            {
                Title = "Welcome to DSPRE!",
                Body =
                    "DSPRE (DS Pokémon ROM Editor) lets you edit the Gen IV games: Diamond, Pearl, " +
                    "Platinum, HeartGold and SoulSilver. You can change maps, events, scripts, trainers, " +
                    "wild encounters, Pokémon data, text and much more.\n\n" +
                    "To begin, open a ROM (.nds file) or a project folder you extracted earlier. Use the " +
                    "buttons on the left, the File menu, or pick something from the Recent list."
            },
            new()
            {
                Title = "Your project on disk",
                Body =
                    "When you open a .nds file, DSPRE extracts it into a folder next to it named " +
                    "\"<rom>_DSPRE_contents\". All of your edits live in that folder, and the original " +
                    ".nds is never touched.\n\n" +
                    "When you want to play your hack, use File > Save ROM to build a fresh .nds from " +
                    "the project.\n\n" +
                    "Tip: keep a backup of your clean ROM. If you know git, putting the project folder " +
                    "under version control makes every change reversible."
            },
            new()
            {
                Title = "Finding your way around",
                Body =
                    "The main window is the Maps workspace. Every map \"header\" of the game is listed " +
                    "on the left, and typing in the search box filters the list (the Fuzzy toggle helps " +
                    "when you only remember part of a name). Selecting a header shows its settings, and " +
                    "the tabs above jump to its map, events, scripts, encounters and text.\n\n" +
                    "Everything else lives in the menus. You can also press Ctrl+P and type the name of " +
                    "any editor (\"trainer\", \"wild\", \"matrix\" and so on) to jump straight to it."
            },
            new()
            {
                Title = "Editing tips",
                Body =
                    "• Dropdowns support type-to-jump: focus one and start typing an entry's name.\n" +
                    "• Editors track unsaved changes and warn you before anything is lost.\n" +
                    "• The major editors support undo and redo (Ctrl+Z / Ctrl+Y).\n" +
                    "• In 3D views, drag with the left button to orbit, the right button to pan, and " +
                    "scroll to zoom. Speeds and axis inversion can be adjusted in Settings.\n" +
                    "• Many dropdown labels (evolution methods, for example) can be renamed via " +
                    "Tools > Edit Dropdown Labels."
            },
            new()
            {
                Title = "Keyboard shortcuts",
                Body =
                    "• Ctrl+P: open the command palette and jump to any editor by name.\n" +
                    "• Ctrl+Z / Ctrl+Y: undo and redo in the major editors.\n" +
                    "• Ctrl+S: save in most editors.\n" +
                    "• In dropdowns, type an entry's name to jump to it.\n" +
                    "• In 3D views: left-drag pans, right-drag orbits, mouse wheel zooms.\n" +
                    "• In the guided tour: Right or Enter for next, Left for back, Esc to skip."
            },
            new()
            {
                Title = "Power tools",
                Body =
                    "• The ROM Patch Toolbox applies binary patches such as ARM9 expansion and dynamic " +
                    "headers. These modify the ROM itself, so back up your project first!\n" +
                    "• Validation & Where-Used scans headers for broken references and finds every place " +
                    "a matrix, script or event file is used.\n" +
                    "• Advanced Header Search queries headers by any field.\n" +
                    "• The NARC and NSBMD utilities unpack and rebuild game archives and model textures.\n\n" +
                    "You can reopen this guide anytime from Tools > Welcome & Tutorial. A short guided " +
                    "tour will also point out where everything is when you open your first ROM."
            },
        };

        /// <summary>
        /// Only shown when the editors that are still being tried out are switched on. It goes second,
        /// straight after the greeting, because it changes what to expect from everything after it.
        /// </summary>
        private static TutorialPage BetaPage()
        {
            var areas = new List<string>();
            foreach (var a in BetaEditors.CountByArea()) areas.Add($"{a.Value} in {a.Key}");

            var features = new List<string>();
            foreach (var f in BetaEditors.Features) features.Add($"• {f.Name} ({f.Where}).");

            return new TutorialPage
            {
                Title = "You are running the unfinished editors",
                Body =
                    $"DSPRE was started with beta features on, so {BetaEditors.Count} editors that "
                    + "are normally hidden are available to you: " + string.Join(", ", areas)
                    + ".\n\n"
                    + "These parts of finished editors are switched on too:\n"
                    + string.Join("\n", features) + "\n\n"
                    + "They are unfinished. They can write a project you cannot open again, so back "
                    + "up your work before using one, and keep the clean .nds somewhere safe.\n\n"
                    + "If you report a problem, please say whether a beta editor or feature was "
                    + "involved. It is the difference between a bug in DSPRE and a bug in something "
                    + "we already know is half built, and it saves everybody a lot of time."
            };
        }

        private static TutorialPage[] BuildPages()
        {
            if (!BetaEditors.Enabled) return StandardPages;
            var pages = new List<TutorialPage>(StandardPages);
            pages.Insert(1, BetaPage());
            return pages.ToArray();
        }

        private readonly TutorialPage[] Pages = BuildPages();

        public ObservableCollection<string> RecentProjects { get; } = new();

        private int _pageIndex;
        public int PageIndex
        {
            get => _pageIndex;
            set
            {
                if (value < 0 || value >= Pages.Length || value == _pageIndex) return;
                _pageIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PageTitle));
                OnPropertyChanged(nameof(PageBody));
                OnPropertyChanged(nameof(PageLabel));
                OnPropertyChanged(nameof(CanGoBack));
                OnPropertyChanged(nameof(CanGoNext));
            }
        }

        public string PageTitle => Pages[_pageIndex].Title;
        public string PageBody => Pages[_pageIndex].Body;
        public string PageLabel => $"{_pageIndex + 1} / {Pages.Length}";
        public bool CanGoBack => _pageIndex > 0;
        public bool CanGoNext => _pageIndex < Pages.Length - 1;

        public void Back() => PageIndex = _pageIndex - 1;
        public void Next() => PageIndex = _pageIndex + 1;

        public bool ShowAtStartup
        {
            get => SettingsManager.Settings?.showWelcomeOnStartup ?? true;
            set
            {
                if (SettingsManager.Settings == null) return;
                SettingsManager.Settings.showWelcomeOnStartup = value;
                SettingsManager.Save();
                OnPropertyChanged();
            }
        }

        /// <summary>So nobody forgets which mode they started in after clicking past the page.</summary>
        public string WindowTitle => BetaEditors.Enabled
            ? "Welcome to DSPRE (beta features on)"
            : "Welcome to DSPRE";

        public bool HasRecents => RecentProjects.Count > 0;

        public WelcomeViewModel()
        {
            if (Design.IsDesignMode)
            {
                RecentProjects.Add(@"C:\hacks\HeartGold (USA)_DSPRE_contents");
                return;
            }
            var recents = SettingsManager.Settings?.recentProjects;
            if (recents != null)
            {
                foreach (var r in recents) RecentProjects.Add(r);
            }
        }
    }
}
