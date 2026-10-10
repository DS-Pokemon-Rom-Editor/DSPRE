using Avalonia.Controls;
using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using DSPRE.Avalonia.Views.Shell;
using DSPRE.Csv;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>One of the 7 evolution slots shown in the Evolutions tab.</summary>
    public class EvolutionRowViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // Name arrays injected by parent ViewModel so ParamLabel can show actual names
        internal string[] ItemNames;
        internal string[] MoveNames;
        internal string[] PokemonNames;

        // Set by the parent VM when hg-engine is linked: MethodIndex then indexes into the real EVO_*
        // names read from the checkout instead of DSPRE's vanilla EvolutionMethod enum + LabelStore.
        internal bool UseHgEngineNames;
        internal string[] HgMethodNames;

        // hg-engine's target field packs a form override into its high bits (see HgEngineEvolutions).
        // No UI exposes changing this; it's only round-tripped so an existing one is never silently lost.
        internal int HgTargetFormId;

        private int _methodIndex;
        public int MethodIndex
        {
            get => _methodIndex;
            // A combo that loses its items (the tab unloading) reports -1; the stored method is kept.
            set { if (value >= 0 && _methodIndex != value) { _methodIndex = value; OnPropertyChanged(); OnPropertyChanged(nameof(ParamLabel)); OnPropertyChanged(nameof(IsParamEnabled)); OnPropertyChanged(nameof(ParamMaximum)); OnPropertyChanged(nameof(IsTargetEnabled)); Changed?.Invoke(); } }
        }

        private int _targetIndex;
        public int TargetIndex
        {
            get => _targetIndex;
            set
            {
                // The ComboBox reports -1 for a target it doesn't list; the stored value is kept and Save names it.
                if (value < 0) return;
                if (_targetIndex != value) { _targetIndex = value; OnPropertyChanged(); Changed?.Invoke(); }
            }
        }

        private int _param;
        public int Param
        {
            get => _param;
            set { if (_param != value) { _param = value; OnPropertyChanged(); OnPropertyChanged(nameof(ParamLabel)); Changed?.Invoke(); } }
        }

        /// <summary>Forces the bound method combo to re-resolve its displayed text after the label list was
        /// edited in place (Avalonia keeps a stale SelectedItem when the selected entry is replaced). Toggles
        /// the index via the backing field only, no data write, no Changed event.</summary>
        public void RefreshMethodDisplay()
        {
            if (_methodIndex < 0) return;
            int v = _methodIndex;
            _methodIndex = -1; OnPropertyChanged(nameof(MethodIndex));   // blank the combo this frame …
            // … then restore on a LATER frame so the ComboBox actually re-resolves its displayed item
            // (a synchronous -1→v toggle gets coalesced into one update and the stale text stays).
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _methodIndex = v; OnPropertyChanged(nameof(MethodIndex));
            }, global::Avalonia.Threading.DispatcherPriority.Background);
        }

        // The param's MEANING comes from the customisable LabelStore attribute (Tools ▸ Edit Dropdown
        // Labels ▸ Evolution Methods ▸ Parameter), defaulting to EvolutionFile.evoDescriptions. This lets a
        // ROM hack repurpose/add methods AND say what their parameter is (level / item / move / species…).
        // hg-engine mode has no equivalent per-index customisation store (the method list itself is read
        // live from the checkout, not user-curated), so it infers a meaning from the real EVO_* name instead.
        private EvolutionParamMeaning Meaning => MeaningFor(_methodIndex, UseHgEngineNames, HgMethodNames);

        internal static EvolutionParamMeaning MeaningFor(int method, bool hgEngine, string[] hgNames)
        {
            if (method < 0) return EvolutionParamMeaning.Ignored;
            if (hgEngine) return MeaningFromHgEngineName(hgNames != null && method < hgNames.Length ? hgNames[method] : null);
            return (EvolutionParamMeaning)DSPRE.Avalonia.Data.LabelStore.GetAttr("evolution_methods", method);
        }

        private static EvolutionParamMeaning MeaningFromHgEngineName(string name)
        {
            if (string.IsNullOrEmpty(name) || name == "EVO_NONE") return EvolutionParamMeaning.Ignored;
            if (name.Contains("LEVEL")) return EvolutionParamMeaning.FromLevel;
            if (name.Contains("ITEM") || name.Contains("STONE")) return EvolutionParamMeaning.ItemName;
            // EVO_HAS_MOVE_TYPE's param is a type id, not a move.
            if (name.Contains("MOVE_TYPE")) return EvolutionParamMeaning.CustomNumber;
            if (name.Contains("MOVE")) return EvolutionParamMeaning.MoveName;
            if (name.Contains("PARTY_MON") || name.Contains("TRADE_SPECIFIC_MON")) return EvolutionParamMeaning.PokemonName;
            if (name.Contains("BEAUTY")) return EvolutionParamMeaning.BeautyValue;
            return EvolutionParamMeaning.CustomNumber;
        }

        /// <summary>The target-species dropdown is disabled for a CustomNumber method: its parameter is a
        /// raw value and the evolution target is handled by the hack's own code, not a picked species.</summary>
        // hg-engine reads the target for every method except EVO_NONE.
        internal static bool NeedsTargetFor(int method, bool hgEngine, string[] hgNames)
        {
            EvolutionParamMeaning meaning = MeaningFor(method, hgEngine, hgNames);
            return hgEngine ? meaning != EvolutionParamMeaning.Ignored : meaning != EvolutionParamMeaning.CustomNumber;
        }

        public bool IsTargetEnabled => NeedsTargetFor(_methodIndex, UseHgEngineNames, HgMethodNames);

        /// <summary>Re-raises the parameter-display properties after the param meaning was customised.</summary>
        public void RefreshParam()
        {
            OnPropertyChanged(nameof(ParamLabel));
            OnPropertyChanged(nameof(IsParamEnabled));
            OnPropertyChanged(nameof(ParamMaximum));
            OnPropertyChanged(nameof(IsTargetEnabled));
        }

        public string ParamLabel
        {
            get
            {
                if (_methodIndex < 0) return string.Empty;
                EvolutionParamMeaning meaning = Meaning;
                switch (meaning)
                {
                    case EvolutionParamMeaning.FromLevel:
                        return $"From Level: {_param}";
                    case EvolutionParamMeaning.ItemName:
                        if (ItemNames != null && _param >= 0 && _param < ItemNames.Length)
                            return $"({ItemNames[_param]})";
                        return $"(Item #{_param})";
                    case EvolutionParamMeaning.MoveName:
                        if (MoveNames != null && _param >= 0 && _param < MoveNames.Length)
                            return $"({MoveNames[_param]})";
                        return $"(Move #{_param})";
                    case EvolutionParamMeaning.PokemonName:
                        if (PokemonNames != null && _param >= 0 && _param < PokemonNames.Length)
                            return $"({PokemonNames[_param]})";
                        return $"(Pokémon #{_param})";
                    case EvolutionParamMeaning.BeautyValue:
                        return $"Beauty >= {_param}";
                    case EvolutionParamMeaning.CustomNumber:
                        return $"Value: {_param}";
                    default:
                        return string.Empty;
                }
            }
        }

        public decimal ParamMaximum
        {
            get
            {
                if (_methodIndex < 0) return 65535;
                EvolutionParamMeaning meaning = Meaning;
                switch (meaning)
                {
                    case EvolutionParamMeaning.FromLevel:
                        return 100;
                    case EvolutionParamMeaning.ItemName:
                        return ItemNames != null ? ItemNames.Length - 1 : 65535;
                    case EvolutionParamMeaning.MoveName:
                        return MoveNames != null ? MoveNames.Length - 1 : 65535;
                    case EvolutionParamMeaning.PokemonName:
                        return PokemonNames != null ? PokemonNames.Length - 1 : 65535;
                    case EvolutionParamMeaning.BeautyValue:
                        return 255;
                    case EvolutionParamMeaning.CustomNumber:
                        return short.MaxValue;   // raw value within the param field's (short) range
                    default:
                        return 65535;
                }
            }
        }

        public bool IsParamEnabled
        {
            get
            {
                if (_methodIndex < 0) return false;
                return Meaning != EvolutionParamMeaning.Ignored;
            }
        }

        // Fired whenever any field changes so parent VM can mark dirty
        public Action Changed;
    }

    public class EvolutionsEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
            if (n != nameof(HasUnsavedChanges)) return;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImportNote)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasImportNote)));
        }

        public string ImportNote
        {
            get
            {
                int n = _pending.Count + (_heldRecord && _dirty ? 1 : 0);
                return n == 0 ? "" : $"{n} imported Pokémon {(n == 1 ? "has" : "have")} unsaved changes. Press Save to write them to disk.";
            }
        }
        public bool HasImportNote => ImportNote.Length > 0;
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(f, v)) return false;
            f = v; OnPropertyChanged(n); return true;
        }
        // ─── Lists ────────────────────────────────────────────────────────────────
        public ObservableCollection<string> MethodNames { get; } = new();
        public ObservableCollection<string> PokemonNames { get; } = new();

        private bool UseHgEngineSource => DSPRE.HgEngine.HgEngineProject.IsActive;
        private System.Collections.Generic.List<(string Name, int Value)> _hgMethodOptions = new();
        private string[] _hgMethodNamesArray = System.Array.Empty<string>();

        /// <summary>Vanilla: labels come from the customisable LabelStore (Tools ▸ Edit Dropdown Labels) so
        /// a ROM hack can rename/add methods, indices kept stable, combos refresh in place. hg-engine:
        /// the real EVO_* names are read live from the linked checkout and rebuilt outright instead,
        /// since that list isn't user-curated.</summary>
        private void ReloadMethodNames()
        {
            if (UseHgEngineSource)
            {
                _hgMethodOptions = DSPRE.HgEngine.HgEngineEvolutions.GetMethodOptions();
                _hgMethodNamesArray = new string[_hgMethodOptions.Count];
                for (int i = 0; i < _hgMethodOptions.Count; i++) _hgMethodNamesArray[i] = _hgMethodOptions[i].Name;

                MethodNames.Clear();
                foreach ((string Name, int Value) opt in _hgMethodOptions) MethodNames.Add(opt.Name);
            }
            else
            {
                DSPRE.Avalonia.Data.LabelStore.Sync(MethodNames, "evolution_methods");
            }

            foreach (EvolutionRowViewModel row in EvoRows)
            {
                row.UseHgEngineNames = UseHgEngineSource;
                row.HgMethodNames = _hgMethodNamesArray;
            }
        }

        private void OnLabelsChanged(object sender, EventArgs e)
        {
            ReloadMethodNames();
            foreach (EvolutionRowViewModel row in EvoRows) { row.RefreshMethodDisplay(); row.RefreshParam(); }   // un-blank + re-read param meaning
        }

        /// <summary>Unsubscribes from app-wide events; call when the host window closes.</summary>
        public void Detach()
        {
            AppEvents.LabelsChanged -= OnLabelsChanged;
            AppEvents.RomPatchStateChanged -= OnPatchStateChanged;
        }

        // One row per evolution slot the ROM has: 7 in the game, more with the toolbox patch or hg-engine.
        public ObservableCollection<EvolutionRowViewModel> EvoRows { get; } = new();

        /// <summary>The slots are the game's 7 and the toolbox patch can raise them.</summary>
        public bool CanOfferSlots => !UseHgEngineSource && EvolutionSlots.WhyNot() == null && !EvolutionSlots.Expanded;

        public void OpenSlotsPatch() => AvaloniaEditorLauncher.OpenPatchToolboxAt("evolutionSlots");

        // After the toolbox raised the slots, the new rows appear and the Pokémon is read again.
        private void OnPatchStateChanged(object sender, EventArgs e)
        {
            if (UseHgEngineSource) return;
            OnPropertyChanged(nameof(CanOfferSlots));
            if (EvolutionSlots.Current() <= EvoRows.Count) return;
            AddRows(EvolutionSlots.Current() - EvoRows.Count);
            if (_currentId >= 0 && !_dirty) LoadMon(_currentId);
        }

        private string[] _itemNames, _moveNames, _rowPokemonNames;

        private void AddRows(int count)
        {
            for (int i = 0; i < count; i++)
            {
                EvolutionRowViewModel row = new EvolutionRowViewModel
                {
                    ItemNames    = _itemNames,
                    MoveNames    = _moveNames,
                    PokemonNames = _rowPokemonNames,
                    UseHgEngineNames = UseHgEngineSource,
                    HgMethodNames    = _hgMethodNamesArray,
                    MethodIndex  = 0,
                    TargetIndex  = 0,
                    Param        = 0
                };
                row.Changed = () => { if (!_loading) SetDirty(); };
                EvoRows.Add(row);
            }
        }

        // ─── Dirty tracking ───────────────────────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty || _pending.Count > 0;
        public string UnsavedChangesDescription => _pending.Count > 0 ? $"Evolutions ({_pending.Count + (_dirty ? 1 : 0)} Pokémon)" : $"Evolutions (Mon {_currentId})";
        public void SaveChanges() => Save();
        public void DiscardChanges()
        {
            _pending.Clear();
            DiscardRecordEdits();
        }

        /// <summary>Drops the shown Pokémon's edits and keeps the other imported ones. The caller loads a Pokémon next.</summary>
        public void DiscardRecordEdits() { _heldRecord = false; _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        // Imported Pokémon waiting for Save. The shown one moves back here on switch, so switching doesn't prompt.
        private readonly Dictionary<int, EvolutionCsv.Slot[]> _pending = new Dictionary<int, EvolutionCsv.Slot[]>();
        private bool _heldRecord;

        /// <summary>Edits on the shown Pokémon that switching would lose.</summary>
        public bool HasRecordEdits => _dirty && !_heldRecord;

        /// <summary>Called before another Pokémon is loaded: keeps the shown one's imported edits.</summary>
        public void HoldForSwitch()
        {
            if (!_heldRecord) return;
            if (_dirty) _pending[_currentId] = CurrentSlots();
            _heldRecord = false;
            _dirty = false;
        }

        private int _currentId = -1;

        // Entries past the Pokedex are the alternate forms, whose evolution files are empty in every
        // game; the base Pokemon's own entry is what the game reads.
        private bool _isAltForm;
        public bool IsAltForm { get => _isAltForm; private set { if (_isAltForm != value) { _isAltForm = value; OnPropertyChanged(); } } }
        public string AltFormNotice => "Alternate forms have no evolutions of their own. Set them on the base Pokémon's entry instead.";
        private EvolutionFile _current;
        private bool _loading;

        // ── Undo / redo (ISupportsUndo) ────────────────────────────────────────
        // Snapshot = the per-row (method, param, target) values, lossless and independent of how Save()
        // compacts the file. Edit bursts within CoalesceMs collapse into one step.
        private readonly DSPRE.Avalonia.UndoHistory<(int, int, int, int)[]> _history = new();
        private DateTime _lastCaptureUtc = DateTime.MinValue;
        private const int CoalesceMs = 500;

        public bool CanUndo => _history.CanUndo;
        public bool CanRedo => _history.CanRedo;
        public void Undo() { if (_history.CanUndo) ApplyRows(_history.Undo()); }
        public void Redo() { if (_history.CanRedo) ApplyRows(_history.Redo()); }
        private void RaiseUndoState() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private (int, int, int, int)[] SnapshotRows()
        {
            (int, int, int, int)[] s = new (int, int, int, int)[EvoRows.Count];
            for (int i = 0; i < EvoRows.Count; i++)
                s[i] = (EvoRows[i].MethodIndex, EvoRows[i].Param, EvoRows[i].TargetIndex, EvoRows[i].HgTargetFormId);
            return s;
        }

        private void ApplyRows((int, int, int, int)[] s)
        {
            if (s == null) return;
            _loading = true;
            for (int i = 0; i < EvoRows.Count && i < s.Length; i++)
            {
                EvoRows[i].MethodIndex = s[i].Item1;
                EvoRows[i].Param       = s[i].Item2;
                EvoRows[i].TargetIndex = s[i].Item3;
                EvoRows[i].HgTargetFormId = s[i].Item4;
            }
            _loading = false;
            _dirty = _history.IsDirty;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            RaiseUndoState();
        }

        private void RecordUndoSnapshot()
        {
            bool coalesce = (DateTime.UtcNow - _lastCaptureUtc).TotalMilliseconds < CoalesceMs;
            _history.Capture(SnapshotRows(), coalesce);
            _lastCaptureUtc = DateTime.UtcNow;
            RaiseUndoState();
        }

        // ─── Design-time constructor ──────────────────────────────────────────────
        public EvolutionsEditorViewModel()
        {
            if (!Design.IsDesignMode) return;

            foreach (string n in Enum.GetNames<EvolutionMethod>()) MethodNames.Add(n);
            for (int i = 0; i < 10; i++) PokemonNames.Add($"Pokémon {i}");

            for (int i = 0; i < EvolutionFile.numEvolutions; i++)
            {
                EvoRows.Add(new EvolutionRowViewModel { MethodIndex = 0, TargetIndex = 0, Param = 0 });
            }
        }

        // ─── Runtime constructor ──────────────────────────────────────────────────
        public EvolutionsEditorViewModel(string[] pokemonNames)
        {
            _itemNames = RomInfo.GetItemNames();
            _moveNames = RomInfo.GetAttackNames();
            _rowPokemonNames = pokemonNames;

            ReloadMethodNames();
            // Only real species can be evolution targets; on hg-engine every entry is one.
            _lastTarget = UseHgEngineSource ? pokemonNames.Length - 1 : Math.Min(RomInfo.LastVanillaSpecies, pokemonNames.Length - 1);
            for (int i = 0; i <= _lastTarget; i++) PokemonNames.Add(pokemonNames[i]);

            // Live refresh: when dropdown labels are customised (Tools ▸ Edit Dropdown Labels), reload.
            AppEvents.LabelsChanged += OnLabelsChanged;
            AppEvents.RomPatchStateChanged += OnPatchStateChanged;

            // hg-engine sets its own slot count; Eevee alone needs eight.
            AddRows(UseHgEngineSource && DSPRE.HgEngine.HgEngineEvolutions.MaxSlots() is int max and > 0 ? max : EvolutionSlots.Current());
        }

        private string _hgLoadError;
        private int _lastTarget = int.MaxValue;

        // ─── Load ─────────────────────────────────────────────────────────────────
        public void LoadMon(int id)
        {
            _loading = true;
            try
            {
                _currentId = id;
                IsAltForm = id >= DSPRE.RomInfo.GetPokemonNames().Length;

                (EvolutionCsv.Slot[] slots, string error) = ReadSlots(id);
                // Saving rows that didn't load faithfully would overwrite the real entries.
                _hgLoadError = UseHgEngineSource ? error : null;
                if (!UseHgEngineSource) _current = new EvolutionFile();
                ShowSlots(slots);

                _dirty = false;
                OnPropertyChanged(nameof(HasUnsavedChanges));

                _history.Reset(SnapshotRows());   // loaded state is the clean undo baseline for this mon
                _lastCaptureUtc = DateTime.MinValue;
                RaiseUndoState();
            }
            finally { _loading = false; }

            // The import is one undo step on top of the saved data.
            _heldRecord = _pending.Remove(id, out EvolutionCsv.Slot[] held);
            if (_heldRecord) TakeImported(held);
        }

        /// <summary>A Pokémon's slots from the ROM, or on hg-engine from data/Evolutions.c, with why they can't be saved.</summary>
        private (EvolutionCsv.Slot[] Slots, string Error) ReadSlots(int id)
        {
            EvolutionCsv.Slot[] slots = new EvolutionCsv.Slot[EvoRows.Count];
            string error = null;
            if (UseHgEngineSource)
            {
                // Evolutions isn't synced from a packed NARC, so the vanilla read would show stale ROM data.
                if (!DSPRE.HgEngine.HgEngineEvolutions.TryGetEntries(id, EvoRows.Count, out List<HgEngineEvolutions.EvoEntry> hgEntries, out string loadError))
                    error = loadError;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (hgEntries == null || i >= hgEntries.Count) { slots[i] = new EvolutionCsv.Slot(NoneMethod, 0, 0, 0); continue; }
                    HgEngineEvolutions.EvoEntry e = hgEntries[i];
                    int idx = _hgMethodOptions.FindIndex(o => o.Value == e.MethodValue);
                    bool targetListed = e.TargetSpeciesId >= 0 && e.TargetSpeciesId < PokemonNames.Count;
                    if (e.Unresolved) error ??= $"Evolution {i + 1} could not be read: {e.RawText}";
                    if (idx < 0) error ??= $"Evolution {i + 1} uses method {e.MethodValue}, which this checkout doesn't name.";
                    if (!targetListed) error ??= $"Evolution {i + 1} targets species {e.TargetSpeciesId}, which isn't in the species list.";
                    slots[i] = new EvolutionCsv.Slot(idx >= 0 ? idx : 0, e.Param, targetListed ? e.TargetSpeciesId : 0, e.TargetFormId);
                }
            }
            else
            {
                EvolutionFile file = id > 0 ? new EvolutionFile(id) : new EvolutionFile();
                for (int i = 0; i < slots.Length; i++)
                {
                    EvolutionData d = file.data != null && i < file.data.Length ? file.data[i] : default;
                    slots[i] = new EvolutionCsv.Slot((int)d.method, d.param, d.target >= 0 ? d.target : 0, 0);
                }
            }
            return (slots, error);
        }

        private void ShowSlots(IReadOnlyList<EvolutionCsv.Slot> slots)
        {
            for (int i = 0; i < EvoRows.Count; i++)
            {
                EvolutionCsv.Slot s = i < slots.Count ? slots[i] : new EvolutionCsv.Slot(NoneMethod, 0, 0, 0);
                EvoRows[i].MethodIndex = s.Method;
                EvoRows[i].Param = s.Param;
                EvoRows[i].TargetIndex = s.Target;
                EvoRows[i].HgTargetFormId = s.Form;
            }
        }

        private void TakeImported(EvolutionCsv.Slot[] slots)
        {
            _loading = true;
            ShowSlots(slots);
            _loading = false;
            _lastCaptureUtc = DateTime.MinValue;
            RecordUndoSnapshot();
            _dirty = _history.IsDirty;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private EvolutionCsv.Slot[] CurrentSlots() => EvoRows.Select(r => new EvolutionCsv.Slot(r.MethodIndex, r.Param, r.TargetIndex, r.HgTargetFormId)).ToArray();

        private int NoneMethod => UseHgEngineSource ? Math.Max(0, _hgMethodOptions.FindIndex(o => o.Name == "EVO_NONE")) : (int)EvolutionMethod.None;

        // ─── CSV ──────────────────────────────────────────────────────────────────
        private EvolutionCsv.Setup CsvSetup() => new EvolutionCsv.Setup
        {
            Pokemon = _rowPokemonNames,
            Methods = MethodNames.ToArray(),
            // Earlier exports and the docs export wrote the code's own method names.
            MethodAliases = UseHgEngineSource ? null : Enum.GetValues<EvolutionMethod>().Select(m => ((int)(short)m, m.ToString())),
            NoneMethod = NoneMethod,
            LastTarget = _lastTarget,
            FirstForm = DSPRE.RomInfo.GetPokemonNames().Length,
            SlotCount = EvoRows.Count,
            Items = _itemNames,
            Moves = _moveNames,
            Meaning = m => EvolutionRowViewModel.MeaningFor(m, UseHgEngineSource, _hgMethodNamesArray),
            NeedsTarget = m => EvolutionRowViewModel.NeedsTargetFor(m, UseHgEngineSource, _hgMethodNamesArray),
            Current = id => id == _currentId ? (CurrentSlots(), _hgLoadError)
                : _pending.TryGetValue(id, out EvolutionCsv.Slot[] staged) ? (staged, null) : ReadSlots(id),
        };

        private int SpeciesCount => UseHgEngineSource ? _rowPokemonNames.Length : Math.Min(RomInfo.GetEvolutionFilesCount(), _rowPokemonNames.Length);

        /// <summary>Every Pokémon's evolutions as this editor holds them, unsaved edits included.</summary>
        public async System.Threading.Tasks.Task ExportCsvAsync(Window owner)
        {
            string path = await DialogHelper.SaveFile(owner, "Export evolutions CSV", new[] { DialogHelper.CsvFilter, DialogHelper.AllFilter }, "evolutions.csv");
            if (path == null) return;
            try
            {
                EvolutionCsv.Setup setup = CsvSetup();
                using System.IO.StreamWriter writer = new System.IO.StreamWriter(path);
                EvolutionCsv.Write(writer, setup, Enumerable.Range(0, SpeciesCount).Select(id => (id, (IReadOnlyList<EvolutionCsv.Slot>)setup.Current(id).Slots)));
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException)
            {
                await DialogHelper.ShowError($"Export failed:\n{ex.Message}", "Evolutions", owner);
            }
        }

        /// <summary>Replaces the evolutions of the Pokémon in the file. Changes stay unsaved until Save.</summary>
        public async System.Threading.Tasks.Task ImportCsvAsync(Window owner)
        {
            if (_currentId < 0) return;
            EvolutionCsv importer = new EvolutionCsv(CsvSetup());
            CsvImportSession session = await CsvImportReviewView.ReviewAsync(owner, importer);
            if (session == null) return;

            Dictionary<int, EvolutionCsv.Slot[]> result = importer.Result(session.Accepted);
            foreach ((int id, EvolutionCsv.Slot[] slots) in result)
            {
                if (id != _currentId) { _pending[id] = slots; continue; }
                TakeImported(slots);
                _heldRecord = true;
            }
            OnPropertyChanged(nameof(HasUnsavedChanges));
            SaveNotice.Show(result.Count == 1 ? "Evolutions imported for 1 Pokémon." : $"Evolutions imported for {result.Count} Pokémon.");
        }

        // ─── Save ─────────────────────────────────────────────────────────────────
        public void Save()
        {
            if (_currentId < 0) return;
            if (UseHgEngineSource) { _ = SaveHgEngineAsync(); return; }
            if (_current == null) return;

            foreach ((int id, EvolutionCsv.Slot[] slots) in _pending.OrderBy(kv => kv.Key).ToList())
            {
                string pendingProblem = BuildFile(slots, out EvolutionFile pendingFile);
                if (pendingProblem != null)
                {
                    _ = DSPRE.Avalonia.DialogHelper.ShowError($"Evolutions of {NameOf(id)} were not saved: {pendingProblem}.", "Evolutions");
                    return;
                }
                pendingFile.SaveToFileDefaultDir(id, showSuccessMessage: false);
                _pending.Remove(id);
            }

            string problem = BuildFile(CurrentSlots(), out EvolutionFile newFile);
            if (problem != null)
            {
                _ = DSPRE.Avalonia.DialogHelper.ShowError($"Evolutions were not saved: {problem}.", "Evolutions");
                return;
            }
            newFile.SaveToFileDefaultDir(_currentId, showSuccessMessage: false);
            _current = newFile;
            MarkSaved();
        }

        private string NameOf(int id) => id >= 0 && id < _rowPokemonNames.Length ? $"#{id} {_rowPokemonNames[id]}" : $"#{id}";

        /// <summary>The file for one Pokémon's slots, or why it can't be written.</summary>
        private string BuildFile(IReadOnlyList<EvolutionCsv.Slot> slots, out EvolutionFile file)
        {
            file = new EvolutionFile();
            List<EvolutionData> data = new List<EvolutionData>();
            for (int i = 0; i < slots.Count; i++)
            {
                EvolutionData ed = new EvolutionData
                {
                    method = (EvolutionMethod)slots[i].Method,
                    param  = (short)slots[i].Param,
                    target = (short)slots[i].Target
                };
                if (ed.method == EvolutionMethod.None) continue;
                bool needsTarget = EvolutionRowViewModel.NeedsTargetFor(slots[i].Method, false, null);
                string problem = ed.Problem(needsTarget);
                if (problem == null && needsTarget && slots[i].Target > _lastTarget)
                    problem = $"targets #{slots[i].Target}, which is not a species";
                if (problem != null) return $"evolution {i + 1} {problem}";
                data.Add(ed);
            }
            file.data = data.ToArray();
            return null;
        }

        async System.Threading.Tasks.Task<bool> IEditorWithUnsavedChanges.SaveChangesAsync()
        {
            if (UseHgEngineSource) await SaveHgEngineAsync();
            else Save();
            return !HasUnsavedChanges;
        }

        private async System.Threading.Tasks.Task SaveHgEngineAsync()
        {
            if (_currentId < 0) return;
            int species = _currentId;

            // Saving rows that didn't load faithfully would overwrite the real entries.
            if (_hgLoadError != null)
            {
                await DSPRE.Avalonia.DialogHelper.ShowError($"Evolutions were not saved:\n{_hgLoadError}", "Evolutions");
                return;
            }
            List<(int Species, List<(string MethodName, int Param, int TargetSpeciesId, int TargetFormId)> Entries)> work =
                _pending.OrderBy(kv => kv.Key).Select(kv => (kv.Key, SourceEntries(kv.Value))).ToList();
            work.Add((species, SourceEntries(CurrentSlots())));

            string failed = null;
            (bool saved, string error) = await DSPRE.Avalonia.HgEngineSave.RunAsync(() =>
            {
                foreach ((int id, List<(string MethodName, int Param, int TargetSpeciesId, int TargetFormId)> entries) in work)
                    if (!DSPRE.HgEngine.HgEngineEvolutions.TrySetEntries(id, entries, out string writeError)) { failed = NameOf(id); return writeError; }
                return null;
            });
            if (!saved)
            {
                if (error == null) return;
                AppLogger.Error($"hg-engine evolutions write failed for species {failed ?? species.ToString()}: {error}");
                await DSPRE.Avalonia.DialogHelper.ShowError($"Evolutions{(failed != null ? " of " + failed : "")} were not saved:\n{error}", "Evolutions");
                return;
            }
            foreach ((int id, _) in work) _pending.Remove(id);
            if (species == _currentId) MarkSaved();
            else OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private List<(string MethodName, int Param, int TargetSpeciesId, int TargetFormId)> SourceEntries(IEnumerable<EvolutionCsv.Slot> slots)
            => slots.Select(s => (s.Method >= 0 && s.Method < _hgMethodOptions.Count ? _hgMethodOptions[s.Method].Name : "EVO_NONE", s.Param, s.Target, s.Form)).ToList();

        private void MarkSaved()
        {
            _dirty = false;
            _heldRecord = false;
            SaveNotice.Saved(UnsavedChangesDescription);
            OnPropertyChanged(nameof(HasUnsavedChanges));
            _history.MarkSaved();
            RaiseUndoState();
        }

        private void SetDirty()
        {
            if (_loading) return;
            RecordUndoSnapshot();
            if (!_dirty) { _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        }
    }
}
