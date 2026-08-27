using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>
    /// Avalonia port of the WinForms <c>GreatMarshEncounterEditor</c> (DPPt).
    /// Edits the Great Marsh daily-Pokémon pools (only the species per slot is
    /// editable). Embedded as a tab in the Encounters editor.
    /// </summary>
    public class GreatMarshEncounterViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
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
            set { if (Set(ref _selectedSlotIndex, value)) LoadSlot(value); }
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
        public void DiscardChanges() { _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetDirty() { if (_dirty) return; _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetClean() { if (!_dirty) return; _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

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
            foreach (var n in GetPokemonNames()) SpeciesNames.Add(n);

            LoadFile();
        }

        private void LoadFile()
        {
            try
            {
                _file = new GreatMarshEncounterFile(true);
                _suppress = true;
                GroupNames.Clear();
                foreach (var g in _file.Groups) GroupNames.Add(g.Name);
                _suppress = false;

                if (GroupNames.Count > 0) SelectedGroupIndex = 0;
            }
            catch (Exception ex)
            {
                _ = DialogHelper.ShowError($"Error loading Great Marsh encounters: {ex.Message}", "Error");
            }
        }

        private void RefreshGroupDisplay()
        {
            if (_file == null || _selectedGroupIndex < 0 || _selectedGroupIndex >= _file.Groups.Count) return;
            var group = _file.Groups[_selectedGroupIndex];
            GroupDescription = group.Description;

            _suppress = true;
            EncounterSlots.Clear();
            for (int i = 0; i < group.Encounters.Count; i++)
                EncounterSlots.Add($"Slot {i:D2}: {group.Encounters[i]}");
            _suppress = false;

            if (EncounterSlots.Count > 0) SelectedSlotIndex = 0;
        }

        private void LoadSlot(int slot)
        {
            if (_file == null || slot < 0) { ClearFields(); return; }
            var group = _file.Groups[_selectedGroupIndex];
            if (slot >= group.Encounters.Count) { ClearFields(); return; }

            var enc = group.Encounters[slot];
            _suppress = true;
            SelectedSpeciesIndex = enc.Species < SpeciesNames.Count ? enc.Species : 0;
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
            if (_file == null || _selectedGroupIndex < 0 || _selectedSlotIndex < 0) return;
            var group = _file.Groups[_selectedGroupIndex];
            if (_selectedSlotIndex >= group.Encounters.Count) return;

            group.Encounters[_selectedSlotIndex].Species = (ushort)(species >= 0 ? species : 0);
            SetDirty();

            int slot = _selectedSlotIndex;
            _suppress = true;
            EncounterSlots[slot] = $"Slot {slot:D2}: {group.Encounters[slot]}";
            _suppress = false;

            if (species >= 0) UpdateIcon(species);
        }

        private void UpdateIcon(int species)
        {
            try
            {
                if (species <= 0) { PokemonIcon = null; return; }
                var gdi = DSUtils.GetPokePicRaw(species, 64, 64);
                PokemonIcon = ImageConverter.ToAvaloniaBitmap(gdi);
            }
            catch { PokemonIcon = null; }
        }

        // ── Save / export / import ─────────────────────────────────────────────────
        public void Save()
        {
            if (_file == null) return;
            _file.SaveToNarc();
            SetClean();
        }

        public async Task ExportAsync()
        {
            if (_file == null) return;
            var filter = new FilePickerFileType("Binary files") { Patterns = new[] { "*.bin" } };
            string path = await DialogHelper.SaveFile(_owner, "Export Great Marsh Encounters",
                new[] { filter }, "great_marsh_encounters.bin");
            if (path == null) return;
            _file.ExportToFile(path);
        }

        public async Task ImportAsync()
        {
            if (_file == null) return;
            var filter = new FilePickerFileType("Binary files") { Patterns = new[] { "*.bin" } };
            string path = await DialogHelper.OpenFile(_owner, "Import Great Marsh Encounters", new[] { filter });
            if (path == null) return;

            try
            {
                if (_file.ImportFromFile(path))
                {
                    _suppress = true;
                    GroupNames.Clear();
                    foreach (var g in _file.Groups) GroupNames.Add(g.Name);
                    _suppress = false;

                    if (GroupNames.Count > 0) SelectedGroupIndex = 0;
                    SetDirty();
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
