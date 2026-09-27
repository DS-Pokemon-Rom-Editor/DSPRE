using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>The Bug-Catching Contest opponents (HGSS), a tab of the Encounters editor.</summary>
    public class BugContestTrainersViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private BugContestTrainerFile _file;
        private string[] _names = new string[0];

        public ObservableCollection<string> OpponentNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<RowViewModel> Rows { get; } = new ObservableCollection<RowViewModel>();
        public ObservableCollection<string> SpeciesNames { get; } = new ObservableCollection<string>();

        public string[] DayOptions { get; } =
            { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Any day" };
        public string[] DexOptions { get; } = { "Always", "National Dex only" };

        private string _unavailable;
        public bool IsAvailable => _unavailable == null;
        public string Unavailable => _unavailable ?? "";

        private int _selectedOpponent = -1;
        public int SelectedOpponent
        {
            get => _selectedOpponent;
            set { if (Set(ref _selectedOpponent, value)) ShowOpponent(); }
        }

        private string _problems = "";
        public string Problems { get => _problems; private set { if (Set(ref _problems, value)) OnPropertyChanged(nameof(HasProblems)); } }
        public bool HasProblems => _problems.Length > 0;

        private string _warnings = "";
        public string Warnings { get => _warnings; private set { if (Set(ref _warnings, value)) OnPropertyChanged(nameof(HasWarnings)); } }
        public bool HasWarnings => _warnings.Length > 0;

        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => "Bug Contest Opponents";
        public void SaveChanges() => _ = SaveAsync();
        async Task<bool> IEditorWithUnsavedChanges.SaveChangesAsync()
        {
            await SaveAsync();
            return !HasUnsavedChanges;
        }
        public void DiscardChanges() { if (_dirty) Load(); }

        private void SetDirty(bool dirty)
        {
            if (_dirty == dirty) return;
            _dirty = dirty;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        public BugContestTrainersViewModel() { }

        public void Setup()
        {
            SpeciesNames.Clear();
            foreach (var n in GetPokemonNames()) SpeciesNames.Add(n);
            _names = BugContestTrainerFile.OpponentNames();
            OpponentNames.Clear();
            foreach (var n in _names) OpponentNames.Add(n);
            Load();
        }

        private void Load()
        {
            string path = Filesystem.GetBugContestTrainerPath();
            try
            {
                _file = gameFamily != GameFamilies.HGSS ? null : BugContestTrainerFile.Load(path);
                _unavailable = _file == null ? "The Bug-Catching Contest exists only in HeartGold and SoulSilver." : null;
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException)
            {
                _file = null;
                _unavailable = $"The contest opponents could not be read: {e.Message}";
            }
            OnPropertyChanged(nameof(IsAvailable));
            OnPropertyChanged(nameof(Unavailable));

            SetDirty(false);
            if (_selectedOpponent < 0 && _file != null) _selectedOpponent = 0;
            OnPropertyChanged(nameof(SelectedOpponent));
            ShowOpponent();
        }

        private void ShowOpponent()
        {
            Rows.Clear();
            if (_file != null && _selectedOpponent >= 0 && _selectedOpponent < BugContestTrainerFile.Opponents)
                for (int r = 0; r < BugContestTrainerFile.RowsPerOpponent; r++)
                    Rows.Add(new RowViewModel(r + 1, _file.Rows[_selectedOpponent, r], Edited));
            Recheck();
        }

        private void Edited()
        {
            SetDirty(true);
            Recheck();
        }

        private string NameOf(int o) => o < _names.Length ? _names[o] : $"Opponent {o + 1}";

        private void Recheck()
        {
            Problems = _file == null ? "" : string.Join("\n", _file.Problems(NameOf));
            Warnings = _file == null ? "" : string.Join("\n", _file.Warnings(NameOf));
        }

        public async Task SaveAsync()
        {
            if (_file == null || !_dirty) return;
            if (HasProblems)
            {
                await DialogHelper.ShowError($"The contest opponents were not saved:\n{Problems}", "Bug Contest Opponents");
                return;
            }
            try
            {
                _file.Save(Filesystem.GetBugContestTrainerPath());
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                await DialogHelper.ShowError($"The contest opponents were not saved:\n{e.Message}", "Bug Contest Opponents");
                return;
            }
            SetDirty(false);
            SaveNotice.Saved(UnsavedChangesDescription);
        }

        public void Locate()
        {
            string path = Filesystem.GetBugContestTrainerPath();
            if (File.Exists(path)) SystemShell.RevealInFileManager(path);
        }

        /// <summary>One of an opponent's eight rows. Values the pickers can't show are kept until changed.</summary>
        public class RowViewModel : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

            private readonly BugContestTrainerFile.Row _row;
            private readonly Action _edited;

            public RowViewModel(int number, BugContestTrainerFile.Row row, Action edited)
            {
                Number = number;
                _row = row;
                _edited = edited;
            }

            public int Number { get; }

            public int SpeciesIndex
            {
                get => _row.Species;
                set
                {
                    if (value < 0) { global::Avalonia.Threading.Dispatcher.UIThread.Post(() => Raise(nameof(SpeciesIndex))); return; }   // a cleared box goes back to the stored species
                    if (value == _row.Species) return;
                    _row.Species = (ushort)value; Changed();
                }
            }

            public int DayIndex
            {
                get => _row.Day >= BugContestTrainerFile.AnyDay ? BugContestTrainerFile.AnyDay : _row.Day;
                set
                {
                    if (value < 0 || value > BugContestTrainerFile.AnyDay || value == DayIndex) return;
                    _row.Day = (byte)value;
                    Changed();
                }
            }

            public int DexIndex
            {
                get => _row.NationalDex == 0 ? 0 : 1;
                set
                {
                    if (value < 0 || value > 1 || value == DexIndex) return;
                    _row.NationalDex = (byte)value;
                    Changed();
                }
            }

            public decimal Score
            {
                get => _row.Score;
                set { if (value == _row.Score) return; _row.Score = (ushort)Math.Clamp(value, 0, ushort.MaxValue); Changed(); }
            }

            public decimal Variation
            {
                get => _row.Variation;
                set { if (value == _row.Variation) return; _row.Variation = (ushort)Math.Clamp(value, 0, ushort.MaxValue); Changed(); }
            }

            public string Range => _row.Variation == 0 ? "" : $"{_row.LowestScore} to {_row.HighestScore}";

            private void Changed()
            {
                Raise(nameof(SpeciesIndex)); Raise(nameof(DayIndex)); Raise(nameof(DexIndex));
                Raise(nameof(Score)); Raise(nameof(Variation)); Raise(nameof(Range));
                _edited();
            }
        }
    }
}
