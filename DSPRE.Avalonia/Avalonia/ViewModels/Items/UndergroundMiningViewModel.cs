using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Items
{
    /// <summary>The Underground walls: how likely each treasure is, every row's art and shape, and the wall settings in code.</summary>
    public class UndergroundMiningViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private MiningTable _table;
        private byte[] _saved;
        private int _memberCount;

        private ByteStateUndo _undo;
        private void StartUndo() => _undo = new ByteStateUndo(() => _table.State(), b => { _table.Restore(b); Changed(art: true); }, () => { Raise(nameof(CanUndo)); Raise(nameof(CanRedo)); });
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
            MiningArt.Unpack();
            _memberCount = MiningArt.MemberCount();
            _table = MiningTable.Load();
            _saved = _table.State();
            StartUndo();
            foreach (MiningTable.Row row in _table.Rows) Rows.Add(new TreasureRow(this, row, row.IsRock && row == _table.Rows.First(r => r.IsRock)));
            RefreshNames();
            SelectedRow = Rows.FirstOrDefault();
        }

        public void RefreshNames()
        {
            string[] items = GetItemNames();
            Dictionary<string, int> seen = new Dictionary<string, int>();
            foreach (TreasureRow r in Rows)
            {
                string name = _table.NameOf(r.Row, items);
                seen[name] = seen.TryGetValue(name, out int n) ? n + 1 : 1;
                // Rotated shapes of one treasure are separate rows.
                r.Name = seen[name] > 1 ? $"{name} ({seen[name]})" : name;
            }
            foreach (string n in new[] { nameof(OrbNameA), nameof(OrbNameB), nameof(ShapeNote) }) Raise(n);
        }

        public sealed class TreasureRow : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            internal void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
            private readonly UndergroundMiningViewModel _o;
            private readonly MiningTable.Row _row;
            private string _name;
            public string Name
            {
                get => _name;
                set { if (_name == value) return; _name = value; Raise(nameof(Name)); }
            }
            internal MiningTable.Row Row => _row;
            public bool IsRock => _row.IsRock;
            public bool IsTreasure => !_row.IsRock;
            /// <summary>The first rock row carries the heading for the rocks.</summary>
            public bool IsFirstRock { get; }

            private Bitmap _art;
            public Bitmap Art => _art ??= MiningArt.Render(_row);
            internal void RedrawArt() { _art = null; Raise(nameof(Art)); }

            public TreasureRow(UndergroundMiningViewModel owner, MiningTable.Row row, bool firstRock) { _o = owner; _row = row; IsFirstRock = firstRock; }

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
                _o.Changed(column: c);
            }

            /// <param name="column">Only that column's share; -1 for every value.</param>
            internal void Refresh(int column = -1)
            {
                string[] names = column >= 0 ? new[] { $"P{column}" }
                    : new[] { nameof(W0), nameof(W1), nameof(W2), nameof(W3), nameof(P0), nameof(P1), nameof(P2), nameof(P3) };
                foreach (string n in names) Raise(n);
            }
        }

        internal string Percent(MiningTable.Row row, int column)
        {
            if (row.IsRock) return "";
            int total = _table.Total(column);
            return total == 0 ? "" : $"{100.0 * row.Weights[column] / total:0.0}%";
        }

        public string Totals => _table == null ? "" :
            "Totals: " + string.Join(", ", Enumerable.Range(0, 4).Select(c => $"{Columns[c]} {_table.Total(c)}"));

        public string Problem => _table?.Problem() ?? "";
        public bool HasProblem => Problem.Length > 0;

        // ── Selected row ─────────────────────────────────────────────────────────────

        private TreasureRow _selectedRow;
        public TreasureRow SelectedRow
        {
            get => _selectedRow;
            set
            {
                if (_selectedRow == value) return;
                _selectedRow = value;
                BuildShapeCells();
                foreach (string n in new[] { nameof(SelectedRow), nameof(HasSelection), nameof(SelectedArt), nameof(SelectedSprite), nameof(SelectedPalette),
                                             nameof(SelectedSize), nameof(ShapeNote), nameof(HasShape), nameof(ShapeColumns), nameof(ShapeWidth),
                                             nameof(ShapeHeight), nameof(ShapeArt), nameof(ArtProblem) }) Raise(n);
            }
        }

        public bool HasSelection => _selectedRow != null;
        public Bitmap SelectedArt => _selectedRow?.Art;
        public string SelectedSize => _selectedRow == null ? "" : $"{_selectedRow.Row.Width / 2} by {_selectedRow.Row.Height / 2} cells";
        public int MaxMember => Math.Max(0, _memberCount - 1);

        public decimal SelectedSprite
        {
            get => _selectedRow?.Row.Sprite ?? 0;
            set
            {
                if (_selectedRow == null || (ushort)value == _selectedRow.Row.Sprite) return;
                _selectedRow.Row.Sprite = (ushort)value;
                Changed(art: true);
            }
        }

        public decimal SelectedPalette
        {
            get => _selectedRow?.Row.Palette ?? 0;
            set
            {
                if (_selectedRow == null || (ushort)value == _selectedRow.Row.Palette) return;
                _selectedRow.Row.Palette = (ushort)value;
                Changed(art: true);
            }
        }

        /// <summary>Why the chosen members can't draw this row, or empty.</summary>
        public string ArtProblem
        {
            get
            {
                if (_selectedRow == null) return "";
                if (MiningArt.KindOf(_selectedRow.Row.Sprite) != "tiles") return $"Member {_selectedRow.Row.Sprite} is not a tile set.";
                if (MiningArt.KindOf(_selectedRow.Row.Palette) != "palette") return $"Member {_selectedRow.Row.Palette} is not a palette.";
                return "";
            }
        }

        public sealed class ShapeCell : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private readonly UndergroundMiningViewModel _o;
            private readonly MiningTable.Row _row;
            private readonly int _at;
            public ShapeCell(UndergroundMiningViewModel owner, MiningTable.Row row, int at) { _o = owner; _row = row; _at = at; }
            public double Size => _o.CellSize;
            public bool Solid
            {
                get => _row.Shape[_at] == (byte)'x';
                set
                {
                    if (value == Solid) return;
                    _row.Shape[_at] = value ? (byte)'x' : (byte)'o';
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Solid)));
                    _o.Changed(art: true);
                }
            }
            internal void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Solid)));
        }

        public ObservableCollection<ShapeCell> ShapeCells { get; } = new ObservableCollection<ShapeCell>();
        public bool HasShape => _selectedRow?.Row.Shape != null;
        public int ShapeColumns => Math.Max(1, _selectedRow?.Row.ShapeColumns ?? 1);
        private int ShapeRowCount => Math.Max(1, (_selectedRow?.Row.Height ?? 2) / 2);

        /// <summary>One cell is 2 by 2 tiles; drawn at 3x, or smaller when the grid would not fit the panel.</summary>
        public double CellSize => Math.Min(48, 240 / ShapeColumns);
        public double ShapeWidth => CellSize * ShapeColumns;
        public double ShapeHeight => CellSize * ShapeRowCount;

        /// <summary>The whole picture under the shape grid, so empty cells still show what they cut away.</summary>
        public Bitmap ShapeArt => _selectedRow == null ? null : MiningArt.Render(_selectedRow.Row, masked: false);

        private void BuildShapeCells()
        {
            ShapeCells.Clear();
            if (_selectedRow?.Row.Shape is byte[] shape)
                for (int i = 0; i < shape.Length; i++) ShapeCells.Add(new ShapeCell(this, _selectedRow.Row, i));
        }

        public string ShapeNote
        {
            get
            {
                if (_selectedRow == null) return "";
                if (_selectedRow.Row.Shape == null) return "Full rectangle";
                List<string> others = _table.SharingShape(_selectedRow.Row).Select(r => Rows[r.Index].Name).ToList();
                return others.Count == 0 ? "" : $"Shared with {string.Join(", ", others)}";
            }
        }

        // ── Wall settings ────────────────────────────────────────────────────────────

        public bool HasCode => _table?.Code != null;
        public bool HasOrbBonus => _table?.Code?.HasOrbBonus == true;
        private MiningCode Code => _table?.Code;

        public decimal TreasureMin { get => Code?.TreasureMin ?? 0; set => SetCode(c => c.TreasureMin = (int)value); }
        public decimal TreasureMax { get => Code?.TreasureMax ?? 0; set => SetCode(c => c.TreasureMax = (int)value); }
        public decimal FirstDig { get => Code?.FirstDig ?? 0; set => SetCode(c => c.FirstDig = (int)value); }
        public decimal Tries { get => Code?.Tries ?? 0; set => SetCode(c => c.Tries = (int)value); }
        public decimal OrbBonusA { get => Code?.OrbBonusA ?? 0; set => SetCode(c => c.OrbBonusA = (int)value); }
        public decimal OrbBonusB { get => Code?.OrbBonusB ?? 0; set => SetCode(c => c.OrbBonusB = (int)value); }
        public int MaxTreasures => MiningCode.MaxTreasures;

        private string RowName(int miningId) => Rows.FirstOrDefault(r => r.Row.MiningId == miningId)?.Name ?? $"Row id {miningId}";
        public string OrbNameA => Code == null ? "" : RowName(Code.OrbRowA);
        public string OrbNameB => Code == null ? "" : RowName(Code.OrbRowB);

        private void SetCode(Action<MiningCode> set)
        {
            if (Code == null) return;
            byte[] before = _table.State();
            set(Code);
            if (_table.State().AsSpan().SequenceEqual(before)) return;
            Changed();
        }

        // ── Changes, undo and saving ─────────────────────────────────────────────────

        private void Changed(bool art = false, int column = -1)
        {
            foreach (TreasureRow r in Rows)
            {
                r.Refresh(column);
                if (art) r.RedrawArt();
            }
            foreach (ShapeCell c in ShapeCells) c.Refresh();
            foreach (string n in new[] { nameof(Totals), nameof(Problem), nameof(HasProblem), nameof(HasUnsavedChanges), nameof(SelectedArt),
                                         nameof(SelectedSprite), nameof(SelectedPalette), nameof(ShapeArt), nameof(ArtProblem), nameof(TreasureMin), nameof(TreasureMax),
                                         nameof(FirstDig), nameof(Tries), nameof(OrbBonusA), nameof(OrbBonusB) }) Raise(n);
            _undo?.Record();
        }

        public bool HasUnsavedChanges => _table != null && !_table.State().AsSpan().SequenceEqual(_saved);
        public string UnsavedChangesDescription => "Underground mining";

        public void SaveChanges() => DialogHelper.Watch(SaveChangesAsync());

        public async Task<bool> SaveChangesAsync()
        {
            if (_table == null) return true;
            if (HasProblem) { await DialogHelper.ShowErrorAsync(Problem, "Underground mining error"); return false; }
            try { _table.Save(); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException)
            {
                await DialogHelper.ShowErrorAsync("The Underground mining changes were not saved:\n" + e.Message, "Underground mining error");
                return false;
            }
            _saved = _table.State();
            Changed();
            SaveNotice.Saved(UnsavedChangesDescription);
            return true;
        }

        public void DiscardChanges()
        {
            if (_table == null) return;
            _table.Restore(_saved);
            StartUndo();
            Changed(art: true);
        }
    }
}
