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

namespace DSPRE.Avalonia.ViewModels.Items
{
    /// <summary>How likely each Underground treasure is, for each kind of player.</summary>
    public class UndergroundMiningViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private MiningTable _table;
        private byte[] _saved;

        private ByteStateUndo _undo;
        private void StartUndo() => _undo = new ByteStateUndo(() => _table.ToBytes(), b => { _table.RestoreWeights(b); Changed(true); }, () => { Raise(nameof(CanUndo)); Raise(nameof(CanRedo)); });
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();

        public string[] Columns => MiningTable.Columns;
        public ObservableCollection<TreasureRow> Rows { get; } = new ObservableCollection<TreasureRow>();

        public UndergroundMiningViewModel() { }

        public UndergroundMiningViewModel(bool load)
        {
            if (!load) return;
            _table = MiningTable.Load();
            _saved = _table.ToBytes();
            StartUndo();
            string[] items = GetItemNames();
            var seen = new Dictionary<string, int>();
            foreach (var row in _table.Treasures)
            {
                string name = _table.NameOf(row, items);
                seen[name] = seen.TryGetValue(name, out int n) ? n + 1 : 1;
                // Rotated shapes of one treasure are separate rows.
                Rows.Add(new TreasureRow(this, row, seen[name] > 1 ? $"{name} ({seen[name]})" : name));
            }
        }

        public sealed class TreasureRow : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private readonly UndergroundMiningViewModel _o;
            private readonly MiningTable.Row _row;
            public string Name { get; }
            public TreasureRow(UndergroundMiningViewModel owner, MiningTable.Row row, string name) { _o = owner; _row = row; Name = name; }

            public decimal W0 { get => _row.Weights[0]; set => Set(0, value); }
            public decimal W1 { get => _row.Weights[1]; set => Set(1, value); }
            public decimal W2 { get => _row.Weights[2]; set => Set(2, value); }
            public decimal W3 { get => _row.Weights[3]; set => Set(3, value); }
            public string P0 => _o.Percent(_row, 0);
            public string P1 => _o.Percent(_row, 1);
            public string P2 => _o.Percent(_row, 2);
            public string P3 => _o.Percent(_row, 3);

            private void Set(int c, decimal v)
            {
                ushort w = (ushort)Math.Clamp(v, 0, ushort.MaxValue);
                if (_row.Weights[c] == w) return;
                _row.Weights[c] = w;
                _o.Changed(false, c);
            }

            /// <param name="column">Only that column's share; -1 for every value.</param>
            internal void Refresh(int column = -1)
            {
                var names = column >= 0 ? new[] { $"P{column}" }
                    : new[] { nameof(W0), nameof(W1), nameof(W2), nameof(W3), nameof(P0), nameof(P1), nameof(P2), nameof(P3) };
                foreach (var n in names) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
            }
        }

        internal string Percent(MiningTable.Row row, int column)
        {
            int total = _table.Total(column);
            return total == 0 ? "" : $"{100.0 * row.Weights[column] / total:0.0}%";
        }

        public string Totals => _table == null ? "" :
            "Totals: " + string.Join(" · ", Enumerable.Range(0, 4).Select(c => $"{Columns[c]} {_table.Total(c)}"));

        public string Problem => _table?.Problem() ?? "";
        public bool HasProblem => Problem.Length > 0;

        // One edited weight only moves the shares in its column.
        private void Changed(bool weights = true, int column = -1)
        {
            foreach (var r in Rows) r.Refresh(weights ? -1 : column);
            foreach (var n in new[] { nameof(Totals), nameof(Problem), nameof(HasProblem), nameof(HasUnsavedChanges) }) Raise(n);
            _undo?.Record();
        }

        public bool HasUnsavedChanges => _table != null && !_table.ToBytes().AsSpan().SequenceEqual(_saved);
        public string UnsavedChangesDescription => "Underground mining";

        public void SaveChanges() => _ = SaveChangesAsync();

        public async Task<bool> SaveChangesAsync()
        {
            if (_table == null) return true;
            if (HasProblem) { await DialogHelper.ShowError(Problem, "Underground Mining"); return false; }
            try { _table.Save(); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException)
            {
                await DialogHelper.ShowError("The mining odds were not saved:\n" + e.Message, "Underground Mining");
                return false;
            }
            _saved = _table.ToBytes();
            Changed();
            SaveNotice.Saved(UnsavedChangesDescription);
            return true;
        }

        public void DiscardChanges()
        {
            if (_table == null) return;
            _table.RestoreWeights(_saved);
            StartUndo();
            Changed();
        }
    }
}
