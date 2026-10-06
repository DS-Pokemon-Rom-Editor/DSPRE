using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Media.Imaging;
using DSPRE.Avalonia;
using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.Resources;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Tools
{
    /// <summary>
    /// Avalonia port of the WinForms <c>TableEditor</c> (data only: the animated
    /// trainer-class sprite preview is intentionally omitted; it will return once
    /// the Trainer Editor is ported and its sprite renderer can be reused).
    ///
    /// Edits three/four ARM9-backed tables, gated by game family:
    ///   • Conditional Music table        (HGSS)
    ///   • Battle Effects Combo table      (HGSS + Plat)
    ///   • VS Trainer / VS Pokémon tables  (HGSS)
    /// </summary>
    public class TableEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        // ── Suppress handlers during population (mirrors Helpers.DisableHandlers) ──
        private bool _suppress;

        // ── Backing tables ───────────────────────────────────────────────────────
        private List<(ushort header, ushort flag, ushort music)> _condMusicTable;
        private uint _condMusicStartAddr;

        private List<(ushort vsGraph, ushort battleSSEQ)> _effectsComboTable;
        private uint _effectsComboStartAddr;

        private List<(int trainerClass, int comboID)> _vsTrainerList;
        private uint _vsTrainerStartAddr;

        private List<(int pokemonID, int comboID)> _vsPokemonList;

        private string[] _headerNames = Array.Empty<string>();
        private string[] _pokeNames = Array.Empty<string>();
        private string[] _trcNames = Array.Empty<string>();

        // ── Section visibility (set during setup by game family) ──────────────────
        private bool _showConditionalMusic;
        public bool ShowConditionalMusic { get => _showConditionalMusic; private set => Set(ref _showConditionalMusic, value); }

        private bool _showEffectsCombos;
        public bool ShowEffectsCombos { get => _showEffectsCombos; private set => Set(ref _showEffectsCombos, value); }

        private bool _showVsTables;
        public bool ShowVsTables { get => _showVsTables; private set => Set(ref _showVsTables, value); }

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        // The intro editors own the combo, class and species tables; they stay here only where those editors can't open the ROM.
        private bool _showIntroLinks;
        public bool ShowIntroLinks { get => _showIntroLinks; private set => Set(ref _showIntroLinks, value); }

        public bool NoTablesAvailable => !ShowConditionalMusic && !ShowEffectsCombos && !ShowVsTables && !ShowIntroLinks;

        // ── List/combo sources ────────────────────────────────────────────────────
        public ObservableCollection<string> HeaderNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> CondMusicItems { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> ComboItems { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> TrainerNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> PokemonNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> VsTrainerItems { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> VsPokemonItems { get; } = new ObservableCollection<string>();

        // ── Conditional Music selection/detail ────────────────────────────────────
        private int _condSelectedIndex = -1;
        public int CondSelectedIndex
        {
            get => _condSelectedIndex;
            set { if (Set(ref _condSelectedIndex, value) && value >= 0) LoadCondEntry(value); }
        }

        private int _condHeaderIndex = -1;
        public int CondHeaderIndex
        {
            get => _condHeaderIndex;
            set { if (Set(ref _condHeaderIndex, value) && !_suppress && value >= 0) OnCondHeaderChanged(value); }
        }

        private decimal _condFlag;
        public decimal CondFlag
        {
            get => _condFlag;
            set { if (Set(ref _condFlag, value) && !_suppress) UpdateCondTuple(flag: (ushort)value); }
        }

        private decimal _condMusic;
        public decimal CondMusic
        {
            get => _condMusic;
            set { if (Set(ref _condMusic, value)) { SyncMusicName(); if (!_suppress) UpdateCondTuple(music: (ushort)value); } }
        }


        // The song names the header editor already shows, so a track is pickable here by name too.
        public MappedCombo MusicNames { get; } = new MappedCombo();

        private int _condMusicComboIndex = -1;
        public int CondMusicComboIndex
        {
            get => _condMusicComboIndex;
            set { if (Set(ref _condMusicComboIndex, value) && !_suppress && value >= 0) CondMusic = MusicNames.KeyAt(value); }
        }

        private void SyncMusicName()
        {
            _condMusicComboIndex = MusicNames.IndexOf((int)_condMusic);
            OnPropertyChanged(nameof(CondMusicComboIndex));
        }

        // ── Effects Combo selection/detail ────────────────────────────────────────
        private int _comboSelectedIndex = -1;
        public int ComboSelectedIndex
        {
            get => _comboSelectedIndex;
            set { if (Set(ref _comboSelectedIndex, value) && value >= 0) LoadComboEntry(value); }
        }

        private decimal _vsAnimation;
        public decimal VsAnimation { get => _vsAnimation; set { if (Set(ref _vsAnimation, value)) OnComboEdited(); } }

        private decimal _battleSseq;
        public decimal BattleSseq { get => _battleSseq; set { if (Set(ref _battleSseq, value)) OnComboEdited(); } }

        // ── VS Trainer selection/detail ───────────────────────────────────────────
        private int _vsTrainerSelectedIndex = -1;
        public int VsTrainerSelectedIndex
        {
            get => _vsTrainerSelectedIndex;
            set { if (Set(ref _vsTrainerSelectedIndex, value) && value >= 0) LoadVsTrainerEntry(value); }
        }

        private int _trainerClassIndex = -1;
        public int TrainerClassIndex { get => _trainerClassIndex; set { if (Set(ref _trainerClassIndex, value)) { OnVsTrainerEdited(); UpdateTrainerSprite(); } } }

        // ── Trainer-class sprite preview (restored via the shared renderer) ──────────
        private readonly TrainerClassSpriteRenderer _trainerSprite = new TrainerClassSpriteRenderer();

        private Bitmap _trainerSpriteImage;
        public Bitmap TrainerSpriteImage { get => _trainerSpriteImage; private set => Set(ref _trainerSpriteImage, value); }

        public bool HasTrainerSprite => _trainerSprite.HasSprite;

        private decimal _trainerSpriteFrame;
        public decimal TrainerSpriteFrame
        {
            get => _trainerSpriteFrame;
            set { if (Set(ref _trainerSpriteFrame, value)) RenderTrainerSprite(); }
        }

        private decimal _trainerSpriteFrameMax;
        public decimal TrainerSpriteFrameMax { get => _trainerSpriteFrameMax; private set => Set(ref _trainerSpriteFrameMax, value); }

        private void UpdateTrainerSprite()
        {
            if (_trainerClassIndex < 0) { TrainerSpriteImage = null; return; }
            int maxFrame = _trainerSprite.Load(_trainerClassIndex);
            TrainerSpriteFrameMax = maxFrame;
            OnPropertyChanged(nameof(HasTrainerSprite));
            if (_trainerSpriteFrame > maxFrame) { _trainerSpriteFrame = 0; OnPropertyChanged(nameof(TrainerSpriteFrame)); }
            RenderTrainerSprite();
        }

        private void RenderTrainerSprite()
        {
            TrainerSpriteImage = _trainerSprite.Render((int)_trainerSpriteFrame, 80, 80);
        }

        private int _trainerComboIndex = -1;
        public int TrainerComboIndex { get => _trainerComboIndex; set { if (Set(ref _trainerComboIndex, value)) OnVsTrainerEdited(); } }

        // ── VS Pokémon selection/detail (display only, original Save is a no-op) ──
        private int _vsPokemonSelectedIndex = -1;
        public int VsPokemonSelectedIndex
        {
            get => _vsPokemonSelectedIndex;
            set { if (Set(ref _vsPokemonSelectedIndex, value) && value >= 0) LoadVsPokemonEntry(value); }
        }

        private int _pokemonIndex = -1;
        public int PokemonIndex { get => _pokemonIndex; set { if (Set(ref _pokemonIndex, value)) OnVsPokemonEdited(); } }

        private int _pokemonComboIndex = -1;
        public int PokemonComboIndex { get => _pokemonComboIndex; set { if (Set(ref _pokemonComboIndex, value)) OnVsPokemonEdited(); } }

        // ── Dirty tracking ────────────────────────────────────────────────────────
        private bool _condDirty, _effectsDirty, _vsTrainerDirty, _vsPokemonDirty;
        // Rows edited since the last save, so one Save writes every changed row, not just the shown one.
        private readonly HashSet<int> _dirtyCombos = new HashSet<int>();
        private readonly HashSet<int> _dirtyVsTrainers = new HashSet<int>();
        private readonly HashSet<int> _dirtyVsPokemon = new HashSet<int>();
        public bool HasUnsavedChanges => _condDirty || _effectsDirty || _vsTrainerDirty || _vsPokemonDirty;
        public string UnsavedChangesDescription => "Table Editor";
        public void SaveChanges() => _ = SaveAllAsync();

        async Task<bool> IEditorWithUnsavedChanges.SaveChangesAsync()
        {
            await SaveAllAsync();
            return !HasUnsavedChanges;
        }

        public async Task SaveAllAsync()
        {
            if (!HasUnsavedChanges) return;
            int cond = _condSelectedIndex, combo = _comboSelectedIndex, vs = _vsTrainerSelectedIndex, poke = _vsPokemonSelectedIndex;
            bool toSource = _fromSource && (_effectsDirty || _vsTrainerDirty || _vsPokemonDirty);
            if (_condDirty) SaveConditionalMusic();
            if (_effectsDirty) await SaveEffectCombosAsync();
            if (_vsTrainerDirty) await SaveVsTrainersAsync();
            if (_vsPokemonDirty) await SaveVsPokemonAsync();
            Reselect(cond, combo, vs, poke);
            if (!HasUnsavedChanges)
            {
                _undo?.MarkSaved();
                SaveNotice.Saved(UnsavedChangesDescription);
                StatusText = toSource
                    ? $"Saved to {HgEngineMusicTables.SourceRelPath}. Compile the ROM to apply it."
                    : "Tables saved.";
            }
        }

        /// <summary>Re-reads every table and keeps the shown rows.</summary>
        public void DiscardChanges()
        {
            if (HasUnsavedChanges)
            {
                int cond = _condSelectedIndex, combo = _comboSelectedIndex, vs = _vsTrainerSelectedIndex, poke = _vsPokemonSelectedIndex;
                _suppress = true;
                try
                {
                    SetupConditionalMusic();
                    SetupBattleEffects();
                }
                catch (Exception ex) { StatusText = $"Error loading tables: {ex.Message}"; }
                finally { _suppress = false; }
                Reselect(cond, combo, vs, poke);
            }
            _condDirty = _effectsDirty = _vsTrainerDirty = _vsPokemonDirty = false;
            _dirtyCombos.Clear();
            _dirtyVsTrainers.Clear();
            _dirtyVsPokemon.Clear();
            ResetUndo();
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        // Rewriting a list's items can drop the list's selection; put it back and refresh the fields.
        private void Reselect(int cond, int combo, int vsTrainer, int vsPokemon)
        {
            _suppress = true;
            try
            {
                if (_condSelectedIndex != cond) { _condSelectedIndex = cond; OnPropertyChanged(nameof(CondSelectedIndex)); }
                if (_comboSelectedIndex != combo) { _comboSelectedIndex = combo; OnPropertyChanged(nameof(ComboSelectedIndex)); }
                if (_vsTrainerSelectedIndex != vsTrainer) { _vsTrainerSelectedIndex = vsTrainer; OnPropertyChanged(nameof(VsTrainerSelectedIndex)); }
                if (_vsPokemonSelectedIndex != vsPokemon) { _vsPokemonSelectedIndex = vsPokemon; OnPropertyChanged(nameof(VsPokemonSelectedIndex)); }
            }
            finally { _suppress = false; }
            LoadCondEntry(cond);
            LoadComboEntry(combo);
            LoadVsTrainerEntry(vsTrainer);
            LoadVsPokemonEntry(vsPokemon);
        }

        private void MarkDirty(ref bool flag) { flag = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        // ── Undo / redo: all three tables ────────────────────────────────────────
        private sealed record TablesState(int[][] Cond, int[][] Combos, int[][] VsTrainers, int[][] VsPokemon);
        private DSPRE.Avalonia.ByteStateUndo _undo;
        private TablesState _saved;
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();
        private void RaiseUndo() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private TablesState Current() => new TablesState(
            _condMusicTable?.Select(r => new int[] { r.header, r.flag, r.music }).ToArray() ?? Array.Empty<int[]>(),
            _effectsComboTable?.Select(r => new int[] { r.vsGraph, r.battleSSEQ }).ToArray() ?? Array.Empty<int[]>(),
            _vsTrainerList?.Select(r => new int[] { r.trainerClass, r.comboID }).ToArray() ?? Array.Empty<int[]>(),
            _vsPokemonList?.Select(r => new int[] { r.pokemonID, r.comboID }).ToArray() ?? Array.Empty<int[]>());

        private void ResetUndo()
        {
            _saved = Current();
            _undo = new DSPRE.Avalonia.ByteStateUndo(() => DSPRE.Avalonia.UndoJson.Take(Current()), ApplyState, RaiseUndo);
            RaiseUndo();
        }

        private void ApplyState(byte[] state)
        {
            TablesState s = DSPRE.Avalonia.UndoJson.Read<TablesState>(state);
            for (int i = 0; _condMusicTable != null && i < s.Cond.Length && i < _condMusicTable.Count; i++)
                _condMusicTable[i] = ((ushort)s.Cond[i][0], (ushort)s.Cond[i][1], (ushort)s.Cond[i][2]);
            for (int i = 0; _effectsComboTable != null && i < s.Combos.Length && i < _effectsComboTable.Count; i++)
                _effectsComboTable[i] = ((ushort)s.Combos[i][0], (ushort)s.Combos[i][1]);
            for (int i = 0; _vsTrainerList != null && i < s.VsTrainers.Length && i < _vsTrainerList.Count; i++)
                _vsTrainerList[i] = (s.VsTrainers[i][0], s.VsTrainers[i][1]);
            for (int i = 0; _vsPokemonList != null && s.VsPokemon != null && i < s.VsPokemon.Length && i < _vsPokemonList.Count; i++)
                _vsPokemonList[i] = (s.VsPokemon[i][0], s.VsPokemon[i][1]);
            RecountDirty();
            int cond = _condSelectedIndex, combo = _comboSelectedIndex, vs = _vsTrainerSelectedIndex, poke = _vsPokemonSelectedIndex;
            _suppress = true;
            try
            {
                for (int i = 0; _condMusicTable != null && i < _condMusicTable.Count && i < CondMusicItems.Count; i++)
                    if (CondMusicItems[i] != HeaderNameAt(_condMusicTable[i].header)) CondMusicItems[i] = HeaderNameAt(_condMusicTable[i].header);
                for (int i = 0; _effectsComboTable != null && i < _effectsComboTable.Count && i < ComboItems.Count; i++)
                {
                    string label = $"Combo {i:D2} - Effect #{_effectsComboTable[i].vsGraph}, Music #{_effectsComboTable[i].battleSSEQ}";
                    if (ComboItems[i] != label) ComboItems[i] = label;
                }
                for (int i = 0; _vsTrainerList != null && i < _vsTrainerList.Count && i < VsTrainerItems.Count; i++)
                {
                    string label = $"{TrainerLabel(_vsTrainerList[i].trainerClass)} uses Combo #{_vsTrainerList[i].comboID}";
                    if (VsTrainerItems[i] != label) VsTrainerItems[i] = label;
                }
                for (int i = 0; _vsPokemonList != null && i < _vsPokemonList.Count && i < VsPokemonItems.Count; i++)
                    if (VsPokemonItems[i] != PokemonRowLabel(i)) VsPokemonItems[i] = PokemonRowLabel(i);
            }
            finally { _suppress = false; }
            Reselect(cond, combo, vs, poke);
        }

        // Rows count as changed while they differ from what was last read or saved, so undoing back clears them.
        private void RecountDirty()
        {
            TablesState now = Current();
            _condDirty = !Same(now.Cond, _saved.Cond);
            _dirtyCombos.Clear();
            for (int i = 0; i < now.Combos.Length; i++)
                if (i >= _saved.Combos.Length || !now.Combos[i].SequenceEqual(_saved.Combos[i])) _dirtyCombos.Add(i);
            _effectsDirty = _dirtyCombos.Count > 0;
            _dirtyVsTrainers.Clear();
            for (int i = 0; i < now.VsTrainers.Length; i++)
                if (i >= _saved.VsTrainers.Length || !now.VsTrainers[i].SequenceEqual(_saved.VsTrainers[i])) _dirtyVsTrainers.Add(i);
            _vsTrainerDirty = _dirtyVsTrainers.Count > 0;
            _dirtyVsPokemon.Clear();
            for (int i = 0; i < now.VsPokemon.Length; i++)
                if (i >= _saved.VsPokemon.Length || !now.VsPokemon[i].SequenceEqual(_saved.VsPokemon[i])) _dirtyVsPokemon.Add(i);
            _vsPokemonDirty = _dirtyVsPokemon.Count > 0;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private static bool Same(int[][] a, int[][] b) => a.Length == b.Length && a.Zip(b).All(p => p.First.SequenceEqual(p.Second));

        private void Edited()
        {
            if (_undo == null) return;
            _undo.Record();
            RecountDirty();
        }

        // ── Constructors ──────────────────────────────────────────────────────────
        public TableEditorViewModel()
        {
            if (!Design.IsDesignMode) return;
            ShowEffectsCombos = true;
            ComboItems.Add("Combo 00 - Effect #1, Music #2");
            StatusText = "Design mode";
        }

        public TableEditorViewModel(IEnumerable<string> headerNames)
        {
            // One label for a header across the whole app, place name and all.
            IReadOnlyList<string> friendly = HeaderLabels.Friendly();
            string[] given = headerNames?.ToArray() ?? Array.Empty<string>();
            _headerNames = friendly.Count == given.Length ? friendly.ToArray() : given;
        }

        // ── Setup ─────────────────────────────────────────────────────────────────
        public async Task SetupAsync()
        {
            StatusText = "Loading tables…";
            try
            {
                _suppress = true;
                MusicNames.Load(gameFamily switch
                {
                    GameFamilies.DP => PokeDatabase.MusicDB.DPMusicDict,
                    GameFamilies.Plat => PokeDatabase.MusicDB.PtMusicDict,
                    _ => PokeDatabase.MusicDB.HGSSMusicDict,
                });
                SetupConditionalMusic();
                SetupBattleEffects();
                _suppress = false;

                if (ShowConditionalMusic && CondMusicItems.Count > 0) CondSelectedIndex = 0;
                if (ShowVsTables && VsTrainerItems.Count > 0) VsTrainerSelectedIndex = 0;
                if (ShowVsTables && VsPokemonItems.Count > 0) VsPokemonSelectedIndex = 0;
                if (ShowEffectsCombos && ComboItems.Count > 0) ComboSelectedIndex = 0;

                OnPropertyChanged(nameof(NoTablesAvailable));
                ResetUndo();
                StatusText = _tablesNote ?? $"Tables loaded ({gameFamily}).";
            }
            catch (Exception ex)
            {
                _suppress = false;
                StatusText = $"Error loading tables: {ex.Message}";
                await DialogHelper.ShowError($"Failed to load tables:\n{ex.Message}", "Table Editor Error");
            }
        }

        // ── Conditional Music setup ────────────────────────────────────────────────
        private void SetupConditionalMusic()
        {
            if (gameFamily != GameFamilies.HGSS)
            {
                ShowConditionalMusic = false;
                return;
            }

            // Header names for the combo / entry labels.
            HeaderNames.Clear();
            foreach (string h in _headerNames) HeaderNames.Add(h);

            (uint start, List<ConditionalMusicTable.Row> rows) = ConditionalMusicTable.Read();
            _condMusicStartAddr = start;
            _condMusicTable = new List<(ushort, ushort, ushort)>();
            CondMusicItems.Clear();
            foreach (ConditionalMusicTable.Row row in rows)
            {
                _condMusicTable.Add((row.Header, row.Flag, row.Music));
                CondMusicItems.Add(HeaderNameAt(row.Header));
            }
            ShowConditionalMusic = true;
        }

        private string HeaderNameAt(int index) =>
            index >= 0 && index < _headerNames.Length ? _headerNames[index] : $"Header {index}";

        // ── Battle Effects setup ───────────────────────────────────────────────────
        private void SetupBattleEffects()
        {
            if (!BattleMusicTables.IsSupported)
            {
                ShowEffectsCombos = false;
                ShowVsTables = false;
                return;
            }

            DSUtils.TryUnpackNarcs(new List<DirNames> {
                DirNames.trainerGraphics, DirNames.textArchives, DirNames.monIcons });
            SetMonIconsPalTableAddress();

            // Writes go back to wherever the tables were read from.
            _fromSource = gameFamily == GameFamilies.HGSS && HgEngineMusicTables.TablesInSource;
            // Only hand the tables over where the intro editors are enabled and can open this ROM; elsewhere they stay editable here.
            ShowIntroLinks = !_fromSource && !isHGE && DSPRE.ROMFiles.VsIntroTables.WhyNot() == null
                && BetaEditors.Allows("VsIntroEditorView") && BetaEditors.Allows("WildIntroEditorView");
            if (ShowIntroLinks)
            {
                ShowEffectsCombos = false;
                ShowVsTables = false;
                return;
            }
            BattleMusicTables tables = _fromSource ? HgEngineMusicTables.ReadBattle() : BattleMusicTables.LoadRom();
            if (tables == null)
            {
                ShowEffectsCombos = false;
                ShowVsTables = false;
                _tablesNote = isHGE ? "Link the hg-engine checkout to edit these tables." : null;
                return;
            }
            _battleTables = tables;
            _effectsComboTable = tables.Combos.Rows;
            _effectsComboStartAddr = tables.Combos.Start;
            RomPatchState.flag_MainComboTableRepointed = tables.Combos.Repointed;

            ComboItems.Clear();
            for (int i = 0; i < _effectsComboTable.Count; i++)
                ComboItems.Add($"Combo {i:D2} - Effect #{_effectsComboTable[i].vsGraph}, Music #{_effectsComboTable[i].battleSSEQ}");

            if (gameFamily == GameFamilies.HGSS)
            {
                _vsTrainerList = tables.Classes.Rows;
                _vsTrainerStartAddr = tables.Classes.Start;
                RomPatchState.flag_TrainerClassBattleTableRepointed = tables.Classes.Repointed;
                _vsPokemonList = tables.Species.Rows;
                _vsPokemonStartAddr = tables.Species.Start;
                RomPatchState.flag_PokemonBattleTableRepointed = tables.Species.Repointed;

                _pokeNames = GetPokemonNames();
                PokemonNames.Clear();
                for (int i = 0; i < _pokeNames.Length; i++) PokemonNames.Add($"[{i}] {_pokeNames[i]}");

                _trcNames = GetTrainerClassNames();
                TrainerNames.Clear();
                for (int i = 0; i < _trcNames.Length; i++) TrainerNames.Add($"[{i:D3}] {_trcNames[i]}");

                VsTrainerItems.Clear();
                foreach ((int classID, int comboID) in _vsTrainerList)
                    VsTrainerItems.Add($"{TrainerLabel(classID)} uses Combo #{comboID}");

                VsPokemonItems.Clear();
                for (int i = 0; i < _vsPokemonList.Count; i++) VsPokemonItems.Add(PokemonRowLabel(i));
                OnPropertyChanged(nameof(CanAddVsPokemonRows));
                ShowVsTables = true;
            }
            else
            {
                ShowVsTables = false;
            }

            ShowEffectsCombos = true;
        }

        private uint _vsPokemonStartAddr;
        private BattleMusicTables _battleTables;
        private bool _fromSource;
        private string _tablesNote;

        private string TrainerLabel(int classID) =>
            classID >= 0 && classID < _trcNames.Length ? $"[{classID:D3}] {_trcNames[classID]}" : $"[{classID:D3}] ?";

        // ── Conditional Music handlers ─────────────────────────────────────────────
        private void LoadCondEntry(int index)
        {
            if (_condMusicTable == null || index < 0 || index >= _condMusicTable.Count) return;
            _suppress = true;
            try
            {
                (ushort header, ushort flag, ushort music) e = _condMusicTable[index];
                CondHeaderIndex = e.header;
                CondFlag = e.flag;
                CondMusic = e.music;
            }
            finally { _suppress = false; }
        }

        private void OnCondHeaderChanged(int headerIndex) => UpdateCondTuple(header: (ushort)headerIndex);

        private void UpdateCondTuple(ushort? header = null, ushort? flag = null, ushort? music = null)
        {
            if (_condMusicTable == null || _condSelectedIndex < 0 || _condSelectedIndex >= _condMusicTable.Count) return;
            (ushort header, ushort flag, ushort music) cur = _condMusicTable[_condSelectedIndex];
            _condMusicTable[_condSelectedIndex] = (header ?? cur.header, flag ?? cur.flag, music ?? cur.music);
            MarkDirty(ref _condDirty);
            Edited();
        }

        public void SaveConditionalMusic()
        {
            if (_condMusicTable == null) return;
            ConditionalMusicTable.Write(_condMusicStartAddr, _condMusicTable
                .Select(r => new ConditionalMusicTable.Row { Header = r.header, Flag = r.flag, Music = r.music }).ToList());
            _condDirty = false;
            if (_saved != null) _saved = _saved with { Cond = Current().Cond };
            OnPropertyChanged(nameof(HasUnsavedChanges));
            StatusText = "Conditional music table saved.";
        }

        // ── Effects Combo handlers ─────────────────────────────────────────────────
        private void LoadComboEntry(int index)
        {
            if (_effectsComboTable == null || index < 0 || index >= _effectsComboTable.Count) return;
            _suppress = true;
            try
            {
                (ushort vsGraph, ushort battleSSEQ) e = _effectsComboTable[index];
                VsAnimation = e.vsGraph;
                BattleSseq = e.battleSSEQ;
            }
            finally { _suppress = false; }
        }

        private async Task SaveEffectCombosAsync()
        {
            if (_effectsComboTable == null) return;
            List<int> rows = _dirtyCombos.Where(i => i >= 0 && i < _effectsComboTable.Count).OrderBy(i => i).ToList();

            if (_fromSource)
            {
                (bool saved, string error) = await HgEngineSave.RunAsync(() =>
                {
                    foreach (int i in rows)
                        if (!HgEngineMusicTables.TrySetCombo(i, _effectsComboTable[i].vsGraph, _effectsComboTable[i].battleSSEQ, out string e)) return e;
                    return null;
                });
                if (!saved) { if (error != null) StatusText = error; return; }
            }
            else
            {
                foreach (int i in rows) _battleTables.WriteCombo(i);
            }
            _dirtyCombos.Clear();
            _effectsDirty = false;
            if (_saved != null) _saved = _saved with { Combos = Current().Combos };
            OnPropertyChanged(nameof(HasUnsavedChanges));

            _suppress = true;
            try
            {
                foreach (int i in rows)
                    ComboItems[i] = $"Combo {i:D2} - Effect #{_effectsComboTable[i].vsGraph}, Music #{_effectsComboTable[i].battleSSEQ}";
            }
            finally { _suppress = false; }
        }

        // An edited combo goes straight into the table so switching rows keeps it.
        private void OnComboEdited()
        {
            int index = _comboSelectedIndex;
            if (_suppress || _effectsComboTable == null || index < 0 || index >= _effectsComboTable.Count) return;
            _effectsComboTable[index] = ((ushort)VsAnimation, (ushort)BattleSseq);
            _dirtyCombos.Add(index);
            MarkDirty(ref _effectsDirty);
            Edited();
        }

        // ── VS Trainer handlers ────────────────────────────────────────────────────
        private void LoadVsTrainerEntry(int index)
        {
            if (_vsTrainerList == null || index < 0 || index >= _vsTrainerList.Count) return;
            _suppress = true;
            try
            {
                (int trainerClass, int comboID) e = _vsTrainerList[index];
                TrainerClassIndex = e.trainerClass;
                TrainerComboIndex = e.comboID;
            }
            finally { _suppress = false; }
        }

        private async Task SaveVsTrainersAsync()
        {
            if (_vsTrainerList == null) return;
            List<int> rows = _dirtyVsTrainers.Where(i => i >= 0 && i < _vsTrainerList.Count).OrderBy(i => i).ToList();

            if (_fromSource)
            {
                (bool saved, string error) = await HgEngineSave.RunAsync(() =>
                {
                    foreach (int i in rows)
                        if (!HgEngineMusicTables.TrySetClassCombo(i, _vsTrainerList[i].trainerClass, _vsTrainerList[i].comboID, out string e)) return e;
                    return null;
                });
                if (!saved) { if (error != null) StatusText = error; return; }
            }
            else
            {
                foreach (int i in rows) _battleTables.WriteClass(i);
            }
            _dirtyVsTrainers.Clear();
            _vsTrainerDirty = false;
            if (_saved != null) _saved = _saved with { VsTrainers = Current().VsTrainers };
            OnPropertyChanged(nameof(HasUnsavedChanges));

            _suppress = true;
            try
            {
                foreach (int i in rows)
                    VsTrainerItems[i] = $"{TrainerLabel(_vsTrainerList[i].trainerClass)} uses Combo #{_vsTrainerList[i].comboID}";
            }
            finally { _suppress = false; }
        }

        // An edited row goes straight into the table so switching rows keeps it.
        private void OnVsTrainerEdited()
        {
            int index = _vsTrainerSelectedIndex;
            if (_suppress || _vsTrainerList == null || index < 0 || index >= _vsTrainerList.Count) return;
            _vsTrainerList[index] = ((ushort)Math.Max(0, _trainerClassIndex), (ushort)Math.Max(0, _trainerComboIndex));
            _dirtyVsTrainers.Add(index);
            MarkDirty(ref _vsTrainerDirty);
            Edited();
        }

        // ── VS Pokémon handlers ────────────────────────────────────────────────────
        private string PokemonRowLabel(int row)
        {
            (int pokeID, int comboID) = _vsPokemonList[row];
            string name = pokeID >= 0 && pokeID < _pokeNames.Length ? _pokeNames[pokeID] : "UNKNOWN";
            return $"[{pokeID:D3}] {name} uses Combo #{comboID}";
        }

        // An edited row goes straight into the table so switching rows keeps it.
        private void OnVsPokemonEdited()
        {
            int index = _vsPokemonSelectedIndex;
            if (_suppress || _vsPokemonList == null || index < 0 || index >= _vsPokemonList.Count) return;
            _vsPokemonList[index] = (Math.Max(0, _pokemonIndex), Math.Max(0, _pokemonComboIndex));
            _dirtyVsPokemon.Add(index);
            MarkDirty(ref _vsPokemonDirty);
            _suppress = true;
            try { VsPokemonItems[index] = PokemonRowLabel(index); }
            finally { _suppress = false; }
            Edited();
        }

        private async Task SaveVsPokemonAsync()
        {
            if (_vsPokemonList == null) return;
            List<int> rows = _dirtyVsPokemon.Where(i => i >= 0 && i < _vsPokemonList.Count).OrderBy(i => i).ToList();
            // Rows store the species in 10 bits.
            int tooBig = rows.FirstOrDefault(i => _vsPokemonList[i].pokemonID > 0x3FF, -1);
            if (tooBig >= 0) { StatusText = $"Row {tooBig}: species above 1023 can't have their own battle music."; return; }
            if (_fromSource)
            {
                (bool saved, string error) = await HgEngineSave.RunAsync(() =>
                {
                    foreach (int i in rows)
                        if (!HgEngineMusicTables.TrySetSpeciesCombo(i, _vsPokemonList[i].pokemonID, _vsPokemonList[i].comboID, out string e)) return e;
                    return null;
                });
                if (!saved) { if (error != null) StatusText = error; return; }
            }
            else
            {
                foreach (int i in rows) _battleTables.WriteSpecies(i);
            }
            _dirtyVsPokemon.Clear();
            _vsPokemonDirty = false;
            if (_saved != null) _saved = _saved with { VsPokemon = Current().VsPokemon };
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        /// <summary>hg-engine builds this table from source and DSPRE keeps its search count in step, so rows can come and go.</summary>
        public bool CanAddVsPokemonRows => _fromSource && _vsPokemonList != null;

        public async Task AddVsPokemonRowAsync()
        {
            if (!CanAddVsPokemonRows) return;
            if (HasUnsavedChanges) { StatusText = "Save or discard your changes first."; return; }
            int species = Math.Max(0, _pokemonIndex), combo = Math.Max(0, _pokemonComboIndex);
            (bool saved, string error) = await HgEngineSave.RunAsync(() => HgEngineMusicTables.TryAddSpeciesRow(species, combo, out string e) ? null : e);
            if (!saved) { if (error != null) StatusText = error; return; }
            ReloadVsPokemon(_vsPokemonList.Count);
            StatusText = $"Row added to {HgEngineMusicTables.SourceRelPath}. Compile the ROM to apply it.";
        }

        public async Task RemoveVsPokemonRowAsync()
        {
            int row = _vsPokemonSelectedIndex;
            if (!CanAddVsPokemonRows || row < 0 || row >= _vsPokemonList.Count) return;
            if (HasUnsavedChanges) { StatusText = "Save or discard your changes first."; return; }
            (bool saved, string error) = await HgEngineSave.RunAsync(() => HgEngineMusicTables.TryRemoveSpeciesRow(row, out string e) ? null : e);
            if (!saved) { if (error != null) StatusText = error; return; }
            ReloadVsPokemon(Math.Min(row, _vsPokemonList.Count - 2));
            StatusText = $"Row removed from {HgEngineMusicTables.SourceRelPath}. Compile the ROM to apply it.";
        }

        private void ReloadVsPokemon(int select)
        {
            int cond = _condSelectedIndex, combo = _comboSelectedIndex, vs = _vsTrainerSelectedIndex;
            _suppress = true;
            try { SetupBattleEffects(); }
            finally { _suppress = false; }
            ResetUndo();
            Reselect(cond, combo, vs, Math.Min(select, _vsPokemonList.Count - 1));
        }
        private void LoadVsPokemonEntry(int index)
        {
            if (_vsPokemonList == null || index < 0 || index >= _vsPokemonList.Count) return;
            _suppress = true;
            try
            {
                (int pokemonID, int comboID) e = _vsPokemonList[index];
                PokemonIndex = e.pokemonID >= 0 && e.pokemonID < PokemonNames.Count ? e.pokemonID : 0;
                PokemonComboIndex = e.comboID;
            }
            finally { _suppress = false; }
        }

        // ── Info dialogs ───────────────────────────────────────────────────────────
    }
}
