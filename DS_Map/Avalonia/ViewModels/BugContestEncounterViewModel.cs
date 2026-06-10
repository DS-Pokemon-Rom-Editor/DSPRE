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

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>
    /// Avalonia port of the WinForms <c>BugContestEncounterEditor</c> (HGSS).
    /// Edits the Bug-Catching Contest encounter sets (species, level range, the
    /// threshold-based Rate, and Score). Embedded as a tab in the Encounters editor.
    /// </summary>
    public class BugContestEncounterViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
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
        public ObservableCollection<string> EncounterRows { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> SpeciesNames { get; } = new ObservableCollection<string>();

        private bool _isAvailable;
        public bool IsAvailable { get => _isAvailable; private set => Set(ref _isAvailable, value); }
        public bool IsNotAvailable => !_isAvailable;

        private string _setDescription = "";
        public string SetDescription { get => _setDescription; set => Set(ref _setDescription, value); }

        private string _effectiveRateText = "Effective: ~0%";
        public string EffectiveRateText { get => _effectiveRateText; set => Set(ref _effectiveRateText, value); }

        private IBrush _effectiveRateColor = Brushes.Gray;
        public IBrush EffectiveRateColor { get => _effectiveRateColor; set => Set(ref _effectiveRateColor, value); }

        private string _rateWarning = "";
        public string RateWarning { get => _rateWarning; set => Set(ref _rateWarning, value); }

        private Bitmap _pokemonIcon;
        public Bitmap PokemonIcon { get => _pokemonIcon; set => Set(ref _pokemonIcon, value); }

        private decimal _dummyValue;
        public decimal DummyValue { get => _dummyValue; set => Set(ref _dummyValue, value); }

        private int _selectedSetIndex = -1;
        public int SelectedSetIndex
        {
            get => _selectedSetIndex;
            set { if (Set(ref _selectedSetIndex, value) && value >= 0) RefreshSetDisplay(); }
        }

        private int _selectedEncounterIndex = -1;
        public int SelectedEncounterIndex
        {
            get => _selectedEncounterIndex;
            set { if (Set(ref _selectedEncounterIndex, value)) LoadEncounter(value); }
        }

        private int _speciesIndex = -1;
        public int SpeciesIndex { get => _speciesIndex; set { if (Set(ref _speciesIndex, value) && !_suppress) ApplyEdit(); } }

        private decimal _minLevel = 1;
        public decimal MinLevel { get => _minLevel; set { if (Set(ref _minLevel, value) && !_suppress) ApplyEdit(); } }

        private decimal _maxLevel = 1;
        public decimal MaxLevel { get => _maxLevel; set { if (Set(ref _maxLevel, value) && !_suppress) ApplyEdit(); } }

        private decimal _rate;
        public decimal Rate { get => _rate; set { if (Set(ref _rate, value) && !_suppress) { ApplyEdit(); UpdateRateDisplay(); } } }

        private decimal _score;
        public decimal Score { get => _score; set { if (Set(ref _score, value) && !_suppress) ApplyEdit(); } }

        // ── Dirty tracking ───────────────────────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => "Bug Contest Encounter Editor";
        public void SaveChanges() => Save();
        public void DiscardChanges() { _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetDirty() { if (_dirty) return; _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetClean() { if (!_dirty) return; _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        // ── Constructors ──────────────────────────────────────────────────────────
        public BugContestEncounterViewModel()
        {
            if (!Design.IsDesignMode) return;
            IsAvailable = true;
            SetNames.Add("National Park");
            EncounterRows.Add("Caterpie");
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
                    "Bug Contest encounter file not found.\nExpected location: data/mushi/mushi_encount.bin", "File Not Found");
                IsAvailable = false; OnPropertyChanged(nameof(IsNotAvailable)); return;
            }
            IsAvailable = true; OnPropertyChanged(nameof(IsNotAvailable));

            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.monIcons });
            SetMonIconsPalTableAddress();

            SpeciesNames.Clear();
            foreach (var n in GetPokemonNames()) SpeciesNames.Add(n);

            LoadFile(() => new BugContestEncounterFile(true));
        }

        private void LoadFile(Func<BugContestEncounterFile> factory)
        {
            try
            {
                _file = factory();
                _suppress = true;
                SetNames.Clear();
                foreach (var s in _file.Sets) SetNames.Add(s.ToString());
                _suppress = false;
                if (SetNames.Count > 0) SelectedSetIndex = 0;
                SetClean();
            }
            catch (Exception ex)
            {
                _ = DialogHelper.ShowError($"Error loading Bug Contest encounters: {ex.Message}", "Error");
            }
        }

        private BugContestEncounterSet CurrentSet =>
            _file != null && _selectedSetIndex >= 0 && _selectedSetIndex < _file.Sets.Count
                ? _file.Sets[_selectedSetIndex] : null;

        private void RefreshSetDisplay()
        {
            var set = CurrentSet;
            if (set == null) return;
            SetDescription = set.Description;

            _suppress = true;
            EncounterRows.Clear();
            foreach (var enc in set.Encounters) EncounterRows.Add(enc.ToString());
            _suppress = false;

            if (EncounterRows.Count > 0) SelectedEncounterIndex = 0;
            UpdateRateDisplay();
        }

        private void LoadEncounter(int index)
        {
            var set = CurrentSet;
            if (set == null || index < 0 || index >= set.Encounters.Count) { ClearFields(); return; }
            var enc = set.Encounters[index];

            _suppress = true;
            SpeciesIndex = enc.Species < SpeciesNames.Count ? enc.Species : 0;
            MinLevel = Clamp(enc.MinLevel, 0, 100);
            MaxLevel = Clamp(enc.MaxLevel, 0, 100);
            Rate = Math.Min(99, (int)enc.Rate);
            Score = enc.Score;
            DummyValue = enc.Dummy;
            _suppress = false;

            UpdateIcon(enc.Species);
            UpdateRateDisplay();
        }

        private static decimal Clamp(int v, int lo, int hi) => Math.Max(lo, Math.Min(hi, v));

        private void ClearFields()
        {
            _suppress = true;
            SpeciesIndex = -1; MinLevel = 1; MaxLevel = 1; Rate = 0; Score = 0; DummyValue = 0;
            PokemonIcon = null;
            EffectiveRateText = "Effective: ~0%"; EffectiveRateColor = Brushes.Gray; RateWarning = "";
            _suppress = false;
        }

        private void ApplyEdit()
        {
            var set = CurrentSet;
            if (set == null || _selectedEncounterIndex < 0 || _selectedEncounterIndex >= set.Encounters.Count) return;
            var enc = set.Encounters[_selectedEncounterIndex];

            enc.Species = (ushort)(_speciesIndex >= 0 ? _speciesIndex : 0);
            enc.MinLevel = (byte)_minLevel;
            enc.MaxLevel = (byte)_maxLevel;
            enc.Rate = (byte)_rate;
            enc.Score = (byte)_score;
            // Dummy/Terminator is read-only.

            SetDirty();

            int sel = _selectedEncounterIndex;
            _suppress = true;
            EncounterRows[sel] = enc.ToString();
            _suppress = false;

            if (_speciesIndex >= 0) UpdateIcon(_speciesIndex);
        }

        private void UpdateIcon(int species)
        {
            try
            {
                if (species <= 0) { PokemonIcon = null; return; }
                using var gdi = DSUtils.GetPokePic(species, 64, 64);
                PokemonIcon = ImageConverter.ToAvaloniaBitmap(gdi);
            }
            catch { PokemonIcon = null; }
        }

        // ── Effective-rate calculation + validation (ported verbatim) ───────────────
        private int CalculateEffectiveRate(int index)
        {
            var set = CurrentSet;
            if (set == null || index < 0 || index >= set.Encounters.Count) return 0;
            int currentRate = set.Encounters[index].Rate;
            if (index == 0) return Math.Max(0, 100 - currentRate);
            int previousRate = set.Encounters[index - 1].Rate;
            return Math.Max(0, previousRate - currentRate);
        }

        private string ValidateRates()
        {
            var set = CurrentSet;
            if (set == null) return "";
            var warnings = new List<string>();

            var rateGroups = set.Encounters
                .Select((enc, idx) => new { enc.Rate, Index = idx })
                .Where(x => x.Rate > 0)
                .GroupBy(x => x.Rate)
                .Where(g => g.Count() > 1);
            foreach (var group in rateGroups)
            {
                var indices = group.Select(x => x.Index + 1).ToArray();
                warnings.Add($"⚠ Rate {group.Key} duplicated at entries {string.Join(", ", indices)} - only first triggers!");
            }

            for (int i = 1; i < set.Encounters.Count; i++)
            {
                int prevRate = set.Encounters[i - 1].Rate;
                int currRate = set.Encounters[i].Rate;
                if (currRate >= prevRate && currRate > 0)
                    warnings.Add($"⚠ Entry {i + 1} (rate {currRate}) never triggers - rate must be < {prevRate}.");
            }
            return string.Join("\n", warnings);
        }

        private void UpdateRateDisplay()
        {
            if (_selectedEncounterIndex >= 0)
            {
                int eff = CalculateEffectiveRate(_selectedEncounterIndex);
                EffectiveRateText = $"Effective: ~{eff}%";
                EffectiveRateColor = eff == 0 ? Brushes.DarkRed : eff < 5 ? Brushes.DarkOrange : Brushes.DarkGreen;
            }
            else
            {
                EffectiveRateText = "Effective: ~0%";
                EffectiveRateColor = Brushes.Gray;
            }
            RateWarning = ValidateRates();
        }

        // ── Save / export / import ─────────────────────────────────────────────────
        public void Save()
        {
            if (_file == null) return;
            _file.SaveToFile();
            SetClean();
        }

        public async Task ExportAsync()
        {
            if (_file == null) return;
            var filter = new FilePickerFileType("Binary files") { Patterns = new[] { "*.bin" } };
            string path = await DialogHelper.SaveFile(_owner, "Export Bug Contest Encounters",
                new[] { filter }, "mushi_encount.bin");
            if (path == null) return;
            _file.SaveToFile(path);
        }

        public async Task ImportAsync()
        {
            var filter = new FilePickerFileType("Binary files") { Patterns = new[] { "*.bin" } };
            string path = await DialogHelper.OpenFile(_owner, "Import Bug Contest Encounters", new[] { filter });
            if (path == null) return;

            try
            {
                LoadFile(() => new BugContestEncounterFile(path));
                SetDirty();
                await DialogHelper.ShowInfo("Bug Contest encounters imported successfully!", "Import Complete");
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowError($"Error importing file: {ex.Message}", "Import Error");
            }
        }

        public void Locate()
        {
            string path = Filesystem.GetBugContestEncounterPath();
            if (File.Exists(path)) Helpers.ExplorerSelect(path);
        }

        public Task ShowRateHelp() => DialogHelper.ShowInfo(
            "The Rate value is a THRESHOLD, not a percentage.\n\n" +
            "The game rolls 0-99 and checks entries top to bottom; the first entry where (roll >= rate) wins.\n\n" +
            "Effective rate:\n" +
            "  • First entry: 100 - rate\n" +
            "  • Other entries: rate[previous] - rate[current]\n\n" +
            "Rates must strictly descend, or later entries never trigger.",
            "Rate System Help");
    }
}
