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

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>The EXP each growth curve needs per level.</summary>
    public class GrowthCurveEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private GrowthTable _table;
        private uint[][] _saved;

        private ByteStateUndo _undo;
        private void StartUndo() => _undo = new ByteStateUndo(() => _table.Totals.SelectMany(c => c).SelectMany(BitConverter.GetBytes).ToArray(), ApplyTotals, () => { Raise(nameof(CanUndo)); Raise(nameof(CanRedo)); });
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();

        private void ApplyTotals(byte[] b)
        {
            int k = 0;
            for (int c = 0; c < GrowthTable.Curves; c++)
                for (int l = 0; l < _table.Totals[c].Length; l++, k += 4) _table.Totals[c][l] = BitConverter.ToUInt32(b, k);
            foreach (LevelRow row in Levels) row.Refresh();
            Changed();
        }

        public string[] CurveNames => GrowthTable.CurveNames;
        public ObservableCollection<LevelRow> Levels { get; } = new ObservableCollection<LevelRow>();

        public GrowthCurveEditorViewModel() { }

        public GrowthCurveEditorViewModel(bool load)
        {
            if (!load) return;
            _table = GrowthTable.Load();
            _saved = Enumerable.Range(0, GrowthTable.Curves).Select(_table.Copy).ToArray();
            StartUndo();
            ShowCurve();
        }

        private int _curve;
        public int SelectedCurve
        {
            get => _curve;
            set { if (value < 0 || value >= GrowthTable.Curves || value == _curve) return; _curve = value; Raise(); ShowCurve(); }
        }

        /// <summary>One level of the shown curve. Level 0 isn't shown; the game never uses it.</summary>
        public sealed class LevelRow : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private readonly GrowthCurveEditorViewModel _owner;
            public int Level { get; }
            public bool IsFirst => Level == 1;   // level 1 is always 0 EXP
            public LevelRow(GrowthCurveEditorViewModel owner, int level) { _owner = owner; Level = level; }

            public decimal Total
            {
                get => _owner.TotalAt(Level);
                set => _owner.SetTotal(Level, value);
            }
            public string ToNext => Level >= 100 ? "" : (_owner.TotalAt(Level + 1) - _owner.TotalAt(Level)).ToString("N0");

            internal void Refresh()
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Total)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ToNext)));
            }
        }

        private decimal TotalAt(int level) => _table == null ? 0 : _table.Totals[_curve][level];

        private void SetTotal(int level, decimal value)
        {
            if (_table == null) return;
            if (level == 1) { if (Levels.Count > 0) Levels[0].Refresh(); return; }   // level 1 is always 0
            uint v = (uint)Math.Clamp(value, 0, uint.MaxValue);
            if (_table.Totals[_curve][level] == v) return;
            _table.Totals[_curve][level] = v;
            if (level - 2 >= 0 && level - 2 < Levels.Count) Levels[level - 2].Refresh();
            if (level - 1 < Levels.Count) Levels[level - 1].Refresh();
            Changed();
            _undo?.Record();
        }

        private void ShowCurve()
        {
            Levels.Clear();
            if (_table != null) for (int l = 1; l < GrowthTable.Levels; l++) Levels.Add(new LevelRow(this, l));
            Changed();
        }

        /// <summary>The shown curve's totals, level 1 to 100, for the chart.</summary>
        public IReadOnlyList<uint> CurveForChart => _table == null ? Array.Empty<uint>() : _table.Totals[_curve].Skip(1).ToArray();

        public string Problem => _table?.Problem(_curve) ?? "";
        public bool HasProblem => Problem.Length > 0;
        public string Level100 => _table == null ? "" : $"Level 100: {_table.Totals[_curve][100]:N0} EXP";

        private void Changed()
        {
            foreach (string n in new[] { nameof(CurveForChart), nameof(Problem), nameof(HasProblem), nameof(Level100), nameof(HasUnsavedChanges) })
                Raise(n);
        }

        private bool CurveChanged(int c) => !_table.Totals[c].AsSpan().SequenceEqual(_saved[c]);

        public bool HasUnsavedChanges => _table != null && Enumerable.Range(0, GrowthTable.Curves).Any(CurveChanged);
        public string UnsavedChangesDescription => "Growth curves";

        public void SaveChanges() => _ = SaveChangesAsync();

        public async Task<bool> SaveChangesAsync()
        {
            if (_table == null) return true;
            List<int> changed = Enumerable.Range(0, GrowthTable.Curves).Where(CurveChanged).ToList();
            foreach (int c in changed)
                if (_table.Problem(c) is string p) { await DialogHelper.ShowError(p, "Growth Curves"); return false; }
            try
            {
                foreach (int c in changed) { _table.Save(c); _saved[c] = _table.Copy(c); }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException)
            {
                await DialogHelper.ShowError("The growth curves were not saved:\n" + e.Message, "Growth Curves");
                return false;
            }
            Raise(nameof(HasUnsavedChanges));
            SaveNotice.Saved(UnsavedChangesDescription);
            return true;
        }

        public void DiscardChanges()
        {
            if (_table == null) return;
            for (int c = 0; c < GrowthTable.Curves; c++) _table.Totals[c] = (uint[])_saved[c].Clone();
            foreach (LevelRow row in Levels) row.Refresh();
            StartUndo();
            Changed();
        }
    }
}
