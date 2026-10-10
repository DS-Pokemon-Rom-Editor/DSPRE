using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using DSPRE.Csv;

namespace DSPRE.Avalonia.ViewModels.Shell
{
    /// <summary>Shows every problem a CSV import found, lets each be fixed, skipped with its row or kept, and lists
    /// what applying would change.</summary>
    public class CsvImportReviewViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public sealed class ProblemRow
        {
            public ProblemRow(CsvRecord record, CsvIssue issue, string column)
            {
                Record = record;
                Issue = issue;
                Column = column;
            }
            public CsvRecord Record { get; }
            public CsvIssue Issue { get; }
            public int Line => Record.Line;
            public string What => Record.Label.Length > 0 ? Record.Label : "?";
            public string Column { get; }
            public string Message => Issue.Message;
            public string State => Record.Skipped ? "Skipped" : Record.IsKept(Issue) ? "Kept" : Issue.Kind == CsvIssueKind.Error ? "Error" : "Warning";
            public bool IsOpen => !Record.Skipped && !Record.IsKept(Issue);
            public bool IsError => IsOpen && Issue.Kind == CsvIssueKind.Error;
            public bool IsWarning => IsOpen && Issue.Kind == CsvIssueKind.Warning;
        }

        private readonly CsvImportSession _session;

        public CsvImportReviewViewModel(CsvImportSession session)
        {
            _session = session;
            Title = $"{session.Importer.Title} CSV import";
            Refresh(null);
            SelectedTab = Problems.Count == 0 ? 1 : 0;
        }

        public CsvImportSession Session => _session;
        public string Title { get; }
        public bool Confirmed { get; private set; }

        public ObservableCollection<ProblemRow> Problems { get; } = new ObservableCollection<ProblemRow>();
        public ObservableCollection<CsvChange> Changes { get; } = new ObservableCollection<CsvChange>();

        public string FileText => string.Join("\n", _session.FileProblems.Concat(_session.Notes));
        public bool HasFileProblems => _session.FileProblems.Count > 0;
        public bool HasNotes => _session.FileProblems.Count == 0 && _session.Notes.Count > 0;

        private string _summary = "";
        public string Summary { get => _summary; private set { _summary = value; OnPropertyChanged(); } }
        public string ProblemsHeader => $"Problems ({Problems.Count(p => p.IsOpen)})";
        public string ChangesHeader => $"Changes ({Changes.Count})";

        private bool _showSettled = true;
        public bool ShowSettled { get => _showSettled; set { if (_showSettled == value) return; _showSettled = value; OnPropertyChanged(); Refresh(_selected); } }

        private int _selectedTab;
        public int SelectedTab { get => _selectedTab; set { if (_selectedTab == value) return; _selectedTab = value; OnPropertyChanged(); } }

        private ProblemRow _selected;
        public ProblemRow Selected
        {
            get => _selected;
            set
            {
                if (ReferenceEquals(_selected, value)) return;
                _selected = value;
                _editText = value != null && value.Issue.Column >= 0 ? value.Record.Cells[value.Issue.Column] : "";
                OnPropertyChanged();
                OnPropertyChanged(nameof(EditText));
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(CanEdit));
                OnPropertyChanged(nameof(EditLabel));
                OnPropertyChanged(nameof(Fixes));
                OnPropertyChanged(nameof(HasFixes));
                OnPropertyChanged(nameof(SkipText));
                OnPropertyChanged(nameof(CanKeep));
                OnPropertyChanged(nameof(KeepText));
                OnPropertyChanged(nameof(RowText));
            }
        }

        public bool HasSelection => _selected != null;
        public bool CanEdit => _selected != null && _selected.Issue.Column >= 0 && !_selected.Record.Skipped;
        public string EditLabel => _selected != null && _selected.Issue.Column >= 0 ? _session.Importer.Columns[_selected.Issue.Column] : "";
        public IReadOnlyList<CsvFix> Fixes => _selected != null && !_selected.Record.Skipped ? _selected.Issue.Fixes : System.Array.Empty<CsvFix>();
        public bool HasFixes => Fixes.Count > 0;
        public string SkipText => _selected?.Record.Skipped == true ? "Include row" : "Skip row";
        public bool CanKeep => _selected != null && !_selected.Record.Skipped && _selected.Issue.Kind == CsvIssueKind.Warning;
        public string KeepText => _selected != null && _selected.Record.IsKept(_selected.Issue) ? "Undo keep" : "Keep";
        /// <summary>The whole row as the file has it, for context.</summary>
        public string RowText => _selected == null ? "" : string.Join("\n",
            _session.Importer.Columns.Select((c, i) => $"{c}: {_selected.Record.Cells[i]}"));

        private string _editText = "";
        public string EditText { get => _editText; set { if (_editText == value) return; _editText = value ?? ""; OnPropertyChanged(); } }

        public bool CanApply => _session.CanApply;

        public void ApplyEdit()
        {
            if (!CanEdit) return;
            Change(_selected, () => _session.SetCell(_selected.Record, _selected.Issue.Column, _editText));
        }

        public void UseFix(CsvFix fix)
        {
            if (_selected == null || fix == null) return;
            Change(_selected, () => _session.SetCell(_selected.Record, fix.Column, fix.Text));
        }

        public void ToggleSkip()
        {
            if (_selected == null) return;
            Change(_selected, () => _session.SetSkipped(_selected.Record, !_selected.Record.Skipped));
        }

        public void ToggleKeep()
        {
            if (!CanKeep) return;
            Change(_selected, () => _selected.Record.Keep(_selected.Issue, !_selected.Record.IsKept(_selected.Issue)));
        }

        public void SkipRowsWithErrors()
        {
            foreach (CsvRecord r in _session.Records.Where(r => !r.Skipped && r.HasErrors).ToList()) r.Skipped = true;
            _session.CheckAll();
            Refresh(_selected);
        }

        public void KeepAllWarnings()
        {
            foreach (CsvRecord r in _session.Records.Where(r => !r.Skipped))
                foreach (CsvIssue i in r.Issues.Where(i => i.Kind == CsvIssueKind.Warning)) r.Keep(i, true);
            Refresh(_selected);
        }

        public void Accept() { if (CanApply) Confirmed = true; }

        private void Change(ProblemRow at, System.Action change)
        {
            change();
            Refresh(at);
        }

        // Rebuild after each change and reselect the same problem, or the next open one.
        private void Refresh(ProblemRow at)
        {
            List<ProblemRow> all = new List<ProblemRow>();
            foreach (CsvRecord r in _session.Records)
                foreach (CsvIssue i in r.Issues)
                    all.Add(new ProblemRow(r, i, i.Column >= 0 ? _session.Importer.Columns[i.Column] : "Row"));
            List<ProblemRow> shown = _showSettled ? all : all.Where(p => p.IsOpen).ToList();

            Problems.Clear();
            foreach (ProblemRow p in shown) Problems.Add(p);
            Changes.Clear();
            if (_session.FileProblems.Count == 0)
                foreach (CsvChange c in _session.Changes()) Changes.Add(c);

            ProblemRow again = at == null ? null
                : shown.FirstOrDefault(p => ReferenceEquals(p.Record, at.Record) && p.Issue.Column == at.Issue.Column && p.IsOpen)
                  ?? shown.FirstOrDefault(p => p.Record.Index >= at.Record.Index && p.IsOpen)
                  ?? shown.FirstOrDefault(p => p.IsOpen)
                  ?? shown.FirstOrDefault(p => ReferenceEquals(p.Record, at.Record));
            _selected = null;
            Selected = again;

            int rows = _session.Records.Count, skipped = _session.Records.Count(r => r.Skipped);
            int errors = all.Count(p => p.IsError), warnings = all.Count(p => p.IsWarning);
            int changedRecords = Changes.Select(c => c.Record).Distinct().Count();
            List<string> parts = new List<string> { $"{_session.FileName}: {rows} rows" };
            if (errors > 0) parts.Add(Plural(errors, "error"));
            if (warnings > 0) parts.Add(Plural(warnings, "warning"));
            if (skipped > 0) parts.Add($"{skipped} skipped");
            parts.Add(changedRecords == 0 ? "nothing changes" : $"{changedRecords} {(changedRecords == 1 ? "entry changes" : "entries change")}");
            Summary = string.Join(", ", parts) + ".";

            OnPropertyChanged(nameof(ProblemsHeader));
            OnPropertyChanged(nameof(ChangesHeader));
            OnPropertyChanged(nameof(CanApply));
        }

        private static string Plural(int n, string word) => $"{n} {word}{(n == 1 ? "" : "s")}";
    }
}
