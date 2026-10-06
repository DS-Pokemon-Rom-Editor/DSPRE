using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// A searchable view over a list of names that an editor selects by index. The shown rows follow the filter; the
    /// selection is still read and written through the editor's own index, so its unsaved-changes guard keeps working.
    /// Rows that hold the filter come first; near misses (about one typo per four letters) only when none do.
    /// </summary>
    public sealed class FilteredNames : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>The shown rows were rebuilt, so a view can bring the selection back into sight.</summary>
        public event EventHandler Rebuilt;

        private readonly ObservableCollection<string> _source;
        private readonly Func<int> _getIndex;
        private readonly Action<int> _setIndex;
        private readonly List<int> _map = new();
        private string _filter = "";
        private bool _rebuilding;

        public ObservableCollection<string> Shown { get; } = new();

        public FilteredNames(ObservableCollection<string> source, Func<int> getIndex, Action<int> setIndex,
                             INotifyPropertyChanged owner, string indexProperty)
        {
            _source = source; _getIndex = getIndex; _setIndex = setIndex;
            _source.CollectionChanged += (_, _) => Rebuild();
            owner.PropertyChanged += (_, e) => { if (e.PropertyName == indexProperty) Raise(nameof(ShownIndex)); };
            Rebuild();
        }

        public string Filter
        {
            get => _filter;
            set
            {
                string v = value ?? "";
                if (v == _filter) return;
                _filter = v;
                Raise(nameof(Filter));
                Rebuild();
            }
        }

        public bool IsFiltered => _filter.Trim().Length > 0;

        public int ShownIndex
        {
            get => _map.IndexOf(_getIndex());
            set
            {
                if (_rebuilding || value < 0 || value >= _map.Count) return;
                _setIndex(_map[value]);
                Raise(nameof(ShownIndex));
            }
        }

        private void Rebuild()
        {
            _rebuilding = true;
            try
            {
                _map.Clear();
                Shown.Clear();
                string q = _filter.Trim();
                if (q.Length == 0)
                    for (int i = 0; i < _source.Count; i++) _map.Add(i);
                else
                {
                    for (int i = 0; i < _source.Count; i++)
                        if (SearchMatch.Contains(_source[i], q)) _map.Add(i);
                    if (_map.Count == 0)
                        for (int i = 0; i < _source.Count; i++)
                            if (SearchMatch.NearMiss(_source[i], q)) _map.Add(i);
                }
                foreach (int i in _map) Shown.Add(_source[i]);
            }
            finally { _rebuilding = false; }
            Raise(nameof(IsFiltered));
            Raise(nameof(ShownIndex));
            Rebuilt?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Scrolls <paramref name="list"/> to the selected row whenever the shown rows change.</summary>
        public void KeepInView(global::Avalonia.Controls.ListBox list) =>
            Rebuilt += (_, _) => global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (list.SelectedIndex >= 0) list.ScrollIntoView(list.SelectedIndex);
            }, global::Avalonia.Threading.DispatcherPriority.Background);

        private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
