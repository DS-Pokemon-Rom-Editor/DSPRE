using Avalonia.Controls;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
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
            set { if (_methodIndex != value) { _methodIndex = value; OnPropertyChanged(); OnPropertyChanged(nameof(ParamLabel)); OnPropertyChanged(nameof(IsParamEnabled)); OnPropertyChanged(nameof(ParamMaximum)); OnPropertyChanged(nameof(IsTargetEnabled)); Changed?.Invoke(); } }
        }

        private int _targetIndex;
        public int TargetIndex
        {
            get => _targetIndex;
            set { if (_targetIndex != value) { _targetIndex = value; OnPropertyChanged(); Changed?.Invoke(); } }
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
        private EvolutionParamMeaning Meaning
        {
            get
            {
                if (_methodIndex < 0) return EvolutionParamMeaning.Ignored;
                if (UseHgEngineNames) return MeaningFromHgEngineName();
                return (EvolutionParamMeaning)DSPRE.Avalonia.Data.LabelStore.GetAttr("evolution_methods", _methodIndex);
            }
        }

        private EvolutionParamMeaning MeaningFromHgEngineName()
        {
            string name = HgMethodNames != null && _methodIndex < HgMethodNames.Length ? HgMethodNames[_methodIndex] : null;
            if (string.IsNullOrEmpty(name) || name == "EVO_NONE") return EvolutionParamMeaning.Ignored;
            if (name.Contains("LEVEL")) return EvolutionParamMeaning.FromLevel;
            if (name.Contains("ITEM") || name.Contains("STONE")) return EvolutionParamMeaning.ItemName;
            if (name.Contains("MOVE")) return EvolutionParamMeaning.MoveName;
            if (name.Contains("PARTY_MON") || name.Contains("TRADE_SPECIFIC_MON")) return EvolutionParamMeaning.PokemonName;
            if (name.Contains("BEAUTY")) return EvolutionParamMeaning.BeautyValue;
            return EvolutionParamMeaning.CustomNumber;
        }

        /// <summary>The target-species dropdown is disabled for a CustomNumber method: its parameter is a
        /// raw value and the evolution target is handled by the hack's own code, not a picked species.</summary>
        public bool IsTargetEnabled => Meaning != EvolutionParamMeaning.CustomNumber;

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
                var meaning = Meaning;
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
                var meaning = Meaning;
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
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
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
                foreach (var opt in _hgMethodOptions) MethodNames.Add(opt.Name);
            }
            else
            {
                DSPRE.Avalonia.Data.LabelStore.Sync(MethodNames, "evolution_methods");
            }

            foreach (var row in EvoRows)
            {
                row.UseHgEngineNames = UseHgEngineSource;
                row.HgMethodNames = _hgMethodNamesArray;
            }
        }

        private void OnLabelsChanged(object sender, EventArgs e)
        {
            ReloadMethodNames();
            foreach (var row in EvoRows) { row.RefreshMethodDisplay(); row.RefreshParam(); }   // un-blank + re-read param meaning
        }

        /// <summary>Unsubscribes from app-wide events; call when the host window closes.</summary>
        public void Detach() => AppEvents.LabelsChanged -= OnLabelsChanged;

        // 7 evolution slots
        public ObservableCollection<EvolutionRowViewModel> EvoRows { get; } = new();

        // ─── Dirty tracking ───────────────────────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => $"Evolutions (Mon {_currentId})";
        public void SaveChanges() => Save();
        public void DiscardChanges() { _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        private int _currentId = -1;
        private EvolutionFile _current;
        private bool _loading;

        // ── Undo / redo (ISupportsUndo) ────────────────────────────────────────
        // Snapshot = the per-row (method, param, target) values, lossless and independent of how Save()
        // compacts the file. Edit bursts within CoalesceMs collapse into one step.
        private readonly DSPRE.Avalonia.UndoHistory<(int, int, int)[]> _history = new();
        private DateTime _lastCaptureUtc = DateTime.MinValue;
        private const int CoalesceMs = 500;

        public bool CanUndo => _history.CanUndo;
        public bool CanRedo => _history.CanRedo;
        public void Undo() { if (_history.CanUndo) ApplyRows(_history.Undo()); }
        public void Redo() { if (_history.CanRedo) ApplyRows(_history.Redo()); }
        private void RaiseUndoState() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private (int, int, int)[] SnapshotRows()
        {
            var s = new (int, int, int)[EvoRows.Count];
            for (int i = 0; i < EvoRows.Count; i++)
                s[i] = (EvoRows[i].MethodIndex, EvoRows[i].Param, EvoRows[i].TargetIndex);
            return s;
        }

        private void ApplyRows((int, int, int)[] s)
        {
            if (s == null) return;
            _loading = true;
            for (int i = 0; i < EvoRows.Count && i < s.Length; i++)
            {
                EvoRows[i].MethodIndex = s[i].Item1;
                EvoRows[i].Param       = s[i].Item2;
                EvoRows[i].TargetIndex = s[i].Item3;
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

            foreach (var n in Enum.GetNames<EvolutionMethod>()) MethodNames.Add(n);
            for (int i = 0; i < 10; i++) PokemonNames.Add($"Pokémon {i}");

            for (int i = 0; i < EvolutionFile.numEvolutions; i++)
            {
                EvoRows.Add(new EvolutionRowViewModel { MethodIndex = 0, TargetIndex = 0, Param = 0 });
            }
        }

        // ─── Runtime constructor ──────────────────────────────────────────────────
        public EvolutionsEditorViewModel(string[] pokemonNames)
        {
            string[] itemNames = RomInfo.GetItemNames();
            string[] moveNames = RomInfo.GetAttackNames();

            ReloadMethodNames();
            foreach (var n in pokemonNames) PokemonNames.Add(n);

            // Live refresh: when dropdown labels are customised (Tools ▸ Edit Dropdown Labels), reload.
            AppEvents.LabelsChanged += OnLabelsChanged;

            for (int i = 0; i < EvolutionFile.numEvolutions; i++)
            {
                var row = new EvolutionRowViewModel
                {
                    ItemNames    = itemNames,
                    MoveNames    = moveNames,
                    PokemonNames = pokemonNames,
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

        // ─── Load ─────────────────────────────────────────────────────────────────
        public void LoadMon(int id)
        {
            _loading = true;
            try
            {
                _currentId = id;

                if (UseHgEngineSource)
                {
                    // Evolutions isn't synced from a packed NARC, so the vanilla read path below would
                    // show stale ROM data instead of the checkout's real data/Evolutions.c.
                    DSPRE.HgEngine.HgEngineEvolutions.TryGetEntries(id, EvolutionFile.numEvolutions, out var hgEntries, out _);
                    for (int i = 0; i < EvolutionFile.numEvolutions; i++)
                    {
                        var row = EvoRows[i];
                        if (i < hgEntries.Count)
                        {
                            var e = hgEntries[i];
                            int idx = _hgMethodOptions.FindIndex(o => o.Value == e.MethodValue);
                            row.MethodIndex = idx >= 0 ? idx : 0;
                            row.Param       = e.Param;
                            row.TargetIndex = e.TargetSpeciesId >= 0 && e.TargetSpeciesId < PokemonNames.Count ? e.TargetSpeciesId : 0;
                            row.HgTargetFormId = e.TargetFormId;
                        }
                        else
                        {
                            row.MethodIndex = 0; row.Param = 0; row.TargetIndex = 0; row.HgTargetFormId = 0;
                        }
                    }
                }
                else
                {
                    _current = id > 0 ? new EvolutionFile(id) : new EvolutionFile();
                    if (_current.data == null)
                        _current.data = new EvolutionData[EvolutionFile.numEvolutions];

                    for (int i = 0; i < EvolutionFile.numEvolutions; i++)
                    {
                        var row = EvoRows[i];
                        var d = i < _current.data.Length ? _current.data[i] : default;
                        row.MethodIndex = (int)d.method;
                        row.Param       = d.param;
                        row.TargetIndex = d.target >= 0 ? d.target : 0;
                    }
                }

                _dirty = false;
                OnPropertyChanged(nameof(HasUnsavedChanges));

                _history.Reset(SnapshotRows());   // loaded state is the clean undo baseline for this mon
                _lastCaptureUtc = DateTime.MinValue;
                RaiseUndoState();
            }
            finally { _loading = false; }
        }

        // ─── Save ─────────────────────────────────────────────────────────────────
        public void Save()
        {
            if (_currentId < 0) return;

            if (UseHgEngineSource)
            {
                var uiEntries = new System.Collections.Generic.List<(string MethodName, int Param, int TargetSpeciesId, int TargetFormId)>(EvoRows.Count);
                foreach (var row in EvoRows)
                {
                    string methodName = row.MethodIndex >= 0 && row.MethodIndex < _hgMethodOptions.Count
                        ? _hgMethodOptions[row.MethodIndex].Name : "EVO_NONE";
                    uiEntries.Add((methodName, row.Param, row.TargetIndex, row.HgTargetFormId));
                }
                if (!DSPRE.HgEngine.HgEngineEvolutions.TrySetEntries(_currentId, uiEntries, out string error))
                    AppLogger.Error($"hg-engine evolutions write failed for species {_currentId}: {error}");
            }
            else
            {
                if (_current == null) return;
                var newFile = new EvolutionFile();
                var data = new System.Collections.Generic.List<EvolutionData>();

                foreach (var row in EvoRows)
                {
                    var method = (EvolutionMethod)row.MethodIndex;
                    var ed = new EvolutionData
                    {
                        method = method,
                        param  = (short)row.Param,
                        target = (short)row.TargetIndex
                    };
                    if (ed.isValid()) data.Add(ed);
                }

                newFile.data = data.ToArray();
                newFile.SaveToFileDefaultDir(_currentId, showSuccessMessage: false);
                _current = newFile;
            }

            _dirty = false;
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
