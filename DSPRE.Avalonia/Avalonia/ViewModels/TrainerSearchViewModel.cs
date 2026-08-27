using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DSPRE.Avalonia.ViewModels
{
    public class TrainerSearchResult
    {
        public int Index { get; }
        public string Display { get; }
        public TrainerSearchResult(int index, string display) { Index = index; Display = display; }
        public override string ToString() => Display;
    }

    /// <summary>
    /// Avalonia port of the WinForms <c>TrainerSearch</c> dialog. Filters the trainer
    /// name list by a text operator (Contains / Does-not-contain / Is-exactly / Is-not).
    /// "Go to" reports the chosen trainer's original index back to the Trainer Editor.
    /// </summary>
    public class TrainerSearchViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private string[] _names = Array.Empty<string>();

        public ObservableCollection<string> Operators { get; } = new ObservableCollection<string>
        { "Contains", "Does not contain", "Is Exactly", "Is Not" };
        public ObservableCollection<TrainerSearchResult> Results { get; } = new ObservableCollection<TrainerSearchResult>();

        private int _operatorIndex;
        public int OperatorIndex { get => _operatorIndex; set { if (Set(ref _operatorIndex, value) && AutoSearch) Search(); } }

        private string _searchText = "";
        public string SearchText { get => _searchText; set { if (Set(ref _searchText, value) && AutoSearch) Search(); } }

        private bool _caseSensitive;
        public bool CaseSensitive { get => _caseSensitive; set { if (Set(ref _caseSensitive, value) && AutoSearch) Search(); } }

        private bool _autoSearch = true;
        public bool AutoSearch { get => _autoSearch; set => Set(ref _autoSearch, value); }

        private TrainerSearchResult _selectedResult;
        public TrainerSearchResult SelectedResult { get => _selectedResult; set => Set(ref _selectedResult, value); }

        public int ResultIndex { get; private set; } = -1;
        public bool Confirmed { get; private set; }

        public TrainerSearchViewModel() { }

        public TrainerSearchViewModel(IEnumerable<string> names)
        {
            _names = System.Linq.Enumerable.ToArray(names);
            Reset();
        }

        public void Reset()
        {
            Results.Clear();
            for (int i = 0; i < _names.Length; i++) Results.Add(new TrainerSearchResult(i, _names[i]));
            if (Results.Count > 0) SelectedResult = Results[0];
        }

        public void Search()
        {
            if (string.IsNullOrWhiteSpace(_searchText)) { Reset(); return; }

            Results.Clear();
            var cmp = _caseSensitive ? StringComparison.InvariantCulture : StringComparison.InvariantCultureIgnoreCase;
            for (int i = 0; i < _names.Length; i++)
            {
                string s = _names[i];
                bool match = _operatorIndex switch
                {
                    0 => s.IndexOf(_searchText, cmp) >= 0,
                    1 => s.IndexOf(_searchText, cmp) < 0,
                    2 => s.Equals(_searchText, cmp),
                    3 => !s.Equals(_searchText, cmp),
                    _ => false
                };
                if (match) Results.Add(new TrainerSearchResult(i, s));
            }
            if (Results.Count > 0) SelectedResult = Results[0];
        }

        public bool GoTo()
        {
            if (_selectedResult == null) return false;
            ResultIndex = _selectedResult.Index;
            Confirmed = true;
            return true;
        }
    }
}
