using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using DSPRE.Avalonia;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>One bulk-editable learnset row: which species learns which move at which level.</summary>
    public sealed class BulkLearnsetRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void On(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        public ObservableCollection<string> SpeciesNames { get; }
        public ObservableCollection<string> MoveNames { get; }
        private readonly Action _changed;
        public BulkLearnsetRow(ObservableCollection<string> species, ObservableCollection<string> moves, int sp, int lvl, int mv, Action changed)
        { SpeciesNames = species; MoveNames = moves; _species = sp; _level = lvl; _move = mv; _changed = changed; }

        private int _species; public int SpeciesIndex { get => _species; set { if (_species == value) return; _species = value; On(nameof(SpeciesIndex)); _changed(); } }
        private int _level;   public int Level        { get => _level;   set { if (_level == value) return; _level = value; On(nameof(Level)); _changed(); } }
        private int _move;    public int MoveIndex    { get => _move;    set { if (_move == value) return; _move = value; On(nameof(MoveIndex)); _changed(); } }
    }

    /// <summary>
    /// Avalonia port of the WinForms Bulk Learnset Editor: edit every Pokémon's level-up learnset in
    /// one grid. Rows are (species, level, move); a species filter narrows the view, and Save writes
    /// each species' rows back to its learnset file (sorted by level).
    /// </summary>
    public class BulkLearnsetEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        public ObservableCollection<string> SpeciesNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> MoveNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> SpeciesFilter { get; } = new ObservableCollection<string> { "All species" };
        public ObservableCollection<BulkLearnsetRow> Rows { get; } = new ObservableCollection<BulkLearnsetRow>();

        private readonly List<BulkLearnsetRow> _all = new List<BulkLearnsetRow>();
        private int _learnsetCount;

        private int _filterIndex;
        public int FilterIndex { get => _filterIndex; set { if (Set(ref _filterIndex, value)) ApplyFilter(); } }

        private int _selectedRow = -1;
        public int SelectedRow { get => _selectedRow; set => Set(ref _selectedRow, value); }

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => "Bulk learnsets";
        public void SaveChanges() => SaveAll();
        public void DiscardChanges() { _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); Load(); }
        private void Dirty() { if (_dirty) return; _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetClean() { if (!_dirty) return; _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        public BulkLearnsetEditorViewModel() { }
        public BulkLearnsetEditorViewModel(bool _) { }

        public async Task SetupAsync(Window owner)
        {
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.learnsets });
                foreach (var n in GetPokemonNames()) SpeciesNames.Add(n);
                foreach (var n in GetAttackNames()) MoveNames.Add(n);
                _learnsetCount = GetLearnsetFilesCount();
                for (int i = 0; i < _learnsetCount; i++)
                    SpeciesFilter.Add(i < SpeciesNames.Count ? $"{i}: {SpeciesNames[i]}" : $"Species {i}");
                Load();
            }
            catch (Exception ex)
            {
                StatusText = "Error: " + ex.Message;
                await DialogHelper.ShowError($"Failed to set up Bulk Learnset Editor:\n{ex.Message}", "Bulk Learnsets");
            }
        }

        private void Load()
        {
            _all.Clear();
            try
            {
                for (int id = 0; id < _learnsetCount; id++)
                {
                    var ls = new LearnsetData(id);
                    foreach (var (level, move) in ls.list)
                        _all.Add(new BulkLearnsetRow(SpeciesNames, MoveNames, id, level, move, Dirty));
                }
            }
            catch (Exception ex) { AppLogger.Error("Bulk learnset load failed: " + ex.Message); }
            ApplyFilter();
            SetClean();
            StatusText = $"{_all.Count} learnset rows across {_learnsetCount} species.";
        }

        private void ApplyFilter()
        {
            Rows.Clear();
            int sp = _filterIndex - 1; // 0 = "All species"
            foreach (var r in _all)
                if (_filterIndex == 0 || r.SpeciesIndex == sp) Rows.Add(r);
            OnPropertyChanged(nameof(Rows));
        }

        public void AddRow()
        {
            int sp = _filterIndex > 0 ? _filterIndex - 1 : 0;
            var row = new BulkLearnsetRow(SpeciesNames, MoveNames, sp, 1, 0, Dirty);
            _all.Add(row);
            if (_filterIndex == 0 || row.SpeciesIndex == sp) Rows.Add(row);
            Dirty();
        }

        public void RemoveSelected()
        {
            if (_selectedRow < 0 || _selectedRow >= Rows.Count) return;
            var row = Rows[_selectedRow];
            _all.Remove(row);
            Rows.RemoveAt(_selectedRow);
            Dirty();
        }

        public void SaveAll()
        {
            try
            {
                // Group current rows by species and rewrite each species' learnset file.
                var bySpecies = _all.GroupBy(r => r.SpeciesIndex).ToDictionary(g => g.Key, g => g.ToList());
                for (int id = 0; id < _learnsetCount; id++)
                {
                    var ls = new LearnsetData(id);
                    ls.list.Clear();
                    if (bySpecies.TryGetValue(id, out var rows))
                        foreach (var r in rows.OrderBy(x => x.Level).ThenBy(x => x.MoveIndex))
                            if (!ls.list.Contains(((byte)r.Level, (ushort)r.MoveIndex)))
                                ls.list.Add(((byte)r.Level, (ushort)r.MoveIndex));
                    ls.SaveToFileDefaultDir(id, showSuccessMessage: false);
                }
                SetClean();
                StatusText = "Saved all learnsets.";
            }
            catch (Exception ex) { _ = DialogHelper.ShowError($"Save failed:\n{ex.Message}", "Bulk Learnsets"); }
        }
    }
}
