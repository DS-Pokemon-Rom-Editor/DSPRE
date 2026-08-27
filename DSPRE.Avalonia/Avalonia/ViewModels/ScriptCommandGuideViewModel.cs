using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>Backs the move-script command guide window: a searchable reference list of every opcode's
    /// single-word command name, what it does and what its arguments mean.</summary>
    public class ScriptCommandGuideViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T field, T value, [CallerMemberName] string n = null)
        { if (Equals(field, value)) return false; field = value; OnPropertyChanged(n); return true; }

        private readonly List<GuideEntry> _all;

        public string Title { get; }
        public ObservableCollection<GuideEntry> Entries { get; } = new();

        private string _searchText = "";
        public string SearchText { get => _searchText; set { if (Set(ref _searchText, value)) Refresh(); } }

        public ScriptCommandGuideViewModel(bool isWest)
        {
            Title = isWest ? "Move animation commands (WEST)" : "Move effect-sequence commands";
            _all = new List<GuideEntry>(isWest ? ScriptCommandGuide.ForWest() : ScriptCommandGuide.ForWazaSeq());
            Refresh();
        }

        public ScriptCommandGuideViewModel() : this(true) { }   // design-time / parameterless

        private void Refresh()
        {
            Entries.Clear();
            string q = _searchText?.Trim();
            foreach (var e in _all)
            {
                if (!string.IsNullOrEmpty(q)
                    && e.Command.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0
                    && e.Title.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0
                    && e.Params.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0
                    && e.Description.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                Entries.Add(e);
            }
        }
    }
}
