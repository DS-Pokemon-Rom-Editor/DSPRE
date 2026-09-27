using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;

namespace DSPRE.Avalonia.ViewModels.Items
{
    /// <summary>Read-only display row for one ground-item script entry.</summary>
    public sealed class GroundItemRow
    {
        public int ScriptIndex { get; set; }
        public string ItemName { get; set; }
        public int Quantity { get; set; }
        public bool InUse { get; set; }
        public string InUseText => InUse ? "Yes" : "No";
    }

    /// <summary>
    /// Backing model for the ground item list dialog (Event Editor's Overworld Item panel). Edits stay in a
    /// <see cref="GroundItemScriptsLogic.Session"/> until Save, which also renumbers the item events.
    /// </summary>
    public class GroundItemScriptsViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        public ObservableCollection<GroundItemRow> Entries { get; } = new ObservableCollection<GroundItemRow>();
        public List<string> ItemNames { get; } = new List<string>();

        private GroundItemRow _selectedEntry;
        public GroundItemRow SelectedEntry
        {
            get => _selectedEntry;
            set { if (Set(ref _selectedEntry, value)) OnPropertyChanged(nameof(CanRemove)); }
        }
        public bool CanRemove => _selectedEntry != null;

        private int _newItemIndex = -1;
        public int NewItemIndex { get => _newItemIndex; set => Set(ref _newItemIndex, value); }

        private decimal _newQuantity = 1;
        public decimal NewQuantity { get => _newQuantity; set => Set(ref _newQuantity, value); }

        private string _statusText = "";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        /// <summary>Set once a save has written anything, so the owning Event Editor knows to
        /// re-sync its own Item dropdown (indices may have shifted).</summary>
        public bool Changed { get; private set; }

        private GroundItemScriptsLogic.Session _session;
        public bool HasUnsavedChanges => _session?.HasChanges == true;

        public GroundItemScriptsViewModel()
        {
            if (Design.IsDesignMode) return;
            foreach (string name in RomInfo.GetItemNames()) ItemNames.Add(name);
            _session = new GroundItemScriptsLogic.Session();
            Refresh();
        }

        private void Refresh()
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
            Entries.Clear();
            foreach (var e in _session.Entries())
            {
                string name = e.ItemId >= 0 && e.ItemId < ItemNames.Count ? ItemNames[e.ItemId] : ("Item " + e.ItemId);
                Entries.Add(new GroundItemRow { ScriptIndex = e.ScriptIndex, ItemName = name, Quantity = e.Quantity, InUse = e.InUse });
            }
        }

        public void AddEntry()
        {
            if (_newItemIndex < 0 || _newItemIndex >= ItemNames.Count)
            {
                StatusText = "Pick an item first.";
                return;
            }

            _session.Add(_newItemIndex, (int)_newQuantity);
            StatusText = "";
            Refresh();
        }

        public void RemoveSelectedEntry()
        {
            if (_selectedEntry == null) return;

            string error = _session.Remove(_selectedEntry.ScriptIndex);
            if (error != null)
            {
                StatusText = error;
                return;
            }

            StatusText = "";
            SelectedEntry = null;
            Refresh();
        }

        public void Save()
        {
            if (!HasUnsavedChanges) return;
            _session.Save();
            Changed = true;
            StatusText = "";
            Refresh();
        }

        public void Discard()
        {
            _session = new GroundItemScriptsLogic.Session();
            SelectedEntry = null;
            StatusText = "";
            Refresh();
        }
    }
}
