using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using DSPRE.HgEngine;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>One form-slot row: which species this base Pokémon can turn into, and whether that
    /// change needs the NEEDS_REVERSION flag (reverts back to base under specific conditions: Mega
    /// Evolution, Primal Reversion, and a few late-gen Terastal/other forms all use it).</summary>
    public class FormSlotRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private int _pokemonIndex;
        public int PokemonIndex { get => _pokemonIndex; set { if (_pokemonIndex != value) { _pokemonIndex = value; OnPropertyChanged(); Changed?.Invoke(); } } }

        private bool _needsReversion;
        public bool NeedsReversion { get => _needsReversion; set { if (_needsReversion != value) { _needsReversion = value; OnPropertyChanged(); Changed?.Invoke(); } } }

        /// <summary>Fired on any edit so the owning VM can mark itself dirty without a full event-hookup dance.</summary>
        public System.Action Changed;
    }

    /// <summary>
    /// hg-engine-only editor for data/PokeFormDataTbl.c: which form species exist for a base Pokémon
    /// (Mega Evolutions, regional forms, Gmax, etc.) and each one's NEEDS_REVERSION flag. Forms
    /// themselves (base stats, types, abilities...) are edited through the normal Pokémon Editor; this
    /// editor only manages which form entries exist and how they're flagged, not their contents.
    /// </summary>
    public class HgEngineFormEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public string HgEngineBanner => HgEngineProject.BannerText;

        public ObservableCollection<string> PokemonNames { get; } = new();
        public ObservableCollection<FormSlotRow> Slots { get; } = new();

        private readonly HgEngineSymbolTable _species;
        private Dictionary<string, List<HgEngineFormRegistry.FormSlot>> _table;

        private int _selectedSpeciesIndex = -1;
        public int SelectedSpeciesIndex
        {
            get => _selectedSpeciesIndex;
            set
            {
                if (RecordSwitchGuard.IsSnappingBack || _selectedSpeciesIndex == value) return;
                if (HasUnsavedChanges)
                {
                    int requested = value;
                    RecordSwitchGuard.SnapBack(() => _selectedSpeciesIndex, v => _selectedSpeciesIndex = v, () => OnPropertyChanged(nameof(SelectedSpeciesIndex)));
                    _ = SwitchSpeciesAsync(requested);
                    return;
                }
                _selectedSpeciesIndex = value; OnPropertyChanged(); LoadSelected();
            }
        }

        private async System.Threading.Tasks.Task SwitchSpeciesAsync(int requested)
        {
            if (!await RecordSwitchGuard.ConfirmLeaveAsync(this, null, "species")) return;
            _selectedSpeciesIndex = requested;
            OnPropertyChanged(nameof(SelectedSpeciesIndex));
            LoadSelected();
        }

        public string SelectedSpeciesDesignator =>
            _selectedSpeciesIndex >= 0 && _species != null && _species.TryGetNameWithPrefix(_selectedSpeciesIndex, "SPECIES_", out string n) ? n : null;

        private byte[] _loaded;
        public bool HasUnsavedChanges => _loaded != null && !TakeState().AsSpan().SequenceEqual(_loaded);

        // One species at a time: its history starts when it is shown.
        private ByteStateUndo _undo;
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();

        private byte[] TakeState() => ByteStateUndo.Pack(w =>
        {
            w.Write(Slots.Count);
            foreach (FormSlotRow row in Slots) { w.Write(row.PokemonIndex); w.Write(row.NeedsReversion); }
        });

        private void ApplyState(byte[] state)
        {
            Slots.Clear();
            ByteStateUndo.Unpack(state, r =>
            {
                for (int n = r.ReadInt32(), i = 0; i < n; i++) AddRow(r.ReadInt32(), r.ReadBoolean());
            });
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void StartUndo()
        {
            _loaded = TakeState();
            _undo = new ByteStateUndo(TakeState, ApplyState, () => { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); });
            OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo));
        }
        public string UnsavedChangesDescription =>
            _selectedSpeciesIndex >= 0 && _selectedSpeciesIndex < PokemonNames.Count
                ? $"Form Editor ({PokemonNames[_selectedSpeciesIndex]})" : "Form Editor";

        private string _statusText = "Select a species to view or edit its forms.";
        public string StatusText { get => _statusText; set { if (_statusText != value) { _statusText = value; OnPropertyChanged(); } } }

        // ── Design-time constructor ──────────────────────────────────────────
        public HgEngineFormEditorViewModel()
        {
            if (!global::Avalonia.Controls.Design.IsDesignMode) return;
            for (int i = 0; i < 20; i++) PokemonNames.Add($"Species {i:D3}");
        }

        // ── Runtime constructor ──────────────────────────────────────────────
        public HgEngineFormEditorViewModel(string[] pokemonNames)
        {
            foreach (string n in pokemonNames) PokemonNames.Add(n);
            _species = HgEngineSymbolTable.Load("include/constants/species.h");
            _table = HgEngineFormRegistry.LoadAll();
            _selectedSpeciesIndex = 1;
            LoadSelected();
        }

        private void LoadSelected()
        {
            Slots.Clear();
            string designator = SelectedSpeciesDesignator;
            if (designator != null && _table.TryGetValue(designator, out List<HgEngineFormRegistry.FormSlot> slots))
            {
                foreach (HgEngineFormRegistry.FormSlot slot in slots)
                {
                    int id = _species != null && _species.TryGetValue(slot.SpeciesSymbol, out int v) ? v : -1;
                    AddRow(id, slot.NeedsReversion);
                }
            }
            StatusText = Slots.Count > 0
                ? $"{Slots.Count} form(s) found in PokeFormDataTbl.c."
                : "No forms registered for this species in PokeFormDataTbl.c.";
            StartUndo();
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void AddRow(int pokemonIndex, bool needsReversion)
        {
            FormSlotRow row = new FormSlotRow { PokemonIndex = pokemonIndex < 0 ? 0 : pokemonIndex, NeedsReversion = needsReversion };
            row.Changed = SetDirty;   // wired after construction, so populating initial values here never marks dirty
            Slots.Add(row);
        }

        public void AddSlot()
        {
            AddRow(_selectedSpeciesIndex >= 0 ? _selectedSpeciesIndex : 0, false);
            SetDirty();
        }

        public void RemoveSlot(FormSlotRow row)
        {
            if (Slots.Remove(row)) SetDirty();
        }

        private void SetDirty()
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
            _undo?.Record();
        }

        public void SaveChanges() => _ = SaveAsync();

        async System.Threading.Tasks.Task<bool> IEditorWithUnsavedChanges.SaveChangesAsync()
        {
            await SaveAsync();
            return !HasUnsavedChanges;
        }

        public async System.Threading.Tasks.Task SaveAsync()
        {
            if (_selectedSpeciesIndex < 0 || _species == null) return;
            string SymbolFor(int id) => _species.TryGetNameWithPrefix(id, "SPECIES_", out string n) ? n : null;

            List<HgEngineFormRegistry.FormSlot> desired = new List<HgEngineFormRegistry.FormSlot>();
            foreach (FormSlotRow row in Slots)
            {
                string symbol = SymbolFor(row.PokemonIndex);
                if (symbol == null) continue;   // couldn't resolve, skip rather than write garbage
                desired.Add(new HgEngineFormRegistry.FormSlot(row.NeedsReversion, symbol));
            }

            int species = _selectedSpeciesIndex;
            (bool saved, string error) = await DSPRE.Avalonia.HgEngineSave.RunAsync(() =>
                HgEngineFormRegistry.TrySaveSpeciesForms(species, desired, out string writeError) ? null : writeError);
            if (!saved)
            {
                if (error == null) return;
                StatusText = $"Save failed: {error}";
                AppLogger.Error($"hg-engine form registry write failed for species {species}: {error}");
                return;
            }

            _table = HgEngineFormRegistry.LoadAll();   // refresh from disk so future selections see the new state
            _loaded = TakeState();
            SaveNotice.Saved(UnsavedChangesDescription);
            StatusText = $"Saved {desired.Count} form(s) for {(SelectedSpeciesIndex < PokemonNames.Count ? PokemonNames[SelectedSpeciesIndex] : SelectedSpeciesIndex.ToString())}.";
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        public void DiscardChanges() => LoadSelected();
    }
}
