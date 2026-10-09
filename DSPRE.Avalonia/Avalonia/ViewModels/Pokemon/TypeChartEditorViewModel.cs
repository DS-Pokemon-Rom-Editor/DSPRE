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
    /// <summary>How well each type hits each other type.</summary>
    public class TypeChartEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private TypeChart _chart;

        private ByteStateUndo _undo;
        private void StartUndo() => _undo = new ByteStateUndo(() => _chart.Matchups.SelectMany(m => new[] { m.Attacker, m.Defender, m.Tenths, (byte)(m.ForesightRemovable ? 1 : m.RingTargetRemovable ? 2 : 0) }).ToArray(), ApplyMatchups, () => { Raise(nameof(CanUndo)); Raise(nameof(CanRedo)); });
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();

        private void ApplyMatchups(byte[] b)
        {
            _chart.Matchups.Clear();
            for (int k = 0; k + 3 < b.Length; k += 4)
                _chart.Matchups.Add(new TypeChart.Matchup { Attacker = b[k], Defender = b[k + 1], Tenths = b[k + 2], ForesightRemovable = b[k + 3] == 1, RingTargetRemovable = b[k + 3] == 2 });
            Changed();
        }


        public string[] TypeNames { get; } = Array.Empty<string>();
        public int TypeCount { get; }

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
        public global::Avalonia.Media.IImage AttackerIcon => _attacker < 0 ? null : DSPRE.Avalonia.Data.TypeIcons.For(_attacker);
        public global::Avalonia.Media.IImage DefenderIcon => _attacker < 0 ? null : DSPRE.Avalonia.Data.TypeIcons.For(_defender);

        // ── Highlighted row (attacking) and column (defending), toggled from the type icons ──
        private int _row = -1, _col = -1;
        public int HighlightRow => _row;
        public int HighlightColumn => _col;
        public event EventHandler HighlightChanged;

        public void ToggleRow(int type) { _row = _row == type ? -1 : type; HighlightMoved(); }
        public void ToggleColumn(int type) { _col = _col == type ? -1 : type; HighlightMoved(); }

        private void HighlightMoved()
        {
            RefreshSummary();
            HighlightChanged?.Invoke(this, EventArgs.Empty);
        }

        public sealed class SummaryLine
        {
            public global::Avalonia.Media.IImage From { get; init; }
            public global::Avalonia.Media.IImage To { get; init; }
            public string FromName { get; init; }
            public string ToName { get; init; }
            public bool FromHasIcon => From != null;
            public bool ToHasIcon => To != null;
            public string Multiplier { get; init; }
        }

        public ObservableCollection<SummaryLine> Summary { get; } = new();
        public bool HasSummary => Summary.Count > 0;

        private static string Times(int tenths, bool foresight) => tenths switch
        {
            0 => foresight ? "×0 (Foresight hits)" : "×0",
            5 => "×½",
            10 => "×1",
            20 => "×2",
            _ => "×" + (tenths / 10m).ToString("0.#"),
        };

        private void RefreshSummary()
        {
            Summary.Clear();
            if (_chart != null)
            {
                SummaryLine Line(int a, int d) => new SummaryLine
                {
                    From = DSPRE.Avalonia.Data.TypeIcons.For(a), To = DSPRE.Avalonia.Data.TypeIcons.For(d),
                    FromName = NameOf(a), ToName = NameOf(d), Multiplier = Times(TenthsAt(a, d), ForesightAt(a, d)),
                };
                if (_row >= 0 && _col >= 0) Summary.Add(Line(_row, _col));
                else if (_row >= 0) for (int d = 0; d < TypeCount; d++) Summary.Add(Line(_row, d));
                else if (_col >= 0) for (int a = 0; a < TypeCount; a++) Summary.Add(Line(a, _col));
            }
            Raise(nameof(HasSummary));
        }

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
                    // Stays in Custom while typing, even through 1.0 or 2.0.
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

        /// <summary>The selected pair's multiplier as a factor, not tenths.</summary>
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
            foreach (string n in new[] { nameof(SelectedAttacker), nameof(SelectedDefender), nameof(SelectedTitle), nameof(AttackerIcon), nameof(DefenderIcon), nameof(SelectedChoice),
                                      nameof(IsCustom), nameof(Multiplier), nameof(Foresight), nameof(CanForesight) })
                Raise(n);
        }

        public string Room => _chart == null ? ""
            : _chart.FromSource ? $"{_chart.Matchups.Count} matchups · chart {_chart.Where}"
            : $"{_chart.Matchups.Count} of {_chart.MaxMatchups} matchups used · chart {_chart.Where}";

        public string Problem => _chart?.Problem() ?? "";
        public bool HasProblem => Problem.Length > 0;

        public string Warning
        {
            get
            {
                if (_chart == null) return "";
                List<string> notes = _chart.Unusual.Select(m => $"{NameOf(m.Attacker)} → {NameOf(m.Defender)} {m.Tenths / 10m}×").ToList();
                if (notes.Count == 0) return "";
                return "Battle messages and Conversion 2 only know 0×, ½× and 2×: " + string.Join(", ", notes.Take(6)) + (notes.Count > 6 ? "…" : "") + ". "
                    + "Stealth Rock only deals 0, ½, 1, 2 or 4 times its damage, and Wonder Guard, Filter, Solid Rock, Expert Belt "
                    + "and Tinted Lens treat custom values as neutral.";
            }
        }
        public bool HasWarning => Warning.Length > 0;

        private void Changed()
        {
            RaiseSelection();
            foreach (string n in new[] { nameof(Room), nameof(Problem), nameof(HasProblem), nameof(Warning), nameof(HasWarning), nameof(HasUnsavedChanges) })
                Raise(n);
            CellsChanged?.Invoke(this, EventArgs.Empty);
            RefreshSummary();
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
            if (_chart.FromSource)
            {
                (bool saved, string error) = await HgEngineSave.RunAsync(() => { _chart.Save(); return null; });
                if (!saved)
                {
                    if (error != null) await DialogHelper.ShowError("The type chart was not saved:\n" + error, "Type Chart");
                    return false;
                }
            }
            else
            {
                try { _chart.Save(); }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException)
                {
                    await DialogHelper.ShowError("The type chart was not saved:\n" + e.Message, "Type Chart");
                    return false;
                }
            }
            _savedKey = Key();
            Changed();
            SaveNotice.Saved(UnsavedChangesDescription);
            return true;
        }

        public bool CanMakeRoom => _chart != null && !_chart.InExpansion && !_chart.FromSource;
        public string MakeRoomTip => $"The \"Expand the type chart\" patch in the ROM Patch Toolbox moves the chart to the expanded ARM9 area, where it holds up to {TypeChart.ExpandedCapacity - 2} matchups";

        /// <summary>Moving the chart changes the game's code, so the toolbox does it.</summary>
        public Task MakeRoomAsync() => PatchHandover.OfferAsync("typeChartExpanded", "Expand the type chart", "Room for more matchups", "Type Chart");

        /// <summary>Rereads the chart after a toolbox patch moved it, unless edits here are still unsaved.</summary>
        public void OnPatchStateChanged()
        {
            if (_chart == null || HasUnsavedChanges) return;
            DiscardChanges();
            Raise(nameof(CanMakeRoom));
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
