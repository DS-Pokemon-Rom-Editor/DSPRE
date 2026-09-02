using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.ViewModels.Tools
{
    /// <summary>One editable row: a numeric value and its label, with the built-in default as a hint.</summary>
    public sealed class LabelEntryRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void On(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private readonly Action<int, string> _apply;
        private readonly Action<int, int> _applyAttr;
        public int Index { get; }
        public string DefaultHint { get; }
        public bool IsBeyondDefault { get; }

        private string _value;
        public string Value
        {
            get => _value;
            set { if (_value == value) return; _value = value; On(nameof(Value)); _apply(Index, value); }
        }

        // Optional per-entry attribute (e.g. evolution param meaning). AttrOptions == null → no attr column.
        public System.Collections.Generic.IReadOnlyList<string> AttrOptions { get; }
        public bool HasAttr => AttrOptions != null;
        private int _attrIndex;
        public int AttrIndex
        {
            get => _attrIndex;
            set { if (_attrIndex == value) return; _attrIndex = value; On(nameof(AttrIndex)); _applyAttr?.Invoke(Index, value); }
        }

        public LabelEntryRow(int index, string value, string defaultHint, bool beyondDefault, Action<int, string> apply,
            System.Collections.Generic.IReadOnlyList<string> attrOptions, int attrIndex, Action<int, int> applyAttr)
        {
            Index = index; _value = value; DefaultHint = defaultHint; IsBeyondDefault = beyondDefault; _apply = apply;
            AttrOptions = attrOptions; _attrIndex = attrIndex; _applyAttr = applyAttr;
        }
    }

    /// <summary>
    /// Edits <see cref="LabelStore"/> categories: renaming hardcoded dropdown entries and adding entries
    /// beyond the game's defaults (up to the field's data-type cap). Scope is per-project or global.
    /// </summary>
    public class LabelEditorViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private readonly List<LabelCategory> _all = new();   // every category, ordered by group then name
        private readonly List<string> _keys = new();         // keys of the CURRENT group's categories
        public ObservableCollection<string> GroupNames { get; } = new();   // tabs (one per editor)
        public ObservableCollection<string> CategoryNames { get; } = new();
        public ObservableCollection<LabelEntryRow> Entries { get; } = new();

        private int _selGroup = -1;
        public int SelectedGroupIndex { get => _selGroup; set { if (Set(ref _selGroup, value)) ReloadGroup(); } }

        private int _selCat = -1;
        public int SelectedCategoryIndex { get => _selCat; set { if (Set(ref _selCat, value)) ReloadEntries(); } }

        private bool _globalScope;
        public bool GlobalScope
        {
            get => _globalScope;
            set { if (Set(ref _globalScope, value)) { OnPropertyChanged(nameof(ScopeText)); ReloadEntries(); } }
        }
        public string ScopeText => _globalScope
            ? "Global: applies to every ROM you open on this machine."
            : "This project: saved with the current ROM (workDir/dspre_labels.json).";

        private bool _dirty;
        public bool HasUnsavedChanges { get => _dirty; private set => Set(ref _dirty, value); }

        private string _status = "Pick a category to rename its entries, or add entries a ROM hack introduces.";
        public string StatusText { get => _status; set => Set(ref _status, value); }

        public bool CanAddEntry
        {
            get
            {
                var cat = CurrentCategory; return cat != null && LabelStore.DraftCount(cat.Key, _globalScope) < cat.Cap;
            }
        }

        private LabelCategory CurrentCategory =>
            _selCat >= 0 && _selCat < _keys.Count ? LabelStore.GetCategory(_keys[_selCat]) : null;

        public bool CurrentHasAttr => CurrentCategory?.HasAttr == true;
        public string AttrColumnHeader => CurrentCategory?.AttrName ?? "";

        public LabelEditorViewModel()
        {
            _all.AddRange(LabelStore.Categories.OrderBy(c => c.Group).ThenBy(c => c.DisplayName));
            foreach (var g in _all.Select(c => c.Group).Distinct())
                GroupNames.Add(g);
            if (GroupNames.Count > 0) SelectedGroupIndex = 0;
        }

        private void ReloadGroup()
        {
            _keys.Clear();
            CategoryNames.Clear();
            if (_selGroup >= 0 && _selGroup < GroupNames.Count)
            {
                string group = GroupNames[_selGroup];
                foreach (var c in _all.Where(c => c.Group == group))
                {
                    _keys.Add(c.Key);
                    CategoryNames.Add(c.DisplayName);
                }
            }
            SelectedCategoryIndex = _keys.Count > 0 ? 0 : -1;
            OnPropertyChanged(nameof(SelectedCategoryIndex));
        }

        private void ReloadEntries()
        {
            Entries.Clear();
            var cat = CurrentCategory;
            if (cat == null) { OnPropertyChanged(nameof(CanAddEntry)); return; }
            int count = LabelStore.DraftCount(cat.Key, _globalScope);
            for (int i = 0; i < count; i++)
                Entries.Add(MakeRow(cat, i));
            OnPropertyChanged(nameof(CanAddEntry));
            OnPropertyChanged(nameof(CurrentHasAttr));
            OnPropertyChanged(nameof(AttrColumnHeader));
        }

        private LabelEntryRow MakeRow(LabelCategory cat, int index)
        {
            var attrOpts = cat.HasAttr ? cat.AttrOptions : null;
            int attrIdx = cat.HasAttr ? Math.Max(0, Math.Min(cat.AttrOptions.Count - 1, LabelStore.GetDraftAttr(cat.Key, index, _globalScope))) : 0;
            string value = LabelStore.GetDraftLabel(cat.Key, index, _globalScope);
            // Edits go to the DRAFT only; nothing reaches the real store (or other editors) until Save.
            return new LabelEntryRow(index, value, LabelStore.GetDefault(cat.Key, index), index >= cat.Defaults.Count,
                (i, v) => { LabelStore.DraftSetLabel(cat.Key, i, v, _globalScope); HasUnsavedChanges = true; },
                attrOpts, attrIdx,
                (i, a) => { LabelStore.DraftSetAttr(cat.Key, i, a, _globalScope); HasUnsavedChanges = true; });
        }

        public void AddEntry()
        {
            var cat = CurrentCategory;
            if (cat == null || !CanAddEntry) return;
            int index = LabelStore.DraftCount(cat.Key, _globalScope);
            string def = LabelStore.GetDefault(cat.Key, index);
            LabelStore.DraftSetLabel(cat.Key, index, def, _globalScope);
            if (cat.HasAttr) LabelStore.DraftSetAttr(cat.Key, index, cat.AttrDefaultForNew, _globalScope);   // e.g. evolution → "CustomNumber"
            Entries.Add(MakeRow(cat, index));
            HasUnsavedChanges = true;
            OnPropertyChanged(nameof(CanAddEntry));
            StatusText = $"Added entry {index} to “{cat.DisplayName}”. Rename it, then Save.";
        }

        public void ResetCategory()
        {
            var cat = CurrentCategory;
            if (cat == null) return;
            LabelStore.DraftReset(cat.Key, _globalScope);
            HasUnsavedChanges = true;
            ReloadEntries();
            StatusText = $"Reset “{cat.DisplayName}” to defaults ({(GlobalScope ? "global" : "project")}). Save to apply.";
        }

        public void Save()
        {
            LabelStore.CommitDraft();
            AppEvents.RaiseLabelsChanged();   // refresh every open editor's dropdowns
            HasUnsavedChanges = false;
            StatusText = $"Saved labels.";
        }

        /// <summary>Throws away unsaved edits (the draft), called when the window closes without saving.</summary>
        public void Discard()
        {
            LabelStore.DiscardDraft();
            HasUnsavedChanges = false;
        }
    }
}
