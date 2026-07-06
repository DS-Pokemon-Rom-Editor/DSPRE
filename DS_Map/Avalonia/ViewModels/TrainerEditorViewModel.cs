using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Media.Imaging;
using DSPRE.Avalonia;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>
    /// Avalonia port of the WinForms <c>TrainerEditor</c> — core scope: trainer
    /// selection + properties (name, class, AI flags, held items, battle flags) and
    /// the full 6-Pokémon party (species/form/level/moves/item/gender/ability/IV/ball
    /// seals), plus the trainer-class sprite preview (shared
    /// <see cref="TrainerClassSpriteRenderer"/>).
    ///
    /// Deferred (sub-forms): Battle Message editor, DV Calculator, Mon Reorder,
    /// Trainer Search. Simplification: the per-species "more than one gender" gate is
    /// not applied — the gender selector is editable whenever the game supports it
    /// (HGSS / AI-backport).
    /// </summary>
    public class TrainerEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private Window _owner;
        private bool _suppress;
        private TrainerFile _trainer;
        private int _loadedTrainerId = -1;

        private string[] _abilityNames = Array.Empty<string>();
        private (int abi1, int abi2)[] _abilities = Array.Empty<(int, int)>();
        private bool _abilityEditable;
        private bool _genderEditable;
        private bool _ballEnabled;
        private bool _formVisible;

        public ObservableCollection<string> TrainerNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> TrainerClassItems { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> PokemonNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> MoveNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> ItemNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<TrainerPartyMonViewModel> Party { get; } = new ObservableCollection<TrainerPartyMonViewModel>();
        public ObservableCollection<AiFlagViewModel> AiFlags { get; } = new ObservableCollection<AiFlagViewModel>();

        // Held items (4 combos)
        public ObservableCollection<TrainerItemSlotViewModel> TrainerItems { get; } = new ObservableCollection<TrainerItemSlotViewModel>();

        private static readonly string[] AiFlagLabels =
        {
            "AI 0", "Basic", "Evaluate Attack", "Expert", "Setup", "Risky",
            "Prioritize Extremes", "Baton Pass", "Tag Strategy", "Check HP", "Weather", "Harassment"
        };

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        /// <summary>Trainer id to select once the list loads (set before SetupAsync; e.g. from a "Go to Trainer #N" jump).</summary>
        public int InitialIndex { get; set; }

        // ── Trainer selection / properties ──────────────────────────────────────────
        private int _selectedTrainerIndex = -1;
        public int SelectedTrainerIndex
        {
            get => _selectedTrainerIndex;
            set { if (Set(ref _selectedTrainerIndex, value) && !_suppress && value >= 0) LoadTrainer(value); }
        }

        private string _trainerName = "";
        public string TrainerName { get => _trainerName; set { if (Set(ref _trainerName, value) && !_suppress) SetDirty(); } }

        private int _trainerClassIndex = -1;
        public int TrainerClassIndex
        {
            get => _trainerClassIndex;
            set { if (Set(ref _trainerClassIndex, value)) { if (!_suppress) SetDirty(); UpdateTrainerSprite(); } }
        }

        private bool _doubleBattle; public bool DoubleBattle { get => _doubleBattle; set { if (Set(ref _doubleBattle, value) && !_suppress) SetDirty(); } }
        private bool _chooseMoves; public bool ChooseMoves { get => _chooseMoves; set { if (Set(ref _chooseMoves, value)) { ApplyMovesEnabled(); if (!_suppress) SetDirty(); } } }
        private bool _chooseItems; public bool ChooseItems { get => _chooseItems; set { if (Set(ref _chooseItems, value)) { ApplyItemsEnabled(); if (!_suppress) SetDirty(); } } }

        private decimal _partyCount = 1;
        public decimal PartyCount
        {
            get => _partyCount;
            set { if (Set(ref _partyCount, value)) { ApplyPartyVisibility(); if (!_suppress) SetDirty(); } }
        }

        // ── Trainer-class sprite ─────────────────────────────────────────────────────
        private readonly TrainerClassSpriteRenderer _sprite = new TrainerClassSpriteRenderer();
        private Bitmap _classImage; public Bitmap ClassImage { get => _classImage; private set => Set(ref _classImage, value); }
        public bool HasClassSprite => _sprite.HasSprite;
        private decimal _classFrame; public decimal ClassFrame { get => _classFrame; set { if (Set(ref _classFrame, value)) RenderSprite(); } }
        private decimal _classFrameMax; public decimal ClassFrameMax { get => _classFrameMax; private set => Set(ref _classFrameMax, value); }

        // ── Dirty tracking ───────────────────────────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => $"Trainer Editor (Trainer {_loadedTrainerId})";
        public void SaveChanges() => Save();
        public void DiscardChanges() { _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); if (_selectedTrainerIndex >= 0) LoadTrainer(_selectedTrainerIndex); }
        // RecordUndoSnapshot runs BEFORE the _dirty short-circuit so EVERY edit is captured (not just the first).
        private void SetDirty() { if (_suppress) return; RecordUndoSnapshot(); if (_dirty) return; _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetClean() { if (!_dirty) return; _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        // ── Undo / redo (ISupportsUndo) ────────────────────────────────────────
        // Composite snapshot via the SAME serialization the proven Save/Load round-trip uses: trp bytes +
        // party bytes (synced from the VM first) + the name. Edit bursts within CoalesceMs collapse into one.
        private sealed class TrainerSnapshot { public byte[] Trp; public byte[] Party; public string Name; }
        private readonly DSPRE.Avalonia.UndoHistory<TrainerSnapshot> _history = new();
        private DateTime _lastCaptureUtc = DateTime.MinValue;
        private const int CoalesceMs = 500;

        public bool CanUndo => _history.CanUndo;
        public bool CanRedo => _history.CanRedo;
        public void Undo() { if (_history.CanUndo) ApplyState(_history.Undo()); }
        public void Redo() { if (_history.CanRedo) ApplyState(_history.Redo()); }
        private void RaiseUndoState() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private TrainerSnapshot Snapshot()
        {
            if (_trainer == null) return null;
            SyncToTrainer();
            return new TrainerSnapshot
            {
                Trp   = _trainer.trp.ToByteArray(),
                Party = _trainer.party.ToByteArray(),
                Name  = _trainerName,
            };
        }

        private void ApplyState(TrainerSnapshot snap)
        {
            if (snap == null || _trainer == null || _loadedTrainerId < 0) return;
            _suppress = true;
            _trainer = new TrainerFile(
                new TrainerProperties((ushort)_loadedTrainerId, new MemoryStream(snap.Trp)),
                new MemoryStream(snap.Party),
                snap.Name);
            PopulateFromTrainer();
            _suppress = false;

            _dirty = _history.IsDirty;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            RaiseUndoState();
        }

        private void RecordUndoSnapshot()
        {
            if (_suppress || _trainer == null) return;
            bool coalesce = (DateTime.UtcNow - _lastCaptureUtc).TotalMilliseconds < CoalesceMs;
            _history.Capture(Snapshot(), coalesce);
            _lastCaptureUtc = DateTime.UtcNow;
            RaiseUndoState();
        }

        // ── Constructors ────────────────────────────────────────────────────────────
        public TrainerEditorViewModel()
        {
            if (!Design.IsDesignMode) return;
            TrainerNames.Add("DESIGN TRAINER");
            for (int i = 0; i < AiFlagLabels.Length; i++) AiFlags.Add(new AiFlagViewModel(AiFlagLabels[i]));
        }

        public TrainerEditorViewModel(bool _) { }

        private void OnNamesChanged(object sender, System.EventArgs e)
        {
            DSPRE.Avalonia.Data.ListSync.Apply(PokemonNames, GetPokemonNames());
            DSPRE.Avalonia.Data.ListSync.Apply(MoveNames,    GetAttackNames());
            DSPRE.Avalonia.Data.ListSync.Apply(ItemNames,    GetItemNames());
            DSPRE.Avalonia.Data.ListSync.Apply(TrainerNames, new System.Collections.Generic.List<string>(DSPRE.TrainerNames.GetAll()));
        }
        /// <summary>Unsubscribes from app-wide events; call when the editor window closes.</summary>
        public void Detach() => AppEvents.NamesChanged -= OnNamesChanged;

        // ── Setup ─────────────────────────────────────────────────────────────────────
        public async Task SetupAsync(Window owner)
        {
            _owner = owner;
            StatusText = "Loading trainers…";
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> {
                    DirNames.trainerProperties, DirNames.trainerParty, DirNames.trainerGraphics,
                    DirNames.personalPokeData, DirNames.monIcons, DirNames.textArchives });
                SetMonIconsPalTableAddress();

                _genderEditable = gameFamily == GameFamilies.HGSS || AIBackportEnabled;
                _abilityEditable = _genderEditable;
                _ballEnabled = gameFamily != GameFamilies.DP;
                _formVisible = gameFamily != GameFamilies.DP;

                foreach (var n in GetPokemonNames()) PokemonNames.Add(n);
                foreach (var n in GetAttackNames()) MoveNames.Add(n);
                foreach (var n in GetItemNames()) ItemNames.Add(n);
                _abilityNames = GetAbilityNames();
                LoadAbilities();

                foreach (var n in DSPRE.TrainerNames.GetAll()) TrainerNames.Add(n);
                AppEvents.NamesChanged += OnNamesChanged;   // live-refresh names from the Text editor

                string[] classNames = GetTrainerClassNames();
                for (int i = 0; i < classNames.Length; i++) TrainerClassItems.Add($"[{i:D3}] {classNames[i]}");

                for (int i = 0; i < AiFlagLabels.Length; i++)
                {
                    var f = new AiFlagViewModel(AiFlagLabels[i]);
                    f.Changed += (s, e) => SetDirty();
                    AiFlags.Add(f);
                }

                for (int i = 0; i < TrainerProperties.TRAINER_ITEMS; i++)
                {
                    var slot = new TrainerItemSlotViewModel(ItemNames);
                    slot.Changed += (s, e) => SetDirty();
                    TrainerItems.Add(slot);
                }

                for (int i = 0; i < TrainerFile.POKE_IN_PARTY; i++)
                {
                    var mon = new TrainerPartyMonViewModel(i, PokemonNames, MoveNames, ItemNames,
                        _abilityNames, _abilities, _abilityEditable, _genderEditable, _formVisible, _ballEnabled);
                    mon.Changed += (s, e) => SetDirty();
                    Party.Add(mon);
                }

                StatusText = $"Loaded {TrainerNames.Count} trainers ({gameFamily}).";
                if (TrainerNames.Count > 0)
                    SelectedTrainerIndex = Math.Min(Math.Max(0, InitialIndex), TrainerNames.Count - 1);
            }
            catch (Exception ex)
            {
                StatusText = $"Error loading trainers: {ex.Message}";
                await DialogHelper.ShowError($"Failed to load trainers:\n{ex.Message}", "Trainer Editor Error");
            }
        }

        private void LoadAbilities()
        {
            int count = PokemonNames.Count;
            _abilities = new (int, int)[count];
            string dir = gameDirs[DirNames.personalPokeData].unpackedDir;
            for (int i = 0; i < count; i++)
            {
                try
                {
                    string path = Path.Combine(dir, i.ToString("D4"));
                    if (!File.Exists(path)) { _abilities[i] = (0, 0); continue; }
                    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                    var sp = new SpeciesFile(fs);
                    _abilities[i] = (sp.Ability1, sp.Ability2);
                }
                catch { _abilities[i] = (0, 0); }
            }
        }

        // ── Load a trainer ─────────────────────────────────────────────────────────────
        private void LoadTrainer(int index)
        {
            try
            {
                string suffix = Path.DirectorySeparatorChar + index.ToString("D4");
                string[] trNames = GetSimpleTrainerNames();
                bool error = index >= trNames.Length;

                using (var propStream = new FileStream(gameDirs[DirNames.trainerProperties].unpackedDir + suffix, FileMode.Open, FileAccess.Read))
                using (var partyStream = new FileStream(gameDirs[DirNames.trainerParty].unpackedDir + suffix, FileMode.Open, FileAccess.Read))
                {
                    _trainer = new TrainerFile(
                        new TrainerProperties((ushort)index, propStream),
                        partyStream,
                        error ? TrainerFile.NAME_NOT_FOUND : trNames[index]);
                }
                _loadedTrainerId = index;

                _suppress = true;
                try { PopulateFromTrainer(); }
                finally { _suppress = false; }

                SetClean();
                _history.Reset(Snapshot());   // loaded state is the clean undo baseline for this trainer
                _lastCaptureUtc = DateTime.MinValue;
                RaiseUndoState();
                StatusText = $"Trainer {index} loaded.";
                OnPropertyChanged(nameof(UnsavedChangesDescription));
            }
            catch (Exception ex)
            {
                _ = DialogHelper.ShowError($"Failed to load trainer {index}:\n{ex.Message}", "Trainer Editor Error");
            }
        }

        /// <summary>Pushes <see cref="_trainer"/> into the bound fields + party mons. Caller guards with _suppress.</summary>
        private void PopulateFromTrainer()
        {
            var trp = _trainer.trp;
            TrainerName = _trainer.name;
            TrainerClassIndex = trp.trainerClass;
            DoubleBattle = trp.doubleBattle;
            ChooseMoves = trp.chooseMoves;
            ChooseItems = trp.chooseItems;
            PartyCount = Math.Max(1, (int)trp.partyCount);

            for (int i = 0; i < TrainerItems.Count && i < trp.trainerItems.Length; i++)
                TrainerItems[i].ItemIndex = trp.trainerItems[i];

            for (int i = 0; i < AiFlags.Count; i++)
                AiFlags[i].Checked = trp.AI != null && i < trp.AI.Count && trp.AI[i];

            for (int i = 0; i < Party.Count; i++)
                LoadMon(Party[i], _trainer.party[i]);

            ApplyMovesEnabled();
            ApplyItemsEnabled();
            ApplyPartyVisibility();
        }

        /// <summary>Pushes the live VM fields into <see cref="_trainer"/> (shared by Save and snapshotting).</summary>
        private void SyncToTrainer()
        {
            if (_trainer == null) return;
            var trp = _trainer.trp;
            trp.partyCount = (byte)_partyCount;
            trp.chooseMoves = _chooseMoves;
            trp.chooseItems = _chooseItems;
            trp.doubleBattle = _doubleBattle;
            trp.trainerClass = (byte)Math.Max(0, _trainerClassIndex);

            for (int i = 0; i < trp.trainerItems.Length && i < TrainerItems.Count; i++)
                trp.trainerItems[i] = (ushort)Math.Max(0, TrainerItems[i].ItemIndex);

            for (int i = 0; i < AiFlags.Count && trp.AI != null && i < trp.AI.Count; i++)
                trp.AI[i] = AiFlags[i].Checked;

            for (int i = 0; i < TrainerFile.POKE_IN_PARTY; i++)
                _trainer.party[i].moves = _chooseMoves ? new ushort[4] : null;

            for (int i = 0; i < (int)_partyCount; i++)
            {
                var mon = Party[i];
                var p = _trainer.party[i];
                p.pokeID = (ushort)Math.Max(0, mon.SpeciesIndex);
                p.formID = (ushort)mon.FormId;
                p.level = (ushort)mon.Level;

                if (_chooseMoves)
                    p.moves = new ushort[] { (ushort)Math.Max(0, mon.Move1), (ushort)Math.Max(0, mon.Move2), (ushort)Math.Max(0, mon.Move3), (ushort)Math.Max(0, mon.Move4) };

                if (_chooseItems)
                    p.heldItem = (ushort)Math.Max(0, mon.ItemIndex);

                p.difficulty = (byte)mon.Difficulty;

                var flags = PartyPokemon.GenderAndAbilityFlags.NO_FLAGS;
                if (_genderEditable)
                {
                    if (mon.GenderIndex == 1) flags = PartyPokemon.GenderAndAbilityFlags.FORCE_MALE;
                    else if (mon.GenderIndex == 2) flags = PartyPokemon.GenderAndAbilityFlags.FORCE_FEMALE;
                }
                if (mon.AbilityIndex == 1) flags |= PartyPokemon.GenderAndAbilityFlags.ABILITY_SLOT1;
                else if (mon.AbilityIndex == 2) flags |= PartyPokemon.GenderAndAbilityFlags.ABILITY_SLOT2;
                p.genderAndAbilityFlags = flags;

                p.ballSeals = (ushort)mon.BallSeals;
            }
        }

        private void LoadMon(TrainerPartyMonViewModel mon, PartyPokemon p)
        {
            int gender = 0;
            if (p.genderAndAbilityFlags.HasFlag(PartyPokemon.GenderAndAbilityFlags.FORCE_MALE)) gender = 1;
            else if (p.genderAndAbilityFlags.HasFlag(PartyPokemon.GenderAndAbilityFlags.FORCE_FEMALE)) gender = 2;

            int ability = 0;
            if (p.genderAndAbilityFlags.HasFlag(PartyPokemon.GenderAndAbilityFlags.ABILITY_SLOT1)) ability = 1;
            else if (p.genderAndAbilityFlags.HasFlag(PartyPokemon.GenderAndAbilityFlags.ABILITY_SLOT2)) ability = 2;

            int[] moves = null;
            if (p.moves != null)
            {
                moves = new int[4];
                for (int j = 0; j < 4 && j < p.moves.Length; j++) moves[j] = p.moves[j];
            }

            mon.Load(p.pokeID ?? 0, p.formID, Math.Max(1, (int)p.level), moves, p.heldItem ?? 0, gender, ability, p.difficulty, p.ballSeals);
        }

        private void ApplyMovesEnabled() { foreach (var m in Party) m.MovesEnabled = _chooseMoves; }
        private void ApplyItemsEnabled() { foreach (var m in Party) m.ItemEnabled = _chooseItems; }
        private void ApplyPartyVisibility()
        {
            int count = (int)_partyCount;
            for (int i = 0; i < Party.Count; i++) Party[i].IsVisible = i < count;
        }

        // ── Sprite ──────────────────────────────────────────────────────────────────
        private void UpdateTrainerSprite()
        {
            if (_trainerClassIndex < 0) { ClassImage = null; return; }
            int maxFrame = _sprite.Load(_trainerClassIndex);
            ClassFrameMax = maxFrame;
            OnPropertyChanged(nameof(HasClassSprite));
            if (_classFrame > maxFrame) { _classFrame = 0; OnPropertyChanged(nameof(ClassFrame)); }
            RenderSprite();
        }
        private void RenderSprite() => ClassImage = _sprite.Render((int)_classFrame, 96, 96);

        // ── Save ──────────────────────────────────────────────────────────────────────
        public void Save()
        {
            if (_trainer == null || _selectedTrainerIndex < 0) return;
            SyncToTrainer();

            string indexStr = Path.DirectorySeparatorChar + _selectedTrainerIndex.ToString("D4");
            File.WriteAllBytes(gameDirs[DirNames.trainerProperties].unpackedDir + indexStr, _trainer.trp.ToByteArray());
            File.WriteAllBytes(gameDirs[DirNames.trainerParty].unpackedDir + indexStr, _trainer.party.ToByteArray());

            UpdateTrainerName(_trainerName);

            SetClean();
            _history.MarkSaved();
            RaiseUndoState();
            StatusText = $"Trainer {_selectedTrainerIndex} saved.";
        }

        // ── Mon reorder support ──────────────────────────────────────────────────────
        public IEnumerable<(int index, string display)> GetPartyForReorder()
        {
            int count = (int)_partyCount;
            for (int i = 0; i < count && i < Party.Count; i++)
            {
                var m = Party[i];
                string name = m.SpeciesIndex >= 0 && m.SpeciesIndex < PokemonNames.Count ? PokemonNames[m.SpeciesIndex] : "?";
                yield return (i, $"[{i}] {name} Lv. {(int)m.Level}");
            }
        }

        public void ReorderParty(List<int> newOrder)
        {
            int count = newOrder.Count;
            if (count == 0) return;
            var snap = new List<(int sp, int form, int lvl, int[] moves, int item, int gen, int ab, int diff, int ball)>();
            for (int i = 0; i < count && i < Party.Count; i++)
            {
                var m = Party[i];
                snap.Add((m.SpeciesIndex, (int)m.FormId, (int)m.Level,
                    new[] { m.Move1, m.Move2, m.Move3, m.Move4 }, m.ItemIndex,
                    m.GenderIndex, m.AbilityIndex, (int)m.Difficulty, (int)m.BallSeals));
            }
            for (int i = 0; i < count && i < Party.Count; i++)
            {
                int from = newOrder[i];
                if (from < 0 || from >= snap.Count) continue;
                var s = snap[from];
                Party[i].Load(s.sp, s.form, s.lvl, s.moves, s.item, s.gen, s.ab, s.diff, s.ball);
            }
            SetDirty();
        }

        public void GoToTrainer(int index)
        {
            if (index >= 0 && index < TrainerNames.Count) SelectedTrainerIndex = index;
        }

        // ── DV Calculator support ─────────────────────────────────────────────────────
        public (ushort trainerId, byte trainerClass, List<(int pokeId, int level, int gender, int ability, int dv)> party) GetDVCalcInput()
        {
            var list = new List<(int, int, int, int, int)>();
            int count = (int)_partyCount;
            for (int i = 0; i < count && i < Party.Count; i++)
            {
                var m = Party[i];
                list.Add((m.SpeciesIndex, (int)m.Level, m.GenderIndex, m.AbilityIndex, (int)m.Difficulty));
            }
            ushort tid = _trainer?.trp.trainerID ?? (ushort)Math.Max(0, _selectedTrainerIndex);
            return (tid, (byte)Math.Max(0, _trainerClassIndex), list);
        }

        public void ApplyDVCalc(IReadOnlyList<(int dv, int gender, int ability)> results)
        {
            for (int i = 0; i < results.Count && i < Party.Count; i++)
            {
                var m = Party[i];
                m.Difficulty = results[i].dv;
                m.GenderIndex = results[i].gender;
                m.AbilityIndex = results[i].ability;
            }
            SetDirty();
        }

        private void UpdateTrainerName(string newName)
        {
            try
            {
                _trainer.name = newName;
                var ta = new TextArchive(trainerNamesMessageNumber);
                ta.SetSimpleTrainerName(_trainer.trp.trainerID, newName);
                ta.SaveToExpandedDir(trainerNamesMessageNumber, showSuccessMessage: false);
            }
            catch (Exception ex) { AppLogger.Error("Trainer name save failed: " + ex.Message); }
        }
    }

    // ── Small sub-VMs ──────────────────────────────────────────────────────────────
    public class AiFlagViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler Changed;
        public string Label { get; }
        private bool _checked;
        public bool Checked
        {
            get => _checked;
            set { if (_checked != value) { _checked = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Checked))); Changed?.Invoke(this, EventArgs.Empty); } }
        }
        public AiFlagViewModel(string label) { Label = label; }
    }

    public class TrainerItemSlotViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler Changed;
        public ObservableCollection<string> ItemNames { get; }
        private int _itemIndex = -1;
        public int ItemIndex
        {
            get => _itemIndex;
            set { if (_itemIndex != value) { _itemIndex = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ItemIndex))); Changed?.Invoke(this, EventArgs.Empty); } }
        }
        public TrainerItemSlotViewModel(ObservableCollection<string> itemNames) { ItemNames = itemNames; }
    }
}
