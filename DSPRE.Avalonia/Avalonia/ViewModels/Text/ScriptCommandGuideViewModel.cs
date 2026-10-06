using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.ViewModels.Text
{
    /// <summary>Backs the move-script command guide window: a searchable reference list of every opcode's
    /// single-word command name, what it does and what its arguments mean.</summary>
    public class ScriptCommandGuideViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T field, T value, [CallerMemberName] string n = null)
        { if (Equals(field, value)) return false; field = value; OnPropertyChanged(n); return true; }

        private List<GuideEntry> _all;
        private readonly List<(string Name, Func<IReadOnlyList<GuideEntry>> Load)> _sections = new();

        public string Title { get; }
        public string Hint { get; }
        public bool IsEventScript { get; }

        /// <summary>Column headers; event scripts show a command's number where move scripts show a title.</summary>
        public string SecondColumn => IsEventScript ? "ID" : "Title";
        public string ThirdColumn => IsEventScript ? "Parameters" : "Arguments";

        public List<string> SectionNames { get; } = new();
        public bool HasSections => SectionNames.Count > 1;

        private int _sectionIndex;
        public int SectionIndex
        {
            get => _sectionIndex;
            set
            {
                if (value < 0 || value >= _sections.Count || !Set(ref _sectionIndex, value)) return;
                _all = new List<GuideEntry>(_sections[value].Load());
                Refresh();
            }
        }
        public ObservableCollection<GuideEntry> Entries { get; } = new();

        private string _searchText = "";
        public string SearchText { get => _searchText; set { if (Set(ref _searchText, value)) Refresh(); } }

        public ScriptCommandGuideViewModel(bool isAnimation)
        {
            Title = isAnimation ? "Move animation commands" : "Move effect-sequence commands";
            Hint = "Every command usable in this script's text editor, with its arguments and what it does. Type a command's arguments as label=value (e.g. slot=0); unlabeled numbers just fill whichever argument slots are left, in order.";
            _all = new List<GuideEntry>(isAnimation ? ScriptCommandGuide.ForWest() : ScriptCommandGuide.ForWazaSeq());
            Refresh();
        }

        /// <summary>The event-script command database for the loaded ROM: commands, movements and comparison operators.</summary>
        public static ScriptCommandGuideViewModel ForEventScripts() => new ScriptCommandGuideViewModel(
            "Script commands",
            "The commands, movements and comparison operators of the loaded ROM's script command database, as the script editor reads them.",
            true);

        private ScriptCommandGuideViewModel(string title, string hint, bool eventScript)
        {
            Title = title;
            Hint = hint;
            IsEventScript = eventScript;
            _sections.Add(("Commands", ScriptCommandGuide.ForEventScripts));
            _sections.Add(("Movements", ScriptCommandGuide.ForMovements));
            _sections.Add(("Comparison operators", ScriptCommandGuide.ForComparisonOperators));
            foreach ((string Name, Func<IReadOnlyList<GuideEntry>> Load) section in _sections) SectionNames.Add(section.Name);
            _all = new List<GuideEntry>(_sections[0].Load());
            Refresh();
        }

        public ScriptCommandGuideViewModel() : this(true) { }   // design-time / parameterless

        private void Refresh()
        {
            Entries.Clear();
            string q = _searchText?.Trim();
            foreach (GuideEntry e in _all)
            {
                if (!string.IsNullOrEmpty(q)
                    && !SearchMatch.Contains(e.Command, q)
                    && !SearchMatch.Contains(e.Title, q)
                    && !SearchMatch.Contains(e.Params, q)
                    && !SearchMatch.Contains(e.Description, q))
                    continue;
                Entries.Add(e);
            }
        }
    }
}
