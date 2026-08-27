using Avalonia.Controls;
using Avalonia.Collections;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>
    /// Avalonia port of the WinForms Advanced Header Search: pick a header field, an operator and a
    /// value; results list every matching header. Query logic is the shared core
    /// <see cref="HeaderSearchEngine"/>. Double-tapping a result opens the Header editor on it.
    /// </summary>
    public class HeaderSearchViewModel : INotifyPropertyChanged
    {
        private const int AutoSearchDelayMilliseconds = 200;

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
        private readonly AvaloniaList<string> _results = new();
        public AvaloniaList<string> Results => _results;

        private int _fieldIndex;
        public int FieldIndex
        {
            get => _fieldIndex;
            set
            {
                if (!Set(ref _fieldIndex, value)) return;
                RebuildOperators();
                if (AutoSearch) ScheduleSearch(debounce: true);
            }
        }

        private int _operatorIndex;
        public int OperatorIndex
        {
            get => _operatorIndex;
            set { if (Set(ref _operatorIndex, value) && AutoSearch) ScheduleSearch(debounce: true); }
        }

        private string _valueText = "";
        public string ValueText
        {
            get => _valueText;
            set { if (Set(ref _valueText, value ?? string.Empty) && AutoSearch) ScheduleSearch(debounce: true); }
        }

        private bool _autoSearch = true;
        public bool AutoSearch
        {
            get => _autoSearch;
            set
            {
                if (!Set(ref _autoSearch, value)) return;
                if (!value) CancelPendingSearch();
            }
        }

        private string _statusText = "Ready";
        public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }

        private int _selectedResultIndex = -1;
        public int SelectedResultIndex { get => _selectedResultIndex; set => Set(ref _selectedResultIndex, value); }

        private CancellationTokenSource _searchCancellation;
        private int _searchGeneration;
        private bool _disposed;

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
            ScheduleSearch(debounce: false);
        }

        private void ScheduleSearch(bool debounce)
        {
            CancelPendingSearch();
            if (_disposed) return;

            int generation = unchecked(++_searchGeneration);
            SelectedResultIndex = -1;

            if (string.IsNullOrEmpty(_valueText))
            {
                _results.Clear();
                StatusText = "Ready";
                return;
            }

            if (Fields.Count == 0 || Operators.Count == 0)
            {
                _results.Clear();
                StatusText = "Search is unavailable until headers are loaded.";
                return;
            }

            int fieldIndex = Math.Clamp(_fieldIndex, 0, Fields.Count - 1);
            int fieldKey = FieldKey(fieldIndex);
            int operatorIndex = Math.Clamp(_operatorIndex, 0, Operators.Count - 1);
            string searchConfiguration =
                $"{Fields[fieldIndex]} {Operators[operatorIndex].ToLowerInvariant()} \"{_valueText}\"";
            List<string> internalNames = _internalNames.ToList();
            ushort finalID = (ushort)Math.Min(internalNames.Count, ushort.MaxValue);

            var cancellation = new CancellationTokenSource();
            _searchCancellation = cancellation;
            StatusText = debounce ? "Waiting for more input..." : "Searching...";
            _ = RunSearchAsync(cancellation, generation, debounce, internalNames, finalID,
                fieldKey, operatorIndex, _valueText, searchConfiguration);
        }

        private async Task RunSearchAsync(
            CancellationTokenSource cancellation,
            int generation,
            bool debounce,
            List<string> internalNames,
            ushort finalID,
            int fieldKey,
            int operatorIndex,
            string value,
            string searchConfiguration)
        {
            CancellationToken token = cancellation.Token;
            try
            {
                if (debounce)
                {
                    await Task.Delay(AutoSearchDelayMilliseconds, token).ConfigureAwait(false);
                }

                token.ThrowIfCancellationRequested();
                HashSet<string> result;
                try
                {
                    result = await Task.Run(
                        () => HeaderSearchEngine.AdvancedSearch(0, finalID, internalNames, fieldKey,
                            operatorIndex, value, token), token).ConfigureAwait(false);
                }
                catch (FormatException)
                {
                    PostSearchUpdate(token, generation,
                        () => StatusText = "Make sure the value to search is correct.");
                    return;
                }

                List<string> sortedResults = result?.OrderBy(x => x).ToList();
                PostSearchUpdate(token, generation,
                    () => ApplySearchResult(sortedResults, searchConfiguration));
            }
            catch (OperationCanceledException)
            {
                // A newer keystroke or query superseded this search.
            }
            catch (Exception ex)
            {
                AppLogger.Error("Header search failed: " + ex);
                PostSearchUpdate(token, generation,
                    () => StatusText = "Header search failed.");
            }
            finally
            {
                if (ReferenceEquals(_searchCancellation, cancellation))
                {
                    _searchCancellation = null;
                }
                cancellation.Dispose();
            }
        }

        private void PostSearchUpdate(CancellationToken token, int generation, Action update)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_disposed || token.IsCancellationRequested || generation != _searchGeneration) return;
                update();
            });
        }

        private void ApplySearchResult(List<string> result, string searchConfiguration)
        {
            if (result is null || result.Count == 0)
            {
                _results.Clear();
                StatusText = "No header's " + searchConfiguration;
                return;
            }

            _results.Clear();
            _results.AddRange(result);
            StatusText = $"{result.Count} header(s) whose {searchConfiguration}";
        }

        private void CancelPendingSearch()
        {
            var cancellation = _searchCancellation;
            _searchCancellation = null;
            try { cancellation?.Cancel(); }
            catch (ObjectDisposedException) { }
            unchecked { _searchGeneration++; }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            CancelPendingSearch();
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
