using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using global::Avalonia.Media;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>
    /// Avalonia port of the WinForms <c>BattleTowerEditor</c>: the Battle Tower's trainer roster
    /// (class + rematch messages + which Pokémon sets they can draw from) and the shared pool of
    /// Pokémon sets those trainers reference by index.
    /// </summary>
    public class BattleTowerEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
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
        public string UnsavedChangesDescription => "Battle Tower Editor";
        public void SaveChanges() { SaveTrainers(); SaveSets(); }
        public void DiscardChanges() { _isDirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

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
            set { if (Set(ref _trainerClassIndex, value) && !_suppress) { CurrentTrainer.TrainerType = (ushort)Math.Max(0, value); MarkDirty(); } }
        }

        private string _trainerName = "";
        public string TrainerName
        {
            get => _trainerName;
            set { if (Set(ref _trainerName, value) && !_suppress) { CurrentTrainer.Name = value; MarkDirty(); } }
        }

        private string _message1 = "", _message2 = "", _message3 = "";
        public string Message1 { get => _message1; set { if (Set(ref _message1, value) && !_suppress) { CurrentTrainer.Messages[0] = value; MarkDirty(); } } }
        public string Message2 { get => _message2; set { if (Set(ref _message2, value) && !_suppress) { CurrentTrainer.Messages[1] = value; MarkDirty(); } } }
        public string Message3 { get => _message3; set { if (Set(ref _message3, value) && !_suppress) { CurrentTrainer.Messages[2] = value; MarkDirty(); } } }

        public ObservableCollection<string> SetIdLabels { get; } = new();

        private int _selectedSetIdIndex = -1;
        public int SelectedSetIdIndex { get => _selectedSetIdIndex; set => Set(ref _selectedSetIdIndex, value); }

        private int _addSetNumber;
        public int AddSetNumber
        {
            get => _addSetNumber;
            set { if (Set(ref _addSetNumber, value)) OnPropertyChanged(nameof(AddSetPreviewLabel)); }
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
        public int SetSpeciesIndex { get => _setSpeciesIndex; set { if (Set(ref _setSpeciesIndex, value) && !_suppress) SetFieldChanged(species: true); } }

        private int _move1 = -1, _move2 = -1, _move3 = -1, _move4 = -1;
        public int Move1 { get => _move1; set { if (Set(ref _move1, value) && !_suppress) SetFieldChanged(); } }
        public int Move2 { get => _move2; set { if (Set(ref _move2, value) && !_suppress) SetFieldChanged(); } }
        public int Move3 { get => _move3; set { if (Set(ref _move3, value) && !_suppress) SetFieldChanged(); } }
        public int Move4 { get => _move4; set { if (Set(ref _move4, value) && !_suppress) SetFieldChanged(); } }

        private int _natureIndex;
        public int NatureIndex { get => _natureIndex; set { if (Set(ref _natureIndex, value) && !_suppress) SetFieldChanged(); } }

        private int _itemIndex;
        public int ItemIndex { get => _itemIndex; set { if (Set(ref _itemIndex, value) && !_suppress) SetFieldChanged(); } }

        private int _form;
        public int Form { get => _form; set { if (Set(ref _form, value) && !_suppress) SetFieldChanged(); } }

        private bool _evHp, _evAtk, _evDef, _evSpe, _evSpa, _evSpd;
        public bool EvHp { get => _evHp; set { if (Set(ref _evHp, value) && !_suppress) SetFieldChanged(); } }
        public bool EvAtk { get => _evAtk; set { if (Set(ref _evAtk, value) && !_suppress) SetFieldChanged(); } }
        public bool EvDef { get => _evDef; set { if (Set(ref _evDef, value) && !_suppress) SetFieldChanged(); } }
        public bool EvSpe { get => _evSpe; set { if (Set(ref _evSpe, value) && !_suppress) SetFieldChanged(); } }
        public bool EvSpa { get => _evSpa; set { if (Set(ref _evSpa, value) && !_suppress) SetFieldChanged(); } }
        public bool EvSpd { get => _evSpd; set { if (Set(ref _evSpd, value) && !_suppress) SetFieldChanged(); } }

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
                StatusText = "Battle Tower data was not found for this game.";
                return;
            }

            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.battleTowerTrainers, DirNames.battleTowerPokemon });
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.monIcons });
            SetMonIconsPalTableAddress();

            _setFile = new BattleTowerPokemonSetFile(true);
            _trainerFile = new BattleTowerTrainerFile(true);

            foreach (var n in GetTrainerClassNames()) TrainerClassNames.Add(n);
            foreach (var n in GetPokemonNames()) PokemonNames.Add(n);
            foreach (var n in GetAttackNames()) MoveNames.Add(n);
            foreach (var n in GetItemNames()) ItemNames.Add(n);
            foreach (var n in BattleTowerPokemonSet.NatureNames) NatureNames.Add(n);

            RefreshTrainerList();
            RefreshSetList();
            OnPropertyChanged(nameof(AddSetPreviewLabel));
            UpdateStatus();
        }

        private void MarkDirty() { _isDirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); UpdateStatus(); }

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
            var trainer = CurrentTrainer;
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
            Message1 = trainer.Messages.Length > 0 ? trainer.Messages[0] : "";
            Message2 = trainer.Messages.Length > 1 ? trainer.Messages[1] : "";
            Message3 = trainer.Messages.Length > 2 ? trainer.Messages[2] : "";
            RefreshSetIdsList(trainer);
            _suppress = false;
            OnPropertyChanged(nameof(IsTrainerSelected));
        }

        private void RefreshSetIdsList(BattleTowerTrainer trainer)
        {
            SetIdLabels.Clear();
            foreach (ushort id in trainer.SetIDs)
                SetIdLabels.Add($"Set {id:D3}: {SetLabelFor(id)}");
        }

        private string SetLabelFor(int id) => (_setFile != null && id >= 0 && id < _setFile.Sets.Count) ? _setFile.Sets[id].ToString() : "?";

        public void NewTrainer()
        {
            _trainerFile.Trainers.Add(new BattleTowerTrainer());
            RefreshTrainerList(_trainerFile.Trainers.Count - 1);
            MarkDirty();
        }

        public void AddSetToTrainer()
        {
            var trainer = CurrentTrainer;
            if (trainer == null) return;
            int setId = AddSetNumber;
            if (setId <= 0) return; // set 0 is the blank/unused placeholder entry
            trainer.SetIDs.Add((ushort)setId);
            RefreshSetIdsList(trainer);
            MarkDirty();
        }

        public void RemoveSetFromTrainer()
        {
            var trainer = CurrentTrainer;
            if (trainer == null || SelectedSetIdIndex < 0 || SelectedSetIdIndex >= trainer.SetIDs.Count) return;
            trainer.SetIDs.RemoveAt(SelectedSetIdIndex);
            RefreshSetIdsList(trainer);
            MarkDirty();
        }

        /// <summary>Double-click a set-ID row in the trainer's list: jump to that set on the Sets tab.</summary>
        public void NavigateToSetId()
        {
            var trainer = CurrentTrainer;
            if (trainer == null || SelectedSetIdIndex < 0 || SelectedSetIdIndex >= trainer.SetIDs.Count) return;
            int setId = trainer.SetIDs[SelectedSetIdIndex];
            if (setId < 0 || setId >= SetLabels.Count) return;
            ActiveTabIndex = 1;
            SelectedSetIndex = setId;
        }

        public void SaveTrainers()
        {
            if (_trainerFile == null) return;
            _trainerFile.SaveToNarc();
            _isDirty = false;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            UpdateStatus();
        }

        public void ExportTrainers(string path) => _trainerFile?.ExportToFile(path);

        public void ImportTrainers(string path)
        {
            if (_trainerFile != null && _trainerFile.ImportFromFile(path))
            {
                RefreshTrainerList();
                MarkDirty();
            }
        }

        public void LocateTrainers()
        {
            string path = Filesystem.battleTowerTrainers;
            if (!string.IsNullOrEmpty(path) && System.IO.Directory.Exists(path))
                SystemShell.RevealInFileManager(path);
        }

        // ── Sets tab logic ───────────────────────────────────────────────
        private void RefreshSetList(int selectIndex = 0)
        {
            _suppress = true;
            SetLabels.Clear();
            for (int i = 0; i < _setFile.Sets.Count; i++)
                SetLabels.Add($"Set {i:D3}: {_setFile.Sets[i]}");
            _suppress = false;

            if (SetLabels.Count > 0)
                SelectedSetIndex = Math.Min(selectIndex, SetLabels.Count - 1);
        }

        private void LoadSet(int index)
        {
            _suppress = true;
            var set = CurrentSet;
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

        private void SetFieldChanged(bool species = false)
        {
            var set = CurrentSet;
            if (set == null) return;

            set.Species = (ushort)Math.Max(0, SetSpeciesIndex);
            set.Moves[0] = (ushort)Math.Max(0, Move1);
            set.Moves[1] = (ushort)Math.Max(0, Move2);
            set.Moves[2] = (ushort)Math.Max(0, Move3);
            set.Moves[3] = (ushort)Math.Max(0, Move4);
            set.Nature = (byte)Math.Max(0, NatureIndex);
            set.Item = (ushort)Math.Max(0, ItemIndex);
            set.Form = (ushort)Form;

            byte flags = 0;
            if (EvHp) flags |= 0x01;
            if (EvAtk) flags |= 0x02;
            if (EvDef) flags |= 0x04;
            if (EvSpe) flags |= 0x08;
            if (EvSpa) flags |= 0x10;
            if (EvSpd) flags |= 0x20;
            set.EvFlags = flags;

            if (species) SpeciesIcon = _icons.Get(set.Species);

            int index = _selectedSetIndex;
            if (index >= 0 && index < SetLabels.Count)
            {
                _suppress = true;
                SetLabels[index] = $"Set {index:D3}: {set}";
                _suppress = false;
            }

            MarkDirty();
        }

        public void NewSet()
        {
            _setFile.Sets.Add(new BattleTowerPokemonSet());
            RefreshSetList(_setFile.Sets.Count - 1);
            MarkDirty();
        }

        public void SaveSets()
        {
            if (_setFile == null) return;
            _setFile.SaveToNarc();
            _isDirty = false;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            UpdateStatus();
        }

        public void ExportSets(string path) => _setFile?.ExportToFile(path);

        public void ImportSets(string path)
        {
            if (_setFile != null && _setFile.ImportFromFile(path))
            {
                RefreshSetList();
                MarkDirty();
            }
        }

        public void LocateSets()
        {
            string path = Filesystem.battleTowerPokemon;
            if (!string.IsNullOrEmpty(path) && System.IO.Directory.Exists(path))
                SystemShell.RevealInFileManager(path);
        }

        private void UpdateStatus() =>
            StatusText = $"{_trainerFile?.Trainers.Count ?? 0} trainers, {_setFile?.Sets.Count ?? 0} Pokémon sets." +
                (_isDirty ? " Unsaved changes." : "");
    }
}
