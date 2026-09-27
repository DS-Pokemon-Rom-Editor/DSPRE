using System;
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
    /// <summary>How well each type hits each other type.</summary>
    public class TypeChartEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private TypeChart _chart;

        private ByteStateUndo _undo;
        private void StartUndo() => _undo = new ByteStateUndo(() => _chart.Matchups.SelectMany(m => new[] { m.Attacker, m.Defender, m.Tenths, (byte)(m.ForesightRemovable ? 1 : 0) }).ToArray(), ApplyMatchups, () => { Raise(nameof(CanUndo)); Raise(nameof(CanRedo)); });
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();

        private void ApplyMatchups(byte[] b)
        {
            _chart.Matchups.Clear();
            for (int k = 0; k + 3 < b.Length; k += 4)
                _chart.Matchups.Add(new TypeChart.Matchup { Attacker = b[k], Defender = b[k + 1], Tenths = b[k + 2], ForesightRemovable = b[k + 3] != 0 });
            Changed();
        }


        public string[] TypeNames { get; } = Array.Empty<string>();
        public int TypeCount { get; }

        /// <summary>Raised when any cell's value changes, so the grid can repaint.</summary>
        public event EventHandler CellsChanged;

        public TypeChartEditorViewModel() { }

        public TypeChartEditorViewModel(bool load)
        {
            if (!load) return;
            _chart = TypeChart.Load();
            _savedKey = Key();
            StartUndo();
            TypeNames = GetTypeNames();
            int highest = _chart.Matchups.Count == 0 ? 0 : _chart.Matchups.Max(m => Math.Max(m.Attacker, m.Defender)) + 1;
            TypeCount = Math.Min(32, Math.Max(TypeNames.Length, highest));
            _attacker = 10; _defender = 11;
        }

        public string NameOf(int type) => type < TypeNames.Length ? TypeNames[type] : $"Type {type}";

        public int TenthsAt(int attacker, int defender) => _chart?.Find(attacker, defender)?.Tenths ?? TypeChart.Neutral;
        public bool ForesightAt(int attacker, int defender) => _chart?.Find(attacker, defender)?.ForesightRemovable ?? false;

        private int _attacker = -1, _defender = -1;
        public int SelectedAttacker => _attacker;
        public int SelectedDefender => _defender;

        /// <summary>Selects a cell; selecting the one already selected steps it to the next usual value.</summary>
        public void Click(int attacker, int defender)
        {
            if (attacker == _attacker && defender == _defender)
            {
                int now = TenthsAt(attacker, defender);
                int next = now == 10 ? 20 : now == 20 ? 5 : now == 5 ? 0 : 10;
                _customMode = false;
                SetSelected(next);
                return;
            }
            _attacker = attacker; _defender = defender;
            _customMode = false;
            RaiseSelection();
        }

        public string SelectedTitle => _attacker < 0 ? "" : $"{NameOf(_attacker)} attacking {NameOf(_defender)}";

        public string[] Choices { get; } = { "No effect (0×)", "Not very effective (½×)", "Neutral (1×)", "Super effective (2×)", "Custom" };

        public int SelectedChoice
        {
            get
            {
                if (_attacker < 0) return -1;
                if (_customMode) return 4;
                return TenthsAt(_attacker, _defender) switch { 0 => 0, 5 => 1, 10 => 2, 20 => 3, _ => 4 };
            }
            set
            {
                if (_attacker < 0 || value < 0 || value == SelectedChoice) return;
                if (value == 4)
                {
                    // Stays in Custom while typing, even through 1.0 or 2.0; a custom value starts at 1.5x.
                    bool preset = TenthsAt(_attacker, _defender) is 0 or 5 or 10 or 20;
                    _customMode = true;
                    if (preset) SetSelected(15); else RaiseSelection();
                    return;
                }
                _customMode = false;
                SetSelected(new[] { 0, 5, 10, 20 }[value]);
            }
        }
        private bool _customMode;

        public bool IsCustom => SelectedChoice == 4;

        /// <summary>The selected pair's multiplier, e.g. 1.5 for 1.5×.</summary>
        public decimal Multiplier
        {
            get => _attacker < 0 ? 1 : TenthsAt(_attacker, _defender) / 10m;
            set { if (_attacker >= 0) SetSelected((int)Math.Round(Math.Clamp(value, 0, 25.5m) * 10)); }
        }

        public bool Foresight
        {
            get => _attacker >= 0 && ForesightAt(_attacker, _defender);
            set { if (_attacker >= 0 && TenthsAt(_attacker, _defender) == 0) { _chart.Set(_attacker, _defender, 0, value); Changed(); } }
        }
        public bool CanForesight => _attacker >= 0 && TenthsAt(_attacker, _defender) == 0;

        private void SetSelected(int tenths)
        {
            if (_chart == null || _attacker < 0) return;
            bool keepForesight = ForesightAt(_attacker, _defender);
            _chart.Set(_attacker, _defender, tenths, keepForesight);
            Changed();
        }

        private void RaiseSelection()
        {
            foreach (var n in new[] { nameof(SelectedAttacker), nameof(SelectedDefender), nameof(SelectedTitle), nameof(SelectedChoice),
                                      nameof(IsCustom), nameof(Multiplier), nameof(Foresight), nameof(CanForesight) })
                Raise(n);
        }

        public string Room => _chart == null ? "" : $"{_chart.Matchups.Count} of {_chart.MaxMatchups} matchups used · chart {_chart.Where}";

        public string Problem => _chart?.Problem() ?? "";
        public bool HasProblem => Problem.Length > 0;

        public string Warning
        {
            get
            {
                if (_chart == null) return "";
                var notes = _chart.Unusual.Select(m => $"{NameOf(m.Attacker)} → {NameOf(m.Defender)} {m.Tenths / 10m}×").ToList();
                string unusual = notes.Count == 0 ? "" :
                    "Battle messages and Conversion 2 only know 0×, ½× and 2×: " + string.Join(", ", notes.Take(6)) + (notes.Count > 6 ? "…" : "") + ".";
                return unusual;
            }
        }
        public bool HasWarning => Warning.Length > 0;

        private void Changed()
        {
            RaiseSelection();
            foreach (var n in new[] { nameof(Room), nameof(Problem), nameof(HasProblem), nameof(Warning), nameof(HasWarning), nameof(HasUnsavedChanges) })
                Raise(n);
            CellsChanged?.Invoke(this, EventArgs.Empty);
            _undo?.Record();
        }

        // Compared as a set: undoing an edit re-adds a pair at the end of its section, which is the same chart.
        private string Key() => _chart == null ? "" : string.Join(";", _chart.Matchups
            .OrderBy(m => m.ForesightRemovable).ThenBy(m => m.Attacker).ThenBy(m => m.Defender)
            .Select(m => $"{m.Attacker},{m.Defender},{m.Tenths},{m.ForesightRemovable}"));
        private string _savedKey = "";

        public bool HasUnsavedChanges => _chart != null && Key() != _savedKey;
        public string UnsavedChangesDescription => "Type chart";

        public void SaveChanges() => _ = SaveChangesAsync();

        public async Task<bool> SaveChangesAsync()
        {
            if (_chart == null) return true;
            if (HasProblem) { await DialogHelper.ShowError(Problem, "Type Chart"); return false; }
            try { _chart.Save(); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException)
            {
                await DialogHelper.ShowError("The type chart was not saved:\n" + e.Message, "Type Chart");
                return false;
            }
            _savedKey = Key();
            Changed();
            SaveNotice.Saved(UnsavedChangesDescription);
            return true;
        }

        public bool CanMakeRoom => _chart != null && !_chart.InExpansion;
        private bool? _expansionReady;
        public bool ExpansionReady => _expansionReady ??= SyntheticOverlaySpace.Available();
        public string MakeRoomTip => !ExpansionReady ? "Needs the ARM9 expansion from the ROM Patch Toolbox"
            : $"Move the chart to the expanded ARM9 area, where it holds up to {TypeChart.ExpandedCapacity - 2} matchups";

        /// <summary>Moves the chart, edits included, to the expanded ARM9 area so it can hold more matchups.</summary>
        public async Task MakeRoomAsync()
        {
            if (!CanMakeRoom) return;
            if (!await DialogHelper.AskYesNo($"Move the type chart to the expanded ARM9 area? It will hold up to {TypeChart.ExpandedCapacity - 2} matchups instead of {_chart.MaxMatchups}. " +
                "This saves the chart, including any unsaved edits.", "Type Chart")) return;
            try { _chart.MoveToExpansion(); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException)
            {
                // The Pokétch copy is written last, so the chart itself may already have moved.
                await DialogHelper.ShowError(_chart.InExpansion
                    ? "The type chart moved, but the Pokétch copy wasn't updated:\n" + e.Message
                    : "The type chart was not moved:\n" + e.Message, "Type Chart");
                if (!_chart.InExpansion) return;
            }
            _savedKey = Key();
            Raise(nameof(CanMakeRoom));
            Changed();
            SaveNotice.Saved(UnsavedChangesDescription);
        }

        public void DiscardChanges()
        {
            if (_chart == null) return;
            try { _chart = TypeChart.Load(); }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is InvalidOperationException)
            {
                _ = DialogHelper.ShowError("The saved type chart couldn't be read back:\n" + e.Message, "Type Chart");
                return;
            }
            _savedKey = Key();
            StartUndo();
            Changed();
        }
    }
}
