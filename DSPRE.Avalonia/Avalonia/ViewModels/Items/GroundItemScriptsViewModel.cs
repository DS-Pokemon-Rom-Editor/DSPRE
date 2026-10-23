using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Avalonia.Controls;

namespace DSPRE.Avalonia.ViewModels.Items
{
    /// <summary>Read-only display row for one ground-item script entry.</summary>
    public sealed class GroundItemRow
    {
        public int ScriptIndex { get; set; }
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public int Quantity { get; set; }
        public bool InUse { get; set; }
        public string InUseText => InUse ? "Yes" : "No";
    }

    /// <summary>
    /// Backing model for the ground item list dialog (Event editor's Overworld Item panel). Edits stay in a
    /// <see cref="GroundItemScriptsLogic.Session"/> until Save, which also renumbers the item events. Each edit is an
    /// undo step; Save starts the history again, since it renumbers the entries the steps refer to.
    /// </summary>
    public class GroundItemScriptsViewModel : INotifyPropertyChanged, DSPRE.Editors.IEditorWithUnsavedChanges, ISupportsUndo
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
            set
            {
                if (!Set(ref _selectedEntry, value)) return;
                OnPropertyChanged(nameof(CanRemove));
                // The picker and quantity show the selected entry, ready to change it.
                if (value == null) return;
                if (value.ItemId >= 0 && value.ItemId < ItemNames.Count) NewItemIndex = value.ItemId;
                NewQuantity = value.Quantity;
            }
        }
        public bool CanRemove => _selectedEntry != null;

        private int _newItemIndex = -1;
        public int NewItemIndex { get => _newItemIndex; set => Set(ref _newItemIndex, value); }

        private decimal _newQuantity = 1;
        public decimal NewQuantity { get => _newQuantity; set => Set(ref _newQuantity, value); }

        private string _statusText = "";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        /// <summary>Set once a save has written anything, so the owning Event editor knows to
        /// re-sync its own Item dropdown (indices may have shifted).</summary>
        public bool Changed { get; private set; }

        private GroundItemScriptsLogic.Session _session;
        private readonly UndoHistory<GroundItemScriptsLogic.Session.Snapshot> _history = new();
        public bool HasUnsavedChanges => _session?.HasChanges == true;
        public string UnsavedChangesDescription => "Ground item list";

        public GroundItemScriptsViewModel()
        {
            if (Design.IsDesignMode) return;
            foreach (string name in RomInfo.GetItemNames()) ItemNames.Add(name);
            _session = new GroundItemScriptsLogic.Session();
            _history.Reset(_session.Take());
            Refresh();
        }

        public bool CanUndo => _history.CanUndo;
        public bool CanRedo => _history.CanRedo;

        public void Undo()
        {
            if (!_history.CanUndo) return;
            _session.Restore(_history.Undo());
            SelectedEntry = null;
            Refresh();
        }

        public void Redo()
        {
            if (!_history.CanRedo) return;
            _session.Restore(_history.Redo());
            SelectedEntry = null;
            Refresh();
        }

        // After a change to the pending list.
        private void Step()
        {
            _history.Capture(_session.Take());
            Refresh();
        }

        public void SaveChanges() => Save();
        public void DiscardChanges() => Discard();

        private void Refresh()
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            Entries.Clear();
            foreach (GroundItemScriptsLogic.Entry e in _session.Entries())
            {
                string name = e.ItemId >= 0 && e.ItemId < ItemNames.Count ? ItemNames[e.ItemId] : ("Item " + e.ItemId);
                Entries.Add(new GroundItemRow { ScriptIndex = e.ScriptIndex, ItemId = e.ItemId, ItemName = name, Quantity = e.Quantity, InUse = e.InUse });
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
            Step();
        }

        public void ChangeSelectedEntry()
        {
            if (_selectedEntry == null) return;
            if (_newItemIndex < 0 || _newItemIndex >= ItemNames.Count)
            {
                StatusText = "Pick an item first.";
                return;
            }

            int index = _selectedEntry.ScriptIndex;
            _session.Update(index, _newItemIndex, (int)_newQuantity);
            StatusText = "";
            Step();
            foreach (GroundItemRow row in Entries)
                if (row.ScriptIndex == index) { SelectedEntry = row; break; }
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
            Step();
        }

        public void Save()
        {
            if (!HasUnsavedChanges) return;
            string failed = null;
            try { if (!_session.Save()) failed = ""; }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { failed = " " + ex.Message; }
            if (failed != null)
            {
                StatusText = "The item script file couldn't be saved." + failed;
                return;
            }
            _history.Reset(_session.Take());
            Changed = true;
            StatusText = "";
            Refresh();
        }

        public void Discard()
        {
            _session = new GroundItemScriptsLogic.Session();
            _history.Reset(_session.Take());
            SelectedEntry = null;
            StatusText = "";
            Refresh();
        }
    }
}
