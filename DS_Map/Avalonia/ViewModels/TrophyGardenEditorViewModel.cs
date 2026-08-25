using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using global::Avalonia.Media;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>
    /// Avalonia port of the WinForms <c>TrophyGardenEncounterEditor</c>: the 16-species pool Trophy
    /// Garden picks its daily-changing Pokémon from (Diamond/Pearl/Platinum). Which two are active
    /// right now lives in the save file, not the ROM, so it isn't shown here.
    /// </summary>
    public class TrophyGardenEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private TrophyGardenEncounterFile _file;
        private readonly PokemonIconCache _icons = new();
        private bool _suppress;
        private bool _isDirty;

        public bool IsAvailable => TrophyGardenEncounterFile.IsAvailable();

        public ObservableCollection<string> SlotLabels { get; } = new();
        public ObservableCollection<string> PokemonNames { get; } = new();

        private int _selectedSlotIndex = -1;
        public int SelectedSlotIndex
        {
            get => _selectedSlotIndex;
            set
            {
                if (!Set(ref _selectedSlotIndex, value)) return;
                if (_suppress) return;
                LoadSlot(value);
            }
        }

        private int _speciesIndex = -1;
        public int SpeciesIndex
        {
            get => _speciesIndex;
            set
            {
                if (!Set(ref _speciesIndex, value)) return;
                if (_suppress) return;
                ApplySpeciesChange();
            }
        }

        private IImage _pokemonIcon;
        public IImage PokemonIcon { get => _pokemonIcon; private set => Set(ref _pokemonIcon, value); }

        private string _slotInfoText = "Slot: N/A";
        public string SlotInfoText { get => _slotInfoText; private set => Set(ref _slotInfoText, value); }

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        // ── IEditorWithUnsavedChanges ──
        public bool HasUnsavedChanges => _isDirty;
        public string UnsavedChangesDescription => "Trophy Garden Encounter Editor";
        public void SaveChanges() => Save();
        public void DiscardChanges() { _isDirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        public TrophyGardenEditorViewModel()
        {
            if (!IsAvailable)
            {
                StatusText = "Trophy Garden is only available on Diamond, Pearl and Platinum.";
                return;
            }

            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.encounterExtended });

            if (string.IsNullOrEmpty(Filesystem.encounterExtended) || !Directory.Exists(Filesystem.encounterExtended))
            {
                StatusText = "Trophy Garden encounter files not found. Expected location: arc/encdata_ex.narc";
                return;
            }

            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.monIcons });
            SetMonIconsPalTableAddress();

            foreach (var name in GetPokemonNames()) PokemonNames.Add(name);

            LoadEncounterFile();
        }

        private void LoadEncounterFile()
        {
            _file = new TrophyGardenEncounterFile(true);
            RefreshSlotLabels();
            if (SlotLabels.Count > 0) SelectedSlotIndex = 0;
            UpdateStatus();
        }

        private void RefreshSlotLabels()
        {
            _suppress = true;
            SlotLabels.Clear();
            for (int i = 0; i < _file.Encounters.Count; i++)
                SlotLabels.Add($"Slot {i:D2}: {_file.Encounters[i]}");
            _suppress = false;
        }

        private void LoadSlot(int index)
        {
            _suppress = true;
            if (_file == null || index < 0 || index >= _file.Encounters.Count)
            {
                SpeciesIndex = -1;
                PokemonIcon = null;
                SlotInfoText = "Slot: N/A";
                _suppress = false;
                return;
            }

            var encounter = _file.Encounters[index];
            SpeciesIndex = encounter.Species < PokemonNames.Count ? encounter.Species : 0;
            SlotInfoText = $"Slot number: {index:D2}";
            PokemonIcon = _icons.Get(encounter.Species);
            _suppress = false;
        }

        private void ApplySpeciesChange()
        {
            if (_file == null || _selectedSlotIndex < 0 || _selectedSlotIndex >= _file.Encounters.Count) return;

            var encounter = _file.Encounters[_selectedSlotIndex];
            encounter.Species = (ushort)(SpeciesIndex >= 0 ? SpeciesIndex : 0);

            int slot = _selectedSlotIndex;
            RefreshSlotLabels();
            SelectedSlotIndex = slot;
            PokemonIcon = _icons.Get(encounter.Species);

            _isDirty = true;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        public void Save()
        {
            if (_file == null) return;
            _file.SaveToNarc();
            _isDirty = false;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            UpdateStatus();
        }

        public void Export(string path)
        {
            _file?.ExportToFile(path);
        }

        public void Import(string path)
        {
            if (_file == null) return;
            if (_file.ImportFromFile(path))
            {
                RefreshSlotLabels();
                if (SlotLabels.Count > 0) SelectedSlotIndex = 0;
                _isDirty = true;
                OnPropertyChanged(nameof(HasUnsavedChanges));
                AppMessages.Info("Trophy Garden encounters imported successfully!", "Import Complete");
            }
        }

        public void Locate()
        {
            string path = Filesystem.encounterExtended;
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                SystemShell.RevealInFileManager(path);
            else
                AppMessages.Warning("Trophy Garden encounter directory not found.", "Directory Not Found");
        }

        public void ShowHelp()
        {
            AppMessages.Info(
@"After speaking with Mr. Backlot (Pokémon Mansion, Route 212 North) with the National
Dex obtained, the Trophy Garden starts offering a special Pokémon each day.

How it works: each day, the game randomly picks from this 16-species list (avoiding
repeats of the currently active picks). Up to two daily Pokémon can be active at once,
replacing the Trophy Garden's two 5% grass encounter slots.

This editor only changes the pool of 16 possible species. Which ones are active right
now is stored in your save file, not the ROM, so it isn't shown here.

Data format: 16 Pokémon slots, 4 bytes each (2-byte species ID + 2-byte padding), 64
bytes total. File location: encdata_ex.narc index 8.",
                "Trophy Garden System Help");
        }

        private void UpdateStatus() =>
            StatusText = $"{_file?.Encounters.Count ?? 0} slots.{(_isDirty ? " Unsaved changes." : "")}";
    }
}
