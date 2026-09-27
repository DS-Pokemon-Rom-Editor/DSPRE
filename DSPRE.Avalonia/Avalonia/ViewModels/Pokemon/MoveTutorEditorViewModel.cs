using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>The move tutors' moves, prices and places, and which Pokémon each can teach.</summary>
    public class MoveTutorEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private MoveTutorData _data;
        private byte[] _savedPool, _savedMasks;

        private ByteStateUndo _undo;
        private void StartUndo() => _undo = new ByteStateUndo(() => _data.PoolBytes().Concat(_data.MaskBytes()).ToArray(), ApplyTutors, () => { Raise(nameof(CanUndo)); Raise(nameof(CanRedo)); });
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();

        private void ApplyTutors(byte[] b)
        {
            int pool = _data.PoolBytes().Length;
            _data.Restore(b[..pool], b[pool..]);
            _tutorNames = null;
            foreach (var row in Pool) row.Refresh();
            ShowSpecies(); ShowTutor();
            Changed(true);
        }

        public string[] MoveNames { get; } = Array.Empty<string>();
        public string[] Places => _data?.Places ?? Array.Empty<string>();
        public bool Platinum => _data?.Platinum ?? false;
        public bool HeartGold => _data != null && !_data.Platinum;

        public ObservableCollection<PoolRow> Pool { get; } = new ObservableCollection<PoolRow>();

        /// <summary>Every Pokémon with a tutor row: species 1-493, then the forms with their own personal file.</summary>
        public List<(string Name, int Row)> Species { get; } = new List<(string, int)>();
        // Rows of Species the filter lets through; built once per filter so the ListBox keeps its selection.
        private List<int> _shown;
        private List<string> _shownNames;
        private List<int> Shown => _shown ??= Enumerable.Range(0, Species.Count)
            .Where(i => _speciesFilter.Length == 0 || Species[i].Name.Contains(_speciesFilter, StringComparison.OrdinalIgnoreCase)).ToList();
        public List<string> SpeciesNames => _shownNames ??= Shown.Select(i => Species[i].Name).ToList();

        private string _speciesFilter = "";
        public string SpeciesFilter
        {
            get => _speciesFilter;
            set
            {
                value ??= "";
                if (value == _speciesFilter) return;
                _speciesFilter = value;
                _shown = null; _shownNames = null;
                Raise(); Raise(nameof(SpeciesNames)); Raise(nameof(SelectedSpecies));
            }
        }
        public ObservableCollection<CheckRow> MovesOfSpecies { get; } = new ObservableCollection<CheckRow>();
        public ObservableCollection<CheckRow> SpeciesOfMove { get; } = new ObservableCollection<CheckRow>();
        // Built once per change: a new list on every read makes the ListBox drop its selection.
        private List<string> _tutorNames;
        public List<string> TutorNames => _tutorNames ??= _data?.Pool.Select((t, i) => $"{i + 1}. {Name(t.Move)}").ToList() ?? new List<string>();

        public MoveTutorEditorViewModel() { }

        public MoveTutorEditorViewModel(bool load)
        {
            if (!load) return;
            _data = MoveTutorData.Load();
            _savedPool = _data.PoolBytes();
            _savedMasks = _data.MaskBytes();
            StartUndo();
            MoveNames = GetAttackNames();
            for (int i = 0; i < _data.Pool.Count; i++) Pool.Add(new PoolRow(this, i));

            string[] names = GetPokemonNamesWithForms(508);
            for (int id = 1; id < names.Length; id++)
                if (MoveTutorData.RowOf(id) is int row && row >= 0) Species.Add((names[id], row));
            _species = 0; _tutor = 0;
            ShowSpecies(); ShowTutor();
        }

        private string Name(int move) => move < MoveNames.Length ? MoveNames[move] : $"Move {move}";

        public sealed class PoolRow : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private readonly MoveTutorEditorViewModel _o;
            private readonly int _i;
            public PoolRow(MoveTutorEditorViewModel owner, int index) { _o = owner; _i = index; }
            private MoveTutorData.Tutor T => _o._data.Pool[_i];

            public int Number => _i + 1;
            public int Move
            {
                get => T.Move;
                set
                {
                    // "None" isn't a tutor move; put the box back to the stored one.
                    if (value <= 0) { global::Avalonia.Threading.Dispatcher.UIThread.Post(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Move)))); return; }
                    if (value != T.Move) { T.Move = (ushort)value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Move))); _o.Changed(true); }
                }
            }
            public int Where { get => T.Where; set { if (value >= 0 && value != T.Where) { T.Where = value; _o.Changed(false); } } }
            public decimal Cost0 { get => T.Costs[0]; set => SetCost(0, value); }
            public decimal Cost1 { get => T.Costs.Length > 1 ? T.Costs[1] : 0; set => SetCost(1, value); }
            public decimal Cost2 { get => T.Costs.Length > 2 ? T.Costs[2] : 0; set => SetCost(2, value); }
            public decimal Cost3 { get => T.Costs.Length > 3 ? T.Costs[3] : 0; set => SetCost(3, value); }

            private void SetCost(int k, decimal v)
            {
                if (k >= T.Costs.Length) return;
                byte b = (byte)Math.Clamp(v, 0, 255);
                if (T.Costs[k] == b) return;
                T.Costs[k] = b;
                _o.Changed(false);
            }

            internal void Refresh()
            {
                foreach (var n in new[] { nameof(Move), nameof(Where), nameof(Cost0), nameof(Cost1), nameof(Cost2), nameof(Cost3) })
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
            }
        }

        public sealed class CheckRow : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private readonly Func<bool> _get;
            private readonly Action<bool> _set;
            public string Name { get; }
            public CheckRow(string name, Func<bool> get, Action<bool> set) { Name = name; _get = get; _set = set; }
            public bool IsChecked { get => _get(); set { if (value != _get()) { _set(value); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked))); } } }
            internal void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
        }

        private int _species = -1;
        /// <summary>The selected row of the filtered list; <c>_species</c> is the row of Species it shows.</summary>
        public int SelectedSpecies
        {
            get => Shown.IndexOf(_species);
            set
            {
                if (value < 0 || value >= Shown.Count || Shown[value] == _species) return;
                _species = Shown[value];
                Raise(); ShowSpecies();
            }
        }

        private int _tutor = -1;
        public int SelectedTutor { get => _tutor; set { if (value >= 0 && _data != null && value < _data.Pool.Count && value != _tutor) { _tutor = value; Raise(); ShowTutor(); } } }

        private void ShowSpecies()
        {
            MovesOfSpecies.Clear();
            if (_data == null || _species < 0) return;
            int row = Species[_species].Row;
            for (int t = 0; t < _data.Pool.Count; t++)
            {
                int tutor = t;
                MovesOfSpecies.Add(new CheckRow($"{t + 1}. {Name(_data.Pool[t].Move)}", () => _data.Learns(row, tutor), v => { _data.SetLearns(row, tutor, v); Changed(false); }));
            }
        }

        private void ShowTutor()
        {
            SpeciesOfMove.Clear();
            if (_data == null || _tutor < 0) return;
            int tutor = _tutor;
            foreach (var (name, row) in Species)
            {
                int r = row;
                SpeciesOfMove.Add(new CheckRow(name, () => _data.Learns(r, tutor), v => { _data.SetLearns(r, tutor, v); Changed(false); }));
            }
        }

        public string Problem => _data?.Problem(MoveNames.Length) ?? "";
        public bool HasProblem => Problem.Length > 0;

        private void Changed(bool movesRenamed)
        {
            // A new names list clears the ListBox selection, so the selection is raised again after it.
            if (movesRenamed) { _tutorNames = null; Raise(nameof(TutorNames)); Raise(nameof(SelectedTutor)); ShowSpecies(); }
            Raise(nameof(Problem)); Raise(nameof(HasProblem)); Raise(nameof(HasUnsavedChanges));
            _undo?.Record();
        }

        public bool HasUnsavedChanges => _data != null &&
            (!_data.PoolBytes().AsSpan().SequenceEqual(_savedPool) || !_data.MaskBytes().AsSpan().SequenceEqual(_savedMasks));
        public string UnsavedChangesDescription => "Move tutors";

        public void SaveChanges() => _ = SaveChangesAsync();

        public async Task<bool> SaveChangesAsync()
        {
            if (_data == null) return true;
            if (HasProblem) { await DialogHelper.ShowError(Problem, "Move Tutors"); return false; }
            try { _data.Save(MoveNames.Length); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException)
            {
                await DialogHelper.ShowError("The move tutors were not saved:\n" + e.Message, "Move Tutors");
                return false;
            }
            _savedPool = _data.PoolBytes();
            _savedMasks = _data.MaskBytes();
            Changed(false);
            SaveNotice.Saved(UnsavedChangesDescription);
            return true;
        }

        public void DiscardChanges()
        {
            if (_data == null) return;
            try { _data = MoveTutorData.Load(); }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is InvalidOperationException)
            {
                _ = DialogHelper.ShowError("The saved tutors couldn't be read back:\n" + e.Message, "Move Tutors");
                return;
            }
            _tutorNames = null;
            _savedPool = _data.PoolBytes();
            _savedMasks = _data.MaskBytes();
            StartUndo();
            foreach (var row in Pool) row.Refresh();
            ShowSpecies(); ShowTutor();
            Changed(true);
        }
    }
}
