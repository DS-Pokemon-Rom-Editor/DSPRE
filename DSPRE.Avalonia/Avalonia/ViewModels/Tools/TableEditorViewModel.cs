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
    public class TableEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
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

        public bool NoTablesAvailable => !ShowConditionalMusic && !ShowEffectsCombos && !ShowVsTables;

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
        public decimal VsAnimation { get => _vsAnimation; set { if (Set(ref _vsAnimation, value)) MarkEffectsDirty(); } }

        private decimal _battleSseq;
        public decimal BattleSseq { get => _battleSseq; set { if (Set(ref _battleSseq, value)) MarkEffectsDirty(); } }

        // ── VS Trainer selection/detail ───────────────────────────────────────────
        private int _vsTrainerSelectedIndex = -1;
        public int VsTrainerSelectedIndex
        {
            get => _vsTrainerSelectedIndex;
            set { if (Set(ref _vsTrainerSelectedIndex, value) && value >= 0) LoadVsTrainerEntry(value); }
        }

        private int _trainerClassIndex = -1;
        public int TrainerClassIndex { get => _trainerClassIndex; set { if (Set(ref _trainerClassIndex, value)) { MarkVsTrainerDirty(); UpdateTrainerSprite(); } } }

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
        public int TrainerComboIndex { get => _trainerComboIndex; set { if (Set(ref _trainerComboIndex, value)) MarkVsTrainerDirty(); } }

        // ── VS Pokémon selection/detail (display only, original Save is a no-op) ──
        private int _vsPokemonSelectedIndex = -1;
        public int VsPokemonSelectedIndex
        {
            get => _vsPokemonSelectedIndex;
            set { if (Set(ref _vsPokemonSelectedIndex, value) && value >= 0) LoadVsPokemonEntry(value); }
        }

        private int _pokemonIndex = -1;
        public int PokemonIndex { get => _pokemonIndex; set => Set(ref _pokemonIndex, value); }

        private int _pokemonComboIndex = -1;
        public int PokemonComboIndex { get => _pokemonComboIndex; set => Set(ref _pokemonComboIndex, value); }

        // ── Dirty tracking ────────────────────────────────────────────────────────
        private bool _condDirty, _effectsDirty, _vsTrainerDirty;
        public bool HasUnsavedChanges => _condDirty || _effectsDirty || _vsTrainerDirty;
        public string UnsavedChangesDescription => "Table Editor";
        public void SaveChanges()
        {
            if (_condDirty) SaveConditionalMusic();
            if (_effectsDirty) SaveEffectCombo();
            if (_vsTrainerDirty) SaveVsTrainer();
        }
        public void DiscardChanges()
        {
            _condDirty = _effectsDirty = _vsTrainerDirty = false;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
        private void MarkDirty(ref bool flag) { flag = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }

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
            var friendly = HeaderLabels.Friendly();
            var given = headerNames?.ToArray() ?? Array.Empty<string>();
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
                StatusText = $"Tables loaded ({gameFamily}).";
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
            foreach (var h in _headerNames) HeaderNames.Add(h);

            SetConditionalMusicTableOffsetToRAMAddress();
            _condMusicTable = new List<(ushort, ushort, ushort)>();
            _condMusicStartAddr = BitConverter.ToUInt32(ARM9.ReadBytes(conditionalMusicTableOffsetToRAMAddress, 4), 0) - ARM9.address;
            byte count = ARM9.ReadByte(conditionalMusicTableOffsetToRAMAddress - 8);

            CondMusicItems.Clear();
            using (var ar = new ARM9.Reader(_condMusicStartAddr))
            {
                for (int i = 0; i < count; i++)
                {
                    ushort header = ar.ReadUInt16();
                    ushort flag = ar.ReadUInt16();
                    ushort music = ar.ReadUInt16();
                    _condMusicTable.Add((header, flag, music));
                    CondMusicItems.Add(HeaderNameAt(header));
                }
            }
            ShowConditionalMusic = true;
        }

        private string HeaderNameAt(int index) =>
            index >= 0 && index < _headerNames.Length ? _headerNames[index] : $"Header {index}";

        // ── Battle Effects setup ───────────────────────────────────────────────────
        private void SetupBattleEffects()
        {
            if (gameFamily != GameFamilies.HGSS && gameFamily != GameFamilies.Plat)
            {
                ShowEffectsCombos = false;
                ShowVsTables = false;
                return;
            }

            DSUtils.TryUnpackNarcs(new List<DirNames> {
                DirNames.trainerGraphics, DirNames.textArchives, DirNames.monIcons });
            SetBattleEffectsData();
            SetMonIconsPalTableAddress();

            _effectsComboTable = new List<(ushort, ushort)>();
            _effectsComboStartAddr = BitConverter.ToUInt32(ARM9.ReadBytes(effectsComboTableOffsetToRAMAddress, 4), 0);
            RomPatchState.flag_MainComboTableRepointed = _effectsComboStartAddr >= synthOverlayLoadAddress;
            _effectsComboStartAddr -= RomPatchState.flag_MainComboTableRepointed ? synthOverlayLoadAddress : ARM9.address;

            byte comboCount;
            string expArmPath = Filesystem.expArmPath;

            if (gameFamily == GameFamilies.HGSS)
            {
                comboCount = ARM9.ReadByte(effectsComboTableOffsetToSizeLimiter);

                _vsPokemonList = new List<(int, int)>();
                _vsTrainerList = new List<(int, int)>();

                _vsPokemonStartAddr = BitConverter.ToUInt32(ARM9.ReadBytes(vsPokemonEntryTableOffsetToRAMAddress, 4), 0);
                RomPatchState.flag_PokemonBattleTableRepointed = _vsPokemonStartAddr >= synthOverlayLoadAddress;
                _vsPokemonStartAddr -= RomPatchState.flag_PokemonBattleTableRepointed ? synthOverlayLoadAddress : ARM9.address;

                _vsTrainerStartAddr = BitConverter.ToUInt32(ARM9.ReadBytes(vsTrainerEntryTableOffsetToRAMAddress, 4), 0);
                RomPatchState.flag_TrainerClassBattleTableRepointed = _vsTrainerStartAddr >= synthOverlayLoadAddress;
                _vsTrainerStartAddr -= RomPatchState.flag_TrainerClassBattleTableRepointed ? synthOverlayLoadAddress : ARM9.address;

                _pokeNames = GetPokemonNames();
                PokemonNames.Clear();
                for (int i = 0; i < _pokeNames.Length; i++) PokemonNames.Add($"[{i}] {_pokeNames[i]}");

                _trcNames = GetTrainerClassNames();
                TrainerNames.Clear();
                for (int i = 0; i < _trcNames.Length; i++) TrainerNames.Add($"[{i:D3}] {_trcNames[i]}");

                VsTrainerItems.Clear();
                VsPokemonItems.Clear();
            }
            else
            {
                comboCount = 35;
            }

            // Main combo table.
            ComboItems.Clear();
            using (var ar = new DSUtils.EasyReader(RomPatchState.flag_MainComboTableRepointed ? expArmPath : arm9Path, _effectsComboStartAddr))
            {
                for (int i = 0; i < comboCount; i++)
                {
                    ushort effect = ar.ReadUInt16();
                    ushort music = ar.ReadUInt16();
                    _effectsComboTable.Add((effect, music));
                    ComboItems.Add($"Combo {i:D2} - Effect #{effect}, Music #{music}");
                }
            }

            if (gameFamily == GameFamilies.HGSS)
            {
                // VS Trainer table.
                using (var ar = new DSUtils.EasyReader(RomPatchState.flag_TrainerClassBattleTableRepointed ? expArmPath : arm9Path, _vsTrainerStartAddr))
                {
                    byte trainerCount = ARM9.ReadByte(vsTrainerEntryTableOffsetToSizeLimiter);
                    for (int i = 0; i < trainerCount; i++)
                    {
                        ushort entry = ar.ReadUInt16();
                        int classID = entry & 1023;
                        int comboID = entry >> 10;
                        _vsTrainerList.Add((classID, comboID));
                        VsTrainerItems.Add($"{TrainerLabel(classID)} uses Combo #{comboID}");
                    }
                }

                // VS Pokémon table.
                using (var ar = new DSUtils.EasyReader(RomPatchState.flag_PokemonBattleTableRepointed ? expArmPath : arm9Path, _vsPokemonStartAddr))
                {
                    byte pokeCount = ARM9.ReadByte(vsPokemonEntryTableOffsetToSizeLimiter);
                    for (int i = 0; i < pokeCount; i++)
                    {
                        ushort entry = ar.ReadUInt16();
                        int pokeID = entry & 1023;
                        int comboID = entry >> 10;
                        _vsPokemonList.Add((pokeID, comboID));
                        string name = pokeID >= 0 && pokeID < _pokeNames.Length ? _pokeNames[pokeID] : "UNKNOWN";
                        VsPokemonItems.Add($"[{pokeID:D3}] {name} uses Combo #{comboID}");
                    }
                }
                ShowVsTables = true;
            }
            else
            {
                ShowVsTables = false;
            }

            ShowEffectsCombos = true;
        }

        private uint _vsPokemonStartAddr;

        private string TrainerLabel(int classID) =>
            classID >= 0 && classID < _trcNames.Length ? $"[{classID:D3}] {_trcNames[classID]}" : $"[{classID:D3}] ?";

        // ── Conditional Music handlers ─────────────────────────────────────────────
        private void LoadCondEntry(int index)
        {
            if (_condMusicTable == null || index < 0 || index >= _condMusicTable.Count) return;
            _suppress = true;
            try
            {
                var e = _condMusicTable[index];
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
            var cur = _condMusicTable[_condSelectedIndex];
            _condMusicTable[_condSelectedIndex] = (header ?? cur.header, flag ?? cur.flag, music ?? cur.music);
            MarkDirty(ref _condDirty);
        }

        public void SaveConditionalMusic()
        {
            if (_condMusicTable == null) return;
            for (int i = 0; i < _condMusicTable.Count; i++)
            {
                ARM9.WriteBytes(BitConverter.GetBytes(_condMusicTable[i].header), (uint)(_condMusicStartAddr + 6 * i));
                ARM9.WriteBytes(BitConverter.GetBytes(_condMusicTable[i].flag), (uint)(_condMusicStartAddr + 6 * i + 2));
                ARM9.WriteBytes(BitConverter.GetBytes(_condMusicTable[i].music), (uint)(_condMusicStartAddr + 6 * i + 4));
            }
            _condDirty = false;
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
                var e = _effectsComboTable[index];
                VsAnimation = e.vsGraph;
                BattleSseq = e.battleSSEQ;
            }
            finally { _suppress = false; }
        }

        public void SaveEffectCombo()
        {
            int index = _comboSelectedIndex;
            if (_effectsComboTable == null || index < 0 || index >= _effectsComboTable.Count) return;

            ushort effect = (ushort)VsAnimation;
            ushort music = (ushort)BattleSseq;
            _effectsComboTable[index] = (effect, music);

            string expArmPath = Filesystem.expArmPath;
            using (var wr = new DSUtils.EasyWriter(RomPatchState.flag_MainComboTableRepointed ? expArmPath : arm9Path, _effectsComboStartAddr + 4 * (uint)index))
            {
                wr.Write(effect);
                wr.Write(music);
            }
            _effectsDirty = false;
            OnPropertyChanged(nameof(HasUnsavedChanges));

            string updated = $"Combo {index:D2} - Effect #{effect}, Music #{music}";
            _suppress = true;
            ComboItems[index] = updated;
            _suppress = false;
            StatusText = "Effect combo saved.";
        }

        // Mark the combo detail dirty when the user edits the numerics.
        public void MarkEffectsDirty() { if (!_suppress) MarkDirty(ref _effectsDirty); }

        // ── VS Trainer handlers ────────────────────────────────────────────────────
        private void LoadVsTrainerEntry(int index)
        {
            if (_vsTrainerList == null || index < 0 || index >= _vsTrainerList.Count) return;
            _suppress = true;
            try
            {
                var e = _vsTrainerList[index];
                TrainerClassIndex = e.trainerClass;
                TrainerComboIndex = e.comboID;
            }
            finally { _suppress = false; }
        }

        public void SaveVsTrainer()
        {
            int index = _vsTrainerSelectedIndex;
            if (_vsTrainerList == null || index < 0 || index >= _vsTrainerList.Count) return;

            ushort trainerClass = (ushort)Math.Max(0, _trainerClassIndex);
            ushort comboID = (ushort)Math.Max(0, _trainerComboIndex);
            _vsTrainerList[index] = (trainerClass, comboID);

            string expArmPath = Filesystem.expArmPath;
            using (var wr = new DSUtils.EasyWriter(RomPatchState.flag_TrainerClassBattleTableRepointed ? expArmPath : arm9Path, _vsTrainerStartAddr + 2 * (uint)index))
            {
                wr.Write((ushort)((trainerClass & 1023) + (comboID << 10)));
            }
            _vsTrainerDirty = false;
            OnPropertyChanged(nameof(HasUnsavedChanges));

            _suppress = true;
            VsTrainerItems[index] = $"{TrainerLabel(trainerClass)} uses Combo #{comboID}";
            _suppress = false;
            StatusText = "VS Trainer entry saved.";
        }

        public void MarkVsTrainerDirty() { if (!_suppress) MarkDirty(ref _vsTrainerDirty); }

        // ── VS Pokémon handlers (display only) ─────────────────────────────────────
        private void LoadVsPokemonEntry(int index)
        {
            if (_vsPokemonList == null || index < 0 || index >= _vsPokemonList.Count) return;
            _suppress = true;
            try
            {
                var e = _vsPokemonList[index];
                PokemonIndex = e.pokemonID >= 0 && e.pokemonID < PokemonNames.Count ? e.pokemonID : 0;
                PokemonComboIndex = e.comboID;
            }
            finally { _suppress = false; }
        }

        // ── Info dialogs ───────────────────────────────────────────────────────────
        public Task ShowConditionalMusicHelp() => DialogHelper.ShowInfo(
            "For each Location in the list, override Header's music with chosen Music ID, if Flag is set.",
            "How this table works");

        public Task ShowEffectsComboHelp() => DialogHelper.ShowInfo(
            "An entry of this table is a combination of VS. Graphics + Battle Theme.\n\n" +
            (gameFamily == GameFamilies.HGSS ? "Each entry can be \"inherited\" by one or more Pokémon or Trainer classes." : ""),
            "How this table works");

        public Task ShowVsTrainerHelp() => DialogHelper.ShowInfo(
            "Each entry of this table links a Trainer Class to an Effect Combo from the Combos Table.\n\n" +
            "Every Trainer Class with a given combo will start the same VS. Sequence and Battle Theme.",
            "How this table works");

        public Task ShowVsPokemonHelp() => DialogHelper.ShowInfo(
            "Each entry of this table links a \"Wild\" Pokémon to an Effect Combo from the Combos Table.\n\n" +
            "Whenever that Pokémon is encountered in the tall grass or via script command, its VS. Sequence and Battle Theme will be automatically triggered.",
            "How this table works");
    }
}
