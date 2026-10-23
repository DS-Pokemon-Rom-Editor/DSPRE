using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using global::Avalonia.Media;
using DSPRE.Avalonia.Data;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Trainers
{
    /// <summary>
    /// The Battle Tower's trainers (class, messages and the sets they draw from) and the shared pool of
    /// Pokémon sets.
    /// </summary>
    public class BattleTowerEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private BattleTowerTrainerFile _trainerFile;
        private BattleTowerPokemonSetFile _setFile;
        private readonly PokemonIconCache _icons = new();
        private bool _suppress;
        private bool _isDirty;

        public bool IsAvailable => BattleTowerTrainerFile.IsAvailable() && BattleTowerPokemonSetFile.IsAvailable();

        public ObservableCollection<string> TrainerClassNames { get; } = new();
        public ObservableCollection<string> PokemonNames { get; } = new();
        public ObservableCollection<string> MoveNames { get; } = new();
        public ObservableCollection<string> ItemNames { get; } = new();
        public ObservableCollection<string> NatureNames { get; } = new();

        private int _activeTabIndex;
        public int ActiveTabIndex { get => _activeTabIndex; set => Set(ref _activeTabIndex, value); }

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        // ── IEditorWithUnsavedChanges ──
        public bool HasUnsavedChanges => _isDirty;
        public string UnsavedChangesDescription => "Battle Tower trainers and Pokémon sets";
        public void SaveChanges()
        {
            if (_trainerFile == null || _setFile == null) return;
            // Saves both files, then reports any failure once; a failure leaves the edits unsaved.
            string trainersError = _trainerFile.Save();
            string setsError = _setFile.Save();
            if (trainersError != null || setsError != null)
            {
                string message = trainersError != null && setsError != null
                    ? (trainersError == setsError ? "The trainers and Pokémon sets weren't saved:\n" + trainersError
                        : "The trainers weren't saved:\n" + trainersError + "\n\nThe Pokémon sets weren't saved:\n" + setsError)
                    : trainersError != null ? "The trainers weren't saved:\n" + trainersError : "The Pokémon sets weren't saved:\n" + setsError;
                _ = DialogHelper.ShowErrorAsync(message, "Battle Tower editor");
                return;
            }
            _isDirty = false;
            _undo?.MarkSaved();
            SaveNotice.Saved(UnsavedChangesDescription);
            OnPropertyChanged(nameof(HasUnsavedChanges));
            UpdateStatus();
        }

        public void DiscardChanges()
        {
            if (_isDirty && _trainerFile != null && _setFile != null)
            {
                int trainer = Math.Max(0, _selectedTrainerIndex), set = Math.Max(0, _selectedSetIndex);
                _setFile = new BattleTowerPokemonSetFile(true);
                _trainerFile = new BattleTowerTrainerFile(true);
                RefreshTrainerList(trainer);
                RefreshSetList(set);
                // The list may keep the same index, which skips the setter's reload.
                LoadTrainer(_selectedTrainerIndex);
                LoadSet(_selectedSetIndex);
                OnPropertyChanged(nameof(AddSetPreviewLabel));
            }
            _isDirty = false;
            ResetUndo();
            OnPropertyChanged(nameof(HasUnsavedChanges));
            UpdateStatus();
        }

        // ── Undo / redo: both files, trainers and sets ──
        private sealed record TowerState(List<BattleTowerTrainer> Trainers, List<BattleTowerPokemonSet> Sets);
        private DSPRE.Avalonia.ByteStateUndo _undo;
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();
        private void RaiseUndo() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private byte[] TakeState() => DSPRE.Avalonia.UndoJson.Take(new TowerState(_trainerFile.Trainers.ToList(), _setFile.Sets.ToList()));

        private void ApplyState(byte[] state)
        {
            TowerState s = DSPRE.Avalonia.UndoJson.Read<TowerState>(state);
            int trainer = Math.Max(0, _selectedTrainerIndex), set = Math.Max(0, _selectedSetIndex);
            _trainerFile.Trainers.Clear();
            foreach (BattleTowerTrainer t in s.Trainers) _trainerFile.Trainers.Add(t);
            _setFile.Sets.Clear();
            foreach (BattleTowerPokemonSet p in s.Sets) _setFile.Sets.Add(p);
            RefreshTrainerList(trainer);
            RefreshSetList(set);
            // The list may keep the same index, which skips the setter's reload.
            LoadTrainer(_selectedTrainerIndex);
            LoadSet(_selectedSetIndex);
            OnPropertyChanged(nameof(AddSetPreviewLabel));
            _isDirty = _undo.IsDirty;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            UpdateStatus();
        }

        private void ResetUndo()
        {
            if (_trainerFile == null || _setFile == null) return;
            _undo = new DSPRE.Avalonia.ByteStateUndo(TakeState, ApplyState, RaiseUndo);
            RaiseUndo();
        }

        public void LocateActive() { if (ActiveTabIndex == 1) LocateSets(); else LocateTrainers(); }

        // ── Trainers tab ─────────────────────────────────────────────────
        public ObservableCollection<string> TrainerLabels { get; } = new();

        private int _selectedTrainerIndex = -1;
        public int SelectedTrainerIndex
        {
            get => _selectedTrainerIndex;
            set { if (Set(ref _selectedTrainerIndex, value) && !_suppress) LoadTrainer(value); }
        }

        private int _trainerClassIndex = -1;
        public int TrainerClassIndex
        {
            get => _trainerClassIndex;
            set
            {
                // A cleared list reports -1; an out-of-range class keeps its value as read.
                if (Set(ref _trainerClassIndex, value) && !_suppress && value >= 0)
                {
                    CurrentTrainer.TrainerType = (ushort)value;
                    CurrentTrainer.UnreadBytes = null;
                    MarkDirty();
                }
            }
        }

        private string _trainerName = "";
        public string TrainerName
        {
            get => _trainerName;
            set { if (Set(ref _trainerName, value) && !_suppress) { CurrentTrainer.Name = value; MarkDirty(); } }
        }

        private string _message1 = "", _message2 = "", _message3 = "";
        // The \r each message ends with is kept out of the boxes and put back on save.
        private readonly string[] _messageEnds = { "", "", "" };
        public string Message1 { get => _message1; set { if (Set(ref _message1, value) && !_suppress) { CurrentTrainer.Messages[0] = MessageMarkup.ToRaw(value, _messageEnds[0]); MarkDirty(); } } }
        public string Message2 { get => _message2; set { if (Set(ref _message2, value) && !_suppress) { CurrentTrainer.Messages[1] = MessageMarkup.ToRaw(value, _messageEnds[1]); MarkDirty(); } } }
        public string Message3 { get => _message3; set { if (Set(ref _message3, value) && !_suppress) { CurrentTrainer.Messages[2] = MessageMarkup.ToRaw(value, _messageEnds[2]); MarkDirty(); } } }

        private string ShowMessage(BattleTowerTrainer trainer, int i)
        {
            _messageEnds[i] = "";
            return trainer.Messages.Length > i ? MessageMarkup.ToDisplay(trainer.Messages[i], out _messageEnds[i]) : "";
        }

        public ObservableCollection<string> SetIdLabels { get; } = new();

        private int _selectedSetIdIndex = -1;
        public int SelectedSetIdIndex { get => _selectedSetIdIndex; set => Set(ref _selectedSetIdIndex, value); }

        private int _addSetNumber;
        public int AddSetNumber
        {
            get => _addSetNumber;
            set { if (value >= 0 && Set(ref _addSetNumber, value)) OnPropertyChanged(nameof(AddSetPreviewLabel)); }
        }

        public string AddSetPreviewLabel => $"({SetLabelFor(AddSetNumber)})";

        public bool IsTrainerSelected => CurrentTrainer != null;
        private BattleTowerTrainer CurrentTrainer =>
            (_trainerFile != null && _selectedTrainerIndex >= 0 && _selectedTrainerIndex < _trainerFile.Trainers.Count)
                ? _trainerFile.Trainers[_selectedTrainerIndex] : null;

        // ── Sets tab ─────────────────────────────────────────────────────
        public ObservableCollection<string> SetLabels { get; } = new();

        private int _selectedSetIndex = -1;
        public int SelectedSetIndex
        {
            get => _selectedSetIndex;
            set { if (Set(ref _selectedSetIndex, value) && !_suppress) LoadSet(value); }
        }

        private int _setSpeciesIndex = -1;
        public int SetSpeciesIndex { get => _setSpeciesIndex; set { if (!Set(ref _setSpeciesIndex, value)) return; RebuildForms(); if (!_suppress) SetFieldChanged(SetField.Species); } }

        /// <summary>The picked species' form names; a form with no name keeps the number box.</summary>
        public ObservableCollection<string> FormNames { get; } = new ObservableCollection<string>();
        public bool ShowFormNames => FormNames.Count > 1 && _form < FormNames.Count;
        public bool ShowFormNumber => !ShowFormNames && _form != 0;
        public int FormIndex { get => !_formBlank && _form < FormNames.Count ? _form : -1; set { if (value >= 0 && !_formBlank) Form = value; } }
        private bool _formBlank, _formRepushPending;

        private void RebuildForms()
        {
            string[] names = _setSpeciesIndex > 0 ? PokemonForms.NamesOf(_setSpeciesIndex) : null;
            DSPRE.Avalonia.Data.ListSync.Apply(FormNames, names ?? new[] { "Normal" });
            RaiseForms();
            // Rewriting the names drops the box's pick after this returns, so the pick is pushed again: blank, then the form.
            if (_formRepushPending) return;
            _formRepushPending = true;
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _formBlank = true; OnPropertyChanged(nameof(FormIndex));
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() => { _formBlank = false; _formRepushPending = false; OnPropertyChanged(nameof(FormIndex)); },
                    global::Avalonia.Threading.DispatcherPriority.Background);
            }, global::Avalonia.Threading.DispatcherPriority.Background);
        }

        private void RaiseForms()
        {
            OnPropertyChanged(nameof(FormIndex)); OnPropertyChanged(nameof(ShowFormNames)); OnPropertyChanged(nameof(ShowFormNumber));
        }

        private int _move1 = -1, _move2 = -1, _move3 = -1, _move4 = -1;
        public int Move1 { get => _move1; set { if (Set(ref _move1, value) && !_suppress) SetFieldChanged(SetField.Move1); } }
        public int Move2 { get => _move2; set { if (Set(ref _move2, value) && !_suppress) SetFieldChanged(SetField.Move2); } }
        public int Move3 { get => _move3; set { if (Set(ref _move3, value) && !_suppress) SetFieldChanged(SetField.Move3); } }
        public int Move4 { get => _move4; set { if (Set(ref _move4, value) && !_suppress) SetFieldChanged(SetField.Move4); } }

        private int _natureIndex;
        public int NatureIndex { get => _natureIndex; set { if (Set(ref _natureIndex, value) && !_suppress) SetFieldChanged(SetField.Nature); } }

        private int _itemIndex;
        public int ItemIndex { get => _itemIndex; set { if (Set(ref _itemIndex, value) && !_suppress) SetFieldChanged(SetField.Item); } }

        private int _form;
        public int Form { get => _form; set { if (!Set(ref _form, value)) return; RaiseForms(); if (!_suppress) SetFieldChanged(SetField.Form); } }

        private bool _evHp, _evAtk, _evDef, _evSpe, _evSpa, _evSpd;
        public bool EvHp { get => _evHp; set { if (Set(ref _evHp, value) && !_suppress) SetFieldChanged(SetField.Evs); } }
        public bool EvAtk { get => _evAtk; set { if (Set(ref _evAtk, value) && !_suppress) SetFieldChanged(SetField.Evs); } }
        public bool EvDef { get => _evDef; set { if (Set(ref _evDef, value) && !_suppress) SetFieldChanged(SetField.Evs); } }
        public bool EvSpe { get => _evSpe; set { if (Set(ref _evSpe, value) && !_suppress) SetFieldChanged(SetField.Evs); } }
        public bool EvSpa { get => _evSpa; set { if (Set(ref _evSpa, value) && !_suppress) SetFieldChanged(SetField.Evs); } }
        public bool EvSpd { get => _evSpd; set { if (Set(ref _evSpd, value) && !_suppress) SetFieldChanged(SetField.Evs); } }

        private IImage _speciesIcon;
        public IImage SpeciesIcon { get => _speciesIcon; private set => Set(ref _speciesIcon, value); }

        public bool IsSetSelected => CurrentSet != null;
        private BattleTowerPokemonSet CurrentSet =>
            (_setFile != null && _selectedSetIndex >= 0 && _selectedSetIndex < _setFile.Sets.Count)
                ? _setFile.Sets[_selectedSetIndex] : null;

        public BattleTowerEditorViewModel()
        {
            if (!IsAvailable)
            {
                StatusText = "Battle Tower data wasn't found for this game.";
                return;
            }

            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.BattleTowerTrainers, DirNames.BattleTowerPokemon });
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.MonIcons });
            SetMonIconsPalTableAddress();

            _setFile = new BattleTowerPokemonSetFile(true);
            _trainerFile = new BattleTowerTrainerFile(true);

            foreach (string n in GetTrainerClassNames()) TrainerClassNames.Add(n);
            foreach (string n in GetPokemonNames()) PokemonNames.Add(n);
            foreach (string n in GetAttackNames()) MoveNames.Add(n);
            foreach (string n in GetItemNames()) ItemNames.Add(n);
            foreach (string n in BattleTowerPokemonSet.NatureNames) NatureNames.Add(n);

            RefreshTrainerList();
            RefreshSetList();
            OnPropertyChanged(nameof(AddSetPreviewLabel));
            ResetUndo();
            UpdateStatus();
        }

        public void RefreshNames()
        {
            if (_trainerFile == null || _setFile == null) return;
            int trainer = _selectedTrainerIndex, set = _selectedSetIndex;
            DSPRE.Avalonia.Data.ListSync.Apply(TrainerClassNames, GetTrainerClassNames());
            DSPRE.Avalonia.Data.ListSync.Apply(PokemonNames, GetPokemonNames());
            DSPRE.Avalonia.Data.ListSync.Apply(MoveNames, GetAttackNames());
            DSPRE.Avalonia.Data.ListSync.Apply(ItemNames, GetItemNames());
            _suppress = true;
            DSPRE.Avalonia.Data.ListSync.Apply(TrainerLabels, _trainerFile.Trainers.Select((t, i) => $"[{i:D3}] {t}").ToList());
            DSPRE.Avalonia.Data.ListSync.Apply(SetLabels, _setFile.Sets.Select((s, i) => SetRow(i)).ToList());
            if (CurrentTrainer != null)
                DSPRE.Avalonia.Data.ListSync.Apply(SetIdLabels, CurrentTrainer.SetIDs.Select(id => SetRow(id)).ToList());
            _suppress = false;
            OnPropertyChanged(nameof(AddSetPreviewLabel));
            // A list box drops its pick when the picked row's text is replaced, after this returns.
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => { SelectedTrainerIndex = trainer; SelectedSetIndex = set; },
                global::Avalonia.Threading.DispatcherPriority.Background);
        }

        private void MarkDirty()
        {
            _undo?.Record();
            _isDirty = _undo?.IsDirty ?? true;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            UpdateStatus();
        }

        // ── Trainers tab logic ───────────────────────────────────────────
        private void RefreshTrainerList(int selectIndex = 0)
        {
            _suppress = true;
            TrainerLabels.Clear();
            for (int i = 0; i < _trainerFile.Trainers.Count; i++)
                TrainerLabels.Add($"[{i:D3}] {_trainerFile.Trainers[i]}");
            _suppress = false;

            if (TrainerLabels.Count > 0)
                SelectedTrainerIndex = Math.Min(selectIndex, TrainerLabels.Count - 1);
        }

        private void LoadTrainer(int index)
        {
            _suppress = true;
            BattleTowerTrainer trainer = CurrentTrainer;
            if (trainer == null)
            {
                TrainerClassIndex = -1;
                TrainerName = "";
                Message1 = Message2 = Message3 = "";
                SetIdLabels.Clear();
                _suppress = false;
                OnPropertyChanged(nameof(IsTrainerSelected));
                return;
            }

            TrainerClassIndex = trainer.TrainerType < TrainerClassNames.Count ? trainer.TrainerType : -1;
            TrainerName = trainer.Name;
            Message1 = ShowMessage(trainer, 0);
            Message2 = ShowMessage(trainer, 1);
            Message3 = ShowMessage(trainer, 2);
            RefreshSetIdsList(trainer);
            _suppress = false;
            OnPropertyChanged(nameof(IsTrainerSelected));
        }

        private void RefreshSetIdsList(BattleTowerTrainer trainer)
        {
            SetIdLabels.Clear();
            foreach (ushort id in trainer.SetIDs)
                SetIdLabels.Add(SetRow(id));
        }

        private string SetLabelFor(int id) => (_setFile != null && id >= 0 && id < _setFile.Sets.Count) ? _setFile.Sets[id].ToString() : "?";

        private string SetRow(int id) =>
            _setFile != null && id >= 0 && id < _setFile.Sets.Count && _setFile.Sets[id].Species == 0 ? $"Set {id} (empty)" : $"Set {id}: {SetLabelFor(id)}";

        public void NewTrainer()
        {
            _trainerFile.Trainers.Add(new BattleTowerTrainer());
            RefreshTrainerList(_trainerFile.Trainers.Count - 1);
            MarkDirty();
        }

        public void AddSetToTrainer()
        {
            BattleTowerTrainer trainer = CurrentTrainer;
            if (trainer == null) return;
            int setId = AddSetNumber;
            if (setId <= 0) return; // set 0 is the blank/unused placeholder entry
            trainer.SetIDs.Add((ushort)setId);
            trainer.UnreadBytes = null;
            RefreshSetIdsList(trainer);
            MarkDirty();
        }

        public void RemoveSetFromTrainer()
        {
            BattleTowerTrainer trainer = CurrentTrainer;
            if (trainer == null || SelectedSetIdIndex < 0 || SelectedSetIdIndex >= trainer.SetIDs.Count) return;
            trainer.SetIDs.RemoveAt(SelectedSetIdIndex);
            trainer.UnreadBytes = null;
            RefreshSetIdsList(trainer);
            MarkDirty();
        }

        /// <summary>Double-click a set-ID row in the trainer's list: jump to that set on the Sets tab.</summary>
        public void NavigateToSetId()
        {
            BattleTowerTrainer trainer = CurrentTrainer;
            if (trainer == null || SelectedSetIdIndex < 0 || SelectedSetIdIndex >= trainer.SetIDs.Count) return;
            int setId = trainer.SetIDs[SelectedSetIdIndex];
            if (setId < 0 || setId >= SetLabels.Count) return;
            ActiveTabIndex = 1;
            SelectedSetIndex = setId;
        }

        public void ExportTrainers(string path) { if (_trainerFile != null && _trainerFile.ExportToFile(path, showSuccessMessage: false)) SaveNotice.Exported(path); }

        public void ImportTrainers(string path)
        {
            if (_trainerFile != null && _trainerFile.ImportFromFile(path))
            {
                RefreshTrainerList();
                MarkDirty();
                SaveNotice.Imported("Battle Tower trainers", path);
            }
        }

        public void LocateTrainers()
        {
            string path = Filesystem.BattleTowerTrainers;
            if (!string.IsNullOrEmpty(path) && System.IO.Directory.Exists(path))
                SystemShell.RevealInFileManager(path);
        }

        // ── Sets tab logic ───────────────────────────────────────────────
        private void RefreshSetList(int selectIndex = 0)
        {
            _suppress = true;
            SetLabels.Clear();
            for (int i = 0; i < _setFile.Sets.Count; i++)
                SetLabels.Add(SetRow(i));
            _suppress = false;

            if (SetLabels.Count > 0)
                SelectedSetIndex = Math.Min(selectIndex, SetLabels.Count - 1);
        }

        private void LoadSet(int index)
        {
            _suppress = true;
            BattleTowerPokemonSet set = CurrentSet;
            if (set == null)
            {
                SpeciesIcon = null;
                _suppress = false;
                OnPropertyChanged(nameof(IsSetSelected));
                return;
            }

            SetSpeciesIndex = set.Species < PokemonNames.Count ? set.Species : -1;
            Move1 = set.Moves[0] < MoveNames.Count ? set.Moves[0] : -1;
            Move2 = set.Moves[1] < MoveNames.Count ? set.Moves[1] : -1;
            Move3 = set.Moves[2] < MoveNames.Count ? set.Moves[2] : -1;
            Move4 = set.Moves[3] < MoveNames.Count ? set.Moves[3] : -1;
            NatureIndex = set.Nature < NatureNames.Count ? set.Nature : -1;
            ItemIndex = set.Item < ItemNames.Count ? set.Item : -1;
            Form = set.Form;

            EvHp = (set.EvFlags & 0x01) != 0;
            EvAtk = (set.EvFlags & 0x02) != 0;
            EvDef = (set.EvFlags & 0x04) != 0;
            EvSpe = (set.EvFlags & 0x08) != 0;
            EvSpa = (set.EvFlags & 0x10) != 0;
            EvSpd = (set.EvFlags & 0x20) != 0;

            SpeciesIcon = _icons.Get(set.Species);
            _suppress = false;
            OnPropertyChanged(nameof(IsSetSelected));
        }

        private enum SetField { Species, Move1, Move2, Move3, Move4, Nature, Item, Form, Evs }

        // Only the edited field is written, so a value with no list row keeps its value as read.
        // A list reports -1 when cleared, which is not an edit.
        private void SetFieldChanged(SetField field)
        {
            BattleTowerPokemonSet set = CurrentSet;
            if (set == null) return;

            switch (field)
            {
                case SetField.Species:
                    if (SetSpeciesIndex < 0) return;
                    set.Species = (ushort)SetSpeciesIndex;
                    SpeciesIcon = _icons.Get(set.Species);
                    break;
                case SetField.Move1: if (Move1 < 0) return; set.Moves[0] = (ushort)Move1; break;
                case SetField.Move2: if (Move2 < 0) return; set.Moves[1] = (ushort)Move2; break;
                case SetField.Move3: if (Move3 < 0) return; set.Moves[2] = (ushort)Move3; break;
                case SetField.Move4: if (Move4 < 0) return; set.Moves[3] = (ushort)Move4; break;
                case SetField.Nature: if (NatureIndex < 0) return; set.Nature = (byte)NatureIndex; break;
                case SetField.Item: if (ItemIndex < 0) return; set.Item = (ushort)ItemIndex; break;
                case SetField.Form: set.Form = (ushort)Form; break;
                case SetField.Evs:
                    byte flags = (byte)(set.EvFlags & ~0x3F);
                    if (EvHp) flags |= 0x01;
                    if (EvAtk) flags |= 0x02;
                    if (EvDef) flags |= 0x04;
                    if (EvSpe) flags |= 0x08;
                    if (EvSpa) flags |= 0x10;
                    if (EvSpd) flags |= 0x20;
                    set.EvFlags = flags;
                    break;
            }
            set.UnreadBytes = null;

            int index = _selectedSetIndex;
            if (index >= 0 && index < SetLabels.Count)
            {
                // Replacing the row clears the list's selection, which would leave later edits with no set.
                _suppress = true;
                SetLabels[index] = SetRow(index);
                _selectedSetIndex = index;
                _suppress = false;
                OnPropertyChanged(nameof(SelectedSetIndex));
            }

            MarkDirty();
        }

        public void NewSet()
        {
            _setFile.Sets.Add(new BattleTowerPokemonSet());
            RefreshSetList(_setFile.Sets.Count - 1);
            MarkDirty();
        }

        public void ExportSets(string path) { if (_setFile != null && _setFile.ExportToFile(path, showSuccessMessage: false)) SaveNotice.Exported(path); }

        public void ImportSets(string path)
        {
            if (_setFile != null && _setFile.ImportFromFile(path))
            {
                RefreshSetList();
                MarkDirty();
                SaveNotice.Imported("Battle Tower Pokémon sets", path);
            }
        }

        public void LocateSets()
        {
            string path = Filesystem.BattleTowerPokemon;
            if (!string.IsNullOrEmpty(path) && System.IO.Directory.Exists(path))
                SystemShell.RevealInFileManager(path);
        }

        private void UpdateStatus() =>
            StatusText = "";
    }
}
