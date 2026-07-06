using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>
    /// Avalonia port of the WinForms Advanced Header Search: pick a header field, an operator and a
    /// value; results list every matching header. Query logic is the shared core
    /// <see cref="HeaderSearchEngine"/>. Double-tapping a result opens the Header editor on it.
    /// </summary>
    public class HeaderSearchViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (EqualityComparer<T>.Default.Equals(f, v)) return false;
            f = v; OnPropertyChanged(n); return true;
        }

        private List<string> _internalNames = new();

        public ObservableCollection<string> Fields { get; } = new();
        public ObservableCollection<string> Operators { get; } = new();
        public ObservableCollection<string> Results { get; } = new();

        private int _fieldIndex;
        public int FieldIndex
        {
            get => _fieldIndex;
            set
            {
                if (!Set(ref _fieldIndex, value)) return;
                RebuildOperators();
                if (AutoSearch) Search(report: false);
            }
        }

        private int _operatorIndex;
        public int OperatorIndex
        {
            get => _operatorIndex;
            set { if (Set(ref _operatorIndex, value) && AutoSearch) Search(report: false); }
        }

        private string _valueText = "";
        public string ValueText
        {
            get => _valueText;
            set { if (Set(ref _valueText, value) && AutoSearch) Search(report: false); }
        }

        private bool _autoSearch = true;
        public bool AutoSearch { get => _autoSearch; set => Set(ref _autoSearch, value); }

        private string _statusText = "Ready";
        public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }

        private int _selectedResultIndex = -1;
        public int SelectedResultIndex { get => _selectedResultIndex; set => Set(ref _selectedResultIndex, value); }

        // Design-time
        public HeaderSearchViewModel()
        {
            if (!Design.IsDesignMode) return;
            Fields.Add("Matrix (ID)");
            Operators.Add("Equals");
            Results.Add("006 -   D17R1101");
        }

        public HeaderSearchViewModel(bool _)
        {
            _internalNames = HeaderLists.GetInternalNames() ?? new List<string>();
            foreach (var f in HeaderSearchEngine.SearchableFields.Values) Fields.Add(f);
            _fieldIndex = 0;
            RebuildOperators();
        }

        private void RebuildOperators()
        {
            Operators.Clear();
            var names = HeaderSearchEngine.IsNumericField(FieldKey(_fieldIndex))
                ? HeaderSearchEngine.NumOperatorNames.Values
                : HeaderSearchEngine.TextOperatorNames.Values.AsEnumerable();
            foreach (var o in names) Operators.Add(o);
            _operatorIndex = 0;
            OnPropertyChanged(nameof(OperatorIndex));
        }

        // The Fields combo shows the dict VALUES in dict order; map index back to the enum key.
        private int FieldKey(int comboIndex)
            => (int)HeaderSearchEngine.SearchableFields.Keys.ElementAt(Math.Clamp(comboIndex, 0, HeaderSearchEngine.SearchableFields.Count - 1));

        public void Search(bool report = true)
        {
            Results.Clear();
            if (string.IsNullOrEmpty(_valueText))
            {
                StatusText = "Ready";
                return;
            }

            HashSet<string> result;
            try
            {
                result = HeaderSearchEngine.AdvancedSearch(0, (ushort)_internalNames.Count, _internalNames,
                    FieldKey(_fieldIndex), _operatorIndex, _valueText);
            }
            catch (FormatException)
            {
                StatusText = "Make sure the value to search is correct.";
                return;
            }

            string searchConfiguration = $"{Fields[_fieldIndex]} {Operators[_operatorIndex].ToLower()} \"{_valueText}\"";
            if (result is null || result.Count == 0)
            {
                StatusText = "No header's " + searchConfiguration;
                return;
            }

            foreach (var r in result.OrderBy(x => x)) Results.Add(r);
            StatusText = $"{result.Count} header(s) whose {searchConfiguration}";
        }

        /// <summary>Header id of the selected result, or -1.</summary>
        public int SelectedHeaderId()
        {
            if (_selectedResultIndex < 0 || _selectedResultIndex >= Results.Count) return -1;
            string s = Results[_selectedResultIndex];
            int cut = s.IndexOf(' ');
            string idPart = cut > 0 ? s.Substring(0, cut) : s;
            return int.TryParse(idPart.TrimEnd('-', ' '), out int id) ? id : -1;
        }
    }
}
