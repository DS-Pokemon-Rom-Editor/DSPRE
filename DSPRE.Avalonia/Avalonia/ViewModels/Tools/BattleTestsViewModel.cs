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
    public class BattleTestsViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
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
            set
            {
                if ((value ?? "") == _text) return;
                _text = value ?? "";
                if (!_restoring) Capture();
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasUnsavedChanges));
                Problems = _selected == null ? new List<HgEngineBattleTests.Problem>() : HgEngineBattleTests.Problems(_selected, _text);
                OnPropertyChanged(nameof(Problems));
                OnPropertyChanged(nameof(ProblemText));
            }
        }

        public List<HgEngineBattleTests.Problem> Problems { get; private set; } = new();

        // ── Undo, shared by the Code and Form tabs ──────────────────────────
        private readonly UndoHistory<string> _history = new();
        private bool _restoring;
        private DateTime _lastCapture;

        // Typing, in either tab, lands one step per pause rather than one per key.
        private void Capture()
        {
            DateTime now = DateTime.UtcNow;
            _history.Capture(_text, coalesce: now - _lastCapture < TimeSpan.FromMilliseconds(800));
            _lastCapture = now;
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        }

        private void Restore(string text)
        {
            _restoring = true;
            Text = text;
            _restoring = false;
            _lastCapture = default;
            ShowForm(_formShown);
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        }

        public bool CanUndo => _history.CanUndo;
        public bool CanRedo => _history.CanRedo;
        public void Undo() => Restore(_history.Undo());
        public void Redo() => Restore(_history.Redo());

        // ── Form tab ────────────────────────────────────────────────────────
        private bool _formShown;
        public BattleTestFormViewModel Form { get; private set; }

        /// <summary>Builds the form from the text as it is now; called when the Form tab opens or another test opens on it.</summary>
        public void ShowForm(bool shown)
        {
            _formShown = shown;
            if (!shown || _selected == null) return;
            Form = new BattleTestFormViewModel(_selected, _text, t => Text = t);
            OnPropertyChanged(nameof(Form));
        }

        // ── New test from a template ────────────────────────────────────────
        public List<string> Templates => _all;
        private string _template;
        /// <summary>The test a new one starts as a copy of; the open one when none is picked.</summary>
        public string Template { get => _template; set { _template = value; OnPropertyChanged(); } }

        public string ProblemText => Problems.Count switch
        {
            0 => "",
            1 => Problems[0].Message,
            _ => $"{Problems.Count} problems. First: {Problems[0].Message}",
        };

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
            foreach (string t in _all.Where(t => _filter.Length == 0 || t.Contains(_filter, StringComparison.OrdinalIgnoreCase))) Tests.Add(t);
            if (keep != null && Tests.Contains(keep)) OnPropertyChanged(nameof(Selected));
        }

        private void Open(string rel)
        {
            try { _loadedText = HgEngineBattleTests.Read(rel).Replace("\r\n", "\n"); }
            catch (Exception ex) { _loadedText = ""; StatusText = ex.Message; }
            _restoring = true;
            Text = _loadedText;
            _restoring = false;
            _history.Reset(_text);
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            ShowForm(_formShown);
        }

        public void SaveChanges()
        {
            if (!HasUnsavedChanges) return;
            string error = HgEngineBattleTests.Write(_selected, _text);
            if (error != null) { StatusText = "Not saved: " + error; return; }
            _loadedText = _text;
            _history.MarkSaved();
            OnPropertyChanged(nameof(HasUnsavedChanges));
            SaveNotice.Saved(UnsavedChangesDescription);
            StatusText = $"Saved {_selected}.";
        }

        public void DiscardChanges() { if (_selected != null) Open(_selected); }

        /// <summary>Adds a test named <see cref="NewName"/>, starting from the open one.</summary>
        public void AddTest()
        {
            if (HasUnsavedChanges) { StatusText = "Save or discard this test first."; return; }
            string from = _template ?? _selected;
            if (from == null) { StatusText = "Pick a test to start from."; return; }
            string rel = NewName.Trim().Replace('\\', '/');
            if (rel.Length > 0 && !rel.EndsWith(".c", StringComparison.OrdinalIgnoreCase)) rel += ".c";
            string start;
            try { start = from == _selected ? _loadedText : HgEngineBattleTests.Read(from).Replace("\r\n", "\n"); }
            catch (Exception ex) { StatusText = ex.Message; return; }
            string error = HgEngineBattleTests.Create(rel, start);
            if (error != null) { StatusText = error; return; }
            _all = HgEngineBattleTests.List();
            NewName = "";
            Filter = "";
            Selected = rel;
            StatusText = $"Added {rel}, a copy of {from}.";
            Template = null;
            OnPropertyChanged(nameof(Templates));
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
            _history.Reset(_text);
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(Text));
            OnPropertyChanged(nameof(HasUnsavedChanges));
            Refill();
            if (Tests.Count > 0) Selected = Tests[0];
            StatusText = $"Deleted {rel}.";
            return null;
        }
    }
}
