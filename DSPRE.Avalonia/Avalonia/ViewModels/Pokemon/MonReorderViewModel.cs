using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    public class MonReorderItem
    {
        public int OriginalIndex { get; }
        public string Display { get; }
        public MonReorderItem(int originalIndex, string display) { OriginalIndex = originalIndex; Display = display; }
        public override string ToString() => Display;
    }

    /// <summary>
    /// Avalonia port of the WinForms <c>MonReorderForm</c>. Reorders a trainer's party
    /// slots. Returns the new order as a permutation of the original slot indices,
    /// which the Trainer Editor applies to the live party data.
    /// </summary>
    public class MonReorderViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        public ObservableCollection<MonReorderItem> Items { get; } = new ObservableCollection<MonReorderItem>();

        private int _selectedIndex = -1;
        public int SelectedIndex
        {
            get => _selectedIndex;
            set { if (Set(ref _selectedIndex, value)) { OnPropertyChanged(nameof(CanMoveUp)); OnPropertyChanged(nameof(CanMoveDown)); } }
        }

        public bool CanMoveUp => _selectedIndex > 0;
        public bool CanMoveDown => _selectedIndex >= 0 && _selectedIndex < Items.Count - 1;

        public bool Confirmed { get; private set; }
        public List<int> ResultOrder => Items.Select(i => i.OriginalIndex).ToList();

        public MonReorderViewModel() { }

        public MonReorderViewModel(IEnumerable<(int index, string display)> party)
        {
            foreach (var p in party) Items.Add(new MonReorderItem(p.index, p.display));
            if (Items.Count > 0) SelectedIndex = 0;
        }

        public void MoveUp() => Move(-1);
        public void MoveDown() => Move(1);

        private void Move(int delta)
        {
            int i = _selectedIndex;
            int j = i + delta;
            if (i < 0 || j < 0 || j >= Items.Count) return;
            var item = Items[i];
            Items.RemoveAt(i);
            Items.Insert(j, item);
            SelectedIndex = j;
        }

        public void Confirm() => Confirmed = true;
    }
}
