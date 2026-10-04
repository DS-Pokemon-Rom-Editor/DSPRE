using DSPRE.Editors;
using DSPRE.HgEngine;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DSPRE.Avalonia.ViewModels.Tools
{
    /// <summary>hg-engine's battle test scenarios (data/battle_tests), listed and edited as the source they are.</summary>
    public class BattleTestsViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private List<string> _all = new();
        public ObservableCollection<string> Tests { get; } = new();

        private string _filter = "";
        public string Filter { get => _filter; set { _filter = value ?? ""; OnPropertyChanged(); Refill(); } }

        private string _selected;
        public string Selected
        {
            get => _selected;
            set
            {
                if (value == _selected || value == null) return;
                // A switch away from unsaved text would lose it, so the list stays where it is until Save or Discard.
                if (HasUnsavedChanges) { StatusText = "Save or discard this test first."; OnPropertyChanged(); return; }
                _selected = value;
                OnPropertyChanged();
                Open(value);
            }
        }

        private string _loadedText = "", _text = "";
        public string Text
        {
            get => _text;
            set { _text = value ?? ""; OnPropertyChanged(); OnPropertyChanged(nameof(HasUnsavedChanges)); }
        }

        public bool HasUnsavedChanges => _selected != null && _text != _loadedText;
        public string UnsavedChangesDescription => $"Battle test {_selected}";

        private string _status = "";
        public string StatusText { get => _status; set { _status = value; OnPropertyChanged(); } }

        private string _newName = "";
        public string NewName { get => _newName; set { _newName = value ?? ""; OnPropertyChanged(); } }

        public BattleTestsViewModel() { }

        public BattleTestsViewModel(bool load)
        {
            if (!load) return;
            _all = HgEngineBattleTests.List();
            Refill();
            StatusText = $"{_all.Count} tests in {HgEngineBattleTests.RelDir}.";
            if (Tests.Count > 0) Selected = Tests[0];
        }

        private void Refill()
        {
            string keep = _selected;
            Tests.Clear();
            foreach (var t in _all.Where(t => _filter.Length == 0 || t.Contains(_filter, StringComparison.OrdinalIgnoreCase))) Tests.Add(t);
            if (keep != null && Tests.Contains(keep)) OnPropertyChanged(nameof(Selected));
        }

        private void Open(string rel)
        {
            try { _loadedText = HgEngineBattleTests.Read(rel).Replace("\r\n", "\n"); }
            catch (Exception ex) { _loadedText = ""; StatusText = ex.Message; }
            Text = _loadedText;
        }

        public void SaveChanges()
        {
            if (!HasUnsavedChanges) return;
            string error = HgEngineBattleTests.Write(_selected, _text);
            if (error != null) { StatusText = "Not saved: " + error; return; }
            _loadedText = _text;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            SaveNotice.Saved(UnsavedChangesDescription);
            StatusText = $"Saved {_selected}.";
        }

        public void DiscardChanges() { if (_selected != null) Open(_selected); }

        /// <summary>Adds a test named <see cref="NewName"/>, starting from the open one.</summary>
        public void AddTest()
        {
            if (HasUnsavedChanges) { StatusText = "Save or discard this test first."; return; }
            if (_selected == null) { StatusText = "Open a test to start the new one from."; return; }
            string rel = NewName.Trim().Replace('\\', '/');
            if (rel.Length > 0 && !rel.EndsWith(".c", StringComparison.OrdinalIgnoreCase)) rel += ".c";
            string error = HgEngineBattleTests.Create(rel, _loadedText);
            if (error != null) { StatusText = error; return; }
            _all = HgEngineBattleTests.List();
            NewName = "";
            Filter = "";
            Selected = rel;
            StatusText = $"Added {rel}, a copy of the test it started from.";
        }

        public string DeleteSelected()
        {
            if (_selected == null) return "Open a test first.";
            string rel = _selected;
            string error = HgEngineBattleTests.Delete(rel);
            if (error != null) return error;
            _all = HgEngineBattleTests.List();
            _selected = null;
            _loadedText = _text = "";
            OnPropertyChanged(nameof(Text));
            OnPropertyChanged(nameof(HasUnsavedChanges));
            Refill();
            if (Tests.Count > 0) Selected = Tests[0];
            StatusText = $"Deleted {rel}.";
            return null;
        }
    }
}
