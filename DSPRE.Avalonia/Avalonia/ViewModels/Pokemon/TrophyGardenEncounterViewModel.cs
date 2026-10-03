using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using global::Avalonia.Media;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>
    /// The 16-species pool Trophy Garden picks its daily Pokémon from (DP/Pt); the active two live in the save file.
    /// </summary>
    public class TrophyGardenEncounterViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo
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
        // Edits live in the loaded file, so discarding reads it again.
        public void DiscardChanges()
        {
            if (_isDirty && _file != null) LoadEncounterFile();
            _isDirty = false;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        // ── Undo / redo: every slot's species ──
        private DSPRE.Avalonia.ByteStateUndo _undo;
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();
        private void RaiseUndo() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private byte[] TakeState() => DSPRE.Avalonia.UndoJson.Take(_file.Encounters.Select(en => (int)en.Species).ToArray());

        private void ApplyState(byte[] state)
        {
            var species = DSPRE.Avalonia.UndoJson.Read<int[]>(state);
            for (int i = 0; i < species.Length && i < _file.Encounters.Count; i++) _file.Encounters[i].Species = (ushort)species[i];
            int slot = _selectedSlotIndex;
            RefreshSlotLabels();
            _selectedSlotIndex = -1;
            if (slot >= 0 && slot < SlotLabels.Count) SelectedSlotIndex = slot;
            _isDirty = _undo.IsDirty;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void Edited()
        {
            _undo?.Record();
            _isDirty = _undo?.IsDirty ?? true;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        public TrophyGardenEncounterViewModel()
        {
            if (!IsAvailable)
            {
                StatusText = "Trophy Garden is only in Diamond, Pearl and Platinum.";
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
            _selectedSlotIndex = -1;
            if (SlotLabels.Count > 0) SelectedSlotIndex = 0;
            _undo = new DSPRE.Avalonia.ByteStateUndo(TakeState, ApplyState, RaiseUndo);
            RaiseUndo();
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
            SpeciesIndex = encounter.Species < PokemonNames.Count ? encounter.Species : -1;
            SlotInfoText = $"Slot number: {index:D2}";
            PokemonIcon = _icons.Get(encounter.Species);
            _suppress = false;
        }

        private void ApplySpeciesChange()
        {
            if (_file == null || SpeciesIndex < 0 || _selectedSlotIndex < 0 || _selectedSlotIndex >= _file.Encounters.Count) return;

            var encounter = _file.Encounters[_selectedSlotIndex];
            encounter.Species = (ushort)SpeciesIndex;

            int slot = _selectedSlotIndex;
            RefreshSlotLabels();
            SelectedSlotIndex = slot;
            PokemonIcon = _icons.Get(encounter.Species);

            Edited();
        }

        public void Save()
        {
            if (_file == null) return;
            if (_file.Problem() is string problem) { _ = DialogHelper.ShowError(problem, "Trophy Garden"); return; }
            // The file reports its own write error.
            if (!_file.SaveToNarc(showSuccessMessage: false)) return;
            _isDirty = false;
            _undo?.MarkSaved();
            SaveNotice.Saved(UnsavedChangesDescription);
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
                // Reset first so slot 0 still reloads from the imported file.
                _selectedSlotIndex = -1;
                if (SlotLabels.Count > 0) SelectedSlotIndex = 0;
                Edited();
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
