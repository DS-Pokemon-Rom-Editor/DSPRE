using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Platform.Storage;
using DSPRE.Avalonia;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>
    /// Avalonia port of the WinForms <c>GreatMarshEncounterEditor</c> (DPPt).
    /// Edits the Great Marsh daily-Pokémon pools (only the species per slot is
    /// editable). Embedded as a tab in the Encounters editor.
    /// </summary>
    public class GreatMarshEncounterViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private Window _owner;
        private bool _suppress;
        private GreatMarshEncounterFile _file;

        public ObservableCollection<string> GroupNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> EncounterSlots { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> SpeciesNames { get; } = new ObservableCollection<string>();

        private bool _isAvailable;
        public bool IsAvailable { get => _isAvailable; private set => Set(ref _isAvailable, value); }
        public bool IsNotAvailable => !_isAvailable;

        private string _groupDescription = "";
        public string GroupDescription { get => _groupDescription; set => Set(ref _groupDescription, value); }

        private string _slotInfoText = "Slot: N/A";
        public string SlotInfoText { get => _slotInfoText; set => Set(ref _slotInfoText, value); }

        private Bitmap _pokemonIcon;
        public Bitmap PokemonIcon { get => _pokemonIcon; set => Set(ref _pokemonIcon, value); }

        private int _selectedGroupIndex = -1;
        public int SelectedGroupIndex
        {
            get => _selectedGroupIndex;
            set { if (Set(ref _selectedGroupIndex, value) && value >= 0) RefreshGroupDisplay(); }
        }

        private int _selectedSlotIndex = -1;
        public int SelectedSlotIndex
        {
            get => _selectedSlotIndex;
            set { if (Set(ref _selectedSlotIndex, value) && !_suppress) LoadSlot(value); }
        }

        private int _selectedSpeciesIndex = -1;
        public int SelectedSpeciesIndex
        {
            get => _selectedSpeciesIndex;
            set { if (Set(ref _selectedSpeciesIndex, value) && !_suppress) OnSpeciesChanged(value); }
        }

        // ── Dirty tracking ───────────────────────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => "Great Marsh Encounter Editor";
        public void SaveChanges() => Save();
        // Edits live in the loaded file, so discarding reads it again.
        public void DiscardChanges() { if (_dirty && _file != null) LoadFile(); SetClean(); }
        private void SetDirty() { if (_dirty) return; _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetClean() { if (!_dirty) return; _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        // ── Undo / redo: every slot's species ───────────────────────────────────
        private DSPRE.Avalonia.ByteStateUndo _undo;
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();
        private void RaiseUndo() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private byte[] TakeState() => DSPRE.Avalonia.UndoJson.Take(
            _file.Groups.Select(g => g.Encounters.Select(en => (int)en.Species).ToArray()).ToArray());

        private void ApplyState(byte[] state)
        {
            int[][] species = DSPRE.Avalonia.UndoJson.Read<int[][]>(state);
            for (int g = 0; g < species.Length && g < _file.Groups.Count; g++)
                for (int s = 0; s < species[g].Length && s < _file.Groups[g].Encounters.Count; s++)
                    _file.Groups[g].Encounters[s].Species = (ushort)species[g][s];
            int slot = _selectedSlotIndex;
            RefreshGroupDisplay();
            if (slot >= 0 && slot < EncounterSlots.Count) { _selectedSlotIndex = -1; SelectedSlotIndex = slot; }
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
        public GreatMarshEncounterViewModel()
        {
            if (!Design.IsDesignMode) return;
            IsAvailable = true;
            GroupNames.Add("Post-National Dex");
            EncounterSlots.Add("Slot 00: Bidoof");
        }

        public GreatMarshEncounterViewModel(bool _) { }

        // ── Setup ────────────────────────────────────────────────────────────────
        public async Task SetupAsync(Window owner)
        {
            _owner = owner;
            if (!GreatMarshEncounterFile.IsAvailable())
            {
                IsAvailable = false;
                OnPropertyChanged(nameof(IsNotAvailable));
                return;
            }
            IsAvailable = true;
            OnPropertyChanged(nameof(IsNotAvailable));

            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.encounterExtended });
            if (string.IsNullOrEmpty(Filesystem.encounterExtended) || !Directory.Exists(Filesystem.encounterExtended))
            {
                await DialogHelper.ShowError(
                    "Great Marsh encounter files not found.\nExpected location: arc/encdata_ex.narc", "Files Not Found");
                IsAvailable = false;
                OnPropertyChanged(nameof(IsNotAvailable));
                return;
            }

            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.monIcons });
            SetMonIconsPalTableAddress();

            SpeciesNames.Clear();
            foreach (string n in GetPokemonNames()) SpeciesNames.Add(n);

            LoadFile();
        }

        private void LoadFile()
        {
            try
            {
                _file = new GreatMarshEncounterFile(true);
                _suppress = true;
                GroupNames.Clear();
                foreach (GreatMarshEncounterGroup g in _file.Groups) GroupNames.Add(g.Name);
                _suppress = false;

                _selectedGroupIndex = -1;
                if (GroupNames.Count > 0) SelectedGroupIndex = 0;
                ResetUndo();
            }
            catch (Exception ex)
            {
                _ = DialogHelper.ShowError($"Error loading Great Marsh encounters: {ex.Message}", "Error");
            }
        }

        private void RefreshGroupDisplay()
        {
            if (_file == null || _selectedGroupIndex < 0 || _selectedGroupIndex >= _file.Groups.Count) return;
            GreatMarshEncounterGroup group = _file.Groups[_selectedGroupIndex];
            GroupDescription = group.Description;

            _suppress = true;
            EncounterSlots.Clear();
            for (int i = 0; i < group.Encounters.Count; i++)
                EncounterSlots.Add($"Slot {i:D2}: {group.Encounters[i]}");
            _suppress = false;

            // Reset first so slot 0 of the new list still loads its species.
            _selectedSlotIndex = -1;
            if (EncounterSlots.Count > 0) SelectedSlotIndex = 0;
            else { OnPropertyChanged(nameof(SelectedSlotIndex)); ClearFields(); }
        }

        private void LoadSlot(int slot)
        {
            if (_file == null || slot < 0) { ClearFields(); return; }
            GreatMarshEncounterGroup group = _file.Groups[_selectedGroupIndex];
            if (slot >= group.Encounters.Count) { ClearFields(); return; }

            GreatMarshEncounter enc = group.Encounters[slot];
            _suppress = true;
            SelectedSpeciesIndex = enc.Species < SpeciesNames.Count ? enc.Species : -1;
            SlotInfoText = $"Slot number: {slot:D2}";
            _suppress = false;
            UpdateIcon(enc.Species);
        }

        private void ClearFields()
        {
            _suppress = true;
            SelectedSpeciesIndex = -1;
            PokemonIcon = null;
            SlotInfoText = "Slot: N/A";
            _suppress = false;
        }

        private void OnSpeciesChanged(int species)
        {
            if (_file == null || _selectedGroupIndex < 0 || _selectedSlotIndex < 0 || species < 0) return;
            GreatMarshEncounterGroup group = _file.Groups[_selectedGroupIndex];
            if (_selectedSlotIndex >= group.Encounters.Count) return;

            group.Encounters[_selectedSlotIndex].Species = (ushort)species;
            Edited();

            int slot = _selectedSlotIndex;
            _suppress = true;
            EncounterSlots[slot] = $"Slot {slot:D2}: {group.Encounters[slot]}";
            // Replacing the selected row clears the list's selection, and every later pick would then miss the slot.
            _selectedSlotIndex = slot;
            OnPropertyChanged(nameof(SelectedSlotIndex));
            _suppress = false;

            UpdateIcon(species);
        }

        private void UpdateIcon(int species)
        {
            try
            {
                if (species <= 0) { PokemonIcon = null; return; }
                RawImage gdi = DSUtils.GetPokePicRaw(species, 64, 64);
                PokemonIcon = ImageConverter.ToAvaloniaBitmap(gdi);
            }
            catch { PokemonIcon = null; }
        }

        // ── Save / export / import ─────────────────────────────────────────────────
        public void Save()
        {
            if (_file == null) return;
            // The file reports its own write error.
            if (!_file.SaveToNarc(showSuccessMessage: false)) return;
            _undo?.MarkSaved();
            SetClean();
            SaveNotice.Saved(UnsavedChangesDescription);
        }

        public async Task ExportAsync()
        {
            if (_file == null) return;
            FilePickerFileType filter = new FilePickerFileType("Binary files") { Patterns = new[] { "*.bin" } };
            string path = await DialogHelper.SaveFile(_owner, "Export Great Marsh Encounters",
                new[] { filter }, "great_marsh_encounters.bin");
            if (path == null) return;
            _file.ExportToFile(path);
        }

        public async Task ImportAsync()
        {
            if (_file == null) return;
            FilePickerFileType filter = new FilePickerFileType("Binary files") { Patterns = new[] { "*.bin" } };
            string path = await DialogHelper.OpenFile(_owner, "Import Great Marsh Encounters", new[] { filter });
            if (path == null) return;

            try
            {
                if (_file.ImportFromFile(path))
                {
                    _suppress = true;
                    GroupNames.Clear();
                    foreach (GreatMarshEncounterGroup g in _file.Groups) GroupNames.Add(g.Name);
                    _suppress = false;

                    _selectedGroupIndex = -1;
                    if (GroupNames.Count > 0) SelectedGroupIndex = 0;
                    Edited();
                    await DialogHelper.ShowInfo("Great Marsh encounters imported successfully!", "Import Complete");
                }
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowError($"Error importing file: {ex.Message}", "Import Error");
            }
        }

        public void Locate()
        {
            string path = Filesystem.encounterExtended;
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                SystemShell.RevealInFileManager(path);
        }
    }
}
