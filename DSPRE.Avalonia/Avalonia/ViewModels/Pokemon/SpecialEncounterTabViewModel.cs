using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Platform.Storage;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>A Special encounters tab over a <see cref="SpeciesSlotArchive"/> with several groups.</summary>
    public abstract class SpecialEncounterTabViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        protected bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private const string ErrorTitle = "Special encounters editor";

        private Window _owner;
        private bool _suppress;
        private SpeciesSlotArchive _file;

        protected abstract SpeciesSlotArchive LoadArchive();
        public abstract string UnsavedChangesDescription { get; }
        public abstract string ListHeading { get; }
        /// <summary>A line above the group picker, or empty.</summary>
        public virtual string SlotsNote => "";
        public virtual string SlotsNoteTip => "";
        public bool HasSlotsNote => !string.IsNullOrEmpty(SlotsNote);

        protected virtual string SlotLabel(int slot, SpeciesSlot entry) => $"Slot {slot:D2}: {entry}";
        protected virtual string SlotInfo(int slot) => slot < 0 ? "Slot: N/A" : $"Slot number: {slot:D2}";
        /// <summary>The canvas the big icon is drawn on; the view scales it to its box.</summary>
        protected virtual int IconSize => 64;
        /// <summary>Why the file can't be saved as it is, or null.</summary>
        protected virtual string SaveProblem(SpeciesSlotArchive file) => null;

        public ObservableCollection<string> GroupNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> EncounterSlots { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> SpeciesNames { get; } = new ObservableCollection<string>();

        private bool _isAvailable;
        public bool IsAvailable { get => _isAvailable; private set { if (Set(ref _isAvailable, value)) OnPropertyChanged(nameof(IsNotAvailable)); } }
        public bool IsNotAvailable => !_isAvailable;

        private string _groupDescription = "";
        public string GroupDescription { get => _groupDescription; set => Set(ref _groupDescription, value); }

        private string _slotInfoText;
        public string SlotInfoText { get => _slotInfoText ??= SlotInfo(-1); set => Set(ref _slotInfoText, value); }

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

        protected void DesignSample(string group, string slot)
        {
            IsAvailable = true;
            GroupNames.Add(group);
            EncounterSlots.Add(slot);
        }

        // ── Unsaved changes ──────────────────────────────────────────────────────
        private byte[] _saved;
        public bool HasUnsavedChanges => _file != null && !_file.ToBytes().AsSpan().SequenceEqual(_saved);
        public void SaveChanges() => DialogHelper.Watch(SaveChangesAsync());

        public void DiscardChanges()
        {
            if (_file == null) return;
            ShowState(_saved);
            ResetUndo();
        }

        // ── Undo / redo ──────────────────────────────────────────────────────────
        private ByteStateUndo _undo;
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();
        private void RaiseUndo() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        /// <summary>Puts <paramref name="state"/> in the file and redraws the group shown, keeping the slot.</summary>
        private void ShowState(byte[] state)
        {
            _file.CopyFrom(state);
            int slot = _selectedSlotIndex;
            RefreshGroupDisplay();
            if (slot >= 0 && slot < EncounterSlots.Count) { _selectedSlotIndex = -1; SelectedSlotIndex = slot; }
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void ResetUndo()
        {
            _undo = new ByteStateUndo(() => _file.ToBytes(), ShowState, RaiseUndo);
            RaiseUndo();
        }

        private void Edited()
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
            _undo?.Record();
        }

        // ── Setup ────────────────────────────────────────────────────────────────
        public async Task SetupAsync(Window owner)
        {
            _owner = owner;
            IsAvailable = SpeciesSlotArchive.IsAvailable();
            if (!IsAvailable) return;

            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.EncounterExtended });
            if (string.IsNullOrEmpty(Filesystem.EncounterExtended) || !Directory.Exists(Filesystem.EncounterExtended))
            {
                await DialogHelper.ShowErrorAsync($"arc/encdata_ex.narc is missing, so the {UnsavedChangesDescription} can't be shown.", ErrorTitle);
                IsAvailable = false;
                return;
            }

            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.MonIcons });
            SetMonIconsPalTableAddress();

            SpeciesNames.Clear();
            foreach (string n in GetPokemonNames()) SpeciesNames.Add(n);

            LoadFile();
        }

        private void LoadFile()
        {
            try
            {
                _file = LoadArchive();
                _saved = _file.ToBytes();
                ShowGroups();
                ResetUndo();
            }
            catch (Exception ex)
            {
                CrashReporter.LogHandled(ex);
                _ = DialogHelper.ShowErrorAsync($"Couldn't load the {UnsavedChangesDescription}: {ex.Message}", ErrorTitle);
            }
        }

        private void ShowGroups()
        {
            _suppress = true;
            GroupNames.Clear();
            foreach (SpeciesSlotGroup g in _file.Groups) GroupNames.Add(g.Name);
            _suppress = false;

            _selectedGroupIndex = -1;
            if (GroupNames.Count > 0) SelectedGroupIndex = 0;
        }

        public void RefreshNames()
        {
            if (SpeciesNames.Count == 0) return;
            _suppress = true;
            try { Data.ListSync.Apply(SpeciesNames, GetPokemonNames()); }
            finally { _suppress = false; }
            if (_file == null) return;
            int slot = _selectedSlotIndex;
            RefreshGroupDisplay();
            _selectedSlotIndex = -1;
            if (slot >= 0 && slot < EncounterSlots.Count) SelectedSlotIndex = slot;
            else if (EncounterSlots.Count > 0) SelectedSlotIndex = 0;
        }

        private void RefreshGroupDisplay()
        {
            if (_file == null || _selectedGroupIndex < 0 || _selectedGroupIndex >= _file.Groups.Count) return;
            SpeciesSlotGroup group = _file.Groups[_selectedGroupIndex];
            GroupDescription = group.Description;

            _suppress = true;
            EncounterSlots.Clear();
            for (int i = 0; i < group.Encounters.Count; i++) EncounterSlots.Add(SlotLabel(i, group.Encounters[i]));
            _suppress = false;

            // Reset first so slot 0 of the new list still loads its species.
            _selectedSlotIndex = -1;
            if (EncounterSlots.Count > 0) SelectedSlotIndex = 0;
            else { OnPropertyChanged(nameof(SelectedSlotIndex)); ClearFields(); }
        }

        private void LoadSlot(int slot)
        {
            if (_file == null || slot < 0) { ClearFields(); return; }
            SpeciesSlotGroup group = _file.Groups[_selectedGroupIndex];
            if (slot >= group.Encounters.Count) { ClearFields(); return; }

            SpeciesSlot enc = group.Encounters[slot];
            _suppress = true;
            SelectedSpeciesIndex = enc.Species < SpeciesNames.Count ? enc.Species : -1;
            SlotInfoText = SlotInfo(slot);
            _suppress = false;
            UpdateIcon(enc.Species);
        }

        private void ClearFields()
        {
            _suppress = true;
            SelectedSpeciesIndex = -1;
            PokemonIcon = null;
            SlotInfoText = SlotInfo(-1);
            _suppress = false;
        }

        private void OnSpeciesChanged(int species)
        {
            if (_file == null || _selectedGroupIndex < 0 || _selectedSlotIndex < 0 || species < 0) return;
            SpeciesSlotGroup group = _file.Groups[_selectedGroupIndex];
            if (_selectedSlotIndex >= group.Encounters.Count) return;

            group.Encounters[_selectedSlotIndex].Species = (ushort)species;
            Edited();

            int slot = _selectedSlotIndex;
            _suppress = true;
            EncounterSlots[slot] = SlotLabel(slot, group.Encounters[slot]);
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
                RawImage gdi = DSUtils.GetPokePicRaw(species, IconSize, IconSize);
                PokemonIcon = ImageConverter.ToAvaloniaBitmap(gdi);
            }
            catch (Exception ex) { AppLogger.WarnOnce($"{GetType().Name}.UpdateIcon", ex); PokemonIcon = null; }
        }

        // ── Save / export / import ─────────────────────────────────────────────────
        public async Task<bool> SaveChangesAsync()
        {
            if (_file == null) return true;
            if (SaveProblem(_file) is string problem)
            {
                await DialogHelper.ShowErrorAsync(problem, ErrorTitle, _owner);
                return false;
            }
            try { _file.Save(); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                CrashReporter.LogHandled(e);
                await DialogHelper.ShowErrorAsync($"The {UnsavedChangesDescription} weren't saved.\n{e.Message}", ErrorTitle, _owner);
                return false;
            }
            _saved = _file.ToBytes();
            OnPropertyChanged(nameof(HasUnsavedChanges));
            SaveNotice.Saved(UnsavedChangesDescription);
            return true;
        }

        private string FileName => UnsavedChangesDescription.ToLowerInvariant().Replace(' ', '_') + ".bin";

        public async Task ExportAsync()
        {
            if (_file == null) return;
            FilePickerFileType filter = new FilePickerFileType("Binary files") { Patterns = new[] { "*.bin" } };
            string path = await DialogHelper.SaveFileAsync(_owner, $"Export {UnsavedChangesDescription}", new[] { filter }, FileName);
            if (path == null) return;
            try { PlainFiles.WriteBytes(path, _file.ToBytes()); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                CrashReporter.LogHandled(e);
                await DialogHelper.ShowErrorAsync($"Couldn't export {UnsavedChangesDescription}: {e.Message}", ErrorTitle, _owner);
                return;
            }
            SaveNotice.Exported(path);
        }

        public async Task ImportAsync()
        {
            if (_file == null) return;
            FilePickerFileType filter = new FilePickerFileType("Binary files") { Patterns = new[] { "*.bin" } };
            string path = await DialogHelper.OpenFileAsync(_owner, $"Import {UnsavedChangesDescription}", new[] { filter });
            if (path == null) return;

            byte[] data;
            try { data = File.ReadAllBytes(path); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                CrashReporter.LogHandled(e);
                await DialogHelper.ShowErrorAsync($"Couldn't import {UnsavedChangesDescription}: {e.Message}", ErrorTitle, _owner);
                return;
            }
            _file.CopyFrom(data);
            ShowGroups();
            Edited();
            SaveNotice.Imported(UnsavedChangesDescription, path);
        }

        public void Locate()
        {
            string path = Filesystem.EncounterExtended;
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                SystemShell.RevealInFileManager(path);
        }
    }
}
