using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>One palette entry: a display name + the action that opens it.</summary>
    public sealed class CommandItem
    {
        public string Name { get; init; }
        public string Keywords { get; init; } = "";   // extra search terms
        public Action Run { get; init; }
        public override string ToString() => Name;
    }

    /// <summary>
    /// Quick-open / command palette: type to filter the editor list, Enter (or click) to launch it.
    /// Mirrors the main menu so any editor is reachable in two keystrokes.
    /// </summary>
    public class CommandPaletteViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void On([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private readonly List<CommandItem> _all;
        public ObservableCollection<CommandItem> Items { get; } = new();

        private string _search = "";
        public string SearchText { get => _search; set { if (_search == value) return; _search = value; On(); Refilter(); } }

        private int _selectedIndex;
        public int SelectedIndex { get => _selectedIndex; set { if (_selectedIndex == value) return; _selectedIndex = value; On(); } }

        public CommandPaletteViewModel(IEnumerable<CommandItem> commands)
        {
            _all = commands.ToList();
            Refilter();
        }

        private void Refilter()
        {
            string q = _search?.Trim() ?? "";
            Items.Clear();
            IEnumerable<CommandItem> matches = string.IsNullOrEmpty(q)
                ? _all
                : _all.Where(c => Match(c, q)).OrderByDescending(c => Score(c, q));
            foreach (var c in matches) Items.Add(c);
            SelectedIndex = Items.Count > 0 ? 0 : -1;
        }

        private static bool Match(CommandItem c, string q)
            => c.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || c.Keywords.Contains(q, StringComparison.OrdinalIgnoreCase);

        // Rank: name prefix > name contains > keyword match.
        private static int Score(CommandItem c, string q)
        {
            if (c.Name.StartsWith(q, StringComparison.OrdinalIgnoreCase)) return 3;
            if (c.Name.Contains(q, StringComparison.OrdinalIgnoreCase)) return 2;
            return 1;
        }

        /// <summary>The currently highlighted command, or null.</summary>
        public CommandItem Selected => _selectedIndex >= 0 && _selectedIndex < Items.Count ? Items[_selectedIndex] : null;
    }
}
