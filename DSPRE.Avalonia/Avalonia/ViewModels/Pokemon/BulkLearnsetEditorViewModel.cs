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
using DSPRE.Avalonia.Views.Shell;
using DSPRE.Csv;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Pokemon
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
    public class BulkLearnsetEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo
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
        private void Dirty()
        {
            _undo?.Record();
            bool dirty = _undo?.IsDirty ?? true;
            if (_dirty == dirty) return;
            _dirty = dirty;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        // ── Undo / redo: every row ──
        private DSPRE.Avalonia.ByteStateUndo _undo;
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();
        private void RaiseUndo() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private byte[] TakeState() => DSPRE.Avalonia.UndoJson.Take(_all.Select(r => new[] { r.SpeciesIndex, r.Level, r.MoveIndex }).ToArray());

        private void ApplyState(byte[] state)
        {
            int[][] rows = DSPRE.Avalonia.UndoJson.Read<int[][]>(state);
            int keep = _selectedRow;
            _all.Clear();
            foreach (int[] r in rows) _all.Add(new BulkLearnsetRow(SpeciesNames, MoveNames, r[0], r[1], r[2], Dirty));
            ApplyFilter();
            if (keep >= 0 && keep < Rows.Count) { _selectedRow = -1; SelectedRow = keep; }
            bool dirty = _undo.IsDirty;
            if (_dirty != dirty) { _dirty = dirty; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        }
        private void SetClean() { if (!_dirty) return; _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        public BulkLearnsetEditorViewModel() { }
        public BulkLearnsetEditorViewModel(bool _) { }

        public async Task SetupAsync(Window owner)
        {
            // Opened straight from the Pokémon editor, not the launcher.
            if (AvaloniaEditorLauncher.Refused("BulkLearnsetEditorView")) { owner?.Close(); return; }
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.learnsets });
                foreach (string n in GetPokemonNames()) SpeciesNames.Add(n);
                foreach (string n in GetAttackNames()) MoveNames.Add(n);
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

        // hg-engine: each species' own LevelMoves in learnsets.json as loaded, to save only what changed.
        private Dictionary<int, List<(int level, int move)>> _sourceLists;
        private string _loadError;

        private void Load()
        {
            _all.Clear();
            _sourceLists = null;
            _loadError = null;
            try
            {
                if (HgEngine.HgEngineProject.IsActive)
                {
                    if (!HgEngine.HgEngineLearnsets.TryGetAllLevelMoves(out _sourceLists, out string error)) throw new InvalidOperationException(error);
                    foreach ((int id, List<(int level, int move)> list) in _sourceLists.OrderBy(kv => kv.Key))
                        foreach ((int level, int move) in list)
                            _all.Add(new BulkLearnsetRow(SpeciesNames, MoveNames, id, level, move, Dirty));
                }
                else for (int id = 0; id < _learnsetCount; id++)
                {
                    LearnsetData ls = new LearnsetData(id);
                    foreach ((byte level, ushort move) in ls.list)
                        _all.Add(new BulkLearnsetRow(SpeciesNames, MoveNames, id, level, move, Dirty));
                }
            }
            catch (Exception ex) { _loadError = ex.Message; AppLogger.Error("Bulk learnset load failed: " + ex.Message); }
            ApplyFilter();
            SetClean();
            _undo = new DSPRE.Avalonia.ByteStateUndo(TakeState, ApplyState, RaiseUndo);
            RaiseUndo();
            StatusText = $"{_all.Count} learnset rows across {_learnsetCount} species.";
        }

        private void ApplyFilter()
        {
            Rows.Clear();
            int sp = _filterIndex - 1; // 0 = "All species"
            foreach (BulkLearnsetRow r in _all)
                if (_filterIndex == 0 || r.SpeciesIndex == sp) Rows.Add(r);
            OnPropertyChanged(nameof(Rows));
        }

        public void AddRow()
        {
            int sp = _filterIndex > 0 ? _filterIndex - 1 : 0;
            BulkLearnsetRow row = new BulkLearnsetRow(SpeciesNames, MoveNames, sp, 1, 0, Dirty);
            _all.Add(row);
            if (_filterIndex == 0 || row.SpeciesIndex == sp) Rows.Add(row);
            Dirty();
        }

        public void RemoveSelected()
        {
            if (_selectedRow < 0 || _selectedRow >= Rows.Count) return;
            BulkLearnsetRow row = Rows[_selectedRow];
            _all.Remove(row);
            Rows.RemoveAt(_selectedRow);
            Dirty();
        }

        /// <summary>Writes every Pokémon's rows as this editor holds them, unsaved edits included.</summary>
        public async Task ExportAsync(Window owner)
        {
            string path = await DialogHelper.SaveFile(owner, "Export learnsets CSV", new[] { DialogHelper.CsvFilter, DialogHelper.AllFilter }, "learnsets.csv");
            if (path == null) return;
            try
            {
                using System.IO.StreamWriter writer = new System.IO.StreamWriter(path);
                LearnsetCsv.Write(writer, SpeciesNames.ToArray(), MoveNames.ToArray(), Lists().Select(kv => (kv.Key, (IReadOnlyList<LearnsetCsv.Move>)kv.Value)), single: false);
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException)
            {
                await DialogHelper.ShowError($"Export failed:\n{ex.Message}", "Bulk Learnsets", owner);
            }
        }

        /// <summary>Replaces the learnsets of the Pokémon in the file. Changes stay unsaved until Save all.</summary>
        public async Task ImportAsync(Window owner)
        {
            if (_loadError != null)
            {
                await DialogHelper.ShowError("The learnsets couldn't be read:\n" + _loadError, "Bulk Learnsets", owner);
                return;
            }
            Dictionary<int, List<LearnsetCsv.Move>> now = Lists();
            LearnsetCsv importer = new LearnsetCsv(SpeciesNames.ToArray(), MoveNames.ToArray(), _learnsetCount, -1,
                id => (now.TryGetValue(id, out List<LearnsetCsv.Move> list) ? list : new List<LearnsetCsv.Move>(), null));
            CsvImportSession session = await CsvImportReviewView.ReviewAsync(owner, importer);
            if (session == null) return;

            Dictionary<int, List<LearnsetCsv.Move>> imported = importer.Result(session.Accepted);
            // A Pokémon's new rows go where its old ones started, so the grid keeps its order.
            List<BulkLearnsetRow> rows = new List<BulkLearnsetRow>();
            HashSet<int> placed = new HashSet<int>();
            foreach (BulkLearnsetRow r in _all)
            {
                if (!imported.TryGetValue(r.SpeciesIndex, out List<LearnsetCsv.Move> list)) { rows.Add(r); continue; }
                if (placed.Add(r.SpeciesIndex)) rows.AddRange(list.Select(m => new BulkLearnsetRow(SpeciesNames, MoveNames, r.SpeciesIndex, m.Level, m.MoveId, Dirty)));
            }
            foreach ((int id, List<LearnsetCsv.Move> list) in imported.OrderBy(kv => kv.Key))
                if (placed.Add(id)) rows.AddRange(list.Select(m => new BulkLearnsetRow(SpeciesNames, MoveNames, id, m.Level, m.MoveId, Dirty)));
            _all.Clear();
            _all.AddRange(rows);
            ApplyFilter();
            Dirty();
            StatusText = $"Learnsets imported for {imported.Count} Pokémon. Press Save all to write them to disk.";
        }

        private Dictionary<int, List<LearnsetCsv.Move>> Lists() => _all.GroupBy(r => r.SpeciesIndex).OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.Select(r => new LearnsetCsv.Move(r.Level, r.MoveIndex)).Distinct().ToList());

        public void SaveAll()
        {
            if (_loadError != null)
            {
                _ = DialogHelper.ShowError("The learnsets couldn't be read, so saving would replace them:\n" + _loadError, "Bulk Learnsets");
                return;
            }
            if (_sourceLists != null) { _ = SaveSourceAsync(); return; }
            try
            {
                // Group current rows by species and rewrite each species' learnset file.
                Dictionary<int, List<BulkLearnsetRow>> bySpecies = _all.GroupBy(r => r.SpeciesIndex).ToDictionary(g => g.Key, g => g.ToList());
                for (int id = 0; id < _learnsetCount; id++)
                {
                    LearnsetData ls = new LearnsetData(id);
                    byte[] was = ls.ToByteArray();
                    ls.list.Clear();
                    // Rows keep their order: the game does not need levels sorted (retail Pt species 354 isn't),
                    // and its default moveset follows the file order.
                    if (bySpecies.TryGetValue(id, out List<BulkLearnsetRow> rows))
                        foreach (BulkLearnsetRow r in rows)
                            if (!ls.list.Contains(((byte)r.Level, (ushort)r.MoveIndex)))
                                ls.list.Add(((byte)r.Level, (ushort)r.MoveIndex));
                    if (ls.ToByteArray().AsSpan().SequenceEqual(was)) continue;
                    ls.SaveToFileDefaultDir(id, showSuccessMessage: false);
                }
                _undo?.MarkSaved();
                SetClean();
                SaveNotice.Saved(UnsavedChangesDescription);
                StatusText = "Saved all learnsets.";
            }
            catch (Exception ex) { _ = DialogHelper.ShowError($"Save failed:\n{ex.Message}", "Bulk Learnsets"); }
        }

        // Only species whose rows changed are written into learnsets.json.
        private async Task SaveSourceAsync()
        {
            Dictionary<int, List<(int Level, int MoveIndex)>> now = _all.GroupBy(r => r.SpeciesIndex).ToDictionary(g => g.Key,
                g => g.Select(r => (r.Level, r.MoveIndex)).Distinct().ToList());
            Dictionary<int, IReadOnlyList<(int level, int move)>> changes = new Dictionary<int, IReadOnlyList<(int level, int move)>>();
            foreach ((int id, List<(int Level, int MoveIndex)> rows) in now)
                if (!_sourceLists.TryGetValue(id, out List<(int level, int move)> was) || !was.SequenceEqual(rows)) changes[id] = rows;
            foreach (int id in _sourceLists.Keys)
                if (!now.ContainsKey(id)) changes[id] = new List<(int, int)>();

            (bool saved, string error) = await HgEngineSave.RunAsync(() =>
                changes.Count == 0 || HgEngine.HgEngineLearnsets.TrySaveLevelMoves(changes, out string e) ? null : e);
            if (!saved) { if (error != null) await DialogHelper.ShowError("The learnsets were not saved:\n" + error, "Bulk Learnsets"); return; }
            foreach ((int id, IReadOnlyList<(int level, int move)> rows) in changes)
                if (rows.Count > 0) _sourceLists[id] = rows.ToList(); else _sourceLists.Remove(id);
            _undo?.MarkSaved();
            SetClean();
            SaveNotice.Saved(UnsavedChangesDescription);
            StatusText = $"Saved {changes.Count} species into learnsets.json.";
        }
    }
}
