using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Media;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Platform.Storage;
using DSPRE.Avalonia;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>
    /// Avalonia port of the WinForms <c>BugContestEncounterEditor</c> (HGSS).
    /// Edits the Bug-Catching Contest encounter sets (species, level range, the
    /// threshold-based Rate, and Score). Embedded as a tab in the Encounters editor.
    /// </summary>
    public class BugContestEncounterViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private Window _owner;
        private bool _suppress;
        private BugContestEncounterFile _file;

        public ObservableCollection<string> SetNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<BugRow> Rows { get; } = new ObservableCollection<BugRow>();

        /// <summary>One slot of the shown set, edited in place.</summary>
        public sealed class BugRow : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
            private readonly BugContestEncounter _e;
            private readonly Action _edited;
            public BugRow(BugContestEncounter e, ObservableCollection<string> species, Action edited) { _e = e; SpeciesNames = species; _edited = edited; }
            public ObservableCollection<string> SpeciesNames { get; }
            public int Species => _e.Species;
            public int SpeciesIndex { get => _e.Species < SpeciesNames.Count ? _e.Species : -1; set { if (value < 0 || value == _e.Species) return; _e.Species = (ushort)value; Raise(); Raise(nameof(Species)); _edited(); } }
            // Shown clamped to the boxes' range; only an edit to that box writes it back.
            public decimal MinLevel { get => Math.Min((int)_e.MinLevel, 100); set { if ((byte)value == _e.MinLevel) return; _e.MinLevel = (byte)value; Raise(); _edited(); } }
            public decimal MaxLevel { get => Math.Min((int)_e.MaxLevel, 100); set { if ((byte)value == _e.MaxLevel) return; _e.MaxLevel = (byte)value; Raise(); _edited(); } }
            public decimal Rate { get => _e.Rate; set { if ((byte)value == _e.Rate) return; _e.Rate = (byte)value; Raise(); _edited(); } }
            public decimal Score { get => _e.Score; set { if ((byte)value == _e.Score) return; _e.Score = (byte)value; Raise(); _edited(); } }
            private string _chance = "";
            public string Chance { get => _chance; set { if (_chance == value) return; _chance = value; Raise(); } }
            private IBrush _chanceBrush = Brushes.Gray;
            public IBrush ChanceBrush { get => _chanceBrush; set { if (_chanceBrush == value) return; _chanceBrush = value; Raise(); } }
        }
        public ObservableCollection<string> SpeciesNames { get; } = new ObservableCollection<string>();

        private bool _isAvailable;
        public bool IsAvailable { get => _isAvailable; private set => Set(ref _isAvailable, value); }
        public bool IsNotAvailable => !_isAvailable;

        private string _setDescription = "";
        public string SetDescription { get => _setDescription; set => Set(ref _setDescription, value); }

        private string _rateWarning = "";
        public string RateWarning { get => _rateWarning; set => Set(ref _rateWarning, value); }

        private int _selectedSetIndex = -1;
        public int SelectedSetIndex
        {
            get => _selectedSetIndex;
            set { if (Set(ref _selectedSetIndex, value) && value >= 0) RefreshSetDisplay(); }
        }

        // ── Dirty tracking ───────────────────────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => "Bug Contest encounters";
        public void SaveChanges() => Save();
        // Edits live in the loaded file, so discarding reads it again.
        public void DiscardChanges() { if (_dirty && _file != null) LoadFile(() => new BugContestEncounterFile(true)); SetClean(); }
        private void SetDirty() { if (_dirty) return; _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetClean() { if (!_dirty) return; _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        // ── Undo / redo: every set's encounters ─────────────────────────────────
        private DSPRE.Avalonia.ByteStateUndo _undo;
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();
        private void RaiseUndo() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private byte[] TakeState() => DSPRE.Avalonia.UndoJson.Take(_file.Sets.Select(s => s.Encounters
            .Select(en => new int[] { en.Species, en.MinLevel, en.MaxLevel, en.Rate, en.Score, en.Dummy }).ToArray()).ToArray());

        private void ApplyState(byte[] state)
        {
            int[][][] sets = DSPRE.Avalonia.UndoJson.Read<int[][][]>(state);
            for (int s = 0; s < sets.Length && s < _file.Sets.Count; s++)
                for (int i = 0; i < sets[s].Length && i < _file.Sets[s].Encounters.Count; i++)
                {
                    BugContestEncounter en = _file.Sets[s].Encounters[i];
                    int[] v = sets[s][i];
                    en.Species = (ushort)v[0]; en.MinLevel = (byte)v[1]; en.MaxLevel = (byte)v[2];
                    en.Rate = (byte)v[3]; en.Score = (byte)v[4]; en.Dummy = (ushort)v[5];
                }
            RefreshSetDisplay();
            if (_undo.IsDirty) SetDirty(); else SetClean();
        }

        private void ResetUndo()
        {
            if (_file == null) return;
            _undo = new DSPRE.Avalonia.ByteStateUndo(TakeState, ApplyState, RaiseUndo);
            RaiseUndo();
        }

        private void Edited()
        {
            _undo?.Record();
            if (_undo == null || _undo.IsDirty) SetDirty(); else SetClean();
        }

        // ── Constructors ──────────────────────────────────────────────────────────
        public BugContestEncounterViewModel()
        {
            if (!Design.IsDesignMode) return;
            IsAvailable = true;
            SetNames.Add("National Park");
        }

        public BugContestEncounterViewModel(bool _) { }

        // ── Setup ────────────────────────────────────────────────────────────────
        public async Task SetupAsync(Window owner)
        {
            _owner = owner;
            if (!BugContestEncounterFile.IsAvailable())
            {
                IsAvailable = false; OnPropertyChanged(nameof(IsNotAvailable)); return;
            }
            if (!Filesystem.BugContestEncounterFileExists())
            {
                await DialogHelper.ShowError(
                    "Bug Contest encounter file not found.\nExpected location: data/mushi/mushi_encount.bin", "Special encounters editor error");
                IsAvailable = false; OnPropertyChanged(nameof(IsNotAvailable)); return;
            }
            IsAvailable = true; OnPropertyChanged(nameof(IsNotAvailable));

            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.monIcons });
            SetMonIconsPalTableAddress();

            SpeciesNames.Clear();
            foreach (string n in GetPokemonNames()) SpeciesNames.Add(n);

            LoadFile(() => new BugContestEncounterFile(true));
        }

        private void LoadFile(Func<BugContestEncounterFile> factory, bool newHistory = true)
        {
            try
            {
                _file = factory();
                _suppress = true;
                SetNames.Clear();
                foreach (BugContestEncounterSet s in _file.Sets) SetNames.Add(s.ToString());
                _suppress = false;
                // Reset first so set 0 of a newly imported file still refreshes.
                _selectedSetIndex = -1;
                if (SetNames.Count > 0) SelectedSetIndex = 0;
                SetClean();
                if (newHistory || _undo == null) ResetUndo();
            }
            catch (Exception ex)
            {
                _ = DialogHelper.ShowError($"Error loading Bug Contest encounters: {ex.Message}", "Special encounters editor error");
            }
        }

        private BugContestEncounterSet CurrentSet =>
            _file != null && _selectedSetIndex >= 0 && _selectedSetIndex < _file.Sets.Count
                ? _file.Sets[_selectedSetIndex] : null;

        public void RefreshNames()
        {
            if (SpeciesNames.Count == 0) return;
            _suppress = true;
            try { ListSync.Apply(SpeciesNames, GetPokemonNames()); }
            finally { _suppress = false; }
        }

        private void RefreshSetDisplay()
        {
            BugContestEncounterSet set = CurrentSet;
            if (set == null) return;
            SetDescription = set.Description;

            Rows.Clear();
            foreach (BugContestEncounter enc in set.Encounters) Rows.Add(new BugRow(enc, SpeciesNames, RowEdited));
            UpdateRateDisplay();
        }

        // ── Effective-rate calculation + validation (ported verbatim) ───────────────
        private int CalculateEffectiveRate(int index)
        {
            BugContestEncounterSet set = CurrentSet;
            if (set == null || index < 0 || index >= set.Encounters.Count) return 0;
            int currentRate = set.Encounters[index].Rate;
            if (index == 0) return Math.Max(0, 100 - currentRate);
            int previousRate = set.Encounters[index - 1].Rate;
            return Math.Max(0, previousRate - currentRate);
        }

        private string ValidateRates()
        {
            BugContestEncounterSet set = CurrentSet;
            if (set == null) return "";
            List<string> warnings = new List<string>();

            var rateGroups = set.Encounters
                .Select((enc, idx) => new { enc.Rate, Index = idx })
                .Where(x => x.Rate > 0)
                .GroupBy(x => x.Rate)
                .Where(g => g.Count() > 1);
            foreach (var group in rateGroups)
            {
                int[] indices = group.Select(x => x.Index + 1).ToArray();
                warnings.Add($"Rate {group.Key} duplicated at entries {string.Join(", ", indices)}; only the first one is used.");
            }

            if (set.Encounters.Count > 0 && set.Encounters[set.Encounters.Count - 1].Rate != 0)
                warnings.Add("The last entry's rate must be 0, or low rolls read past the list.");

            for (int i = 1; i < set.Encounters.Count; i++)
            {
                int prevRate = set.Encounters[i - 1].Rate;
                int currRate = set.Encounters[i].Rate;
                if (currRate >= prevRate && currRate > 0)
                    warnings.Add($"Entry {i + 1} (rate {currRate}) never triggers - rate must be < {prevRate}.");
            }
            return string.Join("\n", warnings);
        }

        private void RowEdited()
        {
            Edited();
            UpdateRateDisplay();
        }

        private void UpdateRateDisplay()
        {
            for (int i = 0; i < Rows.Count; i++)
            {
                int eff = CalculateEffectiveRate(i);
                Rows[i].Chance = eff + "%";
                Rows[i].ChanceBrush = eff == 0 ? StatusBrushes.Bad : eff < 5 ? StatusBrushes.Warn : StatusBrushes.Good;
            }
            RateWarning = ValidateRates();
        }

        // ── Save / export / import ─────────────────────────────────────────────────
        public void Save()
        {
            if (_file == null) return;
            if (_file.Problem() is string problem) { _ = DialogHelper.ShowError(problem, "Bug Contest error"); return; }
            // The file reports its own write error.
            if (!_file.SaveToFile(showSuccessMessage: false)) return;
            _undo?.MarkSaved();
            SetClean();
            SaveNotice.Saved(UnsavedChangesDescription);
        }

        public async Task ExportAsync()
        {
            if (_file == null) return;
            FilePickerFileType filter = new FilePickerFileType("Binary files") { Patterns = new[] { "*.bin" } };
            string path = await DialogHelper.SaveFile(_owner, "Export Bug Contest encounters",
                new[] { filter }, "mushi_encount.bin");
            if (path == null) return;
            try { _file.SaveToFile(path, showSuccessMessage: false); SaveNotice.Exported(path); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                await DialogHelper.ShowError($"Export failed:\n{ex.Message}", "Special encounters editor error");
            }
        }

        public async Task ImportAsync()
        {
            FilePickerFileType filter = new FilePickerFileType("Binary files") { Patterns = new[] { "*.bin" } };
            string path = await DialogHelper.OpenFile(_owner, "Import Bug Contest encounters", new[] { filter });
            if (path == null) return;

            try
            {
                // An import is one undoable step over what was shown.
                LoadFile(() => new BugContestEncounterFile(path), newHistory: false);
                Edited();
                SaveNotice.Imported("Bug Contest encounters", path);
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowError($"Error importing file: {ex.Message}", "Special encounters editor error");
            }
        }

        public void Locate()
        {
            string path = Filesystem.GetBugContestEncounterPath();
            if (File.Exists(path)) SystemShell.RevealInFileManager(path);
        }
    }
}
