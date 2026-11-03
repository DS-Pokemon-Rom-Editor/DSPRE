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
        public bool NeedsReversion { get => _needsReversion; set { if (_needsReversion != value) { _needsReversion = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowRevertsTo)); Changed?.Invoke(); } } }

        private int _megaItem;
        public int MegaItem { get => _megaItem; set { if (value >= 0 && _megaItem != value) { _megaItem = value; OnPropertyChanged(); Changed?.Invoke(); } } }

        private int _megaMove;
        public int MegaMove { get => _megaMove; set { if (value >= 0 && _megaMove != value) { _megaMove = value; OnPropertyChanged(); Changed?.Invoke(); } } }

        private int _revertsTo;
        /// <summary>The form it goes back to when a battle ends, for a battle-only form.</summary>
        public int RevertsTo { get => _revertsTo; set { if (value >= 0 && _revertsTo != value) { _revertsTo = value; OnPropertyChanged(); Changed?.Invoke(); } } }

        /// <summary>Set when its species can have a row in FormReversionMapping.c.</summary>
        public bool CanRevert { get; set; }
        public bool ShowRevertsTo => CanRevert && NeedsReversion;
        public void RaiseRevert() => OnPropertyChanged(nameof(ShowRevertsTo));

        /// <summary>The form number it was loaded as, 0 for a new row. Removing a form renumbers the ones after it.</summary>
        public int LoadedForm;

        /// <summary>Fired on any edit so the owner can mark itself dirty.</summary>
        public System.Action Changed;
    }

    /// <summary>
    /// hg-engine only: the forms a Pokémon has in data/PokeFormDataTbl.c and their NEEDS_REVERSION flags.
    /// Form stats are edited in the Pokémon editor.
    /// </summary>
    public class HgEngineFormEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public string HgEngineBanner => HgEngineProject.BannerText;

        public ObservableCollection<string> PokemonNames { get; } = new();
        public ObservableCollection<FormSlotRow> Slots { get; } = new();
        public ObservableCollection<string> ItemNames { get; } = new();
        public ObservableCollection<string> MoveNames { get; } = new();

        /// <summary>Whether this checkout builds Mega Evolution.</summary>
        public bool MegaAvailable { get; }

        private readonly HgEngineSymbolTable _species;
        private Dictionary<string, List<HgEngineFormRegistry.FormSlot>> _table;
        private List<HgEngineMegaTables.Row> _megaItems = new(), _megaMoves = new();

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
            foreach (FormSlotRow row in Slots)
            {
                w.Write(row.PokemonIndex); w.Write(row.NeedsReversion);
                w.Write(row.MegaItem); w.Write(row.MegaMove); w.Write(row.LoadedForm); w.Write(row.RevertsTo);
            }
        });

        private void ApplyState(byte[] state)
        {
            Slots.Clear();
            ByteStateUndo.Unpack(state, r =>
            {
                for (int n = r.ReadInt32(), i = 0; i < n; i++) AddRow(r.ReadInt32(), r.ReadBoolean(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32());
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
                ? $"Forms of {PokemonNames[_selectedSpeciesIndex]}" : "Forms";

        private string _statusText = "";
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
            MegaAvailable = HgEngineMegaTables.Available;
            if (MegaAvailable)
            {
                foreach (string n in RomInfo.GetItemNames()) ItemNames.Add(n);
                foreach (string n in RomInfo.GetAttackNames()) MoveNames.Add(n);
                LoadMega();
            }
            LoadReversion();
            _selectedSpeciesIndex = 1;
            LoadSelected();
        }

        private Dictionary<string, HgEngineFormReversion.Row> _reversion;

        private void LoadReversion()
        {
            _reversion = HgEngineFormReversion.Available && HgEngineFormReversion.TryRead(out Dictionary<string, HgEngineFormReversion.Row> rows, out string error)
                ? rows : null;
        }

        // FormReversionMapping.c only has rows for species from SPECIES_MEGA_START on.
        private bool CanRevert(int pokemonIndex) =>
            _reversion != null && _species != null && _species.TryGetValue("SPECIES_MEGA_START", out int start) && pokemonIndex >= start;

        private string SymbolOf(int id) => _species != null && _species.TryGetNameWithPrefix(id, "SPECIES_", out string n) ? n : null;

        private int RevertsTo(int pokemonIndex) =>
            _reversion != null && SymbolOf(pokemonIndex) is string s && _reversion.TryGetValue(s, out HgEngineFormReversion.Row row) ? row.Form : 0;

        private void LoadMega()
        {
            HgEngineMegaTables.TryRead(HgEngineMegaTables.StoneTable, out _megaItems, out _, out _);
            HgEngineMegaTables.TryRead(HgEngineMegaTables.MoveTable, out _megaMoves, out _, out _);
        }

        // A form's number is its place in the species' list, starting at 1. The first row shown stands for the form.
        private int MegaTrigger(List<HgEngineMegaTables.Row> table, int form) =>
            table.FirstOrDefault(r => r.Species == _selectedSpeciesIndex && r.TargetForm == form)?.Trigger ?? 0;

        private void LoadSelected()
        {
            Slots.Clear();
            string designator = SelectedSpeciesDesignator;
            if (designator != null && _table.TryGetValue(designator, out List<HgEngineFormRegistry.FormSlot> slots))
            {
                int form = 0;
                foreach (HgEngineFormRegistry.FormSlot slot in slots)
                {
                    int id = _species != null && _species.TryGetValue(slot.SpeciesSymbol, out int v) ? v : -1;
                    form++;
                    AddRow(id, slot.NeedsReversion, MegaTrigger(_megaItems, form), MegaTrigger(_megaMoves, form), form, RevertsTo(id));
                }
            }
            ShowFormCount();
            StartUndo();
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void ShowFormCount() => StatusText = Slots.Count > 0
            ? $"{Plural.Of(Slots.Count, "form")} found in PokeFormDataTbl.c."
            : "No forms registered for this species in PokeFormDataTbl.c.";

        private void AddRow(int pokemonIndex, bool needsReversion, int megaItem = 0, int megaMove = 0, int loadedForm = 0, int revertsTo = 0)
        {
            FormSlotRow row = new FormSlotRow
            {
                PokemonIndex = pokemonIndex < 0 ? 0 : pokemonIndex, NeedsReversion = needsReversion,
                MegaItem = megaItem, MegaMove = megaMove, LoadedForm = loadedForm, RevertsTo = revertsTo,
                CanRevert = CanRevert(pokemonIndex),
            };
            row.Changed = () =>
            {
                bool can = CanRevert(row.PokemonIndex);
                if (row.CanRevert != can) { row.CanRevert = can; row.RaiseRevert(); }
                SetDirty();
            };   // wired after construction, so populating initial values here never marks dirty
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

        public void SaveChanges() => DialogHelper.Watch(SaveAsync());

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
                if (symbol == null) continue;   // unresolved: skipped so nothing wrong is written
                desired.Add(new HgEngineFormRegistry.FormSlot(row.NeedsReversion, symbol));
            }

            int species = _selectedSpeciesIndex;
            List<HgEngineMegaTables.Row> items = MegaRows(_megaItems, r => r.MegaItem);
            List<HgEngineMegaTables.Row> moves = MegaRows(_megaMoves, r => r.MegaMove);
            Dictionary<string, int> reversion = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (FormSlotRow row in Slots)
                if (row.CanRevert && SymbolOf(row.PokemonIndex) is string s) reversion[s] = row.NeedsReversion ? row.RevertsTo : RevertsTo(row.PokemonIndex);
            (bool saved, string error) = await DSPRE.Avalonia.HgEngineSave.RunAsync(() =>
            {
                if (!HgEngineFormRegistry.TrySaveSpeciesForms(species, desired, out string writeError)) return writeError;
                if (reversion.Count > 0 && !HgEngineFormReversion.TryWrite(reversion, out writeError)) return writeError;
                if (!MegaAvailable) return null;
                if (!HgEngineMegaTables.TryWrite(HgEngineMegaTables.StoneTable, species, items, out writeError)) return writeError;
                return HgEngineMegaTables.TryWrite(HgEngineMegaTables.MoveTable, species, moves, out writeError) ? null : writeError;
            });
            if (!saved)
            {
                if (error == null) return;
                StatusText = $"Couldn't save: {error}";
                AppLogger.Error($"hg-engine form registry write failed for species {species}: {error}");
                return;
            }

            _table = HgEngineFormRegistry.LoadAll();   // refresh from disk so future selections see the new state
            if (MegaAvailable) LoadMega();
            LoadReversion();
            for (int i = 0; i < Slots.Count; i++) Slots[i].LoadedForm = i + 1;
            _loaded = TakeState();
            ShowFormCount();
            SaveNotice.Saved(UnsavedChangesDescription);
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        /// <summary>The species' rows of a Mega table as the form list now has them. A form with two rows
        /// (Greninja from either base form) changes trigger on all of them, and removing a form renumbers the rest.</summary>
        private List<HgEngineMegaTables.Row> MegaRows(List<HgEngineMegaTables.Row> table, Func<FormSlotRow, int> trigger)
        {
            Dictionary<int, int> number = new Dictionary<int, int>();
            for (int i = 0; i < Slots.Count; i++) if (Slots[i].LoadedForm > 0) number[Slots[i].LoadedForm] = i + 1;

            List<HgEngineMegaTables.Row> rows = new List<HgEngineMegaTables.Row>();
            for (int i = 0; i < Slots.Count; i++)
            {
                int wanted = trigger(Slots[i]), form = i + 1;
                if (wanted == 0) continue;
                List<HgEngineMegaTables.Row> old = table.Where(r => r.Species == _selectedSpeciesIndex && r.TargetForm == Slots[i].LoadedForm).ToList();
                if (old.Count == 0) { rows.Add(new HgEngineMegaTables.Row(_selectedSpeciesIndex, wanted, form, 0)); continue; }
                foreach (HgEngineMegaTables.Row row in old)
                    rows.Add(row with { Trigger = row.Trigger == old[0].Trigger ? wanted : row.Trigger, TargetForm = form,
                        BaseForm = number.TryGetValue(row.BaseForm, out int moved) ? moved : row.BaseForm });
            }
            return rows;
        }

        public void DiscardChanges() => LoadSelected();
    }
}
